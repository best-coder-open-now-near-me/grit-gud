using System;

namespace GritGud.Domain.Gameplay
{
    public enum DestructiblePropState
    {
        Intact,
        Damaged,
        Destroyed,
    }

    public enum DestructiblePropPosture
    {
        Upright,
        Toppled,
    }

    public readonly struct GameplayPropPose
    {
        public GameplayPropPose(
            GameplayPosition position,
            float pitchDegrees,
            float yawDegrees,
            float rollDegrees)
        {
            if (!IsFinite(pitchDegrees)
                || !IsFinite(yawDegrees)
                || !IsFinite(rollDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(pitchDegrees));
            }

            Position = position;
            PitchDegrees = pitchDegrees;
            YawDegrees = yawDegrees;
            RollDegrees = rollDegrees;
        }

        public GameplayPosition Position { get; }

        public float PitchDegrees { get; }

        public float YawDegrees { get; }

        public float RollDegrees { get; }

        public GameplayPropPose WithPosition(GameplayPosition position) =>
            new GameplayPropPose(
                position,
                PitchDegrees,
                YawDegrees,
                RollDegrees);

        public bool HasSameState(GameplayPropPose other) =>
            Position.DistanceTo(other.Position) == 0f
            && PitchDegrees == other.PitchDegrees
            && YawDegrees == other.YawDegrees
            && RollDegrees == other.RollDegrees;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class DestructiblePropDefinition
    {
        public DestructiblePropDefinition(
            string id,
            float maximumIntegrity,
            DestructiblePropState initialState)
            : this(
                id,
                maximumIntegrity,
                initialState,
                new GameplayPosition(0f, 0f, 0f))
        {
        }

        public DestructiblePropDefinition(
            string id,
            float maximumIntegrity,
            DestructiblePropState initialState,
            GameplayPosition position)
            : this(
                id,
                maximumIntegrity,
                initialState,
                new GameplayPropPose(position, 0f, 0f, 0f),
                DestructiblePropPosture.Upright)
        {
        }

        public DestructiblePropDefinition(
            string id,
            float maximumIntegrity,
            DestructiblePropState initialState,
            GameplayPropPose pose,
            DestructiblePropPosture initialPosture)
            : this(
                id,
                maximumIntegrity,
                initialState,
                pose,
                initialPosture,
                fractureChunkCount: 0)
        {
        }

        public DestructiblePropDefinition(
            string id,
            float maximumIntegrity,
            DestructiblePropState initialState,
            GameplayPropPose pose,
            DestructiblePropPosture initialPosture,
            int fractureChunkCount)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Destructible props require stable identifiers.",
                    nameof(id));
            }

