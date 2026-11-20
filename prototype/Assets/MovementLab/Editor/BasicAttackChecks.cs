using System;
using UnityEngine;

namespace Moba.MovementLab
{
    internal static class BasicAttackChecks
    {
        private const float Step = 0.02f;
        private const float Tolerance = 0.015f;

        private static readonly Vector2 Dummy = new Vector2(4f, 0f);
        private static readonly Vector2 Tree = Vector2.zero;
        private static readonly Vector2 InRange = new Vector2(2.25f, 0f);
        private static readonly Vector2 MoveAway = new Vector2(2.25f, 3f);

        public static void Run()
        {
            var course = new MovementCourse(10f, 7f, new[] { Tree, Dummy }, 0.9f, 0.46f);
            var attack = new BasicAttackTuning();
            var quick = new MovementTuning { TurnRate = 300f };
            var deliberate = new MovementTuning { TurnRate = 150f };

            CheckApproach(course, attack, quick);
            int quickTurnTicks = CheckTurnAndTiming(course, attack, quick);
            int deliberateTurnTicks = CheckTurnAndTiming(course, attack, deliberate);
            Check(quickTurnTicks < deliberateTurnTicks, "The quick attack turn should enter the shared wind-up sooner");
            CheckMoveOrders(course, attack, quick);

            Debug.Log("Basic attack checks passed: approach and clearance, both turn profiles, wind-up and recovery, single hit, and move-order interruption.");
        }

        private static void CheckApproach(MovementCourse course, BasicAttackTuning attack, MovementTuning movement)
        {
            var motor = new MovementMotor(new Vector2(-5f, 0f), 90f);
            var sequence = new BasicAttackSequence(Dummy, attack);
            Check(sequence.TryAttack(motor, course), "Out-of-range attack was refused");
            Check(sequence.Phase == BasicAttackPhase.Approaching && motor.Route.Count > 1,
                "The approach should route around the intervening tree");

            for (int tick = 0; tick < 750 && sequence.Phase == BasicAttackPhase.Approaching; tick++)
            {
                sequence.Tick(motor, movement, Step);
                Check(course.IsWalkable(motor.Position), "The attack approach entered a blocked position");
            }

            Check(sequence.Phase == BasicAttackPhase.Turning || sequence.Phase == BasicAttackPhase.WindUp,
                "The approach did not progress into the attack");
            Check(!motor.IsMoving && motor.Speed == 0f && Vector2.Distance(motor.Position, Dummy) <= attack.Range + Tolerance,
                "The attacker must settle within range before winding up");
            Check(sequence.HitCount == 0, "The approach recorded an early hit");
        }

        private static int CheckTurnAndTiming(MovementCourse course, BasicAttackTuning attack, MovementTuning movement)
        {
            var motor = new MovementMotor(InRange, 270f);
            var sequence = new BasicAttackSequence(Dummy, attack);
            Check(sequence.TryAttack(motor, course) && sequence.Phase == BasicAttackPhase.Turning,
                "The in-range attack should face the dummy first");

            sequence.Tick(motor, movement, Step);
            float firstTurn = Mathf.Abs(Mathf.DeltaAngle(270f, motor.HeadingDegrees));
            Check(Mathf.Abs(firstTurn - movement.TurnRate * Step) <= Tolerance,
                "The attack did not use the current movement profile's turn rate");

            int ticks = 1;
            while (sequence.Phase == BasicAttackPhase.Turning && ticks < 120)
            {
                sequence.Tick(motor, movement, Step);
                ticks++;
            }

            Check(sequence.Phase == BasicAttackPhase.WindUp && sequence.HitCount == 0 && motor.Position == InRange,
                "The attacker must face the dummy in place before the hit");
            CheckWindUp(sequence, motor, movement, attack);

            float remainingRecovery = (1f - sequence.PhaseProgress) * attack.RecoverySeconds;
            sequence.Tick(motor, movement, remainingRecovery - 0.01f);
            Check(sequence.Phase == BasicAttackPhase.Recovery && sequence.HitCount == 1, "Recovery ended too early");
            sequence.Tick(motor, movement, 0.02f);
            Check(sequence.Phase == BasicAttackPhase.Ready && sequence.HitCount == 1, "The attack did not recover after one hit");
            sequence.Tick(motor, movement, 1f);
            Check(sequence.Phase == BasicAttackPhase.Ready && sequence.HitCount == 1, "The attack repeated without another order");
            return ticks;
        }

        private static void CheckMoveOrders(MovementCourse course, BasicAttackTuning attack, MovementTuning movement)
        {
            var motor = new MovementMotor(InRange, 90f);
            var sequence = new BasicAttackSequence(Dummy, attack);
            Check(sequence.TryAttack(motor, course), "Attack order was refused");
            sequence.Tick(motor, movement, Step);
            sequence.Tick(motor, movement, attack.WindUpSeconds * 0.5f);
            float progress = sequence.PhaseProgress;
            Check(sequence.Phase == BasicAttackPhase.WindUp && progress > 0f && progress < 1f,
                "Move-order checks require an attack in progress before the hit");
            Check(!sequence.TryAttack(motor, course) && sequence.PhaseProgress == progress,
                "A repeated attack restarted the active wind-up");
            Check(!sequence.TryMove(motor, course, Tree) && sequence.Phase == BasicAttackPhase.WindUp
                && sequence.PhaseProgress == progress, "A blocked move interrupted the wind-up");
            Check(sequence.TryMove(motor, course, MoveAway) && sequence.Phase == BasicAttackPhase.Ready
                && sequence.Outcome == BasicAttackOutcome.Interrupted && motor.Destination == MoveAway,
                "A valid move did not interrupt the wind-up and start the route");
            sequence.Tick(motor, movement, attack.WindUpSeconds);
            Check(sequence.HitCount == 0, "An interrupted wind-up recorded a hit");

            motor = new MovementMotor(InRange, 90f);
            sequence = new BasicAttackSequence(Dummy, attack);
            Check(sequence.TryAttack(motor, course), "Second attack order was refused");
            sequence.Tick(motor, movement, Step);
            CheckWindUp(sequence, motor, movement, attack);
            Check(sequence.TryMove(motor, course, MoveAway) && sequence.Phase == BasicAttackPhase.Ready
                && sequence.Outcome == BasicAttackOutcome.Hit && sequence.HitCount == 1 && motor.Destination == MoveAway,
                "A valid move after impact must retain the hit and cancel only recovery");
        }

        private static void CheckWindUp(BasicAttackSequence sequence, MovementMotor motor, MovementTuning movement,
            BasicAttackTuning attack)
        {
            float remainingWindUp = (1f - sequence.PhaseProgress) * attack.WindUpSeconds;
            sequence.Tick(motor, movement, remainingWindUp - 0.01f);
            Check(sequence.Phase == BasicAttackPhase.WindUp && sequence.HitCount == 0, "A hit landed before the wind-up elapsed");
            sequence.Tick(motor, movement, 0.02f);
            Check(sequence.Phase == BasicAttackPhase.Recovery && sequence.Outcome == BasicAttackOutcome.Hit && sequence.HitCount == 1,
                "The completed wind-up should record one hit and begin recovery");
        }

        private static void Check(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }
    }
}
