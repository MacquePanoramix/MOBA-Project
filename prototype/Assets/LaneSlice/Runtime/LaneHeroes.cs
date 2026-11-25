using Moba.MovementLab;
using UnityEngine;

namespace Moba.LaneSlice
{
    public sealed partial class LaneMatch
    {
        private HeroState CreateHero(int id, LaneTeam team, LaneHeroId heroId, Vector2 pos, float headingDeg)
        {
            var h = new HeroState
            {
                id = id,
                team = team,
                heroId = heroId,
                motor = new MovementMotor(pos, headingDeg),
                goldExact = tuning.startingGold,
                aimDir = team == LaneTeam.Blue ? Vector2.right : Vector2.left,
            };
            RecalcHeroStats(h, preserveHpRatio: false);
            h.hp = h.maxHp;
            return h;
        }

        private float GetBaseBasicRange(LaneHeroId id)
        {
            switch (id)
            {
                case LaneHeroId.Miravel: return tuning.luisBasicRange;
                case LaneHeroId.Swordsman: return tuning.swordsmanBasicRange;
                case LaneHeroId.LaserShooter: return tuning.laserBasicRange;
                default: return 2.2f; // Ogre cannot basic attack
            }
        }

        private float ApplyCdr(HeroState h, float baseCd)
        {
            return Mathf.Max(0.4f, baseCd * CooldownFactor(h.abilityHaste));
        }

        private void RecalcHeroStats(HeroState h, bool preserveHpRatio)
        {
            float hpRatio = (preserveHpRatio && h.maxHp > 0f) ? Mathf.Clamp01(h.hp / h.maxHp) : 1f;

            float baseHp, hpPerLvl, baseAd, adPerLvl, baseArmor, baseMr, baseRegen, baseSpeed, turnRate;
            switch (h.heroId)
            {
                case LaneHeroId.Ogre:
                    baseHp = 720f; hpPerLvl = 48f; baseAd = 60f; adPerLvl = 5.0f;
                    baseArmor = 16f; baseMr = 12f; baseRegen = 1.6f;
                    baseSpeed = tuning.ogreSpeed; turnRate = tuning.ogreTurnRate;
                    break;
                case LaneHeroId.Miravel:
                    baseHp = 530f; hpPerLvl = 34f; baseAd = 48f; adPerLvl = 3.5f;
                    baseArmor = 8f; baseMr = 18f; baseRegen = 1.4f;
                    baseSpeed = tuning.luisSpeed; turnRate = tuning.luisTurnRate;
                    break;
                case LaneHeroId.LaserShooter:
                    baseHp = 480f; hpPerLvl = 30f; baseAd = 46f; adPerLvl = 3.5f;
                    baseArmor = 8f; baseMr = 10f; baseRegen = 1.3f;
                    baseSpeed = tuning.laserSpeed; turnRate = tuning.laserTurnRate;
                    break;
                default: // Old Man Swordsman
                    baseHp = 615f; hpPerLvl = 40f; baseAd = 54f; adPerLvl = 4.0f;
                    baseArmor = 14f; baseMr = 14f; baseRegen = 1.5f;
                    baseSpeed = tuning.swordsmanSpeed; turnRate = tuning.swordsmanTurnRate;
                    break;
            }

            float itemHp = 0f, itemAd = 0f, itemAs = 0f, itemAh = 0f, itemSpeed = 0f, itemArmor = 0f, itemMr = 0f, itemRegen = 0f;
            float itemStr = 0f, itemAgi = 0f, itemInt = 0f, itemHeal = 0f, itemAuraRegen = 0f;
            bool cowardOwned = false, hatchet = false, salve = false, buckler = false, thorn = false;
            bool greaves = false, colossus = false, spellweaver = false, grudge = false;

            for (int i = 0; i < h.items.Length; i++)
            {
                if (h.items[i] == 0 || !LaneItems.TryGet(h.items[i], out LaneItemDef item)) continue;
                itemHp += item.AddMaxHp;
                itemAd += item.AddAttackDamage;
                itemAs += item.AddAttackSpeed;
                itemAh += item.AddAbilityHaste;
                itemSpeed += item.AddMoveSpeed;
                itemArmor += item.AddArmor;
                itemMr += item.AddMagicResist;
                itemRegen += item.AddRegen;
                itemStr += item.AddStrength;
                itemAgi += item.AddAgility;
                itemInt += item.AddIntelligence;
                itemHeal += item.AddHealPower;
                itemAuraRegen += item.AddAlliedAuraRegen;
                if (item.IsCowardsShield) cowardOwned = true;
                if (item.IsMinionHatchet) hatchet = true;
                if (item.IsWoodlandSalve) salve = true;
                if (item.IsBrawlerBuckler) buckler = true;
                if (item.IsBlightedThorn) thorn = true;
                if (item.IsMomentumGreaves) greaves = true;
                if (item.IsColossusPauldron) colossus = true;
                if (item.IsSpellweaverHourglass) spellweaver = true;
                if (item.IsExecutionerGrudge) grudge = true;
            }

            int lvlOffset = h.level - 1;
            // Core rule (01_CORE_SYSTEMS): Str/Agi/Int start at 0 (LoL AP model) and confer ZERO inherent universal stats.
            h.strength = itemStr;
            h.agility = itemAgi;
            h.intelligence = itemInt;
            h.hasHatchet = hatchet;
            h.hasSalve = salve;
            h.hasBuckler = buckler;
            h.hasBlightedThorn = thorn;
            h.hasMomentumGreaves = greaves;
            h.hasColossusPauldron = colossus;
            h.hasSpellweaverHourglass = spellweaver;
            h.hasExecutionerGrudge = grudge;
            if (!greaves)
            {
                h.momentumFactor = 0f;
                h.momentumBonus = 0f;
            }
            if (!colossus)
            {
                h.shieldHp = 0f;
                h.shieldTimer = 0f;
            }
            if (!spellweaver)
            {
                h.spellweaverReadyTimer = 0f;
            }
            h.hasCowardsShield = cowardOwned;
            if (!cowardOwned)
            {
                h.cowardShieldFactor = 0f;
                h.cowardShieldBonus = 0f;
            }
            else
            {
                h.cowardShieldBonus = tuning.cowardShieldResistFormula.Evaluate(1, h.agility) * h.cowardShieldFactor;
            }

            float swordBonus = h.swordStacks * (6f + 2f * Mathf.Max(1, h.rankE));
            float swordShred = h.swordDebuffStacks * 8f;

            h.maxHp = baseHp + hpPerLvl * lvlOffset + itemHp;
            h.attackDamage = baseAd + adPerLvl * lvlOffset + itemAd;
            h.attackSpeed = Mathf.Max(0.4f, 1.0f + itemAs);
            h.abilityHaste = Mathf.Max(0f, itemAh);
            h.cooldownReduction = h.abilityHaste / (100f + h.abilityHaste);
            h.armor = baseArmor + itemArmor + swordBonus - swordShred + h.cowardShieldBonus;
            h.magicResist = baseMr + itemMr + swordBonus - swordShred + h.cowardShieldBonus;
            h.regenRate = baseRegen + itemRegen;
            h.healPower = 1f + itemHeal + h.takedownHealBonus;
            h.itemAlliedAuraRegen = itemAuraRegen;
            h.moveSpeed = baseSpeed + itemSpeed + h.momentumBonus;
            h.turnRate = turnRate;
            h.ogreAnimSpeed = h.heroId == LaneHeroId.Ogre
                ? Mathf.Max(0.4f, tuning.ogreAnimSpeedFormula.Evaluate(1, h.agility) + (h.attackSpeed - 1f) * tuning.ogreAttackSpeedAnimRatio)
                : 1f;

            if (preserveHpRatio)
            {
                h.hp = Mathf.Clamp(h.maxHp * hpRatio, 1f, h.maxHp);
            }
        }

