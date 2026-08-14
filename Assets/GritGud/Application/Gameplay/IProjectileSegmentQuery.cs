using System;
using GritGud.Domain.Gameplay;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public readonly struct ProjectileSegmentQuery
    {
        public ProjectileSegmentQuery(
            ProjectileFlightSnapshot flight,
            GameplayPosition segmentEnd)
        {
            if (flight.Launch == null
                || flight.Status != ProjectileFlightStatus.InFlight)
            {
                throw new ArgumentException(
                    "Projectile queries require an in-flight state.",
                    nameof(flight));
            }

            if (flight.Position.DistanceTo(segmentEnd) <= 0f)
            {
                throw new ArgumentException(
                    "Projectile queries require a non-empty segment.",
                    nameof(segmentEnd));
            }

            Flight = flight;
            SegmentEnd = segmentEnd;
        }

        public ProjectileFlightSnapshot Flight { get; }

        public string ProjectileId => Flight.ProjectileId;

        public GameplayPosition SegmentStart => Flight.Position;

        public GameplayPosition SegmentEnd { get; }

        public float Radius => Flight.Launch.Definition.Radius;

        public float StartingTurnTime => Flight.ElapsedTurnTime;

        public float ArrivalTurnTime => Flight.ElapsedTurnTime
            + (SegmentStart.DistanceTo(SegmentEnd)
                / Flight.Launch.Definition.SpeedPerTurn);
    }

    public readonly struct ProjectileSegmentQueryResult
    {
        private ProjectileSegmentQueryResult(
            long worldStateRevision,
            string hitEntityId,
            float collisionFraction,
            bool isDefined,
            IEnumerable<BlastEffectRecord> blastEffects = null)
        {
            if (worldStateRevision < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldStateRevision));
            }

            bool hasCollision = !string.IsNullOrWhiteSpace(hitEntityId);
            if (hasCollision
                && (float.IsNaN(collisionFraction)
                    || float.IsInfinity(collisionFraction)
                    || collisionFraction < 0f
                    || collisionFraction > 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(collisionFraction));
            }

            WorldStateRevision = worldStateRevision;
            HitEntityId = hasCollision ? hitEntityId : string.Empty;
            CollisionFraction = hasCollision ? collisionFraction : 0f;
            IsDefined = isDefined;
            BlastEffects = new List<BlastEffectRecord>(
                blastEffects ?? System.Array.Empty<BlastEffectRecord>()).AsReadOnly();
        }

        public long WorldStateRevision { get; }

        public bool HasCollision => !string.IsNullOrEmpty(HitEntityId);

        public string HitEntityId { get; }

        public float CollisionFraction { get; }

        internal bool IsDefined { get; }

        public IReadOnlyList<BlastEffectRecord> BlastEffects { get; }

        public static ProjectileSegmentQueryResult Clear(
            long worldStateRevision) =>
            new ProjectileSegmentQueryResult(
                worldStateRevision,
                string.Empty,
                0f,
                isDefined: true);

        public static ProjectileSegmentQueryResult Collision(
            long worldStateRevision,
            string hitEntityId,
            float collisionFraction,
            IEnumerable<BlastEffectRecord> blastEffects = null)
        {
            if (string.IsNullOrWhiteSpace(hitEntityId))
            {
                throw new ArgumentException(
                    "Projectile collisions require a hit entity identifier.",
                    nameof(hitEntityId));
            }

            return new ProjectileSegmentQueryResult(
                worldStateRevision,
                hitEntityId,
                collisionFraction,
                isDefined: true,
                blastEffects: blastEffects);
        }
    }

    public interface IProjectileSegmentQuery
    {
        /// <summary>
        /// Samples the current world without mutating it. Callers may use the
        /// result as a prediction and repeat the query before committing an
        /// advance after actors have reacted.
        /// </summary>
        ProjectileSegmentQueryResult Query(ProjectileSegmentQuery query);
    }
}
