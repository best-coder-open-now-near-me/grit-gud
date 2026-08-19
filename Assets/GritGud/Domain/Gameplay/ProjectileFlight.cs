using System;

namespace GritGud.Domain.Gameplay
{
    public enum ProjectileFlightStatus
    {
        InFlight,
        Impacted,
        Expired,
    }

    public sealed class ProjectileFlightDefinition
    {
        public ProjectileFlightDefinition(
            string id,
            float speedPerTurn,
            float radius,
            float maximumRange,
            float standingLaunchHeight = 0f,
            float crouchedLaunchHeight = 0f,
            bool opensEmergencyReactionWindow = false,
            float blastRadius = 0f,
            float blastWoundMovementPenalty = 0f,
            float blastIntegrityDamage = 0f)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Projectile identifiers cannot be empty.",
                    nameof(id));
            }

            SpeedPerTurn = RequirePositive(speedPerTurn, nameof(speedPerTurn));
            Radius = RequirePositive(radius, nameof(radius));
            MaximumRange = RequirePositive(maximumRange, nameof(maximumRange));
            StandingLaunchHeight = RequireNonNegative(
                standingLaunchHeight,
                nameof(standingLaunchHeight));
            CrouchedLaunchHeight = RequireNonNegative(
                crouchedLaunchHeight,
                nameof(crouchedLaunchHeight));
            OpensEmergencyReactionWindow = opensEmergencyReactionWindow;
            BlastRadius = RequireNonNegative(blastRadius, nameof(blastRadius));
            BlastWoundMovementPenalty = RequireNonNegative(
                blastWoundMovementPenalty,
                nameof(blastWoundMovementPenalty));
            BlastIntegrityDamage = RequireNonNegative(
                blastIntegrityDamage,
                nameof(blastIntegrityDamage));
            if ((BlastRadius == 0f)
                != (BlastWoundMovementPenalty == 0f
                    && BlastIntegrityDamage == 0f))
                throw new ArgumentException(
                    "Projectile blast radius and consequences must be authored together.");
            Id = id;
        }

        public string Id { get; }

        public float SpeedPerTurn { get; }

        public float Radius { get; }

        public float MaximumRange { get; }

        public float StandingLaunchHeight { get; }

        public float CrouchedLaunchHeight { get; }

        public bool OpensEmergencyReactionWindow { get; }

        public float BlastRadius { get; }

        public float BlastWoundMovementPenalty { get; }

        public float BlastIntegrityDamage { get; }

        public GameplayPosition GetLaunchOrigin(GameplayActorPose pose)
        {
            float height = pose.Stance == ActorStance.Crouched
                ? CrouchedLaunchHeight
                : StandingLaunchHeight;
            return new GameplayPosition(
                pose.Position.X,
                pose.Position.Y + height,
                pose.Position.Z);
        }

        private static float RequirePositive(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static float RequireNonNegative(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    public sealed class ProjectileLaunchRecord
    {
        private const float PositionTolerance = 0.0001f;

        private readonly double directionX;
        private readonly double directionY;
        private readonly double directionZ;

        public ProjectileLaunchRecord(
            long sequence,
            string projectileId,
            string attackerId,
            string intendedTargetId,
            string actionId,
            GameplayPosition origin,
            GameplayPosition aimPoint,
            ProjectileFlightDefinition definition,
            int turnActionPointTimeScale,
            int remainingActionPointsAfterLaunch)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            ProjectileId = RequireText(projectileId, nameof(projectileId));
            AttackerId = RequireText(attackerId, nameof(attackerId));
            IntendedTargetId = RequireText(
                intendedTargetId,
                nameof(intendedTargetId));
            ActionId = RequireText(actionId, nameof(actionId));
            Definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            if (turnActionPointTimeScale <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(turnActionPointTimeScale));
            }
            if (remainingActionPointsAfterLaunch < 0
                || remainingActionPointsAfterLaunch
                    > turnActionPointTimeScale)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingActionPointsAfterLaunch));
            }

            float aimDistance = origin.DistanceTo(aimPoint);
            if (aimDistance <= PositionTolerance)
            {
                throw new ArgumentException(
                    "Projectile launches require a distinct aim point.",
                    nameof(aimPoint));
            }

            Sequence = sequence;
            Origin = origin;
            AimPoint = aimPoint;
            TurnActionPointTimeScale = turnActionPointTimeScale;
            RemainingActionPointsAfterLaunch =
                remainingActionPointsAfterLaunch;
            directionX = ((double)aimPoint.X - origin.X) / aimDistance;
            directionY = ((double)aimPoint.Y - origin.Y) / aimDistance;
            directionZ = ((double)aimPoint.Z - origin.Z) / aimDistance;
        }

        public long Sequence { get; }

        public string ProjectileId { get; }

        public string AttackerId { get; }

        public string IntendedTargetId { get; }

        public string ActionId { get; }

        public GameplayPosition Origin { get; }

        public GameplayPosition AimPoint { get; }

        public ProjectileFlightDefinition Definition { get; }

        public int TurnActionPointTimeScale { get; }

        public int RemainingActionPointsAfterLaunch { get; }

        public GameplayPosition GetPosition(float distanceTraveled)
        {
            if (float.IsNaN(distanceTraveled)
                || float.IsInfinity(distanceTraveled)
                || distanceTraveled < -PositionTolerance
                || distanceTraveled
                    > Definition.MaximumRange + PositionTolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(distanceTraveled));
            }

            // Segment records recover traveled distance from float world
            // positions. That reconstruction can land a few millionths beyond
            // either endpoint even when the authored scalar distance was exact.
            // Preserve the range invariant while accepting harmless coordinate
            // roundoff at the boundary.
            float clampedDistance = Math.Max(
                0f,
                Math.Min(Definition.MaximumRange, distanceTraveled));

            return new GameplayPosition(
                (float)(Origin.X + (directionX * clampedDistance)),
                (float)(Origin.Y + (directionY * clampedDistance)),
                (float)(Origin.Z + (directionZ * clampedDistance)));
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Projectile launch fields cannot be empty.",
                    parameterName);
            }

            return value;
        }
    }

    public sealed class ProjectileImpactRecord
    {
        public ProjectileImpactRecord(
            string projectileId,
            string hitEntityId,
            GameplayPosition position,
            float arrivalTurnTime,
            long worldStateRevision,
            System.Collections.Generic.IEnumerable<BlastEffectRecord> blastEffects = null)
        {
            if (string.IsNullOrWhiteSpace(projectileId))
            {
                throw new ArgumentException(
                    "Projectile impacts require a projectile identifier.",
                    nameof(projectileId));
            }

            if (string.IsNullOrWhiteSpace(hitEntityId))
            {
                throw new ArgumentException(
                    "Projectile impacts require a hit entity identifier.",
                    nameof(hitEntityId));
            }

            if (float.IsNaN(arrivalTurnTime)
                || float.IsInfinity(arrivalTurnTime)
                || arrivalTurnTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(arrivalTurnTime));
            }

            if (worldStateRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(worldStateRevision));
            }

            ProjectileId = projectileId;
            HitEntityId = hitEntityId;
            Position = position;
            ArrivalTurnTime = arrivalTurnTime;
            WorldStateRevision = worldStateRevision;
            BlastEffects = new System.Collections.Generic.List<BlastEffectRecord>(
                blastEffects ?? Array.Empty<BlastEffectRecord>()).AsReadOnly();
        }

        public string ProjectileId { get; }

        public string HitEntityId { get; }

        public GameplayPosition Position { get; }

        public float ArrivalTurnTime { get; }

        public long WorldStateRevision { get; }

        public System.Collections.Generic.IReadOnlyList<BlastEffectRecord> BlastEffects { get; }
    }

    public readonly struct ProjectileFlightSnapshot
    {
        private const float ValueTolerance = 0.0001f;

        public ProjectileFlightSnapshot(
            ProjectileLaunchRecord launch,
            GameplayPosition position,
            float distanceTraveled,
            float elapsedTurnTime,
            ProjectileFlightStatus status,
            ProjectileImpactRecord impact = null)
        {
            Launch = launch ?? throw new ArgumentNullException(nameof(launch));
            if (float.IsNaN(distanceTraveled)
                || float.IsInfinity(distanceTraveled)
                || distanceTraveled < 0f
                || distanceTraveled > launch.Definition.MaximumRange)
            {
                throw new ArgumentOutOfRangeException(nameof(distanceTraveled));
            }

            if (float.IsNaN(elapsedTurnTime)
                || float.IsInfinity(elapsedTurnTime)
                || elapsedTurnTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTurnTime));
            }

            if (!Enum.IsDefined(typeof(ProjectileFlightStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            GameplayPosition expectedPosition = launch.GetPosition(distanceTraveled);
            float expectedTime = distanceTraveled / launch.Definition.SpeedPerTurn;
            if (position.DistanceTo(expectedPosition) > ValueTolerance
                || Math.Abs(elapsedTurnTime - expectedTime) > ValueTolerance)
            {
                throw new ArgumentException(
                    "Projectile state must remain on its recorded trajectory.",
                    nameof(position));
            }

            if (status == ProjectileFlightStatus.Impacted)
            {
                if (impact == null
                    || !string.Equals(
                        launch.ProjectileId,
                        impact.ProjectileId,
                        StringComparison.Ordinal)
                    || impact.Position.DistanceTo(position) > ValueTolerance
                    || Math.Abs(impact.ArrivalTurnTime - elapsedTurnTime)
                        > ValueTolerance)
                {
                    throw new ArgumentException(
                        "Impacted projectiles require a matching impact record.",
                        nameof(impact));
                }
            }
            else if (impact != null)
            {
                throw new ArgumentException(
                    "Only impacted projectiles can contain impact state.",
                    nameof(impact));
            }

            if (status == ProjectileFlightStatus.Expired
                && Math.Abs(distanceTraveled - launch.Definition.MaximumRange)
                    > ValueTolerance)
            {
                throw new ArgumentException(
                    "Expired projectiles must finish at maximum range.",
                    nameof(distanceTraveled));
            }

            if (status == ProjectileFlightStatus.InFlight
                && distanceTraveled + ValueTolerance
                    >= launch.Definition.MaximumRange)
            {
                throw new ArgumentException(
                    "In-flight projectiles cannot already be at maximum range.",
                    nameof(distanceTraveled));
            }

            Position = position;
            DistanceTraveled = distanceTraveled;
            ElapsedTurnTime = elapsedTurnTime;
            Status = status;
            Impact = impact;
        }

        public ProjectileLaunchRecord Launch { get; }

        public string ProjectileId => Launch.ProjectileId;

        public GameplayPosition Position { get; }

        public float DistanceTraveled { get; }

        public float ElapsedTurnTime { get; }

        public ProjectileFlightStatus Status { get; }

        public ProjectileImpactRecord Impact { get; }
    }

    public sealed class ProjectileAdvanceRecord
    {
        private const float ValueTolerance = 0.0001f;

        public ProjectileAdvanceRecord(
            long sequence,
            ProjectileFlightSnapshot previous,
            ProjectileFlightSnapshot resulting,
            float requestedTurnTime,
            GameplayPosition segmentEnd,
            long worldStateRevision,
            float? collisionFraction)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (previous.Launch == null
                || resulting.Launch == null
                || previous.Status != ProjectileFlightStatus.InFlight
                || !string.Equals(
                    previous.ProjectileId,
                    resulting.ProjectileId,
                    StringComparison.Ordinal)
                || previous.Launch.Sequence != resulting.Launch.Sequence)
            {
                throw new ArgumentException(
                    "Projectile advances require one matching in-flight state.",
                    nameof(previous));
            }

            if (float.IsNaN(requestedTurnTime)
                || float.IsInfinity(requestedTurnTime)
                || requestedTurnTime <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedTurnTime));
            }

            if (worldStateRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(worldStateRevision));
            }

            float segmentDistance = previous.Position.DistanceTo(segmentEnd);
            if (segmentDistance <= ValueTolerance)
            {
                throw new ArgumentException(
                    "Projectile advances require a non-empty segment.",
                    nameof(segmentEnd));
            }

            float segmentEndDistance = previous.DistanceTraveled + segmentDistance;
            GameplayPosition expectedEnd = previous.Launch.GetPosition(
                segmentEndDistance);
            if (segmentEnd.DistanceTo(expectedEnd) > ValueTolerance)
            {
                throw new ArgumentException(
                    "Projectile segments must remain on the launch trajectory.",
                    nameof(segmentEnd));
            }

            float expectedResultDistance = segmentEndDistance;
            if (collisionFraction.HasValue)
            {
                float fraction = collisionFraction.Value;
                if (float.IsNaN(fraction)
                    || float.IsInfinity(fraction)
                    || fraction < 0f
                    || fraction > 1f
                    || resulting.Status != ProjectileFlightStatus.Impacted
                    || resulting.Impact == null
                    || resulting.Impact.WorldStateRevision != worldStateRevision)
                {
                    throw new ArgumentException(
                        "Collision advances require a valid frozen impact.",
                        nameof(collisionFraction));
                }

                expectedResultDistance = previous.DistanceTraveled
                    + (segmentDistance * fraction);
            }
            else if (resulting.Status == ProjectileFlightStatus.Impacted)
            {
                throw new ArgumentException(
                    "Impacts require a collision fraction.",
                    nameof(collisionFraction));
            }

            if (Math.Abs(resulting.DistanceTraveled - expectedResultDistance)
                > ValueTolerance)
            {
                throw new ArgumentException(
                    "Projectile result does not match its queried segment.",
                    nameof(resulting));
            }

            Sequence = sequence;
            Previous = previous;
            Resulting = resulting;
            RequestedTurnTime = requestedTurnTime;
            SegmentEnd = segmentEnd;
            WorldStateRevision = worldStateRevision;
            CollisionFraction = collisionFraction;
        }

        public long Sequence { get; }

        public string ProjectileId => Previous.ProjectileId;

        public ProjectileFlightSnapshot Previous { get; }

        public ProjectileFlightSnapshot Resulting { get; }

        public float RequestedTurnTime { get; }

        public GameplayPosition SegmentStart => Previous.Position;

        public GameplayPosition SegmentEnd { get; }

        public long WorldStateRevision { get; }

        public float? CollisionFraction { get; }
    }
}
