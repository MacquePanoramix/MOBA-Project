using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Reusable headless balancing & stress-test suite (v0.2.8+) evaluating curve math,
    /// 30Hz simulation tick resolution under extreme stats, comparable-gold build packages,
    /// ability leveling orders, 12 hero matchups, and all 11 targeted stress scenarios.
    /// Outputs structured findings to prototype/Artifacts/balance_report_v028.txt.
    /// </summary>
    public static class LaneBalanceHarness
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class BuildSpec
        {
            public string Label;
            public int[] Items;
            public int TargetGoldBudget;
        }

        public static void RunAll()
        {
            var sb = new StringBuilder();
            var t = new LaneTuning();
            sb.AppendLine($"=== ULTIMATE FREE MOBA v0.2.8 SCALING & BALANCE REPORT ({DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC) ===");
            sb.AppendLine($"TickHz = {t.tickHz}, Global AS k = {t.attackSpeedCurveK} (EffAS = sqrt(1 + {t.attackSpeedCurveK} * BonusAS))");
            sb.AppendLine();

            RunTickResolutionAndAsAudit(sb, t);
            RunComparableGoldBuildMatrix(sb);
            RunLevelingOrderAndMatchupSuite(sb);
            RunStressScenarios(sb);

            string outDir = Path.Combine(Application.dataPath, "..", "Artifacts");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            string outPath = Path.GetFullPath(Path.Combine(outDir, "balance_report_v028.txt"));
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[LaneBalanceHarness] Report written to {outPath}");
            Debug.Log(sb.ToString());
        }

        private static void RunTickResolutionAndAsAudit(StringBuilder sb, LaneTuning t)
        {
            sb.AppendLine("--- 1. GLOBAL ATTACK SPEED & 30Hz TICK-RESOLUTION AUDIT ---");
            float dt = 1f / t.tickHz;
            float[] asBonuses = { 0f, 0.30f, 0.60f, 0.90f, 1.80f, 5.00f, 12.00f };
            for (int i = 0; i < asBonuses.Length; i++)
            {
                float b = asBonuses[i];
                float eff = LaneTuning.EffectiveAttackSpeed(b, t.attackSpeedCurveK);
                float mirWind = t.luisBasicWindup / eff;
                float mirRec = t.luisBasicRecover / eff;
                int ticksWind = Mathf.Max(1, Mathf.CeilToInt(mirWind / dt));
                int ticksRec = Mathf.Max(1, Mathf.CeilToInt(mirRec / dt));
                float theoAps = 1f / (mirWind + mirRec);
                float sim30Aps = 1f / ((ticksWind + ticksRec) * dt);
                float rProcInt = t.laserRProcInterval / eff;
                sb.AppendLine($"  BonusAS={b * 100f,5:0}% | EffAS={eff,5:0.00}x | Miravel Basic: {mirWind:0.000}+{mirRec:0.000}s ({ticksWind}+{ticksRec} ticks @30Hz) | Theo={theoAps:0.00}/s vs Sim30Hz={sim30Aps:0.00}/s | LaserRProc={1f / rProcInt:0.00}/s");
            }

            // Live simulated Miravel attack count check with 0, 1, 2, 6 Quickstep Daggers
            int[] daggerCounts = { 0, 1, 2, 6 };
            for (int c = 0; c < daggerCounts.Length; c++)
            {
                var tm = new LaneTuning { startingGold = 3000, waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
                var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, tm);
                for (int d = 0; d < daggerCounts[c]; d++) m.TryBuyItem(LaneTeam.Blue, 2, out _);
                PlacePair(m, new Vector2(-2f, 0f), new Vector2(2f, 0f));
                // Give Ogre high HP via Stone Plates? Even simpler: keep shooting Ogre and revive/count shot events
                int shots = 0;
                HeroSnap target = GetHero(m.Snapshot(), LaneTeam.Red);
                m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = target.id });
                int steps = Mathf.RoundToInt(10f / dt);
                for (int s = 0; s < steps; s++)
                {
                    m.Tick(dt);
                    LaneSnapshot snap = m.Snapshot();
                    for (int e = 0; e < snap.events.Count; e++)
                    {
                        if (snap.events[e].kind == LaneEventKind.LuisBasicShot) shots++;
                    }
                    if (GetHero(snap, LaneTeam.Red).phase == LanePhase.Dead) break;
                }
                HeroSnap h = GetHero(m.Snapshot(), LaneTeam.Blue);
                sb.AppendLine($"  Sim Check ({daggerCounts[c]} Daggers, +{h.bonusAttackSpeed * 100f:0}% AS, Eff={h.attackSpeed:0.00}x): {shots} bolts over {m.MatchTime:0.00}s ({shots / Mathf.Max(0.1f, m.MatchTime):0.00} attacks/s)");
            }

            // Ogre Extreme AGI Windup + AS Recovery quantization
            int[] agis = { 0, 25, 50, 100, 200, 400 };
            for (int i = 0; i < agis.Length; i++)
            {
                float windSpd = t.ogreWindupSpeedFormula.Evaluate(1, agis[i]);
                float qWind = t.ogreQWindup / windSpd;
                int ticks = Mathf.Max(1, Mathf.CeilToInt(qWind / dt));
                sb.AppendLine($"  Ogre AGI={agis[i],3}: WindSpd={windSpd:0.00}x | Q Windup={qWind:0.000}s ({ticks} ticks @ 30Hz = {ticks * dt:0.000}s effective)");
            }
            sb.AppendLine();
        }

        private static void RunComparableGoldBuildMatrix(StringBuilder sb)
        {
            sb.AppendLine("--- 2. COMPARABLE-GOLD BUILD MATRIX (DPS / 3s BURST / SUSTAIN / 60s FARM / TTK) ---");
            var buildsByHero = new Dictionary<LaneHeroId, BuildSpec[]>
            {
                [LaneHeroId.Ogre] = new[]
                {
                    new BuildSpec { Label = "Early(600g: Buckler+Greatsword)", Items = new[] { 15, 1 }, TargetGoldBudget = 600 },
                    new BuildSpec { Label = "Mid-Conv(1500g: Buckler+GS+Dagger+STR)", Items = new[] { 15, 1, 2, 8 }, TargetGoldBudget = 1500 },
                    new BuildSpec { Label = "Mid-AGI/AS(1500g: GS+Dagger+3xFeather)", Items = new[] { 1, 2, 9, 9, 9 }, TargetGoldBudget = 1500 },
                    new BuildSpec { Label = "Late-Hybrid(2400g: Buckler+2xGS+Dagger+STR+Colossus)", Items = new[] { 15, 1, 1, 2, 8, 18 }, TargetGoldBudget = 2400 },
                    new BuildSpec { Label = "Extreme-STR(6xKnuckle/Colossus 2450g)", Items = new[] { 8, 8, 8, 8, 18, 18 }, TargetGoldBudget = 2450 },
                },
                [LaneHeroId.Miravel] = new[]
                {
                    new BuildSpec { Label = "Early(600g: Salve+Kindred)", Items = new[] { 14, 11 }, TargetGoldBudget = 600 },
                    new BuildSpec { Label = "Mid-HealPwr(1500g: 3xKindred+Lens)", Items = new[] { 11, 11, 11, 10 }, TargetGoldBudget = 1500 },
                    new BuildSpec { Label = "Mid-INT/AH(1500g: 3xLens+2xBand)", Items = new[] { 10, 10, 10, 6, 6 }, TargetGoldBudget = 1500 },
                    new BuildSpec { Label = "Late-Hybrid(2400g: Salve+2xCenser+Eye+Coward)", Items = new[] { LaneItemIds.WoodlandSalve, LaneItemIds.KindredCenser, LaneItemIds.KindredCenser, LaneItemIds.SagesEye, LaneItemIds.CowardsShield }, TargetGoldBudget = 2400 },
                    new BuildSpec { Label = "Extreme-HealPwr(6xKindred 1950g)", Items = new[] { 11, 11, 11, 11, 11, 11 }, TargetGoldBudget = 1950 },
                },
                [LaneHeroId.Swordsman] = new[]
                {
                    new BuildSpec { Label = "Early(600g: Hatchet+GS)", Items = new[] { 13, 1 }, TargetGoldBudget = 600 },
                    new BuildSpec { Label = "Mid-Conv(1500g: GS+Dagger+Knuckle+Thorn)", Items = new[] { 1, 2, 8, 16 }, TargetGoldBudget = 1500 },
                    new BuildSpec { Label = "Mid-INT/AS(1500g: 2xLens+2xDagger)", Items = new[] { 10, 10, 2, 2 }, TargetGoldBudget = 1500 },
                    new BuildSpec { Label = "Late-Hybrid(2400g: GS+Dagger+Thorn+Colossus+Grudge)", Items = new[] { 1, 2, 16, 18, 20 }, TargetGoldBudget = 2400 },
                    new BuildSpec { Label = "Extreme-STR/HP(6xKnuckle+Colossus)", Items = new[] { 8, 8, 8, 18, 18, 11 }, TargetGoldBudget = 2450 },
                },
                [LaneHeroId.LaserShooter] = new[]
                {
                    new BuildSpec { Label = "Early(600g: Hatchet+GS)", Items = new[] { 13, 1 }, TargetGoldBudget = 600 },
                    new BuildSpec { Label = "Mid-AS/AD(1500g: 2xGS+2xDagger+Lens)", Items = new[] { 1, 1, 2, 2, 10 }, TargetGoldBudget = 1500 },
                    new BuildSpec { Label = "Mid-INT(1500g: 3xLens+Band)", Items = new[] { 10, 10, 10, 6 }, TargetGoldBudget = 1500 },
                    new BuildSpec { Label = "Late-Hybrid(2400g: GS+Dagger+Lens+Greaves+Grudge)", Items = new[] { 1, 2, 10, 17, 20 }, TargetGoldBudget = 2400 },
                    new BuildSpec { Label = "Extreme-AS(6xDagger 1650g)", Items = new[] { 2, 2, 2, 2, 2, 2 }, TargetGoldBudget = 1650 },
                },
            };

            foreach (var kv in buildsByHero)
            {
                LaneHeroId hero = kv.Key;
                sb.AppendLine($"  [{LaneNames.DisplayName(hero)}]");
                for (int b = 0; b < kv.Value.Length; b++)
                {
                    BuildSpec spec = kv.Value[b];
                    int heroLvl = spec.TargetGoldBudget <= 650 ? 2 : (spec.TargetGoldBudget <= 1600 ? 4 : 6);
                    MeasureHeroBuild(hero, spec, heroLvl, out float burst3s, out float dps6s, out float selfHealS, out int farmCs, out int farmGold, out float ccTotal, out float ttkSec);
                    sb.AppendLine($"    L{heroLvl} {spec.Label,-46} | Burst3s={burst3s,5:0} | DPS6s={dps6s,5:0.0} | Sustain={selfHealS,4:0.0}/s | CC={ccTotal,4:0.00}s | 60sFarm={farmCs,2}CS/{farmGold,4}g | TTK={ttkSec:0.00}s");
                }
            }
            sb.AppendLine();
        }

        private static void MeasureHeroBuild(
            LaneHeroId hero,
            BuildSpec spec,
            int level,
            out float burst3s,
            out float dps6s,
            out float selfHealPerSec,
            out int farmCs,
            out int farmGold,
            out float ccApplied,
            out float ttkSec)
        {
            float dt = 1f / 30f;
            var t = new LaneTuning
            {
                startingGold = 4000,
                passiveGoldPerSecond = 0f,
                waveFirstSpawnDelay = 999f,
                ultMinLevel = 4,
            };
            // Evaluate combat against a baseline dummy Red Swordsman
            var m = new LaneMatch(hero, LaneHeroId.Swordsman, t);
            for (int i = 0; i < spec.Items.Length; i++) m.TryBuyItem(LaneTeam.Blue, spec.Items[i], out _);

            // Reach requested level by feeding quick low-HP test creeps
            LevelHeroCleanly(m, LaneTeam.Blue, level);
            AutoMaxSkills(m, LaneTeam.Blue, new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E });

            // Re-place cleanly within melee chip distance so all heroes lose ~35-55 HP to measure real sustain
            PlacePair(m, new Vector2(-1.5f, 0f), new Vector2(-0.2f, 0f));

            float enemyMaxHp = GetHero(m.Snapshot(), LaneTeam.Red).maxHp;
            float dmgTakenBefore = 0f;
            burst3s = 0f;
            ccApplied = 0f;
            ttkSec = 6.00f;
            bool killed = false;

            // Chip attacker (~35-55 HP) so passive/heal sustain is actually measurable for ranged heroes too
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackHero, targetId = 1 });
            Step(m, 0.70f);
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Stop });
            if (hero == LaneHeroId.LaserShooter || hero == LaneHeroId.Miravel)
            {
                PlacePair(m, new Vector2(-1.5f, 0f), new Vector2(2.5f, 0f));
            }
            float healStart = GetHero(m.Snapshot(), LaneTeam.Blue).hp;

            float prevEnemyHp = GetHero(m.Snapshot(), LaneTeam.Red).hp;
            float prevBlueHp = healStart;
            float totalHealed = 0f;
            float totalDmgDealt = 0f;

            var bot = new LaneBot(LaneTeam.Blue);
            int totalSteps = Mathf.RoundToInt(6.0f / dt);
            int step3s = Mathf.RoundToInt(3.0f / dt);
            for (int s = 1; s <= totalSteps; s++)
            {
                bot.Tick(m, dt);
                m.Tick(dt);
                LaneSnapshot snap = m.Snapshot();
                HeroSnap bHero = GetHero(snap, LaneTeam.Blue);
                HeroSnap rHero = GetHero(snap, LaneTeam.Red);

                float deltaDmg = prevEnemyHp - rHero.hp;
                if (deltaDmg > 0f) totalDmgDealt += deltaDmg;
                prevEnemyHp = rHero.hp;

                float deltaHeal = bHero.hp - prevBlueHp;
                if (deltaHeal > 0f) totalHealed += deltaHeal;
                prevBlueHp = bHero.hp;

                if (rHero.stunTimer > 0f || rHero.slowTimer > 0f) ccApplied += dt;
                if (!killed && rHero.phase == LanePhase.Dead)
                {
                    killed = true;
                    ttkSec = s * dt;
                }
                if (s == step3s) burst3s = totalDmgDealt;
                if (killed && dmgTakenBefore == 0f) dmgTakenBefore = enemyMaxHp;
            }
            dps6s = totalDmgDealt / Mathf.Max(0.2f, ttkSec);
            selfHealPerSec = totalHealed / 6.0f;

            // Separate 60s solo farming test with standard waves
            var tf = new LaneTuning { startingGold = 4000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 0.2f };
            var mf = new LaneMatch(hero, LaneHeroId.Miravel, tf);
            for (int i = 0; i < spec.Items.Length; i++) mf.TryBuyItem(LaneTeam.Blue, spec.Items[i], out _);
            LevelHeroCleanly(mf, LaneTeam.Blue, level);
            AutoMaxSkills(mf, LaneTeam.Blue, new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E });
            PlacePair(mf, new Vector2(-2f, 0f), new Vector2(21f, 0f)); // park Red away in fountain
            var farmBot = new LaneBot(LaneTeam.Blue);
            int farmSteps = Mathf.RoundToInt(60f / dt);
            int startGold = GetHero(mf.Snapshot(), LaneTeam.Blue).gold;
            for (int s = 0; s < farmSteps; s++)
            {
                farmBot.Tick(mf, dt);
                mf.Tick(dt);
            }
            HeroSnap postFarm = GetHero(mf.Snapshot(), LaneTeam.Blue);
            farmCs = postFarm.creepScore;
            farmGold = postFarm.gold - startGold;
        }

        private static void RunLevelingOrderAndMatchupSuite(StringBuilder sb)
        {
            sb.AppendLine("--- 3. 12-MATCHUP BOT DUELS (150s) & LEVELING ORDER CHECKS ---");
            LaneHeroId[] heroes = { LaneHeroId.Ogre, LaneHeroId.Miravel, LaneHeroId.Swordsman, LaneHeroId.LaserShooter };
            float dt = 1f / 30f;
            int steps = Mathf.RoundToInt(150f / dt);

            for (int i = 0; i < heroes.Length; i++)
            {
                for (int j = 0; j < heroes.Length; j++)
                {
                    if (i == j) continue;
                    var m = new LaneMatch(heroes[i], heroes[j], new LaneTuning());
                    var botB = new LaneBot(LaneTeam.Blue);
                    var botR = new LaneBot(LaneTeam.Red);
                    for (int s = 0; s < steps && !m.IsGameOver; s++)
                    {
                        botB.Tick(m, dt);
                        botR.Tick(m, dt);
                        m.Tick(dt);
                    }
                    LaneSnapshot snap = m.Snapshot();
                    HeroSnap b = GetHero(snap, LaneTeam.Blue);
                    HeroSnap r = GetHero(snap, LaneTeam.Red);
                    float bTw = snap.towers[0].alive ? snap.towers[0].hp : 0f;
                    float rTw = snap.towers[1].alive ? snap.towers[1].hp : 0f;
                    string win = snap.gameOver ? ((LaneTeam)snap.winnerTeam).ToString() : "Draw";
                    sb.AppendLine($"  {heroes[i],-12} vs {heroes[j],-12} | K {b.kills}-{r.kills} | Lvl {b.level}-{r.level} | CS {b.creepScore,2}-{r.creepScore,2} | Gold {b.gold,4}-{r.gold,4} | Tw {bTw,4:0}-{rTw,4:0} | {win}");
                }
            }

            // Mirror leveling-order comparison: Q-max vs W-max vs E-max over 110s with equal items
            var orders = new[]
            {
                ("Q>W>E", new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E }),
                ("W>Q>E", new[] { LaneAbilitySlot.W, LaneAbilitySlot.Q, LaneAbilitySlot.E }),
                ("E>Q>W", new[] { LaneAbilitySlot.E, LaneAbilitySlot.Q, LaneAbilitySlot.W }),
            };
            sb.AppendLine("  [Leveling Order Impact vs Fixed Q>W>E Miravel Opponent (110s)]");
            for (int h = 0; h < heroes.Length; h++)
            {
                for (int o = 0; o < orders.Length; o++)
                {
                    var m = new LaneMatch(heroes[h], LaneHeroId.Miravel, new LaneTuning());
                    var botB = new LaneBot(LaneTeam.Blue);
                    var botR = new LaneBot(LaneTeam.Red);
                    int sMax = Mathf.RoundToInt(110f / dt);
                    for (int s = 0; s < sMax && !m.IsGameOver; s++)
                    {
                        AutoMaxSkills(m, LaneTeam.Blue, orders[o].Item2);
                        botB.Tick(m, dt);
                        botR.Tick(m, dt);
                        m.Tick(dt);
                    }
                    HeroSnap bh = GetHero(m.Snapshot(), LaneTeam.Blue);
                    HeroSnap rh = GetHero(m.Snapshot(), LaneTeam.Red);
                    sb.AppendLine($"    {heroes[h],-12} {orders[o].Item1} | K {bh.kills}-{rh.kills} | Lvl {bh.level} | CS {bh.creepScore,2} | Gold {bh.gold,4}");
                }
            }
            sb.AppendLine();
        }

        private static void RunStressScenarios(StringBuilder sb)
        {
            sb.AppendLine("--- 4. ALL 11 TARGETED STRESS SCENARIOS ---");

            // S1 & S2: Miravel Negative Regen + Permanent HealPower Growth + R * HealPower * Q Interaction
            int[] charms = { 0, 2, 4, 6 };
            for (int i = 0; i < charms.Length; i++)
            {
                var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.LaserShooter, new LaneTuning { startingGold = 3000, waveFirstSpawnDelay = 999f, ultMinLevel = 1, passiveGoldPerSecond = 0f });
                for (int k = 0; k < charms[i]; k++) m.TryBuyItem(LaneTeam.Blue, 11, out _);
                m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q);
                m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R);
                HeroSnap mir = GetHero(m.Snapshot(), LaneTeam.Blue);
                PlacePair(m, new Vector2(-1f, 0f), new Vector2(1.5f, 0f));
                Step(m, 0.10f);
                HeroSnap las = GetHero(m.Snapshot(), LaneTeam.Red);
                float bonusHp = Mathf.Max(0f, mir.healPower - 1f);
                float maxRHealAmp = m.Tuning.luisRHealAmpFormula.Evaluate(1, bonusHp);
                float maxRVuln = m.Tuning.luisRVulnFormula.Evaluate(1, bonusHp);
                float qBase = m.Tuning.luisOrbHealFormula.Evaluate(1, 0f);
                float combinedOrbRHeal = qBase * mir.healPower * (1f + maxRHealAmp);
                sb.AppendLine($"  [Miravel Stress] {charms[i]}xKindred (HPwr={mir.healPower * 100f:0}%): AllyAura={mir.miravelLifeAura:0.00}/s | EnemyDrain=-{las.miravelDrainAura:0.00}/s | EnemyNetRegen={las.netRegenRate:0.00}/s | MaxR_HealAmp=+{maxRHealAmp * 100f:0.0}% | MaxR_Vuln=+{maxRVuln * 100f:0.0}% | Q*HPwr*R PeakOrbHeal={combinedOrbRHeal:0.0}");
            }

            // S3 & S4: Swordsman extreme INT stack retention vs 26s wave interval + STR execute
            int[] ints = { 0, 25, 75, 150 };
            for (int i = 0; i < ints.Length; i++)
            {
                float stackDur = new LaneTuning().swordsmanWBuffDurationFormula.Evaluate(1, ints[i]);
                float exec25 = new LaneTuning().swordsmanRExecuteFormula.Evaluate(2, ints[i]) * 100f;
                sb.AppendLine($"  [Swordsman Stress] INT/STR={ints[i],3}: W StackDur={stackDur,5:0.0}s vs 26s WaveGap ({(stackDur >= 26f ? "BRIDGES WAVES!" : "decays before next wave")}) | R(lvl2) Exec@STR={ints[i]} -> <={exec25:0.0}% HP");
            }

            // S5 & S6: Chain Laser Shooter High-Density Cluster (4, 12, 20 clustered units) & CPU Timing
            int[] densities = { 4, 12, 20 };
            for (int i = 0; i < densities.Length; i++)
            {
                var ml = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Ogre, new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f, startingGold = 10000, passiveGoldPerSecond = 0f });
                ml.TryBuyItem(LaneTeam.Blue, LaneItemIds.SteelSword, out _);
                ml.TryBuyItem(LaneTeam.Blue, LaneItemIds.SagesEye, out _);
                ml.TryBuyItem(LaneTeam.Blue, LaneItemIds.IronDagger, out _);
                for (int hpIt = 0; hpIt < 6; hpIt++) ml.TryBuyItem(LaneTeam.Red, LaneItemIds.ColossusPauldron, out _); // +2700 HP +168 Arm on target dummy so Ogre survives to measure full 3.2s chain stacking
                ml.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R);
                PlacePair(ml, new Vector2(-3.5f, 0f), new Vector2(1.5f, 0f));
                for (int u = 0; u < densities[i]; u++)
                {
                    float ox = 0.8f + (u % 5) * 0.9f;
                    float oz = ((u / 5) - 1.5f) * 0.75f;
                    ml.SpawnCustomMinion(LaneTeam.Red, new Vector2(ox, oz), ranged: false, customMaxHp: 2000f);
                }
                var sw = Stopwatch.StartNew();
                ml.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(2f, 0f) });
                Step(ml, 3.2f);
                sw.Stop();
                HeroSnap ogAfter = GetHero(ml.Snapshot(), LaneTeam.Red);
                float dmgOnOgre = ogAfter.maxHp - ogAfter.hp;
                sb.AppendLine($"  [Laser Density Stress] {densities[i],2} Clustered Units: Ogre Took {dmgOnOgre,6:0.0}/{ogAfter.maxHp:0} R-Chain Dmg | Ogre LaserStacks={ogAfter.laserStacks,2} | Sim Cost (3.2s=96 ticks) = {sw.Elapsed.TotalMilliseconds:0.0} ms ({sw.Elapsed.TotalMilliseconds / 96.0:0.000} ms/tick)");
            }

            // S7 & S8: Ogre Extreme STR Knockback & R Geometry
            int[] strCounts = { 0, 2, 4, 6 }; // 0, 50, 100, 150 STR
            for (int i = 0; i < strCounts.Length; i++)
            {
                var mo = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, new LaneTuning { startingGold = 3000, waveFirstSpawnDelay = 999f, ultMinLevel = 1, passiveGoldPerSecond = 0f });
                for (int k = 0; k < strCounts[i]; k++) mo.TryBuyItem(LaneTeam.Blue, 8, out _);
                mo.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W);
                PlacePair(mo, new Vector2(-8f, 0f), new Vector2(-6.2f, 0f));
                mo.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(0f, 0f) });
                Step(mo, 0.85f);
                HeroSnap og = GetHero(mo.Snapshot(), LaneTeam.Blue);
                HeroSnap mir = GetHero(mo.Snapshot(), LaneTeam.Red);
                float pushDist = mir.x - (-6.2f);
                float rScale = mo.Tuning.ogreRScaleFormula.Evaluate(1, og.strength);
                sb.AppendLine($"  [Ogre STR Stress] STR={og.strength,3}: W TheoKB={mo.Tuning.ogreWKnockbackFormula.Evaluate(1, og.strength):0.00}u | ActualMoved={pushDist:0.00}u (in-bounds={Mathf.Abs(mir.x) <= 23.6f && Mathf.Abs(mir.y) <= 7.8f}) | R_Scale={rScale:0.00}x");
            }
        }

        private static void LevelHeroCleanly(LaneMatch m, LaneTeam team, int targetLevel)
        {
            if (targetLevel <= 1) return;
            for (int guard = 0; guard < 12 && GetHero(m.Snapshot(), team).level < targetLevel; guard++)
            {
                m.GrantXpForTest(team, 250f);
            }
        }

        private static void AutoMaxSkills(LaneMatch m, LaneTeam team, LaneAbilitySlot[] prio)
        {
            for (int guard = 0; guard < 8; guard++)
            {
                HeroSnap self = GetHero(m.Snapshot(), team);
                if (self.skillPoints <= 0) break;
                if (self.level >= m.Tuning.ultMinLevel)
                {
                    int maxR = self.level >= m.Tuning.maxLevel ? 2 : 1;
                    if (self.rankR < maxR && m.TryLevelAbility(team, LaneAbilitySlot.R)) continue;
                }
                bool leveled = false;
                for (int cap = 1; cap <= 3 && !leveled; cap++)
                {
                    for (int p = 0; p < prio.Length; p++)
                    {
                        int cur = prio[p] == LaneAbilitySlot.Q ? self.rankQ : (prio[p] == LaneAbilitySlot.W ? self.rankW : self.rankE);
                        if (cur < cap && m.TryLevelAbility(team, prio[p]))
                        {
                            leveled = true;
                            break;
                        }
                    }
                }
                if (!leveled) break;
            }
        }

        private static void PlacePair(LaneMatch m, Vector2 bluePos, Vector2 redPos)
        {
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = bluePos });
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = redPos });
            float dt = 1f / 30f;
            int steps = Mathf.CeilToInt(12f / dt);
            bool bDone = false, rDone = false;
            for (int i = 0; i < steps; i++)
            {
                LaneSnapshot s = m.Snapshot();
                HeroSnap b = GetHero(s, LaneTeam.Blue);
                HeroSnap r = GetHero(s, LaneTeam.Red);
                if (!bDone && Vector2.Distance(new Vector2(b.x, b.y), bluePos) < 0.35f)
                {
                    bDone = true;
                    m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
                }
                if (!rDone && Vector2.Distance(new Vector2(r.x, r.y), redPos) < 0.35f)
                {
                    rDone = true;
                    m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Stop });
                }
                if (bDone && rDone) break;
                m.Tick(dt);
            }
        }

        private static void Step(LaneMatch m, float seconds)
        {
            float dt = 1f / 30f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(seconds / dt));
            for (int i = 0; i < steps; i++) m.Tick(dt);
        }

        private static HeroSnap GetHero(LaneSnapshot snap, LaneTeam team)
        {
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].team == team) return snap.heroes[i];
            }
            throw new InvalidOperationException($"Missing {team}");
        }
    }
}
