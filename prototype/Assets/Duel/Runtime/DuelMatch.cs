using System;
using Moba.MovementLab;
using UnityEngine;

namespace Moba.Duel
{
    public enum DuelHero { Ogre, Luis }
    public enum DuelOrder { Move, Basic, Slam, Sweep }

    public static class DuelArena
    {
        public const float HalfWidth = 12f;
        public const float HalfDepth = 5.6f;
        public const float UnitRadius = 0.48f;
        public const float TreeRadius = 0.72f;
        public static readonly Vector2 OgreSpawn = new Vector2(-7.5f, 0f);
        public static readonly Vector2 LuisSpawn = new Vector2(7.5f, 0f);
        private static readonly Vector2[] Trees =
        {
            new Vector2(-6f, -3.5f), new Vector2(6f, 3.5f),
            new Vector2(-3.2f, 2.85f), new Vector2(3.2f, -2.85f),
            new Vector2(-9.2f, 3.4f), new Vector2(9.2f, -3.4f)
        };
        public static Vector2[] TreeCenters => (Vector2[])Trees.Clone();
    }

    [Serializable]
    public sealed class DuelTuning
    {
        // 2026-11-22 baseline before 2026-11-23 playtest pass: Ogre (4.4 speed, 165 turn, 0.82s Q, 0.32s W), Luis (5.0 speed, 300 turn).
        [Min(1f)] public float ogreMaxHp = 140f, luisMaxHp = 105f;
        public MovementTuning ogreMovement = new MovementTuning { MaxSpeed = 3.2f, TurnRate = 250f };
        public MovementTuning luisMovement = new MovementTuning { MaxSpeed = 3.65f, TurnRate = 540f };
        [Min(0.01f)] public float slamRange = 4.3f, slamHalfWidth = 0.85f;
        [Min(0.01f)] public float slamWindup = 0.85f, slamRecovery = 0.34f, slamCooldown = 3.4f;
        [Min(0f)] public float slamDamage = 30f, slamStun = 0.60f;
        [Min(0.01f)] public float sweepRange = 2.65f;
        [Range(1f, 179f)] public float sweepHalfAngle = 65f;
        [Min(0.01f)] public float sweepWindup = 0.50f, sweepRecovery = 0.30f, sweepCooldown = 3f;
        [Min(0f)] public float sweepDamage = 15f, sweepPush = 2.1f;
        [Min(0.01f)] public float basicRange = 2.25f, basicWindup = 0.50f, basicRecovery = 0.35f;
        [Min(0f)] public float basicDamage = 13f;
        [Min(0.01f)] public float orbPickupRadius = 0.68f, orbPulseRadius = 3f;
        [Min(0f)] public float orbHeal = 14f, orbDamage = 17f;
        [Min(0.01f)] public float orbFirstDelay = 1.5f, orbInterval = 5f, orbLifetime = 6f;
    }

    [Serializable]
    public sealed class DuelUnitFrame
    {
        public Vector2 position, aim;
        public float heading, hp, maxHp, progress, qCooldown, wCooldown, stun;
        public int action;
        public bool recovering, moving, chasing, immune;
    }

    [Serializable]
    public sealed class DuelFrame
    {
        public DuelUnitFrame ogre, luis;
        public bool orbActive, finished, hostReady, guestReady;
        public Vector2 orb;
        public int winner, orbPickups, orbDenials;
        public float orbTimer;
        public string notice;
    }

    // Only this simulation decides contacts, damage, crowd control, orb ownership and the winner.
    public sealed class DuelMatch
    {
        private const float Epsilon = 0.00001f;
        private const float ChaseRepathSeconds = 0.18f;
        private static readonly Vector2[] OrbSites =
        {
            Vector2.zero, new Vector2(0f, 1.7f), new Vector2(0f, -1.7f)
        };

        private sealed class Fighter
        {
            public MovementMotor motor;
            public readonly MovementTuning movement;
            public readonly float maxHp;
            public float hp, qCooldown, wCooldown, stun;
            public DuelOrder action = DuelOrder.Move;
            public Vector2 aim;
            public bool recovering, chasing;
            public float elapsed, windup, recovery, chaseRepath;

            public Fighter(Vector2 spawn, float heading, float maxHp, MovementTuning movement)
            {
                motor = new MovementMotor(spawn, heading);
                this.maxHp = Mathf.Max(1f, maxHp);
                hp = this.maxHp;
                this.movement = movement;
                aim = heading == 90f ? Vector2.right : Vector2.left;
            }

