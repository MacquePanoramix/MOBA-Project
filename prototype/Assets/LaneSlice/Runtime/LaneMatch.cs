using System;
using System.Collections.Generic;
using Moba.MovementLab;
using UnityEngine;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Pure-C# fixed-step MOBA lane simulation. Authoritative over heroes, waves, towers,
    /// items, levels, projectiles, orbs, illusions, and combat math.
    /// </summary>
    public sealed partial class LaneMatch
    {
        private static readonly float[] XpThresholds = { 0f, 180f, 240f, 320f, 420f, 540f };

        private sealed class HeroState
        {
            public int id;
            public LaneTeam team;
            public LaneHeroId heroId;
            public MovementMotor motor;
            public int level = 1;
            public float xp;
            public int skillPoints = 1;
            public float goldExact;
            public int kills;
            public int deaths;
            public int creepScore;

            public float hp;
            public float maxHp;
            public float respawnTimer;
            public bool invisible;
            public float invisTimer;

            // Conventional stats vs independent Str/Agi/Int scaling attributes (0 inherent bonuses)
            public float strength;
            public float agility;
            public float intelligence;
            public float attackDamage;
            public float attackSpeed = 1f;
            public float bonusAttackSpeed;
            public float abilityHaste;
            public float cooldownReduction;
            public float armor;
            public float magicResist;
            public float regenRate;
            public float netRegenRate;
            public float healPower = 1f;
            public float takedownHealBonus;
            public float itemAlliedAuraRegen;
            public float moveSpeed;
            public float turnRate;
            public float damageReduction;
            public float ogreAnimSpeed = 1f;
            public float ogreWindupSpeed = 1f;
            public float ogreRecoverSpeed = 1f;
            public bool hasCowardsShield;
            public float cowardShieldFactor;
            public float cowardShieldBonus;
            public float cowardRetreatStreak;
            public float cowardGraceTimer;
            public byte cowardShieldState;
            public Vector2 prevMotorPos;
            public int lastHeroAttackerId;
            public float lastHeroDamageAge = 999f;
            public float scale = 1f;

            public LanePhase phase = LanePhase.Idle;
            public LaneAbilitySlot activeSlot = LaneAbilitySlot.None;
            public float phaseTimer;
            public float phaseDuration;
            public Vector2 aimDir = Vector2.right;
            public Vector2 castTarget;

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

            // Miravel (Luís) kit state
            public float wardTimer;
            public float orbCooldown = 1.0f;
            public float curseTimer;
            public int curseStacks;
            public int curseSourceHeroId;
            public float curseTickAccum;

            // Swordsman kit state
            public bool twinSecondPending;
            public int twinTargetKind;
            public int twinTargetId;
            public int swordStacks;
            public float swordStackTimer;
            public int swordDebuffStacks;
            public float swordDebuffTimer;
            public int swordLastTargetKind;
            public int swordLastTargetId;
            public float swordMarkTimer;
            public Vector2 dashEndPos;
            public bool dashHitApplied;
            public int dashTargetKind;
            public int dashTargetId;

            // Ogre R queue
            public readonly List<(LaneAbilitySlot slot, Vector2 dir)> rQueue = new List<(LaneAbilitySlot, Vector2)>();
            public int rStrikeIndex;
            public bool rStrikeInFollowthrough;
            public int basicSwingAlt;

            // Chain Laser Shooter state
            public int laserStacks;
            public float laserStackTimer;
            public float laserQCurRange;
            public int laserQKillCount;
            public readonly HashSet<int> laserQHitIds = new HashSet<int>();
            public float laserRProcAccum;
            public int laserSlowOverlapCount;
            public float laserSlowElapsed;
            public Vector2 laserEStepStartPos;
            public Vector2 laserEStepEndPos;
            public bool laserEStepPending;
            public bool swordsmanLastStrikeHeavy;

            // Aura & expanded item passives / status
            public ItemPassiveFlags passiveFlags;
            public bool HasPassive(ItemPassiveFlags flag) => (passiveFlags & flag) != 0;

            public float miravelDrainAura;
            public float miravelLifeAura;
            public float momentumFactor;
            public float momentumBonus;
            public float shieldHp;
            public float shieldTimer;
            public float colossusCd;
            public float grievousTimer;

            // Stage D/E Custom Item & Specialized Component State
            public float firstStrikeIdleTimer;
            public float slipperyTimer;
            public float slipperyCd;
            public float slipperyInitialBonus;
            public float slipperyBonus;
            public readonly List<DeferredDamageEntry> deferredDamage = new List<DeferredDamageEntry>();
            public readonly List<DeferredHealEntry> deferredHeals = new List<DeferredHealEntry>();
            public readonly List<AdaptiveScalesEntry> adaptiveScales = new List<AdaptiveScalesEntry>();
            public readonly List<HeatEntry> heatEntries = new List<HeatEntry>();

            // Stage C Shared Hero Resource state
            public HeroResourceKind resourceKind;
            public float mana;
            public float maxMana;
            public float manaRegen;
            public float percentManaRegenPerSec;
            public float netManaRegen;
            public float rcrStat;
            public float rcrPct;
            public float lowManaPopupCd;

            // Targeting / Attack-Move / Input buffering
            public LaneTargetKind attackTargetKind = LaneTargetKind.None;
            public int attackTargetId;
            public bool isAttackMoving;
            public Vector2 attackMoveDest;
            public LaneOrder? bufferedOrder;
            public float bufferedOrderTimer;

            public readonly int[] items = new int[LaneItems.MaxSlots];
        }

        private sealed class TowerState
        {
            public int id;
            public LaneTeam team;
            public Vector2 pos;
            public float hp;
            public float maxHp;
            public bool alive = true;
            public LaneTargetKind targetKind = LaneTargetKind.None;
            public int targetId;
            public float fireCooldown;
            public bool aggroLockedOnHero;
        }

        private sealed class MinionState
        {
            public int id;
            public LaneTeam team;
            public bool ranged;
            public MovementMotor motor;
            public float hp;
            public float maxHp;
            public float attackCooldown;
            public float attackWindup;
            public bool windingUp;
            public LaneTargetKind targetKind = LaneTargetKind.None;
            public int targetId;
            public float stunTimer;
            public float slowTimer;
            public float slowAmount;
            public int laserSlowOverlapCount;
            public float laserSlowElapsed;
            public int swordShredStacks;
            public float swordShredTimer;
            public float damageTakenMult = 1f;
            public float healReceivedMult = 1f;
            public int laserStacks;
            public float laserStackTimer;
            public bool isPreviewDummy;
            public readonly List<HeatEntry> heatEntries = new List<HeatEntry>();
        }

        private sealed class DeferredDamageEntry
        {
            public float totalAmount;
            public float paidSoFar;
            public float duration;
            public float elapsed;
            public int attackerHeroId;
            public LaneTeam attackerTeam;
        }

        private sealed class DeferredHealEntry
        {
            public float totalAmount;
            public float paidSoFar;
            public float duration;
            public float elapsed;
        }

        private sealed class AdaptiveScalesEntry
        {
            public int attackerHeroId;
            public int stacks;
            public float holdTimer;
            public float decayAccum;
        }

        private sealed class HeatEntry
        {
            public int ownerHeroId;
            public int stacks;
            public float holdTimer;
            public float decayAccum;
            public float dotAccum;
        }

        private sealed class IllusionState
        {
            public int id;
            public int ownerHeroId;
            public LaneTeam team;
            public MovementMotor motor;
            public float hp;
            public float maxHp;
            public float timeLeft;
            public Vector2 dest;
            public float moveSpeed;
            public float armor;
            public float magicResist;
            public bool isLaserDecoy;
            public float decoyRadius;
            public int laserStacks;
            public float laserStackTimer;
            public bool isPreviewDummy;
        }

        private sealed class ProjectileState
        {
            public int id;
            public LaneTeam team;
            public LaneProjKind kind;
            public int sourceHeroId;
            public int sourceMinionId;
            public Vector2 pos;
            public float speed;
            public float damage;
            public bool isMagic;
            public LaneTargetKind targetKind;
            public int targetId;
            public Vector2 lastKnownTarget;
        }

        private sealed class OrbState
        {
            public int id;
            public LaneTeam ownerTeam;
            public int ownerHeroId;
            public Vector2 pos;
            public float timeLeft;
            public float duration;
        }

        private sealed class ZoneState
        {
            public int ownerHeroId;
            public LaneTeam team;
            public Vector2 pos;
            public float radius;
            public float elapsed;
            public float duration;
            public float tickAccum;
            public int rankR;
        }

        private readonly LaneTuning tuning;
        private readonly LaneMap map;
        private readonly HeroState[] heroes = new HeroState[2];
        private readonly TowerState[] towers = new TowerState[2];
        private readonly List<MinionState> minions = new List<MinionState>();
        private readonly List<IllusionState> illusions = new List<IllusionState>();
        private readonly List<ProjectileState> projectiles = new List<ProjectileState>();
        private readonly List<OrbState> orbs = new List<OrbState>();
        private readonly List<ZoneState> zones = new List<ZoneState>();
        private readonly List<VisualEventSnap> events = new List<VisualEventSnap>();
        private readonly List<VisualEventSnap> pendingOrderEvents = new List<VisualEventSnap>();
        private readonly List<LaserBeamSnap> laserBeams = new List<LaserBeamSnap>();
        private readonly List<Vector2> scratchRoute = new List<Vector2>();
        private readonly Dictionary<LaneTeam, PreviewStatOverrides> previewOverrides = new Dictionary<LaneTeam, PreviewStatOverrides>();

        private int nextEntityId = 100;
        private int tickCount;
        private float matchTime;
        private bool inTick;
        private float nextWaveTimer;
        private int waveNumber;
        private bool gameOver;
        private int winnerTeam = -1;
        private string bannerText = string.Empty;
        private float bannerTimer;

        public LaneTuning Tuning => tuning;
        public LaneMap Map => map;
        public int TickCount => tickCount;
        public float MatchTime => matchTime;
        public bool IsGameOver => gameOver;
        public int WinnerTeam => winnerTeam;

        public LaneMatch(LaneHeroId blueHero, LaneHeroId redHero, LaneTuning customTuning = null)
        {
            tuning = customTuning ?? new LaneTuning();
            map = new LaneMap(tuning);

            heroes[0] = CreateHero(1, LaneTeam.Blue, blueHero, map.SpawnPos(LaneTeam.Blue), 90f);
            heroes[1] = CreateHero(2, LaneTeam.Red, redHero, map.SpawnPos(LaneTeam.Red), 270f);

            towers[0] = new TowerState
            {
                id = 10,
                team = LaneTeam.Blue,
                pos = map.TowerPos(LaneTeam.Blue),
                hp = tuning.towerMaxHp,
                maxHp = tuning.towerMaxHp,
                alive = true,
            };
            towers[1] = new TowerState
            {
                id = 11,
                team = LaneTeam.Red,
                pos = map.TowerPos(LaneTeam.Red),
                hp = tuning.towerMaxHp,
                maxHp = tuning.towerMaxHp,
                alive = true,
            };

            nextWaveTimer = tuning.waveFirstSpawnDelay;
        }

        private MovementCourse ActiveCourse() => map.CourseFor(towers[0].alive, towers[1].alive);

        private HeroState GetHero(LaneTeam team) => heroes[team == LaneTeam.Blue ? 0 : 1];
        private HeroState GetEnemyHero(LaneTeam team) => heroes[team == LaneTeam.Blue ? 1 : 0];
        private TowerState GetEnemyTower(LaneTeam team) => towers[team == LaneTeam.Blue ? 1 : 0];

        public void SetPreviewStatOverrides(LaneTeam team, PreviewStatOverrides overrides)
        {
            previewOverrides[team] = overrides;
            HeroState h = GetHero(team);
            if (overrides.enabled)
            {
                h.level = Mathf.Clamp(overrides.level > 0 ? overrides.level : 1, 1, tuning.maxLevel);
                int maxBasicRank = Mathf.Clamp(overrides.abilityRank > 0 ? overrides.abilityRank : 1, 1, 3);
                h.rankQ = maxBasicRank;
                h.rankW = maxBasicRank;
                h.rankE = maxBasicRank;
                h.rankR = Mathf.Clamp(overrides.abilityRank > 0 ? overrides.abilityRank : 1, 1, 2);
            }
            RecalcHeroStats(h, preserveHpRatio: true);
        }

        public void DisableTowersForPreview()
        {
            towers[0].alive = false;
            towers[0].hp = 0f;
            towers[1].alive = false;
            towers[1].hp = 0f;
        }

        public int ConfigurePreviewHero(LaneTeam team, Vector2 pos, float headingDeg, float hpFraction = 1f)
        {
            HeroState h = GetHero(team);
            Vector2 safe = map.ClampInsideLane(pos);
            h.motor = new MovementMotor(safe, headingDeg);
            h.prevMotorPos = safe;
            h.phase = LanePhase.Idle;
            h.activeSlot = LaneAbilitySlot.None;
            h.phaseTimer = 0f;
            h.phaseDuration = 0f;
            h.cdQ = 0f;
            h.cdW = 0f;
            h.cdE = 0f;
            h.cdR = 0f;
            h.stunTimer = 0f;
            h.slowTimer = 0f;
            h.slowAmount = 0f;
            h.curseTimer = 0f;
            h.curseStacks = 0;
            h.wardTimer = 0f;
            h.invisible = false;
            h.invisTimer = 0f;
            h.swordStacks = 0;
            h.swordDebuffStacks = 0;
            h.laserStacks = 0;
            h.rQueue.Clear();
            h.rStrikeIndex = 0;
            h.twinSecondPending = false;
            h.lowManaPopupCd = 0f;
            ClearAttackAndRoute(h);
            RecalcHeroStats(h, preserveHpRatio: false);
            h.hp = Mathf.Clamp(h.maxHp * Mathf.Clamp01(hpFraction), 1f, h.maxHp);
            return h.id;
        }

        public int SpawnPreviewDummyMinion(LaneTeam team, Vector2 pos, bool ranged = false, float maxHp = 800f, float hpFraction = 1f)
        {
            Vector2 safe = map.ClampInsideLane(pos);
            float hp = Mathf.Clamp(maxHp * Mathf.Clamp01(hpFraction), 1f, maxHp);
            var m = new MinionState
            {
                id = AllocId(),
                team = team,
                ranged = ranged,
                motor = new MovementMotor(safe, team == LaneTeam.Blue ? 90f : 270f),
                hp = hp,
                maxHp = maxHp,
                isPreviewDummy = true,
            };
            minions.Add(m);
            return m.id;
        }

        public int SpawnPreviewOrb(LaneTeam team, Vector2 pos)
        {
            HeroState owner = GetHero(team);
            Vector2 safe = map.ClampInsideLane(pos);
            int id = AllocId();
            orbs.Add(new OrbState
            {
                id = id,
                ownerTeam = team,
                ownerHeroId = owner.id,
                pos = safe,
                timeLeft = tuning.luisOrbDuration,
                duration = tuning.luisOrbDuration,
            });
            PushEvent(LaneEventKind.LuisOrbSpawn, team, safe, Vector2.up, tuning.luisOrbPickupRadius, 0f, string.Empty);
            return id;
        }

        public static float ResistanceFactor(float resistance)
        {
            return resistance >= 0f
                ? 100f / (100f + resistance)
                : 2f - 100f / (100f - resistance);
        }

        public static float CooldownFactor(float abilityHaste)
        {
            return 100f / (100f + Mathf.Max(0f, abilityHaste));
        }

        public void EnqueueOrApplyOrder(LaneOrder order)
        {
            if (gameOver) return;
            if (order.kind == LaneOrderKind.LevelAbility)
            {
                TryLevelAbility(order.team, (LaneAbilitySlot)order.param);
                return;
            }
            if (order.kind == LaneOrderKind.BuyItem)
            {
                TryBuyItem(order.team, order.param, out _);
                return;
            }
            if (order.kind == LaneOrderKind.SellItem)
            {
                TrySellItem(order.team, order.param, out _);
                return;
            }

            HeroState h = GetHero(order.team);

            bool isUnitCommand = order.kind == LaneOrderKind.Stop ||
                                 order.kind == LaneOrderKind.Move ||
                                 order.kind == LaneOrderKind.AttackMove ||
                                 order.kind == LaneOrderKind.AttackHero ||
                                 order.kind == LaneOrderKind.AttackMinion ||
                                 order.kind == LaneOrderKind.AttackTower;

            if (isUnitCommand && order.unitIds != null && order.unitIds.Length > 0)
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < order.unitIds.Length; i++)
                {
                    int uid = order.unitIds[i];
                    if (!seen.Add(uid)) continue;

                    if (uid == h.id)
                    {
                        ProcessHeroOrder(h, order);
                    }
                    else if (TryFindAuthorizedControllableIllusion(order.team, h.id, uid, out IllusionState ill))
                    {
                        ApplyControllableIllusionOrder(ill, order);
                    }
                }
                return;
            }

            ProcessHeroOrder(h, order);
        }

        private void ProcessHeroOrder(HeroState h, LaneOrder order)
        {
            if (h.phase == LanePhase.Dead)
            {
                return;
            }

            HeroControlProfile profile = HeroControlCatalog.ForHero(h.heroId);

            if (order.kind == LaneOrderKind.Stop)
            {
                if (!profile.SupportsStop)
                {
                    return;
                }
                CancelChannelIfInterrupted(h, byMoveOrOrder: true);
                if (h.phase == LanePhase.Windup)
                {
                    h.phase = LanePhase.Idle;
                    h.activeSlot = LaneAbilitySlot.None;
                    h.twinSecondPending = false;
                }
                ClearAttackAndRoute(h);
                h.bufferedOrder = null;
                h.bufferedOrderTimer = 0f;
                return;
            }

            if (order.kind == LaneOrderKind.AttackMove ||
                order.kind == LaneOrderKind.AttackHero ||
                order.kind == LaneOrderKind.AttackMinion ||
                order.kind == LaneOrderKind.AttackTower)
            {
                if (!profile.SupportsBasicAttack)
                {
                    return;
                }
            }

            // Ogre R channel accepts Q and W combo inputs directly!
            if (h.heroId == LaneHeroId.Ogre && h.phase == LanePhase.Channeling && h.activeSlot == LaneAbilitySlot.R)
            {
                if (order.kind == LaneOrderKind.CastQ || order.kind == LaneOrderKind.CastW)
                {
                    if (h.rQueue.Count < tuning.ogreRMaxStrikes)
                    {
                        Vector2 dir = DirectionTo(h.motor.Position, order.point, h.aimDir);
                        h.rQueue.Add((order.kind == LaneOrderKind.CastQ ? LaneAbilitySlot.Q : LaneAbilitySlot.W, dir));
                        h.aimDir = dir;
                    }
                    return;
                }
            }

            bool isCast = order.kind >= LaneOrderKind.CastQ && order.kind <= LaneOrderKind.CastR;

            if (h.stunTimer > 0f && !IsCcImmune(h))
            {
                if (isCast)
                {
                    h.bufferedOrder = order;
                    h.bufferedOrderTimer = 0.25f;
                }
                return;
            }

            // Miravel R channel is interrupted by issuing a new movement, attack, or other ability order
            if (h.heroId == LaneHeroId.Miravel && h.phase == LanePhase.Channeling && h.activeSlot == LaneAbilitySlot.R)
            {
                if (order.kind != LaneOrderKind.CastR)
                {
                    InterruptChannel(h);
                }
                else
                {
                    return;
                }
            }

            // Ability casts immediately cancel basic-attack windup or basic backswing so spells never feel eaten
            if (isCast && (h.phase == LanePhase.Windup || (h.phase == LanePhase.Recover && h.activeSlot == LaneAbilitySlot.None)))
            {
                h.phase = LanePhase.Idle;
                h.twinSecondPending = false;
            }

            // Buffer cast requests briefly during another ability cast, channel, dash, or ability recovery
            if (isCast && (h.phase == LanePhase.Casting || h.phase == LanePhase.Channeling ||
                           h.phase == LanePhase.ComboSeq || h.phase == LanePhase.ExecuteDash ||
                           (h.phase == LanePhase.Recover && h.activeSlot != LaneAbilitySlot.None)))
            {
                h.bufferedOrder = order;
                h.bufferedOrderTimer = 0.30f;
                return;
            }

            ApplyOrderImmediate(h, order);
        }

        private bool TryFindAuthorizedControllableIllusion(LaneTeam team, int ownerHeroId, int unitId, out IllusionState found)
        {
            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                if (ill.id == unitId &&
                    ill.team == team &&
                    ill.ownerHeroId == ownerHeroId &&
                    !ill.isLaserDecoy &&
                    ill.hp > 0f)
                {
                    found = ill;
                    return true;
                }
            }
            found = null;
            return false;
        }

        private void ApplyControllableIllusionOrder(IllusionState ill, LaneOrder order)
        {
            if (order.kind == LaneOrderKind.Stop)
            {
                ill.motor.Stop();
                ill.dest = ill.motor.Position;
                return;
            }

            Vector2 rawPoint = order.point;
            if (order.kind == LaneOrderKind.AttackHero ||
                order.kind == LaneOrderKind.AttackMinion ||
                order.kind == LaneOrderKind.AttackTower)
            {
                LaneTargetKind tk = order.kind == LaneOrderKind.AttackHero
                    ? LaneTargetKind.Hero
                    : (order.kind == LaneOrderKind.AttackMinion ? LaneTargetKind.Minion : LaneTargetKind.Tower);
                if (IsValidHostileTarget(ill.team, tk, order.targetId, out Vector2 tPos, out _))
                {
                    rawPoint = tPos;
                }
            }

            Vector2 safePos = map.NearestWalkable(ill.motor.Position, towers[0].alive, towers[1].alive);
            if ((safePos - ill.motor.Position).sqrMagnitude > 0.0025f)
            {
                ill.motor.SetPositionPreserveSpeed(safePos);
            }

            Vector2 dest = map.NearestWalkable(map.ClampInsideLane(rawPoint), towers[0].alive, towers[1].alive);
            ill.dest = dest;
            if (Vector2.Distance(ill.motor.Position, dest) > 0.12f)
            {
                ill.motor.TrySetDestination(ActiveCourse(), dest);
            }
            else
            {
                ill.motor.Stop();
            }
        }

        private void ApplyOrderImmediate(HeroState h, LaneOrder order)
        {
            if (h.phase == LanePhase.Dead) return;

            switch (order.kind)
            {
                case LaneOrderKind.Move:
                    if (h.heroId == LaneHeroId.LaserShooter && h.phase == LanePhase.Channeling && h.activeSlot == LaneAbilitySlot.R)
                    {
                        ClearAttackIntent(h);
                        TryRouteHero(h, order.point);
                        break;
                    }
                    if (IsBusyCasting(h)) return;
                    if (h.phase == LanePhase.Windup || (h.phase == LanePhase.Recover && h.activeSlot == LaneAbilitySlot.None))
                    {
                        h.phase = LanePhase.Idle;
                        h.activeSlot = LaneAbilitySlot.None;
                        h.twinSecondPending = false;
                    }
                    ClearAttackIntent(h);
                    TryRouteHero(h, order.point);
                    break;

                case LaneOrderKind.AttackMove:
                {
                    if (IsBusyCasting(h)) return;
                    Vector2 clamped = map.ClampInsideLane(order.point);
                    PushEvent(LaneEventKind.AttackMoveRing, h.team, clamped, h.aimDir, 0.9f, 0f, string.Empty);

                    bool found = TryAcquireTargetNear(h, clamped, tuning.attackMoveScanRadius, out LaneTargetKind foundKind, out int foundId);
                    if (!found)
                    {
                        float selfScan = Mathf.Max(GetBaseBasicRange(h.heroId) + 1.8f, 4.8f);
                        found = TryAcquireTargetNear(h, h.motor.Position, selfScan, out foundKind, out foundId);
                    }

                    if (found)
                    {
                        bool sameTarget = h.attackTargetKind == foundKind && h.attackTargetId == foundId;
                        bool inBasicWindup = h.phase == LanePhase.Windup;
                        bool inBasicRecover = h.phase == LanePhase.Recover && h.activeSlot == LaneAbilitySlot.None;

                        h.isAttackMoving = true;
                        h.attackMoveDest = clamped;

                        if (inBasicWindup && (sameTarget || h.twinSecondPending))
                        {
                            break;
                        }
                        if (inBasicRecover)
                        {
                            h.attackTargetKind = foundKind;
                            h.attackTargetId = foundId;
                            break;
                        }
                        if (inBasicWindup && !sameTarget)
                        {
                            h.phase = LanePhase.Idle;
                            h.activeSlot = LaneAbilitySlot.None;
                            h.twinSecondPending = false;
                        }
                        h.attackTargetKind = foundKind;
                        h.attackTargetId = foundId;
                    }
                    else
                    {
                        if (h.phase == LanePhase.Windup || (h.phase == LanePhase.Recover && h.activeSlot == LaneAbilitySlot.None))
                        {
                            h.phase = LanePhase.Idle;
                            h.activeSlot = LaneAbilitySlot.None;
                            h.twinSecondPending = false;
                        }
                        ClearAttackIntent(h);
                        h.isAttackMoving = true;
                        h.attackMoveDest = clamped;
                        TryRouteHero(h, clamped);
                    }
                    break;
                }

                case LaneOrderKind.AttackHero:
                case LaneOrderKind.AttackMinion:
                case LaneOrderKind.AttackTower:
                    if (IsBusyCasting(h)) return;
                    LaneTargetKind tk = order.kind == LaneOrderKind.AttackHero
                        ? LaneTargetKind.Hero
                        : (order.kind == LaneOrderKind.AttackMinion ? LaneTargetKind.Minion : LaneTargetKind.Tower);
                    if (!IsValidHostileTarget(h.team, tk, order.targetId, out Vector2 tPos, out _))
                    {
                        TryRouteHero(h, order.point);
                        return;
                    }
                    h.isAttackMoving = false;
                    h.attackTargetKind = tk;
                    h.attackTargetId = order.targetId;
                    break;

                case LaneOrderKind.CastQ:
                    TryCastAbility(h, LaneAbilitySlot.Q, order.point);
                    break;
                case LaneOrderKind.CastW:
                    TryCastAbility(h, LaneAbilitySlot.W, order.point);
                    break;
                case LaneOrderKind.CastE:
                    TryCastAbility(h, LaneAbilitySlot.E, order.point);
                    break;
                case LaneOrderKind.CastR:
                    TryCastAbility(h, LaneAbilitySlot.R, order.point);
                    break;
            }
        }

        public bool TryLevelAbility(LaneTeam team, LaneAbilitySlot slot)
        {
            if (gameOver) return false;
            HeroState h = GetHero(team);
            if (h.skillPoints <= 0) return false;

            switch (slot)
            {
                case LaneAbilitySlot.Q:
                    if (h.rankQ >= 3) return false;
                    h.rankQ++;
                    break;
                case LaneAbilitySlot.W:
                    if (h.rankW >= 3) return false;
                    h.rankW++;
                    break;
                case LaneAbilitySlot.E:
                    if (h.rankE >= 3) return false;
                    h.rankE++;
                    break;
                case LaneAbilitySlot.R:
                    int maxR = h.level >= tuning.maxLevel ? 2 : 1;
                    if (h.level < tuning.ultMinLevel || h.rankR >= maxR) return false;
                    h.rankR++;
                    break;
                default:
                    return false;
            }

            h.skillPoints--;
            RecalcHeroStats(h, preserveHpRatio: true);
            return true;
        }

        public bool TryBuyItem(LaneTeam team, int itemId, out string reason)
        {
            if (gameOver)
            {
                reason = "Match ended.";
                return false;
            }
            HeroState h = GetHero(team);
            int goldInt = Mathf.FloorToInt(h.goldExact);
            PurchasePlan plan = LaneItems.ResolvePurchase(itemId, h.items, goldInt);
            if (!plan.ValidItem)
            {
                reason = plan.Reason;
                return false;
            }

            bool inFountain = h.phase == LanePhase.Dead || map.IsInFountain(h.team, h.motor.Position);
            if (!inFountain)
            {
                reason = "Shop only open inside your Fountain or while respawning.";
                return false;
            }

            if (!plan.CanPurchase)
            {
                reason = plan.Reason;
                return false;
            }

            int targetSlot = -1;
            if (plan.ConsumedSlots.Count > 0)
            {
                targetSlot = plan.ConsumedSlots[0];
                for (int c = 0; c < plan.ConsumedSlots.Count; c++)
                {
                    h.items[plan.ConsumedSlots[c]] = 0;
                }
            }
            else
            {
                for (int i = 0; i < h.items.Length; i++)
                {
                    if (h.items[i] == 0)
                    {
                        targetSlot = i;
                        break;
                    }
                }
            }

            if (targetSlot < 0)
            {
                reason = "Inventory full (6/6).";
                return false;
            }

            h.items[targetSlot] = plan.TargetItem.Id;
            h.goldExact -= plan.RemainingCost;
            RecalcHeroStats(h, preserveHpRatio: true);
            PushEvent(LaneEventKind.Purchase, h.team, h.motor.Position, Vector2.up, 1f, plan.RemainingCost, plan.TargetItem.Name);
            reason = plan.ConsumedSlots.Count > 0 && plan.RemainingCost < plan.BaseCost
                ? $"Upgraded to {plan.TargetItem.Name} ({plan.RemainingCost}g)."
                : $"Bought {plan.TargetItem.Name}.";
            return true;
        }

        public bool TrySellItem(LaneTeam team, int slotIndex, out string reason)
        {
            if (gameOver)
            {
                reason = "Match ended.";
                return false;
            }
            HeroState h = GetHero(team);
            if (slotIndex < 0 || slotIndex >= h.items.Length)
            {
                reason = "Invalid inventory slot.";
                return false;
            }
            int itemId = h.items[slotIndex];
            if (itemId == 0 || !LaneItems.TryGet(itemId, out LaneItemDef item))
            {
                reason = "Slot is empty.";
                return false;
            }

            bool inFountain = h.phase == LanePhase.Dead || map.IsInFountain(h.team, h.motor.Position);
            if (!inFountain)
            {
                reason = "Items can only be sold inside your Fountain or while respawning.";
                return false;
            }

            int refund = LaneItems.SellRefund(item.Cost);
            h.items[slotIndex] = 0;
            h.goldExact += refund;
            RecalcHeroStats(h, preserveHpRatio: true);
            PushEvent(LaneEventKind.Purchase, h.team, h.motor.Position, Vector2.up, 1f, refund, $"Sold +{refund}g");
            reason = $"Sold {item.Name} (+{refund}g).";
            return true;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || gameOver) return;
            tickCount++;
            matchTime += dt;
            events.Clear();
            if (pendingOrderEvents.Count > 0)
            {
                events.AddRange(pendingOrderEvents);
                pendingOrderEvents.Clear();
            }
            laserBeams.Clear();

            inTick = true;
            try
            {
                if (bannerTimer > 0f)
                {
                    bannerTimer = Mathf.Max(0f, bannerTimer - dt);
                    if (bannerTimer <= 0f) bannerText = string.Empty;
                }

                TickWaveSpawns(dt);
                TickOrbs(dt);
                TickZones(dt);
                TickHeroes(dt);
                TickIllusions(dt);
                TickMinions(dt);
                TickTowers(dt);
                TickProjectiles(dt);
            }
            finally
            {
                inTick = false;
            }
        }

        private void ClearAttackIntent(HeroState h)
        {
            h.attackTargetKind = LaneTargetKind.None;
            h.attackTargetId = 0;
            h.isAttackMoving = false;
        }

        private void ClearAttackAndRoute(HeroState h)
        {
            ClearAttackIntent(h);
            h.motor.Stop();
        }

        private static bool IsBusyCasting(HeroState h)
        {
            return h.phase == LanePhase.Casting ||
                   h.phase == LanePhase.Channeling ||
                   h.phase == LanePhase.ComboSeq ||
                   h.phase == LanePhase.ExecuteDash;
        }

        private bool IsCcImmune(HeroState h)
        {
            if (h.heroId != LaneHeroId.Ogre) return false;
            if (h.phase == LanePhase.Casting || h.phase == LanePhase.Channeling || h.phase == LanePhase.ComboSeq)
            {
                return true;
            }
            return tuning.ogreImmuneDuringRecovery && h.phase == LanePhase.Recover && h.activeSlot != LaneAbilitySlot.None;
        }

        private void TryRouteHero(HeroState h, Vector2 destination)
        {
            MovementCourse course = ActiveCourse();
            Vector2 safePos = map.NearestWalkable(h.motor.Position, towers[0].alive, towers[1].alive);
            if ((safePos - h.motor.Position).sqrMagnitude > 0.0025f)
            {
                h.motor.SetPositionPreserveSpeed(safePos);
            }
            Vector2 safeDest = map.NearestWalkable(destination, towers[0].alive, towers[1].alive);
            h.motor.TrySetDestination(course, safeDest);
        }

        private bool TryAcquireTargetNear(HeroState h, Vector2 searchCenter, float radius, out LaneTargetKind kind, out int targetId)
        {
            float bestDistSq = radius * radius;
            kind = LaneTargetKind.None;
            targetId = 0;

            HeroState enemy = GetEnemyHero(h.team);
            if (IsHostileHeroTargetable(enemy))
            {
                float dSq = (enemy.motor.Position - searchCenter).sqrMagnitude;
                if (dSq <= bestDistSq)
                {
                    bestDistSq = dSq;
                    kind = LaneTargetKind.Hero;
                    targetId = enemy.id;
                }
            }

            if (kind != LaneTargetKind.Hero)
            {
                for (int i = 0; i < illusions.Count; i++)
                {
                    IllusionState ill = illusions[i];
                    if (ill.team == h.team || ill.hp <= 0f) continue;
                    float dSq = (ill.motor.Position - searchCenter).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        kind = LaneTargetKind.Hero;
                        targetId = ill.id;
                    }
                }
            }

            for (int i = 0; i < minions.Count; i++)
            {
                MinionState m = minions[i];
                if (m.team == h.team || m.hp <= 0f) continue;
                float dSq = (m.motor.Position - searchCenter).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    kind = LaneTargetKind.Minion;
                    targetId = m.id;
                }
            }

            TowerState enemyTower = GetEnemyTower(h.team);
            if (enemyTower.alive)
            {
                float effectiveDist = Mathf.Max(0f, Vector2.Distance(enemyTower.pos, searchCenter) - tuning.towerRadius);
                if (effectiveDist * effectiveDist < bestDistSq)
                {
                    kind = LaneTargetKind.Tower;
                    targetId = enemyTower.id;
                }
            }

            return kind != LaneTargetKind.None;
        }

        private bool IsHostileHeroTargetable(HeroState enemy)
        {
            return enemy != null && enemy.phase != LanePhase.Dead && enemy.phase != LanePhase.ExecuteDash && !enemy.invisible;
        }

        private bool IsValidHostileTarget(LaneTeam selfTeam, LaneTargetKind kind, int id, out Vector2 pos, out float radius)
        {
            pos = Vector2.zero;
            radius = 0.45f;

            switch (kind)
            {
                case LaneTargetKind.Hero:
                    HeroState enemy = GetEnemyHero(selfTeam);
                    if (enemy.id == id && IsHostileHeroTargetable(enemy))
                    {
                        pos = enemy.motor.Position;
                        radius = 0.52f;
                        return true;
                    }
                    // Also allow targeting active enemy illusions if matching ID
                    for (int i = 0; i < illusions.Count; i++)
                    {
                        if (illusions[i].id == id && illusions[i].team != selfTeam && illusions[i].hp > 0f)
                        {
                            pos = illusions[i].motor.Position;
                            radius = illusions[i].isLaserDecoy ? illusions[i].decoyRadius : 0.50f;
                            return true;
                        }
                    }
                    return false;

                case LaneTargetKind.Minion:
                    for (int i = 0; i < minions.Count; i++)
                    {
                        if (minions[i].id == id && minions[i].team != selfTeam && minions[i].hp > 0f)
                        {
                            pos = minions[i].motor.Position;
                            radius = 0.34f;
                            return true;
                        }
                    }
                    return false;

                case LaneTargetKind.Tower:
                    TowerState tower = GetEnemyTower(selfTeam);
                    if (tower.alive && tower.id == id)
                    {
                        pos = tower.pos;
                        radius = tuning.towerRadius;
                        return true;
                    }
                    return false;
            }
            return false;
        }

        private static Vector2 DirectionTo(Vector2 from, Vector2 to, Vector2 fallback)
        {
            Vector2 delta = to - from;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : (fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.right);
        }

        private static float AngleDegrees(Vector2 dir)
        {
            return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        }

        private void ApplyStunToHero(HeroState target, float duration, int attackerHeroId)
        {
            if (target.phase == LanePhase.Dead || target.phase == LanePhase.ExecuteDash) return;
            if (IsCcImmune(target))
            {
                PushEvent(LaneEventKind.CcBlocked, target.team, target.motor.Position, Vector2.up, 0.9f, 0f, "IMMUNE");
                return;
            }

            if (target.phase == LanePhase.Channeling)
            {
                InterruptChannel(target);
            }
            else if (target.phase == LanePhase.Windup)
            {
                target.phase = LanePhase.Idle;
                target.twinSecondPending = false;
            }
            else if (target.phase == LanePhase.Casting && target.heroId == LaneHeroId.LaserShooter && target.activeSlot == LaneAbilitySlot.E)
            {
                target.phase = LanePhase.Idle;
                target.activeSlot = LaneAbilitySlot.None;
                target.laserEStepPending = false;
            }

            target.stunTimer = Mathf.Max(target.stunTimer, duration);
            target.rStrikeInFollowthrough = false;
            target.motor.Stop();

            if (attackerHeroId > 0)
            {
                HeroState attacker = heroes[target.team == LaneTeam.Blue ? 1 : 0];
                if (attacker.id == attackerHeroId) TriggerColossusShield(attacker);
            }
        }

        private void ApplySlowToHero(HeroState target, float slowFraction, float duration)
        {
            if (target.phase == LanePhase.Dead || IsCcImmune(target)) return;
            float effectiveSlow = AdjustSlowForWeightlessness(target, slowFraction);
            target.slowAmount = Mathf.Max(target.slowAmount, effectiveSlow);
            target.slowTimer = Mathf.Max(target.slowTimer, duration);
        }

        private float AdjustSlowForWeightlessness(HeroState target, float slowFraction)
        {
            float clamped = Mathf.Clamp(slowFraction, 0f, 0.95f);
            if (target != null && target.HasPassive(ItemPassiveFlags.Weightlessness))
            {
                clamped *= (1f - Mathf.Clamp01(tuning.weightlessnessSlowReduction));
            }
            return clamped;
        }

        private void ComputeLaserWSlow(int totalShocks, int rankW, float elapsedSoFar, out float combinedSlow, out float remainingDuration)
        {
            float singleSlow = tuning.laserWSlowBase + tuning.laserWSlowPerRank * Mathf.Max(0, rankW - 1);
            if (totalShocks <= 1)
            {
                combinedSlow = singleSlow;
            }
            else
            {
                float extra = totalShocks - 1f;
                float hp = Mathf.Max(0.1f, tuning.laserWSlowOverlapHalfPoint);
                combinedSlow = singleSlow + (tuning.laserWSlowMaxCap - singleSlow) * (extra / (extra + hp));
            }
            float totalDur = tuning.laserWSlowDuration * Mathf.Sqrt(1f + tuning.laserWSlowDurSqrtK * Mathf.Max(0, totalShocks - 1));
            remainingDuration = Mathf.Max(0f, totalDur - Mathf.Max(0f, elapsedSoFar));
        }

        private void ApplyLaserWSlow(HeroState target, int hitsThisCast, int rankW)
        {
            if (target.phase == LanePhase.Dead || hitsThisCast <= 0) return;
            if (IsCcImmune(target))
            {
                PushEvent(LaneEventKind.CcBlocked, target.team, target.motor.Position, Vector2.up, 1.1f, 0f, "CC IMMUNE");
                return;
            }
            if (target.slowTimer <= 0f)
            {
                target.laserSlowOverlapCount = 0;
                target.laserSlowElapsed = 0f;
            }
            target.laserSlowOverlapCount += hitsThisCast;
            ComputeLaserWSlow(target.laserSlowOverlapCount, rankW, target.laserSlowElapsed, out float slowAmt, out float remDur);
            target.slowAmount = Mathf.Max(target.slowAmount, AdjustSlowForWeightlessness(target, slowAmt));
            target.slowTimer = Mathf.Max(target.slowTimer, remDur);
        }

        private void ApplyLaserWSlow(MinionState target, int hitsThisCast, int rankW)
        {
            if (target.hp <= 0f || hitsThisCast <= 0) return;
            if (target.slowTimer <= 0f)
            {
                target.laserSlowOverlapCount = 0;
                target.laserSlowElapsed = 0f;
            }
            target.laserSlowOverlapCount += hitsThisCast;
            ComputeLaserWSlow(target.laserSlowOverlapCount, rankW, target.laserSlowElapsed, out float slowAmt, out float remDur);
            target.slowAmount = Mathf.Max(target.slowAmount, slowAmt);
            target.slowTimer = Mathf.Max(target.slowTimer, remDur);
        }

        public void GrantXpForTest(LaneTeam team, float amount)
        {
            GrantHeroXp(GetHero(team), amount);
        }

        public int SpawnCustomMinion(LaneTeam team, Vector2 pos, bool ranged = false, float customMaxHp = -1f)
        {
            Vector2 safe = map.NearestWalkable(pos, towers[0].alive, towers[1].alive);
            float hp = customMaxHp > 0f ? customMaxHp : (ranged ? tuning.minionMaxHpRanged : tuning.minionMaxHpMelee);
            int id = AllocId();
            minions.Add(new MinionState
            {
                id = id,
                team = team,
                ranged = ranged,
                motor = new MovementMotor(safe, team == LaneTeam.Blue ? 90f : 270f),
                hp = hp,
                maxHp = hp,
            });
            return id;
        }

        private void ApplyDisplacementToHero(HeroState target, Vector2 dir, float distance, int attackerHeroId = 0)
        {
            if (target.phase == LanePhase.Dead || target.phase == LanePhase.ExecuteDash) return;
            if (IsCcImmune(target))
            {
                PushEvent(LaneEventKind.CcBlocked, target.team, target.motor.Position, Vector2.up, 0.9f, 0f, "IMMUNE");
                return;
            }

            if (target.phase == LanePhase.Channeling)
            {
                InterruptChannel(target);
            }

            if (attackerHeroId > 0)
            {
                HeroState attacker = heroes[target.team == LaneTeam.Blue ? 1 : 0];
                if (attacker.id == attackerHeroId) TriggerColossusShield(attacker);
            }

            if (target.HasPassive(ItemPassiveFlags.ImmovableMountain))
            {
                float resistFrac = Mathf.Clamp(tuning.immovableMountainFormula.Evaluate(1, target.strength), 0f, 0.90f);
                distance *= (1f - resistFrac);
            }

            Vector2 moved = map.PushOut(target.motor.Position, dir.normalized * distance, towers[0].alive, towers[1].alive);
            float heading = target.motor.HeadingDegrees;
            target.motor = new MovementMotor(moved, heading);
            target.prevMotorPos = moved;

            // Resolve open question: does displacement break Swordsman's second Twin-Blade strike if knocked out of reach?
            if (tuning.swordsmanDisplaceCancelsSecondHit &&
                target.heroId == LaneHeroId.Swordsman &&
                target.twinSecondPending)
            {
                if (IsValidHostileTarget(target.team, (LaneTargetKind)target.twinTargetKind, target.twinTargetId, out Vector2 tPos, out float tRad))
                {
                    if (Vector2.Distance(moved, tPos) > tuning.swordsmanBasicRange + tRad + tuning.basicMotionBuffer * 0.5f)
                    {
                        target.twinSecondPending = false;
                    }
                }
                else
                {
                    target.twinSecondPending = false;
                }
            }
        }

        private void TriggerColossusShield(HeroState h)
        {
            if (h == null || h.phase == LanePhase.Dead || !h.HasPassive(ItemPassiveFlags.TitanAegis) || h.colossusCd > 0f) return;
            float shield = tuning.colossusShieldFormula.Evaluate(1, h.strength);
            h.shieldHp = Mathf.Max(h.shieldHp, shield);
            h.shieldTimer = 4.0f;
            h.colossusCd = 12.0f;
            PushEvent(LaneEventKind.LevelUp, h.team, h.motor.Position, Vector2.up, 1.2f, shield, $"SHIELD +{Mathf.RoundToInt(shield)}");
        }

        private HeroState FindHeroById(int heroId)
        {
            if (heroId <= 0) return null;
            for (int i = 0; i < heroes.Length; i++)
            {
                if (heroes[i].id == heroId) return heroes[i];
            }
            return null;
        }

        private float GetAdaptiveScalesBonus(HeroState target, int attackerHeroId)
        {
            if (target == null || !target.HasPassive(ItemPassiveFlags.AdaptiveScales) || attackerHeroId <= 0) return 0f;
            for (int i = 0; i < target.adaptiveScales.Count; i++)
            {
                AdaptiveScalesEntry e = target.adaptiveScales[i];
                if (e.attackerHeroId == attackerHeroId && e.stacks > 0)
                {
                    float maxBonus = tuning.adaptiveScalesMaxResistFormula.Evaluate(1, target.strength);
                    return maxBonus * (e.stacks / (e.stacks + Mathf.Max(0.1f, tuning.adaptiveScalesStackHalfPoint)));
                }
            }
            return 0f;
        }

        private void AddOrRefreshAdaptiveScales(HeroState target, int attackerHeroId)
        {
            if (target == null || !target.HasPassive(ItemPassiveFlags.AdaptiveScales) || attackerHeroId <= 0) return;
            for (int i = 0; i < target.adaptiveScales.Count; i++)
            {
                if (target.adaptiveScales[i].attackerHeroId == attackerHeroId)
                {
                    target.adaptiveScales[i].stacks = Mathf.Min(tuning.adaptiveScalesMaxStacks, target.adaptiveScales[i].stacks + 1);
                    target.adaptiveScales[i].holdTimer = tuning.adaptiveScalesHoldDuration;
                    target.adaptiveScales[i].decayAccum = 0f;
                    return;
                }
            }
            target.adaptiveScales.Add(new AdaptiveScalesEntry
            {
                attackerHeroId = attackerHeroId,
                stacks = 1,
                holdTimer = tuning.adaptiveScalesHoldDuration,
                decayAccum = 0f,
            });
        }

        private void AddOrRefreshHeat(List<HeatEntry> list, int ownerHeroId)
        {
            if (ownerHeroId <= 0) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].ownerHeroId == ownerHeroId)
                {
                    list[i].stacks = Mathf.Min(tuning.draconicHeatMaxStacks, list[i].stacks + 1);
                    list[i].holdTimer = tuning.draconicHeatHoldDuration;
                    list[i].decayAccum = 0f;
                    return;
                }
            }
            list.Add(new HeatEntry
            {
                ownerHeroId = ownerHeroId,
                stacks = 1,
                holdTimer = tuning.draconicHeatHoldDuration,
                decayAccum = 0f,
                dotAccum = 0f,
            });
        }

        private float ComputeTotalHeatShred(List<HeatEntry> list)
        {
            float total = 0f;
            float hp = Mathf.Max(0.1f, tuning.draconicHeatHalfPoint);
            for (int i = 0; i < list.Count; i++)
            {
                HeatEntry e = list[i];
                if (e.stacks <= 0) continue;
                HeroState owner = FindHeroById(e.ownerHeroId);
                float intel = owner != null ? owner.intelligence : 0f;
                total += tuning.moltenEdgeShredFormula.Evaluate(1, intel) * (e.stacks / (e.stacks + hp));
            }
            return total;
        }

        public static bool IsPointInBananaCrescent(Vector2 candidate, Vector2 attackerPos, Vector2 primaryPos, float intelligence, LaneTuning t)
        {
            LaneTuning cfg = t ?? new LaneTuning();
            Vector2 delta = primaryPos - attackerPos;
            Vector2 fwd = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
            float outerR = cfg.bananaCrescentRadiusFormula.Evaluate(1, intelligence);
            Vector2 outerCenter = primaryPos + fwd * (outerR * 0.42f);
            float innerR = outerR * 0.78f;
            Vector2 innerCenter = primaryPos - fwd * (outerR * 0.20f);
            return (candidate - outerCenter).sqrMagnitude <= outerR * outerR &&
                   (candidate - innerCenter).sqrMagnitude > innerR * innerR;
        }

        private void TriggerBananaCrescent(HeroState attacker, LaneTargetKind primaryKind, int primaryId, Vector2 primaryPos)
        {
            if (attacker == null || !attacker.HasPassive(ItemPassiveFlags.CrescentStrike)) return;
            float coeff = tuning.bananaCrescentCoeffFormula.Evaluate(1, attacker.agility);
            float cleaveDmg = attacker.attackDamage * coeff;
            if (cleaveDmg <= 0f) return;

            Vector2 atkPos = attacker.motor.Position;
            Vector2 fwd = DirectionTo(atkPos, primaryPos, attacker.aimDir);
            float outerR = tuning.bananaCrescentRadiusFormula.Evaluate(1, attacker.intelligence);

            HeroState enemyHero = GetEnemyHero(attacker.team);
            if ((primaryKind != LaneTargetKind.Hero || enemyHero.id != primaryId) &&
                enemyHero.phase != LanePhase.Dead &&
                IsPointInBananaCrescent(enemyHero.motor.Position, atkPos, primaryPos, attacker.intelligence, tuning))
            {
                DealDamageToHero(enemyHero, cleaveDmg, isMagic: false, isTrueDamage: false, attacker.id, attacker.team,
                    triggerMiravelWard: false, flags: DamageFlags.None);
            }

            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                if (ill.team != attacker.team && ill.hp > 0f &&
                    (primaryKind != LaneTargetKind.Hero || ill.id != primaryId) &&
                    IsPointInBananaCrescent(ill.motor.Position, atkPos, primaryPos, attacker.intelligence, tuning))
                {
                    DealDamageToIllusion(ill, cleaveDmg, isMagic: false, attacker.id, attacker.team);
                }
            }

            for (int i = 0; i < minions.Count; i++)
            {
                MinionState m = minions[i];
                if (m.team != attacker.team && m.hp > 0f &&
                    (primaryKind != LaneTargetKind.Minion || m.id != primaryId) &&
                    IsPointInBananaCrescent(m.motor.Position, atkPos, primaryPos, attacker.intelligence, tuning))
                {
                    DealDamageToMinion(m, cleaveDmg, isMagic: false, attacker.id, attacker.team, isOnHit: false);
                }
            }

            PushEvent(LaneEventKind.BananaCrescent, attacker.team, primaryPos, fwd, outerR, cleaveDmg, string.Empty);
        }

        private void DealDamageToHero(
            HeroState target,
            float rawDamage,
            bool isMagic,
            bool isTrueDamage,
            int attackerHeroId,
            LaneTeam attackerTeam,
            bool triggerMiravelWard = true,
            DamageFlags flags = DamageFlags.None)
        {
            if (target.phase == LanePhase.Dead || target.phase == LanePhase.ExecuteDash || rawDamage <= 0f) return;

            HeroState attacker = FindHeroById(attackerHeroId);
            if (attacker != null && attacker.team == target.team) attacker = null;

            bool isDeferredPayout = (flags & DamageFlags.DeferredPayout) != 0;
            bool isDoT = (flags & DamageFlags.DamageOverTime) != 0;
            bool isBasicOrMinion = (flags & DamageFlags.BasicOrMinion) != 0;
            bool isOnHit = (flags & DamageFlags.OnHit) != 0;

            // 1. Wounded Strike / Final Judgment outgoing hero damage amp (superseded cleanly)
            if (attacker != null && !isDeferredPayout)
            {
                float missingFrac = 1f - Mathf.Clamp01(target.hp / Mathf.Max(1f, target.maxHp));
                if (attacker.HasPassive(ItemPassiveFlags.FinalJudgment))
                {
                    float maxAmp = tuning.finalJudgmentFormula.Evaluate(1, attacker.strength);
                    rawDamage *= (1f + maxAmp * missingFrac);
                }
                else if (attacker.HasPassive(ItemPassiveFlags.WoundedStrike))
                {
                    rawDamage *= (1f + tuning.bloodstoneMaxAmp * missingFrac);
                }
            }

            // 2. Resistance mitigation + per-attacker Adaptive Scales
            float adaptiveBonus = (!isTrueDamage && attackerHeroId > 0 && attackerTeam != target.team)
                ? GetAdaptiveScalesBonus(target, attackerHeroId)
                : 0f;
            float baseRes = (isMagic ? target.magicResist : target.armor) + adaptiveBonus;
            float mult = isTrueDamage ? 1f : ResistanceFactor(baseRes);
            float postRes = rawDamage * mult * target.damageTakenMult * (1f - target.damageReduction);
            if (isBasicOrMinion && target.HasPassive(ItemPassiveFlags.Sturdy) && postRes > 0f)
            {
                postRes = Mathf.Max(1f, postRes - tuning.brawlerBucklerBlock);
            }

            // 3. Shield absorption prior to Ancient Vitality deferral split (conserves overflow exactly)
            if (target.shieldHp > 0f && postRes > 0f)
            {
                float absorbed = Mathf.Min(target.shieldHp, postRes);
                target.shieldHp -= absorbed;
                postRes -= absorbed;
                if (target.shieldHp <= 0f) target.shieldTimer = 0f;
            }

            float immediateLoss = postRes;
            if (!isDeferredPayout && target.HasPassive(ItemPassiveFlags.AncientVitality) && postRes > 0f)
            {
                float deferFrac = Mathf.Clamp(tuning.ancientVitalityDeferFractionFormula.Evaluate(1, target.maxHp), 0f, 0.70f);
                float deferDur = Mathf.Max(0.5f, tuning.ancientVitalityDurationFormula.Evaluate(1, target.strength));
                float deferred = postRes * deferFrac;
                immediateLoss = postRes - deferred;
                if (deferred > 0.0001f)
                {
                    target.deferredDamage.Add(new DeferredDamageEntry
                    {
                        totalAmount = deferred,
                        paidSoFar = 0f,
                        duration = deferDur,
                        elapsed = 0f,
                        attackerHeroId = attackerHeroId,
                        attackerTeam = attackerTeam,
                    });
                }
            }

            target.hp = Mathf.Max(0f, target.hp - immediateLoss);

            if (attacker != null)
            {
                target.lastHeroAttackerId = attackerHeroId;
                target.lastHeroDamageAge = 0f;
                bool recalcTarget = false;

                if (target.momentumFactor > 0f || target.momentumBonus > 0f)
                {
                    target.momentumFactor = 0f;
                    target.momentumBonus = 0f;
                    recalcTarget = true;
                }

                if (attacker.HasPassive(ItemPassiveFlags.Wither))
                {
                    target.grievousTimer = Mathf.Max(target.grievousTimer, tuning.grievousDuration);
                }

                // Tower aggro: switch victim's allied tower onto the attacker if both are near tower protection
                TowerState allyTower = towers[target.team == LaneTeam.Blue ? 0 : 1];
                if (allyTower.alive && IsHostileHeroTargetable(attacker) &&
                    Vector2.Distance(attacker.motor.Position, allyTower.pos) <= tuning.towerAttackRange &&
                    Vector2.Distance(target.motor.Position, allyTower.pos) <= tuning.towerAttackRange + 2.5f)
                {
                    if (allyTower.targetKind != LaneTargetKind.Hero || allyTower.targetId != attacker.id)
                    {
                        allyTower.targetKind = LaneTargetKind.Hero;
                        allyTower.targetId = attacker.id;
                        allyTower.aggroLockedOnHero = true;
                        PushEvent(LaneEventKind.TowerAggro, allyTower.team, attacker.motor.Position, Vector2.up, 1.35f, 0f, "TOWER AGGRO!");
                    }
                }

                // Miravel E Ward check when enemy hero damages Miravel
                if (triggerMiravelWard && target.heroId == LaneHeroId.Miravel && target.wardTimer > 0f)
                {
                    ApplyMiravelCurse(attacker, target);
                }

                // Slippery Trickster: enemy hero damage burst on fixed cooldown (no AH scaling, no refresh on CD)
                if (!isDeferredPayout && target.hp > 0f && target.HasPassive(ItemPassiveFlags.SlipperyTrickster) && target.slipperyCd <= 0f)
                {
                    target.slipperyInitialBonus = tuning.slipperyTricksterBonusFormula.Evaluate(1, target.agility);
                    target.slipperyBonus = target.slipperyInitialBonus;
                    target.slipperyTimer = tuning.slipperyTricksterDuration;
                    target.slipperyCd = tuning.slipperyTricksterCooldown;
                    recalcTarget = true;
                }

                // On-hit triggers: Shared Heat (Draconic Blade) & Crescent Strike (Banana Blade)
                if (isOnHit)
                {
                    if (attacker.HasPassive(ItemPassiveFlags.DragonfireAndMoltenEdge) && target.hp > 0f)
                    {
                        AddOrRefreshHeat(target.heatEntries, attacker.id);
                        recalcTarget = true;
                    }
                    if (attacker.HasPassive(ItemPassiveFlags.CrescentStrike))
                    {
                        TriggerBananaCrescent(attacker, LaneTargetKind.Hero, target.id, target.motor.Position);
                    }
                }

                if (recalcTarget && target.hp > 0f)
                {
                    RecalcHeroStats(target, preserveHpRatio: true);
                }
            }

            // Adaptive Scales: build independent per-attacker stacks on direct physical/magical hero hits (never DoTs)
            if (!isTrueDamage && !isDoT && !isDeferredPayout && attackerHeroId > 0 && attackerTeam != target.team &&
                target.hp > 0f && target.HasPassive(ItemPassiveFlags.AdaptiveScales))
            {
                AddOrRefreshAdaptiveScales(target, attackerHeroId);
            }

            if (target.hp > 0f && target.HasPassive(ItemPassiveFlags.TitanAegis) && target.colossusCd <= 0f &&
                target.hp / Mathf.Max(1f, target.maxHp) < 0.45f)
            {
                TriggerColossusShield(target);
            }

            if (target.hp <= 0f)
            {
                OnHeroKilled(target, attackerHeroId, attackerTeam);
            }
        }

        private void DealDamageToMinion(MinionState m, float rawDamage, bool isMagic, int attackerHeroId, LaneTeam attackerTeam, bool isOnHit = false)
        {
            if (m.hp <= 0f || rawDamage <= 0f) return;
            HeroState attacker = FindHeroById(attackerHeroId);
            if (attacker != null && !isMagic && attacker.HasPassive(ItemPassiveFlags.Cleaver))
            {
                rawDamage += tuning.minerHatchetBonusDmg;
            }
            float moltenShred = ComputeTotalHeatShred(m.heatEntries);
            float res = (isMagic ? 0f : -m.swordShredStacks * tuning.swordsmanWStackShred) - moltenShred;
            res = Mathf.Max(-60f, res);
            float dmg = rawDamage * ResistanceFactor(res) * m.damageTakenMult;
            m.hp = Mathf.Max(0f, m.hp - dmg);

            if (isOnHit && attacker != null)
            {
                if (attacker.HasPassive(ItemPassiveFlags.DragonfireAndMoltenEdge) && m.hp > 0f)
                {
                    AddOrRefreshHeat(m.heatEntries, attacker.id);
                }
                if (attacker.HasPassive(ItemPassiveFlags.CrescentStrike))
                {
                    TriggerBananaCrescent(attacker, LaneTargetKind.Minion, m.id, m.motor.Position);
                }
            }

            if (m.hp <= 0f)
            {
                float gold = m.ranged ? tuning.minionGoldRanged : tuning.minionGoldMelee;
                float xp = m.ranged ? tuning.minionXpRanged : tuning.minionXpMelee;

                // Gold & CS strictly require a hero last hit
                HeroState enemyHero = GetEnemyHero(m.team);
                bool heroLastHit = attackerHeroId > 0 && attackerTeam != m.team && enemyHero.id == attackerHeroId;
                if (heroLastHit)
                {
                    enemyHero.goldExact += gold;
                    enemyHero.creepScore++;
                }

                // Full 100% XP is awarded to the opposing hero on last hit OR whenever alive within minionXpRadius
                if (heroLastHit ||
                    (enemyHero.phase != LanePhase.Dead &&
                     Vector2.Distance(enemyHero.motor.Position, m.motor.Position) <= tuning.minionXpRadius))
                {
                    GrantHeroXp(enemyHero, xp);
                }
            }
        }

        private void DealDamageToTower(TowerState tower, float rawDamage, int attackerHeroId, LaneTeam attackerTeam)
        {
            if (!tower.alive || rawDamage <= 0f) return;
            float dmg = rawDamage * ResistanceFactor(tuning.towerArmor);
            tower.hp = Mathf.Max(0f, tower.hp - dmg);

            if (tower.hp <= 0f)
            {
                tower.alive = false;
                winnerTeam = (int)attackerTeam;
                gameOver = true;
                bannerText = $"{(attackerTeam == LaneTeam.Blue ? "BLUE" : "RED")} DESTROYS THE OUTER TOWER — VICTORY!";
                bannerTimer = 10f;
            }
        }

        private void DealDamageToIllusion(IllusionState ill, float rawDamage, bool isMagic, int attackerHeroId, LaneTeam attackerTeam, int sourceMinionId = 0)
        {
            if (ill.hp <= 0f || rawDamage <= 0f) return;
            float dmg = rawDamage * ResistanceFactor(isMagic ? ill.magicResist : ill.armor);
            ill.hp = Mathf.Max(0f, ill.hp - dmg);

            HeroState owner = heroes[ill.team == LaneTeam.Blue ? 0 : 1];
            if (ill.isLaserDecoy && attackerTeam != ill.team)
            {
                int rankE = Mathf.Max(1, owner.rankE);
                float zapDmg = tuning.laserEDecoyDamageFormula.Evaluate(rankE, owner.intelligence);
                float stunDur = tuning.laserEStunFormula.Evaluate(rankE, 0f);

                if (attackerHeroId > 0)
                {
                    HeroState attacker = heroes[attackerTeam == LaneTeam.Blue ? 0 : 1];
                    if (attacker.id == attackerHeroId && attacker.phase != LanePhase.Dead)
                    {
                        float mult = LaserStackMultiplier(owner, attacker.laserStacks);
                        DealDamageToHero(attacker, zapDmg * mult, isMagic: true, isTrueDamage: false,
                            owner.id, owner.team, triggerMiravelWard: true);
                        ApplyLaserStackHero(attacker);
                        ApplyStunToHero(attacker, stunDur, owner.id);
                        PushEvent(LaneEventKind.LaserShock, owner.team, ill.motor.Position, Vector2.up, 1.3f, zapDmg, "DECOY ZAP!");
                    }
                    // Designer rule: if damaged by a hero, the decoy disappears
                    ill.hp = 0f;
                    ill.timeLeft = 0f;
                    return;
                }

                if (sourceMinionId > 0)
                {
                    for (int i = 0; i < minions.Count; i++)
                    {
                        if (minions[i].id == sourceMinionId && minions[i].hp > 0f)
                        {
                            float mult = LaserStackMultiplier(owner, minions[i].laserStacks);
                            DealDamageToMinion(minions[i], zapDmg * mult, true, owner.id, owner.team);
                            ApplyLaserStackMinion(minions[i]);
                            minions[i].stunTimer = Mathf.Max(minions[i].stunTimer, stunDur);
                            PushEvent(LaneEventKind.LaserShock, owner.team, minions[i].motor.Position, Vector2.up, 0.9f, zapDmg, string.Empty);
                            break;
                        }
                    }
                }
                return;
            }

            // Designer-confirmed: damaging Miravel's W illusion while E Ward is active curses the attacker!
            if (owner.wardTimer > 0f && attackerHeroId > 0 && attackerTeam != ill.team)
            {
                HeroState attacker = heroes[attackerTeam == LaneTeam.Blue ? 0 : 1];
                if (attacker.id == attackerHeroId)
                {
                    ApplyMiravelCurse(attacker, owner);
                }
            }
        }

        private float ComputeFountainOfLifeMult(HeroState target, HealSourceKind source)
        {
            if (target == null || source == HealSourceKind.LingeringRestoration || !target.HasPassive(ItemPassiveFlags.FountainOfLife))
            {
                return 1f;
            }
            float missingFrac = 1f - Mathf.Clamp01(target.hp / Mathf.Max(1f, target.maxHp));
            float maxAmp = tuning.fountainOfLifeFormula.Evaluate(1, Mathf.Max(0f, target.healPower - 1f));
            return 1f + maxAmp * missingFrac;
        }

        private void HealHero(HeroState target, float rawAmount, HealSourceKind source = HealSourceKind.AbilityOrItem)
        {
            if (target.phase == LanePhase.Dead || rawAmount <= 0f) return;
            float fountainMult = ComputeFountainOfLifeMult(target, source);
            float gMult = (source != HealSourceKind.LingeringRestoration && target.grievousTimer > 0f)
                ? (1f - tuning.grievousHealReduction)
                : 1f;
            float hpBefore = target.hp;
            target.hp = Mathf.Min(target.maxHp, target.hp + rawAmount * target.healReceivedMult * fountainMult * gMult);
            float actualRestored = Mathf.Max(0f, target.hp - hpBefore);

            if (source == HealSourceKind.AbilityOrItem && actualRestored > 0f && target.HasPassive(ItemPassiveFlags.LingeringRestoration))
            {
                target.deferredHeals.Add(new DeferredHealEntry
                {
                    totalAmount = actualRestored * tuning.lingeringRestorationShare,
                    paidSoFar = 0f,
                    duration = Mathf.Max(0.25f, tuning.lingeringRestorationDuration),
                    elapsed = 0f,
                });
            }
        }

        private void OnHeroKilled(HeroState victim, int attackerHeroId, LaneTeam attackerTeam)
        {
            if (victim.phase == LanePhase.Channeling)
            {
                InterruptChannel(victim);
            }
            victim.phase = LanePhase.Dead;
            victim.hp = 0f;
            victim.mana = 0f;
            victim.netManaRegen = 0f;
            victim.lowManaPopupCd = 0f;
            victim.deaths++;
            victim.respawnTimer = tuning.baseRespawnSeconds + (victim.level - 1) * tuning.respawnPerLevelSeconds;
            victim.invisible = false;
            victim.wardTimer = 0f;
            victim.stunTimer = 0f;
            victim.slowTimer = 0f;
            victim.slowAmount = 0f;
            victim.curseTimer = 0f;
            victim.curseStacks = 0;
            victim.twinSecondPending = false;
            victim.cowardShieldFactor = 0f;
            victim.cowardShieldBonus = 0f;
            victim.cowardRetreatStreak = 0f;
            victim.cowardGraceTimer = 0f;
            victim.cowardShieldState = 0;
            victim.shieldHp = 0f;
            victim.shieldTimer = 0f;
            victim.grievousTimer = 0f;
            victim.momentumFactor = 0f;
            victim.momentumBonus = 0f;
            victim.slipperyTimer = 0f;
            victim.slipperyInitialBonus = 0f;
            victim.slipperyBonus = 0f;
            victim.deferredDamage.Clear();
            victim.deferredHeals.Clear();
            victim.adaptiveScales.Clear();
            victim.heatEntries.Clear();
            victim.firstStrikeIdleTimer = tuning.firstStrikeInactivitySeconds;
            victim.rStrikeInFollowthrough = false;
            victim.miravelDrainAura = 0f;
            victim.miravelLifeAura = 0f;
            victim.netRegenRate = 0f;
            victim.lastHeroDamageAge = 999f;
            victim.laserStacks = 0;
            victim.laserStackTimer = 0f;
            victim.laserQCurRange = 0f;
            victim.laserQKillCount = 0;
            victim.laserQHitIds.Clear();
            victim.laserRProcAccum = 0f;
            victim.laserSlowOverlapCount = 0;
            victim.laserSlowElapsed = 0f;
            victim.laserEStepPending = false;
            victim.swordsmanLastStrikeHeavy = false;
            victim.rQueue.Clear();
            ClearAttackAndRoute(victim);

            HeroState killer = heroes[victim.team == LaneTeam.Blue ? 1 : 0];
            bool creditedKiller = attackerHeroId == killer.id || Vector2.Distance(killer.motor.Position, victim.motor.Position) <= 13f;
            if (creditedKiller)
            {
                killer.kills++;
                killer.goldExact += tuning.heroKillGold;
                GrantHeroXp(killer, tuning.heroKillXp);

                // Miravel Passive: permanent HealPower bonus on takedown!
                if (killer.heroId == LaneHeroId.Miravel)
                {
                    killer.takedownHealBonus += tuning.luisTakedownHealPowerGain;
                    RecalcHeroStats(killer, preserveHpRatio: true);
                }
            }

            string msg = $"{LaneNames.ShortName(killer.heroId)} SLAIN {LaneNames.ShortName(victim.heroId)}!";
            bannerText = msg;
            bannerTimer = 3.2f;
            PushEvent(LaneEventKind.KillBanner, killer.team, victim.motor.Position, Vector2.up, 1.5f, 0f, msg);
        }

        private void GrantHeroXp(HeroState h, float amount)
        {
            if (h.level >= tuning.maxLevel || amount <= 0f) return;
            h.xp += amount;
            while (h.level < tuning.maxLevel)
            {
                float need = XpThresholds[h.level];
                if (h.xp < need) break;
                h.xp -= need;
                h.level++;
                h.skillPoints++;
                RecalcHeroStats(h, preserveHpRatio: true);
                PushEvent(LaneEventKind.LevelUp, h.team, h.motor.Position, Vector2.up, 1.2f, h.level, $"LEVEL {h.level}");
            }
            if (h.level >= tuning.maxLevel) h.xp = 0f;
        }

        private int AllocId() => ++nextEntityId;

        private void PushEvent(LaneEventKind kind, LaneTeam team, Vector2 pos, Vector2 dir, float radius, float value, string label)
        {
            var ev = new VisualEventSnap
            {
                id = AllocId(),
                kind = kind,
                team = team,
                x = pos.x,
                y = pos.y,
                dirX = dir.x,
                dirY = dir.y,
                radius = radius,
                value = value,
                label = label ?? string.Empty,
            };
            events.Add(ev);
            if (!inTick)
            {
                pendingOrderEvents.Add(ev);
            }
        }

        private void PushLaserBeam(LaneTeam team, Vector2 from, Vector2 to, float width, bool isUlt)
        {
            if (laserBeams.Count >= 24) return;
            laserBeams.Add(new LaserBeamSnap
            {
                team = team,
                x0 = from.x,
                y0 = from.y,
                x1 = to.x,
                y1 = to.y,
                width = width,
                isUlt = isUlt,
            });
        }

        public LaneSnapshot Snapshot()
        {
            var snap = new LaneSnapshot
            {
                tick = tickCount,
                matchTime = matchTime,
                nextWaveIn = nextWaveTimer,
                waveNumber = waveNumber,
                gameOver = gameOver,
                winnerTeam = winnerTeam,
                banner = bannerText ?? string.Empty,
            };

            for (int i = 0; i < heroes.Length; i++)
            {
                HeroState h = heroes[i];
                float deferTotal = 0f;
                for (int d = 0; d < h.deferredDamage.Count; d++)
                {
                    deferTotal += Mathf.Max(0f, h.deferredDamage[d].totalAmount - h.deferredDamage[d].paidSoFar);
                }
                int maxHeatStacks = 0;
                float maxHeatTimer = 0f;
                for (int ht = 0; ht < h.heatEntries.Count; ht++)
                {
                    if (h.heatEntries[ht].stacks > maxHeatStacks) maxHeatStacks = h.heatEntries[ht].stacks;
                    if (h.heatEntries[ht].holdTimer > maxHeatTimer) maxHeatTimer = h.heatEntries[ht].holdTimer;
                }
                int maxScaleStacks = 0;
                for (int sc = 0; sc < h.adaptiveScales.Count; sc++)
                {
                    if (h.adaptiveScales[sc].stacks > maxScaleStacks) maxScaleStacks = h.adaptiveScales[sc].stacks;
                }
                float maxScaleBonus = maxScaleStacks > 0
                    ? tuning.adaptiveScalesMaxResistFormula.Evaluate(1, h.strength) * (maxScaleStacks / (maxScaleStacks + Mathf.Max(0.1f, tuning.adaptiveScalesStackHalfPoint)))
                    : 0f;

                var hs = new HeroSnap
                {
                    id = h.id,
                    team = h.team,
                    heroId = h.heroId,
                    level = h.level,
                    xp = h.xp,
                    xpToNext = h.level < tuning.maxLevel ? XpThresholds[h.level] : 0f,
                    skillPoints = h.skillPoints,
                    gold = Mathf.FloorToInt(h.goldExact),
                    kills = h.kills,
                    deaths = h.deaths,
                    creepScore = h.creepScore,
                    x = h.motor.Position.x,
                    y = h.motor.Position.y,
                    headingDeg = h.motor.HeadingDegrees,
                    speed = h.motor.Speed,
                    radius = 0.50f,
                    hp = h.hp,
                    maxHp = h.maxHp,
                    respawnTimer = h.respawnTimer,
                    invisible = h.invisible,
                    ccImmune = IsCcImmune(h),
                    attackDamage = h.attackDamage,
                    attackSpeed = h.attackSpeed,
                    bonusAttackSpeed = h.bonusAttackSpeed,
                    abilityHaste = h.abilityHaste,
                    cooldownReduction = h.cooldownReduction,
                    armor = h.armor,
                    magicResist = h.magicResist,
                    regenRate = h.regenRate,
                    netRegenRate = h.netRegenRate,
                    healPower = h.healPower,
                    moveSpeed = h.moveSpeed,
                    damageReduction = h.damageReduction,
                    cowardShieldBonus = h.cowardShieldBonus,
                    cowardShieldFactor = h.cowardShieldFactor,
                    cowardShieldState = h.cowardShieldState,
                    ogreAnimSpeed = h.ogreWindupSpeed,
                    ogreWindupSpeed = h.ogreWindupSpeed,
                    ogreRecoverSpeed = h.ogreRecoverSpeed,
                    ogreQCentralHalfWidth = h.heroId == LaneHeroId.Ogre ? tuning.ogreQWidth * h.scale : 0f,
                    ogreQOuterHalfWidth = h.heroId == LaneHeroId.Ogre ? tuning.ogreQOuterWidthFormula.Evaluate(Mathf.Max(1, h.rankQ), h.strength) : 0f,
                    ogreWRange = h.heroId == LaneHeroId.Ogre ? tuning.ogreWRange * h.scale : 0f,
                    ogreERadius = h.heroId == LaneHeroId.Ogre ? tuning.ogreERadiusFormula.Evaluate(Mathf.Max(1, h.rankE), h.strength) * h.scale : 0f,
                    miravelAuraRadius = h.heroId == LaneHeroId.Miravel ? tuning.luisAuraRadiusFormula.Evaluate(1, h.intelligence) : 0f,
                    strength = h.strength,
                    agility = h.agility,
                    intelligence = h.intelligence,
                    scale = h.scale,
                    phase = h.phase,
                    activeSlot = h.activeSlot,
                    phaseTimer = h.phaseTimer,
                    phaseDuration = h.phaseDuration,
                    aimDirX = h.aimDir.x,
                    aimDirY = h.aimDir.y,
                    castTargetX = h.castTarget.x,
                    castTargetY = h.castTarget.y,
                    rankQ = h.rankQ,
                    rankW = h.rankW,
                    rankE = h.rankE,
                    rankR = h.rankR,
                    cdQ = h.cdQ,
                    cdW = h.cdW,
                    cdE = h.cdE,
                    cdR = h.cdR,
                    maxCdQ = h.maxCdQ,
                    maxCdW = h.maxCdW,
                    maxCdE = h.maxCdE,
                    maxCdR = h.maxCdR,
                    stunTimer = h.stunTimer,
                    slowTimer = h.slowTimer,
                    slowAmount = h.slowAmount,
                    damageTakenMult = h.damageTakenMult,
                    healReceivedMult = h.healReceivedMult,
                    curseTimer = h.curseTimer,
                    curseStacks = h.curseStacks,
                    wardTimer = h.wardTimer,
                    orbCooldown = h.orbCooldown,
                    swordResistDelta = h.swordStacks * (tuning.swordsmanWStackGain + 2f * Mathf.Max(0, h.rankW - 1)) - h.swordDebuffStacks * tuning.swordsmanWStackShred,
                    swordStacks = h.swordStacks > 0 ? h.swordStacks : -h.swordDebuffStacks,
                    swordStackTimer = Mathf.Max(h.swordStackTimer, h.swordDebuffTimer),
                    swordMarkedTargetId = h.swordMarkTimer > 0f ? h.swordLastTargetId : 0,
                    swordMarkedTimer = h.swordMarkTimer,
                    attackTargetKind = (int)h.attackTargetKind,
                    attackTargetId = h.attackTargetId,
                    isAttackMoving = h.isAttackMoving,
                    rComboCount = h.rQueue.Count,
                    miravelDrainAura = h.miravelDrainAura,
                    miravelLifeAura = h.miravelLifeAura,
                    shieldHp = h.shieldHp,
                    shieldTimer = h.shieldTimer,
                    grievousTimer = h.grievousTimer,
                    momentumBonus = h.momentumBonus,
                    colossusCd = h.colossusCd,
                    deferredDamageTotal = deferTotal,
                    heatStacks = maxHeatStacks,
                    heatTimer = maxHeatTimer,
                    adaptiveScalesStacks = maxScaleStacks,
                    adaptiveScalesBonus = maxScaleBonus,
                    firstStrikeReady = h.HasPassive(ItemPassiveFlags.FirstStrike) && h.firstStrikeIdleTimer >= tuning.firstStrikeInactivitySeconds - 0.0001f,
                    firstStrikeIdle = h.firstStrikeIdleTimer,
                    slipperyBonus = h.slipperyBonus,
                    slipperyCd = h.slipperyCd,
                    rStrikeIndex = h.rStrikeIndex,
                    rStrikeIsFinal = h.phase == LanePhase.ComboSeq && h.rQueue.Count > 0 && h.rStrikeIndex == h.rQueue.Count - 1,
                    basicSwingAlt = h.basicSwingAlt,
                    isTwinSecondStrike = h.twinSecondPending || (h.phase == LanePhase.Recover && h.activeSlot == LaneAbilitySlot.None && h.swordsmanLastStrikeHeavy),
                    laserStacks = h.laserStacks,
                    laserStackTimer = h.laserStackTimer,
                    laserBeamRange = (h.heroId == LaneHeroId.LaserShooter &&
                        ((h.phase == LanePhase.Casting && h.activeSlot == LaneAbilitySlot.Q) ||
                         (h.phase == LanePhase.Channeling && h.activeSlot == LaneAbilitySlot.R)))
                        ? h.laserQCurRange
                        : 0f,
                    laserStepProgress = (h.heroId == LaneHeroId.LaserShooter && h.phase == LanePhase.Casting && h.activeSlot == LaneAbilitySlot.E && h.phaseDuration > 0.01f)
                        ? 1f - Mathf.Clamp01(h.phaseTimer / h.phaseDuration)
                        : 0f,
                    resourceKind = h.resourceKind,
                    mana = h.mana,
                    maxMana = h.maxMana,
                    manaRegen = h.manaRegen,
                    netManaRegen = h.netManaRegen,
                    rcrStat = h.rcrStat,
                    rcrPct = h.rcrPct,
                    costQ = EvaluateAbilityResourceCost(h, LaneAbilitySlot.Q),
                    costW = EvaluateAbilityResourceCost(h, LaneAbilitySlot.W),
                    costE = EvaluateAbilityResourceCost(h, LaneAbilitySlot.E),
                    costR = EvaluateAbilityResourceCost(h, LaneAbilitySlot.R),
                    costModeR = GetAbilityResourceSpec(h.heroId, LaneAbilitySlot.R, tuning).Mode,
                };
                for (int r = 0; r < h.rQueue.Count; r++)
                {
                    hs.rQueue.Add(new LaneQueuedStrikeSnap
                    {
                        slot = (int)h.rQueue[r].slot,
                        dirX = h.rQueue[r].dir.x,
                        dirY = h.rQueue[r].dir.y,
                    });
                }
                Array.Copy(h.items, hs.items, LaneItems.MaxSlots);
                snap.heroes.Add(hs);
            }

            for (int i = 0; i < towers.Length; i++)
            {
                TowerState t = towers[i];
                snap.towers.Add(new TowerSnap
                {
                    id = t.id,
                    team = t.team,
                    x = t.pos.x,
                    y = t.pos.y,
                    hp = t.hp,
                    maxHp = t.maxHp,
                    alive = t.alive,
                    attackRange = tuning.towerAttackRange,
                    targetKind = (int)t.targetKind,
                    targetId = t.targetId,
                    fireCooldown = t.fireCooldown,
                    aggroLockedOnHero = t.aggroLockedOnHero,
                });
            }

            for (int i = 0; i < minions.Count; i++)
            {
                MinionState m = minions[i];
                snap.minions.Add(new MinionSnap
                {
                    id = m.id,
                    team = m.team,
                    ranged = m.ranged,
                    x = m.motor.Position.x,
                    y = m.motor.Position.y,
                    headingDeg = m.motor.HeadingDegrees,
                    hp = m.hp,
                    maxHp = m.maxHp,
                    slowTimer = m.slowTimer,
                    stunTimer = m.stunTimer,
                    laserStacks = m.laserStacks,
                });
            }

            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                snap.illusions.Add(new IllusionSnap
                {
                    id = ill.id,
                    team = ill.team,
                    x = ill.motor.Position.x,
                    y = ill.motor.Position.y,
                    headingDeg = ill.motor.HeadingDegrees,
                    speed = ill.motor.Speed,
                    hp = ill.hp,
                    maxHp = ill.maxHp,
                    timeLeft = ill.timeLeft,
                    isLaserDecoy = ill.isLaserDecoy,
                    controllable = !ill.isLaserDecoy,
                    decoyRadius = ill.decoyRadius,
                    laserStacks = ill.laserStacks,
                });
            }

            for (int i = 0; i < projectiles.Count; i++)
            {
                ProjectileState p = projectiles[i];
                snap.projectiles.Add(new ProjectileSnap
                {
                    id = p.id,
                    team = p.team,
                    kind = p.kind,
                    x = p.pos.x,
                    y = p.pos.y,
                    targetX = p.lastKnownTarget.x,
                    targetY = p.lastKnownTarget.y,
                });
            }

            for (int i = 0; i < orbs.Count; i++)
            {
                OrbState o = orbs[i];
                snap.orbs.Add(new OrbSnap
                {
                    id = o.id,
                    ownerTeam = o.ownerTeam,
                    x = o.pos.x,
                    y = o.pos.y,
                    timeLeft = o.timeLeft,
                    duration = o.duration,
                });
            }

            for (int i = 0; i < zones.Count; i++)
            {
                ZoneState z = zones[i];
                snap.zones.Add(new ZoneSnap
                {
                    team = z.team,
                    x = z.pos.x,
                    y = z.pos.y,
                    radius = z.radius,
                    intensity = Mathf.Clamp01(z.elapsed / Mathf.Max(0.25f, z.duration)),
                    timeLeft = Mathf.Max(0f, z.duration - z.elapsed),
                });
            }

            for (int i = 0; i < events.Count; i++)
            {
                snap.events.Add(events[i]);
            }

            for (int i = 0; i < laserBeams.Count; i++)
            {
                snap.beams.Add(laserBeams[i]);
            }

            return snap;
        }

        public static HeroResourceKind GetHeroResourceKind(LaneHeroId heroId)
        {
            return (heroId == LaneHeroId.Miravel || heroId == LaneHeroId.LaserShooter)
                ? HeroResourceKind.Mana
                : HeroResourceKind.None;
        }

        public static AbilityResourceSpec GetAbilityResourceSpec(LaneHeroId heroId, LaneAbilitySlot slot, LaneTuning t)
        {
            if (t == null) t = new LaneTuning();
            if (heroId == LaneHeroId.Miravel)
            {
                switch (slot)
                {
                    case LaneAbilitySlot.W:
                        return new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.Flat, t.miravelWManaBase, t.miravelWManaPerRank, usesCostReduction: true);
                    case LaneAbilitySlot.E:
                        return new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.Flat, t.miravelEManaBase, t.miravelEManaPerRank, usesCostReduction: true);
                    case LaneAbilitySlot.R:
                        return new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.ContinuousPerSecond, t.miravelRManaPerSecBase, t.miravelRManaPerSecPerRank, usesCostReduction: true);
                    default:
                        return new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.None, 0f, 0f, usesCostReduction: false);
                }
            }
            if (heroId == LaneHeroId.LaserShooter)
            {
                switch (slot)
                {
                    case LaneAbilitySlot.Q:
                        return new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.Flat, t.laserQManaBase, t.laserQManaPerRank, usesCostReduction: true);
                    case LaneAbilitySlot.W:
                        return new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.Flat, t.laserWManaBase, t.laserWManaPerRank, usesCostReduction: true);
                    case LaneAbilitySlot.E:
                        return new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.Flat, t.laserEManaFlat, 0f, usesCostReduction: true);
                    case LaneAbilitySlot.R:
                        return new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.Flat, t.laserRManaUpfront, 0f, usesCostReduction: true);
                    default:
                        return new AbilityResourceSpec(HeroResourceKind.Mana, ResourceCostMode.None, 0f, 0f, usesCostReduction: false);
                }
            }
            return default;
        }

        private float EvaluateAbilityResourceCost(HeroState h, LaneAbilitySlot slot)
        {
            if (h == null || h.resourceKind == HeroResourceKind.None) return 0f;
            AbilityResourceSpec spec = GetAbilityResourceSpec(h.heroId, slot, tuning);
            if (spec.Mode == ResourceCostMode.None) return 0f;
            int rank = slot == LaneAbilitySlot.Q ? h.rankQ
                : (slot == LaneAbilitySlot.W ? h.rankW
                : (slot == LaneAbilitySlot.E ? h.rankE : h.rankR));
            if (rank <= 0) rank = 1;
            return spec.EvaluateEffectiveCost(rank, h.maxMana, h.mana, h.rcrPct);
        }

        public float ComputeEffectiveAbilityCost(LaneTeam team, LaneAbilitySlot slot)
        {
            return EvaluateAbilityResourceCost(GetHero(team), slot);
        }

        public void SetHeroManaForTest(LaneTeam team, float manaAmount)
        {
            HeroState h = GetHero(team);
            h.mana = h.resourceKind == HeroResourceKind.Mana ? Mathf.Clamp(manaAmount, 0f, h.maxMana) : 0f;
        }

        public void SetHeroGoldForTest(LaneTeam team, int gold)
        {
            HeroState h = GetHero(team);
            h.goldExact = Mathf.Max(0, gold);
        }

        public void SetHeroHpForTest(LaneTeam team, float hpAmount)
        {
            HeroState h = GetHero(team);
            if (h.phase == LanePhase.Dead) return;
            h.hp = Mathf.Clamp(hpAmount, 1f, h.maxHp);
        }

        public void ApplyDamageForTest(LaneTeam targetTeam, float rawDamage, bool isMagic, bool isTrueDamage, LaneTeam attackerTeam, DamageFlags flags = DamageFlags.None)
        {
            HeroState target = GetHero(targetTeam);
            HeroState attacker = GetHero(attackerTeam);
            DealDamageToHero(target, rawDamage, isMagic, isTrueDamage, attacker.id, attacker.team, triggerMiravelWard: false, flags: flags);
        }

        public void ApplyDamageFromAttackerIdForTest(LaneTeam targetTeam, int attackerId, float rawDamage, bool isMagic, LaneTeam attackerTeam, DamageFlags flags = DamageFlags.None)
        {
            HeroState target = GetHero(targetTeam);
            DealDamageToHero(target, rawDamage, isMagic, false, attackerId, attackerTeam, triggerMiravelWard: false, flags: flags);
        }

        public float ApplyDisplacementForTest(LaneTeam targetTeam, Vector2 dir, float distance, LaneTeam attackerTeam)
        {
            HeroState target = GetHero(targetTeam);
            HeroState attacker = GetHero(attackerTeam);
            Vector2 before = target.motor.Position;
            ApplyDisplacementToHero(target, dir, distance, attacker.id);
            return Vector2.Distance(before, target.motor.Position);
        }

        public bool TryConsumeResourceSpec(LaneTeam team, AbilityResourceSpec spec, int rank, out float spent)
        {
            HeroState h = GetHero(team);
            spent = 0f;
            if (h.resourceKind != spec.ResourceKind || spec.ResourceKind == HeroResourceKind.None || spec.Mode == ResourceCostMode.None)
            {
                return true;
            }
            float cost = spec.EvaluateEffectiveCost(Mathf.Max(1, rank), h.maxMana, h.mana, h.rcrPct);
            if (spec.Mode == ResourceCostMode.ContinuousPerSecond)
            {
                float minRequired = Mathf.Max(1f, cost * tuning.miravelRMinStartFraction);
                if (h.mana + 0.0001f < minRequired)
                {
                    EmitInsufficientManaFeedback(h);
                    return false;
                }
                return true;
            }
            if (h.mana + 0.0001f < cost)
            {
                EmitInsufficientManaFeedback(h);
                return false;
            }
            spent = Mathf.Min(h.mana, cost);
            h.mana = Mathf.Max(0f, h.mana - spent);
            if (spent > 0f && spec.Mode == ResourceCostMode.Flat && h.HasPassive(ItemPassiveFlags.FrugalFocus))
            {
                h.mana = Mathf.Min(h.maxMana, h.mana + spent * tuning.frugalFocusRefundRatio);
            }
            return true;
        }

        private bool TryConsumeHeroAbilityResource(HeroState h, LaneAbilitySlot slot)
        {
            if (h.resourceKind == HeroResourceKind.None) return true;
            AbilityResourceSpec spec = GetAbilityResourceSpec(h.heroId, slot, tuning);
            int rank = slot == LaneAbilitySlot.Q ? h.rankQ
                : (slot == LaneAbilitySlot.W ? h.rankW
                : (slot == LaneAbilitySlot.E ? h.rankE : h.rankR));
            return TryConsumeResourceSpec(h.team, spec, Mathf.Max(1, rank), out _);
        }

        private void EmitInsufficientManaFeedback(HeroState h)
        {
            if (h.lowManaPopupCd > 0f) return;
            h.lowManaPopupCd = 0.85f;
            PushEvent(LaneEventKind.InsufficientMana, h.team, h.motor.Position, Vector2.up, 0.9f, h.mana, "Not enough Mana");
        }
    }
}
