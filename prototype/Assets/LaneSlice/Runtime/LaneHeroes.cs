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
                prevMotorPos = pos,
                goldExact = tuning.startingGold,
                aimDir = team == LaneTeam.Blue ? Vector2.right : Vector2.left,
                firstStrikeIdleTimer = tuning.firstStrikeInactivitySeconds,
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
            float manaRatio = (preserveHpRatio && h.maxMana > 0f) ? Mathf.Clamp01(h.mana / h.maxMana) : 1f;

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
            float itemMaxMana = 0f, itemManaRegen = 0f, itemPctManaRegen = 0f, itemRcr = 0f;
            ItemPassiveFlags rawPassives = ItemPassiveFlags.None;

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
                itemMaxMana += item.AddMaxMana;
                itemManaRegen += item.AddManaRegen;
                itemPctManaRegen += item.AddPercentManaRegen;
                itemRcr += item.AddResourceCostReduction;
                rawPassives |= item.PassiveFlags;
            }

            int lvlOffset = h.level - 1;
            float ovMaxHp = 0f, ovAd = 0f, ovAs = 0f, ovAh = 0f, ovHeal = 0f;
            float ovMaxMana = 0f, ovManaRegen = 0f, ovPctManaRegen = 0f, ovRcr = 0f;
            if (previewOverrides.TryGetValue(h.team, out PreviewStatOverrides ov) && ov.enabled)
            {
                itemStr += Mathf.Max(0f, ov.bonusStrength);
                itemAgi += Mathf.Max(0f, ov.bonusAgility);
                itemInt += Mathf.Max(0f, ov.bonusIntelligence);
                ovAd = ov.bonusAttackDamage;
                ovAs = ov.bonusAttackSpeed;
                ovAh = ov.bonusAbilityHaste;
                ovHeal = ov.bonusHealPower;
                ovMaxHp = ov.bonusMaxHp;
                ovMaxMana = ov.bonusMaxMana;
                ovManaRegen = ov.bonusManaRegen;
                ovPctManaRegen = ov.bonusPercentManaRegen;
                ovRcr = ov.bonusResourceCostReduction;
            }

            // Core rule (01_CORE_SYSTEMS): Str/Agi/Int start at 0 (LoL AP model) and confer ZERO inherent universal stats.
            h.strength = itemStr;
            h.agility = itemAgi;
            h.intelligence = itemInt;
            h.passiveFlags = LaneItems.NormalizeEffectivePassives(rawPassives);
            if (!h.HasPassive(ItemPassiveFlags.Windstrider))
            {
                h.momentumFactor = 0f;
                h.momentumBonus = 0f;
            }
            if (!h.HasPassive(ItemPassiveFlags.TitanAegis))
            {
                h.shieldHp = 0f;
                h.shieldTimer = 0f;
            }
            if (!h.HasPassive(ItemPassiveFlags.AdaptiveScales))
            {
                h.adaptiveScales.Clear();
            }
            bool cowardOwned = h.HasPassive(ItemPassiveFlags.FlightReflex);
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

            // New W (Stance Temper): stacks & rank scaling read h.rankW
            float swordBonus = h.swordStacks * (tuning.swordsmanWStackGain + 2f * Mathf.Max(0, h.rankW - 1));
            float swordShred = h.swordDebuffStacks * tuning.swordsmanWStackShred;
            float moltenShred = ComputeTotalHeatShred(h.heatEntries);

            h.maxHp = Mathf.Max(100f, baseHp + hpPerLvl * lvlOffset + itemHp + ovMaxHp);
            float giantsMightAd = h.HasPassive(ItemPassiveFlags.GiantsMight)
                ? h.maxHp * tuning.giantsMightAdPerMaxHp
                : 0f;
            h.attackDamage = Mathf.Max(1f, baseAd + adPerLvl * lvlOffset + itemAd + ovAd + giantsMightAd);
            h.bonusAttackSpeed = Mathf.Max(0f, itemAs + ovAs);
            // v0.2.8 Global unbounded diminishing Attack Speed: EffectiveAS = sqrt(1 + k * BonusAS)
            h.attackSpeed = LaneTuning.EffectiveAttackSpeed(h.bonusAttackSpeed, tuning.attackSpeedCurveK);
            h.abilityHaste = Mathf.Max(0f, itemAh + ovAh);
            h.cooldownReduction = h.abilityHaste / (100f + h.abilityHaste);
            h.armor = baseArmor + itemArmor + swordBonus - swordShred - moltenShred + h.cowardShieldBonus;
            h.magicResist = baseMr + itemMr + swordBonus - swordShred - moltenShred + h.cowardShieldBonus;
            h.regenRate = baseRegen + itemRegen;
            h.healPower = Mathf.Max(0.1f, 1f + itemHeal + ovHeal + h.takedownHealBonus);
            h.itemAlliedAuraRegen = itemAuraRegen;
            h.moveSpeed = baseSpeed + itemSpeed + h.momentumBonus + h.slipperyBonus;
            h.turnRate = turnRate;

            h.resourceKind = GetHeroResourceKind(h.heroId);
            if (h.resourceKind == HeroResourceKind.Mana)
            {
                float bMana = h.heroId == LaneHeroId.Miravel ? tuning.miravelBaseMana : tuning.laserBaseMana;
                float mPerLvl = h.heroId == LaneHeroId.Miravel ? tuning.miravelManaPerLevel : tuning.laserManaPerLevel;
                float bRegen = h.heroId == LaneHeroId.Miravel ? tuning.miravelBaseManaRegen : tuning.laserBaseManaRegen;
                float rPerLvl = h.heroId == LaneHeroId.Miravel ? tuning.miravelManaRegenPerLevel : tuning.laserManaRegenPerLevel;

                h.rcrStat = Mathf.Max(0f, itemRcr + ovRcr);
                h.rcrPct = Mathf.Clamp(tuning.resourceCostReductionFormula.Evaluate(1, h.rcrStat), 0f, 0.99f);
                h.maxMana = Mathf.Max(1f, bMana + mPerLvl * lvlOffset + itemMaxMana + ovMaxMana);
                float passivePctManaRegen = h.HasPassive(ItemPassiveFlags.AstralSurge)
                    ? tuning.astralSurgePctManaRegen
                    : (h.HasPassive(ItemPassiveFlags.Wellspring) ? tuning.wellspringPctManaRegen : 0f);
                h.percentManaRegenPerSec = Mathf.Max(0f, itemPctManaRegen + ovPctManaRegen + passivePctManaRegen);
                float flatRegen = Mathf.Max(0f, bRegen + rPerLvl * lvlOffset + itemManaRegen + ovManaRegen);
                h.manaRegen = flatRegen + h.maxMana * h.percentManaRegenPerSec;
                h.mana = preserveHpRatio ? Mathf.Clamp(h.maxMana * manaRatio, 0f, h.maxMana) : h.maxMana;
                h.netManaRegen = h.manaRegen;
            }
            else
            {
                h.rcrStat = 0f;
                h.rcrPct = 0f;
                h.maxMana = 0f;
                h.percentManaRegenPerSec = 0f;
                h.manaRegen = 0f;
                h.mana = 0f;
                h.netManaRegen = 0f;
            }

            if (h.heroId == LaneHeroId.Ogre)
            {
                h.ogreWindupSpeed = tuning.ogreWindupSpeedFormula.Evaluate(1, h.agility);
                h.ogreRecoverSpeed = h.attackSpeed;
                h.ogreAnimSpeed = h.ogreWindupSpeed;
                if (h.phase == LanePhase.Channeling || h.phase == LanePhase.ComboSeq)
                {
                    h.scale = tuning.ogreRScaleFormula.Evaluate(Mathf.Max(1, h.rankR), h.strength);
                }
                else
                {
                    h.scale = 1f;
                }
            }
            else
            {
                h.ogreWindupSpeed = 1f;
                h.ogreRecoverSpeed = 1f;
                h.ogreAnimSpeed = 1f;
                h.scale = 1f;
            }

            if (preserveHpRatio)
            {
                h.hp = Mathf.Clamp(h.maxHp * hpRatio, 1f, h.maxHp);
            }
        }

        private void TickHeroes(float dt)
        {
            for (int i = 0; i < heroes.Length; i++)
            {
                HeroState h = heroes[i];
                TickSingleHero(h, dt);
                if (h.phase != LanePhase.Dead)
                {
                    if (h.hasCowardsShield)
                    {
                        UpdateCowardsShield(h, dt);
                    }
                    h.prevMotorPos = h.motor.Position;
                }
                else
                {
                    h.prevMotorPos = h.motor.Position;
                }
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
                    h.prevMotorPos = spawn;
                    RecalcHeroStats(h, preserveHpRatio: false);
                    h.hp = h.maxHp;
                    h.phase = LanePhase.Idle;
                    h.activeSlot = LaneAbilitySlot.None;
                    h.scale = 1f;
                    h.damageReduction = 0f;
                    h.damageTakenMult = 1f;
                    h.healReceivedMult = 1f;
                    h.cowardRetreatStreak = 0f;
                    h.cowardGraceTimer = 0f;
                    h.cowardShieldState = 0;
                    h.laserSlowOverlapCount = 0;
                    h.laserSlowElapsed = 0f;
                    h.laserEStepPending = false;
                    h.swordsmanLastStrikeHeavy = false;
                    h.firstStrikeIdleTimer = tuning.firstStrikeInactivitySeconds;
                }
                return;
            }

            h.damageTakenMult = 1f;
            h.healReceivedMult = 1f;
            ReapplyActiveZonesOnHero(h);

            // Timers: damage age, Momentum Greaves, Shield, Grievous, Slippery Trickster, First Strike
            h.lastHeroDamageAge += dt;
            if (h.colossusCd > 0f) h.colossusCd = Mathf.Max(0f, h.colossusCd - dt);
            if (h.shieldTimer > 0f)
            {
                h.shieldTimer = Mathf.Max(0f, h.shieldTimer - dt);
                if (h.shieldTimer <= 0f) h.shieldHp = 0f;
            }
            if (h.grievousTimer > 0f) h.grievousTimer = Mathf.Max(0f, h.grievousTimer - dt);
            if (h.slipperyCd > 0f) h.slipperyCd = Mathf.Max(0f, h.slipperyCd - dt);
            if (h.phase != LanePhase.Windup && !h.twinSecondPending)
            {
                h.firstStrikeIdleTimer = Mathf.Min(tuning.firstStrikeInactivitySeconds, h.firstStrikeIdleTimer + dt);
            }

            bool momentumChanged = false;
            if (h.HasPassive(ItemPassiveFlags.Windstrider))
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

            if (h.slipperyTimer > 0f)
            {
                h.slipperyTimer = Mathf.Max(0f, h.slipperyTimer - dt);
                float nextSlip = h.slipperyTimer > 0f
                    ? h.slipperyInitialBonus * (h.slipperyTimer / Mathf.Max(0.1f, tuning.slipperyTricksterDuration))
                    : 0f;
                if (Mathf.Abs(nextSlip - h.slipperyBonus) > 0.01f || nextSlip == 0f) momentumChanged = true;
                h.slipperyBonus = nextSlip;
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
                h.laserSlowElapsed += dt;
                h.slowTimer = Mathf.Max(0f, h.slowTimer - dt);
                if (h.slowTimer <= 0f)
                {
                    h.slowAmount = 0f;
                    h.laserSlowOverlapCount = 0;
                    h.laserSlowElapsed = 0f;
                }
            }
            else if (h.laserSlowOverlapCount > 0)
            {
                h.laserSlowOverlapCount = 0;
                h.laserSlowElapsed = 0f;
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

            // Adaptive Scales one-by-one decay after hold period
            for (int sc = h.adaptiveScales.Count - 1; sc >= 0; sc--)
            {
                AdaptiveScalesEntry e = h.adaptiveScales[sc];
                if (e.stacks <= 0)
                {
                    h.adaptiveScales.RemoveAt(sc);
                    continue;
                }
                if (e.holdTimer > 0f)
                {
                    e.holdTimer -= dt;
                }
                else
                {
                    e.decayAccum += dt;
                    if (e.decayAccum >= tuning.adaptiveScalesDecayStep)
                    {
                        e.decayAccum -= tuning.adaptiveScalesDecayStep;
                        e.stacks--;
                        if (e.stacks <= 0) h.adaptiveScales.RemoveAt(sc);
                    }
                }
            }

            // Shared Heat (Dragonfire DoT + Molten Edge shred decay)
            if (h.heatEntries.Count > 0)
            {
                float heatHalf = Mathf.Max(0.1f, tuning.draconicHeatHalfPoint);
                for (int hi = h.heatEntries.Count - 1; hi >= 0; hi--)
                {
                    HeatEntry he = h.heatEntries[hi];
                    if (he.stacks <= 0)
                    {
                        h.heatEntries.RemoveAt(hi);
                        recalcNeeded = true;
                        continue;
                    }
                    he.dotAccum += dt;
                    while (he.dotAccum >= 0.5f && h.phase != LanePhase.Dead)
                    {
                        he.dotAccum -= 0.5f;
                        HeroState owner = FindHeroById(he.ownerHeroId);
                        float intel = owner != null ? owner.intelligence : 0f;
                        float dps = tuning.dragonfireDpsFormula.Evaluate(1, intel);
                        float stackFactor = he.stacks / (he.stacks + heatHalf);
                        DealDamageToHero(h, dps * 0.5f * stackFactor, isMagic: true, isTrueDamage: false,
                            he.ownerHeroId, owner != null ? owner.team : (h.team == LaneTeam.Blue ? LaneTeam.Red : LaneTeam.Blue),
                            triggerMiravelWard: false, flags: DamageFlags.DamageOverTime);
                        if (h.phase == LanePhase.Dead) return;
                    }
                    if (he.holdTimer > 0f)
                    {
                        he.holdTimer -= dt;
                    }
                    else
                    {
                        he.decayAccum += dt;
                        if (he.decayAccum >= tuning.draconicHeatDecayStep)
                        {
                            he.decayAccum -= tuning.draconicHeatDecayStep;
                            he.stacks--;
                            recalcNeeded = true;
                            if (he.stacks <= 0) h.heatEntries.RemoveAt(hi);
                        }
                    }
                }
            }

            // Ancient Vitality deferred damage payout (even distribution, no double mitigation/re-deferral, credits original attacker)
            if (h.deferredDamage.Count > 0)
            {
                for (int di = h.deferredDamage.Count - 1; di >= 0; di--)
                {
                    DeferredDamageEntry de = h.deferredDamage[di];
                    de.elapsed += dt;
                    float frac = de.duration > 0.0001f ? Mathf.Clamp01(de.elapsed / de.duration) : 1f;
                    float targetPaid = de.totalAmount * frac;
                    float payNow = Mathf.Max(0f, targetPaid - de.paidSoFar);
                    de.paidSoFar += payNow;
                    if (payNow > 0f)
                    {
                        h.hp = Mathf.Max(0f, h.hp - payNow);
                        if (h.hp <= 0f)
                        {
                            OnHeroKilled(h, de.attackerHeroId, de.attackerTeam);
                            return;
                        }
                    }
                    if (de.elapsed >= de.duration - 0.0001f)
                    {
                        h.deferredDamage.RemoveAt(di);
                    }
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

            // Passive health regen + Miravel aura (INT radius, HealPower allied & drain, can push negative!) + universal Kindred Charm aura + Salve
            float allyAura = 0f;
            float enemyPenalty = 0f;
            HeroState drainSource = null;
            for (int j = 0; j < heroes.Length; j++)
            {
                HeroState other = heroes[j];
                if (other.phase == LanePhase.Dead) continue;
                float d = Vector2.Distance(h.motor.Position, other.motor.Position);

                // Kindred Charm allied regen aura works for ANY hero purchasing the item
                if (other.team == h.team && other.itemAlliedAuraRegen > 0f && d <= tuning.kindredAuraRadius)
                {
                    allyAura += other.itemAlliedAuraRegen * other.healPower;
                }

                if (other.heroId != LaneHeroId.Miravel) continue;
                float mirRadius = tuning.luisAuraRadiusFormula.Evaluate(1, other.intelligence);
                if (d > mirRadius) continue;

                if (other.team == h.team)
                {
                    allyAura += (tuning.luisAllyAuraRegenBase + 0.35f * (other.level - 1)) * other.healPower;
                }
                else
                {
                    enemyPenalty += (tuning.luisEnemyAuraRegenPenaltyBase + 0.35f * (other.level - 1)) * other.healPower;
                    drainSource = other;
                }
            }

            h.miravelLifeAura = allyAura;
            h.miravelDrainAura = enemyPenalty;

            float salveRegen = (h.HasPassive(ItemPassiveFlags.MossMend) && h.lastHeroDamageAge >= tuning.woodlandSalveOutOfCombat) ? tuning.woodlandSalveRegen : 0f;
            float primordialRegen = h.HasPassive(ItemPassiveFlags.PrimordialVitality)
                ? tuning.primordialVitalityMaxRegen * (1f - Mathf.Clamp01(h.hp / Mathf.Max(1f, h.maxHp)))
                : 0f;
            float grievousMult = h.grievousTimer > 0f ? (1f - tuning.grievousHealReduction) : 1f;
            float netRegen = h.regenRate + allyAura + salveRegen + primordialRegen - enemyPenalty;
            if (netRegen > 0f)
            {
                float fountainMult = ComputeFountainOfLifeMult(h, HealSourceKind.NaturalRegen);
                float effectivePositive = netRegen * h.healReceivedMult * fountainMult * grievousMult;
                h.netRegenRate = effectivePositive;
                h.hp = Mathf.Min(h.maxHp, h.hp + effectivePositive * dt);
            }
            else if (netRegen < 0f)
            {
                // Provisional rule (05_OPEN_QUESTIONS): negative natural regeneration applies raw HP decay
                // bypassing Grievous Wounds, resistances, shields, and E Ward; grants Miravel kill credit if lethal.
                h.netRegenRate = netRegen;
                h.hp = Mathf.Max(0f, h.hp + netRegen * dt);
                if (h.hp <= 0f)
                {
                    int srcId = drainSource != null ? drainSource.id : 0;
                    LaneTeam srcTeam = drainSource != null ? drainSource.team : (h.team == LaneTeam.Blue ? LaneTeam.Red : LaneTeam.Blue);
                    OnHeroKilled(h, srcId, srcTeam);
                    return;
                }
            }
            else
            {
                h.netRegenRate = 0f;
            }
            if (map.IsInFountain(h.team, h.motor.Position))
            {
                HealHero(h, tuning.fountainHealPerSec * dt, HealSourceKind.Fountain);
            }
            // Lingering Restoration delayed healing ticks (never recursively stored, never Fountain-of-Life double-amped)
            if (h.deferredHeals.Count > 0)
            {
                for (int hl = h.deferredHeals.Count - 1; hl >= 0; hl--)
                {
                    DeferredHealEntry he = h.deferredHeals[hl];
                    he.elapsed += dt;
                    float frac = he.duration > 0.0001f ? Mathf.Clamp01(he.elapsed / he.duration) : 1f;
                    float targetPaid = he.totalAmount * frac;
                    float payNow = Mathf.Max(0f, targetPaid - he.paidSoFar);
                    he.paidSoFar += payNow;
                    if (payNow > 0f) HealHero(h, payNow, HealSourceKind.LingeringRestoration);
                    if (he.elapsed >= he.duration - 0.0001f) h.deferredHeals.RemoveAt(hl);
                }
            }
            if (h.lowManaPopupCd > 0f) h.lowManaPopupCd = Mathf.Max(0f, h.lowManaPopupCd - dt);

            // Continuous Mana regeneration (active inside and outside combat) + fountain recovery + Mana passives
            if (h.resourceKind == HeroResourceKind.Mana && h.maxMana > 0f)
            {
                float fountainMana = map.IsInFountain(h.team, h.motor.Position) ? tuning.fountainManaPerSec : 0f;
                float dewSipMana = (h.HasPassive(ItemPassiveFlags.DewSip) && h.lastHeroDamageAge >= tuning.manaDewOutOfCombat)
                    ? tuning.manaDewOutOfCombatRegen
                    : 0f;
                float tidecallerMana = h.HasPassive(ItemPassiveFlags.TidecallerFlow)
                    ? tuning.tidecallerMaxMissingManaRegen * (1f - Mathf.Clamp01(h.mana / Mathf.Max(1f, h.maxMana)))
                    : 0f;
                float totalManaRegen = h.manaRegen + dewSipMana + tidecallerMana + fountainMana;
                if (totalManaRegen > 0f)
                {
                    h.mana = Mathf.Min(h.maxMana, h.mana + totalManaRegen * dt);
                }
                float activeChannelDrain = (h.heroId == LaneHeroId.Miravel && h.phase == LanePhase.Channeling && h.activeSlot == LaneAbilitySlot.R)
                    ? EvaluateAbilityResourceCost(h, LaneAbilitySlot.R)
                    : 0f;
                h.netManaRegen = totalManaRegen - activeChannelDrain;
            }
            else
            {
                h.netManaRegen = 0f;
            }

            // Miravel Q Passive Orb Spawner — inverse Ability Haste cooldown scaling without artificial 4.0s floor
            if (h.heroId == LaneHeroId.Miravel && h.rankQ >= 1)
            {
                h.orbCooldown -= dt;
                if (h.orbCooldown <= 0f)
                {
                    SpawnLuisOrb(h);
                    h.orbCooldown = ApplyCdr(h, tuning.luisOrbInterval - 0.6f * (h.rankQ - 1));
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
            Vector2 moveDelta = h.motor.Position - h.prevMotorPos;
            float actualSpeed = dt > 0.0001f ? moveDelta.magnitude / dt : 0f;
            float minRetreatSpeed = Mathf.Max(0.45f, h.moveSpeed * tuning.cowardShieldMinSpeedRatio);
            bool movingMeaningfully = actualSpeed >= minRetreatSpeed;

            bool hasEligibleAttacker = false;
            Vector2 awayFromAttacker = Vector2.zero;
            if (h.lastHeroDamageAge <= tuning.cowardShieldMemorySeconds && h.lastHeroAttackerId > 0)
            {
                HeroState attacker = h.lastHeroAttackerId == heroes[0].id ? heroes[0] : heroes[1];
                if (attacker.id == h.lastHeroAttackerId && attacker.phase != LanePhase.Dead)
                {
                    Vector2 delta = h.motor.Position - attacker.motor.Position;
                    if (delta.sqrMagnitude > 0.0025f)
                    {
                        hasEligibleAttacker = true;
                        awayFromAttacker = delta.normalized;
                    }
                }
            }

            if (hasEligibleAttacker)
            {
                float moveDot = movingMeaningfully ? Vector2.Dot(moveDelta.normalized, awayFromAttacker) : 0f;
                bool isRetreating = movingMeaningfully && moveDot >= tuning.cowardShieldRetreatDot;
                bool isPursuing = movingMeaningfully && moveDot <= tuning.cowardShieldPursuitDot;

                if (isRetreating)
                {
                    h.cowardRetreatStreak += dt;
                    if (h.cowardRetreatStreak >= tuning.cowardShieldCommitDelay)
                    {
                        h.cowardShieldFactor = Mathf.Min(1f, h.cowardShieldFactor + dt / Mathf.Max(0.05f, tuning.cowardShieldBuildupSeconds));
                    }
                    // Grace period is earned only after sustained retreating (blocks rapid left-right exploit)
                    if (h.cowardRetreatStreak >= tuning.cowardShieldMinGraceStreak && h.cowardShieldFactor >= 0.15f)
                    {
                        h.cowardGraceTimer = tuning.cowardShieldGraceSeconds;
                    }
                    h.cowardShieldState = h.cowardShieldFactor >= 0.99f
                        ? (byte)2
                        : (h.cowardShieldFactor > 0.001f ? (byte)1 : (byte)0);
                }
                else if (isPursuing)
                {
                    // Actively moving toward attacker breaks grace immediately and decays faster than standing still
                    h.cowardRetreatStreak = 0f;
                    h.cowardGraceTimer = 0f;
                    h.cowardShieldFactor = Mathf.Max(0f, h.cowardShieldFactor - dt / Mathf.Max(0.05f, tuning.cowardShieldPursuitDecaySeconds));
                    h.cowardShieldState = h.cowardShieldFactor > 0.001f ? (byte)4 : (byte)0;
                }
                else
                {
                    // Brief stops, stuns, obstacles, or perpendicular drift hold existing stacks during grace without adding more
                    h.cowardRetreatStreak = 0f;
                    if (h.cowardGraceTimer > 0f && h.cowardShieldFactor > 0.001f)
                    {
                        h.cowardGraceTimer = Mathf.Max(0f, h.cowardGraceTimer - dt);
                        h.cowardShieldState = 3;
                    }
                    else
                    {
                        h.cowardShieldFactor = Mathf.Max(0f, h.cowardShieldFactor - dt / Mathf.Max(0.05f, tuning.cowardShieldStopDecaySeconds));
                        h.cowardShieldState = h.cowardShieldFactor > 0.001f ? (byte)4 : (byte)0;
                    }
                }
            }
            else
            {
                h.cowardRetreatStreak = 0f;
                h.cowardGraceTimer = 0f;
                h.cowardShieldFactor = Mathf.Max(0f, h.cowardShieldFactor - dt / Mathf.Max(0.05f, tuning.cowardShieldStopDecaySeconds));
                h.cowardShieldState = h.cowardShieldFactor > 0.001f ? (byte)4 : (byte)0;
            }

            float nextBonus = tuning.cowardShieldResistFormula.Evaluate(1, h.agility) * h.cowardShieldFactor;
            if (Mathf.Abs(nextBonus - oldBonus) > 0.05f || (h.cowardShieldFactor <= 0.001f && oldBonus > 0f))
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
            bool firstStrike = h.HasPassive(ItemPassiveFlags.FirstStrike) &&
                               h.firstStrikeIdleTimer >= tuning.firstStrikeInactivitySeconds - 0.0001f;
            // Consume First Strike on wind-up start so cancelling repeatedly cannot exploit shortened wind-up
            h.firstStrikeIdleTimer = 0f;

            h.aimDir = DirectionTo(h.motor.Position, targetPos, h.aimDir);
            h.phase = LanePhase.Windup;
            h.activeSlot = LaneAbilitySlot.None;
            h.twinSecondPending = false;
            h.swordsmanLastStrikeHeavy = false;
            float rawWindup = h.heroId == LaneHeroId.Miravel ? tuning.luisBasicWindup
                : (h.heroId == LaneHeroId.LaserShooter ? tuning.laserBasicWindup : tuning.swordsmanHit1Windup);
            if (firstStrike)
            {
                float red = Mathf.Clamp(tuning.firstStrikeReductionFormula.Evaluate(1, h.agility), 0f, 0.75f);
                float reducedRaw = Mathf.Max(tuning.firstStrikeMinRawWindup, rawWindup * (1f - red));
                h.phaseDuration = Mathf.Max(tuning.firstStrikeMinFinalWindup, reducedRaw / asMult);
            }
            else
            {
                h.phaseDuration = rawWindup / asMult;
            }
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

            // Swordsman Twin-Blade: 1st light strike -> AGI-improved transition -> 2nd heavy strike -> Recovery (all scaled inversely by Attack Speed)
            if (!h.twinSecondPending)
            {
                h.basicSwingAlt = (h.basicSwingAlt + 2) & ~1;
                h.swordsmanLastStrikeHeavy = false;
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
                float transitionDelay = tuning.swordsmanHit2DelayFormula.Evaluate(1, h.agility);
                h.phaseDuration = transitionDelay / asMult;
                h.phaseTimer = h.phaseDuration;
            }
            else
            {
                h.basicSwingAlt = (h.basicSwingAlt + 1) | 1;
                h.twinSecondPending = false;
                h.swordsmanLastStrikeHeavy = true;
                ExecuteSwordsmanStrike(h, isFirstStrike: false, (LaneTargetKind)h.twinTargetKind, h.twinTargetId, tPos);
                h.phase = LanePhase.Recover;
                h.phaseDuration = tuning.swordsmanBasicRecover / asMult;
                h.phaseTimer = h.phaseDuration;
            }
        }

        private void ExecuteSwordsmanStrike(HeroState h, bool isFirstStrike, LaneTargetKind kind, int targetId, Vector2 targetPos)
        {
            float adRatio = isFirstStrike ? tuning.swordsmanHit1AdRatio : tuning.swordsmanHit2AdRatio;
            float baseStrike = h.attackDamage * adRatio;

            if (kind == LaneTargetKind.Hero || kind == LaneTargetKind.Minion)
            {
                h.swordLastTargetKind = (int)kind;
                h.swordLastTargetId = targetId;
                h.swordMarkTimer = tuning.swordsmanRLookbackFormula.Evaluate(Mathf.Max(1, h.rankR), h.intelligence);
            }

            ApplyBasicStrikeToTarget(h, kind, targetId, baseStrike);
            PushEvent(LaneEventKind.SwordsmanSlash, h.team, targetPos, h.aimDir,
                isFirstStrike ? 0.85f : 1.45f, baseStrike, isFirstStrike ? "LIGHT" : "HEAVY");

            // Swapped W & E (v0.2.8): 1st light strike activates W (Stance Temper), 2nd heavy strike activates E (Vital Draw)
            if (isFirstStrike && h.rankW >= 1 && (kind == LaneTargetKind.Hero || kind == LaneTargetKind.Minion))
            {
                float selfDur = tuning.swordsmanWBuffDurationFormula.Evaluate(h.rankW, h.intelligence);
                float debuffDur = tuning.swordsmanWDebuffDurationFormula.Evaluate(h.rankW, h.intelligence);

                h.swordStacks = Mathf.Min(tuning.swordsmanWMaxStacks, h.swordStacks + 1);
                h.swordStackTimer = selfDur;
                RecalcHeroStats(h, preserveHpRatio: true);

                if (kind == LaneTargetKind.Hero)
                {
                    HeroState enemy = GetEnemyHero(h.team);
                    if (enemy.id == targetId && enemy.phase != LanePhase.Dead)
                    {
                        enemy.swordDebuffStacks = Mathf.Min(tuning.swordsmanWMaxStacks, enemy.swordDebuffStacks + 1);
                        enemy.swordDebuffTimer = debuffDur;
                        RecalcHeroStats(enemy, preserveHpRatio: true);
                    }
                }
                else
                {
                    for (int m = 0; m < minions.Count; m++)
                    {
                        if (minions[m].id == targetId && minions[m].hp > 0f)
                        {
                            minions[m].swordShredStacks = Mathf.Min(tuning.swordsmanWMaxStacks, minions[m].swordShredStacks + 1);
                            minions[m].swordShredTimer = debuffDur;
                            break;
                        }
                    }
                }
                PushEvent(LaneEventKind.SwordsmanEProc, h.team, targetPos, Vector2.up, 0.85f, h.swordStacks, $"W:{h.swordStacks}");
            }
            else if (!isFirstStrike && h.rankE >= 1 && (kind == LaneTargetKind.Hero || kind == LaneTargetKind.Minion))
            {
                float targetMaxHp = GetTargetMaxHp(kind, targetId);
                if (targetMaxHp > 0f)
                {
                    float pct = tuning.swordsmanEPercentHpFormula.Evaluate(h.rankE, h.strength);
                    float bonusDmg = targetMaxHp * pct;
                    if (kind == LaneTargetKind.Minion) bonusDmg = Mathf.Min(bonusDmg, 140f);
                    ApplyMagicOrPhysicalToTarget(h, kind, targetId, bonusDmg, isMagic: true);
                    float heal = bonusDmg * tuning.swordsmanEHealShare * h.healPower;
                    HealHero(h, heal);
                    PushEvent(LaneEventKind.SwordsmanWHeal, h.team, h.motor.Position, Vector2.up, 1.1f, heal, $"+{Mathf.RoundToInt(heal)}");
                }
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
                    DealDamageToHero(enemy, dmg, isMagic, false, attacker.id, attacker.team,
                        flags: isBasicAttack ? (DamageFlags.BasicOrMinion | DamageFlags.OnHit) : DamageFlags.None);
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
                        DealDamageToMinion(minions[i], dmg, isMagic, attacker.id, attacker.team, isOnHit: isBasicAttack);
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
                    if (!TryConsumeHeroAbilityResource(h, LaneAbilitySlot.W)) return;
                    CastMiravelW(h, cursorPoint);
                    OnHeroCastAbilityForCurse(h, isFollowupStrike: false);
                    return;
                }
                if (slot == LaneAbilitySlot.E)
                {
                    if (!TryConsumeHeroAbilityResource(h, LaneAbilitySlot.E)) return;
                    CastMiravelE(h);
                    OnHeroCastAbilityForCurse(h, isFollowupStrike: false);
                    return;
                }
                if (slot == LaneAbilitySlot.R)
                {
                    if (!TryConsumeHeroAbilityResource(h, LaneAbilitySlot.R)) return;
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
                    h.phaseDuration = tuning.swordsmanQWindupFormula.Evaluate(h.rankQ, h.agility);
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
                    if (!TryConsumeHeroAbilityResource(h, LaneAbilitySlot.Q)) return;
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
                    if (!TryConsumeHeroAbilityResource(h, LaneAbilitySlot.E)) return;
                    CastLaserE(h, cursorPoint);
                    return;
                }
                if (slot == LaneAbilitySlot.R)
                {
                    if (!TryConsumeHeroAbilityResource(h, LaneAbilitySlot.R)) return;
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

                float windupMult = Mathf.Max(0.4f, h.ogreWindupSpeed);

                if (slot == LaneAbilitySlot.R)
                {
                    h.phase = LanePhase.Channeling;
                    h.activeSlot = LaneAbilitySlot.R;
                    h.phaseDuration = tuning.ogreRChannelDuration;
                    h.phaseTimer = h.phaseDuration;
                    h.damageReduction = tuning.ogreRDamageReduction;
                    h.scale = tuning.ogreRScaleFormula.Evaluate(Mathf.Max(1, h.rankR), h.strength);
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
                    h.phaseDuration = tuning.ogreQWindup / windupMult;
                    h.cdQ = ApplyCdr(h, tuning.ogreQCooldown - 0.6f * (h.rankQ - 1));
                    h.maxCdQ = h.cdQ;
                }
                else if (slot == LaneAbilitySlot.W)
                {
                    h.phaseDuration = tuning.ogreWWindup / windupMult;
                    h.cdW = ApplyCdr(h, tuning.ogreWCooldown - 0.6f * (h.rankW - 1));
                    h.maxCdW = h.cdW;
                }
                else
                {
                    h.phaseDuration = tuning.ogreEWindup / windupMult;
                    h.cdE = ApplyCdr(h, tuning.ogreECooldown - 0.8f * (h.rankE - 1));
                    h.maxCdE = h.cdE;
                }
                h.phaseTimer = h.phaseDuration;
                OnHeroCastAbilityForCurse(h, false);
            }
        }

        private void OnHeroCastAbilityForCurse(HeroState caster, bool isFollowupStrike)
        {
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
                radius = tuning.luisRRadiusFormula.Evaluate(h.rankR, h.intelligence),
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
                int procGuard = 0;
                while (h.laserRProcAccum >= interval && procGuard < 16)
                {
                    h.laserRProcAccum -= interval;
                    FireLaserRBeamProc(h);
                    procGuard++;
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

            if (h.heroId == LaneHeroId.Miravel && h.activeSlot == LaneAbilitySlot.R)
            {
                float drainPerSec = EvaluateAbilityResourceCost(h, LaneAbilitySlot.R);
                float drainTick = drainPerSec * dt;
                if (h.mana < drainTick || h.mana <= 0.0001f)
                {
                    h.mana = 0f;
                    h.netManaRegen = h.manaRegen;
                    InterruptChannel(h);
                    return;
                }
                h.mana = Mathf.Max(0f, h.mana - drainTick);
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
                h.phaseDuration = tuning.ogreRPostComboRecover / Mathf.Max(0.4f, h.ogreRecoverSpeed);
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
            h.phaseDuration = baseW / Mathf.Max(0.4f, h.ogreWindupSpeed);
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
                h.phaseDuration = Mathf.Max(0.005f, tuning.ogreRStrikeFollowthrough / Mathf.Max(0.4f, h.ogreRecoverSpeed));
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

            if (h.heroId == LaneHeroId.LaserShooter && h.activeSlot == LaneAbilitySlot.E && h.laserEStepPending)
            {
                float progress = 1f - Mathf.Clamp01(h.phaseTimer / Mathf.Max(0.01f, h.phaseDuration));
                Vector2 nextStep = Vector2.Lerp(h.laserEStepStartPos, h.laserEStepEndPos, progress);
                h.motor = new MovementMotor(nextStep, AngleDegrees(h.aimDir));
                if (h.phaseTimer <= 0f)
                {
                    CompleteLaserEStep(h);
                }
                return;
            }

            if (h.phaseTimer > 0f) return;

            if (h.heroId == LaneHeroId.LaserShooter && h.activeSlot == LaneAbilitySlot.R)
            {
                h.phase = LanePhase.Channeling;
                h.activeSlot = LaneAbilitySlot.R;
                h.phaseDuration = tuning.laserRDurationFormula.Evaluate(Mathf.Max(1, h.rankR), h.intelligence);
                h.phaseTimer = h.phaseDuration;
                h.laserRProcAccum = 0f;
                h.laserQCurRange = tuning.laserRRange;
                FireLaserRBeamProc(h);
                return;
            }

            float recoverMult = Mathf.Max(0.4f, h.ogreRecoverSpeed);

            if (h.heroId == LaneHeroId.Ogre)
            {
                if (h.activeSlot == LaneAbilitySlot.Q)
                {
                    ExecuteOgreQ(h, 1f, false);
                    h.phase = LanePhase.Recover;
                    h.phaseDuration = tuning.ogreQRecover / recoverMult;
                }
                else if (h.activeSlot == LaneAbilitySlot.W)
                {
                    ExecuteOgreW(h, 1f, false);
                    h.phase = LanePhase.Recover;
                    h.phaseDuration = tuning.ogreWRecover / recoverMult;
                }
                else
                {
                    ExecuteOgreE(h);
                    h.phase = LanePhase.Recover;
                    h.phaseDuration = tuning.ogreERecover / recoverMult;
                }
                h.phaseTimer = h.phaseDuration;
                return;
            }

            if (h.heroId == LaneHeroId.Swordsman && h.activeSlot == LaneAbilitySlot.Q)
            {
                ExecuteSwordsmanQ(h);
                h.phase = LanePhase.Recover;
                h.phaseDuration = tuning.swordsmanQRecover; // Fixed: AGI only accelerates Q wind-up
                h.phaseTimer = h.phaseDuration;
            }
        }

        private static int ClassifyOgreQZone(
            Vector2 point,
            Vector2 origin,
            Vector2 fwd,
            float centralRange,
            float centralHalfW,
            float outerRange,
            float totalHalfW)
        {
            Vector2 delta = point - origin;
            float along = Vector2.Dot(delta, fwd);
            if (along < -0.25f) return 0;
            float perp = Mathf.Abs(delta.x * fwd.y - delta.y * fwd.x);
            if (along <= centralRange && perp <= centralHalfW) return 1;
            if (along <= outerRange && perp <= totalHalfW) return 2;
            return 0;
        }

        private void ExecuteOgreQ(HeroState h, float damageMult, bool isCritBanner)
        {
            int rankQ = Mathf.Max(1, h.rankQ);
            float scale = h.scale;
            float centralDmg = tuning.ogreQDamageFormula.Evaluate(rankQ, h.attackDamage) * damageMult;
            float outerDmg = centralDmg * tuning.ogreQOuterDamageRatio;
            float stun = tuning.ogreQStunFormula.Evaluate(rankQ, h.strength);

            float centralRange = tuning.ogreQRange * scale;
            float centralHalfW = tuning.ogreQWidth * scale;
            float outerExtraW = tuning.ogreQOuterWidthFormula.Evaluate(rankQ, h.strength);
            float totalHalfW = centralHalfW + outerExtraW;
            float outerRange = centralRange + outerExtraW * 0.45f;

            Vector2 fwd = h.aimDir.normalized;
            Vector2 origin = h.motor.Position;

            HeroState enemy = GetEnemyHero(h.team);
            if (enemy.phase != LanePhase.Dead)
            {
                int zone = ClassifyOgreQZone(enemy.motor.Position, origin, fwd, centralRange, centralHalfW, outerRange, totalHalfW);
                if (zone == 1)
                {
                    DealDamageToHero(enemy, centralDmg, false, false, h.id, h.team);
                    ApplyStunToHero(enemy, stun, h.id);
                }
                else if (zone == 2)
                {
                    DealDamageToHero(enemy, outerDmg, false, false, h.id, h.team);
                }
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                if (illusions[i].team == h.team) continue;
                int zone = ClassifyOgreQZone(illusions[i].motor.Position, origin, fwd, centralRange, centralHalfW, outerRange, totalHalfW);
                if (zone == 1)
                {
                    DealDamageToIllusion(illusions[i], centralDmg, false, h.id, h.team);
                }
                else if (zone == 2)
                {
                    DealDamageToIllusion(illusions[i], outerDmg, false, h.id, h.team);
                }
            }
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i].team == h.team) continue;
                int zone = ClassifyOgreQZone(minions[i].motor.Position, origin, fwd, centralRange, centralHalfW, outerRange, totalHalfW);
                if (zone == 1)
                {
                    DealDamageToMinion(minions[i], centralDmg, false, h.id, h.team);
                    minions[i].stunTimer = Mathf.Max(minions[i].stunTimer, stun);
                }
                else if (zone == 2)
                {
                    DealDamageToMinion(minions[i], outerDmg, false, h.id, h.team);
                }
            }

            PushEvent(isCritBanner ? LaneEventKind.OgreStrike : LaneEventKind.OgreSlam, h.team, origin, fwd, centralRange, centralDmg,
                isCritBanner ? $"CRIT {Mathf.RoundToInt(centralDmg)}!" : "CENTRAL");
            PushEvent(LaneEventKind.OgreQShockwave, h.team, origin, fwd, totalHalfW, outerRange, "SHOCKWAVE");
        }

        private void ExecuteOgreW(HeroState h, float damageMult, bool isCritBanner)
        {
            int rankW = Mathf.Max(1, h.rankW);
            float wRange = tuning.ogreWRange * h.scale;
            float dmg = tuning.ogreWDamageFormula.Evaluate(rankW, h.attackDamage) * damageMult;
            float knockback = tuning.ogreWKnockbackFormula.Evaluate(rankW, h.strength);
            Vector2 fwd = h.aimDir.normalized;
            Vector2 origin = h.motor.Position;

            HeroState enemy = GetEnemyHero(h.team);
            if (enemy.phase != LanePhase.Dead && IsInCone(enemy.motor.Position, origin, fwd, wRange + 0.40f, tuning.ogreWArcCos))
            {
                DealDamageToHero(enemy, dmg, false, false, h.id, h.team);
                Vector2 pushDir = DirectionTo(origin, enemy.motor.Position, fwd);
                ApplyDisplacementToHero(enemy, pushDir, knockback, h.id);
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                if (illusions[i].team != h.team && IsInCone(illusions[i].motor.Position, origin, fwd, wRange + 0.40f, tuning.ogreWArcCos))
                {
                    DealDamageToIllusion(illusions[i], dmg, false, h.id, h.team);
                }
            }
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i].team != h.team && IsInCone(minions[i].motor.Position, origin, fwd, wRange + 0.30f, tuning.ogreWArcCos))
                {
                    DealDamageToMinion(minions[i], dmg, false, h.id, h.team);
                    Vector2 pushDir = DirectionTo(origin, minions[i].motor.Position, fwd);
                    Vector2 moved = map.PushOut(minions[i].motor.Position, pushDir * (knockback * 0.8f), towers[0].alive, towers[1].alive);
                    minions[i].motor = new MovementMotor(moved, minions[i].motor.HeadingDegrees);
                }
            }

            PushEvent(isCritBanner ? LaneEventKind.OgreStrike : LaneEventKind.OgreSweep, h.team, origin, fwd, wRange, dmg,
                isCritBanner ? $"CRIT {Mathf.RoundToInt(dmg)}!" : string.Empty);
        }

        private void ExecuteOgreE(HeroState h)
        {
            int rankE = Mathf.Max(1, h.rankE);
            float eRadius = tuning.ogreERadiusFormula.Evaluate(rankE, h.strength) * h.scale;
            float dmg = tuning.ogreEDamageFormula.Evaluate(rankE, h.attackDamage);
            float stun = tuning.ogreEStunFormula.Evaluate(rankE, h.strength);
            Vector2 origin = h.motor.Position;

            HeroState enemy = GetEnemyHero(h.team);
            if (enemy.phase != LanePhase.Dead && Vector2.Distance(enemy.motor.Position, origin) <= eRadius + 0.38f)
            {
                DealDamageToHero(enemy, dmg, false, false, h.id, h.team);
                ApplyStunToHero(enemy, stun, h.id);
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                if (illusions[i].team != h.team && Vector2.Distance(illusions[i].motor.Position, origin) <= eRadius + 0.38f)
                {
                    DealDamageToIllusion(illusions[i], dmg, false, h.id, h.team);
                }
            }
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i].team != h.team && Vector2.Distance(minions[i].motor.Position, origin) <= eRadius + 0.30f)
                {
                    DealDamageToMinion(minions[i], dmg, false, h.id, h.team);
                    minions[i].stunTimer = Mathf.Max(minions[i].stunTimer, stun);
                }
            }

            PushEvent(LaneEventKind.OgreStomp, h.team, origin, h.aimDir, eRadius, dmg, string.Empty);
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
                h.swordMarkTimer = tuning.swordsmanRLookbackFormula.Evaluate(Mathf.Max(1, h.rankR), h.intelligence);
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
            float dashSpeed = tuning.swordsmanRDashSpeedFormula.Evaluate(Mathf.Max(1, h.rankR), h.agility);
            float step = dashSpeed * dt;
            Vector2 next = dist <= step ? h.dashEndPos : h.motor.Position + toEnd.normalized * step;
            h.motor = new MovementMotor(next, AngleDegrees(h.aimDir));
            h.prevMotorPos = next;

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
            float execThresh = Mathf.Min(0.95f, tuning.swordsmanRExecuteFormula.Evaluate(h.rankR, h.strength));

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
                        DealDamageToHero(enemy, enemy.maxHp * 4f + 999f, false, true, h.id, h.team);
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
