using System;
using UnityEngine;

namespace Moba.MovementLab
{
    public enum BasicAttackPhase
    {
        Ready,
        Approaching,
        Turning,
        WindUp,
        Recovery
    }

    public enum BasicAttackOutcome
    {
        None,
        Interrupted,
        Hit
    }

    [Serializable]
    public sealed class BasicAttackTuning
    {
        public float Range = 2.25f;
        public float WindUpSeconds = 0.50f;
        public float RecoverySeconds = 0.35f;
    }

    public sealed class BasicAttackSequence
    {
        private const int ApproachDirections = 12;
        private const float TimingTolerance = 0.000001f;

        private readonly Vector2 target;
        private readonly BasicAttackTuning tuning;
        private float phaseDuration;
        private float phaseElapsed;

        public BasicAttackPhase Phase { get; private set; }
        public BasicAttackOutcome Outcome { get; private set; }
        public int HitCount { get; private set; }
        public float PhaseProgress => phaseDuration > 0f ? Mathf.Clamp01(phaseElapsed / phaseDuration) : 0f;

        public BasicAttackSequence(Vector2 target, BasicAttackTuning tuning)
        {
            this.target = target;
            this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        }

        public bool TryAttack(MovementMotor motor, MovementCourse course)
        {
            if (Phase != BasicAttackPhase.Ready || tuning.Range <= 0f)
                return false;

            Vector2 offset = motor.Position - target;
            if (offset.sqrMagnitude <= tuning.Range * tuning.Range)
            {
                motor.Stop();
                Phase = BasicAttackPhase.Turning;
                return true;
            }

            float nearSideAngle = Mathf.Atan2(offset.y, offset.x);
            if (!TryApproach(motor, course, nearSideAngle, tuning.Range * 0.8f) &&
                !TryApproach(motor, course, nearSideAngle, tuning.Range * 0.98f))
                return false;

            Phase = BasicAttackPhase.Approaching;
            return true;
        }

        public bool TryMove(MovementMotor motor, MovementCourse course, Vector2 destination)
        {
            if (!motor.TrySetDestination(course, destination))
                return false;

            if (Phase == BasicAttackPhase.Approaching || Phase == BasicAttackPhase.Turning ||
                Phase == BasicAttackPhase.WindUp)
                Outcome = BasicAttackOutcome.Interrupted;

            SetPhase(BasicAttackPhase.Ready);
            return true;
        }

        public void Tick(MovementMotor motor, MovementTuning movement, float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return;

            switch (Phase)
            {
                case BasicAttackPhase.Ready:
                    motor.Tick(deltaTime, movement);
                    break;
                case BasicAttackPhase.Approaching:
                    motor.Tick(deltaTime, movement);
                    if (!motor.IsMoving)
                        Phase = BasicAttackPhase.Turning;
                    break;
                case BasicAttackPhase.Turning:
                    if (motor.TurnToward(target, movement, deltaTime))
                        SetPhase(BasicAttackPhase.WindUp, tuning.WindUpSeconds);
                    break;
                case BasicAttackPhase.WindUp:
                    phaseElapsed += deltaTime;
                    if (phaseElapsed + TimingTolerance >= phaseDuration)
                    {
                        float spillover = Mathf.Max(0f, phaseElapsed - phaseDuration);
                        Outcome = BasicAttackOutcome.Hit;
                        HitCount++;
                        SetPhase(BasicAttackPhase.Recovery, tuning.RecoverySeconds);
                        AdvanceRecovery(spillover);
                    }
                    break;
                case BasicAttackPhase.Recovery:
                    AdvanceRecovery(deltaTime);
                    break;
            }
        }

        private bool TryApproach(MovementMotor motor, MovementCourse course, float nearSideAngle, float distance)
        {
            // Prefer the near side. If it is obstructed, try progressively wider angles around the dummy.
            for (int sample = 0; sample < ApproachDirections; sample++)
            {
                int signedStep = (sample + 1) / 2 * (sample % 2 == 0 ? -1 : 1);
                float angle = nearSideAngle + signedStep * (2f * Mathf.PI / ApproachDirections);
                Vector2 position = target + distance * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (motor.TrySetDestination(course, position))
                    return true;
            }
            return false;
        }

        private void AdvanceRecovery(float deltaTime)
        {
            phaseElapsed += deltaTime;
            if (phaseElapsed + TimingTolerance >= phaseDuration)
                SetPhase(BasicAttackPhase.Ready);
        }

        private void SetPhase(BasicAttackPhase phase, float duration = 0f)
        {
            Phase = phase;
            phaseDuration = Mathf.Max(0f, duration);
            phaseElapsed = 0f;
        }
    }
}