        private void TickHeroes(float dt)
        {
            for (int i = 0; i < heroes.Length; i++)
            {
                TickSingleHero(heroes[i], dt);
            }
        }

        private void TickSingleHero(HeroState h, float dt)
        {
            h.goldExact += tuning.passiveGoldPerSecond * dt;
            h.cdQ = Mathf.Max(0f, h.cdQ - dt);
            h.cdW = Mathf.Max(0f, h.cdW - dt);
            h.cdE = Mathf.Max(0f, h.cdE - dt);
            h.cdR = Mathf.Max(0f, h.cdR - dt);

            if (h.phase == LanePhase.Dead)
            {
                h.respawnTimer = Mathf.Max(0f, h.respawnTimer - dt);
                if (h.respawnTimer <= 0f)
                {
                    Vector2 spawn = map.SpawnPos(h.team);
                    h.motor = new MovementMotor(spawn, h.team == LaneTeam.Blue ? 90f : 270f);
                    RecalcHeroStats(h, preserveHpRatio: false);
                    h.hp = h.maxHp;
                    h.phase = LanePhase.Idle;
                    h.activeSlot = LaneAbilitySlot.None;
                    h.scale = 1f;
                    h.damageReduction = 0f;
                    h.damageTakenMult = 1f;
                    h.healReceivedMult = 1f;
                }
                return;
            }

            h.damageTakenMult = 1f;
            h.healReceivedMult = 1f;
            ReapplyActiveZonesOnHero(h);

            // The Coward's Shield, Momentum Greaves, Shield, Spellweaver, and Grievous Wounds timers
            h.lastHeroDamageAge += dt;
            if (h.hasCowardsShield)
            {
                UpdateCowardsShield(h, dt);
            }
            if (h.colossusCd > 0f) h.colossusCd = Mathf.Max(0f, h.colossusCd - dt);
            if (h.shieldTimer > 0f)
            {
                h.shieldTimer = Mathf.Max(0f, h.shieldTimer - dt);
                if (h.shieldTimer <= 0f) h.shieldHp = 0f;
            }
            if (h.spellweaverCd > 0f) h.spellweaverCd = Mathf.Max(0f, h.spellweaverCd - dt);
            if (h.spellweaverReadyTimer > 0f) h.spellweaverReadyTimer = Mathf.Max(0f, h.spellweaverReadyTimer - dt);
            if (h.grievousTimer > 0f) h.grievousTimer = Mathf.Max(0f, h.grievousTimer - dt);

            bool momentumChanged = false;
            if (h.hasMomentumGreaves)
            {
                float targetM = (h.motor.IsMoving && h.lastHeroDamageAge > 2.0f)
                    ? Mathf.Min(1f, h.momentumFactor + dt / 3.0f)
                    : Mathf.Max(0f, h.momentumFactor - dt / 1.5f);
                if (Mathf.Abs(targetM - h.momentumFactor) > 0.0001f)
                {
                    h.momentumFactor = targetM;
                    float nextBonus = tuning.momentumGreavesFormula.Evaluate(1, h.agility) * h.momentumFactor;
                    if (Mathf.Abs(nextBonus - h.momentumBonus) > 0.02f) momentumChanged = true;
                    h.momentumBonus = nextBonus;
                }
            }

            if (h.invisTimer > 0f)
            {
                h.invisTimer = Mathf.Max(0f, h.invisTimer - dt);
                if (h.invisTimer <= 0f) h.invisible = false;
            }

            if (h.wardTimer > 0f) h.wardTimer = Mathf.Max(0f, h.wardTimer - dt);
            if (h.stunTimer > 0f) h.stunTimer = Mathf.Max(0f, h.stunTimer - dt);
            if (h.slowTimer > 0f)
            {
                h.slowTimer = Mathf.Max(0f, h.slowTimer - dt);
                if (h.slowTimer <= 0f) h.slowAmount = 0f;
            }
            if (h.swordMarkTimer > 0f) h.swordMarkTimer = Mathf.Max(0f, h.swordMarkTimer - dt);
            if (h.laserStackTimer > 0f)
            {
                h.laserStackTimer -= dt;
                if (h.laserStackTimer <= 0f)
                {
                    h.laserStackTimer = 0f;
                    h.laserStacks = 0;
                }
            }

            bool recalcNeeded = false;
            if (h.swordStackTimer > 0f)
            {
                h.swordStackTimer -= dt;
                if (h.swordStackTimer <= 0f)
                {
                    h.swordStackTimer = 0f;
                    h.swordStacks = 0;
                    recalcNeeded = true;
                }
            }
            if (h.swordDebuffTimer > 0f)
            {
                h.swordDebuffTimer -= dt;
                if (h.swordDebuffTimer <= 0f)
                {
                    h.swordDebuffTimer = 0f;
                    h.swordDebuffStacks = 0;
                    recalcNeeded = true;
                }
            }
            if (recalcNeeded || momentumChanged) RecalcHeroStats(h, preserveHpRatio: true);

            // Miravel E Curse tick
            if (h.curseTimer > 0f)
            {
                h.curseTimer = Mathf.Max(0f, h.curseTimer - dt);
                h.curseTickAccum += dt;
                if (h.curseTickAccum >= 0.5f)
                {
                    h.curseTickAccum -= 0.5f;
                    HeroState source = h.curseSourceHeroId == heroes[0].id ? heroes[0] : heroes[1];
                    int rankE = Mathf.Max(1, source.rankE);
                    float baseDps = tuning.luisCurseDpsFormula.Evaluate(rankE, source.intelligence);
                    float stackMult = 1f + 0.35f * Mathf.Max(0, h.curseStacks - 1);
                    DealDamageToHero(h, baseDps * 0.5f * stackMult, isMagic: true, isTrueDamage: false,
                        source.id, source.team, triggerMiravelWard: false);
                    PushEvent(LaneEventKind.LuisCurseProc, source.team, h.motor.Position, Vector2.up, 0.7f, h.curseStacks, string.Empty);
                    if (h.phase == LanePhase.Dead) return;
                }
                if (h.curseTimer <= 0f) h.curseStacks = 0;
            }

            // Passive health regen + Miravel aura + Woodland Salve + Grievous Wounds + Fountain heal
            float allyAura = 0f;
            float enemyPenalty = 0f;
            for (int j = 0; j < heroes.Length; j++)
            {
                HeroState other = heroes[j];
                if (other.heroId != LaneHeroId.Miravel || other.phase == LanePhase.Dead) continue;
                float d = Vector2.Distance(h.motor.Position, other.motor.Position);
                if (d > tuning.luisAuraRadius) continue;

                if (other.team == h.team)
                {
                    allyAura += (tuning.luisAllyAuraRegenBase + 0.35f * other.level + other.itemAlliedAuraRegen) * other.healPower;
                }
                else
                {
                    enemyPenalty += tuning.luisEnemyAuraRegenPenaltyBase + 0.35f * other.level;
                }
            }

            h.miravelLifeAura = allyAura;
            h.miravelDrainAura = enemyPenalty;

            float salveRegen = (h.hasSalve && h.lastHeroDamageAge >= tuning.woodlandSalveOutOfCombat) ? tuning.woodlandSalveRegen : 0f;
            float grievousMult = h.grievousTimer > 0f ? (1f - tuning.grievousHealReduction) : 1f;
            float effectiveNaturalRegen = Mathf.Max(0f, h.regenRate + allyAura + salveRegen - enemyPenalty) * grievousMult;
            if (effectiveNaturalRegen > 0f)
            {
                h.hp = Mathf.Min(h.maxHp, h.hp + effectiveNaturalRegen * dt);
            }
            if (map.IsInFountain(h.team, h.motor.Position))
            {
                HealHero(h, tuning.fountainHealPerSec * dt);
            }

            // Miravel Q Passive Orb Spawner
            if (h.heroId == LaneHeroId.Miravel && h.rankQ >= 1)
            {
                h.orbCooldown -= dt;
                if (h.orbCooldown <= 0f)
                {
                    SpawnLuisOrb(h);
                    h.orbCooldown = Mathf.Max(4.0f, tuning.luisOrbInterval - 0.8f * (h.rankQ - 1));
                }
            }

            if (h.bufferedOrder.HasValue)
            {
                h.bufferedOrderTimer -= dt;
                if (h.bufferedOrderTimer <= 0f) h.bufferedOrder = null;
            }

            switch (h.phase)
            {
                case LanePhase.Windup:
                    TickBasicWindup(h, dt);
                    break;
                case LanePhase.Casting:
                    TickAbilityCasting(h, dt);
                    break;
                case LanePhase.Channeling:
                    TickHeroChannel(h, dt);
                    break;
                case LanePhase.ComboSeq:
                    TickOgreComboSequence(h, dt);
                    break;
                case LanePhase.ExecuteDash:
                    TickSwordsmanDash(h, dt);
                    break;
                case LanePhase.Recover:
                    h.phaseTimer -= dt;
                    if (h.phaseTimer <= 0f)
                    {
                        h.phase = LanePhase.Idle;
                        h.activeSlot = LaneAbilitySlot.None;
                        if (h.bufferedOrder.HasValue && (h.stunTimer <= 0f || IsCcImmune(h)))
                        {
                            LaneOrder ord = h.bufferedOrder.Value;
                            h.bufferedOrder = null;
                            ApplyOrderImmediate(h, ord);
                            return;
                        }
                    }
                    break;
            }

            if (h.stunTimer > 0f && !IsCcImmune(h)) return;

            if (h.phase == LanePhase.Idle)
            {
                if (h.bufferedOrder.HasValue)
                {
                    LaneOrder ord = h.bufferedOrder.Value;
                    h.bufferedOrder = null;
                    ApplyOrderImmediate(h, ord);
                    if (h.phase != LanePhase.Idle) return;
                }

                TickIdleMovementAndAcquire(h, dt);
            }
        }

