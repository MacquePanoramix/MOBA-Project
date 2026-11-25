using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public sealed partial class LaneHud
    {
        private int settingsTab;
        private Vector2 settingsScroll;
        private ControlCommand? capturingCommand;
        private int captureStartFrame;
        private ControlCommand? pendingConflictCmd;
        private KeyCode pendingConflictKey;
        private List<BindingConflict> pendingConflicts;
        private string settingsNotice = string.Empty;

        private void DrawRtsSelectionBox()
        {
            if (!scene.IsRtsDragBoxActive) return;
            Rect box = scene.RtsDragBoxGuiRect;
            if (box.width < 2f || box.height < 2f) return;

            DrawBox(box, new Color(0.24f, 0.92f, 0.45f, 0.16f));
            Color border = new Color(0.32f, 1f, 0.52f, 0.90f);
            DrawBox(new Rect(box.xMin, box.yMin, box.width, 2f), border);
            DrawBox(new Rect(box.xMin, box.yMax - 2f, box.width, 2f), border);
            DrawBox(new Rect(box.xMin, box.yMin, 2f, box.height), border);
            DrawBox(new Rect(box.xMax - 2f, box.yMin, 2f, box.height), border);
        }

        private void DrawRtsSelectionBar()
        {
            if (!scene.IsRtsActiveForCurrentHero || snap == null) return;
            EnsureStyles();

            float barW = Mathf.Min(680f, Screen.width - 240f);
            float barH = 30f;
            float dockH = 128f;
            var r = new Rect(10f, Screen.height - dockH - barH - 16f, barW, barH);
            TrackPointer(r);
            DrawBox(r, new Color(0.08f, 0.14f, 0.11f, 0.92f));

            LaneRtsSelection rts = scene.RtsSelection;
            var auth = new List<ControllableUnitInfo>();
            rts.GetAuthorizedUnits(snap, localTeam, auth);
            string selectedSummary = BuildSelectedUnitSummary(rts.SelectedUnitIds, auth);

            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 3f, r.width - 16f, r.height - 6f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#78ff9e><b>RTS ({rts.SelectedUnitIds.Count}):</b></color> {selectedSummary}", sheetTextStyle, GUILayout.Width(250f));

            if (GUILayout.Button($"All ({KeyLabel(ControlCommand.RtsSelectAllUnits)})", GUILayout.Width(72f), GUILayout.Height(22f)))
            {
                rts.SelectAllControllable(snap, localTeam);
            }
            if (GUILayout.Button($"Hero ({KeyLabel(ControlCommand.RtsSelectHero)})", GUILayout.Width(74f), GUILayout.Height(22f)))
            {
                rts.SelectHero(snap, localTeam);
            }
            if (GUILayout.Button($"Cycle ({KeyLabel(ControlCommand.RtsCycleUnit)})", GUILayout.Width(78f), GUILayout.Height(22f)))
            {
                rts.CycleSelectedUnit(snap, localTeam);
            }

            GUILayout.FlexibleSpace();
            for (int g = 1; g <= LaneRtsSelection.MaxControlGroups; g++)
            {
                int cnt = rts.GetControlGroupCount(g);
                string gKey = KeyLabel(ControlCommand.RtsGroup1 + (g - 1));
                string label = cnt > 0 ? $"[{gKey}:{cnt}]" : $"[{gKey}:-]";
                if (GUILayout.Button(label, GUILayout.Width(42f), GUILayout.Height(22f)))
                {
                    rts.RecallControlGroup(g, snap, localTeam, Time.unscaledTime, out _, out _);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static string BuildSelectedUnitSummary(IReadOnlyList<int> selectedIds, List<ControllableUnitInfo> auth)
        {
            if (selectedIds == null || selectedIds.Count == 0) return "None";
            var names = new List<string>(selectedIds.Count);
            for (int i = 0; i < selectedIds.Count; i++)
            {
                for (int j = 0; j < auth.Count; j++)
                {
                    if (auth[j].Id == selectedIds[i])
                    {
                        names.Add(auth[j].Label);
                        break;
                    }
                }
            }
            return names.Count > 0 ? string.Join(", ", names) : "None";
        }

        private void HandleKeybindCaptureEvent()
        {
            if (!capturingCommand.HasValue || Time.frameCount <= captureStartFrame) return;

            Event ev = Event.current;
            if (ev == null) return;

            KeyCode captured = KeyCode.None;
            if (ev.type == EventType.KeyDown)
            {
                if (ev.keyCode == KeyCode.Escape)
                {
                    capturingCommand = null;
                    settingsNotice = "Keybind change cancelled.";
                    ev.Use();
                    return;
                }
                if (ev.keyCode != KeyCode.None)
                {
                    captured = ev.keyCode;
                }
            }
            else if (ev.type == EventType.MouseDown)
            {
                switch (ev.button)
                {
                    case 0: captured = KeyCode.Mouse0; break;
                    case 1: captured = KeyCode.Mouse1; break;
                    case 2: captured = KeyCode.Mouse2; break;
                    case 3: captured = KeyCode.Mouse3; break;
                    case 4: captured = KeyCode.Mouse4; break;
                }
            }

            if (captured == KeyCode.None) return;

            ev.Use();
            ControlCommand targetCmd = capturingCommand.Value;
            capturingCommand = null;
            PlayerSettingsStore store = scene.Settings;
            if (store == null) return;

            if (store.TrySetBinding(targetCmd, captured, ConflictResolutionMode.RejectIfConflict, out List<BindingConflict> conflicts))
            {
                pendingConflictCmd = null;
                pendingConflicts = null;
                settingsNotice = store.LastStatus;
            }
            else if (conflicts != null && conflicts.Count > 0)
            {
                pendingConflictCmd = targetCmd;
                pendingConflictKey = captured;
                pendingConflicts = conflicts;
                settingsNotice = string.Empty;
            }
            else
            {
                pendingConflictCmd = null;
                pendingConflicts = null;
                settingsNotice = store.LastStatus;
            }
        }

        private void DrawSettingsDrawer(HeroSnap liveSelf, bool inMatch)
        {
            PlayerSettingsStore store = scene.Settings;
            if (store == null) return;
            EnsureStyles();

            float w = Mathf.Min(780f, Screen.width - 28f);
            float h = Mathf.Min(580f, Screen.height - 36f);
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            TrackPointer(r);
            DrawBox(r, new Color(0.09f, 0.11f, 0.14f, 0.99f));

            GUILayout.BeginArea(new Rect(r.x + 14f, r.y + 12f, r.width - 28f, r.height - 24f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><size=15>ULTIMATE FREE MOBA — PERSONAL SETTINGS & CONTROLS</size></b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Restore Defaults", GUILayout.Width(122f), GUILayout.Height(24f)))
            {
                store.RestoreDefaults();
                capturingCommand = null;
                pendingConflictCmd = null;
                settingsNotice = store.LastStatus;
            }
            if (GUILayout.Button("X", GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                SettingsOpen = false;
                capturingCommand = null;
                pendingConflictCmd = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>Saved locally outside install directory (survives auto-updates): {store.SettingsPath}</color>", sheetTextStyle);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            DrawSettingsTabButton("Gameplay & RTS", 0);
            DrawSettingsTabButton("MOBA & Global Keys", 1);
            DrawSettingsTabButton("RTS Keys", 2);
            DrawSettingsTabButton("Future Hero Contexts", 3);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            if (capturingCommand.HasValue)
            {
                ControlCommandMeta capMeta = ControlCommandCatalog.GetMeta(capturingCommand.Value);
                GUILayout.BeginHorizontal(cardBoxStyle);
                GUILayout.Label($"<color=#ffcf44><b>Listening for \"{capMeta.DisplayName}\" ({ControlCommandCatalog.FormatContextLabel(capMeta.Context)})... Press any key or mouse button (Esc to cancel).</b></color>", sheetTextStyle);
                GUILayout.EndHorizontal();
            }
            else if (pendingConflictCmd.HasValue && pendingConflicts != null && pendingConflicts.Count > 0)
            {
                DrawConflictResolutionBanner(store);
            }
            else if (!string.IsNullOrEmpty(settingsNotice))
            {
                GUILayout.Label($"<color={LaneHeroSheet.ColLive}>{settingsNotice}</color>", sheetTextStyle);
            }

            settingsScroll = GUILayout.BeginScrollView(settingsScroll);
            if (settingsTab == 0)
            {
                DrawSettingsGameplayTab(store, liveSelf);
            }
            else if (settingsTab == 1)
            {
                DrawSettingsBindingsList(store, ControlContext.ConventionalMoba, scene.ActiveControlProfile);
                GUILayout.Space(8);
                DrawSettingsBindingsList(store, ControlContext.Global, scene.ActiveControlProfile);
            }
            else if (settingsTab == 2)
            {
                HeroControlProfile active = scene.ActiveControlProfile;
                string rtsInfo = active.RtsSupport == RtsControlMode.Required
                    ? "Required by active hero (always enabled regardless of optional toggle)."
                    : (active.IsRtsEnabled(store.Data)
                        ? "Active on conventional heroes (enabled in your preferences)."
                        : "Currently disabled on conventional heroes — enable in 'Gameplay & RTS' or bind keys below anyway.");
                GUILayout.Label($"<color={LaneHeroSheet.ColInt}>{rtsInfo}</color>", sheetTextStyle);
                GUILayout.Space(4);
                DrawSettingsBindingsList(store, ControlContext.RtsMultiUnit, active);
            }
            else
            {
                DrawFutureContextsTab(store);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawSettingsTabButton(string label, int index)
        {
            bool active = settingsTab == index;
            GUI.backgroundColor = active ? new Color(0.35f, 0.78f, 0.52f) : Color.white;
            if (GUILayout.Button(label, GUILayout.Height(25f)))
            {
                settingsTab = index;
                settingsScroll = Vector2.zero;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawConflictResolutionBanner(PlayerSettingsStore store)
        {
            ControlCommand target = pendingConflictCmd.Value;
            ControlCommandMeta targetMeta = ControlCommandCatalog.GetMeta(target);
            BindingConflict first = pendingConflicts[0];
            ControlCommandMeta conflictMeta = ControlCommandCatalog.GetMeta(first.ConflictingCommand);
            string keyName = ControlCommandCatalog.FormatKeyLabel(pendingConflictKey);

            GUILayout.BeginVertical(cardBoxStyle);
            GUILayout.Label(
                $"<color=#ff8a7a><b>Key Conflict:</b></color> [{keyName}] is already bound to <b>{conflictMeta.DisplayName}</b> ({ControlCommandCatalog.FormatContextLabel(first.ConflictingContext)}), which overlaps with <b>{targetMeta.DisplayName}</b> ({ControlCommandCatalog.FormatContextLabel(targetMeta.Context)}).",
                sheetTextStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Swap Keys", GUILayout.Height(24f), GUILayout.Width(110f)))
            {
                store.TrySetBinding(target, pendingConflictKey, ConflictResolutionMode.SwapWithConflicting, out _);
                settingsNotice = store.LastStatus;
                pendingConflictCmd = null;
                pendingConflicts = null;
            }
            if (GUILayout.Button("Assign & Unbind Other", GUILayout.Height(24f), GUILayout.Width(165f)))
            {
                store.TrySetBinding(target, pendingConflictKey, ConflictResolutionMode.UnbindConflicting, out _);
                settingsNotice = store.LastStatus;
                pendingConflictCmd = null;
                pendingConflicts = null;
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(24f), GUILayout.Width(80f)))
            {
                pendingConflictCmd = null;
                pendingConflicts = null;
                settingsNotice = "Kept existing keybindings.";
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawSettingsGameplayTab(PlayerSettingsStore store, HeroSnap liveSelf)
        {
            HeroControlProfile profile = liveSelf != null
                ? HeroControlCatalog.ForHero(liveSelf.heroId)
                : scene.ActiveControlProfile;

            GUILayout.BeginVertical(cardBoxStyle);
            GUILayout.Label($"<b>OPTIONAL RTS CONTROLS (PERSONAL PREFERENCE)</b>", cardHeaderStyle);
            GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>Disabled by default for conventional heroes. Enables Left-Drag box selection, multi-unit orders, and Ctrl+1..4 control groups on units you own (e.g. Miravel's Shimmer Decoy). Heroes requiring RTS automatically receive them regardless of this toggle.</color>", sheetTextStyle);
            GUILayout.Space(4);

            bool prevOpt = store.Data.enableOptionalRtsControls;
            bool nextOpt = GUILayout.Toggle(prevOpt, " Enable Optional RTS Controls for Conventional Heroes");
            if (nextOpt != prevOpt)
            {
                store.SetOptionalRtsEnabled(nextOpt);
                settingsNotice = nextOpt ? "Optional RTS controls enabled." : "Optional RTS controls disabled.";
            }

            bool prevDbl = store.Data.rtsDoubleTapGroupCentersCamera;
            bool nextDbl = GUILayout.Toggle(prevDbl, " Double-tap Control Group key centers camera on unit(s)");
            if (nextDbl != prevDbl)
            {
                store.SetDoubleTapCenterCamera(nextDbl);
            }

            bool prevAuto = store.Data.rtsAutoSelectSummons;
            bool nextAuto = GUILayout.Toggle(prevAuto, " Auto-add newly summoned controllable units to selection");
            if (nextAuto != prevAuto)
            {
                store.SetAutoSelectSummons(nextAuto);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
            GUILayout.BeginVertical(cardBoxStyle);
            GUILayout.Label($"<b>ACTIVE HERO CONTROL PROFILE — {profile.DisplayName.ToUpperInvariant()}</b>", cardHeaderStyle);
            GUILayout.Label($"Primary Context: <b>{ControlCommandCatalog.FormatContextLabel(profile.PrimaryContext)}</b>  •  RTS Mode: <b>{profile.RtsSupport}</b>  •  Effective RTS Now: <b>{(profile.IsRtsEnabled(store.Data) ? "ON" : "OFF")}</b>", sheetTextStyle);
            GUILayout.Label($"Movement Model: {profile.MovementSummary}", sheetTextStyle);
            GUILayout.Label(
                $"Stop Command: <b>{(profile.SupportsStop ? $"Enabled [{store.GetKeyLabel(ControlCommand.Stop)}]" : "Unavailable (Hero Rule)")}</b>  •  " +
                $"Basic Attack / Attack-Move: <b>{(profile.SupportsBasicAttack ? "Enabled" : "Unavailable (Hero Rule)")}</b>",
                sheetTextStyle);
            foreach (KeyValuePair<ControlCommand, string> kv in profile.CommandNotes)
            {
                ControlCommandMeta m = ControlCommandCatalog.GetMeta(kv.Key);
                GUILayout.Label($"• <b>{m.DisplayName}:</b> {kv.Value}", sheetTextStyle);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
            GUILayout.BeginVertical(cardBoxStyle);
            GUILayout.Label("<b>EXTENSIBLE ARCHITECTURE PREVIEW</b>", cardHeaderStyle);
            GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>Settings schema v{store.Data.version} separates physical keys from hero command availability and preserves unknown future keys/preferences across updates. Visit 'Future Hero Contexts' to preview Kobold King (Required RTS), Airplane Fighter (No Stop), and Twin-Stick Shooter (WASD, S != conflict).</color>", sheetTextStyle);
            GUILayout.EndVertical();
        }

        private void DrawSettingsBindingsList(PlayerSettingsStore store, ControlContext context, HeroControlProfile profile)
        {
            GUILayout.Label($"<b>{ControlCommandCatalog.FormatContextLabel(context).ToUpperInvariant()}</b>", cardHeaderStyle);
            for (int i = 0; i < ControlCommandCatalog.All.Length; i++)
            {
                ControlCommandMeta meta = ControlCommandCatalog.All[i];
                if (meta.Context != context) continue;

                string availTag = string.Empty;
                if (profile != null && meta.Context != ControlContext.Global)
                {
                    if (profile.IsUnavailable(meta.Command))
                        availTag = " <color=#ff8a7a>[Unavailable on Hero]</color>";
                    else if (profile.IsOptional(meta.Command))
                        availTag = profile.IsCommandAvailable(meta.Command, store.Data)
                            ? " <color=#78ff9e>[Optional: ON]</color>"
                            : " <color=#b0bec8>[Optional: OFF]</color>";
                    else if (profile.IsRequired(meta.Command))
                        availTag = " <color=#9ad8ff>[Hero Active]</color>";
                }

                string keyText = store.GetKeyLabel(meta.Command);
                bool isCapturingThis = capturingCommand.HasValue && capturingCommand.Value == meta.Command;

                GUILayout.BeginHorizontal(cardBoxStyle);
                GUILayout.BeginVertical(GUILayout.Width(455f));
                GUILayout.Label($"<b>{meta.DisplayName}</b>{availTag}", sheetTextStyle);
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>{meta.Description}</color>", sheetTextStyle);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();
                GUI.backgroundColor = isCapturingThis ? new Color(1f, 0.80f, 0.30f) : Color.white;
                if (GUILayout.Button(isCapturingThis ? "Press Key..." : $"[{keyText}]", GUILayout.Width(130f), GUILayout.Height(30f)))
                {
                    capturingCommand = meta.Command;
                    captureStartFrame = Time.frameCount;
                    pendingConflictCmd = null;
                    pendingConflicts = null;
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawFutureContextsTab(PlayerSettingsStore store)
        {
            HeroControlProfile kobold = HeroControlCatalog.ForFutureArchetype("KoboldKing");
            HeroControlProfile plane = HeroControlCatalog.ForFutureArchetype("AirplaneFighter");
            HeroControlProfile twin = HeroControlCatalog.ForFutureArchetype("TwinStickShooter");

            GUILayout.BeginVertical(cardBoxStyle);
            GUILayout.Label("<b>FUTURE HERO CONTROL PROFILES (ARCHITECTURAL PREVIEW ONLY)</b>", cardHeaderStyle);
            GUILayout.Label($"• <b>{kobold.DisplayName}:</b> RTS Support = <b>{kobold.RtsSupport}</b> (Stop enabled, RTS always ON regardless of toggle).", sheetTextStyle);
            GUILayout.Label($"• <b>{plane.DisplayName}:</b> Primary = <b>{plane.PrimaryContext}</b>, Stop = <b>Disabled</b>, Continuous Flight.", sheetTextStyle);
            GUILayout.Label($"• <b>{twin.DisplayName}:</b> Primary = <b>{twin.PrimaryContext}</b>, Stop = <b>Disabled</b> (S = Backward does NOT conflict with MOBA Stop).", sheetTextStyle);
            GUILayout.EndVertical();

            GUILayout.Space(6);
            DrawSettingsBindingsList(store, ControlContext.TwinStick, twin);
            GUILayout.Space(8);
            DrawSettingsBindingsList(store, ControlContext.FlightSteering, plane);
        }
    }
}
