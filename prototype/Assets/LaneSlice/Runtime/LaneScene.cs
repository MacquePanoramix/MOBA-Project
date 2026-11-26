using System;
using System.Collections.Generic;
using UnityEngine;

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
                    if (hud.SettingsOpen) hud.SettingsOpen = false;
                    else if (hud.HeroSheetOpen) hud.HeroSheetOpen = false;
                    else if (hud.ShopOpen) hud.ShopOpen = false;
                    else if (hud.ShowHelp) hud.ShowHelp = false;
                    else if (mode != SessionMode.Lobby) hud.SettingsOpen = true;
                }
                else if (mode == SessionMode.Lobby && WasCommandPressed(ControlCommand.ToggleSettings, checkAvailable: false))
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
                bool runPreview = !headlessMode && hud != null && hud.HeroSheetOpen && !hud.SettingsOpen &&
                                  (settingsStore == null || settingsStore.Data.enableAbilityDemos);
                abilityPreview.Tick(Time.deltaTime, runPreview);
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
            if (WasCommandPressed(ControlCommand.ToggleShop, checkAvailable: false)) hud.ShopOpen = !hud.ShopOpen;
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
            if (!IsRtsActiveForCurrentHero || hud.ShopOpen || hud.HeroSheetOpen || hud.SettingsOpen)
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
            if (checkAvailable && !ActiveControlProfile.IsCommandAvailable(cmd, settingsStore.Data)) return false;
            KeyCode key = settingsStore.GetKey(cmd);
            if (key == KeyCode.None) return false;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6 && hud.PointerOverUi) return false;
            return Input.GetKey(key);
        }

        private bool WasRawKeyTriggered(KeyCode key)
        {
            if (key == KeyCode.None) return false;
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
            hud.Draw(mainCam, world.CameraLocked);
        }

        private void OnDestroy()
        {
            abilityPreview?.Dispose();
            abilityPreview = null;
            CleanupLink();
        }
    }
}
