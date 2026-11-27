using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public enum LaneItemCategory : byte
    {
        Starter = 0,
        Component = 1,
        SpecializedComponent = 2,
        Advanced = 3,
        ProvisionalComponent = 4,
        ProvisionalAdvanced = 5,
    }

    [Flags]
    public enum ItemPassiveFlags : uint
    {
        None = 0u,
        WoundedStrike = 1u << 0,
        FinalJudgment = 1u << 1,
        FlightReflex = 1u << 2,
        LingeringRestoration = 1u << 3,
        FountainOfLife = 1u << 4,
        AdaptiveScales = 1u << 5,
        PrimordialVitality = 1u << 6,
        DragonfireAndMoltenEdge = 1u << 7,
        AncientVitality = 1u << 8,
        GiantsMight = 1u << 9,
        ImmovableMountain = 1u << 10,
        FirstStrike = 1u << 11,
        Weightlessness = 1u << 12,
        CrescentStrike = 1u << 13,
        SlipperyTrickster = 1u << 14,
        MossMend = 1u << 15,
        Cleaver = 1u << 16,
        Sturdy = 1u << 17,
        Wither = 1u << 18,
        Windstrider = 1u << 19,
        TitanAegis = 1u << 20,
        AlliedRegenAura = 1u << 21,
        DewSip = 1u << 22,
        Wellspring = 1u << 23,
        AstralSurge = 1u << 24,
        TidecallerFlow = 1u << 25,
        FrugalFocus = 1u << 26,
    }

    public readonly struct RecipeIngredient
    {
        public readonly int ItemId;
        public readonly int Quantity;

        public RecipeIngredient(int itemId, int quantity = 1)
        {
            ItemId = itemId;
            Quantity = Mathf.Max(1, quantity);
        }
    }

    public sealed class ItemPassiveDoc
    {
        public readonly string Name;
        public readonly string PlainSummary;
        public readonly string[] ScalingArrows;
        public readonly string MathLine;

        public ItemPassiveDoc(string name, string plainSummary, string[] scalingArrows = null, string mathLine = "")
        {
            Name = name ?? string.Empty;
            PlainSummary = plainSummary ?? string.Empty;
            ScalingArrows = scalingArrows ?? Array.Empty<string>();
            MathLine = mathLine ?? string.Empty;
        }
    }

    public readonly struct LaneItemDef
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int Cost;
        public readonly LaneItemCategory Category;
        public readonly bool IsApproved;
        public readonly string Fantasy;

        public readonly float AddMaxHp;
        public readonly float AddAttackDamage;
        public readonly float AddAttackSpeed;
        public readonly float AddAbilityHaste;
        public readonly float AddArmor;
        public readonly float AddMagicResist;
        public readonly float AddMoveSpeed;
        public readonly float AddRegen;
        public readonly float AddHealPower;
        public readonly float AddAlliedAuraRegen;
        public readonly float AddStrength;
        public readonly float AddAgility;
        public readonly float AddIntelligence;
        public readonly float AddMaxMana;
        public readonly float AddManaRegen;
        public readonly float AddPercentManaRegen;
        public readonly float AddResourceCostReduction;

        public readonly ItemPassiveFlags PassiveFlags;
        public readonly RecipeIngredient[] Recipe;
        public readonly ItemPassiveDoc[] Passives;
        public readonly string Summary;

        public LaneItemDef(
            int id,
            string name,
            int cost,
            LaneItemCategory category,
            bool isApproved,
            string fantasy,
            float addMaxHp = 0f,
            float addAttackDamage = 0f,
            float addAttackSpeed = 0f,
            float addAbilityHaste = 0f,
            float addArmor = 0f,
            float addMagicResist = 0f,
            float addMoveSpeed = 0f,
            float addRegen = 0f,
            float addHealPower = 0f,
            float addAlliedAuraRegen = 0f,
            float addStrength = 0f,
            float addAgility = 0f,
            float addIntelligence = 0f,
            float addMaxMana = 0f,
            float addManaRegen = 0f,
            float addPercentManaRegen = 0f,
            float addResourceCostReduction = 0f,
            ItemPassiveFlags passiveFlags = ItemPassiveFlags.None,
            RecipeIngredient[] recipe = null,
            ItemPassiveDoc[] passives = null,
            string summary = "")
        {
            Id = id;
            Name = name;
            Cost = cost;
            Category = category;
            IsApproved = isApproved;
            Fantasy = fantasy ?? string.Empty;
            AddMaxHp = addMaxHp;
            AddAttackDamage = addAttackDamage;
            AddAttackSpeed = addAttackSpeed;
            AddAbilityHaste = addAbilityHaste;
            AddArmor = addArmor;
            AddMagicResist = addMagicResist;
            AddMoveSpeed = addMoveSpeed;
            AddRegen = addRegen;
            AddHealPower = addHealPower;
            AddAlliedAuraRegen = addAlliedAuraRegen;
            AddStrength = addStrength;
            AddAgility = addAgility;
            AddIntelligence = addIntelligence;
            AddMaxMana = addMaxMana;
            AddManaRegen = addManaRegen;
            AddPercentManaRegen = addPercentManaRegen;
            AddResourceCostReduction = addResourceCostReduction;
            PassiveFlags = passiveFlags;
            Recipe = recipe ?? Array.Empty<RecipeIngredient>();
            Passives = passives ?? Array.Empty<ItemPassiveDoc>();
            Summary = summary ?? string.Empty;
        }

        public bool IsStarter => Category == LaneItemCategory.Starter;
        public bool IsAdvancedOrCapstone => Category == LaneItemCategory.Advanced || Category == LaneItemCategory.ProvisionalAdvanced;
        public bool HasRecipe => Recipe != null && Recipe.Length > 0;
    }

    public enum RecipeNodeState : byte
    {
        Owned = 0,
        PartialAffordable = 1,
        PartialUnaffordable = 2,
        MissingAffordable = 3,
        MissingUnaffordable = 4,
    }

    public sealed class RecipeTreeNode
    {
        public LaneItemDef Item;
        public int Depth;
        public int RequiredQuantity;
        public int OwnedDirectCount;
        public bool SatisfiedByParent;
        public int DirectRemainingCost;
        public int CombineCost;
        public bool DirectCanAfford;
        public bool DirectCanPurchase;
        public RecipeNodeState State;
        public readonly List<RecipeTreeNode> Children = new List<RecipeTreeNode>();

        public int MissingCopies => Mathf.Max(0, RequiredQuantity - OwnedDirectCount);
        public bool IsSatisfied => SatisfiedByParent || OwnedDirectCount >= RequiredQuantity;
    }

    public sealed class PurchasePlan
    {
        public bool ValidItem;
        public LaneItemDef TargetItem;
        public int BaseCost;
        public int ConsumedValue;
        public int RemainingCost;
        public bool HasSlotAfterConsume;
        public bool CanAfford;
        public bool CanPurchase;
        public string Reason;
        public readonly List<int> ConsumedSlots = new List<int>();
    }

    public sealed class AutoPurchasePlan
    {
        public bool ValidItem;
        public LaneItemDef TargetItem;
        public bool BuysCompleteTarget;
        public int TotalGoldSpent;
        public int InitialRemainingCost;
        public int FinalRemainingCost;
        public string ActionLabel;
        public string Reason;
        public readonly List<int> StepItemIds = new List<int>();
        public readonly List<int> StepCosts = new List<int>();
        public readonly List<string> StepNames = new List<string>();

        public bool CanBuyAnything => StepItemIds.Count > 0;
    }

    /// <summary>
    /// Stable item IDs across saves, snapshots, bots, and tests.
    /// Legacy ID 19 (Spellweaver's Hourglass) is intentionally retired with no Forgotten Hourglass replacement.
    /// </summary>
    public static class LaneItemIds
    {
        // 21 Approved Components (Zero Mana stats)
        public const int SmallAxe = 101;
        public const int HeavyAxe = 102;
        public const int ShortSword = 103;
        public const int SteelSword = 104;
        public const int Titanstone = 105;
        public const int Windfeather = 106;
        public const int SagesEye = 107;
        public const int LifeCrystal = 108;
        public const int Heartstone = 109;
        public const int Bloodstone = 110;
        public const int WoodenShield = 111;
        public const int SilverAmulet = 112;
        public const int ReinforcedShield = 113;
        public const int SpringWater = 114;
        public const int SacredPearl = 115;
        public const int HealingElixir = 116;
        public const int DragonScale = 117;
        public const int PrimordialBlood = 118;
        public const int IronDagger = 119;
        public const int NimbleDagger = 120;
        public const int WingedCharm = 121;

        // 6 Approved Custom Advanced Items (Zero Mana stats)
        public const int ExecutionersAxe = 201;
        public const int CowardsShield = 202;
        public const int ChaliceOfLife = 203;
        public const int DraconicBlade = 204;
        public const int GiantsHeart = 205;
        public const int BananaBlade = 206;

        // Provisional Starters & Utility / Mana items (Provisional placeholders only)
        public const int WoodlandSalve = 301;
        public const int MinersHatchet = 302;
        public const int BrawlersBuckler = 303;
        public const int ManaDewVial = 304;
        public const int VitalityVest = 305;

        public const int SapphireShard = 311;
        public const int DewdropCharm = 312;
        public const int RunedSigil = 313;
        public const int ClockworkPin = 314;
        public const int WellspringFlask = 315;

        public const int KindredCenser = 401;
        public const int BlightedThorn = 402;
        public const int AstralReservoir = 403;
        public const int TidecallerChalice = 404;
        public const int EconomistsTome = 405;
        public const int MomentumGreaves = 406;
        public const int ColossusPauldron = 407;
    }

    public static class LaneItems
    {
        public const int MaxSlots = 6;
        public const float SellRefundRatio = 0.70f;
        public const int CowardsShieldId = LaneItemIds.CowardsShield;

        public static int SellRefund(int cost) => Mathf.RoundToInt(cost * SellRefundRatio);

        /// <summary>
        /// Safely migrates legacy v0.2.8 item IDs to the permanent v0.3.0 catalog IDs.
        /// Explicitly excludes legacy ID 19 (Spellweaver's Hourglass), which is removed without replacement.
        /// </summary>
        public static int MigrateItemId(int rawId)
        {
            switch (rawId)
            {
                case 1: return LaneItemIds.SteelSword;
                case 2: return LaneItemIds.IronDagger;
                case 3: return LaneItemIds.VitalityVest;
                case 4: return LaneItemIds.WoodenShield;
                case 5: return LaneItemIds.SilverAmulet;
                case 6: return LaneItemIds.ClockworkPin;
                case 7: return LaneItemIds.WingedCharm;
                case 8: return LaneItemIds.Titanstone;
                case 9: return LaneItemIds.Windfeather;
                case 10: return LaneItemIds.SagesEye;
                case 11: return LaneItemIds.KindredCenser;
                case 12: return LaneItemIds.CowardsShield;
                case 13: return LaneItemIds.MinersHatchet;
                case 14: return LaneItemIds.WoodlandSalve;
                case 15: return LaneItemIds.BrawlersBuckler;
                case 16: return LaneItemIds.BlightedThorn;
                case 17: return LaneItemIds.MomentumGreaves;
                case 18: return LaneItemIds.ColossusPauldron;
                case 19: return 0; // Deliberately retired — Forgotten Hourglass not implemented
                case 20: return LaneItemIds.ExecutionersAxe;
                default: return rawId;
            }
        }

        public static string CategoryLabel(LaneItemCategory cat)
        {
            switch (cat)
            {
                case LaneItemCategory.Starter: return "Starter";
                case LaneItemCategory.Component: return "Component";
                case LaneItemCategory.SpecializedComponent: return "Specialized";
                case LaneItemCategory.Advanced: return "Advanced";
                case LaneItemCategory.ProvisionalComponent: return "Prov. Component";
                case LaneItemCategory.ProvisionalAdvanced: return "Prov. Advanced";
                default: return cat.ToString();
            }
        }

        public static ItemPassiveFlags NormalizeEffectivePassives(ItemPassiveFlags raw)
        {
            // Approved rule (Part 8.1): Final Judgment supersedes Bloodstone's Wounded Strike when owned.
            if ((raw & ItemPassiveFlags.FinalJudgment) != 0)
            {
                raw &= ~ItemPassiveFlags.WoundedStrike;
            }
            // Provisional Mana upgrade rule (Stage F): Astral Reservoir's Astral Surge supersedes Wellspring Flask's Wellspring.
            if ((raw & ItemPassiveFlags.AstralSurge) != 0)
            {
                raw &= ~ItemPassiveFlags.Wellspring;
            }
            return raw;
        }

        public static readonly LaneItemDef[] Catalog = new[]
        {
            // =========================================================================
            // PROVISIONAL STARTERS (Stage D/E baseline; Mana Dew Vial added in Stage F)
            // =========================================================================
            new LaneItemDef(LaneItemIds.WoodlandSalve, "Woodland Salve", 180, LaneItemCategory.Starter, isApproved: false,
                fantasy: "Moss-wrapped salve that quietly patches small scrapes between skirmishes.",
                addMaxHp: 75f,
                passiveFlags: ItemPassiveFlags.MossMend,
                passives: new[]
                {
                    new ItemPassiveDoc("Moss Mend", "Grants bonus Health Regeneration while out of enemy hero combat for 5s.",
                        new[] { "Trigger: Out of hero combat ≥5s • Independent of all stats" },
                        "Out-of-combat sustain: +4.5 HP/s after 5.0s without taking hero damage."),
                },
                summary: "+75 HP • Moss Mend (+4.5 HP/s after 5s out of hero combat)"),

            new LaneItemDef(LaneItemIds.MinersHatchet, "Miner's Hatchet", 200, LaneItemCategory.Starter, isApproved: false,
                fantasy: "Notched iron cleaver balanced for quick creep harvesting.",
                addAttackDamage: 10f,
                passiveFlags: ItemPassiveFlags.Cleaver,
                passives: new[]
                {
                    new ItemPassiveDoc("Cleaver", "Basic attacks and physical hits deal bonus flat damage to lane minions.",
                        new[] { "Target: Lane minions only • Fixed flat bonus" },
                        "Minion bonus physical dmg: +18 flat before minion Armor."),
                },
                summary: "+10 AD • Cleaver (+18 flat physical dmg vs minions)"),

            new LaneItemDef(LaneItemIds.BrawlersBuckler, "Brawler's Buckler", 220, LaneItemCategory.Starter, isApproved: false,
                fantasy: "Dented targe built to shrug off chip hits from creeps and early harassment.",
                addMaxHp: 80f, addArmor: 12f,
                passiveFlags: ItemPassiveFlags.Sturdy,
                passives: new[]
                {
                    new ItemPassiveDoc("Sturdy", "Blocks flat damage from incoming minion attacks and hero basic hits (min 1 dmg).",
                        new[] { "Trigger: Minion shots & hero basic attacks • Fixed block" },
                        "Post-mitigation block: -6 dmg (floored at 1)."),
                },
                summary: "+80 HP, +12 Arm • Sturdy (Blocks 6 dmg vs basics/minions)"),

            new LaneItemDef(LaneItemIds.ManaDewVial, "Mana Dew Vial", 200, LaneItemCategory.Starter, isApproved: false,
                fantasy: "Morning-gathered arcane dew corked for steady lane casting.",
                addMaxMana: 90f, addManaRegen: 1.2f,
                passiveFlags: ItemPassiveFlags.DewSip,
                passives: new[]
                {
                    new ItemPassiveDoc("Dew Sip", "Grants bonus Mana Regeneration while out of enemy hero combat for 5.0s.",
                        new[] { "Trigger: Out of hero combat ≥5s • Fixed bonus regen" },
                        "Out-of-combat sustain: +1.5 Mana/s after 5.0s without taking hero damage."),
                },
                summary: "+90 Max Mana, +1.2 Mana/s • Dew Sip (+1.5 Mana/s out of combat • Provisional)"),

            new LaneItemDef(LaneItemIds.VitalityVest, "Vitality Vest", 250, LaneItemCategory.Starter, isApproved: false,
                fantasy: "Quilted jerkin sewn with hearty roots for simple early toughness.",
                addMaxHp: 160f, addRegen: 1.5f,
                summary: "+160 HP, +1.5 HP/s"),

            new LaneItemDef(LaneItemIds.SapphireShard, "Sapphire Shard", 200, LaneItemCategory.ProvisionalComponent, isApproved: false,
                fantasy: "Chipped blue prism expanding an arcanist's working reserve.",
                addMaxMana: 160f,
                summary: "+160 Max Mana (Provisional)"),

            new LaneItemDef(LaneItemIds.DewdropCharm, "Dewdrop Charm", 220, LaneItemCategory.ProvisionalComponent, isApproved: false,
                fantasy: "Porous talisman that condenses ambient ether into steady replenishment.",
                addManaRegen: 2.8f,
                summary: "+2.8 Mana/s (Provisional)"),

            new LaneItemDef(LaneItemIds.RunedSigil, "Runed Sigil", 280, LaneItemCategory.ProvisionalComponent, isApproved: false,
                fantasy: "Inscribed discipline seal that trims wasted spellcraft from every cast.",
                addResourceCostReduction: 20f,
                summary: "+20 RCR (Diminishing cost reduction <65% • Provisional)"),

            new LaneItemDef(LaneItemIds.ClockworkPin, "Clockwork Pin", 280, LaneItemCategory.ProvisionalComponent, isApproved: false,
                fantasy: "Brass winding pin keeping ability rhythm crisp without granting attributes.",
                addAbilityHaste: 22f,
                summary: "+22 Ability Haste (CD = Base × 100 / (100 + AH) • Provisional)"),

            new LaneItemDef(LaneItemIds.WellspringFlask, "Wellspring Flask", 520, LaneItemCategory.ProvisionalComponent, isApproved: false,
                fantasy: "Pressurized crystal flask cycling a deeper reserve back into steady flow.",
                addMaxMana: 180f, addManaRegen: 2.8f,
                passiveFlags: ItemPassiveFlags.Wellspring,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.SapphireShard, 1),
                    new RecipeIngredient(LaneItemIds.DewdropCharm, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Wellspring", "Regenerates a percentage of your Maximum Mana every second (unique; replaced by Astral Surge).",
                        new[] { "Max Mana → Flat Mana/s gain [Linear]" },
                        "Bonus Mana/s = +0.4% × MaxMana/s (Sapphire Shard + Dewdrop Charm + 100g)."),
                },
                summary: "+180 Max Mana, +2.8 Mana/s • Wellspring (+0.4% Max Mana/s • Provisional)"),

            // =========================================================================
            // 21 APPROVED COMPONENTS (ZERO Mana / ManaRegen / RCR)
            // =========================================================================
            new LaneItemDef(LaneItemIds.SmallAxe, "Small Axe", 240, LaneItemCategory.Component, isApproved: true,
                fantasy: "A simple weapon embodying raw destructive force.",
                addAttackDamage: 22f,
                summary: "+22 Attack Damage"),

            new LaneItemDef(LaneItemIds.HeavyAxe, "Heavy Axe", 580, LaneItemCategory.Component, isApproved: true,
                fantasy: "A substantial weapon emphasizing raw offensive power.",
                addAttackDamage: 52f,
                recipe: new[] { new RecipeIngredient(LaneItemIds.SmallAxe, 2) },
                summary: "+52 Attack Damage (2 × Small Axe + 100g)"),

            new LaneItemDef(LaneItemIds.ShortSword, "Short Sword", 180, LaneItemCategory.Component, isApproved: true,
                fantasy: "A conventional, practical sword.",
                addAttackDamage: 14f,
                summary: "+14 Attack Damage"),

            new LaneItemDef(LaneItemIds.SteelSword, "Steel Sword", 360, LaneItemCategory.Component, isApproved: true,
                fantasy: "A well-crafted conventional steel weapon.",
                addAttackDamage: 26f,
                recipe: new[] { new RecipeIngredient(LaneItemIds.ShortSword, 1) },
                summary: "+26 Attack Damage (Short Sword + 180g)"),

            new LaneItemDef(LaneItemIds.Titanstone, "Titanstone", 300, LaneItemCategory.Component, isApproved: true,
                fantasy: "An exceptionally dense ancient mineral embodying physical might.",
                addStrength: 25f,
                summary: "+25 Strength (independent attribute; no inherent stats)"),

            new LaneItemDef(LaneItemIds.Windfeather, "Windfeather", 300, LaneItemCategory.Component, isApproved: true,
                fantasy: "A delicate feather embodying nimbleness and physical finesse.",
                addAgility: 25f,
                summary: "+25 Agility (independent attribute; no inherent stats)"),

            new LaneItemDef(LaneItemIds.SagesEye, "Sage's Eye", 300, LaneItemCategory.Component, isApproved: true,
                fantasy: "A luminous gemstone embodying wisdom, ingenuity, understanding and mental clarity.",
                addIntelligence: 25f,
                summary: "+25 Intelligence (independent attribute; no inherent stats)"),

            new LaneItemDef(LaneItemIds.LifeCrystal, "Life Crystal", 220, LaneItemCategory.Component, isApproved: true,
                fantasy: "Crystallized life energy.",
                addMaxHp: 160f,
                summary: "+160 Max Health"),

            new LaneItemDef(LaneItemIds.Heartstone, "Heartstone", 560, LaneItemCategory.Component, isApproved: true,
                fantasy: "A large reddish mineral with a gently pulsating core containing concentrated vitality.",
                addMaxHp: 400f,
                recipe: new[] { new RecipeIngredient(LaneItemIds.LifeCrystal, 2) },
                summary: "+400 Max Health (2 × Life Crystal + 120g)"),

            new LaneItemDef(LaneItemIds.Bloodstone, "Bloodstone", 440, LaneItemCategory.SpecializedComponent, isApproved: true,
                fantasy: "A crimson stone that hungers to finish weakened foes.",
                addMaxHp: 180f,
                passiveFlags: ItemPassiveFlags.WoundedStrike,
                recipe: new[] { new RecipeIngredient(LaneItemIds.LifeCrystal, 1) },
                passives: new[]
                {
                    new ItemPassiveDoc("Wounded Strike", "Increases outgoing hero damage as the target becomes more wounded.",
                        new[] { "Target missing Health % → Damage amplification [Linear] • No attribute scaling" },
                        "Bonus dmg: up to +14% linearly as target approaches 0% HP (replaced by Final Judgment)."),
                },
                summary: "+180 HP • Wounded Strike (+0–14% dmg vs wounded heroes)"),

            new LaneItemDef(LaneItemIds.WoodenShield, "Wooden Shield", 180, LaneItemCategory.Component, isApproved: true,
                fantasy: "Sturdy seasoned timber bound in iron bands to turn away physical blows.",
                addArmor: 18f,
                summary: "+18 Armor"),

            new LaneItemDef(LaneItemIds.SilverAmulet, "Silver Amulet", 200, LaneItemCategory.Component, isApproved: true,
                fantasy: "Etched silver charm that grounds incoming magical force.",
                addMagicResist: 20f,
                summary: "+20 Magic Resist"),

            new LaneItemDef(LaneItemIds.ReinforcedShield, "Reinforced Shield", 640, LaneItemCategory.Component, isApproved: true,
                fantasy: "Layered wood-and-silver kite shield forged for balanced frontline defense.",
                addArmor: 42f, addMagicResist: 26f,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.WoodenShield, 2),
                    new RecipeIngredient(LaneItemIds.SilverAmulet, 1),
                },
                summary: "+42 Armor, +26 MR (1.62:1 Arm:MR • 2 × Wooden Shield + Silver Amulet + 80g)"),

            new LaneItemDef(LaneItemIds.SpringWater, "Spring Water", 200, LaneItemCategory.Component, isApproved: true,
                fantasy: "Pure mountain spring draught carrying steady natural recovery.",
                addRegen: 2.5f,
                summary: "+2.5 HP/s Regen"),

            new LaneItemDef(LaneItemIds.SacredPearl, "Sacred Pearl", 240, LaneItemCategory.Component, isApproved: true,
                fantasy: "Warm lustrous pearl that amplifies restorative techniques and benedictions.",
                addHealPower: 0.20f,
                summary: "+20% Heal Power"),

            new LaneItemDef(LaneItemIds.HealingElixir, "Healing Elixir", 560, LaneItemCategory.SpecializedComponent, isApproved: true,
                fantasy: "A glowing phial whose restorative draught echoes softly through the body.",
                addRegen: 3.2f, addHealPower: 0.25f,
                passiveFlags: ItemPassiveFlags.LingeringRestoration,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.SpringWater, 1),
                    new RecipeIngredient(LaneItemIds.SacredPearl, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Lingering Restoration", "A share of actual Health restored by abilities/items is gently repeated over the next 3.5s. Overhealing and recursive procs never trigger it.",
                        new[] { "Actual Health restored → Repeated healing over 3.5s [Linear] • No attribute scaling" },
                        "Repeats 28% of actual ability/item HP restored smoothly over 3.5s (no overheal, non-recursive)."),
                },
                summary: "+3.2 HP/s, +25% HealPower • Lingering Restoration (28% echo)"),

            new LaneItemDef(LaneItemIds.DragonScale, "Dragon Scale", 620, LaneItemCategory.SpecializedComponent, isApproved: true,
                fantasy: "A rare scale embodying draconic toughness and adaptive protection.",
                addArmor: 42f, addMagicResist: 14f, addStrength: 16f,
                passiveFlags: ItemPassiveFlags.AdaptiveScales,
                passives: new[]
                {
                    new ItemPassiveDoc("Adaptive Scales", "Direct physical or magical hits from an enemy hero build Armor & MR specifically against that attacker. DoTs do not qualify; stacks decay one at a time.",
                        new[]
                        {
                            "Qualifying direct hits (max 6 per attacker) → Resistance share [Bounded diminishing]",
                            "STR → Maximum Armor & MR bonus vs that attacker [Bounded diminishing]",
                        },
                        "Per-attacker bonus = MaxResist(STR) × stacks/(stacks+3), MaxResist = 20 + 35×STR/(STR+65). Holds 3.5s, decays 1 stack/1.2s."),
                },
                summary: "+42 Arm, +14 MR (3.0:1), +16 STR • Adaptive Scales (per-attacker)"),

            new LaneItemDef(LaneItemIds.PrimordialBlood, "Primordial Blood", 540, LaneItemCategory.SpecializedComponent, isApproved: true,
                fantasy: "Blood containing the concentrated life essence and physical might of ancient creatures.",
                addStrength: 18f, addRegen: 3.0f,
                passiveFlags: ItemPassiveFlags.PrimordialVitality,
                passives: new[]
                {
                    new ItemPassiveDoc("Primordial Vitality", "Natural Health Regeneration grows stronger as your Health drops. Does not directly amplify ability/allied heals.",
                        new[] { "Missing Health % → Bonus Health Regeneration [Linear] • No attribute scaling" },
                        "Bonus HP/s = +12.0 × (1 − HP/MaxHP) linearly."),
                },
                summary: "+18 STR, +3.0 HP/s • Primordial Vitality (+0–12 HP/s missing HP)"),

            new LaneItemDef(LaneItemIds.IronDagger, "Iron Dagger", 260, LaneItemCategory.Component, isApproved: true,
                fantasy: "A simple, lightweight iron blade designed for quick and practical attacks.",
                addAttackSpeed: 0.28f,
                summary: "+28% Attack Speed (no AD, no AGI)"),

            new LaneItemDef(LaneItemIds.NimbleDagger, "Nimble Dagger", 660, LaneItemCategory.SpecializedComponent, isApproved: true,
                fantasy: "A refined, lightweight and finely balanced dagger embodying quickness and finesse.",
                addAttackSpeed: 0.34f, addAgility: 25f,
                passiveFlags: ItemPassiveFlags.FirstStrike,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.IronDagger, 1),
                    new RecipeIngredient(LaneItemIds.Windfeather, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("First Strike", "After 3.0s without beginning a basic attack (even during combat or spellcasts), your next opening basic attack winds up faster. Consumed on wind-up start.",
                        new[] { "AGI → Wind-up reduction [Bounded diminishing] • Fixed 3.0s basic inactivity; min wind-up preserved" },
                        "Wind-up × (1 − [0.22 + 0.43×AGI/(AGI+60)]), raw floor 0.08s, final floor 0.065s. Resets on wind-up start."),
                },
                summary: "+34% AS, +25 AGI • First Strike (faster opening basic wind-up)"),

            new LaneItemDef(LaneItemIds.WingedCharm, "Winged Charm", 340, LaneItemCategory.SpecializedComponent, isApproved: true,
                fantasy: "A small talisman shaped like outstretched wings, embodying freedom and swiftness of movement.",
                addMoveSpeed: 0.75f,
                passiveFlags: ItemPassiveFlags.Weightlessness,
                passives: new[]
                {
                    new ItemPassiveDoc("Weightlessness", "Reduces the slow strength of incoming slows without shortening their duration or affecting stuns.",
                        new[] { "Slow strength → Reduced by fixed 25% • Duration unchanged • No stat scaling" },
                        "Effective slow = IncomingSlow × (1 − 0.25); slow & stun durations unchanged."),
                },
                summary: "+0.75 MS • Weightlessness (-25% slow strength; full duration)"),

            // =========================================================================
            // 6 APPROVED CUSTOM ADVANCED ITEMS (ZERO Mana / ManaRegen / RCR)
            // =========================================================================
            new LaneItemDef(LaneItemIds.ExecutionersAxe, "Executioner's Axe", 1550, LaneItemCategory.Advanced, isApproved: true,
                fantasy: "A powerful executioner's weapon embodying raw destructive power and finishing wounded enemies.",
                addAttackDamage: 56f, addStrength: 28f, addMaxHp: 240f,
                passiveFlags: ItemPassiveFlags.FinalJudgment,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.HeavyAxe, 1),
                    new RecipeIngredient(LaneItemIds.Titanstone, 1),
                    new RecipeIngredient(LaneItemIds.Bloodstone, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Final Judgment", "Amplifies all outgoing damage (basic attacks and abilities) against wounded enemy heroes. Replaces Bloodstone's Wounded Strike.",
                        new[]
                        {
                            "Target missing Health % → Damage amplification [Linear]",
                            "STR → Maximum amplification cap [Bounded diminishing]",
                        },
                        "Amp = (1 − TargetHP/MaxHP) × [0.24 + 0.28×STR/(STR+65)]; supersedes Wounded Strike."),
                },
                summary: "+56 AD, +28 STR, +240 HP • Final Judgment (+dmg vs wounded heroes)"),

            new LaneItemDef(LaneItemIds.CowardsShield, "The Coward's Shield", 1200, LaneItemCategory.Advanced, isApproved: true,
                fantasy: "A shield embodying survival instincts, nimbleness and protection while escaping danger.",
                addArmor: 46f, addMagicResist: 28f, addAgility: 28f,
                passiveFlags: ItemPassiveFlags.FlightReflex,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.ReinforcedShield, 1),
                    new RecipeIngredient(LaneItemIds.Windfeather, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Flight Reflex", "Sustained movement away from a recent enemy hero attacker builds bonus Armor & Magic Resist after a short commit delay, with a brief stop/stun grace window and faster decay when pursuing.",
                        new[] { "AGI → Peak Armor & Magic Resist bonus [Linear] • Buildup & decay timing fixed" },
                        "Bonus Arm/MR = (20 + 0.85×AGI) × RetreatFactor; 0.15s commit, 1.55s ramp, 0.75s grace, 1.90s stop / 0.85s pursuit decay."),
                },
                summary: "+46 Arm, +28 MR, +28 AGI • Flight Reflex (retreat Armor/MR buildup)"),

            new LaneItemDef(LaneItemIds.ChaliceOfLife, "Chalice of Life", 1180, LaneItemCategory.Advanced, isApproved: true,
                fantasy: "An enchanted vessel containing extraordinary restorative life energy.",
                addMaxHp: 280f, addRegen: 4.5f, addHealPower: 0.35f,
                passiveFlags: ItemPassiveFlags.FountainOfLife | ItemPassiveFlags.LingeringRestoration,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.LifeCrystal, 1),
                    new RecipeIngredient(LaneItemIds.HealingElixir, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Fountain of Life", "Amplifies all valid incoming healing received by you (natural HP/s, self heals, allied heals, Fountain) as your Health drops. Excludes Lingering Restoration echoes.",
                        new[]
                        {
                            "Wielder missing Health % → Incoming healing boost [Linear]",
                            "Bonus Heal Power → Maximum incoming healing boost [Bounded diminishing]",
                        },
                        "IncomingHealMult = 1 + (1 − HP/MaxHP) × [0.28 + 0.55×BonusHPwr/(BonusHPwr+0.65)]."),
                    new ItemPassiveDoc("Lingering Restoration", "Inherited unchanged from Healing Elixir: repeats 28% of actual ability/item HP restored over 3.5s (no overhealing or recursion).",
                        new[] { "Actual Health restored → Repeated healing over 3.5s [Linear] • No attribute scaling" },
                        "Repeats 28% of net ability/item heal over 3.5s."),
                },
                summary: "+280 HP, +4.5 HP/s, +35% HealPower • Fountain of Life + Lingering Restoration"),

            new LaneItemDef(LaneItemIds.DraconicBlade, "Draconic Blade", 1950, LaneItemCategory.Advanced, isApproved: true,
                fantasy: "A dark steel blade infused with draconic essence, glowing molten cracks, and extraordinary dragon scales.",
                addAttackDamage: 44f, addStrength: 40f, addArmor: 48f, addMagicResist: 16f, addRegen: 4.5f,
                passiveFlags: ItemPassiveFlags.DragonfireAndMoltenEdge | ItemPassiveFlags.AdaptiveScales | ItemPassiveFlags.PrimordialVitality,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.SteelSword, 1),
                    new RecipeIngredient(LaneItemIds.DragonScale, 1),
                    new RecipeIngredient(LaneItemIds.PrimordialBlood, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Dragonfire (Shared Heat)", "Basic attacks and on-hit effects apply Heat stacks (max 8) that burn enemies with Magical damage over time; Heat refreshes on hit and dissipates one stack at a time.",
                        new[]
                        {
                            "Heat stacks → Burn multiplier [Bounded diminishing]",
                            "INT → Burning damage per second [Linear]",
                        },
                        "DPS = (18 + 0.55×INT) × heat/(heat+4), ticks every 0.5s for 3.5s hold + 1 stack/1.0s decay."),
                    new ItemPassiveDoc("Molten Edge (Shared Heat)", "Shared Heat stacks shred the target's Armor and Magic Resist over prolonged skirmishes.",
                        new[]
                        {
                            "Heat stacks → Shred share [Bounded diminishing]",
                            "INT → Maximum flat Armor & MR reduction [Bounded diminishing]",
                        },
                        "Shred = [14 + 26×INT/(INT+65)] × heat/(heat+4) flat Armor & MR."),
                    new ItemPassiveDoc("Adaptive Scales", "Inherited unchanged from Dragon Scale: per-attacker Armor & MR stacks from direct hits.",
                        new[]
                        {
                            "Direct hits → Resistance share [Bounded diminishing]",
                            "STR → Max Armor & MR vs attacker [Bounded diminishing]",
                        },
                        "Per-attacker = (20 + 35×STR/(STR+65)) × stacks/(stacks+3)."),
                    new ItemPassiveDoc("Primordial Vitality", "Inherited unchanged from Primordial Blood: up to +12.0 HP/s natural regen linearly with missing Health.",
                        new[] { "Missing Health % → Natural HP/s [Linear] • No attribute scaling" },
                        "+12.0 × (1 − HP/MaxHP) HP/s."),
                },
                summary: "+44 AD, +40 STR, +48 Arm, +16 MR (3.0:1), +4.5 HP/s • Heat (Dragonfire+Molten Edge) + Adaptive Scales + Primordial Vitality"),

            new LaneItemDef(LaneItemIds.GiantsHeart, "The Giant's Heart", 2100, LaneItemCategory.Advanced, isApproved: true,
                fantasy: "A massive petrified heart taken from an ancient mountain giant, pulsing with amber endurance and weight.",
                addMaxHp: 650f, addStrength: 70f, addRegen: 4.0f,
                passiveFlags: ItemPassiveFlags.AncientVitality | ItemPassiveFlags.GiantsMight | ItemPassiveFlags.ImmovableMountain | ItemPassiveFlags.PrimordialVitality,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.Heartstone, 1),
                    new RecipeIngredient(LaneItemIds.Titanstone, 2),
                    new RecipeIngredient(LaneItemIds.PrimordialBlood, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Ancient Vitality", "Defers a portion of incoming physical, magical, and true damage, paying each hit out evenly over time without double mitigation, re-deferral, or refresh.",
                        new[]
                        {
                            "Max Health → Proportion deferred [Bounded diminishing]",
                            "STR → Deferral payout duration [Bounded diminishing]",
                        },
                        "DeferPct = 0.16 + 0.34×MaxHP/(MaxHP+1800) [<50%]; Duration = 2.4 + 2.6×STR/(STR+80)s."),
                    new ItemPassiveDoc("Giant's Might", "Converts your Maximum Health directly into genuine Attack Damage (scales all AD abilities too).",
                        new[] { "Max Health → Bonus Attack Damage [Linear]" },
                        "Bonus AD = +3.0% of Max Health (MaxHP × 0.030)."),
                    new ItemPassiveDoc("Immovable Mountain", "Reduces forced displacement distance (knockbacks, pulls), never granting complete immunity and never shortening stuns/slows.",
                        new[] { "STR → Displacement distance reduction [Bounded diminishing, <65%]" },
                        "DisplacementFactor = 1 − 0.65×STR/(STR+70)."),
                    new ItemPassiveDoc("Primordial Vitality", "Inherited unchanged from Primordial Blood: up to +12.0 HP/s linearly with missing HP.",
                        new[] { "Missing Health % → Natural HP/s [Linear] • No attribute scaling" },
                        "+12.0 × (1 − HP/MaxHP) HP/s."),
                },
                summary: "+650 HP, +70 STR, +4.0 HP/s (no direct AD/Arm/MR) • Ancient Vitality + Giant's Might + Immovable Mountain + Primordial Vitality"),

            new LaneItemDef(LaneItemIds.BananaBlade, "Banana Blade", 1850, LaneItemCategory.Advanced, isApproved: true,
                fantasy: "A sleek curved blade shaped like a banana — prized for impossible angles and slippery skirmishing.",
                addAttackDamage: 28f, addAttackSpeed: 0.38f, addMoveSpeed: 0.80f, addAgility: 34f, addIntelligence: 34f,
                passiveFlags: ItemPassiveFlags.CrescentStrike | ItemPassiveFlags.SlipperyTrickster | ItemPassiveFlags.FirstStrike | ItemPassiveFlags.Weightlessness,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.SteelSword, 1),
                    new RecipeIngredient(LaneItemIds.SagesEye, 1),
                    new RecipeIngredient(LaneItemIds.NimbleDagger, 1),
                    new RecipeIngredient(LaneItemIds.WingedCharm, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Crescent Strike", "Basic attacks and on-hit effects sweep secondary enemies inside a geometric crescent behind and around the primary target (Physical; never hits primary twice or recurses).",
                        new[]
                        {
                            "Attack Damage → Secondary Physical damage base [Linear]",
                            "AGI → Secondary damage coefficient [Bounded diminishing]",
                            "INT → Crescent reach & size [Bounded diminishing]",
                        },
                        "SecDmg = AD × [0.28 + 0.52×AGI/(AGI+70)]; CrescentRadius = 2.30 + 1.75×INT/(INT+75)u."),
                    new ItemPassiveDoc("Slippery Trickster", "Taking enemy hero damage grants a burst of Move Speed that decays smoothly over 2.2s on a fixed 14s cooldown (no AH scaling; min/towers do not trigger).",
                        new[] { "AGI → Initial Move Speed burst [Bounded diminishing] • INT does not affect • Fixed 14s CD" },
                        "Burst = 0.70 + 1.80×AGI/(AGI+60) MS decaying over 2.2s; fixed 14.0s CD."),
                    new ItemPassiveDoc("First Strike", "Inherited unchanged from Nimble Dagger: faster opening basic wind-up after 3.0s without basic attacks.",
                        new[] { "AGI → Wind-up reduction [Bounded diminishing]" },
                        "Wind-up × (1 − [0.22 + 0.43×AGI/(AGI+60)])."),
                    new ItemPassiveDoc("Weightlessness", "Inherited unchanged from Winged Charm: reduces incoming slow strength by fixed 25%.",
                        new[] { "Fixed 25% slow strength reduction • No stat scaling" },
                        "EffectiveSlow = IncomingSlow × 0.75; full duration kept."),
                },
                summary: "+34 AGI, +34 INT, +38% AS, +28 AD, +0.80 MS • Crescent Strike + Slippery Trickster + First Strike + Weightlessness"),

            // =========================================================================
            // PROVISIONAL ADVANCED ITEMS (Stage D/E baseline; Stage F adds Mana capstones)
            // =========================================================================
            new LaneItemDef(LaneItemIds.KindredCenser, "Kindred Censer", 720, LaneItemCategory.ProvisionalAdvanced, isApproved: false,
                fantasy: "Warm brass censer emitting a protective restorative fragrance to nearby teammates.",
                addHealPower: 0.28f, addRegen: 2.0f, addAlliedAuraRegen: 2.2f,
                passiveFlags: ItemPassiveFlags.AlliedRegenAura,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.SacredPearl, 1),
                    new RecipeIngredient(LaneItemIds.SpringWater, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Kindred Fragrance", "Emits an allied regeneration aura (works on any hero) within 7.5u.",
                        new[] { "Heal Power → Allied aura HP/s output [Linear]" },
                        "Allied aura: +2.2 × HealPower HP/s within 7.5u."),
                },
                summary: "+28% HealPower, +2.0 HP/s, +2.2 Allied Aura HP/s (Provisional)"),

            new LaneItemDef(LaneItemIds.BlightedThorn, "Blighted Thorn", 760, LaneItemCategory.ProvisionalAdvanced, isApproved: false,
                fantasy: "Venom-darkened briar steel whose wounds resist mending.",
                addAttackDamage: 22f, addArmor: 24f,
                passiveFlags: ItemPassiveFlags.Wither,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.ShortSword, 1),
                    new RecipeIngredient(LaneItemIds.WoodenShield, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Wither", "Hero damage applies Grievous Wounds, reducing enemy healing and positive regen by 35% for 3.5s.",
                        new[] { "Fixed -35% incoming heal/regen for 3.5s" },
                        "Enemy healing & positive regen × 0.65 for 3.5s."),
                },
                summary: "+22 AD, +24 Arm • Wither (-35% enemy healing for 3.5s • Provisional)"),

            new LaneItemDef(LaneItemIds.AstralReservoir, "Astral Reservoir", 1120, LaneItemCategory.ProvisionalAdvanced, isApproved: false,
                fantasy: "Starlit vessel holding a deep arcane sea that endlessly feeds itself.",
                addMaxMana: 340f, addManaRegen: 3.5f, addIntelligence: 30f,
                passiveFlags: ItemPassiveFlags.AstralSurge,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.WellspringFlask, 1),
                    new RecipeIngredient(LaneItemIds.SagesEye, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Astral Surge", "Generates Percentage Maximum Mana Regeneration per second. Supersedes Wellspring Flask's Wellspring.",
                        new[] { "Max Mana → Flat Mana/s gain [Linear]" },
                        "Bonus Mana/s = +0.7% × MaxMana/s (supersedes Wellspring; Wellspring Flask + Sage's Eye + 300g)."),
                },
                summary: "+340 Max Mana, +3.5 Mana/s, +30 INT • Astral Surge (+0.7% Max Mana/s • Provisional)"),

            new LaneItemDef(LaneItemIds.TidecallerChalice, "Tidecaller Chalice", 1080, LaneItemCategory.ProvisionalAdvanced, isApproved: false,
                fantasy: "Tidal basin that rushes fresh Mana back as your reserves run dry.",
                addManaRegen: 4.2f, addHealPower: 0.26f, addAbilityHaste: 25f,
                passiveFlags: ItemPassiveFlags.TidecallerFlow,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.DewdropCharm, 1),
                    new RecipeIngredient(LaneItemIds.SacredPearl, 1),
                    new RecipeIngredient(LaneItemIds.ClockworkPin, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Tidecaller Flow", "Grants extra Mana Regeneration scaling linearly as your current Mana percentage drops.",
                        new[] { "Missing Mana % → Bonus Mana/s [Linear] • No attribute scaling" },
                        "Bonus Mana/s = +6.0 × (1 − Mana/MaxMana) linearly."),
                },
                summary: "+4.2 Mana/s, +26% HealPower, +25 AH • Tidecaller Flow (+0–6 Mana/s missing Mana • Provisional)"),

            new LaneItemDef(LaneItemIds.EconomistsTome, "Economist's Tome", 1160, LaneItemCategory.ProvisionalAdvanced, isApproved: false,
                fantasy: "Marginalia-packed ledger that recycles spare magic back into your pool after every spell.",
                addResourceCostReduction: 32f, addAbilityHaste: 25f, addIntelligence: 30f,
                passiveFlags: ItemPassiveFlags.FrugalFocus,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.RunedSigil, 1),
                    new RecipeIngredient(LaneItemIds.ClockworkPin, 1),
                    new RecipeIngredient(LaneItemIds.SagesEye, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Frugal Focus", "After casting a Flat-cost Mana ability, refunds 12% of the Mana actually spent. Does not reduce upfront cost or refund continuous/zero-cost skills.",
                        new[] { "Actual Flat Mana spent → Immediate post-cast refund (clamped to Max Mana)" },
                        "Post-cast refund = 12% × FlatManaSpent (requires full upfront cost first)."),
                },
                summary: "+32 RCR, +25 AH, +30 INT • Frugal Focus (12% Flat Mana refund • Provisional)"),

            new LaneItemDef(LaneItemIds.MomentumGreaves, "Momentum Greaves", 1100, LaneItemCategory.ProvisionalAdvanced, isApproved: false,
                fantasy: "Feather-trimmed striders built for unhurried map rotations.",
                addMoveSpeed: 0.85f, addAgility: 26f,
                passiveFlags: ItemPassiveFlags.Windstrider | ItemPassiveFlags.Weightlessness,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.WingedCharm, 1),
                    new RecipeIngredient(LaneItemIds.Windfeather, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Windstrider", "Sustained movement out of combat ramps bonus Move Speed; taking hero damage drops momentum.",
                        new[] { "AGI → Peak out-of-combat Move Speed [Bounded diminishing]" },
                        "+0.45 + 0.65×AGI/(AGI+45) MS over 3s out of combat."),
                    new ItemPassiveDoc("Weightlessness", "Inherited from Winged Charm: -25% incoming slow strength.",
                        new[] { "Fixed -25% slow strength" },
                        "EffectiveSlow = IncomingSlow × 0.75."),
                },
                summary: "+0.85 MS, +26 AGI • Windstrider + Weightlessness (Provisional)"),

            new LaneItemDef(LaneItemIds.ColossusPauldron, "Colossus Pauldron", 1360, LaneItemCategory.ProvisionalAdvanced, isApproved: false,
                fantasy: "Siege-forged shoulder plate that answers heavy blows with a stone barrier.",
                addMaxHp: 450f, addArmor: 28f, addStrength: 28f,
                passiveFlags: ItemPassiveFlags.TitanAegis,
                recipe: new[]
                {
                    new RecipeIngredient(LaneItemIds.Heartstone, 1),
                    new RecipeIngredient(LaneItemIds.WoodenShield, 1),
                    new RecipeIngredient(LaneItemIds.Titanstone, 1),
                },
                passives: new[]
                {
                    new ItemPassiveDoc("Titan Aegis", "Inflicting Stun/Knockback or dropping below 45% HP grants a 4s absorption shield (12s cooldown).",
                        new[] { "STR → Shield capacity [Linear]" },
                        "Shield = 65 + 2.00 × STR HP for 4.0s (12.0s CD)."),
                },
                summary: "+450 HP, +28 Arm, +28 STR • Titan Aegis (STR shield • Provisional)"),
        };

        public static bool TryGet(int id, out LaneItemDef def)
        {
            int migrated = MigrateItemId(id);
            if (migrated <= 0)
            {
                def = default;
                return false;
            }
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].Id == migrated)
                {
                    def = Catalog[i];
                    return true;
                }
            }
            def = default;
            return false;
        }

        public static List<LaneItemDef> GetUpgradesInto(int itemId)
        {
            var result = new List<LaneItemDef>();
            int canonical = MigrateItemId(itemId);
            if (canonical <= 0) return result;
            for (int i = 0; i < Catalog.Length; i++)
            {
                LaneItemDef c = Catalog[i];
                if (c.Recipe == null) continue;
                for (int r = 0; r < c.Recipe.Length; r++)
                {
                    if (c.Recipe[r].ItemId == canonical)
                    {
                        result.Add(c);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Duplicate-aware recursive recipe resolution.
        /// Direct components are matched before descending into nested sub-recipes so
        /// already-assembled intermediates (e.g. Heartstone vs Life Crystals) are preferred
        /// and unrelated inventory slots are never touched.
        /// </summary>
        public static PurchasePlan ResolvePurchase(int rawTargetId, int[] inventory, int currentGold)
        {
            var plan = new PurchasePlan();
            if (!TryGet(rawTargetId, out LaneItemDef target))
            {
                plan.ValidItem = false;
                plan.Reason = "Unknown item.";
                return plan;
            }

            plan.ValidItem = true;
            plan.TargetItem = target;
            plan.BaseCost = target.Cost;

            int len = inventory != null ? inventory.Length : MaxSlots;
            int[] working = new int[len];
            bool hasFreeSlot = false;
            for (int i = 0; i < len; i++)
            {
                int slotId = inventory != null && i < inventory.Length ? MigrateItemId(inventory[i]) : 0;
                working[i] = slotId;
                if (slotId == 0) hasFreeSlot = true;
            }

            int consumedValue = 0;
            if (target.Recipe != null && target.Recipe.Length > 0)
            {
                for (int r = 0; r < target.Recipe.Length; r++)
                {
                    RecipeIngredient req = target.Recipe[r];
                    ConsumeRecipeIngredient(req.ItemId, req.Quantity, working, plan.ConsumedSlots, ref consumedValue);
                }
            }

            plan.ConsumedValue = consumedValue;
            plan.RemainingCost = Mathf.Max(0, target.Cost - consumedValue);
            plan.HasSlotAfterConsume = hasFreeSlot || plan.ConsumedSlots.Count > 0;
            plan.CanAfford = currentGold >= plan.RemainingCost;

            if (!plan.HasSlotAfterConsume)
            {
                plan.CanPurchase = false;
                plan.Reason = "Inventory full (6/6).";
            }
            else if (!plan.CanAfford)
            {
                plan.CanPurchase = false;
                plan.Reason = $"Need {plan.RemainingCost}g (have {currentGold}g).";
            }
            else
            {
                plan.CanPurchase = true;
                plan.Reason = $"Buy {target.Name} for {plan.RemainingCost}g.";
            }

            return plan;
        }

        public static int GetCombineCost(in LaneItemDef def)
        {
            if (def.Recipe == null || def.Recipe.Length == 0) return 0;
            int childSum = 0;
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                if (TryGet(def.Recipe[i].ItemId, out LaneItemDef child))
                {
                    childSum += child.Cost * def.Recipe[i].Quantity;
                }
            }
            return Mathf.Max(0, def.Cost - childSum);
        }

        /// <summary>
        /// Builds the hierarchical recipe tree for any catalog item with duplicate-aware ownership
        /// and affordability states. Never emits purchasable recipe-scroll nodes.
        /// </summary>
        public static RecipeTreeNode BuildRecipeTree(int rawTargetId, int[] inventory, int currentGold)
        {
            if (!TryGet(rawTargetId, out LaneItemDef rootItem)) return null;

            int len = inventory != null ? inventory.Length : MaxSlots;
            int[] working = new int[len];
            for (int i = 0; i < len; i++)
            {
                working[i] = inventory != null && i < inventory.Length ? MigrateItemId(inventory[i]) : 0;
            }

            return BuildRecipeNodeRecursive(rootItem, depth: 0, requiredQty: 1, parentSatisfied: false, working, inventory, currentGold);
        }

        private static RecipeTreeNode BuildRecipeNodeRecursive(
            LaneItemDef item,
            int depth,
            int requiredQty,
            bool parentSatisfied,
            int[] workingInventory,
            int[] originalInventory,
            int currentGold)
        {
            int directOwned = 0;
            if (depth > 0 && !parentSatisfied)
            {
                for (int q = 0; q < requiredQty; q++)
                {
                    int slot = FindAvailableSlot(workingInventory, item.Id);
                    if (slot >= 0)
                    {
                        workingInventory[slot] = -1;
                        directOwned++;
                    }
                }
            }

            PurchasePlan nextCopyPlan = ResolvePurchase(item.Id, originalInventory, currentGold);
            var node = new RecipeTreeNode
            {
                Item = item,
                Depth = depth,
                RequiredQuantity = requiredQty,
                OwnedDirectCount = directOwned,
                SatisfiedByParent = parentSatisfied,
                DirectRemainingCost = nextCopyPlan.RemainingCost,
                CombineCost = GetCombineCost(in item),
                DirectCanAfford = nextCopyPlan.CanAfford,
                DirectCanPurchase = nextCopyPlan.CanPurchase,
            };

            bool satisfiedThisLevel = parentSatisfied || (depth > 0 && directOwned >= requiredQty);
            int childMultiplier = parentSatisfied ? requiredQty : Mathf.Max(1, requiredQty - directOwned);
            if (item.Recipe != null && item.Recipe.Length > 0)
            {
                for (int i = 0; i < item.Recipe.Length; i++)
                {
                    RecipeIngredient req = item.Recipe[i];
                    if (!TryGet(req.ItemId, out LaneItemDef ingDef)) continue;
                    RecipeTreeNode childNode = BuildRecipeNodeRecursive(
                        ingDef,
                        depth + 1,
                        req.Quantity * childMultiplier,
                        satisfiedThisLevel,
                        workingInventory,
                        originalInventory,
                        currentGold);
                    node.Children.Add(childNode);
                }
            }

            bool hasOwnedDescendant = HasAnyOwnedDescendant(node);
            if (node.IsSatisfied)
            {
                node.State = RecipeNodeState.Owned;
            }
            else if (directOwned > 0 || hasOwnedDescendant)
            {
                node.State = node.DirectCanPurchase ? RecipeNodeState.PartialAffordable : RecipeNodeState.PartialUnaffordable;
            }
            else
            {
                node.State = node.DirectCanPurchase ? RecipeNodeState.MissingAffordable : RecipeNodeState.MissingUnaffordable;
            }

            return node;
        }

        private static bool HasAnyOwnedDescendant(RecipeTreeNode node)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                RecipeTreeNode ch = node.Children[i];
                if (ch.OwnedDirectCount > 0 || HasAnyOwnedDescendant(ch)) return true;
            }
            return false;
        }

        /// <summary>
        /// Deterministic MOBA automatic component purchasing.
        /// Policy order:
        ///   1) Complete target immediately if affordable and slots permit.
        ///   2) Otherwise greedily step through missing recipe nodes using current gold/slots:
        ///      (a) must pass strict monotonic RemainingCost reduction on target & preserve unrelated slots,
        ///      (b) prefer upgrades consuming owned components over occupying new slots,
        ///      (c) prefer shallower intermediates directly in the recipe tree over deeper leaves,
        ///      (d) prefer higher-cost recipe ingredients first, then declaration order.
        /// Never purchases unrelated items, never sells items, and never creates recipe scrolls.
        /// </summary>
        public static AutoPurchasePlan ResolveAutoPurchase(int rawTargetId, int[] inventory, int currentGold)
        {
            var auto = new AutoPurchasePlan();
            PurchasePlan initialTargetPlan = ResolvePurchase(rawTargetId, inventory, currentGold);
            auto.ValidItem = initialTargetPlan.ValidItem;
            auto.TargetItem = initialTargetPlan.TargetItem;
            auto.InitialRemainingCost = initialTargetPlan.RemainingCost;
            auto.FinalRemainingCost = initialTargetPlan.RemainingCost;

            if (!initialTargetPlan.ValidItem)
            {
                auto.Reason = initialTargetPlan.Reason;
                auto.ActionLabel = "Invalid Item";
                return auto;
            }

            LaneItemDef target = initialTargetPlan.TargetItem;
            if (initialTargetPlan.CanPurchase)
            {
                auto.BuysCompleteTarget = true;
                auto.TotalGoldSpent = initialTargetPlan.RemainingCost;
                auto.FinalRemainingCost = 0;
                auto.StepItemIds.Add(target.Id);
                auto.StepCosts.Add(initialTargetPlan.RemainingCost);
                auto.StepNames.Add(target.Name);
                bool isUpgrade = initialTargetPlan.ConsumedSlots.Count > 0 && initialTargetPlan.RemainingCost < target.Cost;
                auto.ActionLabel = isUpgrade
                    ? $"Upgrade {target.Name} ({initialTargetPlan.RemainingCost}g)"
                    : $"Buy {target.Name} ({initialTargetPlan.RemainingCost}g)";
                auto.Reason = initialTargetPlan.Reason;
                return auto;
            }

            if (!target.HasRecipe)
            {
                auto.ActionLabel = initialTargetPlan.Reason;
                auto.Reason = initialTargetPlan.Reason;
                return auto;
            }

            int len = inventory != null ? inventory.Length : MaxSlots;
            int[] simInv = new int[len];
            for (int i = 0; i < len; i++)
            {
                simInv[i] = inventory != null && i < inventory.Length ? MigrateItemId(inventory[i]) : 0;
            }
            int simGold = Mathf.Max(0, currentGold);

            var candidates = new List<RecipeTreeNode>();
            for (int iter = 0; iter < 12; iter++)
            {
                PurchasePlan targetNow = ResolvePurchase(target.Id, simInv, simGold);
                if (targetNow.CanPurchase)
                {
                    ApplySimulatedPurchase(targetNow, simInv, ref simGold);
                    auto.BuysCompleteTarget = true;
                    auto.StepItemIds.Add(target.Id);
                    auto.StepCosts.Add(targetNow.RemainingCost);
                    auto.StepNames.Add(target.Name);
                    auto.TotalGoldSpent += targetNow.RemainingCost;
                    break;
                }

                RecipeTreeNode tree = BuildRecipeTree(target.Id, simInv, simGold);
                candidates.Clear();
                CollectUnownedCandidates(tree, candidates);

                PurchasePlan bestPlan = null;
                int bestConsumes = -1;
                int bestNegDepth = int.MinValue;
                int bestCost = -1;
                int prevRem = targetNow.RemainingCost;

                for (int c = 0; c < candidates.Count; c++)
                {
                    RecipeTreeNode node = candidates[c];
                    PurchasePlan cp = ResolvePurchase(node.Item.Id, simInv, simGold);
                    if (!cp.CanPurchase || cp.RemainingCost <= 0) continue;

                    int[] trialInv = (int[])simInv.Clone();
                    int trialGold = simGold;
                    if (!ApplySimulatedPurchase(cp, trialInv, ref trialGold)) continue;

                    PurchasePlan afterTarget = ResolvePurchase(target.Id, trialInv, trialGold);
                    int reduction = prevRem - afterTarget.RemainingCost;
                    if (reduction < cp.RemainingCost || reduction <= 0) continue;

                    int consumes = cp.ConsumedSlots.Count > 0 ? 1 : 0;
                    int negDepth = -node.Depth;
                    int baseCost = node.Item.Cost;

                    bool better = bestPlan == null ||
                                  consumes > bestConsumes ||
                                  (consumes == bestConsumes && negDepth > bestNegDepth) ||
                                  (consumes == bestConsumes && negDepth == bestNegDepth && baseCost > bestCost);
                    if (better)
                    {
                        bestPlan = cp;
                        bestConsumes = consumes;
                        bestNegDepth = negDepth;
                        bestCost = baseCost;
                    }
                }

                if (bestPlan == null) break;

                ApplySimulatedPurchase(bestPlan, simInv, ref simGold);
                auto.StepItemIds.Add(bestPlan.TargetItem.Id);
                auto.StepCosts.Add(bestPlan.RemainingCost);
                auto.StepNames.Add(bestPlan.TargetItem.Name);
                auto.TotalGoldSpent += bestPlan.RemainingCost;
            }

            PurchasePlan finalPlan = ResolvePurchase(target.Id, simInv, simGold);
            auto.FinalRemainingCost = finalPlan.RemainingCost;

            if (auto.StepItemIds.Count == 0)
            {
                auto.Reason = initialTargetPlan.Reason;
                auto.ActionLabel = initialTargetPlan.Reason;
                return auto;
            }

            string summary = SummarizeSteps(auto.StepNames);
            if (auto.BuysCompleteTarget)
            {
                auto.ActionLabel = $"Complete {target.Name} ({auto.TotalGoldSpent}g)";
                auto.Reason = $"Completed {target.Name} ({summary}) for {auto.TotalGoldSpent}g.";
            }
            else
            {
                auto.ActionLabel = $"Buy {summary} ({auto.TotalGoldSpent}g)";
                auto.Reason = $"Bought components: {summary} ({auto.TotalGoldSpent}g; {auto.FinalRemainingCost}g left for {target.Name}).";
            }
            return auto;
        }

        private static void CollectUnownedCandidates(RecipeTreeNode node, List<RecipeTreeNode> list)
        {
            if (node == null) return;
            if (node.Depth >= 1 && !node.IsSatisfied)
            {
                list.Add(node);
            }
            for (int i = 0; i < node.Children.Count; i++)
            {
                CollectUnownedCandidates(node.Children[i], list);
            }
        }

        private static bool ApplySimulatedPurchase(PurchasePlan plan, int[] working, ref int gold)
        {
            if (plan == null || !plan.CanPurchase || gold < plan.RemainingCost) return false;
            int targetSlot = -1;
            if (plan.ConsumedSlots.Count > 0)
            {
                targetSlot = plan.ConsumedSlots[0];
                for (int c = 0; c < plan.ConsumedSlots.Count; c++)
                {
                    working[plan.ConsumedSlots[c]] = 0;
                }
            }
            else
            {
                for (int i = 0; i < working.Length; i++)
                {
                    if (working[i] == 0)
                    {
                        targetSlot = i;
                        break;
                    }
                }
            }
            if (targetSlot < 0) return false;
            working[targetSlot] = plan.TargetItem.Id;
            gold -= plan.RemainingCost;
            return true;
        }

        private static string SummarizeSteps(List<string> names)
        {
            if (names == null || names.Count == 0) return string.Empty;
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i];
                if (!counts.ContainsKey(n))
                {
                    counts[n] = 0;
                    order.Add(n);
                }
                counts[n]++;
            }
            var parts = new List<string>(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                string n = order[i];
                parts.Add(counts[n] > 1 ? $"{counts[n]}× {n}" : n);
            }
            return parts.Count <= 2 ? string.Join(" + ", parts) : $"{names.Count} components";
        }

        private static void ConsumeRecipeIngredient(
            int requiredId,
            int quantity,
            int[] workingInventory,
            List<int> consumedSlots,
            ref int consumedValue)
        {
            for (int q = 0; q < quantity; q++)
            {
                int directSlot = FindAvailableSlot(workingInventory, requiredId);
                if (directSlot >= 0)
                {
                    workingInventory[directSlot] = -1;
                    consumedSlots.Add(directSlot);
                    if (TryGet(requiredId, out LaneItemDef ingDef))
                    {
                        consumedValue += ingDef.Cost;
                    }
                }
                else if (TryGet(requiredId, out LaneItemDef ingDef) && ingDef.Recipe != null && ingDef.Recipe.Length > 0)
                {
                    for (int s = 0; s < ingDef.Recipe.Length; s++)
                    {
                        RecipeIngredient sub = ingDef.Recipe[s];
                        ConsumeRecipeIngredient(sub.ItemId, sub.Quantity, workingInventory, consumedSlots, ref consumedValue);
                    }
                }
            }
        }

        private static int FindAvailableSlot(int[] workingInventory, int canonicalId)
        {
            for (int i = 0; i < workingInventory.Length; i++)
            {
                if (workingInventory[i] == canonicalId) return i;
            }
            return -1;
        }
    }
}
