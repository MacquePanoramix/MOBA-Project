using System.Collections.Generic;
using Moba.MovementLab;
using UnityEngine;

namespace Moba.LaneSlice
{
    public sealed partial class LaneMatch
    {
        private readonly List<(LaneTargetKind kind, int id, Vector2 pos)> scratchBeamTargets =
            new List<(LaneTargetKind, int, Vector2)>();
        private readonly List<(LaneTargetKind kind, int id, Vector2 pos, int stacks)> scratchShockSources =
            new List<(LaneTargetKind, int, Vector2, int)>();
        private readonly HashSet<int> scratchChainVisited = new HashSet<int>();

        private float LaserStackMultiplier(HeroState shooter, int targetStacks)
        {
            if (shooter == null || targetStacks <= 0) return 1f;
            float maxAmp = tuning.laserStackMaxAmpFormula.Evaluate(1, shooter.intelligence);
            float ratio = targetStacks / (targetStacks + Mathf.Max(1f, tuning.laserStackHalfPoint));
            return 1f + maxAmp * ratio;
        }

        private void ApplyLaserStackHero(HeroState victim)
        {
            if (victim == null || victim.phase == LanePhase.Dead) return;
            victim.laserStacks++;
            victim.laserStackTimer = tuning.laserStackDuration;
        }

        private void ApplyLaserStackMinion(MinionState victim)
        {
            if (victim == null || victim.hp <= 0f) return;
            victim.laserStacks++;
            victim.laserStackTimer = tuning.laserStackDuration;
        }

        private void ApplyLaserStackIllusion(IllusionState victim)
        {
            if (victim == null || victim.hp <= 0f) return;
            victim.laserStacks++;
            victim.laserStackTimer = tuning.laserStackDuration;
        }

        private void ResolveLaserDecoyBlocking(HeroState h)
        {
            if (h == null || h.phase == LanePhase.Dead || h.phase == LanePhase.ExecuteDash) return;
            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                if (!ill.isLaserDecoy || ill.team == h.team || ill.hp <= 0f) continue;

                Vector2 delta = h.motor.Position - ill.motor.Position;
                float dist = delta.magnitude;
                float minDist = ill.decoyRadius + 0.48f;
                if (dist < minDist)
                {
                    Vector2 pushDir = dist > 0.001f ? delta / dist : -DirectionTo(h.motor.Position, ill.motor.Position, h.aimDir);
                    Vector2 desired = ill.motor.Position + pushDir * minDist;
                    Vector2 safePos = map.NearestWalkable(desired, towers[0].alive, towers[1].alive);
                    h.motor.SetPositionPreserveSpeed(safePos);
                }
            }
        }

        private void FireChainLaserAttack(HeroState h, LaneTargetKind firstKind, int firstId, Vector2 firstPos, float damageScale, bool isUltProc)
        {
            if (firstKind == LaneTargetKind.Tower)
            {
                float twDmg = h.attackDamage * damageScale;
                ApplyMagicOrPhysicalToTarget(h, firstKind, firstId, twDmg, isMagic: false, isBasicAttack: !isUltProc);
                PushLaserBeam(h.team, h.motor.Position + h.aimDir * 0.42f, firstPos, 0.16f, isUltProc);
                return;
            }

            scratchChainVisited.Clear();
            scratchChainVisited.Add(firstId);
            Vector2 startPos = h.motor.Position + h.aimDir * 0.42f;
            PushLaserBeam(h.team, startPos, firstPos, isUltProc ? 0.14f : 0.18f, isUltProc);

            float firstDmg = h.attackDamage * damageScale;
            ApplyChainHitToEntity(h, firstKind, firstId, firstDmg, isBasicAttack: !isUltProc);

            Vector2 prevPos = firstPos;
            for (int hop = 1; ; hop++)
            {
                if (!FindClosestUnvisitedChainTarget(h.team, prevPos, scratchChainVisited,
                        out LaneTargetKind nextKind, out int nextId, out Vector2 nextPos))
                {
                    break;
                }
                scratchChainVisited.Add(nextId);
                float ratio = Mathf.Max(tuning.laserChainMinRatio, Mathf.Pow(tuning.laserChainDecay, hop));
                PushLaserBeam(h.team, prevPos, nextPos, 0.10f, isUltProc);
                ApplyChainHitToEntity(h, nextKind, nextId, h.attackDamage * damageScale * ratio, isBasicAttack: false);
                prevPos = nextPos;
            }
        }

