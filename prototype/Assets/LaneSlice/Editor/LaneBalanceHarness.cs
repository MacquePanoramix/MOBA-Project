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
    /// Reusable headless balancing & stress-test suite (v0.3.0) evaluating curve math,
    /// 30Hz simulation tick resolution under extreme stats (including First Strike quantization),
    /// comparable-gold build packages (actual gold spent, custom & Mana items, cross-role builds),
    /// burst vs sustained combat, Coward's Shield disengage, 45s Mana endurance, 12 hero matchups
    /// with item completion & OOM timings, leveling orders, and targeted stress scenarios.
    /// Outputs structured findings to prototype/Artifacts/balance_report_v030.txt.
    /// </summary>
    public static class LaneBalanceHarness
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class BuildSpec
        {
            public string Label;
            public int[] Items;
        }

        public static void RunAll()
        {
            var prevCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = Inv;
            try
            {
                var sb = new StringBuilder();
                var t = new LaneTuning();
                sb.AppendLine($"=== ULTIMATE FREE MOBA v0.3.0 SCALING, RESOURCE & ITEM BALANCE REPORT ({DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC) ===");
                sb.AppendLine($"TickHz = {t.tickHz}, Global AS k = {t.attackSpeedCurveK} (EffAS = sqrt(1 + {t.attackSpeedCurveK} * BonusAS))");
                sb.AppendLine();

                RunTickResolutionAndAsAudit(sb, t);
                RunComparableGoldBuildMatrix(sb);
                RunSustainedDisengageAndManaEndurance(sb);
                RunLevelingOrderAndMatchupSuite(sb);
                RunStressScenarios(sb);

                string outDir = Path.Combine(Application.dataPath, "..", "Artifacts");
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                string outPath = Path.GetFullPath(Path.Combine(outDir, "balance_report_v030.txt"));
                File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[LaneBalanceHarness] Report written to {outPath}");
                Debug.Log(sb.ToString());
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = prevCulture;
            }
        }

        private static void RunTickResolutionAndAsAudit(StringBuilder sb, LaneTuning t)
        {
            sb.AppendLine("--- 1. GLOBAL ATTACK SPEED, FIRST STRIKE & 30Hz TICK-RESOLUTION AUDIT ---");
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

            // Live simulated Miravel attack count check with 0, 1, 2, 6 Iron Daggers
            int[] daggerCounts = { 0, 1, 2, 6 };
            for (int c = 0; c < daggerCounts.Length; c++)
            {
                var tm = new LaneTuning { startingGold = 3000, waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
                var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, tm);
                for (int d = 0; d < daggerCounts[c]; d++) m.TryBuyItem(LaneTeam.Blue, LaneItemIds.IronDagger, out _);
                PlacePair(m, new Vector2(-2f, 0f), new Vector2(2f, 0f));
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
                sb.AppendLine($"  Sim Check ({daggerCounts[c]} Iron Daggers, +{h.bonusAttackSpeed * 100f:0}% AS, Eff={h.attackSpeed:0.00}x): {shots} bolts over {m.MatchTime:0.00}s ({shots / Mathf.Max(0.1f, m.MatchTime):0.00} attacks/s)");
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

            // Part 12.4 First Strike 30Hz Tick-Quantization Audit (without altering approved global AS curve)
            float[] fsAsScales = { 0f, 0.50f, 1.50f };
            for (int a = 0; a < fsAsScales.Length; a++)
            {
                float effAs = LaneTuning.EffectiveAttackSpeed(fsAsScales[a], t.attackSpeedCurveK);
                for (int i = 0; i < agis.Length; i++)
                {
                    float red = Mathf.Clamp(t.firstStrikeReductionFormula.Evaluate(1, agis[i]), 0f, 0.90f);
                    float rawReduced = Mathf.Max(t.firstStrikeMinRawWindup, t.laserBasicWindup * (1f - red));
                    float theoFinal = Mathf.Max(t.firstStrikeMinFinalWindup, rawReduced / effAs);
                    int ticks = Mathf.Max(1, Mathf.CeilToInt(theoFinal / dt));
                    sb.AppendLine($"  FirstStrike(Laser 0.22s) BonusAS={fsAsScales[a] * 100f,3:0}% AGI={agis[i],3}: Red={red * 100f,4:0.0}% Theo={theoFinal:0.000}s | Quant30Hz={ticks}t ({ticks * dt:0.000}s)");
                }
            }
            sb.AppendLine();
        }

        private static void RunComparableGoldBuildMatrix(StringBuilder sb)
        {
            sb.AppendLine("--- 2. COMPARABLE-GOLD BUILD MATRIX (ACTUAL GOLD SPENT / BURST / DPS / SUSTAIN / 60s FARM / TTK) ---");
            var buildsByHero = new Dictionary<LaneHeroId, BuildSpec[]>
            {
                [LaneHeroId.Ogre] = new[]
                {
                    new BuildSpec { Label = "Early(Buckler+SteelSword)", Items = new[] { LaneItemIds.BrawlersBuckler, LaneItemIds.SteelSword } },
                    new BuildSpec { Label = "Mid-Colossus(1360g)+SmallAxe", Items = new[] { LaneItemIds.ColossusPauldron, LaneItemIds.SmallAxe } },
                    new BuildSpec { Label = "Mid-Conv(HeavyAxe+Titan+Feather)", Items = new[] { LaneItemIds.HeavyAxe, LaneItemIds.Titanstone, LaneItemIds.Windfeather } },
                    new BuildSpec { Label = "Late-Juggernaut(GiantsHeart+Buckler)", Items = new[] { LaneItemIds.GiantsHeart, LaneItemIds.BrawlersBuckler } },
                    new BuildSpec { Label = "Late-Cross(DraconicBlade+Buckler)", Items = new[] { LaneItemIds.DraconicBlade, LaneItemIds.BrawlersBuckler } },
                },
                [LaneHeroId.Miravel] = new[]
                {
                    new BuildSpec { Label = "Early(ManaDew+SacredPearl+Spring)", Items = new[] { LaneItemIds.ManaDewVial, LaneItemIds.SacredPearl, LaneItemIds.SpringWater } },
                    new BuildSpec { Label = "Mid-Sustain(ChaliceOfLife+Salve)", Items = new[] { LaneItemIds.ChaliceOfLife, LaneItemIds.WoodlandSalve } },
                    new BuildSpec { Label = "Mid-ManaFlow(TidecallerChalice+Charm)", Items = new[] { LaneItemIds.TidecallerChalice, LaneItemIds.WingedCharm } },
                    new BuildSpec { Label = "Late-Hybrid(Chalice+Coward+Dew)", Items = new[] { LaneItemIds.ChaliceOfLife, LaneItemIds.CowardsShield, LaneItemIds.ManaDewVial } },
                    new BuildSpec { Label = "Late-Efficiency(Astral+Economist)", Items = new[] { LaneItemIds.AstralReservoir, LaneItemIds.EconomistsTome } },
                },
                [LaneHeroId.Swordsman] = new[]
                {
                    new BuildSpec { Label = "Early(Hatchet+SteelSword)", Items = new[] { LaneItemIds.MinersHatchet, LaneItemIds.SteelSword } },
                    new BuildSpec { Label = "Mid-Executioner(ExecutionersAxe)", Items = new[] { LaneItemIds.ExecutionersAxe } },
                    new BuildSpec { Label = "Mid-CrossCrescent(NimbleDagger+Thorn)", Items = new[] { LaneItemIds.NimbleDagger, LaneItemIds.BlightedThorn } },
                    new BuildSpec { Label = "Late-Draconic(Draconic+Hatchet)", Items = new[] { LaneItemIds.DraconicBlade, LaneItemIds.MinersHatchet } },
                    new BuildSpec { Label = "Late-CrossTitan(GiantsHeart+Hatchet)", Items = new[] { LaneItemIds.GiantsHeart, LaneItemIds.MinersHatchet } },
                },
                [LaneHeroId.LaserShooter] = new[]
                {
                    new BuildSpec { Label = "Early(ManaDew+ShortSword+Dagger)", Items = new[] { LaneItemIds.ManaDewVial, LaneItemIds.ShortSword, LaneItemIds.IronDagger } },
                    new BuildSpec { Label = "Mid-Tempo(Astral+Dagger+Short)", Items = new[] { LaneItemIds.AstralReservoir, LaneItemIds.IronDagger, LaneItemIds.ShortSword } },
                    new BuildSpec { Label = "Mid-Economist(Economist+Dagger)", Items = new[] { LaneItemIds.EconomistsTome, LaneItemIds.IronDagger } },
                    new BuildSpec { Label = "Late-Banana(BananaBlade+Dew)", Items = new[] { LaneItemIds.BananaBlade, LaneItemIds.ManaDewVial } },
                    new BuildSpec { Label = "Late-CrossAxe(Executioner+Dew+Dagger)", Items = new[] { LaneItemIds.ExecutionersAxe, LaneItemIds.ManaDewVial, LaneItemIds.IronDagger } },
                },
            };

            foreach (var kv in buildsByHero)
            {
                LaneHeroId hero = kv.Key;
                sb.AppendLine($"  [{LaneNames.DisplayName(hero)}]");
                for (int b = 0; b < kv.Value.Length; b++)
                {
                    BuildSpec spec = kv.Value[b];
                    MeasureHeroBuild(hero, spec, out int heroLvl, out int goldSpent, out float burst3s, out float dps6s, out float selfHealS, out int farmCs, out int farmGold, out float ccTotal, out float ttkSec);
                    sb.AppendLine($"    L{heroLvl} ({goldSpent,4}g) {spec.Label,-39} | Burst3s={burst3s,5:0} | DPS6s={dps6s,5:0.0} | Sustain={selfHealS,4:0.0}/s | CC={ccTotal,4:0.00}s | 60sFarm={farmCs,2}CS/{farmGold,4}g | TTK={ttkSec:0.00}s");
                }
            }
            sb.AppendLine();
        }

        private static void MeasureHeroBuild(
            LaneHeroId hero,
            BuildSpec spec,
            out int level,
            out int goldSpent,
            out float burst3s,
            out float dps6s,
            out float selfHealPerSec,
            out int farmCs,
            out int farmGold,
            out float ccApplied,
            out float ttkSec)
        {
            float dt = 1f / 30f;
            const int startBudget = 10000;
            var t = new LaneTuning
            {
                startingGold = startBudget,
                passiveGoldPerSecond = 0f,
                waveFirstSpawnDelay = 999f,
                ultMinLevel = 4,
            };
            var m = new LaneMatch(hero, LaneHeroId.Swordsman, t);
            for (int i = 0; i < spec.Items.Length; i++) m.TryBuyItem(LaneTeam.Blue, spec.Items[i], out _);
            goldSpent = startBudget - GetHero(m.Snapshot(), LaneTeam.Blue).gold;
            level = goldSpent <= 700 ? 2 : (goldSpent <= 1650 ? 4 : 6);
            m.SetHeroGoldForTest(LaneTeam.Blue, 0);
            m.SetHeroGoldForTest(LaneTeam.Red, 0);

            LevelHeroCleanly(m, LaneTeam.Blue, level);
            AutoMaxSkills(m, LaneTeam.Blue, new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E });

            PlacePair(m, new Vector2(-1.5f, 0f), new Vector2(-0.2f, 0f));

            burst3s = 0f;
            ccApplied = 0f;
            ttkSec = 6.00f;
            bool killed = false;

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
            }
            dps6s = totalDmgDealt / Mathf.Max(0.2f, ttkSec);
            selfHealPerSec = totalHealed / 6.0f;

            var tf = new LaneTuning { startingGold = startBudget, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 0.2f };
            var mf = new LaneMatch(hero, LaneHeroId.Miravel, tf);
            for (int i = 0; i < spec.Items.Length; i++) mf.TryBuyItem(LaneTeam.Blue, spec.Items[i], out _);
            mf.SetHeroGoldForTest(LaneTeam.Blue, 0);
            mf.SetHeroGoldForTest(LaneTeam.Red, 0);
            LevelHeroCleanly(mf, LaneTeam.Blue, level);
            AutoMaxSkills(mf, LaneTeam.Blue, new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E });
            PlacePair(mf, new Vector2(-2f, 0f), new Vector2(21f, 0f));
            var farmBot = new LaneBot(LaneTeam.Blue);
            int farmSteps = Mathf.RoundToInt(60f / dt);
            for (int s = 0; s < farmSteps; s++)
            {
                farmBot.Tick(mf, dt);
                mf.Tick(dt);
            }
            HeroSnap postFarm = GetHero(mf.Snapshot(), LaneTeam.Blue);
            farmCs = postFarm.creepScore;
            farmGold = postFarm.gold;
        }

        private static void RunSustainedDisengageAndManaEndurance(StringBuilder sb)
        {
            sb.AppendLine("--- 3. BURST VS SUSTAINED (12s), DISENGAGE SURVIVABILITY & 45s MANA ENDURANCE ---");
            float dt = 1f / 30f;

            // A. Burst (3s) vs Sustained (12s) vs Durable Target Dummy (4x Heartstone)
            var combatCases = new[]
            {
                (hero: LaneHeroId.Swordsman, label: "Swordsman (Executioner+SteelSword)", items: new[] { LaneItemIds.ExecutionersAxe, LaneItemIds.SteelSword }),
                (hero: LaneHeroId.Swordsman, label: "Swordsman (DraconicBlade+SmallAxe)", items: new[] { LaneItemIds.DraconicBlade, LaneItemIds.SmallAxe }),
                (hero: LaneHeroId.Swordsman, label: "Swordsman (BananaBlade)", items: new[] { LaneItemIds.BananaBlade }),
                (hero: LaneHeroId.Ogre, label: "Ogre (GiantsHeart+SmallAxe)", items: new[] { LaneItemIds.GiantsHeart, LaneItemIds.SmallAxe }),
                (hero: LaneHeroId.LaserShooter, label: "LaserShooter (BananaBlade+ManaDew)", items: new[] { LaneItemIds.BananaBlade, LaneItemIds.ManaDewVial }),
                (hero: LaneHeroId.Miravel, label: "Miravel (ChaliceOfLife+Tidecaller)", items: new[] { LaneItemIds.ChaliceOfLife, LaneItemIds.TidecallerChalice }),
            };
            for (int c = 0; c < combatCases.Length; c++)
            {
                var tc = new LaneTuning { startingGold = 10000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f, ultMinLevel = 4 };
                var mc = new LaneMatch(combatCases[c].hero, LaneHeroId.Ogre, tc);
                for (int it = 0; it < combatCases[c].items.Length; it++) mc.TryBuyItem(LaneTeam.Blue, combatCases[c].items[it], out _);
                for (int hs = 0; hs < 4; hs++) mc.TryBuyItem(LaneTeam.Red, LaneItemIds.Heartstone, out _);
                int goldSpent = 10000 - GetHero(mc.Snapshot(), LaneTeam.Blue).gold;
                mc.SetHeroGoldForTest(LaneTeam.Blue, 0);
                mc.SetHeroGoldForTest(LaneTeam.Red, 0);
                LevelHeroCleanly(mc, LaneTeam.Blue, 6);
                AutoMaxSkills(mc, LaneTeam.Blue, new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E });
                PlacePair(mc, new Vector2(-1.5f, 0f), new Vector2(1.2f, 0f));

                var bot = new LaneBot(LaneTeam.Blue);
                float startDummyHp = GetHero(mc.Snapshot(), LaneTeam.Red).hp;
                float prevDummyHp = startDummyHp;
                float dealt3s = 0f, dealt12s = 0f;
                int steps12 = Mathf.RoundToInt(12f / dt);
                int step3 = Mathf.RoundToInt(3f / dt);
                for (int s = 1; s <= steps12; s++)
                {
                    bot.Tick(mc, dt);
                    mc.Tick(dt);
                    HeroSnap d = GetHero(mc.Snapshot(), LaneTeam.Red);
                    float dlt = prevDummyHp - d.hp;
                    if (dlt > 0f) dealt12s += dlt;
                    prevDummyHp = d.hp;
                    if (s == step3) dealt3s = dealt12s;
                    if (d.hp < 800f) mc.SetHeroHpForTest(LaneTeam.Red, startDummyHp);
                    prevDummyHp = GetHero(mc.Snapshot(), LaneTeam.Red).hp;
                }
                sb.AppendLine($"  [Burst vs 12s Sust] {combatCases[c].label,-34} ({goldSpent,4}g) | 3s={dealt3s,6:0} ({dealt3s / 3f,5:0.0}/s) | 12s={dealt12s,6:0} ({dealt12s / 12f,5:0.0}/s)");
            }

            // B. Coward's Shield Disengage Survivability (Miravel retreating 4.0s from chasing Swordsman)
            for (int hasCoward = 0; hasCoward <= 1; hasCoward++)
            {
                var td = new LaneTuning { startingGold = 3000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };
                var md = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Swordsman, td);
                if (hasCoward == 1) md.TryBuyItem(LaneTeam.Blue, LaneItemIds.CowardsShield, out _);
                md.TryBuyItem(LaneTeam.Red, LaneItemIds.SteelSword, out _);
                md.TryBuyItem(LaneTeam.Red, LaneItemIds.IronDagger, out _);
                PlacePair(md, new Vector2(-2f, 0f), new Vector2(0f, 0f));
                md.ApplyDamageForTest(LaneTeam.Blue, 20f, false, false, LaneTeam.Red);
                md.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-18f, 0f) });
                float peakBonusArm = 0f;
                int redId = GetHero(md.Snapshot(), LaneTeam.Red).id;
                int bId = GetHero(md.Snapshot(), LaneTeam.Blue).id;
                int chaseSteps = Mathf.RoundToInt(4.0f / dt);
                for (int s = 0; s < chaseSteps; s++)
                {
                    if (s % 4 == 0) md.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackHero, targetId = bId });
                    md.Tick(dt);
                    HeroSnap b = GetHero(md.Snapshot(), LaneTeam.Blue);
                    if (b.cowardShieldBonus > peakBonusArm) peakBonusArm = b.cowardShieldBonus;
                }
                HeroSnap endMir = GetHero(md.Snapshot(), LaneTeam.Blue);
                sb.AppendLine($"  [Disengage 4s] Miravel {(hasCoward == 1 ? "WITH Coward's Shield" : "NO Coward's Shield  ")}: HP={endMir.hp,5:0.0}/{endMir.maxHp:0} | PeakFlightResist=+{peakBonusArm:0.0}");
            }

            // C. 45s Mana Endurance & Casts-to-OOM
            LaneHeroId[] manaHeroes = { LaneHeroId.Miravel, LaneHeroId.LaserShooter };
            var manaLoadouts = new[]
            {
                (label: "No Mana Items", itemId: 0),
                (label: "Mana Dew Vial (200g)", itemId: LaneItemIds.ManaDewVial),
                (label: "Wellspring Flask (520g)", itemId: LaneItemIds.WellspringFlask),
                (label: "Tidecaller Chalice (1080g)", itemId: LaneItemIds.TidecallerChalice),
                (label: "Astral Reservoir (1120g)", itemId: LaneItemIds.AstralReservoir),
                (label: "Economist's Tome (1160g)", itemId: LaneItemIds.EconomistsTome),
            };
            for (int h = 0; h < manaHeroes.Length; h++)
            {
                for (int p = 0; p < manaLoadouts.Length; p++)
                {
                    var tm = new LaneTuning { startingGold = 3000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };
                    var mm = new LaneMatch(manaHeroes[h], LaneHeroId.Ogre, tm);
                    if (manaLoadouts[p].itemId > 0) mm.TryBuyItem(LaneTeam.Blue, manaLoadouts[p].itemId, out _);
                    mm.SetHeroGoldForTest(LaneTeam.Blue, 0);
                    LevelHeroCleanly(mm, LaneTeam.Blue, 4);
                    AutoMaxSkills(mm, LaneTeam.Blue, new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E });
                    PlacePair(mm, new Vector2(-1f, 0f), new Vector2(2f, 0f));

                    // For Chain Laser, land 1 Mana-free basic attack first so Ogre holds a Laser stack for W shockwave casts
                    if (manaHeroes[h] == LaneHeroId.LaserShooter)
                    {
                        int targetId = GetHero(mm.Snapshot(), LaneTeam.Red).id;
                        mm.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = targetId });
                        Step(mm, 0.50f);
                        mm.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
                    }

                    int castsOk = 0;
                    float firstOomSec = -1f;
                    int endSteps = Mathf.RoundToInt(45f / dt);
                    for (int s = 0; s < endSteps; s++)
                    {
                        int phaseMod = s % 60;
                        LaneAbilitySlot slotToTry = LaneAbilitySlot.None;
                        LaneOrderKind orderKind = LaneOrderKind.Stop;
                        if (phaseMod == 0 && manaHeroes[h] == LaneHeroId.LaserShooter)
                        {
                            slotToTry = LaneAbilitySlot.Q;
                            orderKind = LaneOrderKind.CastQ;
                        }
                        else if (phaseMod == 15)
                        {
                            slotToTry = LaneAbilitySlot.W;
                            orderKind = LaneOrderKind.CastW;
                        }
                        else if (phaseMod == 30)
                        {
                            slotToTry = LaneAbilitySlot.E;
                            orderKind = LaneOrderKind.CastE;
                        }

                        HeroSnap preCast = slotToTry != LaneAbilitySlot.None ? GetHero(mm.Snapshot(), LaneTeam.Blue) : null;
                        float preCd = preCast == null ? 99f : (slotToTry == LaneAbilitySlot.Q ? preCast.cdQ : (slotToTry == LaneAbilitySlot.W ? preCast.cdW : preCast.cdE));
                        float slotCost = slotToTry != LaneAbilitySlot.None ? mm.ComputeEffectiveAbilityCost(LaneTeam.Blue, slotToTry) : 0f;
                        if (preCast != null && preCast.phase != LanePhase.Dead && preCd <= 0f)
                        {
                            mm.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = orderKind, point = new Vector2(2f, 0f) });
                        }

                        mm.Tick(dt);

                        if (preCast != null && preCast.phase != LanePhase.Dead && preCd <= 0f)
                        {
                            LaneSnapshot postSnap = mm.Snapshot();
                            HeroSnap postCast = GetHero(postSnap, LaneTeam.Blue);
                            float postCd = slotToTry == LaneAbilitySlot.Q ? postCast.cdQ : (slotToTry == LaneAbilitySlot.W ? postCast.cdW : postCast.cdE);
                            bool started = postCd > 0f || postCast.phase == LanePhase.Windup || postCast.mana < preCast.mana - 1f;
                            if (started)
                            {
                                castsOk++;
                            }
                            else if (firstOomSec < 0f && preCast.mana + 0.001f < slotCost)
                            {
                                firstOomSec = s * dt;
                            }
                        }
                    }
                    HeroSnap finalM = GetHero(mm.Snapshot(), LaneTeam.Blue);
                    string oomLabel = firstOomSec < 0f ? "Never OOM" : $"OOM@{firstOomSec:0.0}s";
                    sb.AppendLine($"  [Mana 45s {manaHeroes[h],-12}] {manaLoadouts[p].label,-27}: Casts={castsOk,2} | EndMana={finalM.mana,5:0}/{finalM.maxMana:0} ({finalM.manaRegen,4:0.0}/s, RCR -{finalM.rcrPct * 100f,2:0}%) | {oomLabel}");
                }
            }
            sb.AppendLine();
        }

        private static void RunLevelingOrderAndMatchupSuite(StringBuilder sb)
        {
            sb.AppendLine("--- 4. 12-MATCHUP BOT DUELS (150s), ITEM COMPLETIONS, OOM TIME & LEVELING ORDERS ---");
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
                    float firstAdvB = -1f, firstAdvR = -1f;
                    float lowManaTimeB = 0f, lowManaTimeR = 0f;

                    for (int s = 0; s < steps && !m.IsGameOver; s++)
                    {
                        botB.Tick(m, dt);
                        botR.Tick(m, dt);
                        m.Tick(dt);

                        LaneSnapshot snapStep = m.Snapshot();
                        HeroSnap sbStep = GetHero(snapStep, LaneTeam.Blue);
                        HeroSnap srStep = GetHero(snapStep, LaneTeam.Red);
                        if (firstAdvB < 0f && CountCompletedAdvanced(sbStep.items) > 0) firstAdvB = m.MatchTime;
                        if (firstAdvR < 0f && CountCompletedAdvanced(srStep.items) > 0) firstAdvR = m.MatchTime;
                        if (sbStep.resourceKind == HeroResourceKind.Mana && sbStep.maxMana > 0f && sbStep.mana / sbStep.maxMana < 0.20f) lowManaTimeB += dt;
                        if (srStep.resourceKind == HeroResourceKind.Mana && srStep.maxMana > 0f && srStep.mana / srStep.maxMana < 0.20f) lowManaTimeR += dt;
                    }
                    LaneSnapshot snap = m.Snapshot();
                    HeroSnap b = GetHero(snap, LaneTeam.Blue);
                    HeroSnap r = GetHero(snap, LaneTeam.Red);
                    float bTw = snap.towers[0].alive ? snap.towers[0].hp : 0f;
                    float rTw = snap.towers[1].alive ? snap.towers[1].hp : 0f;
                    string win = snap.gameOver ? ((LaneTeam)snap.winnerTeam).ToString() : "Draw";
                    string b1 = firstAdvB < 0f ? "-" : $"{firstAdvB:0}s";
                    string r1 = firstAdvR < 0f ? "-" : $"{firstAdvR:0}s";
                    sb.AppendLine($"  {heroes[i],-12} vs {heroes[j],-12} | K {b.kills}-{r.kills} | Lvl {b.level}-{r.level} | CS {b.creepScore,2}-{r.creepScore,2} | Adv {CountCompletedAdvanced(b.items)}({b1})-{CountCompletedAdvanced(r.items)}({r1}) | LowMana {lowManaTimeB,4:0.0}s/{lowManaTimeR,4:0.0}s | Tw {bTw,4:0}-{rTw,4:0} | {win}");
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

        private static int CountCompletedAdvanced(int[] items)
        {
            if (items == null) return 0;
            int n = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (LaneItems.TryGet(items[i], out LaneItemDef d) && d.IsAdvancedOrCapstone) n++;
            }
            return n;
        }

        private static void RunStressScenarios(StringBuilder sb)
        {
            sb.AppendLine("--- 5. TARGETED & MULTI-PASSIVE STRESS SCENARIOS ---");

            // S1 & S2: Miravel Negative Regen + Permanent HealPower Growth + R * HealPower * Q Interaction
            int[] charms = { 0, 2, 4, 6 };
            for (int i = 0; i < charms.Length; i++)
            {
                var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.LaserShooter, new LaneTuning { startingGold = 10000, waveFirstSpawnDelay = 999f, ultMinLevel = 1, passiveGoldPerSecond = 0f });
                for (int k = 0; k < charms[i]; k++) m.TryBuyItem(LaneTeam.Blue, LaneItemIds.KindredCenser, out _);
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
                for (int hpIt = 0; hpIt < 6; hpIt++) ml.TryBuyItem(LaneTeam.Red, LaneItemIds.ColossusPauldron, out _);
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
            int[] strCounts = { 0, 2, 4, 6 };
            for (int i = 0; i < strCounts.Length; i++)
            {
                var mo = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, new LaneTuning { startingGold = 3000, waveFirstSpawnDelay = 999f, ultMinLevel = 1, passiveGoldPerSecond = 0f });
                for (int k = 0; k < strCounts[i]; k++) mo.TryBuyItem(LaneTeam.Blue, LaneItemIds.Titanstone, out _);
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

            // S9: Multi-Passive Capstone Stack Stress (Giant's Heart + Draconic Blade + Chalice of Life + Banana Blade)
            var mp = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, new LaneTuning { startingGold = 12000, waveFirstSpawnDelay = 999f, ultMinLevel = 1, passiveGoldPerSecond = 0f });
            mp.TryBuyItem(LaneTeam.Blue, LaneItemIds.GiantsHeart, out _);
            mp.TryBuyItem(LaneTeam.Blue, LaneItemIds.DraconicBlade, out _);
            mp.TryBuyItem(LaneTeam.Blue, LaneItemIds.ChaliceOfLife, out _);
            mp.TryBuyItem(LaneTeam.Blue, LaneItemIds.BananaBlade, out _);
            mp.TryBuyItem(LaneTeam.Red, LaneItemIds.GiantsHeart, out _);
            mp.TryBuyItem(LaneTeam.Red, LaneItemIds.DraconicBlade, out _);
            PlacePair(mp, new Vector2(-1.2f, 0f), new Vector2(0.2f, 0f));
            mp.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(1.1f, 0.35f), maxHp: 3000f);
            mp.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(mp, 4.0f);
            HeroSnap sbMp = GetHero(mp.Snapshot(), LaneTeam.Blue);
            HeroSnap srMp = GetHero(mp.Snapshot(), LaneTeam.Red);
            sb.AppendLine($"  [Multi-Passive Capstone Stress] Swordsman(GH+DB+CoL+BB) AD={sbMp.attackDamage:0} MaxHP={sbMp.maxHp:0} | vs Ogre(GH+DB) Heat={srMp.heatStacks} Scales={srMp.adaptiveScalesStacks} Deferred={srMp.deferredDamageTotal:0.0}");
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
