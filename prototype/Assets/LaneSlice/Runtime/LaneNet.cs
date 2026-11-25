using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Moba.LaneSlice
{
    public interface ILaneLink : IDisposable
    {
        bool IsHost { get; }
        bool IsConnected { get; }
        string StatusText { get; }
        string JoinCode { get; }
        int PingMs { get; }

        event Action<LaneHeroId> RemotePickChanged;
        event Action<LaneHeroId, LaneHeroId, LaneTuning> StartReceived;
        event Action<LaneOrder> OrderReceived;
        event Action<LaneSnapshot> SnapshotReceived;
        event Action<string> Disconnected;

        void Tick();
        void SendPick(LaneHeroId hero);
        void SendStart(LaneHeroId hostHero, LaneHeroId guestHero, LaneTuning tuning);
        void SendOrder(LaneOrder order);
        void SendSnapshot(LaneSnapshot snap);
    }

    public static class LaneWireCodec
    {
        public const int MaxFrameBytes = 65536;
        public const string HelloBanner = "MOBA-LANE/1";

        public enum MsgType : byte
        {
            Pick = 1,
            Start = 2,
            Order = 3,
            Snapshot = 4,
            Ping = 5,
            Pong = 6,
        }

        [Serializable]
        private struct PickPayload
        {
            public int hero;
        }

        [Serializable]
        private sealed class StartPayload
        {
            public int hostHero;
            public int guestHero;
            public LaneTuning tuning;
        }

        [Serializable]
        private struct PingPayload
        {
            public long ticks;
        }

        public static byte[] EncodePick(LaneHeroId hero) =>
            Encode(MsgType.Pick, JsonUtility.ToJson(new PickPayload { hero = (int)hero }));

        public static byte[] EncodeStart(LaneHeroId hostHero, LaneHeroId guestHero, LaneTuning tuning) =>
            Encode(MsgType.Start, JsonUtility.ToJson(new StartPayload
            {
                hostHero = (int)hostHero,
                guestHero = (int)guestHero,
                tuning = tuning,
            }));

        public static byte[] EncodeOrder(LaneOrder order) =>
            Encode(MsgType.Order, JsonUtility.ToJson(order));

        public static byte[] EncodeSnapshot(LaneSnapshot snap) =>
            Encode(MsgType.Snapshot, JsonUtility.ToJson(snap));

        public static byte[] EncodePing(long ticks) =>
            Encode(MsgType.Ping, JsonUtility.ToJson(new PingPayload { ticks = ticks }));

        public static byte[] EncodePong(long ticks) =>
            Encode(MsgType.Pong, JsonUtility.ToJson(new PingPayload { ticks = ticks }));

        private static byte[] Encode(MsgType type, string json)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(json);
            if (utf8.Length + 1 > MaxFrameBytes)
            {
                throw new InvalidOperationException($"Lane frame exceeded {MaxFrameBytes} bytes ({utf8.Length + 1}).");
            }
            var packet = new byte[utf8.Length + 1];
            packet[0] = (byte)type;
            Buffer.BlockCopy(utf8, 0, packet, 1, utf8.Length);
            return packet;
        }

        public static void Dispatch(
            byte[] packet,
            Action<byte[]> sendReply,
            Action<LaneHeroId> onPick,
            Action<LaneHeroId, LaneHeroId, LaneTuning> onStart,
            Action<LaneOrder> onOrder,
            Action<LaneSnapshot> onSnapshot,
            Action<int> onPingMeasured)
        {
            if (packet == null || packet.Length < 2) return;
            var type = (MsgType)packet[0];
            string json = Encoding.UTF8.GetString(packet, 1, packet.Length - 1);

            switch (type)
            {
                case MsgType.Pick:
                    PickPayload p = JsonUtility.FromJson<PickPayload>(json);
                    onPick?.Invoke((LaneHeroId)p.hero);
                    break;
                case MsgType.Start:
                    StartPayload s = JsonUtility.FromJson<StartPayload>(json);
                    onStart?.Invoke((LaneHeroId)s.hostHero, (LaneHeroId)s.guestHero, s.tuning ?? new LaneTuning());
                    break;
                case MsgType.Order:
                    onOrder?.Invoke(JsonUtility.FromJson<LaneOrder>(json));
                    break;
                case MsgType.Snapshot:
                    onSnapshot?.Invoke(JsonUtility.FromJson<LaneSnapshot>(json));
                    break;
                case MsgType.Ping:
                    PingPayload ping = JsonUtility.FromJson<PingPayload>(json);
                    sendReply?.Invoke(EncodePong(ping.ticks));
                    break;
                case MsgType.Pong:
                    PingPayload pong = JsonUtility.FromJson<PingPayload>(json);
                    long rttMs = Math.Max(0L, (DateTime.UtcNow.Ticks - pong.ticks) / TimeSpan.TicksPerMillisecond);
                    onPingMeasured?.Invoke((int)Math.Min(9999L, rttMs));
                    break;
            }
        }
    }

    /// <summary>
    /// Direct TCP fallback transport using explicit host/port and 4-byte length-prefixed JSON packets.
    /// Supports 0.0.0.0 bind, async DNS resolution, tcp://host:port normalization, and reconnect cleanup.
    /// </summary>
    public sealed class TcpLaneLink : ILaneLink
    {
        private readonly object gate = new object();
        private readonly Queue<byte[]> incoming = new Queue<byte[]>();
        private TcpListener listener;
        private TcpClient client;
        private NetworkStream stream;
        private Thread worker;
        private volatile bool running = true;
        private volatile bool connected;
        private string statusText;
        private float pingAccum;
        private int pingMs;
        private string disconnectReason;

        public bool IsHost { get; }
        public bool IsConnected => connected;
        public string JoinCode => string.Empty;
        public int PingMs => pingMs;

        public string StatusText
        {
            get { lock (gate) return statusText; }
            private set { lock (gate) statusText = value; }
        }

        public event Action<LaneHeroId> RemotePickChanged;
        public event Action<LaneHeroId, LaneHeroId, LaneTuning> StartReceived;
        public event Action<LaneOrder> OrderReceived;
        public event Action<LaneSnapshot> SnapshotReceived;
        public event Action<string> Disconnected;

        private TcpLaneLink(bool isHost, string initialStatus)
        {
            IsHost = isHost;
            statusText = initialStatus;
        }

        public static TcpLaneLink Host(string bindHost, int port)
        {
            IPAddress ip = IPAddress.Any;
            if (!string.IsNullOrWhiteSpace(bindHost) && bindHost.Trim() != "0.0.0.0")
            {
                IPAddress.TryParse(bindHost.Trim(), out ip);
            }

            var link = new TcpLaneLink(true, $"Listening on {ip}:{port}...");
            link.listener = new TcpListener(ip, port);
            link.listener.Start();
            link.worker = new Thread(link.RunHostLoop) { IsBackground = true, Name = "TcpLaneHost" };
            link.worker.Start();
            return link;
        }

        public static TcpLaneLink Connect(string addressOrHost, int defaultPort = 47777)
        {
            ParseEndpoint(addressOrHost, defaultPort, out string host, out int port);
            var link = new TcpLaneLink(false, $"Connecting to {host}:{port}...");
            link.worker = new Thread(() => link.RunClientLoop(host, port)) { IsBackground = true, Name = "TcpLaneClient" };
            link.worker.Start();
            return link;
        }

        public static void ParseEndpoint(string raw, int defaultPort, out string host, out int port)
        {
            host = "127.0.0.1";
            port = defaultPort;
            if (string.IsNullOrWhiteSpace(raw)) return;

            string s = raw.Trim();
            if (s.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(6);
            int slash = s.IndexOf('/');
            if (slash >= 0) s = s.Substring(0, slash);

            int colon = s.LastIndexOf(':');
            if (colon > 0 && int.TryParse(s.Substring(colon + 1), out int parsedPort) && parsedPort > 0 && parsedPort <= 65535)
            {
                port = parsedPort;
                s = s.Substring(0, colon);
            }
            if (!string.IsNullOrWhiteSpace(s)) host = s.Trim();
        }

        private void RunHostLoop()
        {
            try
            {
                TcpClient accepted = listener.AcceptTcpClient();
                if (!running)
                {
                    accepted.Close();
                    return;
                }
                ConfigureSocket(accepted);
                client = accepted;
                stream = accepted.GetStream();
                SendRawHello();
                ReadRawHello();
                connected = true;
                StatusText = "TCP peer connected.";
                ReadLoop();
            }
            catch (Exception ex)
            {
                if (running)
                {
                    disconnectReason = $"TCP Host closed: {ex.Message}";
                    StatusText = disconnectReason;
                }
            }
            finally
            {
                connected = false;
            }
        }

        private void RunClientLoop(string host, int port)
        {
            try
            {
                IPAddress ip;
                if (!IPAddress.TryParse(host, out ip))
                {
                    IPAddress[] resolved = Dns.GetHostAddressesAsync(host).GetAwaiter().GetResult();
                    ip = null;
                    for (int i = 0; i < resolved.Length; i++)
                    {
                        if (resolved[i].AddressFamily == AddressFamily.InterNetwork)
                        {
                            ip = resolved[i];
                            break;
                        }
                    }
                    if (ip == null && resolved.Length > 0) ip = resolved[0];
                    if (ip == null) throw new InvalidOperationException($"Could not resolve {host}");
                }

                var cli = new TcpClient();
                cli.ConnectAsync(ip, port).GetAwaiter().GetResult();
                if (!running)
                {
                    cli.Close();
                    return;
                }
                ConfigureSocket(cli);
                client = cli;
                stream = cli.GetStream();
                SendRawHello();
                ReadRawHello();
                connected = true;
                StatusText = $"Connected to {host}:{port}";
                ReadLoop();
            }
            catch (Exception ex)
            {
                if (running)
                {
                    disconnectReason = $"TCP Connect failed: {ex.Message}";
                    StatusText = disconnectReason;
                }
            }
            finally
            {
                connected = false;
            }
        }

        private static void ConfigureSocket(TcpClient cli)
        {
            cli.NoDelay = true;
            cli.SendTimeout = 5000;
        }

        private void SendRawHello()
        {
            byte[] banner = Encoding.ASCII.GetBytes(LaneWireCodec.HelloBanner);
            lock (gate)
            {
                stream.Write(banner, 0, banner.Length);
                stream.Flush();
            }
        }

        private void ReadRawHello()
        {
            byte[] expected = Encoding.ASCII.GetBytes(LaneWireCodec.HelloBanner);
            byte[] buf = new byte[expected.Length];
            ReadExact(stream, buf, expected.Length);
            string got = Encoding.ASCII.GetString(buf);
            if (got != LaneWireCodec.HelloBanner)
            {
                throw new InvalidDataException($"Bad TCP banner '{got}'");
            }
        }

        private void ReadLoop()
        {
            var lenBuf = new byte[4];
            while (running && stream != null)
            {
                ReadExact(stream, lenBuf, 4);
                int len = BitConverter.ToInt32(lenBuf, 0);
                if (len <= 0 || len > LaneWireCodec.MaxFrameBytes)
                {
                    throw new InvalidDataException($"Invalid frame length {len}");
                }
                var payload = new byte[len];
                ReadExact(stream, payload, len);
                lock (incoming) incoming.Enqueue(payload);
            }
        }

        private static void ReadExact(NetworkStream s, byte[] buf, int len)
        {
            int offset = 0;
            while (offset < len)
            {
                int read = s.Read(buf, offset, len - offset);
                if (read <= 0) throw new EndOfStreamException("Peer disconnected.");
                offset += read;
            }
        }

        private void SendPacket(byte[] packet)
        {
            if (!connected || stream == null) return;
            try
            {
                byte[] lenBuf = BitConverter.GetBytes(packet.Length);
                lock (gate)
                {
                    stream.Write(lenBuf, 0, 4);
                    stream.Write(packet, 0, packet.Length);
                }
            }
            catch (Exception ex)
            {
                disconnectReason = $"TCP send error: {ex.Message}";
                connected = false;
            }
        }

        public void Tick()
        {
            if (!string.IsNullOrEmpty(disconnectReason))
            {
                string reason = disconnectReason;
                disconnectReason = null;
                Disconnected?.Invoke(reason);
            }

            if (connected)
            {
                pingAccum += Time.unscaledDeltaTime;
                if (pingAccum >= 1.0f)
                {
                    pingAccum = 0f;
                    SendPacket(LaneWireCodec.EncodePing(DateTime.UtcNow.Ticks));
                }
            }

            while (true)
            {
                byte[] pkt;
                lock (incoming)
                {
                    if (incoming.Count == 0) break;
                    pkt = incoming.Dequeue();
                }

                LaneWireCodec.Dispatch(
                    pkt,
                    SendPacket,
                    p => RemotePickChanged?.Invoke(p),
                    (h, g, t) => StartReceived?.Invoke(h, g, t),
                    o => OrderReceived?.Invoke(o),
                    s => SnapshotReceived?.Invoke(s),
                    rtt => pingMs = rtt);
            }
        }

        public void SendPick(LaneHeroId hero) => SendPacket(LaneWireCodec.EncodePick(hero));
        public void SendStart(LaneHeroId hostHero, LaneHeroId guestHero, LaneTuning tuning) => SendPacket(LaneWireCodec.EncodeStart(hostHero, guestHero, tuning));
        public void SendOrder(LaneOrder order) => SendPacket(LaneWireCodec.EncodeOrder(order));
        public void SendSnapshot(LaneSnapshot snap) => SendPacket(LaneWireCodec.EncodeSnapshot(snap));

        public void Dispose()
        {
            running = false;
            connected = false;
            try { listener?.Stop(); } catch { }
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
        }
    }

    /// <summary>
    /// NAT-traversal online multiplayer transport over Unity Relay + Unity Transport (DTLS/UTP).
    /// Requires Unity Cloud Project ID linked in Project Settings -> Services to create/join Room Codes.
    /// </summary>
    public sealed class RelayLaneLink : ILaneLink
    {
        private readonly object gate = new object();
        private readonly Queue<byte[]> outboundControl = new Queue<byte[]>();
        private byte[] pendingSnapshot;

        private NetworkDriver driver;
        private NetworkPipeline pipeline;
        private NetworkConnection connection;
        private bool driverCreated;
        private bool isConnected;
        private bool disposed;
        private string statusText = "Initializing Unity Services...";
        private string joinCode = string.Empty;
        private int pingMs;
        private float pingAccum;
        private string disconnectError;

        public bool IsHost { get; }
        public bool IsConnected => isConnected;
        public string StatusText
        {
            get { lock (gate) return statusText; }
            private set { lock (gate) statusText = value; }
        }
        public string JoinCode
        {
            get { lock (gate) return joinCode; }
            private set { lock (gate) joinCode = value; }
        }
        public int PingMs => pingMs;

        public event Action<LaneHeroId> RemotePickChanged;
        public event Action<LaneHeroId, LaneHeroId, LaneTuning> StartReceived;
        public event Action<LaneOrder> OrderReceived;
        public event Action<LaneSnapshot> SnapshotReceived;
        public event Action<string> Disconnected;

        private RelayLaneLink(bool isHost)
        {
            IsHost = isHost;
        }

        public static RelayLaneLink HostRelay()
        {
            var link = new RelayLaneLink(true);
            _ = link.InitHostAsync();
            return link;
        }

        public static RelayLaneLink JoinRelay(string code)
        {
            var link = new RelayLaneLink(false);
            _ = link.InitClientAsync(code);
            return link;
        }

        private async Task<bool> EnsureUnityServicesSignedInAsync()
        {
            if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
            {
                string msg = "Unity Cloud Project ID is unlinked. Link a free Unity Project ID in Editor (Project Settings -> Services) to use Relay Room Codes, or use Direct TCP.";
                StatusText = msg;
                disconnectError = msg;
                return false;
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var opts = new InitializationOptions();
                opts.SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 12));
                await UnityServices.InitializeAsync(opts);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            return true;
        }

        private async Task InitHostAsync()
        {
            try
            {
                StatusText = "Connecting to Unity Services...";
                if (!await EnsureUnityServicesSignedInAsync() || disposed) return;

                StatusText = "Creating Relay Room...";
                Allocation alloc = await RelayService.Instance.CreateAllocationAsync(1);
                if (disposed) return;

                string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                if (disposed) return;

                JoinCode = code;
                RelayServerData serverData = BuildServerData(
                    alloc.ServerEndpoints,
                    alloc.AllocationIdBytes,
                    alloc.ConnectionData,
                    alloc.ConnectionData,
                    alloc.Key);

                SetupDriver(ref serverData, listen: true);
                StatusText = $"Relay Room ready! Code: {code} (waiting for friend...)";
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    disconnectError = $"Relay host error: {ex.Message}";
                    StatusText = disconnectError;
                }
            }
        }

        private async Task InitClientAsync(string rawCode)
        {
            try
            {
                string code = (rawCode ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(code))
                {
                    disconnectError = "Enter a 6-character Relay Room Code first.";
                    StatusText = disconnectError;
                    return;
                }

                StatusText = $"Joining Relay Room {code}...";
                if (!await EnsureUnityServicesSignedInAsync() || disposed) return;

                JoinAllocation join = await RelayService.Instance.JoinAllocationAsync(code);
                if (disposed) return;

                JoinCode = code;
                RelayServerData serverData = BuildServerData(
                    join.ServerEndpoints,
                    join.AllocationIdBytes,
                    join.ConnectionData,
                    join.HostConnectionData,
                    join.Key);

                SetupDriver(ref serverData, listen: false);
                StatusText = $"Relay allocated, connecting to host ({code})...";
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    disconnectError = $"Relay join error: {ex.Message}";
                    StatusText = disconnectError;
                }
            }
        }

        private static RelayServerData BuildServerData(
            List<RelayServerEndpoint> endpoints,
            byte[] allocIdBytes,
            byte[] connData,
            byte[] hostConnData,
            byte[] hmacKey)
        {
            RelayServerEndpoint chosen = null;
            for (int i = 0; i < endpoints.Count; i++)
            {
                if (endpoints[i].ConnectionType == RelayServerEndpoint.ConnectionTypeDtls)
                {
                    chosen = endpoints[i];
                    break;
                }
            }
            if (chosen == null && endpoints.Count > 0) chosen = endpoints[0];
            if (chosen == null) throw new InvalidOperationException("No Relay server endpoint returned.");

            return new RelayServerData(
                chosen.Host,
                (ushort)chosen.Port,
                allocIdBytes,
                connData,
                hostConnData,
                hmacKey,
                chosen.Secure,
                chosen.ConnectionType == RelayServerEndpoint.ConnectionTypeWss);
        }

        private void SetupDriver(ref RelayServerData serverData, bool listen)
        {
            var settings = new NetworkSettings(Allocator.Persistent);
            try
            {
                settings.WithRelayParameters(ref serverData);
                settings.WithFragmentationStageParameters(payloadCapacity: LaneWireCodec.MaxFrameBytes);
                settings.WithReliableStageParameters(windowSize: 64);

                driver = NetworkDriver.Create(settings);
            }
            finally
            {
                settings.Dispose();
            }

            pipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));

            if (listen)
            {
                NetworkEndpoint bindEp = serverData.Endpoint.Family == NetworkFamily.Ipv6
                    ? NetworkEndpoint.AnyIpv6
                    : NetworkEndpoint.AnyIpv4;
                if (driver.Bind(bindEp) != 0)
                {
                    throw new InvalidOperationException("Transport failed to bind host Relay endpoint.");
                }
                if (driver.Listen() != 0)
                {
                    throw new InvalidOperationException("Transport failed to listen on Relay.");
                }
            }
            else
            {
                // Let NetworkDriver.EnsureBindFromEndpoint bind the matching IPv4/IPv6 wildcard for serverData.Endpoint
                connection = driver.Connect();
            }

            driverCreated = true;
        }

        private void SendPacket(byte[] packet)
        {
            if (disposed || !driverCreated || !isConnected || !connection.IsCreated) return;
            lock (gate)
            {
                if (packet.Length > 0 && packet[0] == (byte)LaneWireCodec.MsgType.Snapshot)
                {
                    pendingSnapshot = packet;
                }
                else if (outboundControl.Count < 128)
                {
                    outboundControl.Enqueue(packet);
                }
            }
        }

        private void FlushOutbound()
        {
            lock (gate)
            {
                while (outboundControl.Count > 0 && isConnected && connection.IsCreated)
                {
                    byte[] pkt = outboundControl.Peek();
                    int res = driver.BeginSend(pipeline, connection, out DataStreamWriter writer, pkt.Length);
                    if (res != 0) break;
                    outboundControl.Dequeue();
                    if (!writer.WriteBytes(pkt))
                    {
                        driver.AbortSend(writer);
                        break;
                    }
                    driver.EndSend(writer);
                }

                if (pendingSnapshot != null && isConnected && connection.IsCreated)
                {
                    byte[] snapPkt = pendingSnapshot;
                    int res = driver.BeginSend(pipeline, connection, out DataStreamWriter writer, snapPkt.Length);
                    if (res == 0)
                    {
                        pendingSnapshot = null;
                        if (writer.WriteBytes(snapPkt))
                        {
                            driver.EndSend(writer);
                        }
                        else
                        {
                            driver.AbortSend(writer);
                        }
                    }
                }
            }
        }

        public void Tick()
        {
            if (disposed) return;

            if (!string.IsNullOrEmpty(disconnectError))
            {
                string err = disconnectError;
                disconnectError = null;
                Disconnected?.Invoke(err);
            }

            if (!driverCreated) return;

            driver.ScheduleUpdate().Complete();

            RelayConnectionStatus relayStatus = driver.GetRelayConnectionStatus();
            if (relayStatus == RelayConnectionStatus.AllocationInvalid)
            {
                isConnected = false;
                disconnectError = "Relay allocation expired or became invalid.";
                StatusText = disconnectError;
                return;
            }

            if (IsHost)
            {
                NetworkConnection accepted;
                while ((accepted = driver.Accept()) != default)
                {
                    connection = accepted;
                    isConnected = true;
                    StatusText = $"Connected via Relay ({JoinCode})";
                }
            }

            if (!connection.IsCreated) return;

            NetworkEvent.Type ev;
            while ((ev = driver.PopEventForConnection(connection, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
            {
                if (ev == NetworkEvent.Type.Connect)
                {
                    isConnected = true;
                    StatusText = $"Connected via Relay ({JoinCode})";
                }
                else if (ev == NetworkEvent.Type.Data)
                {
                    var bytes = new byte[reader.Length];
                    reader.ReadBytes(bytes);
                    LaneWireCodec.Dispatch(
                        bytes,
                        SendPacket,
                        p => RemotePickChanged?.Invoke(p),
                        (h, g, t) => StartReceived?.Invoke(h, g, t),
                        o => OrderReceived?.Invoke(o),
                        s => SnapshotReceived?.Invoke(s),
                        rtt => pingMs = rtt);
                }
                else if (ev == NetworkEvent.Type.Disconnect)
                {
                    connection = default;
                    isConnected = false;
                    disconnectError = "Remote player disconnected from Relay.";
                    StatusText = disconnectError;
                    break;
                }
            }

            if (isConnected)
            {
                pingAccum += Time.unscaledDeltaTime;
                if (pingAccum >= 1.0f)
                {
                    pingAccum = 0f;
                    SendPacket(LaneWireCodec.EncodePing(DateTime.UtcNow.Ticks));
                }
                FlushOutbound();
            }
        }

        public void SendPick(LaneHeroId hero) => SendPacket(LaneWireCodec.EncodePick(hero));
        public void SendStart(LaneHeroId hostHero, LaneHeroId guestHero, LaneTuning tuning) => SendPacket(LaneWireCodec.EncodeStart(hostHero, guestHero, tuning));
        public void SendOrder(LaneOrder order) => SendPacket(LaneWireCodec.EncodeOrder(order));
        public void SendSnapshot(LaneSnapshot snap) => SendPacket(LaneWireCodec.EncodeSnapshot(snap));

        public void Dispose()
        {
            disposed = true;
            isConnected = false;
            if (driverCreated)
            {
                driverCreated = false;
                try { driver.Dispose(); } catch { }
            }
        }
    }
}
