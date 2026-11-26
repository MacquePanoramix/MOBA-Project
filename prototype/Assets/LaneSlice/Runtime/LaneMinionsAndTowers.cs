using Moba.MovementLab;
using UnityEngine;

namespace Moba.LaneSlice
{
    public sealed partial class LaneMatch
    {
        private void TickWaveSpawns(float dt)
        {
            nextWaveTimer -= dt;
            if (nextWaveTimer > 0f) return;

            nextWaveTimer = tuning.waveInterval;
            waveNumber++;
            SpawnWaveForTeam(LaneTeam.Blue);
            SpawnWaveForTeam(LaneTeam.Red);
        }

        private void SpawnWaveForTeam(LaneTeam team)
        {
            Vector2 basePos = map.SpawnPos(team);
            float forward = team == LaneTeam.Blue ? 1f : -1f;
            float heading = team == LaneTeam.Blue ? 90f : 270f;

            for (int i = 0; i < tuning.meleeCreepsPerWave; i++)
            {
                float zOff = (i - 1) * 0.9f;
                Vector2 p = map.NearestWalkable(basePos + new Vector2(forward * 1.4f, zOff), towers[0].alive, towers[1].alive);
                minions.Add(new MinionState
                {
                    id = AllocId(),
                    team = team,
                    ranged = false,
                    motor = new MovementMotor(p, heading),
                    hp = tuning.minionMaxHpMelee,
                    maxHp = tuning.minionMaxHpMelee,
                });
            }

            for (int i = 0; i < tuning.rangedCreepsPerWave; i++)
            {
                float zOff = 0f;
                Vector2 p = map.NearestWalkable(basePos + new Vector2(forward * 0.3f, zOff), towers[0].alive, towers[1].alive);
                minions.Add(new MinionState
                {
                    id = AllocId(),
                    team = team,
                    ranged = true,
                    motor = new MovementMotor(p, heading),
                    hp = tuning.minionMaxHpRanged,
                    maxHp = tuning.minionMaxHpRanged,
                });
            }
        }

        private void SpawnLuisOrb(HeroState luis)
        {
            int activeCount = 0;
            for (int i = 0; i < orbs.Count; i++)
            {
                if (orbs[i].ownerHeroId == luis.id) activeCount++;
            }
            if (activeCount >= 2) return;

            float towardCenter = luis.team == LaneTeam.Blue ? 3.2f : -3.2f;
            float zOffset = ((orbs.Count + tickCount) % 2 == 0) ? 1.4f : -1.4f;
            Vector2 spawnPos = map.NearestWalkable(
                new Vector2(Mathf.Clamp(luis.motor.Position.x + towardCenter, -9.5f, 9.5f), zOffset),
                towers[0].alive, towers[1].alive);

            orbs.Add(new OrbState
            {
                id = AllocId(),
                ownerTeam = luis.team,
                ownerHeroId = luis.id,
                pos = spawnPos,
                timeLeft = tuning.luisOrbDuration,
                duration = tuning.luisOrbDuration,
            });
            PushEvent(LaneEventKind.LuisOrbSpawn, luis.team, spawnPos, Vector2.up, tuning.luisOrbPickupRadius, 0f, string.Empty);
        }

        private void TickOrbs(float dt)
        {
            for (int i = orbs.Count - 1; i >= 0; i--)
            {
                OrbState o = orbs[i];
                o.timeLeft -= dt;
                if (o.timeLeft <= 0f)
                {
                    orbs.RemoveAt(i);
                    continue;
                }

                HeroState owner = GetHero(o.ownerTeam);
                HeroState enemy = GetEnemyHero(o.ownerTeam);

                bool ownerTouched = owner.phase != LanePhase.Dead &&
                    Vector2.Distance(owner.motor.Position, o.pos) <= tuning.luisOrbPickupRadius;
                bool enemyTouched = enemy.phase != LanePhase.Dead &&
                    Vector2.Distance(enemy.motor.Position, o.pos) <= tuning.luisOrbPickupRadius;

                if (ownerTouched && (!enemyTouched ||
                    Vector2.Distance(owner.motor.Position, o.pos) <= Vector2.Distance(enemy.motor.Position, o.pos)))
                {
                    TriggerLuisOrbPulse(owner, o);
                    orbs.RemoveAt(i);
                }
                else if (enemyTouched)
                {
                    PushEvent(LaneEventKind.LuisOrbDeny, enemy.team, o.pos, Vector2.up, 1.0f, 0f, "DENIED");
                    orbs.RemoveAt(i);
                }
            }
        }