        private void ApplyChainHitToEntity(HeroState shooter, LaneTargetKind kind, int targetId, float baseDamage, bool isBasicAttack)
        {
            if (kind == LaneTargetKind.Hero)
            {
                HeroState enemy = GetEnemyHero(shooter.team);
                if (enemy.id == targetId && enemy.phase != LanePhase.Dead)
                {
                    float dmg = baseDamage * LaserStackMultiplier(shooter, enemy.laserStacks);
                    DealDamageToHero(enemy, dmg, isMagic: false, isTrueDamage: false, shooter.id, shooter.team,
                        triggerMiravelWard: true, flags: isBasicAttack ? (DamageFlags.BasicOrMinion | DamageFlags.OnHit) : DamageFlags.None);
                    ApplyLaserStackHero(enemy);
                    return;
                }
                for (int i = 0; i < illusions.Count; i++)
                {
                    IllusionState ill = illusions[i];
                    if (ill.id == targetId && ill.team != shooter.team && ill.hp > 0f)
                    {
                        float dmg = baseDamage * LaserStackMultiplier(shooter, ill.laserStacks);
                        DealDamageToIllusion(ill, dmg, false, shooter.id, shooter.team);
                        ApplyLaserStackIllusion(ill);
                        return;
                    }
                }
            }
            else if (kind == LaneTargetKind.Minion)
            {
                for (int i = 0; i < minions.Count; i++)
                {
                    MinionState m = minions[i];
                    if (m.id == targetId && m.team != shooter.team && m.hp > 0f)
                    {
                        float dmg = baseDamage * LaserStackMultiplier(shooter, m.laserStacks);
                        DealDamageToMinion(m, dmg, false, shooter.id, shooter.team, isOnHit: isBasicAttack);
                        ApplyLaserStackMinion(m);
                        return;
                    }
                }
            }
        }

