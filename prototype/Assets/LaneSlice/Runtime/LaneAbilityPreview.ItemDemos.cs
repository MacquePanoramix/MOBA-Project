using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public sealed partial class LaneAbilityPreview
    {
        private static readonly Dictionary<(int itemId, int passiveIdx), List<PreviewSliderSpec>> ItemSliderCache =
            new Dictionary<(int, int), List<PreviewSliderSpec>>();

        private static readonly string[] DefaultItemExNames = { "Standard Demo" };
        private static readonly string[] WoundedTargetExNames = { "vs Wounded Foe (35% HP)", "vs Full HP Foe (100%)" };
        private static readonly string[] FountainExNames = { "Low Health (28% HP)", "High Health (82% HP)" };
        private static readonly string[] FirstStrikeExNames = { "Empowered (>3.0s idle)", "Ordinary (Immediate hit)" };
        private static readonly string[] WeightlessExNames = { "With Weightlessness (-25%)", "Without Item (Full Slow)" };
        private static readonly string[] ImmovableExNames = { "With Immovable Mountain", "Without Item (Full Push)" };

        public static string[] GetItemExampleNames(int rawItemId, int passiveIndex)
        {
            if (!LaneItems.TryGet(rawItemId, out LaneItemDef item) || item.Passives == null || item.Passives.Length == 0)
            {
                return DefaultItemExNames;
            }
            int idx = Mathf.Clamp(passiveIndex, 0, item.Passives.Length - 1);
            string pName = item.Passives[idx].Name;
            if (pName.StartsWith("Final Judgment") || pName.StartsWith("Wounded Strike")) return WoundedTargetExNames;
            if (pName.StartsWith("Fountain of Life")) return FountainExNames;
            if (pName.StartsWith("First Strike")) return FirstStrikeExNames;
            if (pName.StartsWith("Weightlessness")) return WeightlessExNames;
            if (pName.StartsWith("Immovable Mountain")) return ImmovableExNames;
            return DefaultItemExNames;
        }

        public static List<PreviewSliderSpec> GetRelevantItemSliders(int rawItemId, int passiveIndex)
        {
            int id = LaneItems.MigrateItemId(rawItemId);
            if (!LaneItems.TryGet(id, out LaneItemDef item) || item.Passives == null || item.Passives.Length == 0)
            {
                return EmptySliderList;
            }
            int pIdx = Mathf.Clamp(passiveIndex, 0, item.Passives.Length - 1);
            if (ItemSliderCache.TryGetValue((id, pIdx), out List<PreviewSliderSpec> cached))
            {
                return cached;
            }

            string pName = item.Passives[pIdx].Name;
            var list = new List<PreviewSliderSpec>();
            void Add(PreviewStatField f) => list.Add(CreateSliderSpec(f));

            if (pName.StartsWith("Final Judgment")) Add(PreviewStatField.Strength);
            else if (pName.StartsWith("Flight Reflex")) Add(PreviewStatField.Agility);
            else if (pName.StartsWith("Fountain of Life")) Add(PreviewStatField.HealPower);
            else if (pName.StartsWith("Dragonfire"))
            {
                Add(PreviewStatField.Intelligence);
                Add(PreviewStatField.AttackSpeed);
            }
            else if (pName.StartsWith("Molten Edge")) Add(PreviewStatField.Intelligence);
            else if (pName.StartsWith("Adaptive Scales")) Add(PreviewStatField.Strength);
            else if (pName.StartsWith("Ancient Vitality"))
            {
                Add(PreviewStatField.MaxHp);
                Add(PreviewStatField.Strength);
            }
            else if (pName.StartsWith("Giant's Might")) Add(PreviewStatField.MaxHp);
            else if (pName.StartsWith("Immovable Mountain")) Add(PreviewStatField.Strength);
            else if (pName.StartsWith("Crescent Strike"))
            {
                Add(PreviewStatField.AttackDamage);
                Add(PreviewStatField.Agility);
                Add(PreviewStatField.Intelligence);
            }
            else if (pName.StartsWith("Slippery Trickster")) Add(PreviewStatField.Agility);
            else if (pName.StartsWith("First Strike")) Add(PreviewStatField.Agility);
            else if (pName.StartsWith("Wellspring") || pName.StartsWith("Astral Surge")) Add(PreviewStatField.MaxMana);
            else if (pName.StartsWith("Kindred Fragrance")) Add(PreviewStatField.HealPower);
            else if (pName.StartsWith("Frugal Focus")) Add(PreviewStatField.ResourceCostReduction);
            else if (pName.StartsWith("Windstrider")) Add(PreviewStatField.Agility);
            else if (pName.StartsWith("Titan Aegis")) Add(PreviewStatField.Strength);
            // Fixed passives (Wounded Strike, Lingering Restoration, Primordial Vitality, Weightlessness,
            // Moss Mend, Cleaver, Sturdy, Dew Sip, Wither, Tidecaller Flow) intentionally keep 0 scaling sliders.

            ItemSliderCache[(id, pIdx)] = list;
            return list;
        }

        private static readonly List<PreviewSliderSpec> EmptySliderList = new List<PreviewSliderSpec>(0);

        public void SelectItemPassive(int rawItemId, int passiveIndex = 0, int exampleIndex = 0)
        {
            int id = LaneItems.MigrateItemId(rawItemId);
            if (!LaneItems.TryGet(id, out LaneItemDef item)) return;
            int clampedP = (item.Passives != null && item.Passives.Length > 0)
                ? Mathf.Clamp(passiveIndex, 0, item.Passives.Length - 1)
                : 0;
            bool changed = Mode != PreviewMode.ItemPassive || SelectedItemId != id || SelectedPassiveIndex != clampedP;

            Mode = PreviewMode.ItemPassive;
            SelectedItemId = id;
            SelectedPassiveIndex = clampedP;
            SelectedPassiveName = (item.Passives != null && clampedP < item.Passives.Length)
                ? item.Passives[clampedP].Name
                : item.Name;

            string[] exNames = GetItemExampleNames(id, clampedP);
            ExampleIndex = Mathf.Clamp(exampleIndex, 0, exNames.Length - 1);

            if (changed)
            {
                Overrides = new PreviewStatOverrides { enabled = true, level = 4, abilityRank = 1 };
            }
            RestartScenario();
        }

        public void ResetItemOverrides()
        {
            Overrides = new PreviewStatOverrides { enabled = true, level = 4, abilityRank = 1 };
            RestartScenario();
        }

        private Vector3 PositionItemCameraOffset()
        {
            if (SelectedPassiveName.StartsWith("Flight Reflex") || SelectedPassiveName.StartsWith("Slippery") || SelectedPassiveName.StartsWith("Windstrider"))
            {
                return new Vector3(-0.8f, 0f, 0f);
            }
            if (SelectedPassiveName.StartsWith("Crescent Strike"))
            {
                return new Vector3(0.9f, 0f, 0f);
            }
            return Vector3.zero;
        }

        private void RestartItemPassiveScenario()
        {
            LaneItems.TryGet(SelectedItemId, out LaneItemDef item);
            string pName = (item.Passives != null && SelectedPassiveIndex < item.Passives.Length)
                ? item.Passives[SelectedPassiveIndex].Name
                : item.Name;
            SelectedPassiveName = pName;

            LaneHeroId blueHero = ChooseDemoHeroForPassive(pName);
            LaneHeroId redHero = blueHero == LaneHeroId.Miravel ? LaneHeroId.Swordsman : LaneHeroId.Miravel;
            sim = new LaneMatch(blueHero, redHero, previewTuning, sharedPreviewMap);
            sim.DisableTowersForPreview();
            sim.SetPreviewStatOverrides(LaneTeam.Blue, Overrides);

            // Some comparison variants deliberately omit the item to illustrate baseline vs passive effect
            bool omitItemForBaselineComparison =
                (pName.StartsWith("Weightlessness") && ExampleIndex == 1) ||
                (pName.StartsWith("Immovable Mountain") && ExampleIndex == 1);
            if (!omitItemForBaselineComparison)
            {
                sim.GrantPreviewItem(LaneTeam.Blue, SelectedItemId);
            }

            BuildItemPassiveScript(item, pName);
        }

        private static LaneHeroId ChooseDemoHeroForPassive(string pName)
        {
            if (pName.StartsWith("Fountain of Life") || pName.StartsWith("Lingering Restoration") ||
                pName.StartsWith("Kindred Fragrance") || pName.StartsWith("Wellspring") ||
                pName.StartsWith("Astral Surge") || pName.StartsWith("Tidecaller Flow") ||
                pName.StartsWith("Frugal Focus") || pName.StartsWith("Dew Sip"))
            {
                return LaneHeroId.Miravel;
            }
            if (pName.StartsWith("Ancient Vitality") || pName.StartsWith("Immovable Mountain") || pName.StartsWith("Titan Aegis"))
            {
                return LaneHeroId.Ogre;
            }
            return LaneHeroId.Swordsman;
        }

        private void BuildItemPassiveScript(LaneItemDef item, string pName)
        {
            sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.8f, 0f), 90f);
            redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.4f, 0f), 270f);
            loopDuration = 4.5f;

            if (pName.StartsWith("Final Judgment") || pName.StartsWith("Wounded Strike"))
            {
                float targetFrac = ExampleIndex == 1 ? 1.0f : 0.35f;
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.5f, 0f), 270f, hpFraction: targetFrac);
                ScenarioCaption = ExampleIndex == 1
                    ? $"{pName}: baseline attacks against a 100% HP target (0% missing-HP bonus)."
                    : $"{pName}: attacking a 35% HP wounded enemy amplifies all outgoing damage.";
                At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
            }
            else if (pName.StartsWith("Flight Reflex"))
            {
                loopDuration = 5.0f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0.2f, 0f), 270f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2.6f, 0f), 270f);
                ScenarioCaption = "Flight Reflex: enemy hit marks attacker, retreating builds AGI-scaled Arm/MR; pursuing drops it faster.";
                At(0.20f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 55f, isMagic: true));
                At(0.30f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-7.0f, 0f) }));
                At(3.10f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(2.0f, 0f) }));
            }
            else if (pName.StartsWith("Fountain of Life"))
            {
                float hpFrac = ExampleIndex == 1 ? 0.82f : 0.28f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f, hpFraction: hpFrac);
                ScenarioCaption = ExampleIndex == 1
                    ? "Fountain of Life (82% HP): slight missing-HP boost to incoming heals."
                    : "Fountain of Life (28% HP): low Health + Heal Power strongly amplifies incoming heals & regen.";
                At(0.50f, () => sim.TriggerPreviewHeal(LaneTeam.Blue, 120f));
                At(1.80f, () => sim.TriggerPreviewHeal(LaneTeam.Blue, 120f));
            }
            else if (pName.StartsWith("Lingering Restoration"))
            {
                loopDuration = 4.8f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f, hpFraction: 0.35f);
                ScenarioCaption = "Lingering Restoration: initial heal restores HP, then echoes 28% smoothly over 3.5s (non-recursive).";
                At(0.45f, () => sim.TriggerPreviewHeal(LaneTeam.Blue, 150f));
            }
            else if (pName.StartsWith("Dragonfire"))
            {
                loopDuration = 5.2f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.4f, 0f), 270f);
                ScenarioCaption = "Dragonfire: attacks stack Heat (max 8) for INT-scaled Magic DoT, then dissipate 1 stack/s after hold.";
                At(0.20f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
                At(2.80f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-4.5f, 0f) }));
            }
            else if (pName.StartsWith("Molten Edge"))
            {
                loopDuration = 4.8f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.4f, 0f), 270f);
                ScenarioCaption = "Molten Edge: shared Heat stacks shred target Armor & Magic Resist (INT raises max shred).";
                At(0.20f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
            }
            else if (pName.StartsWith("Adaptive Scales"))
            {
                loopDuration = 4.6f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.6f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.4f, 0f), 270f);
                ScenarioCaption = "Adaptive Scales: direct hits from an enemy hero stack per-attacker Arm/MR (STR caps max bonus).";
                At(0.35f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 45f, isMagic: false, isBasicHit: true));
                At(0.95f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 45f, isMagic: true, isBasicHit: false));
                At(1.55f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 45f, isMagic: false, isBasicHit: true));
                At(2.15f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 45f, isMagic: true, isBasicHit: false));
            }
            else if (pName.StartsWith("Ancient Vitality"))
            {
                loopDuration = 4.8f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2.2f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2.2f, 0f), 270f);
                ScenarioCaption = "Ancient Vitality: incoming spikes split into immediate HP drop + amber staggered payout over Duration(STR).";
                At(0.45f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 360f, isMagic: false, isBasicHit: true));
                At(2.40f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 240f, isMagic: true, isBasicHit: false));
            }
            else if (pName.StartsWith("Giant's Might"))
            {
                loopDuration = 4.0f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.5f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.5f, 0f), 270f);
                ScenarioCaption = "Giant's Might: 3.0% of Max Health converts into genuine Attack Damage (+Max HP slider updates AD).";
                At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
            }
            else if (pName.StartsWith("Immovable Mountain"))
            {
                loopDuration = 3.8f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0.6f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2.4f, 0f), 270f);
                ScenarioCaption = ExampleIndex == 1
                    ? "Baseline without Immovable Mountain: receives full 3.60u forced knockback distance."
                    : "Immovable Mountain: STR reduces forced knockback/pull distance without shortening stuns.";
                At(0.50f, () => sim.TriggerPreviewDisplacement(LaneTeam.Blue, Vector2.left, 3.6f));
            }
            else if (pName.StartsWith("Primordial Vitality"))
            {
                loopDuration = 4.4f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f, hpFraction: 0.25f);
                ScenarioCaption = "Primordial Vitality: natural HP/s ramps linearly up to +12.0 HP/s as missing Health grows.";
            }
            else if (pName.StartsWith("Crescent Strike"))
            {
                loopDuration = 4.2f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2.2f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(-0.3f, 0f), 270f);
                sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(1.4f, 1.35f), maxHp: 750f);
                sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(1.9f, -0.15f), maxHp: 750f);
                sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(1.3f, -1.45f), maxHp: 750f);
                ScenarioCaption = "Crescent Strike (AD, AGI dmg%, INT radius): sweeps Physical arc behind primary target onto nearby enemies.";
                At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
            }
            else if (pName.StartsWith("Slippery Trickster"))
            {
                loopDuration = 4.5f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-4.0f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(-1.2f, 0f), 270f);
                ScenarioCaption = "Slippery Trickster: hero damage grants an AGI-scaled Move Speed burst decaying over 2.2s (fixed 14s CD).";
                At(0.15f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(6.0f, 0f) }));
                At(0.55f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 80f, isMagic: false, isBasicHit: true));
            }
            else if (pName.StartsWith("First Strike"))
            {
                loopDuration = 3.6f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.5f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.4f, 0f), 270f);
                if (ExampleIndex == 0)
                {
                    sim.SetPreviewFirstStrikeIdle(LaneTeam.Blue, previewTuning.firstStrikeInactivitySeconds + 0.1f);
                    ScenarioCaption = "First Strike Empowered: >=3.0s without starting a basic speeds opening wind-up (scales with AGI).";
                }
                else
                {
                    sim.SetPreviewFirstStrikeIdle(LaneTeam.Blue, 0f);
                    ScenarioCaption = "Ordinary Attack: immediately attacking uses standard wind-up until 3.0s basic inactivity elapses.";
                }
                At(0.20f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
            }
            else if (pName.StartsWith("Weightlessness"))
            {
                loopDuration = 3.8f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-4.5f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(-4.8f, 1.8f), 270f);
                ScenarioCaption = ExampleIndex == 1
                    ? "Without Weightlessness: 40% incoming slow applies at full 40% strength for 2.6s."
                    : "Weightlessness: fixed -25% slow strength (40% -> 30%), keeping full 2.6s slow duration.";
                At(0.15f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(5.5f, 0f) }));
                At(0.45f, () => sim.TriggerPreviewSlow(LaneTeam.Blue, 0.40f, 2.6f));
            }
            else if (pName.StartsWith("Moss Mend"))
            {
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f, hpFraction: 0.45f);
                ScenarioCaption = "Moss Mend (Provisional): grants +4.5 HP/s bonus Health regen once out of enemy hero combat for 5.0s.";
                At(0.20f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 25f, isMagic: false));
                At(1.30f, () => sim.SetPreviewOutOfCombatAge(LaneTeam.Blue, 5.2f));
            }
            else if (pName.StartsWith("Cleaver"))
            {
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.8f, 0f), 90f);
                int mId = sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(0.1f, 0f), maxHp: 500f);
                ScenarioCaption = "Cleaver (Provisional): basic hits & physical hits deal +18 flat bonus damage vs lane minions.";
                At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackMinion, targetId = mId }));
            }
            else if (pName.StartsWith("Sturdy"))
            {
                ScenarioCaption = "Sturdy (Provisional): incoming minion shots & hero basics lose flat 6 post-mitigation damage (min 1).";
                At(0.45f, () => sim.TriggerPreviewMinionHitHero(LaneTeam.Blue, 22f));
                At(1.35f, () => sim.TriggerPreviewMinionHitHero(LaneTeam.Blue, 22f));
                At(2.25f, () => sim.TriggerPreviewMinionHitHero(LaneTeam.Blue, 22f));
            }
            else if (pName.StartsWith("Dew Sip"))
            {
                sim.SetPreviewHeroManaFraction(LaneTeam.Blue, 0.35f);
                ScenarioCaption = "Dew Sip (Provisional): grants +1.5 Mana/s after 5.0s out of enemy hero combat.";
                At(0.20f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 20f, isMagic: false));
                At(1.30f, () => sim.SetPreviewOutOfCombatAge(LaneTeam.Blue, 5.2f));
            }
            else if (pName.StartsWith("Wellspring") || pName.StartsWith("Astral Surge"))
            {
                sim.SetPreviewHeroManaFraction(LaneTeam.Blue, 0.25f);
                ScenarioCaption = $"{pName} (Provisional): converts Max Mana into steady flat Mana/s (+Max Mana slider raises regen).";
            }
            else if (pName.StartsWith("Kindred Fragrance"))
            {
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f, hpFraction: 0.65f);
                ScenarioCaption = "Kindred Fragrance (Provisional): emits 7.5u allied HP/s aura scaling with Heal Power.";
            }
            else if (pName.StartsWith("Wither"))
            {
                loopDuration = 4.4f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.5f, 0f), 270f, hpFraction: 0.45f);
                ScenarioCaption = "Wither (Provisional): hero damage inflicts -35% Grievous Wounds on enemy healing & positive regen for 3.5s.";
                At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
                At(1.30f, () => sim.TriggerPreviewHeal(LaneTeam.Red, 150f));
            }
            else if (pName.StartsWith("Tidecaller Flow"))
            {
                sim.SetPreviewHeroManaFraction(LaneTeam.Blue, 0.15f);
                ScenarioCaption = "Tidecaller Flow (Provisional): ramps up to +6.0 Mana/s linearly as current Mana runs low.";
            }
            else if (pName.StartsWith("Frugal Focus"))
            {
                loopDuration = 4.2f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2.2f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.4f, 0f), 270f);
                ScenarioCaption = "Frugal Focus (Provisional): flat-cost Mana casts refund 12% of Mana actually spent right after cast.";
                At(0.40f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(0.8f, 0f) }));
            }
            else if (pName.StartsWith("Windstrider"))
            {
                loopDuration = 5.2f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-5.5f, 0f), 90f);
                ScenarioCaption = "Windstrider (Provisional): out-of-combat movement ramps AGI-scaled Move Speed; hero hits drop it.";
                At(0.15f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(5.0f, 0f) }));
                At(3.40f, () => sim.TriggerPreviewHeroDamage(LaneTeam.Red, LaneTeam.Blue, 35f, isMagic: false));
            }
            else if (pName.StartsWith("Titan Aegis"))
            {
                loopDuration = 4.4f;
                sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.6f, 0f), 90f);
                redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.8f, 0f), 270f);
                ScenarioCaption = "Titan Aegis (Provisional): inflicting Stun/Knockback or dropping <45% HP grants a STR-scaled 4s shield.";
                At(0.35f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(0.8f, 0f) }));
            }
            else
            {
                ScenarioCaption = $"{item.Name}: stats previewed in isolation.";
            }
        }

        private void UpdateItemLiveTelemetry()
        {
            if (latestSnap == null || latestSnap.heroes == null || latestSnap.heroes.Count < 2)
            {
                LiveTelemetryLine = string.Empty;
                return;
            }
            HeroSnap self = latestSnap.heroes[0];
            HeroSnap enemy = latestSnap.heroes[1];
            string pName = SelectedPassiveName ?? string.Empty;

            if (pName.StartsWith("Final Judgment"))
            {
                float missing = 1f - Mathf.Clamp01(enemy.hp / Mathf.Max(1f, enemy.maxHp));
                float maxAmp = 0.24f + 0.28f * self.strength / (self.strength + 65f);
                LiveTelemetryLine = $"STR {self.strength:0} • Target HP {enemy.hp / Mathf.Max(1f, enemy.maxHp) * 100f:0}% • Dmg Amp +{missing * maxAmp * 100f:0.0}%";
            }
            else if (pName.StartsWith("Wounded Strike"))
            {
                float missing = 1f - Mathf.Clamp01(enemy.hp / Mathf.Max(1f, enemy.maxHp));
                LiveTelemetryLine = $"Target HP {enemy.hp / Mathf.Max(1f, enemy.maxHp) * 100f:0}% • Wounded Strike Amp +{missing * 14f:0.0}%";
            }
            else if (pName.StartsWith("Flight Reflex"))
            {
                LiveTelemetryLine = $"AGI {self.agility:0} • Bonus +{self.cowardShieldBonus:0.0} Arm/MR • Total Arm {self.armor:0} / MR {self.magicResist:0}";
            }
            else if (pName.StartsWith("Fountain of Life"))
            {
                float missing = 1f - Mathf.Clamp01(self.hp / Mathf.Max(1f, self.maxHp));
                float bhp = Mathf.Max(0f, self.healPower - 1f);
                float maxAmp = 0.28f + 0.55f * bhp / (bhp + 0.65f);
                LiveTelemetryLine = $"HP {self.hp / Mathf.Max(1f, self.maxHp) * 100f:0}% • HealPwr {self.healPower * 100f:0}% • In-Heal Mult {(1f + missing * maxAmp):0.00}x";
            }
            else if (pName.StartsWith("Lingering Restoration"))
            {
                LiveTelemetryLine = $"HP {self.hp:0}/{self.maxHp:0} • Regen {self.netRegenRate:+0.0;-0.0}/s (28% echo over 3.5s)";
            }
            else if (pName.StartsWith("Dragonfire"))
            {
                float dps = (18f + 0.55f * self.intelligence) * (enemy.heatStacks / (enemy.heatStacks + 4f));
                LiveTelemetryLine = $"INT {self.intelligence:0} • AS {self.attackSpeed:0.00}x • Heat x{enemy.heatStacks}/8 • Burn {dps:0.0} Mag/s";
            }
            else if (pName.StartsWith("Molten Edge"))
            {
                float shred = (14f + 26f * self.intelligence / (self.intelligence + 65f)) * (enemy.heatStacks / (enemy.heatStacks + 4f));
                LiveTelemetryLine = $"INT {self.intelligence:0} • Heat x{enemy.heatStacks}/8 • Shred -{shred:0.0} • Foe Arm {enemy.armor:0} / MR {enemy.magicResist:0}";
            }
            else if (pName.StartsWith("Adaptive Scales"))
            {
                LiveTelemetryLine = $"STR {self.strength:0} • Scales x{self.adaptiveScalesStacks}/6 • +{self.adaptiveScalesBonus:0.0} Arm/MR vs Attacker";
            }
            else if (pName.StartsWith("Ancient Vitality"))
            {
                float defFrac = previewTuning.ancientVitalityDeferFractionFormula.Evaluate(1, self.maxHp);
                float dur = previewTuning.ancientVitalityDurationFormula.Evaluate(1, self.strength);
                LiveTelemetryLine = $"MaxHP {self.maxHp:0} (Defer {defFrac * 100f:0.0}%) • STR {self.strength:0} ({dur:0.00}s) • Deferred Pool {self.deferredDamageTotal:0} HP";
            }
            else if (pName.StartsWith("Giant's Might"))
            {
                float bonusAd = self.maxHp * previewTuning.giantsMightAdPerMaxHp;
                LiveTelemetryLine = $"MaxHP {self.maxHp:0} • Giant's Might +{bonusAd:0.0} AD • Effective AD {self.attackDamage:0.0}";
            }
            else if (pName.StartsWith("Immovable Mountain"))
            {
                float red = previewTuning.immovableMountainFormula.Evaluate(1, self.strength);
                float distFactor = 1f - red;
                LiveTelemetryLine = $"STR {self.strength:0} • Push Reduction -{red * 100f:0.0}% • 3.60u -> {3.60f * distFactor:0.00}u (Stun unchanged)";
            }
            else if (pName.StartsWith("Primordial Vitality"))
            {
                float missing = 1f - Mathf.Clamp01(self.hp / Mathf.Max(1f, self.maxHp));
                LiveTelemetryLine = $"HP {self.hp / Mathf.Max(1f, self.maxHp) * 100f:0}% • Bonus +{12f * missing:0.0}/s • Net Regen {self.netRegenRate:+0.0;-0.0}/s";
            }
            else if (pName.StartsWith("Crescent Strike"))
            {
                float secCoef = 0.28f + 0.52f * self.agility / (self.agility + 70f);
                float rad = 2.30f + 1.75f * self.intelligence / (self.intelligence + 75f);
                LiveTelemetryLine = $"AD {self.attackDamage:0} • AGI Sec {secCoef * 100f:0.0}% ({self.attackDamage * secCoef:0} dmg) • INT Reach {rad:0.00}u";
            }
            else if (pName.StartsWith("Slippery Trickster"))
            {
                float peak = previewTuning.slipperyTricksterBonusFormula.Evaluate(1, self.agility);
                LiveTelemetryLine = $"AGI {self.agility:0} (Peak +{peak:0.00}) • Active +{self.slipperyBonus:0.00} MS • Speed {self.speed:0.00} • CD {self.slipperyCd:0.0}s";
            }
            else if (pName.StartsWith("First Strike"))
            {
                float red = previewTuning.firstStrikeReductionFormula.Evaluate(1, self.agility);
                float baseW = 0.28f;
                float empW = Mathf.Max(previewTuning.firstStrikeMinFinalWindup, Mathf.Max(previewTuning.firstStrikeMinRawWindup, baseW) * (1f - red));
                LiveTelemetryLine = $"AGI {self.agility:0} (-{red * 100f:0.0}%) • Windup {baseW * 1000f:0}ms -> {empW * 1000f:0}ms • {(self.firstStrikeReady ? "READY" : $"Idle {self.firstStrikeIdle:0.0}/3.0s")}";
            }
            else if (pName.StartsWith("Weightlessness"))
            {
                LiveTelemetryLine = $"Slowed {self.slowAmount * 100f:0}% ({self.slowTimer:0.0}s left) • Speed {self.speed:0.00} u/s (duration unchanged)";
            }
            else if (pName.StartsWith("Wellspring") || pName.StartsWith("Astral Surge") || pName.StartsWith("Tidecaller Flow") || pName.StartsWith("Dew Sip"))
            {
                LiveTelemetryLine = $"Mana {self.mana:0}/{self.maxMana:0} • Net Mana/s {self.netManaRegen:+0.00;-0.00}/s";
            }
            else if (pName.StartsWith("Frugal Focus"))
            {
                LiveTelemetryLine = $"RCR {self.rcrStat:0} (-{self.rcrPct * 100f:0.0}% cost) • W Cost {self.costW:0} -> 12% Refunded • Mana {self.mana:0}/{self.maxMana:0}";
            }
            else if (pName.StartsWith("Kindred Fragrance"))
            {
                LiveTelemetryLine = $"HealPwr {self.healPower * 100f:0}% • Aura Output +{2.2f * self.healPower:0.00} HP/s within 7.5u";
            }
            else if (pName.StartsWith("Wither"))
            {
                LiveTelemetryLine = $"Target Anti-Heal {(enemy.grievousTimer > 0f ? $"-35% ({enemy.grievousTimer:0.0}s)" : "Waiting hit")} • Foe HP {enemy.hp:0}/{enemy.maxHp:0}";
            }
            else if (pName.StartsWith("Windstrider"))
            {
                LiveTelemetryLine = $"AGI {self.agility:0} • Momentum +{self.momentumBonus:0.00} MS • Speed {self.speed:0.00} u/s";
            }
            else if (pName.StartsWith("Titan Aegis"))
            {
                float cap = 65f + 2.00f * self.strength;
                LiveTelemetryLine = $"STR {self.strength:0} (Shield Cap {cap:0}) • Active Shield {self.shieldHp:0} HP • CD {self.colossusCd:0.0}s";
            }
            else
            {
                LiveTelemetryLine = $"HP {self.hp:0}/{self.maxHp:0} • AD {self.attackDamage:0} • Arm {self.armor:0} / MR {self.magicResist:0} • MS {self.speed:0.00}";
            }
        }
    }
}