        private void UpdateCowardsShield(HeroState h, float dt)
        {
            float oldBonus = h.cowardShieldBonus;
            if (h.lastHeroDamageAge <= tuning.cowardShieldMemorySeconds && h.lastHeroAttackerId > 0)
            {
                HeroState attacker = h.lastHeroAttackerId == heroes[0].id ? heroes[0] : heroes[1];
                if (attacker.id == h.lastHeroAttackerId && attacker.phase != LanePhase.Dead)
                {
                    Vector2 toAttacker = attacker.motor.Position - h.motor.Position;
                    if (toAttacker.sqrMagnitude > 0.01f)
                    {
                        float rad = h.motor.HeadingDegrees * Mathf.Deg2Rad;
                        Vector2 facing = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                        float dot = Vector2.Dot(facing, toAttacker.normalized);
                        if (dot < -0.20f)
                        {
                            h.cowardShieldFactor = Mathf.Min(1f, h.cowardShieldFactor + 4.0f * dt);
                        }
                        else if (dot > 0.20f)
                        {
                            h.cowardShieldFactor = Mathf.Max(0f, h.cowardShieldFactor - 2.2f * dt);
                        }
                        else
                        {
                            h.cowardShieldFactor = Mathf.Max(0f, h.cowardShieldFactor - 0.9f * dt);
                        }
                    }
                }
                else
                {
                    h.cowardShieldFactor = Mathf.Max(0f, h.cowardShieldFactor - 1.5f * dt);
                }
            }
            else
            {
                h.cowardShieldFactor = Mathf.Max(0f, h.cowardShieldFactor - 1.5f * dt);
            }

            float nextBonus = tuning.cowardShieldResistFormula.Evaluate(1, h.agility) * h.cowardShieldFactor;
            if (Mathf.Abs(nextBonus - oldBonus) > 0.05f)
            {
                RecalcHeroStats(h, preserveHpRatio: true);
            }
        }

        private void TickIdleMovementAndAcquire(HeroState h, float dt)
        {
            if (h.isAttackMoving && h.attackTargetKind == LaneTargetKind.None)
            {
                float scan = Mathf.Max(GetBaseBasicRange(h.heroId) + 1.8f, 4.8f);
                if (TryAcquireTargetNear(h, h.motor.Position, scan, out LaneTargetKind kind, out int id))
                {
                    h.attackTargetKind = kind;
                    h.attackTargetId = id;
                }
            }

            if (h.attackTargetKind != LaneTargetKind.None)
            {
                if (!IsValidHostileTarget(h.team, h.attackTargetKind, h.attackTargetId, out Vector2 tPos, out float tRad))
                {
                    h.attackTargetKind = LaneTargetKind.None;
                    h.attackTargetId = 0;
                    if (h.isAttackMoving && Vector2.Distance(h.motor.Position, h.attackMoveDest) > 0.7f)
                    {
                        TryRouteHero(h, h.attackMoveDest);
                    }
                    return;
                }

                if (h.heroId == LaneHeroId.Ogre)
                {
                    if (Vector2.Distance(h.motor.Position, tPos) > 2.2f)
                    {
                        TryRouteHero(h, tPos);
                        AdvanceMotor(h, dt);
                    }
                    else
                    {
                        h.motor.Stop();
                        h.motor.TurnToward(tPos, new MovementTuning { TurnRate = h.turnRate }, dt);
                    }
                    return;
                }

                float baseRange = GetBaseBasicRange(h.heroId);
                float dist = Vector2.Distance(h.motor.Position, tPos);
                if (dist <= baseRange + tRad)
                {
                    h.motor.Stop();
                    h.motor.TurnToward(tPos, new MovementTuning { TurnRate = h.turnRate }, dt);
                    StartBasicWindup(h, tPos);
                    return;
                }

                if (!h.motor.IsMoving || Vector2.Distance(h.motor.Destination, tPos) > 0.65f)
                {
                    TryRouteHero(h, tPos);
                }
            }

            if (h.motor.IsMoving)
            {
                AdvanceMotor(h, dt);
            }
        }

        private void AdvanceMotor(HeroState h, float dt, float speedScale = 1f)
        {
            float effSpeed = h.moveSpeed * speedScale * (1f - Mathf.Clamp(h.slowAmount, 0f, 0.75f));
            var mt = new MovementTuning
            {
                MaxSpeed = Mathf.Max(0.8f, effSpeed),
                // v0.2.1 baseline: Acceleration = 14f, Deceleration = 18f, TurnSlowdownAngle = 80f
                Acceleration = 26f,
                Deceleration = 32f,
                TurnRate = h.turnRate,
                TurnSlowdownAngle = 90f,
            };
            h.motor.Tick(dt, mt);
            ResolveLaserDecoyBlocking(h);
        }

        private void StartBasicWindup(HeroState h, Vector2 targetPos)
        {
            if (h.heroId == LaneHeroId.Miravel && h.invisible) h.invisible = false;

            float asMult = Mathf.Max(0.4f, h.attackSpeed);
            h.aimDir = DirectionTo(h.motor.Position, targetPos, h.aimDir);
            h.phase = LanePhase.Windup;
            h.activeSlot = LaneAbilitySlot.None;
            h.twinSecondPending = false;
            float rawWindup = h.heroId == LaneHeroId.Miravel ? tuning.luisBasicWindup
                : (h.heroId == LaneHeroId.LaserShooter ? tuning.laserBasicWindup : tuning.swordsmanHit1Windup);
            h.phaseDuration = rawWindup / asMult;
            h.phaseTimer = h.phaseDuration;
        }

