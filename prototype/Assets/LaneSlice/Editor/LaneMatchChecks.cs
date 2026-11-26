using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Moba.LaneSlice
{
    public static class LaneMatchChecks
    {
        public static void Run()
        {
            CheckZeroInherentAttributeBonuses();
            CheckOgreMultiDimensionalScaling();
            CheckConventionalItemsAndCowardsShield();
            CheckAutoAttackMotionBufferAndAttackMove();
            CheckOgreFullKitAndImmunity();
            CheckMiravelFullKit();
            CheckSwordsmanFullKitAndNegativeResist();
            CheckWavesShopTowerAndVictory();
            CheckHeroSheetCoversAllHeroesAndLiveScaling();
            CheckRepeatedMoveClicksPreserveSpeed();
            CheckTowerAggrosHeroOnDamage();
            CheckMiravelECastKeepsInvisibility();
            CheckSellItemRefundAndNewItemPassives();
            CheckRepeatedAttackMoveSwordsmanBotAndOgreERadius();
            CheckMinionProximityXpWithoutLastHit();
            CheckLaserShooterFullKitAndDecoyBlocking();
            CheckStopCommandAndHeroControlProfiles();
            CheckOptionalRtsSelectionControlGroupsAndOwnership();
            CheckPersonalSettingsKeybindingsConflictsAndPersistence();
            CheckAbilityPreviewsAndInteractiveSandbox();
            CheckBotVsBotBalanceDiagnostic();
            Debug.Log("[LaneMatchChecks] All 21 deterministic checks passed.");
        }

        private static void CheckZeroInherentAttributeBonuses()
        {
            var tuning = new LaneTuning { startingGold = 1200, passiveGoldPerSecond = 0f };
            var match = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, tuning);
            HeroSnap before = GetHero(match.Snapshot(), LaneTeam.Blue);

            AssertClose(before.strength, 0f, "Base Strength must start at 0");
            AssertClose(before.agility, 0f, "Base Agility must start at 0");
            AssertClose(before.intelligence, 0f, "Base Intelligence must start at 0");

            Assert(match.TryBuyItem(LaneTeam.Blue, 8, out _), "Buy Titan's Knuckle (+25 STR)");
            Assert(match.TryBuyItem(LaneTeam.Blue, 9, out _), "Buy Zephyr Feather (+25 AGI)");
            Assert(match.TryBuyItem(LaneTeam.Blue, 10, out _), "Buy Arcane Lens (+25 INT)");

            HeroSnap after = GetHero(match.Snapshot(), LaneTeam.Blue);
            AssertClose(after.strength, 25f, "STR updated");
            AssertClose(after.agility, 25f, "AGI updated");
            AssertClose(after.intelligence, 25f, "INT updated");

            AssertClose(after.maxHp, before.maxHp, "STR must not implicitly add MaxHp");
            AssertClose(after.regenRate, before.regenRate, "STR must not implicitly add Regen");
            AssertClose(after.armor, before.armor, "AGI must not implicitly add Armor");
            AssertClose(after.attackSpeed, before.attackSpeed, "AGI must not implicitly add Attack Speed");
            AssertClose(after.attackDamage, before.attackDamage, "Attributes must not implicitly add AD");
            AssertClose(after.magicResist, before.magicResist, "Attributes must not implicitly add MR");
            AssertClose(after.moveSpeed, before.moveSpeed, "Attributes must not implicitly add Move Speed");
        }

        private static void CheckOgreMultiDimensionalScaling()
        {
            // 1. v0.2.8 Split Ogre Animation Scaling: AGI -> Windup Speed (unbounded sqrt ONLY), Attack Speed -> Recovery Speed (global unbounded sqrt ONLY), STR -> Outer Q width / Knockback / E Radius / R Size / Stun
            var t1 = new LaneTuning { startingGold = 1500, waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
            var mBase = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            var mAd = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            var mAgi = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            var mStr = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            var mAs = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);

            Assert(mAd.TryBuyItem(LaneTeam.Blue, 1, out _), "Ogre buys Iron Greatsword (+AD)");
            Assert(mAgi.TryBuyItem(LaneTeam.Blue, 9, out _), "Ogre buys Zephyr Feather (+25 AGI)");
            Assert(mStr.TryBuyItem(LaneTeam.Blue, 8, out _), "Ogre buys Titan's Knuckle (+25 STR)");
            Assert(mAs.TryBuyItem(LaneTeam.Blue, 2, out _), "Ogre buys Quickstep Dagger (+30% AS)");

            HeroSnap agi25 = GetHero(mAgi.Snapshot(), LaneTeam.Blue);
            float expectedAgi25 = t1.ogreWindupSpeedFormula.Evaluate(1, 25f);
            AssertClose(agi25.ogreWindupSpeed, expectedAgi25, "25 AGI must set unbounded sqrt Ogre Windup speed");
            AssertClose(agi25.ogreRecoverSpeed, 1.0f, "AGI must NOT alter Ogre Recovery speed");

            HeroSnap as30 = GetHero(mAs.Snapshot(), LaneTeam.Blue);
            float expectedAs30 = LaneTuning.EffectiveAttackSpeed(0.30f, t1.attackSpeedCurveK);
            AssertClose(as30.ogreWindupSpeed, 1.0f, "Attack Speed must NOT alter Ogre Windup speed");
            AssertClose(as30.ogreRecoverSpeed, expectedAs30, "Attack Speed must set global sqrt Ogre Recovery speed");

            // Combined AS + AGI affect their respective phases independently
            var mAsAgi = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            Assert(mAsAgi.TryBuyItem(LaneTeam.Blue, 2, out _) && mAsAgi.TryBuyItem(LaneTeam.Blue, 9, out _),
                "Ogre buys Quickstep Dagger + Zephyr Feather");
            HeroSnap bothSnap = GetHero(mAsAgi.Snapshot(), LaneTeam.Blue);
            AssertClose(bothSnap.ogreWindupSpeed, expectedAgi25, "Combined build windup uses AGI only");
            AssertClose(bothSnap.ogreRecoverSpeed, expectedAs30, "Combined build recovery uses AS only");

            // Unbounded diminishing returns on AGI wind-up & STR stun/knockback
            float w0 = t1.ogreQWindup / t1.ogreWindupSpeedFormula.Evaluate(1, 0f);
            float w50 = t1.ogreQWindup / t1.ogreWindupSpeedFormula.Evaluate(1, 50f);
            float w100 = t1.ogreQWindup / t1.ogreWindupSpeedFormula.Evaluate(1, 100f);
            float w150 = t1.ogreQWindup / t1.ogreWindupSpeedFormula.Evaluate(1, 150f);
            Assert((w0 - w50) > (w50 - w100) && (w50 - w100) > (w100 - w150) && w150 < w100,
                "AGI wind-up duration must decrease with unbounded diminishing returns");

            foreach (var m in new[] { mBase, mAd, mAgi, mStr, mAs })
            {
                Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Q");
            }
            MovePairTo(mBase, new Vector2(-1f, 0f), new Vector2(1.5f, 0f));
            MovePairTo(mAd, new Vector2(-1f, 0f), new Vector2(1.5f, 0f));
            MovePairTo(mAgi, new Vector2(-1f, 0f), new Vector2(1.5f, 0f));
            MovePairTo(mStr, new Vector2(-1f, 0f), new Vector2(1.5f, 0f));
            MovePairTo(mAs, new Vector2(-1f, 0f), new Vector2(1.5f, 0f));

            float baseHpBefore = GetHero(mBase.Snapshot(), LaneTeam.Red).hp;
            float adHpBefore = GetHero(mAd.Snapshot(), LaneTeam.Red).hp;

            mBase.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });
            mAd.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });
            mAgi.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });
            mStr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });
            mAs.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });

            // Windup test: AGI 25 finishes Q windup faster (~0.71s < 0.88s), while +30% AS STILL takes full ~0.88s windup!
            Step(mAgi, 0.75f);
            Step(mAs, 0.75f);
            Assert(GetHero(mAgi.Snapshot(), LaneTeam.Red).stunTimer > 0f, "AGI Ogre Q windup must finish faster than 0.75s");
            Assert(GetHero(mAs.Snapshot(), LaneTeam.Red).stunTimer <= 0f, "AS must NOT speed up Ogre Q wind-up (still winding at 0.75s)");

            // Recovery test: continue Base (0.88 + 0.36 = 1.24s) vs AS (0.88 + 0.36/1.311 = 1.155s)
            Step(mBase, 1.17f);
            Step(mAs, 0.42f); // total 0.75 + 0.42 = 1.17s
            Assert(GetHero(mBase.Snapshot(), LaneTeam.Blue).phase == LanePhase.Recover, "Base Ogre still in Q Recover at 1.17s");
            Assert(GetHero(mAs.Snapshot(), LaneTeam.Blue).phase == LanePhase.Idle, "High-AS Ogre must finish Q Recover faster by 1.17s!");

            Step(mBase, 0.25f);
            Step(mAd, 1.00f);
            Step(mStr, 1.00f);

            float baseDmg = baseHpBefore - GetHero(mBase.Snapshot(), LaneTeam.Red).hp;
            float adDmg = adHpBefore - GetHero(mAd.Snapshot(), LaneTeam.Red).hp;
            Assert(adDmg > baseDmg + 25f, $"Iron Greatsword (+AD) must boost Ogre Q damage ({adDmg} vs {baseDmg})");

            float baseStun = GetHero(mBase.Snapshot(), LaneTeam.Red).stunTimer;
            float strStun = GetHero(mStr.Snapshot(), LaneTeam.Red).stunTimer;
            Assert(strStun > baseStun + 0.12f, $"STR must boost Ogre Q stun duration ({strStun} vs {baseStun})");

            // Dual-Zone Q mutual exclusivity & STR-scaled outer width (Zero CC on outer!)
            var mQZoneBase = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            var mQZoneStr = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            Assert(mQZoneStr.TryBuyItem(LaneTeam.Blue, 8, out _) && mQZoneStr.TryBuyItem(LaneTeam.Blue, 8, out _) && mQZoneStr.TryBuyItem(LaneTeam.Blue, 8, out _), "75 STR");
            Assert(mQZoneBase.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Q");
            Assert(mQZoneStr.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Q");

            // Place enemy at perp = 1.35u (outside central 0.95u, inside outer 0.95+0.65=1.60u)
            MovePairTo(mQZoneBase, new Vector2(0f, 0f), new Vector2(2.5f, 1.35f));
            float mirOuterPre = GetHero(mQZoneBase.Snapshot(), LaneTeam.Red).hp;
            mQZoneBase.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(4f, 0f) });
            Step(mQZoneBase, 1.0f);
            HeroSnap mirOuterPost = GetHero(mQZoneBase.Snapshot(), LaneTeam.Red);
            float outerDealt = mirOuterPre - mirOuterPost.hp;
            Assert(outerDealt > 35f && outerDealt < baseDmg * 0.75f && mirOuterPost.stunTimer <= 0.001f,
                $"Ogre Q outer shockwave must deal reduced dmg without double-hit and ZERO CC (outerDmg={outerDealt:0.0} vs central={baseDmg:0.0}, stun={mirOuterPost.stunTimer:0.00})");

            // Place enemy at perp = 1.90u: misses STR=0 (total 1.60u), hits outer shockwave at STR=75 (total 2.44u)
            var mQWideMiss = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            Assert(mQWideMiss.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Q");
            MovePairTo(mQWideMiss, new Vector2(0f, 0f), new Vector2(2.5f, 1.90f));
            MovePairTo(mQZoneStr, new Vector2(0f, 0f), new Vector2(2.5f, 1.90f));
            float missHp0 = GetHero(mQWideMiss.Snapshot(), LaneTeam.Red).hp;
            float strOuterHp0 = GetHero(mQZoneStr.Snapshot(), LaneTeam.Red).hp;
            mQWideMiss.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(4f, 0f) });
            mQZoneStr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(4f, 0f) });
            Step(mQWideMiss, 1.0f);
            Step(mQZoneStr, 1.0f);
            AssertClose(GetHero(mQWideMiss.Snapshot(), LaneTeam.Red).hp, missHp0, "Base STR=0 Ogre Q must miss at perp=1.90u");
            Assert(strOuterHp0 - GetHero(mQZoneStr.Snapshot(), LaneTeam.Red).hp > 35f && GetHero(mQZoneStr.Snapshot(), LaneTeam.Red).stunTimer <= 0.001f,
                "STR=75 must expand Ogre Q outer shockwave to hit at perp=1.90u without stunning");

            // W unbounded sqrt knockback distance (STR 0 < STR 50 < STR 150) & PushOut safety
            var mwBase = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            var mwStr = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            var mwHiStr = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t1);
            Assert(mwStr.TryBuyItem(LaneTeam.Blue, 8, out _) && mwStr.TryBuyItem(LaneTeam.Blue, 8, out _), "50 STR");
            for (int k = 0; k < 5; k++) Assert(mwHiStr.TryBuyItem(LaneTeam.Blue, 8, out _), "125 STR");
            Assert(mwBase.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level W");
            Assert(mwStr.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level W");
            Assert(mwHiStr.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level W");
            MovePairTo(mwBase, new Vector2(0f, 0f), new Vector2(1.8f, 0f));
            MovePairTo(mwStr, new Vector2(0f, 0f), new Vector2(1.8f, 0f));
            MovePairTo(mwHiStr, new Vector2(0f, 0f), new Vector2(1.8f, 0f));
            mwBase.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(3f, 0f) });
            mwStr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(3f, 0f) });
            mwHiStr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(3f, 0f) });
            Step(mwBase, 0.85f);
            Step(mwStr, 0.85f);
            Step(mwHiStr, 0.85f);
            float kb0 = GetHero(mwBase.Snapshot(), LaneTeam.Red).x;
            float kb50 = GetHero(mwStr.Snapshot(), LaneTeam.Red).x;
            float kb125 = GetHero(mwHiStr.Snapshot(), LaneTeam.Red).x;
            Assert(kb50 > kb0 + 1.2f && kb125 > kb50 + 1.2f,
                $"Unbounded sqrt W knockback must scale steeply with STR ({kb0:0.00} < {kb50:0.00} < {kb125:0.00})");

            // E STR-scaled impact radius & R proportional transformed geometry
            var mrSize = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f, startingGold = 1500, passiveGoldPerSecond = 0f });
            Assert(mrSize.TryBuyItem(LaneTeam.Blue, 8, out _) && mrSize.TryBuyItem(LaneTeam.Blue, 8, out _), "50 STR for R size");
            Assert(mrSize.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Unlock R");
            MovePairTo(mrSize, new Vector2(-1f, 0f), new Vector2(5.5f, 0f));
            float expectedScale = mrSize.Tuning.ogreRScaleFormula.Evaluate(1, 50f);
            mrSize.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(5.5f, 0f) });
            Step(mrSize, 0.10f);
            HeroSnap ogBig = GetHero(mrSize.Snapshot(), LaneTeam.Blue);
            AssertClose(ogBig.scale, expectedScale, "Ogre R size must scale boundedly with STR");
            AssertClose(ogBig.ogreQCentralHalfWidth, mrSize.Tuning.ogreQWidth * expectedScale, "Transformed Q central width must scale proportionally with R size");
            AssertClose(ogBig.ogreWRange, mrSize.Tuning.ogreWRange * expectedScale, "Transformed W range must scale proportionally with R size");
        }

        private static void CheckConventionalItemsAndCowardsShield()
        {
            var t = new LaneTuning { startingGold = 3000, waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };

            // 1. Global Attack Speed Unbounded Diminishing Returns (No Hard Cap)
            var mAs1 = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            var mAs2 = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            var mAs6 = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(mAs1.TryBuyItem(LaneTeam.Blue, 2, out _), "1 Dagger (+30% AS)");
            for (int k = 0; k < 2; k++) Assert(mAs2.TryBuyItem(LaneTeam.Blue, 2, out _), "2 Daggers (+60% AS)");
            for (int k = 0; k < 6; k++) Assert(mAs6.TryBuyItem(LaneTeam.Blue, 2, out _), "6 Daggers (+180% AS)");
            float e0 = LaneTuning.EffectiveAttackSpeed(0f, t.attackSpeedCurveK);
            float e1 = GetHero(mAs1.Snapshot(), LaneTeam.Blue).attackSpeed;
            float e2 = GetHero(mAs2.Snapshot(), LaneTeam.Blue).attackSpeed;
            float e6 = GetHero(mAs6.Snapshot(), LaneTeam.Blue).attackSpeed;
            Assert((e1 - e0) > (e2 - e1) && e6 > e2 + 0.5f,
                $"Global AS must follow unbounded diminishing curve without hard cap (1.00 -> {e1:0.00} -> {e2:0.00} -> {e6:0.00})");

            // 2. Kindred Charm universal aura works for non-Miravel hero (Ogre heals Swordsman ally)
            var mKc = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Ogre, t);
            HeroSnap blue0 = GetHero(mKc.Snapshot(), LaneTeam.Blue);
            AssertClose(blue0.miravelLifeAura, 0f, "Base Ogre emits/receives 0 life aura");
            Assert(mKc.TryBuyItem(LaneTeam.Blue, 11, out _), "Ogre buys Kindred Charm");
            Step(mKc, 0.05f);
            HeroSnap ogKindred = GetHero(mKc.Snapshot(), LaneTeam.Blue);
            Assert(ogKindred.miravelLifeAura >= 1.6f * 1.21f,
                $"Kindred Charm must activate allied regen aura on non-Miravel heroes (got +{ogKindred.miravelLifeAura:0.00}/s)");

            // 3. The Coward's Shield — ALL 10 movement-based retreat, grace, decay & anti-exploit cases!
            LaneMatch SetupCowardScenario(bool extraAgi = false)
            {
                var mc = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
                Assert(mc.TryBuyItem(LaneTeam.Blue, 12, out _), "Buy Coward's Shield");
                if (extraAgi) Assert(mc.TryBuyItem(LaneTeam.Blue, 9, out _), "Buy +25 AGI");
                Assert(mc.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.E), "Ogre E");
                MovePairTo(mc, new Vector2(0f, 0f), new Vector2(1.5f, 0f));
                mc.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastE, point = Vector2.zero });
                Step(mc, 1.85f); // hit + stun expires
                return mc;
            }

            // Case 1 & 2: Stationary facing away -> 0 protection; Sustained retreat -> full protection
            LaneMatch mc1 = SetupCowardScenario();
            mc1.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-1.2f, 0f) });
            Step(mc1, 0.12f); // turn facing west toward (-1.2)
            mc1.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            Step(mc1, 1.50f); // Stand completely still facing AWAY from Red attacker at +1.5
            AssertClose(GetHero(mc1.Snapshot(), LaneTeam.Blue).cowardShieldBonus, 0f,
                "Case 2: Stationary hero facing away must earn 0 Coward's Shield bonus");

            mc1.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-18f, 0f) });
            Step(mc1, 1.90f);
            HeroSnap sustained = GetHero(mc1.Snapshot(), LaneTeam.Blue);
            float expectedCap12 = t.cowardShieldResistFormula.Evaluate(1, 12f);
            Assert(sustained.cowardShieldBonus >= expectedCap12 * 0.95f && sustained.cowardShieldState == 2,
                $"Case 1: Sustained retreat must reach full bonus (got {sustained.cowardShieldBonus:0.0} vs {expectedCap12:0.0}, state={sustained.cowardShieldState})");

            // Verify AGI still scales shield magnitude (linear) without bypassing buildup timing
            LaneMatch mcHiAgi = SetupCowardScenario(extraAgi: true);
            mcHiAgi.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-18f, 0f) });
            Step(mcHiAgi, 1.90f);
            Assert(GetHero(mcHiAgi.Snapshot(), LaneTeam.Blue).cowardShieldBonus > sustained.cowardShieldBonus + 18f,
                "Extra AGI must linearly scale peak Coward's Shield resistance");

            // Case 3: Brief retreat flick (<0.14s) before attacking -> negligible/zero protection
            LaneMatch mcFlick = SetupCowardScenario();
            mcFlick.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-10f, 0f) });
            Step(mcFlick, 0.13f);
            mcFlick.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            Step(mcFlick, 0.04f);
            Assert(GetHero(mcFlick.Snapshot(), LaneTeam.Blue).cowardShieldBonus < 1.0f,
                $"Case 3: Very brief retreat flick must grant ~0 protection (got {GetHero(mcFlick.Snapshot(), LaneTeam.Blue).cowardShieldBonus:0.00})");

            // Case 4: Rapid alternating back-and-forth exploit -> blocked (cannot build or preserve via alternating grace refresh)
            LaneMatch mcExploit = SetupCowardScenario();
            for (int rep = 0; rep < 8; rep++)
            {
                mcExploit.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-10f, 0f) });
                Step(mcExploit, 0.22f);
                mcExploit.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(10f, 0f) });
                Step(mcExploit, 0.22f);
            }
            Assert(GetHero(mcExploit.Snapshot(), LaneTeam.Blue).cowardShieldBonus < 4.0f,
                $"Case 4: Rapid alternating direction exploit must not accumulate meaningful shield (got {GetHero(mcExploit.Snapshot(), LaneTeam.Blue).cowardShieldBonus:0.00})");

            // Case 5 & 8: Brief stop during genuine retreat preserves stacks via grace without building, and resuming builds cleanly
            LaneMatch mcGrace = SetupCowardScenario();
            mcGrace.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-18f, 0f) });
            Step(mcGrace, 1.00f); // partial buildup (~0.55) + earns grace
            float bonusBeforeStop = GetHero(mcGrace.Snapshot(), LaneTeam.Blue).cowardShieldBonus;
            Assert(bonusBeforeStop > 10f && bonusBeforeStop < expectedCap12 * 0.85f, $"Partial genuine buildup ready ({bonusBeforeStop:0.0})");
            mcGrace.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            Step(mcGrace, 0.40f); // inside 0.75s grace!
            HeroSnap pausedSnap = GetHero(mcGrace.Snapshot(), LaneTeam.Blue);
            Assert(Mathf.Abs(pausedSnap.cowardShieldBonus - bonusBeforeStop) <= 0.1f && pausedSnap.cowardShieldState == 3,
                $"Case 5: Brief stop during retreat must hold protection in Grace without building ({bonusBeforeStop:0.0} -> {pausedSnap.cowardShieldBonus:0.0}, state={pausedSnap.cowardShieldState})");

            mcGrace.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-18f, 0f) });
            Step(mcGrace, 1.10f);
            Assert(GetHero(mcGrace.Snapshot(), LaneTeam.Blue).cowardShieldBonus > bonusBeforeStop + 6f,
                "Case 8: Resuming retreat after brief stop must continue building from preserved stacks");

            // Case 6: Stun during genuine retreat preserves stacks via grace without increasing
            LaneMatch mcStun = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, new LaneTuning { startingGold = 1500, waveFirstSpawnDelay = 999f, ogreQWindup = 0.25f, ogreQCooldown = 1.0f });
            Assert(mcStun.TryBuyItem(LaneTeam.Blue, 12, out _), "Shield");
            Assert(mcStun.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.Q), "Ogre Q");
            MovePairTo(mcStun, new Vector2(0f, 0f), new Vector2(0.8f, 0f));
            mcStun.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = Vector2.zero });
            Step(mcStun, 1.35f); // hit + stun clear
            mcStun.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-18f, 0f) });
            Step(mcStun, 0.65f); // retreating (~2.6u left, still inside Ogre 4.7u Q range!)
            mcStun.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = new Vector2(-6f, 0f) });
            Step(mcStun, 0.30f); // Ogre Q hits & stuns Miravel mid-retreat
            HeroSnap atStun = GetHero(mcStun.Snapshot(), LaneTeam.Blue);
            float bonusAtStun = atStun.cowardShieldBonus;
            Assert(atStun.stunTimer > 0.6f && bonusAtStun > 5f,
                $"Case 6 setup: Miravel must be stunned while holding Coward stacks (stun={atStun.stunTimer:0.00}, bonus={bonusAtStun:0.0})");
            Step(mcStun, 0.40f); // stays inside 0.75s grace while stunned
            HeroSnap midStun = GetHero(mcStun.Snapshot(), LaneTeam.Blue);
            Assert(midStun.stunTimer > 0.2f && Mathf.Abs(midStun.cowardShieldBonus - bonusAtStun) <= 0.1f && midStun.cowardShieldState == 3,
                $"Case 6: Stun during retreat must preserve grace without gaining stacks (atStun={bonusAtStun:0.0}, midStun={midStun.cowardShieldBonus:0.0}, state={midStun.cowardShieldState})");

            // Case 7: Encountering obstacle mid-retreat (pinned at lane wall -23.45u) preserves via grace cleanly
            LaneMatch mcWall = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(mcWall.TryBuyItem(LaneTeam.Blue, 12, out _), "Shield");
            Assert(mcWall.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.E), "Ogre E");
            MovePairTo(mcWall, new Vector2(-20.2f, 0f), new Vector2(-18.6f, 0f));
            mcWall.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastE, point = new Vector2(-20.2f, 0f) });
            Step(mcWall, 1.85f);
            mcWall.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-23.4f, 0f) });
            Step(mcWall, 1.15f); // walks to wall (~0.75s, earning grace) & holds pinned against boundary inside grace!
            HeroSnap atWall = GetHero(mcWall.Snapshot(), LaneTeam.Blue);
            Assert(atWall.cowardShieldBonus > 8f && atWall.cowardShieldState == 3,
                $"Case 7: Hitting boundary wall mid-retreat must still hold earned protection in Grace (bonus={atWall.cowardShieldBonus:0.0}, state={atWall.cowardShieldState})");

            // Case 9: Turning back to actively pursue attacker decays FASTER than standing still after grace!
            LaneMatch mcStopDecay = SetupCowardScenario();
            LaneMatch mcPursuitDecay = SetupCowardScenario();
            mcStopDecay.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-18f, 0f) });
            mcPursuitDecay.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-18f, 0f) });
            Step(mcStopDecay, 1.85f);
            Step(mcPursuitDecay, 1.85f);
            mcStopDecay.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            mcPursuitDecay.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(5f, 0f) });
            Step(mcStopDecay, 0.55f);
            Step(mcPursuitDecay, 0.55f);
            float afterStop = GetHero(mcStopDecay.Snapshot(), LaneTeam.Blue).cowardShieldBonus;
            float afterPursuit = GetHero(mcPursuitDecay.Snapshot(), LaneTeam.Blue).cowardShieldBonus;
            Assert(afterPursuit < afterStop - 8f && afterPursuit > 0f,
                $"Case 9: Active pursuit must decay gradually AND faster than standing still (pursuit={afterPursuit:0.0} vs stop={afterStop:0.0})");

            // Case 10: Expiration of recent-attacker memory (5.0s) decays stacks smoothly to 0
            Step(mcStopDecay, 6.2f);
            AssertClose(GetHero(mcStopDecay.Snapshot(), LaneTeam.Blue).cowardShieldBonus, 0f,
                "Case 10: Expired recent-attacker memory must decay shield bonus smoothly to 0");

            // Chrono Band Ability Haste check (no hard cap, independent of attributes)
            var mCdr = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            Assert(mCdr.TryBuyItem(LaneTeam.Blue, 6, out _), "Buy Chrono Band (+22 AH)");
            Assert(mCdr.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Q");
            mCdr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(5f, 0f) });
            Step(mCdr, 0.05f);
            HeroSnap ahOne = GetHero(mCdr.Snapshot(), LaneTeam.Blue);
            float expectedCdOne = t.ogreQCooldown * LaneMatch.CooldownFactor(22f);
            Assert(Mathf.Abs(ahOne.cdQ - expectedCdOne) < 0.1f, $"1 Chrono Band (+22 AH) must set Q CD near {expectedCdOne:0.00}s (got {ahOne.cdQ:0.00}s)");

            var mAhStack = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            for (int k = 0; k < 4; k++) Assert(mAhStack.TryBuyItem(LaneTeam.Blue, 6, out _), $"Chrono Band #{k + 1}");
            Assert(mAhStack.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Q");
            mAhStack.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(5f, 0f) });
            Step(mAhStack, 0.05f);
            HeroSnap ahFour = GetHero(mAhStack.Snapshot(), LaneTeam.Blue);
            float expectedCdFour = t.ogreQCooldown * LaneMatch.CooldownFactor(88f);
            Assert(ahFour.abilityHaste > 87.5f && ahFour.cooldownReduction > 0.46f && Mathf.Abs(ahFour.cdQ - expectedCdFour) < 0.1f,
                $"88 AH must apply diminishing no-cap CD reduction ({ahFour.cooldownReduction * 100f:0.0}%, cd={ahFour.cdQ:0.00}s)");
        }

        private static void CheckAutoAttackMotionBufferAndAttackMove()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f };
            // 1. Ogre cannot basic attack
            var mo = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            MovePairTo(mo, new Vector2(0f, 0f), new Vector2(1.8f, 0f));
            float mirHp0 = GetHero(mo.Snapshot(), LaneTeam.Red).hp;
            mo.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(mo, 2.5f);
            Assert(GetHero(mo.Snapshot(), LaneTeam.Red).hp >= mirHp0 - 1f, "Ogre must never perform basic attacks");

            // 2. Swordsman continuous Twin Blade attacks + generous motion buffer when opponent steps slightly back
            var ms = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            MovePairTo(ms, new Vector2(0f, 0f), new Vector2(2.2f, 0f));
            float ogHp0 = GetHero(ms.Snapshot(), LaneTeam.Red).hp;
            ms.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(ms, 0.10f); // mid-windup
            // Opponent moves slightly away (+0.5u) -> outside 2.35 base range, STILL inside buffer (2.35+0.52+1.15 = 4.02)
            ms.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = new Vector2(3.1f, 0f) });
            Step(ms, 2.2f);
            float ogHp1 = GetHero(ms.Snapshot(), LaneTeam.Red).hp;
            Assert(ogHp1 < ogHp0 - 80f, $"Continuous auto-attack + motion buffer must land multi-hit swings (dmg={ogHp0 - ogHp1})");

            // 3. A-key Quick-Cast Attack-Move acquires nearby enemy without clicking directly on top of them
            var ma = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            MovePairTo(ma, new Vector2(-4f, 0f), new Vector2(1f, 3.0f));
            float enemyHpStart = GetHero(ma.Snapshot(), LaneTeam.Red).hp;
            ma.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMove, point = new Vector2(1f, 0f) });
            Step(ma, 1.5f);
            Assert(GetHero(ma.Snapshot(), LaneTeam.Red).hp < enemyHpStart - 20f,
                "A-key Attack-Move near enemy must acquire & shoot without exact cursor-on-target");
        }

        private static void CheckOgreFullKitAndImmunity()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, startingGold = 0, ogreQCooldown = 1.0f };
            var m = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Ogre, t);
            MovePairTo(m, new Vector2(-1f, 0f), new Vector2(1.2f, 0f));

            // CC immunity during Ogre Q windup blocks incoming enemy Q stun
            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Blue Ogre Q");
            Assert(m.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.Q), "Red Ogre Q");
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = new Vector2(-1f, 0f) });
            Step(m, 0.35f);
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });

            bool sawBlocked = false;
            float dt = 1f / 30f;
            for (int i = 0; i < 20; i++)
            {
                m.Tick(dt);
                LaneSnapshot s = m.Snapshot();
                for (int e = 0; e < s.events.Count; e++)
                {
                    if (s.events[e].kind == LaneEventKind.CcBlocked && s.events[e].team == LaneTeam.Blue) sawBlocked = true;
                }
            }
            Assert(sawBlocked, "Ogre CC immunity during Q must emit CcBlocked against incoming enemy stun");
            AssertClose(GetHero(m.Snapshot(), LaneTeam.Blue).stunTimer, 0f, "CC-immune Ogre must take 0 stun");
            // Grant XP to level 4 for R unlock
            var mr = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f });
            Assert(mr.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Unlock Ogre R at lvl 1 via ultMinLevel=1");
            MovePairTo(mr, new Vector2(-1f, 0f), new Vector2(2.0f, 0f));
            mr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(2f, 0f) });
            Step(mr, 0.1f);
            Assert(GetHero(mr.Snapshot(), LaneTeam.Blue).scale > 1.2f, "Ogre grows during R channel");
            Assert(GetHero(mr.Snapshot(), LaneTeam.Blue).damageReduction > 0.5f, "Ogre gains DR during R channel");

            // Queue 3 aimed strikes (Q, W, Q) during channel!
            mr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });
            mr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(3f, 0f) });
            mr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(4f, 0f) });
            Assert(GetHero(mr.Snapshot(), LaneTeam.Blue).rComboCount == 3, "Queued 3 strikes into R");

            bool sawCrit = false;
            for (int i = 0; i < 160; i++)
            {
                mr.Tick(dt);
                LaneSnapshot s = mr.Snapshot();
                for (int e = 0; e < s.events.Count; e++)
                {
                    if (s.events[e].kind == LaneEventKind.OgreStrike && s.events[e].label.Contains("CRIT")) sawCrit = true;
                }
            }
            Assert(sawCrit, "Ogre R final strike must fire CRIT event");
        }

        private static void CheckMiravelFullKit()
        {
            var t = new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f, luisOrbInterval = 2.0f, ogreRStacksCursePerStrike = false };
            var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);

            // Confirm Level-1 HealPower starts at 100% (1.0x) and Passive Life Aura is immediately active
            Step(m, 0.05f);
            HeroSnap mirStart = GetHero(m.Snapshot(), LaneTeam.Blue);
            AssertClose(mirStart.healPower, 1.0f, "Miravel Heal Power must start at 100% (1.0)");
            AssertClose(mirStart.miravelLifeAura, 2.20f, "Miravel passive Life Aura must start at +2.20/s at Level 1");

            // Level W & test invis + illusion walking to exact cast cursor & stopping!
            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Miravel W");
            MovePairTo(m, new Vector2(-2f, 0f), new Vector2(4.0f, 0f));
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(0.5f, 0f) });
            Step(m, 0.25f);
            HeroSnap mir = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(mir.invisible, "Miravel W grants invisibility");
            Assert(m.Snapshot().illusions.Count == 1, "Miravel W leaves illusion");
            IllusionSnap ill0 = m.Snapshot().illusions[0];
            Assert(ill0.speed > 0.5f, $"Illusion must walk with non-zero speed while moving (speed={ill0.speed:0.00})");
            Step(m, 0.30f);
            Assert(m.Snapshot().illusions[0].x > ill0.x + 0.5f, "Illusion walks toward cursor");
            Step(m, 0.95f);
            IllusionSnap illArrived = m.Snapshot().illusions[0];
            Assert(Mathf.Abs(illArrived.x - 0.5f) < 0.25f && illArrived.speed <= 0.01f,
                $"Illusion must arrive and stop animating at cast cursor (x={illArrived.x:0.00}, speed={illArrived.speed:0.00})");
            Step(m, 0.50f);
            IllusionSnap illStay = m.Snapshot().illusions[0];
            Assert(Mathf.Abs(illStay.x - illArrived.x) < 0.01f && illStay.speed <= 0.01f,
                $"Illusion must stay stopped after arriving (x={illStay.x:0.00}, speed={illStay.speed:0.00})");

            // Verify NO 6.5u distance cap on Miravel W decoy
            var mwFar = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(mwFar.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Miravel W far");
            MovePairTo(mwFar, new Vector2(-7.5f, 2.5f), new Vector2(15f, 2.5f));
            mwFar.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(4.5f, 2.5f) });
            Step(mwFar, 2.1f);
            IllusionSnap illFarMid = mwFar.Snapshot().illusions[0];
            Assert(illFarMid.x > 0.2f && illFarMid.speed > 0.5f,
                $"Illusion must travel well past old 6.5u limit toward far cursor (x={illFarMid.x:0.00}, speed={illFarMid.speed:0.00})");
            Step(mwFar, 1.3f);
            IllusionSnap illFarStop = mwFar.Snapshot().illusions[0];
            Assert(Mathf.Abs(illFarStop.x - 4.5f) < 0.25f && illFarStop.speed <= 0.01f,
                $"Long-range illusion must stop at far cast cursor (x={illFarStop.x:0.00}, speed={illFarStop.speed:0.00})");

            // E Ward curse triggers on Miravel OR W illusion hit, and escalates when cursed attacker casts abilities
            var mCurse = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f, ogreQCooldown = 1.0f, ogreRStacksCursePerStrike = false });
            Assert(mCurse.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Miravel E");
            Assert(mCurse.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.Q), "Ogre Q");
            MovePairTo(mCurse, new Vector2(-1f, 0f), new Vector2(1.4f, 0f));
            mCurse.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.zero });
            mCurse.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = new Vector2(-1f, 0f) });
            Step(mCurse, 1.0f);
            HeroSnap ogreCursed = GetHero(mCurse.Snapshot(), LaneTeam.Red);
            Assert(ogreCursed.curseTimer > 0f && ogreCursed.curseStacks == 1, "Attacking warded Miravel must apply 1 curse stack");
            float durBefore = ogreCursed.curseTimer;

            // Ogre casts Q again while cursed -> stack escalates & duration extends!
            Step(mCurse, 0.35f);
            mCurse.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = new Vector2(-1f, 0f) });
            Step(mCurse, 0.05f);
            HeroSnap escalated = GetHero(mCurse.Snapshot(), LaneTeam.Red);
            Assert(escalated.curseStacks == 2 && escalated.curseTimer > durBefore, "Cursed enemy ability cast must increase stacks & extend duration");

            // Check Miravel R ramping zone + move interrupt!
            var mR = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(mR.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Miravel R");
            MovePairTo(mR, new Vector2(-2f, 0f), new Vector2(0f, 0f));
            mR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(0f, 0f) });
            Step(mR, 0.5f);
            Assert(mR.Snapshot().zones.Count == 1, "Miravel R channel creates active Sanctum Zone");
            Assert(GetHero(mR.Snapshot(), LaneTeam.Red).damageTakenMult > 1.02f, "Miravel R ramps vulnerability on enemies inside");
            // Moving interrupts R channel!
            mR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-5f, 0f) });
            Step(mR, 0.1f);
            Assert(mR.Snapshot().zones.Count == 0, "Issuing move interrupts Miravel R channel");

            // Miravel Q orb spawn + enemy deny + friendly pulse heal/damage
            var mq = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            MovePairTo(mq, new Vector2(-3f, 0f), new Vector2(3f, 0f));
            Assert(mq.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Miravel Q orb spawner");
            Step(mq, 1.15f);
            Assert(mq.Snapshot().orbs.Count >= 1, "Miravel Q must spawn orb after ranking Q");
            OrbSnap spawnedOrb = mq.Snapshot().orbs[0];
            mq.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = new Vector2(spawnedOrb.x, spawnedOrb.y) });
            bool sawDeny = false;
            float dtQ = 1f / 30f;
            for (int i = 0; i < 180 && mq.Snapshot().orbs.Count > 0; i++)
            {
                mq.Tick(dtQ);
                LaneSnapshot sq = mq.Snapshot();
                for (int e = 0; e < sq.events.Count; e++)
                {
                    if (sq.events[e].kind == LaneEventKind.LuisOrbDeny) sawDeny = true;
                }
            }
            Assert(sawDeny, "Enemy stepping onto Miravel orb must deny it");

            // Negative natural regeneration deals real HP decay and awards Miravel kill + permanent HealPower!
            var mNeg = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.LaserShooter, new LaneTuning
            {
                startingGold = 1500,
                passiveGoldPerSecond = 0f,
                waveFirstSpawnDelay = 999f,
            });
            Assert(mNeg.TryBuyItem(LaneTeam.Blue, 11, out _) && mNeg.TryBuyItem(LaneTeam.Blue, 11, out _), "2x Kindred Charm (+44% HealPower)");
            MovePairTo(mNeg, new Vector2(-1f, 0f), new Vector2(1.8f, 0f));
            Step(mNeg, 0.10f);
            HeroSnap drainedLaser = GetHero(mNeg.Snapshot(), LaneTeam.Red);
            Assert(drainedLaser.netRegenRate < -2.0f,
                $"Miravel drain aura must push enemy natural regen negative (netRegen={drainedLaser.netRegenRate:0.00}/s)");

            // Bring LaserShooter to low HP cleanly then verify negative natural regen alone ticks Laser to 0 HP & grants takedown!
            HeroSnap preDotWait = drainedLaser;
            Step(mNeg, 4.0f);
            HeroSnap postDotWait = GetHero(mNeg.Snapshot(), LaneTeam.Red);
            Assert(postDotWait.hp < preDotWait.hp - 8.0f,
                $"Negative regen must reduce enemy HP passively over time ({preDotWait.hp:0.0} -> {postDotWait.hp:0.0})");

            // Chip Laser low via basics then back off (still in aura, no Q orbs) so negative regen scores the lethal tick!
            mNeg.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            for (int tick = 0; tick < 360; tick++)
            {
                mNeg.Tick(1f / 30f);
                HeroSnap rNow = GetHero(mNeg.Snapshot(), LaneTeam.Red);
                if (rNow.hp <= 65f)
                {
                    mNeg.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
                    break;
                }
            }
            Step(mNeg, 0.45f); // flush any in-flight basic projectile
            HeroSnap beforeDrainFinish = GetHero(mNeg.Snapshot(), LaneTeam.Red);
            Assert(beforeDrainFinish.phase != LanePhase.Dead && beforeDrainFinish.hp > 0f,
                $"Target must survive attack chip so negative regen delivers the lethal tick (hp={beforeDrainFinish.hp:0.0})");
            float mirHealBeforeKill = GetHero(mNeg.Snapshot(), LaneTeam.Blue).healPower;
            Step(mNeg, 24.0f); // Stand in aura while passive negative regen finishes remaining HP cleanly
            HeroSnap laserKilled = GetHero(mNeg.Snapshot(), LaneTeam.Red);
            HeroSnap mirAfterKill = GetHero(mNeg.Snapshot(), LaneTeam.Blue);
            Assert(laserKilled.phase == LanePhase.Dead && mirAfterKill.kills >= 1 && mirAfterKill.healPower > mirHealBeforeKill + 0.05f,
                $"Lethal negative regen must kill target, credit Miravel, and grant permanent HealPower (dead={laserKilled.phase}, kills={mirAfterKill.kills}, hpwr {mirHealBeforeKill:0.00}->{mirAfterKill.healPower:0.00})");

            // Miravel R HealPower & INT scaling checks (baseline 100% vs +HealPower & +INT)
            var mRBase = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f, startingGold = 1500, passiveGoldPerSecond = 0f });
            var mRHeal = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f, startingGold = 1500, passiveGoldPerSecond = 0f });
            Assert(mRHeal.TryBuyItem(LaneTeam.Blue, 11, out _) && mRHeal.TryBuyItem(LaneTeam.Blue, 11, out _), "2x Kindred Charm (+44% HPwr)");
            Assert(mRBase.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Miravel R");
            Assert(mRHeal.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Miravel R");
            MovePairTo(mRBase, new Vector2(-2f, 0f), new Vector2(0f, 0f));
            MovePairTo(mRHeal, new Vector2(-2f, 0f), new Vector2(0f, 0f));
            mRBase.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = Vector2.zero });
            mRHeal.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = Vector2.zero });
            Step(mRBase, 2.0f);
            Step(mRHeal, 2.0f);
            Assert(GetHero(mRHeal.Snapshot(), LaneTeam.Red).damageTakenMult > GetHero(mRBase.Snapshot(), LaneTeam.Red).damageTakenMult + 0.03f,
                "Bonus Heal Power must amplify Miravel R enemy vulnerability above 100% baseline");
            Assert(GetHero(mRHeal.Snapshot(), LaneTeam.Blue).healReceivedMult > GetHero(mRBase.Snapshot(), LaneTeam.Blue).healReceivedMult + 0.03f,
                "Bonus Heal Power must amplify Miravel R allied healing amplification");

            // Check Miravel Q spawn interval scales inversely with Ability Haste below old 4.0s cap
            var mQAh = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, new LaneTuning { startingGold = 1500, waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f });
            for (int k = 0; k < 5; k++) Assert(mQAh.TryBuyItem(LaneTeam.Blue, 6, out _), "110 AH");
            Assert(mQAh.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Q");
            MovePairTo(mQAh, new Vector2(-3f, 0f), new Vector2(5f, 0f));
            Step(mQAh, 1.15f); // first orb spawns at t~1.0s
            float orbCd = GetHero(mQAh.Snapshot(), LaneTeam.Blue).orbCooldown;
            Assert(orbCd < 3.9f, $"Miravel Q orb spawn interval must scale cleanly with Ability Haste below 4.0s (got {orbCd:0.00}s)");

            // Check E Ward curse triggers when W Illusion takes hero damage (while Miravel keeps distance)
            var waveLoot = new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 0.05f, waveInterval = 20f, minionMaxHpMelee = 20f, minionMaxHpRanged = 20f, minionXpMelee = 100f };
            var mIll = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, waveLoot);
            Assert(mIll.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Miravel W");
            MovePairTo(mIll, new Vector2(-1.5f, 0f), new Vector2(18f, 0f));
            mIll.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMove, point = new Vector2(4f, 0f) });
            Step(mIll, 6.0f);
            HeroSnap m2 = GetHero(mIll.Snapshot(), LaneTeam.Blue);
            Assert(m2.level >= 2 && mIll.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Miravel must level to 2 and learn E for illusion ward test");
            Assert(mIll.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.Q), "Ogre Q");
            MovePairTo(mIll, new Vector2(-1.5f, 0f), new Vector2(4.2f, 0f));
            mIll.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(2.0f, 0f) });
            Step(mIll, 0.35f);
            mIll.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-10f, 0f) });
            mIll.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.zero });
            mIll.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = new Vector2(0f, 0f) });
            Step(mIll, 1.05f);
            Assert(GetHero(mIll.Snapshot(), LaneTeam.Red).curseTimer > 0f, "Damaging Miravel W illusion while E ward is active must curse attacker");
        }

        private static void CheckSwordsmanFullKitAndNegativeResist()
        {
            var t = new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f, startingGold = 1500, passiveGoldPerSecond = 0f };

            // 1. NEW W (Stance Temper) activates on 1st strike, pushing enemy Armor negative & scaling durations linearly with INT!
            var me = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            var meInt = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            Assert(meInt.TryBuyItem(LaneTeam.Blue, 10, out _) && meInt.TryBuyItem(LaneTeam.Blue, 10, out _), "+50 INT");
            Assert(me.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Swordsman W (Stance Temper)");
            Assert(meInt.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Swordsman W (Stance Temper INT)");
            MovePairTo(me, new Vector2(0f, 0f), new Vector2(1.6f, 0f));
            MovePairTo(meInt, new Vector2(0f, 0f), new Vector2(1.6f, 0f));

            // Attack long enough for 1st strike (Hit 1 at ~0.20s -> W Stance Temper immediately!)
            me.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            meInt.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(me, 0.28f);
            Step(meInt, 0.28f);
            me.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            meInt.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            HeroSnap swHit1 = GetHero(me.Snapshot(), LaneTeam.Blue);
            HeroSnap swIntHit1 = GetHero(meInt.Snapshot(), LaneTeam.Blue);
            HeroSnap mirHit1 = GetHero(me.Snapshot(), LaneTeam.Red);
            Assert(swHit1.swordStacks == 1 && mirHit1.armor <= 0.1f,
                $"Swapped W (Stance Temper) must apply on 1st Light strike and shred Miravel Armor (swStacks={swHit1.swordStacks}, mirArm={mirHit1.armor})");
            Assert(swIntHit1.swordStackTimer > swHit1.swordStackTimer + 3.0f,
                $"INT must linearly extend W Stance Temper buff/debuff duration ({swIntHit1.swordStackTimer:0.0}s vs {swHit1.swordStackTimer:0.0}s)");

            // Keep attacking for full stack negative-armor check
            me.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(me, 2.8f);
            HeroSnap sw = GetHero(me.Snapshot(), LaneTeam.Blue);
            HeroSnap mir = GetHero(me.Snapshot(), LaneTeam.Red);
            Assert(sw.swordStacks >= 2 && mir.armor < 0f && LaneMatch.ResistanceFactor(mir.armor) > 1.0f,
                $"Swordsman W stacks must shred Armor negative ({mir.armor}) & stay at {sw.swordStacks} self stacks");

            // 2. NEW E (Vital Draw) activates ONLY on 2nd Heavy strike; interruption before Hit 2 denies healing!
            var mHealTest = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(mHealTest.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Swordsman E (Vital Draw)");
            Assert(mHealTest.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.Q), "Ogre Q to damage & interrupt Swordsman");
            MovePairTo(mHealTest, new Vector2(0f, 0f), new Vector2(1.6f, 0f));
            mHealTest.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = Vector2.zero });
            Step(mHealTest, 0.95f); // Ogre chips & stuns Swordsman
            Step(mHealTest, 1.25f);
            float hpChipped = GetHero(mHealTest.Snapshot(), LaneTeam.Blue).hp;
            Assert(hpChipped < 560f, "Swordsman took damage first to verify E healing");

            // Let Hit 1 fire at ~0.20s, STOP before Hit 2 (~0.60s) -> NO Vital Draw heal on Hit 1!
            mHealTest.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(mHealTest, 0.27f); // Hit 1 landed, still winding Hit 2
            float hpAfterHit1 = GetHero(mHealTest.Snapshot(), LaneTeam.Blue).hp;
            mHealTest.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop }); // cancel Hit 2!
            Assert(hpAfterHit1 <= hpChipped + 2.5f,
                $"Interrupted/Hit-1 Swordsman must NOT receive Vital Draw E healing (before={hpChipped:0.0}, afterHit1={hpAfterHit1:0.0})");

            // Now let full Twin-Blade complete through Hit 2 (~0.65s) -> Vital Draw E heals!
            mHealTest.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(mHealTest, 0.68f);
            float hpAfterHit2 = GetHero(mHealTest.Snapshot(), LaneTeam.Blue).hp;
            Assert(hpAfterHit2 > hpAfterHit1 + 15f,
                $"2nd Heavy strike must trigger E Vital Draw %HP dmg & heal ({hpAfterHit1:0.0} -> {hpAfterHit2:0.0})");

            // 3. Swordsman Q AGI windup only (fixed recovery) & R INT mark + AGI dash + STR execute reset!
            var mr = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            Assert(mr.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Swordsman R");
            MovePairTo(mr, new Vector2(0f, 0f), new Vector2(1.7f, 0f));
            mr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(mr, 0.35f);
            Assert(GetHero(mr.Snapshot(), LaneTeam.Blue).swordMarkedTargetId == 2, "Swordsman attack must mark R target");

            Step(mr, 5.2f);
            HeroSnap mirLow = GetHero(mr.Snapshot(), LaneTeam.Red);
            Assert(mirLow.phase != LanePhase.Dead, "Miravel must still be alive before Swordsman R execute slash");
            mr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = Vector2.zero });
            Step(mr, 0.7f);
            HeroSnap mirEnd = GetHero(mr.Snapshot(), LaneTeam.Red);
            HeroSnap swEnd = GetHero(mr.Snapshot(), LaneTeam.Blue);
            Assert(mirEnd.phase == LanePhase.Dead, "Swordsman R must kill/execute low Miravel");
            AssertClose(swEnd.cdR, 0f, "Swordsman R execute/kill must reset R cooldown to 0");

            // 4. Swordsman Q->R combo + AGI speeds Q windup without reducing fixed Q recovery
            var mQR = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, new LaneTuning
            {
                ultMinLevel = 1,
                waveFirstSpawnDelay = 0.05f,
                minionMaxHpMelee = 55f,
                minionMaxHpRanged = 55f,
                minionXpMelee = 200f,
                passiveGoldPerSecond = 0f,
            });
            Assert(mQR.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Q on Swordsman Q->R check");
            MovePairTo(mQR, new Vector2(0f, 0f), new Vector2(6.5f, 0f));
            float dt = 1f / 30f;
            for (int i = 0; i < 240; i++)
            {
                mQR.Tick(dt);
                MinionSnap c = FindFirstEnemyMinion(mQR.Snapshot(), LaneTeam.Blue);
                if (c != null && Mathf.Abs(c.x) <= 2.1f) break;
            }
            MinionSnap firstCreep = FindFirstEnemyMinion(mQR.Snapshot(), LaneTeam.Blue);
            Assert(firstCreep != null, "Minion arrived for level-up kill");
            mQR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMinion, targetId = firstCreep.id, point = new Vector2(firstCreep.x, firstCreep.y) });
            Step(mQR, 0.70f);
            Assert(GetHero(mQR.Snapshot(), LaneTeam.Blue).level >= 2 && mQR.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R),
                "Swordsman reaches lvl 2 and unlocks R for Q->R check");

            MovePairTo(mQR, new Vector2(0f, 0f), new Vector2(3.1f, 0f));
            Assert(GetHero(mQR.Snapshot(), LaneTeam.Blue).swordMarkedTargetId != 2, "Red Miravel not yet R-marked at 3.1u");
            mQR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(3.1f, 0f) });
            Step(mQR, 0.35f);
            HeroSnap postQ = GetHero(mQR.Snapshot(), LaneTeam.Blue);
            Assert(postQ.swordMarkedTargetId == 2,
                "Swordsman Q at 3.1u must apply R mark to enemy hero (even with creeps nearby)");
            AssertClose(postQ.phaseDuration, mQR.Tuning.swordsmanQRecover, "Swordsman Q recovery must stay fixed (not reduced by AGI)");
            float redHpAfterQ = GetHero(mQR.Snapshot(), LaneTeam.Red).hp;
            mQR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(3.1f, 0f) });
            Step(mQR, 0.70f);
            float redHpAfterR = GetHero(mQR.Snapshot(), LaneTeam.Red).hp;
            Assert(redHpAfterR < redHpAfterQ - 70f,
                $"Swordsman R after Q-mark must dash and slash enemy hero ({redHpAfterQ:0} -> {redHpAfterR:0})");
        }

        private static void CheckWavesShopTowerAndVictory()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 0.2f, startingGold = 350, passiveGoldPerSecond = 0f };
            var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);

            // Shop rejected mid-lane, allowed inside Fountain
            MovePairTo(m, new Vector2(0f, 0f), new Vector2(18f, 0f));
            Assert(!m.TryBuyItem(LaneTeam.Blue, 1, out string midErr), $"Mid-lane purchase must fail (got: {midErr})");
            MovePairTo(m, t.blueFountainPos, new Vector2(18f, 0f));
            Assert(m.TryBuyItem(LaneTeam.Blue, 1, out _), "Fountain purchase must succeed");

            // Waves spawn & tower prioritizes minions before hero
            Step(m, 0.4f);
            Assert(m.Snapshot().minions.Count >= 6, "First creep wave must spawn for both sides");

            // Verify tower targets enemy minions first even when enemy hero is also inside tower range
            var mTower = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, new LaneTuning { waveFirstSpawnDelay = 0.05f, ogreECooldown = 1.5f, minionMaxHpMelee = 90f, minionMaxHpRanged = 90f });
            Assert(mTower.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Ogre E to push wave");
            MovePairTo(mTower, new Vector2(1.0f, 0f), new Vector2(20f, 0f));
            bool orderedIntoTower = false;
            bool checkedPriority = false;
            float tStep = 0f;
            while (tStep < 18f)
            {
                mTower.Tick(1f / 30f);
                tStep += 1f / 30f;
                LaneSnapshot st = mTower.Snapshot();
                HeroSnap blueHero = GetHero(st, LaneTeam.Blue);

                bool anyRedCreep = false;
                bool blueUnderTower = false;
                for (int mi = 0; mi < st.minions.Count; mi++)
                {
                    if (st.minions[mi].team == LaneTeam.Red)
                    {
                        anyRedCreep = true;
                        if (blueHero.cdE <= 0f && Vector2.Distance(new Vector2(blueHero.x, blueHero.y), new Vector2(st.minions[mi].x, st.minions[mi].y)) <= 2.35f)
                        {
                            mTower.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.zero });
                        }
                    }
                    else if (st.minions[mi].team == LaneTeam.Blue && st.minions[mi].x > 5.0f)
                    {
                        blueUnderTower = true;
                    }
                }
                if (!anyRedCreep && blueUnderTower && !orderedIntoTower)
                {
                    orderedIntoTower = true;
                    mTower.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(7.5f, 0f) });
                }

                TowerSnap redTw = st.towers[1];
                if (orderedIntoTower && redTw.targetId > 0)
                {
                    Assert(redTw.targetKind == (int)LaneTargetKind.Minion, $"Outer tower targeted {redTw.targetKind} instead of Minion");
                    checkedPriority = true;
                    break;
                }
            }
            Assert(checkedPriority, "Outer tower must prioritize enemy minions inside range when hero is also in range");

            // Verify outer tower fall triggers victory cleanly
            var mWin = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, new LaneTuning { towerMaxHp = 90f, waveFirstSpawnDelay = 999f });
            MovePairTo(mWin, t.redTowerPos + new Vector2(-1.8f, 0f), t.redFountainPos);
            mWin.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackTower, targetId = 11 });
            Step(mWin, 2.5f);
            Assert(mWin.IsGameOver && mWin.WinnerTeam == (int)LaneTeam.Blue, "Destroying Red outer tower must declare Blue victory");
        }

        private static void MovePairTo(LaneMatch m, Vector2 bluePos, Vector2 redPos)
        {
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = bluePos });
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = redPos });

            float maxWait = 12.0f;
            float dt = 1f / 30f;
            int steps = Mathf.CeilToInt(maxWait / dt);
            bool bDone = false, rDone = false;
            HeroSnap b = null, r = null;
            for (int i = 0; i < steps; i++)
            {
                LaneSnapshot s = m.Snapshot();
                b = GetHero(s, LaneTeam.Blue);
                r = GetHero(s, LaneTeam.Red);
                if (!bDone && Vector2.Distance(new Vector2(b.x, b.y), bluePos) < 0.22f)
                {
                    bDone = true;
                    m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
                }
                if (!rDone && Vector2.Distance(new Vector2(r.x, r.y), redPos) < 0.22f)
                {
                    rDone = true;
                    m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Stop });
                }
                if (bDone && rDone) break;
                m.Tick(dt);
            }
            Assert(bDone && rDone, $"MovePairTo did not reach targets: blue=({b?.x:0.00},{b?.y:0.00})->({bluePos.x:0.00},{bluePos.y:0.00}) red=({r?.x:0.00},{r?.y:0.00})->({redPos.x:0.00},{redPos.y:0.00})");
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Stop });
        }

        private static void Step(LaneMatch m, float seconds)
        {
            float dt = 1f / 30f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(seconds / dt));
            for (int i = 0; i < steps; i++)
            {
                m.Tick(dt);
            }
        }

        private static bool HasNoHardCapFormulaText(string spec) =>
            !string.IsNullOrEmpty(spec) &&
            !spec.Contains(", max ") &&
            !spec.Contains("max 45%") &&
            !spec.Contains("capped at");

        private static void CheckHeroSheetCoversAllHeroesAndLiveScaling()
        {
            var t = new LaneTuning { startingGold = 1200, passiveGoldPerSecond = 0f };
            string ogreSpec = LaneHeroSheet.BuildHeroSheet(LaneHeroId.Ogre, t, null);
            string miravelSpec = LaneHeroSheet.BuildHeroSheet(LaneHeroId.Miravel, t, null);
            string swordsmanSpec = LaneHeroSheet.BuildHeroSheet(LaneHeroId.Swordsman, t, null);
            string laserSpec = LaneHeroSheet.BuildHeroSheet(LaneHeroId.LaserShooter, t, null);

            Assert(HasNoHardCapFormulaText(ogreSpec) && ogreSpec.Contains("135% AD") && ogreSpec.Contains("Unbounded") &&
                   ogreSpec.Contains("Global Effective AS") && ogreSpec.Contains("Outer Shockwave") &&
                   ogreSpec.Contains("/ 10 STR") && ogreSpec.Contains("@ 25") && ogreSpec.Contains("Ability Haste"),
                "Ogre hero sheet must list explicit AD/AGI(sqrt)/AS(sqrt)/STR curves, dual-zone Q & breakpoints without opaque hard caps");
            Assert(HasNoHardCapFormulaText(miravelSpec) && miravelSpec.Contains("INT") && miravelSpec.Contains("/ 10 INT") &&
                   miravelSpec.Contains("@ 25") && miravelSpec.Contains("Ability Haste") && miravelSpec.Contains("Coward's Shield") &&
                   miravelSpec.Contains("starts 100%") && miravelSpec.Contains("NEGATIVE") && miravelSpec.Contains("Kindred Charm"),
                "Miravel hero sheet must list explicit HealPower/INT curves, negative-regen passive, Kindred Charm, Ability Haste, and Coward's Shield rules");
            Assert(HasNoHardCapFormulaText(swordsmanSpec) && swordsmanSpec.Contains("[W (1ST HIT)] Stance Temper") &&
                   swordsmanSpec.Contains("[E (2ND HIT)] Vital Draw") && swordsmanSpec.Contains("STR") &&
                   swordsmanSpec.Contains("/ 10 STR") && swordsmanSpec.Contains("@ 25") && swordsmanSpec.Contains("AD") &&
                   swordsmanSpec.Contains("Ability Haste") && swordsmanSpec.Contains("Coward's Shield"),
                "Swordsman hero sheet must list swapped W/E, Twin-Blade AD/AGI/INT/STR/HealPower scaling, Ability Haste, and Coward's Shield");
            Assert(HasNoHardCapFormulaText(laserSpec) && laserSpec.Contains("7.2/6.5/5.8s") && laserSpec.Contains("0.88s") &&
                   laserSpec.Contains("INT") && laserSpec.Contains("AGI") && laserSpec.Contains("rank-only") &&
                   laserSpec.Contains("Ability Haste") && laserSpec.Contains("Coward's Shield"),
                "Chain Laser Shooter hero sheet must list INT Q speed / W radius / R duration, rank-only E stun, AGI step/MS, Ability Haste, and Coward's Shield");
            Assert(swordsmanSpec.Contains("2.1s") && swordsmanSpec.Contains("CD 5.5/5.0/4.5s") &&
                   swordsmanSpec.Contains("5.0s") && !swordsmanSpec.Contains("for 21s") &&
                   !swordsmanSpec.Contains("within 51s") && !swordsmanSpec.Contains("CD 61/"),
                "Swordsman hero sheet must format decimals cleanly");
            Assert(ogreSpec.Contains("CD 6.8/6.2/5.6s") && ogreSpec.Contains("CD 8.5/7.7/6.9s"),
                "Ogre hero sheet must format cooldown decimals cleanly");
            Assert(miravelSpec.Contains("+2.2") && miravelSpec.Contains("-2.6") && miravelSpec.Contains("Channel 4.2s"),
                "Miravel hero sheet must format aura & channel decimals cleanly");

            var match = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            HeroSnap before = GetHero(match.Snapshot(), LaneTeam.Blue);
            string liveBefore = LaneHeroSheet.BuildHeroSheet(LaneHeroId.Ogre, t, before);
            Assert(liveBefore.Contains("Wind 1.00x / Rec 1.00x") && liveBefore.Contains("0.88s/0.36s"),
                $"Ogre live level-1 sheet should show 1.00x windup/recover speeds and 0.88s/0.36s Q timing");

            Assert(match.TryBuyItem(LaneTeam.Blue, 1, out _), "Buy Iron Greatsword (+26 AD)");
            Assert(match.TryBuyItem(LaneTeam.Blue, 2, out _), "Buy Quickstep Dagger (+30% AS)");
            Assert(match.TryBuyItem(LaneTeam.Blue, 8, out _), "Buy Titan's Knuckle (+25 STR)");
            Assert(match.TryBuyItem(LaneTeam.Blue, 9, out _), "Buy Zephyr Feather (+25 AGI)");

            HeroSnap after = GetHero(match.Snapshot(), LaneTeam.Blue);
            string liveAfter = LaneHeroSheet.BuildHeroSheet(LaneHeroId.Ogre, t, after);
            Assert(liveAfter != liveBefore, "Hero sheet live output should update when items are purchased");
            var inv = CultureInfo.InvariantCulture;
            Assert(liveAfter.Contains($"Wind {after.ogreWindupSpeed.ToString("0.00", inv)}x / Rec {after.ogreRecoverSpeed.ToString("0.00", inv)}x"),
                "Ogre live sheet must reflect separate AGI windup speed and AS recovery speed");

            float qDmgAfter = t.ogreQDamageFormula.Evaluate(1, after.attackDamage);
            float qStunAfter = t.ogreQStunFormula.Evaluate(1, after.strength);
            float qWindupAfter = t.ogreQWindup / Mathf.Max(0.4f, after.ogreWindupSpeed);
            float qRecoverAfter = t.ogreQRecover / Mathf.Max(0.4f, after.ogreRecoverSpeed);
            Assert(liveAfter.Contains($"{qDmgAfter.ToString("0", inv)} central") && liveAfter.Contains($"{qStunAfter.ToString("0.00", inv)}s stun") &&
                   liveAfter.Contains($"{qWindupAfter.ToString("0.00", inv)}s/{qRecoverAfter.ToString("0.00", inv)}s"),
                "Ogre live sheet must match evaluated live Q damage, stun duration, and split AGI-windup/AS-recover times");

            // Stage A Readability Check: every hero must have 5 structured cards with PlainDescription (no formula math symbols),
            // KeyFacts, What Scales arrows, MathLines, and PracticalTips matching real formulas.
            foreach (LaneHeroId hid in new[] { LaneHeroId.Ogre, LaneHeroId.Miravel, LaneHeroId.Swordsman, LaneHeroId.LaserShooter })
            {
                LaneHeroSheet.HeroGuideData gd = LaneHeroSheet.BuildHeroGuide(hid, t, null);
                Assert(gd.Cards.Count == 5 && !string.IsNullOrEmpty(gd.IdentitySummary), $"{hid} guide must provide IdentitySummary and 5 ability cards.");
                foreach (LaneHeroSheet.AbilityCardData card in gd.Cards)
                {
                    Assert(!string.IsNullOrEmpty(card.PlainDescription) &&
                           !card.PlainDescription.Contains("sqrt(") &&
                           !card.PlainDescription.Contains("S/(S+") &&
                           !card.PlainDescription.Contains("100/("),
                        $"{hid} {card.KeyBadge} PlainDescription must explain ability cleanly without math formulas.");
                    Assert(card.KeyFacts.Count >= 1 && card.ScalingRows.Count >= 1 && card.MathLines.Count >= 1 && !string.IsNullOrEmpty(card.PracticalTip),
                        $"{hid} {card.KeyBadge} missing KeyFacts, ScalingRows, MathLines, or PracticalTip.");
                }
            }
        }

        private static void CheckAbilityPreviewsAndInteractiveSandbox()
        {
            var t = new LaneTuning { startingGold = 1200, passiveGoldPerSecond = 0f };
            var liveMatch = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            HeroSnap liveBefore = GetHero(liveMatch.Snapshot(), LaneTeam.Blue);

            using (var preview = new LaneAbilityPreview(t, enableRenderVisuals: false))
            {
                LaneHeroId[] heroes = { LaneHeroId.Ogre, LaneHeroId.Miravel, LaneHeroId.Swordsman, LaneHeroId.LaserShooter };
                LaneAbilitySlot[] slots = { LaneAbilitySlot.None, LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E, LaneAbilitySlot.R };

                foreach (LaneHeroId hero in heroes)
                {
                    foreach (LaneAbilitySlot slot in slots)
                    {
                        string[] exNames = LaneAbilityPreview.GetExampleNames(hero, slot);
                        for (int ex = 0; ex < exNames.Length; ex++)
                        {
                            preview.Select(hero, slot, ex);
                            Assert(!string.IsNullOrEmpty(preview.ScenarioCaption), $"Preview caption missing for {hero} {slot} ex {ex}");

                            int steps = Mathf.CeilToInt((preview.LoopDuration + 0.25f) * 30f);
                            for (int i = 0; i < steps; i++)
                            {
                                preview.Tick(1f / 30f, shouldRun: true);
                            }

                            LaneSnapshot pSnap = preview.LatestPreviewSnapshot;
                            Assert(pSnap != null && pSnap.heroes.Count == 2, $"Preview snapshot invalid for {hero} {slot}");
                            for (int hi = 0; hi < pSnap.heroes.Count; hi++)
                            {
                                HeroSnap hs = pSnap.heroes[hi];
                                Assert(!float.IsNaN(hs.hp) && !float.IsNaN(hs.x) && !float.IsNaN(hs.y), $"Preview NaN in {hero} {slot}");
                            }
                        }
                    }
                }

                // Interactive stat override reflects real formulas without touching live match state
                preview.Select(LaneHeroId.Ogre, LaneAbilitySlot.E, 0);
                PreviewStatOverrides ov = LaneAbilityPreview.DefaultOverrides(LaneAbilitySlot.E);
                ov.bonusStrength = 60f;
                ov.bonusAttackDamage = 40f;
                preview.ApplyLiveOverrides(ov);
                preview.Tick(1f / 30f, shouldRun: true);

                HeroSnap previewSnap = LaneHeroSheet.EvaluatePreviewHeroSnap(LaneHeroId.Ogre, t, ov);
                AssertClose(previewSnap.strength, 60f, "Preview hero snap applies bonus STR");
                AssertClose(previewSnap.attackDamage, liveBefore.attackDamage + 40f, "Preview hero snap applies bonus AD");

                HeroSnap liveAfter = GetHero(liveMatch.Snapshot(), LaneTeam.Blue);
                AssertClose(liveAfter.strength, liveBefore.strength, "Preview overrides must never mutate separate active live match");
                AssertClose(liveAfter.hp, liveBefore.hp, "Preview stepping must never mutate separate active live match HP");

                // Only relevant sliders should be shown per ability
                List<PreviewSliderSpec> mirWSliders = LaneAbilityPreview.GetRelevantSliders(LaneHeroId.Miravel, LaneAbilitySlot.W);
                bool hasAh = mirWSliders.Exists(s => s.Field == PreviewStatField.AbilityHaste);
                bool hasRcr = mirWSliders.Exists(s => s.Field == PreviewStatField.ResourceCostReduction);
                bool hasIrrelevantStat = mirWSliders.Exists(s =>
                    s.Field == PreviewStatField.AttackDamage ||
                    s.Field == PreviewStatField.Strength ||
                    s.Field == PreviewStatField.Agility ||
                    s.Field == PreviewStatField.Intelligence);
                Assert(mirWSliders.Count == 4 && hasAh && hasRcr && !hasIrrelevantStat,
                    "Miravel W preview sliders must expose only Ability Haste & Mana/RCR controls (no irrelevant AD/STR/AGI/INT)");

                // Deactivating halts simulation ticks completely
                int ticksBeforeOff = preview.SimulationTickCount;
                Assert(ticksBeforeOff > 0, "Preview should advance ticks while active");
                preview.Tick(0.05f, shouldRun: false);
                preview.Tick(0.05f, shouldRun: false);
                Assert(!preview.IsActive && preview.SimulationTickCount == ticksBeforeOff,
                    "Disabling/closing preview must immediately halt preview simulation work");

                preview.ResetOverrides();
                AssertClose(preview.Overrides.bonusStrength, 0f, "ResetOverrides clears bonus stats");
            }

            // Personal preference persistence for enableAbilityDemos
            string tempSettingsPath = Path.Combine(Path.GetTempPath(), "moba_preview_pref_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new PlayerSettingsStore(tempSettingsPath, persistToDisk: true);
                Assert(store.Data.enableAbilityDemos, "Ability demos enabled by default");
                store.SetAbilityDemosEnabled(false);

                var reloaded = new PlayerSettingsStore(tempSettingsPath, persistToDisk: true);
                reloaded.Load();
                Assert(!reloaded.Data.enableAbilityDemos, "Ability demos disable setting must persist locally");

                reloaded.RestoreDefaults();
                Assert(reloaded.Data.enableAbilityDemos, "RestoreDefaults re-enables ability demos");
            }
            finally
            {
                if (File.Exists(tempSettingsPath)) File.Delete(tempSettingsPath);
            }
        }

        private static void CheckRepeatedMoveClicksPreserveSpeed()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, startingGold = 0, passiveGoldPerSecond = 0f };
            var m = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            MovePairTo(m, new Vector2(-16f, 0f), new Vector2(18f, 0f));

            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(14f, 0f) });
            Step(m, 0.70f);
            HeroSnap moving = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(moving.speed >= t.ogreSpeed * 0.90f, $"Ogre should reach near full move speed (got {moving.speed:0.00})");

            // Repeated rapid slightly-jittered clicks along walk path must NOT reset speed back to 0!
            for (int i = 0; i < 10; i++)
            {
                m.EnqueueOrApplyOrder(new LaneOrder
                {
                    team = LaneTeam.Blue,
                    kind = LaneOrderKind.Move,
                    point = new Vector2(14f, (i % 2 == 0) ? 0.25f : -0.25f),
                });
                Step(m, 0.10f);
                HeroSnap cur = GetHero(m.Snapshot(), LaneTeam.Blue);
                Assert(cur.speed >= cur.moveSpeed * 0.85f,
                    $"Repeated move click #{i} slowed hero down unexpectedly (speed={cur.speed:0.00}, max={cur.moveSpeed:0.00})");
            }

            // Swordsman basic-attack backswing (Recover) is immediately move-cancelable
            var ms = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            MovePairTo(ms, new Vector2(0f, 0f), new Vector2(1.8f, 0f));
            ms.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            float dt = 1f / 30f;
            bool enteredRecover = false;
            for (int i = 0; i < 60; i++)
            {
                ms.Tick(dt);
                HeroSnap b = GetHero(ms.Snapshot(), LaneTeam.Blue);
                if (b.phase == LanePhase.Recover && b.activeSlot == LaneAbilitySlot.None)
                {
                    enteredRecover = true;
                    break;
                }
            }
            Assert(enteredRecover, "Swordsman should enter basic-attack Recover backswing");
            ms.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-10f, 0f) });
            Step(ms, 0.05f);
            HeroSnap canceled = GetHero(ms.Snapshot(), LaneTeam.Blue);
            Assert(canceled.phase == LanePhase.Idle,
                $"Move click during basic backswing must cancel Recover immediately (phase={canceled.phase})");
            // Wait past 180-deg turn slowdown (480 deg/s => ~0.19s) to confirm immediate acceleration away
            Step(ms, 0.25f);
            HeroSnap accelerated = GetHero(ms.Snapshot(), LaneTeam.Blue);
            Assert(accelerated.x < -0.25f && accelerated.speed > 1.0f,
                $"Hero must accelerate cleanly after backswing cancel turn (x={accelerated.x:0.00}, speed={accelerated.speed:0.00})");
        }

        private static void CheckTowerAggrosHeroOnDamage()
        {
            var t = new LaneTuning
            {
                startingGold = 500,
                waveFirstSpawnDelay = 0.05f,
                minionMaxHpMelee = 280f,
                minionMaxHpRanged = 180f,
            };
            var m = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            Assert(m.TryBuyItem(LaneTeam.Blue, 13, out _), "Buy Hatchet for wave clear");
            Assert(m.TryBuyItem(LaneTeam.Blue, 1, out _), "Buy Iron Greatsword");
            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Swordsman Q for tower aggro trigger");
            MovePairTo(m, new Vector2(1.0f, 0f), new Vector2(18f, 0f));

            bool minionsUnderRedTower = false;
            float dt = 1f / 30f;
            for (int i = 0; i < 600; i++)
            {
                m.Tick(dt);
                LaneSnapshot s = m.Snapshot();
                HeroSnap blueHero = GetHero(s, LaneTeam.Blue);
                bool hasRedCreep = false;
                bool hasBlueCreepUnderTower = false;
                MinionSnap closestRed = null;
                float bestD = float.MaxValue;
                for (int mi = 0; mi < s.minions.Count; mi++)
                {
                    MinionSnap mn = s.minions[mi];
                    if (mn.team == LaneTeam.Red)
                    {
                        hasRedCreep = true;
                        float d = Vector2.Distance(new Vector2(blueHero.x, blueHero.y), new Vector2(mn.x, mn.y));
                        if (d < bestD) { bestD = d; closestRed = mn; }
                    }
                    else if (mn.team == LaneTeam.Blue && mn.x >= 5.2f)
                    {
                        hasBlueCreepUnderTower = true;
                    }
                }
                if (hasRedCreep && closestRed != null && (blueHero.attackTargetId == 0 || blueHero.phase == LanePhase.Idle))
                {
                    m.EnqueueOrApplyOrder(new LaneOrder
                    {
                        team = LaneTeam.Blue,
                        kind = LaneOrderKind.AttackMinion,
                        targetId = closestRed.id,
                        point = new Vector2(closestRed.x, closestRed.y),
                    });
                }

                TowerSnap redTw = s.towers[1];
                if (!hasRedCreep && hasBlueCreepUnderTower && redTw.targetKind == (int)LaneTargetKind.Minion)
                {
                    minionsUnderRedTower = true;
                    break;
                }
            }
            Assert(minionsUnderRedTower, "Blue wave must establish minion tanking under Red tower first");

            // Position Blue Swordsman & Red Miravel inside Red tower protection while Blue minions tank
            MovePairTo(m, new Vector2(7.5f, 0f), new Vector2(9.2f, 0f));
            TowerSnap beforeHit = m.Snapshot().towers[1];
            Assert(beforeHit.targetKind == (int)LaneTargetKind.Minion && !beforeHit.aggroLockedOnHero,
                $"Tower should still target minions before hero damage (targetKind={beforeHit.targetKind}, locked={beforeHit.aggroLockedOnHero})");

            // Blue Swordsman Q cleaves Red Miravel under Red tower -> Red tower MUST snap aggro onto Blue Swordsman
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(9.2f, 0f) });
            bool sawAggroEvent = false;
            for (int i = 0; i < 30; i++)
            {
                m.Tick(dt);
                LaneSnapshot s = m.Snapshot();
                for (int e = 0; e < s.events.Count; e++)
                {
                    if (s.events[e].kind == LaneEventKind.TowerAggro) sawAggroEvent = true;
                }
                if (s.towers[1].aggroLockedOnHero) break;
            }

            LaneSnapshot afterHit = m.Snapshot();
            HeroSnap blueSwordsman = GetHero(afterHit, LaneTeam.Blue);
            TowerSnap lockedTw = afterHit.towers[1];
            Assert(sawAggroEvent, "Damaging enemy hero under enemy tower must emit TowerAggro event");
            Assert(lockedTw.aggroLockedOnHero && lockedTw.targetKind == (int)LaneTargetKind.Hero && lockedTw.targetId == blueSwordsman.id,
                $"Tower must switch aggro onto attacking hero (locked={lockedTw.aggroLockedOnHero}, kind={lockedTw.targetKind}, id={lockedTw.targetId})");

            // Leave tower range -> aggroLockedOnHero clears cleanly
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-3f, 0f) });
            Step(m, 3.0f);
            TowerSnap afterEscape = m.Snapshot().towers[1];
            Assert(!afterEscape.aggroLockedOnHero && afterEscape.targetKind != (int)LaneTargetKind.Hero,
                $"Tower must clear hero aggro lock after attacker leaves range (locked={afterEscape.aggroLockedOnHero}, kind={afterEscape.targetKind})");
        }

        private static void CheckMiravelECastKeepsInvisibility()
        {
            // Grant XP so Miravel hits lvl 3 and can learn W, E, and R
            var ml = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, new LaneTuning
            {
                ultMinLevel = 1,
                waveFirstSpawnDelay = 0.05f,
                minionMaxHpMelee = 10f,
                minionMaxHpRanged = 10f,
                minionXpMelee = 250f,
                luisWDuration = 5.0f,
            });
            Assert(ml.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level W");
            MovePairTo(ml, new Vector2(-1f, 0f), new Vector2(18f, 0f));
            ml.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMove, point = new Vector2(2f, 0f) });
            Step(ml, 6.5f);
            HeroSnap leveled = GetHero(ml.Snapshot(), LaneTeam.Blue);
            Assert(leveled.level >= 3 && ml.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E) && ml.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R),
                $"Miravel needs W/E/R unlocked (lvl={leveled.level})");

            ml.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(4f, 0f) });
            Step(ml, 0.15f);
            Assert(GetHero(ml.Snapshot(), LaneTeam.Blue).invisible, "W grants invisibility");

            ml.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.zero });
            Step(ml, 0.15f);
            HeroSnap afterE = GetHero(ml.Snapshot(), LaneTeam.Blue);
            Assert(afterE.invisible && afterE.wardTimer > 0f,
                $"Miravel E cast must preserve W stealth (invis={afterE.invisible}, ward={afterE.wardTimer:0.00})");

            ml.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(3f, 0f) });
            Step(ml, 0.10f);
            Assert(!GetHero(ml.Snapshot(), LaneTeam.Blue).invisible, "Miravel R channel must break W stealth");
        }

        private static void CheckSellItemRefundAndNewItemPassives()
        {
            var inv = CultureInfo.InvariantCulture;
            var t = new LaneTuning { startingGold = 3000, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 999f };

            // 1. Sell 70% refund & fountain-only enforcement
            var mSell = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            Assert(mSell.TryBuyItem(LaneTeam.Blue, 13, out _), "Buy Miner's Hatchet (200g)");
            Assert(GetHero(mSell.Snapshot(), LaneTeam.Blue).gold == 2800, "Gold after buying 200g Hatchet");
            MovePairTo(mSell, new Vector2(0f, 0f), new Vector2(18f, 0f));
            Assert(!mSell.TrySellItem(LaneTeam.Blue, 0, out _), "Mid-lane item sell must fail");
            MovePairTo(mSell, t.blueFountainPos, new Vector2(18f, 0f));
            Assert(mSell.TrySellItem(LaneTeam.Blue, 0, out _), "Fountain item sell must succeed");
            HeroSnap soldSnap = GetHero(mSell.Snapshot(), LaneTeam.Blue);
            Assert(soldSnap.gold == 2940 && soldSnap.items[0] == 0,
                $"70% sell refund must restore +140g (gold={soldSnap.gold}, slot0={soldSnap.items[0]})");

            // 2. Miner's Hatchet (+18 minion dmg)
            var tw = new LaneTuning { startingGold = 500, passiveGoldPerSecond = 0f, waveFirstSpawnDelay = 0.05f };
            var mNoHatchet = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, tw);
            var mHatchet = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, tw);
            Assert(mHatchet.TryBuyItem(LaneTeam.Blue, 13, out _), "Buy Hatchet");
            MovePairTo(mNoHatchet, new Vector2(0f, 0f), new Vector2(20f, 0f));
            MovePairTo(mHatchet, new Vector2(0f, 0f), new Vector2(20f, 0f));
            float dt = 1f / 30f;
            for (int i = 0; i < 240; i++)
            {
                mNoHatchet.Tick(dt);
                mHatchet.Tick(dt);
                MinionSnap c0 = FindFirstEnemyMinion(mNoHatchet.Snapshot(), LaneTeam.Blue);
                MinionSnap c1 = FindFirstEnemyMinion(mHatchet.Snapshot(), LaneTeam.Blue);
                if (c0 != null && c1 != null && Mathf.Abs(c0.x) <= 2.1f && Mathf.Abs(c1.x) <= 2.1f) break;
            }
            MinionSnap mnBase = FindFirstEnemyMinion(mNoHatchet.Snapshot(), LaneTeam.Blue);
            MinionSnap mnHatchet = FindFirstEnemyMinion(mHatchet.Snapshot(), LaneTeam.Blue);
            Assert(mnBase != null && mnHatchet != null, "Minions present at midfield");
            float hpBeforeBase = mnBase.hp;
            float hpBeforeHat = mnHatchet.hp;
            mNoHatchet.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMinion, targetId = mnBase.id, point = new Vector2(mnBase.x, mnBase.y) });
            mHatchet.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMinion, targetId = mnHatchet.id, point = new Vector2(mnHatchet.x, mnHatchet.y) });
            Step(mNoHatchet, 0.35f);
            Step(mHatchet, 0.35f);
            float dmgNoHat = hpBeforeBase - FindMinionHp(mNoHatchet.Snapshot(), mnBase.id);
            float dmgHat = hpBeforeHat - FindMinionHp(mHatchet.Snapshot(), mnHatchet.id);
            Assert(dmgHat > dmgNoHat + 18f, $"Miner's Hatchet must add +18 bonus minion dmg ({dmgHat:0.0} vs {dmgNoHat:0.0})");

            // 3. Brawler's Buckler (-6 basic/minion block)
            var mBuckler = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Swordsman, t);
            Assert(mBuckler.TryBuyItem(LaneTeam.Red, 15, out _), "Swordsman buys Brawler's Buckler");
            MovePairTo(mBuckler, new Vector2(-2f, 0f), new Vector2(2f, 0f));
            mBuckler.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            float singleHitLoss = 0f;
            float prevHp = GetHero(mBuckler.Snapshot(), LaneTeam.Red).hp;
            for (int i = 0; i < 40; i++)
            {
                mBuckler.Tick(dt);
                float curHp = GetHero(mBuckler.Snapshot(), LaneTeam.Red).hp;
                if (prevHp - curHp > 5f)
                {
                    singleHitLoss = prevHp - curHp;
                    break;
                }
                prevHp = curHp;
            }
            // Miravel 48 AD vs 26 Armor => 38.10; minus 6 block => ~32.05
            Assert(singleHitLoss > 20f && singleHitLoss < 34.5f,
                $"Brawler's Buckler must block 6 flat basic dmg (observed={singleHitLoss.ToString("0.0", inv)})");

            // 4. Blighted Thorn (-35% healing/regen) & Woodland Salve (+4.5 HP/s after 5s OOC)
            var mHealClean = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            var mHealThorn = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            Assert(mHealThorn.TryBuyItem(LaneTeam.Blue, 16, out _), "Buy Blighted Thorn");
            MovePairTo(mHealClean, new Vector2(15.2f, 0f), new Vector2(16.8f, 0f));
            MovePairTo(mHealThorn, new Vector2(15.2f, 0f), new Vector2(16.8f, 0f));
            mHealClean.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            mHealThorn.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(mHealClean, 0.85f);
            Step(mHealThorn, 0.85f);
            Assert(GetHero(mHealThorn.Snapshot(), LaneTeam.Red).grievousTimer > 2.0f, "Blighted Thorn must inflict Grievous Wounds");
            // Step into outer edge of Red Fountain (center 21.2, radius 3.2 -> 18.8) keeping grievousTimer > 2.0s active
            MovePairTo(mHealClean, new Vector2(14.5f, 0f), new Vector2(18.8f, 0f));
            MovePairTo(mHealThorn, new Vector2(14.5f, 0f), new Vector2(18.8f, 0f));
            float hpC0 = GetHero(mHealClean.Snapshot(), LaneTeam.Red).hp;
            float hpT0 = GetHero(mHealThorn.Snapshot(), LaneTeam.Red).hp;
            Step(mHealClean, 1.0f);
            Step(mHealThorn, 1.0f);
            float healedClean = GetHero(mHealClean.Snapshot(), LaneTeam.Red).hp - hpC0;
            float healedThorn = GetHero(mHealThorn.Snapshot(), LaneTeam.Red).hp - hpT0;
            Assert(healedThorn < healedClean - 10f,
                $"Blighted Thorn -35% anti-heal must noticeably reduce fountain healing ({healedThorn:0.0} vs {healedClean:0.0})");

            var mSalve = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, t);
            Assert(mSalve.TryBuyItem(LaneTeam.Blue, 14, out _), "Buy Woodland Salve");
            MovePairTo(mSalve, new Vector2(0f, 0f), new Vector2(1.5f, 0f));
            Assert(mSalve.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.E), "Ogre E to chip Salve wearer");
            mSalve.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastE, point = Vector2.zero });
            Step(mSalve, 0.70f);
            mSalve.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-15f, 0f) });
            Step(mSalve, 5.2f);
            float salveBefore = GetHero(mSalve.Snapshot(), LaneTeam.Blue).hp;
            Step(mSalve, 1.0f);
            float salveGain = GetHero(mSalve.Snapshot(), LaneTeam.Blue).hp - salveBefore;
            Assert(salveGain > 5.0f, $"Woodland Salve must grant +4.5 HP/s after 5s out of combat (1s gain={salveGain:0.00})");

            // 5. Momentum Greaves (+MS out of combat, resets when hit)
            var mMom = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(mMom.TryBuyItem(LaneTeam.Blue, 17, out _), "Buy Momentum Greaves");
            MovePairTo(mMom, new Vector2(-18f, 0f), new Vector2(18f, 0f));
            Step(mMom, 2.2f); // Ensure lastHeroDamageAge > 2s
            mMom.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(16f, 0f) });
            Step(mMom, 2.5f);
            HeroSnap fastMom = GetHero(mMom.Snapshot(), LaneTeam.Blue);
            Assert(fastMom.momentumBonus > 0.35f, $"Momentum Greaves must ramp out-of-combat MS (got +{fastMom.momentumBonus:0.00})");

            // 6. Colossus Pauldron (CC shield + absorb)
            var mCol = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            Assert(mCol.TryBuyItem(LaneTeam.Blue, 18, out _), "Buy Colossus Pauldron");
            Assert(mCol.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Q");
            MovePairTo(mCol, new Vector2(0f, 0f), new Vector2(2f, 0f));
            mCol.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });
            Step(mCol, 0.95f);
            HeroSnap shielded = GetHero(mCol.Snapshot(), LaneTeam.Blue);
            Assert(shielded.shieldHp > 80f, $"Colossus Pauldron must grant STR shield when stunning target (shield={shielded.shieldHp:0})");
            float hpBeforeHit = shielded.hp;
            float shBeforeHit = shielded.shieldHp;
            Step(mCol, 1.3f); // wait Miravel stun end
            mCol.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackHero, targetId = 1 });
            Step(mCol, 0.8f);
            HeroSnap afterAbsorb = GetHero(mCol.Snapshot(), LaneTeam.Blue);
            Assert(afterAbsorb.shieldHp < shBeforeHit && afterAbsorb.hp >= hpBeforeHit - 5f,
                $"Colossus shield must absorb damage before HP drops (sh {shBeforeHit:0}->{afterAbsorb.shieldHp:0}, hp {hpBeforeHit:0}->{afterAbsorb.hp:0})");

            // 7. Spellweaver's Hourglass (Arcane Echo burst after cast)
            var mSw = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(mSw.TryBuyItem(LaneTeam.Blue, 19, out _), "Buy Spellweaver's Hourglass");
            Assert(mSw.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Level E");
            MovePairTo(mSw, new Vector2(-2f, 0f), new Vector2(2f, 0f));
            mSw.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.zero });
            Step(mSw, 0.05f);
            Assert(GetHero(mSw.Snapshot(), LaneTeam.Blue).spellweaverReady, "Spellweaver must arm after casting E");
            mSw.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            bool sawArcaneEcho = false;
            for (int i = 0; i < 40; i++)
            {
                mSw.Tick(dt);
                LaneSnapshot ss = mSw.Snapshot();
                for (int e = 0; e < ss.events.Count; e++)
                {
                    if (ss.events[e].kind == LaneEventKind.LuisCurseProc && ss.events[e].label.Contains("ARCANE"))
                    {
                        sawArcaneEcho = true;
                    }
                }
            }
            Assert(sawArcaneEcho && !GetHero(mSw.Snapshot(), LaneTeam.Blue).spellweaverReady,
                "Spellweaver's Hourglass must fire ARCANE echo magic burst on next hero hit and consume buff");

            // 8. Executioner's Grudge (+18% vs <40% HP)
            var mEx = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, new LaneTuning
            {
                startingGold = 2000,
                passiveGoldPerSecond = 0f,
                waveFirstSpawnDelay = 999f,
            });
            Assert(mEx.TryBuyItem(LaneTeam.Blue, 20, out _), "Buy Executioner's Grudge");
            MovePairTo(mEx, new Vector2(-3f, 0f), new Vector2(1f, 0f));
            mEx.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            float dmgHigh = 0f;
            float dmgLow = 0f;
            for (int i = 0; i < 400; i++)
            {
                HeroSnap ogBefore = GetHero(mEx.Snapshot(), LaneTeam.Red);
                mEx.Tick(dt);
                HeroSnap ogAfter = GetHero(mEx.Snapshot(), LaneTeam.Red);
                float drop = ogBefore.hp - ogAfter.hp;
                if (drop > 20f)
                {
                    if (dmgHigh == 0f && ogBefore.hp / ogBefore.maxHp > 0.70f) dmgHigh = drop;
                    if (ogBefore.hp / ogBefore.maxHp < 0.38f)
                    {
                        dmgLow = drop;
                        break;
                    }
                }
            }
            Assert(dmgHigh > 0f && dmgLow > dmgHigh * 1.15f,
                $"Executioner's Grudge must amp dmg by ~18% below 40% HP (low={dmgLow:0.0} vs high={dmgHigh:0.0})");
        }

        private static void CheckRepeatedAttackMoveSwordsmanBotAndOgreERadius()
        {
            float dt = 1f / 30f;

            // 1. Repeated 0.24s AttackMove spam near minions must preserve Swordsman windup and damage/kill minions
            var ma = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, new LaneTuning
            {
                waveFirstSpawnDelay = 0.05f,
                startingGold = 0,
                passiveGoldPerSecond = 0f,
            });
            MovePairTo(ma, new Vector2(0f, 0f), new Vector2(19f, 0f));

            bool creepArrived = false;
            for (int i = 0; i < 240; i++)
            {
                ma.Tick(dt);
                LaneSnapshot s = ma.Snapshot();
                HeroSnap blue = GetHero(s, LaneTeam.Blue);
                MinionSnap m = FindFirstEnemyMinion(s, LaneTeam.Blue);
                if (m != null && Vector2.Distance(new Vector2(blue.x, blue.y), new Vector2(m.x, m.y)) <= 2.3f)
                {
                    creepArrived = true;
                    break;
                }
            }
            Assert(creepArrived, "Red minion wave must arrive within melee range of Swordsman");

            float waveHpBefore = SumTeamMinionHp(ma.Snapshot(), LaneTeam.Red);
            int slashes = 0;
            for (int i = 0; i < 105; i++)
            {
                if (i % 7 == 0)
                {
                    ma.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMove, point = new Vector2(-1.5f, 0f) });
                }
                ma.Tick(dt);
                LaneSnapshot s = ma.Snapshot();
                for (int e = 0; e < s.events.Count; e++)
                {
                    if (s.events[e].kind == LaneEventKind.SwordsmanSlash && s.events[e].team == LaneTeam.Blue)
                    {
                        slashes++;
                    }
                }
            }
            LaneSnapshot afterSpam = ma.Snapshot();
            HeroSnap blueAfter = GetHero(afterSpam, LaneTeam.Blue);
            float waveHpLost = waveHpBefore - SumTeamMinionHp(afterSpam, LaneTeam.Red);
            Assert(slashes >= 4 && (blueAfter.creepScore >= 1 || waveHpLost >= 180f),
                $"Repeated 0.24s AttackMove must preserve Swordsman windup & damage minions (slashes={slashes}, CS={blueAfter.creepScore}, hpLost={waveHpLost:0.0})");

            // 2. AttackMove spam every tick during Recover cannot exploit-reset basic attack cooldown
            var mExp = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Ogre, new LaneTuning
            {
                waveFirstSpawnDelay = 999f,
                passiveGoldPerSecond = 0f,
            });
            MovePairTo(mExp, new Vector2(0f, 0f), new Vector2(1.8f, 0f));
            float ogStartHp = GetHero(mExp.Snapshot(), LaneTeam.Red).hp;
            int exploitSlashes = 0;
            for (int i = 0; i < 60; i++)
            {
                mExp.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMove, point = new Vector2(1.8f, 0f) });
                mExp.Tick(dt);
                LaneSnapshot s = mExp.Snapshot();
                for (int e = 0; e < s.events.Count; e++)
                {
                    if (s.events[e].kind == LaneEventKind.SwordsmanSlash && s.events[e].team == LaneTeam.Blue)
                    {
                        exploitSlashes++;
                    }
                }
            }
            float ogDealt = ogStartHp - GetHero(mExp.Snapshot(), LaneTeam.Red).hp;
            Assert(ogDealt > 80f && ogDealt < 180f && exploitSlashes <= 6,
                $"AttackMove spam must deal valid Twin-Blade dmg without skipping Recover backswing (dmg={ogDealt:0.0}, slashes={exploitSlashes})");

            // 3. Swordsman LaneBot actively farms minion CS instead of stalling at 0 CS
            var mb = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, new LaneTuning { passiveGoldPerSecond = 0f });
            var bot = new LaneBot(LaneTeam.Blue);
            int farmTicks = Mathf.RoundToInt(42f / dt);
            for (int i = 0; i < farmTicks; i++)
            {
                bot.Tick(mb, dt);
                mb.Tick(dt);
            }
            HeroSnap farmed = GetHero(mb.Snapshot(), LaneTeam.Blue);
            Assert(farmed.creepScore >= 3, $"Swordsman bot must farm lane minions cleanly (got CS={farmed.creepScore})");

            // 4. Ogre [E] tightened radius (2.45u) boundary verification
            var te = new LaneTuning { waveFirstSpawnDelay = 999f, startingGold = 0, passiveGoldPerSecond = 0f };
            Assert(te.ogreERadius < 2.6f, $"Ogre E radius must be tightened below 2.6u (got {te.ogreERadius:0.00})");

            var mOut = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, te);
            Assert(mOut.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Level Blue Ogre E (out check)");
            MovePairTo(mOut, Vector2.zero, new Vector2(3.15f, 0f));
            float outHpBefore = GetHero(mOut.Snapshot(), LaneTeam.Red).hp;
            mOut.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.zero });
            Step(mOut, 0.75f);
            HeroSnap outAfter = GetHero(mOut.Snapshot(), LaneTeam.Red);
            Assert(Mathf.Abs(outAfter.hp - outHpBefore) < 0.01f && outAfter.stunTimer <= 0.001f,
                $"Ogre E (2.45u) must miss target at 3.15u (hp={outAfter.hp:0.0} vs {outHpBefore:0.0}, stun={outAfter.stunTimer:0.00})");

            var mIn = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, te);
            Assert(mIn.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Level Blue Ogre E (in check)");
            MovePairTo(mIn, Vector2.zero, new Vector2(2.30f, 0f));
            float inHpBefore = GetHero(mIn.Snapshot(), LaneTeam.Red).hp;
            mIn.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.zero });
            Step(mIn, 0.75f);
            HeroSnap inAfter = GetHero(mIn.Snapshot(), LaneTeam.Red);
            Assert(inAfter.hp < inHpBefore - 25f && inAfter.stunTimer > 0.5f,
                $"Ogre E (2.45u) must hit & stun target at 2.30u (dmg={inHpBefore - inAfter.hp:0.0}, stun={inAfter.stunTimer:0.00})");
        }

        private static void CheckMinionProximityXpWithoutLastHit()
        {
            float dt = 1f / 30f;

            // Helper: Move Blue Ogre to idleSpot (never attacks) while Red Miravel clears Blue's 4 minions at midfield,
            // letting Red's 4 surviving minions march cleanly into Blue outer tower.
            HeroSnap RunTowerCreepDeathScenario(Vector2 blueIdleSpot, Vector2 redPostClearSpot, out float redXpBeforeTower, out int redLvlBeforeTower, out HeroSnap redAfterOut)
            {
                var t = new LaneTuning
                {
                    waveFirstSpawnDelay = 0.05f,
                    waveInterval = 40f,
                    startingGold = 0,
                    passiveGoldPerSecond = 0f,
                    minionMaxHpMelee = 48f,
                    minionMaxHpRanged = 48f,
                    minionMeleeDamage = 1f,
                    minionRangedDamage = 1f,
                    minionXpMelee = 60f,
                    minionXpRanged = 60f,
                    towerAttackDamage = 200f,
                    towerAttackInterval = 0.35f,
                };
                var m = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
                MovePairTo(m, blueIdleSpot, new Vector2(1.2f, 0f));

                int orderedTarget = 0;
                for (int i = 0; i < 360; i++)
                {
                    m.Tick(dt);
                    LaneSnapshot s = m.Snapshot();
                    HeroSnap red = GetHero(s, LaneTeam.Red);
                    if (red.creepScore >= 4) break;
                    MinionSnap blueCreep = FindFirstEnemyMinion(s, LaneTeam.Red);
                    if (blueCreep != null && (orderedTarget != blueCreep.id || red.attackTargetId != blueCreep.id))
                    {
                        orderedTarget = blueCreep.id;
                        m.EnqueueOrApplyOrder(new LaneOrder
                        {
                            team = LaneTeam.Red,
                            kind = LaneOrderKind.AttackMinion,
                            targetId = blueCreep.id,
                            point = new Vector2(blueCreep.x, blueCreep.y),
                        });
                    }
                }

                LaneSnapshot postClear = m.Snapshot();
                HeroSnap redPostClear = GetHero(postClear, LaneTeam.Red);
                Assert(redPostClear.creepScore >= 4, $"Miravel must clear all 4 Blue minions at midfield (cs={redPostClear.creepScore})");
                redXpBeforeTower = redPostClear.xp;
                redLvlBeforeTower = redPostClear.level;

                m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = redPostClearSpot });
                Step(m, 8.5f);

                LaneSnapshot fin = m.Snapshot();
                redAfterOut = GetHero(fin, LaneTeam.Red);
                return GetHero(fin, LaneTeam.Blue);
            }

            // 1. Living hero within minionXpRadius (12u) receives full 100% XP when tower kills enemy creeps (0 Gold/CS);
            //    enemy hero standing near their OWN dying creeps receives 0 XP.
            HeroSnap blueNear = RunTowerCreepDeathScenario(new Vector2(-14.0f, 0f), new Vector2(-10.0f, 7.2f),
                out float redXpBefore, out int redLvlBefore, out HeroSnap redNearAfter);
            Assert(blueNear.gold == 0 && blueNear.creepScore == 0,
                $"Proximity soak must not grant gold/CS without last hit (gold={blueNear.gold}, cs={blueNear.creepScore})");
            Assert(blueNear.level >= 2,
                $"Blue Ogre within 12u of 4 tower-killed enemy creeps (240 XP) must reach Level 2 (lvl={blueNear.level}, xp={blueNear.xp:0.0})");
            Assert(redNearAfter.level == redLvlBefore && Mathf.Abs(redNearAfter.xp - redXpBefore) < 0.01f,
                $"Red Miravel near own dying Red creeps must gain 0 XP (lvl {redLvlBefore}->{redNearAfter.level}, xp {redXpBefore:0.0}->{redNearAfter.xp:0.0})");

            // 2. Hero outside minionXpRadius (inside Fountain > 16u away) gains 0 XP and 0 gold.
            HeroSnap blueFar = RunTowerCreepDeathScenario(new Vector2(-21.2f, 0f), new Vector2(18.0f, 0f),
                out _, out _, out _);
            Assert(blueFar.level == 1 && blueFar.xp <= 0.01f && blueFar.gold == 0,
                $"Hero outside 12u soak radius must earn 0 XP and 0 gold (lvl={blueFar.level}, xp={blueFar.xp:0.0}, gold={blueFar.gold})");
        }

        private static float SumTeamMinionHp(LaneSnapshot snap, LaneTeam team)
        {
            float sum = 0f;
            for (int i = 0; i < snap.minions.Count; i++)
            {
                if (snap.minions[i].team == team && snap.minions[i].hp > 0f) sum += snap.minions[i].hp;
            }
            return sum;
        }

        private static void CheckLaserShooterFullKitAndDecoyBlocking()
        {
            float dt = 1f / 30f;

            // (a) Passive: unlimited chain bounces (never repeats target), decaying hop dmg, and Laser Stacks amplify follow-up laser dmg
            var tp = new LaneTuning
            {
                waveFirstSpawnDelay = 0.05f,
                waveInterval = 40f,
                minionMaxHpMelee = 220f,
                minionMaxHpRanged = 220f,
                minionMeleeDamage = 0f,
                minionRangedDamage = 0f,
                startingGold = 0,
                passiveGoldPerSecond = 0f,
            };
            var mp = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, tp);
            MovePairTo(mp, new Vector2(-3.2f, 0f), new Vector2(1.6f, 0f));
            for (int i = 0; i < 240; i++)
            {
                mp.Tick(dt);
                LaneSnapshot sWave = mp.Snapshot();
                int near = 0;
                for (int m = 0; m < sWave.minions.Count; m++)
                {
                    if (sWave.minions[m].team == LaneTeam.Red && sWave.minions[m].x <= 5.5f) near++;
                }
                if (near >= 4) break;
            }

            LaneSnapshot preShot1 = mp.Snapshot();
            HeroSnap red0 = GetHero(preShot1, LaneTeam.Red);
            var redMinionHp0 = new System.Collections.Generic.Dictionary<int, float>();
            for (int m = 0; m < preShot1.minions.Count; m++)
            {
                if (preShot1.minions[m].team == LaneTeam.Red) redMinionHp0[preShot1.minions[m].id] = preShot1.minions[m].hp;
            }

            mp.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = red0.id, point = new Vector2(red0.x, red0.y) });
            Step(mp, 0.32f);
            mp.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });

            LaneSnapshot postShot1 = mp.Snapshot();
            HeroSnap red1 = GetHero(postShot1, LaneTeam.Red);
            float shot1HeroDmg = red0.hp - red1.hp;
            Assert(shot1HeroDmg > 25f && red1.laserStacks == 1,
                $"Chain Laser basic must damage primary target and apply exactly 1 stack (dmg={shot1HeroDmg:0.0}, stacks={red1.laserStacks})");

            int bouncedMinions = 0;
            float minBounceDmg = float.MaxValue;
            for (int m = 0; m < postShot1.minions.Count; m++)
            {
                MinionSnap ms = postShot1.minions[m];
                if (ms.team == LaneTeam.Red && redMinionHp0.TryGetValue(ms.id, out float hp0))
                {
                    float mdmg = hp0 - ms.hp;
                    if (mdmg > 1f)
                    {
                        bouncedMinions++;
                        Assert(ms.laserStacks == 1, $"Each bounced minion must be hit at most once per shot (stacks={ms.laserStacks})");
                        if (mdmg < minBounceDmg) minBounceDmg = mdmg;
                    }
                }
            }
            Assert(bouncedMinions >= 3, $"Chain Laser must bounce past 3+ minions with no bounce cap (bounced={bouncedMinions})");
            Assert(shot1HeroDmg > minBounceDmg + 5f, $"Subsequent chain hops must deal reduced damage (primary={shot1HeroDmg:0.0}, last={minBounceDmg:0.0})");

            // 2nd and 3rd shots on stacked target deal progressively more damage than 1st shot
            Step(mp, 0.70f); // clear basic attack cooldown
            float redBeforeShot2 = GetHero(mp.Snapshot(), LaneTeam.Red).hp;
            mp.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = red1.id, point = new Vector2(red1.x, red1.y) });
            Step(mp, 0.25f);
            mp.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            HeroSnap red2 = GetHero(mp.Snapshot(), LaneTeam.Red);
            float shot2HeroDmg = redBeforeShot2 - red2.hp;
            Assert(red2.laserStacks == 2 && shot2HeroDmg > shot1HeroDmg + 1.0f,
                $"Laser Stack on target must amplify follow-up laser damage (shot1={shot1HeroDmg:0.0}, shot2={shot2HeroDmg:0.0}, stacks={red2.laserStacks})");

            Step(mp, 0.70f);
            float redBeforeShot3 = GetHero(mp.Snapshot(), LaneTeam.Red).hp;
            mp.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = red2.id, point = new Vector2(red2.x, red2.y) });
            Step(mp, 0.25f);
            mp.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            HeroSnap red3 = GetHero(mp.Snapshot(), LaneTeam.Red);
            float shot3HeroDmg = redBeforeShot3 - red3.hp;
            Assert(red3.laserStacks == 3 && shot3HeroDmg > shot2HeroDmg + 0.8f,
                $"Additional Laser Stacks must keep scaling damage higher (shot2={shot2HeroDmg:0.0}, shot3={shot3HeroDmg:0.0}, stacks={red3.laserStacks})");

            // (b) [Q] extending line beam hits each unit once, grants escalating multi-kill bonus gold, & INT sqrt accelerates speed (not AGI)
            var tq = new LaneTuning
            {
                waveFirstSpawnDelay = 0.05f,
                waveInterval = 40f,
                minionMaxHpMelee = 38f,
                minionMaxHpRanged = 38f,
                minionMeleeDamage = 0f,
                minionRangedDamage = 0f,
                minionGoldMelee = 20,
                minionGoldRanged = 16,
                startingGold = 1200,
                passiveGoldPerSecond = 0f,
            };
            var mqInt = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, tq);
            var mqAgi = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, tq);
            Assert(mqInt.TryBuyItem(LaneTeam.Blue, 10, out _) && mqInt.TryBuyItem(LaneTeam.Blue, 10, out _) && mqInt.TryBuyItem(LaneTeam.Blue, 10, out _), "75 INT");
            Assert(mqAgi.TryBuyItem(LaneTeam.Blue, 9, out _) && mqAgi.TryBuyItem(LaneTeam.Blue, 9, out _) && mqAgi.TryBuyItem(LaneTeam.Blue, 9, out _), "75 AGI");
            Assert(mqInt.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q) && mqAgi.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Q");
            MovePairTo(mqInt, new Vector2(-3.2f, 0f), new Vector2(16.0f, 0f));
            MovePairTo(mqAgi, new Vector2(-3.2f, 0f), new Vector2(16.0f, 0f));
            mqInt.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(4.0f, 0f) });
            mqAgi.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(4.0f, 0f) });
            Step(mqInt, 0.05f);
            Step(mqAgi, 0.05f);
            HeroSnap qi = GetHero(mqInt.Snapshot(), LaneTeam.Blue);
            HeroSnap qa = GetHero(mqAgi.Snapshot(), LaneTeam.Blue);
            Assert(qi.phaseDuration < qa.phaseDuration - 0.25f && Mathf.Abs(qa.phaseDuration - tq.laserQDuration) < 0.02f,
                $"Laser Q extension speed must scale with INT sqrt and NOT AGI (intDur={qi.phaseDuration:0.00}s vs agiDur={qa.phaseDuration:0.00}s)");

            var mq = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, tq);
            Assert(mq.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Laser Q");
            MovePairTo(mq, new Vector2(-3.2f, 0f), new Vector2(16.0f, 0f));
            for (int i = 0; i < 240; i++)
            {
                mq.Tick(dt);
                LaneSnapshot sw = mq.Snapshot();
                int inLine = 0;
                for (int m = 0; m < sw.minions.Count; m++)
                {
                    if (sw.minions[m].team == LaneTeam.Red && sw.minions[m].x > -2.5f && sw.minions[m].x < 4.5f) inLine++;
                }
                if (inLine >= 4) break;
            }
            HeroSnap qBefore = GetHero(mq.Snapshot(), LaneTeam.Blue);
            mq.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(4.0f, 0f) });
            Step(mq, 0.28f);
            HeroSnap qMid = GetHero(mq.Snapshot(), LaneTeam.Blue);
            Assert(qMid.phase == LanePhase.Casting && qMid.activeSlot == LaneAbilitySlot.Q &&
                   qMid.laserBeamRange > tq.laserQStartRange + 0.1f && qMid.laserBeamRange < tq.laserQMaxRange - 0.1f,
                $"Laser Q beam must extend gradually over its duration (range={qMid.laserBeamRange:0.00})");
            Step(mq, 0.80f);
            HeroSnap qAfter = GetHero(mq.Snapshot(), LaneTeam.Blue);
            int qKills = qAfter.creepScore - qBefore.creepScore;
            int qGoldGained = qAfter.gold - qBefore.gold;
            int minEscBonus = Mathf.RoundToInt(tq.laserQMultiKillBonusGold) * (qKills - 1) * qKills / 2;
            Assert(qKills >= 3 && qGoldGained >= qKills * 16 + minEscBonus && qGoldGained > qKills * 20 + 20,
                $"Laser Q multi-kill must grant escalating bonus gold (kills={qKills}, goldGained={qGoldGained})");

            // (c) [W] overlapping shocks stack both slow strength and slow duration
            var twSingle = new LaneTuning { waveFirstSpawnDelay = 999f, startingGold = 0, passiveGoldPerSecond = 0f };
            var mw1 = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, twSingle);
            Assert(mw1.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level Laser W (single shock)");
            MovePairTo(mw1, new Vector2(-3.0f, 0f), new Vector2(1.5f, 0f));
            mw1.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2, point = new Vector2(1.5f, 0f) });
            Step(mw1, 0.30f);
            mw1.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            mw1.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(1.5f, 0f) });
            Step(mw1, 0.04f);
            HeroSnap redW1 = GetHero(mw1.Snapshot(), LaneTeam.Red);

            var twMulti = new LaneTuning
            {
                waveFirstSpawnDelay = 0.05f,
                waveInterval = 40f,
                minionMaxHpMelee = 250f,
                minionMaxHpRanged = 250f,
                minionMeleeDamage = 0f,
                minionRangedDamage = 0f,
                startingGold = 0,
                passiveGoldPerSecond = 0f,
            };
            var mwN = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, twMulti);
            Assert(mwN.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level Laser W (multi shock)");
            MovePairTo(mwN, new Vector2(-3.5f, 0f), new Vector2(1.2f, 0f));
            for (int i = 0; i < 240; i++)
            {
                mwN.Tick(dt);
                LaneSnapshot sw = mwN.Snapshot();
                int closeCreeps = 0;
                for (int m = 0; m < sw.minions.Count; m++)
                {
                    if (sw.minions[m].team == LaneTeam.Red && Vector2.Distance(new Vector2(sw.minions[m].x, sw.minions[m].y), new Vector2(1.2f, 0f)) <= 2.3f)
                    {
                        closeCreeps++;
                    }
                }
                if (closeCreeps >= 2) break;
            }
            HeroSnap redPreN = GetHero(mwN.Snapshot(), LaneTeam.Red);
            mwN.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redPreN.id, point = new Vector2(redPreN.x, redPreN.y) });
            Step(mwN, 0.32f);
            mwN.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            mwN.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(redPreN.x, redPreN.y) });
            Step(mwN, 0.04f);
            HeroSnap redWN = GetHero(mwN.Snapshot(), LaneTeam.Red);
            Assert(redWN.slowAmount > redW1.slowAmount + 0.05f && redWN.slowAmount < 0.82f &&
                   redWN.slowTimer > redW1.slowTimer + 0.45f && redWN.slowTimer < twMulti.laserWSlowDuration * 2.5f,
                $"Overlapping W shocks must stack slow (bounded <0.82) & duration (unbounded sqrt, single={redW1.slowAmount:0.00}/{redW1.slowTimer:0.00}s vs multi={redWN.slowAmount:0.00}/{redWN.slowTimer:0.00}s)");

            // Verify staggered/refreshed W casts while slow is active obey sqrt diminishing returns (no refresh bypass!)
            var mRef = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, new LaneTuning { startingGold = 1500, waveFirstSpawnDelay = 999f, laserWCooldown = 0.4f, passiveGoldPerSecond = 0f });
            Assert(mRef.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level W");
            MovePairTo(mRef, new Vector2(-3.0f, 0f), new Vector2(1.5f, 0f));
            mRef.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2, point = new Vector2(1.5f, 0f) });
            Step(mRef, 0.30f);
            mRef.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            mRef.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(1.5f, 0f) });
            Step(mRef, 0.55f); // 0.55s elapsed out of 1.15s
            mRef.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(1.5f, 0f) });
            Step(mRef, 0.04f);
            HeroSnap refAfter = GetHero(mRef.Snapshot(), LaneTeam.Red);
            float totalSpan = 0.55f + 0.04f + refAfter.slowTimer;
            float expectedTotalSpan = mRef.Tuning.laserWSlowDuration * Mathf.Sqrt(2f); // ~1.626s
            Assert(Mathf.Abs(totalSpan - expectedTotalSpan) < 0.12f,
                $"Refreshed W cast while slowed must obey sqrt(2) duration ceiling without bypass (totalSpan={totalSpan:0.00}s vs sqrt2={expectedTotalSpan:0.00}s)");

            // (d) [E] animated step-back spawns stationary decoy ONLY upon completion; stun cancels step; rank-only stun; AGI speeds step
            var te = new LaneTuning { waveFirstSpawnDelay = 999f, startingGold = 1500, passiveGoldPerSecond = 0f };
            var me = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, te);
            Assert(me.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Level Laser E");
            MovePairTo(me, Vector2.zero, new Vector2(2.6f, 0f));
            me.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = new Vector2(2.6f, 0f) });
            Step(me, 0.15f); // mid-step (~0.38s total): moving smoothly back, NO decoy spawned yet!
            LaneSnapshot midStep = me.Snapshot();
            HeroSnap blueMidStep = GetHero(midStep, LaneTeam.Blue);
            Assert(blueMidStep.x < -0.2f && blueMidStep.x > -1.3f && midStep.illusions.Count == 0 && blueMidStep.laserStepProgress > 0.1f,
                $"Laser E must animate step-back over time before spawning decoy (x={blueMidStep.x:0.00}, ill={midStep.illusions.Count}, prog={blueMidStep.laserStepProgress:0.00})");

            Step(me, 0.30f); // complete 0.38s step -> decoy appears at previous spot!
            LaneSnapshot decoySnap = me.Snapshot();
            HeroSnap blueStep = GetHero(decoySnap, LaneTeam.Blue);
            Assert(blueStep.x < -1.4f && decoySnap.illusions.Count == 1 && decoySnap.illusions[0].isLaserDecoy,
                $"Laser E completion must finish step-back & spawn stationary decoy (blueX={blueStep.x:0.00}, ill={decoySnap.illusions.Count})");

            // Enemy hero trying to walk straight through decoy gets blocked east of decoy's actual spawn spot
            Vector2 decoyPos = new Vector2(decoySnap.illusions[0].x, decoySnap.illusions[0].y);
            me.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = new Vector2(-3.0f, 0f) });
            Step(me, 0.70f);
            HeroSnap redBlocked = GetHero(me.Snapshot(), LaneTeam.Red);
            float distFromDecoy = Vector2.Distance(new Vector2(redBlocked.x, redBlocked.y), decoyPos);
            Assert(redBlocked.x >= decoyPos.x + 0.95f && distFromDecoy >= te.laserEDecoyRadius + 0.44f && me.Snapshot().illusions.Count == 1,
                $"Laser decoy must block enemy hero movement across it (redX={redBlocked.x:0.00}, decoyX={decoyPos.x:0.00}, dist={distFromDecoy:0.00})");

            // Enemy Miravel projectile toward Blue behind decoy is intercepted; Miravel takes zap dmg + stun + stack; decoy disappears
            float blueHpBeforeProj = GetHero(me.Snapshot(), LaneTeam.Blue).hp;
            float redHpBeforeProj = redBlocked.hp;
            me.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackHero, targetId = blueStep.id, point = new Vector2(blueStep.x, blueStep.y) });
            Step(me, 0.55f);
            LaneSnapshot afterProj = me.Snapshot();
            HeroSnap blueAfterProj = GetHero(afterProj, LaneTeam.Blue);
            HeroSnap redAfterProj = GetHero(afterProj, LaneTeam.Red);
            Assert(blueHpBeforeProj - blueAfterProj.hp < 2.0f,
                $"Laser decoy must intercept enemy ranged basic projectile (hp {blueHpBeforeProj:0.0} -> {blueAfterProj.hp:0.0})");
            Assert(redAfterProj.hp < redHpBeforeProj - 20f && redAfterProj.stunTimer > 0.15f && redAfterProj.laserStacks >= 1,
                $"Damaging decoy must zap & stun attacker (hp {redHpBeforeProj:0.0}->{redAfterProj.hp:0.0}, stun={redAfterProj.stunTimer:0.00}, stacks={redAfterProj.laserStacks})");
            Assert(afterProj.illusions.Count == 0, "Damaging Laser decoy as a hero must destroy it");

            // Creeps must never target or damage the Laser decoy
            var tec = new LaneTuning
            {
                waveFirstSpawnDelay = 0.05f,
                waveInterval = 40f,
                minionMaxHpMelee = 200f,
                minionMaxHpRanged = 200f,
                minionMeleeDamage = 18f,
                minionRangedDamage = 18f,
                laserEDecoyDuration = 12f,
                startingGold = 0,
                passiveGoldPerSecond = 0f,
            };
            var mec = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, tec);
            Assert(mec.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Level Laser E (creep ignore test)");
            MovePairTo(mec, new Vector2(-1.0f, 0f), new Vector2(16.0f, 0f));
            mec.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = new Vector2(2.0f, 0f) });
            Step(mec, 0.45f);
            mec.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-12.0f, 0f) });
            Step(mec, 7.5f);
            LaneSnapshot creepSnap = mec.Snapshot();
            Assert(creepSnap.illusions.Count == 1 && Mathf.Abs(creepSnap.illusions[0].hp - tec.laserEDecoyHp) < 0.01f,
                $"Creeps must ignore Laser decoy without damaging it (hp={creepSnap.illusions[0].hp:0.0})");

            // (e) [R] brief wind-up pause, then fire continuous chain-proc beam while moving freely
            var tr = new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f, startingGold = 0, passiveGoldPerSecond = 0f };
            var mr = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, tr);
            Assert(mr.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Level Laser R");
            MovePairTo(mr, new Vector2(-2.0f, 0f), new Vector2(3.8f, 0f));
            float redHpBeforeR = GetHero(mr.Snapshot(), LaneTeam.Red).hp;
            mr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(3.8f, 0f) });
            Step(mr, 0.25f);
            HeroSnap rWind = GetHero(mr.Snapshot(), LaneTeam.Blue);
            Assert(rWind.phase == LanePhase.Casting && rWind.activeSlot == LaneAbilitySlot.R,
                $"Laser R must have brief wind-up pause before firing (phase={rWind.phase})");

            Step(mr, 0.35f);
            HeroSnap rChan = GetHero(mr.Snapshot(), LaneTeam.Blue);
            Assert(rChan.phase == LanePhase.Channeling && rChan.activeSlot == LaneAbilitySlot.R,
                $"Laser R must enter Channeling firing state after wind-up (phase={rChan.phase})");

            mr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(2.2f, 0f) });
            Step(mr, 1.4f);
            HeroSnap blueAfterR = GetHero(mr.Snapshot(), LaneTeam.Blue);
            HeroSnap redAfterR = GetHero(mr.Snapshot(), LaneTeam.Red);
            Assert(blueAfterR.x > rChan.x + 2.0f, $"Laser Shooter must move freely while channeling R (x {rChan.x:0.00}->{blueAfterR.x:0.00})");
            Assert(redAfterR.laserStacks >= 3 && redAfterR.hp < redHpBeforeR - 70f,
                $"Laser R continuous beam must deliver multiple chain basic procs (stacks={redAfterR.laserStacks}, dmg={redHpBeforeR - redAfterR.hp:0.0})");

            // INT scales R duration & STR does NOT scale Laser E stun duration
            var mRInt = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, new LaneTuning { ultMinLevel = 1, waveFirstSpawnDelay = 999f, startingGold = 1500, passiveGoldPerSecond = 0f });
            Assert(mRInt.TryBuyItem(LaneTeam.Blue, 10, out _) && mRInt.TryBuyItem(LaneTeam.Blue, 10, out _), "50 INT");
            Assert(mRInt.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "R");
            mRInt.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(4f, 0f) });
            Step(mRInt, 0.55f);
            HeroSnap rIntChan = GetHero(mRInt.Snapshot(), LaneTeam.Blue);
            Assert(rIntChan.phaseDuration > rChan.phaseDuration + 0.55f,
                $"INT must extend Laser R duration (int={rIntChan.phaseDuration:0.00}s vs base={rChan.phaseDuration:0.00}s)");

            var mEStr = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, new LaneTuning { waveFirstSpawnDelay = 999f, startingGold = 1500, passiveGoldPerSecond = 0f });
            Assert(mEStr.TryBuyItem(LaneTeam.Blue, 8, out _) && mEStr.TryBuyItem(LaneTeam.Blue, 8, out _), "50 STR");
            Assert(mEStr.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "E");
            MovePairTo(mEStr, Vector2.zero, new Vector2(2.6f, 0f));
            mEStr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = new Vector2(2.6f, 0f) });
            Step(mEStr, 0.45f);
            int blueStrId = GetHero(mEStr.Snapshot(), LaneTeam.Blue).id;
            Vector2 blueStrPos = new Vector2(GetHero(mEStr.Snapshot(), LaneTeam.Blue).x, GetHero(mEStr.Snapshot(), LaneTeam.Blue).y);
            mEStr.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackHero, targetId = blueStrId, point = blueStrPos });
            float maxObservedStun = 0f;
            for (int tick = 0; tick < 30; tick++)
            {
                mEStr.Tick(dt);
                float st = GetHero(mEStr.Snapshot(), LaneTeam.Red).stunTimer;
                if (st > maxObservedStun) maxObservedStun = st;
            }
            Assert(maxObservedStun > 0.5f && Mathf.Abs(maxObservedStun - te.laserEStunFormula.baseValue) < 0.08f,
                $"Laser E stun must depend on rank only without STR scaling (observed={maxObservedStun:0.00}s, rank1={te.laserEStunFormula.baseValue:0.00}s)");
        }

        private static void CheckStopCommandAndHeroControlProfiles()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, ultMinLevel = 1, startingGold = 0, passiveGoldPerSecond = 0f };

            // 1. Stop cancels active movement
            var mMove = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            MovePairTo(mMove, new Vector2(-4f, 0f), new Vector2(8f, 0f));
            mMove.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(4f, 0f) });
            Step(mMove, 0.25f);
            HeroSnap moving = GetHero(mMove.Snapshot(), LaneTeam.Blue);
            Assert(moving.speed > 1.0f, $"Miravel should be moving before Stop (speed={moving.speed:0.00})");
            mMove.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            HeroSnap stoppedImmediate = GetHero(mMove.Snapshot(), LaneTeam.Blue);
            AssertClose(stoppedImmediate.speed, 0f, "Stop must halt movement immediately");
            Step(mMove, 0.25f);
            HeroSnap stayed = GetHero(mMove.Snapshot(), LaneTeam.Blue);
            AssertClose(stayed.x, stoppedImmediate.x, "Hero must remain stationary after Stop");

            // 2. Stop cancels basic Windup with zero damage & clears attack intent
            var mWind = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            MovePairTo(mWind, new Vector2(0f, 0f), new Vector2(4.2f, 0f));
            float ogHp0 = GetHero(mWind.Snapshot(), LaneTeam.Red).hp;
            mWind.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(mWind, 0.14f);
            HeroSnap inWindup = GetHero(mWind.Snapshot(), LaneTeam.Blue);
            Assert(inWindup.phase == LanePhase.Windup, $"Expected Windup phase (got {inWindup.phase})");
            mWind.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            HeroSnap canceled = GetHero(mWind.Snapshot(), LaneTeam.Blue);
            Assert(canceled.phase == LanePhase.Idle && canceled.attackTargetKind == 0,
                $"Stop must cancel Windup and clear attack intent (phase={canceled.phase}, target={canceled.attackTargetKind})");
            Step(mWind, 0.65f);
            LaneSnapshot afterCancel = mWind.Snapshot();
            Assert(ogHp0 - GetHero(afterCancel, LaneTeam.Red).hp < 2.0f,
                $"Cancelled Windup must deal zero attack damage (hp {ogHp0:0.0} -> {GetHero(afterCancel, LaneTeam.Red).hp:0.0})");
            Assert(afterCancel.projectiles.Count == 0, "Cancelled Windup must fire zero projectiles");

            // 3. Stop during Recover clears follow-up intent without bypassing recovery cooldown
            var mRec = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            MovePairTo(mRec, new Vector2(0f, 0f), new Vector2(4.2f, 0f));
            mRec.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(mRec, 0.34f); // luisBasicWindup=0.26 -> entered Recover
            HeroSnap recovering = GetHero(mRec.Snapshot(), LaneTeam.Blue);
            Assert(recovering.phase == LanePhase.Recover && recovering.phaseTimer > 0.20f,
                $"Expected Recover backswing after shot (phase={recovering.phase}, t={recovering.phaseTimer:0.00})");
            mRec.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            HeroSnap recStopped = GetHero(mRec.Snapshot(), LaneTeam.Blue);
            Assert(recStopped.phase == LanePhase.Recover && recStopped.attackTargetKind == 0,
                $"Stop must preserve Recover timer while clearing auto-attack intent (phase={recStopped.phase})");
            Step(mRec, 0.90f);
            HeroSnap recDone = GetHero(mRec.Snapshot(), LaneTeam.Blue);
            Assert(recDone.phase == LanePhase.Idle && recDone.attackTargetId == 0 && mRec.Snapshot().projectiles.Count == 0,
                "Miravel must stay idle without firing a second shot after Stop during Recover");

            // 4. Stop interrupts Miravel [R] Sanctum channel
            var mMirR = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            Assert(mMirR.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Level Miravel R");
            MovePairTo(mMirR, Vector2.zero, new Vector2(4f, 0f));
            mMirR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(2f, 0f) });
            Step(mMirR, 0.25f);
            Assert(mMirR.Snapshot().zones.Count == 1 && GetHero(mMirR.Snapshot(), LaneTeam.Blue).phase == LanePhase.Channeling,
                "Miravel R channel must be active before Stop");
            mMirR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            LaneSnapshot mirRAfter = mMirR.Snapshot();
            Assert(GetHero(mirRAfter, LaneTeam.Blue).phase == LanePhase.Idle && mirRAfter.zones.Count == 0,
                "Stop must cancel Miravel [R] Sanctum channel");

            // 5. Stop respects committed ability casts (Ogre [Q], Ogre [R] channel, Swordsman [R] dash) & stops LaserShooter feet during mobile [R]
            var mOgre = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            Assert(mOgre.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.Q), "Level Ogre Q");
            MovePairTo(mOgre, Vector2.zero, new Vector2(2.2f, 0f));
            float mirBeforeQ = GetHero(mOgre.Snapshot(), LaneTeam.Red).hp;
            mOgre.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2.2f, 0f) });
            Step(mOgre, 0.25f);
            mOgre.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            Assert(GetHero(mOgre.Snapshot(), LaneTeam.Blue).phase == LanePhase.Casting, "Stop must NOT cancel committed Ogre Q cast");
            Step(mOgre, 0.80f);
            Assert(GetHero(mOgre.Snapshot(), LaneTeam.Red).hp < mirBeforeQ - 30f, "Committed Ogre Q must finish after Stop");

            var mOgreR = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            Assert(mOgreR.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Level Ogre R");
            MovePairTo(mOgreR, Vector2.zero, new Vector2(2.0f, 0f));
            mOgreR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(2f, 0f) });
            Step(mOgreR, 0.20f);
            mOgreR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2f, 0f) });
            mOgreR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            Assert(GetHero(mOgreR.Snapshot(), LaneTeam.Blue).phase == LanePhase.Channeling &&
                   GetHero(mOgreR.Snapshot(), LaneTeam.Blue).rComboCount == 1,
                "Stop must NOT cancel Ogre [R] Rampage channel");

            var mSwR = new LaneMatch(LaneHeroId.Swordsman, LaneHeroId.Miravel, t);
            Assert(mSwR.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Level Swordsman R");
            MovePairTo(mSwR, Vector2.zero, new Vector2(1.8f, 0f));
            mSwR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2 });
            Step(mSwR, 0.35f); // First strike marks Red for R
            mSwR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = new Vector2(5.2f, 0f) });
            Step(mSwR, 0.70f);
            float mirPreDashHp = GetHero(mSwR.Snapshot(), LaneTeam.Red).hp;
            mSwR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(5.2f, 0f) });
            Step(mSwR, 0.04f);
            Assert(GetHero(mSwR.Snapshot(), LaneTeam.Blue).phase == LanePhase.ExecuteDash, "Swordsman R dash should start");
            mSwR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            Step(mSwR, 0.35f);
            Assert(GetHero(mSwR.Snapshot(), LaneTeam.Red).hp < mirPreDashHp - 60f, "Stop must NOT cancel committed Swordsman R dash");

            var mLaserR = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Miravel, t);
            Assert(mLaserR.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.R), "Level Laser R");
            MovePairTo(mLaserR, Vector2.zero, new Vector2(4.0f, 0f));
            mLaserR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(4.0f, 0f) });
            Step(mLaserR, 0.60f);
            mLaserR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(4.0f, 0f) });
            Step(mLaserR, 0.15f);
            Assert(GetHero(mLaserR.Snapshot(), LaneTeam.Blue).speed > 1.0f, "LaserShooter walks during mobile R");
            mLaserR.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop });
            HeroSnap laserStoppedR = GetHero(mLaserR.Snapshot(), LaneTeam.Blue);
            Assert(laserStoppedR.speed == 0f && laserStoppedR.phase == LanePhase.Channeling && laserStoppedR.activeSlot == LaneAbilitySlot.R,
                $"Stop must halt LaserShooter feet while preserving [R] overdrive beam (spd={laserStoppedR.speed}, phase={laserStoppedR.phase})");

            // 6. Hero Control Profile availability checks & Ogre authoritative rejection of basic attacks / AttackMove
            var defSettings = new PlayerSettingsData();
            HeroControlProfile ogreProf = HeroControlCatalog.ForHero(LaneHeroId.Ogre);
            Assert(ogreProf.SupportsStop && !ogreProf.SupportsBasicAttack, "Ogre profile must allow Stop and disallow basic attacks");
            Assert(!ogreProf.IsCommandAvailable(ControlCommand.AttackOrder, defSettings) &&
                   !ogreProf.IsCommandAvailable(ControlCommand.AttackMove, defSettings),
                "Ogre profile must mark AttackOrder and AttackMove unavailable");

            mOgre.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMove, point = new Vector2(5f, 0f) });
            mOgre.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = 2, point = new Vector2(2f, 0f) });
            HeroSnap ogAfterAtk = GetHero(mOgre.Snapshot(), LaneTeam.Blue);
            Assert(!ogAfterAtk.isAttackMoving && ogAfterAtk.attackTargetKind == 0,
                "Ogre must ignore AttackMove and AttackHero orders authoritatively");

            // 7. Future archetype control profiles (architectural verification)
            HeroControlProfile koboldProf = HeroControlCatalog.ForFutureArchetype("KoboldKing");
            HeroControlProfile planeProf = HeroControlCatalog.ForFutureArchetype("AirplaneFighter");
            HeroControlProfile twinProf = HeroControlCatalog.ForFutureArchetype("TwinStickShooter");
            Assert(koboldProf.RtsSupport == RtsControlMode.Required &&
                   koboldProf.IsRtsEnabled(new PlayerSettingsData { enableOptionalRtsControls = false }) &&
                   koboldProf.IsCommandAvailable(ControlCommand.RtsGroup1, new PlayerSettingsData { enableOptionalRtsControls = false }),
                "Kobold King profile must always enable RTS controls even when Optional RTS toggle is false");
            var allEnabledSettings = new PlayerSettingsData { enableOptionalRtsControls = true };
            Assert(!planeProf.SupportsStop && !planeProf.IsCommandAvailable(ControlCommand.Stop, allEnabledSettings) &&
                   !planeProf.IsCommandAvailable(ControlCommand.RtsGroup1, allEnabledSettings),
                "Airplane Fighter must disallow Stop and RTS even if personal toggle is on");
            Assert(!twinProf.SupportsStop && !twinProf.IsCommandAvailable(ControlCommand.Stop, allEnabledSettings) &&
                   twinProf.TryGetAbilityCommand(LaneAbilitySlot.R, out ControlCommand twinUlt) &&
                   twinUlt == ControlCommand.TwinStickAbility4,
                "Twin-Stick Shooter must disallow Stop and expose custom ability slot commands");
            Assert(!HeroControlCatalog.ForHero(LaneHeroId.Miravel).IsRtsEnabled(defSettings) &&
                   HeroControlCatalog.ForHero(LaneHeroId.Miravel).IsRtsEnabled(allEnabledSettings),
                "Conventional heroes must disable RTS by default and enable when personal preference opts in");
        }

        private static void CheckOptionalRtsSelectionControlGroupsAndOwnership()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, startingGold = 0, passiveGoldPerSecond = 0f, luisWIllusionDuration = 4.5f };
            var m = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Miravel, t);
            Assert(m.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.W), "Level Blue Miravel W");
            Assert(m.TryLevelAbility(LaneTeam.Red, LaneAbilitySlot.W), "Level Red Miravel W");
            MovePairTo(m, new Vector2(-3f, 0f), new Vector2(6f, 0f));

            // Spawn controllable decoy on both teams
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(-1.0f, 1.8f) });
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastW, point = new Vector2(4.0f, -1.8f) });
            Step(m, 0.08f);

            LaneSnapshot snap = m.Snapshot();
            Assert(snap.illusions.Count == 2, $"Expected 2 illusions in mirror test (got {snap.illusions.Count})");
            IllusionSnap blueIll = snap.illusions[0].team == LaneTeam.Blue ? snap.illusions[0] : snap.illusions[1];
            IllusionSnap redIll = snap.illusions[0].team == LaneTeam.Red ? snap.illusions[0] : snap.illusions[1];
            Assert(blueIll.controllable && redIll.controllable, "Miravel W illusions must report controllable=true");

            var rts = new LaneRtsSelection();
            var auth = new List<ControllableUnitInfo>();
            rts.GetAuthorizedUnits(snap, LaneTeam.Blue, auth);
            Assert(auth.Count == 2 && auth[0].Id == 1 && auth[1].Id == blueIll.id,
                $"Blue authorized units must contain only Blue hero + Blue decoy (count={auth.Count})");

            // Non-controllable LaserShooter [E] decoy is excluded even for its owner (wait 0.45s for animated step completion!)
            var ml = new LaneMatch(LaneHeroId.LaserShooter, LaneHeroId.Ogre, t);
            Assert(ml.TryLevelAbility(LaneTeam.Blue, LaneAbilitySlot.E), "Level Laser E");
            MovePairTo(ml, new Vector2(-1f, 0f), new Vector2(4f, 0f));
            ml.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = new Vector2(2f, 0f) });
            Step(ml, 0.45f);
            LaneSnapshot laserSnap = ml.Snapshot();
            Assert(laserSnap.illusions.Count == 1 && !laserSnap.illusions[0].controllable,
                "Laser E hologram decoy must be non-controllable");
            rts.GetAuthorizedUnits(laserSnap, LaneTeam.Blue, auth);
            Assert(auth.Count == 1 && auth[0].IsHero, "Laser decoy must never appear as RTS selectable unit");

            // Disabled RTS pins selection to hero
            rts.SyncWithSnapshot(snap, LaneTeam.Blue, rtsActive: false, autoSelectSummons: true);
            Assert(rts.SelectedUnitIds.Count == 1 && rts.SelectedUnitIds[0] == 1, "RTS off must pin selection to hero");

            // Enabled RTS: unauthorized enemy selection rejected, decoy selection, empty drag preserves selection, multi-select, control groups & double-tap
            rts.SyncWithSnapshot(snap, LaneTeam.Blue, rtsActive: true, autoSelectSummons: false);
            Assert(!rts.SelectSingleUnit(redIll.id, false, snap, LaneTeam.Blue), "Selecting enemy illusion must be rejected");
            Assert(!rts.SelectSingleUnit(2, false, snap, LaneTeam.Blue), "Selecting enemy hero must be rejected");
            Assert(rts.SelectSingleUnit(blueIll.id, false, snap, LaneTeam.Blue), "Selecting own decoy must succeed");
            Assert(rts.SelectByPredicate(_ => false, false, snap, LaneTeam.Blue) == 0 &&
                   rts.SelectedUnitIds.Count == 1 && rts.SelectedUnitIds[0] == blueIll.id,
                "Empty drag-box must preserve existing selection");
            Assert(rts.AssignControlGroup(2, snap, LaneTeam.Blue), "Assign decoy to group 2");
            Assert(rts.SelectByPredicate(_ => true, false, snap, LaneTeam.Blue) == 2 && rts.SelectedUnitIds.Count == 2,
                "Drag-box matching both authorized units must multi-select hero + decoy");
            Assert(rts.AssignControlGroup(1, snap, LaneTeam.Blue), "Assign both units to group 1");

            Assert(rts.RecallControlGroup(2, snap, LaneTeam.Blue, 10.0f, out bool centerFirst, out _), "Recall group 2");
            Assert(!centerFirst && rts.SelectedUnitIds.Count == 1 && rts.SelectedUnitIds[0] == blueIll.id,
                "Single-tap recall selects group without camera snap");
            Assert(rts.RecallControlGroup(2, snap, LaneTeam.Blue, 10.15f, out bool centerSecond, out _), "Double-tap recall group 2");
            Assert(centerSecond, "Double-tap recall within window must request camera centering");

            // Issue Move and Stop ONLY to Decoy without touching Blue Hero position/orders
            HeroSnap heroBefore = GetHero(snap, LaneTeam.Blue);
            int[] decoyOnly = rts.GetCommandTargetUnitIds(true, snap, LaneTeam.Blue);
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-10f, -3f), unitIds = decoyOnly });
            Step(m, 0.35f);
            LaneSnapshot afterDecoyMove = m.Snapshot();
            HeroSnap heroAfter = GetHero(afterDecoyMove, LaneTeam.Blue);
            IllusionSnap decoyMoved = afterDecoyMove.illusions[0].id == blueIll.id ? afterDecoyMove.illusions[0] : afterDecoyMove.illusions[1];
            AssertClose(heroAfter.x, heroBefore.x, "Moving decoy alone must not move Miravel");
            Assert(decoyMoved.speed > 1.0f && decoyMoved.x < blueIll.x - 0.5f,
                $"Commanding decoy alone must steer decoy toward target (x {blueIll.x:0.00}->{decoyMoved.x:0.00}, spd={decoyMoved.speed:0.00})");

            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop, unitIds = decoyOnly });
            LaneSnapshot afterDecoyStop = m.Snapshot();
            IllusionSnap decoyStopped = afterDecoyStop.illusions[0].id == blueIll.id ? afterDecoyStop.illusions[0] : afterDecoyStop.illusions[1];
            AssertClose(decoyStopped.speed, 0f, "Stop targeting decoy alone must halt decoy");

            // Authoritative server check: unauthorized unitIds sent on Blue order cannot move Red hero or Red illusion!
            Vector2 redIllPosBefore = new Vector2(redIll.x, redIll.y);
            HeroSnap redHeroBefore = GetHero(afterDecoyStop, LaneTeam.Red);
            m.EnqueueOrApplyOrder(new LaneOrder
            {
                team = LaneTeam.Blue,
                kind = LaneOrderKind.Move,
                point = new Vector2(-18f, 0f),
                unitIds = new[] { 2, redIll.id },
            });
            m.EnqueueOrApplyOrder(new LaneOrder
            {
                team = LaneTeam.Blue,
                kind = LaneOrderKind.Stop,
                unitIds = new[] { 2, redIll.id },
            });
            Step(m, 0.04f);
            HeroSnap redHeroAfter = GetHero(m.Snapshot(), LaneTeam.Red);
            AssertClose(redHeroAfter.x, redHeroBefore.x, "Enemy hero must ignore unauthorized Move from opponent");

            // Recall Group 1 (Hero + Decoy) and issue simultaneous multi-unit Move east
            rts.RecallControlGroup(1, m.Snapshot(), LaneTeam.Blue, 20.0f, out _, out _);
            int[] bothIds = rts.GetCommandTargetUnitIds(true, m.Snapshot(), LaneTeam.Blue);
            float decoyXBeforeBoth = GetIllusionById(m.Snapshot(), blueIll.id).x;
            float heroXBeforeBoth = GetHero(m.Snapshot(), LaneTeam.Blue).x;
            m.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(8f, 0f), unitIds = bothIds });
            Step(m, 0.45f);
            LaneSnapshot afterBoth = m.Snapshot();
            Assert(GetHero(afterBoth, LaneTeam.Blue).x > heroXBeforeBoth + 0.8f &&
                   GetIllusionById(afterBoth, blueIll.id).x > decoyXBeforeBoth + 0.8f,
                "Multi-unit Move command must move both Hero and Decoy together");

            // Expire illusion -> Sync prunes dead ID & restores hero selection
            Step(m, 4.5f);
            LaneSnapshot expiredSnap = m.Snapshot();
            Assert(expiredSnap.illusions.Count == 0, "Illusions should have expired");
            rts.SyncWithSnapshot(expiredSnap, LaneTeam.Blue, rtsActive: true, autoSelectSummons: false);
            Assert(rts.SelectedUnitIds.Count == 1 && rts.SelectedUnitIds[0] == 1,
                "Selection must prune expired decoy and fall back to hero");
        }

        private static void CheckPersonalSettingsKeybindingsConflictsAndPersistence()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "MobaSettingsCheck_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(tempDir, "personal_settings.json");
            try
            {
                var store = new PlayerSettingsStore(path, persistToDisk: true);
                store.Load();

                Assert(!store.Data.enableOptionalRtsControls, "Optional RTS controls must default to false");
                Assert(store.GetKey(ControlCommand.Stop) == KeyCode.S, "Stop must default to S");
                Assert(store.GetKey(ControlCommand.TwinStickMoveDown) == KeyCode.S, "Twin-Stick Move Down must default to S");

                store.ValidateAllBindings(out var initConflicts, out var initUnbound);
                Assert(initConflicts.Count == 0 && initUnbound.Count == 0,
                    $"Default bindings must have zero conflicts/unbound commands (conflicts={initConflicts.Count}, unbound={initUnbound.Count})");

                // Context-aware conflict checks
                Assert(store.FindConflicts(ControlCommand.Stop, KeyCode.S).Count == 0,
                    "Stop=S must NOT conflict with TwinStickMoveDown=S across separate hero contexts");
                Assert(!store.TrySetBinding(ControlCommand.Stop, KeyCode.A, ConflictResolutionMode.RejectIfConflict, out var atkConflicts) &&
                       atkConflicts.Count == 1 && atkConflicts[0].ConflictingCommand == ControlCommand.AttackMove,
                    "Stop=A must detect conflict with AttackMove");
                Assert(!store.TrySetBinding(ControlCommand.Stop, KeyCode.B, ConflictResolutionMode.RejectIfConflict, out var globalConflicts) &&
                       globalConflicts.Count == 1 && globalConflicts[0].ConflictingCommand == ControlCommand.ToggleShop,
                    "Stop=B must detect conflict with Global ToggleShop");
                Assert(!store.TrySetBinding(ControlCommand.RtsGroup1, KeyCode.Q, ConflictResolutionMode.RejectIfConflict, out var rtsConflicts) &&
                       rtsConflicts.Count == 1 && rtsConflicts[0].ConflictingCommand == ControlCommand.CastQ,
                    "RtsGroup1=Q must detect conflict with CastQ");
                Assert(!store.TrySetBinding(ControlCommand.Stop, KeyCode.Escape, ConflictResolutionMode.SwapWithConflicting, out _),
                    "Reserved Escape key must be rejected");
                Assert(!store.TrySetBinding(ControlCommand.Stop, KeyCode.LeftControl, ConflictResolutionMode.SwapWithConflicting, out _),
                    "Reserved Ctrl modifier key must be rejected");

                // Swap & Unbind resolution modes
                Assert(store.TrySetBinding(ControlCommand.Stop, KeyCode.H, ConflictResolutionMode.RejectIfConflict, out _), "Bind Stop=H");
                Assert(store.TrySetBinding(ControlCommand.Stop, KeyCode.B, ConflictResolutionMode.SwapWithConflicting, out _), "Swap Stop(H) with Shop(B)");
                Assert(store.GetKey(ControlCommand.Stop) == KeyCode.B && store.GetKey(ControlCommand.ToggleShop) == KeyCode.H,
                    "SwapWithConflicting must exchange both keys cleanly");

                Assert(store.TrySetBinding(ControlCommand.AttackMove, KeyCode.B, ConflictResolutionMode.UnbindConflicting, out _), "Rebind AttackMove=B, unbind Stop");
                Assert(store.GetKey(ControlCommand.AttackMove) == KeyCode.B && store.GetKey(ControlCommand.Stop) == KeyCode.None,
                    "UnbindConflicting must assign new command and clear conflicting binding");

                // Persist across restart (fresh instance reads disk file)
                store.SetOptionalRtsEnabled(true);
                store.SetExtraPref("custom_future_pref", "verified_v027");
                Assert(File.Exists(path), "Settings file must be written to disk");

                var reloaded = new PlayerSettingsStore(path, persistToDisk: true);
                reloaded.Load();
                Assert(reloaded.Data.enableOptionalRtsControls, "Optional RTS preference must persist across restart");
                Assert(reloaded.GetKey(ControlCommand.AttackMove) == KeyCode.B, "AttackMove rebind must persist");
                Assert(reloaded.GetKey(ControlCommand.Stop) == KeyCode.None, "Unbound Stop state must persist");
                Assert(reloaded.GetExtraPref("custom_future_pref") == "verified_v027", "Extensible extraPrefs must persist");

                // Forward compatibility when older JSON lacks newly added commands
                File.WriteAllText(path, "{\"version\":1,\"enableOptionalRtsControls\":true,\"keybindings\":[{\"command\":\"Stop\",\"key\":\"G\"}]}");
                var partialLoad = new PlayerSettingsStore(path, persistToDisk: true);
                partialLoad.Load();
                Assert(partialLoad.Data.enableOptionalRtsControls &&
                       partialLoad.GetKey(ControlCommand.Stop) == KeyCode.G &&
                       partialLoad.GetKey(ControlCommand.CastQ) == KeyCode.Q,
                    "Partial settings JSON must retain saved overrides and backfill new commands with defaults");

                // Restore defaults & persist
                partialLoad.RestoreDefaults(save: true);
                var afterReset = new PlayerSettingsStore(path, persistToDisk: false);
                afterReset.Load();
                Assert(!afterReset.Data.enableOptionalRtsControls &&
                       afterReset.GetKey(ControlCommand.Stop) == KeyCode.S &&
                       afterReset.GetKey(ControlCommand.AttackMove) == KeyCode.A &&
                       afterReset.GetKey(ControlCommand.ToggleShop) == KeyCode.B,
                    "RestoreDefaults must revert and persist all defaults cleanly");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        private static IllusionSnap GetIllusionById(LaneSnapshot snap, int id)
        {
            for (int i = 0; i < snap.illusions.Count; i++)
            {
                if (snap.illusions[i].id == id) return snap.illusions[i];
            }
            throw new InvalidOperationException($"Missing illusion {id} in snapshot");
        }

        private static void CheckBotVsBotBalanceDiagnostic()
        {
            var pairs = new[]
            {
                (LaneHeroId.Ogre, LaneHeroId.Miravel),
                (LaneHeroId.Miravel, LaneHeroId.Ogre),
                (LaneHeroId.Ogre, LaneHeroId.Swordsman),
                (LaneHeroId.Swordsman, LaneHeroId.Ogre),
                (LaneHeroId.Miravel, LaneHeroId.Swordsman),
                (LaneHeroId.Swordsman, LaneHeroId.Miravel),
                (LaneHeroId.LaserShooter, LaneHeroId.Ogre),
                (LaneHeroId.Ogre, LaneHeroId.LaserShooter),
                (LaneHeroId.LaserShooter, LaneHeroId.Miravel),
                (LaneHeroId.Miravel, LaneHeroId.LaserShooter),
                (LaneHeroId.LaserShooter, LaneHeroId.Swordsman),
                (LaneHeroId.Swordsman, LaneHeroId.LaserShooter),
            };

            float dt = 1f / 30f;
            int totalTicks = Mathf.RoundToInt(120f / dt);

            for (int p = 0; p < pairs.Length; p++)
            {
                (LaneHeroId blueId, LaneHeroId redId) = pairs[p];
                var m = new LaneMatch(blueId, redId, new LaneTuning());
                var botB = new LaneBot(LaneTeam.Blue);
                var botR = new LaneBot(LaneTeam.Red);

                for (int step = 0; step < totalTicks && !m.IsGameOver; step++)
                {
                    botB.Tick(m, dt);
                    botR.Tick(m, dt);
                    m.Tick(dt);
                }

                LaneSnapshot snap = m.Snapshot();
                HeroSnap b = GetHero(snap, LaneTeam.Blue);
                HeroSnap r = GetHero(snap, LaneTeam.Red);
                Assert(!float.IsNaN(b.hp) && !float.IsNaN(b.x) && !float.IsNaN(r.hp) && !float.IsNaN(r.x),
                    $"NaN detected in bot matchup {blueId} vs {redId}");
                Assert(b.gold > 0 && r.gold > 0, $"Economy stalled in {blueId} vs {redId}");

                float bTw = snap.towers[0].alive ? snap.towers[0].hp : 0f;
                float rTw = snap.towers[1].alive ? snap.towers[1].hp : 0f;
                string win = snap.gameOver ? ((LaneTeam)snap.winnerTeam).ToString() : "None";
                Debug.Log($"[BalanceDiagnostic] {blueId} vs {redId} (120s): Kills {b.kills}-{r.kills} | CS {b.creepScore}-{r.creepScore} | Gold {b.gold}-{r.gold} | TowerHP {bTw:0}-{rTw:0} | Winner={win}");
            }
        }

        private static MinionSnap FindFirstEnemyMinion(LaneSnapshot snap, LaneTeam myTeam)
        {
            for (int i = 0; i < snap.minions.Count; i++)
            {
                if (snap.minions[i].team != myTeam && snap.minions[i].hp > 0f) return snap.minions[i];
            }
            return null;
        }

        private static float FindMinionHp(LaneSnapshot snap, int minionId)
        {
            for (int i = 0; i < snap.minions.Count; i++)
            {
                if (snap.minions[i].id == minionId) return snap.minions[i].hp;
            }
            return 0f;
        }

        private static HeroSnap GetHero(LaneSnapshot snap, LaneTeam team)
        {
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].team == team) return snap.heroes[i];
            }
            throw new InvalidOperationException($"Missing {team} hero in snapshot");
        }

        private static void Assert(bool cond, string message)
        {
            if (!cond) throw new InvalidOperationException($"LaneMatchChecks failed: {message}");
        }

        private static void AssertClose(float actual, float expected, string message)
        {
            if (Mathf.Abs(actual - expected) > 0.02f)
            {
                throw new InvalidOperationException($"LaneMatchChecks failed: {message} (expected {expected}, got {actual})");
            }
        }
    }
}
