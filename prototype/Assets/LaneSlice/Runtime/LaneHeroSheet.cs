using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Data-driven MOBA Hero Guide builder driving both the accessible Hero Guide UI
    /// (Plain description, Key facts, What scales, expandable Show Math, Practical tips)
    /// and HUD hover tooltips directly from live LaneTuning formulas.
    /// Explicitly distinguishes Linear, Bounded diminishing, Unbounded diminishing, and Inverse duration curves.
    /// </summary>
    public static class LaneHeroSheet
    {
        public const string ColAd = "#ff9c4a";
        public const string ColStr = "#ff6b6b";
        public const string ColAgi = "#5de287";
        public const string ColInt = "#62c6ff";
        public const string ColHeal = "#66ffd1";
        public const string ColHp = "#79e278";
        public const string ColArm = "#e3b363";
        public const string ColMr = "#8ec8ff";
        public const string ColMagic = "#c58cff";
        public const string ColPhys = "#ffbd59";
        public const string ColCc = "#ffe066";
        public const string ColLive = "#78ff9e";
        public const string ColMana = "#4da6ff";
        public const string ColMuted = "#9fb0c2";
        public const string ColText = "#e8eff7";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public sealed class KeyFactPill
        {
            public string Label;
            public string Value;
            public string ColorHex;
        }

        public sealed class ScalingRow
        {
            public string StatTag;
            public string StatColorHex;
            public string AffectedProperty;
            public string CurveLabel;
            public string SummaryText;
            public string LiveText;
        }

        public sealed class AbilityCardData
        {
            public LaneAbilitySlot Slot;
            public int CardIndex;
            public string KeyBadge;
            public string Name;
            public string MetaRight;
            public string PlainDescription;
            public string PracticalTip;
            public readonly List<KeyFactPill> KeyFacts = new List<KeyFactPill>();
            public readonly List<ScalingRow> ScalingRows = new List<ScalingRow>();
            public readonly List<string> StatLines = new List<string>();
            public readonly List<string> MathLines = new List<string>();
            public string LiveLine;

            public string Description => PlainDescription;
            public string MathLine => MathLines.Count > 0 ? string.Join("\n", MathLines) : string.Empty;
        }

        public sealed class HeroGuideData
        {
            public LaneHeroId Hero;
            public string Title;
            public string RoleLine;
            public string IdentitySummary;
            public string BaseStatsLine;
            public string LiveStatsLine;
            public readonly List<AbilityCardData> Cards = new List<AbilityCardData>();
            public readonly List<string> RuleLines = new List<string>();
        }

        private static string F(float v, string fmt = "0") => v.ToString(fmt, Inv);

        public static string FormatFormula(
            StatFormula f,
            string statName,
            string unit = "",
            bool asPercent = false,
            int maxRanks = 3)
        {
            var sb = new StringBuilder();
            AppendRankProgression(sb, f, unit, asPercent, maxRanks);

            if (f.curve == ScalingCurve.UnboundedSqrt)
            {
                if (unit == "x")
                {
                    float s25 = f.Evaluate(1, 25f);
                    float s50 = f.Evaluate(1, 50f);
                    sb.Append($" (sqrt +{F(f.halfPoint, "0.###")}/pt, Unbounded diminishing • {F(s25, "0.00")}x @ 25, {F(s50, "0.00")}x @ 50)");
                }
                else
                {
                    string b25 = FormatBonusValue(f.EvaluateBonusAt(25f), unit, asPercent);
                    string b50 = FormatBonusValue(f.EvaluateBonusAt(50f), unit, asPercent);
                    sb.Append($" (Unbounded sqrt +{b25} @ 25, +{b50} @ 50)");
                }
                return sb.ToString();
            }

            if (unit == "x" && f.curve == ScalingCurve.Linear)
            {
                float ratePct = f.coeffOrMaxBonus * 100f;
                float s25 = f.Evaluate(1, 25f);
                float s50 = f.Evaluate(1, 50f);
                sb.Append($" (+{F(ratePct, "0.#")}% / {statName}, Linear • {F(s25, "0.00")}x @ 25, {F(s50, "0.00")}x @ 50)");
                return sb.ToString();
            }

            if (f.curve == ScalingCurve.Linear)
            {
                if (Mathf.Abs(f.coeffOrMaxBonus) > 0.0001f)
                {
                    if (!asPercent && string.IsNullOrEmpty(unit))
                    {
                        sb.Append($" + {F(f.coeffOrMaxBonus * 100f)}% {statName} (Linear)");
                    }
                    else if (asPercent)
                    {
                        sb.Append($" + {F(f.coeffOrMaxBonus * 100f, "0.#")}% / {statName} (Linear)");
                    }
                    else
                    {
                        sb.Append($" + {F(f.coeffOrMaxBonus, "0.##")}{unit} / {statName} (Linear)");
                    }
                }
            }
            else if (f.coeffOrMaxBonus < 0f)
            {
                float s50 = f.Evaluate(1, 50f);
                float floor = f.baseValue + f.coeffOrMaxBonus;
                sb.Append($" (Bounded diminishing toward {F(floor, "0.00")}{unit} • {F(s50, "0.00")}{unit} @ 50)");
            }
            else
            {
                string rateStr = FormatBonusRatePer10(f, statName, unit, asPercent);
                string b25Str = FormatBonusValue(f.EvaluateBonusAt(25f), unit, asPercent);
                string b50Str = FormatBonusValue(f.EvaluateBonusAt(50f), unit, asPercent);
                sb.Append($" (+{rateStr}, Bounded diminishing • +{b25Str} @ 25, +{b50Str} @ 50)");
            }

            return sb.ToString();
        }

        public static string FormatPrettyFormula(
            StatFormula f,
            string baseColorHex,
            string statName,
            string statColorHex,
            string unit = "",
            bool asPercent = false,
            int maxRanks = 3)
        {
            var baseSb = new StringBuilder();
            AppendRankProgression(baseSb, f, unit, asPercent, maxRanks);

            string scaling;
            if (f.curve == ScalingCurve.UnboundedSqrt)
            {
                if (unit == "x")
                {
                    float s25 = f.Evaluate(1, 25f);
                    float s100 = f.Evaluate(1, 100f);
                    scaling = $"(Unbounded sqrt via {statName} • {F(s25, "0.00")}x @ 25, {F(s100, "0.00")}x @ 100)";
                    return $"<color={baseColorHex}><b>{F(f.baseValue, "0.00")}x</b></color> <color={statColorHex}>{scaling}</color>";
                }
                string sq25 = FormatBonusValue(f.EvaluateBonusAt(25f), unit, asPercent);
                string sq100 = FormatBonusValue(f.EvaluateBonusAt(100f), unit, asPercent);
                scaling = $"(Unbounded sqrt via {statName} • +{sq25} @ 25, +{sq100} @ 100)";
            }
            else if (unit == "x" && f.curve == ScalingCurve.Linear)
            {
                float ratePct = f.coeffOrMaxBonus * 100f;
                float s25 = f.Evaluate(1, 25f);
                float s50 = f.Evaluate(1, 50f);
                scaling = $"(Linear +{F(ratePct, "0.#")}% / {statName} • {F(s25, "0.00")}x @ 25, {F(s50, "0.00")}x @ 50)";
                return $"<color={baseColorHex}><b>{F(f.baseValue, "0.00")}x</b></color> <color={statColorHex}>{scaling}</color>";
            }
            else if (f.curve == ScalingCurve.Linear)
            {
                if (Mathf.Abs(f.coeffOrMaxBonus) <= 0.0001f)
                {
                    return $"<color={baseColorHex}><b>{baseSb}</b></color>";
                }
                if (!asPercent && string.IsNullOrEmpty(unit))
                {
                    scaling = $"(+{F(f.coeffOrMaxBonus * 100f)}% {statName}, Linear)";
                }
                else if (asPercent)
                {
                    scaling = $"(+{F(f.coeffOrMaxBonus * 100f, "0.#")}% / {statName}, Linear)";
                }
                else
                {
                    string b25 = FormatBonusValue(f.EvaluateBonusAt(25f), unit, false);
                    string b50 = FormatBonusValue(f.EvaluateBonusAt(50f), unit, false);
                    scaling = $"(+{F(f.coeffOrMaxBonus, "0.##")}{unit} / {statName}, Linear • +{b25} @ 25, +{b50} @ 50)";
                }
            }
            else if (f.coeffOrMaxBonus < 0f)
            {
                float s25 = f.Evaluate(1, 25f);
                float s50 = f.Evaluate(1, 50f);
                float floor = f.baseValue + f.coeffOrMaxBonus;
                scaling = $"(Bounded diminishing via {statName} → {F(floor, "0.00")}{unit} • {F(s25, "0.00")}{unit} @ 25, {F(s50, "0.00")}{unit} @ 50)";
            }
            else
            {
                string rateStr = FormatBonusRatePer10(f, statName, unit, asPercent);
                string b25Str = FormatBonusValue(f.EvaluateBonusAt(25f), unit, asPercent);
                string b50Str = FormatBonusValue(f.EvaluateBonusAt(50f), unit, asPercent);
                scaling = $"(+{rateStr}, Bounded diminishing • +{b25Str} @ 25, +{b50Str} @ 50)";
            }

            return $"<color={baseColorHex}><b>{baseSb}</b></color> <color={statColorHex}>{scaling}</color>";
        }

        public static string FormatExactMath(
            StatFormula f,
            string statName,
            string unit = "",
            bool asPercent = false,
            int maxRanks = 3,
            string customDomain = "")
        {
            string sLabel = string.IsNullOrEmpty(customDomain) ? statName : customDomain;
            if (f.curve == ScalingCurve.UnboundedSqrt)
            {
                if (unit == "x")
                {
                    return $"[Unbounded diminishing → Inverse duration] Speed = sqrt(1 + {F(f.halfPoint, "0.####")} × {sLabel}); Duration = Base / Speed";
                }
                string coeffStr = asPercent ? $"{F(f.coeffOrMaxBonus * 100f, "0.#")}%" : $"{F(f.coeffOrMaxBonus, "0.##")}{unit}";
                var bSb = new StringBuilder();
                AppendRankProgression(bSb, f, unit, asPercent, maxRanks);
                return $"[Unbounded diminishing] [{bSb}] + {coeffStr} × (sqrt(1 + {F(f.halfPoint, "0.####")} × {sLabel}) - 1)";
            }

            var baseSb = new StringBuilder();
            AppendRankProgression(baseSb, f, unit, asPercent, maxRanks);
            if (f.curve == ScalingCurve.Linear)
            {
                if (Mathf.Abs(f.coeffOrMaxBonus) <= 0.0001f)
                {
                    return $"[Rank-only] {baseSb}";
                }
                string mult = asPercent ? $"{F(f.coeffOrMaxBonus * 100f, "0.#")}%" : $"{F(f.coeffOrMaxBonus, "0.##")}{unit}";
                return $"[Linear] [{baseSb}] + {mult} × {sLabel}";
            }

            if (f.coeffOrMaxBonus < 0f)
            {
                string redStr = $"{F(-f.coeffOrMaxBonus, "0.##")}{unit}";
                float floor = f.baseValue + f.coeffOrMaxBonus;
                return $"[Bounded diminishing reduction] [{baseSb}] - {redStr} × {sLabel} / ({sLabel} + {F(f.halfPoint, "0.##")}) → {F(floor, "0.00")}{unit} floor";
            }

            string bonusStr = asPercent ? $"{F(f.coeffOrMaxBonus * 100f, "0.#")}%" : $"{F(f.coeffOrMaxBonus, "0.##")}{unit}";
            return $"[Bounded diminishing] [{baseSb}] + {bonusStr} × {sLabel} / ({sLabel} + {F(f.halfPoint, "0.##")}) → +{bonusStr} asymptote";
        }

        private static string FormatBonusRatePer10(StatFormula f, string statName, string unit, bool asPercent)
        {
            float per10 = f.halfPoint > 0f ? (f.coeffOrMaxBonus / f.halfPoint) * 10f : 0f;
            if (asPercent)
            {
                return $"{F(per10 * 100f, "0.#")}% / 10 {statName}";
            }
            return $"{F(per10, "0.##")}{unit} / 10 {statName}";
        }

        private static string FormatBonusValue(float val, string unit, bool asPercent)
        {
            if (asPercent) return $"{F(val * 100f, "0.#")}%";
            if (Mathf.Abs(val) >= 10f) return $"{F(val, "0")}{unit}";
            return $"{F(val, "0.00")}{unit}";
        }

        private static void AddFact(AbilityCardData card, string label, string value, string colorHex = ColText)
        {
            if (string.IsNullOrEmpty(value)) return;
            card.KeyFacts.Add(new KeyFactPill
            {
                Label = label,
                Value = value,
                ColorHex = colorHex,
            });
        }

        private static void AddScalingRow(
            AbilityCardData card,
            string statTag,
            string statColorHex,
            string affectedProperty,
            string curveLabel,
            string summaryText,
            string liveText = "")
        {
            card.ScalingRows.Add(new ScalingRow
            {
                StatTag = statTag,
                StatColorHex = statColorHex,
                AffectedProperty = affectedProperty,
                CurveLabel = curveLabel,
                SummaryText = summaryText,
                LiveText = liveText,
            });
            string bullet = $"<color={statColorHex}><b>{statTag}</b></color> → {affectedProperty}: {summaryText}";
            if (!string.IsNullOrEmpty(liveText))
            {
                bullet += $" <color={ColLive}>({liveText})</color>";
            }
            card.StatLines.Add(bullet);
        }

        public static HeroSnap EvaluatePreviewHeroSnap(LaneHeroId hero, LaneTuning t, PreviewStatOverrides ov)
        {
            LaneTuning safeTuning = t ?? new LaneTuning
            {
                waveFirstSpawnDelay = 99999f,
                passiveGoldPerSecond = 0f,
                startingGold = 0,
            };
            var previewSim = new LaneMatch(hero, LaneHeroId.Swordsman, safeTuning);
            previewSim.SetPreviewStatOverrides(LaneTeam.Blue, ov);
            LaneSnapshot snap = previewSim.Snapshot();
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].team == LaneTeam.Blue) return snap.heroes[i];
            }
            return null;
        }

        public static HeroGuideData BuildHeroGuide(LaneHeroId hero, LaneTuning t, HeroSnap liveSelf = null)
        {
            HeroSnap live = (liveSelf != null && liveSelf.heroId == hero) ? liveSelf : null;
            var g = new HeroGuideData
            {
                Hero = hero,
                Title = LaneNames.DisplayName(hero),
                RoleLine = LaneNames.RoleSummary(hero),
            };

            switch (hero)
            {
                case LaneHeroId.Ogre:
                    BuildOgreGuide(g, t, live);
                    break;
                case LaneHeroId.Miravel:
                    BuildMiravelGuide(g, t, live);
                    break;
                case LaneHeroId.Swordsman:
                    BuildSwordsmanGuide(g, t, live);
                    break;
                case LaneHeroId.LaserShooter:
                    BuildLaserShooterGuide(g, t, live);
                    break;
            }

            for (int i = 0; i < g.Cards.Count; i++)
            {
                g.Cards[i].CardIndex = i;
            }

            g.RuleLines.Add($"<color={ColInt}><b>Zero Free Attribute Stats:</b></color> <color={ColStr}>STR</color>, <color={ColAgi}>AGI</color>, and <color={ColInt}>INT</color> start at <b>0</b> and only affect abilities or items that explicitly scale with them.");
            g.RuleLines.Add($"<color={ColMana}><b>Shared Hero Resource System (Mana vs Resourceless):</b></color> Miravel & Chain Laser spend <b>Mana</b> (natural regen stays active inside & outside combat; <b>Resource Cost Reduction</b> uses Bounded diminishing returns strictly below 100%). Ogre & Old Man Swordsman are intentionally <b>Resourceless</b>.");
            g.RuleLines.Add($"<color={ColAgi}><b>Global Attack Speed (Unbounded diminishing → Inverse duration):</b></color> Effective AS = <b>sqrt(1 + {F(t.attackSpeedCurveK, "0.##")} × Bonus AS)</b> (+30% AS → {F(LaneTuning.EffectiveAttackSpeed(0.30f, t.attackSpeedCurveK), "0.00")}x, +120% → {F(LaneTuning.EffectiveAttackSpeed(1.20f, t.attackSpeedCurveK), "0.00")}x, no hard cap). Governs basic attacks, Swordsman strikes, Ogre ability recovery, & Chain Laser [R] procs.");
            g.RuleLines.Add($"<color={ColAgi}><b>The Coward's Shield (1200g):</b></color> +46 Arm, +28 MR, +28 AGI • Sustained retreat velocity away from a recent hero attacker ({F(t.cowardShieldMemorySeconds, "0.0")}s) builds {FormatPrettyFormula(t.cowardShieldResistFormula, ColCc, "AGI", ColAgi, " Arm/MR", maxRanks: 1)}; {F(t.cowardShieldGraceSeconds, "0.00")}s grace holds on brief stops/stuns; fades in {F(t.cowardShieldStopDecaySeconds, "0.0")}s stopped or {F(t.cowardShieldPursuitDecaySeconds, "0.00")}s pursuing.");
            g.RuleLines.Add($"<color={ColHeal}><b>Kindred Censer (720g) & Colossus Pauldron (1360g):</b></color> Kindred Censer emits +{F(2.2f, "0.0")} × HealPower HP/s within {F(t.kindredAuraRadius, "0.0")}u for allies on <b>any</b> hero; Colossus triggers when its owner inflicts Stun/Knockback or drops <45% HP.");
            g.RuleLines.Add($"<color={ColMuted}><b>Curve Guide:</b></color> <b>Linear</b> = steady gain per point; <b>Bounded diminishing</b> = approaches a soft asymptote; <b>Unbounded diminishing</b> = keeps growing without a hard limit via square root; <b>Inverse duration</b> = faster speed shrinks time by dividing (Base / Speed). Armor/MR use 100/(100+R) above 0 and 2 - 100/(100-R) below 0; Ability Haste uses Base × 100/(100+AH).");
            return g;
        }

        public static AbilityCardData BuildAbilityTooltip(LaneHeroId hero, LaneAbilitySlot slot, LaneTuning t, HeroSnap liveSelf)
        {
            HeroGuideData g = BuildHeroGuide(hero, t, liveSelf);
            for (int i = 0; i < g.Cards.Count; i++)
            {
                if (g.Cards[i].Slot == slot) return g.Cards[i];
            }
            return null;
        }

        public static string BuildItemTooltip(LaneItemDef item, bool canSellNow, int remainingCost = -1)
        {
            var sb = new StringBuilder();
            string tierCol = item.Category == LaneItemCategory.Starter ? ColMuted
                : ((item.Category == LaneItemCategory.Advanced || item.Category == LaneItemCategory.ProvisionalAdvanced) ? ColCc : ColInt);
            string tag = item.IsApproved ? LaneItems.CategoryLabel(item.Category) : $"{LaneItems.CategoryLabel(item.Category)} • Provisional";
            string costStr = remainingCost >= 0 && remainingCost < item.Cost
                ? $"<color=#ffdf5c>{remainingCost}g remaining</color> <color={ColMuted}>({item.Cost}g total)</color>"
                : $"<color=#ffdf5c>{item.Cost}g</color>";
            sb.AppendLine($"<b><size=14>{item.Name}</size></b>  <color={tierCol}>[{tag}]</color>  {costStr}");
            if (!string.IsNullOrEmpty(item.Fantasy)) sb.AppendLine($"<i><color={ColMuted}>{item.Fantasy}</color></i>");
            sb.AppendLine(item.Summary);
            if (item.Passives != null && item.Passives.Length > 0)
            {
                for (int p = 0; p < item.Passives.Length; p++)
                {
                    ItemPassiveDoc doc = item.Passives[p];
                    sb.AppendLine($"<color={ColCc}><b>{doc.Name}:</b></color> {doc.PlainSummary}");
                    for (int s = 0; s < doc.ScalingArrows.Length; s++)
                    {
                        sb.AppendLine($"  • {doc.ScalingArrows[s]}");
                    }
                }
            }
            if (item.Recipe != null && item.Recipe.Length > 0)
            {
                sb.Append("Recipe: ");
                for (int r = 0; r < item.Recipe.Length; r++)
                {
                    if (r > 0) sb.Append(" + ");
                    string ingName = LaneItems.TryGet(item.Recipe[r].ItemId, out LaneItemDef ingDef) ? ingDef.Name : $"#{item.Recipe[r].ItemId}";
                    sb.Append(item.Recipe[r].Quantity > 1 ? $"{item.Recipe[r].Quantity}× {ingName}" : ingName);
                }
                sb.AppendLine();
            }
            int refund = LaneItems.SellRefund(item.Cost);
            sb.AppendLine(canSellNow
                ? $"<color={ColLive}>Sell refund: +{refund}g (70%) — use Sell button in [B] Shop</color>"
                : $"<color={ColMuted}>Sell value: +{refund}g (70%, open [B] Shop in Fountain or while Dead)</color>");
            return sb.ToString().TrimEnd();
        }

        public static string BuildHeroSheet(LaneHeroId hero, LaneTuning t, HeroSnap liveSelf = null)
        {
            HeroGuideData g = BuildHeroGuide(hero, t, liveSelf);
            var sb = new StringBuilder();
            sb.AppendLine($"<b><size=15>{g.Title}</size></b> — {g.IdentitySummary}");
            sb.AppendLine(g.BaseStatsLine);
            if (!string.IsNullOrEmpty(g.LiveStatsLine)) sb.AppendLine(g.LiveStatsLine);

            for (int i = 0; i < g.Cards.Count; i++)
            {
                AbilityCardData c = g.Cards[i];
                sb.AppendLine();
                sb.AppendLine($"<b>[{c.KeyBadge}] {c.Name}</b> ({c.MetaRight})");
                if (!string.IsNullOrEmpty(c.PlainDescription)) sb.AppendLine($"  {c.PlainDescription}");
                if (c.KeyFacts.Count > 0)
                {
                    sb.Append("  Key Info: ");
                    for (int k = 0; k < c.KeyFacts.Count; k++)
                    {
                        if (k > 0) sb.Append("  •  ");
                        sb.Append($"{c.KeyFacts[k].Label}: {c.KeyFacts[k].Value}");
                    }
                    sb.AppendLine();
                }
                for (int s = 0; s < c.ScalingRows.Count; s++)
                {
                    ScalingRow r = c.ScalingRows[s];
                    sb.AppendLine($"  • {r.StatTag} → {r.AffectedProperty} [{r.CurveLabel}]: {r.SummaryText}");
                }
                if (!string.IsNullOrEmpty(c.LiveLine)) sb.AppendLine($"  {c.LiveLine}");
                if (!string.IsNullOrEmpty(c.PracticalTip)) sb.AppendLine($"  Tip: {c.PracticalTip}");
                for (int m = 0; m < c.MathLines.Count; m++)
                {
                    sb.AppendLine($"  <color={ColMuted}>{c.MathLines[m]}</color>");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"<b><color={ColInt}>SHARED ATTRIBUTE & ITEM RULES</color></b>");
            for (int i = 0; i < g.RuleLines.Count; i++)
            {
                sb.AppendLine($"• {g.RuleLines[i]}");
            }
            return sb.ToString();
        }

        private static void BuildOgreGuide(HeroGuideData g, LaneTuning t, HeroSnap live)
        {
            g.IdentitySummary = "Heavy club bruiser with no basic attacks, crowd-control immune animations, huge Strength stuns & knockbacks, and a combo ultimate.";
            g.BaseStatsLine = $"HP <b>720</b> (+48/lvl)  •  <color={ColAd}>AD <b>60</b> (+5/lvl)</color>  •  Arm <b>16</b>  •  MR <b>12</b>  •  Regen <b>1.6/s</b>  •  Move <b>{F(t.ogreSpeed, "0.00")}</b>  •  Turn <b>{F(t.ogreTurnRate)}°/s</b>";
            if (live != null)
            {
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • Eff AS {F(live.attackSpeed, "0.00")}x (+{F(live.bonusAttackSpeed * 100f)}%) • STR {F(live.strength)} • AGI {F(live.agility)} • AH {F(live.abilityHaste)} • Wind {F(live.ogreWindupSpeed, "0.00")}x / Rec {F(live.ogreRecoverSpeed, "0.00")}x</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Heavy Club Discipline",
                MetaRight = t.ogreImmuneDuringRecovery ? "Resourceless  •  CC-Immune Cast + Recovery" : "Resourceless  •  CC-Immune Cast Wind-up",
                PlainDescription = "Ogre cannot perform basic attacks and relies completely on club abilities. While casting an ability, Ogre shrugs off stuns, slows, and knockbacks. Agility speeds up swing wind-ups, while Attack Speed speeds up recovery after hitting.",
                PracticalTip = "Start winding up an ability right before an enemy stun lands so your animation absorbs the crowd control and still completes.",
            };
            AddFact(passive, "Restriction", "No basic attacks or Attack-Move", ColCc);
            AddFact(passive, "Immunity", t.ogreImmuneDuringRecovery ? "Unstoppable during wind-up & recovery" : "Unstoppable during wind-up", ColCc);
            AddScalingRow(passive, "AGI", ColAgi, "Ability wind-up speed (all swings)", "Unbounded sqrt → Inverse",
                FormatFormula(t.ogreWindupSpeedFormula, "AGI", "x", maxRanks: 1),
                live != null ? $"{F(live.ogreWindupSpeed, "0.00")}x" : "");
            AddScalingRow(passive, "Attack Speed", ColAgi, "Post-hit recovery speed", "Global Effective AS → Inverse",
                $"Global Effective AS = sqrt(1 + {F(t.attackSpeedCurveK, "0.##")} × Bonus AS)",
                live != null ? $"{F(live.ogreRecoverSpeed, "0.00")}x" : "");
            passive.MathLines.Add($"Wind-up multiplier = {FormatExactMath(t.ogreWindupSpeedFormula, "AGI", "x", maxRanks: 1)} (0.40x min clamp)");
            passive.MathLines.Add($"Recovery speed = Global Effective AS = sqrt(1 + {F(t.attackSpeedCurveK, "0.##")} × BonusAS); Recovery Time = BaseRecover / EffectiveAS");
            if (live != null) passive.LiveLine = $"<color={ColLive}>Now: Wind-ups {F(live.ogreWindupSpeed, "0.00")}x (AGI) • Recoveries {F(live.ogreRecoverSpeed, "0.00")}x (AS)</color>";
            g.Cards.Add(passive);

            var q = new AbilityCardData
            {
                Slot = LaneAbilitySlot.Q,
                KeyBadge = "Q",
                Name = "Club Slam",
                MetaRight = $"Dual-Zone Line  •  CD {F(t.ogreQCooldown, "0.0")}/{F(t.ogreQCooldown - 0.6f, "0.0")}/{F(t.ogreQCooldown - 1.2f, "0.0")}s",
                PlainDescription = "Raises the club overhead and smashes a line in front of Ogre. Enemies inside the narrow central impact zone take full physical damage and are stunned. Enemies caught only in the wider outer shockwave take reduced damage with no crowd control.",
                PracticalTip = "Aim the center of your slam at enemies you want to stun. Use the wider outer shockwave to poke or clear creeps safely.",
            };
            AddFact(q, "Central Dmg", FormatRankProgressionOnly(t.ogreQDamageFormula) + " + 135% AD Physical", ColPhys);
            AddFact(q, "Outer Shockwave", $"{F(t.ogreQOuterDamageRatio * 100f)}% of Central Dmg (No CC)", ColPhys);
            AddFact(q, "Crowd Control", FormatRankProgressionOnly(t.ogreQStunFormula, "s") + " Stun (Central only)", ColCc);
            AddFact(q, "Central Zone", $"{F(t.ogreQRange, "0.0")}u × {F(t.ogreQWidth * 2f, "0.00")}u (scales with R size)");
            AddFact(q, "Timing", $"Wind {F(t.ogreQWindup, "0.00")}s / Rec {F(t.ogreQRecover, "0.00")}s");
            AddFact(q, "Cooldown", $"CD {F(t.ogreQCooldown, "0.0")}/{F(t.ogreQCooldown - 0.6f, "0.0")}/{F(t.ogreQCooldown - 1.2f, "0.0")}s");
            AddScalingRow(q, "AD", ColAd, "Central & Outer slam damage", "Linear", FormatFormula(t.ogreQDamageFormula, "AD"));
            AddScalingRow(q, "STR", ColStr, "Central stun duration", "Bounded diminishing", FormatFormula(t.ogreQStunFormula, "STR", "s"));
            AddScalingRow(q, "STR", ColStr, "Outer Shockwave extra width", "Bounded diminishing", FormatFormula(t.ogreQOuterWidthFormula, "STR", "u"));
            AddScalingRow(q, "AGI / AS", ColAgi, "Wind-up speed (AGI) & Recovery speed (AS)", "Unbounded sqrt → Inverse", "Divides wind-up & recovery durations separately");
            AddScalingRow(q, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            q.MathLines.Add($"Central Damage: {FormatExactMath(t.ogreQDamageFormula, "AD")} (Outer = ×{F(t.ogreQOuterDamageRatio, "0.00")})");
            q.MathLines.Add($"Central Stun: {FormatExactMath(t.ogreQStunFormula, "STR", "s")} | Outer +Half-Width: {FormatExactMath(t.ogreQOuterWidthFormula, "STR", "u")}");
            q.MathLines.Add($"Zone Geometry: Central ({F(t.ogreQRange, "0.0")}u × {F(t.ogreQWidth * 2f, "0.00")}u) × R Scale | Timing = {F(t.ogreQWindup, "0.00")}s/WindSpd + {F(t.ogreQRecover, "0.00")}s/RecSpd");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankQ);
                float cdmg = t.ogreQDamageFormula.Evaluate(r, live.attackDamage);
                float wind = Mathf.Max(0.4f, live.ogreWindupSpeed);
                float rec = Mathf.Max(0.4f, live.ogreRecoverSpeed);
                q.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankQ)}): {F(cdmg)} central / {F(cdmg * t.ogreQOuterDamageRatio)} outer • {F(t.ogreQStunFormula.Evaluate(r, live.strength), "0.00")}s stun • +{F(t.ogreQOuterWidthFormula.Evaluate(r, live.strength), "0.00")}u outer • {F(t.ogreQWindup / wind, "0.00")}s/{F(t.ogreQRecover / rec, "0.00")}s</color>";
            }
            g.Cards.Add(q);

            var w = new AbilityCardData
            {
                Slot = LaneAbilitySlot.W,
                KeyBadge = "W",
                Name = "Club Sweep",
                MetaRight = $"Front Arc  •  CD {F(t.ogreWCooldown, "0.0")}/{F(t.ogreWCooldown - 0.6f, "0.0")}/{F(t.ogreWCooldown - 1.2f, "0.0")}s",
                PlainDescription = "Swings the club horizontally across a wide arc in front of Ogre, dealing physical damage and hurling all enemies hit backward until they stop against trees or walls. Stacking Strength can knock enemies dramatically far away.",
                PracticalTip = "Knock enemies back into your own tower or away from Sanctuary Orbs. Watch extreme Strength values so you do not knock kills safely out of your own reach.",
            };
            AddFact(w, "Damage", FormatRankProgressionOnly(t.ogreWDamageFormula) + " + 105% AD Physical", ColPhys);
            AddFact(w, "Knockback", FormatRankProgressionOnly(t.ogreWKnockbackFormula, "u") + " pushback", ColCc);
            AddFact(w, "Reach", $"{F(t.ogreWRange, "0.0")}u cone (scales with R size)");
            AddFact(w, "Timing", $"Wind {F(t.ogreWWindup, "0.00")}s / Rec {F(t.ogreWRecover, "0.00")}s");
            AddFact(w, "Cooldown", $"CD {F(t.ogreWCooldown, "0.0")}/{F(t.ogreWCooldown - 0.6f, "0.0")}/{F(t.ogreWCooldown - 1.2f, "0.0")}s");
            AddScalingRow(w, "AD", ColAd, "Sweep damage dealt", "Linear", FormatFormula(t.ogreWDamageFormula, "AD"));
            AddScalingRow(w, "STR", ColStr, "Knockback distance (no upper cap)", "Unbounded diminishing", FormatFormula(t.ogreWKnockbackFormula, "STR", "u"));
            AddScalingRow(w, "AGI / AS", ColAgi, "Wind-up speed (AGI) & Recovery speed (AS)", "Unbounded sqrt → Inverse", "Divides wind-up & recovery durations separately");
            AddScalingRow(w, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            w.MathLines.Add($"Damage: {FormatExactMath(t.ogreWDamageFormula, "AD")}");
            w.MathLines.Add($"Knockback: {FormatExactMath(t.ogreWKnockbackFormula, "STR", "u")} (Minions pushed 0.8x; clamps cleanly at walls/trees)");
            w.MathLines.Add($"Cone: {F(t.ogreWRange, "0.0")}u × R Scale, cos ≥ {F(t.ogreWArcCos, "0.00")} • Timing = {F(t.ogreWWindup, "0.00")}s/WindSpd + {F(t.ogreWRecover, "0.00")}s/RecSpd");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankW);
                float wind = Mathf.Max(0.4f, live.ogreWindupSpeed);
                float rec = Mathf.Max(0.4f, live.ogreRecoverSpeed);
                w.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankW)}): {F(t.ogreWDamageFormula.Evaluate(r, live.attackDamage))} dmg • {F(t.ogreWKnockbackFormula.Evaluate(r, live.strength), "0.00")}u push • {F(t.ogreWWindup / wind, "0.00")}s/{F(t.ogreWRecover / rec, "0.00")}s</color>";
            }
            g.Cards.Add(w);

            var e = new AbilityCardData
            {
                Slot = LaneAbilitySlot.E,
                KeyBadge = "E",
                Name = "Quake Stomp",
                MetaRight = $"Point-Blank Area  •  CD {F(t.ogreECooldown, "0.0")}/{F(t.ogreECooldown - 0.8f, "0.0")}/{F(t.ogreECooldown - 1.6f, "0.0")}s",
                PlainDescription = "Slams both feet into the earth around Ogre, damaging and stunning every enemy uniformly across the entire tremor circle. Both the area and stun grow with Strength, and the area expands even further while enlarged by Rampage Combo.",
                PracticalTip = "Fastest regular stun Ogre has — use it when melee fighters engage close so you can line up a guaranteed central Club Slam.",
            };
            AddFact(e, "Damage", FormatRankProgressionOnly(t.ogreEDamageFormula) + " + 115% AD Physical", ColPhys);
            AddFact(e, "Crowd Control", FormatRankProgressionOnly(t.ogreEStunFormula, "s") + " Stun (Uniform full area)", ColCc);
            AddFact(e, "Radius", $"{F(t.ogreERadius, "0.00")}u base (+STR, × R size)");
            AddFact(e, "Timing", $"Wind {F(t.ogreEWindup, "0.00")}s / Rec {F(t.ogreERecover, "0.00")}s");
            AddFact(e, "Cooldown", $"CD {F(t.ogreECooldown, "0.0")}/{F(t.ogreECooldown - 0.8f, "0.0")}/{F(t.ogreECooldown - 1.6f, "0.0")}s");
            AddScalingRow(e, "AD", ColAd, "Stomp damage dealt", "Linear", FormatFormula(t.ogreEDamageFormula, "AD"));
            AddScalingRow(e, "STR", ColStr, "Area radius", "Bounded diminishing", FormatFormula(t.ogreERadiusFormula, "STR", "u", maxRanks: 1));
            AddScalingRow(e, "STR", ColStr, "Stun duration", "Bounded diminishing", FormatFormula(t.ogreEStunFormula, "STR", "s"));
            AddScalingRow(e, "AGI / AS", ColAgi, "Wind-up speed (AGI) & Recovery speed (AS)", "Unbounded sqrt → Inverse", "Divides wind-up & recovery durations separately");
            AddScalingRow(e, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            e.MathLines.Add($"Damage: {FormatExactMath(t.ogreEDamageFormula, "AD")}");
            e.MathLines.Add($"Stun: {FormatExactMath(t.ogreEStunFormula, "STR", "s")} | Radius: ({FormatExactMath(t.ogreERadiusFormula, "STR", "u", maxRanks: 1)}) × R Scale");
            e.MathLines.Add($"Timing = {F(t.ogreEWindup, "0.00")}s/WindSpd + {F(t.ogreERecover, "0.00")}s/RecSpd");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankE);
                float wind = Mathf.Max(0.4f, live.ogreWindupSpeed);
                float rec = Mathf.Max(0.4f, live.ogreRecoverSpeed);
                e.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankE)}): {F(t.ogreEDamageFormula.Evaluate(r, live.attackDamage))} dmg • {F(t.ogreEStunFormula.Evaluate(r, live.strength), "0.00")}s stun • {F(t.ogreERadiusFormula.Evaluate(r, live.strength), "0.00")}u radius • {F(t.ogreEWindup / wind, "0.00")}s/{F(t.ogreERecover / rec, "0.00")}s</color>";
            }
            g.Cards.Add(e);

            var ult = new AbilityCardData
            {
                Slot = LaneAbilitySlot.R,
                KeyBadge = "R",
                Name = "Rampage Combo",
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Channel + {t.ogreRMaxStrikes}-Hit Combo  •  CD {F(t.ogreRCooldown)}/{F(t.ogreRCooldown - 4f)}s",
                PlainDescription = $"Channels for {F(t.ogreRChannelDuration, "0.0")}s while growing huge and gaining {F(t.ogreRDamageReduction * 100f)}% damage reduction. Press Q or W toward your cursor up to {t.ogreRMaxStrikes} times during the channel to queue a flurry of lunging strikes; Ogre then chains through all queued hits ending in a heavy critical slam.",
                PracticalTip = "Queue Q and W strikes toward where the enemy will retreat, or weave W after Q to keep pushing them into your final 1.85x critical strike.",
            };
            AddFact(ult, "Channel", $"{F(t.ogreRChannelDuration, "0.0")}s • {F(t.ogreRDamageReduction * 100f)}% Fixed Damage Reduction", ColCc);
            AddFact(ult, "Queued Combo", $"Up to {t.ogreRMaxStrikes} Q/W strikes ({F(t.ogreRLungeDistance, "0.00")}u lunge each)");
            AddFact(ult, "Combo Power", $"Hits 1–3: 0.82x dmg  •  Final Hit: {F(t.ogreRFinalCritMultiplier, "0.00")}x Crit", ColPhys);
            AddFact(ult, "Cooldown", $"CD {F(t.ogreRCooldown)}/{F(t.ogreRCooldown - 4f)}s");
            AddScalingRow(ult, "STR", ColStr, "Body size & proportional Q/W/E area enlargement", "Bounded diminishing", FormatFormula(t.ogreRScaleFormula, "STR", "x", maxRanks: 2));
            AddScalingRow(ult, "AD / STR", ColAd, "Each queued Q/W strike damage & CC", "Inherits Q & W", "Uses ranked Q/W formulas × combo multiplier");
            AddScalingRow(ult, "AGI / AS", ColAgi, "Fast/final wind-ups (AGI) & followthroughs (AS)", "Unbounded sqrt → Inverse", $"Base {F(t.ogreRFastWindup, "0.00")}s / {F(t.ogreRFinalWindup, "0.00")}s windup");
            AddScalingRow(ult, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            ult.MathLines.Add($"Size Scale = {FormatExactMath(t.ogreRScaleFormula, "STR", "x", maxRanks: 2)} (multiplies Q/W/E area dimensions proportionally)");
            ult.MathLines.Add($"Strike Windup = ({F(t.ogreRFastWindup, "0.00")}s fast / {F(t.ogreRFinalWindup, "0.00")}s final) / WindupSpd; Followthrough = {F(t.ogreRStrikeFollowthrough, "0.00")}s / RecSpd; End Recover = {F(t.ogreRPostComboRecover, "0.00")}s / RecSpd");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankR);
                float sz = t.ogreRScaleFormula.Evaluate(r, live.strength);
                float wind = Mathf.Max(0.4f, live.ogreWindupSpeed);
                ult.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankR)}): {F(sz, "0.00")}x size • fast {F(t.ogreRFastWindup / wind, "0.00")}s / final {F(t.ogreRFinalWindup / wind, "0.00")}s windup</color>";
            }
            g.Cards.Add(ult);
        }

        private static void BuildMiravelGuide(HeroGuideData g, LaneTuning t, HeroSnap live)
        {
            g.IdentitySummary = "Sustain and denial mage who fights through vitality-draining auras, Sanctuary Orbs, deceptive Shimmer Decoys, and her Ramping Sanctum.";
            g.BaseStatsLine = $"HP <b>530</b> (+34/lvl)  •  <color={ColMana}>Mana <b>{F(t.miravelBaseMana)}</b> (+{F(t.miravelManaPerLevel)}/lvl, <b>{F(t.miravelBaseManaRegen, "0.0")}/s</b> +{F(t.miravelManaRegenPerLevel, "0.00")}/lvl)</color>  •  <color={ColAd}>AD <b>48</b> (+3.5/lvl)</color>  •  Arm <b>8</b>  •  MR <b>18</b>  •  Regen <b>1.4/s</b>  •  Move <b>{F(t.luisSpeed, "0.00")}</b>";
            if (live != null)
            {
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • <color={ColMana}>Mana {F(live.mana)}/{F(live.maxMana)} ({live.manaRegen:+0.0;-0.0}/s, RCR -{F(live.rcrPct * 100f)}%)</color> • INT {F(live.intelligence)} • AH {F(live.abilityHaste)} • HealPower {F(live.healPower * 100f)}%</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Vitality Drain Aura & Takedown Growth",
                MetaRight = $"Ranged {F(t.luisBasicRange, "0.0")}u  •  AS Scaled",
                PlainDescription = "Projects an aura that heals nearby allies and drains enemy health regeneration. High Heal Power can push an enemy's net regeneration NEGATIVE, draining their health every second and crediting lethal kills to Miravel. Every takedown permanently boosts Heal Power.",
                PracticalTip = "Stay within aura range against low-sustain opponents to wear them down through walls and fog — negative regeneration ignores Armor, MR, and shields.",
            };
            AddFact(passive, "Attack Range", $"{F(t.luisBasicRange, "0.0")}u ranged projectile");
            AddFact(passive, "Resource", "0 Mana (Aura stays active at 0 Mana)", ColMana);
            AddFact(passive, "Allied Regen", $"+{F(t.luisAllyAuraRegenBase, "0.0")} HP/s (+0.35/lvl) × Heal Power", ColHeal);
            AddFact(passive, "Enemy Drain", $"-{F(t.luisEnemyAuraRegenPenaltyBase, "0.0")} HP/s (+0.35/lvl) × Heal Power", ColMagic);
            AddFact(passive, "Aura Radius", $"{F(t.luisAuraRadius, "0.0")}u base (+INT)", ColInt);
            AddFact(passive, "Takedowns", $"+{F(t.luisTakedownHealPowerGain * 100f)}% permanent Heal Power / kill", ColHeal);
            AddScalingRow(passive, "Heal Power", ColHeal, "Allied healing & enemy regen drain", "Linear (starts 100%)", $"Scales both +{F(t.luisAllyAuraRegenBase, "0.0")} ally & -{F(t.luisEnemyAuraRegenPenaltyBase, "0.0")} enemy drain",
                live != null ? $"+{F((t.luisAllyAuraRegenBase + 0.35f * (live.level - 1)) * live.healPower, "0.0")} / -{F((t.luisEnemyAuraRegenPenaltyBase + 0.35f * (live.level - 1)) * live.healPower, "0.0")} HP/s" : "");
            AddScalingRow(passive, "INT", ColInt, "Aura radius", "Bounded diminishing", FormatFormula(t.luisAuraRadiusFormula, "INT", "u", maxRanks: 1),
                live != null ? $"{F(t.luisAuraRadiusFormula.Evaluate(1, live.intelligence), "0.00")}u" : "");
            AddScalingRow(passive, "AD / AS", ColAd, "Basic staff attack damage (AD) & speed (AS)", "Linear AD / Unbounded AS", "Standard ranged basic attack");
            passive.MathLines.Add($"Ally Regen = ({F(t.luisAllyAuraRegenBase, "0.0")} + 0.35×(Lvl-1)) × HealPower | Enemy Drain = ({F(t.luisEnemyAuraRegenPenaltyBase, "0.0")} + 0.35×(Lvl-1)) × HealPower");
            passive.MathLines.Add($"Radius = {FormatExactMath(t.luisAuraRadiusFormula, "INT", "u", maxRanks: 1)} | Heal Power starts at 100% + {F(t.luisTakedownHealPowerGain * 100f)}%/takedown");
            if (live != null)
            {
                float allyHpS = (t.luisAllyAuraRegenBase + 0.35f * (live.level - 1)) * live.healPower;
                float enemyPen = (t.luisEnemyAuraRegenPenaltyBase + 0.35f * (live.level - 1)) * live.healPower;
                float aRad = t.luisAuraRadiusFormula.Evaluate(1, live.intelligence);
                passive.LiveLine = $"<color={ColLive}>Now: Ally +{F(allyHpS, "0.0")}/s • Drain -{F(enemyPen, "0.0")}/s • Radius {F(aRad, "0.00")}u</color>";
            }
            g.Cards.Add(passive);

            var q = new AbilityCardData
            {
                Slot = LaneAbilitySlot.Q,
                KeyBadge = "Q (PASSIVE)",
                Name = "Sanctuary Orbs",
                MetaRight = $"Auto-Spawns every {F(t.luisOrbInterval, "0.0")}/{F(t.luisOrbInterval - 0.6f, "0.0")}/{F(t.luisOrbInterval - 1.2f, "0.0")}s × AH",
                PlainDescription = "Automatically spawns an energy orb nearby on a timer. Walking over your own orb triggers a wide pulse that heals nearby allies (including Miravel) and deals magic damage to enemies. Enemy heroes can step on an uncollected orb to crush it.",
                PracticalTip = "Fight near spawned orbs so picking them up hits both you and the enemy, or zone enemies with threats while collecting free lane sustain.",
            };
            AddFact(q, "Pulse Healing", "68 / 88 / 108 × Heal Power", ColHeal);
            AddFact(q, "Magic Damage", "62 / 80 / 98 × Heal Power", ColMagic);
            AddFact(q, "Pulse Radius", $"{F(t.luisOrbPulseRadius, "0.0")}u base (+INT)", ColInt);
            AddFact(q, "Duration", $"{F(t.luisOrbDuration)}s ground lifetime (max 2 orbs)");
            AddFact(q, "Cooldown", $"{F(t.luisOrbInterval, "0.0")}/{F(t.luisOrbInterval - 0.6f, "0.0")}/{F(t.luisOrbInterval - 1.2f, "0.0")}s spawn × AH (automatic)");
            AddFact(q, "Resource", "0 Mana (Automatic passive pulse)", ColMana);
            AddScalingRow(q, "Heal Power", ColHeal, "Orb heal & magic pulse damage", "Linear", "68/88/108 heal & 62/80/98 dmg × Heal Power");
            AddScalingRow(q, "INT", ColInt, "Pulse explosion radius", "Bounded diminishing", FormatFormula(t.luisOrbPulseRadiusFormula, "INT", "u"));
            AddScalingRow(q, "Ability Haste", ColInt, "Orb spawn interval (no 4s artificial floor)", "Inverse", "Interval = Base × 100 / (100 + AH)");
            q.MathLines.Add($"Heal = [68/88/108] × HealPower | Magic Damage = [62/80/98] × HealPower | Allied creeps heal 0.65x");
            q.MathLines.Add($"Pulse Radius = {FormatExactMath(t.luisOrbPulseRadiusFormula, "INT", "u")} | Interval = [{F(t.luisOrbInterval, "0.0")}/{F(t.luisOrbInterval - 0.6f, "0.0")}/{F(t.luisOrbInterval - 1.2f, "0.0")}s] × 100/(100+AH)");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankQ);
                float heal = t.luisOrbHealFormula.Evaluate(r, 0f) * live.healPower;
                float dmg = t.luisOrbDamageFormula.Evaluate(r, 0f) * live.healPower;
                float rad = t.luisOrbPulseRadiusFormula.Evaluate(r, live.intelligence);
                float cd = LiveCd(live, t.luisOrbInterval - 0.6f * (r - 1));
                q.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankQ)}): {F(heal)} heal • {F(dmg)} dmg • {F(rad, "0.00")}u radius • every {F(cd, "0.00")}s</color>";
            }
            g.Cards.Add(q);

            var w = new AbilityCardData
            {
                Slot = LaneAbilitySlot.W,
                KeyBadge = "W",
                Name = "Shimmer Decoy",
                MetaRight = $"Active  •  CD {F(t.luisWCooldown, "0.0")}/{F(t.luisWCooldown - 1.1f, "0.0")}/{F(t.luisWCooldown - 2.2f, "0.0")}s",
                PlainDescription = "Turns Miravel invisible while sending a controllable illusion walking to your cursor position (where it stops moving). Basic attacking ends stealth, while casting Retribution Ward (E) does NOT break invisibility.",
                PracticalTip = "Walk the decoy straight ahead under Retribution Ward while you stealth-reposition sideways — if an enemy strikes the decoy, they get cursed.",
            };
            AddFact(w, "Stealth Duration", $"{F(t.luisWDuration, "0.0")}/{F(t.luisWDuration + 0.4f, "0.0")}/{F(t.luisWDuration + 0.8f, "0.0")}s invisibility", ColInt);
            AddFact(w, "Decoy Lifetime", $"{F(t.luisWIllusionDuration, "0.0")}s illusion");
            AddFact(w, "Cooldown", $"CD {F(t.luisWCooldown, "0.0")}/{F(t.luisWCooldown - 1.1f, "0.0")}/{F(t.luisWCooldown - 2.2f, "0.0")}s");
            AddFact(w, "Mana Cost", $"{F(t.miravelWManaBase)} / {F(t.miravelWManaBase + t.miravelWManaPerRank)} / {F(t.miravelWManaBase + t.miravelWManaPerRank * 2f)} Mana", ColMana);
            AddScalingRow(w, "Ability Haste", ColInt, "Cooldown reduction only", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            AddScalingRow(w, "Resource Cost Reduction", ColMana, "Reduces Mana cost (<100% cap)", "Bounded diminishing",
                FormatFormula(t.resourceCostReductionFormula, "RCR", "", asPercent: true, maxRanks: 1),
                live != null && live.costW > 0f ? $"{F(live.costW)} Mana" : "");
            w.MathLines.Add($"Invisibility = {F(t.luisWDuration, "0.0")}/{F(t.luisWDuration + 0.4f, "0.0")}/{F(t.luisWDuration + 0.8f, "0.0")}s (fixed); Decoy = {F(t.luisWIllusionDuration, "0.0")}s (copies MS/Arm/MR)");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankW);
                w.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankW)}): {F(t.luisWDuration + 0.4f * (r - 1), "0.0")}s invis • {F(LiveCd(live, t.luisWCooldown - 1.1f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(w);

            var e = new AbilityCardData
            {
                Slot = LaneAbilitySlot.E,
                KeyBadge = "E",
                Name = "Retribution Ward",
                MetaRight = $"Anti-Cast Ward  •  CD {F(t.luisECooldown, "0.0")}/{F(t.luisECooldown - 0.8f, "0.0")}/{F(t.luisECooldown - 1.6f, "0.0")}s",
                PlainDescription = "Places a ward around Miravel and any active decoy without breaking stealth. Enemy heroes who damage a warded unit become cursed with burning magic damage; every ability they cast while cursed adds more damage stacks (up to 5) and extends the curse.",
                PracticalTip = "Save Ward until just before an enemy burst window, or pair it with W so hitting your decoy punishes aggressive opponents.",
            };
            AddFact(e, "Curse Burn", FormatRankProgressionOnly(t.luisCurseDpsFormula) + " + 65% INT magic DPS/stack", ColMagic);
            AddFact(e, "Ward Duration", $"{F(t.luisEWardDuration, "0.0")}/{F(t.luisEWardDuration + 0.6f, "0.0")}/{F(t.luisEWardDuration + 1.2f, "0.0")}s");
            AddFact(e, "Curse Duration", $"{F(t.luisECurseBaseDuration, "0.0")}/{F(t.luisECurseBaseDuration + 0.4f, "0.0")}/{F(t.luisECurseBaseDuration + 0.8f, "0.0")}s (+{F(t.luisECurseBonusDuration, "0.0")}s/cast)");
            AddFact(e, "Cooldown", $"CD {F(t.luisECooldown, "0.0")}/{F(t.luisECooldown - 0.8f, "0.0")}/{F(t.luisECooldown - 1.6f, "0.0")}s");
            AddFact(e, "Mana Cost", $"{F(t.miravelEManaBase)} / {F(t.miravelEManaBase + t.miravelEManaPerRank)} / {F(t.miravelEManaBase + t.miravelEManaPerRank * 2f)} Mana (Curse extensions free)", ColMana);
            AddFact(e, "Interaction", "Safe during W stealth • Up to 5 stacks", ColInt);
            AddScalingRow(e, "INT", ColInt, "Curse magic damage per second", "Linear", FormatFormula(t.luisCurseDpsFormula, "INT"));
            AddScalingRow(e, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            AddScalingRow(e, "Resource Cost Reduction", ColMana, "Reduces initial cast cost (<100% cap)", "Bounded diminishing",
                FormatFormula(t.resourceCostReductionFormula, "RCR", "", asPercent: true, maxRanks: 1),
                live != null && live.costE > 0f ? $"{F(live.costE)} Mana" : "");
            e.MathLines.Add($"1-Stack DPS = {FormatExactMath(t.luisCurseDpsFormula, "INT")}; Total DPS = DPS × (1 + 0.35 × (stacks - 1)) → 2.40x at 5 stacks");
            e.MathLines.Add($"Durations fixed: Ward {F(t.luisEWardDuration, "0.0")}s + 0.6s/rank; Curse {F(t.luisECurseBaseDuration, "0.0")}s + 0.4s/rank + {F(t.luisECurseBonusDuration, "0.0")}s per enemy cast");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankE);
                float dps1 = t.luisCurseDpsFormula.Evaluate(r, live.intelligence);
                e.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankE)}): {F(dps1, "0.0")} → {F(dps1 * 2.40f, "0.0")} DPS • {F(LiveCd(live, t.luisECooldown - 0.8f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(e);

            var ult = new AbilityCardData
            {
                Slot = LaneAbilitySlot.R,
                KeyBadge = "R",
                Name = "Ramping Sanctum",
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Channel {F(t.luisRChannelDuration, "0.0")}s  •  CD {F(t.luisRCooldown)}/{F(t.luisRCooldown - 3f)}s",
                PlainDescription = "Creates a sanctum zone at target area and channels to steadily ramp its power from 0% to 100%. Inside the zone, allies receive boosted healing from all sources while enemies are progressively slowed and take amplified damage from everything. Issuing any other order cancels the channel.",
                PracticalTip = "Place Ramping Sanctum slightly ahead of your target so they stay inside during the full ramp-up, then grab a Sanctuary Orb for a massive amplified burst.",
            };
            AddFact(ult, "Ally Heal Amp", "25% / 30% max (+Bonus Heal Power)", ColHeal);
            AddFact(ult, "Enemy Vulnerability", "28% / 34% dmg taken (+Bonus Heal Power)", ColCc);
            AddFact(ult, "Crowd Control", $"Ramps to {F(t.luisRMaxSlow * 100f)}/{F((t.luisRMaxSlow + 0.06f) * 100f)}% Slow (fixed)", ColCc);
            AddFact(ult, "Range / Radius", $"{F(t.luisRRange, "0.0")}u cast  •  {F(t.luisRRadius, "0.0")}u base (+INT)");
            AddFact(ult, "Channel / CD", $"Channel {F(t.luisRChannelDuration, "0.0")}s  •  CD {F(t.luisRCooldown)}/{F(t.luisRCooldown - 3f)}s");
            AddFact(ult, "Mana Drain", $"{F(t.miravelRManaPerSecBase)} / {F(t.miravelRManaPerSecBase + t.miravelRManaPerSecPerRank)} Mana/s (Continuous, independent of INT; ends if OOM)", ColMana);
            AddScalingRow(ult, "INT", ColInt, "Sanctum zone radius", "Bounded diminishing", FormatFormula(t.luisRRadiusFormula, "INT", "u", maxRanks: 2));
            AddScalingRow(ult, "Heal Power", ColHeal, "Max ally healing amp & enemy vulnerability (>100%)", "Bounded diminishing", "Bonus Heal Power above 100% raises both caps");
            AddScalingRow(ult, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            AddScalingRow(ult, "Resource Cost Reduction", ColMana, "Reduces continuous Mana drain/sec (<100% cap)", "Bounded diminishing",
                FormatFormula(t.resourceCostReductionFormula, "RCR", "", asPercent: true, maxRanks: 1),
                live != null && live.costR > 0f ? $"{F(live.costR, "0.0")}/s" : "");
            ult.MathLines.Add($"Zone Radius = {FormatExactMath(t.luisRRadiusFormula, "INT", "u", maxRanks: 2)} | Channel = {F(t.luisRChannelDuration, "0.0")}s");
            ult.MathLines.Add($"Max Ally Heal Amp = {FormatExactMath(t.luisRHealAmpFormula, "BonusHealPwr", "", asPercent: true, maxRanks: 2, customDomain: "(HealPwr-1)")}");
            ult.MathLines.Add($"Max Enemy Vuln = {FormatExactMath(t.luisRVulnFormula, "BonusHealPwr", "", asPercent: true, maxRanks: 2, customDomain: "(HealPwr-1)")}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankR);
                float bonusHp = Mathf.Max(0f, live.healPower - 1f);
                float rad = t.luisRRadiusFormula.Evaluate(r, live.intelligence);
                float healAmp = t.luisRHealAmpFormula.Evaluate(r, bonusHp) * 100f;
                float vuln = t.luisRVulnFormula.Evaluate(r, bonusHp) * 100f;
                ult.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankR)}): {F(rad, "0.00")}u radius • +{F(healAmp, "0.0")}% ally heal • +{F(vuln, "0.0")}% enemy vuln • {F(LiveCd(live, t.luisRCooldown - 3f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(ult);
        }

        private static void BuildSwordsmanGuide(HeroGuideData g, LaneTuning t, HeroSnap live)
        {
            g.IdentitySummary = "Deliberate dual-blade duelist whose rapid 1st sabre hit steals resistances and whose heavy 2nd greatsword hit heals for % target Max HP.";
            g.BaseStatsLine = $"HP <b>615</b> (+40/lvl)  •  <color={ColAd}>AD <b>54</b> (+4/lvl)</color>  •  Arm <b>14</b>  •  MR <b>14</b>  •  Regen <b>1.5/s</b>  •  Move <b>{F(t.swordsmanSpeed, "0.00")}</b>  •  Turn <b>{F(t.swordsmanTurnRate)}°/s</b>";
            if (live != null)
            {
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • STR {F(live.strength)} • AGI {F(live.agility)} • INT {F(live.intelligence)} • Eff AS {F(live.attackSpeed, "0.00")}x • HPwr {F(live.healPower * 100f)}%</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Twin-Blade Technique",
                MetaRight = $"Resourceless  •  Melee {F(t.swordsmanBasicRange, "0.00")}u",
                PlainDescription = "Each basic attack performs two distinct swings. The 1st strike uses a quick light sabre that triggers [W] Stance Temper. The 2nd strike uses a heavier greatsword that triggers [E] Vital Draw — if enemies knock you away or dash out before Hit 2 lands, Hit 2 and its heal do not trigger.",
                PracticalTip = "Watch for enemies holding knockbacks to break your 2nd hit. Use Q slow first so the heavy second blade connects cleanly.",
            };
            AddFact(passive, "Hit 1 (Light Sabre)", $"{F(t.swordsmanHit1AdRatio * 100f)}% AD physical ({F(t.swordsmanHit1Windup, "0.00")}s wind)", ColPhys);
            AddFact(passive, "Hit 2 (Heavy Sword)", $"{F(t.swordsmanHit2AdRatio * 100f)}% AD physical ({F(t.swordsmanHit2DelayFormula.baseValue, "0.00")}s wind)", ColAd);
            AddFact(passive, "Attack Range", $"{F(t.swordsmanBasicRange, "0.00")}u melee  •  Rec {F(t.swordsmanBasicRecover, "0.00")}s");
            AddScalingRow(passive, "AD", ColAd, "Both strikes' physical damage", "Linear", $"{F(t.swordsmanHit1AdRatio * 100f)}% + {F(t.swordsmanHit2AdRatio * 100f)}% = {F((t.swordsmanHit1AdRatio + t.swordsmanHit2AdRatio) * 100f)}% total AD");
            AddScalingRow(passive, "AGI", ColAgi, "Hit 2 transition delay (stays heavier than Hit 1)", "Bounded diminishing", FormatFormula(t.swordsmanHit2DelayFormula, "AGI", "s", maxRanks: 1));
            AddScalingRow(passive, "Attack Speed", ColAgi, "Hit 1, Hit 2, and recovery cadence", "Global Effective AS → Inverse", "Divides Hit 1, Hit 2 transition & recovery time");
            passive.MathLines.Add($"Hit 1 Wind-up = {F(t.swordsmanHit1Windup, "0.00")}s / EffectiveAS ({F(t.swordsmanHit1AdRatio * 100f)}% AD)");
            passive.MathLines.Add($"Hit 2 Transition = ({FormatExactMath(t.swordsmanHit2DelayFormula, "AGI", "s", maxRanks: 1)}) / EffectiveAS ({F(t.swordsmanHit2AdRatio * 100f)}% AD)");
            passive.MathLines.Add($"Recovery = {F(t.swordsmanBasicRecover, "0.00")}s / EffectiveAS");
            if (live != null)
            {
                float effAs = Mathf.Max(0.4f, live.attackSpeed);
                float h1 = live.attackDamage * t.swordsmanHit1AdRatio;
                float h2 = live.attackDamage * t.swordsmanHit2AdRatio;
                float d2 = t.swordsmanHit2DelayFormula.Evaluate(1, live.agility) / effAs;
                passive.LiveLine = $"<color={ColLive}>Now: Hit1 {F(h1, "0.0")} dmg ({F(t.swordsmanHit1Windup / effAs, "0.00")}s) → Hit2 {F(h2, "0.0")} dmg ({F(d2, "0.00")}s)</color>";
            }
            g.Cards.Add(passive);

            var q = new AbilityCardData
            {
                Slot = LaneAbilitySlot.Q,
                KeyBadge = "Q",
                Name = "Hamstring Arc",
                MetaRight = $"Cone {F(t.swordsmanQRange, "0.0")}u  •  CD {F(t.swordsmanQCooldown, "0.0")}/{F(t.swordsmanQCooldown - 0.5f, "0.0")}/{F(t.swordsmanQCooldown - 1.0f, "0.0")}s",
                PlainDescription = "Cleaves a forward arc with steel, dealing physical damage, slowing targets hit, and marking an enemy (prioritizing heroes) so Finishing Cross can dash onto them. Agility speeds up the initial swing wind-up while post-swing recovery always stays deliberate.",
                PracticalTip = "Use Hamstring Arc from slightly outside melee reach to slow fleeing opponents and arm your R mark even before getting a basic attack off.",
            };
            AddFact(q, "Damage", FormatRankProgressionOnly(t.swordsmanQDamageFormula) + " + 90% AD Physical", ColPhys);
            AddFact(q, "Crowd Control", $"40/45/50% Slow for {F(t.swordsmanQSlowDuration, "0.0")}s", ColCc);
            AddFact(q, "Range / Timing", $"{F(t.swordsmanQRange, "0.0")}u arc  •  Fixed Rec {F(t.swordsmanQRecover, "0.00")}s");
            AddFact(q, "Cooldown", $"CD {F(t.swordsmanQCooldown, "0.0")}/{F(t.swordsmanQCooldown - 0.5f, "0.0")}/{F(t.swordsmanQCooldown - 1.0f, "0.0")}s");
            AddScalingRow(q, "AD", ColAd, "Slash damage dealt", "Linear", FormatFormula(t.swordsmanQDamageFormula, "AD"));
            AddScalingRow(q, "AGI", ColAgi, "Wind-up duration only (recovery fixed)", "Bounded diminishing", FormatFormula(t.swordsmanQWindupFormula, "AGI", "s", maxRanks: 1));
            AddScalingRow(q, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            q.MathLines.Add($"Damage = {FormatExactMath(t.swordsmanQDamageFormula, "AD")}");
            q.MathLines.Add($"Wind-up = {FormatExactMath(t.swordsmanQWindupFormula, "AGI", "s", maxRanks: 1)} | Fixed Recovery = {F(t.swordsmanQRecover, "0.00")}s | Slow = 40/45/50% for {F(t.swordsmanQSlowDuration, "0.0")}s (fixed)");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankQ);
                float dmg = t.swordsmanQDamageFormula.Evaluate(r, live.attackDamage);
                float wind = t.swordsmanQWindupFormula.Evaluate(r, live.agility);
                q.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankQ)}): {F(dmg)} dmg • {F(wind, "0.00")}s windup / {F(t.swordsmanQRecover, "0.00")}s rec • {F(LiveCd(live, t.swordsmanQCooldown - 0.5f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(q);

            var w = new AbilityCardData
            {
                Slot = LaneAbilitySlot.W,
                KeyBadge = "W (1ST HIT)",
                Name = "Stance Temper",
                MetaRight = $"Passive • Procs on Light Hit 1  •  Max {t.swordsmanWMaxStacks} stacks",
                PlainDescription = "Whenever your 1st light sabre attack hits a hero or minion, you steal defenses: building bonus Armor and Magic Resist on yourself while shredding the target's resistances (able to push enemy Armor/MR negative to amplify damage). Your self buff and enemy debuff timers refresh separately.",
                PracticalTip = "Trade in repeated short exchanges — your stolen Armor & MR persist even if you switch targets to finish a low minion.",
            };
            AddFact(w, "Self Gain", $"+8 / +10 / +12 Arm & MR/stack (6 max = +48/+60/+72)", ColArm);
            AddFact(w, "Enemy Shred", $"-{F(t.swordsmanWStackShred)} Arm & MR/stack (-48 min, can go negative!)", ColStr);
            AddFact(w, "Base Duration", $"{F(t.swordsmanWBuffDurationFormula.baseValue, "0.0")}s self & enemy (separate timers, +INT)");
            AddScalingRow(w, "INT", ColInt, "Both self buff & enemy debuff durations", "Linear", FormatFormula(t.swordsmanWBuffDurationFormula, "INT", "s", maxRanks: 1));
            w.MathLines.Add($"Per-Stack Resist (fixed by rank): +8/+10/+12 self Arm & MR; -{F(t.swordsmanWStackShred)} enemy Arm & MR; Max {t.swordsmanWMaxStacks} stacks");
            w.MathLines.Add($"Separate Timers: {FormatExactMath(t.swordsmanWBuffDurationFormula, "INT", "s", maxRanks: 1)}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankW);
                float dur = t.swordsmanWBuffDurationFormula.Evaluate(r, live.intelligence);
                float perStack = t.swordsmanWStackGain + 2f * (r - 1);
                w.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankW)}): +{F(perStack)}/stack ({F(live.swordResistDelta)} active) • {F(dur, "0.0")}s duration</color>";
            }
            g.Cards.Add(w);

            var e = new AbilityCardData
            {
                Slot = LaneAbilitySlot.E,
                KeyBadge = "E (2ND HIT)",
                Name = "Vital Draw",
                MetaRight = "Passive • Procs on Heavy Hit 2",
                PlainDescription = $"Whenever your 2nd heavy greatsword strike connects, it slices for bonus magic damage based on the target's Maximum Health and heals you for {F(t.swordsmanEHealShare * 100f)}% of that bonus damage (further boosted by Heal Power). Being stunned or knocked out of range before Hit 2 denies the heal.",
                PracticalTip = "Max this first into high-HP bruisers like Ogre, and keep enemies slowed so they cannot cancel your second blade swing.",
            };
            AddFact(e, "Bonus Magic Dmg", "4.5% / 5.9% / 7.3% Target Max HP (+STR)", ColMagic);
            AddFact(e, "Vampiric Heal", $"{F(t.swordsmanEHealShare * 100f)}% of bonus dmg × Heal Power", ColHeal);
            AddFact(e, "Minion Cap", "140 max bonus dmg vs creeps");
            AddScalingRow(e, "STR", ColStr, "Target % Max HP bonus magic damage", "Bounded diminishing", FormatFormula(t.swordsmanEPercentHpFormula, "STR", "", asPercent: true));
            AddScalingRow(e, "Heal Power", ColHeal, "Self healing from Vital Draw damage", "Linear", $"{F(t.swordsmanEHealShare * 100f)}% bonus dmg × Heal Power");
            e.MathLines.Add($"% Max HP Magic = {FormatExactMath(t.swordsmanEPercentHpFormula, "STR", "", asPercent: true)} (140 minion cap)");
            e.MathLines.Add($"Healing = FinalBonusDmg × {F(t.swordsmanEHealShare * 100f)}% × HealPower");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankE);
                float pct = t.swordsmanEPercentHpFormula.Evaluate(r, live.strength) * 100f;
                e.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankE)}): {F(pct, "0.0")}% Target Max HP • heals {F(t.swordsmanEHealShare * live.healPower * 100f)}% of bonus</color>";
            }
            g.Cards.Add(e);

            var ult = new AbilityCardData
            {
                Slot = LaneAbilitySlot.R,
                KeyBadge = "R",
                Name = "Finishing Cross",
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Range {F(t.swordsmanRRange, "0.0")}u  •  CD {F(t.swordsmanRCooldown)}/{F(t.swordsmanRCooldown - 4f)}s",
                PlainDescription = "Dashes rapidly past the last enemy marked by a basic attack or Hamstring Arc, slashing for heavy physical damage. If the enemy is at or below the execute health threshold, it executes them instantly and refreshes Finishing Cross's cooldown.",
                PracticalTip = "Hold R until the marked enemy drops into execute range — executing a hero resets the cooldown immediately while slashing early puts R on a long wait.",
            };
            AddFact(ult, "Damage", FormatRankProgressionOnly(t.swordsmanRDamageFormula, maxRanks: 2) + " + 135% AD Physical", ColPhys);
            AddFact(ult, "Execute Threshold", "≤ 20% / 24% Target Max HP (+STR)", ColCc);
            AddFact(ult, "Range / Cooldown", $"{F(t.swordsmanRRange, "0.0")}u dash range  •  CD {F(t.swordsmanRCooldown)}/{F(t.swordsmanRCooldown - 4f)}s");
            AddFact(ult, "Restriction", "Requires active Q / Basic mark within range", ColCc);
            AddScalingRow(ult, "AD", ColAd, "Slash physical damage", "Linear", FormatFormula(t.swordsmanRDamageFormula, "AD", maxRanks: 2));
            AddScalingRow(ult, "STR", ColStr, "Execute HP% threshold (<100%)", "Bounded diminishing", FormatFormula(t.swordsmanRExecuteFormula, "STR", "", asPercent: true, maxRanks: 2));
            AddScalingRow(ult, "AGI", ColAgi, "Dash travel speed (faster dash)", "Linear → Inverse", FormatFormula(t.swordsmanRDashSpeedFormula, "AGI", "u/s", maxRanks: 2));
            AddScalingRow(ult, "INT", ColInt, "Target mark memory duration", "Linear", FormatFormula(t.swordsmanRLookbackFormula, "INT", "s", maxRanks: 2));
            AddScalingRow(ult, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            ult.MathLines.Add($"Slash Damage = {FormatExactMath(t.swordsmanRDamageFormula, "AD", maxRanks: 2)} | Execute ≤ {FormatExactMath(t.swordsmanRExecuteFormula, "STR", "", asPercent: true, maxRanks: 2)}");
            ult.MathLines.Add($"Dash Speed = {FormatExactMath(t.swordsmanRDashSpeedFormula, "AGI", "u/s", maxRanks: 2)} | Mark Memory = {FormatExactMath(t.swordsmanRLookbackFormula, "INT", "s", maxRanks: 2)}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankR);
                float dmg = t.swordsmanRDamageFormula.Evaluate(r, live.attackDamage);
                float exec = t.swordsmanRExecuteFormula.Evaluate(r, live.strength) * 100f;
                float dspd = t.swordsmanRDashSpeedFormula.Evaluate(r, live.agility);
                float mem = t.swordsmanRLookbackFormula.Evaluate(r, live.intelligence);
                ult.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankR)}): {F(dmg)} dmg • ≤{F(exec, "0.0")}% exec • {F(dspd, "0.0")}u/s • {F(mem, "0.0")}s mark</color>";
            }
            g.Cards.Add(ult);
        }

        private static void BuildLaserShooterGuide(HeroGuideData g, LaneTuning t, HeroSnap live)
        {
            g.IdentitySummary = "Long-range laser ADC whose endless chained beams stack vulnerability, trigger explosive slow shocks, and step patiently out of trouble.";
            g.BaseStatsLine = $"HP <b>480</b> (+30/lvl)  •  <color={ColMana}>Mana <b>{F(t.laserBaseMana)}</b> (+{F(t.laserManaPerLevel)}/lvl, <b>{F(t.laserBaseManaRegen, "0.0")}/s</b> +{F(t.laserManaRegenPerLevel, "0.00")}/lvl)</color>  •  <color={ColAd}>AD <b>46</b> (+3.5/lvl)</color>  •  Arm <b>8</b>  •  MR <b>10</b>  •  Move <b>{F(t.laserSpeed, "0.00")}</b>";
            if (live != null)
            {
                float stackScale = t.laserStackMaxAmpFormula.Evaluate(1, live.intelligence) * 100f;
                float qExtMult = t.laserQSpeedFormula.Evaluate(1, live.intelligence);
                float bounceR = t.laserChainRadiusFormula.Evaluate(1, live.intelligence);
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • <color={ColMana}>Mana {F(live.mana)}/{F(live.maxMana)} ({live.manaRegen:+0.0;-0.0}/s, RCR -{F(live.rcrPct * 100f)}%)</color> • EffAS {F(live.attackSpeed, "0.00")}x • AGI {F(live.agility)} • INT {F(live.intelligence)} • Chain {F(bounceR, "0.00")}u • StackCap +{F(stackScale, "0.0")}% • QSpd {F(qExtMult, "0.00")}x</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Chain Laser & Amplifier Stacks",
                MetaRight = $"Range {F(t.laserBasicRange, "0.0")}u  •  Unlimited Hops",
                PlainDescription = "Basic attacks fire an instant laser that bounces endlessly to nearby unhit enemies (never repeating a target in one chain, losing some power each hop). Every laser hit adds a Laser Stack on the victim, amplifying all future laser damage they take from you.",
                PracticalTip = "Attack the front minion in a wave to bounce laser stacks onto a hiding enemy hero from safely beyond normal attack range.",
            };
            AddFact(passive, "Range / Damage", $"{F(t.laserBasicRange, "0.0")}u range  •  100% AD (0.70x/hop, 0.25x min)", ColPhys);
            AddFact(passive, "Resource", "0 Mana (Basic chains remain free at 0 Mana)", ColMana);
            AddFact(passive, "Hop Radius", $"{F(t.laserChainRadiusFormula.baseValue, "0.0")}u base (+INT)", ColInt);
            AddFact(passive, "Stack Amp", $"Up to +45% (+INT) over {F(t.laserStackDuration, "0.0")}s", ColInt);
            AddScalingRow(passive, "AD", ColAd, "Primary & chained beam physical damage", "Linear", "100% AD primary, decaying 0.70x per bounce");
            AddScalingRow(passive, "Attack Speed", ColAgi, "Attack fire rate", "Global Effective AS → Inverse", "Wind-up 0.22s & Rec 0.66s divide by Effective AS");
            AddScalingRow(passive, "INT", ColInt, "Bounce hop search distance", "Bounded diminishing", FormatFormula(t.laserChainRadiusFormula, "INT", "u", maxRanks: 1));
            AddScalingRow(passive, "INT", ColInt, "Maximum stack amplification asymptote", "Bounded diminishing", FormatFormula(t.laserStackMaxAmpFormula, "INT", "", asPercent: true, maxRanks: 1));
            passive.MathLines.Add($"Chain Hop Radius = {FormatExactMath(t.laserChainRadiusFormula, "INT", "u", maxRanks: 1)} | Falloff = max({F(t.laserChainMinRatio, "0.00")}, {F(t.laserChainDecay, "0.00")}^hop)");
            passive.MathLines.Add($"Stack Damage Mult = 1 + MaxAmp × stacks/(stacks+{F(t.laserStackHalfPoint)}); MaxAmp = {FormatExactMath(t.laserStackMaxAmpFormula, "INT", "", asPercent: true, maxRanks: 1)}");
            if (live != null)
            {
                float asym = t.laserStackMaxAmpFormula.Evaluate(1, live.intelligence) * 100f;
                float hop = t.laserChainRadiusFormula.Evaluate(1, live.intelligence);
                passive.LiveLine = $"<color={ColLive}>Now: {F(live.attackDamage, "0.0")} hit dmg • {F(hop, "0.00")}u hop • +{F(asym * 0.5f, "0.0")}% @ {F(t.laserStackHalfPoint)} stacks (+{F(asym, "0.0")}% max)</color>";
            }
            g.Cards.Add(passive);

            var q = new AbilityCardData
            {
                Slot = LaneAbilitySlot.Q,
                KeyBadge = "Q",
                Name = "Extending Line Beam",
                MetaRight = $"Fixed {F(t.laserQStartRange, "0.0")}→{F(t.laserQMaxRange, "0.0")}u  •  CD {F(t.laserQCooldown, "0.0")}/{F(t.laserQCooldown - 0.7f, "0.0")}/{F(t.laserQCooldown - 1.4f, "0.0")}s",
                PlainDescription = "Fires an extending beam from short range out to full range in a chosen direction, damaging each unit once and applying a Laser Stack. Killing multiple targets in one beam awards escalating bonus gold for every kill after the first.",
                PracticalTip = "Wait for ranged and melee creeps to align in a single line (especially with a hero behind them) to trigger multi-kill bonus gold.",
            };
            AddFact(q, "Damage", FormatRankProgressionOnly(t.laserQDamageFormula) + " + 105% AD (+Stacks)", ColPhys);
            AddFact(q, "Extend Speed", $"Base {F(t.laserQDuration, "0.00")}s duration (+INT speed)");
            AddFact(q, "Bonus Gold", $"+{F(t.laserQMultiKillBonusGold)}g escalating per extra kill", ColCc);
            AddFact(q, "Cooldown", $"CD {F(t.laserQCooldown, "0.0")}/{F(t.laserQCooldown - 0.7f, "0.0")}/{F(t.laserQCooldown - 1.4f, "0.0")}s");
            AddFact(q, "Mana Cost", $"{F(t.laserQManaBase)} / {F(t.laserQManaBase + t.laserQManaPerRank)} / {F(t.laserQManaBase + t.laserQManaPerRank * 2f)} Mana", ColMana);
            AddScalingRow(q, "AD", ColAd, "Beam physical damage", "Linear", FormatFormula(t.laserQDamageFormula, "AD"));
            AddScalingRow(q, "INT", ColInt, "Beam extension speed (replaces AGI)", "Unbounded sqrt → Inverse", FormatFormula(t.laserQSpeedFormula, "INT", "x", maxRanks: 1));
            AddScalingRow(q, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            AddScalingRow(q, "Resource Cost Reduction", ColMana, "Reduces Mana cost (<100% cap)", "Bounded diminishing",
                FormatFormula(t.resourceCostReductionFormula, "RCR", "", asPercent: true, maxRanks: 1),
                live != null && live.costQ > 0f ? $"{F(live.costQ)} Mana" : "");
            q.MathLines.Add($"Damage = {FormatExactMath(t.laserQDamageFormula, "AD")} × LaserStackMultiplier");
            q.MathLines.Add($"Extension Duration = {F(t.laserQDuration, "0.00")}s / ({FormatExactMath(t.laserQSpeedFormula, "INT", "x", maxRanks: 1)})");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankQ);
                float dmg = t.laserQDamageFormula.Evaluate(r, live.attackDamage);
                float dur = t.laserQDuration / Mathf.Max(1f, t.laserQSpeedFormula.Evaluate(r, live.intelligence));
                q.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankQ)}): {F(dmg)} dmg • {F(dur, "0.00")}s extend • {F(LiveCd(live, t.laserQCooldown - 0.7f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(q);

            var w = new AbilityCardData
            {
                Slot = LaneAbilitySlot.W,
                KeyBadge = "W",
                Name = "Resonant Stack Shock",
                MetaRight = $"Scan {F(t.laserWCastRange, "0.0")}u  •  CD {F(t.laserWCooldown, "0.0")}/{F(t.laserWCooldown - 0.7f, "0.0")}/{F(t.laserWCooldown - 1.4f, "0.0")}s",
                PlainDescription = "Triggers a shockwave from every nearby enemy carrying Laser Stacks. Each shock deals magic damage and slows nearby foes; overlapping shockwaves stack slow strength softly and extend slow duration so detonating a full minion wave slows heavily.",
                PracticalTip = "Bounce laser basics across a minion wave first, then cast W while the enemy stands inside the wave to stack multiple overlapping shocks.",
            };
            AddFact(w, "Shock Damage", FormatRankProgressionOnly(t.laserWDamageFormula) + " + 70% INT Magic/shock", ColMagic);
            AddFact(w, "Crowd Control", $"20/24/28% single → up to {F(t.laserWSlowMaxCap * 100f)}% overlapping Slow", ColCc);
            AddFact(w, "Radius / Range", $"{F(t.laserWShockRadiusFormula.baseValue, "0.00")}u (+INT) shock • {F(t.laserWCastRange, "0.0")}u scan");
            AddFact(w, "Cooldown", $"CD {F(t.laserWCooldown, "0.0")}/{F(t.laserWCooldown - 0.7f, "0.0")}/{F(t.laserWCooldown - 1.4f, "0.0")}s");
            AddFact(w, "Mana Cost", $"{F(t.laserWManaBase)} / {F(t.laserWManaBase + t.laserWManaPerRank)} / {F(t.laserWManaBase + t.laserWManaPerRank * 2f)} Mana (Independent of enemy count)", ColMana);
            AddFact(w, "Requirement", "Needs ≥1 stacked hostile unit in range", ColCc);
            AddScalingRow(w, "INT", ColInt, "Per-shock magic damage", "Linear", FormatFormula(t.laserWDamageFormula, "INT"));
            AddScalingRow(w, "INT", ColInt, "Shockwave explosion radius", "Bounded diminishing", FormatFormula(t.laserWShockRadiusFormula, "INT", "u"));
            AddScalingRow(w, "Overlapping Shocks", ColCc, "Combined slow strength (bounded) & duration (sqrt)", "Bounded + Unbounded sqrt", $"Base 20/24/28% → {F(t.laserWSlowMaxCap * 100f)}% limit; Dur {F(t.laserWSlowDuration, "0.00")}s × sqrt(n)");
            AddScalingRow(w, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            AddScalingRow(w, "Resource Cost Reduction", ColMana, "Reduces Mana cost (<100% cap)", "Bounded diminishing",
                FormatFormula(t.resourceCostReductionFormula, "RCR", "", asPercent: true, maxRanks: 1),
                live != null && live.costW > 0f ? $"{F(live.costW)} Mana" : "");
            w.MathLines.Add($"Shock Damage = ({FormatExactMath(t.laserWDamageFormula, "INT")}) × (1 + 0.12×(srcStacks-1)) × TargetStackMult | Shock Radius = {FormatExactMath(t.laserWShockRadiusFormula, "INT", "u")}");
            w.MathLines.Add($"Overlap Slow (anti-bypass): Single=20/24/28%; TotalSlow → bounded {F(t.laserWSlowMaxCap * 100f)}% cap; TotalDur = {F(t.laserWSlowDuration, "0.00")}s × sqrt(n) minus prior elapsed");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankW);
                float dmg = t.laserWDamageFormula.Evaluate(r, live.intelligence);
                float rad = t.laserWShockRadiusFormula.Evaluate(r, live.intelligence);
                w.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankW)}): {F(dmg, "0.0")} dmg/shock • {F(rad, "0.00")}u radius • {F(LiveCd(live, t.laserWCooldown - 0.7f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(w);

            var e = new AbilityCardData
            {
                Slot = LaneAbilitySlot.E,
                KeyBadge = "E",
                Name = "Hologram Reposition",
                MetaRight = $"Step {F(t.laserEStepBack, "0.00")}u  •  CD {F(t.laserECooldown, "0.0")}/{F(t.laserECooldown - 0.9f, "0.0")}/{F(t.laserECooldown - 1.8f, "0.0")}s",
                PlainDescription = "Calmly takes an animated single step backward away from danger (not an instant blink) and places a stationary hologram decoy at your starting spot once the step finishes. The hologram blocks enemy movement and non-tower projectiles, and retaliates with magic damage and a brief stun if attacked.",
                PracticalTip = "Step straight back right before a projectile lands or while a melee enemy lunges in so your hologram catches the hit and stuns them.",
            };
            AddFact(e, "Step Back", $"{F(t.laserEStepBack, "0.00")}u animated pace (stun interrupts step)", ColAgi);
            AddFact(e, "Retaliation Zap", FormatRankProgressionOnly(t.laserEDecoyDamageFormula) + " + 65% INT Magic", ColMagic);
            AddFact(e, "Crowd Control", "0.65 / 0.80 / 0.95s Stun (rank-only)", ColCc);
            AddFact(e, "Decoy Lifetime", $"{F(t.laserEDecoyDuration, "0.0")}s ({F(t.laserEDecoyHp)} HP)");
            AddFact(e, "Cooldown", $"CD {F(t.laserECooldown, "0.0")}/{F(t.laserECooldown - 0.9f, "0.0")}/{F(t.laserECooldown - 1.8f, "0.0")}s");
            AddFact(e, "Mana Cost", $"{F(t.laserEManaFlat)} Mana (Constant across ranks)", ColMana);
            AddScalingRow(e, "AGI", ColAgi, "Step-back animation speed (shorter duration)", "Bounded diminishing", FormatFormula(t.laserEStepDurationFormula, "AGI", "s"));
            AddScalingRow(e, "INT", ColInt, "Decoy retaliation magic damage", "Linear", FormatFormula(t.laserEDecoyDamageFormula, "INT"));
            AddScalingRow(e, "Rank Only", ColCc, "Retaliation stun duration (no attribute scaling)", "Rank-only", "0.65 / 0.80 / 0.95s");
            AddScalingRow(e, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            AddScalingRow(e, "Resource Cost Reduction", ColMana, "Reduces Mana cost (<100% cap)", "Bounded diminishing",
                FormatFormula(t.resourceCostReductionFormula, "RCR", "", asPercent: true, maxRanks: 1),
                live != null && live.costE > 0f ? $"{F(live.costE)} Mana" : "");
            e.MathLines.Add($"Step Duration = {FormatExactMath(t.laserEStepDurationFormula, "AGI", "s")} | Distance fixed {F(t.laserEStepBack, "0.00")}u");
            e.MathLines.Add($"Retaliation = {FormatExactMath(t.laserEDecoyDamageFormula, "INT")} + 1 Laser Stack + 0.65/0.80/0.95s stun (rank-only)");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankE);
                float stepS = t.laserEStepDurationFormula.Evaluate(r, live.agility);
                float zap = t.laserEDecoyDamageFormula.Evaluate(r, live.intelligence);
                float stun = t.laserEStunFormula.Evaluate(r, 0f);
                e.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankE)}): {F(stepS, "0.00")}s step • {F(zap, "0.0")} zap • {F(stun, "0.00")}s stun • {F(LiveCd(live, t.laserECooldown - 0.9f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(e);

            var ult = new AbilityCardData
            {
                Slot = LaneAbilitySlot.R,
                KeyBadge = "R",
                Name = "Overdrive Prism Beam",
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Beam {F(t.laserRRange, "0.0")}u × {F(t.laserRHalfWidth * 2f, "0.00")}u  •  CD {F(t.laserRCooldown)}/{F(t.laserRCooldown - 4f)}s",
                PlainDescription = $"After a brief {F(t.laserRWindup, "0.00")}s charge-up, fires a sustained prism beam forward while allowing you to keep moving (you can press Stop to halt your feet without cancelling the beam). Every enemy inside the beam triggers rapid Chain Laser basic-attack procs.",
                PracticalTip = "Kite backward along the lane while keeping both the enemy hero and creep wave caught inside your beam for explosive multi-bounce damage.",
            };
            AddFact(ult, "Proc Damage", $"{F(t.laserRProcDamageScale * 100f)}% AD + Chain Bounces/Stacks", ColPhys);
            AddFact(ult, "Proc Interval", $"Every {F(t.laserRProcInterval, "0.00")}s ÷ Effective AS", ColAgi);
            AddFact(ult, "Duration", $"{F(t.laserRDurationFormula.baseValue, "0.0")}/{F(t.laserRDurationFormula.baseValue + t.laserRDurationFormula.perRank, "0.0")}s (+INT)", ColInt);
            AddFact(ult, "Beam Size / Cooldown", $"{F(t.laserRRange, "0.0")}u × {F(t.laserRHalfWidth * 2f, "0.00")}u  •  CD {F(t.laserRCooldown)}/{F(t.laserRCooldown - 4f)}s");
            AddFact(ult, "Upfront Mana", $"{F(t.laserRManaUpfront)} Mana on cast (0 continuous drain)", ColMana);
            AddScalingRow(ult, "AD", ColAd, "Damage per Overdrive beam tick", "Linear", "62% AD physical per proc (+Chain bounces)");
            AddScalingRow(ult, "Attack Speed", ColAgi, "Beam proc cadence (faster ticks)", "Global Effective AS → Inverse", $"Every {F(t.laserRProcInterval, "0.00")}s / Effective AS");
            AddScalingRow(ult, "INT", ColInt, "Beam active duration", "Bounded diminishing", FormatFormula(t.laserRDurationFormula, "INT", "s", maxRanks: 2));
            AddScalingRow(ult, "AGI", ColAgi, "Movement speed retained while firing (<100%)", "Bounded diminishing", FormatFormula(t.laserRMoveRatioFormula, "AGI", "x", maxRanks: 2));
            AddScalingRow(ult, "Ability Haste", ColInt, "Cooldown reduction", "Inverse", "Cooldown = Base × 100 / (100 + AH)");
            AddScalingRow(ult, "Resource Cost Reduction", ColMana, "Reduces upfront Mana cost (<100% cap)", "Bounded diminishing",
                FormatFormula(t.resourceCostReductionFormula, "RCR", "", asPercent: true, maxRanks: 1),
                live != null && live.costR > 0f ? $"{F(live.costR)} Mana" : "");
            ult.MathLines.Add($"Proc Damage = 62% AD every {F(t.laserRProcInterval, "0.00")}s / EffectiveAS (up to 16 procs/tick accumulator)");
            ult.MathLines.Add($"Duration = {FormatExactMath(t.laserRDurationFormula, "INT", "s", maxRanks: 2)} | Retained Move Speed = {FormatExactMath(t.laserRMoveRatioFormula, "AGI", "x", maxRanks: 2)}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankR);
                float procDmg = live.attackDamage * t.laserRProcDamageScale;
                float dur = t.laserRDurationFormula.Evaluate(r, live.intelligence);
                float movePct = t.laserRMoveRatioFormula.Evaluate(r, live.agility) * 100f;
                float iv = t.laserRProcInterval / Mathf.Max(0.4f, live.attackSpeed);
                ult.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankR)}): {F(procDmg, "0.0")} dmg every {F(iv, "0.00")}s • {F(dur, "0.00")}s beam • {F(movePct, "0.0")}% MS</color>";
            }
            g.Cards.Add(ult);
        }

        private static string FormatRankProgressionOnly(StatFormula f, string unit = "", bool asPercent = false, int maxRanks = 3)
        {
            var sb = new StringBuilder();
            AppendRankProgression(sb, f, unit, asPercent, maxRanks);
            return sb.ToString();
        }

        private static void AppendRankProgression(StringBuilder sb, StatFormula f, string unit, bool asPercent, int maxRanks)
        {
            int ranks = Mathf.Max(1, maxRanks);
            for (int r = 1; r <= ranks; r++)
            {
                if (r > 1) sb.Append(" / ");
                float val = f.baseValue + f.perRank * (r - 1);
                sb.Append(FormatScalar(val, asPercent));
            }
            if (!asPercent && !string.IsNullOrEmpty(unit))
            {
                sb.Append(unit);
            }
        }

        private static string FormatScalar(float v, bool asPercent)
        {
            if (asPercent)
            {
                float pct = v * 100f;
                return Mathf.Abs(pct - Mathf.Round(pct)) < 0.05f ? $"{F(pct)}%" : $"{F(pct, "0.0")}%";
            }
            if (Mathf.Abs(v - Mathf.Round(v)) < 0.01f) return F(v);
            if (Mathf.Abs(v * 10f - Mathf.Round(v * 10f)) < 0.02f) return F(v, "0.0");
            return F(v, "0.00");
        }

        private static float LiveCd(HeroSnap live, float baseCd) =>
            Mathf.Max(0.4f, baseCd * LaneMatch.CooldownFactor(live.abilityHaste));

        private static string RankTag(int rank) => rank > 0 ? $"R{rank}" : "Unlearned / R1 preview";
    }
}