        private void TickBasicWindup(HeroState h, float dt)
        {
            if (!IsValidHostileTarget(h.team, h.attackTargetKind, h.attackTargetId, out Vector2 tPos, out float tRad))
            {
                h.phase = LanePhase.Idle;
                h.twinSecondPending = false;
                h.attackTargetKind = LaneTargetKind.None;
                return;
            }

            float maxAllowed = GetBaseBasicRange(h.heroId) + tRad + tuning.basicMotionBuffer;
            if (Vector2.Distance(h.motor.Position, tPos) > maxAllowed)
            {
                h.phase = LanePhase.Idle;
                h.twinSecondPending = false;
                return;
            }

            h.aimDir = DirectionTo(h.motor.Position, tPos, h.aimDir);
            h.motor.TurnToward(tPos, new MovementTuning { TurnRate = h.turnRate }, dt);

            h.phaseTimer -= dt;
            if (h.phaseTimer > 0f) return;

            float asMult = Mathf.Max(0.4f, h.attackSpeed);

            if (h.heroId == LaneHeroId.Miravel)
            {
                h.basicSwingAlt = (h.basicSwingAlt + 1) & 1;
                float dmg = h.attackDamage;
                projectiles.Add(new ProjectileState
                {
                    id = AllocId(),
                    team = h.team,
                    kind = LaneProjKind.LuisBolt,
                    sourceHeroId = h.id,
                    pos = h.motor.Position + h.aimDir * 0.45f,
                    speed = tuning.luisBasicProjSpeed,
                    damage = dmg,
                    isMagic = false,
                    targetKind = h.attackTargetKind,
                    targetId = h.attackTargetId,
                    lastKnownTarget = tPos,
                });
                PushEvent(LaneEventKind.LuisBasicShot, h.team, h.motor.Position, h.aimDir, 0.8f, dmg, string.Empty);

                h.phase = LanePhase.Recover;
                h.phaseDuration = tuning.luisBasicRecover / asMult;
                h.phaseTimer = h.phaseDuration;
                return;
            }

            if (h.heroId == LaneHeroId.LaserShooter)
            {
                h.basicSwingAlt = (h.basicSwingAlt + 1) & 1;
                FireChainLaserAttack(h, h.attackTargetKind, h.attackTargetId, tPos, damageScale: 1f, isUltProc: false);
                h.phase = LanePhase.Recover;
                h.phaseDuration = tuning.laserBasicRecover / asMult;
                h.phaseTimer = h.phaseDuration;
                return;
            }

            // Swordsman Twin Blade (Strike 1 -> Hit 2 delay -> Recovery)
            if (!h.twinSecondPending)
            {
                h.basicSwingAlt = (h.basicSwingAlt + 2) & ~1;
                ExecuteSwordsmanStrike(h, isFirstStrike: true, h.attackTargetKind, h.attackTargetId, tPos);
                if (h.attackTargetKind == LaneTargetKind.None || !IsValidHostileTarget(h.team, h.attackTargetKind, h.attackTargetId, out _, out _))
                {
                    h.phase = LanePhase.Recover;
                    h.phaseDuration = tuning.swordsmanBasicRecover / asMult;
                    h.phaseTimer = h.phaseDuration;
                    return;
                }

                h.twinSecondPending = true;
                h.twinTargetKind = (int)h.attackTargetKind;
                h.twinTargetId = h.attackTargetId;
                h.phaseDuration = tuning.swordsmanHit2Delay / asMult;
                h.phaseTimer = h.phaseDuration;
            }
            else
            {
                h.basicSwingAlt = (h.basicSwingAlt + 1) | 1;
                h.twinSecondPending = false;
                ExecuteSwordsmanStrike(h, isFirstStrike: false, (LaneTargetKind)h.twinTargetKind, h.twinTargetId, tPos);
                h.phase = LanePhase.Recover;
                h.phaseDuration = tuning.swordsmanBasicRecover / asMult;
                h.phaseTimer = h.phaseDuration;
            }
        }

        private void ExecuteSwordsmanStrike(HeroState h, bool isFirstStrike, LaneTargetKind kind, int targetId, Vector2 targetPos)
        {
            float baseStrike = h.attackDamage * tuning.swordsmanStrikeAdRatio;

            if (kind == LaneTargetKind.Hero || kind == LaneTargetKind.Minion)
            {
                h.swordLastTargetKind = (int)kind;
                h.swordLastTargetId = targetId;
                h.swordMarkTimer = tuning.swordsmanRLookbackSeconds;
            }

            ApplyBasicStrikeToTarget(h, kind, targetId, baseStrike);
            PushEvent(LaneEventKind.SwordsmanSlash, h.team, targetPos, h.aimDir, 0.95f, baseStrike, string.Empty);

            if (isFirstStrike && h.rankW >= 1 && (kind == LaneTargetKind.Hero || kind == LaneTargetKind.Minion))
            {
                float targetMaxHp = GetTargetMaxHp(kind, targetId);
                if (targetMaxHp > 0f)
                {
                    float pct = tuning.swordsmanWPercentHpFormula.Evaluate(h.rankW, h.strength);
                    float bonusDmg = targetMaxHp * pct;
                    if (kind == LaneTargetKind.Minion) bonusDmg = Mathf.Min(bonusDmg, 140f);
                    ApplyMagicOrPhysicalToTarget(h, kind, targetId, bonusDmg, isMagic: true);
                    float heal = bonusDmg * tuning.swordsmanWHealShare * h.healPower;
                    HealHero(h, heal);
                    PushEvent(LaneEventKind.SwordsmanWHeal, h.team, h.motor.Position, Vector2.up, 1.1f, heal, $"+{Mathf.RoundToInt(heal)}");
                }
            }
            else if (!isFirstStrike && h.rankE >= 1 && (kind == LaneTargetKind.Hero || kind == LaneTargetKind.Minion))
            {
                h.swordStacks = Mathf.Min(tuning.swordsmanEMaxStacks, h.swordStacks + 1);
                h.swordStackTimer = tuning.swordsmanEStackDuration;
                RecalcHeroStats(h, preserveHpRatio: true);

                if (kind == LaneTargetKind.Hero)
                {
                    HeroState enemy = GetEnemyHero(h.team);
                    if (enemy.id == targetId && enemy.phase != LanePhase.Dead)
                    {
                        enemy.swordDebuffStacks = Mathf.Min(tuning.swordsmanEMaxStacks, enemy.swordDebuffStacks + 1);
                        enemy.swordDebuffTimer = tuning.swordsmanEStackDuration;
                        RecalcHeroStats(enemy, preserveHpRatio: true);
                    }
                }
                else
                {
                    for (int m = 0; m < minions.Count; m++)
                    {
                        if (minions[m].id == targetId && minions[m].hp > 0f)
                        {
                            minions[m].swordShredStacks = Mathf.Min(tuning.swordsmanEMaxStacks, minions[m].swordShredStacks + 1);
                            minions[m].swordShredTimer = tuning.swordsmanEStackDuration;
                            break;
                        }
                    }
                }
                PushEvent(LaneEventKind.SwordsmanEProc, h.team, targetPos, Vector2.up, 0.85f, h.swordStacks, $"E:{h.swordStacks}");
            }
        }

        private float GetTargetMaxHp(LaneTargetKind kind, int targetId)
        {
            if (kind == LaneTargetKind.Hero)
            {
                for (int i = 0; i < heroes.Length; i++)
                {
                    if (heroes[i].id == targetId) return heroes[i].maxHp;
                }
            }
            else if (kind == LaneTargetKind.Minion)
            {
                for (int i = 0; i < minions.Count; i++)
                {
                    if (minions[i].id == targetId) return minions[i].maxHp;
                }
            }
            return 0f;
        }

        private void ApplyBasicStrikeToTarget(HeroState attacker, LaneTargetKind kind, int targetId, float physicalDmg)
        {
            ApplyMagicOrPhysicalToTarget(attacker, kind, targetId, physicalDmg, isMagic: false, isBasicAttack: true);
        }

