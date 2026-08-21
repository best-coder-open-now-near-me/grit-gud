using System;

namespace GritGud.Domain.Gameplay
{
    public enum ActorStance
    {
        Standing,
        Crouched,
    }

    public readonly struct GameplayPosition
    {
        public GameplayPosition(float x, float y, float z)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
            {
                throw new ArgumentException(
                    "Gameplay positions must contain only finite values.");
            }

            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        public float DistanceTo(GameplayPosition other)
        {
            double deltaX = (double)other.X - X;
            double deltaY = (double)other.Y - Y;
            double deltaZ = (double)other.Z - Z;
            double distance = Math.Sqrt(
                (deltaX * deltaX)
                + (deltaY * deltaY)
                + (deltaZ * deltaZ));

            if (distance > float.MaxValue)
            {
                throw new InvalidOperationException(
                    "The distance between gameplay positions exceeds the supported range.");
            }

            return (float)distance;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct GameplayActorPose
    {
        public GameplayActorPose(GameplayPosition position, float facingDegrees)
            : this(position, facingDegrees, ActorStance.Standing)
        {
        }

        public GameplayActorPose(
            GameplayPosition position,
            float facingDegrees,
            ActorStance stance)
        {
            if (float.IsNaN(facingDegrees) || float.IsInfinity(facingDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(facingDegrees));
            }

            if (!Enum.IsDefined(typeof(ActorStance), stance))
            {
                throw new ArgumentOutOfRangeException(nameof(stance));
            }

            Position = position;
            FacingDegrees = NormalizeDegrees(facingDegrees);
            Stance = stance;
        }

        public GameplayPosition Position { get; }

        public float FacingDegrees { get; }

        public ActorStance Stance { get; }

        private static float NormalizeDegrees(float value)
        {
            float normalized = value % 360f;
            if (normalized < 0f)
            {
                normalized += 360f;
            }

            return normalized == 0f ? 0f : normalized;
        }
    }

    public sealed class ExplorationPoseRecord
    {
        public ExplorationPoseRecord(
            string actorId,
            GameplayActorPose previousPose,
            GameplayActorPose resultingPose)
        {
            ActorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException(
                    "Exploration pose records require an actor identifier.",
                    nameof(actorId))
                : actorId;
            PreviousPose = previousPose;
            ResultingPose = resultingPose;
        }

        public string ActorId { get; }
        public GameplayActorPose PreviousPose { get; }
        public GameplayActorPose ResultingPose { get; }
    }
}
