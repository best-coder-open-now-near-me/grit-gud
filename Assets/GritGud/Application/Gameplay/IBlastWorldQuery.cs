using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public readonly struct BlastWorldQuery
    {
        public BlastWorldQuery(GameplayPosition origin, float radius)
        {
            if (float.IsNaN(radius)
                || float.IsInfinity(radius)
                || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            Origin = origin;
            Radius = radius;
        }

        public GameplayPosition Origin { get; }

        public float Radius { get; }
    }

    public sealed class BlastWorldQueryResult
    {
        public BlastWorldQueryResult(
            BlastWorldQuery query,
            long worldStateRevision,
            IEnumerable<BlastEffectRecord> effects)
        {
            if (worldStateRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldStateRevision));
            }

            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var copy = new List<BlastEffectRecord>();
            var subjectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlastEffectRecord effect in effects)
            {
                if (effect == null)
                {
                    throw new ArgumentException(
                        "Blast query results cannot contain null effects.",
                        nameof(effects));
                }

                if (effect.Distance > query.Radius)
                {
                    throw new ArgumentException(
                        "Blast effects must be inside the queried radius.",
                        nameof(effects));
                }

                if (!subjectIds.Add(effect.EntityId))
                {
                    throw new ArgumentException(
                        $"Blast subject '{effect.EntityId}' appears more than once.",
                        nameof(effects));
                }

                copy.Add(effect);
            }

            copy.Sort((left, right) => string.CompareOrdinal(
                left.EntityId,
                right.EntityId));
            Query = query;
            WorldStateRevision = worldStateRevision;
            Effects = copy.AsReadOnly();
        }

        public BlastWorldQuery Query { get; }

        public long WorldStateRevision { get; }

        public IReadOnlyList<BlastEffectRecord> Effects { get; }
    }

    public interface IBlastWorldQuery
    {
        BlastWorldQueryResult Query(BlastWorldQuery query);
    }
}