        private void ApplyMagicOrPhysicalToTarget(HeroState attacker, LaneTargetKind kind, int targetId, float dmg, bool isMagic, bool isBasicAttack = false)
        {
            if (kind == LaneTargetKind.Hero)
            {
                HeroState enemy = GetEnemyHero(attacker.team);
                if (enemy.id == targetId)
                {
                    DealDamageToHero(enemy, dmg, isMagic, false, attacker.id, attacker.team, isBasicOrMinion: isBasicAttack);
                    return;
                }
                for (int i = 0; i < illusions.Count; i++)
                {
                    if (illusions[i].id == targetId)
                    {
                        DealDamageToIllusion(illusions[i], dmg, isMagic, attacker.id, attacker.team);
                        return;
                    }
                }
            }
            else if (kind == LaneTargetKind.Minion)
            {
                for (int i = 0; i < minions.Count; i++)
                {
                    if (minions[i].id == targetId)
                    {
                        DealDamageToMinion(minions[i], dmg, isMagic, attacker.id, attacker.team);
                        return;
                    }
                }
            }
            else if (kind == LaneTargetKind.Tower && !isMagic)
            {
                TowerState tower = GetEnemyTower(attacker.team);
                if (tower.id == targetId)
                {
                    DealDamageToTower(tower, dmg, attacker.id, attacker.team);
                }
            }
        }

        private void TryCastAbility(HeroState h, LaneAbilitySlot slot, Vector2 cursorPoint)
        {
            int rank = slot == LaneAbilitySlot.Q ? h.rankQ :
                       slot == LaneAbilitySlot.W ? h.rankW :
                       slot == LaneAbilitySlot.E ? h.rankE : h.rankR;
            if (rank <= 0) return;

            float cd = slot == LaneAbilitySlot.Q ? h.cdQ :
                       slot == LaneAbilitySlot.W ? h.cdW :
                       slot == LaneAbilitySlot.E ? h.cdE : h.cdR;
            if (cd > 0f) return;

            Vector2 dir = DirectionTo(h.motor.Position, cursorPoint, h.aimDir);

            if (h.heroId == LaneHeroId.Miravel)
            {
                if (slot == LaneAbilitySlot.Q) return;
                if (slot == LaneAbilitySlot.W)
                {
                    CastMiravelW(h, cursorPoint);
                    OnHeroCastAbilityForCurse(h, isFollowupStrike: false);
                    return;
                }
                if (slot == LaneAbilitySlot.E)
                {
                    CastMiravelE(h);
                    OnHeroCastAbilityForCurse(h, isFollowupStrike: false);
                    return;
                }
                if (slot == LaneAbilitySlot.R)
                {
                    CastMiravelR(h, cursorPoint);
                    OnHeroCastAbilityForCurse(h, isFollowupStrike: false);
                    return;
                }
            }

            if (h.heroId == LaneHeroId.Swordsman)
            {
                if (slot == LaneAbilitySlot.W || slot == LaneAbilitySlot.E) return;
                if (slot == LaneAbilitySlot.Q)
                {
                    h.motor.Stop();
                    h.aimDir = dir;
                    h.motor = new MovementMotor(h.motor.Position, AngleDegrees(dir));
                    h.phase = LanePhase.Casting;
                    h.activeSlot = LaneAbilitySlot.Q;
                    h.phaseDuration = tuning.swordsmanQWindup;
                    h.phaseTimer = h.phaseDuration;
                    h.cdQ = ApplyCdr(h, tuning.swordsmanQCooldown - 0.5f * (h.rankQ - 1));
                    h.maxCdQ = h.cdQ;
                    OnHeroCastAbilityForCurse(h, false);
                    return;
                }
                if (slot == LaneAbilitySlot.R)
                {
                    TryCastSwordsmanR(h);
                    return;
                }
            }

            if (h.heroId == LaneHeroId.LaserShooter)
            {
                if (slot == LaneAbilitySlot.Q)
                {
                    CastLaserQ(h, cursorPoint);
                    return;
                }
                if (slot == LaneAbilitySlot.W)
                {
                    CastLaserW(h);
                    return;
                }
                if (slot == LaneAbilitySlot.E)
                {
                    CastLaserE(h, cursorPoint);
                    return;
                }
                if (slot == LaneAbilitySlot.R)
                {
                    CastLaserR(h, cursorPoint);
                    return;
                }
            }

            if (h.heroId == LaneHeroId.Ogre)
            {
                h.motor.Stop();
                h.attackTargetKind = LaneTargetKind.None;
                h.isAttackMoving = false;
                h.aimDir = dir;
                h.motor = new MovementMotor(h.motor.Position, AngleDegrees(dir));

                float animMult = Mathf.Max(0.4f, h.ogreAnimSpeed);

                if (slot == LaneAbilitySlot.R)
                {
                    h.phase = LanePhase.Channeling;
                    h.activeSlot = LaneAbilitySlot.R;
                    h.phaseDuration = tuning.ogreRChannelDuration;
                    h.phaseTimer = h.phaseDuration;
                    h.damageReduction = tuning.ogreRDamageReduction;
                    h.scale = tuning.ogreRScaleBonus;
                    h.rQueue.Clear();
                    h.rStrikeIndex = 0;
                    h.rStrikeInFollowthrough = false;
                    h.cdR = ApplyCdr(h, tuning.ogreRCooldown - 4f * (h.rankR - 1));
                    h.maxCdR = h.cdR;
                    OnHeroCastAbilityForCurse(h, false);
                    return;
                }

                h.phase = LanePhase.Casting;
                h.activeSlot = slot;
                if (slot == LaneAbilitySlot.Q)
                {
                    h.phaseDuration = tuning.ogreQWindup / animMult;
                    h.cdQ = ApplyCdr(h, tuning.ogreQCooldown - 0.6f * (h.rankQ - 1));
                    h.maxCdQ = h.cdQ;
                }
                else if (slot == LaneAbilitySlot.W)
                {
                    h.phaseDuration = tuning.ogreWWindup / animMult;
                    h.cdW = ApplyCdr(h, tuning.ogreWCooldown - 0.6f * (h.rankW - 1));
                    h.maxCdW = h.cdW;
                }
                else
                {
                    h.phaseDuration = tuning.ogreEWindup / animMult;
                    h.cdE = ApplyCdr(h, tuning.ogreECooldown - 0.8f * (h.rankE - 1));
                    h.maxCdE = h.cdE;
                }
                h.phaseTimer = h.phaseDuration;
                OnHeroCastAbilityForCurse(h, false);
            }
        }

        private void OnHeroCastAbilityForCurse(HeroState caster, bool isFollowupStrike)
        {
            if (!isFollowupStrike && caster.hasSpellweaverHourglass && caster.spellweaverCd <= 0f)
            {
                caster.spellweaverReadyTimer = 5.0f;
            }
            if (caster.curseTimer <= 0f) return;
            if (isFollowupStrike && !tuning.ogreRStacksCursePerStrike) return;

            caster.curseStacks = Mathf.Min(5, caster.curseStacks + 1);
            caster.curseTimer += tuning.luisECurseBonusDuration;
        }

        private void ApplyMiravelCurse(HeroState victim, HeroState miravel)
        {
            victim.curseSourceHeroId = miravel.id;
            victim.curseStacks = Mathf.Max(1, victim.curseStacks);
            victim.curseTimer = Mathf.Max(victim.curseTimer, tuning.luisECurseBaseDuration + 0.4f * Mathf.Max(0, miravel.rankE - 1));
            PushEvent(LaneEventKind.LuisCurseProc, miravel.team, victim.motor.Position, Vector2.up, 1.0f, victim.curseStacks, "CURSED");
        }

