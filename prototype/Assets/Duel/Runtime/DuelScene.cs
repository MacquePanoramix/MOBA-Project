using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace Moba.Duel
{
    public sealed class DuelScene : MonoBehaviour
    {
        [SerializeField] private Shader surfaceShader;
        [SerializeField] private Shader lineShader;
        [SerializeField] private DuelTuning tuning = new DuelTuning();

        internal enum Mode { Lobby, Practice, Host, Guest }
        internal Mode Session { get; private set; }
        internal DuelHero Hero { get; private set; }
        internal DuelFrame Frame { get; private set; }
        internal int Round { get; private set; }
        internal bool MenuOpen { get; private set; }
        internal string Status { get; private set; } = "";
        internal string HostAddress { get; private set; }
        internal int PingMs => link.PingMs;
        internal bool IsReady => Frame != null && (Session == Mode.Guest ? Frame.guestReady || readySent : Frame.hostReady);
        internal bool Armed => armed.HasValue;

        private const float Step = 1f / 60f;
        private const float BroadcastInterval = 1f / 30f;
        private readonly DuelLink link = new DuelLink();
        private DuelWorld world;
        private DuelHud hud;
        private DuelMatch match;
        private DuelBot opponent;
        private DuelHero preferredHero;
        private float accumulated, broadcastIn;
        private bool hostReady, guestReady, readySent;
        private DuelOrder? armed;

        private void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            HostAddress = FindHostAddress();
            world = new DuelWorld(transform, surfaceShader, lineShader, tuning);
            hud = new DuelHud(this);
            link.PeerConnected += OnPeerConnected;
            link.PeerDisconnected += OnPeerDisconnected;
            link.FrameReceived += ReceiveFrame;
            link.OrderReceived += (order, point) => match?.Command(Other(Hero), order, point);
            link.RematchReceived += () => { if (Frame != null && Frame.finished) guestReady = true; };
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-duelHost") { Host(DuelHero.Ogre, args[i + 1]); break; }
                if (args[i] == "-duelJoin") { Join(args[i + 1]); break; }
            }
        }

        internal static DuelHero Other(DuelHero hero) => hero == DuelHero.Ogre ? DuelHero.Luis : DuelHero.Ogre;

        private void Update()
        {
            link.Poll();
            if (Session == Mode.Guest && !link.Connecting && !link.Connected)
            {
                ReturnToLobby(link.Status);
            }
            if (Input.GetKeyDown(KeyCode.Escape) && Frame != null)
            {
                if (armed.HasValue) armed = null;
                else MenuOpen = !MenuOpen;
            }
            if (Frame != null && !Frame.finished && !MenuOpen) ReadPlayerInput();
            if (match != null && !(MenuOpen && Session == Mode.Practice))
            {
                accumulated += Mathf.Min(Time.unscaledDeltaTime, 0.20f);
                while (accumulated >= Step)
                {
                    if (opponent != null && !Frame.finished) opponent.Tick(match, Frame, Step);
                    match.Tick(Step);
                    Frame = match.Snapshot();
                    Frame.hostReady = hostReady;
                    Frame.guestReady = guestReady;
                    accumulated -= Step;
                }
                if (Session == Mode.Host && link.Connected)
                {
                    if (Frame.finished && hostReady && guestReady) StartRound(Other(Hero));
                    broadcastIn -= Time.unscaledDeltaTime;
                    if (broadcastIn <= 0f) Broadcast();
                }
            }
            world.Show(Frame, Hero, Time.unscaledDeltaTime);
        }

        private void ReadPlayerInput()
        {
            Vector2 mouse = Input.mousePosition;
            if (hud.BlocksWorldInput(mouse) || !world.TryGround(mouse, out Vector2 point)) return;
            if (Hero == DuelHero.Ogre && Input.GetKeyDown(KeyCode.Q)) Issue(DuelOrder.Slam, point);
            else if (Hero == DuelHero.Ogre && Input.GetKeyDown(KeyCode.W)) Issue(DuelOrder.Sweep, point);
            else if (armed.HasValue && Input.GetMouseButtonDown(0)) Issue(armed.Value, point);
            else if (Input.GetMouseButtonDown(1))
            {
                // Clicks on the height of a visible model can project behind its ground position.
                Vector2 feet = world.Camera.WorldToScreenPoint(new Vector3(Frame.ogre.position.x, 0f, Frame.ogre.position.y));
                Vector2 shoulders = world.Camera.WorldToScreenPoint(new Vector3(Frame.ogre.position.x, 1.5f, Frame.ogre.position.y));
                Vector2 axis = shoulders - feet;
                float t = Mathf.Clamp01(Vector2.Dot(mouse - feet, axis) / Mathf.Max(axis.sqrMagnitude, 0.01f));
                float hitPixels = Screen.height * 0.58f / (2f * world.Camera.orthographicSize);
                bool hitOgre = (point - Frame.ogre.position).sqrMagnitude < 0.8f * 0.8f ||
                    (mouse - (feet + t * axis)).sqrMagnitude < hitPixels * hitPixels;
                Issue(Hero == DuelHero.Luis && hitOgre ? DuelOrder.Basic : DuelOrder.Move, point);
            }
        }

        internal void Arm(DuelOrder order) { armed = armed == order ? (DuelOrder?)null : order; }

        internal void Issue(DuelOrder order, Vector2 point)
        {
            if (Frame == null || Frame.finished) return;
            armed = null;
            if (Session == Mode.Guest) link.SendOrder(order, point);
            else match?.Command(Hero, order, point);
            if (order == DuelOrder.Move) world.SetMovementMarker(point);
        }

        internal void Practice(DuelHero hero)
        {
            link.Close();
            Session = Mode.Practice;
            Round = 0;
            Status = "";
            StartRound(hero);
        }

        internal void Host(DuelHero hero, string address = null)
        {
            if (!link.Host(DuelLink.DefaultPort, address)) { Status = link.Status; return; }
            Session = Mode.Host;
            preferredHero = hero;
            if (!string.IsNullOrWhiteSpace(address) && address.Trim() != "0.0.0.0")
                HostAddress = address.Trim();
            Round = 0;
            Status = "Waiting for a friend";
        }

        internal void Join(string address)
        {
            if (!link.Join(address, DuelLink.DefaultPort)) { Status = link.Status; return; }
            Session = Mode.Guest;
            Status = link.Status;
        }

        private void StartRound(DuelHero hero)
        {
            Round++;
            Hero = hero;
            hostReady = guestReady = readySent = MenuOpen = false;
            accumulated = broadcastIn = 0f;
            armed = null;
            match = new DuelMatch(tuning);
            opponent = Session == Mode.Practice ? new DuelBot(Other(Hero), tuning) : null;
            Frame = match.Snapshot();
            if (Session == Mode.Host) Broadcast();
            Debug.Log($"Duel round {Round} started ({Session}, {DuelNames.For(hero)}).");
        }

        private void Broadcast()
        {
            Frame.hostReady = hostReady;
            Frame.guestReady = guestReady;
            link.SendFrame(Frame, Other(Hero), Round);
            broadcastIn = BroadcastInterval;
        }

        private void OnPeerConnected()
        {
            if (Session == Mode.Host) StartRound(preferredHero);
            else Status = "Waiting for the first round";
        }

        private void OnPeerDisconnected()
        {
            Frame = null;
            match = null;
            armed = null;
            MenuOpen = false;
            Status = Session == Mode.Host ? "Friend disconnected. Waiting for them to rejoin." : link.Status;
        }

        private void ReceiveFrame(DuelFrame frame, DuelHero assigned, int round)
        {
            if (round != Round || Frame == null)
            {
                armed = null;
                readySent = MenuOpen = false;
                Debug.Log($"Duel round {round} received (Guest, {DuelNames.For(assigned)}).");
            }
            Hero = assigned;
            Round = round;
            Frame = frame;
        }

        internal void Rematch(bool swapPractice)
        {
            if (Session == Mode.Practice) { StartRound(swapPractice ? Other(Hero) : Hero); return; }
            if (Frame == null || !Frame.finished || IsReady) return;
            MenuOpen = false;
            if (Session == Mode.Host) { hostReady = true; Broadcast(); }
            else if (Session == Mode.Guest) { link.SendRematch(); readySent = true; }
        }

        internal void Resume() { MenuOpen = false; }
        internal void ReturnToLobby(string message = "")
        {
            link.Close();
            Session = Mode.Lobby;
            Frame = null;
            match = null;
            opponent = null;
            armed = null;
            MenuOpen = readySent = false;
            Status = message;
        }

        private static string FindHostAddress()
        {
            string lan = null;
            try
            {
                foreach (var device in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (device.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var entry in device.GetIPProperties().UnicastAddresses)
                    {
                        IPAddress ip = entry.Address;
                        if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)) continue;
                        var b = ip.GetAddressBytes();
                        if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return ip.ToString();
                        if (lan == null && (b[0] == 10 || (b[0] == 192 && b[1] == 168) ||
                            (b[0] == 172 && b[1] >= 16 && b[1] <= 31))) lan = ip.ToString();
                    }
                }
            }
            catch (NetworkInformationException) { }
            catch (NotImplementedException) { }
            return lan ?? "127.0.0.1";
        }

        private void OnGUI() => hud?.Draw();
        private void OnDestroy() { link.Dispose(); world?.Dispose(); hud?.Dispose(); }
    }
}
