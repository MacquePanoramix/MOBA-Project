using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Moba.Duel
{
    // One host and one guest. All callbacks run on the caller's thread during Poll.
    // Only the host sends snapshots; the guest's connection identifies its seat.
    public sealed class DuelLink : IDisposable
    {
        public const int DefaultPort = 47777;
        private const int MaxFrameBytes = 16384;
        private const int MaxCommandBytes = 96;
        private const int MaxQueuedCommands = 64;
        private const float MaxCoordinate = 1024f;
        private const string Hello = "HELLO MOBA-DUEL/1";
        private const string Welcome = "WELCOME MOBA-DUEL/1";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public event Action PeerConnected;
        public event Action PeerDisconnected;
        public event Action<DuelOrder, Vector2> OrderReceived;
        public event Action RematchReceived;
        public event Action<DuelFrame, DuelHero, int> FrameReceived;

        public bool IsHost => listener != null;
        public bool Listening => listener != null;
        public bool Connected { get; private set; }
        public bool Connecting => (peer != null || resolving != null) && !Connected;
        public int Port { get; private set; }
        public int PingMs { get; private set; } = -1;
        public string Status { get; private set; } = "Offline";

        private Socket listener;
        private Socket peer;
        private Task<IPAddress[]> resolving;
        private int dialPort;
        private bool dialing;
        private int round = -1;
        private int lastRematch = -1;
        private bool roundFinished;
        private readonly byte[] chunk = new byte[4096];
        private readonly byte[] line = new byte[MaxFrameBytes];
        private int lineLength;
        private readonly Queue<byte[]> outgoing = new Queue<byte[]>();
        private byte[] sending;
        private int sent;
        private byte[] latestFrame;
        private double started, receivedAt, keepAliveAt, windowStart;
        private int receivedLines;
        private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        private static long NowMs => (long)(Now * 1000.0);

        public bool Host(int port, string bindIPv4 = null)
        {
            Close();
            IPAddress address = IPAddress.Any;
            if (port < 0 || port > 65535)
                return Fail("Enter a valid port.");
            if (!string.IsNullOrWhiteSpace(bindIPv4))
            {
                string trimmed = bindIPv4.Trim();
                if (trimmed != "0.0.0.0" && !TryIPv4(trimmed, out address))
                    return Fail("Enter a valid local IPv4 address and port.");
            }
            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listener.Blocking = false;
                listener.Bind(new IPEndPoint(address, port));
                listener.Listen(2);
                Port = ((IPEndPoint)listener.LocalEndPoint).Port;
                Status = "Waiting for a guest";
                return true;
            }
            catch (SocketException e) { Close(); return Fail("Cannot host: " + e.SocketErrorCode); }
        }

        public bool Join(string addressText, int defaultPort)
        {
            Close();
            if (!TryParseEndpoint(addressText, defaultPort, out string host, out int port, out IPAddress address))
                return Fail("Enter host:port or IPv4 address.");
            if (address != null)
                return DialAddress(address, port);

            dialPort = port;
            Port = port;
            started = Now;
            Status = "Resolving host";
            resolving = Task.Run(() => Dns.GetHostAddresses(host));
            return true;
        }

        private bool DialAddress(IPAddress address, int port)
        {
            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                StartPeer(socket);
                Port = port;
                dialing = true;
                Status = "Connecting";
                try { peer.Connect(new IPEndPoint(address, port)); }
                catch (SocketException e) when (WouldWait(e.SocketErrorCode)) { return true; }
                FinishDial();
                return true;
            }
            catch (SocketException e) { Drop("Cannot join: " + e.SocketErrorCode); return false; }
        }

        public void Poll()
        {
            try
            {
                if (IsHost && listener.Poll(0, SelectMode.SelectRead))
                {
                    Socket incoming = listener.Accept();
                    if (peer != null) incoming.Dispose();
                    else { StartPeer(incoming); Status = "Checking guest connection"; }
                }
                if (resolving != null)
                {
                    if (Now - started > 10)
                    {
                        Drop("Timed out resolving host address");
                        return;
                    }
                    if (!resolving.IsCompleted) return;
                    Task<IPAddress[]> done = resolving;
                    resolving = null;
                    if (done.IsFaulted || done.IsCanceled)
                    {
                        Drop("Could not resolve host address");
                        return;
                    }
                    IPAddress ipv4 = null;
                    foreach (IPAddress candidate in done.Result)
                    {
                        if (candidate.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipv4 = candidate;
                            break;
                        }
                    }
                    if (ipv4 == null)
                    {
                        Drop("Host has no IPv4 address");
                        return;
                    }
                    if (!DialAddress(ipv4, dialPort)) return;
                }
                if (peer == null) return;
                double now = Now;
                if ((!Connected && now - started > 10) || (Connected && now - receivedAt > 20))
                {
                    Drop("Connection timed out");
                    return;
                }
                if (dialing)
                {
                    if (!peer.Poll(0, SelectMode.SelectWrite) && !peer.Poll(0, SelectMode.SelectError)) return;
                    var error = (SocketError)(int)peer.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
                    if (error != SocketError.Success) { Drop("Cannot join: " + error); return; }
                    FinishDial();
                }
                if (Connected && now >= keepAliveAt)
                {
                    QueueLine("PING " + NowMs.ToString(Invariant));
                    keepAliveAt = now + 1.5;
                }
                Flush();
                // Bound the work done in a single game update, including fragmented packets.
                for (int i = 0; i < 8 && peer != null && peer.Poll(0, SelectMode.SelectRead); i++)
                {
                    int count = peer.Receive(chunk);
                    if (count == 0) { Drop("Peer disconnected"); return; }
                    for (int j = 0; j < count && peer != null; j++)
                    {
                        if (chunk[j] == (byte)'\n')
                        {
                            string message = Utf8.GetString(line, 0, lineLength);
                            lineLength = 0;
                            if (now - windowStart >= 1) { receivedLines = 0; windowStart = now; }
                            if (++receivedLines > 128 || !ReceiveLine(message)) { Drop("Unsupported or invalid peer message"); return; }
                            receivedAt = now;
                        }
                        else
                        {
                            int limit = IsHost || !Connected ? MaxCommandBytes : MaxFrameBytes;
                            if (lineLength >= limit) { Drop("Peer message too large"); return; }
                            line[lineLength++] = chunk[j];
                        }
                    }
                }
            }
            catch (SocketException e) when (WouldWait(e.SocketErrorCode)) { }
            catch (SocketException e) { Drop("Connection lost: " + e.SocketErrorCode); }
            catch (DecoderFallbackException) { Drop("Invalid peer text"); }
        }

        public void SendOrder(DuelOrder order, Vector2 target)
        {
            if (!Connected || IsHost || round < 0 || !ValidOrder((int)order) || !ValidPoint(target)) return;
            QueueLine(string.Format(Invariant, "ORDER {0} {1} {2:R} {3:R}", round, (int)order, target.x, target.y));
        }

        public void SendRematch()
        {
            if (Connected && !IsHost && round >= 0 && roundFinished)
                QueueLine("REMATCH " + round.ToString(Invariant));
        }

        public void SendFrame(DuelFrame frame, DuelHero clientHero, int roundId)
        {
            if (!IsHost || !Connected || frame == null || roundId < 0 || roundId < round) return;
            string json = JsonUtility.ToJson(frame);
            string message = string.Format(Invariant, "FRAME {0} {1} {2}\n", roundId, (int)clientHero, json);
            if (Utf8.GetByteCount(message) > MaxFrameBytes) return;
            round = roundId;
            roundFinished = frame.finished;
            // A slow peer needs the latest state. Never build a backlog of obsolete snapshots.
            latestFrame = Utf8.GetBytes(message);
        }

        private bool ReceiveLine(string message)
        {
            if (!Connected)
            {
                if (message != (IsHost ? Hello : Welcome)) return false;
                if (IsHost) QueueLine(Welcome);
                Connected = true;
                keepAliveAt = Now + 0.2;
                Status = "Connected";
                PeerConnected?.Invoke();
                return true;
            }
            if (message == "PING") return true;
            if (message.StartsWith("PING ", StringComparison.Ordinal))
            {
                string token = message.Substring(5);
                if (token.Length > 24 || !long.TryParse(token, NumberStyles.Integer, Invariant, out _)) return false;
                QueueLine("PONG " + token);
                return true;
            }
            if (message.StartsWith("PONG ", StringComparison.Ordinal))
            {
                string token = message.Substring(5);
                if (token.Length > 24 || !long.TryParse(token, NumberStyles.Integer, Invariant, out long sentMs)) return false;
                long elapsedMs = NowMs - sentMs;
                if (elapsedMs >= 0 && elapsedMs <= 60000)
                {
                    int rtt = (int)Math.Min(elapsedMs, 9999L);
                    PingMs = PingMs < 0 ? rtt : Mathf.RoundToInt(Mathf.Lerp(PingMs, rtt, 0.35f));
                }
                return true;
            }
            if (!IsHost) return ReceiveFrame(message);
            string[] parts = message.Split(' ');
            if (parts.Length < 2 || !TryNonnegative(parts[1], out int suppliedRound)) return false;
            if (parts[0] == "REMATCH" && parts.Length == 2)
            {
                if (suppliedRound == round && roundFinished && lastRematch != round) { lastRematch = round; RematchReceived?.Invoke(); }
                return true;
            }
            if (parts[0] != "ORDER" || parts.Length != 5 || !TryNonnegative(parts[2], out int action) || !ValidOrder(action) ||
                !float.TryParse(parts[3], NumberStyles.Float, Invariant, out float x) ||
                !float.TryParse(parts[4], NumberStyles.Float, Invariant, out float y)) return false;
            var target = new Vector2(x, y);
            if (!ValidPoint(target)) return false;
            if (suppliedRound == round) OrderReceived?.Invoke((DuelOrder)action, target);
            return true;
        }

        private bool ReceiveFrame(string message)
        {
            string[] parts = message.Split(new[] { ' ' }, 4, StringSplitOptions.None);
            if (parts.Length != 4 || parts[0] != "FRAME" || !TryNonnegative(parts[1], out int frameRound) ||
                frameRound < round || !TryNonnegative(parts[2], out int hero) || hero > 1 ||
                !parts[3].StartsWith("{", StringComparison.Ordinal)) return false;
            DuelFrame frame;
            try { frame = JsonUtility.FromJson<DuelFrame>(parts[3]); }
            catch (ArgumentException) { return false; }
            if (frame == null || !ValidUnit(frame.ogre) || !ValidUnit(frame.luis) || !ValidPoint(frame.orb) ||
                frame.winner < -1 || frame.winner > 1 || (frame.notice != null && frame.notice.Length > 512)) return false;
            round = frameRound;
            roundFinished = frame.finished;
            FrameReceived?.Invoke(frame, (DuelHero)hero, round);
            return true;
        }

        private static bool TryParseEndpoint(string raw, int defaultPort, out string host, out int port, out IPAddress ipv4)
        {
            host = null;
            port = defaultPort;
            ipv4 = null;
            if (string.IsNullOrWhiteSpace(raw) || defaultPort < 1 || defaultPort > 65535) return false;
            string text = raw.Trim();
            if (text.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(6);
            text = text.TrimEnd('/');
            if (string.IsNullOrEmpty(text)) return false;

            int firstColon = text.IndexOf(':');
            int lastColon = text.LastIndexOf(':');
            if (firstColon >= 0)
            {
                if (firstColon != lastColon) return false;
                string portText = text.Substring(lastColon + 1);
                if (!TryNonnegative(portText, out port) || port < 1 || port > 65535) return false;
                text = text.Substring(0, lastColon);
            }
            if (string.IsNullOrEmpty(text)) return false;
            if (TryIPv4(text, out ipv4))
            {
                if (ipv4.Equals(IPAddress.Any)) return false;
                host = text;
                return true;
            }
            if (!IsValidHostname(text)) return false;
            host = text;
            return true;
        }

        private static bool IsValidHostname(string host)
        {
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
            if (host.Length > 253 || !host.Contains(".") || host.StartsWith(".", StringComparison.Ordinal) ||
                host.EndsWith(".", StringComparison.Ordinal)) return false;
            string[] labels = host.Split('.');
            foreach (string label in labels)
            {
                if (label.Length == 0 || label.Length > 63) return false;
                if (label[0] == '-' || label[label.Length - 1] == '-') return false;
                for (int i = 0; i < label.Length; i++)
                {
                    char c = label[i];
                    bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-';
                    if (!ok) return false;
                }
            }
            return true;
        }

        private static bool ValidUnit(DuelUnitFrame unit)
        {
            return unit != null && ValidPoint(unit.position) && ValidPoint(unit.aim) &&
                unit.hp >= 0 && unit.maxHp > 0 && unit.hp <= unit.maxHp && unit.maxHp <= 1000000f &&
                unit.heading >= -360f && unit.heading <= 360f && unit.progress >= 0 && unit.progress <= 1.001f &&
                unit.action >= 0 && unit.action <= 3 && ValidTimer(unit.qCooldown) && ValidTimer(unit.wCooldown) && ValidTimer(unit.stun);
        }

        private static bool ValidTimer(float time) => time >= -0.01f && time <= 1000000f;
        private static bool ValidOrder(int value) => value >= (int)DuelOrder.Move && value <= (int)DuelOrder.Sweep;
        private static bool ValidPoint(Vector2 p) => Mathf.Abs(p.x) <= MaxCoordinate && Mathf.Abs(p.y) <= MaxCoordinate;
        private static bool TryNonnegative(string text, out int value) => int.TryParse(text, NumberStyles.None, Invariant, out value) && value >= 0;
        private static bool TryIPv4(string text, out IPAddress address) =>
            IPAddress.TryParse(text, out address) && address.AddressFamily == AddressFamily.InterNetwork;
        private static bool WouldWait(SocketError error) =>
            error == SocketError.WouldBlock || error == SocketError.InProgress || error == SocketError.AlreadyInProgress;

        private void StartPeer(Socket socket)
        {
            peer = socket;
            peer.Blocking = false;
            peer.NoDelay = true;
            started = receivedAt = windowStart = Now;
            keepAliveAt = started + 2;
            receivedLines = 0;
        }

        private void FinishDial()
        {
            dialing = false;
            QueueLine(Hello);
            Status = "Waiting for host";
        }

        private void QueueLine(string message)
        {
            if (outgoing.Count < MaxQueuedCommands)
                outgoing.Enqueue(Utf8.GetBytes(message + "\n"));
        }

        private void Flush()
        {
            for (int i = 0; i < 8 && peer.Poll(0, SelectMode.SelectWrite); i++)
            {
                if (sending == null)
                {
                    if (outgoing.Count != 0) sending = outgoing.Dequeue();
                    else if (latestFrame != null) { sending = latestFrame; latestFrame = null; }
                    else return;
                    sent = 0;
                }
                sent += peer.Send(sending, sent, sending.Length - sent, SocketFlags.None);
                if (sent < sending.Length) return;
                sending = null;
            }
        }

        private void Drop(string reason)
        {
            bool notify = Connected;
            resolving = null;
            dialPort = 0;
            PingMs = -1;
            peer?.Dispose();
            peer = null;
            dialing = Connected = false;
            round = lastRematch = -1;
            roundFinished = false;
            sending = latestFrame = null;
            lineLength = sent = 0;
            outgoing.Clear();
            Status = reason + (IsHost ? ". Waiting for a guest" : "");
            if (notify) PeerDisconnected?.Invoke();
        }

        private bool Fail(string message) { Status = message; return false; }

        public void Close()
        {
            listener?.Dispose();
            listener = null;
            Port = 0;
            Drop("Offline");
        }

        public void Dispose() => Close();
    }
}
