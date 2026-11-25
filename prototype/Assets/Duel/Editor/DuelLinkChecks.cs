using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Moba.Duel
{
    public static class DuelLinkChecks
    {
        [MenuItem("Duel/Run Connection Checks")]
        public static void Run()
        {
            using (var host = new DuelLink())
            using (var guest = new DuelLink())
            {
                Check(!guest.Join("not-an-ip", DuelLink.DefaultPort), "Invalid address accepted");
                Check(!guest.Join("bad:99999", DuelLink.DefaultPort), "Invalid port accepted");
                Check(!guest.Join("0.0.0.0", DuelLink.DefaultPort), "Unspecified IPv4 accepted");
                Check(host.Host(0), host.Status);
                int joins = 0, drops = 0, orders = 0, readies = 0, frames = 0, shownRound = -1;
                var shownHero = DuelHero.Ogre;
                var click = new Vector2(2.75f, -1f);
                host.PeerConnected += () => joins++;
                host.PeerDisconnected += () => drops++;
                host.OrderReceived += (command, destination) =>
                {
                    Check(command == DuelOrder.Move && destination == click, "Order or target changed in transit");
                    orders++;
                };
                host.RematchReceived += () => readies++;
                guest.FrameReceived += (state, hero, id) =>
                {
                    Check(state.notice == "Luís — orb", "UTF-8 snapshot changed in transit");
                    frames++;
                    shownRound = id;
                    shownHero = hero;
                };

                Check(guest.Join($"127.0.0.1:{host.Port}", DuelLink.DefaultPort), guest.Status);
                Pump(host, guest, () => guest.Connected && joins == 1, "Two-way handshake");
                Pump(host, guest, () => guest.PingMs >= 0 && host.PingMs >= 0, "RTT ping measurement");
                var frame = new DuelMatch(new DuelTuning()).Snapshot();
                frame.notice = "Luís — orb";
                host.SendFrame(frame, DuelHero.Luis, 1);
                Pump(host, guest, () => shownRound == 1, "First state");
                Check(shownHero == DuelHero.Luis, "Initial seat changed");
                guest.SendOrder((DuelOrder)999, click);
                guest.SendOrder(DuelOrder.Slam, new Vector2(float.NaN, 2));
                guest.SendOrder(DuelOrder.Move, click);
                guest.SendRematch();
                Pump(host, guest, () => orders == 1, "Guest order");
                Check(readies == 0, "A live round accepted a rematch");

                guest.SendOrder(DuelOrder.Move, click); // Queued with old round, delivered after host advances.
                frame.finished = true;
                frame.winner = (int)DuelHero.Ogre;
                host.SendFrame(frame, DuelHero.Ogre, 2);
                Pump(host, guest, () => shownRound == 2, "Second state");
                guest.SendRematch();
                guest.SendRematch();
                Pump(host, guest, () => readies > 0, "Finished round rematch");
                Check(shownHero == DuelHero.Ogre && orders == 1 && readies == 1, "Swap, stale order, or duplicate ready failed");
                Check(frames >= 2, "Snapshots missing");
                guest.Close();
                Pump(host, guest, () => drops == 1, "Host disconnect notice");
                Check(host.Listening && !host.Connected, "Host did not reopen guest seat");

                // A misconfigured peer must never advance the match or claim its guest seat.
                using (var wrongProtocol = new TcpClient(AddressFamily.InterNetwork))
                {
                    wrongProtocol.Connect(IPAddress.Loopback, host.Port);
                    byte[] mismatch = Encoding.UTF8.GetBytes("HELLO MOBA-DUEL/0\n");
                    wrongProtocol.GetStream().Write(mismatch, 0, mismatch.Length);
                    Pump(host, guest, () => host.Status.Contains("invalid peer message"), "Version mismatch detection");
                }
                Check(joins == 1, "An incompatible client began play");
                Check(guest.Join($"tcp://localhost:{host.Port}", DuelLink.DefaultPort), guest.Status);
                Pump(host, guest, () => guest.Connected && joins == 2, "Guest DNS rejoin");
            }
            UnityEngine.Debug.Log("Duel connection checks passed: loopback seats, frames, typed commands, invalid/stale input, rematch, disconnect and rejoin.");
        }

        private static void Pump(DuelLink host, DuelLink guest, Func<bool> done, string step)
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 3000)
            {
                host.Poll();
                guest.Poll();
                if (done()) return;
                Thread.Sleep(1);
            }
            throw new InvalidOperationException(step + " timed out. Host: " + host.Status + ". Guest: " + guest.Status);
        }

        private static void Check(bool condition, string failure)
        {
            if (!condition) throw new InvalidOperationException(failure);
        }
    }
}
