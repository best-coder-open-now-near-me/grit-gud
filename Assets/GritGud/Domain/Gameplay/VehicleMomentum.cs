using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public sealed class VehicleMomentumProfile
    {
        public VehicleMomentumProfile(
            float maximumSpeed,
            float accelerationPerTurn,
            float brakingPerTurn,
            float lowSpeedTurnDegrees,
            float highSpeedTurnDegrees,
            float baseTurningRadius,
            float speedTurningRadiusFactor)
        {
            MaximumSpeed = RequirePositive(maximumSpeed, nameof(maximumSpeed));
            AccelerationPerTurn = RequirePositive(
                accelerationPerTurn,
                nameof(accelerationPerTurn));
            BrakingPerTurn = RequirePositive(brakingPerTurn, nameof(brakingPerTurn));
            LowSpeedTurnDegrees = RequireTurnDegrees(
                lowSpeedTurnDegrees,
                nameof(lowSpeedTurnDegrees));
            HighSpeedTurnDegrees = RequireTurnDegrees(
                highSpeedTurnDegrees,
                nameof(highSpeedTurnDegrees));
            if (highSpeedTurnDegrees > lowSpeedTurnDegrees)
            {
                throw new ArgumentException(
                    "Vehicle steering must narrow or remain equal as speed increases.",
                    nameof(highSpeedTurnDegrees));
            }

            BaseTurningRadius = RequirePositive(
                baseTurningRadius,
                nameof(baseTurningRadius));
            if (!IsFinite(speedTurningRadiusFactor)
                || speedTurningRadiusFactor < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(speedTurningRadiusFactor));
            }

            SpeedTurningRadiusFactor = speedTurningRadiusFactor;
        }

        public float MaximumSpeed { get; }

        public float AccelerationPerTurn { get; }

        public float BrakingPerTurn { get; }

        public float LowSpeedTurnDegrees { get; }

        public float HighSpeedTurnDegrees { get; }

        public float BaseTurningRadius { get; }

        public float SpeedTurningRadiusFactor { get; }

        public float GetMinimumTurningRadius(float speed) =>
            BaseTurningRadius
            + (Math.Max(0f, speed) * SpeedTurningRadiusFactor);

        private static float RequirePositive(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static float RequireTurnDegrees(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f || value > 180f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct VehicleMomentumState
    {
        public VehicleMomentumState(
            string vehicleId,
            GameplayPosition position,
            float forwardDegrees,
            float speed)
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                throw new ArgumentException(
                    "Vehicle momentum requires a stable identifier.",
                    nameof(vehicleId));
            }

            if (!IsFinite(forwardDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(forwardDegrees));
            }

            if (!IsFinite(speed) || speed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }

            VehicleId = vehicleId;
            Position = position;
            ForwardDegrees = NormalizeDegrees(forwardDegrees);
            Speed = speed;
        }

        public string VehicleId { get; }

        public GameplayPosition Position { get; }

        public float ForwardDegrees { get; }

        public float Speed { get; }

        private static float NormalizeDegrees(float value)
        {
            float normalized = value % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class VehicleMovementEnvelope
    {
        public VehicleMovementEnvelope(
            VehicleMomentumState state,
            float minimumDistance,
            float maximumDistance,
            float maximumTurnDegrees)
        {
            if (minimumDistance < 0f || maximumDistance < minimumDistance)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumDistance));
            }

            if (maximumTurnDegrees <= 0f || maximumTurnDegrees > 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTurnDegrees));
            }

            State = state;
            MinimumDistance = minimumDistance;
            MaximumDistance = maximumDistance;
            MaximumTurnDegrees = maximumTurnDegrees;
        }

        public VehicleMomentumState State { get; }

        public float MinimumDistance { get; }

        public float MaximumDistance { get; }

        public float MaximumTurnDegrees { get; }

        public IReadOnlyList<GameplayPosition> CreateBoundary(int arcSegments)
        {
            if (arcSegments < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(arcSegments));
            }

            var points = new List<GameplayPosition>(arcSegments + 3)
            {
                State.Position,
            };
            for (int index = 0; index <= arcSegments; index++)
            {
                float fraction = index / (float)arcSegments;
                float angle = State.ForwardDegrees
                    - MaximumTurnDegrees
                    + ((MaximumTurnDegrees * 2f) * fraction);
                points.Add(Offset(State.Position, angle, MaximumDistance));
            }

            points.Add(State.Position);
            return points.AsReadOnly();
        }

        public IReadOnlyList<GameplayPosition> CreateMinimumDistanceArc(
            int arcSegments)
        {
            if (arcSegments < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(arcSegments));
            }

            var points = new List<GameplayPosition>(arcSegments + 1);
            for (int index = 0; index <= arcSegments; index++)
            {
                float fraction = index / (float)arcSegments;
                float angle = State.ForwardDegrees
                    - MaximumTurnDegrees
                    + ((MaximumTurnDegrees * 2f) * fraction);
                points.Add(Offset(State.Position, angle, MinimumDistance));
            }

            return points.AsReadOnly();
        }

        private static GameplayPosition Offset(
            GameplayPosition origin,
            float degrees,
            float distance)
        {
            double radians = degrees * (Math.PI / 180d);
            return new GameplayPosition(
                origin.X + (float)(Math.Sin(radians) * distance),
                origin.Y,
                origin.Z + (float)(Math.Cos(radians) * distance));
        }
    }

    public sealed class VehicleMomentumRecord
    {
        private readonly IReadOnlyList<GameplayPosition> path;

        public VehicleMomentumRecord(
            long sequence,
            VehicleMomentumState previous,
            VehicleMomentumState resulting,
            IEnumerable<GameplayPosition> resolvedPath)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (!string.Equals(
                    previous.VehicleId,
                    resulting.VehicleId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Vehicle movement cannot change vehicle identity.",
                    nameof(resulting));
            }

            if (resolvedPath == null)
            {
                throw new ArgumentNullException(nameof(resolvedPath));
            }

            var copiedPath = new List<GameplayPosition>(resolvedPath);
            if (copiedPath.Count < 2
                || copiedPath[0].DistanceTo(previous.Position) > 0f
                || copiedPath[copiedPath.Count - 1].DistanceTo(resulting.Position) > 0f)
            {
                throw new ArgumentException(
                    "Vehicle records require a complete origin-to-destination path.",
                    nameof(resolvedPath));
            }

            Sequence = sequence;
            Previous = previous;
            Resulting = resulting;
            path = copiedPath.AsReadOnly();
        }

        public long Sequence { get; }

        public VehicleMomentumState Previous { get; }

        public VehicleMomentumState Resulting { get; }

        public IReadOnlyList<GameplayPosition> Path => path;
    }
}