        private void CastMiravelW(HeroState h, Vector2 cursorPoint)
        {
            h.invisible = true;
            h.invisTimer = tuning.luisWDuration + 0.4f * (h.rankW - 1);
            h.cdW = ApplyCdr(h, tuning.luisWCooldown - 1.1f * (h.rankW - 1));
            h.maxCdW = h.cdW;

            Vector2 spawnPos = map.NearestWalkable(h.motor.Position, towers[0].alive, towers[1].alive);
            Vector2 dest = map.NearestWalkable(map.ClampInsideLane(cursorPoint), towers[0].alive, towers[1].alive);
            var illMotor = new MovementMotor(spawnPos, AngleDegrees(DirectionTo(spawnPos, dest, h.aimDir)));
            if (Vector2.Distance(spawnPos, dest) > 0.15f)
            {
                illMotor.TrySetDestination(ActiveCourse(), dest);
            }

            illusions.Add(new IllusionState
            {
                id = AllocId(),
                ownerHeroId = h.id,
                team = h.team,
                motor = illMotor,
                hp = Mathf.Max(80f, h.hp),
                maxHp = h.maxHp,
                timeLeft = tuning.luisWIllusionDuration,
                dest = dest,
                moveSpeed = h.moveSpeed,
                armor = h.armor,
                magicResist = h.magicResist,
            });
            PushEvent(LaneEventKind.LuisIllusionCast, h.team, spawnPos, DirectionTo(spawnPos, dest, h.aimDir), 1.1f, 0f, string.Empty);
        }

        private void CastMiravelE(HeroState h)
        {
            // Designer rule: Retribution Ward preserves active W invisibility so stealth ambushes work
            h.wardTimer = tuning.luisEWardDuration + 0.6f * (h.rankE - 1);
            h.cdE = ApplyCdr(h, tuning.luisECooldown - 0.8f * (h.rankE - 1));
            h.maxCdE = h.cdE;
        }

        private void CastMiravelR(HeroState h, Vector2 cursorPoint)
        {
            if (h.invisible) h.invisible = false;
            h.motor.Stop();
            h.attackTargetKind = LaneTargetKind.None;
            h.isAttackMoving = false;

            Vector2 offset = cursorPoint - h.motor.Position;
            if (offset.magnitude > tuning.luisRRange)
            {
                offset = offset.normalized * tuning.luisRRange;
            }
            Vector2 center = map.ClampInsideLane(h.motor.Position + offset);
            h.castTarget = center;
            h.aimDir = DirectionTo(h.motor.Position, center, h.aimDir);
            h.phase = LanePhase.Channeling;
            h.activeSlot = LaneAbilitySlot.R;
            h.phaseDuration = tuning.luisRChannelDuration;
            h.phaseTimer = h.phaseDuration;
            h.cdR = ApplyCdr(h, tuning.luisRCooldown - 3f * (h.rankR - 1));
            h.maxCdR = h.cdR;

            zones.Add(new ZoneState
            {
                ownerHeroId = h.id,
                team = h.team,
                pos = center,
                radius = tuning.luisRRadius,
                duration = tuning.luisRChannelDuration,
                rankR = h.rankR,
            });
        }

        private void InterruptChannel(HeroState h)
        {
            if (h.heroId == LaneHeroId.Miravel && h.activeSlot == LaneAbilitySlot.R)
            {
                for (int i = zones.Count - 1; i >= 0; i--)
                {
                    if (zones[i].ownerHeroId == h.id) zones.RemoveAt(i);
                }
            }
            h.rStrikeInFollowthrough = false;
            h.phase = LanePhase.Idle;
            h.activeSlot = LaneAbilitySlot.None;
        }

        private void CancelChannelIfInterrupted(HeroState h, bool byMoveOrOrder)
        {
            if (byMoveOrOrder && h.heroId == LaneHeroId.Miravel && h.phase == LanePhase.Channeling && h.activeSlot == LaneAbilitySlot.R)
            {
                InterruptChannel(h);
            }
        }

        private void TickHeroChannel(HeroState h, float dt)
        {
            if (h.heroId == LaneHeroId.LaserShooter && h.activeSlot == LaneAbilitySlot.R)
            {
                h.laserQCurRange = tuning.laserRRange;
                if (h.motor.IsMoving)
                {
                    float rRatio = tuning.laserRMoveRatioFormula.Evaluate(Mathf.Max(1, h.rankR), h.agility);
                    AdvanceMotor(h, dt, rRatio);
                }
                float interval = tuning.laserRProcInterval / Mathf.Max(0.4f, h.attackSpeed);
                h.laserRProcAccum += dt;
                if (h.laserRProcAccum >= interval)
                {
                    h.laserRProcAccum -= interval;
                    FireLaserRBeamProc(h);
                }

                h.phaseTimer -= dt;
                if (h.phaseTimer <= 0f)
                {
                    h.laserQCurRange = 0f;
                    h.phase = LanePhase.Recover;
                    h.phaseDuration = 0.16f;
                    h.phaseTimer = h.phaseDuration;
                }
                return;
            }

            h.phaseTimer -= dt;
            if (h.phaseTimer > 0f) return;

            if (h.heroId == LaneHeroId.Miravel)
            {
                InterruptChannel(h);
                return;
            }

            if (h.heroId == LaneHeroId.Ogre)
            {
                if (h.rQueue.Count == 0)
                {
                    h.scale = 1f;
                    h.damageReduction = 0f;
                    h.phase = LanePhase.Idle;
                    h.activeSlot = LaneAbilitySlot.None;
                    return;
                }
                BeginNextOgreComboStrike(h);
            }
        }

        private void BeginNextOgreComboStrike(HeroState h)
        {
            h.rStrikeInFollowthrough = false;
            if (h.rStrikeIndex >= h.rQueue.Count)
            {
                h.scale = 1f;
                h.damageReduction = 0f;
                h.rQueue.Clear();
                h.phase = LanePhase.Recover;
                h.phaseDuration = 0.25f / Mathf.Max(0.4f, h.ogreAnimSpeed);
                h.phaseTimer = h.phaseDuration;
                return;
            }

            (LaneAbilitySlot slot, Vector2 dir) = h.rQueue[h.rStrikeIndex];
            h.aimDir = dir;
            h.activeSlot = slot;
            h.phase = LanePhase.ComboSeq;
            Vector2 moved = map.PushOut(h.motor.Position, dir * tuning.ogreRLungeDistance, towers[0].alive, towers[1].alive);
            h.motor = new MovementMotor(moved, AngleDegrees(dir));

            bool isFinal = h.rStrikeIndex == h.rQueue.Count - 1;
            float baseW = isFinal ? tuning.ogreRFinalWindup : tuning.ogreRFastWindup;
            h.phaseDuration = baseW / Mathf.Max(0.4f, h.ogreAnimSpeed);
            h.phaseTimer = h.phaseDuration;
        }

        private void TickOgreComboSequence(HeroState h, float dt)
        {
            h.phaseTimer -= dt;
            if (h.phaseTimer > 0f) return;

            if (h.rStrikeInFollowthrough)
            {
                h.rStrikeInFollowthrough = false;
                BeginNextOgreComboStrike(h);
                return;
            }

            bool isFinal = h.rStrikeIndex == h.rQueue.Count - 1;
            float dmgMult = isFinal ? tuning.ogreRFinalCritMultiplier : 0.82f;
            if (h.rQueue[h.rStrikeIndex].slot == LaneAbilitySlot.Q)
            {
                ExecuteOgreQ(h, dmgMult, isFinal);
            }
            else
            {
                ExecuteOgreW(h, dmgMult, isFinal);
            }

            h.rStrikeIndex++;
            if (h.rStrikeIndex < h.rQueue.Count)
            {
                h.rStrikeInFollowthrough = true;
                h.phaseDuration = Mathf.Max(0.06f, tuning.ogreRStrikeFollowthrough);
                h.phaseTimer = h.phaseDuration;
                return;
            }

            BeginNextOgreComboStrike(h);
        }