            public bool Immune => (action == DuelOrder.Slam || action == DuelOrder.Sweep) && !recovering;

            public void ClearAction()
            {
                action = DuelOrder.Move;
                chasing = recovering = false;
                elapsed = 0f;
            }

            public DuelUnitFrame Snapshot() => new DuelUnitFrame
            {
                position = motor.Position, heading = motor.HeadingDegrees, aim = aim,
                hp = hp, maxHp = maxHp, action = (int)action, recovering = recovering,
                progress = action == DuelOrder.Move ? 0f : recovering ? 1f : Mathf.Clamp01(elapsed / windup),
                qCooldown = qCooldown, wCooldown = wCooldown, stun = stun,
                moving = motor.IsMoving, chasing = chasing, immune = Immune
            };
        }

        private readonly DuelTuning tuning;
        private readonly MovementCourse course;
        private readonly Fighter ogre, luis;
        private Vector2 orb;
        private bool orbActive;
        private int nextOrbSite, orbPickups, orbDenials;
        private float orbTimer;
        private int winner = -1;
        private string notice = "Contest the orb in the clearing.";

        public DuelMatch(DuelTuning tuning)
        {
            this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            course = new MovementCourse(DuelArena.HalfWidth, DuelArena.HalfDepth,
                DuelArena.TreeCenters, DuelArena.TreeRadius, DuelArena.UnitRadius);
            ogre = new Fighter(DuelArena.OgreSpawn, 90f, tuning.ogreMaxHp, tuning.ogreMovement);
            luis = new Fighter(DuelArena.LuisSpawn, 270f, tuning.luisMaxHp, tuning.luisMovement);
            orbTimer = tuning.orbFirstDelay;
        }

        public DuelFrame Snapshot() => new DuelFrame
        {
            ogre = ogre.Snapshot(), luis = luis.Snapshot(), orb = orb, orbActive = orbActive,
            orbTimer = orbTimer, orbPickups = orbPickups, orbDenials = orbDenials,
            finished = winner >= 0, winner = winner, notice = notice
        };

        public void Command(DuelHero hero, DuelOrder order, Vector2 target)
        {
            if (winner >= 0 || (hero != DuelHero.Ogre && hero != DuelHero.Luis) ||
                !Finite(target.x) || !Finite(target.y)) return;
            Fighter actor = hero == DuelHero.Ogre ? ogre : luis;
            if (actor.stun > 0f) return;
            if (order == DuelOrder.Move)
            {
                if (actor.action == DuelOrder.Slam || actor.action == DuelOrder.Sweep) return;
                if (!actor.motor.TrySetDestination(course, target)) return;
                actor.chasing = false;
                // Ground orders cancel the basic before impact. Its recovery still runs while walking.
                if (!actor.recovering) actor.ClearAction();
                return;
            }
            if (actor.action != DuelOrder.Move) return;
            if (order == DuelOrder.Basic && hero == DuelHero.Luis)
            {
                if (!luis.chasing)
                {
                    luis.chasing = true;
                    luis.chaseRepath = 0f;
                }
                return;
            }
            if (hero != DuelHero.Ogre) return;
            if (order != DuelOrder.Slam && order != DuelOrder.Sweep) return;
            bool slam = order == DuelOrder.Slam;
            if ((slam ? ogre.qCooldown : ogre.wCooldown) > 0f) return;
            Vector2 direction = target - ogre.motor.Position;
            if (direction.sqrMagnitude < Epsilon || !Finite(direction.sqrMagnitude)) return;
            StartAttack(ogre, order, direction.normalized, slam ? tuning.slamWindup : tuning.sweepWindup,
                slam ? tuning.slamRecovery : tuning.sweepRecovery);
            if (slam) ogre.qCooldown = tuning.slamCooldown;
            else ogre.wCooldown = tuning.sweepCooldown;
        }

        public void Tick(float dt)
        {
            if (!(dt > 0f) || !Finite(dt) || winner >= 0) return;
            // Subdivide for consistent contact and path checks if the host briefly hitches.
            float remaining = Mathf.Min(dt, 10f);
            while (remaining > Epsilon && winner < 0)
            {
                float step = Mathf.Min(remaining, 1f / 60f);
                Step(step);
                remaining -= step;
            }
        }

