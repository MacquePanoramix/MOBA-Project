using System;
using System.Collections.Generic;

namespace Moba.LaneSlice
{
    public enum ItemBrowseTab : byte
    {
        All = 0,
        Starters = 1,
        Basic = 2,
        IntermediateSpec = 3,
        Advanced = 4,
        Provisional = 5,
    }

    public enum ItemDisplayGroup : byte
    {
        Starters = 0,
        BasicComponents = 1,
        IntermediateAndSpecialized = 2,
        AdvancedItems = 3,
    }

    [Flags]
    public enum ItemStatFilter : uint
    {
        None = 0u,
        AttackDamage = 1u << 0,
        AttackSpeed = 1u << 1,
        MaxHp = 1u << 2,
        HealthRegen = 1u << 3,
        Armor = 1u << 4,
        MagicResist = 1u << 5,
        MoveSpeed = 1u << 6,
        HealPower = 1u << 7,
        AbilityHaste = 1u << 8,
        Strength = 1u << 9,
        Agility = 1u << 10,
        Intelligence = 1u << 11,
        MaxMana = 1u << 12,
        ManaRegen = 1u << 13,
        ResourceCostReduction = 1u << 14,
    }

    public readonly struct StatFilterMeta
    {
        public readonly ItemStatFilter Flag;
        public readonly string ShortLabel;
        public readonly string FullLabel;
        public readonly string ColorHex;

        public StatFilterMeta(ItemStatFilter flag, string shortLabel, string fullLabel, string colorHex)
        {
            Flag = flag;
            ShortLabel = shortLabel;
            FullLabel = fullLabel;
            ColorHex = colorHex;
        }
    }

    /// <summary>
    /// Shared search, 15-stat multi-filter, and non-restrictive progression grouping
    /// used by both the Fountain Shop and the Item Compendium.
    /// Stat filters evaluate DIRECTLY granted stats only (so passives like Giant's Might do not masquerade as direct AD).
    /// Text queries search Name, Fantasy, Summary, direct stat keywords, and Passives (including ScalingArrows).
    /// </summary>
    public static class LaneItemCatalogQuery
    {
        public static readonly StatFilterMeta[] AllStatFilters = new[]
        {
            new StatFilterMeta(ItemStatFilter.AttackDamage, "AD", "Attack Damage", LaneHeroSheet.ColAd),
            new StatFilterMeta(ItemStatFilter.AttackSpeed, "AS", "Attack Speed", LaneHeroSheet.ColAgi),
            new StatFilterMeta(ItemStatFilter.MaxHp, "HP", "Max Health", LaneHeroSheet.ColHp),
            new StatFilterMeta(ItemStatFilter.HealthRegen, "HP/s", "Health Regeneration", LaneHeroSheet.ColLive),
            new StatFilterMeta(ItemStatFilter.Armor, "Arm", "Armor", LaneHeroSheet.ColArm),
            new StatFilterMeta(ItemStatFilter.MagicResist, "MR", "Magic Resistance", LaneHeroSheet.ColMr),
            new StatFilterMeta(ItemStatFilter.MoveSpeed, "MS", "Movement Speed", LaneHeroSheet.ColCc),
            new StatFilterMeta(ItemStatFilter.HealPower, "Heal%", "Heal Power", LaneHeroSheet.ColHeal),
            new StatFilterMeta(ItemStatFilter.AbilityHaste, "Haste", "Ability Haste", LaneHeroSheet.ColInt),
            new StatFilterMeta(ItemStatFilter.Strength, "STR", "Strength", LaneHeroSheet.ColStr),
            new StatFilterMeta(ItemStatFilter.Agility, "AGI", "Agility", LaneHeroSheet.ColAgi),
            new StatFilterMeta(ItemStatFilter.Intelligence, "INT", "Intelligence", LaneHeroSheet.ColInt),
            new StatFilterMeta(ItemStatFilter.MaxMana, "Mana", "Maximum Mana", LaneHeroSheet.ColMana),
            new StatFilterMeta(ItemStatFilter.ManaRegen, "MP/s", "Mana Regeneration", LaneHeroSheet.ColMana),
            new StatFilterMeta(ItemStatFilter.ResourceCostReduction, "RCR", "Resource Cost Reduction", LaneHeroSheet.ColMana),
        };