        private void TickAbilityCasting(HeroState h, float dt)
        {
            if (h.heroId == LaneHeroId.LaserShooter && h.activeSlot == LaneAbilitySlot.Q)
            {
                TickLaserQBeam(h, dt);
                return;
            }

            h.phaseTimer -= dt;
            if (h.phaseTimer > 0f) return;

            if (h.heroId == LaneHeroId.LaserShooter && h.activeSlot == LaneAbilitySlot.R)
            {
                h.phase = LanePhase.Channeling;
                h.activeSlot = LaneAbilitySlot.R;
                h.phaseDuration = tuning.laserRDuration;
                h.phaseTimer = h.phaseDuration;
                h.laserRProcAccum = 0f;
                h.laserQCurRange = tuning.laserRRange;
                FireLaserRBeamProc(h);
                return;
            }

            float animMult = Mathf.Max(0.4f, h.ogreAnimSpeed);

            if (h.heroId == LaneHeroId.Ogre)
            {
                if (h.activeSlot == LaneAbilitySlot.Q)
                {
                    ExecuteOgreQ(h, 1f, false);
                    h.phase = LanePhase.Recover;
                    h.phaseDuration = tuning.ogreQRecover / animMult;
                }
                else if (h.activeSlot == LaneAbilitySlot.W)
                {
                    ExecuteOgreW(h, 1f, false);
                    h.phase = LanePhase.Recover;
                    h.phaseDuration = tuning.ogreWRecover / animMult;
                }
                else
                {
                    ExecuteOgreE(h);
                    h.phase = LanePhase.Recover;
                    h.phaseDuration = tuning.ogreERecover / animMult;
                }
                h.phaseTimer = h.phaseDuration;
                return;
            }

            if (h.heroId == LaneHeroId.Swordsman && h.activeSlot == LaneAbilitySlot.Q)
            {
                ExecuteSwordsmanQ(h);
                h.phase = LanePhase.Recover;
                h.phaseDuration = tuning.swordsmanQRecover;
                h.phaseTimer = h.phaseDuration;
            }
        }