        private void Step(float dt)
        {
            ogre.qCooldown = Mathf.Max(0f, ogre.qCooldown - dt);
            ogre.wCooldown = Mathf.Max(0f, ogre.wCooldown - dt);
            ogre.stun = Mathf.Max(0f, ogre.stun - dt);
            luis.stun = Mathf.Max(0f, luis.stun - dt);
            if (ogre.stun <= 0f && ogre.action == DuelOrder.Move) ogre.motor.Tick(dt, ogre.movement);
            if (luis.stun <= 0f)
            {
                if (luis.chasing) ChaseOgre(dt);
                if (luis.action == DuelOrder.Move || luis.recovering) luis.motor.Tick(dt, luis.movement);
            }
            // A slam or sweep reaching its impact on the same step as a basic can interrupt that basic.
            AdvanceAttack(ogre, dt);
            if (winner < 0) AdvanceAttack(luis, dt);
            if (winner < 0) AdvanceOrb(dt);
        }

        private void ChaseOgre(float dt)
        {
            Vector2 toOgre = ogre.motor.Position - luis.motor.Position;
            if (toOgre.sqrMagnitude <= tuning.basicRange * tuning.basicRange)
            {
                luis.motor.Stop();
                if (luis.motor.TurnToward(ogre.motor.Position, luis.movement, dt))
                {
                    Vector2 direction = toOgre.sqrMagnitude < Epsilon ? luis.aim : toOgre.normalized;
                    StartAttack(luis, DuelOrder.Basic, direction, tuning.basicWindup, tuning.basicRecovery);
                }
                return;
            }

            luis.chaseRepath -= dt;
            if (luis.chaseRepath > 0f) return;
            luis.chaseRepath = ChaseRepathSeconds;
            Vector2 nearSide = ogre.motor.Position - toOgre.normalized * (tuning.basicRange * 0.78f);
            // The near-side point occasionally falls inside a tree. The opponent's center is walkable.
            if (!luis.motor.TrySetDestination(course, nearSide))
                luis.motor.TrySetDestination(course, ogre.motor.Position);
        }

        private static void StartAttack(Fighter actor, DuelOrder order, Vector2 aim, float windup, float recovery)
        {
            actor.motor.Stop();
            actor.action = order;
            actor.aim = aim;
            actor.windup = Mathf.Max(0.01f, windup);
            actor.recovery = Mathf.Max(0.01f, recovery);
            actor.chasing = actor.recovering = false;
            actor.elapsed = 0f;
        }

        private void AdvanceAttack(Fighter actor, float dt)
        {
            if (actor.action == DuelOrder.Move || actor.stun > 0f) return;
            actor.elapsed += dt;
            if (actor.recovering)
            {
                if (actor.elapsed + Epsilon >= actor.recovery) actor.ClearAction();
                return;
            }
            actor.motor.TurnToward(actor.motor.Position + actor.aim, actor.movement, dt);
            if (actor.elapsed + Epsilon < actor.windup) return;
            actor.elapsed = Mathf.Max(0f, actor.elapsed - actor.windup);
            actor.recovering = true;
            if (actor == ogre) ResolveClub();
            else if ((luis.motor.Position - ogre.motor.Position).sqrMagnitude <= tuning.basicRange * tuning.basicRange)
                Damage(ogre, tuning.basicDamage, DuelNames.OrbKeeper + " landed a basic attack.");
            else notice = DuelNames.OrbKeeper + "'s attack missed.";
        }

        private void ResolveClub()
        {
            Vector2 delta = luis.motor.Position - ogre.motor.Position;
            float forward = Vector2.Dot(delta, ogre.aim);
            bool slam = ogre.action == DuelOrder.Slam;
            float lateral = Mathf.Abs(delta.x * ogre.aim.y - delta.y * ogre.aim.x);
            bool hit = slam
                ? forward >= 0f && forward <= tuning.slamRange && lateral <= tuning.slamHalfWidth
                : delta.sqrMagnitude <= tuning.sweepRange * tuning.sweepRange &&
                  forward >= delta.magnitude * Mathf.Cos(tuning.sweepHalfAngle * Mathf.Deg2Rad);
            if (!hit)
            {
                notice = DuelNames.Ogre + (slam ? "'s slam missed." : "'s sweep missed.");
                return;
            }
            if (slam)
            {
                TryStun(DuelHero.Luis, tuning.slamStun);
                Damage(luis, tuning.slamDamage, DuelNames.Ogre + "'s slam stunned " + DuelNames.OrbKeeper + ".");
            }
            else
            {
                Push(luis, delta.sqrMagnitude < Epsilon ? ogre.aim : delta.normalized, tuning.sweepPush);
                Damage(luis, tuning.sweepDamage, DuelNames.Ogre + " swept " + DuelNames.OrbKeeper + " back.");
            }
        }

