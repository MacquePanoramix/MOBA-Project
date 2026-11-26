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

            Debug.Log("[LaneResourceAndItemChecks] All Stage C-E Resource, Recipe, Component & Custom Item checks passed.");
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