        private void TriggerLuisOrbPulse(HeroState luis, OrbState orb)
        {
            int rankQ = Mathf.Max(1, luis.rankQ);
            float rawHeal = tuning.luisOrbHealFormula.Evaluate(rankQ, 0f) * luis.healPower;
            float dmg = tuning.luisOrbDamageFormula.Evaluate(rankQ, 0f) * luis.healPower;
            float r = tuning.luisOrbPulseRadiusFormula.Evaluate(rankQ, luis.intelligence);

            if (luis.phase != LanePhase.Dead && Vector2.Distance(luis.motor.Position, orb.pos) <= r + 0.5f)
            {
                HealHero(luis, rawHeal);
            }
            for (int i = 0; i < minions.Count; i++)
            {
                MinionState m = minions[i];
                if (m.hp <= 0f || Vector2.Distance(m.motor.Position, orb.pos) > r + 0.35f) continue;
                if (m.team == luis.team)
                {
                    m.hp = Mathf.Min(m.maxHp, m.hp + rawHeal * 0.65f * m.healReceivedMult);
                }
                else
                {
                    DealDamageToMinion(m, dmg, true, luis.id, luis.team);
                }
            }
            HeroState enemy = GetEnemyHero(luis.team);
            if (enemy.phase != LanePhase.Dead && Vector2.Distance(enemy.motor.Position, orb.pos) <= r + 0.5f)
            {
                DealDamageToHero(enemy, dmg, true, false, luis.id, luis.team);
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                if (illusions[i].team != luis.team && Vector2.Distance(illusions[i].motor.Position, orb.pos) <= r + 0.45f)
                {
                    DealDamageToIllusion(illusions[i], dmg, true, luis.id, luis.team);
                }
            }

            PushEvent(LaneEventKind.LuisOrbPulse, luis.team, orb.pos, Vector2.up, r, rawHeal, $"+{Mathf.RoundToInt(rawHeal)}");
        }