        private void ExecuteOgreQ(HeroState h, float damageMult, bool isCritBanner)
        {
            int rankQ = Mathf.Max(1, h.rankQ);
            float dmg = tuning.ogreQDamageFormula.Evaluate(rankQ, h.attackDamage) * damageMult;
            float stun = tuning.ogreQStunFormula.Evaluate(rankQ, h.strength);
            Vector2 fwd = h.aimDir.normalized;
            Vector2 origin = h.motor.Position;

            HeroState enemy = GetEnemyHero(h.team);
            if (enemy.phase != LanePhase.Dead && IsInBeam(enemy.motor.Position, origin, fwd, tuning.ogreQRange, tuning.ogreQWidth + 0.45f))
            {
                DealDamageToHero(enemy, dmg, false, false, h.id, h.team);
                ApplyStunToHero(enemy, stun, h.id);
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                if (illusions[i].team != h.team && IsInBeam(illusions[i].motor.Position, origin, fwd, tuning.ogreQRange, tuning.ogreQWidth + 0.45f))
                {
                    DealDamageToIllusion(illusions[i], dmg, false, h.id, h.team);
                }
            }
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i].team != h.team && IsInBeam(minions[i].motor.Position, origin, fwd, tuning.ogreQRange, tuning.ogreQWidth + 0.35f))
                {
                    DealDamageToMinion(minions[i], dmg, false, h.id, h.team);
                    minions[i].stunTimer = Mathf.Max(minions[i].stunTimer, stun);
                }
            }

            PushEvent(isCritBanner ? LaneEventKind.OgreStrike : LaneEventKind.OgreSlam, h.team, origin, fwd, tuning.ogreQRange, dmg,
                isCritBanner ? $"CRIT {Mathf.RoundToInt(dmg)}!" : string.Empty);
        }

        private void ExecuteOgreW(HeroState h, float damageMult, bool isCritBanner)
        {
            int rankW = Mathf.Max(1, h.rankW);
            float dmg = tuning.ogreWDamageFormula.Evaluate(rankW, h.attackDamage) * damageMult;
            float knockback = tuning.ogreWKnockbackFormula.Evaluate(rankW, h.strength);
            Vector2 fwd = h.aimDir.normalized;
            Vector2 origin = h.motor.Position;

            HeroState enemy = GetEnemyHero(h.team);
            if (enemy.phase != LanePhase.Dead && IsInCone(enemy.motor.Position, origin, fwd, tuning.ogreWRange + 0.45f, tuning.ogreWArcCos))
            {
                DealDamageToHero(enemy, dmg, false, false, h.id, h.team);
                Vector2 pushDir = DirectionTo(origin, enemy.motor.Position, fwd);
                ApplyDisplacementToHero(enemy, pushDir, knockback, h.id);
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                if (illusions[i].team != h.team && IsInCone(illusions[i].motor.Position, origin, fwd, tuning.ogreWRange + 0.45f, tuning.ogreWArcCos))
                {
                    DealDamageToIllusion(illusions[i], dmg, false, h.id, h.team);
                }
            }
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i].team != h.team && IsInCone(minions[i].motor.Position, origin, fwd, tuning.ogreWRange + 0.35f, tuning.ogreWArcCos))
                {
                    DealDamageToMinion(minions[i], dmg, false, h.id, h.team);
                    Vector2 pushDir = DirectionTo(origin, minions[i].motor.Position, fwd);
                    Vector2 moved = map.PushOut(minions[i].motor.Position, pushDir * (knockback * 0.8f), towers[0].alive, towers[1].alive);
                    minions[i].motor = new MovementMotor(moved, minions[i].motor.HeadingDegrees);
                }
            }

            PushEvent(isCritBanner ? LaneEventKind.OgreStrike : LaneEventKind.OgreSweep, h.team, origin, fwd, tuning.ogreWRange, dmg,
                isCritBanner ? $"CRIT {Mathf.RoundToInt(dmg)}!" : string.Empty);
        }

        private void ExecuteOgreE(HeroState h)
        {
            int rankE = Mathf.Max(1, h.rankE);
            float dmg = tuning.ogreEDamageFormula.Evaluate(rankE, h.attackDamage);
            float stun = tuning.ogreEStunFormula.Evaluate(rankE, h.strength);
            Vector2 origin = h.motor.Position;

            HeroState enemy = GetEnemyHero(h.team);
            if (enemy.phase != LanePhase.Dead && Vector2.Distance(enemy.motor.Position, origin) <= tuning.ogreERadius + 0.45f)
            {
                DealDamageToHero(enemy, dmg, false, false, h.id, h.team);
                ApplyStunToHero(enemy, stun, h.id);
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                if (illusions[i].team != h.team && Vector2.Distance(illusions[i].motor.Position, origin) <= tuning.ogreERadius + 0.45f)
                {
                    DealDamageToIllusion(illusions[i], dmg, false, h.id, h.team);
                }
            }
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i].team != h.team && Vector2.Distance(minions[i].motor.Position, origin) <= tuning.ogreERadius + 0.35f)
                {
                    DealDamageToMinion(minions[i], dmg, false, h.id, h.team);
                    minions[i].stunTimer = Mathf.Max(minions[i].stunTimer, stun);
                }
            }

            PushEvent(LaneEventKind.OgreStomp, h.team, origin, h.aimDir, tuning.ogreERadius, dmg, string.Empty);
        }

        private void ExecuteSwordsmanQ(HeroState h)
        {
            float dmg = tuning.swordsmanQDamageFormula.Evaluate(h.rankQ, h.attackDamage);
            float slow = tuning.swordsmanQSlow + 0.05f * Mathf.Max(0, h.rankQ - 1);
            Vector2 fwd = h.aimDir.normalized;
            Vector2 origin = h.motor.Position;

            LaneTargetKind markedKind = LaneTargetKind.None;
            int markedId = 0;

            HeroState enemy = GetEnemyHero(h.team);
            if (enemy.phase != LanePhase.Dead && IsInCone(enemy.motor.Position, origin, fwd, tuning.swordsmanQRange + 0.45f, tuning.swordsmanQArcCos))
            {
                DealDamageToHero(enemy, dmg, false, false, h.id, h.team);
                ApplySlowToHero(enemy, slow, tuning.swordsmanQSlowDuration);
                if (enemy.phase != LanePhase.Dead && !enemy.invisible)
                {
                    markedKind = LaneTargetKind.Hero;
                    markedId = enemy.id;
                }
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                if (illusions[i].team != h.team && IsInCone(illusions[i].motor.Position, origin, fwd, tuning.swordsmanQRange + 0.45f, tuning.swordsmanQArcCos))
                {
                    DealDamageToIllusion(illusions[i], dmg, false, h.id, h.team);
                    if (markedKind == LaneTargetKind.None && illusions[i].hp > 0f)
                    {
                        markedKind = LaneTargetKind.Hero;
                        markedId = illusions[i].id;
                    }
                }
            }

            float closestMinionDistSq = float.MaxValue;
            int closestMinionId = 0;
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i].team != h.team && IsInCone(minions[i].motor.Position, origin, fwd, tuning.swordsmanQRange + 0.35f, tuning.swordsmanQArcCos))
                {
                    DealDamageToMinion(minions[i], dmg, false, h.id, h.team);
                    if (minions[i].hp > 0f)
                    {
                        minions[i].slowAmount = Mathf.Max(minions[i].slowAmount, slow);
                        minions[i].slowTimer = Mathf.Max(minions[i].slowTimer, tuning.swordsmanQSlowDuration);
                        float dSq = (minions[i].motor.Position - origin).sqrMagnitude;
                        if (dSq < closestMinionDistSq)
                        {
                            closestMinionDistSq = dSq;
                            closestMinionId = minions[i].id;
                        }
                    }
                }
            }

            if (markedKind == LaneTargetKind.None && closestMinionId > 0)
            {
                markedKind = LaneTargetKind.Minion;
                markedId = closestMinionId;
            }
            if (markedKind != LaneTargetKind.None)
            {
                h.swordLastTargetKind = (int)markedKind;
                h.swordLastTargetId = markedId;
                h.swordMarkTimer = tuning.swordsmanRLookbackSeconds;
            }

            PushEvent(LaneEventKind.SwordsmanQSlash, h.team, origin, fwd, tuning.swordsmanQRange, dmg, string.Empty);
        }

        private void TryCastSwordsmanR(HeroState h)
        {
            if (h.swordMarkTimer <= 0f || h.swordLastTargetId == 0) return;

            LaneTargetKind kind = (LaneTargetKind)h.swordLastTargetKind;
            if (!IsValidHostileTarget(h.team, kind, h.swordLastTargetId, out Vector2 tPos, out _)) return;
            if (Vector2.Distance(h.motor.Position, tPos) > tuning.swordsmanRRange) return;

            h.motor.Stop();
            Vector2 dir = DirectionTo(h.motor.Position, tPos, h.aimDir);
            h.aimDir = dir;
            Vector2 pastTarget = map.NearestWalkable(tPos + dir * 1.15f, towers[0].alive, towers[1].alive);

            h.phase = LanePhase.ExecuteDash;
            h.activeSlot = LaneAbilitySlot.R;
            h.dashTargetKind = (int)kind;
            h.dashTargetId = h.swordLastTargetId;
            h.dashEndPos = pastTarget;
            h.dashHitApplied = false;
            h.cdR = ApplyCdr(h, tuning.swordsmanRCooldown - 4f * (h.rankR - 1));
            h.maxCdR = h.cdR;
            OnHeroCastAbilityForCurse(h, false);
        }

        private void TickSwordsmanDash(HeroState h, float dt)
        {
            Vector2 toEnd = h.dashEndPos - h.motor.Position;
            float dist = toEnd.magnitude;
            float step = tuning.swordsmanRDashSpeed * dt;
            Vector2 next = dist <= step ? h.dashEndPos : h.motor.Position + toEnd.normalized * step;
            h.motor = new MovementMotor(next, AngleDegrees(h.aimDir));

            if (!h.dashHitApplied && (dist <= step || dist <= 1.25f))
            {
                h.dashHitApplied = true;
                ExecuteSwordsmanRSlash(h);
            }

            if (dist <= step)
            {
                h.phase = LanePhase.Recover;
                h.phaseDuration = 0.18f;
                h.phaseTimer = h.phaseDuration;
            }
        }

        private void ExecuteSwordsmanRSlash(HeroState h)
        {
            LaneTargetKind kind = (LaneTargetKind)h.dashTargetKind;
            int targetId = h.dashTargetId;
            float dmg = tuning.swordsmanRDamageFormula.Evaluate(h.rankR, h.attackDamage);
            float execThresh = tuning.swordsmanRExecuteThreshold + 0.04f * Mathf.Max(0, h.rankR - 1);

            bool resetCooldown = false;
            Vector2 hitPos = h.motor.Position;

            if (kind == LaneTargetKind.Hero)
            {
                HeroState enemy = GetEnemyHero(h.team);
                if (enemy.id == targetId && enemy.phase != LanePhase.Dead)
                {
                    hitPos = enemy.motor.Position;
                    DealDamageToHero(enemy, dmg, false, false, h.id, h.team);
                    if (enemy.phase == LanePhase.Dead)
                    {
                        resetCooldown = true;
                    }
                    else if (enemy.hp / enemy.maxHp <= execThresh)
                    {
                        DealDamageToHero(enemy, enemy.hp + 999f, false, true, h.id, h.team);
                        resetCooldown = enemy.phase == LanePhase.Dead;
                    }
                }
            }
            else if (kind == LaneTargetKind.Minion)
            {
                for (int i = 0; i < minions.Count; i++)
                {
                    if (minions[i].id == targetId && minions[i].hp > 0f)
                    {
                        hitPos = minions[i].motor.Position;
                        DealDamageToMinion(minions[i], dmg, false, h.id, h.team);
                        if (minions[i].hp > 0f && minions[i].hp / minions[i].maxHp <= execThresh)
                        {
                            DealDamageToMinion(minions[i], minions[i].hp + 999f, false, h.id, h.team);
                        }
                        if (minions[i].hp <= 0f) resetCooldown = true;
                        break;
                    }
                }
            }

            if (resetCooldown)
            {
                h.cdR = 0f;
                PushEvent(LaneEventKind.SwordsmanRSlash, h.team, hitPos, h.aimDir, 1.5f, dmg, "EXECUTE RESET!");
            }
            else
            {
                PushEvent(LaneEventKind.SwordsmanRSlash, h.team, hitPos, h.aimDir, 1.2f, dmg, $"{Mathf.RoundToInt(dmg)}");
            }
        }

        private static bool IsInBeam(Vector2 point, Vector2 origin, Vector2 fwd, float length, float halfWidth)
        {
            Vector2 delta = point - origin;
            float along = Vector2.Dot(delta, fwd);
            if (along < -0.3f || along > length) return false;
            float perp = Mathf.Abs(delta.x * fwd.y - delta.y * fwd.x);
            return perp <= halfWidth;
        }

        private static bool IsInCone(Vector2 point, Vector2 origin, Vector2 fwd, float radius, float minDot)
        {
            Vector2 delta = point - origin;
            float d = delta.magnitude;
            if (d > radius) return false;
            if (d < 0.35f) return true;
            return Vector2.Dot(delta / d, fwd) >= minDot;
        }
    }
}
