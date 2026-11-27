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
            LanePerfAndUxChecks.Run();
            LaneNetChecks.Run();
            Debug.Log("[LaneEditor] All WoodlandLane automated checks PASSED.");
        }

        [MenuItem("Moba/Woodland Lane/Run Balance Experiments")]
        public static void RunBalanceExperiments()
        {
            LaneBalanceHarness.RunAll();
        }

        [MenuItem("Moba/Woodland Lane/Run Preview Perf Bench")]
        public static void RunPreviewPerfBench()
        {
            LanePerfBench.RunPreviewPerfBench();
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
   - [Q / W / E / R]: Quick-Cast abilities toward cursor ([Ctrl + key] or click [+] to level up; Miravel & Chain Laser spend Mana)
   - [F10] or Settings button: Personal Settings (Optional RTS Controls, Ability Video Previews toggle, customizable keybindings stored outside the install directory so updates never wipe them)
   - Optional RTS Controls (off by default on conventional heroes; always enabled for RTS-required heroes like future Kobold King):
       Left-Drag box selection, Shift-click/add, [F2] Hero, [F3] All Units, [Tab] Cycle, [Ctrl+1..4] Assign Group, [1..4] Recall (double-tap centers)
   - [B]: Toggle Fountain Shop (44 recipe-based items across 21 Approved Components, 6 Approved Custom Advanced Items & 17 Provisional Starters/Components/Mana Capstones; 70% sell refund inside Fountain or while Respawning; expandable [Show Math] details)
   - [C]: Toggle Interactive Hero Guide (accessible summary, scaling rows, explicit [Show Math] formulas, and isolated looping Ability Demo video previews with live stat sliders)
   - [Y]: Toggle Locked / Free Camera (Arrow keys, screen-edge, or Middle-Mouse drag to pan; [Space] centers)
   - [F1]: Toggle controls overlay
   - Lane Economy: Last-hitting enemy minions awards Gold + CS; being alive within 12u of an enemy minion death awards full 100% XP even without last-hitting.

2. FOUR PLAYABLE HEROES, RESOURCE SYSTEM & GLOBAL ATTACK SPEED:
   - Global Attack Speed: EffectiveAS = sqrt(1 + k × BonusAS) (unbounded diminishing returns, no hard cap; governs basic attacks, Swordsman 1st/2nd strikes, Ogre recovery speed, and Laser R proc frequency).
   - Resource System: Miravel & Chain Laser use Mana (continuous regen inside & outside combat, bounded Resource Cost Reduction <65%); Ogre & Old Man Swordsman are Resourceless.
   - Ogre (Resourceless): No basic attacks, CC-immune during casts. AD=damage, AGI=Wind-up speed (sqrt), Attack Speed=Recovery speed (sqrt), STR=Outer Q width / W sqrt knockback / E radius / Q&E stuns / R size multiplier (ability areas scale proportionally with R size!).
   - Miravel (Hero Luis — Mana): Heal Power (starts 100%, grows permanently on takedowns) scales Passive ally regen & enemy regen drain (can push enemy regen NEGATIVE for real HP loss!), Mana-free Q orb heal/dmg, and R amps. INT scales radii & E curse DPS. [W] stealth + cursor decoy, [E] stealth-safe curse ward, [R] continuous Mana-drain ramping sanctum.
   - Old Man Swordsman (Resourceless): Asymmetric Twin-Blade: 1st Light strike (faster, lower AD) procs [W] Stance Temper; 2nd Heavy strike (higher AD; AGI speeds transition) procs [E] Vital Draw (STR % Max HP magic + Heal Power heal). [Q] AGI wind-up slow arc, [R] AD dmg, STR execute, AGI dash speed, INT target memory.
   - Chain Laser Shooter (Mana): Mana-free endless bouncing basic lasers (INT radius & stack amp). [Q] AD dmg + INT sqrt extension, [W] INT shock dmg/radius + overlapping slow, [E] animated calm single step-back deploying stun decoy, [R] upfront Mana cost, INT beam duration + AS proc rate + AGI move speed retention.

3. ITEM & COMBAT FEEDBACK HIGHLIGHTS:
   - 6 Approved Custom Advanced Items (Zero Mana stats): Executioner's Axe (Final Judgment), The Coward's Shield (Flight Reflex sustained retreat Arm/MR), Chalice of Life (Fountain of Life + Lingering Restoration), Draconic Blade (Shared Heat Dragonfire + Molten Edge + Adaptive Scales + Primordial Vitality), The Giant's Heart (Ancient Vitality amber deferred-damage HP bar + Giant's Might + Immovable Mountain), Banana Blade (Crescent Strike + Slippery Trickster + First Strike + Weightlessness).
   - Provisional Mana Line (17 temporary starters/components/capstones): Mana Dew Vial, Sapphire Shard, Dewdrop Charm, Runed Sigil, Wellspring Flask, Astral Reservoir, Tidecaller Chalice, Economist's Tome.
   - Status HUD Indicators: HEAT stacks, SCALES stacks, DEFERRED damage payout bar, FIRST STRIKE readiness, and SLIPPERY trickster speed/cooldown.

4. MULTIPLAYER (UNITY RELAY OR DIRECT TCP):
   - Unity Relay (Recommended): Host clicks 'Host Relay Room' -> Copy the 6-letter Room Code -> Guest clicks Paste & 'Join Code'. Zero port-forwarding or extra tools needed!
   - Direct TCP Fallback (:47777): Click 'Host TCP' (or pass '-laneHost 0.0.0.0:47777' / '-laneJoin <host>:47777').

5. ONE-CLICK UPDATES & DIRECT DOWNLOAD FALLBACK:
   - In the Lobby bottom bar, click 'Check for Updates'. Static-manifest checks avoid GitHub REST API rate limits and verify exact byte size + SHA-256 + ZIP integrity before installing.
   - If your network blocks or rate-limits GitHub, click 'Open Download Page' (or 'Releases') to download the latest zip directly in your browser without using the GitHub API:
     https://github.com/MacquePanoramix/MOBA-Project/releases/latest/download/WoodlandLane-Windows.zip
";
            File.WriteAllText(Path.Combine(WindowsBuildDir, "HOW_TO_PLAY.txt"), howTo);
            File.WriteAllText(Path.Combine(WindowsBuildDir, "version.txt"), LaneUpdater.BuildVersion);
            File.WriteAllText(Path.Combine(WindowsBuildDir, "updater_config.json"),
                JsonUtility.ToJson(new LaneUpdater.UpdaterConfig(), true));
            Debug.Log($"[LaneEditor] Build succeeded at {exePath} ({report.summary.totalSize / (1024f * 1024f):0.0} MB).");
        }
    }
}
