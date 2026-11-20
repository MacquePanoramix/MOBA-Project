using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.MovementLab
{
    [Serializable]
    public sealed class MovementTuning
    {
        public float MaxSpeed = 5f;
        public float Acceleration = 8f;
        public float Deceleration = 12f;
        public float TurnRate = 300f;
        public float TurnSlowdownAngle = 85f;
    }

    public sealed class MovementMotor
    {
        private List<Vector2> route = new List<Vector2>();
        private List<Vector2> candidateRoute = new List<Vector2>();
        private float remainingRouteDistance;

        public Vector2 Position { get; private set; }
        public float HeadingDegrees { get; private set; }
        public float Speed { get; private set; }
        public Vector2 Destination { get; private set; }
        public int NextWaypointIndex { get; private set; }
        public bool IsMoving => NextWaypointIndex < route.Count;
        public IReadOnlyList<Vector2> Route => route;

        public MovementMotor(Vector2 start, float headingDegrees)
        {
            Position = start;
            Destination = start;
            HeadingDegrees = Mathf.Repeat(headingDegrees, 360f);
        }

        public bool TrySetDestination(MovementCourse course, Vector2 target)
        {
            if (!course.TryFindRoute(Position, target, candidateRoute))
                return false;

            List<Vector2> previousRoute = route;
            route = candidateRoute;
            candidateRoute = previousRoute;
            NextWaypointIndex = 0;
            Destination = target;
            remainingRouteDistance = 0f;
            for (int index = 1; index < route.Count; index++)
                remainingRouteDistance += Vector2.Distance(route[index - 1], route[index]);

            return true;
        }

        internal void Stop()
        {
            route.Clear();
            NextWaypointIndex = 0;
            remainingRouteDistance = 0f;
            Destination = Position;
            Speed = 0f;
        }

        internal bool TurnToward(Vector2 target, MovementTuning tuning, float deltaTime)
        {
            Vector2 offset = target - Position;
            return offset.sqrMagnitude == 0f || TurnTowardOffset(offset, tuning, deltaTime) <= 0.001f;
        }

        public void Tick(float deltaTime, MovementTuning tuning)
        {
            if (!IsMoving || deltaTime <= 0f)
                return;

            Vector2 waypoint = route[NextWaypointIndex];
            Vector2 offset = waypoint - Position;
            float distance = offset.magnitude;
            if (distance == 0f)
            {
                AdvanceWaypoint();
                return;
            }

            float turnAngle = TurnTowardOffset(offset, tuning, deltaTime);
            float slowdownAngle = Mathf.Clamp(tuning.TurnSlowdownAngle, 1f, 90f);
            float alignment = 1f - Mathf.Clamp01(turnAngle / slowdownAngle);
            float turnSpeedLimit = Mathf.Max(0.1f, tuning.MaxSpeed) * alignment;
            float deceleration = Mathf.Max(0.1f, tuning.Deceleration);
            float arrivalSpeedLimit = Mathf.Sqrt(2f * deceleration * (distance + remainingRouteDistance));
            float targetSpeed = Mathf.Min(turnSpeedLimit, arrivalSpeedLimit);
            float speedChange = targetSpeed < Speed ? deceleration : Mathf.Max(0.1f, tuning.Acceleration);

            // Keep each step on its clear route leg. A hard reversal stops travel while the unit turns.
            float approachedSpeed = Mathf.MoveTowards(Speed, targetSpeed, speedChange * deltaTime);
            float step = Mathf.Min(approachedSpeed, turnSpeedLimit) * deltaTime;
            if (step >= distance)
            {
                Position = waypoint;
                Speed = distance / deltaTime;
                AdvanceWaypoint();
            }
            else
            {
                Position += offset * (step / distance);
                Speed = step / deltaTime;
            }
        }

        private float TurnTowardOffset(Vector2 offset, MovementTuning tuning, float deltaTime)
        {
            float desiredHeading = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
            float turnSpeed = Mathf.Max(1f, tuning.TurnRate);
            float nextHeading = Mathf.MoveTowardsAngle(HeadingDegrees, desiredHeading, turnSpeed * deltaTime);
            HeadingDegrees = Mathf.Repeat(nextHeading, 360f);
            return Mathf.Abs(Mathf.DeltaAngle(HeadingDegrees, desiredHeading));
        }

        private void AdvanceWaypoint()
        {
            Vector2 previous = route[NextWaypointIndex];
            NextWaypointIndex++;
            if (IsMoving)
                remainingRouteDistance = Mathf.Max(0f, remainingRouteDistance - Vector2.Distance(previous, route[NextWaypointIndex]));
            else
                Speed = 0f;
        }
    }
}
