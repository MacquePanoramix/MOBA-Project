using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public enum LaneTeam : byte
    {
        Blue = 0,
        Red = 1,
    }

    public enum LaneHeroId : byte
    {
        Ogre = 0,
        Miravel = 1,
        Swordsman = 2,
        LaserShooter = 3,
    }

    public enum LaneOrderKind : byte
    {
        None = 0,
        Stop = 1,
        Move = 2,
        AttackMove = 3,
        AttackHero = 4,
        AttackMinion = 5,
        AttackTower = 6,
        CastQ = 7,
        CastW = 8,
        CastE = 9,
        CastR = 10,
        LevelAbility = 11,
        BuyItem = 12,
        SellItem = 13,
    }

    public enum LanePhase : byte
    {
        Idle = 0,
        Windup = 1,
        Recover = 2,
        Casting = 3,
        Channeling = 4,
        ComboSeq = 5,
        ExecuteDash = 6,
        Dead = 7,
    }

    public enum LaneAbilitySlot : byte
    {
        None = 0,
        Q = 1,
        W = 2,
        E = 3,
        R = 4,
    }

    public enum LaneProjKind : byte
    {
        LuisBolt = 0,
        MinionBolt = 1,
        TowerShell = 2,
    }

    public enum LaneEventKind : byte
    {
        None = 0,
        AttackMoveRing = 1,
        LuisBasicShot = 2,
        SwordsmanSlash = 3,
        OgreSlam = 4,
        OgreSweep = 5,
        OgreStomp = 6,
        OgreStrike = 7,
        LuisOrbSpawn = 8,
        LuisOrbPulse = 9,
        LuisOrbDeny = 10,
        LuisIllusionCast = 11,
        LuisCurseProc = 12,
        SwordsmanQSlash = 13,
        SwordsmanWHeal = 14,
        SwordsmanEProc = 15,
        SwordsmanRSlash = 16,
        TowerShot = 17,
        MinionShot = 18,
        LevelUp = 19,
        Purchase = 20,
        KillBanner = 21,
        CcBlocked = 22,
        TowerAggro = 23,
        LaserChain = 24,
        LaserBeam = 25,
        LaserShock = 26,
        LaserDecoy = 27,
        LaserGoldBonus = 28,
        OgreQShockwave = 29,
        InsufficientMana = 30,
        BananaCrescent = 31,
    }

    public enum HeroResourceKind : byte
    {
        None = 0,
        Mana = 1,
    }

    public enum ResourceCostMode : byte
    {
        None = 0,
        Flat = 1,
        PercentMax = 2,
        PercentCurrent = 3,
        ContinuousPerSecond = 4,
    }

    [Serializable]
    public struct AbilityResourceSpec
    {
        public HeroResourceKind ResourceKind;
        public ResourceCostMode Mode;
        public float BaseCost;
        public float PerRankCost;
        public bool UsesCostReduction;

        public AbilityResourceSpec(
            HeroResourceKind resourceKind,
            ResourceCostMode mode,
            float baseCost,
            float perRankCost = 0f,
            bool usesCostReduction = true)
        {
            ResourceKind = resourceKind;
            Mode = mode;
            BaseCost = baseCost;
            PerRankCost = perRankCost;
            UsesCostReduction = usesCostReduction;
        }

        public float EvaluateRawCost(int rank, float maxResource, float currentResource)
        {
            if (ResourceKind == HeroResourceKind.None || Mode == ResourceCostMode.None) return 0f;
            float ranked = Mathf.Max(0f, BaseCost + PerRankCost * Mathf.Max(0, rank - 1));
            switch (Mode)
            {
                case ResourceCostMode.Flat:
                case ResourceCostMode.ContinuousPerSecond:
                    return ranked;
                case ResourceCostMode.PercentMax:
                    return Mathf.Max(0f, maxResource) * ranked;
                case ResourceCostMode.PercentCurrent:
                    return Mathf.Max(0f, currentResource) * ranked;
                default:
                    return 0f;
            }
        }

        public float EvaluateEffectiveCost(int rank, float maxResource, float currentResource, float rcrFraction)
        {
            float raw = EvaluateRawCost(rank, maxResource, currentResource);
            if (raw <= 0f) return 0f;
            float mult = UsesCostReduction ? (1f - Mathf.Clamp(rcrFraction, 0f, 0.99f)) : 1f;
            return raw * mult;
        }
    }

    public enum LaneTargetKind : byte
    {
        None = 0,
        Hero = 1,
        Minion = 2,
        Tower = 3,
    }

    public enum ScalingCurve : byte
    {
        Linear = 0,
        Diminishing = 1,
        UnboundedSqrt = 2,
    }

    [Flags]
    public enum DamageFlags : byte
    {
        None = 0,
        BasicOrMinion = 1 << 0,
        OnHit = 1 << 1,
        DamageOverTime = 1 << 2,
        DeferredPayout = 1 << 3,
    }

    public enum HealSourceKind : byte
    {
        AbilityOrItem = 0,
        NaturalRegen = 1,
        Fountain = 2,
        LingeringRestoration = 3,
    }

    /// <summary>
    /// Configurable per-property scaling formula supporting linear growth, bounded hyperbolic
    /// diminishing returns, or unbounded square-root diminishing returns.
    /// Used independently by abilities and item passives on conventional stats or Str/Agi/Int.
    /// </summary>
    [Serializable]
    public struct StatFormula
    {
        public float baseValue;
        public float perRank;
        public ScalingCurve curve;
        public float coeffOrMaxBonus;
        public float halfPoint;
        public float minValue;
        public float maxValue;

        public StatFormula(
            float baseValue,
            float perRank,
            ScalingCurve curve,
            float coeffOrMaxBonus,
            float halfPoint = 0f,
            float minValue = 0f,
            float maxValue = 0f)
        {
            this.baseValue = baseValue;
            this.perRank = perRank;
            this.curve = curve;
            this.coeffOrMaxBonus = coeffOrMaxBonus;
            this.halfPoint = halfPoint;
            this.minValue = minValue;
            this.maxValue = maxValue;
        }

        public float EvaluateBonusAt(float statValue)
        {
            float s = Mathf.Max(0f, statValue);
            switch (curve)
            {
                case ScalingCurve.Linear:
                    return coeffOrMaxBonus * s;
                case ScalingCurve.UnboundedSqrt:
                    return (s > 0f && halfPoint > 0f)
                        ? coeffOrMaxBonus * (Mathf.Sqrt(1f + halfPoint * s) - 1f)
                        : 0f;
                default: // ScalingCurve.Diminishing (Bounded Hyperbolic)
                    return (s > 0f && halfPoint > 0f)
                        ? coeffOrMaxBonus * (s / (s + halfPoint))
                        : 0f;
            }
        }

        public float Evaluate(int rank, float statValue)
        {
            float baseTotal = baseValue + perRank * Mathf.Max(0, rank - 1);
            float result = baseTotal + EvaluateBonusAt(statValue);
            if (result < minValue) result = minValue;
            return result;
        }

        public string CurveLabel()
        {
            switch (curve)
            {
                case ScalingCurve.Linear: return "Linear";
                case ScalingCurve.UnboundedSqrt: return "Unbounded diminishing";
                default: return "Bounded diminishing";
            }
        }
    }

    public static class LaneNames
    {
        public static string ShortName(LaneHeroId hero)
        {
            switch (hero)
            {
                case LaneHeroId.Ogre: return "Ogre";
                case LaneHeroId.Miravel: return "Miravel";
                case LaneHeroId.Swordsman: return "Swordsman";
                case LaneHeroId.LaserShooter: return "Laser";
                default: return hero.ToString();
            }
        }

        public static string DisplayName(LaneHeroId hero)
        {
            switch (hero)
            {
                case LaneHeroId.Ogre: return "Ogre";
                case LaneHeroId.Miravel: return "Miravel (Hero Luís)";
                case LaneHeroId.Swordsman: return "Old Man Swordsman";
                case LaneHeroId.LaserShooter: return "Chain Laser Shooter";
                default: return hero.ToString();
            }
        }

        public static string RoleSummary(LaneHeroId hero)
        {
            switch (hero)
            {
                case LaneHeroId.Ogre:
                    return "No basics • CC-immune casts • AGI=Windup (sqrt), AS=Recovery • Q Dual-Zone Slam • W Sqrt Knockback • R Size+Combos";
                case LaneHeroId.Miravel:
                    return "Ranged staff • HealPower=Aura/Orbs/R Amp, INT=Radii/Curse • Negative-Regen Aura • W Decoy • R Sanctum";
                case LaneHeroId.Swordsman:
                    return "Twin-Blade (Light 1st->W Resist Stacks, Heavy 2nd->E %HP Heal) • Q Slow Arc • R Execute Dash";
                case LaneHeroId.LaserShooter:
                    return "Ranged Laser • Unlimited Bounces + Stacks • Q INT-Speed Beam • W Bounded/Sqrt Overlapping Slow • E Step-Back Decoy • R Moving Beam";
                default:
                    return string.Empty;
            }
        }
    }

    // Item definitions, catalog, and duplicate-aware recipe resolution live in LaneItems.cs.

    /// <summary>
    /// Editable prototype combat, wave, tower, and multi-dimensional scaling placeholders.
    /// Str/Agi/Int confer zero inherent bonuses; only properties referencing them via StatFormula scale.
    /// </summary>
    [Serializable]
    public sealed class LaneTuning
    {
        public float tickHz = 30f;

        // Lane geometry
        public float laneHalfLength = 24f;
        public float laneHalfWidth = 8.2f;
        public float fountainRadius = 3.2f;
        public Vector2 blueFountainPos = new Vector2(-21.2f, 0f);
        public Vector2 redFountainPos = new Vector2(21.2f, 0f);
        public Vector2 blueSpawnPos = new Vector2(-19.8f, 0f);
        public Vector2 redSpawnPos = new Vector2(19.8f, 0f);
        public Vector2 blueTowerPos = new Vector2(-11.2f, 0f);
        public Vector2 redTowerPos = new Vector2(11.2f, 0f);

        // Combat & Attack-Move ergonomics
        public float basicMotionBuffer = 1.15f;
        public float attackMoveScanRadius = 4.5f;

        // Waves, towers, leveling, shop
        public int maxLevel = 6;
        public int ultMinLevel = 4;
        public int startingGold = 500;
        public float passiveGoldPerSecond = 2.0f;
        public float waveFirstSpawnDelay = 2.5f;
        public float waveInterval = 26f;
        public int meleeCreepsPerWave = 3;
        public int rangedCreepsPerWave = 1;
        public float minionMaxHpMelee = 240f;
        public float minionMaxHpRanged = 155f;
        public float minionSpeed = 2.6f;
        public float minionMeleeDamage = 14f;
        public float minionRangedDamage = 18f;
        public float minionMeleeRange = 1.35f;
        public float minionRangedRange = 4.6f;
        public float minionGoldMelee = 22f;
        public float minionGoldRanged = 28f;
        public float minionXpMelee = 44f;
        public float minionXpRanged = 54f;
        public float minionXpRadius = 12.0f;

        public float towerMaxHp = 1350f;
        public float towerRadius = 1.05f;
        public float towerAttackRange = 6.9f;
        public float towerAttackDamage = 68f;
        public float towerAttackInterval = 1.15f;
        public float towerProjectileSpeed = 14f;
        public float towerArmor = 18f;

        public float fountainHealPerSec = 42f;
        public float heroKillGold = 210f;
        public float heroKillXp = 175f;
        public float baseRespawnSeconds = 6.5f;
        public float respawnPerLevelSeconds = 1.4f;

        // Hero base mobility (designer tuned)
        public float ogreSpeed = 3.75f;
        public float ogreTurnRate = 270f;
        public float luisSpeed = 4.35f;
        public float luisTurnRate = 540f;
        public float swordsmanSpeed = 3.45f;
        public float swordsmanTurnRate = 480f;

        // Global Attack Speed (v0.2.8 unbounded diminishing returns: EffectiveAS = sqrt(1 + k * BonusAS))
        public float attackSpeedCurveK = 2.4f;

        public static float EffectiveAttackSpeed(float bonusAs, float curveK)
        {
            return Mathf.Sqrt(1f + Mathf.Max(0f, curveK) * Mathf.Max(0f, bonusAs));
        }

        // Ogre Base Timings & v0.2.8 Scaling (AD=Dmg, AGI=Windup Speed [sqrt], AS=Recovery Speed [sqrt], STR=Outer Q/Stun/KB/E Radius/R Size)
        public float ogreQWindup = 0.88f;
        public float ogreQRecover = 0.36f;
        public float ogreQRange = 4.7f;
        public float ogreQWidth = 0.95f;
        public float ogreQOuterDamageRatio = 0.55f;
        public float ogreQCooldown = 6.8f;

        public float ogreWWindup = 0.72f;
        public float ogreWRecover = 0.30f;
        public float ogreWRange = 3.0f;
        public float ogreWArcCos = 0.15f;
        public float ogreWCooldown = 6.2f;

        public float ogreEWindup = 0.62f;
        public float ogreERecover = 0.32f;
        public float ogreERadius = 2.35f;
        public float ogreECooldown = 8.5f;

        public float ogreRChannelDuration = 2.0f;
        public float ogreRDamageReduction = 0.65f;
        public float ogreRScaleBonus = 1.22f;
        public int ogreRMaxStrikes = 4;
        public float ogreRLungeDistance = 1.35f;
        public float ogreRFastWindup = 0.36f;
        public float ogreRFinalWindup = 0.62f;
        public float ogreRStrikeFollowthrough = 0.10f;
        public float ogreRPostComboRecover = 0.28f;
        public float ogreRFinalCritMultiplier = 1.85f;
        public float ogreRCooldown = 28f;

        // v0.2.8 Split Ogre Animation Speed Formulas + Dual-Zone Q / Sqrt W / STR E & R Size
        public StatFormula ogreWindupSpeedFormula = new StatFormula(1.0f, 0f, ScalingCurve.UnboundedSqrt, 1.0f, 0.022f, 1.0f, 0f);
        public StatFormula ogreQDamageFormula = new StatFormula(55f, 18f, ScalingCurve.Linear, 1.35f);
        public StatFormula ogreQStunFormula = new StatFormula(1.05f, 0.15f, ScalingCurve.Diminishing, 0.85f, 50f, 0.5f, 0f);
        public StatFormula ogreQOuterWidthFormula = new StatFormula(0.65f, 0.10f, ScalingCurve.Diminishing, 1.45f, 55f, 0.20f, 0f);
        public StatFormula ogreWDamageFormula = new StatFormula(40f, 15f, ScalingCurve.Linear, 1.05f);
        public StatFormula ogreWKnockbackFormula = new StatFormula(2.4f, 0.25f, ScalingCurve.UnboundedSqrt, 2.8f, 0.04f, 1.0f, 0f);
        public StatFormula ogreEDamageFormula = new StatFormula(48f, 16f, ScalingCurve.Linear, 1.15f);
        public StatFormula ogreEStunFormula = new StatFormula(0.95f, 0.12f, ScalingCurve.Diminishing, 0.65f, 50f, 0.5f, 0f);
        public StatFormula ogreERadiusFormula = new StatFormula(2.35f, 0f, ScalingCurve.Diminishing, 1.55f, 55f, 1.2f, 0f);
        public StatFormula ogreRScaleFormula = new StatFormula(1.22f, 0.06f, ScalingCurve.Diminishing, 0.52f, 65f, 1.0f, 0f);

        // Open question policy flags (05_OPEN_QUESTIONS)
        public bool ogreImmuneDuringRecovery = true;
        public bool ogreRStacksCursePerStrike = false;
        public bool swordsmanDisplaceCancelsSecondHit = true;

        // Hero Luis (Miravel) Provisional Timings & Formulas (HealPower = Life/Drain/R Amp, INT = Aura/Orb/R Radii & Curse)
        public float luisBasicRange = 6.2f;
        public float luisBasicWindup = 0.26f;
        public float luisBasicRecover = 0.52f;
        public float luisBasicProjSpeed = 16.5f;

        public float luisAuraRadius = 7.2f;
        public StatFormula luisAuraRadiusFormula = new StatFormula(7.2f, 0f, ScalingCurve.Diminishing, 4.2f, 60f, 3.0f, 0f);
        public float luisAllyAuraRegenBase = 2.2f;
        public float luisEnemyAuraRegenPenaltyBase = 2.6f;
        public float luisTakedownHealPowerGain = 0.10f;

        public float luisOrbInterval = 8.0f;
        public float luisOrbDuration = 11.0f;
        public float luisOrbPickupRadius = 1.55f;
        public float luisOrbPulseRadius = 3.8f;
        public StatFormula luisOrbPulseRadiusFormula = new StatFormula(3.8f, 0f, ScalingCurve.Diminishing, 2.4f, 60f, 2.0f, 0f);

        public float luisWDuration = 3.2f;
        public float luisWIllusionDuration = 4.5f;
        public float luisWCooldown = 10.5f;

        public float luisEWardDuration = 4.5f;
        public float luisECurseBaseDuration = 4.2f;
        public float luisECurseBonusDuration = 1.2f;
        public float luisECooldown = 8.5f;

        public float luisRRange = 5.0f;
        public float luisRChannelDuration = 4.2f;
        public float luisRRadius = 4.2f;
        public StatFormula luisRRadiusFormula = new StatFormula(4.2f, 0f, ScalingCurve.Diminishing, 2.6f, 60f, 2.5f, 0f);
        public float luisRMaxSlow = 0.52f;
        public float luisRCooldown = 26f;

        public StatFormula luisOrbHealFormula = new StatFormula(68f, 20f, ScalingCurve.Linear, 0f);
        public StatFormula luisOrbDamageFormula = new StatFormula(62f, 18f, ScalingCurve.Linear, 0f);
        public StatFormula luisCurseDpsFormula = new StatFormula(22f, 8f, ScalingCurve.Linear, 0.65f);
        // Input to R HealAmp & Vuln is BONUS HealPower (healPower - 1.0) so 100% baseline matches rank base
        public StatFormula luisRHealAmpFormula = new StatFormula(0.25f, 0.05f, ScalingCurve.Diminishing, 0.45f, 0.80f, 0f, 0f);
        public StatFormula luisRVulnFormula = new StatFormula(0.28f, 0.06f, ScalingCurve.Diminishing, 0.42f, 0.80f, 0f, 0f);

        // Old Man Swordsman Provisional Timings & Formulas (W = Stance Temper on 1st Hit, E = Vital Draw on 2nd Hit)
        public float swordsmanBasicRange = 2.35f;
        public float swordsmanHit1Windup = 0.20f;
        public StatFormula swordsmanHit2DelayFormula = new StatFormula(0.40f, 0f, ScalingCurve.Diminishing, -0.14f, 45f, 0.24f, 0f);
        public float swordsmanBasicRecover = 0.46f;
        public float swordsmanHit1AdRatio = 0.48f;
        public float swordsmanHit2AdRatio = 0.76f;

        public StatFormula swordsmanQWindupFormula = new StatFormula(0.30f, 0f, ScalingCurve.Diminishing, -0.15f, 45f, 0.14f, 0f);
        public float swordsmanQRecover = 0.24f; // Fixed recovery — NOT reduced by AGI
        public float swordsmanQRange = 3.5f;
        public float swordsmanQArcCos = 0.35f;
        public float swordsmanQSlow = 0.40f;
        public float swordsmanQSlowDuration = 2.1f;
        public float swordsmanQCooldown = 5.5f;

        // New W: Stance Temper (activated by 1st Light Strike)
        public float swordsmanWStackGain = 8f;
        public float swordsmanWStackShred = 8f;
        public int swordsmanWMaxStacks = 6;
        public StatFormula swordsmanWBuffDurationFormula = new StatFormula(6.5f, 0f, ScalingCurve.Linear, 0.08f, 0f, 2f, 0f);
        public StatFormula swordsmanWDebuffDurationFormula = new StatFormula(6.5f, 0f, ScalingCurve.Linear, 0.08f, 0f, 2f, 0f);

        // New E: Vital Draw (activated by 2nd Heavy Strike)
        public float swordsmanEHealShare = 0.55f;
        public StatFormula swordsmanEPercentHpFormula = new StatFormula(0.045f, 0.014f, ScalingCurve.Diminishing, 0.065f, 60f, 0f, 0f);

        // R: Finishing Cross
        public StatFormula swordsmanRLookbackFormula = new StatFormula(10.0f, 0f, ScalingCurve.Linear, 0.16f, 0f, 3f, 0f);
        public float swordsmanRRange = 7.5f;
        public StatFormula swordsmanRDashSpeedFormula = new StatFormula(16.0f, 0f, ScalingCurve.Linear, 0.16f, 0f, 6f, 0f);
        public StatFormula swordsmanRExecuteFormula = new StatFormula(0.20f, 0.04f, ScalingCurve.Diminishing, 0.25f, 70f, 0.10f, 0f);
        public float swordsmanRCooldown = 22f;

        public StatFormula swordsmanQDamageFormula = new StatFormula(45f, 20f, ScalingCurve.Linear, 0.90f);
        public StatFormula swordsmanRDamageFormula = new StatFormula(95f, 40f, ScalingCurve.Linear, 1.35f);

        // Item Passives — Approved custom items & specialized components + retained provisional starters/utility
        public float kindredAuraRadius = 7.5f;
        public float cowardShieldMemorySeconds = 5.0f;
        public float cowardShieldMinSpeedRatio = 0.45f;
        public float cowardShieldRetreatDot = 0.45f;
        public float cowardShieldPursuitDot = -0.25f;
        public float cowardShieldCommitDelay = 0.15f;
        public float cowardShieldBuildupSeconds = 1.55f;
        public float cowardShieldGraceSeconds = 0.75f;
        public float cowardShieldMinGraceStreak = 0.40f;
        public float cowardShieldStopDecaySeconds = 1.90f;
        public float cowardShieldPursuitDecaySeconds = 0.85f;
        public StatFormula cowardShieldResistFormula = new StatFormula(20f, 0f, ScalingCurve.Linear, 0.85f, 0f, 0f, 0f);
        public float minerHatchetBonusDmg = 18f;
        public float woodlandSalveRegen = 4.5f;
        public float woodlandSalveOutOfCombat = 5.0f;
        public float brawlerBucklerBlock = 6f;
        public float grievousHealReduction = 0.35f;
        public float grievousDuration = 3.5f;
        public StatFormula momentumGreavesFormula = new StatFormula(0.45f, 0f, ScalingCurve.Diminishing, 0.65f, 45f, 0f, 0f);
        public StatFormula colossusShieldFormula = new StatFormula(65f, 0f, ScalingCurve.Linear, 2.00f, 0f, 0f, 0f);

        // New Approved Component & Advanced Item Passives (Provisional numbers, approved curves)
        public float bloodstoneMaxAmp = 0.14f;
        public StatFormula finalJudgmentFormula = new StatFormula(0.24f, 0f, ScalingCurve.Diminishing, 0.28f, 65f, 0f, 0f);
        public float lingeringRestorationShare = 0.28f;
        public float lingeringRestorationDuration = 3.5f;
        public StatFormula fountainOfLifeFormula = new StatFormula(0.28f, 0f, ScalingCurve.Diminishing, 0.55f, 0.65f, 0f, 0f);
        public int adaptiveScalesMaxStacks = 6;
        public float adaptiveScalesStackHalfPoint = 3.0f;
        public float adaptiveScalesHoldDuration = 3.5f;
        public float adaptiveScalesDecayStep = 1.2f;
        public StatFormula adaptiveScalesMaxResistFormula = new StatFormula(20f, 0f, ScalingCurve.Diminishing, 35f, 65f, 0f, 0f);
        public float primordialVitalityMaxRegen = 12.0f;
        public int draconicHeatMaxStacks = 8;
        public float draconicHeatHalfPoint = 4.0f;
        public float draconicHeatHoldDuration = 3.5f;
        public float draconicHeatDecayStep = 1.0f;
        public StatFormula dragonfireDpsFormula = new StatFormula(18f, 0f, ScalingCurve.Linear, 0.55f, 0f, 0f, 0f);
        public StatFormula moltenEdgeShredFormula = new StatFormula(14f, 0f, ScalingCurve.Diminishing, 26f, 65f, 0f, 0f);
        public StatFormula ancientVitalityDeferFractionFormula = new StatFormula(0.16f, 0f, ScalingCurve.Diminishing, 0.34f, 1800f, 0f, 0.70f);
        public StatFormula ancientVitalityDurationFormula = new StatFormula(2.4f, 0f, ScalingCurve.Diminishing, 2.6f, 80f, 0.5f, 0f);
        public float giantsMightAdPerMaxHp = 0.030f;
        public StatFormula immovableMountainFormula = new StatFormula(0f, 0f, ScalingCurve.Diminishing, 0.65f, 70f, 0f, 0.90f);
        public StatFormula bananaCrescentCoeffFormula = new StatFormula(0.28f, 0f, ScalingCurve.Diminishing, 0.52f, 70f, 0f, 0f);
        public StatFormula bananaCrescentRadiusFormula = new StatFormula(2.30f, 0f, ScalingCurve.Diminishing, 1.75f, 75f, 1.4f, 0f);
        public StatFormula slipperyTricksterBonusFormula = new StatFormula(0.70f, 0f, ScalingCurve.Diminishing, 1.80f, 60f, 0f, 0f);
        public float slipperyTricksterDuration = 2.2f;
        public float slipperyTricksterCooldown = 14.0f;
        public float firstStrikeInactivitySeconds = 3.0f;
        public StatFormula firstStrikeReductionFormula = new StatFormula(0.22f, 0f, ScalingCurve.Diminishing, 0.43f, 60f, 0f, 0.75f);
        public float firstStrikeMinRawWindup = 0.08f;
        public float firstStrikeMinFinalWindup = 0.065f;
        public float weightlessnessSlowReduction = 0.25f;

        // Chain Laser Shooter Provisional Timings & Formulas (v0.2.8 INT Q speed & W radius & R duration; W sqrt overlap slow; Animated E step)
        public float laserSpeed = 4.00f;
        public float laserTurnRate = 560f;
        public float laserBasicRange = 6.0f;
        public float laserBasicWindup = 0.22f;
        public float laserBasicRecover = 0.66f;
        public StatFormula laserChainRadiusFormula = new StatFormula(4.2f, 0f, ScalingCurve.Diminishing, 3.2f, 60f, 2.0f, 0f);
        public float laserChainDecay = 0.70f;
        public float laserChainMinRatio = 0.25f;
        public float laserStackDuration = 6.0f;
        public float laserStackHalfPoint = 8.0f;
        public StatFormula laserStackMaxAmpFormula = new StatFormula(0.45f, 0f, ScalingCurve.Diminishing, 0.45f, 60f, 0f, 0f);

        public float laserQStartRange = 1.2f;
        public float laserQMaxRange = 8.2f;
        public float laserQHalfWidth = 0.58f;
        public float laserQDuration = 0.88f;
        public float laserQCooldown = 7.2f;
        public float laserQMultiKillBonusGold = 12f;
        public bool laserQTracksCursor = false; // Open question 05_OPEN_QUESTIONS
        public StatFormula laserQDamageFormula = new StatFormula(44f, 18f, ScalingCurve.Linear, 1.05f);
        public StatFormula laserQSpeedFormula = new StatFormula(1.0f, 0f, ScalingCurve.UnboundedSqrt, 1.0f, 0.024f, 1.0f, 0f);

        public float laserWCastRange = 7.8f;
        public StatFormula laserWShockRadiusFormula = new StatFormula(2.40f, 0f, ScalingCurve.Diminishing, 1.85f, 60f, 1.0f, 0f);
        public float laserWSlowBase = 0.20f;
        public float laserWSlowPerRank = 0.04f;
        public float laserWSlowMaxCap = 0.82f;
        public float laserWSlowOverlapHalfPoint = 3.0f;
        public float laserWSlowDuration = 1.15f;
        public float laserWSlowDurSqrtK = 1.0f;
        public float laserWCooldown = 7.5f;
        public bool laserWConsumesStacks = false; // Open question 05_OPEN_QUESTIONS
        public StatFormula laserWDamageFormula = new StatFormula(30f, 12f, ScalingCurve.Linear, 0.70f);

        public float laserEStepBack = 1.65f;
        public StatFormula laserEStepDurationFormula = new StatFormula(0.38f, 0f, ScalingCurve.Diminishing, -0.20f, 50f, 0.18f, 0f);
        public float laserEDecoyDuration = 4.5f;
        public float laserEDecoyHp = 120f;
        public float laserEDecoyRadius = 0.62f;
        public float laserECooldown = 11.0f;
        public StatFormula laserEDecoyDamageFormula = new StatFormula(34f, 14f, ScalingCurve.Linear, 0.65f);
        public StatFormula laserEStunFormula = new StatFormula(0.65f, 0.15f, ScalingCurve.Linear, 0f, 0f, 0.35f, 0f);

        public float laserRWindup = 0.50f;
        public StatFormula laserRDurationFormula = new StatFormula(2.4f, 0.30f, ScalingCurve.Diminishing, 2.0f, 65f, 1.0f, 0f);
        public float laserRRange = 8.0f;
        public float laserRHalfWidth = 0.72f;
        public float laserRProcInterval = 0.42f;
        public float laserRProcDamageScale = 0.62f;
        public float laserRCooldown = 28f;
        public bool laserRTracksCursor = false; // Open question 05_OPEN_QUESTIONS
        public StatFormula laserRMoveRatioFormula = new StatFormula(0.78f, 0f, ScalingCurve.Diminishing, 0.22f, 50f, 0.5f, 0f);

        // Shared Hero Resource System (Stage C / v0.2.12 — Provisional values calibrated with Mana items)
        public StatFormula resourceCostReductionFormula = new StatFormula(0f, 0f, ScalingCurve.Diminishing, 0.65f, 65f, 0f, 0.65f);
        public float fountainManaPerSec = 48f;

        // Miravel (Mana): Passive & auto-Q = 0 Mana; W/E rank flat; R continuous/s independent of INT
        public float miravelBaseMana = 320f;
        public float miravelManaPerLevel = 32f;
        public float miravelBaseManaRegen = 2.6f;
        public float miravelManaRegenPerLevel = 0.25f;
        public float miravelWManaBase = 55f;
        public float miravelWManaPerRank = 10f;
        public float miravelEManaBase = 45f;
        public float miravelEManaPerRank = 10f;
        public float miravelRManaPerSecBase = 26f;
        public float miravelRManaPerSecPerRank = 8f;
        public float miravelRMinStartFraction = 0.25f;

        // Chain Laser (Mana): Passive basics = 0 Mana; Q high rank flat; W lower rank flat; E low constant flat; R substantial constant upfront
        public float laserBaseMana = 300f;
        public float laserManaPerLevel = 28f;
        public float laserBaseManaRegen = 2.2f;
        public float laserManaRegenPerLevel = 0.22f;
        public float laserQManaBase = 65f;
        public float laserQManaPerRank = 13f;
        public float laserWManaBase = 45f;
        public float laserWManaPerRank = 7f;
        public float laserEManaFlat = 35f;
        public float laserRManaUpfront = 110f;
    }

    [Serializable]
    public struct LaneOrder
    {
        public int tick;
        public LaneTeam team;
        public LaneOrderKind kind;
        public Vector2 point;
        public int targetId;
        public int param;
        public int[] unitIds;
    }

    /// <summary>
    /// Isolated local-only stat override payload used exclusively inside the interactive
    /// Hero Guide preview sandbox. Never transmitted over networking or applied to real matches.
    /// </summary>
    [Serializable]
    public struct PreviewStatOverrides
    {
        public bool enabled;
        public int level;
        public int abilityRank;
        public float bonusStrength;
        public float bonusAgility;
        public float bonusIntelligence;
        public float bonusAttackDamage;
        public float bonusAttackSpeed;
        public float bonusAbilityHaste;
        public float bonusHealPower;
        public float bonusMaxHp;
        public float bonusMaxMana;
        public float bonusManaRegen;
        public float bonusPercentManaRegen;
        public float bonusResourceCostReduction;
    }

    [Serializable]
    public sealed class LaneQueuedStrikeSnap
    {
        public int slot;
        public float dirX;
        public float dirY;
    }

    [Serializable]
    public sealed class HeroSnap
    {
        public int id;
        public LaneTeam team;
        public LaneHeroId heroId;
        public int level;
        public float xp;
        public float xpToNext;
        public int skillPoints;
        public int gold;
        public int kills;
        public int deaths;
        public int creepScore;

        public float x;
        public float y;
        public float headingDeg;
        public float speed;
        public float radius;
        public float hp;
        public float maxHp;
        public float respawnTimer;
        public bool invisible;
        public bool ccImmune;

        // Conventional stats vs Independent scaling attributes
        public float attackDamage;
        public float attackSpeed;
        public float abilityHaste;
        public float cooldownReduction;
        public float armor;
        public float magicResist;
        public float regenRate;
        public float healPower;
        public float moveSpeed;
        public float damageReduction;
        public float cowardShieldBonus;
        public float ogreAnimSpeed;
        public float strength;
        public float agility;
        public float intelligence;
        public float scale = 1f;

        public LanePhase phase;
        public LaneAbilitySlot activeSlot;
        public float phaseTimer;
        public float phaseDuration;
        public float aimDirX;
        public float aimDirY;
        public float castTargetX;
        public float castTargetY;

        public int rankQ;
        public int rankW;
        public int rankE;
        public int rankR;
        public float cdQ;
        public float cdW;
        public float cdE;
        public float cdR;
        public float maxCdQ;
        public float maxCdW;
        public float maxCdE;
        public float maxCdR;

        public float stunTimer;
        public float slowTimer;
        public float slowAmount;
        public float damageTakenMult = 1f;
        public float healReceivedMult = 1f;
        public float curseTimer;
        public int curseStacks;
        public float wardTimer;
        public float orbCooldown;
        public float swordResistDelta;
        public int swordStacks;
        public float swordStackTimer;
        public int swordMarkedTargetId;
        public float swordMarkedTimer;
        public int attackTargetKind;
        public int attackTargetId;
        public bool isAttackMoving;
        public int rComboCount;
        public List<LaneQueuedStrikeSnap> rQueue = new List<LaneQueuedStrikeSnap>();
        public int[] items = new int[LaneItems.MaxSlots];

        // Status, shield & procedural animation hints
        public float miravelDrainAura;
        public float miravelLifeAura;
        public float miravelAuraRadius;
        public float netRegenRate;
        public float shieldHp;
        public float shieldTimer;
        public float grievousTimer;
        public float momentumBonus;
        public float colossusCd;
        public float deferredDamageTotal;
        public int heatStacks;
        public float heatTimer;
        public int adaptiveScalesStacks;
        public float adaptiveScalesBonus;
        public bool firstStrikeReady;
        public float firstStrikeIdle;
        public float slipperyBonus;
        public float slipperyCd;
        public int rStrikeIndex;
        public bool rStrikeIsFinal;
        public int basicSwingAlt;
        public bool isTwinSecondStrike;
        public int laserStacks;
        public float laserStackTimer;
        public float laserBeamRange;

        // v0.2.8 Scaling telemetry & UI/telegraph hints
        public float bonusAttackSpeed;
        public float ogreWindupSpeed;
        public float ogreRecoverSpeed;
        public float ogreQCentralHalfWidth;
        public float ogreQOuterHalfWidth;
        public float ogreWRange;
        public float ogreERadius;
        public float cowardShieldFactor;
        public byte cowardShieldState; // 0=Idle, 1=Building, 2=Active, 3=Grace, 4=Decaying
        public float laserStepProgress;

        // Stage C Shared Hero Resource telemetry
        public HeroResourceKind resourceKind;
        public float mana;
        public float maxMana;
        public float manaRegen;
        public float netManaRegen;
        public float rcrStat;
        public float rcrPct;
        public float costQ;
        public float costW;
        public float costE;
        public float costR;
        public ResourceCostMode costModeR;
    }

    [Serializable]
    public sealed class TowerSnap
    {
        public int id;
        public LaneTeam team;
        public float x;
        public float y;
        public float hp;
        public float maxHp;
        public bool alive;
        public float attackRange;
        public int targetKind;
        public int targetId;
        public float fireCooldown;
        public bool aggroLockedOnHero;
    }

    [Serializable]
    public sealed class MinionSnap
    {
        public int id;
        public LaneTeam team;
        public bool ranged;
        public float x;
        public float y;
        public float headingDeg;
        public float hp;
        public float maxHp;
        public float slowTimer;
        public float stunTimer;
        public int laserStacks;
    }

    [Serializable]
    public sealed class IllusionSnap
    {
        public int id;
        public LaneTeam team;
        public float x;
        public float y;
        public float headingDeg;
        public float speed;
        public float hp;
        public float maxHp;
        public float timeLeft;
        public bool isLaserDecoy;
        public bool controllable;
        public float decoyRadius;
        public int laserStacks;
    }

    [Serializable]
    public sealed class ProjectileSnap
    {
        public int id;
        public LaneTeam team;
        public LaneProjKind kind;
        public float x;
        public float y;
        public float targetX;
        public float targetY;
    }

    [Serializable]
    public sealed class OrbSnap
    {
        public int id;
        public LaneTeam ownerTeam;
        public float x;
        public float y;
        public float timeLeft;
        public float duration;
    }

    [Serializable]
    public sealed class ZoneSnap
    {
        public LaneTeam team;
        public float x;
        public float y;
        public float radius;
        public float intensity;
        public float timeLeft;
    }

    [Serializable]
    public sealed class VisualEventSnap
    {
        public int id;
        public LaneEventKind kind;
        public LaneTeam team;
        public float x;
        public float y;
        public float dirX;
        public float dirY;
        public float radius;
        public float value;
        public string label;
    }

    [Serializable]
    public sealed class LaserBeamSnap
    {
        public LaneTeam team;
        public float x0;
        public float y0;
        public float x1;
        public float y1;
        public float width;
        public bool isUlt;
    }

    [Serializable]
    public sealed class LaneSnapshot
    {
        public int tick;
        public float matchTime;
        public float nextWaveIn;
        public int waveNumber;
        public bool gameOver;
        public int winnerTeam = -1;
        public string banner;

        public List<HeroSnap> heroes = new List<HeroSnap>();
        public List<TowerSnap> towers = new List<TowerSnap>();
        public List<MinionSnap> minions = new List<MinionSnap>();
        public List<IllusionSnap> illusions = new List<IllusionSnap>();
        public List<ProjectileSnap> projectiles = new List<ProjectileSnap>();
        public List<OrbSnap> orbs = new List<OrbSnap>();
        public List<ZoneSnap> zones = new List<ZoneSnap>();
        public List<VisualEventSnap> events = new List<VisualEventSnap>();
        public List<LaserBeamSnap> beams = new List<LaserBeamSnap>();
    }
}
