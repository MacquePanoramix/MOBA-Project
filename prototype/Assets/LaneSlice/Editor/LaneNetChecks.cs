using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
            CheckUpdaterRealHttpAndRecoveryScenarios();
            CheckSettingsSurviveSimulatedUpdate();
            Debug.Log("[LaneNetChecks] All TCP loopback, UTP Fragmented+Reliable loopback, Relay, Static-Manifest Updater, and Update-Persistence checks passed.");
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
                guest.SendOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.BuyItemSmart, param = LaneItemIds.Heartstone });
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

                // Synthesize heavy multi-wave snapshot (~22+ KB JSON) including Mana resource telemetry to verify large frame delivery
                var heavyMatch = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, new LaneTuning { waveFirstSpawnDelay = 0.01f });
                heavyMatch.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q);
                heavyMatch.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.W);
                heavyMatch.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(5f, 0f) });
                for (int i = 0; i < 40; i++) heavyMatch.Tick(0.05f);
                LaneSnapshot big = heavyMatch.Snapshot();
                for (int extra = 0; extra < 120; extra++)
                {
                    big.minions.Add(new MinionSnap { id = 5000 + extra, team = LaneTeam.Blue, x = extra * 0.1f, y = 0.5f, hp = 200f, maxHp = 240f });
                }
                int jsonBytes = System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(big));
                if (jsonBytes < 18000 || jsonBytes >= LaneWireCodec.MaxFrameBytes)
                {
                    throw new InvalidOperationException($"Test heavy frame size out of bounds ({jsonBytes} vs max {LaneWireCodec.MaxFrameBytes}).");
                }

                host.SendSnapshot(big);
                PumpBoth(host, guest, 400);

                if (hostOrders.Count != 5 || hostOrders[0].kind != LaneOrderKind.BuyItem ||
                    hostOrders[1].kind != LaneOrderKind.BuyItemSmart ||
                    hostOrders[1].param != LaneItemIds.Heartstone ||
                    hostOrders[2].kind != LaneOrderKind.AttackMove ||
                    hostOrders[3].kind != LaneOrderKind.Move ||
                    hostOrders[3].unitIds == null || hostOrders[3].unitIds.Length != 2 ||
                    hostOrders[3].unitIds[0] != 2 || hostOrders[3].unitIds[1] != 105 ||
                    hostOrders[4].kind != LaneOrderKind.Stop)
                {
                    throw new InvalidOperationException($"Expected 5 remote orders including BuyItemSmart, RTS unitIds & Stop on host, got {hostOrders.Count}");
                }

                if (guestSnap == null || guestSnap.minions.Count != big.minions.Count ||
                    guestSnap.heroes.Count != 2 ||
                    guestSnap.heroes[0].resourceKind != HeroResourceKind.Mana ||
                    guestSnap.heroes[1].resourceKind != HeroResourceKind.Mana ||
                    Mathf.Abs(guestSnap.heroes[0].mana - big.heroes[0].mana) > 0.01f ||
                    Mathf.Abs(guestSnap.heroes[0].maxMana - big.heroes[0].maxMana) > 0.01f ||
                    Mathf.Abs(guestSnap.heroes[0].costQ - big.heroes[0].costQ) > 0.01f ||
                    guestSnap.heroes[1].costModeR != ResourceCostMode.ContinuousPerSecond ||
                    Mathf.Abs(guestSnap.heroes[0].deferredDamageTotal - big.heroes[0].deferredDamageTotal) > 0.01f ||
                    guestSnap.heroes[0].heatStacks != big.heroes[0].heatStacks ||
                    guestSnap.heroes[0].adaptiveScalesStacks != big.heroes[0].adaptiveScalesStacks ||
                    guestSnap.heroes[0].firstStrikeReady != big.heroes[0].firstStrikeReady ||
                    Mathf.Abs(guestSnap.heroes[0].slipperyBonus - big.heroes[0].slipperyBonus) > 0.01f)
                {
                    throw new InvalidOperationException($"Heavy snapshot / Mana resource sync roundtrip failed ({guestSnap?.minions.Count ?? -1} vs {big.minions.Count}).");
                }

                // Authoritative Host execution check for BuyItemSmart (440g Heartstone -> 2x Life Crystal synced back to Guest)
                var syncMatch = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f });
                syncMatch.SetHeroGoldForTest(LaneTeam.Red, 440);
                syncMatch.EnqueueOrApplyOrder(hostOrders[1]);
                LaneSnapshot syncedShopSnap = syncMatch.Snapshot();
                host.SendSnapshot(syncedShopSnap);
                PumpBoth(host, guest, 250);
                if (guestSnap == null || guestSnap.heroes.Count != 2 ||
                    guestSnap.heroes[1].items[0] != LaneItemIds.LifeCrystal ||
                    guestSnap.heroes[1].items[1] != LaneItemIds.LifeCrystal ||
                    guestSnap.heroes[1].gold != 0)
                {
                    throw new InvalidOperationException("Host authoritative BuyItemSmart snapshot did not sync 2x Life Crystal to Guest.");
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

                if (!sent || receivedSnap == null || receivedSnap.minions.Count != big.minions.Count ||
                    receivedSnap.heroes.Count != 2 ||
                    receivedSnap.heroes[0].resourceKind != HeroResourceKind.None ||
                    receivedSnap.heroes[1].resourceKind != HeroResourceKind.Mana ||
                    Mathf.Abs(receivedSnap.heroes[1].maxMana - big.heroes[1].maxMana) > 0.01f)
                {
                    throw new InvalidOperationException(
                        $"UTP Fragmented+Reliable loopback or resource sync failed (sent={sent}, minions={receivedSnap?.minions.Count ?? -1}/{big.minions.Count}).");
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
            if (!LaneUpdater.IsAllowedManifestUrl("https://macquepanoramix.github.io/MOBA-Project/latest.json"))
                throw new InvalidOperationException("Valid github.io HTTPS manifest URL rejected.");
            if (LaneUpdater.IsAllowedDownloadUrl("http://github.com/foo.zip"))
                throw new InvalidOperationException("Insecure HTTP URL must be rejected.");
            if (LaneUpdater.IsAllowedDownloadUrl("https://evil.example.com/WoodlandLane-Windows.zip"))
                throw new InvalidOperationException("Non-GitHub URL must be rejected.");
            if (LaneUpdater.IsAllowedDownloadUrl("https://evilgithub.com/foo.zip"))
                throw new InvalidOperationException("Lookalike non-GitHub domain must be rejected.");

            var prodEndpoints = new LaneUpdater(new LaneUpdater.UpdaterConfig()).BuildCheckEndpoints();
            if (prodEndpoints.Count < 3 ||
                prodEndpoints[0].IsGitHubApi || !prodEndpoints[0].Url.Contains("/releases/latest/download/latest.json") ||
                prodEndpoints[1].IsGitHubApi || !prodEndpoints[1].Url.Contains("raw.githubusercontent.com/") ||
                !prodEndpoints[2].IsGitHubApi || !prodEndpoints[2].Url.Contains("api.github.com/"))
            {
                throw new InvalidOperationException("Production endpoint order must prefer static manifests before optional API fallback.");
            }

            const string validManifest =
                "{\"schemaVersion\":1,\"version\":\"v0.2.9\",\"assetName\":\"WoodlandLane-Windows.zip\"," +
                "\"downloadUrl\":\"https://github.com/MacquePanoramix/MOBA-Project/releases/download/v0.2.9/WoodlandLane-Windows.zip\"," +
                "\"releaseUrl\":\"https://github.com/MacquePanoramix/MOBA-Project/releases/tag/v0.2.9\"," +
                "\"sizeBytes\":36900000,\"sha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"}";
            if (!LaneUpdater.ParseManifestJson(validManifest, "WoodlandLane-Windows.zip", out var parsedManifest, out string parseErr))
                throw new InvalidOperationException($"ParseManifestJson failed on valid payload: {parseErr}");
            if (parsedManifest.version != "v0.2.9" || parsedManifest.sizeBytes != 36900000)
                throw new InvalidOperationException("Parsed manifest fields mismatch.");

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
                script.IndexOf("$backupDir", StringComparison.Ordinal) < 0 ||
                script.IndexOf("last_update_error.txt", StringComparison.Ordinal) < 0 ||
                script.IndexOf("Game''s Dir", StringComparison.Ordinal) < 0 ||
                script.IndexOf("Start-Process", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("BuildApplyScript missing expected PID wait, backup/rollback, error marker, quote escaping, or relaunch.");
            }
        }

        private static void CheckUpdaterRealHttpAndRecoveryScenarios()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "MobaUpdaterHttpSuite_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            int port = FindFreeTcpPort();
            string baseUrl = $"http://127.0.0.1:{port}";
            Func<HttpListenerContext, int> handler = null;

            using (var server = new LocalHttpTestServer(port, ctx => handler(ctx)))
            {
                DateTime virtualNow = new DateTime(2026, 11, 25, 20, 0, 0, DateTimeKind.Utc);
                var client = new TestLoopbackHttpClient();

                List<LaneUpdater.CheckEndpoint> TestEndpoints(bool includeApiFallback = true)
                {
                    var list = new List<LaneUpdater.CheckEndpoint>
                    {
                        new LaneUpdater.CheckEndpoint($"{baseUrl}/releases/latest/download/latest.json", isGitHubApi: false),
                        new LaneUpdater.CheckEndpoint($"{baseUrl}/raw/latest.json", isGitHubApi: false),
                    };
                    if (includeApiFallback)
                    {
                        list.Add(new LaneUpdater.CheckEndpoint($"{baseUrl}/api/releases/latest", isGitHubApi: true));
                    }
                    return list;
                }

                // Prepare a valid zip package for download tests (v0.2.9)
                string validZipPath = Path.Combine(tempRoot, "server_v029.zip");
                CreateTestBuildZip(validZipPath, "v0.2.9", corruptEntry: false);
                byte[] validZipBytes = File.ReadAllBytes(validZipPath);
                string validSha = LaneUpdater.ComputeFileSha256(validZipPath);

                // Scenario 1: Normal static manifest check (API must NOT be contacted!)
                int staticHits = 0;
                int apiHits = 0;
                string servedManifest = BuildManifestJson("v0.2.10", $"{baseUrl}/download/v0.2.10/WoodlandLane-Windows.zip",
                    $"{baseUrl}/releases/tag/v0.2.10", validZipBytes.Length, validSha);

                handler = ctx =>
                {
                    if (ctx.Request.Url.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
                    {
                        apiHits++;
                        WriteResponse(ctx.Response, 500, "API should not be called when static manifest succeeds");
                        return 0;
                    }
                    staticHits++;
                    WriteResponse(ctx.Response, 200, servedManifest, "application/json");
                    return 0;
                };

                var normalUpd = new LaneUpdater(new LaneUpdater.UpdaterConfig(), client, allowLoopbackUrls: true,
                    utcNowProvider: () => virtualNow, overrideGameDir: Path.Combine(tempRoot, "game_norm"),
                    overrideCurrentVersion: "v0.2.9", overrideEndpoints: TestEndpoints());
                RunCoroutineSync(normalUpd.CheckForUpdatesCoroutine());
                if (!normalUpd.UpdateAvailable || normalUpd.LatestTag != "v0.2.10" || normalUpd.LatestSha256 != validSha ||
                    staticHits != 1 || apiHits != 0 || normalUpd.ShowBrowserFallback)
                {
                    throw new InvalidOperationException($"Scenario 1 (update available) failed: avail={normalUpd.UpdateAvailable}, tag={normalUpd.LatestTag}, staticHits={staticHits}, apiHits={apiHits}");
                }

                // Up-to-date check after debounce window
                virtualNow = virtualNow.AddSeconds(5);
                servedManifest = BuildManifestJson("v0.2.9", $"{baseUrl}/download/v0.2.9/WoodlandLane-Windows.zip",
                    $"{baseUrl}/releases/tag/v0.2.9", validZipBytes.Length, validSha);
                RunCoroutineSync(normalUpd.CheckForUpdatesCoroutine());
                if (normalUpd.UpdateAvailable || normalUpd.Status.IndexOf("Up to date", StringComparison.OrdinalIgnoreCase) < 0 || apiHits != 0)
                {
                    throw new InvalidOperationException($"Scenario 1 (up to date) failed: status='{normalUpd.Status}'");
                }

                // Scenario 2: GitHub API 403 rate-limit exhaustion + reset header + cooldown anti-spam + recovery after reset
                int totalHitsBeforeRateLimit = 0;
                long resetEpoch = new DateTimeOffset(virtualNow.AddSeconds(120)).ToUnixTimeSeconds();
                handler = ctx =>
                {
                    totalHitsBeforeRateLimit++;
                    if (ctx.Request.Url.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
                    {
                        ctx.Response.Headers["X-RateLimit-Remaining"] = "0";
                        ctx.Response.Headers["X-RateLimit-Reset"] = resetEpoch.ToString();
                        WriteResponse(ctx.Response, 403, "{\"message\":\"API rate limit exceeded\"}", "application/json");
                        return 0;
                    }
                    WriteResponse(ctx.Response, 404, "Not Found");
                    return 0;
                };

                var rateUpd = new LaneUpdater(new LaneUpdater.UpdaterConfig(), client, allowLoopbackUrls: true,
                    utcNowProvider: () => virtualNow, overrideGameDir: Path.Combine(tempRoot, "game_rate"),
                    overrideCurrentVersion: "v0.2.5", overrideEndpoints: TestEndpoints());
                RunCoroutineSync(rateUpd.CheckForUpdatesCoroutine());
                if (rateUpd.LastErrorKind != UpdateErrorKind.RateLimited || !rateUpd.ShowBrowserFallback || !rateUpd.IsInCooldown ||
                    rateUpd.Status.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) < 0 ||
                    rateUpd.GetCooldownRemainingSeconds() < 110)
                {
                    throw new InvalidOperationException($"Scenario 2 (403 rate limit) failed: kind={rateUpd.LastErrorKind}, status='{rateUpd.Status}'");
                }

                int hitsDuringCooldown = totalHitsBeforeRateLimit;
                RunCoroutineSync(rateUpd.CheckForUpdatesCoroutine());
                if (totalHitsBeforeRateLimit != hitsDuringCooldown || rateUpd.Status.IndexOf("paused", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new InvalidOperationException("Scenario 2 cooldown failed to suppress repeat HTTP requests.");
                }

                // Advance clock past reset -> HTTP 429 Retry-After test, then clean recovery
                virtualNow = virtualNow.AddSeconds(125);
                handler = ctx =>
                {
                    ctx.Response.Headers["Retry-After"] = "45";
                    WriteResponse(ctx.Response, 429, "Too Many Requests");
                    return 0;
                };
                RunCoroutineSync(rateUpd.CheckForUpdatesCoroutine());
                if (rateUpd.LastErrorKind != UpdateErrorKind.RateLimited || rateUpd.GetCooldownRemainingSeconds() < 40)
                {
                    throw new InvalidOperationException($"Scenario 2 (429 Retry-After) failed: kind={rateUpd.LastErrorKind}, rem={rateUpd.GetCooldownRemainingSeconds()}");
                }

                virtualNow = virtualNow.AddSeconds(50);
                handler = ctx =>
                {
                    WriteResponse(ctx.Response, 200, servedManifest, "application/json");
                    return 0;
                };
                RunCoroutineSync(rateUpd.CheckForUpdatesCoroutine());
                if (!rateUpd.UpdateAvailable || rateUpd.LastErrorKind != UpdateErrorKind.None || rateUpd.ShowBrowserFallback)
                {
                    throw new InvalidOperationException($"Scenario 2 recovery after reset failed: status='{rateUpd.Status}'");
                }

                // Scenario 3: Unavailable (404), malformed JSON, untrusted URL in manifest, and unreachable port
                var badCases = new[]
                {
                    "{ broken json !!!",
                    "{\"schemaVersion\":1,\"version\":\"not-a-semver\",\"downloadUrl\":\"https://github.com/a/b.zip\"}",
                    "{\"schemaVersion\":1,\"version\":\"v0.2.9\",\"downloadUrl\":\"https://evil.example.com/WoodlandLane-Windows.zip\"}",
                };
                for (int c = 0; c < badCases.Length; c++)
                {
                    string badPayload = badCases[c];
                    handler = ctx => { WriteResponse(ctx.Response, 200, badPayload, "application/json"); return 0; };
                    var malUpd = new LaneUpdater(new LaneUpdater.UpdaterConfig(), client, allowLoopbackUrls: true,
                        utcNowProvider: () => virtualNow, overrideGameDir: Path.Combine(tempRoot, "game_mal"),
                        overrideCurrentVersion: "v0.2.8", overrideEndpoints: TestEndpoints());
                    RunCoroutineSync(malUpd.CheckForUpdatesCoroutine());
                    if (malUpd.UpdateAvailable || malUpd.LastErrorKind != UpdateErrorKind.MalformedManifest || !malUpd.ShowBrowserFallback)
                    {
                        throw new InvalidOperationException($"Scenario 3 malformed case {c} not handled cleanly: kind={malUpd.LastErrorKind}, status='{malUpd.Status}'");
                    }
                }

                handler = ctx => { WriteResponse(ctx.Response, 404, "missing"); return 0; };
                var unavailUpd = new LaneUpdater(new LaneUpdater.UpdaterConfig(), client, allowLoopbackUrls: true,
                    utcNowProvider: () => virtualNow, overrideGameDir: Path.Combine(tempRoot, "game_404"),
                    overrideCurrentVersion: "v0.2.8", overrideEndpoints: TestEndpoints(includeApiFallback: false));
                RunCoroutineSync(unavailUpd.CheckForUpdatesCoroutine());
                if (unavailUpd.LastErrorKind != UpdateErrorKind.Unavailable || !unavailUpd.ShowBrowserFallback || !unavailUpd.IsInCooldown)
                {
                    throw new InvalidOperationException($"Scenario 3 unavailable 404 check failed: kind={unavailUpd.LastErrorKind}");
                }

                int deadPort = FindFreeTcpPort();
                var deadUpd = new LaneUpdater(new LaneUpdater.UpdaterConfig(), client, allowLoopbackUrls: true,
                    utcNowProvider: () => virtualNow, overrideGameDir: Path.Combine(tempRoot, "game_net"),
                    overrideCurrentVersion: "v0.2.8",
                    overrideEndpoints: new[] { new LaneUpdater.CheckEndpoint($"http://127.0.0.1:{deadPort}/latest.json", false) });
                RunCoroutineSync(deadUpd.CheckForUpdatesCoroutine());
                if (deadUpd.LastErrorKind != UpdateErrorKind.Network || !deadUpd.ShowBrowserFallback)
                {
                    throw new InvalidOperationException($"Scenario 3 unreachable port failed: kind={deadUpd.LastErrorKind}, status='{deadUpd.Status}'");
                }

                // Scenario 4: Interrupted / truncated / wrong SHA / corrupt ZIP downloads leave game directory untouched
                string dlGameDir = Path.Combine(tempRoot, "game_dl");
                Directory.CreateDirectory(dlGameDir);
                string exeMarkerPath = Path.Combine(dlGameDir, "WoodlandLane.exe");
                File.WriteAllText(exeMarkerPath, "ORIGINAL_V025_BINARY");
                File.WriteAllText(Path.Combine(dlGameDir, "version.txt"), "v0.2.5");

                // 4a: HTTP error interrupting download
                string dlUrl = $"{baseUrl}/download/v0.2.9/WoodlandLane-Windows.zip";
                string dlManifest = BuildManifestJson("v0.2.9", dlUrl, $"{baseUrl}/releases/tag/v0.2.9", validZipBytes.Length, validSha);
                handler = ctx =>
                {
                    if (ctx.Request.Url.AbsolutePath.EndsWith("latest.json", StringComparison.Ordinal))
                        WriteResponse(ctx.Response, 200, dlManifest, "application/json");
                    else
                        WriteResponse(ctx.Response, 503, "CDN Reset");
                    return 0;
                };
                var dlUpd = new LaneUpdater(new LaneUpdater.UpdaterConfig(), client, allowLoopbackUrls: true,
                    utcNowProvider: () => virtualNow, overrideGameDir: dlGameDir,
                    overrideCurrentVersion: "v0.2.5", overrideEndpoints: TestEndpoints());
                RunCoroutineSync(dlUpd.CheckForUpdatesCoroutine());
                RunCoroutineSync(dlUpd.DownloadAndApplyCoroutine());
                if (dlUpd.LastErrorKind != UpdateErrorKind.DownloadFailed || !dlUpd.UpdateAvailable || !dlUpd.ShowBrowserFallback ||
                    File.Exists(Path.Combine(dlGameDir, "_update_staging", "update.zip.part")) ||
                    File.ReadAllText(exeMarkerPath) != "ORIGINAL_V025_BINARY")
                {
                    throw new InvalidOperationException($"Scenario 4a (interrupted download) failed: kind={dlUpd.LastErrorKind}, status='{dlUpd.Status}'");
                }

                // 4b: Truncated ZIP bytes
                byte[] truncatedZip = new byte[Math.Min(1600, Math.Max(1100, validZipBytes.Length / 2))];
                Array.Copy(validZipBytes, truncatedZip, truncatedZip.Length);
                handler = ctx =>
                {
                    WriteBinaryResponse(ctx.Response, 200, truncatedZip);
                    return 0;
                };
                RunCoroutineSync(dlUpd.DownloadAndApplyCoroutine());
                if (dlUpd.LastErrorKind != UpdateErrorKind.VerificationFailed || !dlUpd.UpdateAvailable ||
                    File.ReadAllText(exeMarkerPath) != "ORIGINAL_V025_BINARY")
                {
                    throw new InvalidOperationException($"Scenario 4b (truncated zip) failed: kind={dlUpd.LastErrorKind}");
                }

                // 4c: Wrong SHA-256 even when ZIP itself is readable
                string wrongSha = new string('a', 64);
                string wrongShaManifest = BuildManifestJson("v0.2.9", dlUrl, $"{baseUrl}/releases/tag/v0.2.9", validZipBytes.Length, wrongSha);
                virtualNow = virtualNow.AddSeconds(10);
                handler = ctx =>
                {
                    if (ctx.Request.Url.AbsolutePath.EndsWith("latest.json", StringComparison.Ordinal))
                        WriteResponse(ctx.Response, 200, wrongShaManifest, "application/json");
                    else
                        WriteBinaryResponse(ctx.Response, 200, validZipBytes);
                    return 0;
                };
                RunCoroutineSync(dlUpd.CheckForUpdatesCoroutine());
                RunCoroutineSync(dlUpd.DownloadAndApplyCoroutine());
                if (dlUpd.LastErrorKind != UpdateErrorKind.VerificationFailed || dlUpd.Status.IndexOf("SHA-256", StringComparison.OrdinalIgnoreCase) < 0 ||
                    File.ReadAllText(exeMarkerPath) != "ORIGINAL_V025_BINARY")
                {
                    throw new InvalidOperationException($"Scenario 4c (wrong SHA-256) failed: status='{dlUpd.Status}'");
                }

                // 4d: Corrupt member entry inside ZIP passes sizeBytes & SHA but fails full entry CRC read
                string corruptZipPath = Path.Combine(tempRoot, "server_corrupt.zip");
                CreateTestBuildZip(corruptZipPath, "v0.2.9", corruptEntry: true);
                byte[] corruptBytes = File.ReadAllBytes(corruptZipPath);
                string corruptSha = LaneUpdater.ComputeFileSha256(corruptZipPath);
                if (LaneUpdater.ValidateDownloadedZip(corruptZipPath, corruptBytes.Length, corruptSha, out _))
                {
                    throw new InvalidOperationException("ValidateDownloadedZip must reject internally corrupted ZIP member.");
                }

                // Scenario 5 & 6: Successful update from v0.2.5 -> v0.2.9 preserving separate personal_settings.json
                string persistentDir = Path.Combine(tempRoot, "persistent_v025");
                Directory.CreateDirectory(persistentDir);
                string settingsPath = Path.Combine(persistentDir, PlayerSettingsStore.DefaultFileName);
                var settingsStore = new PlayerSettingsStore(settingsPath, persistToDisk: true);
                settingsStore.SetOptionalRtsEnabled(true);
                settingsStore.TrySetBinding(ControlCommand.Stop, KeyCode.G, ConflictResolutionMode.SwapWithConflicting, out _);
                settingsStore.SetExtraPref("v025_migrated", "yes");

                virtualNow = virtualNow.AddSeconds(10);
                handler = ctx =>
                {
                    if (ctx.Request.Url.AbsolutePath.EndsWith("latest.json", StringComparison.Ordinal))
                        WriteResponse(ctx.Response, 200, dlManifest, "application/json");
                    else
                        WriteBinaryResponse(ctx.Response, 200, validZipBytes);
                    return 0;
                };
                RunCoroutineSync(dlUpd.CheckForUpdatesCoroutine());
                RunCoroutineSync(dlUpd.DownloadAndApplyCoroutine());
                string stagedZip = Path.Combine(dlGameDir, "_update_staging", "update.zip");
                string stagedScript = Path.Combine(dlGameDir, "_update_staging", "apply_update.ps1");
                if (!dlUpd.ApplyScriptLaunchedForTest || !File.Exists(stagedZip) || !File.Exists(stagedScript))
                {
                    throw new InvalidOperationException($"Scenario 5 staging failed: launched={dlUpd.ApplyScriptLaunchedForTest}, status='{dlUpd.Status}'");
                }

                // Extract staged zip cleanly as apply_update.ps1 does and verify target install + settings survival
                string extractDir = Path.Combine(dlGameDir, "_update_staging", "extracted");
                ZipFileExtractSafe(stagedZip, extractDir);
                foreach (string srcFile in Directory.GetFiles(extractDir))
                {
                    File.Copy(srcFile, Path.Combine(dlGameDir, Path.GetFileName(srcFile)), overwrite: true);
                }
                if (File.ReadAllText(Path.Combine(dlGameDir, "version.txt")).Trim() != "v0.2.9")
                {
                    throw new InvalidOperationException("Scenario 5 updated version.txt mismatch.");
                }
                var postUpgradeSettings = new PlayerSettingsStore(settingsPath, persistToDisk: false);
                postUpgradeSettings.Load();
                if (!postUpgradeSettings.Data.enableOptionalRtsControls ||
                    postUpgradeSettings.GetKey(ControlCommand.Stop) != KeyCode.G ||
                    postUpgradeSettings.GetExtraPref("v025_migrated") != "yes")
                {
                    throw new InvalidOperationException("Scenario 6 personal settings did not survive v0.2.5 -> v0.2.9 upgrade.");
                }

                // Scenario 7: Recovery from unsuccessful apply attempt + subsequent clean check
                string recGameDir = Path.Combine(tempRoot, "game_recover");
                string recStaging = Path.Combine(recGameDir, "_update_staging");
                Directory.CreateDirectory(recStaging);
                File.WriteAllText(Path.Combine(recGameDir, "WoodlandLane.exe"), "PRESERVED_BUILD");
                File.WriteAllText(Path.Combine(recStaging, "last_update_error.txt"), "Access denied to WoodlandLane.exe");

                var recUpd = new LaneUpdater(new LaneUpdater.UpdaterConfig(), client, allowLoopbackUrls: true,
                    utcNowProvider: () => virtualNow, overrideGameDir: recGameDir,
                    overrideCurrentVersion: "v0.2.8", overrideEndpoints: TestEndpoints());
                recUpd.RecoverFromPreviousUpdateIfNeeded(forceInTest: true);
                if (recUpd.LastErrorKind != UpdateErrorKind.ApplyFailed || !recUpd.ShowBrowserFallback ||
                    recUpd.Status.IndexOf("Previous update did not complete", StringComparison.OrdinalIgnoreCase) < 0 ||
                    Directory.Exists(recStaging) ||
                    File.ReadAllText(Path.Combine(recGameDir, "WoodlandLane.exe")) != "PRESERVED_BUILD")
                {
                    throw new InvalidOperationException($"Scenario 7 recovery failed: kind={recUpd.LastErrorKind}, status='{recUpd.Status}'");
                }

                // Subsequent normal check succeeds after recovery and settings remain intact
                RunCoroutineSync(recUpd.CheckForUpdatesCoroutine());
                if (!recUpd.UpdateAvailable || recUpd.LatestTag != "v0.2.9" || recUpd.ShowBrowserFallback)
                {
                    throw new InvalidOperationException($"Scenario 7 subsequent check failed: status='{recUpd.Status}'");
                }
                postUpgradeSettings.Load();
                if (postUpgradeSettings.GetKey(ControlCommand.Stop) != KeyCode.G)
                {
                    throw new InvalidOperationException("Settings corrupted during recovery check.");
                }
            }

            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }

        private static string BuildManifestJson(string version, string downloadUrl, string releaseUrl, long sizeBytes, string sha256)
        {
            return $"{{\"schemaVersion\":1,\"version\":\"{version}\",\"assetName\":\"WoodlandLane-Windows.zip\"," +
                   $"\"downloadUrl\":\"{downloadUrl}\",\"releaseUrl\":\"{releaseUrl}\"," +
                   $"\"sizeBytes\":{sizeBytes},\"sha256\":\"{sha256}\",\"publishedAtUtc\":\"2026-11-25T20:00:00Z\",\"notes\":\"Test {version}\"}}";
        }

        private static void CreateTestBuildZip(string zipPath, string version, bool corruptEntry)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (FileStream fs = File.Create(zipPath))
            using (var za = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
            {
                var pseudoExe = new byte[4096];
                for (int i = 0; i < pseudoExe.Length; i++)
                {
                    pseudoExe[i] = (byte)(((i * 73 + 19) ^ (i >> 3) ^ (i * i)) & 0xFF);
                }
                WriteZipEntry(za, "WoodlandLane.exe", pseudoExe);
                WriteZipEntry(za, "version.txt", Encoding.UTF8.GetBytes(version));
                WriteZipEntry(za, "updater_config.json", Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LaneUpdater.UpdaterConfig(), true)));
            }

            if (corruptEntry)
            {
                // Corrupt DEFLATE stream payload inside the first entry without altering total length
                byte[] raw = File.ReadAllBytes(zipPath);
                for (int k = 55; k < Math.Min(raw.Length - 4, 95); k++)
                {
                    raw[k] ^= 0xFF;
                }
                File.WriteAllBytes(zipPath, raw);
            }
        }

        private static void WriteZipEntry(ZipArchive za, string name, byte[] bytes)
        {
            ZipArchiveEntry e = za.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            using (Stream s = e.Open()) s.Write(bytes, 0, bytes.Length);
        }

        private static void ZipFileExtractSafe(string zipPath, string extractDir)
        {
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
            Directory.CreateDirectory(extractDir);
            using (FileStream fs = File.OpenRead(zipPath))
            using (var za = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                for (int i = 0; i < za.Entries.Count; i++)
                {
                    ZipArchiveEntry e = za.Entries[i];
                    string outPath = Path.Combine(extractDir, e.FullName);
                    string dir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    if (!e.FullName.EndsWith("/", StringComparison.Ordinal))
                    {
                        using (Stream src = e.Open())
                        using (FileStream dst = File.Create(outPath))
                        {
                            src.CopyTo(dst);
                        }
                    }
                }
            }
        }

        private static void RunCoroutineSync(IEnumerator routine)
        {
            while (routine != null && routine.MoveNext())
            {
                Thread.Sleep(5);
            }
        }

        private static int FindFreeTcpPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private static void WriteResponse(HttpListenerResponse resp, int statusCode, string body, string contentType = "text/plain")
        {
            byte[] data = Encoding.UTF8.GetBytes(body ?? string.Empty);
            resp.StatusCode = statusCode;
            resp.ContentType = contentType;
            resp.ContentLength64 = data.Length;
            resp.OutputStream.Write(data, 0, data.Length);
            resp.OutputStream.Close();
        }

        private static void WriteBinaryResponse(HttpListenerResponse resp, int statusCode, byte[] data)
        {
            resp.StatusCode = statusCode;
            resp.ContentType = "application/zip";
            resp.ContentLength64 = data.Length;
            resp.OutputStream.Write(data, 0, data.Length);
            resp.OutputStream.Close();
        }

        private sealed class LocalHttpTestServer : IDisposable
        {
            private readonly HttpListener listener;
            private readonly Thread thread;
            private volatile bool running = true;

            public LocalHttpTestServer(int port, Func<HttpListenerContext, int> handler)
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
                thread = new Thread(() =>
                {
                    while (running)
                    {
                        try
                        {
                            HttpListenerContext ctx = listener.GetContext();
                            handler?.Invoke(ctx);
                        }
                        catch
                        {
                            if (!running) break;
                        }
                    }
                }) { IsBackground = true };
                thread.Start();
            }

            public void Dispose()
            {
                running = false;
                try { listener.Stop(); } catch { }
                try { listener.Close(); } catch { }
            }
        }

        private sealed class TestLoopbackHttpClient : IUpdateHttpClient
        {
            private sealed class LoopbackOp : IUpdateHttpOp
            {
                public bool IsDone { get; private set; }
                public bool IsSuccess { get; private set; }
                public bool IsNetworkError { get; private set; }
                public long StatusCode { get; private set; }
                public string Error { get; private set; } = string.Empty;
                public string Text { get; private set; } = string.Empty;
                public float Progress { get; private set; }
                public IDictionary<string, string> ResponseHeaders { get; private set; }

                public LoopbackOp(string url, string destinationPath)
                {
                    var t = new Thread(() => Execute(url, destinationPath)) { IsBackground = true };
                    t.Start();
                }

                private void Execute(string url, string destinationPath)
                {
                    try
                    {
                        var req = (HttpWebRequest)WebRequest.Create(url);
                        req.Method = "GET";
                        req.Timeout = 5000;
                        req.ReadWriteTimeout = 5000;
                        using (var resp = (HttpWebResponse)req.GetResponse())
                        {
                            StatusCode = (long)resp.StatusCode;
                            ResponseHeaders = CopyHeaders(resp.Headers);
                            if (destinationPath == null)
                            {
                                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                                {
                                    Text = sr.ReadToEnd();
                                }
                            }
                            else
                            {
                                using (Stream rs = resp.GetResponseStream())
                                using (FileStream fs = File.Create(destinationPath))
                                {
                                    rs.CopyTo(fs);
                                }
                            }
                            Progress = 1f;
                            IsSuccess = true;
                        }
                    }
                    catch (WebException wex)
                    {
                        if (wex.Response is HttpWebResponse errResp)
                        {
                            StatusCode = (long)errResp.StatusCode;
                            ResponseHeaders = CopyHeaders(errResp.Headers);
                            try
                            {
                                using (var sr = new StreamReader(errResp.GetResponseStream(), Encoding.UTF8))
                                {
                                    Text = sr.ReadToEnd();
                                }
                            }
                            catch { }
                            Error = errResp.StatusDescription ?? wex.Message;
                            errResp.Close();
                        }
                        else
                        {
                            IsNetworkError = true;
                            Error = wex.Message;
                        }
                    }
                    catch (Exception ex)
                    {
                        IsNetworkError = true;
                        Error = ex.Message;
                    }
                    finally
                    {
                        IsDone = true;
                    }
                }

                private static Dictionary<string, string> CopyHeaders(WebHeaderCollection headers)
                {
                    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (headers == null) return d;
                    for (int i = 0; i < headers.Count; i++) d[headers.GetKey(i)] = headers.Get(i);
                    return d;
                }

                public void Dispose() { }
            }

            public IUpdateHttpOp GetText(string url) => new LoopbackOp(url, null);
            public IUpdateHttpOp DownloadFile(string url, string destinationPath) => new LoopbackOp(url, destinationPath);
        }

        private static void CheckSettingsSurviveSimulatedUpdate()
        {
            if (LaneUpdater.BuildVersion != "v0.3.1")
            {
                throw new InvalidOperationException($"Expected BuildVersion v0.3.1, got {LaneUpdater.BuildVersion}");
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
                File.WriteAllText(Path.Combine(gameDir, "version.txt"), "v0.2.8");
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
