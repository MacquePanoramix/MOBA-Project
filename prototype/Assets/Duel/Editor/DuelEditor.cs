using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Moba.Duel
{
    public static class DuelEditor
    {
        public const string ScenePath = "Assets/Duel/Scenes/WoodlandDuel.unity";

        [MenuItem("Duel/Run Match Checks")]
        public static void RunMatchChecks()
        {
            DuelMatchChecks.Run();
        }

        [MenuItem("Duel/Run All Checks")]
        public static void RunAllChecks()
        {
            DuelMatchChecks.Run();
            DuelLinkChecks.Run();
            Debug.Log("All Duel checks passed.");
        }

        [MenuItem("Duel/Build Windows Player")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath))
                throw new InvalidOperationException($"The Woodland Duel scene was not found at {ScenePath}.");

            const string output = "Builds/WindowsDuel/WoodlandDuel.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, output,
                BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Woodland Duel Windows build {report.summary.result} with {report.summary.totalErrors} errors.");

            WriteHowToPlay(Path.Combine(Path.GetDirectoryName(output), "HOW_TO_PLAY.txt"));
            Debug.Log($"Woodland Duel Windows build succeeded: {report.summary.outputPath}");
        }

        private static void WriteHowToPlay(string path)
        {
            const string contents =
@"Woodland Duel (1v1 Prototype) — Quick Start

1. Extract the entire zip folder somewhere on your PC (do not run directly inside the zip).
2. Double-click WoodlandDuel.exe.
   - If Windows SmartScreen warns about an unrecognized app, click ""More info"" -> ""Run anyway"".
   - If Windows Defender Firewall asks for network access, allow it.
3. To join your friend's match:
   - Click ""Join a friend"".
   - Copy the host address your friend sends you (e.g. something.ply.gg:12345).
   - Click ""Paste"", then click ""Join"".

Controls:
- Right-click ground: Move (paths around trees)
- Ogre:
  - Q (or HUD button + Left-click): Slam (long wind-up, rectangle stun; CC-immune while winding up)
  - W (or HUD button + Left-click): Sweep (arc knockback; CC-immune while winding up)
- Miravel (Orb Keeper):
  - Right-click Ogre: Basic attack (ground click cancels wind-up)
  - Walk onto the glowing orb in the center clearing to heal yourself and damage nearby enemies.
- Esc: Cancel ability targeting or open pause / rematch menu.
";
            File.WriteAllText(path, contents);
        }
    }
}
