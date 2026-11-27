using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Entry scene controller for WoodlandLane (Milestone 3+4).
    /// Orchestrates lobby setup, 30 Hz authoritative simulation, Unity Relay / TCP links,
    /// MOBA quick-cast + Attack-Move controls, and CLI smoke-test hooks.
    /// </summary>
    public sealed class LaneScene : MonoBehaviour
    {
        public LaneTuning tuning = new LaneTuning();

        private enum SessionMode
        {
            Lobby = 0,
            Practice = 1,
            Host = 2,
            Guest = 3,
        }

        private SessionMode mode = SessionMode.Lobby;
        private LaneHeroId localPick = LaneHeroId.Ogre;
        private LaneHeroId botOrRemotePick = LaneHeroId.Miravel;
        private LaneTeam localTeam = LaneTeam.Blue;

        private LaneMatch match;
        private LaneBot bot;
        private ILaneLink link;
        private LaneWorld world;
        private LaneHud hud;
        private LaneUpdater updater;
        private LaneAbilityPreview abilityPreview;
        private LaneSnapshot latestSnap;
        private Camera mainCam;

        private float simAccum;
        private int hoveredEntityId;
        private string cliStatus = string.Empty;
        private bool hostStartSent;
        private bool headlessMode;
        private string perfBenchOutPath;
        private string uiShotsOutDir;
        private bool benchMeasureActive;
        private float benchUpdateMsAccum;
        private float benchOnGuiMsAccum;
        private readonly Stopwatch benchSw = new Stopwatch();

        private PlayerSettingsStore settingsStore;
        private readonly LaneRtsSelection rtsSelection = new LaneRtsSelection();
        private readonly List<ControllableUnitInfo> scratchAuthorized = new List<ControllableUnitInfo>();
        private bool draggingSelectionBox;
        private Vector2 dragStartScreenPos;
        private Vector2 dragCurrentScreenPos;

        public LaneHeroId LocalPick => localPick;
        public LaneHeroId RemoteOrBotPick => botOrRemotePick;
        public LaneHeroId ActiveHeroId
        {
            get
            {
                HeroSnap s = latestSnap != null ? FindHero(latestSnap, localTeam) : null;
                return s != null ? s.heroId : localPick;
            }
        }
        public HeroControlProfile ActiveControlProfile => HeroControlCatalog.ForHero(ActiveHeroId);
        public PlayerSettingsStore Settings => settingsStore;
        public LaneRtsSelection RtsSelection => rtsSelection;
        public bool IsRtsActiveForCurrentHero => ActiveControlProfile.IsRtsEnabled(settingsStore?.Data);
        public bool IsRtsDragBoxActive => IsRtsActiveForCurrentHero && draggingSelectionBox && (dragCurrentScreenPos - dragStartScreenPos).magnitude >= 6f;
        public Rect RtsDragBoxGuiRect
        {
            get
            {
                float xMin = Mathf.Min(dragStartScreenPos.x, dragCurrentScreenPos.x);
                float xMax = Mathf.Max(dragStartScreenPos.x, dragCurrentScreenPos.x);
                float yTop = Screen.height - Mathf.Max(dragStartScreenPos.y, dragCurrentScreenPos.y);
                float yBot = Screen.height - Mathf.Min(dragStartScreenPos.y, dragCurrentScreenPos.y);
                return Rect.MinMaxRect(xMin, yTop, xMax, yBot);
            }
        }
        public bool IsInMatch => mode != SessionMode.Lobby;
        public bool IsHostOrPractice => mode == SessionMode.Practice || mode == SessionMode.Host;
        public ILaneLink ActiveLink => link;
        public LaneUpdater Updater => updater;
        public LaneAbilityPreview AbilityPreview => abilityPreview;
        public string StatusBanner => link != null ? link.StatusText : cliStatus;

        private void Start()
        {
            headlessMode = Application.isBatchMode;
            Application.runInBackground = true;
            updater = new LaneUpdater();
            updater.RecoverFromPreviousUpdateIfNeeded();
            settingsStore = new PlayerSettingsStore(persistToDisk: !headlessMode);
            settingsStore.Load();

            mainCam = Camera.main;
            if (mainCam == null)
            {
                var camGo = new GameObject("Main Camera");
                mainCam = camGo.AddComponent<Camera>();
                mainCam.tag = "MainCamera";
            }
            mainCam.backgroundColor = new Color(0.11f, 0.14f, 0.12f);
            mainCam.fieldOfView = 45f;

            var map = new LaneMap(tuning);
            world = new LaneWorld(map);
            hud = new LaneHud(this, tuning);
            if (!headlessMode)
            {
                abilityPreview = new LaneAbilityPreview(tuning, enableRenderVisuals: true);
            }

            ParseCliArgs();
        }

        private void ParseCliArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-laneHost", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    TcpLaneLink.ParseEndpoint(args[i + 1], 47777, out string host, out int port);
                    StartHostTcp(host, port);
                    return;
                }
                if (string.Equals(args[i], "-laneJoin", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    JoinTcp(args[i + 1]);
                    return;
                }
                if (string.Equals(args[i], "-perfBench", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    perfBenchOutPath = args[i + 1];
                }
                else if (string.Equals(args[i], "-uiShots", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    uiShotsOutDir = args[i + 1];
                }
            }

            if (!string.IsNullOrEmpty(perfBenchOutPath) && !headlessMode)
            {
                StartCoroutine(RunPerfBenchRoutine(perfBenchOutPath));
            }
            else if (!string.IsNullOrEmpty(uiShotsOutDir) && !headlessMode)
            {
                StartCoroutine(RunUiShotsRoutine(uiShotsOutDir));
            }
        }

        public void SetLocalPick(LaneHeroId hero)
        {
            localPick = hero;
            if (link != null && link.IsConnected)
            {
                link.SendPick(hero);
            }
        }

        public void SetBotHero(LaneHeroId hero)
        {
            botOrRemotePick = hero;
        }

        public void StartPractice()
        {
            CleanupLink();
            mode = SessionMode.Practice;
            localTeam = LaneTeam.Blue;
            latestSnap = null;
            simAccum = 0f;

            match = new LaneMatch(localPick, botOrRemotePick, tuning);
            bot = new LaneBot(LaneTeam.Red);
            rtsSelection.Reset();
            draggingSelectionBox = false;
            hud.SettingsOpen = false;
            hud.ItemCompendiumOpen = false;
            world.ResetVisuals();

            LaneSnapshot snap = match.Snapshot();
            ApplyLocalSnapshot(snap);
            world.CenterCameraOnHero(FindHero(snap, localTeam));
        }

        public void StartHostRelay()
        {
            CleanupLink();
            link = RelayLaneLink.HostRelay();
            HookLink(isHost: true);
        }

        public void JoinRelay(string code)
        {
            CleanupLink();
            link = RelayLaneLink.JoinRelay(code);
            HookLink(isHost: false);
        }

        public void StartHostTcp(string bindHost = "0.0.0.0", int port = 47777)
        {
            CleanupLink();
            link = TcpLaneLink.Host(bindHost, port);
            HookLink(isHost: true);
        }

        public void JoinTcp(string endpoint)
        {
            CleanupLink();
            link = TcpLaneLink.Connect(endpoint, 47777);
            HookLink(isHost: false);
        }

        public void ReturnToLobby()
        {
            CleanupLink();
            mode = SessionMode.Lobby;
            match = null;
            bot = null;
            latestSnap = null;
            hud.ShopOpen = false;
            hud.HeroSheetOpen = false;
            hud.ItemCompendiumOpen = false;
            hud.SettingsOpen = false;
            rtsSelection.Reset();
            draggingSelectionBox = false;
            world.ResetVisuals();
        }

        public void Rematch()
        {
            if (mode == SessionMode.Practice)
            {
                StartPractice();
            }
            else if (mode == SessionMode.Host && link != null && link.IsConnected)
            {
                BeginHostMatchNow();
            }
        }

        public void SendLocalAbilityLevelUp(LaneAbilitySlot slot)
        {
            if (mode == SessionMode.Lobby) return;
            IssueLocalOrder(new LaneOrder
            {
                team = localTeam,
                kind = LaneOrderKind.LevelAbility,
                param = (int)slot,
            });
        }

        public void SendLocalBuyItem(int itemId)
        {
            if (mode == SessionMode.Lobby) return;
            IssueLocalOrder(new LaneOrder
            {
                team = localTeam,
                kind = LaneOrderKind.BuyItem,
                param = itemId,
            });
        }

        public void SendLocalBuyItemSmart(int itemId)
        {
            if (mode == SessionMode.Lobby) return;
            IssueLocalOrder(new LaneOrder
            {
                team = localTeam,
                kind = LaneOrderKind.BuyItemSmart,
                param = itemId,
            });
        }

        public void SendLocalSellItem(int slotIndex)
        {
            if (mode == SessionMode.Lobby) return;
            IssueLocalOrder(new LaneOrder
            {
                team = localTeam,
                kind = LaneOrderKind.SellItem,
                param = slotIndex,
            });
        }

        public void ToggleCameraLock()
        {
            world.CameraLocked = !world.CameraLocked;
        }

        public void CheckForUpdates()
        {
            if (updater != null && !updater.IsBusy)
            {
                StartCoroutine(updater.CheckForUpdatesCoroutine());
            }
        }

        public void DownloadAndApplyUpdate()
        {
            if (updater != null && !updater.IsBusy && updater.UpdateAvailable)
            {
                StartCoroutine(updater.DownloadAndApplyCoroutine());
            }
        }

        public void OpenReleasesInBrowser()
        {
            updater?.OpenReleasePageInBrowser();
        }

        private void HookLink(bool isHost)
        {
            hostStartSent = false;
            localTeam = isHost ? LaneTeam.Blue : LaneTeam.Red;

            link.RemotePickChanged += hero =>
            {
                botOrRemotePick = hero;
            };

            link.StartReceived += (hostHero, guestHero, syncedTuning) =>
            {
                if (isHost) return;
                tuning = syncedTuning ?? tuning;
                mode = SessionMode.Guest;
                localTeam = LaneTeam.Red;
                latestSnap = null;
                rtsSelection.Reset();
                draggingSelectionBox = false;
                hud.SettingsOpen = false;
                hud.ItemCompendiumOpen = false;
                world.ResetVisuals();
                cliStatus = $"Guest match started ({LaneNames.ShortName(guestHero)} vs {LaneNames.ShortName(hostHero)})";
                Debug.Log($"Lane match received (Guest, {LaneNames.ShortName(guestHero)} vs {LaneNames.ShortName(hostHero)}).");
            };

            link.OrderReceived += order =>
            {
                if (mode == SessionMode.Host && match != null)
                {
                    order.team = LaneTeam.Red; // Host authoritative enforcement
                    match.EnqueueOrApplyOrder(order);
                }
            };

            link.SnapshotReceived += snap =>
            {
                if (mode == SessionMode.Guest)
                {
                    bool first = latestSnap == null;
                    ApplyLocalSnapshot(snap);
                    if (first) world.CenterCameraOnHero(FindHero(snap, localTeam));
                }
            };

            link.Disconnected += reason =>
            {
                cliStatus = reason;
                if (mode == SessionMode.Host || mode == SessionMode.Guest)
                {
                    ReturnToLobby();
                }
            };
        }

        private void BeginHostMatchNow()
        {
            mode = SessionMode.Host;
            localTeam = LaneTeam.Blue;
            simAccum = 0f;
            latestSnap = null;
            match = new LaneMatch(localPick, botOrRemotePick, tuning);
            bot = null;
            rtsSelection.Reset();
            draggingSelectionBox = false;
            hud.SettingsOpen = false;
            hud.ItemCompendiumOpen = false;
            world.ResetVisuals();

            link.SendStart(localPick, botOrRemotePick, tuning);
            LaneSnapshot snap = match.Snapshot();
            ApplyLocalSnapshot(snap);
            link.SendSnapshot(snap);
            world.CenterCameraOnHero(FindHero(snap, localTeam));

            Debug.Log($"Lane match started (Host, {LaneNames.ShortName(localPick)} vs {LaneNames.ShortName(botOrRemotePick)}).");
        }

        private void CleanupLink()
        {
            if (link != null)
            {
                link.Dispose();
                link = null;
            }
            hostStartSent = false;
        }

        private void Update()
        {
            if (benchMeasureActive) benchSw.Restart();
            if (link != null)
            {
                link.Tick();

                if (link != null && mode == SessionMode.Lobby && link.IsConnected && !link.IsHost)
                {
                    // Guest announces current pick upon connect
                    if (!hostStartSent)
                    {
                        hostStartSent = true;
                        link.SendPick(localPick);
                    }
                }
                else if (link != null && mode == SessionMode.Lobby && link.IsConnected && link.IsHost && !hostStartSent)
                {
                    // Auto-start when headless CLI -laneHost waits for peer, or host button triggers interactively
                    if (headlessMode)
                    {
                        hostStartSent = true;
                        BeginHostMatchNow();
                    }
                }
            }

            if (mode == SessionMode.Practice || mode == SessionMode.Host)
            {
                float dt = Mathf.Min(0.1f, Time.deltaTime);
                simAccum += dt;
                float step = 1f / Mathf.Max(15f, tuning.tickHz);
                int guard = 0;
                while (simAccum >= step && guard < 4)
                {
                    simAccum -= step;
                    guard++;
                    if (mode == SessionMode.Practice) bot?.Tick(match, step);
                    match.Tick(step);
                    LaneSnapshot snap = match.Snapshot();
                    ApplyLocalSnapshot(snap);
                    if (mode == SessionMode.Host && link != null && link.IsConnected)
                    {
                        link.SendSnapshot(snap);
                    }
                }
            }

            if (!headlessMode && !hud.IsCapturingKeybind)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (hud.IsTextInputFocused) hud.RequestBlurTextInput();
                    else if (hud.SettingsOpen) hud.SettingsOpen = false;
                    else if (hud.HeroSheetOpen) hud.HeroSheetOpen = false;
                    else if (hud.ItemCompendiumOpen) hud.ItemCompendiumOpen = false;
                    else if (hud.ShopOpen) hud.ShopOpen = false;
                    else if (hud.ShowHelp) hud.ShowHelp = false;
                    else if (mode != SessionMode.Lobby) hud.SettingsOpen = true;
                }
                else if (mode == SessionMode.Lobby && !hud.IsTextInputFocused &&
                         WasCommandPressed(ControlCommand.ToggleSettings, checkAvailable: false))
                {
                    hud.SettingsOpen = !hud.SettingsOpen;
                }
            }

            if (mode != SessionMode.Lobby && !headlessMode)
            {
                if (!hud.IsCapturingKeybind)
                {
                    HandleMatchInput();
                }
                world.TickFrame(mainCam, Time.deltaTime);
                hud.TickPopups(Time.deltaTime);
            }

            if (abilityPreview != null)
            {
                bool runPreview = !headlessMode && hud != null && !hud.SettingsOpen &&
                                  (settingsStore == null || settingsStore.Data.enableAbilityDemos) &&
                                  (hud.HeroSheetOpen || (hud.ItemCompendiumOpen && hud.SelectedCompendiumItemHasPassive));
                abilityPreview.Tick(Time.deltaTime, runPreview);
            }
            if (benchMeasureActive)
            {
                benchSw.Stop();
                benchUpdateMsAccum += (float)benchSw.Elapsed.TotalMilliseconds;
            }
        }

        private void HandleMatchInput()
        {
            if (WasCommandPressed(ControlCommand.ToggleSettings, checkAvailable: false))
            {
                hud.SettingsOpen = !hud.SettingsOpen;
            }
            if (hud.SettingsOpen)
            {
                draggingSelectionBox = false;
                return;
            }

            if (WasCommandPressed(ControlCommand.ToggleHelp, checkAvailable: false)) hud.ShowHelp = !hud.ShowHelp;
            if (WasCommandPressed(ControlCommand.ToggleShop, checkAvailable: false))
            {
                hud.ShopOpen = !hud.ShopOpen;
                if (hud.ShopOpen)
                {
                    hud.ItemCompendiumOpen = false;
                    hud.HeroSheetOpen = false;
                }
            }
            if (WasCommandPressed(ControlCommand.ToggleHeroSheet, checkAvailable: false))
            {
                if (hud.HeroSheetOpen)
                {
                    hud.HeroSheetOpen = false;
                }
                else
                {
                    HeroSnap s = latestSnap != null ? FindHero(latestSnap, localTeam) : null;
                    hud.OpenHeroSheet(s != null ? s.heroId : localPick);
                }
            }
            if (WasCommandPressed(ControlCommand.ToggleCameraLock, checkAvailable: false)) ToggleCameraLock();
            if (IsCommandHeld(ControlCommand.CenterCamera, checkAvailable: false) && latestSnap != null)
            {
                world.CenterCameraOnHero(FindHero(latestSnap, localTeam));
            }

            bool hasPoint = RaycastCursorGround(out Vector2 cursorGround);
            hoveredEntityId = (latestSnap != null && hasPoint) ? FindHoveredHostileEntity(latestSnap, localTeam, cursorGround) : 0;

            bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            HandleRtsInput(hasPoint, cursorGround, ctrlHeld, shiftHeld);

            HeroControlProfile profile = ActiveControlProfile;
            int[] targetUnitIds = rtsSelection.GetCommandTargetUnitIds(IsRtsActiveForCurrentHero, latestSnap, localTeam);

            if (ctrlHeld)
            {
                if (WasCommandPressed(ControlCommand.CastQ, checkAvailable: false)) SendLocalAbilityLevelUp(LaneAbilitySlot.Q);
                else if (WasCommandPressed(ControlCommand.CastW, checkAvailable: false)) SendLocalAbilityLevelUp(LaneAbilitySlot.W);
                else if (WasCommandPressed(ControlCommand.CastE, checkAvailable: false)) SendLocalAbilityLevelUp(LaneAbilitySlot.E);
                else if (WasCommandPressed(ControlCommand.CastR, checkAvailable: false)) SendLocalAbilityLevelUp(LaneAbilitySlot.R);
            }
            else if (hasPoint)
            {
                if (WasCommandPressed(ControlCommand.CastQ)) IssueCast(LaneOrderKind.CastQ, cursorGround);
                else if (WasCommandPressed(ControlCommand.CastW)) IssueCast(LaneOrderKind.CastW, cursorGround);
                else if (WasCommandPressed(ControlCommand.CastE)) IssueCast(LaneOrderKind.CastE, cursorGround);
                else if (WasCommandPressed(ControlCommand.CastR)) IssueCast(LaneOrderKind.CastR, cursorGround);
                else if (WasCommandPressed(ControlCommand.AttackMove))
                {
                    IssueLocalOrder(new LaneOrder
                    {
                        team = localTeam,
                        kind = LaneOrderKind.AttackMove,
                        point = cursorGround,
                        unitIds = targetUnitIds,
                    });
                }
            }

            if (WasCommandPressed(ControlCommand.Stop))
            {
                IssueLocalOrder(new LaneOrder
                {
                    team = localTeam,
                    kind = LaneOrderKind.Stop,
                    unitIds = targetUnitIds,
                });
            }

            if (hasPoint)
            {
                KeyCode moveKey = settingsStore.GetKey(ControlCommand.Move);
                KeyCode atkKey = settingsStore.GetKey(ControlCommand.AttackOrder);
                bool sharedKey = moveKey == atkKey;

                if (sharedKey)
                {
                    if (WasRawKeyTriggered(moveKey))
                    {
                        if (hoveredEntityId > 0 && latestSnap != null && profile.SupportsBasicAttack &&
                            TryClassifyTarget(latestSnap, localTeam, hoveredEntityId, out LaneOrderKind atkKind))
                        {
                            IssueLocalOrder(new LaneOrder
                            {
                                team = localTeam,
                                kind = atkKind,
                                targetId = hoveredEntityId,
                                point = cursorGround,
                                unitIds = targetUnitIds,
                            });
                        }
                        else if (profile.IsCommandAvailable(ControlCommand.Move, settingsStore.Data))
                        {
                            IssueLocalOrder(new LaneOrder
                            {
                                team = localTeam,
                                kind = LaneOrderKind.Move,
                                point = cursorGround,
                                unitIds = targetUnitIds,
                            });
                        }
                    }
                }
                else
                {
                    if (WasRawKeyTriggered(atkKey) && profile.SupportsBasicAttack &&
                        hoveredEntityId > 0 && latestSnap != null &&
                        TryClassifyTarget(latestSnap, localTeam, hoveredEntityId, out LaneOrderKind atkKind))
                    {
                        IssueLocalOrder(new LaneOrder
                        {
                            team = localTeam,
                            kind = atkKind,
                            targetId = hoveredEntityId,
                            point = cursorGround,
                            unitIds = targetUnitIds,
                        });
                    }
                    else if (WasRawKeyTriggered(moveKey) && profile.IsCommandAvailable(ControlCommand.Move, settingsStore.Data))
                    {
                        IssueLocalOrder(new LaneOrder
                        {
                            team = localTeam,
                            kind = LaneOrderKind.Move,
                            point = cursorGround,
                            unitIds = targetUnitIds,
                        });
                    }
                }
            }
        }

        private void HandleRtsInput(bool hasPoint, Vector2 cursorGround, bool ctrlHeld, bool shiftHeld)
        {
            if (!IsRtsActiveForCurrentHero || hud.ShopOpen || hud.HeroSheetOpen || hud.ItemCompendiumOpen || hud.SettingsOpen || hud.IsTextInputFocused)
            {
                draggingSelectionBox = false;
                return;
            }

            bool selectionChanged = false;
            if (WasCommandPressed(ControlCommand.RtsSelectHero))
            {
                selectionChanged |= rtsSelection.SelectHero(latestSnap, localTeam);
            }
            if (WasCommandPressed(ControlCommand.RtsSelectAllUnits))
            {
                selectionChanged |= rtsSelection.SelectAllControllable(latestSnap, localTeam) > 0;
            }
            if (WasCommandPressed(ControlCommand.RtsCycleUnit))
            {
                selectionChanged |= rtsSelection.CycleSelectedUnit(latestSnap, localTeam);
            }

            for (int g = 1; g <= LaneRtsSelection.MaxControlGroups; g++)
            {
                ControlCommand grpCmd = ControlCommand.RtsGroup1 + (g - 1);
                if (!WasCommandPressed(grpCmd)) continue;
                if (ctrlHeld)
                {
                    rtsSelection.AssignControlGroup(g, latestSnap, localTeam);
                }
                else if (rtsSelection.RecallControlGroup(g, latestSnap, localTeam, Time.unscaledTime, out bool centerCam, out Vector2 focusPos))
                {
                    selectionChanged = true;
                    if (centerCam && settingsStore != null && settingsStore.Data.rtsDoubleTapGroupCentersCamera)
                    {
                        world.CenterCameraAt(focusPos);
                    }
                }
            }

            bool allowMouse0Rts = settingsStore.GetKey(ControlCommand.Move) != KeyCode.Mouse0 &&
                                  settingsStore.GetKey(ControlCommand.AttackMove) != KeyCode.Mouse0 &&
                                  settingsStore.GetKey(ControlCommand.Stop) != KeyCode.Mouse0 &&
                                  settingsStore.GetKey(ControlCommand.CastQ) != KeyCode.Mouse0 &&
                                  settingsStore.GetKey(ControlCommand.CastW) != KeyCode.Mouse0 &&
                                  settingsStore.GetKey(ControlCommand.CastE) != KeyCode.Mouse0 &&
                                  settingsStore.GetKey(ControlCommand.CastR) != KeyCode.Mouse0;

            if (allowMouse0Rts && Input.GetMouseButtonDown(0) && !hud.PointerOverUi)
            {
                draggingSelectionBox = true;
                dragStartScreenPos = Input.mousePosition;
                dragCurrentScreenPos = Input.mousePosition;
            }

            if (draggingSelectionBox)
            {
                dragCurrentScreenPos = Input.mousePosition;
                if (Input.GetMouseButtonUp(0))
                {
                    float dragLen = (dragCurrentScreenPos - dragStartScreenPos).magnitude;
                    if (dragLen >= 6f && mainCam != null && latestSnap != null)
                    {
                        float xMin = Mathf.Min(dragStartScreenPos.x, dragCurrentScreenPos.x);
                        float xMax = Mathf.Max(dragStartScreenPos.x, dragCurrentScreenPos.x);
                        float yMin = Mathf.Min(dragStartScreenPos.y, dragCurrentScreenPos.y);
                        float yMax = Mathf.Max(dragStartScreenPos.y, dragCurrentScreenPos.y);
                        selectionChanged |= rtsSelection.SelectByPredicate(u =>
                        {
                            Vector3 sp = mainCam.WorldToScreenPoint(new Vector3(u.Pos.x, 0.5f, u.Pos.y));
                            return sp.z > 0f && sp.x >= xMin && sp.x <= xMax && sp.y >= yMin && sp.y <= yMax;
                        }, shiftHeld, latestSnap, localTeam) > 0;
                    }
                    else if (hasPoint && latestSnap != null)
                    {
                        if (TryFindNearestAuthorizedUnit(latestSnap, localTeam, cursorGround, 1.15f, out int hitUnitId))
                        {
                            selectionChanged |= rtsSelection.SelectSingleUnit(hitUnitId, shiftHeld, latestSnap, localTeam);
                        }
                    }
                    draggingSelectionBox = false;
                }
            }

            if (selectionChanged && latestSnap != null)
            {
                world.UpdateSelectionRings(latestSnap, rtsSelection.SelectedUnitIds);
            }
        }

        private bool TryFindNearestAuthorizedUnit(LaneSnapshot snap, LaneTeam team, Vector2 cursor, float maxRadius, out int unitId)
        {
            unitId = 0;
            rtsSelection.GetAuthorizedUnits(snap, team, scratchAuthorized);
            float best = maxRadius;
            for (int i = 0; i < scratchAuthorized.Count; i++)
            {
                float d = Vector2.Distance(cursor, scratchAuthorized[i].Pos);
                if (d < best)
                {
                    best = d;
                    unitId = scratchAuthorized[i].Id;
                }
            }
            return unitId > 0;
        }

        private bool WasCommandPressed(ControlCommand cmd, bool checkAvailable = true)
        {
            if (settingsStore == null) return false;
            if (checkAvailable && !ActiveControlProfile.IsCommandAvailable(cmd, settingsStore.Data)) return false;
            return WasRawKeyTriggered(settingsStore.GetKey(cmd));
        }

        private bool IsCommandHeld(ControlCommand cmd, bool checkAvailable = true)
        {
            if (settingsStore == null) return false;
            if (hud != null && hud.IsTextInputFocused) return false;
            if (checkAvailable && !ActiveControlProfile.IsCommandAvailable(cmd, settingsStore.Data)) return false;
            KeyCode key = settingsStore.GetKey(cmd);
            if (key == KeyCode.None) return false;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6 && hud.PointerOverUi) return false;
            return Input.GetKey(key);
        }

        private bool WasRawKeyTriggered(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            if (hud != null && hud.IsTextInputFocused) return false;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6 && hud.PointerOverUi) return false;
            return Input.GetKeyDown(key);
        }

        private void IssueCast(LaneOrderKind kind, Vector2 point)
        {
            IssueLocalOrder(new LaneOrder
            {
                team = localTeam,
                kind = kind,
                point = point,
            });
        }

        public void IssueLocalOrder(LaneOrder order)
        {
            order.team = localTeam;
            if (mode == SessionMode.Practice || mode == SessionMode.Host)
            {
                match?.EnqueueOrApplyOrder(order);
            }
            else if (mode == SessionMode.Guest && link != null && link.IsConnected)
            {
                link.SendOrder(order);
            }
        }

        private void ApplyLocalSnapshot(LaneSnapshot snap)
        {
            latestSnap = snap;
            bool rtsActive = IsRtsActiveForCurrentHero;
            bool autoSelect = settingsStore != null && settingsStore.Data.rtsAutoSelectSummons;
            rtsSelection.SyncWithSnapshot(snap, localTeam, rtsActive, autoSelect);
            world.ApplySnapshot(snap, localTeam, tuning, hoveredEntityId, rtsActive ? rtsSelection.SelectedUnitIds : null);
            hud.PushSnapshot(snap, localTeam, hoveredEntityId);
        }

        private bool RaycastCursorGround(out Vector2 groundPos)
        {
            groundPos = Vector2.zero;
            if (mainCam == null) return false;
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return false;
            Vector3 hit = ray.GetPoint(enter);
            groundPos = new Vector2(hit.x, hit.z);
            return true;
        }

        private static int FindHoveredHostileEntity(LaneSnapshot snap, LaneTeam selfTeam, Vector2 cursor)
        {
            float best = 1.05f;
            int found = 0;

            for (int i = 0; i < snap.heroes.Count; i++)
            {
                HeroSnap h = snap.heroes[i];
                if (h.team == selfTeam || h.phase == LanePhase.Dead || h.invisible) continue;
                float d = Vector2.Distance(cursor, new Vector2(h.x, h.y));
                if (d < best)
                {
                    best = d;
                    found = h.id;
                }
            }
            for (int i = 0; i < snap.illusions.Count; i++)
            {
                IllusionSnap ill = snap.illusions[i];
                if (ill.team == selfTeam) continue;
                float d = Vector2.Distance(cursor, new Vector2(ill.x, ill.y));
                if (d < best)
                {
                    best = d;
                    found = ill.id;
                }
            }
            for (int i = 0; i < snap.minions.Count; i++)
            {
                MinionSnap m = snap.minions[i];
                if (m.team == selfTeam) continue;
                float d = Vector2.Distance(cursor, new Vector2(m.x, m.y));
                if (d < best)
                {
                    best = d;
                    found = m.id;
                }
            }
            for (int i = 0; i < snap.towers.Count; i++)
            {
                TowerSnap t = snap.towers[i];
                if (t.team == selfTeam || !t.alive) continue;
                float d = Vector2.Distance(cursor, new Vector2(t.x, t.y));
                if (d < 1.35f && (found == 0 || d < best))
                {
                    found = t.id;
                }
            }
            return found;
        }

        private static bool TryClassifyTarget(LaneSnapshot snap, LaneTeam selfTeam, int targetId, out LaneOrderKind kind)
        {
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].id == targetId && snap.heroes[i].team != selfTeam)
                {
                    kind = LaneOrderKind.AttackHero;
                    return true;
                }
            }
            for (int i = 0; i < snap.illusions.Count; i++)
            {
                if (snap.illusions[i].id == targetId && snap.illusions[i].team != selfTeam)
                {
                    kind = LaneOrderKind.AttackHero;
                    return true;
                }
            }
            for (int i = 0; i < snap.minions.Count; i++)
            {
                if (snap.minions[i].id == targetId && snap.minions[i].team != selfTeam)
                {
                    kind = LaneOrderKind.AttackMinion;
                    return true;
                }
            }
            for (int i = 0; i < snap.towers.Count; i++)
            {
                if (snap.towers[i].id == targetId && snap.towers[i].team != selfTeam)
                {
                    kind = LaneOrderKind.AttackTower;
                    return true;
                }
            }
            kind = LaneOrderKind.Move;
            return false;
        }

        private static HeroSnap FindHero(LaneSnapshot snap, LaneTeam team)
        {
            if (snap == null) return null;
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].team == team) return snap.heroes[i];
            }
            return null;
        }

        public void BeginOnlineHostMatchFromLobby()
        {
            if (link != null && link.IsHost && link.IsConnected)
            {
                hostStartSent = true;
                BeginHostMatchNow();
            }
        }

        private void OnGUI()
        {
            if (headlessMode || hud == null) return;
            if (benchMeasureActive)
            {
                benchSw.Restart();
                hud.Draw(mainCam, world.CameraLocked);
                benchSw.Stop();
                benchOnGuiMsAccum += (float)benchSw.Elapsed.TotalMilliseconds;
                return;
            }
            hud.Draw(mainCam, world.CameraLocked);
        }

        private void OnDestroy()
        {
            abilityPreview?.Dispose();
            abilityPreview = null;
            LaneItemIcons.DisposeAll();
            LaneUiCommon.DisposeAll();
            CleanupLink();
        }

        private IEnumerator RunPerfBenchRoutine(string outPath)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 300;
            CultureInfo inv = CultureInfo.InvariantCulture;
            bool origDemos = settingsStore == null || settingsStore.Data.enableAbilityDemos;

            ProfilerRecorder mainThreadRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            ProfilerRecorder gcAllocRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 15);
            ProfilerRecorder drawCallsRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 15);
            ProfilerRecorder setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 15);

            var sb = new StringBuilder();
            sb.AppendLine($"=== WINDOWED PERF BENCH ({LaneUpdater.BuildVersion}, {Screen.width}x{Screen.height}, Gfx={SystemInfo.graphicsDeviceName}, Quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}) ===");
            sb.AppendLine($"TimestampUTC={DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", inv)} | ProfilerValid(MainThread={mainThreadRec.Valid}, DrawCalls={drawCallsRec.Valid}, GCAlloc={gcAllocRec.Valid})");
            sb.AppendLine();

            yield return null;
            yield return null;

            IEnumerator MeasurePhase(string label, int warmupFrames, int measureFrames, Action<int> perFrameAct)
            {
                for (int w = 0; w < warmupFrames; w++)
                {
                    perFrameAct?.Invoke(-1 - w);
                    yield return new WaitForEndOfFrame();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                int gen0Start = GC.CollectionCount(0);
                long memStart = GC.GetTotalMemory(false);
                int meshesStart = Resources.FindObjectsOfTypeAll<Mesh>().Length;
                int matsStart = Resources.FindObjectsOfTypeAll<Material>().Length;

                var frameTimes = new double[measureFrames];
                double updateSum = 0;
                double onGuiSum = 0;
                double profMainSum = 0;
                double drawCallSum = 0;
                double setPassSum = 0;
                double gcFrameSum = 0;

                benchMeasureActive = true;
                var frameSw = new Stopwatch();
                for (int f = 0; f < measureFrames; f++)
                {
                    benchUpdateMsAccum = 0f;
                    benchOnGuiMsAccum = 0f;
                    frameSw.Restart();
                    perFrameAct?.Invoke(f);
                    yield return new WaitForEndOfFrame();
                    frameSw.Stop();

                    frameTimes[f] = frameSw.Elapsed.TotalMilliseconds;
                    updateSum += benchUpdateMsAccum;
                    onGuiSum += benchOnGuiMsAccum;
                    if (mainThreadRec.Valid) profMainSum += mainThreadRec.LastValue / 1_000_000.0;
                    if (drawCallsRec.Valid) drawCallSum += drawCallsRec.LastValue;
                    if (setPassRec.Valid) setPassSum += setPassRec.LastValue;
                    if (gcAllocRec.Valid) gcFrameSum += gcAllocRec.LastValue / 1024.0;
                }
                benchMeasureActive = false;

                long memEnd = GC.GetTotalMemory(false);
                int gen0End = GC.CollectionCount(0);
                int meshesEnd = Resources.FindObjectsOfTypeAll<Mesh>().Length;
                int matsEnd = Resources.FindObjectsOfTypeAll<Material>().Length;

                Array.Sort(frameTimes);
                double sum = 0;
                for (int i = 0; i < frameTimes.Length; i++) sum += frameTimes[i];
                double avgMs = sum / measureFrames;
                double p95Ms = frameTimes[Math.Clamp((int)Math.Floor(measureFrames * 0.95) - 1, 0, measureFrames - 1)];
                double maxMs = frameTimes[measureFrames - 1];
                double fps = avgMs > 0.001 ? 1000.0 / avgMs : 0;

                string profExtra = drawCallsRec.Valid
                    ? $" | ProfMain={(profMainSum / measureFrames).ToString("0.00", inv)}ms Draws={(drawCallSum / measureFrames).ToString("0.0", inv)} SetPass={(setPassSum / measureFrames).ToString("0.0", inv)} FrameGC={(gcFrameSum / measureFrames).ToString("0.0", inv)}KB"
                    : string.Empty;

                sb.AppendLine(
                    $"{label,-34} | FPS={fps.ToString("0.0", inv),6} | Frame Avg={avgMs.ToString("0.00", inv),6}ms P95={p95Ms.ToString("0.00", inv),6}ms Max={maxMs.ToString("0.00", inv),6}ms | " +
                    $"UpdateAvg={(updateSum / measureFrames).ToString("0.00", inv),6}ms OnGuiAvg={(onGuiSum / measureFrames).ToString("0.00", inv),6}ms | " +
                    $"Heap+={(Math.Max(0L, memEnd - memStart) / 1024L),5}KB Gen0={Math.Max(0, gen0End - gen0Start)} Mesh+={(meshesEnd - meshesStart),3} Mat+={(matsEnd - matsStart),3}{profExtra}");
            }

            // 1. Lobby with Hero Guide Closed
            hud.HeroSheetOpen = false;
            yield return MeasurePhase("1_Lobby_GuideClosed", 15, 60, null);

            // 2. Lobby with Hero Guide Open, Demos OFF
            if (settingsStore != null) settingsStore.Data.enableAbilityDemos = false;
            hud.OpenHeroSheet(LaneHeroId.Ogre);
            yield return MeasurePhase("2_Lobby_GuideOpen_DemosOFF", 15, 60, null);

            // 3. Lobby with Hero Guide Open, Demos ON + Ability Switch + Slider Adjust
            if (settingsStore != null) settingsStore.Data.enableAbilityDemos = true;
            hud.OpenHeroSheet(LaneHeroId.LaserShooter);
            var switchSlots = new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E, LaneAbilitySlot.R, LaneAbilitySlot.None };
            yield return MeasurePhase("3_Lobby_GuideOpen_DemosON", 15, 90, f =>
            {
                if (f >= 0 && f % 20 == 19)
                {
                    LaneAbilitySlot nextSlot = switchSlots[(f / 20) % switchSlots.Length];
                    hud.BenchSelectPreviewSlot(LaneHeroId.LaserShooter, nextSlot, 0);
                }
                else if (f >= 0 && f % 6 == 3 && abilityPreview != null)
                {
                    PreviewStatOverrides ov = abilityPreview.Overrides;
                    ov.bonusIntelligence = (f * 3f) % 150f;
                    ov.bonusAgility = (f * 2f) % 120f;
                    abilityPreview.ApplyLiveOverrides(ov);
                }
            });

            // 4. Live Match with Hero Guide Closed
            hud.HeroSheetOpen = false;
            abilityPreview?.Deactivate();
            StartPractice();
            yield return MeasurePhase("4_Match_GuideClosed", 15, 60, null);

            // 5. Live Match with Hero Guide Open, Demos ON
            hud.OpenHeroSheet(LaneHeroId.Miravel);
            hud.BenchSelectPreviewSlot(LaneHeroId.Miravel, LaneAbilitySlot.R, 0);
            yield return MeasurePhase("5_Match_GuideOpen_DemosON", 15, 90, null);

            // 6. Item Compendium Open, Demos ON + Passive Tab Switching + Sliders
            hud.HeroSheetOpen = false;
            hud.ShopOpen = false;
            hud.OpenItemCompendium(LaneItemIds.BananaBlade);
            var itemPasses = new[]
            {
                (LaneItemIds.BananaBlade, 0),
                (LaneItemIds.BananaBlade, 1),
                (LaneItemIds.GiantsHeart, 0),
                (LaneItemIds.GiantsHeart, 1),
                (LaneItemIds.DraconicBlade, 0),
                (LaneItemIds.DraconicBlade, 1),
            };
            yield return MeasurePhase("6_Compendium_DemosON", 15, 90, f =>
            {
                if (f >= 0 && f % 15 == 14)
                {
                    var next = itemPasses[(f / 15) % itemPasses.Length];
                    hud.BenchSelectCompendiumItem(next.Item1, next.Item2);
                }
                else if (f >= 0 && f % 6 == 3 && abilityPreview != null)
                {
                    PreviewStatOverrides ov = abilityPreview.Overrides;
                    ov.bonusAgility = (f * 3f) % 140f;
                    ov.bonusMaxHp = (f * 30f) % 1200f;
                    abilityPreview.ApplyLiveOverrides(ov);
                }
            });

            // 7. Compendium Open/Close Cycle Stability (verifies 0 leaks)
            yield return MeasurePhase("7_Compendium_OpenCloseCycle", 10, 60, f =>
            {
                if (f >= 0 && f % 10 == 0)
                {
                    if (hud.ItemCompendiumOpen)
                    {
                        hud.ItemCompendiumOpen = false;
                        abilityPreview?.Deactivate();
                    }
                    else
                    {
                        hud.OpenItemCompendium(LaneItemIds.GiantsHeart);
                    }
                }
            });
            hud.ItemCompendiumOpen = false;
            abilityPreview?.Deactivate();

            if (settingsStore != null) settingsStore.Data.enableAbilityDemos = origDemos;
            mainThreadRec.Dispose();
            gcAllocRec.Dispose();
            drawCallsRec.Dispose();
            setPassRec.Dispose();

            string dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[LaneScene] Perf bench written to {outPath}");
            Application.Quit(0);
        }

        private IEnumerator RunUiShotsRoutine(string outDir)
        {
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            if (settingsStore != null) settingsStore.Data.enableAbilityDemos = true;

            // 1. Main Lobby showing Item Compendium CTA
            hud.ShopOpen = false;
            hud.HeroSheetOpen = false;
            hud.ItemCompendiumOpen = false;
            yield return new WaitForSeconds(0.35f);
            yield return new WaitForEndOfFrame();
            CaptureScreenPng(Path.Combine(outDir, "01_lobby.png"));

            // 2. Match Shop with Interactive Recipe Tree (partial ownership + duplicate copy count + auto-buy CTA)
            StartPractice();
            yield return null;
            if (match != null)
            {
                match.SetHeroGoldForTest(localTeam, 900);
                match.TryBuyItem(localTeam, LaneItemIds.LifeCrystal, out _);
                match.TryBuyItem(localTeam, LaneItemIds.Titanstone, out _);
                match.SetHeroGoldForTest(localTeam, 600);
                ApplyLocalSnapshot(match.Snapshot());
            }
            hud.ShopOpen = true;
            hud.BenchSelectShopItem(LaneItemIds.GiantsHeart, ItemBrowseTab.All, ItemStatFilter.None, "");
            yield return new WaitForSeconds(0.25f);
            yield return new WaitForEndOfFrame();
            CaptureScreenPng(Path.Combine(outDir, "02_shop_recipe_tree.png"));

            // 3. Shop Filtered Advanced Grid inspecting Banana Blade
            hud.BenchSelectShopItem(LaneItemIds.BananaBlade, ItemBrowseTab.Advanced, ItemStatFilter.None, "");
            yield return new WaitForSeconds(0.25f);
            yield return new WaitForEndOfFrame();
            CaptureScreenPng(Path.Combine(outDir, "03_shop_filtered_grid.png"));

            // 4. Item Compendium — Banana Blade (Crescent Strike demo + 4 passive tabs + AD/AGI/INT sliders)
            hud.ShopOpen = false;
            hud.OpenItemCompendium(LaneItemIds.BananaBlade);
            yield return new WaitForSeconds(0.70f);
            yield return new WaitForEndOfFrame();
            CaptureScreenPng(Path.Combine(outDir, "04_compendium_banana_blade.png"));

            // 5. Item Compendium — The Giant's Heart (Ancient Vitality amber deferred-damage HP bar + direct stats vs passives)
            hud.BenchSelectCompendiumItem(LaneItemIds.GiantsHeart, 0);
            yield return new WaitForSeconds(0.75f);
            yield return new WaitForEndOfFrame();
            CaptureScreenPng(Path.Combine(outDir, "05_compendium_giants_heart.png"));

            // 6. Hero Guide — Chain Laser Overdrive Prism Beam preview
            hud.ItemCompendiumOpen = false;
            hud.OpenHeroSheet(LaneHeroId.LaserShooter);
            hud.BenchSelectPreviewSlot(LaneHeroId.LaserShooter, LaneAbilitySlot.R, 0);
            yield return new WaitForSeconds(0.95f);
            yield return new WaitForEndOfFrame();
            CaptureScreenPng(Path.Combine(outDir, "06_hero_guide.png"));

            Application.Quit(0);
        }

        private static void CaptureScreenPng(string path)
        {
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            byte[] png = EncodeRgbaToPng(tex.GetPixels32(), tex.width, tex.height);
            File.WriteAllBytes(path, png);
            UnityEngine.Object.Destroy(tex);
        }

        private static byte[] EncodeRgbaToPng(Color32[] pixels, int w, int h)
        {
            using (var raw = new MemoryStream())
            {
                // PNG filters: 0 (None) per scanline top-down
                for (int y = h - 1; y >= 0; y--)
                {
                    raw.WriteByte(0);
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        Color32 c = pixels[row + x];
                        raw.WriteByte(c.r);
                        raw.WriteByte(c.g);
                        raw.WriteByte(c.b);
                        raw.WriteByte(c.a);
                    }
                }
                byte[] scan = raw.ToArray();
                byte[] zlib;
                using (var zms = new MemoryStream())
                {
                    zms.WriteByte(0x78);
                    zms.WriteByte(0x01);
                    using (var def = new System.IO.Compression.DeflateStream(zms, System.IO.Compression.CompressionLevel.Fastest, true))
                    {
                        def.Write(scan, 0, scan.Length);
                    }
                    uint adler = ComputeAdler32(scan);
                    WriteBigEndianUInt(zms, adler);
                    zlib = zms.ToArray();
                }

                using (var png = new MemoryStream())
                {
                    byte[] sig = { 137, 80, 78, 71, 13, 10, 26, 10 };
                    png.Write(sig, 0, sig.Length);

                    using (var ihdr = new MemoryStream(13))
                    {
                        WriteBigEndianUInt(ihdr, (uint)w);
                        WriteBigEndianUInt(ihdr, (uint)h);
                        ihdr.WriteByte(8); // bit depth
                        ihdr.WriteByte(6); // RGBA
                        ihdr.WriteByte(0);
                        ihdr.WriteByte(0);
                        ihdr.WriteByte(0);
                        WritePngChunk(png, "IHDR", ihdr.ToArray());
                    }
                    WritePngChunk(png, "IDAT", zlib);
                    WritePngChunk(png, "IEND", Array.Empty<byte>());
                    return png.ToArray();
                }
            }
        }

        private static void WriteBigEndianUInt(Stream s, uint v)
        {
            s.WriteByte((byte)((v >> 24) & 0xFF));
            s.WriteByte((byte)((v >> 16) & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
            s.WriteByte((byte)(v & 0xFF));
        }

        private static uint ComputeAdler32(byte[] data)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        private static void WritePngChunk(Stream s, string type, byte[] data)
        {
            WriteBigEndianUInt(s, (uint)data.Length);
            byte[] t = Encoding.ASCII.GetBytes(type);
            s.Write(t, 0, 4);
            if (data.Length > 0) s.Write(data, 0, data.Length);
            uint crc = 0xFFFFFFFFu;
            crc = UpdateCrc32(crc, t, 0, 4);
            crc = UpdateCrc32(crc, data, 0, data.Length);
            WriteBigEndianUInt(s, crc ^ 0xFFFFFFFFu);
        }

        private static uint UpdateCrc32(uint crc, byte[] buf, int off, int len)
        {
            for (int i = off; i < off + len; i++)
            {
                crc ^= buf[i];
                for (int b = 0; b < 8; b++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : (crc >> 1);
                }
            }
            return crc;
        }
    }
}