        private bool FindClosestUnvisitedChainTarget(
            LaneTeam selfTeam,
            Vector2 from,
            HashSet<int> visited,
            out LaneTargetKind kind,
            out int targetId,
            out Vector2 pos)
        {
            HeroState shooter = GetHero(selfTeam);
            float chainRadius = tuning.laserChainRadiusFormula.Evaluate(1, shooter.intelligence);
            float bestDistSq = chainRadius * chainRadius;
            kind = LaneTargetKind.None;
            targetId = 0;
            pos = Vector2.zero;

            HeroState enemy = GetEnemyHero(selfTeam);
            if (IsHostileHeroTargetable(enemy) && !visited.Contains(enemy.id))
            {
                float dSq = (enemy.motor.Position - from).sqrMagnitude;
                if (dSq <= bestDistSq)
                {
                    bestDistSq = dSq;
                    kind = LaneTargetKind.Hero;
                    targetId = enemy.id;
                    pos = enemy.motor.Position;
                }
            }

            for (int i = 0; i < minions.Count; i++)
            {
                MinionState m = minions[i];
                if (m.team == selfTeam || m.hp <= 0f || visited.Contains(m.id)) continue;
                float dSq = (m.motor.Position - from).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    kind = LaneTargetKind.Minion;
                    targetId = m.id;
                    pos = m.motor.Position;
                }
            }

            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                if (ill.team == selfTeam || ill.hp <= 0f || visited.Contains(ill.id)) continue;
                float dSq = (ill.motor.Position - from).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    kind = LaneTargetKind.Hero;
                    targetId = ill.id;
                    pos = ill.motor.Position;
                }
            }

            return targetId > 0;
        }

        private void CastLaserQ(HeroState h, Vector2 cursorPoint)
        {
            Vector2 dir = DirectionTo(h.motor.Position, cursorPoint, h.aimDir);
            h.motor.Stop();
            h.attackTargetKind = LaneTargetKind.None;
            h.isAttackMoving = false;
            h.aimDir = dir;
            h.motor = new MovementMotor(h.motor.Position, AngleDegrees(dir));

            float speedMult = Mathf.Max(1f, tuning.laserQSpeedFormula.Evaluate(h.rankQ, h.intelligence));
            h.phase = LanePhase.Casting;
            h.activeSlot = LaneAbilitySlot.Q;
            h.phaseDuration = tuning.laserQDuration / speedMult;
            h.phaseTimer = h.phaseDuration;
            h.laserQCurRange = tuning.laserQStartRange;
            h.laserQKillCount = 0;
            h.laserQHitIds.Clear();
            h.cdQ = ApplyCdr(h, tuning.laserQCooldown - 0.7f * (h.rankQ - 1));
            h.maxCdQ = h.cdQ;
            OnHeroCastAbilityForCurse(h, false);
        }

        private void TickLaserQBeam(HeroState h, float dt)
        {
            h.phaseTimer = Mathf.Max(0f, h.phaseTimer - dt);
            float progress = 1f - Mathf.Clamp01(h.phaseTimer / Mathf.Max(0.01f, h.phaseDuration));
            h.laserQCurRange = Mathf.Lerp(tuning.laserQStartRange, tuning.laserQMaxRange, progress);

            Vector2 origin = h.motor.Position;
            Vector2 fwd = h.aimDir;
            float baseDmg = tuning.laserQDamageFormula.Evaluate(h.rankQ, h.attackDamage);

            HeroState enemy = GetEnemyHero(h.team);
            if (IsHostileHeroTargetable(enemy) && !h.laserQHitIds.Contains(enemy.id) &&
                IsInBeam(enemy.motor.Position, origin, fwd, h.laserQCurRange, tuning.laserQHalfWidth + 0.45f))
            {
                h.laserQHitIds.Add(enemy.id);
                float dmg = baseDmg * LaserStackMultiplier(h, enemy.laserStacks);
                DealDamageToHero(enemy, dmg, isMagic: false, isTrueDamage: false, h.id, h.team);
                ApplyLaserStackHero(enemy);
                if (enemy.phase == LanePhase.Dead) AwardLaserQKillBonus(h, enemy.motor.Position);
            }

            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                if (ill.team == h.team || ill.hp <= 0f || h.laserQHitIds.Contains(ill.id)) continue;
                float pad = ill.isLaserDecoy ? ill.decoyRadius : 0.45f;
                if (IsInBeam(ill.motor.Position, origin, fwd, h.laserQCurRange, tuning.laserQHalfWidth + pad))
                {
                    h.laserQHitIds.Add(ill.id);
                    float dmg = baseDmg * LaserStackMultiplier(h, ill.laserStacks);
                    DealDamageToIllusion(ill, dmg, false, h.id, h.team);
                    ApplyLaserStackIllusion(ill);
                }
            }

            for (int i = 0; i < minions.Count; i++)
            {
                MinionState m = minions[i];
                if (m.team == h.team || m.hp <= 0f || h.laserQHitIds.Contains(m.id)) continue;
                if (IsInBeam(m.motor.Position, origin, fwd, h.laserQCurRange, tuning.laserQHalfWidth + 0.35f))
                {
                    h.laserQHitIds.Add(m.id);
                    float dmg = baseDmg * LaserStackMultiplier(h, m.laserStacks);
                    DealDamageToMinion(m, dmg, false, h.id, h.team);
                    ApplyLaserStackMinion(m);
                    if (m.hp <= 0f) AwardLaserQKillBonus(h, m.motor.Position);
                }
            }

            if (h.phaseTimer <= 0f)
            {
                h.laserQCurRange = 0f;
                h.phase = LanePhase.Recover;
                h.phaseDuration = 0.14f;
                h.phaseTimer = h.phaseDuration;
            }
        }

        private void AwardLaserQKillBonus(HeroState h, Vector2 killPos)
        {
            h.laserQKillCount++;
            if (h.laserQKillCount >= 2)
            {
                float bonus = (h.laserQKillCount - 1) * tuning.laserQMultiKillBonusGold;
                h.goldExact += bonus;
                PushEvent(LaneEventKind.LaserGoldBonus, h.team, killPos, Vector2.up, 1.0f, bonus,
                    $"+{Mathf.RoundToInt(bonus)}g BEAM!");
            }
        }

        private void CastLaserW(HeroState h)
        {
            scratchShockSources.Clear();
            Vector2 selfPos = h.motor.Position;
            float rSq = tuning.laserWCastRange * tuning.laserWCastRange;

            HeroState enemy = GetEnemyHero(h.team);
            if (enemy.phase != LanePhase.Dead && enemy.laserStacks > 0 &&
                (enemy.motor.Position - selfPos).sqrMagnitude <= rSq)
            {
                scratchShockSources.Add((LaneTargetKind.Hero, enemy.id, enemy.motor.Position, enemy.laserStacks));
            }
            for (int i = 0; i < minions.Count; i++)
            {
                MinionState m = minions[i];
                if (m.team == h.team || m.hp <= 0f || m.laserStacks <= 0) continue;
                if ((m.motor.Position - selfPos).sqrMagnitude <= rSq)
                {
                    scratchShockSources.Add((LaneTargetKind.Minion, m.id, m.motor.Position, m.laserStacks));
                }
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                if (ill.team == h.team || ill.hp <= 0f || ill.laserStacks <= 0) continue;
                if ((ill.motor.Position - selfPos).sqrMagnitude <= rSq)
                {
                    scratchShockSources.Add((LaneTargetKind.Hero, ill.id, ill.motor.Position, ill.laserStacks));
                }
            }

            if (scratchShockSources.Count == 0) return;
            if (!TryConsumeHeroAbilityResource(h, LaneAbilitySlot.W)) return;

            h.cdW = ApplyCdr(h, tuning.laserWCooldown - 0.7f * (h.rankW - 1));
            h.maxCdW = h.cdW;
            OnHeroCastAbilityForCurse(h, false);

            var hitCounts = new Dictionary<int, int>();
            float baseShockDmg = tuning.laserWDamageFormula.Evaluate(h.rankW, h.intelligence);
            float shockRadius = tuning.laserWShockRadiusFormula.Evaluate(h.rankW, h.intelligence);

            for (int s = 0; s < scratchShockSources.Count; s++)
            {
                (_, _, Vector2 srcPos, int srcStacks) = scratchShockSources[s];
                float shockDmg = baseShockDmg * (1f + 0.12f * Mathf.Max(0, srcStacks - 1));
                PushEvent(LaneEventKind.LaserShock, h.team, srcPos, Vector2.up, shockRadius, srcStacks, string.Empty);

                if (enemy.phase != LanePhase.Dead &&
                    Vector2.Distance(enemy.motor.Position, srcPos) <= shockRadius + 0.45f)
                {
                    float dmg = shockDmg * LaserStackMultiplier(h, enemy.laserStacks);
                    DealDamageToHero(enemy, dmg, isMagic: true, isTrueDamage: false, h.id, h.team);
                    hitCounts[enemy.id] = hitCounts.TryGetValue(enemy.id, out int c) ? c + 1 : 1;
                }

                for (int i = 0; i < minions.Count; i++)
                {
                    MinionState m = minions[i];
                    if (m.team == h.team || m.hp <= 0f) continue;
                    if (Vector2.Distance(m.motor.Position, srcPos) <= shockRadius + 0.35f)
                    {
                        float dmg = shockDmg * LaserStackMultiplier(h, m.laserStacks);
                        DealDamageToMinion(m, dmg, true, h.id, h.team);
                        if (m.hp > 0f)
                        {
                            hitCounts[m.id] = hitCounts.TryGetValue(m.id, out int c) ? c + 1 : 1;
                        }
                    }
                }
            }

            if (tuning.laserWConsumesStacks)
            {
                if (enemy.phase != LanePhase.Dead && (enemy.motor.Position - selfPos).sqrMagnitude <= rSq)
                {
                    enemy.laserStacks = 0;
                    enemy.laserStackTimer = 0f;
                }
                for (int i = 0; i < minions.Count; i++)
                {
                    if (minions[i].team != h.team && (minions[i].motor.Position - selfPos).sqrMagnitude <= rSq)
                    {
                        minions[i].laserStacks = 0;
                        minions[i].laserStackTimer = 0f;
                    }
                }
            }

            // Stack overlapping AND refreshed shock slows using bounded strength + unbounded sqrt duration
            foreach (var kv in hitCounts)
            {
                int n = kv.Value;
                if (kv.Key == enemy.id && enemy.phase != LanePhase.Dead)
                {
                    ApplyLaserWSlow(enemy, n, h.rankW);
                }
                else
                {
                    for (int i = 0; i < minions.Count; i++)
                    {
                        if (minions[i].id == kv.Key && minions[i].hp > 0f)
                        {
                            ApplyLaserWSlow(minions[i], n, h.rankW);
                            break;
                        }
                    }
                }
            }
        }

        private void CastLaserE(HeroState h, Vector2 cursorPoint)
        {
            Vector2 dir = DirectionTo(h.motor.Position, cursorPoint, h.aimDir);
            Vector2 startPos = h.motor.Position;
            Vector2 stepDest = map.PushOut(startPos, -dir * tuning.laserEStepBack, towers[0].alive, towers[1].alive);
            float stepDuration = tuning.laserEStepDurationFormula.Evaluate(h.rankE, h.agility);

            ClearAttackAndRoute(h);
            h.aimDir = dir;
            h.motor = new MovementMotor(startPos, AngleDegrees(dir));

            h.phase = LanePhase.Casting;
            h.activeSlot = LaneAbilitySlot.E;
            h.phaseDuration = stepDuration;
            h.phaseTimer = stepDuration;
            h.laserEStepStartPos = startPos;
            h.laserEStepEndPos = stepDest;
            h.laserEStepPending = true;

            h.cdE = ApplyCdr(h, tuning.laserECooldown - 0.9f * (h.rankE - 1));
            h.maxCdE = h.cdE;
            OnHeroCastAbilityForCurse(h, false);
        }

        private void CompleteLaserEStep(HeroState h)
        {
            h.laserEStepPending = false;
            h.motor = new MovementMotor(h.laserEStepEndPos, AngleDegrees(h.aimDir));
            h.prevMotorPos = h.laserEStepEndPos;

            Vector2 decoyPos = map.NearestWalkable(h.laserEStepStartPos, towers[0].alive, towers[1].alive);
            illusions.Add(new IllusionState
            {
                id = AllocId(),
                ownerHeroId = h.id,
                team = h.team,
                motor = new MovementMotor(decoyPos, AngleDegrees(h.aimDir)),
                hp = tuning.laserEDecoyHp,
                maxHp = tuning.laserEDecoyHp,
                timeLeft = tuning.laserEDecoyDuration,
                dest = decoyPos,
                armor = 0f,
                magicResist = 0f,
                isLaserDecoy = true,
                decoyRadius = tuning.laserEDecoyRadius,
            });
            PushEvent(LaneEventKind.LaserDecoy, h.team, decoyPos, h.aimDir, tuning.laserEDecoyRadius, 0f, string.Empty);

            h.phase = LanePhase.Idle;
            h.activeSlot = LaneAbilitySlot.None;
            h.phaseDuration = 0f;
            h.phaseTimer = 0f;
        }

        private void CastLaserR(HeroState h, Vector2 cursorPoint)
        {
            Vector2 dir = DirectionTo(h.motor.Position, cursorPoint, h.aimDir);
            h.motor.Stop();
            h.attackTargetKind = LaneTargetKind.None;
            h.isAttackMoving = false;
            h.aimDir = dir;
            h.motor = new MovementMotor(h.motor.Position, AngleDegrees(dir));

            h.phase = LanePhase.Casting;
            h.activeSlot = LaneAbilitySlot.R;
            h.phaseDuration = tuning.laserRWindup;
            h.phaseTimer = h.phaseDuration;
            h.laserQCurRange = 0f;
            h.laserRProcAccum = 0f;
            h.cdR = ApplyCdr(h, tuning.laserRCooldown - 4f * (h.rankR - 1));
            h.maxCdR = h.cdR;
            OnHeroCastAbilityForCurse(h, false);
        }

        private void FireLaserRBeamProc(HeroState h)
        {
            scratchBeamTargets.Clear();
            Vector2 origin = h.motor.Position;
            Vector2 fwd = h.aimDir;
            PushLaserBeam(h.team, origin + fwd * 0.45f, origin + fwd * tuning.laserRRange, tuning.laserRHalfWidth * 2f, isUlt: true);

            HeroState enemy = GetEnemyHero(h.team);
            if (IsHostileHeroTargetable(enemy) &&
                IsInBeam(enemy.motor.Position, origin, fwd, tuning.laserRRange, tuning.laserRHalfWidth + 0.45f))
            {
                scratchBeamTargets.Add((LaneTargetKind.Hero, enemy.id, enemy.motor.Position));
            }
            for (int i = 0; i < minions.Count; i++)
            {
                MinionState m = minions[i];
                if (m.team == h.team || m.hp <= 0f) continue;
                if (IsInBeam(m.motor.Position, origin, fwd, tuning.laserRRange, tuning.laserRHalfWidth + 0.35f))
                {
                    scratchBeamTargets.Add((LaneTargetKind.Minion, m.id, m.motor.Position));
                }
            }
            for (int i = 0; i < illusions.Count; i++)
            {
                IllusionState ill = illusions[i];
                if (ill.team == h.team || ill.hp <= 0f) continue;
                float pad = ill.isLaserDecoy ? ill.decoyRadius : 0.45f;
                if (IsInBeam(ill.motor.Position, origin, fwd, tuning.laserRRange, tuning.laserRHalfWidth + pad))
                {
                    scratchBeamTargets.Add((LaneTargetKind.Hero, ill.id, ill.motor.Position));
                }
            }

            for (int i = 0; i < scratchBeamTargets.Count; i++)
            {
                (LaneTargetKind k, int id, Vector2 pos) = scratchBeamTargets[i];
                FireChainLaserAttack(h, k, id, pos, tuning.laserRProcDamageScale, isUltProc: true);
            }
        }
    }
}