        public static readonly ItemDisplayGroup[] GroupOrder =
        {
            ItemDisplayGroup.Starters,
            ItemDisplayGroup.BasicComponents,
            ItemDisplayGroup.IntermediateAndSpecialized,
            ItemDisplayGroup.AdvancedItems,
        };

        public static ItemDisplayGroup GetGroup(in LaneItemDef item)
        {
            switch (item.Category)
            {
                case LaneItemCategory.Starter:
                    return ItemDisplayGroup.Starters;
                case LaneItemCategory.Component:
                case LaneItemCategory.ProvisionalComponent:
                    return item.HasRecipe ? ItemDisplayGroup.IntermediateAndSpecialized : ItemDisplayGroup.BasicComponents;
                case LaneItemCategory.SpecializedComponent:
                    return ItemDisplayGroup.IntermediateAndSpecialized;
                case LaneItemCategory.Advanced:
                case LaneItemCategory.ProvisionalAdvanced:
                default:
                    return ItemDisplayGroup.AdvancedItems;
            }
        }

        public static string GroupLabel(ItemDisplayGroup g)
        {
            switch (g)
            {
                case ItemDisplayGroup.Starters: return "Starting Items";
                case ItemDisplayGroup.BasicComponents: return "Basic Components";
                case ItemDisplayGroup.IntermediateAndSpecialized: return "Intermediate & Specialized Components";
                case ItemDisplayGroup.AdvancedItems: return "Advanced Items";
                default: return g.ToString();
            }
        }

        public static string TabLabel(ItemBrowseTab tab)
        {
            switch (tab)
            {
                case ItemBrowseTab.All: return "All (44)";
                case ItemBrowseTab.Starters: return "Starters";
                case ItemBrowseTab.Basic: return "Basic";
                case ItemBrowseTab.IntermediateSpec: return "Interm / Spec";
                case ItemBrowseTab.Advanced: return "Advanced";
                case ItemBrowseTab.Provisional: return "Provisional";
                default: return tab.ToString();
            }
        }

        public static bool MatchesTab(in LaneItemDef item, ItemBrowseTab tab)
        {
            switch (tab)
            {
                case ItemBrowseTab.Starters:
                    return item.Category == LaneItemCategory.Starter;
                case ItemBrowseTab.Basic:
                    return (item.Category == LaneItemCategory.Component || item.Category == LaneItemCategory.ProvisionalComponent) && !item.HasRecipe;
                case ItemBrowseTab.IntermediateSpec:
                    return item.Category == LaneItemCategory.SpecializedComponent ||
                           ((item.Category == LaneItemCategory.Component || item.Category == LaneItemCategory.ProvisionalComponent) && item.HasRecipe);
                case ItemBrowseTab.Advanced:
                    return item.Category == LaneItemCategory.Advanced || item.Category == LaneItemCategory.ProvisionalAdvanced;
                case ItemBrowseTab.Provisional:
                    return !item.IsApproved;
                case ItemBrowseTab.All:
                default:
                    return true;
            }
        }

        public static bool MatchesDirectStat(in LaneItemDef item, ItemStatFilter flag)
        {
            switch (flag)
            {
                case ItemStatFilter.AttackDamage: return item.AddAttackDamage > 0f;
                case ItemStatFilter.AttackSpeed: return item.AddAttackSpeed > 0f;
                case ItemStatFilter.MaxHp: return item.AddMaxHp > 0f;
                case ItemStatFilter.HealthRegen: return item.AddRegen > 0f || item.AddAlliedAuraRegen > 0f;
                case ItemStatFilter.Armor: return item.AddArmor > 0f;
                case ItemStatFilter.MagicResist: return item.AddMagicResist > 0f;
                case ItemStatFilter.MoveSpeed: return item.AddMoveSpeed > 0f;
                case ItemStatFilter.HealPower: return item.AddHealPower > 0f;
                case ItemStatFilter.AbilityHaste: return item.AddAbilityHaste > 0f;
                case ItemStatFilter.Strength: return item.AddStrength > 0f;
                case ItemStatFilter.Agility: return item.AddAgility > 0f;
                case ItemStatFilter.Intelligence: return item.AddIntelligence > 0f;
                case ItemStatFilter.MaxMana: return item.AddMaxMana > 0f;
                case ItemStatFilter.ManaRegen: return item.AddManaRegen > 0f || item.AddPercentManaRegen > 0f;
                case ItemStatFilter.ResourceCostReduction: return item.AddResourceCostReduction > 0f;
                default: return false;
            }
        }