            if (!IsFinitePositive(maximumIntegrity))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumIntegrity));
            }

            if (!Enum.IsDefined(typeof(DestructiblePropState), initialState))
            {
                throw new ArgumentOutOfRangeException(nameof(initialState));
            }

            if (!Enum.IsDefined(
                    typeof(DestructiblePropPosture),
                    initialPosture))
            {
                throw new ArgumentOutOfRangeException(nameof(initialPosture));
            }

            DestructibleFracture.AllChunksMask(fractureChunkCount);

            Id = id;
            MaximumIntegrity = maximumIntegrity;
            InitialState = initialState;
            Pose = pose;
            InitialPosture = initialPosture;
            FractureChunkCount = fractureChunkCount;
        }

        public string Id { get; }

        public float MaximumIntegrity { get; }

        public DestructiblePropState InitialState { get; }

        public GameplayPosition Position => Pose.Position;

        public GameplayPropPose Pose { get; }

        public DestructiblePropPosture InitialPosture { get; }

        public int FractureChunkCount { get; }

        public DestructiblePropSnapshot CreateInitialSnapshot()
        {
            float remainingIntegrity;
            switch (InitialState)
            {
                case DestructiblePropState.Intact:
                    remainingIntegrity = MaximumIntegrity;
                    break;
                case DestructiblePropState.Damaged:
                    remainingIntegrity = MaximumIntegrity * 0.5f;
                    break;
                case DestructiblePropState.Destroyed:
                    remainingIntegrity = 0f;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported destructible state '{InitialState}'.");
            }

            return new DestructiblePropSnapshot(
                Id,
                InitialState,
                MaximumIntegrity,
                remainingIntegrity,
                Pose,
                InitialPosture,
                FractureChunkCount,
                DestructibleFracture.CreateInitialMask(
                    InitialState,
                    FractureChunkCount,
                    Id));
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    public readonly struct DestructiblePropSnapshot
    {
        public DestructiblePropSnapshot(
            string propId,
            DestructiblePropState state,
            float maximumIntegrity,
            float remainingIntegrity)
            : this(
                propId,
                state,
                maximumIntegrity,
                remainingIntegrity,
                new GameplayPosition(0f, 0f, 0f))
        {
        }

        public DestructiblePropSnapshot(
            string propId,
            DestructiblePropState state,
            float maximumIntegrity,
            float remainingIntegrity,
            GameplayPosition position)
            : this(
                propId,
                state,
                maximumIntegrity,
                remainingIntegrity,
                new GameplayPropPose(position, 0f, 0f, 0f),
                DestructiblePropPosture.Upright)
        {
        }

        public DestructiblePropSnapshot(
            string propId,
            DestructiblePropState state,
            float maximumIntegrity,
            float remainingIntegrity,
            GameplayPropPose pose,
            DestructiblePropPosture posture)
            : this(
                propId,
                state,
                maximumIntegrity,
                remainingIntegrity,
                pose,
                posture,
                fractureChunkCount: 0,
                detachedFractureChunks: 0UL)
        {
        }

        public DestructiblePropSnapshot(
            string propId,
            DestructiblePropState state,
            float maximumIntegrity,
            float remainingIntegrity,
            GameplayPropPose pose,
            DestructiblePropPosture posture,
            int fractureChunkCount,
            ulong detachedFractureChunks)
        {
            if (string.IsNullOrWhiteSpace(propId))
            {
                throw new ArgumentException(
                    "Destructible snapshots require stable identifiers.",
                    nameof(propId));
            }

            if (!IsFinite(maximumIntegrity) || maximumIntegrity <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumIntegrity));
            }

            if (!IsFinite(remainingIntegrity)
                || remainingIntegrity < 0f
                || remainingIntegrity > maximumIntegrity)
            {
                throw new ArgumentOutOfRangeException(nameof(remainingIntegrity));
            }

            if (!Enum.IsDefined(typeof(DestructiblePropState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            if (!Enum.IsDefined(typeof(DestructiblePropPosture), posture))
            {
                throw new ArgumentOutOfRangeException(nameof(posture));
            }

            bool stateMatchesIntegrity = state == DestructiblePropState.Intact
                ? remainingIntegrity == maximumIntegrity
                : state == DestructiblePropState.Damaged
                    ? remainingIntegrity > 0f && remainingIntegrity < maximumIntegrity
                    : remainingIntegrity == 0f;
            if (!stateMatchesIntegrity)
            {
                throw new ArgumentException(
                    "Destructible state must agree with remaining integrity.",
                    nameof(state));
            }
            DestructibleFracture.ValidateSnapshot(
                state,
                fractureChunkCount,
                detachedFractureChunks);

            PropId = propId;
            State = state;
            MaximumIntegrity = maximumIntegrity;
            RemainingIntegrity = remainingIntegrity;
            Pose = pose;
            Posture = posture;
            FractureChunkCount = fractureChunkCount;
            DetachedFractureChunks = detachedFractureChunks;
        }

        public string PropId { get; }

        public DestructiblePropState State { get; }

        public float MaximumIntegrity { get; }

        public float RemainingIntegrity { get; }

        public GameplayPosition Position => Pose.Position;

        public GameplayPropPose Pose { get; }

        public DestructiblePropPosture Posture { get; }

        public int FractureChunkCount { get; }

        public ulong DetachedFractureChunks { get; }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class DestructibleDamageRecord
    {
        public DestructibleDamageRecord(
            long sequence,
            float appliedDamage,
            DestructiblePropSnapshot previous,
            DestructiblePropSnapshot resulting)
            : this(
                sequence,
                appliedDamage,
                previous,
                resulting,
                preferredFractureChunkIndex: -1)
        {
        }

        public DestructibleDamageRecord(
            long sequence,
            float appliedDamage,
            DestructiblePropSnapshot previous,
            DestructiblePropSnapshot resulting,
            int preferredFractureChunkIndex)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (float.IsNaN(appliedDamage)
                || float.IsInfinity(appliedDamage)
                || appliedDamage <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(appliedDamage));
            }

            if (!string.Equals(
                    previous.PropId,
                    resulting.PropId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A damage record cannot change prop identity.",
                    nameof(resulting));
            }
            if (previous.FractureChunkCount != resulting.FractureChunkCount
                || preferredFractureChunkIndex < -1
                || preferredFractureChunkIndex >= previous.FractureChunkCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preferredFractureChunkIndex));
            }

            Sequence = sequence;
            AppliedDamage = appliedDamage;
            Previous = previous;
            Resulting = resulting;
            PreferredFractureChunkIndex = preferredFractureChunkIndex;
        }

        public long Sequence { get; }

        public string PropId => Previous.PropId;

        public float AppliedDamage { get; }

        public DestructiblePropSnapshot Previous { get; }

        public DestructiblePropSnapshot Resulting { get; }

        public int PreferredFractureChunkIndex { get; }

        public ulong NewlyDetachedFractureChunks =>
            Resulting.DetachedFractureChunks
            & ~Previous.DetachedFractureChunks;
    }
}
