using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Full-lane sparring bot supporting Ogre, Miravel (Hero Luis), Old Man Swordsman, and Chain Laser Shooter:
    /// levels skills, purchases starter items at fountain, respects tower aggro, contests orbs,
    /// and executes each kit's signature combos.
    /// </summary>
    public sealed class LaneBot
    {
        private static readonly int[] OgreBuild = { 15, 1, 2, 9, 8, 18 };         // Buckler, Iron Greatsword, Quickstep Dagger, Zephyr Feather (AGI windup), Titan's Knuckle (STR), Colossus Pauldron
        private static readonly int[] MiravelBuild = { 14, 11, 10, 19, 12, 17 }; // Woodland Salve, Kindred Charm (HealPower), Arcane Lens (INT), Spellweaver's Hourglass, Coward's Shield, Momentum Greaves
        private static readonly int[] SwordsmanBuild = { 13, 1, 2, 16, 18, 20 }; // Miner's Hatchet, Iron Greatsword, Quickstep Dagger, Blighted Thorn, Colossus Pauldron, Executioner's Grudge
        private static readonly int[] LaserBuild = { 13, 1, 2, 10, 17, 20 };     // Miner's Hatchet, Iron Greatsword, Quickstep Dagger, Arcane Lens, Momentum Greaves, Executioner's Grudge

        private readonly LaneTeam team;
        private float decideAccum;
        private float comboStepAccum;
        private bool retreatingToFountain;
        private bool hasLastMove;
        private Vector2 lastMoveSent;
        private bool hasLastAttackMove;
        private Vector2 lastAttackMoveSent;

        public LaneBot(LaneTeam team)
        {
            this.team = team;
        }

        public void Tick(LaneMatch match, float dt)
        {
            if (match == null || match.IsGameOver) return;

            LaneSnapshot snap = match.Snapshot();
            HeroSnap self = FindHero(snap, team);
            HeroSnap enemy = FindHero(snap, team == LaneTeam.Blue ? LaneTeam.Red : LaneTeam.Blue);
            if (self == null || enemy == null) return;

            // Level abilities promptly
            if (self.skillPoints > 0)
            {
                TrySkillUp(match, self);
            }

            // Buy items whenever Dead or inside Fountain
            bool inFountain = match.Map.IsInFountain(team, new Vector2(self.x, self.y));
            if (self.phase == LanePhase.Dead || inFountain)
            {
                TryShopNextItem(match, self);
            }

            if (self.phase == LanePhase.Dead)
            {
                hasLastMove = false;
                hasLastAttackMove = false;
                return;
            }

            // Ogre R channel feeds rapid Q/W combo queue!
            if (self.heroId == LaneHeroId.Ogre && self.phase == LanePhase.Channeling && self.activeSlot == LaneAbilitySlot.R)
            {
                comboStepAccum += dt;
                if (comboStepAccum >= 0.28f && self.rComboCount < match.Tuning.ogreRMaxStrikes)
                {
                    comboStepAccum = 0f;
                    Vector2 aim = new Vector2(enemy.x, enemy.y);
                    LaneOrderKind pick = (self.rComboCount % 2 == 0) ? LaneOrderKind.CastQ : LaneOrderKind.CastW;
                    hasLastMove = false;
                    hasLastAttackMove = false;
                    match.EnqueueOrApplyOrder(new LaneOrder { team = team, kind = pick, point = aim });
                }
                return;
            }

            decideAccum += dt;
            if (decideAccum < 0.24f) return;
            decideAccum = 0f;

            Vector2 selfPos = new Vector2(self.x, self.y);
            Vector2 enemyPos = new Vector2(enemy.x, enemy.y);
            Vector2 ownFountain = match.Map.FountainPos(team);
            float hpPct = self.maxHp > 0f ? self.hp / self.maxHp : 1f;

            if (retreatingToFountain)
            {
                bool manaSafe = self.resourceKind != HeroResourceKind.Mana || self.maxMana <= 0f || (self.mana / self.maxMana) >= 0.45f;
                if (inFountain && hpPct >= 0.82f && manaSafe)
                {
                    retreatingToFountain = false;
                }
                else
                {
                    SendMoveIfNeeded(match, self, ownFountain);
                    return;
                }
            }
            else if (hpPct < 0.24f || (self.resourceKind == HeroResourceKind.Mana && self.maxMana > 0f && self.mana < 28f && hpPct < 0.70f))
            {
                retreatingToFountain = true;
                SendMoveIfNeeded(match, self, ownFountain);
                return;
            }

            // Miravel R channel stays still unless threatened right on top
            if (self.heroId == LaneHeroId.Miravel && self.phase == LanePhase.Channeling && self.activeSlot == LaneAbilitySlot.R)
            {
                return;
            }

            TowerSnap enemyTower = FindTower(snap, team == LaneTeam.Blue ? LaneTeam.Red : LaneTeam.Blue);
            bool enemyVisible = enemy.phase != LanePhase.Dead && !enemy.invisible;
            float enemyDist = enemyVisible ? Vector2.Distance(selfPos, enemyPos) : 99f;
            bool enemyIsOneShotDive = enemyVisible &&
                (enemy.hp / Mathf.Max(1f, enemy.maxHp)) < 0.18f &&
                hpPct > 0.55f &&
                enemyDist < 4.5f;

            // Tower safety guard: pull back if targeted by enemy tower or standing inside it without healthy wave cover
            if (enemyTower != null && enemyTower.alive && !enemyIsOneShotDive)
            {
                Vector2 tPos = new Vector2(enemyTower.x, enemyTower.y);
                float distToTower = Vector2.Distance(selfPos, tPos);
                bool towerTargetingMe = enemyTower.targetId == self.id;
                bool unsafeInside = distToTower <= enemyTower.attackRange + 0.9f &&
                    !HasHealthyFriendlyMinionUnderTower(snap, tPos, enemyTower.attackRange);
                if (towerTargetingMe || unsafeInside)
                {
                    Vector2 dirHome = (ownFountain - tPos).sqrMagnitude > 0.01f
                        ? (ownFountain - tPos).normalized
                        : (team == LaneTeam.Blue ? Vector2.left : Vector2.right);
                    Vector2 safeSpot = tPos + dirHome * (enemyTower.attackRange + 2.2f);
                    SendMoveIfNeeded(match, self, match.Map.ClampInsideLane(safeSpot));
                    return;
                }
            }

            // Pick up own orb or deny enemy orb if safely reachable
            if (TryStepOnOrb(snap, self, selfPos, enemyTower, out Vector2 orbPos))
            {
                SendMoveIfNeeded(match, self, orbPos);
                return;
            }

            switch (self.heroId)
            {
                case LaneHeroId.Ogre:
                    TickOgre(match, snap, self, enemy, enemyTower, ownFountain, selfPos, enemyPos, enemyVisible, enemyDist, enemyIsOneShotDive);
                    break;
                case LaneHeroId.Miravel:
                    TickMiravel(match, snap, self, enemy, enemyTower, ownFountain, selfPos, enemyPos, enemyVisible, enemyDist, enemyIsOneShotDive);
                    break;
                case LaneHeroId.Swordsman:
                    TickSwordsman(match, snap, self, enemy, enemyTower, ownFountain, selfPos, enemyPos, enemyVisible, enemyDist, enemyIsOneShotDive);
                    break;
                case LaneHeroId.LaserShooter:
                    TickLaserShooter(match, snap, self, enemy, enemyTower, ownFountain, selfPos, enemyPos, enemyVisible, enemyDist, enemyIsOneShotDive);
                    break;
            }
        }

        private void SendMoveIfNeeded(LaneMatch match, HeroSnap self, Vector2 destination)
        {
            if (self.speed > 0.5f && hasLastMove && Vector2.Distance(lastMoveSent, destination) < 0.6f)
            {
                return;
            }
            lastMoveSent = destination;
            hasLastMove = true;
            hasLastAttackMove = false;
            match.EnqueueOrApplyOrder(new LaneOrder { team = team, kind = LaneOrderKind.Move, point = destination });
        }

        private void SendNonMoveOrder(LaneMatch match, LaneOrder order)
        {
            hasLastMove = false;
            hasLastAttackMove = false;
            match.EnqueueOrApplyOrder(order);
        }

        private void SendAttackOrderIfNeeded(LaneMatch match, HeroSnap self, LaneOrder order)
        {
            LaneTargetKind expected = order.kind == LaneOrderKind.AttackHero
                ? LaneTargetKind.Hero
                : (order.kind == LaneOrderKind.AttackMinion ? LaneTargetKind.Minion : LaneTargetKind.Tower);
            if (self.attackTargetKind == (int)expected &&
                self.attackTargetId == order.targetId &&
                (self.phase == LanePhase.Windup || self.phase == LanePhase.Recover || self.phase == LanePhase.Idle))
            {
                hasLastMove = false;
                hasLastAttackMove = false;
                return;
            }
            SendNonMoveOrder(match, order);
        }

        private void SendAttackMoveIfNeeded(LaneMatch match, HeroSnap self, Vector2 destination)
        {
            if (self.isAttackMoving &&
                (self.phase == LanePhase.Windup ||
                 self.phase == LanePhase.Recover ||
                 (hasLastAttackMove && Vector2.Distance(lastAttackMoveSent, destination) < 1.0f)))
            {
                return;
            }
            hasLastMove = false;
            hasLastAttackMove = true;
            lastAttackMoveSent = destination;
            match.EnqueueOrApplyOrder(new LaneOrder { team = team, kind = LaneOrderKind.AttackMove, point = destination });
        }

        private void TickOgre(LaneMatch match, LaneSnapshot snap, HeroSnap self, HeroSnap enemy, TowerSnap enemyTower, Vector2 ownFountain,
            Vector2 selfPos, Vector2 enemyPos, bool enemyVisible, float enemyDist, bool allowTowerDive)
        {
            bool enemyUnderUnsafeTower = !allowTowerDive && IsUnsafeTowerZone(snap, self, enemyTower, enemyPos);

            if (enemyVisible && !enemyUnderUnsafeTower && enemyDist <= 3.6f && self.rankR >= 1 && self.cdR <= 0f && enemy.hp / enemy.maxHp < 0.78f)
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastR, point = enemyPos });
                return;
            }
            float eCastRange = self.ogreERadius > 0.5f ? self.ogreERadius - 0.15f : 2.15f;
            if (enemyVisible && !enemyUnderUnsafeTower && enemyDist <= Mathf.Max(1.8f, eCastRange) && self.rankE >= 1 && self.cdE <= 0f)
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastE, point = enemyPos });
                return;
            }
            if (enemyVisible && !enemyUnderUnsafeTower && enemyDist <= 2.85f && self.rankW >= 1 && self.cdW <= 0f)
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastW, point = enemyPos });
                return;
            }
            if (enemyVisible && enemyDist <= 4.35f && self.rankQ >= 1 && self.cdQ <= 0f)
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastQ, point = enemyPos });
                return;
            }

            // Otherwise use Q/E to clear waves or position safely ahead of friendly wave
            MinionSnap waveTarget = FindClosestEnemyMinion(snap, selfPos);
            if (waveTarget != null)
            {
                Vector2 mPos = new Vector2(waveTarget.x, waveTarget.y);
                float d = Vector2.Distance(selfPos, mPos);
                if (!IsUnsafeTowerZone(snap, self, enemyTower, mPos))
                {
                    if (d <= 2.5f && self.rankW >= 1 && self.cdW <= 0f)
                    {
                        SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastW, point = mPos });
                        return;
                    }
                    if (d <= 4.0f && self.rankQ >= 1 && self.cdQ <= 0f && CountNearbyEnemyMinions(snap, mPos, 2.2f) >= 2)
                    {
                        SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastQ, point = mPos });
                        return;
                    }
                }
                if (enemyVisible && !enemyUnderUnsafeTower && enemyDist < 7.5f)
                {
                    SendMoveIfNeeded(match, self, enemyPos);
                    return;
                }
            }

            Vector2 skirmishSpot = (enemyVisible && !enemyUnderUnsafeTower) ? enemyPos : MidLaneBias(team);
            skirmishSpot = ClampSafeOutsideEnemyTower(snap, self, enemyTower, ownFountain, skirmishSpot, allowTowerDive);
            SendMoveIfNeeded(match, self, skirmishSpot);
        }

        private void TickMiravel(LaneMatch match, LaneSnapshot snap, HeroSnap self, HeroSnap enemy, TowerSnap enemyTower, Vector2 ownFountain,
            Vector2 selfPos, Vector2 enemyPos, bool enemyVisible, float enemyDist, bool allowTowerDive)
        {
            if (enemyVisible && enemyDist <= 5.2f && self.rankE >= 1 && self.cdE <= 0f && self.mana >= self.costE)
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastE, point = selfPos });
            }

            if (enemyVisible && enemyDist <= 3.1f && self.hp / self.maxHp < 0.65f && self.rankW >= 1 && self.cdW <= 0f && self.mana >= self.costW)
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastW, point = enemyPos });
                return;
            }

            if (enemyVisible && enemyDist <= 4.6f && self.rankR >= 1 && self.cdR <= 0f && enemy.hp / enemy.maxHp < 0.80f &&
                self.mana >= Mathf.Max(30f, self.costR * 1.1f) &&
                !IsUnsafeTowerZone(snap, self, enemyTower, selfPos))
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastR, point = enemyPos });
                return;
            }

            // Kite close enemies while biasing retreat toward own fountain
            if (enemyVisible && enemyDist < 2.9f)
            {
                Vector2 away = (selfPos - enemyPos).sqrMagnitude > 0.01f
                    ? (selfPos - enemyPos).normalized
                    : (ownFountain - selfPos).normalized;
                Vector2 toHome = (ownFountain - selfPos).sqrMagnitude > 0.01f
                    ? (ownFountain - selfPos).normalized
                    : away;
                Vector2 kiteDir = (away * 0.65f + toHome * 0.35f).normalized;
                SendMoveIfNeeded(match, self, match.Map.ClampInsideLane(selfPos + kiteDir * 3.2f));
                return;
            }

            MinionSnap lowMinion = FindLowestEnemyMinion(snap, selfPos, 6.2f);
            if (lowMinion != null)
            {
                Vector2 mPos = new Vector2(lowMinion.x, lowMinion.y);
                if (!IsUnsafeTowerZone(snap, self, enemyTower, mPos) ||
                    Vector2.Distance(selfPos, mPos) <= match.Tuning.luisBasicRange - 0.3f)
                {
                    SendAttackOrderIfNeeded(match, self, new LaneOrder
                    {
                        team = team,
                        kind = LaneOrderKind.AttackMinion,
                        targetId = lowMinion.id,
                        point = mPos,
                    });
                    return;
                }
            }

            bool canSafelyPokeEnemy = enemyVisible &&
                (allowTowerDive || !IsUnsafeTowerZone(snap, self, enemyTower, selfPos));
            if (canSafelyPokeEnemy && enemyDist <= 6.2f)
            {
                // Avoid auto-attacking enemy hero inside enemy tower unless healthy wave covers
                if (enemyTower == null || !enemyTower.alive ||
                    Vector2.Distance(enemyPos, new Vector2(enemyTower.x, enemyTower.y)) > enemyTower.attackRange + 0.5f ||
                    allowTowerDive)
                {
                    SendAttackOrderIfNeeded(match, self, new LaneOrder
                    {
                        team = team,
                        kind = LaneOrderKind.AttackHero,
                        targetId = enemy.id,
                        point = enemyPos,
                    });
                    return;
                }
            }

            Vector2 advance = (enemyVisible && enemyDist < 9f && !IsUnsafeTowerZone(snap, self, enemyTower, enemyPos))
                ? enemyPos
                : MidLaneBias(team);
            advance = ClampSafeOutsideEnemyTower(snap, self, enemyTower, ownFountain, advance, allowTowerDive);
            SendAttackMoveIfNeeded(match, self, advance);
        }

        private void TickSwordsman(LaneMatch match, LaneSnapshot snap, HeroSnap self, HeroSnap enemy, TowerSnap enemyTower, Vector2 ownFountain,
            Vector2 selfPos, Vector2 enemyPos, bool enemyVisible, float enemyDist, bool allowTowerDive)
        {
            // Finish R execute target if marked and in range and low
            if (self.rankR >= 1 && self.cdR <= 0f && self.swordMarkedTargetId > 0)
            {
                if (enemyVisible && enemy.id == self.swordMarkedTargetId && enemyDist <= 7.2f && enemy.hp / enemy.maxHp <= 0.34f)
                {
                    SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastR, point = enemyPos });
                    return;
                }
            }

            bool enemyUnderUnsafeTower = !allowTowerDive && IsUnsafeTowerZone(snap, self, enemyTower, enemyPos);

            if (enemyVisible && !enemyUnderUnsafeTower && enemyDist <= 3.3f && self.rankQ >= 1 && self.cdQ <= 0f)
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastQ, point = enemyPos });
                return;
            }

            if (!enemyVisible || enemyUnderUnsafeTower || enemyDist > 3.2f)
            {
                MinionSnap lowMinion = FindLowestEnemyMinion(snap, selfPos, 5.2f);
                if (lowMinion != null)
                {
                    Vector2 mPos = new Vector2(lowMinion.x, lowMinion.y);
                    if (!IsUnsafeTowerZone(snap, self, enemyTower, mPos))
                    {
                        if (Vector2.Distance(selfPos, mPos) <= 3.2f &&
                            self.rankQ >= 1 &&
                            self.cdQ <= 0f &&
                            CountNearbyEnemyMinions(snap, mPos, 2.2f) >= 2)
                        {
                            SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastQ, point = mPos });
                            return;
                        }
                        SendAttackOrderIfNeeded(match, self, new LaneOrder
                        {
                            team = team,
                            kind = LaneOrderKind.AttackMinion,
                            targetId = lowMinion.id,
                            point = mPos,
                        });
                        return;
                    }
                }
            }

            if (enemyVisible && !enemyUnderUnsafeTower && enemyDist <= 4.8f)
            {
                SendAttackOrderIfNeeded(match, self, new LaneOrder
                {
                    team = team,
                    kind = LaneOrderKind.AttackHero,
                    targetId = enemy.id,
                    point = enemyPos,
                });
                return;
            }

            Vector2 advance = (enemyVisible && !enemyUnderUnsafeTower && enemyDist < 9.5f)
                ? enemyPos
                : MidLaneBias(team);
            advance = ClampSafeOutsideEnemyTower(snap, self, enemyTower, ownFountain, advance, allowTowerDive);
            SendAttackMoveIfNeeded(match, self, advance);
        }

        private void TickLaserShooter(LaneMatch match, LaneSnapshot snap, HeroSnap self, HeroSnap enemy, TowerSnap enemyTower, Vector2 ownFountain,
            Vector2 selfPos, Vector2 enemyPos, bool enemyVisible, float enemyDist, bool allowTowerDive)
        {
            bool enemyUnderUnsafeTower = !allowTowerDive && IsUnsafeTowerZone(snap, self, enemyTower, enemyPos);

            if (self.phase == LanePhase.Channeling && self.activeSlot == LaneAbilitySlot.R)
            {
                if (enemyVisible)
                {
                    Vector2 dirFromEnemy = (selfPos - enemyPos).sqrMagnitude > 0.01f
                        ? (selfPos - enemyPos).normalized
                        : (ownFountain - selfPos).normalized;
                    Vector2 aimSpot = enemyPos + dirFromEnemy * 5.2f;
                    aimSpot = ClampSafeOutsideEnemyTower(snap, self, enemyTower, ownFountain, aimSpot, allowTowerDive);
                    SendMoveIfNeeded(match, self, aimSpot);
                }
                return;
            }

            if (enemyVisible && !enemyUnderUnsafeTower && enemyDist <= 3.6f && self.rankE >= 1 && self.cdE <= 0f && self.mana >= self.costE)
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastE, point = enemyPos });
                return;
            }

            if (enemyVisible && !enemyUnderUnsafeTower && enemyDist <= 6.8f && self.rankR >= 1 && self.cdR <= 0f && self.mana >= self.costR)
            {
                SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastR, point = enemyPos });
                return;
            }

            if (self.rankW >= 1 && self.cdW <= 0f && self.mana >= self.costW)
            {
                float shockRange = match.Tuning.laserWCastRange - 0.3f;
                bool heroStacked = enemyVisible && enemy.laserStacks >= 1 && enemyDist <= shockRange;
                if (heroStacked || CountStackedEnemyMinions(snap, selfPos, shockRange) >= 2)
                {
                    SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastW, point = enemyVisible ? enemyPos : selfPos });
                    return;
                }
            }

            float eReserve = (self.rankE >= 1 && self.cdE <= 0f) ? self.costE : 0f;
            if (self.rankQ >= 1 && self.cdQ <= 0f && self.mana >= self.costQ + eReserve)
            {
                if (enemyVisible && !enemyUnderUnsafeTower && enemyDist <= match.Tuning.laserQMaxRange - 0.4f)
                {
                    SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastQ, point = enemyPos });
                    return;
                }
                MinionSnap qMinion = FindLowestEnemyMinion(snap, selfPos, match.Tuning.laserQMaxRange - 0.6f);
                if (qMinion != null)
                {
                    Vector2 qPos = new Vector2(qMinion.x, qMinion.y);
                    if (!IsUnsafeTowerZone(snap, self, enemyTower, qPos) && CountNearbyEnemyMinions(snap, qPos, 2.5f) >= 2)
                    {
                        SendNonMoveOrder(match, new LaneOrder { team = team, kind = LaneOrderKind.CastQ, point = qPos });
                        return;
                    }
                }
            }

            if (enemyVisible && enemyDist < 3.3f && !enemyUnderUnsafeTower)
            {
                Vector2 stepBack = (selfPos - enemyPos).sqrMagnitude > 0.01f
                    ? selfPos + (selfPos - enemyPos).normalized * 2.4f
                    : ownFountain;
                stepBack = ClampSafeOutsideEnemyTower(snap, self, enemyTower, ownFountain, stepBack, allowTowerDive);
                if (Vector2.Distance(stepBack, selfPos) > 0.6f)
                {
                    SendMoveIfNeeded(match, self, stepBack);
                    return;
                }
            }

            MinionSnap lowMinion = FindLowestEnemyMinion(snap, selfPos, 6.4f);
            if (lowMinion != null)
            {
                Vector2 mPos = new Vector2(lowMinion.x, lowMinion.y);
                if (!IsUnsafeTowerZone(snap, self, enemyTower, mPos) ||
                    Vector2.Distance(selfPos, mPos) <= match.Tuning.laserBasicRange - 0.3f)
                {
                    SendAttackOrderIfNeeded(match, self, new LaneOrder
                    {
                        team = team,
                        kind = LaneOrderKind.AttackMinion,
                        targetId = lowMinion.id,
                        point = mPos,
                    });
                    return;
                }
            }

            bool canPoke = enemyVisible && (allowTowerDive || !IsUnsafeTowerZone(snap, self, enemyTower, selfPos));
            if (canPoke && enemyDist <= match.Tuning.laserBasicRange &&
                (enemyTower == null || !enemyTower.alive ||
                 Vector2.Distance(enemyPos, new Vector2(enemyTower.x, enemyTower.y)) > enemyTower.attackRange + 0.5f ||
                 allowTowerDive))
            {
                SendAttackOrderIfNeeded(match, self, new LaneOrder
                {
                    team = team,
                    kind = LaneOrderKind.AttackHero,
                    targetId = enemy.id,
                    point = enemyPos,
                });
                return;
            }

            Vector2 advance = (enemyVisible && enemyDist < 9.5f && !IsUnsafeTowerZone(snap, self, enemyTower, enemyPos))
                ? enemyPos
                : MidLaneBias(team);
            advance = ClampSafeOutsideEnemyTower(snap, self, enemyTower, ownFountain, advance, allowTowerDive);
            SendAttackMoveIfNeeded(match, self, advance);
        }

        private int CountStackedEnemyMinions(LaneSnapshot snap, Vector2 center, float radius)
        {
            float rSq = radius * radius;
            int count = 0;
            for (int i = 0; i < snap.minions.Count; i++)
            {
                MinionSnap m = snap.minions[i];
                if (m.team != team && m.hp > 0f && m.laserStacks > 0 && (new Vector2(m.x, m.y) - center).sqrMagnitude <= rSq)
                {
                    count++;
                }
            }
            return count;
        }

        private void TrySkillUp(LaneMatch match, HeroSnap self)
        {
            if (self.level >= match.Tuning.ultMinLevel)
            {
                int maxR = self.level >= match.Tuning.maxLevel ? 2 : 1;
                if (self.rankR < maxR)
                {
                    match.EnqueueOrApplyOrder(new LaneOrder { team = team, kind = LaneOrderKind.LevelAbility, param = (int)LaneAbilitySlot.R });
                    return;
                }
            }

            LaneAbilitySlot[] prio;
            if (self.heroId == LaneHeroId.Ogre)
            {
                prio = new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E };
            }
            else if (self.heroId == LaneHeroId.Miravel)
            {
                prio = new[] { LaneAbilitySlot.Q, LaneAbilitySlot.E, LaneAbilitySlot.W };
            }
            else
            {
                prio = new[] { LaneAbilitySlot.Q, LaneAbilitySlot.W, LaneAbilitySlot.E };
            }

            // Put 1 rank into each basic skill first, then max Q -> W -> E
            for (int cap = 1; cap <= 3; cap++)
            {
                for (int i = 0; i < prio.Length; i++)
                {
                    int cur = prio[i] == LaneAbilitySlot.Q ? self.rankQ : (prio[i] == LaneAbilitySlot.W ? self.rankW : self.rankE);
                    if (cur < cap)
                    {
                        match.EnqueueOrApplyOrder(new LaneOrder { team = team, kind = LaneOrderKind.LevelAbility, param = (int)prio[i] });
                        return;
                    }
                }
            }
        }

        private void TryShopNextItem(LaneMatch match, HeroSnap self)
        {
            int[] order = self.heroId == LaneHeroId.Ogre ? OgreBuild
                : (self.heroId == LaneHeroId.Miravel ? MiravelBuild
                : (self.heroId == LaneHeroId.Swordsman ? SwordsmanBuild : LaserBuild));

            for (int i = 0; i < order.Length; i++)
            {
                int id = order[i];
                bool owned = false;
                for (int s = 0; s < self.items.Length; s++)
                {
                    if (self.items[s] == id)
                    {
                        owned = true;
                        break;
                    }
                }
                if (owned) continue;

                if (LaneItems.TryGet(id, out LaneItemDef def) && self.gold >= def.Cost)
                {
                    match.EnqueueOrApplyOrder(new LaneOrder { team = team, kind = LaneOrderKind.BuyItem, param = id });
                }
                break;
            }
        }

        private bool TryStepOnOrb(LaneSnapshot snap, HeroSnap self, Vector2 selfPos, TowerSnap enemyTower, out Vector2 orbPos)
        {
            float best = 4.6f;
            orbPos = Vector2.zero;
            bool found = false;

            for (int i = 0; i < snap.orbs.Count; i++)
            {
                OrbSnap o = snap.orbs[i];
                Vector2 p = new Vector2(o.x, o.y);
                if (IsUnsafeTowerZone(snap, self, enemyTower, p)) continue;
                float d = Vector2.Distance(selfPos, p);
                if (d < best)
                {
                    best = d;
                    orbPos = p;
                    found = true;
                }
            }
            return found;
        }

        private static HeroSnap FindHero(LaneSnapshot snap, LaneTeam t)
        {
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].team == t) return snap.heroes[i];
            }
            return null;
        }

        private static TowerSnap FindTower(LaneSnapshot snap, LaneTeam t)
        {
            for (int i = 0; i < snap.towers.Count; i++)
            {
                if (snap.towers[i].team == t) return snap.towers[i];
            }
            return null;
        }

        private bool HasHealthyFriendlyMinionUnderTower(LaneSnapshot snap, Vector2 towerPos, float range, float minHp = 85f)
        {
            for (int i = 0; i < snap.minions.Count; i++)
            {
                MinionSnap m = snap.minions[i];
                if (m.team != team || m.hp <= minHp) continue;
                if (Vector2.Distance(new Vector2(m.x, m.y), towerPos) <= range - 0.35f) return true;
            }
            return false;
        }

        private bool IsUnsafeTowerZone(LaneSnapshot snap, HeroSnap self, TowerSnap enemyTower, Vector2 pos)
        {
            if (enemyTower == null || !enemyTower.alive) return false;
            Vector2 tPos = new Vector2(enemyTower.x, enemyTower.y);
            if (Vector2.Distance(pos, tPos) > enemyTower.attackRange + 0.9f) return false;
            if (enemyTower.targetId == self.id) return true;
            return !HasHealthyFriendlyMinionUnderTower(snap, tPos, enemyTower.attackRange);
        }

        private Vector2 ClampSafeOutsideEnemyTower(LaneSnapshot snap, HeroSnap self, TowerSnap enemyTower,
            Vector2 ownFountain, Vector2 desired, bool allowTowerDive)
        {
            if (allowTowerDive || !IsUnsafeTowerZone(snap, self, enemyTower, desired))
            {
                return desired;
            }
            Vector2 tPos = new Vector2(enemyTower.x, enemyTower.y);
            Vector2 dirHome = (ownFountain - tPos).sqrMagnitude > 0.01f
                ? (ownFountain - tPos).normalized
                : (team == LaneTeam.Blue ? Vector2.left : Vector2.right);
            return tPos + dirHome * (enemyTower.attackRange + 1.4f);
        }

        private MinionSnap FindClosestEnemyMinion(LaneSnapshot snap, Vector2 from)
        {
            MinionSnap best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < snap.minions.Count; i++)
            {
                MinionSnap m = snap.minions[i];
                if (m.team == team || m.hp <= 0f) continue;
                float d = Vector2.Distance(from, new Vector2(m.x, m.y));
                if (d < bestD)
                {
                    bestD = d;
                    best = m;
                }
            }
            return best;
        }

        private MinionSnap FindLowestEnemyMinion(LaneSnapshot snap, Vector2 from, float maxRange)
        {
            MinionSnap best = null;
            float bestHp = float.MaxValue;
            for (int i = 0; i < snap.minions.Count; i++)
            {
                MinionSnap m = snap.minions[i];
                if (m.team == team || m.hp <= 0f) continue;
                if (Vector2.Distance(from, new Vector2(m.x, m.y)) > maxRange) continue;
                if (m.hp < bestHp)
                {
                    bestHp = m.hp;
                    best = m;
                }
            }
            return best;
        }

        private int CountNearbyEnemyMinions(LaneSnapshot snap, Vector2 center, float radius)
        {
            int count = 0;
            for (int i = 0; i < snap.minions.Count; i++)
            {
                MinionSnap m = snap.minions[i];
                if (m.team == team || m.hp <= 0f) continue;
                if (Vector2.Distance(center, new Vector2(m.x, m.y)) <= radius) count++;
            }
            return count;
        }

        private static Vector2 MidLaneBias(LaneTeam team) =>
            team == LaneTeam.Blue ? new Vector2(-1.5f, 0f) : new Vector2(1.5f, 0f);
    }
}
