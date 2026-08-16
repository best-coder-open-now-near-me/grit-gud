using System;

namespace GritGud.Domain.Gameplay
{
    public enum DisplacementSizeClass
    {
        Tiny,
        Small,
        Medium,
        Large,
        Huge,
    }

    public sealed class PropTopplingDefinition
    {
        public PropTopplingDefinition(
            float pitchOffsetDegrees,
            float rollOffsetDegrees,
            float elevationOffset)
        {
            if (!IsFinite(pitchOffsetDegrees))
                throw new ArgumentOutOfRangeException(nameof(pitchOffsetDegrees));
            if (!IsFinite(rollOffsetDegrees))
                throw new ArgumentOutOfRangeException(nameof(rollOffsetDegrees));
            if (pitchOffsetDegrees == 0f && rollOffsetDegrees == 0f)
            {
                throw new ArgumentException(
                    "Toppling requires a non-zero pitch or roll offset.");
            }

            if (!IsFinite(elevationOffset) || elevationOffset < 0f)
                throw new ArgumentOutOfRangeException(nameof(elevationOffset));

            PitchOffsetDegrees = pitchOffsetDegrees;
            RollOffsetDegrees = rollOffsetDegrees;
            ElevationOffset = elevationOffset;
        }

        public float PitchOffsetDegrees { get; }

        public float RollOffsetDegrees { get; }

        public float ElevationOffset { get; }

        public PropDisplacementState Resolve(
            GameplayPropPose previousPose,
            GameplayPosition destination) =>
            new PropDisplacementState(
                new GameplayPropPose(
                    new GameplayPosition(
                        destination.X,
                        destination.Y + ElevationOffset,
                        destination.Z),
                    previousPose.PitchDegrees + PitchOffsetDegrees,
                    previousPose.YawDegrees,
                    previousPose.RollDegrees + RollOffsetDegrees),
                DestructiblePropPosture.Toppled);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class PropPinningDefinition
    {
        public PropPinningDefinition(
            float maximumActorMass,
            float minimumContactDepth = 0f)
        {
            if (!IsFinitePositive(maximumActorMass))
                throw new ArgumentOutOfRangeException(nameof(maximumActorMass));
            if (!IsFinite(minimumContactDepth) || minimumContactDepth < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumContactDepth));

            MaximumActorMass = maximumActorMass;
            MinimumContactDepth = minimumContactDepth;
        }

        public float MaximumActorMass { get; }

        public float MinimumContactDepth { get; }

        public bool Accepts(float actorMass, float contactDepth) =>
            actorMass <= MaximumActorMass
            && contactDepth >= MinimumContactDepth;

        private static bool IsFinitePositive(float value) =>
            IsFinite(value) && value > 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class DisplacementSubjectDefinition
    {
        public DisplacementSubjectDefinition(
            string id,
            DisplacementSubjectKind kind,
            float mass,
            DisplacementSizeClass size = DisplacementSizeClass.Medium,
            PropTopplingDefinition toppling = null,
            PropPinningDefinition pinning = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Displacement subjects require stable identifiers.",
                    nameof(id));
            }

            if (!Enum.IsDefined(typeof(DisplacementSubjectKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (float.IsNaN(mass)
                || float.IsInfinity(mass)
                || mass <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(mass));
            }

            if (!Enum.IsDefined(typeof(DisplacementSizeClass), size))
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            if (toppling != null && kind != DisplacementSubjectKind.Prop)
            {
                throw new ArgumentException(
                    "Only prop displacement subjects can define toppling.",
                    nameof(toppling));
            }

            if (pinning != null && (kind != DisplacementSubjectKind.Prop
                || toppling == null))
            {
                throw new ArgumentException(
                    "Only toppling props can define actor pinning.",
                    nameof(pinning));
            }

            Id = id;
            Kind = kind;
            Mass = mass;
            Size = size;
            Toppling = toppling;
            Pinning = pinning;
        }

        public string Id { get; }

        public DisplacementSubjectKind Kind { get; }

        public float Mass { get; }

        public DisplacementSizeClass Size { get; }

        public PropTopplingDefinition Toppling { get; }

        public PropPinningDefinition Pinning { get; }
    }
}
