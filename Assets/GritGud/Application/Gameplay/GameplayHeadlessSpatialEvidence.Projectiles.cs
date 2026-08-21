using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    public sealed partial class GameplayHeadlessSpatialEvidence
    {
        public ProjectileSegmentQueryResult CaptureProjectileSegment(
            GameplayCombatStateSnapshot state,
            ProjectileSegmentQuery query)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.RequireCoverage(GameplayCombatStateCoverage.Projectiles);
            ProjectileFlightSnapshot canonical = FindProjectile(
                state.Projectiles,
                query.ProjectileId);
            if (!string.Equals(
                    GameplayCanonicalValueDigest.Calculate(canonical),
                    GameplayCanonicalValueDigest.Calculate(query.Flight),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Projectile segment evidence starts from stale flight state.");

            float bestFraction = float.PositiveInfinity;
            string bestEntityId = null;
            if (TryFindFirstObstacleHit(
                    state,
                    query.SegmentStart,
                    query.SegmentEnd,
                    query.Radius,
                    out string obstacleId,
                    out _,
                    out _,
                    out _,
                    out float obstacleFraction))
            {
                bestFraction = obstacleFraction;
                bestEntityId = obstacleId;
            }
            ConsiderSurfaceHits(
                query.SegmentStart,
                query.SegmentEnd,
                query.Radius,
                ref bestFraction,
                ref bestEntityId);
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
            {
                if (string.Equals(
                        actor.ActorId,
                        query.Flight.Launch.AttackerId,
                        StringComparison.Ordinal))
                    continue;
                foreach (TargetRegionSample sample in
                    ActorTargetProfileCatalog.CreateWorldSamples(
                        actor.Pose,
                        actor.IsPinned))
                    ConsiderSphereHit(
                        query.SegmentStart,
                        query.SegmentEnd,
                        sample.Center,
                        sample.Radius + query.Radius,
                        actor.ActorId,
                        ref bestFraction,
                        ref bestEntityId);
            }
            foreach (DroneSnapshot drone in state.Drones)
            {
                if (!drone.IsOperational) continue;
                foreach (TargetRegionSample sample in
                    GameplayDroneTargetProfile.CreateWorldSamples(drone))
                    ConsiderSphereHit(
                        query.SegmentStart,
                        query.SegmentEnd,
                        sample.Center,
                        sample.Radius + query.Radius,
                        drone.DroneId,
                        ref bestFraction,
                        ref bestEntityId);
            }

            if (bestEntityId == null)
                return ProjectileSegmentQueryResult.Clear(
                    state.Session.JournalSequence);
            GameplayPosition impact = Lerp(
                query.SegmentStart,
                query.SegmentEnd,
                bestFraction);
            IReadOnlyList<BlastEffectRecord> blastEffects =
                query.Flight.Launch.Definition.BlastRadius <= 0f
                    ? Array.Empty<BlastEffectRecord>()
                    : CaptureBlastEffects(
                        state,
                        impact,
                        query.Flight.Launch.Definition.BlastRadius);
            return ProjectileSegmentQueryResult.Collision(
                state.Session.JournalSequence,
                bestEntityId,
                bestFraction,
                blastEffects);
        }

        private void ConsiderSurfaceHits(
            GameplayPosition origin,
            GameplayPosition destination,
            float radius,
            ref float bestFraction,
            ref string bestEntityId)
        {
            float distance = origin.DistanceTo(destination);
            int steps = Math.Max(1, (int)Math.Ceiling(distance / 0.05f));
            foreach (TerrainSurfaceData surface in terrainSurfaces)
                ConsiderTerrainSurfaceHit(
                    surface,
                    origin,
                    destination,
                    radius,
                    steps,
                    ref bestFraction,
                    ref bestEntityId);
            foreach (PlacementSurfaceDefinition surface in placementSurfaces)
                ConsiderPlacementSurfaceHit(
                    surface,
                    origin,
                    destination,
                    radius,
                    steps,
                    ref bestFraction,
                    ref bestEntityId);
        }

        private static void ConsiderTerrainSurfaceHit(
            TerrainSurfaceData surface,
            GameplayPosition origin,
            GameplayPosition destination,
            float radius,
            int steps,
            ref float bestFraction,
            ref string bestEntityId)
        {
            bool previousDefined = TryEvaluateTerrainHeight(
                surface,
                origin,
                out float previousHeight);
            float previousClearance = previousDefined
                ? origin.Y - radius - previousHeight
                : 0f;
            float previousFraction = 0f;
            for (int index = 1; index <= steps; index++)
            {
                float fraction = (float)index / steps;
                if (fraction >= bestFraction) break;
                GameplayPosition point = Lerp(origin, destination, fraction);
                bool defined = TryEvaluateTerrainHeight(
                    surface,
                    point,
                    out float height);
                float clearance = defined
                    ? point.Y - radius - height
                    : 0f;
                if (previousDefined
                    && defined
                    && previousClearance > 0f
                    && clearance <= 0f)
                {
                    float hit = RefineTerrainSurfaceHit(
                        surface,
                        origin,
                        destination,
                        radius,
                        previousFraction,
                        fraction);
                    SetEarlierHit(
                        hit,
                        "world.terrain." + surface.id,
                        ref bestFraction,
                        ref bestEntityId);
                    break;
                }
                previousDefined = defined;
                previousClearance = clearance;
                previousFraction = fraction;
            }
        }

        private static float RefineTerrainSurfaceHit(
            TerrainSurfaceData surface,
            GameplayPosition origin,
            GameplayPosition destination,
            float radius,
            float lower,
            float upper)
        {
            for (int iteration = 0; iteration < 16; iteration++)
            {
                float middle = (lower + upper) * 0.5f;
                GameplayPosition point = Lerp(origin, destination, middle);
                if (TryEvaluateTerrainHeight(surface, point, out float height)
                    && point.Y - radius - height <= 0f)
                    upper = middle;
                else
                    lower = middle;
            }
            return upper;
        }

        private static void ConsiderPlacementSurfaceHit(
            PlacementSurfaceDefinition surface,
            GameplayPosition origin,
            GameplayPosition destination,
            float radius,
            int steps,
            ref float bestFraction,
            ref string bestEntityId)
        {
            bool previousDefined = TryEvaluatePlacementHeight(
                surface,
                origin,
                out float previousHeight);
            float previousClearance = previousDefined
                ? origin.Y - radius - previousHeight
                : 0f;
            float previousFraction = 0f;
            for (int index = 1; index <= steps; index++)
            {
                float fraction = (float)index / steps;
                if (fraction >= bestFraction) break;
                GameplayPosition point = Lerp(origin, destination, fraction);
                bool defined = TryEvaluatePlacementHeight(
                    surface,
                    point,
                    out float height);
                float clearance = defined
                    ? point.Y - radius - height
                    : 0f;
                if (previousDefined
                    && defined
                    && previousClearance > 0f
                    && clearance <= 0f)
                {
                    float hit = RefinePlacementSurfaceHit(
                        surface,
                        origin,
                        destination,
                        radius,
                        previousFraction,
                        fraction);
                    SetEarlierHit(
                        hit,
                        surface.EntityId,
                        ref bestFraction,
                        ref bestEntityId);
                    break;
                }
                previousDefined = defined;
                previousClearance = clearance;
                previousFraction = fraction;
            }
        }

        private static float RefinePlacementSurfaceHit(
            PlacementSurfaceDefinition surface,
            GameplayPosition origin,
            GameplayPosition destination,
            float radius,
            float lower,
            float upper)
        {
            for (int iteration = 0; iteration < 16; iteration++)
            {
                float middle = (lower + upper) * 0.5f;
                GameplayPosition point = Lerp(origin, destination, middle);
                if (TryEvaluatePlacementHeight(surface, point, out float height)
                    && point.Y - radius - height <= 0f)
                    upper = middle;
                else
                    lower = middle;
            }
            return upper;
        }

        private static void ConsiderSphereHit(
            GameplayPosition origin,
            GameplayPosition destination,
            GameplayPosition center,
            float radius,
            string entityId,
            ref float bestFraction,
            ref string bestEntityId)
        {
            if (!TryIntersectSphere(
                    origin,
                    destination,
                    center,
                    radius,
                    out float fraction))
                return;
            SetEarlierHit(
                fraction,
                entityId,
                ref bestFraction,
                ref bestEntityId);
        }

        private static bool TryIntersectSphere(
            GameplayPosition origin,
            GameplayPosition destination,
            GameplayPosition center,
            float radius,
            out float fraction)
        {
            double dx = destination.X - origin.X;
            double dy = destination.Y - origin.Y;
            double dz = destination.Z - origin.Z;
            double mx = origin.X - center.X;
            double my = origin.Y - center.Y;
            double mz = origin.Z - center.Z;
            double a = (dx * dx) + (dy * dy) + (dz * dz);
            double c = (mx * mx) + (my * my) + (mz * mz)
                - (radius * radius);
            if (c <= 0d)
            {
                fraction = 0f;
                return true;
            }
            double b = (mx * dx) + (my * dy) + (mz * dz);
            if (b >= 0d)
            {
                fraction = 0f;
                return false;
            }
            double discriminant = (b * b) - (a * c);
            if (discriminant < 0d)
            {
                fraction = 0f;
                return false;
            }
            double value = (-b - Math.Sqrt(discriminant)) / a;
            if (value < 0d || value > 1d)
            {
                fraction = 0f;
                return false;
            }
            fraction = (float)value;
            return true;
        }

        private static void SetEarlierHit(
            float fraction,
            string entityId,
            ref float bestFraction,
            ref string bestEntityId)
        {
            if (fraction > bestFraction + 0.000001f
                || (Math.Abs(fraction - bestFraction) <= 0.000001f
                    && bestEntityId != null
                    && StringComparer.Ordinal.Compare(
                        entityId,
                        bestEntityId) >= 0))
                return;
            bestFraction = fraction;
            bestEntityId = entityId;
        }

        private static ProjectileFlightSnapshot FindProjectile(
            IReadOnlyList<ProjectileFlightSnapshot> projectiles,
            string projectileId)
        {
            foreach (ProjectileFlightSnapshot projectile in projectiles)
                if (string.Equals(
                    projectile.ProjectileId,
                    projectileId,
                    StringComparison.Ordinal))
                    return projectile;
            throw new KeyNotFoundException(
                $"Projectile '{projectileId}' is absent from canonical state.");
        }
    }
}
