using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Data-driven MOBA ability/stat guide formatter producing both pretty color-coded
    /// ability cards (for Lobby, [C] Scaling sheet, and HUD hover tooltips) and exact formulas.
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

            if (unit == "x" && f.curve == ScalingCurve.Linear)
            {
                float ratePct = f.coeffOrMaxBonus * 100f;
                float s25 = f.Evaluate(1, 25f);
                float s50 = f.Evaluate(1, 50f);
                sb.Append($" (+{F(ratePct, "0.#")}% / {statName} • {F(s25, "0.00")}x @ 25, {F(s50, "0.00")}x @ 50)");
                return sb.ToString();
            }

            if (f.curve == ScalingCurve.Linear)
            {
                if (Mathf.Abs(f.coeffOrMaxBonus) > 0.0001f)
                {
                    if (!asPercent && string.IsNullOrEmpty(unit))
                    {
                        sb.Append($" + {F(f.coeffOrMaxBonus * 100f)}% {statName}");
                    }
                    else if (asPercent)
                    {
                        sb.Append($" + {F(f.coeffOrMaxBonus * 100f, "0.#")}% per {statName}");
                    }
                    else
                    {
                        sb.Append($" + {F(f.coeffOrMaxBonus, "0.##")}{unit} per {statName}");
                    }
                }
            }
            else
            {
                string rateStr = FormatBonusRatePer10(f, statName, unit, asPercent);
                string b25Str = FormatBonusValue(f.EvaluateBonusAt(25f), unit, asPercent);
                string b50Str = FormatBonusValue(f.EvaluateBonusAt(50f), unit, asPercent);
                sb.Append($" (+{rateStr}, diminishing • +{b25Str} @ 25, +{b50Str} @ 50)");
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
            if (unit == "x" && f.curve == ScalingCurve.Linear)
            {
                float ratePct = f.coeffOrMaxBonus * 100f;
                float s25 = f.Evaluate(1, 25f);
                float s50 = f.Evaluate(1, 50f);
                scaling = $"(+{F(ratePct, "0.#")}% / {statName} • {F(s25, "0.00")}x @ 25, {F(s50, "0.00")}x @ 50)";
                return $"<color={baseColorHex}><b>{F(f.baseValue, "0.00")}x</b></color> <color={statColorHex}>{scaling}</color>";
            }
            else if (f.curve == ScalingCurve.Linear)
            {
                if (!asPercent && string.IsNullOrEmpty(unit))
                {
                    scaling = $"(+{F(f.coeffOrMaxBonus * 100f)}% {statName})";
                }
                else if (asPercent)
                {
                    scaling = $"(+{F(f.coeffOrMaxBonus * 100f, "0.#")}% per {statName})";
                }
                else
                {
                    string b25 = FormatBonusValue(f.EvaluateBonusAt(25f), unit, false);
                    string b50 = FormatBonusValue(f.EvaluateBonusAt(50f), unit, false);
                    scaling = $"(+{F(f.coeffOrMaxBonus, "0.##")}{unit} / {statName} • +{b25} @ 25, +{b50} @ 50)";
                }
            }
            else
            {
                string rateStr = FormatBonusRatePer10(f, statName, unit, asPercent);
                string b25Str = FormatBonusValue(f.EvaluateBonusAt(25f), unit, asPercent);
                string b50Str = FormatBonusValue(f.EvaluateBonusAt(50f), unit, asPercent);
                scaling = $"(+{rateStr}, diminishing • +{b25Str} @ 25, +{b50Str} @ 50)";
            }

            return $"<color={baseColorHex}><b>{baseSb}</b></color> <color={statColorHex}>{scaling}</color>";
        }

        public static string FormatExactMath(
            StatFormula f,
            string statName,
            string unit = "",
            bool asPercent = false,
            int maxRanks = 3)
        {
            if (unit == "x" && f.curve == ScalingCurve.Linear)
            {
                float pctPerStat = f.coeffOrMaxBonus * 100f;
                return $"Formula: Speed = 1.00 + {F(f.coeffOrMaxBonus, "0.##")} × {statName} (+{F(pctPerStat, "0.#")}% / {statName}); Wind-up = Base × 100 / (100 + {F(pctPerStat, "0.#")} × {statName}) [no hard cap]";
            }

            var baseSb = new StringBuilder();
            AppendRankProgression(baseSb, f, unit, asPercent, maxRanks);
            if (f.curve == ScalingCurve.Linear)
            {
                string mult = asPercent ? $"{F(f.coeffOrMaxBonus * 100f, "0.#")}%" : F(f.coeffOrMaxBonus, "0.##");
                return $"Formula: [{baseSb}] + {mult} × {statName} (no hard cap)";
            }

            string bonusStr = asPercent ? $"{F(f.coeffOrMaxBonus * 100f, "0.#")}%" : $"{F(f.coeffOrMaxBonus, "0.##")}{unit}";
            return $"Formula: [{baseSb}] + {bonusStr} × {statName} / ({statName} + {F(f.halfPoint)}) (hyperbolic diminishing, approaches +{bonusStr} — no hard cap)";
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

            g.RuleLines.Add($"<color={ColInt}><b>Zero Free Attribute Stats:</b></color> <color={ColStr}>STR</color>, <color={ColAgi}>AGI</color>, and <color={ColInt}>INT</color> start at <b>0</b> and only empower abilities or items that explicitly scale with them.");
            g.RuleLines.Add($"<color={ColAgi}><b>The Coward's Shield (450g):</b></color> +15 Arm/MR, +12 AGI • Retreating from an enemy hero who hit you within {F(t.cowardShieldMemorySeconds, "0.0")}s grants {FormatPrettyFormula(t.cowardShieldResistFormula, ColCc, "AGI", ColAgi, " Arm/MR", maxRanks: 1)}.");
            g.RuleLines.Add($"<color={ColMuted}><b>No Hard Caps — Hyperbolic Diminishing Curves:</b></color> Armor & MR use 100/(100+R) above 0 (negative amplifies 2 - 100/(100-R)); Ability Haste uses Base × 100/(100+AH); Attack/Wind-up Speeds use Base / Speed; and stun/knockback/threshold scalars follow M × Stat / (Stat + H) so stacking stays fair without dead walls.");
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
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • AS {F(live.attackSpeed, "0.00")} • STR {F(live.strength)} • AGI {F(live.agility)} • INT {F(live.intelligence)} • AH {F(live.abilityHaste)} (-{F(live.cooldownReduction * 100f)}% CD) • Windup Speed {F(live.ogreAnimSpeed, "0.00")}x</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Heavy Swing & Multi-Stat Scaling",
                MetaRight = t.ogreImmuneDuringRecovery ? "CC-Immune: Cast + Recovery" : "CC-Immune: Cast Wind-up",
                Description = "No basic attacks — every swing is an ability. Cannot be stunned or displaced while winding up spells.",
                MathLine = $"Formula: Speed = 1.00 + {F(t.ogreAnimSpeedFormula.coeffOrMaxBonus, "0.###")} × AGI + {F(t.ogreAttackSpeedAnimRatio, "0.##")} × (AS - 1.00); Wind-up = Base / Speed [no hard cap]",
            };
            passive.StatLines.Add($"<color={ColAd}>Attack Damage:</color> Increases all club damage  |  <color={ColStr}>STR:</color> Longer stuns & knockback");
            passive.StatLines.Add($"<b><color={ColAgi}>Animation Speed:</color></b> {FormatPrettyFormula(t.ogreAnimSpeedFormula, ColAgi, "AGI", ColAgi, "x", maxRanks: 1)} <color={ColAgi}>+ {F(t.ogreAttackSpeedAnimRatio * 100f)}% of bonus Attack Speed</color> <color={ColMuted}>(e.g. +30% AS → +{F(t.ogreAttackSpeedAnimRatio * 30f)}% speed)</color>");
            if (live != null) passive.LiveLine = $"<color={ColLive}>Now: Wind-ups & recovery run at {F(live.ogreAnimSpeed, "0.00")}x speed</color>";
            g.Cards.Add(passive);

            var q = new AbilityCardData
            {
                Slot = LaneAbilitySlot.Q,
                KeyBadge = "Q",
                Name = "Club Slam",
                MetaRight = $"Beam {F(t.ogreQRange, "0.0")}u × {F(t.ogreQWidth, "0.00")}u  •  Wind-up {F(t.ogreQWindup, "0.00")}s  •  CD {F(t.ogreQCooldown, "0.0")}/{F(t.ogreQCooldown - 0.6f, "0.0")}/{F(t.ogreQCooldown - 1.2f, "0.0")}s",
                Description = "Heft the club overhead and slam a line forward, dealing heavy physical damage and stunning targets hit.",
                MathLine = $"{FormatExactMath(t.ogreQDamageFormula, "AD")}  |  Stun {FormatExactMath(t.ogreQStunFormula, "STR", "s")}",
            };
            q.StatLines.Add($"<b>Physical Damage:</b> {FormatPrettyFormula(t.ogreQDamageFormula, ColPhys, "AD", ColAd)}");
            q.StatLines.Add($"<b>Stun Duration:</b> {FormatPrettyFormula(t.ogreQStunFormula, ColCc, "STR", ColStr, "s")}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankQ);
                float anim = Mathf.Max(0.4f, live.ogreAnimSpeed);
                q.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankQ)}): {F(t.ogreQDamageFormula.Evaluate(r, live.attackDamage))} dmg • {F(t.ogreQStunFormula.Evaluate(r, live.strength), "0.00")}s stun • {F(t.ogreQWindup / anim, "0.00")}s windup • {F(LiveCd(live, t.ogreQCooldown - 0.6f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(q);

            var w = new AbilityCardData
            {
                Slot = LaneAbilitySlot.W,
                KeyBadge = "W",
                Name = "Club Sweep",
                MetaRight = $"Cone {F(t.ogreWRange, "0.0")}u  •  Wind-up {F(t.ogreWWindup, "0.00")}s  •  CD {F(t.ogreWCooldown, "0.0")}/{F(t.ogreWCooldown - 0.6f, "0.0")}/{F(t.ogreWCooldown - 1.2f, "0.0")}s",
                Description = "Sweep horizontally in a wide arc, knocking enemies away cleanly (stops at tree trunks).",
                MathLine = $"{FormatExactMath(t.ogreWDamageFormula, "AD")}  |  Push {FormatExactMath(t.ogreWKnockbackFormula, "STR", "u")}",
            };
            w.StatLines.Add($"<b>Physical Damage:</b> {FormatPrettyFormula(t.ogreWDamageFormula, ColPhys, "AD", ColAd)}");
            w.StatLines.Add($"<b>Knockback Distance:</b> {FormatPrettyFormula(t.ogreWKnockbackFormula, ColCc, "STR", ColStr, "u")}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankW);
                float anim = Mathf.Max(0.4f, live.ogreAnimSpeed);
                w.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankW)}): {F(t.ogreWDamageFormula.Evaluate(r, live.attackDamage))} dmg • {F(t.ogreWKnockbackFormula.Evaluate(r, live.strength), "0.00")}u push • {F(t.ogreWWindup / anim, "0.00")}s windup • {F(LiveCd(live, t.ogreWCooldown - 0.6f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(w);

            var e = new AbilityCardData
            {
                Slot = LaneAbilitySlot.E,
                KeyBadge = "E",
                Name = "Quake Stomp",
                MetaRight = $"AoE {F(t.ogreERadius, "0.00")}u  •  Wind-up {F(t.ogreEWindup, "0.00")}s  •  CD {F(t.ogreECooldown, "0.0")}/{F(t.ogreECooldown - 0.8f, "0.0")}/{F(t.ogreECooldown - 1.6f, "0.0")}s",
                Description = "Leap-stomp the ground beneath Ogre, smashing and stunning every enemy nearby.",
                MathLine = $"{FormatExactMath(t.ogreEDamageFormula, "AD")}  |  Stun {FormatExactMath(t.ogreEStunFormula, "STR", "s")}",
            };
            e.StatLines.Add($"<b>Physical Damage:</b> {FormatPrettyFormula(t.ogreEDamageFormula, ColPhys, "AD", ColAd)}");
            e.StatLines.Add($"<b>Stun Duration:</b> {FormatPrettyFormula(t.ogreEStunFormula, ColCc, "STR", ColStr, "s")}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankE);
                float anim = Mathf.Max(0.4f, live.ogreAnimSpeed);
                e.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankE)}): {F(t.ogreEDamageFormula.Evaluate(r, live.attackDamage))} dmg • {F(t.ogreEStunFormula.Evaluate(r, live.strength), "0.00")}s stun • {F(t.ogreEWindup / anim, "0.00")}s windup • {F(LiveCd(live, t.ogreECooldown - 0.8f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(e);

            var ult = new AbilityCardData
            {
                Slot = LaneAbilitySlot.R,
                KeyBadge = "R",
                Name = "Rampage Combo",
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Channel {F(t.ogreRChannelDuration, "0.0")}s  •  CD {F(t.ogreRCooldown)}/{F(t.ogreRCooldown - 4f)}s",
                Description = $"Brace ({F(t.ogreRDamageReduction * 100f)}% dmg reduction, {F(t.ogreRScaleBonus, "0.00")}x size) and queue up to {t.ogreRMaxStrikes} aimed Q/W strikes, then unleash lunging hits ending in a massive crit.",
                MathLine = $"Combo lunges {F(t.ogreRLungeDistance, "0.00")}u/hit • Wind-ups divided by AGI & AS AnimSpeed ({F(t.ogreRFastWindup, "0.00")}s / {F(t.ogreRFinalWindup, "0.00")}s) + {F(t.ogreRStrikeFollowthrough, "0.00")}s beat",
            };
            ult.StatLines.Add($"<b>Combo Strikes:</b> <color={ColPhys}>0.82x</color> Q/W dmg ({F(t.ogreRFastWindup, "0.00")}s wind-up)  →  Final hit <color={ColCc}><b>{F(t.ogreRFinalCritMultiplier, "0.00")}x CRIT</b></color> ({F(t.ogreRFinalWindup, "0.00")}s wind-up)");
            ult.StatLines.Add($"<b>Scaling:</b> <color={ColAd}>AD</color> strike dmg  •  <color={ColStr}>STR</color> stun/knockback  •  <color={ColAgi}>AGI & AS</color> combo swing speed");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankR);
                float anim = Mathf.Max(0.4f, live.ogreAnimSpeed);
                ult.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankR)}): fast {F(t.ogreRFastWindup / anim, "0.00")}s / final {F(t.ogreRFinalWindup / anim, "0.00")}s windup • {F(LiveCd(live, t.ogreRCooldown - 4f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(ult);
        }

        private static void BuildMiravelGuide(HeroGuideData g, LaneTuning t, HeroSnap live)
        {
            g.BaseStatsLine = $"HP <b>530</b> (+34/lvl)  •  <color={ColAd}>AD <b>48</b> (+3.5/lvl)</color>  •  Arm <b>8</b>  •  MR <b>18</b>  •  Regen <b>1.4/s</b>  •  Move <b>{F(t.luisSpeed, "0.00")}</b>  •  Turn <b>{F(t.luisTurnRate)}°/s</b>";
            if (live != null)
            {
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • INT {F(live.intelligence)} • AH {F(live.abilityHaste)} (-{F(live.cooldownReduction * 100f)}% CD) • HealPower {F(live.healPower * 100f)}% • AS {F(live.attackSpeed, "0.00")}</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Life/Drain Aura & Takedowns",
                MetaRight = $"Aura {F(t.luisAuraRadius, "0.0")}u  •  Ranged Staff {F(t.luisBasicRange, "0.0")}u",
                Description = "Fires 100% AD ranged bolts, boosts nearby allied regen, drains enemy regen (purple ring on victims), and permanently gains Heal Power on hero takedowns.",
                MathLine = $"Ally Aura = ({F(t.luisAllyAuraRegenBase, "0.0")} + 0.35×Lvl + AuraItems) × HealPower (base 100%) | Enemy Drain = -{F(t.luisEnemyAuraRegenPenaltyBase, "0.0")} - 0.35×Lvl",
            };
            passive.StatLines.Add($"<b>Ally Regen / Enemy Drain:</b> <color={ColHeal}>+{F(t.luisAllyAuraRegenBase, "0.0")} (+0.35/lvl) × HealPower (starts at 100%) HP/s</color>  |  <color={ColMagic}>-{F(t.luisEnemyAuraRegenPenaltyBase, "0.0")} (-0.35/lvl) HP/s</color>");
            passive.StatLines.Add($"<b>Takedowns & Basic:</b> <color={ColHeal}>+{F(t.luisTakedownHealPowerGain * 100f)}% permanent Heal Power</color>  |  <color={ColAd}>100% AD</color> bolt ({F(t.luisBasicWindup, "0.00")}s wind-up, AS scaled)");
            if (live != null)
            {
                float itemAura = SumAlliedAuraRegen(live);
                float allyHpS = (t.luisAllyAuraRegenBase + 0.35f * live.level + itemAura) * live.healPower;
                float enemyPen = t.luisEnemyAuraRegenPenaltyBase + 0.35f * live.level;
                passive.LiveLine = $"<color={ColLive}>Now: Ally Aura +{F(allyHpS, "0.0")}/s • Enemy Drain -{F(enemyPen, "0.0")}/s • HealPower {F(live.healPower * 100f)}%</color>";
            }
            g.Cards.Add(passive);

            var q = new AbilityCardData
            {
                Slot = LaneAbilitySlot.Q,
                KeyBadge = "Q (PASSIVE)",
                Name = "Sanctuary Orbs",
                MetaRight = $"Spawn {F(t.luisOrbInterval, "0.0")}/{F(t.luisOrbInterval - 0.8f, "0.0")}/{F(t.luisOrbInterval - 1.6f, "0.0")}s  •  Lasts {F(t.luisOrbDuration)}s  •  Pulse {F(t.luisOrbPulseRadius, "0.0")}u",
                Description = "Periodically drops a glowing orb into the lane. Stepping on it heals allies & blasts enemies; enemies can step on it first to deny it.",
                MathLine = $"Heal {FormatExactMath(t.luisOrbHealFormula, "INT")} × HealPower (base 100%)  |  Dmg {FormatExactMath(t.luisOrbDamageFormula, "INT")}",
            };
            q.StatLines.Add($"<b>Ally Heal:</b> {FormatPrettyFormula(t.luisOrbHealFormula, ColHeal, "INT", ColInt)} <color={ColHeal}>× HealPower (base 100%)</color>");
            q.StatLines.Add($"<b>Enemy Magic Pulse:</b> {FormatPrettyFormula(t.luisOrbDamageFormula, ColMagic, "INT", ColInt)}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankQ);
                float heal = t.luisOrbHealFormula.Evaluate(r, live.intelligence) * live.healPower;
                float dmg = t.luisOrbDamageFormula.Evaluate(r, live.intelligence);
                q.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankQ)}): {F(heal)} heal • {F(dmg)} magic pulse</color>";
            }
            g.Cards.Add(q);

            var w = new AbilityCardData
            {
                Slot = LaneAbilitySlot.W,
                KeyBadge = "W",
                Name = "Shimmer Decoy",
                MetaRight = $"CD {F(t.luisWCooldown, "0.0")}/{F(t.luisWCooldown - 1.1f, "0.0")}/{F(t.luisWCooldown - 2.2f, "0.0")}s  •  Invis {F(t.luisWDuration, "0.0")}/{F(t.luisWDuration + 0.4f, "0.0")}/{F(t.luisWDuration + 0.8f, "0.0")}s",
                Description = $"Turn invisible (breaks on basic attack or R; E Ward stays stealthed!) and send a {F(t.luisWIllusionDuration, "0.0")}s decoy walking to your cursor position (stops on arrival, triggers E Curse if hit).",
            };
            w.StatLines.Add($"<b>Stealth & Illusion:</b> <color={ColInt}>{F(t.luisWDuration, "0.0")} / {F(t.luisWDuration + 0.4f, "0.0")} / {F(t.luisWDuration + 0.8f, "0.0")}s</color> invisibility • Decoy walks to cast cursor & stops (no max distance)");
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
                Description = "Shield yourself and decoys with a Ward (does NOT break W stealth). Any enemy hero hitting you or a decoy is Cursed; casting abilities escalates Curse stacks (max 5).",
                MathLine = $"1-stack DPS {FormatExactMath(t.luisCurseDpsFormula, "INT")}; each stack adds +35% DPS & +{F(t.luisECurseBonusDuration, "0.0")}s",
            };
            e.StatLines.Add($"<b>1-Stack Magic DPS:</b> {FormatPrettyFormula(t.luisCurseDpsFormula, ColMagic, "INT", ColInt)} <color={ColMuted}>(×2.40 at 5 stacks)</color>");
            e.StatLines.Add($"<b>Curse Duration:</b> <color={ColCc}>{F(t.luisECurseBaseDuration, "0.0")}/{F(t.luisECurseBaseDuration + 0.4f, "0.0")}/{F(t.luisECurseBaseDuration + 0.8f, "0.0")}s</color> + {F(t.luisECurseBonusDuration, "0.0")}s per enemy cast");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankE);
                float dps1 = t.luisCurseDpsFormula.Evaluate(r, live.intelligence);
                e.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankE)}): {F(dps1, "0.0")} DPS (1-stack) → {F(dps1 * 2.40f, "0.0")} DPS (5-stack) • {F(LiveCd(live, t.luisECooldown - 0.8f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(e);

            var ult = new AbilityCardData
            {
                Slot = LaneAbilitySlot.R,
                KeyBadge = "R",
                Name = "Ramping Sanctum",
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Range {F(t.luisRRange, "0.0")}u / Radius {F(t.luisRRadius, "0.0")}u  •  CD {F(t.luisRCooldown)}/{F(t.luisRCooldown - 3f)}s",
                Description = $"Channel for {F(t.luisRChannelDuration, "0.0")}s: zone ramps 0→100% power, slowing & amplifying damage to enemies while boosting ally healing.",
                MathLine = FormatExactMath(t.luisRVulnFormula, "INT", "", asPercent: true, maxRanks: 2),
            };
            ult.StatLines.Add($"<b>Enemy Vuln (max):</b> {FormatPrettyFormula(t.luisRVulnFormula, ColCc, "INT", ColInt, "", asPercent: true, maxRanks: 2)}");
            ult.StatLines.Add($"<b>Slow & Ally Heal Boost:</b> <color={ColCc}>{F(t.luisRMaxSlow * 100f)}% slow</color>  •  <color={ColHeal}>+{F(t.luisRMaxHealAmp * 100f)}% healing received</color>");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankR);
                float maxVuln = t.luisRVulnFormula.Evaluate(r, live.intelligence) * 100f;
                ult.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankR)}): max +{F(maxVuln, "0.0")}% enemy dmg taken • {F(LiveCd(live, t.luisRCooldown - 3f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(ult);
        }

        private static void BuildSwordsmanGuide(HeroGuideData g, LaneTuning t, HeroSnap live)
        {
            g.BaseStatsLine = $"HP <b>615</b> (+40/lvl)  •  <color={ColAd}>AD <b>54</b> (+4/lvl)</color>  •  Arm <b>14</b>  •  MR <b>14</b>  •  Regen <b>1.5/s</b>  •  Move <b>{F(t.swordsmanSpeed, "0.00")}</b>  •  Turn <b>{F(t.swordsmanTurnRate)}°/s</b>";
            if (live != null)
            {
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • STR {F(live.strength)} • AS {F(live.attackSpeed, "0.00")} • AH {F(live.abilityHaste)} (-{F(live.cooldownReduction * 100f)}% CD)</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Twin-Blade Technique",
                MetaRight = $"Melee {F(t.swordsmanBasicRange, "0.00")}u  •  AS Scaled",
                Description = $"Deliberate movement speed, but each basic attack delivers two rapid blades: Hit 1 procs [W], Hit 2 procs [E].",
                MathLine = $"Hit 1 windup {F(t.swordsmanHit1Windup, "0.00")}s + Hit 2 delay {F(t.swordsmanHit2Delay, "0.00")}s + Recover {F(t.swordsmanBasicRecover, "0.00")}s (divided by Attack Speed)",
            };
            passive.StatLines.Add($"<b>Twin Strikes:</b> <color={ColPhys}>{F(t.swordsmanStrikeAdRatio * 100f)}% AD × 2</color> <color={ColAd}>(= {F(t.swordsmanStrikeAdRatio * 200f)}% total AD physical dmg)</color>");
            if (live != null)
            {
                float strike = live.attackDamage * t.swordsmanStrikeAdRatio;
                passive.LiveLine = $"<color={ColLive}>Now: {F(strike, "0.0")} × 2 = {F(strike * 2f, "0.0")} physical damage per Twin-Blade</color>";
            }
            g.Cards.Add(passive);

            var q = new AbilityCardData
            {
                Slot = LaneAbilitySlot.Q,
                KeyBadge = "Q",
                Name = "Hamstring Arc",
                MetaRight = $"Cone {F(t.swordsmanQRange, "0.0")}u  •  Wind-up {F(t.swordsmanQWindup, "0.00")}s  •  CD {F(t.swordsmanQCooldown, "0.0")}/{F(t.swordsmanQCooldown - 0.5f, "0.0")}/{F(t.swordsmanQCooldown - 1.0f, "0.0")}s",
                Description = "Cleaves a forward arc with both blades, damaging, heavily slowing, and marking enemies for [R] (prioritizes heroes).",
                MathLine = FormatExactMath(t.swordsmanQDamageFormula, "AD"),
            };
            q.StatLines.Add($"<b>Physical Damage:</b> {FormatPrettyFormula(t.swordsmanQDamageFormula, ColPhys, "AD", ColAd)}");
            q.StatLines.Add($"<b>Slow:</b> <color={ColCc}>{F(t.swordsmanQSlow * 100f)}% / {F((t.swordsmanQSlow + 0.05f) * 100f)}% / {F((t.swordsmanQSlow + 0.10f) * 100f)}%</color> for {F(t.swordsmanQSlowDuration, "0.0")}s");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankQ);
                float dmg = t.swordsmanQDamageFormula.Evaluate(r, live.attackDamage);
                float slow = (t.swordsmanQSlow + 0.05f * (r - 1)) * 100f;
                q.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankQ)}): {F(dmg)} dmg • {F(slow)}% slow • {F(LiveCd(live, t.swordsmanQCooldown - 0.5f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(q);

            var w = new AbilityCardData
            {
                Slot = LaneAbilitySlot.W,
                KeyBadge = "W (1ST HIT)",
                Name = "Vital Draw",
                MetaRight = "Passive • Procs on Twin-Strike 1",
                Description = $"First strike slices for % of target Max HP magic damage and heals you for {F(t.swordsmanWHealShare * 100f)}% of that bonus damage.",
                MathLine = FormatExactMath(t.swordsmanWPercentHpFormula, "STR", "", asPercent: true),
            };
            w.StatLines.Add($"<b>Bonus % Max HP Magic:</b> {FormatPrettyFormula(t.swordsmanWPercentHpFormula, ColMagic, "STR", ColStr, "", asPercent: true)} <color={ColHeal}>• Heals {F(t.swordsmanWHealShare * 100f)}% × HealPower</color>");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankW);
                float pct = t.swordsmanWPercentHpFormula.Evaluate(r, live.strength) * 100f;
                w.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankW)}): {F(pct, "0.0")}% target Max HP magic bonus • heals {F(t.swordsmanWHealShare * live.healPower * 100f)}% of bonus</color>";
            }
            g.Cards.Add(w);

            var e = new AbilityCardData
            {
                Slot = LaneAbilitySlot.E,
                KeyBadge = "E (2ND HIT)",
                Name = "Stance Temper",
                MetaRight = $"Passive • Procs on Twin-Strike 2  •  {F(t.swordsmanEStackDuration, "0.0")}s",
                Description = $"Second strike steals defenses: builds self Armor/MR stacks while shredding enemy Armor/MR (can push resistances negative!).",
                MathLine = $"Gain +(6 + 2×Rank) Arm/MR per stack (max {t.swordsmanEMaxStacks}); Shred -{F(t.swordsmanEStackShred)}/stack (max -{F(t.swordsmanEStackShred * t.swordsmanEMaxStacks)})",
            };
            e.StatLines.Add($"<b>Self Gain:</b> <color={ColAgi}>+8 / +10 / +12 Arm & MR</color> per stack <color={ColMuted}>(max {t.swordsmanEMaxStacks} = +48/+60/+72)</color>");
            e.StatLines.Add($"<b>Enemy Shred:</b> <color={ColStr}>-{F(t.swordsmanEStackShred)} Arm & MR</color> per stack <color={ColMuted}>(max -{F(t.swordsmanEStackShred * t.swordsmanEMaxStacks)} — amplifies damage past 0!)</color>");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankE);
                float perStack = 6f + 2f * r;
                e.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankE)}): {live.swordStacks}/{t.swordsmanEMaxStacks} stacks (+{F(live.swordResistDelta)} Arm/MR, +{F(perStack)}/stack)</color>";
            }
            g.Cards.Add(e);

            var ult = new AbilityCardData
            {
                Slot = LaneAbilitySlot.R,
                KeyBadge = "R",
                Name = "Finishing Cross",
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Range {F(t.swordsmanRRange, "0.0")}u  •  CD {F(t.swordsmanRCooldown)}/{F(t.swordsmanRCooldown - 4f)}s",
                Description = $"Dash cleanly past the last target you hit with a basic attack or [Q] within {F(t.swordsmanRLookbackSeconds)}s. Executes targets ≤ threshold and resets CD on kill.",
                MathLine = FormatExactMath(t.swordsmanRDamageFormula, "AD", maxRanks: 2),
            };
            ult.StatLines.Add($"<b>Physical Dash Damage:</b> {FormatPrettyFormula(t.swordsmanRDamageFormula, ColPhys, "AD", ColAd, maxRanks: 2)}");
            ult.StatLines.Add($"<b>Execute Threshold:</b> <color={ColCc}>≤ {F(t.swordsmanRExecuteThreshold * 100f)}% / {F((t.swordsmanRExecuteThreshold + 0.04f) * 100f)}% HP (true dmg + CD reset)</color>");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankR);
                float dmg = t.swordsmanRDamageFormula.Evaluate(r, live.attackDamage);
                float exec = (t.swordsmanRExecuteThreshold + 0.04f * (r - 1)) * 100f;
                ult.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankR)}): {F(dmg)} dmg • execute ≤ {F(exec)}% HP • {F(LiveCd(live, t.swordsmanRCooldown - 4f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(ult);
        }

        private static void BuildLaserShooterGuide(HeroGuideData g, LaneTuning t, HeroSnap live)
        {
            g.BaseStatsLine = $"HP <b>480</b> (+30/lvl)  •  <color={ColAd}>AD <b>46</b> (+3.5/lvl)</color>  •  Arm <b>8</b>  •  MR <b>10</b>  •  Regen <b>1.3/s</b>  •  Move <b>{F(t.laserSpeed, "0.00")}</b>  •  Turn <b>{F(t.laserTurnRate)}°/s</b>";
            if (live != null)
            {
                float stackScale = t.laserStackMaxAmpFormula.Evaluate(1, live.intelligence) * 100f;
                float qExtMult = Mathf.Max(1f, t.laserQSpeedFormula.Evaluate(1, live.agility));
                g.LiveStatsLine = $"<color={ColLive}>Live (Lvl {live.level}): AD {F(live.attackDamage)} • STR {F(live.strength)} • AGI {F(live.agility)} • INT {F(live.intelligence)} • AH {F(live.abilityHaste)} (-{F(live.cooldownReduction * 100f)}% CD) • Stack Amp Scale +{F(stackScale, "0.0")}% • Q Extend {F(qExtMult, "0.00")}x</color>";
            }

            var passive = new AbilityCardData
            {
                Slot = LaneAbilitySlot.None,
                KeyBadge = "PASSIVE",
                Name = "Chain Laser & Amplifier Stacks",
                MetaRight = $"Range {F(t.laserBasicRange, "0.0")}u  •  Chain {F(t.laserChainRadius, "0.0")}u (Unlimited Hops)",
                Description = "Basic attacks fire a laser that bounces endlessly to unhit enemies within chain range (never repeats a target). Every laser hit adds a Laser Damage stack amplifying your future laser damage against that target.",
                MathLine = $"Hop decay ×{F(t.laserChainDecay * 100f)}% (floor {F(t.laserChainMinRatio * 100f)}%) | Stack Amp: 1 + Asymptote × s / (s + {F(t.laserStackHalfPoint)}) | {FormatExactMath(t.laserStackMaxAmpFormula, "INT", "", asPercent: true, maxRanks: 1)}",
            };
            passive.StatLines.Add($"<b>Chain Bounces:</b> <color={ColPhys}>100% AD first hit</color>, <color={ColMuted}>×{F(t.laserChainDecay * 100f)}% per bounce (floor {F(t.laserChainMinRatio * 100f)}%)</color>");
            passive.StatLines.Add($"<b>Stack Asymptote ({F(t.laserStackDuration, "0.0")}s):</b> {FormatPrettyFormula(t.laserStackMaxAmpFormula, ColInt, "INT", ColInt, "", asPercent: true, maxRanks: 1)} <color={ColMuted}>(half-point {F(t.laserStackHalfPoint)} stacks)</color>");
            if (live != null)
            {
                float asym = t.laserStackMaxAmpFormula.Evaluate(1, live.intelligence) * 100f;
                passive.LiveLine = $"<color={ColLive}>Now: {F(live.attackDamage, "0.0")} first-hit dmg • +{F(asym * 0.5f, "0.0")}% amp @ {F(t.laserStackHalfPoint)} stacks (+{F(asym, "0.0")}% asymptote)</color>";
            }
            g.Cards.Add(passive);

            var q = new AbilityCardData
            {
                Slot = LaneAbilitySlot.Q,
                KeyBadge = "Q",
                Name = "Extending Line Beam",
                MetaRight = $"Range {F(t.laserQStartRange, "0.0")}→{F(t.laserQMaxRange, "0.0")}u  •  {F(t.laserQDuration, "0.00")}s  •  CD {F(t.laserQCooldown, "0.0")}/{F(t.laserQCooldown - 0.7f, "0.0")}/{F(t.laserQCooldown - 1.4f, "0.0")}s",
                Description = $"Fires a continuous extending laser beam{(t.laserQTracksCursor ? " (tracks cursor)" : " (locked line)")} hitting each target once. Killing 2+ units grants +{F(t.laserQMultiKillBonusGold)}g escalating bonus gold per kill after the 1st.",
                MathLine = $"{FormatExactMath(t.laserQDamageFormula, "AD")}  |  Extend {FormatExactMath(t.laserQSpeedFormula, "AGI", "x", maxRanks: 1)}",
            };
            q.StatLines.Add($"<b>Physical Damage:</b> {FormatPrettyFormula(t.laserQDamageFormula, ColPhys, "AD", ColAd)}");
            q.StatLines.Add($"<b>Extension Speed:</b> {FormatPrettyFormula(t.laserQSpeedFormula, ColAgi, "AGI", ColAgi, "x", maxRanks: 1)}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankQ);
                float dmg = t.laserQDamageFormula.Evaluate(r, live.attackDamage);
                float dur = t.laserQDuration / Mathf.Max(1f, t.laserQSpeedFormula.Evaluate(1, live.agility));
                q.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankQ)}): {F(dmg)} dmg • {F(dur, "0.00")}s beam • {F(LiveCd(live, t.laserQCooldown - 0.7f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(q);

            var w = new AbilityCardData
            {
                Slot = LaneAbilitySlot.W,
                KeyBadge = "W",
                Name = "Resonant Stack Shock",
                MetaRight = $"Cast {F(t.laserWCastRange, "0.0")}u  •  Shock {F(t.laserWShockRadius, "0.00")}u  •  CD {F(t.laserWCooldown, "0.0")}/{F(t.laserWCooldown - 0.7f, "0.0")}/{F(t.laserWCooldown - 1.4f, "0.0")}s",
                Description = $"Every stacked enemy in range emits an area shock (+12% dmg per stack above 1). Overlapping shocks stack both slow strength (diminishing) and slow duration (+{F(t.laserWSlowDuration, "0.0")}s each).",
                MathLine = $"{FormatExactMath(t.laserWDamageFormula, "INT")}  |  Slow/shock {FormatExactMath(t.laserWSlowFormula, "INT", "", asPercent: true)}",
            };
            w.StatLines.Add($"<b>Magic Damage/Shock:</b> {FormatPrettyFormula(t.laserWDamageFormula, ColMagic, "INT", ColInt)}");
            w.StatLines.Add($"<b>Overlapping Slow:</b> {FormatPrettyFormula(t.laserWSlowFormula, ColCc, "INT", ColInt, "", asPercent: true)} <color={ColCc}>(+{F(t.laserWSlowDuration, "0.0")}s duration per shock)</color>");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankW);
                float dmg = t.laserWDamageFormula.Evaluate(r, live.intelligence);
                float slowPct = t.laserWSlowFormula.Evaluate(r, live.intelligence) * 100f;
                w.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankW)}): {F(dmg, "0.0")} dmg/shock • {F(slowPct, "0.0")}% single slow • {F(LiveCd(live, t.laserWCooldown - 0.7f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(w);

            var e = new AbilityCardData
            {
                Slot = LaneAbilitySlot.E,
                KeyBadge = "E",
                Name = "Hologram Reposition",
                MetaRight = $"Step {F(t.laserEStepBack, "0.00")}u  •  Decoy {F(t.laserEDecoyDuration, "0.0")}s  •  CD {F(t.laserECooldown, "0.0")}/{F(t.laserECooldown - 0.9f, "0.0")}/{F(t.laserECooldown - 1.8f, "0.0")}s",
                Description = "Step back one pace and deploy a stationary laser decoy blocking enemy hero movement and projectiles (creeps/towers ignore it). Attackers take retaliation shock + stun; hero damage destroys the decoy.",
                MathLine = $"Zap {FormatExactMath(t.laserEDecoyDamageFormula, "INT")}  |  Stun {FormatExactMath(t.laserEStunFormula, "STR", "s")}",
            };
            e.StatLines.Add($"<b>Retaliation Magic Zap:</b> {FormatPrettyFormula(t.laserEDecoyDamageFormula, ColMagic, "INT", ColInt)}");
            e.StatLines.Add($"<b>Retaliation Stun:</b> {FormatPrettyFormula(t.laserEStunFormula, ColCc, "STR", ColStr, "s")}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankE);
                float zap = t.laserEDecoyDamageFormula.Evaluate(r, live.intelligence);
                float stun = t.laserEStunFormula.Evaluate(r, live.strength);
                e.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankE)}): {F(zap, "0.0")} zap dmg • {F(stun, "0.00")}s stun • {F(LiveCd(live, t.laserECooldown - 0.9f * (r - 1)), "0.0")}s CD</color>";
            }
            g.Cards.Add(e);

            var ult = new AbilityCardData
            {
                Slot = LaneAbilitySlot.R,
                KeyBadge = "R",
                Name = "Overdrive Prism Beam",
                MetaRight = $"Lvl {t.ultMinLevel}+  •  Beam {F(t.laserRRange, "0.0")}u × {F(t.laserRHalfWidth * 2f, "0.00")}u  •  {F(t.laserRDuration, "0.0")}s  •  CD {F(t.laserRCooldown)}/{F(t.laserRCooldown - 4f)}s",
                Description = $"After {F(t.laserRWindup, "0.00")}s wind-up, fire a continuous beam for {F(t.laserRDuration, "0.0")}s while moving freely. Every enemy inside receives chain laser basic procs every {F(t.laserRProcInterval, "0.00")}s.",
                MathLine = $"Proc = {F(t.laserRProcDamageScale * 100f)}% AD chain every {F(t.laserRProcInterval, "0.00")}s  |  Move {FormatExactMath(t.laserRMoveRatioFormula, "AGI", "x", maxRanks: 2)}",
            };
            ult.StatLines.Add($"<b>Beam Chain Procs:</b> <color={ColPhys}>{F(t.laserRProcDamageScale * 100f)}% AD</color> <color={ColMuted}>every {F(t.laserRProcInterval, "0.00")}s (adds stacks & bounces endlessly)</color>");
            ult.StatLines.Add($"<b>Move Speed While Firing:</b> {FormatPrettyFormula(t.laserRMoveRatioFormula, ColAgi, "AGI", ColAgi, "x", maxRanks: 2)}");
            if (live != null)
            {
                int r = Mathf.Max(1, live.rankR);
                float procDmg = live.attackDamage * t.laserRProcDamageScale;
                float movePct = t.laserRMoveRatioFormula.Evaluate(r, live.agility) * 100f;
                ult.LiveLine = $"<color={ColLive}>Now ({RankTag(live.rankR)}): {F(procDmg, "0.0")} proc dmg • {F(movePct, "0.0")}% move speed • {F(LiveCd(live, t.laserRCooldown - 4f * (r - 1)), "0.0")}s CD</color>";
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

        private static float SumAlliedAuraRegen(HeroSnap live)
        {
            if (live.items == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < live.items.Length; i++)
            {
                if (live.items[i] > 0 && LaneItems.TryGet(live.items[i], out LaneItemDef item))
                {
                    sum += item.AddAlliedAuraRegen;
                }
            }
            return sum;
        }
    }
}
