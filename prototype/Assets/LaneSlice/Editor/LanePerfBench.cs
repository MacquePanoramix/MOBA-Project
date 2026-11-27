using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Reproducible Unity Editor performance benchmark for the interactive Hero Guide &
    /// shared preview pipeline. Measures average / p95 / max CPU frame times, GC pressure,
    /// and active Mesh / Material counts across closed, paused, animated, switching,
    /// slider-dragging, and extended preview sessions.
    /// </summary>
    public static class LanePerfBench
    {
        private struct BenchSample
        {
            public string Name;
            public int Frames;
            public double TotalMs;
            public double AvgMs;
            public double P95Ms;
            public double MaxMs;
            public long GcKbDelta;
            public int GcGen0Count;
            public int MeshDelta;
            public int MaterialDelta;
            public string Notes;
        }

        public static void RunPreviewPerfBench()
        {
            RunToPath(Path.Combine("Artifacts", "perf_bench_latest.txt"));
        }

        public static void RunToPath(string outPath)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            var tuning = new LaneTuning();
            var samples = new List<BenchSample>();

            // Warmup JIT
            Warmup(tuning);

            samples.Add(MeasureGuideClosed(tuning, frames: 60));
            samples.Add(MeasureGuideOpenDemosOff(tuning, frames: 60));
            samples.Add(MeasureGuideOpenDemosOn(tuning, frames: 90));
            samples.Add(MeasureAbilitySwitching(tuning, switches: 12));
            samples.Add(MeasureSliderDragging(tuning, drags: 30));
            samples.Add(MeasureExtendedSession(tuning, frames: 300));

            var sb = new StringBuilder();
            sb.AppendLine($"=== ULTIMATE FREE MOBA PREVIEW PERF BENCH ({LaneUpdater.BuildVersion}, EditorBatchmode={Application.isBatchMode}) ===");
            sb.AppendLine($"TimestampUTC={DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", inv)} | Note: CPU simulation + guide-build + preview cost (excludes GPU present in batchmode)");
            sb.AppendLine();
            for (int i = 0; i < samples.Count; i++)
            {
                BenchSample s = samples[i];
                double effFps = s.AvgMs > 0.001 ? 1000.0 / s.AvgMs : 99999.0;
                sb.AppendLine(
                    $"{s.Name,-30} | N={s.Frames,3} | Avg={s.AvgMs.ToString("0.000", inv),8}ms | P95={s.P95Ms.ToString("0.000", inv),8}ms | Max={s.MaxMs.ToString("0.000", inv),8}ms | " +
                    $"EstCpuFPS={effFps.ToString("0.0", inv),7} | GC+={s.GcKbDelta,6}KB (Gen0={s.GcGen0Count}) | Mesh+={s.MeshDelta,4} Mat+={s.MaterialDelta,4} | {s.Notes}");
            }

            string dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[LanePerfBench] Wrote report to {outPath}\n{sb}");
        }

        private static void Warmup(LaneTuning tuning)
        {
            using (var p = new LaneAbilityPreview(tuning, enableRenderVisuals: false))
            {
                p.Select(LaneHeroId.Ogre, LaneAbilitySlot.Q, 0);
                p.Tick(1f / 30f, shouldRun: true);
                HeroSnap snap = LaneHeroSheet.EvaluatePreviewHeroSnap(LaneHeroId.Ogre, tuning, p.Overrides);
                LaneHeroSheet.BuildHeroGuide(LaneHeroId.Ogre, tuning, snap);
                LaneAbilityPreview.GetRelevantSliders(LaneHeroId.Ogre, LaneAbilitySlot.Q);
            }
        }

        private static BenchSample RunTimedLoop(string name, int count, string notes, Action<int> step)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            int gen0Before = GC.CollectionCount(0);
            long memBefore = GC.GetTotalMemory(false);
            int meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            int matsBefore = Resources.FindObjectsOfTypeAll<Material>().Length;

            var times = new double[count];
            var swTotal = Stopwatch.StartNew();
            var swStep = new Stopwatch();
            for (int i = 0; i < count; i++)
            {
                swStep.Restart();
                step(i);
                swStep.Stop();
                times[i] = swStep.Elapsed.TotalMilliseconds;
            }
            swTotal.Stop();

            long memAfter = GC.GetTotalMemory(false);
            int gen0After = GC.CollectionCount(0);
            int meshesAfter = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            int matsAfter = Resources.FindObjectsOfTypeAll<Material>().Length;

            Array.Sort(times);
            double sum = 0;
            for (int i = 0; i < times.Length; i++) sum += times[i];
            int p95Idx = Math.Clamp((int)Math.Floor(count * 0.95) - 1, 0, Math.Max(0, count - 1));

            return new BenchSample
            {
                Name = name,
                Frames = count,
                TotalMs = swTotal.Elapsed.TotalMilliseconds,
                AvgMs = count > 0 ? sum / count : 0,
                P95Ms = count > 0 ? times[p95Idx] : 0,
                MaxMs = count > 0 ? times[count - 1] : 0,
                GcKbDelta = Math.Max(0L, (memAfter - memBefore) / 1024L),
                GcGen0Count = Math.Max(0, gen0After - gen0Before),
                MeshDelta = meshesAfter - meshesBefore,
                MaterialDelta = matsAfter - matsBefore,
                Notes = notes,
            };
        }

        private static void SimulateOnGuiHeroGuidePass(
            LaneHeroSheet.HeroGuideCache guideCache,
            LaneHeroId hero,
            LaneAbilitySlot slot,
            LaneTuning tuning,
            PreviewStatOverrides ov,
            int guiPasses)
        {
            for (int pass = 0; pass < guiPasses; pass++)
            {
                guideCache.GetOrBuild(hero, tuning, null, usingSandbox: true, ov, out _);
                LaneAbilityPreview.GetRelevantSliders(hero, slot);
            }
        }

        private static BenchSample MeasureGuideClosed(LaneTuning tuning, int frames)
        {
            var m = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, tuning);
            using (var preview = new LaneAbilityPreview(tuning, enableRenderVisuals: true))
            {
                return RunTimedLoop("1_GuideClosed_MatchTick", frames, "Live match tick with preview inactive", _ =>
                {
                    m.Tick(1f / 60f);
                    preview.Tick(1f / 60f, shouldRun: false);
                });
            }
        }

        private static BenchSample MeasureGuideOpenDemosOff(LaneTuning tuning, int frames)
        {
            var guideCache = new LaneHeroSheet.HeroGuideCache();
            using (var preview = new LaneAbilityPreview(tuning, enableRenderVisuals: true))
            {
                PreviewStatOverrides ov = LaneAbilityPreview.DefaultOverrides(LaneAbilitySlot.Q);
                return RunTimedLoop("2_GuideOpen_DemosOFF", frames, "2x OnGUI pass/frame with Demos OFF", _ =>
                {
                    preview.Tick(1f / 60f, shouldRun: false);
                    SimulateOnGuiHeroGuidePass(guideCache, LaneHeroId.Ogre, LaneAbilitySlot.Q, tuning, ov, guiPasses: 2);
                });
            }
        }

        private static BenchSample MeasureGuideOpenDemosOn(LaneTuning tuning, int frames)
        {
            var guideCache = new LaneHeroSheet.HeroGuideCache();
            using (var preview = new LaneAbilityPreview(tuning, enableRenderVisuals: true))
            {
                preview.Select(LaneHeroId.LaserShooter, LaneAbilitySlot.R, 0);
                return RunTimedLoop("3_GuideOpen_DemosON", frames, "Preview Tick + 2x OnGUI pass/frame", _ =>
                {
                    preview.Tick(1f / 60f, shouldRun: true);
                    SimulateOnGuiHeroGuidePass(guideCache, preview.SelectedHero, preview.SelectedSlot, tuning, preview.Overrides, guiPasses: 2);
                });
            }
        }

        private static BenchSample MeasureAbilitySwitching(LaneTuning tuning, int switches)
        {
            var pairs = new[]
            {
                (LaneHeroId.Ogre, LaneAbilitySlot.Q),
                (LaneHeroId.Ogre, LaneAbilitySlot.W),
                (LaneHeroId.Ogre, LaneAbilitySlot.R),
                (LaneHeroId.Miravel, LaneAbilitySlot.None),
                (LaneHeroId.Miravel, LaneAbilitySlot.W),
                (LaneHeroId.Miravel, LaneAbilitySlot.R),
                (LaneHeroId.Swordsman, LaneAbilitySlot.None),
                (LaneHeroId.Swordsman, LaneAbilitySlot.Q),
                (LaneHeroId.Swordsman, LaneAbilitySlot.R),
                (LaneHeroId.LaserShooter, LaneAbilitySlot.Q),
                (LaneHeroId.LaserShooter, LaneAbilitySlot.E),
                (LaneHeroId.LaserShooter, LaneAbilitySlot.R),
            };
            var guideCache = new LaneHeroSheet.HeroGuideCache();
            using (var preview = new LaneAbilityPreview(tuning, enableRenderVisuals: true))
            {
                preview.Tick(1f / 60f, shouldRun: true);
                return RunTimedLoop("4_AbilityHeroSwitching", switches, "12 hero/slot switches + 2x OnGUI pass", i =>
                {
                    var p = pairs[i % pairs.Length];
                    preview.Select(p.Item1, p.Item2, 0);
                    preview.Tick(1f / 60f, shouldRun: true);
                    SimulateOnGuiHeroGuidePass(guideCache, preview.SelectedHero, preview.SelectedSlot, tuning, preview.Overrides, guiPasses: 2);
                });
            }
        }

        private static BenchSample MeasureSliderDragging(LaneTuning tuning, int drags)
        {
            var guideCache = new LaneHeroSheet.HeroGuideCache();
            using (var preview = new LaneAbilityPreview(tuning, enableRenderVisuals: true))
            {
                preview.Select(LaneHeroId.LaserShooter, LaneAbilitySlot.R, 0);
                preview.Tick(1f / 60f, shouldRun: true);
                return RunTimedLoop("5_SliderDragging", drags, "30 slider updates + 2x OnGUI pass", i =>
                {
                    PreviewStatOverrides ov = preview.Overrides;
                    ov.bonusIntelligence = (i + 1) * 5f;
                    ov.bonusAgility = (i + 1) * 4f;
                    preview.ApplyLiveOverrides(ov);
                    preview.Tick(1f / 60f, shouldRun: true);
                    SimulateOnGuiHeroGuidePass(guideCache, preview.SelectedHero, preview.SelectedSlot, tuning, preview.Overrides, guiPasses: 2);
                });
            }
        }

        private static BenchSample MeasureExtendedSession(LaneTuning tuning, int frames)
        {
            var guideCache = new LaneHeroSheet.HeroGuideCache();
            using (var preview = new LaneAbilityPreview(tuning, enableRenderVisuals: true))
            {
                preview.Select(LaneHeroId.Ogre, LaneAbilitySlot.R, 0);
                preview.Tick(1f / 60f, shouldRun: true);
                return RunTimedLoop("6_ExtendedSession_300f", frames, "Extended loop w/ multiple restarts", i =>
                {
                    if (i % 75 == 74) preview.Select(LaneHeroId.LaserShooter, LaneAbilitySlot.Q, 0);
                    else if (i % 75 == 37) preview.Select(LaneHeroId.Miravel, LaneAbilitySlot.R, 0);
                    preview.Tick(1f / 30f, shouldRun: true);
                    SimulateOnGuiHeroGuidePass(guideCache, preview.SelectedHero, preview.SelectedSlot, tuning, preview.Overrides, guiPasses: 2);
                });
            }
        }
    }
}
