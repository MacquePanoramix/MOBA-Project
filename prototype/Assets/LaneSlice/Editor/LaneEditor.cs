using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Moba.LaneSlice
{
    public static class LaneEditor
    {
        private const string ScenePath = "Assets/LaneSlice/Scenes/WoodlandLane.unity";
        private const string DuelScenePath = "Assets/Duel/Scenes/WoodlandDuel.unity";
        private const string MoveScenePath = "Assets/MovementLab/Scenes/MovementLab.unity";
        private const string WindowsBuildDir = "Builds/WindowsLane";

        [MenuItem("Moba/Woodland Lane/Create Scene")]
        public static void CreateLaneScene()
        {
            string dir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.backgroundColor = new Color(0.11f, 0.14f, 0.12f);
            cam.fieldOfView = 45f;
            camGo.transform.position = new Vector3(0f, 17.5f, -13.2f);
            camGo.transform.rotation = Quaternion.Euler(53f, 0f, 0f);

            var controllerGo = new GameObject("LaneController");
            controllerGo.AddComponent<LaneScene>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            if (File.Exists(DuelScenePath)) buildScenes.Add(new EditorBuildSettingsScene(DuelScenePath, true));
            if (File.Exists(MoveScenePath)) buildScenes.Add(new EditorBuildSettingsScene(MoveScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log($"[LaneEditor] Saved {ScenePath} and registered Build Settings.");
        }

        [MenuItem("Moba/Woodland Lane/Run All Checks")]
        public static void RunAllChecks()
        {
            LaneMatchChecks.Run();
            LaneNetChecks.Run();
            Debug.Log("[LaneEditor] All WoodlandLane automated checks PASSED.");
        }

        [MenuItem("Moba/Woodland Lane/Build Windows Player")]
        public static void BuildWindows()
        {
            CreateLaneScene();
            if (!Directory.Exists(WindowsBuildDir)) Directory.CreateDirectory(WindowsBuildDir);

            string exePath = Path.Combine(WindowsBuildDir, "WoodlandLane.exe");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
            }

            string howTo =
@"ULTIMATE FREE MOBA — Woodland Lane Prototype (" + LaneUpdater.BuildVersion + @") — HOW TO PLAY

1. CONTROLS (CUSTOMIZABLE IN [F10 / SETTINGS]):
   - Right-Click ground: Move
   - Right-Click enemy hero/minion/tower: Continuous auto-attack (unavailable on Ogre, who approaches instead)
   - [A]: Quick-Cast Attack-Move toward cursor (acquires closest enemy near cursor or path; disabled on Ogre)
   - [S]: Stop movement & cancelable wind-up / Miravel R Sanctum (respects committed ability casts & Ogre/Laser R channels)
   - [Q / W / E / R]: Quick-Cast abilities toward cursor ([Ctrl + key] or click [+] to level up)
   - [F10] or Settings button: Personal Settings (Optional RTS Controls, customizable keybindings with conflict handling, saved outside update folder)
   - Optional RTS Controls (off by default on conventional heroes; always enabled for RTS-required heroes like future Kobold King):
       Left-Drag box selection, Shift-click/add, [F2] Hero, [F3] All Units, [Tab] Cycle, [Ctrl+1..4] Assign Group, [1..4] Recall (double-tap centers)
   - [B]: Toggle Fountain Shop (20 tiered Starter/Core/Capstone items + 70% sell refund inside Fountain or while Respawning)
   - [C]: Toggle Hero Numbers & Scaling cards (all base stats, scaling rates, [Show Math] toggle, and live match numbers)
   - [Y]: Toggle Locked / Free Camera (Arrow keys, screen-edge, or Middle-Mouse drag to pan; [Space] centers)
   - [F1]: Toggle controls overlay
   - Lane Economy: Last-hitting enemy minions awards Gold + CS; being alive within 12u of an enemy minion death awards full 100% XP even without last-hitting.

2. FOUR PLAYABLE HEROES:
   - Ogre: No basic attacks, CC-immune during casts. AD scales club damage, AGI (+0.8%/AGI) & Attack Speed (+70% of bonus AS) speed up wind-ups & recovery (smooth Base / Speed curve), STR boosts stun/knockback with diminishing returns (no hard cap). [R] channels DR+size & queues up to 4 aimed Q/W strikes ending in a CRIT!
   - Miravel (Hero Luis): Ranged staff, regen/anti-regen aura, takedown heal power. [Q] passive healing/damage orbs (enemies can deny), [W] invisibility + cursor-walking decoy (walks to cast cursor & stops), [E] curse ward (preserves [W] stealth; triggers when you or decoy take hero damage and escalates on enemy casts), [R] ramping sanctum channel.
   - Old Man Swordsman: Slow move, Twin-Blade 2-hit basics. [Q] slow arc (also applies [R] mark!), [W] 1st-hit %HP heal, [E] 2nd-hit stacking +Resist/-Enemy Resist, [R] dash through last basic-attacked or [Q]-hit target with execute cooldown reset.
   - Chain Laser Shooter: Ranged chain lasers (no bounce cap, stacks amplify laser dmg). [Q] extending line beam (+escalating multi-kill gold), [W] stack-based area shocks whose slow strength & duration stack, [E] step back + stationary decoy blocking heroes & projectiles (zap+stun attackers), [R] move while firing continuous chain-proc prism beam!

3. MULTIPLAYER (UNITY RELAY OR DIRECT TCP):
   - Unity Relay (Recommended): Host clicks 'Host Relay Room' -> Copy the 6-letter Room Code -> Guest clicks Paste & 'Join Code'. Zero port-forwarding or extra tools needed!
   - Direct TCP Fallback (:47777): Click 'Host TCP' (or pass '-laneHost 0.0.0.0:47777' / '-laneJoin <host>:47777').

4. ONE-CLICK UPDATES:
   - In the Lobby bottom bar, click 'Check for Updates'. If a newer release zip exists on GitHub Releases (configured in updater_config.json), click 'Update' to download and relaunch cleanly.
";
            File.WriteAllText(Path.Combine(WindowsBuildDir, "HOW_TO_PLAY.txt"), howTo);
            File.WriteAllText(Path.Combine(WindowsBuildDir, "version.txt"), LaneUpdater.BuildVersion);
            File.WriteAllText(Path.Combine(WindowsBuildDir, "updater_config.json"),
                JsonUtility.ToJson(new LaneUpdater.UpdaterConfig(), true));
            Debug.Log($"[LaneEditor] Build succeeded at {exePath} ({report.summary.totalSize / (1024f * 1024f):0.0} MB).");
        }
    }
}
