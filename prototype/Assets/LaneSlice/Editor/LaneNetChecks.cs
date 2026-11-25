using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Moba.LaneSlice
{
    public static class LaneNetChecks
    {
        public static void Run()
        {
            CheckTcpLoopbackFullCycle();
            CheckTransportFragmentedReliablePipeline();
            CheckRelayOfflineUnlinkedGuard();
            CheckUpdaterParsingAndVersionCompare();
            CheckSettingsSurviveSimulatedUpdate();
            Debug.Log("[LaneNetChecks] All TCP loopback, UTP Fragmented+Reliable loopback, Relay, Updater, and Update-Persistence checks passed.");
        }

        private static void CheckTcpLoopbackFullCycle()
        {
            const int port = 47891;
            using (TcpLaneLink host = TcpLaneLink.Host("0.0.0.0", port))
            using (TcpLaneLink guest = TcpLaneLink.Connect($"tcp://localhost:{port}", port))
            {
                LaneHeroId hostSawGuestPick = LaneHeroId.Ogre;
                bool guestSawStart = false;
                LaneHeroId syncedHost = LaneHeroId.Ogre;
                LaneHeroId syncedGuest = LaneHeroId.Ogre;
                var hostOrders = new List<LaneOrder>();
                LaneSnapshot guestSnap = null;
                string guestDiscReason = null;

                host.RemotePickChanged += h => hostSawGuestPick = h;
                host.OrderReceived += o => hostOrders.Add(o);
                guest.StartReceived += (h, g, t) =>
                {
                    guestSawStart = true;
                    syncedHost = h;
                    syncedGuest = g;
                };
                guest.SnapshotReceived += s => guestSnap = s;
                guest.Disconnected += r => guestDiscReason = r;

                var sw = Stopwatch.StartNew();
                while (!(host.IsConnected && guest.IsConnected) && sw.ElapsedMilliseconds < 4000)
                {
                    host.Tick();
                    guest.Tick();
                    Thread.Sleep(15);
                }
                if (!(host.IsConnected && guest.IsConnected))
                {
                    throw new InvalidOperationException("TcpLaneLink loopback failed to connect within 4s.");
                }

                // Pick, Start, Order (including Shop BuyItem), Large Snapshot (>20 KB)
                guest.SendPick(LaneHeroId.Swordsman);
                PumpBoth(host, guest, 250);
                if (hostSawGuestPick != LaneHeroId.Swordsman)
                {
                    throw new InvalidOperationException($"Expected Swordsman pick, got {hostSawGuestPick}");
                }

                host.SendStart(LaneHeroId.Ogre, LaneHeroId.Swordsman, new LaneTuning());
                PumpBoth(host, guest, 250);
                if (!guestSawStart || syncedHost != LaneHeroId.Ogre || syncedGuest != LaneHeroId.Swordsman)
                {
                    throw new InvalidOperationException("StartReceived payload mismatch over TCP.");
                }

                guest.SendOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.BuyItem, param = 12 });
                guest.SendOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackMove, point = new Vector2(2f, 1f) });
                guest.SendOrder(new LaneOrder
                {
                    team = LaneTeam.Red,
                    kind = LaneOrderKind.Move,
                    point = new Vector2(3.5f, -1.5f),
                    unitIds = new[] { 2, 105 },
                });
                guest.SendOrder(new LaneOrder
                {
                    team = LaneTeam.Red,
                    kind = LaneOrderKind.Stop,
                    unitIds = new[] { 105 },
                });

                // Synthesize heavy multi-wave snapshot (~22+ KB JSON) to verify large frame delivery
                var heavyMatch = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Swordsman, new LaneTuning { waveFirstSpawnDelay = 0.01f });
                for (int i = 0; i < 40; i++) heavyMatch.Tick(0.05f);
                LaneSnapshot big = heavyMatch.Snapshot();
                for (int extra = 0; extra < 120; extra++)
                {
                    big.minions.Add(new MinionSnap { id = 5000 + extra, team = LaneTeam.Blue, x = extra * 0.1f, y = 0.5f, hp = 200f, maxHp = 240f });
                }
                int jsonBytes = System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(big));
                if (jsonBytes < 18000) throw new InvalidOperationException($"Test heavy frame unexpectedly small ({jsonBytes} bytes).");

                host.SendSnapshot(big);
                PumpBoth(host, guest, 400);

                if (hostOrders.Count != 4 || hostOrders[0].kind != LaneOrderKind.BuyItem ||
                    hostOrders[1].kind != LaneOrderKind.AttackMove ||
                    hostOrders[2].kind != LaneOrderKind.Move ||
                    hostOrders[2].unitIds == null || hostOrders[2].unitIds.Length != 2 ||
                    hostOrders[2].unitIds[0] != 2 || hostOrders[2].unitIds[1] != 105 ||
                    hostOrders[3].kind != LaneOrderKind.Stop)
                {
                    throw new InvalidOperationException($"Expected 4 remote orders including RTS unitIds & Stop on host, got {hostOrders.Count}");
                }
                if (guestSnap == null || guestSnap.minions.Count != big.minions.Count)
                {
                    throw new InvalidOperationException($"Heavy snapshot roundtrip failed ({guestSnap?.minions.Count ?? -1} vs {big.minions.Count}).");
                }

                // Force disconnect check
                host.Dispose();
                sw.Restart();
                while (guestDiscReason == null && sw.ElapsedMilliseconds < 2500)
                {
                    guest.Tick();
                    Thread.Sleep(20);
                }
                if (string.IsNullOrEmpty(guestDiscReason))
                {
                    throw new InvalidOperationException("Guest did not report Disconnected after host closed.");
                }
            }
        }

        private static void CheckTransportFragmentedReliablePipeline()
        {
            const ushort port = 47893;
            bool serverCreated = false;
            bool clientCreated = false;
            NetworkDriver server = default;
            NetworkDriver client = default;

            try
            {
                var settings = new NetworkSettings(Allocator.Persistent);
                try
                {
                    settings.WithFragmentationStageParameters(payloadCapacity: LaneWireCodec.MaxFrameBytes);
                    settings.WithReliableStageParameters(windowSize: 64);

                    server = NetworkDriver.Create(settings);
                    serverCreated = true;
                    client = NetworkDriver.Create(settings);
                    clientCreated = true;
                }
                finally
                {
                    settings.Dispose();
                }

                server.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
                NetworkPipeline cPipe = client.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));

                NetworkEndpoint ep = NetworkEndpoint.LoopbackIpv4.WithPort(port);
                if (server.Bind(ep) != 0 || server.Listen() != 0)
                {
                    throw new InvalidOperationException("UTP loopback server failed to bind/listen.");
                }

                NetworkConnection cConn = client.Connect(ep);
                NetworkConnection sConn = default;
                bool clientConnected = false;

                var heavyMatch = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, new LaneTuning { waveFirstSpawnDelay = 0.01f });
                for (int i = 0; i < 30; i++) heavyMatch.Tick(0.05f);
                LaneSnapshot big = heavyMatch.Snapshot();
                for (int extra = 0; extra < 120; extra++)
                {
                    big.minions.Add(new MinionSnap { id = 6000 + extra, team = LaneTeam.Red, x = extra * 0.1f, y = -0.5f, hp = 180f, maxHp = 240f });
                }
                byte[] pkt = LaneWireCodec.EncodeSnapshot(big);
                if (pkt.Length < 18000) throw new InvalidOperationException($"Expected >18KB UTP payload, got {pkt.Length}.");

                bool sent = false;
                LaneSnapshot receivedSnap = null;
                var sw = Stopwatch.StartNew();

                while (receivedSnap == null && sw.ElapsedMilliseconds < 4000)
                {
                    server.ScheduleUpdate().Complete();
                    client.ScheduleUpdate().Complete();

                    NetworkConnection accepted;
                    while ((accepted = server.Accept()) != default) sConn = accepted;

                    if (sConn.IsCreated)
                    {
                        NetworkEvent.Type ev;
                        while ((ev = server.PopEventForConnection(sConn, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
                        {
                            if (ev == NetworkEvent.Type.Data)
                            {
                                var buf = new byte[reader.Length];
                                reader.ReadBytes(buf);
                                LaneWireCodec.Dispatch(buf, null, null, null, null, s => receivedSnap = s, null);
                            }
                        }
                    }

                    NetworkEvent.Type cev;
                    while ((cev = client.PopEventForConnection(cConn, out _)) != NetworkEvent.Type.Empty)
                    {
                        if (cev == NetworkEvent.Type.Connect) clientConnected = true;
                    }

                    if (clientConnected && !sent)
                    {
                        if (client.BeginSend(cPipe, cConn, out DataStreamWriter writer, pkt.Length) == 0)
                        {
                            writer.WriteBytes(pkt);
                            client.EndSend(writer);
                            sent = true;
                        }
                    }

                    Thread.Sleep(10);
                }

                if (!sent || receivedSnap == null || receivedSnap.minions.Count != big.minions.Count)
                {
                    throw new InvalidOperationException(
                        $"UTP Fragmented+Reliable loopback failed (sent={sent}, minions={receivedSnap?.minions.Count ?? -1}/{big.minions.Count}).");
                }
            }
            finally
            {
                if (clientCreated) client.Dispose();
                if (serverCreated) server.Dispose();
            }
        }

        private static void CheckRelayOfflineUnlinkedGuard()
        {
            // When Unity Cloud Project ID is unlinked (blank), RelayLaneLink must fail cleanly without hanging
            if (!string.IsNullOrWhiteSpace(Application.cloudProjectId)) return;

            using (RelayLaneLink relay = RelayLaneLink.HostRelay())
            {
                string err = null;
                relay.Disconnected += r => err = r;

                var sw = Stopwatch.StartNew();
                while (err == null && sw.ElapsedMilliseconds < 2000)
                {
                    relay.Tick();
                    Thread.Sleep(20);
                }
                if (string.IsNullOrEmpty(err) || err.IndexOf("Project ID", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new InvalidOperationException($"Expected clean unlinked Cloud Project guidance, got: '{err}'");
                }
                if (relay.IsConnected) throw new InvalidOperationException("Relay must not report connected when unlinked.");
            }
        }

        private static void CheckUpdaterParsingAndVersionCompare()
        {
            if (!LaneUpdater.IsNewerVersion("v0.2.0", "v0.2.1")) throw new InvalidOperationException("v0.2.1 should be newer than v0.2.0.");
            if (!LaneUpdater.IsNewerVersion("v0.2.0", "v0.10.0")) throw new InvalidOperationException("v0.10.0 should be newer than v0.2.0.");
            if (!LaneUpdater.IsNewerVersion("v0.2.0", "v0.3")) throw new InvalidOperationException("v0.3 should be newer than v0.2.0.");
            if (LaneUpdater.IsNewerVersion("v0.2.0", "v0.2.0")) throw new InvalidOperationException("Same version must not be newer.");
            if (LaneUpdater.IsNewerVersion("v0.3.0", "v0.2.9")) throw new InvalidOperationException("v0.2.9 must not be newer than v0.3.0.");

            if (!LaneUpdater.IsAllowedDownloadUrl("https://github.com/MacquePanoramix/MOBA-Project/releases/download/v0.2.1/WoodlandLane-Windows.zip"))
                throw new InvalidOperationException("Valid github.com HTTPS release URL rejected.");
            if (!LaneUpdater.IsAllowedDownloadUrl("https://objects.githubusercontent.com/github-production-release-asset/foo.zip"))
                throw new InvalidOperationException("Valid githubusercontent.com HTTPS asset URL rejected.");
            if (LaneUpdater.IsAllowedDownloadUrl("http://github.com/foo.zip"))
                throw new InvalidOperationException("Insecure HTTP URL must be rejected.");
            if (LaneUpdater.IsAllowedDownloadUrl("https://evil.example.com/WoodlandLane-Windows.zip"))
                throw new InvalidOperationException("Non-GitHub URL must be rejected.");

            const string sampleJson =
                "{\"tag_name\":\"v0.2.1\",\"assets\":[{\"name\":\"notes.txt\",\"browser_download_url\":\"https://github.com/a/b\",\"size\":12}," +
                "{\"name\":\"WoodlandLane-Windows.zip\",\"browser_download_url\":\"https://github.com/MacquePanoramix/MOBA-Project/releases/download/v0.2.1/WoodlandLane-Windows.zip\",\"size\":37000000}]}";
            if (!LaneUpdater.ParseReleaseJson(sampleJson, "WoodlandLane-Windows.zip", out string tag, out string url, out long size))
                throw new InvalidOperationException("ParseReleaseJson failed on valid payload.");
            if (tag != "v0.2.1" || size != 37000000 || string.IsNullOrEmpty(url))
                throw new InvalidOperationException($"ParseReleaseJson mismatch (tag={tag}, size={size}).");

            string script = LaneUpdater.BuildApplyScript(1234, "C:\\Game's Dir", "C:\\Game's Dir\\_update_staging\\update.zip", "C:\\Game's Dir\\WoodlandLane.exe");
            if (script.IndexOf("1234", StringComparison.Ordinal) < 0 ||
                script.IndexOf("Expand-Archive", StringComparison.Ordinal) < 0 ||
                script.IndexOf("Game''s Dir", StringComparison.Ordinal) < 0 ||
                script.IndexOf("Start-Process", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("BuildApplyScript missing expected PID wait, Expand-Archive, quote escaping, or relaunch.");
            }
        }

        private static void CheckSettingsSurviveSimulatedUpdate()
        {
            if (LaneUpdater.BuildVersion != "v0.2.7")
            {
                throw new InvalidOperationException($"Expected BuildVersion v0.2.7, got {LaneUpdater.BuildVersion}");
            }
            var updaterCfg = new LaneUpdater.UpdaterConfig();
            if (updaterCfg.assetName != "WoodlandLane-Windows.zip")
            {
                throw new InvalidOperationException($"Updater assetName must remain WoodlandLane-Windows.zip for compatibility (got {updaterCfg.assetName}).");
            }

            string root = Path.Combine(Path.GetTempPath(), "MobaUpdatePersistCheck_" + Guid.NewGuid().ToString("N"));
            try
            {
                string persistentDir = Path.Combine(root, "PersistentData");
                string gameDir = Path.Combine(root, "GameInstall");
                string stagingExtracted = Path.Combine(gameDir, "_update_staging", "extracted");
                Directory.CreateDirectory(persistentDir);
                Directory.CreateDirectory(stagingExtracted);

                string settingsPath = Path.Combine(persistentDir, PlayerSettingsStore.DefaultFileName);
                var store = new PlayerSettingsStore(settingsPath, persistToDisk: true);
                store.SetOptionalRtsEnabled(true);
                store.TrySetBinding(ControlCommand.Stop, KeyCode.G, ConflictResolutionMode.SwapWithConflicting, out _);
                store.SetExtraPref("update_check_marker", "survives_overwrite");

                // Simulate old install overwritten by new update ZIP contents inside gameDir
                File.WriteAllText(Path.Combine(gameDir, "version.txt"), "v0.2.6");
                File.WriteAllText(Path.Combine(gameDir, "WoodlandLane.exe"), "old-exe");
                File.WriteAllText(Path.Combine(stagingExtracted, "version.txt"), LaneUpdater.BuildVersion);
                File.WriteAllText(Path.Combine(stagingExtracted, "WoodlandLane.exe"), "new-exe");

                File.Copy(Path.Combine(stagingExtracted, "version.txt"), Path.Combine(gameDir, "version.txt"), overwrite: true);
                File.Copy(Path.Combine(stagingExtracted, "WoodlandLane.exe"), Path.Combine(gameDir, "WoodlandLane.exe"), overwrite: true);
                Directory.Delete(Path.Combine(gameDir, "_update_staging"), recursive: true);

                var postUpdateStore = new PlayerSettingsStore(settingsPath, persistToDisk: false);
                postUpdateStore.Load();
                if (!postUpdateStore.Data.enableOptionalRtsControls ||
                    postUpdateStore.GetKey(ControlCommand.Stop) != KeyCode.G ||
                    postUpdateStore.GetExtraPref("update_check_marker") != "survives_overwrite")
                {
                    throw new InvalidOperationException("Personal settings did not survive simulated game directory update.");
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        private static void PumpBoth(ILaneLink a, ILaneLink b, int ms)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                a.Tick();
                b.Tick();
                Thread.Sleep(15);
            }
        }
    }
}