        private void TickZones(float dt)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                ZoneState z = zones[i];
                z.elapsed += dt;
                if (z.elapsed >= z.duration)
                {
                    zones.RemoveAt(i);
                }
            }
        }

        private void ReapplyActiveZonesOnHero(HeroState h)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                ZoneState z = zones[i];
                if (Vector2.Distance(h.motor.Position, z.pos) > z.radius + 0.4f) continue;

                HeroState owner = GetHero(z.team);
                float bonusHealPower = Mathf.Max(0f, owner.healPower - 1.0f);
                float intensity = Mathf.Clamp01(z.elapsed / Mathf.Max(0.25f, z.duration));
                if (h.team == z.team)
                {
                    float maxHealAmp = tuning.luisRHealAmpFormula.Evaluate(z.rankR, bonusHealPower);
                    h.healReceivedMult = Mathf.Max(h.healReceivedMult, 1f + maxHealAmp * intensity);
                }
                else
                {
                    float maxVuln = tuning.luisRVulnFormula.Evaluate(z.rankR, bonusHealPower);
                    h.damageTakenMult = Mathf.Max(h.damageTakenMult, 1f + maxVuln * intensity);
                    if (!IsCcImmune(h))
                    {
                        float slow = (tuning.luisRMaxSlow + 0.06f * Mathf.Max(0, z.rankR - 1)) * intensity;
                        h.slowAmount = Mathf.Max(h.slowAmount, slow);
                        h.slowTimer = Mathf.Max(h.slowTimer, 0.15f);
                    }
                }
            }
        }

        private void TickIllusions(float dt)
        {
            for (int i = illusions.Count - 1; i >= 0; i--)
            {
                IllusionState ill = illusions[i];
                ill.timeLeft -= dt;
                if (ill.timeLeft <= 0f || ill.hp <= 0f)
                {
                    illusions.RemoveAt(i);
                    continue;
                }
                if (ill.laserStackTimer > 0f)
                {
                    ill.laserStackTimer -= dt;
                    if (ill.laserStackTimer <= 0f) ill.laserStacks = 0;
                }

                if (!ill.isLaserDecoy)
                {
                    if (ill.motor.IsMoving && Vector2.Distance(ill.motor.Position, ill.dest) > 0.12f)
                    {
                        ill.motor.Tick(dt, new MovementTuning
                        {
                            MaxSpeed = Mathf.Max(0.8f, ill.moveSpeed > 0f ? ill.moveSpeed : tuning.luisSpeed),
                            Acceleration = 26f,
                            Deceleration = 32f,
                            TurnRate = tuning.luisTurnRate,
                            TurnSlowdownAngle = 90f,
                        });
                        if (!ill.motor.IsMoving || Vector2.Distance(ill.motor.Position, ill.dest) <= 0.12f)
                        {
                            ill.motor.Stop();
                        }
                    }
                    else if (ill.motor.Speed > 0f || ill.motor.IsMoving)
                    {
                        ill.motor.Stop();
                    }
                }
            }
        }

        private void TickMinions(float dt)
        {
            for (int i = minions.Count - 1; i >= 0; i--)
            {
                MinionState m = minions[i];
                if (m.hp <= 0f)
                {
                    minions.RemoveAt(i);
                    continue;
                }

                if (m.stunTimer > 0f) m.stunTimer = Mathf.Max(0f, m.stunTimer - dt);
                if (m.slowTimer > 0f)
                {
                    m.laserSlowElapsed += dt;
                    m.slowTimer = Mathf.Max(0f, m.slowTimer - dt);
                    if (m.slowTimer <= 0f)
                    {
                        m.slowAmount = 0f;
                        m.laserSlowOverlapCount = 0;
                        m.laserSlowElapsed = 0f;
                    }
                }
                else if (m.laserSlowOverlapCount > 0)
                {
                    m.laserSlowOverlapCount = 0;
                    m.laserSlowElapsed = 0f;
                }
                if (m.swordShredTimer > 0f)
                {
                    m.swordShredTimer -= dt;
                    if (m.swordShredTimer <= 0f) m.swordShredStacks = 0;
                }
                if (m.laserStackTimer > 0f)
                {
                    m.laserStackTimer -= dt;
                    if (m.laserStackTimer <= 0f) m.laserStacks = 0;
                }

                m.damageTakenMult = 1f;
                m.healReceivedMult = 1f;
                for (int z = 0; z < zones.Count; z++)
                {
                    if (Vector2.Distance(m.motor.Position, zones[z].pos) <= zones[z].radius + 0.3f)
                    {
                        HeroState zOwner = GetHero(zones[z].team);
                        float bonusHp = Mathf.Max(0f, zOwner.healPower - 1.0f);
                        float t = Mathf.Clamp01(zones[z].elapsed / Mathf.Max(0.25f, zones[z].duration));
                        if (zones[z].team == m.team)
                        {
                            float maxHealAmp = tuning.luisRHealAmpFormula.Evaluate(zones[z].rankR, bonusHp);
                            m.healReceivedMult = Mathf.Max(m.healReceivedMult, 1f + maxHealAmp * t);
                        }
                        else
                        {
                            float vuln = tuning.luisRVulnFormula.Evaluate(zones[z].rankR, bonusHp);
                            m.damageTakenMult = Mathf.Max(m.damageTakenMult, 1f + vuln * t);
                            m.slowAmount = Mathf.Max(m.slowAmount, tuning.luisRMaxSlow * t);
                            m.slowTimer = Mathf.Max(m.slowTimer, 0.15f);
                        }
                    }
                }

                m.attackCooldown = Mathf.Max(0f, m.attackCooldown - dt);
                if (m.stunTimer > 0f || m.isPreviewDummy)
                {
                    m.windingUp = false;
                    m.motor.Stop();
                    continue;
                }

                AcquireMinionTarget(m);
                float range = m.ranged ? tuning.minionRangedRange : tuning.minionMeleeRange;

                if (m.targetKind != LaneTargetKind.None &&
                    IsValidHostileTarget(m.team, m.targetKind, m.targetId, out Vector2 tPos, out float tRad))
                {
                    float dist = Vector2.Distance(m.motor.Position, tPos);
                    if (dist <= range + tRad + 0.15f)
                    {
                        m.motor.Stop();
                        m.motor.TurnToward(tPos, new MovementTuning { TurnRate = 360f }, dt);

                        if (m.windingUp)
                        {
                            m.attackWindup -= dt;
                            if (m.attackWindup <= 0f)
                            {
                                m.windingUp = false;
                                m.attackCooldown = m.ranged ? 1.05f : 0.95f;
                                FireMinionAttack(m, tPos);
                            }
                        }
                        else if (m.attackCooldown <= 0f)
                        {
                            m.windingUp = true;
                            m.attackWindup = 0.25f;
                        }
                        continue;
                    }
                }
                else
                {
                    tPos = m.team == LaneTeam.Blue ? map.TowerPos(LaneTeam.Red) : map.TowerPos(LaneTeam.Blue);
                    if (!GetEnemyTower(m.team).alive)
                    {
                        tPos = map.FountainPos(m.team == LaneTeam.Blue ? LaneTeam.Red : LaneTeam.Blue);
                    }
                }

                m.windingUp = false;
                if (!m.motor.IsMoving || Vector2.Distance(m.motor.Destination, tPos) > 1.0f)
                {
                    map.TryFindRoute(m.motor.Position, tPos, towers[0].alive, towers[1].alive, scratchRoute);
                    if (scratchRoute.Count > 0) m.motor.TrySetDestination(ActiveCourse(), scratchRoute[scratchRoute.Count - 1]);
                }

                float speed = tuning.minionSpeed * (1f - Mathf.Clamp(m.slowAmount, 0f, 0.7f));
                m.motor.Tick(dt, new MovementTuning
                {
                    MaxSpeed = speed,
                    Acceleration = 10f,
                    Deceleration = 14f,
                    TurnRate = 360f,
                    TurnSlowdownAngle = 85f,
                });
            }
        }

        private void AcquireMinionTarget(MinionState m)
        {
            float aggroRadius = 6.2f;
            float bestDistSq = aggroRadius * aggroRadius;
            m.targetKind = LaneTargetKind.None;
            m.targetId = 0;

            // Priority 1: nearest enemy minion
            for (int i = 0; i < minions.Count; i++)
            {
                MinionState other = minions[i];
                if (other.team == m.team || other.hp <= 0f) continue;
                float dSq = (other.motor.Position - m.motor.Position).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    m.targetKind = LaneTargetKind.Minion;
                    m.targetId = other.id;
                }
            }
            if (m.targetKind != LaneTargetKind.None) return;

            // Priority 2: active enemy illusion or enemy hero (creeps never target Chain Laser decoys)
            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                if (ill.team == m.team || ill.hp <= 0f || ill.isLaserDecoy) continue;
                float dSq = (ill.motor.Position - m.motor.Position).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    m.targetKind = LaneTargetKind.Hero;
                    m.targetId = ill.id;
                }
            }
            HeroState enemy = GetEnemyHero(m.team);
            if (IsHostileHeroTargetable(enemy))
            {
                float dSq = (enemy.motor.Position - m.motor.Position).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    m.targetKind = LaneTargetKind.Hero;
                    m.targetId = enemy.id;
                }
            }
            if (m.targetKind != LaneTargetKind.None) return;

            // Priority 3: enemy tower
            TowerState tower = GetEnemyTower(m.team);
            if (tower.alive && Vector2.Distance(tower.pos, m.motor.Position) <= aggroRadius + tuning.towerRadius)
            {
                m.targetKind = LaneTargetKind.Tower;
                m.targetId = tower.id;
            }
        }

        private void FireMinionAttack(MinionState m, Vector2 tPos)
        {
            float dmg = m.ranged ? tuning.minionRangedDamage : tuning.minionMeleeDamage;
            if (m.ranged)
            {
                projectiles.Add(new ProjectileState
                {
                    id = AllocId(),
                    team = m.team,
                    kind = LaneProjKind.MinionBolt,
                    sourceHeroId = 0,
                    sourceMinionId = m.id,
                    pos = m.motor.Position,
                    speed = 11.5f,
                    damage = dmg,
                    isMagic = false,
                    targetKind = m.targetKind,
                    targetId = m.targetId,
                    lastKnownTarget = tPos,
                });
                PushEvent(LaneEventKind.MinionShot, m.team, m.motor.Position, DirectionTo(m.motor.Position, tPos, Vector2.right), 0.4f, dmg, string.Empty);
                return;
            }

            if (m.targetKind == LaneTargetKind.Hero)
            {
                HeroState enemy = GetEnemyHero(m.team);
                if (enemy.id == m.targetId) DealDamageToHero(enemy, dmg, false, false, 0, m.team, isBasicOrMinion: true);
                else
                {
                    for (int i = 0; i < illusions.Count; i++)
                    {
                        if (illusions[i].id == m.targetId) DealDamageToIllusion(illusions[i], dmg, false, 0, m.team);
                    }
                }
            }
            else if (m.targetKind == LaneTargetKind.Minion)
            {
                for (int i = 0; i < minions.Count; i++)
                {
                    if (minions[i].id == m.targetId)
                    {
                        DealDamageToMinion(minions[i], dmg, false, 0, m.team);
                        break;
                    }
                }
            }
            else if (m.targetKind == LaneTargetKind.Tower)
            {
                TowerState tower = GetEnemyTower(m.team);
                if (tower.id == m.targetId) DealDamageToTower(tower, dmg, 0, m.team);
            }
        }

        private void TickTowers(float dt)
        {
            for (int i = 0; i < towers.Length; i++)
            {
                TowerState t = towers[i];
                if (!t.alive) continue;

                t.fireCooldown = Mathf.Max(0f, t.fireCooldown - dt);
                if (!IsValidHostileTarget(t.team, t.targetKind, t.targetId, out Vector2 tPos, out _) ||
                    Vector2.Distance(t.pos, tPos) > tuning.towerAttackRange)
                {
                    t.aggroLockedOnHero = false;
                    AcquireTowerTarget(t);
                }

                if (t.targetKind != LaneTargetKind.None && t.fireCooldown <= 0f &&
                    IsValidHostileTarget(t.team, t.targetKind, t.targetId, out tPos, out _))
                {
                    t.fireCooldown = tuning.towerAttackInterval;
                    projectiles.Add(new ProjectileState
                    {
                        id = AllocId(),
                        team = t.team,
                        kind = LaneProjKind.TowerShell,
                        sourceHeroId = 0,
                        pos = t.pos,
                        speed = tuning.towerProjectileSpeed,
                        damage = tuning.towerAttackDamage,
                        isMagic = false,
                        targetKind = t.targetKind,
                        targetId = t.targetId,
                        lastKnownTarget = tPos,
                    });
                    PushEvent(LaneEventKind.TowerShot, t.team, t.pos, DirectionTo(t.pos, tPos, Vector2.right), 0.7f, tuning.towerAttackDamage, string.Empty);
                }
            }
        }

        private void AcquireTowerTarget(TowerState t)
        {
            float rangeSq = tuning.towerAttackRange * tuning.towerAttackRange;
            t.targetKind = LaneTargetKind.None;
            t.targetId = 0;

            // Classic lane rule: tower targets enemy minions inside range first!
            float bestMinionSq = rangeSq;
            for (int i = 0; i < minions.Count; i++)
            {
                MinionState m = minions[i];
                if (m.team == t.team || m.hp <= 0f) continue;
                float dSq = (m.motor.Position - t.pos).sqrMagnitude;
                if (dSq <= bestMinionSq)
                {
                    bestMinionSq = dSq;
                    t.targetKind = LaneTargetKind.Minion;
                    t.targetId = m.id;
                }
            }
            if (t.targetKind != LaneTargetKind.None) return;

            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                if (ill.team == t.team || ill.hp <= 0f || ill.isLaserDecoy) continue;
                if ((ill.motor.Position - t.pos).sqrMagnitude <= rangeSq)
                {
                    t.targetKind = LaneTargetKind.Hero;
                    t.targetId = ill.id;
                    return;
                }
            }

            HeroState enemy = GetEnemyHero(t.team);
            if (IsHostileHeroTargetable(enemy) && (enemy.motor.Position - t.pos).sqrMagnitude <= rangeSq)
            {
                t.targetKind = LaneTargetKind.Hero;
                t.targetId = enemy.id;
            }
        }

        private void TickProjectiles(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                ProjectileState p = projectiles[i];
                if (!IsValidHostileTarget(p.team, p.targetKind, p.targetId, out Vector2 tPos, out float tRad))
                {
                    projectiles.RemoveAt(i);
                    continue;
                }

                p.lastKnownTarget = tPos;
                Vector2 delta = tPos - p.pos;
                float dist = delta.magnitude;
                float step = p.speed * dt;
                bool arrives = dist <= step + tRad * 0.45f;
                Vector2 nextPos = arrives ? tPos : p.pos + delta.normalized * step;

                if (p.kind != LaneProjKind.TowerShell)
                {
                    bool blockedByDecoy = false;
                    for (int j = 0; j < illusions.Count; j++)
                    {
                        IllusionState ill = illusions[j];
                        if (!ill.isLaserDecoy || ill.team == p.team || ill.hp <= 0f) continue;
                        float hitR = ill.decoyRadius + 0.25f;
                        if (DistPointToSegmentSq(ill.motor.Position, p.pos, nextPos) <= hitR * hitR)
                        {
                            DealDamageToIllusion(ill, p.damage, p.isMagic, p.sourceHeroId, p.team, p.sourceMinionId);
                            projectiles.RemoveAt(i);
                            blockedByDecoy = true;
                            break;
                        }
                    }
                    if (blockedByDecoy) continue;
                }

                if (arrives)
                {
                    DeliverProjectileHit(p);
                    projectiles.RemoveAt(i);
                    continue;
                }

                p.pos = nextPos;
            }
        }

        private static float DistPointToSegmentSq(Vector2 pt, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.00001f) return (pt - a).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(pt - a, ab) / lenSq);
            Vector2 proj = a + ab * t;
            return (pt - proj).sqrMagnitude;
        }

        private void DeliverProjectileHit(ProjectileState p)
        {
            if (p.targetKind == LaneTargetKind.Hero)
            {
                HeroState enemy = GetEnemyHero(p.team);
                if (enemy.id == p.targetId)
                {
                    bool isBasicOrMinion = p.kind == LaneProjKind.LuisBolt || p.kind == LaneProjKind.MinionBolt;
                    DealDamageToHero(enemy, p.damage, p.isMagic, false, p.sourceHeroId, p.team, isBasicOrMinion: isBasicOrMinion);
                    return;
                }
                for (int i = 0; i < illusions.Count; i++)
                {
                    if (illusions[i].id == p.targetId)
                    {
                        DealDamageToIllusion(illusions[i], p.damage, p.isMagic, p.sourceHeroId, p.team);
                        return;
                    }
                }
            }
            else if (p.targetKind == LaneTargetKind.Minion)
            {
                for (int i = 0; i < minions.Count; i++)
                {
                    if (minions[i].id == p.targetId)
                    {
                        DealDamageToMinion(minions[i], p.damage, p.isMagic, p.sourceHeroId, p.team);
                        return;
                    }
                }
            }
            else if (p.targetKind == LaneTargetKind.Tower)
            {
                TowerState tower = GetEnemyTower(p.team);
                if (tower.id == p.targetId)
                {
                    DealDamageToTower(tower, p.damage, p.sourceHeroId, p.team);
                }
            }
        }
    }
}
