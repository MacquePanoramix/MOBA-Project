using System;
using UnityEditor;
using UnityEngine;

namespace Moba.MovementLab
{
    public static class MovementLabChecks
    {
        private const float Step = 0.02f;
        private const float Tolerance = 0.015f;

        [MenuItem("Movement Lab/Run Movement Checks")]
        public static void Run()
        {
            var spawn = new Vector2(-6f, 0f);
            var obstacle = Vector2.zero;
            var course = new MovementCourse(10f, 7f, new[] { obstacle }, 1.2f, 0.45f);
            var tuning = new MovementTuning();

            var clear = new MovementMotor(spawn, 90f);
            var clearTarget = new Vector2(-6f, 4f);
            Check(clear.TrySetDestination(course, clearTarget), "Open command was refused");
            Check(clear.Route.Count == 1, "An open route should be direct");
            ReachAndSettle(clear, course, tuning, clearTarget);

            var detour = new MovementMotor(spawn, 90f);
            var detourTarget = new Vector2(6f, 0f);
            Check(detour.TrySetDestination(course, detourTarget), "Detour command was refused");
            Check(detour.Route.Count > 1, "The route cut through the obstacle");
            ReachAndSettle(detour, course, tuning, detourTarget);

            var retarget = new MovementMotor(spawn, 90f);
            var firstTarget = new Vector2(-2f, 0f);
            Check(retarget.TrySetDestination(course, firstTarget), "Initial command was refused");
            for (var tick = 0; tick < 20; tick++)
                retarget.Tick(Step, tuning);

            Check(retarget.Speed > 0f, "Retarget test did not start in motion");
            Check(!retarget.TrySetDestination(course, obstacle), "A blocked tree target was accepted");
            Check(retarget.IsMoving && retarget.Destination == firstTarget, "An invalid click discarded a valid route");
            Check(!retarget.TrySetDestination(course, new Vector2(float.NaN, 0f)), "A nonfinite command was accepted");
            Check(!retarget.TrySetDestination(course, new Vector2(11f, 0f)), "An out-of-bounds command was accepted");

            var returnTarget = new Vector2(-8f, 0f);
            Check(retarget.TrySetDestination(course, returnTarget), "Reverse command was refused");
            var beforeTurn = retarget.HeadingDegrees;
            retarget.Tick(Step, tuning);
            var firstTurn = Mathf.Abs(Mathf.DeltaAngle(beforeTurn, retarget.HeadingDegrees));
            Check(firstTurn > 0f && firstTurn <= tuning.TurnRate * Step + Tolerance,
                "Reversal did not respect the configurable turn rate");
            ReachAndSettle(retarget, course, tuning, returnTarget);

            Debug.Log("Movement Lab checks passed: direct route, tree clearance, arrival, retarget/reversal, and invalid commands.");
        }

        private static void ReachAndSettle(MovementMotor motor, MovementCourse course, MovementTuning tuning, Vector2 target)
        {
            for (var tick = 0; tick < 750 && motor.IsMoving; tick++)
            {
                motor.Tick(Step, tuning);
                Check(course.IsWalkable(motor.Position), $"Capsule entered a blocked position {motor.Position}");
            }

            Check(!motor.IsMoving, $"The move to {target} did not complete");
            Check(Vector2.Distance(motor.Position, target) <= Tolerance, $"Arrival missed {target}");
            var stoppedAt = motor.Position;
            for (var tick = 0; tick < 15; tick++)
                motor.Tick(Step, tuning);
            Check(motor.Position == stoppedAt && motor.Speed == 0f, "The capsule did not remain stopped");
        }

        private static void Check(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }
    }
}
