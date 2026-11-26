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
            LaneResourceAndItemChecks.Run();
            LaneNetChecks.Run();
            Debug.Log("[LaneEditor] All WoodlandLane automated checks PASSED.");
        }

        [MenuItem("Moba/Woodland Lane/Run Balance Experiments")]
        public static void RunBalanceExperiments()
        {
            LaneBalanceHarness.RunAll();
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

2. FOUR PLAYABLE HEROES (v0.2.8 — The Scaling Patch):
   - Global Attack Speed: EffectiveAS = sqrt(1 + k × BonusAS) (unbounded diminishing returns, no hard cap; governs basic attacks, Swordsman 1st/2nd strikes, Ogre recovery speed, and Laser R proc frequency).
   - Ogre: No basic attacks, CC-immune during casts. AD=damage, AGI=Wind-up speed (sqrt), Attack Speed=Recovery speed (sqrt), STR=Outer Q width / W sqrt knockback / E radius / Q&E stuns / R size multiplier (ability areas scale proportionally with R size!). Q features mutually exclusive Central slam (full dmg+stun) & Outer shockwave (reduced dmg, 0 CC).
   - Miravel (Hero Luis): Heal Power (starts 100%, grows permanently on takedowns) scales Passive ally regen & enemy regen drain (can push enemy regen NEGATIVE for real HP loss!), Q orb heal/dmg, and R max heal/dmg amps. INT scales Aura/Orb/R radii and E curse DPS. [W] stealth + cursor decoy, [E] stealth-safe curse ward, [R] ramping sanctum.
   - Old Man Swordsman: Asymmetric Twin-Blade: 1st Light strike (faster, lower AD) procs [W] Stance Temper (self+enemy resist stacks, INT scales durations separately); 2nd Heavy strike (slower heavy greatsword, higher AD; AGI speeds transition) procs [E] Vital Draw (STR % Max HP magic + Heal Power linear heal). [Q] AGI wind-up slow arc (fixed recovery), [R] AD dmg, STR execute, AGI dash speed, INT target memory.
   - Chain Laser Shooter: Passive endless bounces (INT bounce radius & stack amp). [Q] AD dmg + INT sqrt beam extension speed, [W] INT shock dmg/radius + bounded/sqrt overlapping & refreshed slow, [E] animated calm single step-back deploying decoy upon completion (AGI speeds step, rank-only stun), [R] INT beam duration + AS proc rate + AGI move speed retention (<100%).

3. ITEM HIGHLIGHTS:
   - The Coward's Shield now requires sustained genuine movement away from a recent hero attacker (no stationary facing exploit; short grace period on brief stops/stuns; faster decay pursuing).
   - Kindred Charm's allied regeneration aura now works on any hero purchasing it.
   - Colossus Pauldron shield triggers when its owner inflicts Stun/Knockback (or drops <45% HP).

3. MULTIPLAYER (UNITY RELAY OR DIRECT TCP):
   - Unity Relay (Recommended): Host clicks 'Host Relay Room' -> Copy the 6-letter Room Code -> Guest clicks Paste & 'Join Code'. Zero port-forwarding or extra tools needed!
   - Direct TCP Fallback (:47777): Click 'Host TCP' (or pass '-laneHost 0.0.0.0:47777' / '-laneJoin <host>:47777').

4. ONE-CLICK UPDATES & DIRECT DOWNLOAD FALLBACK:
   - In the Lobby bottom bar, click 'Check for Updates'. Starting in v0.2.9, update checks read a free public static manifest (no GitHub REST API rate limits) and verify exact byte size + SHA-256 + ZIP integrity before installing.
   - If your network blocks or rate-limits GitHub, click 'Open Download Page' (or 'Releases') to download the latest zip directly in your browser without using the GitHub API:
     https://github.com/MacquePanoramix/MOBA-Project/releases/latest/download/WoodlandLane-Windows.zip
   - Your personal settings & keybindings live outside the install folder and always survive updates.
";
            File.WriteAllText(Path.Combine(WindowsBuildDir, "HOW_TO_PLAY.txt"), howTo);
            File.WriteAllText(Path.Combine(WindowsBuildDir, "version.txt"), LaneUpdater.BuildVersion);
            File.WriteAllText(Path.Combine(WindowsBuildDir, "updater_config.json"),
                JsonUtility.ToJson(new LaneUpdater.UpdaterConfig(), true));
            Debug.Log($"[LaneEditor] Build succeeded at {exePath} ({report.summary.totalSize / (1024f * 1024f):0.0} MB).");
        }
    }
}