        public static bool MatchesStatFilters(in LaneItemDef item, ItemStatFilter filter)
        {
            if (filter == ItemStatFilter.None) return true;
            for (int i = 0; i < AllStatFilters.Length; i++)
            {
                ItemStatFilter bit = AllStatFilters[i].Flag;
                if ((filter & bit) != 0 && !MatchesDirectStat(in item, bit))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool MatchesSearch(in LaneItemDef item, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;

            string[] tokens = query.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            for (int t = 0; t < tokens.Length; t++)
            {
                if (!MatchesToken(in item, tokens[t])) return false;
            }
            return true;
        }

        private static bool MatchesToken(in LaneItemDef item, string tok)
        {
            if (ContainsCI(item.Name, tok) || ContainsCI(item.Fantasy, tok) || ContainsCI(item.Summary, tok))
            {
                return true;
            }

            if (item.AddAttackDamage > 0f && (EqCI(tok, "ad") || ContainsCI("attack damage", tok))) return true;
            if (item.AddAttackSpeed > 0f && (EqCI(tok, "as") || ContainsCI("attack speed", tok))) return true;
            if (item.AddMaxHp > 0f && (EqCI(tok, "hp") || ContainsCI("max health", tok))) return true;
            if ((item.AddRegen > 0f || item.AddAlliedAuraRegen > 0f) && (ContainsCI("hp/s", tok) || ContainsCI("health regeneration", tok) || ContainsCI("regen", tok))) return true;
            if (item.AddArmor > 0f && (EqCI(tok, "arm") || ContainsCI("armor", tok))) return true;
            if (item.AddMagicResist > 0f && (EqCI(tok, "mr") || ContainsCI("magic resistance", tok) || ContainsCI("magic resist", tok))) return true;
            if (item.AddMoveSpeed > 0f && (EqCI(tok, "ms") || ContainsCI("movement speed", tok) || ContainsCI("move speed", tok))) return true;
            if (item.AddHealPower > 0f && ContainsCI("heal power", tok)) return true;
            if (item.AddAbilityHaste > 0f && (EqCI(tok, "ah") || ContainsCI("ability haste", tok))) return true;
            if (item.AddStrength > 0f && (EqCI(tok, "str") || ContainsCI("strength", tok))) return true;
            if (item.AddAgility > 0f && (EqCI(tok, "agi") || ContainsCI("agility", tok))) return true;
            if (item.AddIntelligence > 0f && (EqCI(tok, "int") || ContainsCI("intelligence", tok))) return true;
            if (item.AddMaxMana > 0f && ContainsCI("max mana", tok)) return true;
            if ((item.AddManaRegen > 0f || item.AddPercentManaRegen > 0f) && (ContainsCI("mana regeneration", tok) || ContainsCI("mana/s", tok) || ContainsCI("mp/s", tok))) return true;
            if (item.AddResourceCostReduction > 0f && (EqCI(tok, "rcr") || ContainsCI("resource cost reduction", tok))) return true;

            if (!item.IsApproved && ContainsCI("provisional", tok)) return true;
            if (item.IsApproved && ContainsCI("approved", tok)) return true;

            if (item.Passives != null)
            {
                for (int p = 0; p < item.Passives.Length; p++)
                {
                    ItemPassiveDoc doc = item.Passives[p];
                    if (ContainsCI(doc.Name, tok) || ContainsCI(doc.PlainSummary, tok) || ContainsCI(doc.MathLine, tok))
                    {
                        return true;
                    }
                    for (int sa = 0; sa < doc.ScalingArrows.Length; sa++)
                    {
                        if (ContainsCI(doc.ScalingArrows[sa], tok)) return true;
                    }
                }
            }
            return false;
        }

        public static void FilterItems(ItemBrowseTab tab, ItemStatFilter statFilter, string searchQuery, List<LaneItemDef> results)
        {
            results.Clear();
            for (int i = 0; i < LaneItems.Catalog.Length; i++)
            {
                LaneItemDef item = LaneItems.Catalog[i];
                if (!MatchesTab(in item, tab)) continue;
                if (!MatchesStatFilters(in item, statFilter)) continue;
                if (!MatchesSearch(in item, searchQuery)) continue;
                results.Add(item);
            }
        }

        private static bool ContainsCI(string haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool EqCI(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
