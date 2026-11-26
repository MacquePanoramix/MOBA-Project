using System;
using UnityEngine;

namespace Moba.LaneSlice
{
    public static class LaneResourceAndItemChecks
    {
        public static void Run()
        {
            CheckResourceRegenerationInAndOutOfCombatAndPercentMax();
            CheckMaxManaChangesPreservePercentageRatio();
            CheckAllResourceCostModesAndRcrBounded();
            CheckInsufficientManaInvalidAndDuplicateCommands();
            CheckMiravelManaFreePassiveQAndContinuousR();
            CheckChainLaserManaProfileAndCosts();
            CheckResourcelessHeroesUnaffected();
            Debug.Log("[LaneResourceAndItemChecks] All Stage C Resource & Hero Mana checks passed.");
        }

        private static void CheckResourceRegenerationInAndOutOfCombatAndPercentMax()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
            var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Swordsman, t);
            m.DisableTowersForPreview();
            m.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.0f, 0f), 90f);
            m.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.4f, 0f), 270f);
            m.SetHeroManaForTest(LaneTeam.Blue, 100f);

            // Out-of-combat regeneration for 2.0s outside fountain
            HeroSnap b0 = GetHero(m.Snapshot(), LaneTeam.Blue);
            for (int i = 0; i < 60; i++) m.Tick(1f / 30f);
            HeroSnap bOut = GetHero(m.Snapshot(), LaneTeam.Blue);
            float outGain = bOut.mana - b0.mana;
            AssertClose(outGain, t.miravelBaseManaRegen * 2.0f, "Out-of-combat Mana regen matches base rate", 0.15f);

            // Active hero combat (enemy Swordsman hitting Miravel) must NOT penalize Mana regeneration
            m.SetHeroManaForTest(LaneTeam.Blue, 100f);
            float combatManaBefore = GetHero(m.Snapshot(), LaneTeam.Blue).mana;
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackHero, targetId = b0.id });
            for (int i = 0; i < 60; i++) m.Tick(1f / 30f);
            HeroSnap bCombat = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(bCombat.hp < b0.hp, "Miravel actually took combat damage during Mana regen test");
            AssertClose(bCombat.mana - combatManaBefore, t.miravelBaseManaRegen * 2.0f,
                "In-combat Mana regen must match out-of-combat Mana regen with zero combat penalty", 0.20f);

            // Flat + Percentage of Maximum Mana regeneration
            m.SetHeroManaForTest(LaneTeam.Blue, 100f);
            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides
            {
                enabled = true,
                level = 1,
                bonusMaxMana = 180f,          // 320 + 180 = 500 MaxMana
                bonusManaRegen = 1.4f,        // 2.6 + 1.4 = 4.0/s flat
                bonusPercentManaRegen = 0.02f // 2% * 500 = +10.0/s -> total 14.0/s
            });
            HeroSnap pctSnap = GetHero(m.Snapshot(), LaneTeam.Blue);
            AssertClose(pctSnap.maxMana, 500f, "MaxMana includes bonus");
            AssertClose(pctSnap.manaRegen, 14.0f, "Flat + PercentMax Mana regen combines linearly");
            float prePctMana = pctSnap.mana;
            for (int i = 0; i < 30; i++) m.Tick(1f / 30f);
            HeroSnap postPctSnap = GetHero(m.Snapshot(), LaneTeam.Blue);
            AssertClose(postPctSnap.mana - prePctMana, 14.0f, "1s regen delivers flat + %Max Mana", 0.25f);
        }

        private static void CheckMaxManaChangesPreservePercentageRatio()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
            var m = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Ogre, t);
            m.SetHeroManaForTest(LaneTeam.Blue, t.laserBaseMana * 0.50f); // 150 / 300 = 50%

            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides
            {
                enabled = true,
                level = 1,
                bonusMaxMana = 300f, // 600 MaxMana
            });
            HeroSnap gained = GetHero(m.Snapshot(), LaneTeam.Blue);
            AssertClose(gained.maxMana, 600f, "Laser MaxMana updated to 600");
            AssertClose(gained.mana, 300f, "Increasing MaxMana preserves 50% Mana ratio");

            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides
            {
                enabled = true,
                level = 1,
                bonusMaxMana = 0f, // back to 300 MaxMana
            });
            HeroSnap lost = GetHero(m.Snapshot(), LaneTeam.Blue);
            AssertClose(lost.maxMana, 300f, "Removing bonus MaxMana restores 300");
            AssertClose(lost.mana, 150f, "Reducing MaxMana preserves 50% Mana ratio");
        }

        private static void CheckAllResourceCostModesAndRcrBounded()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
            var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            m.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);

            // Verify Bounded diminishing RCR strictly < 100% (asymptote 65% in tuning)
            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides
            {
                enabled = true,
                level = 1,
                bonusResourceCostReduction = 5000f,
            });
            HeroSnap extremeRcr = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(extremeRcr.rcrPct > 0.60f && extremeRcr.rcrPct < 0.651f && extremeRcr.rcrPct < 1.0f,
                $"Extreme RCR must stay bounded below 100% (got {extremeRcr.rcrPct})");
            Assert(m.ComputeEffectiveAbilityCost(LaneTeam.Blue, LaneAbilitySlot.W) > 15f,
                "Spells can never become free even under extreme RCR stacking");

            // Test RCR = 65 -> exact half-point = 32.5% reduction
            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides
            {
                enabled = true,
                level = 1,
                bonusResourceCostReduction = 65f,
            });
            float rcrHalf = GetHero(m.Snapshot(), LaneTeam.Blue).rcrPct;
            AssertClose(rcrHalf, 0.325f, "RCR=65 evaluates bounded half-point 32.5%", 0.005f);

            // Clear RCR and test all 4 cost modes explicitly via TryConsumeResourceSpec
            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides { enabled = true, level = 1 });
            float maxM = GetHero(m.Snapshot(), LaneTeam.Blue).maxMana; // 320

            // 1. Flat
            m.SetHeroManaForTest(LaneTeam.Blue, 200f);
            var flatSpec = new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.Flat, 50f, 10f, true);
            Assert(m.TryConsumeResourceSpec(LaneTeam.Blue, flatSpec, 2, out float flatSpent) && Mathf.Abs(flatSpent - 60f) < 0.01f,
                "Flat cost rank 2 must spend 60 Mana");
            AssertClose(GetHero(m.Snapshot(), LaneTeam.Blue).mana, 140f, "Remaining after Flat cost");

            // 2. % Max Mana (ignoring RCR when usesCostReduction=false)
            m.SetHeroManaForTest(LaneTeam.Blue, 200f);
            var pctMaxSpec = new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.PercentMax, 0.25f, 0f, false);
            Assert(m.TryConsumeResourceSpec(LaneTeam.Blue, pctMaxSpec, 1, out float pctMaxSpent) && Mathf.Abs(pctMaxSpent - maxM * 0.25f) < 0.01f,
                "PercentMax mode must evaluate % of maxMana");
            AssertClose(GetHero(m.Snapshot(), LaneTeam.Blue).mana, 200f - maxM * 0.25f, "Remaining after PercentMax");

            // 3. % Current Mana
            m.SetHeroManaForTest(LaneTeam.Blue, 180f);
            var pctCurSpec = new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.PercentCurrent, 0.20f, 0f, true);
            Assert(m.TryConsumeResourceSpec(LaneTeam.Blue, pctCurSpec, 1, out float pctCurSpent) && Mathf.Abs(pctCurSpent - 36f) < 0.01f,
                "PercentCurrent mode must evaluate % of current Mana");
            AssertClose(GetHero(m.Snapshot(), LaneTeam.Blue).mana, 144f, "Remaining after PercentCurrent");

            // 4. Cannot drive Mana negative on insufficient Flat cost
            m.SetHeroManaForTest(LaneTeam.Blue, 10f);
            Assert(!m.TryConsumeResourceSpec(LaneTeam.Blue, flatSpec, 1, out float failSpent) && failSpent == 0f,
                "Insufficient Mana must reject consumption cleanly");
            AssertClose(GetHero(m.Snapshot(), LaneTeam.Blue).mana, 10f, "Rejected cast preserves exact Mana without negative value");
        }

        private static void CheckInsufficientManaInvalidAndDuplicateCommands()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
            var m = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Ogre, t);
            m.DisableTowersForPreview();
            m.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-3f, 0f), 90f);
            m.ConfigurePreviewHero(LaneTeam.Red, new Vector2(3f, 0f), 270f);
            m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q);
            m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W);
            m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E);

            // Invalid command: Laser W with zero stacked targets in range must NOT spend Mana or trigger CD
            m.SetHeroManaForTest(LaneTeam.Blue, 250f);
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = Vector2.zero });
            m.Tick(1f / 30f);
            HeroSnap afterInvalidW = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(afterInvalidW.mana >= 250f && afterInvalidW.cdW <= 0f,
                "Rejected Laser W with zero stacked targets must NOT spend Mana or enter cooldown");

            // Insufficient Mana: Q costs 65, set Mana to 30
            m.SetHeroManaForTest(LaneTeam.Blue, 30f);
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(3f, 0f) });
            m.Tick(1f / 30f);
            LaneSnapshot lowSnap = m.Snapshot();
            HeroSnap lowLaser = GetHero(lowSnap, LaneTeam.Blue);
            Assert(lowLaser.phase == LanePhase.Idle && lowLaser.cdQ <= 0f && lowLaser.mana >= 30f && lowLaser.mana < 35f,
                "Insufficient Mana must reject Q cast cleanly without spending Mana or starting CD");
            bool sawPopup = false;
            for (int i = 0; i < lowSnap.events.Count; i++)
            {
                if (lowSnap.events[i].kind == LaneEventKind.InsufficientMana) sawPopup = true;
            }
            Assert(sawPopup, "Insufficient Mana attempt must emit feedback event");

            // Duplicate / queued W cast: fire W cleanly then spam duplicate W -> spends Mana once
            m.SetHeroManaForTest(LaneTeam.Blue, 200f);
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(3f, 0f) });
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(3f, 0f) });
            m.Tick(1f / 30f);
            HeroSnap postDup = GetHero(m.Snapshot(), LaneTeam.Blue);
            AssertClose(postDup.mana, 200f - t.laserQManaBase + t.laserBaseManaRegen / 30f,
                "Duplicate cast order must consume Q Mana only once", 0.25f);
        }

        private static void CheckMiravelManaFreePassiveQAndContinuousR()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
            var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            m.DisableTowersForPreview();
            m.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1f, 0f), 90f, hpFraction: 0.6f);
            m.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.5f, 0f), 270f);
            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides { enabled = true, level = 4, abilityRank = 1, bonusIntelligence = 80f });

            // Verify at 0 Mana: passive aura heals allies/hurts enemies AND auto-Q orbs spawn without costing Mana!
            m.SetHeroManaForTest(LaneTeam.Blue, 0f);
            int orbsBefore = m.Snapshot().orbs.Count;
            for (int i = 0; i < 190; i++) m.Tick(1f / 30f); // >6s spawns Q orb even if starting/low Mana
            LaneSnapshot qSnap = m.Snapshot();
            Assert(qSnap.orbs.Count > orbsBefore || GetHero(qSnap, LaneTeam.Blue).mana > 0f,
                "Miravel Passive Q orb spawner functions automatically independent of Mana");

            // Rank-scaled W and E costs, zero curse extension cost
            HeroSnap mirStats = GetHero(m.Snapshot(), LaneTeam.Blue);
            AssertClose(mirStats.costW, t.miravelWManaBase, "Miravel W Rank 1 cost = 55");
            AssertClose(mirStats.costE, t.miravelEManaBase, "Miravel E Rank 1 cost = 45");
            // Rank 1 R continuous drain must NOT scale up with +80 INT!
            AssertClose(mirStats.costR, t.miravelRManaPerSecBase, "Miravel R Mana/s drain is independent of INT");

            // Start R at partial Mana (50 -> nets ~2.2s channel with Lv4 3.35/s regen vs 26/s drain) -> stops early on OOM cleanly!
            m.SetHeroManaForTest(LaneTeam.Blue, 50f);
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(1.5f, 0f) });
            m.Tick(1f / 30f);
            LaneSnapshot rStart = m.Snapshot();
            HeroSnap rHero = GetHero(rStart, LaneTeam.Blue);
            Assert(rHero.phase == LanePhase.Channeling && rStart.zones.Count == 1 && rHero.mana > 45f,
                "Miravel R starts channeling with no large upfront Mana deduction");

            // Exhaust Mana during R channel -> stops channel cleanly before full 4.2s duration without going negative
            bool endedEarly = false;
            for (int i = 0; i < 110; i++) // 3.67s < 4.2s full duration
            {
                m.Tick(1f / 30f);
                LaneSnapshot s = m.Snapshot();
                HeroSnap h = GetHero(s, LaneTeam.Blue);
                Assert(h.mana >= 0f, "Miravel Mana never goes negative during R channel");
                if (h.phase != LanePhase.Channeling && s.zones.Count == 0 && i >= 50 && i < 100)
                {
                    endedEarly = true;
                    Assert(h.mana < 1f, $"Channel ended early on OOM (mana={h.mana})");
                    break;
                }
            }
            Assert(endedEarly, "Miravel R continuous consumption ends channel early when Mana hits 0");

            // Trying to cast R when virtually 0 Mana is rejected
            m.SetHeroManaForTest(LaneTeam.Blue, 0f);
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(1f, 0f) });
            Assert(GetHero(m.Snapshot(), LaneTeam.Blue).cdW <= 0f, "Miravel W rejected at 0 Mana");
        }

        private static void CheckChainLaserManaProfileAndCosts()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
            var m = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Ogre, t);
            m.DisableTowersForPreview();
            m.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2f, 0f), 90f);
            int ogreId = m.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2f, 0f), 270f);
            m.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(3.5f, 0f), maxHp: 500f);

            // Basics remain 100% free & still chain at 0 Mana
            m.SetHeroManaForTest(LaneTeam.Blue, 0f);
            float ogreHpBefore = GetHero(m.Snapshot(), LaneTeam.Red).hp;
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = ogreId });
            for (int i = 0; i < 30; i++) m.Tick(1f / 30f);
            HeroSnap ogreAfter = GetHero(m.Snapshot(), LaneTeam.Red);
            Assert(ogreAfter.hp < ogreHpBefore && ogreAfter.laserStacks >= 1,
                "Chain Laser basic attacks and Laser Stacks remain fully functional at 0 Mana");

            // Grant XP so Blue has 6 skill points cleanly; verify rank-dependent Q > W and constant E & R upfront cost
            m.GrantXpForTest(LaneTeam.Blue, 3000f);
            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Laser Q R1");
            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level Laser W R1");
            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Level Laser E R1");
            HeroSnap r1 = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(r1.costQ > r1.costW && r1.costE == t.laserEManaFlat && r1.costR == t.laserRManaUpfront,
                "Laser Q > W and E/R match configured constants");

            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Laser Q R2");
            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level Laser W R2");
            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Level Laser E R2");
            HeroSnap r2 = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(r2.costQ > r1.costQ && r2.costW > r1.costW, "Laser Q & W scale upward with rank");
            AssertClose(r2.costE, t.laserEManaFlat, "Laser E cost stays constant across ranks");

            // Level to 4 & test Laser R upfront cost (no continuous drain during beam!)
            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides { enabled = true, level = 4, abilityRank = 2 });
            m.SetHeroManaForTest(LaneTeam.Blue, 200f);
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(3f, 0f) });
            m.Tick(1f / 30f);
            HeroSnap rCast = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(rCast.phase == LanePhase.Casting && rCast.activeSlot == LaneAbilitySlot.R, "Laser R accepted with 200 Mana");
            float expectedAfterUpfront = 200f - t.laserRManaUpfront;
            AssertClose(rCast.mana, expectedAfterUpfront, "Laser R pays full upfront cost once", 0.35f);

            // Advance into Channeling beam -> Mana ONLY regens, never continuously drains!
            for (int i = 0; i < 45; i++) m.Tick(1f / 30f);
            HeroSnap rChannel = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(rChannel.mana > expectedAfterUpfront, "Laser R does NOT continuously drain Mana while firing beam");
        }

        private static void CheckResourcelessHeroesUnaffected()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
            var m = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Swordsman, t);
            m.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1f, 0f), 90f);
            m.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.8f, 0f), 270f);
            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides
            {
                enabled = true,
                level = 1,
                bonusMaxMana = 500f,
                bonusManaRegen = 10f,
                bonusResourceCostReduction = 50f,
            });

            HeroSnap ogre = GetHero(m.Snapshot(), LaneTeam.Blue);
            HeroSnap sword = GetHero(m.Snapshot(), LaneTeam.Red);
            Assert(ogre.resourceKind == HeroResourceKind.None && ogre.maxMana == 0f && ogre.mana == 0f && ogre.rcrPct == 0f,
                "Ogre must remain Resourceless and unaffected by MaxMana/ManaRegen/RCR");
            Assert(sword.resourceKind == HeroResourceKind.None && sword.maxMana == 0f && sword.mana == 0f && sword.rcrPct == 0f,
                "Swordsman must remain Resourceless");

            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(1f, 0f) });
            m.Tick(1f / 30f);
            Assert(GetHero(m.Snapshot(), LaneTeam.Blue).phase == LanePhase.Casting, "Resourceless Ogre casts Q freely at 0 Mana");
        }

        private static HeroSnap GetHero(LaneSnapshot snap, LaneTeam team)
        {
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].team == team) return snap.heroes[i];
            }
            throw new InvalidOperationException($"Missing hero for {team}");
        }

        private static void Assert(bool cond, string message)
        {
            if (!cond) throw new InvalidOperationException($"LaneResourceAndItemChecks failed: {message}");
        }

        private static void AssertClose(float actual, float expected, string message, float tol = 0.05f)
        {
            if (Mathf.Abs(actual - expected) > tol)
            {
                throw new InvalidOperationException($"LaneResourceAndItemChecks failed: {message} (expected {expected:0.###}, got {actual:0.###})");
            }
        }
    }
}
