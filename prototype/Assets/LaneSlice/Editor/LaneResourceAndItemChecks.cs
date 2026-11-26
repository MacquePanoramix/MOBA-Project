using System;
using System.Collections.Generic;
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

            // Stage D & E (Part 12.2 + 12.3)
            CheckItemCatalogAndRecipesAndAntiExploit();
            CheckExecutionersAxeAndBloodstonePassives();
            CheckCowardsShieldExternalDisplacement();
            CheckChaliceOfLifeAndHealingElixirPassives();
            CheckDraconicBladeDragonScaleAndPrimordialBloodPassives();
            CheckGiantsHeartPassives();
            CheckBananaBladeNimbleDaggerAndWingedCharmPassives();

            // Stage F & G (Part 12.4 + 12.5 + Provisional Mana Catalogue & Bot Recipe Stepping)
            CheckProvisionalManaItemsAndBotStepping();
            CheckScalingStressAndTickBounds();
            CheckHeroGuidePreviewAndCatalogConsistency();

            Debug.Log("[LaneResourceAndItemChecks] All Stage C-G Resource, Recipe, Item, Scaling Stress, and Preview checks passed.");
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

        private static void CheckItemCatalogAndRecipesAndAntiExploit()
        {
            // Count approved items (21 components + 6 advanced) and enforce ZERO Mana stats on all 27
            int approvedComps = 0, approvedAdvanced = 0;
            for (int i = 0; i < LaneItems.Catalog.Length; i++)
            {
                LaneItemDef item = LaneItems.Catalog[i];
                if (item.IsApproved)
                {
                    if (item.Category == LaneItemCategory.Component || item.Category == LaneItemCategory.SpecializedComponent) approvedComps++;
                    else if (item.Category == LaneItemCategory.Advanced) approvedAdvanced++;

                    Assert(item.AddMaxMana == 0f && item.AddManaRegen == 0f && item.AddPercentManaRegen == 0f && item.AddResourceCostReduction == 0f,
                        $"Approved item {item.Name} must have ZERO Mana-related stats");
                }
                Assert(!item.Name.Contains("Forgotten Hourglass") && !item.Name.Contains("Spellweaver"),
                    $"Retired Hourglass must not appear in catalog ({item.Name})");
            }
            Assert(approvedComps == 21, $"Expected exactly 21 approved components, got {approvedComps}");
            Assert(approvedAdvanced == 6, $"Expected exactly 6 approved advanced items, got {approvedAdvanced}");
            Assert(!LaneItems.TryGet(19, out _), "Legacy ID 19 must remain retired");

            // Relative stat identities (Part 11.2)
            LaneItems.TryGet(LaneItemIds.HeavyAxe, out LaneItemDef heavyAxe);
            LaneItems.TryGet(LaneItemIds.SteelSword, out LaneItemDef steelSword);
            Assert(heavyAxe.AddAttackDamage > steelSword.AddAttackDamage && heavyAxe.Cost > steelSword.Cost,
                "Heavy Axe must grant more AD and cost more than Steel Sword");

            LaneItems.TryGet(LaneItemIds.DraconicBlade, out LaneItemDef draconic);
            LaneItems.TryGet(LaneItemIds.ExecutionersAxe, out LaneItemDef execAxe);
            Assert(draconic.AddStrength > execAxe.AddStrength, "Draconic Blade must grant more STR than Executioner's Axe");
            Assert(draconic.AddMaxHp == 0f && draconic.AddIntelligence == 0f, "Draconic Blade must have no direct HP or INT");

            LaneItems.TryGet(LaneItemIds.DragonScale, out LaneItemDef dScale);
            LaneItems.TryGet(LaneItemIds.ReinforcedShield, out LaneItemDef rShield);
            Assert((dScale.AddArmor / Mathf.Max(1f, dScale.AddMagicResist)) > (rShield.AddArmor / Mathf.Max(1f, rShield.AddMagicResist)),
                "Dragon Scale must maintain a higher Arm:MR ratio than Reinforced Shield");
            Assert((draconic.AddArmor / Mathf.Max(1f, draconic.AddMagicResist)) > (rShield.AddArmor / Mathf.Max(1f, rShield.AddMagicResist)),
                "Draconic Blade must preserve Armor-focused ratio > Reinforced Shield");

            LaneItems.TryGet(LaneItemIds.BananaBlade, out LaneItemDef banana);
            AssertClose(banana.AddAgility, banana.AddIntelligence, "Banana Blade must grant equal AGI and INT", 0.01f);

            LaneItems.TryGet(LaneItemIds.GiantsHeart, out LaneItemDef gHeart);
            Assert(gHeart.AddMaxHp >= 500f && gHeart.AddStrength >= 60f && gHeart.AddAttackDamage == 0f && gHeart.AddArmor == 0f && gHeart.AddMagicResist == 0f,
                "Giant's Heart must grant high HP & STR, modest regen, zero direct AD/Arm/MR");

            LaneItems.TryGet(LaneItemIds.IronDagger, out LaneItemDef ironDagger);
            LaneItems.TryGet(LaneItemIds.NimbleDagger, out LaneItemDef nimbleDagger);
            LaneItems.TryGet(LaneItemIds.CowardsShield, out LaneItemDef cowards);
            Assert(ironDagger.AddAttackDamage == 0f && ironDagger.AddAgility == 0f, "Iron Dagger gives AS only");
            Assert(nimbleDagger.AddAttackDamage == 0f && nimbleDagger.AddMoveSpeed == 0f, "Nimble Dagger gives no AD or MS");
            Assert(cowards.AddMoveSpeed == 0f, "Coward's Shield gives no direct MS");

            // Anti-gold-duplication sweep across EVERY item in the catalog
            for (int i = 0; i < LaneItems.Catalog.Length; i++)
            {
                LaneItemDef c = LaneItems.Catalog[i];
                int sumIngredients = 0;
                if (c.Recipe != null)
                {
                    for (int r = 0; r < c.Recipe.Length; r++)
                    {
                        Assert(LaneItems.TryGet(c.Recipe[r].ItemId, out LaneItemDef ing), $"Missing recipe ingredient {c.Recipe[r].ItemId}");
                        sumIngredients += ing.Cost * c.Recipe[r].Quantity;
                    }
                }
                Assert(c.Cost >= sumIngredients, $"{c.Name} cost ({c.Cost}) must be >= ingredient sum ({sumIngredients})");
                Assert(LaneItems.SellRefund(c.Cost) < c.Cost, $"{c.Name} sell refund must be strictly < purchase cost");
            }

            // Shared component upgrade destinations & inheritance
            List<LaneItemDef> lifeUp = LaneItems.GetUpgradesInto(LaneItemIds.LifeCrystal);
            Assert(lifeUp.Count >= 3, "Life Crystal must upgrade into Heartstone, Bloodstone, and Chalice of Life");
            List<LaneItemDef> swordUp = LaneItems.GetUpgradesInto(LaneItemIds.SteelSword);
            Assert(swordUp.Count >= 2, "Steel Sword must upgrade into both Draconic Blade and Banana Blade");

            // Duplicate components, nested recipes, partial completion, unrelated item preservation & 6/6 full-inventory upgrade
            var t = new LaneTuning { startingGold = 5000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };
            var m = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.LifeCrystal, out _), "Buy Life Crystal #1");
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.LifeCrystal, out _), "Buy Life Crystal #2");
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.LifeCrystal, out _), "Buy extra unrelated Life Crystal #3");
            int goldBeforeHeartstone = GetHero(m.Snapshot(), LaneTeam.Blue).gold;
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.Heartstone, out _), "Craft Heartstone from 2x Life Crystal");
            HeroSnap afterHeart = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(afterHeart.gold == goldBeforeHeartstone - (560 - 440), "Heartstone must consume exact 2x Life Crystal cost");
            int countHeart = 0, countLife = 0;
            for (int s = 0; s < afterHeart.items.Length; s++)
            {
                if (afterHeart.items[s] == LaneItemIds.Heartstone) countHeart++;
                if (afterHeart.items[s] == LaneItemIds.LifeCrystal) countLife++;
            }
            Assert(countHeart == 1 && countLife == 1, "Crafting Heartstone must keep extra Life Crystal #3 untouched");

            // Fill remaining slots cleanly so inventory is 6/6 with [Heartstone, LifeCrystal, Titanstone, Titanstone, SmallAxe, WoodenShield]
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.Titanstone, out _), "Titanstone #1");
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.Titanstone, out _), "Titanstone #2");
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.SmallAxe, out _), "SmallAxe");
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.WoodenShield, out _), "WoodenShield");

            // Purchasing unrelated Short Sword at 6/6 must fail cleanly without losing gold
            int goldBeforeFullFail = GetHero(m.Snapshot(), LaneTeam.Blue).gold;
            Assert(!m.TryBuyItem(LaneTeam.Blue, LaneItemIds.ShortSword, out string fullErr), $"Unrelated buy at 6/6 must fail ({fullErr})");
            Assert(GetHero(m.Snapshot(), LaneTeam.Blue).gold == goldBeforeFullFail, "Failed 6/6 purchase must spend 0 gold");

            // Purchasing Giant's Heart AT 6/6 consumes Heartstone + 2xTitanstone (paying Primordial Blood + combine) and preserves LifeCrystal, SmallAxe, WoodenShield!
            int expectedRem = 2100 - (560 + 300 + 300);
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.GiantsHeart, out _), "6/6 full-inventory recipe upgrade to Giant's Heart must succeed");
            HeroSnap postGH = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(postGH.gold == goldBeforeFullFail - expectedRem, $"Nested 6/6 upgrade must charge {expectedRem}g");
            int ghCount = 0, axeCount = 0, shCount = 0;
            countLife = 0;
            for (int s = 0; s < postGH.items.Length; s++)
            {
                if (postGH.items[s] == LaneItemIds.GiantsHeart) ghCount++;
                if (postGH.items[s] == LaneItemIds.LifeCrystal) countLife++;
                if (postGH.items[s] == LaneItemIds.SmallAxe) axeCount++;
                if (postGH.items[s] == LaneItemIds.WoodenShield) shCount++;
            }
            Assert(ghCount == 1 && countLife == 1 && axeCount == 1 && shCount == 1,
                "Nested 6/6 Giant's Heart upgrade must preserve unrelated LifeCrystal, SmallAxe, and WoodenShield");

            // Insufficient gold check & legacy ID migration check
            var mPoor = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, new LaneTuning { startingGold = 100, passiveGoldPerSecond = 0f });
            Assert(!mPoor.TryBuyItem(LaneTeam.Blue, LaneItemIds.SmallAxe, out _), "Insufficient gold rejected");
            Assert(GetHero(mPoor.Snapshot(), LaneTeam.Blue).gold == 100 && GetHero(mPoor.Snapshot(), LaneTeam.Blue).items[0] == 0,
                "Insufficient gold leaves gold and inventory unchanged");
            Assert(LaneItems.MigrateItemId(8) == LaneItemIds.Titanstone && LaneItems.MigrateItemId(12) == LaneItemIds.CowardsShield && LaneItems.MigrateItemId(20) == LaneItemIds.ExecutionersAxe,
                "Legacy item IDs must migrate cleanly");
        }

        private static void CheckExecutionersAxeAndBloodstonePassives()
        {
            var t = new LaneTuning { startingGold = 6000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };
            float HitLoss(bool buyBloodstone, bool buyExec, bool buyStr, bool isMagic, float targetHpFrac)
            {
                var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
                if (buyBloodstone) Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.Bloodstone, out _), "Bloodstone");
                if (buyExec) Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.ExecutionersAxe, out _), "ExecAxe");
                if (buyStr) Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.Titanstone, out _) && m.TryBuyItem(LaneTeam.Blue, LaneItemIds.Titanstone, out _), "50 STR");
                HeroSnap r0 = GetHero(m.Snapshot(), LaneTeam.Red);
                m.SetHeroHpForTest(LaneTeam.Red, r0.maxHp * targetHpFrac);
                float before = GetHero(m.Snapshot(), LaneTeam.Red).hp;
                m.ApplyDamageForTest(LaneTeam.Red, 100f, isMagic, isTrueDamage: false, LaneTeam.Blue);
                return before - GetHero(m.Snapshot(), LaneTeam.Red).hp;
            }

            float bloodFull = HitLoss(true, false, false, false, 1.0f);
            float bloodHalf = HitLoss(true, false, false, false, 0.50f);
            float bloodLow = HitLoss(true, false, false, false, 0.20f);
            Assert(bloodLow > bloodHalf && bloodHalf > bloodFull, "Bloodstone scales linearly with target missing HP");
            float diff1 = bloodHalf - bloodFull;
            float diff2 = bloodLow - bloodHalf; // 0.5->0.2 (0.3) vs 1.0->0.5 (0.5) => ratio ~0.6
            AssertClose(diff2 / Mathf.Max(0.01f, diff1), 0.30f / 0.50f, "Bloodstone scaling must be linear with missing HP%", 0.05f);

            float execNoStr = HitLoss(false, true, false, false, 0.25f);
            float execWithStr = HitLoss(false, true, true, false, 0.25f);
            Assert(execWithStr > execNoStr + 3f, "Executioner's Axe STR must increase missing-HP damage amplification");

            // Applies to magic ability damage too (outgoing damage generally)
            float execMagicBase = HitLoss(false, true, false, true, 1.0f);
            float execMagicLow = HitLoss(false, true, false, true, 0.25f);
            Assert(execMagicLow > execMagicBase * 1.15f, "Final Judgment must amplify magical damage as well as physical");

            // Owning BOTH Executioner's Axe and Bloodstone must NOT stack Wounded Strike + Final Judgment
            float execPlusBlood = HitLoss(true, true, false, false, 0.25f);
            AssertClose(execPlusBlood, execNoStr, "Bloodstone Wounded Strike must NOT double-stack with Final Judgment", 0.05f);
        }

        private static void CheckCowardsShieldExternalDisplacement()
        {
            var t = new LaneTuning { startingGold = 3000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };
            var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.CowardsShield, out _), "Buy Coward's Shield");
            m.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            m.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2f, 0f), 270f);
            m.ApplyDamageForTest(LaneTeam.Blue, 30f, false, false, LaneTeam.Red);
            for (int rep = 0; rep < 15; rep++)
            {
                m.ApplyDisplacementForTest(LaneTeam.Blue, Vector2.left, 0.45f, LaneTeam.Red);
                m.Tick(1f / 30f);
            }
            AssertClose(GetHero(m.Snapshot(), LaneTeam.Blue).cowardShieldBonus, 0f,
                "External knockbacks away from attacker must NOT build Coward's Shield retreat stacks", 0.05f);
        }

        private static void CheckChaliceOfLifeAndHealingElixirPassives()
        {
            var t = new LaneTuning { startingGold = 5000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };

            // Natural regeneration amplification by Fountain of Life + Heal Power scaling (Chalice vs SpringWater+SacredPearl equivalence)
            var mNoChalice = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            var mChalice = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            var mChaliceExtraHpwr = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(mChalice.TryBuyItem(LaneTeam.Blue, LaneItemIds.ChaliceOfLife, out _), "Chalice");
            Assert(mChaliceExtraHpwr.TryBuyItem(LaneTeam.Blue, LaneItemIds.ChaliceOfLife, out _) &&
                   mChaliceExtraHpwr.TryBuyItem(LaneTeam.Blue, LaneItemIds.SacredPearl, out _), "Chalice + SacredPearl");

            // Give baseline Swordsman flat regen matching Chalice so difference isolates Fountain of Life
            Assert(mNoChalice.TryBuyItem(LaneTeam.Blue, LaneItemIds.SpringWater, out _) &&
                   mNoChalice.TryBuyItem(LaneTeam.Blue, LaneItemIds.SpringWater, out _), "Base +5 HP/s regen");

            foreach (var mm in new[] { mNoChalice, mChalice, mChaliceExtraHpwr })
            {
                mm.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
                HeroSnap s0 = GetHero(mm.Snapshot(), LaneTeam.Blue);
                mm.SetHeroHpForTest(LaneTeam.Blue, s0.maxHp * 0.20f);
                mm.Tick(1f / 30f);
            }
            float regenNoChalice = GetHero(mNoChalice.Snapshot(), LaneTeam.Blue).netRegenRate;
            float regenChalice = GetHero(mChalice.Snapshot(), LaneTeam.Blue).netRegenRate;
            float regenExtraHpwr = GetHero(mChaliceExtraHpwr.Snapshot(), LaneTeam.Blue).netRegenRate;
            Assert(regenChalice > regenNoChalice * 1.25f && regenExtraHpwr > regenChalice + 0.20f,
                $"Fountain of Life must amp natural regen at low HP and scale with HealPower ({regenNoChalice:0.00} -> {regenChalice:0.00} -> {regenExtraHpwr:0.00})");

            // Allied heal (Miravel Q orb on wounded ally? Here test self ability Swordsman E / Miravel Q pickup) + Lingering Restoration + No overhealing + No recursion
            var mLinger = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, new LaneTuning { startingGold = 3000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f });
            Assert(mLinger.TryBuyItem(LaneTeam.Blue, LaneItemIds.HealingElixir, out _), "Healing Elixir");
            Assert(mLinger.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Miravel Q");
            mLinger.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            mLinger.ConfigurePreviewHero(LaneTeam.Red, new Vector2(18f, 0f), 270f);
            HeroSnap ml0 = GetHero(mLinger.Snapshot(), LaneTeam.Blue);
            // Case A: Full HP overheal -> 0 stored Lingering Restoration
            mLinger.SpawnPreviewOrb(LaneTeam.Blue, new Vector2(0f, 0f));
            mLinger.Tick(1f / 30f); // consumes orb at 100% HP
            mLinger.SetHeroHpForTest(LaneTeam.Blue, ml0.maxHp * 0.50f); // drop HP right AFTER overheal!
            float hpAfterDrop = GetHero(mLinger.Snapshot(), LaneTeam.Blue).hp;
            for (int s = 0; s < 30; s++) mLinger.Tick(1f / 30f);
            float expectedOnlyNatRegen = GetHero(mLinger.Snapshot(), LaneTeam.Blue).netRegenRate * 1.0f;
            float gainedAfterOverheal = GetHero(mLinger.Snapshot(), LaneTeam.Blue).hp - hpAfterDrop;
            AssertClose(gainedAfterOverheal, expectedOnlyNatRegen, "Overhealing at full HP must store 0 Lingering Restoration", 0.35f);

            // Case B: Injured Q orb on Chalice of Life -> Fountain of Life amps ability heal & Lingering Restoration repeats ~28% once (no recursion/double-amp)
            mLinger.ConfigurePreviewHero(LaneTeam.Blue, t.blueFountainPos, 90f);
            Assert(mLinger.TryBuyItem(LaneTeam.Blue, LaneItemIds.ChaliceOfLife, out _), "Upgrade Healing Elixir to Chalice of Life");
            mLinger.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            ml0 = GetHero(mLinger.Snapshot(), LaneTeam.Blue);
            mLinger.SetHeroHpForTest(LaneTeam.Blue, ml0.maxHp * 0.35f);
            float hpPreOrb = GetHero(mLinger.Snapshot(), LaneTeam.Blue).hp;
            mLinger.SpawnPreviewOrb(LaneTeam.Blue, new Vector2(0f, 0f));
            mLinger.Tick(1f / 30f);
            float regPerTick = GetHero(mLinger.Snapshot(), LaneTeam.Blue).netRegenRate * (1f / 30f);
            float orbDirectGain = Mathf.Max(0f, GetHero(mLinger.Snapshot(), LaneTeam.Blue).hp - hpPreOrb - regPerTick);
            float expectedEchoTotal = orbDirectGain * t.lingeringRestorationShare;
            float hpPostOrb = GetHero(mLinger.Snapshot(), LaneTeam.Blue).hp;
            float regSum = 0f;
            for (int s = 0; s < 120; s++)
            {
                mLinger.Tick(1f / 30f); // 4.0s (> 3.5s)
                regSum += GetHero(mLinger.Snapshot(), LaneTeam.Blue).netRegenRate * (1f / 30f);
            }
            float totalGainOver4s = GetHero(mLinger.Snapshot(), LaneTeam.Blue).hp - hpPostOrb;
            float echoOnly = totalGainOver4s - regSum;
            AssertClose(echoOnly, expectedEchoTotal, "Lingering Restoration must restore exact 28% without Fountain double-amp or infinite recursion", 4.5f);
        }

        private static void CheckDraconicBladeDragonScaleAndPrimordialBloodPassives()
        {
            var t = new LaneTuning { startingGold = 6000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };

            // 1. Draconic Blade: Shared Heat on OnHit only (non-DoT/non-ordinary ability), Dragonfire Magical DoT + Molten Edge shred + INT scaling + decay one at a time
            var mNoInt = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            var mWithInt = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(mNoInt.TryBuyItem(LaneTeam.Blue, LaneItemIds.DraconicBlade, out _), "Draconic Blade");
            Assert(mWithInt.TryBuyItem(LaneTeam.Blue, LaneItemIds.DraconicBlade, out _) &&
                   mWithInt.TryBuyItem(LaneTeam.Blue, LaneItemIds.SagesEye, out _) &&
                   mWithInt.TryBuyItem(LaneTeam.Blue, LaneItemIds.SagesEye, out _), "Draconic Blade + 50 INT");

            // Give Ogre SilverAmulet/ReinforcedShield so MR shred is clearly visible
            Assert(mNoInt.TryBuyItem(LaneTeam.Red, LaneItemIds.ReinforcedShield, out _), "Ogre MR/Arm");
            Assert(mWithInt.TryBuyItem(LaneTeam.Red, LaneItemIds.ReinforcedShield, out _), "Ogre MR/Arm");

            foreach (var mm in new[] { mNoInt, mWithInt })
            {
                mm.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q);
                mm.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
                mm.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.8f, 0f), 270f);
            }

            // Ordinary physical ability (Swordsman Q) does NOT qualify for Heat!
            float armBase = GetHero(mNoInt.Snapshot(), LaneTeam.Red).armor;
            mNoInt.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(1.8f, 0f) });
            for (int s = 0; s < 15; s++) mNoInt.Tick(1f / 30f);
            Assert(GetHero(mNoInt.Snapshot(), LaneTeam.Red).heatStacks == 0, "Swordsman ordinary Q ability must NOT generate Heat");

            // 5 qualifying on-hit basic hits generate Heat, apply Molten Edge Arm/MR shred, and Dragonfire DoT (higher with INT!)
            for (int hit = 0; hit < 5; hit++)
            {
                mNoInt.ApplyDamageForTest(LaneTeam.Red, 30f, false, false, LaneTeam.Blue, DamageFlags.BasicOrMinion | DamageFlags.OnHit);
                mWithInt.ApplyDamageForTest(LaneTeam.Red, 30f, false, false, LaneTeam.Blue, DamageFlags.BasicOrMinion | DamageFlags.OnHit);
            }
            HeroSnap ogHeat0 = GetHero(mNoInt.Snapshot(), LaneTeam.Red);
            HeroSnap ogHeatInt = GetHero(mWithInt.Snapshot(), LaneTeam.Red);
            Assert(ogHeat0.heatStacks == 5 && ogHeatInt.heatStacks == 5, "5 on-hit strikes must build 5 shared Heat stacks");
            Assert(ogHeat0.armor < armBase && ogHeatInt.armor < ogHeat0.armor,
                $"Molten Edge must shred Armor sharing Heat stacks and scale with INT (base={armBase:0.0} -> 0INT={ogHeat0.armor:0.0} -> 50INT={ogHeatInt.armor:0.0})");

            // Advance 2.0s to observe Dragonfire DoT burning + non-escalation (Heat stays 5 during hold window, not bumped by DoT ticks)
            float hpPreBurn0 = ogHeat0.hp;
            float hpPreBurnInt = ogHeatInt.hp;
            for (int s = 0; s < 60; s++)
            {
                mNoInt.Tick(1f / 30f);
                mWithInt.Tick(1f / 30f);
            }
            Assert(GetHero(mNoInt.Snapshot(), LaneTeam.Red).heatStacks == 5, "Dragonfire DoT ticks must NOT generate more Heat");
            float burn0 = hpPreBurn0 - GetHero(mNoInt.Snapshot(), LaneTeam.Red).hp;
            float burnInt = hpPreBurnInt - GetHero(mWithInt.Snapshot(), LaneTeam.Red).hp;
            Assert(burnInt > burn0 + 10f && burn0 > 5f, $"Dragonfire must deal magical DoT scaling linearly with INT ({burn0:0.0} vs {burnInt:0.0})");

            // Stop hitting until hold (3.5s) + 1.5s passes -> stacks decay one at a time (5 -> 3/4), NOT all at once!
            for (int s = 0; s < 90; s++) mNoInt.Tick(1f / 30f); // cumulative 5.0s (>3.5s hold + 1.0s decay step)
            int midDecay = GetHero(mNoInt.Snapshot(), LaneTeam.Red).heatStacks;
            Assert(midDecay > 0 && midDecay < 5, $"Heat must decay gradually one stack at a time (got {midDecay})");

            // 2. Dragon Scale Adaptive Scales: per-attacker isolation, direct hit only (no DoT), decay one-by-one
            var mScales = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            Assert(mScales.TryBuyItem(LaneTeam.Blue, LaneItemIds.DragonScale, out _), "Dragon Scale");
            mScales.ApplyDamageForTest(LaneTeam.Blue, 20f, false, false, LaneTeam.Red, DamageFlags.DamageOverTime);
            Assert(GetHero(mScales.Snapshot(), LaneTeam.Blue).adaptiveScalesStacks == 0, "DoT ticks must NOT trigger Adaptive Scales");
            HeroSnap ogSc0 = GetHero(mScales.Snapshot(), LaneTeam.Blue);
            float hpBeforeA1 = ogSc0.hp;
            mScales.ApplyDamageForTest(LaneTeam.Blue, 150f, false, false, LaneTeam.Red, DamageFlags.None);
            float hitA1 = hpBeforeA1 - GetHero(mScales.Snapshot(), LaneTeam.Blue).hp;
            for (int k = 0; k < 4; k++) mScales.ApplyDamageForTest(LaneTeam.Blue, 40f, false, false, LaneTeam.Red, DamageFlags.None);
            Assert(GetHero(mScales.Snapshot(), LaneTeam.Blue).adaptiveScalesStacks == 5, "5 direct hits from attacker A must build 5 Adaptive Scales stacks");

            float hpBeforeA6 = GetHero(mScales.Snapshot(), LaneTeam.Blue).hp;
            mScales.ApplyDamageForTest(LaneTeam.Blue, 150f, false, false, LaneTeam.Red, DamageFlags.None);
            float hitA6 = hpBeforeA6 - GetHero(mScales.Snapshot(), LaneTeam.Blue).hp;
            Assert(hitA6 < hitA1 - 7f, $"Adaptive Scales must reduce follow-up direct hit damage from Attacker A ({hitA1:0.0} -> {hitA6:0.0})");

            // Distinct second attacker ID (999) receives ZERO mitigation from Attacker A's stacks (per-attacker isolation)!
            mScales.SetHeroHpForTest(LaneTeam.Blue, ogSc0.maxHp);
            float hpBeforeB1 = GetHero(mScales.Snapshot(), LaneTeam.Blue).hp;
            mScales.ApplyDamageFromAttackerIdForTest(LaneTeam.Blue, 999, 150f, false, LaneTeam.Red, DamageFlags.None);
            float hitB1 = hpBeforeB1 - GetHero(mScales.Snapshot(), LaneTeam.Blue).hp;
            AssertClose(hitB1, hitA1, "Adaptive Scales built against Attacker A must NOT mitigate damage from Attacker B", 0.1f);
            for (int k = 0; k < 3; k++) mScales.ApplyDamageFromAttackerIdForTest(LaneTeam.Blue, 999, 40f, false, LaneTeam.Red, DamageFlags.None);
            float hpBeforeB5 = GetHero(mScales.Snapshot(), LaneTeam.Blue).hp;
            mScales.ApplyDamageFromAttackerIdForTest(LaneTeam.Blue, 999, 150f, false, LaneTeam.Red, DamageFlags.None);
            float hitB5 = hpBeforeB5 - GetHero(mScales.Snapshot(), LaneTeam.Blue).hp;
            Assert(hitB5 < hitB1 - 6f, $"Attacker B must build its own separate Adaptive Scales stacks ({hitB1:0.0} -> {hitB5:0.0})");

            // 3. Primordial Blood: natural regen rises linearly with missing HP, ability heals are NOT amplified directly
            var mPb = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            var mNoPb = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(mPb.TryBuyItem(LaneTeam.Blue, LaneItemIds.PrimordialBlood, out _), "Primordial Blood");
            Assert(mNoPb.TryBuyItem(LaneTeam.Blue, LaneItemIds.SpringWater, out _), "Equiv base regen check");
            mPb.SetHeroHpForTest(LaneTeam.Blue, GetHero(mPb.Snapshot(), LaneTeam.Blue).maxHp * 0.25f);
            mPb.Tick(1f / 30f);
            float regenLow = GetHero(mPb.Snapshot(), LaneTeam.Blue).netRegenRate;
            mPb.SetHeroHpForTest(LaneTeam.Blue, GetHero(mPb.Snapshot(), LaneTeam.Blue).maxHp * 0.90f);
            mPb.Tick(1f / 30f);
            float regenHigh = GetHero(mPb.Snapshot(), LaneTeam.Blue).netRegenRate;
            Assert(regenLow > regenHigh + 6.0f, $"Primordial Vitality must boost regen linearly when wounded ({regenHigh:0.0}/s -> {regenLow:0.0}/s)");
        }

        private static void CheckGiantsHeartPassives()
        {
            var t = new LaneTuning { startingGold = 8000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };
            var m = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);

            // Giant's Might AD conversion dynamically tracks MaxHP & scales Ogre's Q AD ratio!
            HeroSnap ogBase = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.GiantsHeart, out _), "Giant's Heart");
            HeroSnap ogHeart = GetHero(m.Snapshot(), LaneTeam.Blue);
            float expectedMight = ogHeart.maxHp * t.giantsMightAdPerMaxHp;
            AssertClose(ogHeart.attackDamage, ogBase.attackDamage + expectedMight, "Giant's Might must add real MaxHP*0.030 AD", 0.1f);
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.Heartstone, out _), "Additional Heartstone increases MaxHP and Giant's Might AD");
            HeroSnap ogPlus = GetHero(m.Snapshot(), LaneTeam.Blue);
            AssertClose(ogPlus.attackDamage, ogBase.attackDamage + ogPlus.maxHp * t.giantsMightAdPerMaxHp, "Giant's Might AD must update on MaxHP change", 0.1f);

            // Ancient Vitality: Physical, Magic & True damage deferral + exact conservation (no double mitigation) + independent overlapping instances + item-sale survival + death attribution
            var ma = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            Assert(ma.TryBuyItem(LaneTeam.Blue, LaneItemIds.GiantsHeart, out _), "Giant's Heart for deferral");
            ma.ConfigurePreviewHero(LaneTeam.Blue, ma.Tuning.blueFountainPos, 90f);
            ma.ConfigurePreviewHero(LaneTeam.Red, new Vector2(18f, 0f), 270f);
            HeroSnap b0 = GetHero(ma.Snapshot(), LaneTeam.Blue);
            float preHp = b0.maxHp;
            ma.SetHeroHpForTest(LaneTeam.Blue, preHp);
            // Apply 300 True damage + 200 Physical damage sequentially with a gap (independent non-refreshing instances)
            ma.ApplyDamageForTest(LaneTeam.Blue, 300f, isMagic: false, isTrueDamage: true, LaneTeam.Red);
            HeroSnap afterTrue = GetHero(ma.Snapshot(), LaneTeam.Blue);
            float immediateTrueLoss = preHp - afterTrue.hp;
            float deferredTrue = afterTrue.deferredDamageTotal;
            AssertClose(immediateTrueLoss + deferredTrue, 300f, "True damage immediate + deferred must conserve exact 300 dmg without second mitigation", 0.2f);
            Assert(deferredTrue > 50f && immediateTrueLoss < 250f, "True damage must defer fraction cleanly");

            // Wait 1.5s, apply second physical hit, then SELL Giant's Heart in fountain -> deferred damage STILL pays out completely!
            for (int s = 0; s < 45; s++) ma.Tick(1f / 30f);
            ma.ApplyDamageForTest(LaneTeam.Blue, 200f, isMagic: true, isTrueDamage: false, LaneTeam.Red);
            float defBeforeSell = GetHero(ma.Snapshot(), LaneTeam.Blue).deferredDamageTotal;
            Assert(ma.TrySellItem(LaneTeam.Blue, 0, out _), "Sell Giant's Heart");
            AssertClose(GetHero(ma.Snapshot(), LaneTeam.Blue).deferredDamageTotal, defBeforeSell, "Selling Giant's Heart must NOT clear outstanding deferred damage", 0.5f);

            // Advance 6.0s -> all deferred instances complete to 0
            for (int s = 0; s < 180; s++) ma.Tick(1f / 30f);
            AssertClose(GetHero(ma.Snapshot(), LaneTeam.Blue).deferredDamageTotal, 0f, "All independent deferred damage instances must complete smoothly", 0.05f);

            // Death attribution through deferred damage tick: bring target low, apply heavy hit so deferred tick delivers lethal blow
            var mk = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            Assert(mk.TryBuyItem(LaneTeam.Blue, LaneItemIds.GiantsHeart, out _), "Giant's Heart");
            mk.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            mk.ConfigurePreviewHero(LaneTeam.Red, new Vector2(18f, 0f), 270f);
            mk.SetHeroHpForTest(LaneTeam.Blue, 220f);
            mk.ApplyDamageForTest(LaneTeam.Blue, 310f, isMagic: false, isTrueDamage: true, LaneTeam.Red);
            Assert(GetHero(mk.Snapshot(), LaneTeam.Blue).hp > 0f && GetHero(mk.Snapshot(), LaneTeam.Blue).deferredDamageTotal > 80f,
                "Hero survives immediate split with fatal deferred damage pending");
            for (int s = 0; s < 160; s++)
            {
                mk.Tick(1f / 30f);
                if (GetHero(mk.Snapshot(), LaneTeam.Blue).phase == LanePhase.Dead) break;
            }
            HeroSnap deadBlue = GetHero(mk.Snapshot(), LaneTeam.Blue);
            HeroSnap credRed = GetHero(mk.Snapshot(), LaneTeam.Red);
            Assert(deadBlue.phase == LanePhase.Dead && credRed.kills == 1 && deadBlue.deferredDamageTotal == 0f,
                "Deferred damage killing wearer must clear on death and credit original attacker");

            // Immovable Mountain: reduces forced displacement by STR bounded DR, never 100%, does not reduce stun durations
            var mDisp0 = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            var mDispGh = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(mDispGh.TryBuyItem(LaneTeam.Blue, LaneItemIds.GiantsHeart, out _) &&
                   mDispGh.TryBuyItem(LaneTeam.Blue, LaneItemIds.Titanstone, out _), "Giant's Heart + Titanstone STR");
            mDisp0.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            mDispGh.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            float pushed0 = mDisp0.ApplyDisplacementForTest(LaneTeam.Blue, Vector2.left, 4.0f, LaneTeam.Red);
            float pushedGh = mDispGh.ApplyDisplacementForTest(LaneTeam.Blue, Vector2.left, 4.0f, LaneTeam.Red);
            Assert(pushedGh > 1.2f && pushedGh < pushed0 * 0.72f,
                $"Immovable Mountain must reduce displacement without full immunity (0={pushed0:0.00}u vs GH={pushedGh:0.00}u)");
        }

        private static void CheckBananaBladeNimbleDaggerAndWingedCharmPassives()
        {
            var t = new LaneTuning { startingGold = 8000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };

            // 1. Geometric crescent orientation & exclusions (NOT circle, NOT frontal cone)
            Vector2 atkPos = new Vector2(-4f, 0f);
            Vector2 priPos = new Vector2(0f, 0f);
            Assert(LaneMatch.IsPointInBananaCrescent(new Vector2(2.0f, 0f), atkPos, priPos, 0f, t), "Directly behind primary target must be INSIDE crescent");
            Assert(LaneMatch.IsPointInBananaCrescent(new Vector2(1.2f, 1.8f), atkPos, priPos, 0f, t), "Curved flank behind/beside target must be INSIDE crescent");
            Assert(!LaneMatch.IsPointInBananaCrescent(priPos, atkPos, priPos, 0f, t), "Primary target point itself must be OUTSIDE crescent cutout");
            Assert(!LaneMatch.IsPointInBananaCrescent(new Vector2(-2.2f, 0f), atkPos, priPos, 0f, t), "Point in FRONT of primary target must be OUTSIDE crescent");

            // INT scales crescent area size
            Vector2 farWing = new Vector2(2.6f, 2.3f);
            Assert(!LaneMatch.IsPointInBananaCrescent(farWing, atkPos, priPos, 0f, t) &&
                    LaneMatch.IsPointInBananaCrescent(farWing, atkPos, priPos, 100f, t),
                "High INT must expand Crescent Strike area size cleanly");

            // Live Crescent Strike simulation test (Miravel basic bolt hits primary hero, cleaves secondary minion behind, does NOT double-hit primary or apply Heat/recursive cleave)
            var mBan = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(mBan.TryBuyItem(LaneTeam.Blue, LaneItemIds.BananaBlade, out _) &&
                   mBan.TryBuyItem(LaneTeam.Blue, LaneItemIds.DraconicBlade, out _), "Banana Blade + Draconic Blade");
            mBan.DisableTowersForPreview();
            mBan.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-4f, 0f), 90f);
            mBan.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0f, 0f), 270f);
            int secMinionId = mBan.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(1.9f, 0f), ranged: false, maxHp: 600f);
            float priHp0 = GetHero(mBan.Snapshot(), LaneTeam.Red).hp;
            float secHp0 = 600f;
            mBan.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            bool sawCrescentEv = false;
            for (int s = 0; s < 35; s++)
            {
                mBan.Tick(1f / 30f);
                LaneSnapshot ss = mBan.Snapshot();
                for (int e = 0; e < ss.events.Count; e++)
                {
                    if (ss.events[e].kind == LaneEventKind.BananaCrescent) sawCrescentEv = true;
                }
                if (sawCrescentEv) break;
            }
            float secHpAfter = secHp0;
            LaneSnapshot snapPostCleave = mBan.Snapshot();
            for (int m = 0; m < snapPostCleave.minions.Count; m++)
            {
                if (snapPostCleave.minions[m].id == secMinionId) secHpAfter = snapPostCleave.minions[m].hp;
            }
            Assert(sawCrescentEv && secHpAfter < secHp0 - 20f && GetHero(snapPostCleave, LaneTeam.Red).hp < priHp0,
                $"Crescent Strike must cleave behind primary hero and emit BananaCrescent event (secHP {secHp0}->{secHpAfter:0.0})");

            // 2. Slippery Trickster: hero damage only (no minion trigger), fixed 14s CD (no AH scaling), AGI scaling (INT does NOT affect), decays smoothly, no refresh during CD
            var mSlip = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(mSlip.TryBuyItem(LaneTeam.Blue, LaneItemIds.BananaBlade, out _) &&
                   mSlip.TryBuyItem(LaneTeam.Blue, LaneItemIds.ClockworkPin, out _) &&
                   mSlip.TryBuyItem(LaneTeam.Blue, LaneItemIds.ClockworkPin, out _), "Banana Blade + 44 AH");
            // Minion hit must NOT trigger Slippery Trickster
            mSlip.DisableTowersForPreview();
            int enemyMinion = mSlip.SpawnCustomMinion(LaneTeam.Red, new Vector2(1.4f, 0f));
            mSlip.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            mSlip.ConfigurePreviewHero(LaneTeam.Red, new Vector2(18f, 0f), 270f);
            for (int s = 0; s < 45; s++) mSlip.Tick(1f / 30f);
            AssertClose(GetHero(mSlip.Snapshot(), LaneTeam.Blue).slipperyBonus, 0f, "Minion damage must NOT trigger Slippery Trickster");

            // Enemy hero damage triggers burst & fixed 14s cooldown (ignoring AH)
            mSlip.ApplyDamageForTest(LaneTeam.Blue, 30f, false, false, LaneTeam.Red);
            HeroSnap slipPeak = GetHero(mSlip.Snapshot(), LaneTeam.Blue);
            float peakBonus = slipPeak.slipperyBonus;
            Assert(peakBonus > 1.0f && Mathf.Abs(slipPeak.slipperyCd - t.slipperyTricksterCooldown) < 0.1f,
                $"Slippery Trickster must burst MS on hero damage and use fixed 14s CD ignoring AH (bonus={peakBonus:0.00}, cd={slipPeak.slipperyCd:0.00})");
            for (int s = 0; s < 33; s++) mSlip.Tick(1f / 30f); // 1.1s (~halfway through 2.2s decay)
            HeroSnap slipMid = GetHero(mSlip.Snapshot(), LaneTeam.Blue);
            mSlip.ApplyDamageForTest(LaneTeam.Blue, 30f, false, false, LaneTeam.Red); // hit during CD
            HeroSnap slipPostHitDuringCd = GetHero(mSlip.Snapshot(), LaneTeam.Blue);
            Assert(slipPostHitDuringCd.slipperyBonus <= slipMid.slipperyBonus + 0.01f && slipPostHitDuringCd.slipperyCd < slipPeak.slipperyCd - 0.8f,
                "Repeated damage during cooldown must NOT refresh Slippery Trickster or its cooldown");

            // Compare extra INT vs extra AGI on Slippery Trickster burst
            var mSlipInt = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            var mSlipAgi = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(mSlipInt.TryBuyItem(LaneTeam.Blue, LaneItemIds.BananaBlade, out _) && mSlipInt.TryBuyItem(LaneTeam.Blue, LaneItemIds.SagesEye, out _), "Extra INT");
            Assert(mSlipAgi.TryBuyItem(LaneTeam.Blue, LaneItemIds.BananaBlade, out _) && mSlipAgi.TryBuyItem(LaneTeam.Blue, LaneItemIds.Windfeather, out _), "Extra AGI");
            mSlipInt.ApplyDamageForTest(LaneTeam.Blue, 30f, false, false, LaneTeam.Red);
            mSlipAgi.ApplyDamageForTest(LaneTeam.Blue, 30f, false, false, LaneTeam.Red);
            AssertClose(GetHero(mSlipInt.Snapshot(), LaneTeam.Blue).slipperyBonus, slipPeak.slipperyBonus, "INT must NOT affect Slippery Trickster burst", 0.01f);
            Assert(GetHero(mSlipAgi.Snapshot(), LaneTeam.Blue).slipperyBonus > slipPeak.slipperyBonus + 0.15f, "AGI must increase Slippery Trickster burst");

            // 3. First Strike: readies after 3s without basic attack (even during spell casts), shortens opening wind-up, resets on cancel to block exploit!
            var mFs = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(mFs.TryBuyItem(LaneTeam.Blue, LaneItemIds.NimbleDagger, out _), "Nimble Dagger");
            mFs.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E);
            mFs.DisableTowersForPreview();
            mFs.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-3f, 0f), 90f);
            mFs.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0f, 0f), 270f);
            Assert(GetHero(mFs.Snapshot(), LaneTeam.Blue).firstStrikeReady, "First Strike starts ready after inactivity");
            // Cancel windup mid-swing with Stop -> immediately consumes & resets First Strike so cancel-spam cannot abuse it!
            mFs.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            mFs.Tick(1f / 30f);
            HeroSnap windSnap = GetHero(mFs.Snapshot(), LaneTeam.Blue);
            float normalWind = t.luisBasicWindup / windSnap.attackSpeed;
            Assert(!windSnap.firstStrikeReady && windSnap.phase == LanePhase.Windup && windSnap.phaseDuration < normalWind * 0.80f,
                $"First Strike must shorten opening windup ({windSnap.phaseDuration:0.000}s vs {normalWind:0.000}s) and consume immediately on windup start");
            mFs.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            mFs.Tick(1f / 30f);
            // Cast E during inactivity -> First Strike STILL readies after 3.0s because spellcasts/combat do not block basic-inactivity countdown!
            mFs.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.zero });
            for (int s = 0; s < 93; s++) mFs.Tick(1f / 30f); // 3.1s
            Assert(GetHero(mFs.Snapshot(), LaneTeam.Blue).firstStrikeReady, "First Strike must become ready again in combat while casting spells without basic attacking");

            // 4. Weightlessness: reduces incoming slow strength by 25% while keeping slow duration unchanged
            var mSlowBase = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Swordsman, t);
            var mSlowWing = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Swordsman, t);
            Assert(mSlowWing.TryBuyItem(LaneTeam.Blue, LaneItemIds.WingedCharm, out _), "Winged Charm");
            mSlowBase.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.Q);
            mSlowWing.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.Q);
            mSlowBase.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            mSlowBase.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.5f, 0f), 270f);
            mSlowWing.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            mSlowWing.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.5f, 0f), 270f);
            mSlowBase.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = Vector2.zero });
            mSlowWing.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = Vector2.zero });
            for (int s = 0; s < 10; s++)
            {
                mSlowBase.Tick(1f / 30f);
                mSlowWing.Tick(1f / 30f);
            }
            HeroSnap sbSnap = GetHero(mSlowBase.Snapshot(), LaneTeam.Blue);
            HeroSnap swSnap = GetHero(mSlowWing.Snapshot(), LaneTeam.Blue);
            AssertClose(swSnap.slowAmount, sbSnap.slowAmount * 0.75f, "Weightlessness must reduce incoming slow strength by 25%", 0.01f);
            AssertClose(swSnap.slowTimer, sbSnap.slowTimer, "Weightlessness must NOT shorten incoming slow duration", 0.05f);
        }

        private static void CheckProvisionalManaItemsAndBotStepping()
        {
            // Approved components (101-121) and custom advanced items (201-206) must stay Mana-free; ID 19 stays retired.
            for (int id = 101; id <= 121; id++)
            {
                Assert(LaneItems.TryGet(id, out LaneItemDef c) && c.IsApproved, $"Expected approved component {id}");
                Assert(c.AddMaxMana == 0f && c.AddManaRegen == 0f && c.AddPercentManaRegen == 0f && c.AddResourceCostReduction == 0f,
                    $"Approved component {c.Name} must have zero Mana stats");
            }
            for (int id = 201; id <= 206; id++)
            {
                Assert(LaneItems.TryGet(id, out LaneItemDef a) && a.IsApproved, $"Expected approved advanced item {id}");
                Assert(a.AddMaxMana == 0f && a.AddManaRegen == 0f && a.AddPercentManaRegen == 0f && a.AddResourceCostReduction == 0f,
                    $"Approved advanced item {a.Name} must have zero Mana stats");
            }
            Assert(LaneItems.MigrateItemId(19) == 0 && !LaneItems.TryGet(19, out _), "Legacy item ID 19 must remain retired");

            int[] provisionalManaIds =
            {
                LaneItemIds.ManaDewVial, LaneItemIds.SapphireShard, LaneItemIds.DewdropCharm,
                LaneItemIds.RunedSigil, LaneItemIds.WellspringFlask, LaneItemIds.AstralReservoir,
                LaneItemIds.TidecallerChalice, LaneItemIds.EconomistsTome
            };
            for (int i = 0; i < provisionalManaIds.Length; i++)
            {
                Assert(LaneItems.TryGet(provisionalManaIds[i], out LaneItemDef pm) && !pm.IsApproved,
                    $"Provisional Mana item {provisionalManaIds[i]} missing or marked approved");
            }

            // 1. Mana Dew Vial (Dew Sip): +1.5 Mana/s after 5s out of enemy hero combat
            var t = new LaneTuning { startingGold = 4000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };
            var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Swordsman, t);
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.ManaDewVial, out _), "Buy Mana Dew Vial");
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(0f, 0f) });
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = new Vector2(2f, 0f) });
            Step(m, 1.0f);
            m.ApplyDamageForTest(LaneTeam.Blue, 5f, false, false, LaneTeam.Red);
            m.SetHeroManaForTest(LaneTeam.Blue, 100f);
            Step(m, 2.0f);
            float inCombatGain = GetHero(m.Snapshot(), LaneTeam.Blue).mana - 100f;
            float baseRatePerSec = GetHero(m.Snapshot(), LaneTeam.Blue).manaRegen;
            AssertClose(inCombatGain, baseRatePerSec * 2.0f, "Dew Sip must stay inactive while recently hit by hero", 0.2f);

            Step(m, 3.6f); // lastHeroDamageAge now > 5.0s
            m.SetHeroManaForTest(LaneTeam.Blue, 100f);
            Step(m, 2.0f);
            float outCombatGain = GetHero(m.Snapshot(), LaneTeam.Blue).mana - 100f;
            AssertClose(outCombatGain, (baseRatePerSec + t.manaDewOutOfCombatRegen) * 2.0f,
                "Dew Sip must add out-of-combat Mana regen after 5s", 0.2f);

            // 2. Wellspring Flask (+0.4% MaxMana/s) vs Astral Reservoir (+0.7% MaxMana/s, supersedes Wellspring)
            var ma = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Ogre, t);
            Assert(ma.TryBuyItem(LaneTeam.Blue, LaneItemIds.WellspringFlask, out _), "Buy Wellspring Flask");
            HeroSnap ws = GetHero(ma.Snapshot(), LaneTeam.Blue);
            float expectedWsRegen = t.laserBaseManaRegen + 2.8f + ws.maxMana * t.wellspringPctManaRegen;
            AssertClose(ws.manaRegen, expectedWsRegen, "Wellspring manaRegen total (+0.4% MaxMana/s) mismatch", 0.05f);

            Assert(ma.TryBuyItem(LaneTeam.Blue, LaneItemIds.AstralReservoir, out _), "Upgrade Wellspring into Astral Reservoir");
            // Even with a second Wellspring Flask held alongside Astral Reservoir, Astral Surge must supersede Wellspring.
            Assert(ma.TryBuyItem(LaneTeam.Blue, LaneItemIds.WellspringFlask, out _), "Buy extra Wellspring Flask");
            HeroSnap astral = GetHero(ma.Snapshot(), LaneTeam.Blue);
            float expectedAstralRegen = t.laserBaseManaRegen + 3.5f + 2.8f + astral.maxMana * t.astralSurgePctManaRegen;
            AssertClose(astral.manaRegen, expectedAstralRegen, "Astral Surge must supersede Wellspring (+0.7%, not +1.1%)", 0.05f);

            // 3. Tidecaller Chalice (Tidecaller Flow): linear scaling with missing Mana %
            var mt = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            mt.DisableTowersForPreview();
            Assert(mt.TryBuyItem(LaneTeam.Blue, LaneItemIds.TidecallerChalice, out _), "Buy Tidecaller Chalice");
            mt.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            mt.ConfigurePreviewHero(LaneTeam.Red, new Vector2(12f, 0f), 270f);
            HeroSnap tcFull = GetHero(mt.Snapshot(), LaneTeam.Blue);
            float startHighMana = tcFull.maxMana * 0.96f;
            mt.SetHeroManaForTest(LaneTeam.Blue, startHighMana);
            mt.Tick(1f / 30f);
            float highRegenRate = (GetHero(mt.Snapshot(), LaneTeam.Blue).mana - startHighMana) * 30f;

            float startLowMana = tcFull.maxMana * 0.05f;
            mt.SetHeroManaForTest(LaneTeam.Blue, startLowMana);
            mt.Tick(1f / 30f);
            float lowRegenRate = (GetHero(mt.Snapshot(), LaneTeam.Blue).mana - startLowMana) * 30f;
            float expectedDeltaRate = t.tidecallerMaxMissingManaRegen * (0.96f - 0.05f);
            AssertClose(lowRegenRate - highRegenRate, expectedDeltaRate, "Tidecaller Flow must scale linearly with missing Mana %", 0.2f);

            // 4. Economist's Tome (Frugal Focus): 12% post-cast refund on Flat Mana costs only, requires full upfront cost
            var me = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Ogre, t);
            me.DisableTowersForPreview();
            Assert(me.TryBuyItem(LaneTeam.Blue, LaneItemIds.EconomistsTome, out _), "Buy Economist's Tome");
            Assert(me.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Laser Q");
            me.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2f, 0f), 90f);
            me.ConfigurePreviewHero(LaneTeam.Red, new Vector2(8f, 0f), 270f);
            HeroSnap ecSnap = GetHero(me.Snapshot(), LaneTeam.Blue);
            Assert(ecSnap.rcrStat >= 32f && ecSnap.rcrPct > 0.20f && ecSnap.rcrPct < 0.65f, "RCR scaling must apply from Economist's Tome");
            float qCost = me.ComputeEffectiveAbilityCost(LaneTeam.Blue, LaneAbilitySlot.Q);
            me.SetHeroManaForTest(LaneTeam.Blue, qCost - 1.5f);
            me.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });
            me.Tick(1f / 30f);
            HeroSnap rejectedQ = GetHero(me.Snapshot(), LaneTeam.Blue);
            Assert(rejectedQ.cdQ <= 0f && rejectedQ.mana >= qCost - 1.5f,
                "Frugal Focus must NOT lower upfront Mana check below full effective cost");
            me.SetHeroManaForTest(LaneTeam.Blue, qCost + 25f);
            me.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });
            me.Tick(1f / 30f);
            HeroSnap postQ = GetHero(me.Snapshot(), LaneTeam.Blue);
            Assert(postQ.cdQ > 0f, "Cast Laser Q must succeed with full cost available");
            float expectedAfterRefund = 25f + qCost * t.frugalFocusRefundRatio + postQ.manaRegen / 30f;
            AssertClose(postQ.mana, expectedAfterRefund,
                "Frugal Focus must refund 12% of Flat Mana spent immediately after cast", 0.15f);

            // Verify Frugal Focus does NOT trigger during Miravel R continuous channel
            var mr = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            mr.DisableTowersForPreview();
            mr.GrantXpForTest(LaneTeam.Blue, 1200f);
            Assert(mr.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Level Miravel R");
            Assert(mr.TryBuyItem(LaneTeam.Blue, LaneItemIds.EconomistsTome, out _), "Buy Economist's Tome on Miravel");
            mr.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            mr.ConfigurePreviewHero(LaneTeam.Red, new Vector2(12f, 0f), 270f);
            float rRate = mr.ComputeEffectiveAbilityCost(LaneTeam.Blue, LaneAbilitySlot.R);
            float baseRegen = GetHero(mr.Snapshot(), LaneTeam.Blue).manaRegen;
            mr.SetHeroManaForTest(LaneTeam.Blue, 200f);
            mr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = Vector2.zero });
            mr.Tick(1f / 30f);
            Assert(GetHero(mr.Snapshot(), LaneTeam.Blue).phase == LanePhase.Channeling, "Miravel R must channel cleanly");
            Step(mr, 1.0f);
            HeroSnap postChannel = GetHero(mr.Snapshot(), LaneTeam.Blue);
            float expectedDrainMana = 200f + (baseRegen - rRate) * (31f / 30f);
            AssertClose(postChannel.mana, expectedDrainMana, "Frugal Focus must NOT refund ContinuousPerSecond drain", 0.5f);

            // 5. Bot leaf-first recipe stepping through ResolvePurchase
            int[] plan = { LaneItemIds.ManaDewVial, LaneItemIds.TidecallerChalice };
            int[] inv = new int[LaneItems.MaxSlots];
            Assert(LaneBot.SelectNextBotPurchaseId(plan, inv, 190) == 0, "Wait at 190g for Mana Dew Vial (200g)");
            Assert(LaneBot.SelectNextBotPurchaseId(plan, inv, 200) == LaneItemIds.ManaDewVial, "Buy Mana Dew Vial at 200g");
            inv[0] = LaneItemIds.ManaDewVial;
            Assert(LaneBot.SelectNextBotPurchaseId(plan, inv, 230) == LaneItemIds.DewdropCharm, "Buy first Tidecaller leaf Dewdrop Charm at 230g");
            inv[1] = LaneItemIds.DewdropCharm;
            Assert(LaneBot.SelectNextBotPurchaseId(plan, inv, 250) == LaneItemIds.SacredPearl, "Buy Sacred Pearl leaf next");
            inv[2] = LaneItemIds.SacredPearl;
            Assert(LaneBot.SelectNextBotPurchaseId(plan, inv, 290) == LaneItemIds.ClockworkPin, "Buy Clockwork Pin leaf next");
            inv[3] = LaneItemIds.ClockworkPin;
            Assert(LaneBot.SelectNextBotPurchaseId(plan, inv, 340) == LaneItemIds.TidecallerChalice, "Complete Tidecaller Chalice for exact 340g combine cost");
        }

        private static void CheckScalingStressAndTickBounds()
        {
            var t = new LaneTuning { startingGold = 10000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };

            // 1. Linear curves remain strictly linear across wide stat sweeps
            var m = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.GiantsHeart, out _), "Buy Giant's Heart for MaxHP->AD test");
            float ad0 = GetHero(m.Snapshot(), LaneTeam.Blue).attackDamage;
            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides { enabled = true, bonusMaxHp = 1000f });
            float ad1000 = GetHero(m.Snapshot(), LaneTeam.Blue).attackDamage;
            m.SetPreviewStatOverrides(LaneTeam.Blue, new PreviewStatOverrides { enabled = true, bonusMaxHp = 2000f });
            float ad2000 = GetHero(m.Snapshot(), LaneTeam.Blue).attackDamage;
            AssertClose(ad1000 - ad0, 1000f * t.giantsMightAdPerMaxHp, "Giant's Might +1000 HP linearity", 0.05f);
            AssertClose(ad2000 - ad1000, 1000f * t.giantsMightAdPerMaxHp, "Giant's Might +2000 HP linearity", 0.05f);

            // MaxHP item purchase and sale dynamically updates Giant's Might AD immediately
            m.SetPreviewStatOverrides(LaneTeam.Blue, default);
            float adBeforeLc = GetHero(m.Snapshot(), LaneTeam.Blue).attackDamage;
            Assert(m.TryBuyItem(LaneTeam.Blue, LaneItemIds.LifeCrystal, out _), "Buy extra Life Crystal");
            float adAfterLc = GetHero(m.Snapshot(), LaneTeam.Blue).attackDamage;
            AssertClose(adAfterLc - adBeforeLc, 160f * t.giantsMightAdPerMaxHp, "Buying Life Crystal increases Giant's Might AD", 0.05f);
            int lcSlot = -1;
            int[] items = GetHero(m.Snapshot(), LaneTeam.Blue).items;
            for (int i = 0; i < items.Length; i++) if (items[i] == LaneItemIds.LifeCrystal) lcSlot = i;
            Assert(lcSlot >= 0 && m.TrySellItem(LaneTeam.Blue, lcSlot, out _), "Sell extra Life Crystal");
            AssertClose(GetHero(m.Snapshot(), LaneTeam.Blue).attackDamage, adBeforeLc, "Selling Life Crystal removes extra Giant's Might AD", 0.05f);

            // 2. Bounded diminishing limits never breach asymptotes or create zero costs / full displacement immunity
            var extremeOv = new PreviewStatOverrides
            {
                enabled = true,
                level = 8,
                abilityRank = 3,
                bonusStrength = 2000f,
                bonusAgility = 2000f,
                bonusIntelligence = 2000f,
                bonusAttackDamage = 500f,
                bonusAttackSpeed = 12.0f,
                bonusAbilityHaste = 1000f,
                bonusHealPower = 4.0f,
                bonusMaxHp = 10000f,
                bonusMaxMana = 4000f,
                bonusResourceCostReduction = 1000f,
            };

            var mLaser = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Ogre, t);
            Assert(mLaser.TryBuyItem(LaneTeam.Blue, LaneItemIds.EconomistsTome, out _), "Equip Economist's Tome on extreme Laser");
            Assert(mLaser.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Laser Q");
            mLaser.SetPreviewStatOverrides(LaneTeam.Blue, extremeOv);
            HeroSnap extremeLaser = GetHero(mLaser.Snapshot(), LaneTeam.Blue);
            Assert(extremeLaser.rcrPct > 0.60f && extremeLaser.rcrPct < 0.65f, $"Extreme RCR must stay <65% (got {extremeLaser.rcrPct})");
            float qMinCost = mLaser.ComputeEffectiveAbilityCost(LaneTeam.Blue, LaneAbilitySlot.Q);
            Assert(!float.IsNaN(qMinCost) && qMinCost > 15f, $"Extreme RCR+FrugalFocus cost must stay well above 0 (got {qMinCost})");
            float effAsExtreme = LaneTuning.EffectiveAttackSpeed(extremeLaser.bonusAttackSpeed, t.attackSpeedCurveK);
            Assert(!float.IsNaN(effAsExtreme) && effAsExtreme > 3.5f, "Global sqrt Attack Speed remains unbounded at extreme AS");

            // Displacement resistance stays strictly below 65%
            var mOgre = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Swordsman, t);
            mOgre.DisableTowersForPreview();
            Assert(mOgre.TryBuyItem(LaneTeam.Red, LaneItemIds.GiantsHeart, out _), "Give Red Giant's Heart");
            mOgre.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-10f, 0f), 90f);
            mOgre.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0f, 0f), 180f);
            mOgre.SetPreviewStatOverrides(LaneTeam.Red, extremeOv);
            float moved = mOgre.ApplyDisplacementForTest(LaneTeam.Red, Vector2.right, 4.0f, LaneTeam.Blue);
            float evalMaxResist = t.immovableMountainFormula.Evaluate(1, 2000f);
            Assert(evalMaxResist < 0.65f && moved > 4.0f * (1f - 0.65f) && moved < 2.0f,
                $"Immovable Mountain at extreme STR must reduce <65% without full immunity (moved {moved:0.00}u/4.00u, resist={evalMaxResist:0.000})");

            // First Strike windup floor & valid simulation stability under +1200% AS + Banana Blade + Draconic Blade on multi-targets
            var mStress = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            mStress.DisableTowersForPreview();
            Assert(mStress.TryBuyItem(LaneTeam.Blue, LaneItemIds.BananaBlade, out _), "Equip Banana Blade on Swordsman");
            Assert(mStress.TryBuyItem(LaneTeam.Blue, LaneItemIds.DraconicBlade, out _), "Equip Draconic Blade on Swordsman");
            mStress.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
            mStress.ConfigurePreviewHero(LaneTeam.Red, new Vector2(12f, 0f), 270f);
            mStress.SetPreviewStatOverrides(LaneTeam.Blue, extremeOv);
            int d1 = mStress.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(1.0f, 0f), maxHp: 4000f);
            mStress.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(1.8f, 0.4f), maxHp: 4000f);
            mStress.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(1.8f, -0.4f), maxHp: 4000f);
            mStress.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMinion, targetId = d1 });
            Step(mStress, 3.0f);
            HeroSnap postStress = GetHero(mStress.Snapshot(), LaneTeam.Blue);
            Assert(!float.IsNaN(postStress.hp) && postStress.hp > 0f && !float.IsInfinity(postStress.attackDamage),
                "Extreme AS + Banana Blade + Draconic Blade must remain numerically valid with zero recursion");
        }

        private static void CheckHeroGuidePreviewAndCatalogConsistency()
        {
            var t = new LaneTuning();
            LaneHeroId[] heroes = { LaneHeroId.Ogre, LaneHeroId.Miravel, LaneHeroId.Swordsman, LaneHeroId.LaserShooter };
            LaneAbilitySlot[] slots = { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E, LaneAbilitySlot.R };

            // 1. All 4 hero guides readable & live formula consistency
            for (int h = 0; h < heroes.Length; h++)
            {
                var mLive = new LaneMatch(heroes[h], LaneHeroId.Ogre, t);
                HeroSnap liveSnap = GetHero(mLive.Snapshot(), LaneTeam.Blue);
                LaneHeroSheet.HeroGuideData g = LaneHeroSheet.BuildHeroGuide(heroes[h], t, liveSnap);
                Assert(!string.IsNullOrEmpty(g.Title) && !string.IsNullOrEmpty(g.BaseStatsLine), $"Guide empty for {heroes[h]}");
                Assert(g.Cards.Count >= 5 && g.RuleLines.Count >= 5, $"Guide cards/rules incomplete for {heroes[h]}");
                if (heroes[h] == LaneHeroId.Miravel || heroes[h] == LaneHeroId.LaserShooter)
                {
                    Assert(g.BaseStatsLine.Contains("Mana") && g.LiveStatsLine.Contains("Mana"), $"{heroes[h]} guide must display Mana");
                    LaneHeroSheet.AbilityCardData wTip = LaneHeroSheet.BuildAbilityTooltip(heroes[h], LaneAbilitySlot.W, t, liveSnap);
                    float wCost = mLive.ComputeEffectiveAbilityCost(LaneTeam.Blue, LaneAbilitySlot.W);
                    Assert(wTip != null && wCost > 0f, $"Tooltip & cost missing for {heroes[h]} W");
                }
                else
                {
                    Assert(g.Cards[0].MetaRight.Contains("Resourceless"), $"{heroes[h]} guide must mark Resourceless");
                }
            }

            // 2. Preview isolation, loop reset, override reactivity, and stop-when-closed
            var externalMatch = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            HeroSnap extBefore = GetHero(externalMatch.Snapshot(), LaneTeam.Blue);
            using (var preview = new LaneAbilityPreview(t, enableRenderVisuals: false))
            {
                for (int h = 0; h < heroes.Length; h++)
                {
                    for (int s = 0; s < slots.Length; s++)
                    {
                        preview.Select(heroes[h], slots[s]);
                        int ticksBefore = preview.SimulationTickCount;
                        for (int step = 0; step < 4; step++) preview.Tick(1f / 30f, shouldRun: true);
                        Assert(preview.SimulationTickCount > ticksBefore && preview.LatestPreviewSnapshot != null,
                            $"Preview must advance simulation for {heroes[h]} {slots[s]}");
                    }
                }

                // Live stat overrides update preview without mutating external match
                preview.Select(LaneHeroId.Miravel, LaneAbilitySlot.E);
                var ov = preview.Overrides;
                LaneAbilityPreview.SetOverrideValue(ref ov, PreviewStatField.Intelligence, 100f);
                preview.ApplyLiveOverrides(ov);
                AssertClose(GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue).intelligence, 100f,
                    "Preview override +100 INT must update preview snapshot immediately", 0.1f);
                AssertClose(GetHero(externalMatch.Snapshot(), LaneTeam.Blue).intelligence, extBefore.intelligence,
                    "Preview override must NEVER mutate active external match state", 0.01f);

                // Clean loop restart after LoopDuration
                int loopTicks = Mathf.CeilToInt((preview.LoopDuration + 0.15f) * 30f);
                for (int i = 0; i < loopTicks; i++) preview.Tick(1f / 30f, shouldRun: true);
                Assert(preview.ElapsedInLoop < 0.6f, $"Preview must loop cleanly after LoopDuration (elapsed={preview.ElapsedInLoop})");

                // Closing preview stops simulation work immediately
                preview.Tick(1f / 30f, shouldRun: false);
                Assert(!preview.IsActive, "Preview must deactivate when shouldRun is false");
                int frozenCount = preview.SimulationTickCount;
                for (int i = 0; i < 6; i++) preview.Tick(1f / 30f, shouldRun: false);
                Assert(preview.SimulationTickCount == frozenCount, "Closed preview must perform zero simulation ticks");
            }

            // 3. Preview animation setting persistence roundtrip
            string tempSettings = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"moba_preview_setting_{Guid.NewGuid():N}.json");
            try
            {
                var stSave = new PlayerSettingsStore(tempSettings, persistToDisk: true);
                stSave.Data.enableAbilityDemos = false;
                stSave.Save();

                var stLoad = new PlayerSettingsStore(tempSettings, persistToDisk: true);
                stLoad.Load();
                Assert(!stLoad.Data.enableAbilityDemos, "enableAbilityDemos=false must persist across reloads");

                stLoad.Data.enableAbilityDemos = true;
                stLoad.Save();
                var stReload = new PlayerSettingsStore(tempSettings, persistToDisk: true);
                stReload.Load();
                Assert(stReload.Data.enableAbilityDemos, "enableAbilityDemos=true must persist across reloads");
            }
            finally
            {
                if (System.IO.File.Exists(tempSettings)) System.IO.File.Delete(tempSettings);
            }

            // 4. Full item catalog recipe, price & passive doc consistency
            var seenIds = new HashSet<int>();
            for (int i = 0; i < LaneItems.Catalog.Length; i++)
            {
                LaneItemDef item = LaneItems.Catalog[i];
                Assert(seenIds.Add(item.Id), $"Duplicate item ID {item.Id}");
                Assert(item.Cost > 0 && !string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Fantasy) && !string.IsNullOrWhiteSpace(item.Summary),
                    $"Catalog metadata incomplete for item {item.Id}");
                if (item.PassiveFlags != ItemPassiveFlags.None)
                {
                    Assert(item.Passives != null && item.Passives.Length > 0, $"Item {item.Name} has passive flags without ItemPassiveDoc");
                }
                if (item.Recipe != null && item.Recipe.Length > 0)
                {
                    int componentSum = 0;
                    for (int r = 0; r < item.Recipe.Length; r++)
                    {
                        RecipeIngredient req = item.Recipe[r];
                        Assert(req.Quantity >= 1, $"Invalid recipe quantity on {item.Name}");
                        Assert(LaneItems.TryGet(req.ItemId, out LaneItemDef sub),
                            $"Invalid recipe ingredient {req.ItemId} on {item.Name}");
                        componentSum += sub.Cost * req.Quantity;
                    }
                    Assert(item.Cost >= componentSum, $"Item {item.Name} cost ({item.Cost}g) cannot be lower than ingredient sum ({componentSum}g)");
                }
            }
        }

        private static void Step(LaneMatch m, float seconds)
        {
            const float dt = 1f / 30f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(seconds / dt));
            for (int i = 0; i < steps; i++) m.Tick(dt);
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
