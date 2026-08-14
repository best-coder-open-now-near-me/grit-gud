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

    public sealed class DisplacementSubjectDefinition
    {
        public DisplacementSubjectDefinition(
            string id,
            DisplacementSubjectKind kind,
            float mass,
            DisplacementSizeClass size = DisplacementSizeClass.Medium)
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

            Id = id;
            Kind = kind;
            Mass = mass;
            Size = size;
        }

        public string Id { get; }

        public DisplacementSubjectKind Kind { get; }

        public float Mass { get; }

        public DisplacementSizeClass Size { get; }
    }
}
