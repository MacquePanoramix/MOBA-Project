using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    /// <summary>
    /// IMGUI interface for Ultimate Free MOBA (Woodland Lane vertical slice):
    /// 4-hero lobby, personal Settings & context-aware keybindings, optional/required RTS marquee & unit bar,
    /// world-space health & status overlays, ability & attribute dock, 20-item Shop, and minimap.
    /// </summary>
    public sealed partial class LaneHud
    {
        private readonly LaneScene scene;
        private readonly LaneTuning tuning;
        private readonly List<FloatingPopup> popups = new List<FloatingPopup>();

        private LaneSnapshot snap;
        private LaneTeam localTeam;
        private int hoveredId;
        private string relayJoinInput = string.Empty;
        private string tcpJoinInput = "127.0.0.1:47777";
        private string shopFeedback = string.Empty;
        private float shopFeedbackTimer;
        private Texture2D whiteTex;
        private LaneHeroId inspectedHero = LaneHeroId.Ogre;
        private Vector2 heroSheetScroll;
        private Vector2 shopScroll;
        private GUIStyle sheetTextStyle;
        private GUIStyle sheetBodyStyle;
        private GUIStyle sheetSectionStyle;
        private GUIStyle sheetFactPillStyle;
        private GUIStyle sheetMathBoxStyle;
        private GUIStyle sheetTipBoxStyle;
        private GUIStyle cardBoxStyle;
        private GUIStyle cardHeaderStyle;
        private GUIStyle tooltipStyle;
        private bool showExactMath;
        private readonly HashSet<int> expandedMathCards = new HashSet<int>();
        private LaneAbilitySlot previewSlot = LaneAbilitySlot.Q;
        private bool previewSandboxMode;
        private Vector2 previewPaneScroll;
        private int shopTierFilter = -1; // -1=All, 0=Starter, 1=Core, 2=Capstone
        private string hoverTooltipText;
        private Rect hoverTooltipAnchor;

        public bool ShopOpen { get; set; }
        public bool HeroSheetOpen { get; set; }
        public bool SettingsOpen { get; set; }
        public bool ShowHelp { get; set; }
        public bool PointerOverUi { get; private set; }
        public bool IsCapturingKeybind => capturingCommand.HasValue;

        public void OpenHeroSheet(LaneHeroId hero)
        {
            inspectedHero = hero;
            HeroSheetOpen = true;
            SettingsOpen = false;
            heroSheetScroll = Vector2.zero;
            previewPaneScroll = Vector2.zero;
            previewSlot = LaneAbilitySlot.Q;
            scene.AbilityPreview?.Select(hero, previewSlot, 0);
        }

        public void OpenSettings()
        {
            SettingsOpen = true;
            HeroSheetOpen = false;
            ShopOpen = false;
            capturingCommand = null;
            pendingConflictCmd = null;
            settingsNotice = string.Empty;
        }

        private sealed class FloatingPopup
        {
            public Vector3 worldPos;
            public string text;
            public Color color;
            public float age;
            public float duration;
        }

        public LaneHud(LaneScene scene, LaneTuning tuning)
        {
            this.scene = scene;
            this.tuning = tuning;
        }

        public void PushSnapshot(LaneSnapshot s, LaneTeam team, int hoveredEntityId)
        {
            snap = s;
            localTeam = team;
            hoveredId = hoveredEntityId;

            if (s != null)
            {
                for (int i = 0; i < s.events.Count; i++)
                {
                    VisualEventSnap ev = s.events[i];
                    if (string.IsNullOrEmpty(ev.label)) continue;
                    Color c = Color.white;
                    if (ev.kind == LaneEventKind.LuisOrbPulse || ev.kind == LaneEventKind.SwordsmanWHeal) c = new Color(0.45f, 1f, 0.72f);
                    else if (ev.kind == LaneEventKind.OgreStrike || ev.kind == LaneEventKind.SwordsmanRSlash) c = new Color(1f, 0.76f, 0.25f);
                    else if (ev.kind == LaneEventKind.LuisCurseProc) c = new Color(0.84f, 0.56f, 1f);
                    else if (ev.kind == LaneEventKind.TowerAggro) c = new Color(1f, 0.30f, 0.26f);
                    else if (ev.kind == LaneEventKind.CcBlocked) c = new Color(1f, 0.90f, 0.40f);
                    else if (ev.kind == LaneEventKind.LevelUp) c = new Color(0.50f, 0.88f, 1f);
                    else if (ev.kind == LaneEventKind.LaserGoldBonus) c = new Color(1f, 0.88f, 0.28f);
                    else if (ev.kind == LaneEventKind.LaserShock || ev.kind == LaneEventKind.LaserDecoy) c = new Color(0.32f, 0.94f, 1f);
                    else if (ev.kind == LaneEventKind.InsufficientMana) c = new Color(0.45f, 0.82f, 1f);

                    popups.Add(new FloatingPopup
                    {
                        worldPos = new Vector3(ev.x, 1.85f, ev.y),
                        text = ev.label,
                        color = c,
                        age = 0f,
                        duration = 1.25f,
                    });
                }
            }
        }

        public void TickPopups(float dt)
        {
            if (shopFeedbackTimer > 0f)
            {
                shopFeedbackTimer = Mathf.Max(0f, shopFeedbackTimer - dt);
                if (shopFeedbackTimer <= 0f) shopFeedback = string.Empty;
            }
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                popups[i].age += dt;
                popups[i].worldPos += Vector3.up * (0.95f * dt);
                if (popups[i].age >= popups[i].duration) popups.RemoveAt(i);
            }
        }

        private Texture2D WhiteTex
        {
            get
            {
                if (whiteTex == null)
                {
                    whiteTex = new Texture2D(1, 1);
                    whiteTex.SetPixel(0, 0, Color.white);
                    whiteTex.Apply();
                }
                return whiteTex;
            }
        }

        private void DrawBox(Rect r, Color col)
        {
            Color prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(r, WhiteTex);
            GUI.color = prev;
        }

        public void Draw(Camera cam, bool camLocked)
        {
            PointerOverUi = false;
            HandleKeybindCaptureEvent();

            if (!scene.IsInMatch || snap == null)
            {
                DrawLobby();
                return;
            }

            HeroSnap self = null;
            HeroSnap enemy = null;
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].team == localTeam) self = snap.heroes[i];
                else enemy = snap.heroes[i];
            }

            if (Event.current.type == EventType.Repaint)
            {
                hoverTooltipText = null;
            }

            if (cam != null)
            {
                DrawWorldBars(cam, self);
                DrawPopups(cam);
            }

            DrawRtsSelectionBox();
            bool prevGuiEnabled = GUI.enabled;
            if (SettingsOpen) GUI.enabled = false;
            DrawTopBar(self, enemy, camLocked);
            DrawStatusBar(self, enemy);
            DrawCenterBanners(self);
            DrawRtsSelectionBar();
            if (self != null) DrawBottomDock(self);
            DrawMinimap(self, enemy);
            if (HeroSheetOpen && !SettingsOpen) DrawHeroSheetDrawer(self, inMatch: true);
            if (ShopOpen && self != null && !SettingsOpen) DrawShopDrawer(self);
            if (ShowHelp && !SettingsOpen) DrawHelpOverlay();
            GUI.enabled = prevGuiEnabled;
            if (SettingsOpen)
            {
                DrawBox(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.04f, 0.05f, 0.58f));
                DrawSettingsDrawer(self, inMatch: true);
            }
            DrawHoverTooltip();
        }

        private string KeyLabel(ControlCommand cmd) =>
            scene.Settings != null ? scene.Settings.GetKeyLabel(cmd) : ControlCommandCatalog.FormatKeyLabel(ControlCommandCatalog.GetMeta(cmd).DefaultKey);

        private void DrawLobby()
        {
            if (SettingsOpen)
            {
                DrawBox(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.10f, 0.11f, 0.96f));
                DrawSettingsDrawer(null, inMatch: false);
                return;
            }
            if (HeroSheetOpen)
            {
                DrawBox(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.10f, 0.11f, 0.96f));
                DrawHeroSheetDrawer(null, inMatch: false);
                return;
            }

            float w = Mathf.Min(1080f, Screen.width - 32f);
            float h = Mathf.Min(650f, Screen.height - 36f);
            float cardW = (w - 64f) * 0.25f;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            DrawBox(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.10f, 0.11f, 0.94f));
            DrawBox(panel, new Color(0.13f, 0.16f, 0.18f, 0.98f));

            GUILayout.BeginArea(new Rect(panel.x + 18, panel.y + 14, panel.width - 36, panel.height - 28));
            GUILayout.Label("<b><size=20>ULTIMATE FREE MOBA</size></b>  <color=#b0bec8><size=13>— Woodland Lane Prototype</size></color>");
            string rtsTag = scene.Settings != null && scene.Settings.Data.enableOptionalRtsControls ? "RTS: ON" : "RTS: OFF";
            GUILayout.Label(
                $"Abilities [{KeyLabel(ControlCommand.CastQ)}/{KeyLabel(ControlCommand.CastW)}/{KeyLabel(ControlCommand.CastE)}/{KeyLabel(ControlCommand.CastR)}] • " +
                $"[{KeyLabel(ControlCommand.AttackMove)}] Attack-Move • [{KeyLabel(ControlCommand.Stop)}] Stop • [{KeyLabel(ControlCommand.Move)}] Move/Attack • " +
                $"[{KeyLabel(ControlCommand.ToggleShop)}] Shop • [{KeyLabel(ControlCommand.ToggleHeroSheet)}] Scaling • [{KeyLabel(ControlCommand.ToggleSettings)}] Settings ({rtsTag})");
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>1. Choose Your Hero:</b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Hero Numbers & Scaling", GUILayout.Width(175f), GUILayout.Height(24f)))
            {
                if (HeroSheetOpen) HeroSheetOpen = false;
                else OpenHeroSheet(scene.LocalPick);
            }
            if (GUILayout.Button($"Settings ({KeyLabel(ControlCommand.ToggleSettings)})", GUILayout.Width(135f), GUILayout.Height(24f)))
            {
                if (SettingsOpen) SettingsOpen = false;
                else OpenSettings();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawHeroPickCard(LaneHeroId.Ogre, cardW);
            DrawHeroPickCard(LaneHeroId.Miravel, cardW);
            DrawHeroPickCard(LaneHeroId.Swordsman, cardW);
            DrawHeroPickCard(LaneHeroId.LaserShooter, cardW);
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.BeginHorizontal();

            // Practice vs Bot box
            GUILayout.BeginVertical("box", GUILayout.Width(w * 0.34f));
            GUILayout.Label("<b>Solo vs Sparring Bot</b>");
            GUILayout.Label($"Bot Hero: <b>{LaneNames.DisplayName(scene.RemoteOrBotPick)}</b>");
            GUILayout.BeginHorizontal();
            foreach (LaneHeroId id in new[] { LaneHeroId.Ogre, LaneHeroId.Miravel, LaneHeroId.Swordsman, LaneHeroId.LaserShooter })
            {
                if (GUILayout.Button(LaneNames.ShortName(id), GUILayout.Height(26))) scene.SetBotHero(id);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            if (GUILayout.Button("Start Lane vs Bot", GUILayout.Height(36))) scene.StartPractice();
            GUILayout.EndVertical();

            // Unity Relay box
            GUILayout.BeginVertical("box", GUILayout.Width(w * 0.34f));
            GUILayout.Label("<b>Online via Unity Relay (Free Room Code)</b>");
            if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
            {
                GUILayout.Label("<color=#ffcf66>Note: Unity Cloud Project ID is unlinked in Project Settings -> Services. Link free project in Editor for Room Codes, or use Direct TCP.</color>");
            }
            if (GUILayout.Button("Host Relay Room", GUILayout.Height(28))) scene.StartHostRelay();
            if (scene.ActiveLink != null && !string.IsNullOrEmpty(scene.ActiveLink.JoinCode))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Room Code: <size=16><b>{scene.ActiveLink.JoinCode}</b></size>");
                if (GUILayout.Button("Copy", GUILayout.Width(54), GUILayout.Height(24)))
                {
                    GUIUtility.systemCopyBuffer = scene.ActiveLink.JoinCode;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            relayJoinInput = GUILayout.TextField(relayJoinInput, 12, GUILayout.Width(96));
            if (GUILayout.Button("Paste", GUILayout.Width(50), GUILayout.Height(24)))
            {
                relayJoinInput = (GUIUtility.systemCopyBuffer ?? string.Empty).Trim().ToUpperInvariant();
            }
            if (GUILayout.Button("Join Code", GUILayout.Height(24))) scene.JoinRelay(relayJoinInput);
            GUILayout.EndHorizontal();
            if (scene.ActiveLink != null && scene.ActiveLink.IsConnected && !string.IsNullOrEmpty(scene.ActiveLink.JoinCode))
            {
                GUILayout.Label($"Friend picked: <b>{LaneNames.ShortName(scene.RemoteOrBotPick)}</b>");
                if (scene.ActiveLink.IsHost && GUILayout.Button("Launch Online Match!", GUILayout.Height(30)))
                {
                    scene.BeginOnlineHostMatchFromLobby();
                }
            }
            GUILayout.EndVertical();

            // Direct TCP box
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Direct TCP Fallback (:47777)</b>");
            if (GUILayout.Button("Host TCP (0.0.0.0:47777)", GUILayout.Height(28))) scene.StartHostTcp("0.0.0.0", 47777);
            GUILayout.BeginHorizontal();
            tcpJoinInput = GUILayout.TextField(tcpJoinInput, 40, GUILayout.Width(155));
            if (GUILayout.Button("Join TCP", GUILayout.Height(24))) scene.JoinTcp(tcpJoinInput);
            GUILayout.EndHorizontal();
            if (scene.ActiveLink != null && scene.ActiveLink.IsHost && scene.ActiveLink.IsConnected)
            {
                GUILayout.Label($"Friend picked: <b>{LaneNames.ShortName(scene.RemoteOrBotPick)}</b>");
                if (GUILayout.Button("Launch TCP Match!", GUILayout.Height(30))) scene.BeginOnlineHostMatchFromLobby();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            string status = scene.StatusBanner;
            if (!string.IsNullOrEmpty(status))
            {
                GUILayout.Label($"<b>Status:</b> {status}");
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal("box");
            LaneUpdater upd = scene.Updater;
            GUILayout.Label($"<b>Build:</b> {LaneUpdater.BuildVersion}", GUILayout.Width(92));
            int cdRem = upd != null ? upd.GetCooldownRemainingSeconds() : 0;
            bool inErrorCooldown = cdRem > 0 && upd.LastErrorKind != UpdateErrorKind.None;
            GUI.enabled = upd != null && !upd.IsBusy;
            string checkLabel = inErrorCooldown ? $"Wait ({cdRem}s)" : "Check for Updates";
            if (GUILayout.Button(checkLabel, GUILayout.Width(145), GUILayout.Height(24)))
            {
                scene.CheckForUpdates();
            }
            if (upd != null && upd.UpdateAvailable)
            {
                GUI.backgroundColor = new Color(0.35f, 0.85f, 0.48f);
                string sizeTag = upd.LatestSizeBytes > 0 ? $" ({upd.LatestSizeBytes / (1024f * 1024f):0.0} MB)" : string.Empty;
                if (GUILayout.Button($"Update to {upd.LatestTag}{sizeTag}", GUILayout.Width(185), GUILayout.Height(24)))
                {
                    scene.DownloadAndApplyUpdate();
                }
                GUI.backgroundColor = Color.white;
            }
            GUI.enabled = true;
            if (upd != null)
            {
                bool emphasizeFallback = upd.ShowBrowserFallback;
                if (emphasizeFallback) GUI.backgroundColor = new Color(0.95f, 0.78f, 0.35f);
                string browserBtn = emphasizeFallback ? "Open Download Page" : "Releases";
                float browserW = emphasizeFallback ? 148f : 76f;
                if (GUILayout.Button(browserBtn, GUILayout.Width(browserW), GUILayout.Height(24)))
                {
                    scene.OpenReleasesInBrowser();
                }
                GUI.backgroundColor = Color.white;
            }
            if (upd != null && !string.IsNullOrEmpty(upd.Status))
            {
                GUILayout.Label(upd.Status);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawHeroPickCard(LaneHeroId id, float cardW)
        {
            bool selected = scene.LocalPick == id;
            GUILayout.BeginVertical("box", GUILayout.Width(cardW));
            GUI.backgroundColor = selected ? new Color(0.35f, 0.78f, 0.52f) : Color.white;
            if (GUILayout.Button($"{(selected ? "[SELECTED] " : "")}{LaneNames.DisplayName(id)}", GUILayout.Height(30)))
            {
                scene.SetLocalPick(id);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Label(LaneNames.RoleSummary(id));
            if (GUILayout.Button("Numbers & Scaling", GUILayout.Height(22)))
            {
                OpenHeroSheet(id);
            }
            GUILayout.EndVertical();
        }

        private void DrawWorldBars(Camera cam, HeroSnap local)
        {
            // Heroes
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                HeroSnap h = snap.heroes[i];
                if (h.phase == LanePhase.Dead) continue;
                if (h.invisible && h.team != localTeam) continue;

                Vector3 scr = cam.WorldToScreenPoint(new Vector3(h.x, 2.25f, h.y));
                if (scr.z <= 0f) continue;
                float x = scr.x - 42f;
                float y = Screen.height - scr.y;

                Color fill = h.team == LaneTeam.Blue ? new Color(0.26f, 0.72f, 1f) : new Color(0.95f, 0.34f, 0.30f);
                float hpPct = h.hp / Mathf.Max(1f, h.maxHp);
                DrawBar(new Rect(x, y, 84f, 9f), hpPct, fill);
                if (h.shieldHp > 1f)
                {
                    float shPct = Mathf.Clamp01(h.shieldHp / Mathf.Max(1f, h.maxHp));
                    float barX = x + Mathf.Clamp01(hpPct) * 84f;
                    float shW = Mathf.Min(84f - (barX - x), shPct * 84f);
                    if (shW < 6f)
                    {
                        shW = Mathf.Min(84f, shPct * 84f);
                        barX = x + Mathf.Max(0f, 84f - shW);
                    }
                    DrawBox(new Rect(barX, y, shW, 9f), new Color(0.84f, 0.94f, 1f, 0.95f));
                }
                float labelY = y - 18f;
                if (h.resourceKind == HeroResourceKind.Mana && h.maxMana > 0f)
                {
                    DrawBar(new Rect(x, y + 10f, 84f, 4f), h.mana / Mathf.Max(1f, h.maxMana), new Color(0.24f, 0.62f, 0.98f));
                }

                string tag = $"L{h.level} {LaneNames.ShortName(h.heroId)}";
                if (h.stunTimer > 0f) tag += " [STUN]";
                else if (h.curseTimer > 0f) tag += $" [CURSE x{h.curseStacks}]";
                else if (h.wardTimer > 0f) tag += " [WARD]";
                if (h.laserStacks > 0) tag += $" [LASER x{h.laserStacks}]";
                if (h.miravelDrainAura > 0.05f) tag += " [DRAIN]";
                if (h.grievousTimer > 0f) tag += " [ANTI-HEAL]";
                if (h.shieldHp > 1f) tag += $" [SHIELD {Mathf.RoundToInt(h.shieldHp)}]";
                else if (h.cowardShieldBonus > 1f) tag += $" [COWARD +{Mathf.RoundToInt(h.cowardShieldBonus)}]";

                GUI.Label(new Rect(x - 42f, labelY, 184f, 18f), tag);
            }

            // Illusions & Laser Decoys
            for (int i = 0; i < snap.illusions.Count; i++)
            {
                IllusionSnap ill = snap.illusions[i];
                Vector3 scr = cam.WorldToScreenPoint(new Vector3(ill.x, 2.15f, ill.y));
                if (scr.z <= 0f) continue;
                float sy = Screen.height - scr.y;
                DrawBar(new Rect(scr.x - 35f, sy, 70f, 7f), ill.hp / Mathf.Max(1f, ill.maxHp),
                    new Color(0.45f, 0.92f, 1f));
                if (ill.isLaserDecoy)
                {
                    GUI.Label(new Rect(scr.x - 32f, sy - 16f, 72f, 16f), ill.laserStacks > 0 ? $"DECOY L{ill.laserStacks}" : "DECOY");
                }
            }

            // Minions
            for (int i = 0; i < snap.minions.Count; i++)
            {
                MinionSnap m = snap.minions[i];
                Vector3 scr = cam.WorldToScreenPoint(new Vector3(m.x, 1.35f, m.y));
                if (scr.z <= 0f) continue;
                bool hovered = m.id == hoveredId;
                Color col = m.team == LaneTeam.Blue ? new Color(0.32f, 0.70f, 0.95f) : new Color(0.92f, 0.36f, 0.32f);
                float sy = Screen.height - scr.y;
                DrawBar(new Rect(scr.x - 18f, sy, 36f, hovered ? 6f : 4f), m.hp / Mathf.Max(1f, m.maxHp), col);
                if (m.laserStacks > 0)
                {
                    GUI.Label(new Rect(scr.x + 20f, sy - 7f, 42f, 16f), $"L{m.laserStacks}");
                }
            }

            // Towers
            for (int i = 0; i < snap.towers.Count; i++)
            {
                TowerSnap t = snap.towers[i];
                if (!t.alive) continue;
                Vector3 scr = cam.WorldToScreenPoint(new Vector3(t.x, 4.1f, t.y));
                if (scr.z <= 0f) continue;
                Color col = t.team == LaneTeam.Blue ? new Color(0.28f, 0.72f, 1f) : new Color(0.96f, 0.34f, 0.28f);
                DrawBar(new Rect(scr.x - 48f, Screen.height - scr.y, 96f, 10f), t.hp / Mathf.Max(1f, t.maxHp), col);
                GUI.Label(new Rect(scr.x - 36f, Screen.height - scr.y - 18f, 80f, 18f), $"{Mathf.CeilToInt(t.hp)} HP");
            }
        }

        private void DrawPopups(Camera cam)
        {
            for (int i = 0; i < popups.Count; i++)
            {
                FloatingPopup p = popups[i];
                Vector3 scr = cam.WorldToScreenPoint(p.worldPos);
                if (scr.z <= 0f) continue;
                Color prev = GUI.color;
                Color c = p.color;
                c.a = 1f - (p.age / p.duration);
                GUI.color = c;
                GUI.Label(new Rect(scr.x - 45f, Screen.height - scr.y, 120f, 22f), $"<b>{p.text}</b>");
                GUI.color = prev;
            }
        }

        private void DrawBar(Rect r, float pct, Color fill)
        {
            DrawBox(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(0f, 0f, 0f, 0.78f));
            DrawBox(r, new Color(0.18f, 0.20f, 0.22f, 0.9f));
            DrawBox(new Rect(r.x, r.y, r.width * Mathf.Clamp01(pct), r.height), fill);
        }

        private void DrawTopBar(HeroSnap self, HeroSnap enemy, bool camLocked)
        {
            var bar = new Rect(10f, 8f, Screen.width - 20f, 36f);
            TrackPointer(bar);
            DrawBox(bar, new Color(0.09f, 0.11f, 0.13f, 0.88f));

            GUILayout.BeginArea(new Rect(bar.x + 8, bar.y + 4, bar.width - 16, bar.height - 8));
            GUILayout.BeginHorizontal();
            int mins = Mathf.FloorToInt(snap.matchTime / 60f);
            int secs = Mathf.FloorToInt(snap.matchTime % 60f);

            string kda = self != null ? $"{self.kills}/{self.deaths} CS:{self.creepScore}" : "";
            TowerSnap blueT = snap.towers.Count > 0 ? snap.towers[0] : null;
            TowerSnap redT = snap.towers.Count > 1 ? snap.towers[1] : null;
            string twInfo = $"Towers B:{(blueT != null && blueT.alive ? Mathf.CeilToInt(blueT.hp).ToString() : "DOWN")} | R:{(redT != null && redT.alive ? Mathf.CeilToInt(redT.hp).ToString() : "DOWN")}";
            string pingInfo = scene.ActiveLink != null ? $"Ping:{scene.ActiveLink.PingMs}ms" : "Bot";

            GUILayout.Label($"<b>{mins:00}:{secs:00}</b>  Wave {snap.waveNumber} (next {snap.nextWaveIn:0.0}s)  •  {kda}  •  {twInfo}  •  {pingInfo}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"[{KeyLabel(ControlCommand.ToggleCameraLock)}] {(camLocked ? "Cam:Locked" : "Cam:Free")}", GUILayout.Width(116))) scene.ToggleCameraLock();
            if (GUILayout.Button($"[{KeyLabel(ControlCommand.ToggleHeroSheet)}] Scaling", GUILayout.Width(90)))
            {
                if (HeroSheetOpen) HeroSheetOpen = false;
                else OpenHeroSheet(self != null ? self.heroId : scene.LocalPick);
            }
            if (GUILayout.Button($"[{KeyLabel(ControlCommand.ToggleShop)}] Shop", GUILayout.Width(80))) ShopOpen = !ShopOpen;
            if (GUILayout.Button($"[{KeyLabel(ControlCommand.ToggleSettings)}] Settings", GUILayout.Width(98)))
            {
                if (SettingsOpen) SettingsOpen = false;
                else OpenSettings();
            }
            if (GUILayout.Button($"[{KeyLabel(ControlCommand.ToggleHelp)}] Help", GUILayout.Width(80))) ShowHelp = !ShowHelp;
            if (GUILayout.Button("Lobby", GUILayout.Width(64))) scene.ReturnToLobby();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawCenterBanners(HeroSnap self)
        {
            if (!string.IsNullOrEmpty(snap.banner))
            {
                var r = new Rect((Screen.width - 520f) * 0.5f, 52f, 520f, 34f);
                DrawBox(r, new Color(0.12f, 0.10f, 0.14f, 0.88f));
                GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 24f), $"<b><size=15>{snap.banner}</size></b>");
            }

            if (self != null && self.heroId == LaneHeroId.Ogre && self.phase == LanePhase.Channeling && self.activeSlot == LaneAbilitySlot.R && !snap.gameOver)
            {
                string qKey = KeyLabel(ControlCommand.CastQ);
                string wKey = KeyLabel(ControlCommand.CastW);
                float cw = 560f;
                var cr = new Rect((Screen.width - cw) * 0.5f, 104f, cw, 46f);
                DrawBox(cr, new Color(0.16f, 0.12f, 0.05f, 0.94f));
                string slots = string.Empty;
                for (int i = 0; i < tuning.ogreRMaxStrikes; i++)
                {
                    if (i > 0) slots += "  ";
                    if (self.rQueue != null && i < self.rQueue.Count)
                    {
                        slots += self.rQueue[i].slot == (int)LaneAbilitySlot.Q ? $"[{qKey}]" : $"[{wKey}]";
                    }
                    else
                    {
                        slots += "[ - ]";
                    }
                }
                GUI.Label(new Rect(cr.x + 12f, cr.y + 4f, cr.width - 24f, 20f),
                    $"<b><color=#ffcf44>RAMPAGE CHANNEL ({self.phaseTimer:0.0}s) — Press [{qKey}] or [{wKey}] at cursor to queue strikes!</color></b>");
                GUI.Label(new Rect(cr.x + 12f, cr.y + 23f, cr.width - 24f, 20f),
                    $"<color=#fff2c2>Queued ({self.rComboCount}/{tuning.ogreRMaxStrikes}):  <b>{slots}</b></color>");
            }

            if (snap.gameOver)
            {
                var win = new Rect((Screen.width - 360f) * 0.5f, (Screen.height - 150f) * 0.42f, 360f, 150f);
                TrackPointer(win);
                DrawBox(win, new Color(0.10f, 0.12f, 0.14f, 0.96f));
                GUILayout.BeginArea(new Rect(win.x + 16, win.y + 14, win.width - 32, win.height - 28));
                bool won = snap.winnerTeam == (int)localTeam;
                GUILayout.Label($"<b><size=20>{(won ? "VICTORY!" : "DEFEAT")}</size></b>");
                GUILayout.Label(snap.banner);
                GUILayout.Space(12);
                GUILayout.BeginHorizontal();
                if (scene.IsHostOrPractice && GUILayout.Button("Rematch", GUILayout.Height(34))) scene.Rematch();
                if (GUILayout.Button("Return to Lobby", GUILayout.Height(34))) scene.ReturnToLobby();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        private void DrawBottomDock(HeroSnap h)
        {
            float dockW = Mathf.Min(900f, Screen.width - 220f);
            float dockH = 142f;
            var dock = new Rect(10f, Screen.height - dockH - 10f, dockW, dockH);
            TrackPointer(dock);
            DrawBox(dock, new Color(0.09f, 0.11f, 0.13f, 0.92f));

            GUILayout.BeginArea(new Rect(dock.x + 10, dock.y + 8, dock.width - 20, dock.height - 16));
            GUILayout.BeginHorizontal();

            // Left: Separated Conventional Stats vs Scaling Attributes
            GUILayout.BeginVertical(GUILayout.Width(252f));
            GUILayout.Label($"<b>{LaneNames.DisplayName(h.heroId)}</b> Lvl {h.level}  ({Mathf.RoundToInt(h.xp)}/{Mathf.RoundToInt(h.xpToNext)} XP)");
            string regenCol = h.netRegenRate < -0.05f ? "#ff7b7b" : "#9effbe";
            GUILayout.Label($"HP {Mathf.CeilToInt(h.hp)}/{Mathf.RoundToInt(h.maxHp)} <color={regenCol}>({h.netRegenRate:+0.0;-0.0;+0.0}/s)</color>" +
                            (h.phase == LanePhase.Dead ? $" <color=#ff8888>RESPAWN {h.respawnTimer:0.0}s</color>" : ""));
            if (h.resourceKind == HeroResourceKind.Mana && h.maxMana > 0f)
            {
                string rcrTag = h.rcrPct > 0.005f ? $"  <color=#8cd8ff>RCR -{h.rcrPct * 100f:0}%</color>" : "";
                GUILayout.Label($"<color=#4da6ff>Mana {Mathf.FloorToInt(h.mana)}/{Mathf.RoundToInt(h.maxMana)} ({h.netManaRegen:+0.0;-0.0;+0.0}/s)</color>{rcrTag}");
            }
            GUILayout.Label($"AD:{h.attackDamage:0}  AS:{h.attackSpeed:0.00}x  AH:{h.abilityHaste:0} (-{h.cooldownReduction * 100f:0}% CD)  MS:{h.moveSpeed:0.00}");
            GUILayout.Label($"Arm:{h.armor:0}  MR:{h.magicResist:0}  HPwr:{h.healPower * 100f:0}%");
            GUILayout.Label($"<color=#9ad8ff>Scalars (0 free): STR {h.strength:0} • AGI {h.agility:0} • INT {h.intelligence:0}</color>");
            if (h.heroId == LaneHeroId.Ogre)
            {
                GUILayout.Label($"<color=#ffd070>Ogre Speed: Wind {h.ogreWindupSpeed:0.00}x (AGI) • Rec {h.ogreRecoverSpeed:0.00}x (AS)</color>");
            }
            else if (h.heroId == LaneHeroId.Swordsman && (h.swordStacks != 0 || h.swordMarkedTargetId > 0))
            {
                GUILayout.Label($"<color=#ffd070>W Resist: {(h.swordResistDelta >= 0 ? "+" : "")}{h.swordResistDelta:0}  R Mark: {(h.swordMarkedTargetId > 0 ? "READY" : "-")}</color>");
            }
            GUILayout.EndVertical();

            // Center: Ability Slots Q/W/E/R (respecting bound keys & hero control profile)
            HeroControlProfile activeProfile = HeroControlCatalog.ForHero(h.heroId);
            string qLbl = KeyLabel(ControlCommand.CastQ);
            string wLbl = KeyLabel(ControlCommand.CastW);
            string eLbl = KeyLabel(ControlCommand.CastE);
            string rLbl = KeyLabel(ControlCommand.CastR);
            GUILayout.BeginVertical();
            if (h.heroId == LaneHeroId.Ogre && h.phase == LanePhase.Channeling && h.activeSlot == LaneAbilitySlot.R)
            {
                GUILayout.Label($"<b><color=#ffcf44>{rLbl} CHANNEL: Press {qLbl} or {wLbl} at cursor ({h.rComboCount}/{tuning.ogreRMaxStrikes})</color></b>");
            }
            GUILayout.BeginHorizontal();
            DrawAbilitySlot(h, LaneAbilitySlot.Q, qLbl, h.rankQ, h.cdQ, h.maxCdQ, 3);
            DrawAbilitySlot(h, LaneAbilitySlot.W, wLbl, h.rankW, h.cdW, h.maxCdW, 3);
            DrawAbilitySlot(h, LaneAbilitySlot.E, eLbl, h.rankE, h.cdE, h.maxCdE, 3);
            int maxR = h.level >= tuning.maxLevel ? 2 : 1;
            DrawAbilitySlot(h, LaneAbilitySlot.R, rLbl, h.rankR, h.cdR, h.maxCdR, maxR);
            GUILayout.EndHorizontal();
            if (h.skillPoints > 0)
            {
                GUILayout.Label($"<color=#78ff9e><b>{h.skillPoints} Skill Point(s) available — Ctrl+{qLbl}/{wLbl}/{eLbl}/{rLbl} or click +</b></color>");
            }
            else if (!activeProfile.SupportsBasicAttack)
            {
                GUILayout.Label($"<color=#ffd088>Control Profile: No Basic Attacks • [{KeyLabel(ControlCommand.Stop)}] Stop</color>");
            }
            GUILayout.EndVertical();

            // Right: Gold & Inventory
            GUILayout.BeginVertical(GUILayout.Width(190f));
            GUILayout.Label($"<b>Gold: <color=#ffdf5c>{h.gold}g</color></b>");
            bool canShopNow = h.phase == LanePhase.Dead ||
                (new Vector2(h.x, h.y) - (localTeam == LaneTeam.Blue ? tuning.blueFountainPos : tuning.redFountainPos)).magnitude <= tuning.fountainRadius;
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 3; col++)
                {
                    int idx = row * 3 + col;
                    int id = h.items[idx];
                    LaneItemDef def = default;
                    bool hasItem = id > 0 && LaneItems.TryGet(id, out def);
                    string label = hasItem
                        ? def.Name.Substring(0, Mathf.Min(7, def.Name.Length))
                        : "-";
                    GUILayout.Box(label, GUILayout.Width(58f), GUILayout.Height(26f));
                    Rect boxR = GUILayoutUtility.GetLastRect();
                    if (hasItem && Event.current.type == EventType.Repaint && boxR.Contains(Event.current.mousePosition))
                    {
                        SetHoverTooltip(LaneHeroSheet.BuildItemTooltip(def, canShopNow), GUIUtility.GUIToScreenPoint(boxR.position), boxR.size);
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawAbilitySlot(HeroSnap h, LaneAbilitySlot slot, string key, int rank, float cd, float maxCd, int maxRank)
        {
            float manaCost = slot == LaneAbilitySlot.Q ? h.costQ
                : (slot == LaneAbilitySlot.W ? h.costW
                : (slot == LaneAbilitySlot.E ? h.costE : h.costR));
            bool isContinuous = slot == LaneAbilitySlot.R && h.costModeR == ResourceCostMode.ContinuousPerSecond;
            float requiredMana = isContinuous ? Mathf.Max(1f, manaCost * tuning.miravelRMinStartFraction) : manaCost;
            bool usesMana = h.resourceKind == HeroResourceKind.Mana && manaCost > 0.01f;
            bool noMana = usesMana && rank > 0 && (h.mana + 0.0001f < requiredMana);

            GUI.color = noMana ? new Color(0.75f, 0.80f, 0.90f, 0.90f) : Color.white;
            GUILayout.BeginVertical("box", GUILayout.Width(80f), GUILayout.Height(94f));
            GUI.color = Color.white;
            GUILayout.Label($"<b>[{key}]</b> {(rank > 0 ? $"R{rank}" : "Lock")}");
            bool isPassiveSlot = (h.heroId == LaneHeroId.Miravel && slot == LaneAbilitySlot.Q) ||
                                 (h.heroId == LaneHeroId.Swordsman && (slot == LaneAbilitySlot.W || slot == LaneAbilitySlot.E));
            if (isPassiveSlot)
            {
                GUILayout.Label("<color=#9de0ff>PASSIVE</color>");
            }
            else if (rank <= 0)
            {
                GUILayout.Label("-");
            }
            else if (cd > 0.05f)
            {
                GUILayout.Label($"<color=#ff9070><b>{cd:0.0}s</b></color>");
            }
            else if (noMana)
            {
                GUILayout.Label("<color=#ff7878><b>NO MANA</b></color>");
            }
            else
            {
                GUILayout.Label("<color=#80ff9f>READY</color>");
            }
            if (usesMana && !isPassiveSlot)
            {
                string costCol = noMana ? "#ff8282" : "#62c6ff";
                string costStr = isContinuous ? $"{manaCost:0}/s" : $"{manaCost:0}M";
                GUILayout.Label($"<color={costCol}><b>{costStr}</b></color>");
            }

            bool canLevel = h.skillPoints > 0 && rank < maxRank && (slot != LaneAbilitySlot.R || h.level >= tuning.ultMinLevel);
            if (canLevel && GUILayout.Button("+", GUILayout.Height(20f)))
            {
                scene.SendLocalAbilityLevelUp(slot);
            }
            GUILayout.EndVertical();

            Rect slotRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint && slotRect.Contains(Event.current.mousePosition))
            {
                LaneHeroSheet.AbilityCardData card = LaneHeroSheet.BuildAbilityTooltip(h.heroId, slot, tuning, h);
                if (card != null)
                {
                    SetHoverTooltip(FormatAbilityTooltip(card), GUIUtility.GUIToScreenPoint(slotRect.position), slotRect.size);
                }
            }
        }

        private void DrawShopDrawer(HeroSnap h)
        {
            EnsureStyles();
            float sw = 520f;
            float sh = Mathf.Min(580f, Screen.height - 170f);
            var r = new Rect(Screen.width - sw - 12f, 52f, sw, sh);
            TrackPointer(r);
            DrawBox(r, new Color(0.11f, 0.13f, 0.16f, 0.97f));

            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>FOUNTAIN SHOP</b> — Gold: <color=#ffdf5c>{h.gold}g</color>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(26))) ShopOpen = false;
            GUILayout.EndHorizontal();

            bool canShopHere = h.phase == LanePhase.Dead ||
                (new Vector2(h.x, h.y) - (localTeam == LaneTeam.Blue ? tuning.blueFountainPos : tuning.redFountainPos)).magnitude <= tuning.fountainRadius;
            if (!canShopHere)
            {
                GUILayout.Label("<color=#ffaa66>Move inside your Fountain (or buy/sell while Respawning).</color>");
            }
            if (!string.IsNullOrEmpty(shopFeedback))
            {
                GUILayout.Label($"<b>{shopFeedback}</b>");
            }

            GUILayout.BeginHorizontal();
            DrawShopTierTab("All", -1);
            DrawShopTierTab("Starter", (int)LaneItemTier.Starter);
            DrawShopTierTab("Core", (int)LaneItemTier.Core);
            DrawShopTierTab("Capstone", (int)LaneItemTier.Capstone);
            GUILayout.EndHorizontal();

            int ownedCount = 0;
            for (int s = 0; s < h.items.Length; s++)
            {
                if (h.items[s] > 0) ownedCount++;
            }
            if (ownedCount > 0)
            {
                GUILayout.Label($"<b>Owned Items — Sell ({Mathf.RoundToInt(LaneItems.SellRefundRatio * 100f)}% refund):</b>");
                int drawn = 0;
                for (int s = 0; s < h.items.Length; s++)
                {
                    int ownedId = h.items[s];
                    if (ownedId <= 0 || !LaneItems.TryGet(ownedId, out LaneItemDef ownedDef)) continue;
                    if (drawn % 2 == 0) GUILayout.BeginHorizontal();
                    int refund = LaneItems.SellRefund(ownedDef.Cost);
                    GUILayout.BeginHorizontal(cardBoxStyle, GUILayout.Width(240f));
                    GUILayout.Label($"{ownedDef.Name}", sheetTextStyle, GUILayout.Width(134f));
                    GUI.enabled = canShopHere;
                    if (GUILayout.Button($"Sell +{refund}g", GUILayout.Width(82f)))
                    {
                        scene.SendLocalSellItem(s);
                        shopFeedback = $"Sold {ownedDef.Name} (+{refund}g)";
                        shopFeedbackTimer = 2.0f;
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    Rect ownedRect = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.Repaint && ownedRect.Contains(Event.current.mousePosition))
                    {
                        SetHoverTooltip(LaneHeroSheet.BuildItemTooltip(ownedDef, canShopHere), GUIUtility.GUIToScreenPoint(ownedRect.position), ownedRect.size);
                    }
                    drawn++;
                    if (drawn % 2 == 0) GUILayout.EndHorizontal();
                }
                if (drawn % 2 != 0) GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            shopScroll = GUILayout.BeginScrollView(shopScroll);
            for (int i = 0; i < LaneItems.Catalog.Length; i++)
            {
                LaneItemDef item = LaneItems.Catalog[i];
                if (shopTierFilter >= 0 && (int)item.Tier != shopTierFilter) continue;
                string tierCol = item.Tier == LaneItemTier.Starter ? LaneHeroSheet.ColMuted
                    : (item.Tier == LaneItemTier.Capstone ? LaneHeroSheet.ColCc : LaneHeroSheet.ColInt);

                GUILayout.BeginHorizontal(cardBoxStyle);
                GUILayout.Label($"<color={tierCol}>[{item.Tier}]</color> <b>{item.Name}</b> <color=#ffdf5c>({item.Cost}g)</color> — {item.Summary}", sheetTextStyle, GUILayout.Width(400f));
                GUI.enabled = canShopHere && h.gold >= item.Cost;
                if (GUILayout.Button("Buy", GUILayout.Width(56f), GUILayout.Height(26f)))
                {
                    scene.SendLocalBuyItem(item.Id);
                    shopFeedback = $"Ordered {item.Name}";
                    shopFeedbackTimer = 2.0f;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                Rect itemRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.Repaint && itemRect.Contains(Event.current.mousePosition))
                {
                    SetHoverTooltip(LaneHeroSheet.BuildItemTooltip(item, canShopHere), GUIUtility.GUIToScreenPoint(itemRect.position), itemRect.size);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawShopTierTab(string label, int tierVal)
        {
            bool active = shopTierFilter == tierVal;
            GUI.backgroundColor = active ? new Color(0.35f, 0.78f, 0.52f) : Color.white;
            if (GUILayout.Button(label, GUILayout.Height(22f)))
            {
                shopTierFilter = tierVal;
                shopScroll = Vector2.zero;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawMinimap(HeroSnap self, HeroSnap enemy)
        {
            float mw = 192f;
            float mh = 76f;
            var r = new Rect(Screen.width - mw - 10f, Screen.height - mh - 10f, mw, mh);
            TrackPointer(r);
            DrawBox(r, new Color(0.08f, 0.10f, 0.11f, 0.92f));

            void Dot(float wx, float wy, float sz, Color c)
            {
                float u = Mathf.InverseLerp(-tuning.laneHalfLength, tuning.laneHalfLength, wx);
                float v = 1f - Mathf.InverseLerp(-tuning.laneHalfWidth, tuning.laneHalfWidth, wy);
                DrawBox(new Rect(r.x + u * (r.width - sz), r.y + v * (r.height - sz), sz, sz), c);
            }

            for (int i = 0; i < snap.towers.Count; i++)
            {
                TowerSnap t = snap.towers[i];
                if (!t.alive) continue;
                Dot(t.x, t.y, 7f, t.team == LaneTeam.Blue ? Color.cyan : Color.red);
            }
            for (int i = 0; i < snap.minions.Count; i++)
            {
                MinionSnap m = snap.minions[i];
                Dot(m.x, m.y, 3f, m.team == LaneTeam.Blue ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.5f, 0.5f));
            }
            for (int i = 0; i < snap.orbs.Count; i++)
            {
                OrbSnap o = snap.orbs[i];
                Dot(o.x, o.y, 4f, Color.green);
            }
            if (self != null && self.phase != LanePhase.Dead) Dot(self.x, self.y, 7f, Color.yellow);
            if (enemy != null && enemy.phase != LanePhase.Dead && !enemy.invisible) Dot(enemy.x, enemy.y, 7f, new Color(1f, 0.45f, 0.1f));
        }

        private void DrawHeroSheetDrawer(HeroSnap liveSelf, bool inMatch)
        {
            EnsureStyles();
            LaneAbilityPreview preview = scene.AbilityPreview;
            bool demosOn = scene.Settings == null || scene.Settings.Data.enableAbilityDemos;
            if (preview != null && preview.SelectedHero != inspectedHero)
            {
                preview.Select(inspectedHero, previewSlot, 0);
            }

            float w = inMatch
                ? Mathf.Min(Screen.width - 20f, Mathf.Clamp(Screen.width * 0.84f, 760f, 1180f))
                : Mathf.Min(Screen.width - 20f, Mathf.Clamp(Screen.width * 0.88f, 780f, 1260f));
            float h = inMatch
                ? Mathf.Min(Screen.height - 164f, Mathf.Clamp(Screen.height * 0.82f, 460f, 760f))
                : Mathf.Min(Screen.height - 24f, Mathf.Clamp(Screen.height * 0.88f, 540f, 840f));
            var r = inMatch
                ? new Rect(10f, 52f, w, h)
                : new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            TrackPointer(r);
            DrawBox(r, new Color(0.08f, 0.10f, 0.13f, 0.98f));

            float innerW = r.width - 28f;
            float leftW = Mathf.Floor((innerW - 12f) * 0.56f);
            float rightW = Mathf.Max(280f, innerW - leftW - 12f);
            float leftContentW = Mathf.Max(300f, leftW - 34f);

            GUILayout.BeginArea(new Rect(r.x + 14f, r.y + 12f, innerW, r.height - 24f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={LaneHeroSheet.ColText}><b><size=15>HERO GUIDE, PREVIEW & SCALING SANDBOX</size></b></color>");
            GUILayout.FlexibleSpace();
            foreach (LaneHeroId id in new[] { LaneHeroId.Ogre, LaneHeroId.Miravel, LaneHeroId.Swordsman, LaneHeroId.LaserShooter })
            {
                bool active = inspectedHero == id;
                GUI.backgroundColor = active ? new Color(0.32f, 0.76f, 0.52f) : new Color(0.75f, 0.80f, 0.88f);
                if (GUILayout.Button(LaneNames.ShortName(id), GUILayout.Width(76f), GUILayout.Height(25f)))
                {
                    inspectedHero = id;
                    heroSheetScroll = Vector2.zero;
                    previewPaneScroll = Vector2.zero;
                    preview?.Select(id, previewSlot, 0);
                }
            }
            GUI.backgroundColor = showExactMath ? new Color(0.34f, 0.60f, 0.94f) : new Color(0.75f, 0.80f, 0.88f);
            if (GUILayout.Button(showExactMath ? "Hide All Math" : "Show All Math", GUILayout.Width(102f), GUILayout.Height(25f)))
            {
                showExactMath = !showExactMath;
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("X", GUILayout.Width(28f), GUILayout.Height(25f)))
            {
                HeroSheetOpen = false;
                preview?.Deactivate();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            bool hasLiveMatchHero = liveSelf != null && liveSelf.heroId == inspectedHero;
            bool usingSandbox = !hasLiveMatchHero || previewSandboxMode;
            HeroSnap displayedSnap = hasLiveMatchHero && !usingSandbox
                ? liveSelf
                : (preview != null ? LaneHeroSheet.EvaluatePreviewHeroSnap(inspectedHero, tuning, preview.Overrides) : null);

            LaneHeroSheet.HeroGuideData guide = LaneHeroSheet.BuildHeroGuide(inspectedHero, tuning, displayedSnap);

            GUILayout.BeginHorizontal();

            // LEFT PANE: Structured Ability Guide Cards
            heroSheetScroll = GUILayout.BeginScrollView(heroSheetScroll, GUILayout.Width(leftW));

            GUILayout.BeginVertical(cardBoxStyle);
            string snapBadge = (hasLiveMatchHero && !usingSandbox)
                ? $"<color={LaneHeroSheet.ColLive}><b>[LIVE MATCH]</b></color>"
                : $"<color={LaneHeroSheet.ColInt}><b>[SANDBOX Lv {(preview != null ? preview.Overrides.level : 1)}]</b></color>";
            GUILayout.Label($"<color={LaneHeroSheet.ColText}><b><size=16>{guide.Title}</size></b></color> {snapBadge}  <color={LaneHeroSheet.ColMuted}>— {guide.IdentitySummary}</color>", cardHeaderStyle);
            GUILayout.Space(3);
            GUILayout.Label(guide.BaseStatsLine, sheetTextStyle);
            if (!string.IsNullOrEmpty(guide.LiveStatsLine))
            {
                GUILayout.Label(guide.LiveStatsLine, sheetTextStyle);
            }
            GUILayout.EndVertical();

            for (int i = 0; i < guide.Cards.Count; i++)
            {
                LaneHeroSheet.AbilityCardData card = guide.Cards[i];
                int mathKey = ((int)inspectedHero * 32) + i;
                bool cardMathOpen = showExactMath || expandedMathCards.Contains(mathKey);
                bool isPreviewed = card.Slot == previewSlot;

                GUILayout.Space(7);
                GUI.backgroundColor = isPreviewed ? new Color(0.84f, 0.96f, 0.90f) : Color.white;
                GUILayout.BeginVertical(cardBoxStyle);
                GUI.backgroundColor = Color.white;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"<color={LaneHeroSheet.ColCc}><b>[{card.KeyBadge}]</b></color> <color={LaneHeroSheet.ColText}><b>{card.Name}</b></color>", cardHeaderStyle);
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrEmpty(card.MetaRight))
                {
                    GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>{card.MetaRight}</color>", sheetTextStyle);
                    GUILayout.Space(4);
                }
                GUI.backgroundColor = isPreviewed ? new Color(0.32f, 0.78f, 0.56f) : new Color(0.56f, 0.64f, 0.74f);
                if (GUILayout.Button(isPreviewed ? "Previewing" : "Preview", GUILayout.Width(76f), GUILayout.Height(21f)))
                {
                    previewSlot = card.Slot;
                    preview?.Select(inspectedHero, previewSlot, 0);
                }
                if (card.MathLines.Count > 0)
                {
                    GUI.backgroundColor = cardMathOpen ? new Color(0.36f, 0.62f, 0.94f) : new Color(0.66f, 0.72f, 0.80f);
                    if (GUILayout.Button(cardMathOpen ? "Hide Math" : "Show Math", GUILayout.Width(74f), GUILayout.Height(21f)))
                    {
                        if (showExactMath)
                        {
                            showExactMath = false;
                            expandedMathCards.Remove(mathKey);
                        }
                        else if (!expandedMathCards.Remove(mathKey))
                        {
                            expandedMathCards.Add(mathKey);
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(card.PlainDescription))
                {
                    GUILayout.Space(3);
                    GUILayout.Label(card.PlainDescription, sheetBodyStyle);
                }

                if (card.KeyFacts.Count > 0)
                {
                    GUILayout.Space(4);
                    float usedW = 0f;
                    GUILayout.BeginHorizontal();
                    for (int k = 0; k < card.KeyFacts.Count; k++)
                    {
                        LaneHeroSheet.KeyFactPill kf = card.KeyFacts[k];
                        string pillMarkup = $"<color={LaneHeroSheet.ColMuted}>{kf.Label}:</color> <color={kf.ColorHex}><b>{kf.Value}</b></color>";
                        float pillW = Mathf.Min(leftContentW - 12f, sheetFactPillStyle.CalcSize(new GUIContent(pillMarkup)).x + 12f);
                        if (usedW > 0f && usedW + pillW > leftContentW - 12f)
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                            GUILayout.Space(2);
                            GUILayout.BeginHorizontal();
                            usedW = 0f;
                        }
                        GUILayout.Label(pillMarkup, sheetFactPillStyle, GUILayout.Width(pillW));
                        GUILayout.Space(4);
                        usedW += pillW + 4f;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }

                if (card.ScalingRows.Count > 0)
                {
                    GUILayout.Space(5);
                    GUILayout.Label($"<color={LaneHeroSheet.ColMuted}><b>WHAT SCALES?</b></color>", sheetSectionStyle);
                    for (int s = 0; s < card.ScalingRows.Count; s++)
                    {
                        LaneHeroSheet.ScalingRow row = card.ScalingRows[s];
                        string liveSuffix = string.IsNullOrEmpty(row.LiveText)
                            ? string.Empty
                            : $"  <color={LaneHeroSheet.ColLive}><b>(Now: {row.LiveText})</b></color>";
                        GUILayout.Label(
                            $"  • <color={row.StatColorHex}><b>{row.StatTag}</b></color> → <color={LaneHeroSheet.ColText}>{row.AffectedProperty}</color> <color={LaneHeroSheet.ColMuted}>[{row.CurveLabel}]</color>{liveSuffix}",
                            sheetTextStyle);
                    }
                }

                if (!string.IsNullOrEmpty(card.LiveLine))
                {
                    GUILayout.Space(3);
                    GUILayout.Label(card.LiveLine, sheetTextStyle);
                }

                if (cardMathOpen && card.MathLines.Count > 0)
                {
                    GUILayout.Space(4);
                    GUILayout.BeginVertical(sheetMathBoxStyle);
                    GUILayout.Label($"<color={LaneHeroSheet.ColInt}><b>EXACT MATH</b></color>", sheetSectionStyle);
                    for (int m = 0; m < card.MathLines.Count; m++)
                    {
                        GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>• {card.MathLines[m]}</color>", sheetTextStyle);
                    }
                    GUILayout.EndVertical();
                }

                if (!string.IsNullOrEmpty(card.PracticalTip))
                {
                    GUILayout.Space(4);
                    GUILayout.Label($"<color={LaneHeroSheet.ColCc}><b>Tip:</b></color> <color=#dce7cf>{card.PracticalTip}</color>", sheetTipBoxStyle);
                }

                GUILayout.EndVertical();
            }

            GUILayout.Space(7);
            GUILayout.BeginVertical(cardBoxStyle);
            GUILayout.Label($"<b><color={LaneHeroSheet.ColInt}>SHARED ATTRIBUTE & ITEM RULES</color></b>", cardHeaderStyle);
            for (int i = 0; i < guide.RuleLines.Count; i++)
            {
                GUILayout.Label($"• {guide.RuleLines[i]}", sheetTextStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // RIGHT PANE: Isolated Ability Demonstration & Interactive Scaling Sandbox
            GUILayout.BeginVertical(cardBoxStyle, GUILayout.Width(rightW));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={LaneHeroSheet.ColText}><b>ABILITY DEMO & SANDBOX</b></color>", cardHeaderStyle);
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = demosOn ? new Color(0.32f, 0.76f, 0.52f) : new Color(0.65f, 0.45f, 0.45f);
            if (GUILayout.Button(demosOn ? "Demos: ON" : "Demos: OFF", GUILayout.Width(94f), GUILayout.Height(22f)))
            {
                bool nextDemos = !demosOn;
                scene.Settings?.SetAbilityDemosEnabled(nextDemos);
                if (!nextDemos) preview?.Deactivate();
                demosOn = nextDemos;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            // Slot Selector Row
            GUILayout.BeginHorizontal();
            foreach (LaneAbilitySlot slotOpt in new[] { LaneAbilitySlot.None, LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E, LaneAbilitySlot.R })
            {
                string lbl = slotOpt == LaneAbilitySlot.None ? "Passive" : slotOpt.ToString();
                GUI.backgroundColor = previewSlot == slotOpt ? new Color(0.34f, 0.64f, 0.94f) : new Color(0.72f, 0.78f, 0.85f);
                if (GUILayout.Button(lbl, GUILayout.Height(22f)))
                {
                    previewSlot = slotOpt;
                    preview?.Select(inspectedHero, previewSlot, 0);
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // Multi-example switcher if available
            string[] examples = LaneAbilityPreview.GetExampleNames(inspectedHero, previewSlot);
            if (examples.Length > 1 && preview != null)
            {
                GUILayout.Space(3);
                GUILayout.BeginHorizontal();
                for (int e = 0; e < examples.Length; e++)
                {
                    GUI.backgroundColor = preview.ExampleIndex == e ? new Color(0.32f, 0.76f, 0.52f) : new Color(0.72f, 0.78f, 0.85f);
                    if (GUILayout.Button(examples[e], GUILayout.Height(21f)))
                    {
                        preview.Select(inspectedHero, previewSlot, e);
                    }
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            float boxW = Mathf.Max(240f, rightW - 22f);
            float boxH = Mathf.Clamp(boxW * 0.56f, 150f, 224f);
            Rect texRect = GUILayoutUtility.GetRect(boxW, boxH);
            DrawBox(texRect, new Color(0.05f, 0.07f, 0.10f, 1f));
            if (demosOn && preview != null && preview.PreviewTexture != null)
            {
                GUI.DrawTexture(texRect, preview.PreviewTexture, ScaleMode.ScaleToFit, false);
                preview.DrawOverlayBars(texRect, sheetSectionStyle);
            }
            else
            {
                GUI.Label(
                    new Rect(texRect.x + 12f, texRect.y + texRect.height * 0.34f, texRect.width - 24f, 64f),
                    $"<color={LaneHeroSheet.ColMuted}>Animated ability demonstrations are paused/disabled.\nSliders and scaling previews below remain live.</color>",
                    sheetTextStyle);
            }

            if (preview != null)
            {
                GUILayout.Space(3);
                GUILayout.BeginHorizontal();
                GUI.backgroundColor = preview.IsPaused ? new Color(0.84f, 0.68f, 0.30f) : new Color(0.32f, 0.76f, 0.52f);
                if (GUILayout.Button(preview.IsPaused ? "Play" : "Pause", GUILayout.Width(64f), GUILayout.Height(21f)))
                {
                    preview.IsPaused = !preview.IsPaused;
                }
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Replay", GUILayout.Width(64f), GUILayout.Height(21f)))
                {
                    preview.IsPaused = false;
                    preview.RestartScenario();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>Loop {preview.ElapsedInLoop:0.0}s / {preview.LoopDuration:0.0}s</color>", sheetSectionStyle);
                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(preview.ScenarioCaption))
                {
                    GUILayout.Label($"<color=#dce7cf>{preview.ScenarioCaption}</color>", sheetTextStyle);
                }

                GUILayout.Space(4);
                previewPaneScroll = GUILayout.BeginScrollView(previewPaneScroll);

                if (hasLiveMatchHero)
                {
                    GUILayout.BeginHorizontal();
                    GUI.backgroundColor = !previewSandboxMode ? new Color(0.32f, 0.76f, 0.52f) : new Color(0.68f, 0.74f, 0.80f);
                    if (GUILayout.Button("Live Match Stats", GUILayout.Height(21f))) previewSandboxMode = false;
                    GUI.backgroundColor = previewSandboxMode ? new Color(0.34f, 0.64f, 0.94f) : new Color(0.68f, 0.74f, 0.80f);
                    if (GUILayout.Button("Sandbox Sliders", GUILayout.Height(21f))) previewSandboxMode = true;
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3);
                }

                PreviewStatOverrides ov = preview.Overrides;
                int maxRank = previewSlot == LaneAbilitySlot.R ? 2 : 3;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<color={LaneHeroSheet.ColText}><b>Lv {ov.level}</b></color>", sheetTextStyle, GUILayout.Width(42f));
                if (GUILayout.Button("-", GUILayout.Width(24f), GUILayout.Height(20f)) && ov.level > 1)
                {
                    ov.level--;
                    previewSandboxMode = true;
                    preview.ApplyLiveOverrides(ov);
                }
                if (GUILayout.Button("+", GUILayout.Width(24f), GUILayout.Height(20f)) && ov.level < tuning.maxLevel)
                {
                    ov.level++;
                    previewSandboxMode = true;
                    preview.ApplyLiveOverrides(ov);
                }
                if (previewSlot != LaneAbilitySlot.None)
                {
                    GUILayout.Space(8);
                    GUILayout.Label($"<color={LaneHeroSheet.ColText}><b>Rk {Mathf.Clamp(ov.abilityRank, 1, maxRank)}</b></color>", sheetTextStyle, GUILayout.Width(42f));
                    if (GUILayout.Button("-", GUILayout.Width(24f), GUILayout.Height(20f)) && ov.abilityRank > 1)
                    {
                        ov.abilityRank--;
                        previewSandboxMode = true;
                        preview.ApplyLiveOverrides(ov);
                    }
                    if (GUILayout.Button("+", GUILayout.Width(24f), GUILayout.Height(20f)) && ov.abilityRank < maxRank)
                    {
                        ov.abilityRank++;
                        previewSandboxMode = true;
                        preview.ApplyLiveOverrides(ov);
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset", GUILayout.Width(58f), GUILayout.Height(20f)))
                {
                    preview.ResetOverrides();
                    previewSandboxMode = !hasLiveMatchHero;
                }
                GUILayout.EndHorizontal();

                List<PreviewSliderSpec> sliders = LaneAbilityPreview.GetRelevantSliders(inspectedHero, previewSlot);
                for (int si = 0; si < sliders.Count; si++)
                {
                    PreviewSliderSpec spec = sliders[si];
                    float cur = LaneAbilityPreview.GetOverrideValue(in ov, spec.Field);
                    string valText = spec.AsPercent ? $"+{cur * 100f:0}%" : $"+{cur:0}";
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<color={spec.ColorHex}><b>{spec.Label}</b></color>", sheetTextStyle, GUILayout.Width(124f));
                    float raw = GUILayout.HorizontalSlider(cur, spec.Min, spec.Max);
                    float snapped = Mathf.Clamp(Mathf.Round(raw / spec.Step) * spec.Step, spec.Min, spec.Max);
                    GUILayout.Label($"<b>{valText}</b>", sheetTextStyle, GUILayout.Width(46f));
                    GUILayout.EndHorizontal();
                    if (Mathf.Abs(snapped - cur) > 0.0001f)
                    {
                        LaneAbilityPreview.SetOverrideValue(ref ov, spec.Field, snapped);
                        previewSandboxMode = true;
                        preview.ApplyLiveOverrides(ov);
                    }
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawStatusBar(HeroSnap self, HeroSnap enemy)
        {
            if (self == null || self.phase == LanePhase.Dead) return;
            EnsureStyles();

            var pills = new List<(string text, Color bg)>();
            if (self.miravelLifeAura > 0.05f)
                pills.Add(($"<color={LaneHeroSheet.ColHeal}><b>LIFE AURA +{self.miravelLifeAura:0.0}/s</b></color>", new Color(0.08f, 0.22f, 0.16f, 0.90f)));
            if (self.miravelDrainAura > 0.05f)
                pills.Add(($"<color=#e09eff><b>REGEN DRAIN -{self.miravelDrainAura:0.0}/s</b></color>", new Color(0.24f, 0.10f, 0.30f, 0.92f)));
            if (self.grievousTimer > 0f)
                pills.Add(($"<color=#ff8a7a><b>ANTI-HEAL -35% ({self.grievousTimer:0.0}s)</b></color>", new Color(0.30f, 0.10f, 0.10f, 0.92f)));
            if (self.curseTimer > 0f)
                pills.Add(($"<color=#dca0ff><b>CURSED x{self.curseStacks} ({self.curseTimer:0.0}s)</b></color>", new Color(0.24f, 0.08f, 0.32f, 0.92f)));
            if (self.wardTimer > 0f)
                pills.Add(($"<color=#66ffd1><b>WARD ({self.wardTimer:0.0}s)</b></color>", new Color(0.08f, 0.24f, 0.22f, 0.92f)));
            if (self.shieldHp > 1f)
                pills.Add(($"<color=#bde8ff><b>SHIELD {Mathf.RoundToInt(self.shieldHp)} ({self.shieldTimer:0.0}s)</b></color>", new Color(0.10f, 0.20f, 0.28f, 0.92f)));
            if (self.cowardShieldBonus > 0.5f || self.cowardShieldState > 0)
            {
                string stateTag = self.cowardShieldState == 1 ? "BUILD"
                    : (self.cowardShieldState == 2 ? "ACTIVE"
                    : (self.cowardShieldState == 3 ? "HOLD" : "FADE"));
                Color stateBg = self.cowardShieldState == 2
                    ? new Color(0.10f, 0.24f, 0.28f, 0.92f)
                    : (self.cowardShieldState == 4 ? new Color(0.26f, 0.14f, 0.08f, 0.92f) : new Color(0.22f, 0.20f, 0.08f, 0.92f));
                pills.Add(($"<color=#ffe066><b>COWARD {stateTag} +{Mathf.RoundToInt(self.cowardShieldBonus)}</b></color>", stateBg));
            }
            if (self.spellweaverReady)
                pills.Add(($"<color=#70d0ff><b>ARCANE ECHO READY</b></color>", new Color(0.08f, 0.18f, 0.28f, 0.92f)));
            if (self.momentumBonus > 0.05f)
                pills.Add(($"<color=#78ff9e><b>MOMENTUM +{self.momentumBonus:0.00} MS</b></color>", new Color(0.08f, 0.22f, 0.12f, 0.92f)));
            if (self.stunTimer > 0f)
                pills.Add(($"<color=#ffe066><b>STUNNED ({self.stunTimer:0.0}s)</b></color>", new Color(0.32f, 0.24f, 0.06f, 0.92f)));
            if (self.slowTimer > 0f)
                pills.Add(($"<color=#ffb866><b>SLOWED {self.slowAmount * 100f:0}% ({self.slowTimer:0.0}s)</b></color>", new Color(0.28f, 0.16f, 0.08f, 0.92f)));
            if (self.laserStacks > 0)
                pills.Add(($"<color=#4ee8ff><b>LASER STACKS x{self.laserStacks} ({self.laserStackTimer:0.0}s)</b></color>", new Color(0.08f, 0.22f, 0.28f, 0.92f)));
            if (enemy != null && enemy.phase != LanePhase.Dead && !enemy.invisible)
            {
                if (enemy.curseTimer > 0f)
                    pills.Add(($"<color=#dca0ff><b>ENEMY CURSED x{enemy.curseStacks}</b></color>", new Color(0.18f, 0.10f, 0.24f, 0.88f)));
                if (enemy.grievousTimer > 0f)
                    pills.Add(($"<color=#ff9e8a><b>ENEMY ANTI-HEAL</b></color>", new Color(0.22f, 0.10f, 0.10f, 0.88f)));
                if (enemy.laserStacks > 0)
                    pills.Add(($"<color=#4ee8ff><b>ENEMY LASER x{enemy.laserStacks}</b></color>", new Color(0.08f, 0.18f, 0.26f, 0.88f)));
            }

            float px = 12f;
            float py = 48f;
            for (int i = 0; i < pills.Count; i++)
            {
                GUIContent gc = new GUIContent(pills[i].text);
                Vector2 sz = sheetTextStyle.CalcSize(gc);
                float pw = Mathf.Clamp(sz.x + 14f, 88f, 220f);
                if (px + pw > Screen.width - 12f)
                {
                    px = 12f;
                    py += 24f;
                }
                var pr = new Rect(px, py, pw, 21f);
                DrawBox(pr, pills[i].bg);
                GUI.Label(new Rect(pr.x + 6f, pr.y + 2f, pr.width - 12f, 18f), pills[i].text, sheetTextStyle);
                px += pw + 6f;
            }
        }

        private void SetHoverTooltip(string text, Vector2 screenPos, Vector2 size)
        {
            hoverTooltipText = text;
            hoverTooltipAnchor = new Rect(screenPos.x, screenPos.y, size.x, size.y);
        }

        private void DrawHoverTooltip()
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(hoverTooltipText)) return;
            EnsureStyles();

            float tipW = 320f;
            float tipH = tooltipStyle.CalcHeight(new GUIContent(hoverTooltipText), tipW) + 4f;
            float tx = Mathf.Clamp(hoverTooltipAnchor.x, 8f, Screen.width - tipW - 8f);
            float ty = hoverTooltipAnchor.y - tipH - 8f;
            if (ty < 8f)
            {
                ty = Mathf.Min(Screen.height - tipH - 8f, hoverTooltipAnchor.y + hoverTooltipAnchor.height + 8f);
            }

            GUI.Label(new Rect(tx, ty, tipW, tipH), hoverTooltipText, tooltipStyle);
        }

        private string FormatAbilityTooltip(LaneHeroSheet.AbilityCardData card)
        {
            string s = $"<color={LaneHeroSheet.ColCc}><b>[{card.KeyBadge}]</b></color> <b>{card.Name}</b>";
            if (!string.IsNullOrEmpty(card.MetaRight)) s += $"\n<color={LaneHeroSheet.ColMuted}>{card.MetaRight}</color>";
            if (!string.IsNullOrEmpty(card.PlainDescription)) s += $"\n{card.PlainDescription}";
            if (card.KeyFacts.Count > 0)
            {
                s += "\n";
                for (int k = 0; k < card.KeyFacts.Count; k++)
                {
                    if (k > 0) s += "  •  ";
                    s += $"<color={LaneHeroSheet.ColMuted}>{card.KeyFacts[k].Label}:</color> <color={card.KeyFacts[k].ColorHex}><b>{card.KeyFacts[k].Value}</b></color>";
                }
            }
            if (card.ScalingRows.Count > 0)
            {
                s += $"\n<color={LaneHeroSheet.ColMuted}><b>What Scales:</b></color>";
                for (int i = 0; i < card.ScalingRows.Count; i++)
                {
                    LaneHeroSheet.ScalingRow row = card.ScalingRows[i];
                    s += $"\n• <color={row.StatColorHex}><b>{row.StatTag}</b></color> → {row.AffectedProperty}";
                }
            }
            if (!string.IsNullOrEmpty(card.LiveLine)) s += $"\n{card.LiveLine}";
            if (!string.IsNullOrEmpty(card.PracticalTip)) s += $"\n<color={LaneHeroSheet.ColCc}><b>Tip:</b></color> <color=#dce7cf>{card.PracticalTip}</color>";
            if (showExactMath && card.MathLines.Count > 0) s += $"\n<color={LaneHeroSheet.ColMuted}>{card.MathLine}</color>";
            return s;
        }

        private Texture2D MakeSolidTex(Color col)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, col);
            t.Apply();
            return t;
        }

        private void EnsureStyles()
        {
            if (sheetTextStyle == null)
            {
                sheetTextStyle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    wordWrap = true,
                    fontSize = 12,
                };
            }
            if (sheetBodyStyle == null)
            {
                sheetBodyStyle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    wordWrap = true,
                    fontSize = 13,
                };
                sheetBodyStyle.normal.textColor = new Color(0.91f, 0.94f, 0.97f);
            }
            if (sheetSectionStyle == null)
            {
                sheetSectionStyle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    wordWrap = false,
                    fontSize = 11,
                };
            }
            if (sheetFactPillStyle == null)
            {
                sheetFactPillStyle = new GUIStyle(GUI.skin.box)
                {
                    richText = true,
                    wordWrap = false,
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(7, 7, 3, 3),
                    margin = new RectOffset(0, 0, 1, 1),
                };
                sheetFactPillStyle.normal.background = MakeSolidTex(new Color(0.10f, 0.14f, 0.19f, 0.98f));
                sheetFactPillStyle.normal.textColor = Color.white;
            }
            if (sheetMathBoxStyle == null)
            {
                sheetMathBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(8, 8, 6, 6),
                    margin = new RectOffset(2, 2, 2, 2),
                };
                sheetMathBoxStyle.normal.background = MakeSolidTex(new Color(0.06f, 0.08f, 0.11f, 0.98f));
            }
            if (sheetTipBoxStyle == null)
            {
                sheetTipBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    richText = true,
                    wordWrap = true,
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(8, 8, 5, 5),
                    margin = new RectOffset(2, 2, 2, 2),
                };
                sheetTipBoxStyle.normal.background = MakeSolidTex(new Color(0.13f, 0.16f, 0.12f, 0.96f));
                sheetTipBoxStyle.normal.textColor = new Color(0.86f, 0.91f, 0.81f);
            }
            if (cardHeaderStyle == null)
            {
                cardHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    wordWrap = true,
                    fontSize = 14,
                };
            }
            if (cardBoxStyle == null)
            {
                cardBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(10, 10, 8, 8),
                    margin = new RectOffset(2, 2, 4, 4),
                };
                cardBoxStyle.normal.background = MakeSolidTex(new Color(0.13f, 0.16f, 0.21f, 0.96f));
            }
            if (tooltipStyle == null)
            {
                tooltipStyle = new GUIStyle(GUI.skin.box)
                {
                    richText = true,
                    wordWrap = true,
                    fontSize = 12,
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(9, 9, 7, 7),
                };
                tooltipStyle.normal.background = MakeSolidTex(new Color(0.06f, 0.08f, 0.11f, 0.98f));
                tooltipStyle.normal.textColor = Color.white;
            }
        }

        private void DrawHelpOverlay()
        {
            HeroControlProfile profile = scene.ActiveControlProfile;
            bool rtsActive = scene.IsRtsActiveForCurrentHero;
            float panelH = rtsActive ? 318f : 268f;
            var r = new Rect(16f, 54f, 520f, panelH);
            TrackPointer(r);
            DrawBox(r, new Color(0.08f, 0.10f, 0.12f, 0.94f));
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.Label($"<b>ULTIMATE FREE MOBA — CONTROLS ({profile.DisplayName})</b>");
            if (profile.SupportsBasicAttack)
            {
                GUILayout.Label($"• [{KeyLabel(ControlCommand.Move)}] ground: Move  |  [{KeyLabel(ControlCommand.AttackOrder)}] enemy: Continuous auto-attack");
                GUILayout.Label($"• [{KeyLabel(ControlCommand.AttackMove)}]: Quick-Cast Attack-Move toward cursor");
            }
            else
            {
                GUILayout.Label($"• [{KeyLabel(ControlCommand.Move)}] ground/enemy: Move ({profile.MovementSummary})");
                GUILayout.Label($"• <color=#ffcf66>Basic Attack & Attack-Move disabled by {profile.DisplayName} hero rules</color>");
            }
            GUILayout.Label($"• [{KeyLabel(ControlCommand.CastQ)} / {KeyLabel(ControlCommand.CastW)} / {KeyLabel(ControlCommand.CastE)} / {KeyLabel(ControlCommand.CastR)}]: Quick-Cast abilities  |  Ctrl+key: Level up");
            if (profile.SupportsStop)
            {
                string stopNote = profile.GetCommandNote(ControlCommand.Stop);
                GUILayout.Label($"• [{KeyLabel(ControlCommand.Stop)}]: Stop — {(string.IsNullOrEmpty(stopNote) ? "cancels movement & basic wind-up" : stopNote)}");
            }
            else
            {
                GUILayout.Label("• <color=#ffcf66>Stop command disabled for this hero control profile</color>");
            }
            GUILayout.Label($"• [{KeyLabel(ControlCommand.ToggleShop)}]: Shop  |  [{KeyLabel(ControlCommand.ToggleHeroSheet)}]: Scaling Cards  |  [{KeyLabel(ControlCommand.ToggleSettings)}]: Settings");
            GUILayout.Label($"• [{KeyLabel(ControlCommand.ToggleCameraLock)}]: Cam Lock (Arrows/edge/MMB pan)  |  [{KeyLabel(ControlCommand.CenterCamera)}]: Center hero");
            if (rtsActive)
            {
                GUILayout.Label($"<color=#78ff9e><b>RTS Multi-Unit Active:</b> Left-Drag box select • [{KeyLabel(ControlCommand.RtsSelectHero)}] Hero • [{KeyLabel(ControlCommand.RtsSelectAllUnits)}] All • [{KeyLabel(ControlCommand.RtsCycleUnit)}] Cycle • [{KeyLabel(ControlCommand.RtsGroup1)}-{KeyLabel(ControlCommand.RtsGroup4)}] Groups (Ctrl+key assigns)</color>");
            }
            GUILayout.EndArea();
        }

        private void TrackPointer(Rect guiRect)
        {
            Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (guiRect.Contains(guiMouse)) PointerOverUi = true;
        }
    }
}
