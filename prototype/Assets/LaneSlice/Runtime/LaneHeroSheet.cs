using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Data-driven MOBA ability/stat guide formatter producing both pretty color-coded
    /// ability cards (for Lobby, [C] Scaling sheet, and HUD hover tooltips) and exact simulation formulas.
    /// Explicitly distinguishes Linear, Bounded diminishing, Unbounded diminishing, and Inverse duration curves.
    /// </summary>
    public static class LaneHeroSheet
    {
        public const string ColAd = "#ff9c4a";
        public const string ColStr = "#ff6b6b";
        public const string ColAgi = "#5de287";
        public const string ColInt = "#62c6ff";
        public const string ColHeal = "#66ffd1";
        public const string ColMagic = "#c58cff";
        public const string ColPhys = "#ffbd59";
        public const string ColCc = "#ffe066";
        public const string ColLive = "#78ff9e";
        public const string ColMuted = "#b0bec8";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public sealed class AbilityCardData
        {
            public LaneAbilitySlot Slot;
            public string KeyBadge;
            public string Name;
            public string MetaRight;
            public string Description;
            public readonly List<string> StatLines = new List<string>();
            public string LiveLine;
            public string MathLine;
        }

        public sealed class HeroGuideData
        {
            public LaneHeroId Hero;
            public string Title;
            public string RoleLine;
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
                return $"[Bounded diminishing reduction] [{baseSb}] - {redStr} × {sLabel} / ({sLabel} + {F(f.halfPoint, "0.##")}) → {F(floor, "0.00")}{unit}";
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

            g.RuleLines.Add($"<color={ColInt}><b>Zero Free Attribute Stats:</b></color> <color={ColStr}>STR</color>, <color={ColAgi}>AGI</color>, and <color={ColInt}>INT</color> start at <b>0</b> and only affect abilities or items that explicitly scale with them.");
            g.RuleLines.Add($"<color={ColAgi}><b>Global Attack Speed (Unbounded diminishing → Inverse duration):</b></color> Effective AS = <b>sqrt(1 + {F(t.attackSpeedCurveK, "0.##")} × Bonus AS)</b> (+30% AS → {F(LaneTuning.EffectiveAttackSpeed(0.30f, t.attackSpeedCurveK), "0.00")}x, +120% → {F(LaneTuning.EffectiveAttackSpeed(1.20f, t.attackSpeedCurveK), "0.00")}x, no hard cap). Governs basics, Swordsman hits, Ogre recovery, & Laser R procs.");
            g.RuleLines.Add($"<color={ColAgi}><b>The Coward's Shield (450g):</b></color> +15 Arm/MR, +12 AGI • Sustained retreat velocity away from recent hero attacker ({F(t.cowardShieldMemorySeconds, "0.0")}s) builds {FormatPrettyFormula(t.cowardShieldResistFormula, ColCc, "AGI", ColAgi, " Arm/MR", maxRanks: 1)}; {F(t.cowardShieldGraceSeconds, "0.00")}s grace holds (never builds) on brief stop/stun/obstacle; decays in {F(t.cowardShieldStopDecaySeconds, "0.0")}s stopped or {F(t.cowardShieldPursuitDecaySeconds, "0.00")}s pursuing.");
            g.RuleLines.Add($"<color={ColHeal}><b>Kindred Charm (325g) & Colossus Pauldron (625g):</b></color> Kindred Charm emits +{F(1.6f, "0.0")} × HealPower HP/s within {F(t.kindredAuraRadius, "0.0")}u for allies on <b>any</b> hero; Colossus triggers when its owner inflicts Stun/Knockback or drops <45% HP.");
            g.RuleLines.Add($"<color={ColMuted}><b>Scaling Types & Curves:</b></color> <b>Linear</b> = constant gain/pt; <b>Bounded diminishing</b> = M × S/(S+H); <b>Unbounded diminishing</b> = sqrt(1+k×S); <b>Inverse duration</b> = Base / SpeedMult. Arm/MR use 100/(100+R) above 0 & 2 - 100/(100-R) below 0; Ability Haste uses Base × 100/(100+AH).</color>");
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

        public static string BuildItemTooltip(LaneItemDef item, bool canSellNow)
        {
            var sb = new StringBuilder();
            string tierCol = item.Tier == LaneItemTier.Starter ? ColMuted
                : (item.Tier == LaneItemTier.Capstone ? ColCc : ColInt);
            sb.AppendLine($"<b><size=14>{item.Name}</size></b>  <color={tierCol}>[{item.Tier}]</color>  <color=#ffdf5c>{item.Cost}g</color>");
            sb.AppendLine(item.Summary);
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
            sb.AppendLine($"<b><size=15>{g.Title}</size></b>");
            sb.AppendLine(g.BaseStatsLine);
            if (!string.IsNullOrEmpty(g.LiveStatsLine)) sb.AppendLine(g.LiveStatsLine);

            for (int i = 0; i < g.Cards.Count; i++)
            {
                AbilityCardData c = g.Cards[i];
                sb.AppendLine();
                sb.AppendLine($"<b>[{c.KeyBadge}] {c.Name}</b> ({c.MetaRight})");
                if (!string.IsNullOrEmpty(c.Description)) sb.AppendLine($"  {c.Description}");
                for (int s = 0; s < c.StatLines.Count; s++) sb.AppendLine($"  {c.StatLines[s]}");
                if (!string.IsNullOrEmpty(c.LiveLine)) sb.AppendLine($"  {c.LiveLine}");
                if (!string.IsNullOrEmpty(c.MathLine)) sb.AppendLine($"  <color={ColMuted}>{c.MathLine}</color>");
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
            g.BaseStatsLine = $"HP <b>720</b> (+48/lvl)  •  <color={ColAd}>AD <b>60</b> (+5/lvl)</color>  •  Arm <b>16</b>  •  MR <b>12</b>  •  Regen <b>1.6/s</b>  •  Move <b>{F(t.ogreSpeed, "0.00")}</b>  •  Turn <b>{F(t.ogreTurnRate)}°/s</b>";
            if (live != null)
            {
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • Eff AS {F(live.attackSpeed, "0.00")}x (+{F(live.bonusAttackSpeed * 100f)}%) • STR {F(live.strength)} • AGI {F(live.agility)} • AH {F(live.abilityHaste)} • Wind {F(live.ogreWindupSpeed, "0.00")}x / Rec {F(live.ogreRecoverSpeed, "0.00")}x</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Split Wind-up & Recovery Animation Scaling",
                MetaRight = t.ogreImmuneDuringRecovery ? "CC-Immune: Windup + Recovery" : "CC-Immune: Cast Wind-up",
                Description = "No basic attacks. Wind-ups scale exclusively with Agility (sqrt) while post-hit recoveries scale exclusively with global Attack Speed (sqrt) across Q/W/E and queued R strikes.",
                MathLine = $"Windup: {FormatExactMath(t.ogreWindupSpeedFormula, "AGI", "x", maxRanks: 1)} | Recover: EffectiveAS = sqrt(1 + {F(t.attackSpeedCurveK, "0.##")} × BonusAS) → Recover / EffectiveAS",
            };
            passive.StatLines.Add($"<b>Wind-up Speed:</b> {FormatPrettyFormula(t.ogreWindupSpeedFormula, ColAgi, "AGI", ColAgi, "x", maxRanks: 1)}");
            passive.StatLines.Add($"<b>Recovery Speed:</b> <color={ColAgi}>Global Effective AS = sqrt(1 + {F(t.attackSpeedCurveK, "0.##")} × Bonus AS) (Unbounded diminishing → Inverse duration)</color>");
            if (live != null) passive.LiveLine = $"<color={ColLive}>Now: Wind-ups {F(live.ogreWindupSpeed, "0.00")}x (AGI) • Recoveries {F(live.ogreRecoverSpeed, "0.00")}x (AS)</color>";
            g.Cards.Add(passive);

            var q = new AbilityCardData
            {
                Slot = LaneAbilitySlot.Q,
                KeyBadge = "Q",
                Name = "Club Slam (Central + Shockwave)",
                MetaRight = $"Central {F(t.ogreQRange, "0.0")}u × {F(t.ogreQWidth * 2f, "0.00")}u  •  Wind {F(t.ogreQWindup, "0.00")}s / Rec {F(t.ogreQRecover, "0.00")}s  •  CD {F(t.ogreQCooldown, "0.0")}/{F(t.ogreQCooldown - 0.6f, "0.0")}/{F(t.ogreQCooldown - 1.2f, "0.0")}s",
                Description = $"Two mutually exclusive zones: Central club impact deals full damage + stuns (size scales linearly w/ R); Outer shockwave deals {F(t.ogreQOuterDamageRatio * 100f)}% damage with zero CC (outer width scales w/ STR).",
                MathLine = $"Dmg {FormatExactMath(t.ogreQDamageFormula, "AD")} | Stun {FormatExactMath(t.ogreQStunFormula, "STR", "s")} | Outer +{FormatExactMath(t.ogreQOuterWidthFormula, "STR", "u")}",
            };
            q.StatLines.Add($"<b>Central Impact:</b> {FormatPrettyFormula(t.ogreQDamageFormula, ColPhys, "AD", ColAd)}  •  Stun {FormatPrettyFormula(t.ogreQStunFormula, ColCc, "STR", ColStr, "s")}");
            q.StatLines.Add($"<b>Outer Shockwave:</b> <color={ColPhys}>{F(t.ogreQOuterDamageRatio * 100f)}% Dmg (no CC)</color>  •  +Width {FormatPrettyFormula(t.ogreQOuterWidthFormula, ColCc, "STR", ColStr, "u")}");
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
                MetaRight = $"Cone {F(t.ogreWRange, "0.0")}u  •  Wind {F(t.ogreWWindup, "0.00")}s / Rec {F(t.ogreWRecover, "0.00")}s  •  CD {F(t.ogreWCooldown, "0.0")}/{F(t.ogreWCooldown - 0.6f, "0.0")}/{F(t.ogreWCooldown - 1.2f, "0.0")}s",
                Description = "Sweep horizontally in a wide arc (scales w/ R size), blasting targets away via unbounded square-root STR knockback (stops at trees/walls; huge STR can hurl foes out of reach!).",
                MathLine = $"Dmg {FormatExactMath(t.ogreWDamageFormula, "AD")} | KB {FormatExactMath(t.ogreWKnockbackFormula, "STR", "u")}",
            };
            w.StatLines.Add($"<b>Physical Damage:</b> {FormatPrettyFormula(t.ogreWDamageFormula, ColPhys, "AD", ColAd)}");
            w.StatLines.Add($"<b>Knockback Distance:</b> {FormatPrettyFormula(t.ogreWKnockbackFormula, ColCc, "STR", ColStr, "u")}");
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
                MetaRight = $"Wind {F(t.ogreEWindup, "0.00")}s / Rec {F(t.ogreERecover, "0.00")}s  •  CD {F(t.ogreECooldown, "0.0")}/{F(t.ogreECooldown - 0.8f, "0.0")}/{F(t.ogreECooldown - 1.6f, "0.0")}s",
                Description = "Stomp the earth, dealing damage and stunning every enemy uniformly across the full STR-scaled radius (also scales linearly with R size).",
                MathLine = $"Dmg {FormatExactMath(t.ogreEDamageFormula, "AD")} | Stun {FormatExactMath(t.ogreEStunFormula, "STR", "s")} | Radius ({FormatExactMath(t.ogreERadiusFormula, "STR", "u", maxRanks: 1)}) × Size",
            };
            e.StatLines.Add($"<b>Physical Damage:</b> {FormatPrettyFormula(t.ogreEDamageFormula, ColPhys, "AD", ColAd)}");
            e.StatLines.Add($"<b>Stun & Radius:</b> {FormatPrettyFormula(t.ogreEStunFormula, ColCc, "STR", ColStr, "s")}  •  {FormatPrettyFormula(t.ogreERadiusFormula, ColStr, "STR", ColStr, "u", maxRanks: 1)}");
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
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Channel {F(t.ogreRChannelDuration, "0.0")}s  •  CD {F(t.ogreRCooldown)}/{F(t.ogreRCooldown - 4f)}s",
                Description = $"Grow in size (STR bounded) with {F(t.ogreRDamageReduction * 100f)}% fixed DR and queue up to {t.ogreRMaxStrikes} Q/W strikes. Attack dimensions (Q central/W/E) scale linearly proportionally with R size; final hit stays slower & devastating.",
                MathLine = $"Size {FormatExactMath(t.ogreRScaleFormula, "STR", "x", maxRanks: 2)} | Strike Wind = ({F(t.ogreRFastWindup, "0.00")}/{F(t.ogreRFinalWindup, "0.00")}s) / WindupSpd + {F(t.ogreRStrikeFollowthrough, "0.00")}s / RecSpd",
            };
            ult.StatLines.Add($"<b>Transformation Size:</b> {FormatPrettyFormula(t.ogreRScaleFormula, ColStr, "STR", ColStr, "x", maxRanks: 2)}  <color={ColMuted}>(× proportional area dimensions)</color>");
            ult.StatLines.Add($"<b>Combo Hits:</b> <color={ColPhys}>0.82x</color> Q/W → Final <color={ColCc}><b>{F(t.ogreRFinalCritMultiplier, "0.00")}x CRIT</b></color> • Wind=AGI (sqrt), Rec=AS (sqrt)");
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
            g.BaseStatsLine = $"HP <b>530</b> (+34/lvl)  •  <color={ColAd}>AD <b>48</b> (+3.5/lvl)</color>  •  Arm <b>8</b>  •  MR <b>18</b>  •  Regen <b>1.4/s</b>  •  Move <b>{F(t.luisSpeed, "0.00")}</b>  •  Turn <b>{F(t.luisTurnRate)}°/s</b>";
            if (live != null)
            {
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • INT {F(live.intelligence)} • AH {F(live.abilityHaste)} • HealPower {F(live.healPower * 100f)}% (+{F((live.healPower - 1f) * 100f)}% bonus) • Eff AS {F(live.attackSpeed, "0.00")}x</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Life & Negative-Regen Aura + Takedowns",
                MetaRight = $"Ranged {F(t.luisBasicRange, "0.0")}u  •  AS Scaled",
                Description = "Allied regen bonus & enemy regen drain both scale linearly with Heal Power (base 100%, +10% permanent per takedown). Enemy regen can become NEGATIVE, causing real HP loss and lethal takedowns!",
                MathLine = $"Ally = ({F(t.luisAllyAuraRegenBase, "0.0")}+0.35×(Lvl-1)) × HealPower | Drain = ({F(t.luisEnemyAuraRegenPenaltyBase, "0.0")}+0.35×(Lvl-1)) × HealPower | Radius {FormatExactMath(t.luisAuraRadiusFormula, "INT", "u", maxRanks: 1)}",
            };
            passive.StatLines.Add($"<b>Regen Aura:</b> <color={ColHeal}>+{F(t.luisAllyAuraRegenBase, "0.0")} ally</color> / <color={ColMagic}>-{F(t.luisEnemyAuraRegenPenaltyBase, "0.0")} enemy HP/s</color> × <color={ColHeal}>Heal Power (Linear, starts 100%)</color>");
            passive.StatLines.Add($"<b>Aura Radius:</b> {FormatPrettyFormula(t.luisAuraRadiusFormula, ColInt, "INT", ColInt, "u", maxRanks: 1)}  •  <color={ColHeal}>+{F(t.luisTakedownHealPowerGain * 100f)}% permanent HealPower / kill</color>");
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
                MetaRight = $"Interval {F(t.luisOrbInterval, "0.0")}/{F(t.luisOrbInterval - 0.6f, "0.0")}/{F(t.luisOrbInterval - 1.2f, "0.0")}s × AH  •  Lasts {F(t.luisOrbDuration)}s",
                Description = "Periodically drops a lane orb (spawn interval scaled inversely by Ability Haste, no 4s floor). Allies trigger heal+damage pulse; enemies stepping on it deny the orb.",
                MathLine = $"Heal [68/88/108] × HealPower | Dmg [62/80/98] × HealPower | Radius {FormatExactMath(t.luisOrbPulseRadiusFormula, "INT", "u")}",
            };
            q.StatLines.Add($"<b>Orb Heal & Magic Dmg:</b> <color={ColHeal}>68/88/108 heal</color> • <color={ColMagic}>62/80/98 dmg</color> × <color={ColHeal}>Heal Power (Linear)</color>");
            q.StatLines.Add($"<b>Pulse Radius:</b> {FormatPrettyFormula(t.luisOrbPulseRadiusFormula, ColInt, "INT", ColInt, "u")}  •  <color={ColMuted}>Spawn interval: Inverse AH</color>");
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
                MetaRight = $"CD {F(t.luisWCooldown, "0.0")}/{F(t.luisWCooldown - 1.1f, "0.0")}/{F(t.luisWCooldown - 2.2f, "0.0")}s  •  Invis {F(t.luisWDuration, "0.0")}/{F(t.luisWDuration + 0.4f, "0.0")}/{F(t.luisWDuration + 0.8f, "0.0")}s",
                Description = $"Turn invisible and send a {F(t.luisWIllusionDuration, "0.0")}s decoy walking to your cursor position (stops cleanly on arrival). No attribute scaling — rewards deception & Ability Haste.",
            };
            w.StatLines.Add($"<b>Stealth & Decoy:</b> <color={ColMuted}>Fixed durations (scales with Ability Haste CD only)</color>");
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
                MetaRight = $"Ward {F(t.luisEWardDuration, "0.0")}/{F(t.luisEWardDuration + 0.6f, "0.0")}/{F(t.luisEWardDuration + 1.2f, "0.0")}s  •  CD {F(t.luisECooldown, "0.0")}/{F(t.luisECooldown - 0.8f, "0.0")}/{F(t.luisECooldown - 1.6f, "0.0")}s",
                Description = "Guard yourself & decoys without breaking W stealth. Enemy hero hits Curse attackers; enemy casts escalate stacks up to 5. Durations remain fixed.",
                MathLine = $"1-stack DPS {FormatExactMath(t.luisCurseDpsFormula, "INT")}; ×(1 + 0.35×(stacks-1)), +{F(t.luisECurseBonusDuration, "0.0")}s/cast (fixed)",
            };
            e.StatLines.Add($"<b>1-Stack Magic DPS:</b> {FormatPrettyFormula(t.luisCurseDpsFormula, ColMagic, "INT", ColInt)} <color={ColMuted}>(×2.40 at 5 stacks)</color>");
            e.StatLines.Add($"<b>Curse Duration:</b> <color={ColCc}>{F(t.luisECurseBaseDuration, "0.0")}/{F(t.luisECurseBaseDuration + 0.4f, "0.0")}/{F(t.luisECurseBaseDuration + 0.8f, "0.0")}s</color> + {F(t.luisECurseBonusDuration, "0.0")}s per enemy cast (fixed)");
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
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Range {F(t.luisRRange, "0.0")}u  •  CD {F(t.luisRCooldown)}/{F(t.luisRCooldown - 3f)}s",
                Description = $"Channel {F(t.luisRChannelDuration, "0.0")}s: zone ramps 0→100%, amplifying ALL allied healing received and enemy damage taken (scaled by bonus Heal Power above 100%) across an INT-scaled radius.",
                MathLine = $"Radius {FormatExactMath(t.luisRRadiusFormula, "INT", "u", maxRanks: 2)} | HealAmp {FormatExactMath(t.luisRHealAmpFormula, "BonusHealPwr", "", asPercent: true, maxRanks: 2, customDomain: "(HealPwr-1)")} | Vuln {FormatExactMath(t.luisRVulnFormula, "BonusHealPwr", "", asPercent: true, maxRanks: 2, customDomain: "(HealPwr-1)")}",
            };
            ult.StatLines.Add($"<b>Zone Radius:</b> {FormatPrettyFormula(t.luisRRadiusFormula, ColInt, "INT", ColInt, "u", maxRanks: 2)}  •  <color={ColCc}>Fixed {F(t.luisRMaxSlow * 100f)}%/{F((t.luisRMaxSlow + 0.06f) * 100f)}% max slow</color>");
            ult.StatLines.Add($"<b>Max Ally Heal Amp:</b> <color={ColHeal}>25% / 30%</color> (+Bounded diminishing from Bonus HealPower >100%)");
            ult.StatLines.Add($"<b>Max Enemy Vuln:</b> <color={ColCc}>28% / 34%</color> (+Bounded diminishing from Bonus HealPower >100%)");
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
            g.BaseStatsLine = $"HP <b>615</b> (+40/lvl)  •  <color={ColAd}>AD <b>54</b> (+4/lvl)</color>  •  Arm <b>14</b>  •  MR <b>14</b>  •  Regen <b>1.5/s</b>  •  Move <b>{F(t.swordsmanSpeed, "0.00")}</b>  •  Turn <b>{F(t.swordsmanTurnRate)}°/s</b>";
            if (live != null)
            {
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • STR {F(live.strength)} • AGI {F(live.agility)} • INT {F(live.intelligence)} • Eff AS {F(live.attackSpeed, "0.00")}x • HPwr {F(live.healPower * 100f)}%</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Asymmetric Twin-Blade",
                MetaRight = $"Melee {F(t.swordsmanBasicRange, "0.00")}u  •  AS + AGI",
                Description = "1st Strike (Light Right Sabre): fast wind-up, lower AD, procs [W] Stance Temper. 2nd Strike (Heavy Left Greatsword): deliberate heavier swing, higher AD, procs [E] Vital Draw.",
                MathLine = $"Hit1={F(t.swordsmanHit1Windup, "0.00")}s/EffAS | Hit2 Delay={FormatExactMath(t.swordsmanHit2DelayFormula, "AGI", "s", maxRanks: 1)} / EffAS | Recover={F(t.swordsmanBasicRecover, "0.00")}s/EffAS",
            };
            passive.StatLines.Add($"<b>Blade Damage (Linear AD):</b> <color={ColPhys}>Hit 1 = {F(t.swordsmanHit1AdRatio * 100f)}% AD</color>  →  <color={ColAd}>Hit 2 = {F(t.swordsmanHit2AdRatio * 100f)}% AD</color> (= {F((t.swordsmanHit1AdRatio + t.swordsmanHit2AdRatio) * 100f)}% total)");
            passive.StatLines.Add($"<b>2nd Strike Transition:</b> {FormatPrettyFormula(t.swordsmanHit2DelayFormula, ColAgi, "AGI", ColAgi, "s", maxRanks: 1)} <color={ColMuted}>(then ÷ Effective AS; stays heavier than Hit 1)</color>");
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
                MetaRight = $"Cone {F(t.swordsmanQRange, "0.0")}u  •  Fixed Rec {F(t.swordsmanQRecover, "0.00")}s  •  CD {F(t.swordsmanQCooldown, "0.0")}/{F(t.swordsmanQCooldown - 0.5f, "0.0")}/{F(t.swordsmanQCooldown - 1.0f, "0.0")}s",
                Description = "Cleaves a forward arc, dealing physical dmg, slowing (attribute-independent), and marking targets for [R] (prioritizes heroes). AGI speeds wind-up only (recovery stays fixed).",
                MathLine = $"Dmg {FormatExactMath(t.swordsmanQDamageFormula, "AD")} | Windup {FormatExactMath(t.swordsmanQWindupFormula, "AGI", "s", maxRanks: 1)} (Fixed Rec {F(t.swordsmanQRecover, "0.00")}s)",
            };
            q.StatLines.Add($"<b>Physical Damage:</b> {FormatPrettyFormula(t.swordsmanQDamageFormula, ColPhys, "AD", ColAd)}");
            q.StatLines.Add($"<b>Wind-up & Slow:</b> {FormatPrettyFormula(t.swordsmanQWindupFormula, ColAgi, "AGI", ColAgi, "s", maxRanks: 1)}  •  <color={ColCc}>{F(t.swordsmanQSlow * 100f)}/{F((t.swordsmanQSlow + 0.05f) * 100f)}/{F((t.swordsmanQSlow + 0.10f) * 100f)}% slow ({F(t.swordsmanQSlowDuration, "0.0")}s)</color>");
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
                Description = "1st light blade steals defenses: grants self Arm/MR stacks (+8/+10/+12) and shreds enemy Arm/MR (-8/stack, can go negative!). Buff and debuff timers refresh separately & scale linearly with INT.",
                MathLine = $"Buff/Debuff Duration {FormatExactMath(t.swordsmanWBuffDurationFormula, "INT", "s", maxRanks: 1)} (separate timers; fixed per-stack Arm/MR)",
            };
            w.StatLines.Add($"<b>Resist per Stack (Fixed):</b> <color={ColAgi}>+8 / +10 / +12 Arm/MR self</color>  |  <color={ColStr}>-{F(t.swordsmanWStackShred)} enemy (down to -48)</color>");
            w.StatLines.Add($"<b>Buff & Debuff Durations:</b> {FormatPrettyFormula(t.swordsmanWBuffDurationFormula, ColInt, "INT", ColInt, "s", maxRanks: 1)}");
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
                Description = $"2nd heavy greatsword strike adds % Target Max HP magic dmg (STR bounded diminishing) and heals for {F(t.swordsmanEHealShare * 100f)}% × Heal Power. Interrupting Strike 2 denies healing!",
                MathLine = $"% Max HP {FormatExactMath(t.swordsmanEPercentHpFormula, "STR", "", asPercent: true)} | Heal = BonusDmg × {F(t.swordsmanEHealShare * 100f)}% × HealPower",
            };
            e.StatLines.Add($"<b>Bonus % Max HP Magic:</b> {FormatPrettyFormula(t.swordsmanEPercentHpFormula, ColMagic, "STR", ColStr, "", asPercent: true)}");
            e.StatLines.Add($"<b>Vampiric Heal:</b> <color={ColHeal}>{F(t.swordsmanEHealShare * 100f)}% of bonus dmg × Heal Power (Linear)</color>");
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
                Description = "Dash past your last basic/Q target (INT scales mark memory; AGI scales dash speed -> inverse travel time). Executes below STR-scaled threshold and resets CD on kill.",
                MathLine = $"Dmg {FormatExactMath(t.swordsmanRDamageFormula, "AD", maxRanks: 2)} | Exec {FormatExactMath(t.swordsmanRExecuteFormula, "STR", "", asPercent: true, maxRanks: 2)} | DashSpd {FormatExactMath(t.swordsmanRDashSpeedFormula, "AGI", "u/s", maxRanks: 2)} | Mem {FormatExactMath(t.swordsmanRLookbackFormula, "INT", "s", maxRanks: 2)}",
            };
            ult.StatLines.Add($"<b>Dash Dmg & Execute:</b> {FormatPrettyFormula(t.swordsmanRDamageFormula, ColPhys, "AD", ColAd, maxRanks: 2)}  •  Exec ≤ {FormatPrettyFormula(t.swordsmanRExecuteFormula, ColCc, "STR", ColStr, "", asPercent: true, maxRanks: 2)}");
            ult.StatLines.Add($"<b>Dash Speed & Memory:</b> {FormatPrettyFormula(t.swordsmanRDashSpeedFormula, ColAgi, "AGI", ColAgi, "u/s", maxRanks: 2)}  •  {FormatPrettyFormula(t.swordsmanRLookbackFormula, ColInt, "INT", ColInt, "s", maxRanks: 2)}");
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
            g.BaseStatsLine = $"HP <b>480</b> (+30/lvl)  •  <color={ColAd}>AD <b>46</b> (+3.5/lvl)</color>  •  Arm <b>8</b>  •  MR <b>10</b>  •  Regen <b>1.3/s</b>  •  Move <b>{F(t.laserSpeed, "0.00")}</b>  •  Turn <b>{F(t.laserTurnRate)}°/s</b>";
            if (live != null)
            {
                float stackScale = t.laserStackMaxAmpFormula.Evaluate(1, live.intelligence) * 100f;
                float qExtMult = t.laserQSpeedFormula.Evaluate(1, live.intelligence);
                float bounceR = t.laserChainRadiusFormula.Evaluate(1, live.intelligence);
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • EffAS {F(live.attackSpeed, "0.00")}x • AGI {F(live.agility)} • INT {F(live.intelligence)} • Chain {F(bounceR, "0.00")}u • StackCap +{F(stackScale, "0.0")}% • QSpd {F(qExtMult, "0.00")}x</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Chain Laser & Amplifier Stacks",
                MetaRight = $"Range {F(t.laserBasicRange, "0.0")}u  •  Unlimited Hops",
                Description = "Fire lasers chaining endlessly to unhit enemies within INT-scaled hop range (never repeats a target; fixed 70% hop decay). Every hit applies a Laser Stack amplifying future laser damage (asymptote scales w/ INT).",
                MathLine = $"ChainRad {FormatExactMath(t.laserChainRadiusFormula, "INT", "u", maxRanks: 1)} | StackAmp: 1+MaxAmp×s/(s+{F(t.laserStackHalfPoint)}); MaxAmp {FormatExactMath(t.laserStackMaxAmpFormula, "INT", "", asPercent: true, maxRanks: 1)}",
            };
            passive.StatLines.Add($"<b>Attack Freq & Bounce Dist:</b> <color={ColPhys}>100% AD</color> (Global Unbounded AS)  •  Hop {FormatPrettyFormula(t.laserChainRadiusFormula, ColInt, "INT", ColInt, "u", maxRanks: 1)}");
            passive.StatLines.Add($"<b>Stack Asymptote ({F(t.laserStackDuration, "0.0")}s):</b> {FormatPrettyFormula(t.laserStackMaxAmpFormula, ColInt, "INT", ColInt, "", asPercent: true, maxRanks: 1)}");
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
                MetaRight = $"Fixed {F(t.laserQStartRange, "0.0")}→{F(t.laserQMaxRange, "0.0")}u  •  Base {F(t.laserQDuration, "0.00")}s  •  CD {F(t.laserQCooldown, "0.0")}/{F(t.laserQCooldown - 0.7f, "0.0")}/{F(t.laserQCooldown - 1.4f, "0.0")}s",
                Description = $"Fires a fixed-range extending laser beam hitting each unit once (+{F(t.laserQMultiKillBonusGold)}g escalating per kill after 1st). Intelligence accelerates extension speed (unbounded sqrt → inverse duration).",
                MathLine = $"Dmg {FormatExactMath(t.laserQDamageFormula, "AD")} | Speed {FormatExactMath(t.laserQSpeedFormula, "INT", "x", maxRanks: 1)}",
            };
            q.StatLines.Add($"<b>Physical Damage:</b> {FormatPrettyFormula(t.laserQDamageFormula, ColPhys, "AD", ColAd)}");
            q.StatLines.Add($"<b>Extension Speed:</b> {FormatPrettyFormula(t.laserQSpeedFormula, ColInt, "INT", ColInt, "x", maxRanks: 1)} <color={ColMuted}>(replaces AGI)</color>");
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
                MetaRight = $"Cast {F(t.laserWCastRange, "0.0")}u  •  BaseSlow {F(t.laserWSlowBase * 100f)}/{F((t.laserWSlowBase + t.laserWSlowPerRank) * 100f)}/{F((t.laserWSlowBase + 2f * t.laserWSlowPerRank) * 100f)}%  •  CD {F(t.laserWCooldown, "0.0")}/{F(t.laserWCooldown - 0.7f, "0.0")}/{F(t.laserWCooldown - 1.4f, "0.0")}s",
                Description = "Every stacked enemy blasts a shockwave (dmg & shock radius scale w/ INT; single slow depends on rank only). Overlapping & refreshed slows stack via bounded slow strength (<82%) + unbounded sqrt duration!",
                MathLine = $"Dmg {FormatExactMath(t.laserWDamageFormula, "INT")} | Rad {FormatExactMath(t.laserWShockRadiusFormula, "INT", "u")} | OverlapSlow bounded→{F(t.laserWSlowMaxCap * 100f)}%; Dur={F(t.laserWSlowDuration, "0.00")}s×sqrt(n)",
            };
            w.StatLines.Add($"<b>Magic Damage & Radius:</b> {FormatPrettyFormula(t.laserWDamageFormula, ColMagic, "INT", ColInt)}  •  {FormatPrettyFormula(t.laserWShockRadiusFormula, ColInt, "INT", ColInt, "u")}");
            w.StatLines.Add($"<b>Overlapping/Refreshed Slow:</b> <color={ColCc}>Base 20/24/28% → Bounded cap 82% • Duration {F(t.laserWSlowDuration, "0.00")}s × sqrt(shocks) (Unbounded, anti-bypass)</color>");
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
                MetaRight = $"Step {F(t.laserEStepBack, "0.00")}u  •  Decoy {F(t.laserEDecoyDuration, "0.0")}s  •  CD {F(t.laserECooldown, "0.0")}/{F(t.laserECooldown - 0.9f, "0.0")}/{F(t.laserECooldown - 1.8f, "0.0")}s",
                Description = "Calmly animate a fixed step back (AGI reduces step duration cleanly without teleporting), spawning the blocking hologram at your previous spot upon completion. Retaliates w/ INT zap + rank-only stun.",
                MathLine = $"StepDur {FormatExactMath(t.laserEStepDurationFormula, "AGI", "s")} | Zap {FormatExactMath(t.laserEDecoyDamageFormula, "INT")} | Stun 0.65/0.80/0.95s (rank-only)",
            };
            e.StatLines.Add($"<b>Step-Back & Zap:</b> {FormatPrettyFormula(t.laserEStepDurationFormula, ColAgi, "AGI", ColAgi, "s")}  •  {FormatPrettyFormula(t.laserEDecoyDamageFormula, ColMagic, "INT", ColInt)}");
            e.StatLines.Add($"<b>Retaliation Stun:</b> <color={ColCc}>0.65 / 0.80 / 0.95s (rank-only • no attribute scaling)</color>");
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
                Description = $"After {F(t.laserRWindup, "0.00")}s wind-up, fire a continuous beam (INT bounded duration). Every target inside triggers independent Chain Laser procs (interval = {F(t.laserRProcInterval, "0.00")}s ÷ Effective AS) while you move at up to <100% MS (AGI).",
                MathLine = $"Proc = 62% AD every {F(t.laserRProcInterval, "0.00")}s / EffAS | Dur {FormatExactMath(t.laserRDurationFormula, "INT", "s", maxRanks: 2)} | Move {FormatExactMath(t.laserRMoveRatioFormula, "AGI", "x", maxRanks: 2)}",
            };
            ult.StatLines.Add($"<b>Chain Procs & Dur:</b> <color={ColPhys}>62% AD / proc</color> (Global Unbounded AS)  •  Dur {FormatPrettyFormula(t.laserRDurationFormula, ColInt, "INT", ColInt, "s", maxRanks: 2)}");
            ult.StatLines.Add($"<b>Move Speed Retained:</b> {FormatPrettyFormula(t.laserRMoveRatioFormula, ColAgi, "AGI", ColAgi, "x", maxRanks: 2)} <color={ColMuted}>(approaches 1.00x max)</color>");
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