        public bool TryStun(DuelHero target, float duration)
        {
            if (winner >= 0 || !(duration > 0f) || !Finite(duration) ||
                (target != DuelHero.Ogre && target != DuelHero.Luis)) return false;
            Fighter actor = target == DuelHero.Ogre ? ogre : luis;
            if (actor.Immune) return false;
            actor.stun = Mathf.Max(actor.stun, duration);
            actor.motor.Stop();
            actor.ClearAction();
            return true;
        }

        private void Push(Fighter actor, Vector2 direction, float distance)
        {
            if (actor.Immune) return;
            actor.ClearAction();
            // Trace to first obstruction analytically, so a push can never jump through a trunk.
            Vector2 from = actor.motor.Position;
            float travel = Mathf.Max(0f, distance);
            float limitX = DuelArena.HalfWidth - DuelArena.UnitRadius;
            float limitY = DuelArena.HalfDepth - DuelArena.UnitRadius;
            if (Mathf.Abs(direction.x) > Epsilon)
                travel = Mathf.Min(travel, (Mathf.Sign(direction.x) * limitX - from.x) / direction.x);
            if (Mathf.Abs(direction.y) > Epsilon)
                travel = Mathf.Min(travel, (Mathf.Sign(direction.y) * limitY - from.y) / direction.y);
            float radius = DuelArena.TreeRadius + DuelArena.UnitRadius;
            foreach (Vector2 tree in course.Trees)
            {
                Vector2 offset = tree - from;
                float along = Vector2.Dot(offset, direction);
                if (along <= 0f) continue;
                float perpendicularSq = offset.sqrMagnitude - along * along;
                if (perpendicularSq > radius * radius) continue;
                float firstContact = along - Mathf.Sqrt(Mathf.Max(0f, radius * radius - perpendicularSq));
                travel = Mathf.Min(travel, firstContact - 0.001f);
            }
            Vector2 landing = from + direction * Mathf.Max(0f, travel);
            actor.motor = new MovementMotor(course.IsWalkable(landing) ? landing : from, actor.motor.HeadingDegrees);
        }

        private void AdvanceOrb(float dt)
        {
            orbTimer -= dt;
            if (orbTimer <= 0f)
            {
                if (orbActive)
                {
                    RemoveOrb();
                    notice = "The orb faded.";
                    return;
                }
                orb = OrbSites[nextOrbSite];
                nextOrbSite = (nextOrbSite + 1) % OrbSites.Length;
                orbTimer = Mathf.Max(0.01f, tuning.orbLifetime);
                orbActive = true;
                notice = "An orb appeared.";
            }
            if (!orbActive) return;
            float ogreDistance = (ogre.motor.Position - orb).sqrMagnitude;
            float luisDistance = (luis.motor.Position - orb).sqrMagnitude;
            if (Mathf.Min(ogreDistance, luisDistance) > tuning.orbPickupRadius * tuning.orbPickupRadius) return;
            // An exact tie remains contested until one unit is closer.
            if (Mathf.Abs(ogreDistance - luisDistance) <= Epsilon) return;
            RemoveOrb();
            if (ogreDistance < luisDistance)
            {
                orbDenials++;
                notice = DuelNames.Ogre + " denied the orb.";
                return;
            }
            orbPickups++;
            float beforeHp = luis.hp;
            luis.hp = Mathf.Min(luis.maxHp, luis.hp + Mathf.Max(0f, tuning.orbHeal));
            int healed = Mathf.RoundToInt(luis.hp - beforeHp);
            string healNote = healed > 0 ? $" (+{healed} HP)" : " (full HP)";
            if ((ogre.motor.Position - luis.motor.Position).sqrMagnitude <= tuning.orbPulseRadius * tuning.orbPulseRadius)
                Damage(ogre, tuning.orbDamage, DuelNames.OrbKeeper + "'s orb healed" + healNote + " and pulsed " + DuelNames.Ogre + ".");
            else
                notice = DuelNames.OrbKeeper + " collected the orb" + healNote + ".";
        }

        private void RemoveOrb()
        {
            orbActive = false;
            orbTimer = Mathf.Max(0.01f, tuning.orbInterval);
        }

        private void Damage(Fighter target, float amount, string message)
        {
            target.hp = Mathf.Max(0f, target.hp - Mathf.Max(0f, amount));
            notice = message;
            if (target.hp > 0f) return;
            winner = target == ogre ? (int)DuelHero.Luis : (int)DuelHero.Ogre;
            notice = DuelNames.For((DuelHero)winner) + " wins the duel.";
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
