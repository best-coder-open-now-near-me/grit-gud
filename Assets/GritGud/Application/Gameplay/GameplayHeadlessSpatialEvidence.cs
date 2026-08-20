using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    public readonly struct GameplayLocalSpatialVolume
    {
        public GameplayLocalSpatialVolume(
            GameplayPosition center,
            GameplayPosition size)
        {
            GameplayNumericPolicy.RequireFinite(size.X, nameof(size));
            GameplayNumericPolicy.RequireFinite(size.Y, nameof(size));
            GameplayNumericPolicy.RequireFinite(size.Z, nameof(size));
            if (size.X <= 0f || size.Y <= 0f || size.Z <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    "Spatial volume dimensions must be positive.");
            }

            Center = center;
            Size = size;
        }

        public GameplayPosition Center { get; }
        public GameplayPosition Size { get; }
    }

    public sealed class GameplayFractureSpatialProfile
    {
        public GameplayFractureSpatialProfile(
            string profileId,
            IEnumerable<GameplayLocalSpatialVolume> chunkVolumes)
        {
            ProfileId = GameplayContentIdentity.RequireText(
                profileId,
                nameof(profileId));
            if (chunkVolumes == null)
                throw new ArgumentNullException(nameof(chunkVolumes));
            var chunks = new List<GameplayLocalSpatialVolume>(chunkVolumes);
            if (chunks.Count < 2
                || chunks.Count > DestructibleFracture.MaximumChunkCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunkVolumes),
                    $"Fracture spatial profiles require between 2 and "
                    + $"{DestructibleFracture.MaximumChunkCount} chunks.");
            }
            ChunkVolumes = chunks.AsReadOnly();
        }

        public string ProfileId { get; }
        public IReadOnlyList<GameplayLocalSpatialVolume> ChunkVolumes { get; }
        public int ChunkCount => ChunkVolumes.Count;
    }

    public sealed class GameplaySpatialEvidenceStamp
    {
        public GameplaySpatialEvidenceStamp(
            SpatialContentIdentity spatialIdentity,
            string dynamicSpatialDigest,
            long worldRevision)
        {
            SpatialIdentity = spatialIdentity ?? throw new ArgumentNullException(
                nameof(spatialIdentity));
            DynamicSpatialDigest = GameplayContentIdentity.RequireDigest(
                dynamicSpatialDigest,
                nameof(dynamicSpatialDigest));
            if (worldRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(worldRevision));
            WorldRevision = worldRevision;
        }

        public SpatialContentIdentity SpatialIdentity { get; }
        public string DynamicSpatialDigest { get; }
        public long WorldRevision { get; }
    }

    public static class GameplayDynamicSpatialFingerprint
    {
        public static string Hash(GameplayCombatStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var text = new StringBuilder();
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
            {
                Append(text, "actor", actor.ActorId);
                Append(text, "position", actor.Pose.Position);
                Append(text, "stance", actor.Pose.Stance.ToString());
                Append(text, "wounds", actor.Wounds.WoundCount.ToString());
            }
            foreach (DestructiblePropSnapshot prop in state.Destructibles)
            {
                Append(text, "prop", prop.PropId);
                Append(text, "state", prop.State.ToString());
                Append(text, "position", prop.Pose.Position);
                Append(text, "yaw", prop.Pose.YawDegrees);
                Append(text, "pitch", prop.Pose.PitchDegrees);
                Append(text, "roll", prop.Pose.RollDegrees);
                Append(text, "posture", prop.Posture.ToString());
                Append(text, "fracture", prop.DetachedFractureChunks.ToString());
            }
            foreach (VehicleMomentumState vehicle in state.Vehicles)
            {
                Append(text, "vehicle", vehicle.VehicleId);
                Append(text, "position", vehicle.Position);
                Append(text, "forward", vehicle.ForwardDegrees);
            }
            foreach (DroneSnapshot drone in state.Drones)
            {
                Append(text, "drone", drone.DroneId);
                Append(text, "position", drone.Position);
                Append(text, "facing", drone.FacingDegrees);
                Append(text, "integrity", drone.RemainingIntegrity);
            }
            foreach (SmokeFieldSnapshot smoke in state.SmokeFields)
            {
                Append(text, "smoke", smoke.Field.Id);
                Append(text, "origin", smoke.Field.Origin);
                Append(text, "remaining", smoke.RemainingFraction);
            }
            foreach (FireFieldSnapshot fire in state.FireFields)
            {
                Append(text, "fire", fire.Field.Id);
                Append(text, "origin", fire.Field.Origin);
                Append(text, "radius", fire.CurrentRadius);
                Append(text, "remaining", fire.RemainingFraction);
                Append(text, "pulse", fire.PulseProgress);
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(text.ToString()));
                var result = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                    result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }

        private static void Append(
            StringBuilder text,
            string key,
            object value)
        {
            text.Append(key).Append('=');
            switch (value)
            {
                case GameplayPosition position:
                    text.Append(GameplayNumericPolicy.FormatCanonical(position.X))
                        .Append(',')
                        .Append(GameplayNumericPolicy.FormatCanonical(position.Y))
                        .Append(',')
                        .Append(GameplayNumericPolicy.FormatCanonical(position.Z));
                    break;
                case float number:
                    text.Append(GameplayNumericPolicy.FormatCanonical(number));
                    break;
                default:
                    text.Append(value);
                    break;
            }
            text.Append('\n');
        }
    }

    public sealed class GameplayHeadlessSpatialEvidence
    {
        private sealed class ObstacleDefinition
        {
            public ObstacleDefinition(
                string entityId,
                GameplayPropPose staticPose,
                bool isDestructible,
                IReadOnlyList<CoverVolumeData> volumes,
                GameplayFractureSpatialProfile fractureProfile)
            {
                EntityId = entityId;
                StaticPose = staticPose;
                IsDestructible = isDestructible;
                Volumes = volumes;
                FractureProfile = fractureProfile;
            }

            public string EntityId { get; }
            public GameplayPropPose StaticPose { get; }
            public bool IsDestructible { get; }
            public IReadOnlyList<CoverVolumeData> Volumes { get; }
            public GameplayFractureSpatialProfile FractureProfile { get; }
        }

        private sealed class PlacementSurfaceDefinition
        {
            public PlacementSurfaceDefinition(
                string entityId,
                GameplayPropPose pose,
                LevelPlacementSurfaceData surface)
            {
                EntityId = entityId;
                Pose = pose;
                Surface = surface;
            }

            public string EntityId { get; }
            public GameplayPropPose Pose { get; }
            public LevelPlacementSurfaceData Surface { get; }
        }

        private readonly SpatialContentIdentity spatialIdentity;
        private readonly IReadOnlyList<ObstacleDefinition> obstacles;
        private readonly IReadOnlyList<PlacementSurfaceDefinition>
            placementSurfaces;
        private readonly IReadOnlyList<TerrainSurfaceData> terrainSurfaces;
        private readonly IReadOnlyDictionary<string, string>
            destructibleSurfaceIds;
        private readonly bool hasDestructibleObstacles;

        public GameplayHeadlessSpatialEvidence(
            LevelDocument level,
            SpatialContentIdentity identity,
            IReadOnlyDictionary<string, GameplayFractureSpatialProfile>
                fractureProfilesByArchetype = null)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            spatialIdentity = identity ?? throw new ArgumentNullException(
                nameof(identity));
            if (!string.Equals(
                    level.levelId,
                    identity.LevelId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Spatial identity does not describe the supplied level.",
                    nameof(identity));
            var result = new List<ObstacleDefinition>();
            var surfaces = new List<PlacementSurfaceDefinition>();
            var materialSurfaces = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (LevelEntity entity in level.entities)
            {
                if (entity == null) continue;
                var pose = new GameplayPropPose(
                        new GameplayPosition(
                            entity.transform.position.x,
                            entity.transform.position.y,
                            entity.transform.position.z),
                        entity.transform.pitchDegrees,
                        entity.transform.yawDegrees,
                        entity.transform.rollDegrees);
                if (entity.placementSurface != null)
                    surfaces.Add(new PlacementSurfaceDefinition(
                        entity.id,
                        pose,
                        entity.placementSurface.DeepCopy()));
                if (entity.destructible?.enabled == true)
                {
                    string surfaceId = GameplayContentIdentity.RequireText(
                        entity.destructible.surfaceId,
                        $"destructible surface for '{entity.id}'");
                    if (!materialSurfaces.TryAdd(entity.id, surfaceId))
                        throw new InvalidOperationException(
                            $"Destructible spatial entity '{entity.id}' is duplicated.");
                }
                if (entity.coverVolumes.Count == 0) continue;
                bool destructible = entity.destructible?.enabled == true;
                result.Add(new ObstacleDefinition(
                    entity.id,
                    pose,
                    destructible,
                    new List<CoverVolumeData>(
                        entity.coverVolumes).AsReadOnly(),
                    destructible
                        ? ResolveFractureProfile(
                            entity.archetypeId,
                            fractureProfilesByArchetype)
                        : null));
            }
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.EntityId,
                right.EntityId));
            surfaces.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.EntityId,
                right.EntityId));
            obstacles = result.AsReadOnly();
            placementSurfaces = surfaces.AsReadOnly();
            destructibleSurfaceIds = materialSurfaces;
            var terrain = new List<TerrainSurfaceData>();
            foreach (TerrainSurfaceData surface in level.terrainSurfaces)
                if (surface != null) terrain.Add(surface.DeepCopy());
            terrain.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.id,
                right.id));
            terrainSurfaces = terrain.AsReadOnly();
            foreach (ObstacleDefinition obstacle in obstacles)
                if (obstacle.IsDestructible)
                {
                    hasDestructibleObstacles = true;
                    break;
                }
        }

        public GameplaySpatialEvidenceStamp Stamp(
            GameplayCombatStateSnapshot state) => new GameplaySpatialEvidenceStamp(
                spatialIdentity,
                GameplayDynamicSpatialFingerprint.Hash(state),
                state?.Session.Revision
                    ?? throw new ArgumentNullException(nameof(state)));

        public string GetDestructibleSurfaceId(string propId)
        {
            string id = GameplayContentIdentity.RequireText(
                propId,
                nameof(propId));
            if (destructibleSurfaceIds.TryGetValue(id, out string surfaceId))
                return surfaceId;
            throw new InvalidOperationException(
                $"Destructible prop '{id}' has no authoritative spatial surface metadata.");
        }

        /// <summary>
        /// Mirrors authored spawn grounding without Unity physics. The highest
        /// portable placement or terrain surface intersected by the same
        /// vertical probe wins, and the canonical actor root retains the live
        /// controller's ground clearance.
        /// </summary>
        public GameplayPosition ResolveSpawnPosition(
            GameplayPosition authoredPosition,
            float rootGroundClearance = 0.02f)
        {
            GameplayNumericPolicy.RequireFinite(
                rootGroundClearance,
                nameof(rootGroundClearance));
            if (rootGroundClearance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(rootGroundClearance));
            if (!TryResolveSurfaceHeight(
                    authoredPosition,
                    authoredPosition.Y + 8f,
                    authoredPosition.Y - 12f,
                    out float height))
                throw new InvalidOperationException(
                    $"Gameplay spawn at '{Format(authoredPosition)}' has no portable walkable surface below it.");
            return new GameplayPosition(
                authoredPosition.X,
                height + rootGroundClearance,
                authoredPosition.Z);
        }

        public bool TryResolveMovementPosition(
            GameplayPosition from,
            GameplayPosition requestedDestination,
            float maximumVerticalReach,
            out GameplayPosition resolved,
            float rootGroundClearance = 0.02f)
        {
            GameplayNumericPolicy.RequireFinite(
                maximumVerticalReach,
                nameof(maximumVerticalReach));
            GameplayNumericPolicy.RequireFinite(
                rootGroundClearance,
                nameof(rootGroundClearance));
            if (maximumVerticalReach < 0f || rootGroundClearance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumVerticalReach));
            float currentSurface = from.Y - rootGroundClearance;
            if (!TryResolveSurfaceHeight(
                    requestedDestination,
                    currentSurface + maximumVerticalReach,
                    currentSurface - maximumVerticalReach,
                    out float height))
            {
                resolved = default;
                return false;
            }
            resolved = new GameplayPosition(
                requestedDestination.X,
                height + rootGroundClearance,
                requestedDestination.Z);
            return true;
        }

        public bool BlocksLineOfSight(
            GameplayCombatStateSnapshot state,
            GameplayPosition origin,
            GameplayPosition destination) => BlocksSegment(
                state,
                origin,
                destination,
                expansion: 0f);

        public bool BlocksPath(
            GameplayCombatStateSnapshot state,
            GameplayPosition origin,
            GameplayPosition destination,
            float clearanceRadius)
        {
            GameplayNumericPolicy.RequireFinite(
                clearanceRadius,
                nameof(clearanceRadius));
            if (clearanceRadius < 0f)
                throw new ArgumentOutOfRangeException(nameof(clearanceRadius));
            return BlocksSegment(
                state,
                origin,
                destination,
                clearanceRadius);
        }

        public float EvaluateBlastExposure(
            GameplayCombatStateSnapshot state,
            GameplayPosition blastOrigin,
            GameplayPosition subjectPosition) => BlocksLineOfSight(
                state,
                blastOrigin,
                subjectPosition)
                    ? 0f
                    : 1f;

        /// <summary>
        /// Returns the total length of a proposed segment that crosses active
        /// authoritative fire. Fire is a cost/hazard, not a solid obstacle;
        /// callers can therefore value or prune the route without pretending
        /// it is impassable.
        /// </summary>
        public float EvaluateFireHazardTraversal(
            GameplayCombatStateSnapshot state,
            GameplayPosition origin,
            GameplayPosition destination)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.RequireCoverage(GameplayCombatStateCoverage.FireFields);
            float total = 0f;
            foreach (FireFieldSnapshot fire in state.FireFields)
                total += GameplayFireFieldSession.CalculateHazardTraversal(
                    origin,
                    destination,
                    fire);
            return total;
        }

        public GameplayEvidenceRecord CaptureEvidence(
            string queryKind,
            GameplayCombatStateSnapshot state,
            GameplayPosition origin,
            GameplayPosition destination,
            float clearanceRadius = 0f)
        {
            string kind = GameplayContentIdentity.RequireText(
                queryKind,
                nameof(queryKind));
            GameplaySpatialEvidenceStamp stamp = Stamp(state);
            var text = new StringBuilder()
                .Append(spatialIdentity.StaticSpatialDigest).Append('|')
                .Append(spatialIdentity.EvidenceAlgorithmVersion).Append('|')
                .Append(stamp.DynamicSpatialDigest).Append('|')
                .Append(kind).Append('|')
                .Append(Format(origin)).Append('|')
                .Append(Format(destination)).Append('|')
                .Append(GameplayNumericPolicy.FormatCanonical(clearanceRadius));
            return new GameplayEvidenceRecord(
                "spatial." + kind,
                state.Session.Revision,
                Hash(text.ToString()));
        }

        private bool BlocksSegment(
            GameplayCombatStateSnapshot state,
            GameplayPosition origin,
            GameplayPosition destination,
            float expansion)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (hasDestructibleObstacles)
                state.RequireCoverage(GameplayCombatStateCoverage.Destructibles);
            foreach (ObstacleDefinition obstacle in obstacles)
            {
                if (!obstacle.IsDestructible)
                {
                    foreach (CoverVolumeData volume in obstacle.Volumes)
                        if (Intersects(
                            origin,
                            destination,
                            obstacle.StaticPose,
                            volume,
                            expansion))
                            return true;
                    continue;
                }
                DestructiblePropSnapshot prop = FindProp(
                    state.Destructibles,
                    obstacle.EntityId);
                if (prop.State == DestructiblePropState.Destroyed) continue;
                if (prop.DetachedFractureChunks != 0UL)
                {
                    GameplayFractureSpatialProfile profile =
                        RequireFractureProfile(obstacle, prop);
                    for (int index = 0; index < profile.ChunkCount; index++)
                    {
                        if ((prop.DetachedFractureChunks & (1UL << index)) != 0UL)
                            continue;
                        if (Intersects(
                                origin,
                                destination,
                                prop.Pose,
                                profile.ChunkVolumes[index],
                                expansion))
                            return true;
                    }
                    continue;
                }
                foreach (CoverVolumeData volume in obstacle.Volumes)
                {
                    if (Intersects(
                            origin,
                            destination,
                            prop.Pose,
                            volume,
                            expansion))
                        return true;
                }
            }
            return false;
        }

        private bool TryResolveSurfaceHeight(
            GameplayPosition position,
            float maximumHeight,
            float minimumHeight,
            out float resolvedHeight)
        {
            bool found = false;
            resolvedHeight = float.NegativeInfinity;
            foreach (TerrainSurfaceData terrain in terrainSurfaces)
                if (TryEvaluateTerrainHeight(
                        terrain,
                        position,
                        out float height)
                    && height <= maximumHeight + 0.0001f
                    && height >= minimumHeight - 0.0001f
                    && (!found || height > resolvedHeight))
                {
                    resolvedHeight = height;
                    found = true;
                }
            foreach (PlacementSurfaceDefinition surface in placementSurfaces)
                if (TryEvaluatePlacementHeight(
                        surface,
                        position,
                        out float height)
                    && height <= maximumHeight + 0.0001f
                    && height >= minimumHeight - 0.0001f
                    && (!found || height > resolvedHeight))
                {
                    resolvedHeight = height;
                    found = true;
                }
            return found;
        }

        private static bool TryEvaluateTerrainHeight(
            TerrainSurfaceData surface,
            GameplayPosition position,
            out float height)
        {
            height = 0f;
            if (surface.sampleCountX <= 0
                || surface.sampleCountZ <= 0
                || surface.sampleSpacing <= 0f
                || surface.heightSamples == null
                || surface.heightSamples.Count
                    != surface.sampleCountX * surface.sampleCountZ)
                return false;
            float sampleX = (position.X - surface.origin.x)
                / surface.sampleSpacing;
            float sampleZ = (position.Z - surface.origin.z)
                / surface.sampleSpacing;
            if (sampleX < 0f
                || sampleZ < 0f
                || sampleX > surface.sampleCountX - 1
                || sampleZ > surface.sampleCountZ - 1)
                return false;
            int lowerX = Math.Min(
                (int)Math.Floor(sampleX),
                surface.sampleCountX - 1);
            int lowerZ = Math.Min(
                (int)Math.Floor(sampleZ),
                surface.sampleCountZ - 1);
            int upperX = Math.Min(lowerX + 1, surface.sampleCountX - 1);
            int upperZ = Math.Min(lowerZ + 1, surface.sampleCountZ - 1);
            float fractionX = sampleX - lowerX;
            float fractionZ = sampleZ - lowerZ;
            float lower = Lerp(
                TerrainHeight(surface, lowerX, lowerZ),
                TerrainHeight(surface, upperX, lowerZ),
                fractionX);
            float upper = Lerp(
                TerrainHeight(surface, lowerX, upperZ),
                TerrainHeight(surface, upperX, upperZ),
                fractionX);
            height = Lerp(lower, upper, fractionZ);
            return true;
        }

        private static bool TryEvaluatePlacementHeight(
            PlacementSurfaceDefinition definition,
            GameplayPosition position,
            out float height)
        {
            height = 0f;
            LevelPlacementSurfaceData surface = definition.Surface;
            GameplayPosition local = ToPropLocal(position, definition.Pose);
            float halfX = surface.size.x * 0.5f;
            float halfZ = surface.size.z * 0.5f;
            if (halfX <= 0f
                || halfZ <= 0f
                || local.X < surface.localCenter.x - halfX - 0.0001f
                || local.X > surface.localCenter.x + halfX + 0.0001f
                || local.Z < surface.localCenter.z - halfZ - 0.0001f
                || local.Z > surface.localCenter.z + halfZ + 0.0001f)
                return false;
            float fraction = (local.Z - (surface.localCenter.z - halfZ))
                / (halfZ * 2f);
            float localHeight;
            switch (surface.kind)
            {
                case LevelPlacementSurfaceData.FlatKind:
                    if (!GameplayNumericPolicy.AreEquivalent(
                        surface.negativeZHeight,
                        surface.positiveZHeight))
                        throw new InvalidOperationException(
                            $"Flat placement surface '{definition.EntityId}' has mismatched endpoint heights.");
                    localHeight = surface.negativeZHeight;
                    break;
                case LevelPlacementSurfaceData.RampZKind:
                    localHeight = Lerp(
                        surface.negativeZHeight,
                        surface.positiveZHeight,
                        fraction);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Placement surface '{definition.EntityId}' uses unsupported kind '{surface.kind}'.");
            }
            GameplayPosition world = ToWorld(
                new GameplayPosition(local.X, localHeight, local.Z),
                definition.Pose);
            height = world.Y;
            return true;
        }

        private static float TerrainHeight(
            TerrainSurfaceData surface,
            int x,
            int z) => surface.origin.y
                + surface.minimumElevation
                + surface.heightSamples[(z * surface.sampleCountX) + x]
                    * surface.elevationIncrement;

        private static float Lerp(float from, float to, float fraction) =>
            from + ((to - from) * fraction);

        private static GameplayFractureSpatialProfile ResolveFractureProfile(
            string archetypeId,
            IReadOnlyDictionary<string, GameplayFractureSpatialProfile>
                profiles)
        {
            if (profiles == null) return null;
            string id = archetypeId ?? string.Empty;
            return profiles.TryGetValue(id, out GameplayFractureSpatialProfile profile)
                ? profile ?? throw new ArgumentException(
                    $"Fracture spatial profile '{id}' cannot be null.",
                    nameof(profiles))
                : null;
        }

        private static GameplayFractureSpatialProfile RequireFractureProfile(
            ObstacleDefinition obstacle,
            DestructiblePropSnapshot prop)
        {
            GameplayFractureSpatialProfile profile = obstacle.FractureProfile;
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Spatial destructible '{prop.PropId}' has detached fracture "
                    + "chunks but no registered fracture spatial profile.");
            }
            if (profile.ChunkCount != prop.FractureChunkCount)
            {
                throw new InvalidOperationException(
                    $"Spatial destructible '{prop.PropId}' declares "
                    + $"{prop.FractureChunkCount} fracture chunks but profile "
                    + $"'{profile.ProfileId}' contains {profile.ChunkCount}.");
            }
            return profile;
        }

        private static DestructiblePropSnapshot FindProp(
            IReadOnlyList<DestructiblePropSnapshot> props,
            string propId)
        {
            foreach (DestructiblePropSnapshot prop in props)
                if (string.Equals(prop.PropId, propId, StringComparison.Ordinal))
                    return prop;
            throw new InvalidOperationException(
                $"Spatial destructible '{propId}' is absent from canonical state.");
        }

        private static bool Intersects(
            GameplayPosition origin,
            GameplayPosition destination,
            GameplayPropPose pose,
            CoverVolumeData volume,
            float expansion) => Intersects(
                origin,
                destination,
                pose,
                new GameplayPosition(
                    volume.localCenter.x,
                    volume.localCenter.y,
                    volume.localCenter.z),
                new GameplayPosition(
                    volume.size.x,
                    volume.size.y,
                    volume.size.z),
                expansion);

        private static bool Intersects(
            GameplayPosition origin,
            GameplayPosition destination,
            GameplayPropPose pose,
            GameplayLocalSpatialVolume volume,
            float expansion) => Intersects(
                origin,
                destination,
                pose,
                volume.Center,
                volume.Size,
                expansion);

        private static bool Intersects(
            GameplayPosition origin,
            GameplayPosition destination,
            GameplayPropPose pose,
            GameplayPosition localCenter,
            GameplayPosition size,
            float expansion)
        {
            GameplayPosition localOrigin = ToPropLocal(origin, pose);
            GameplayPosition localDestination = ToPropLocal(
                destination,
                pose);
            float halfX = (size.X * 0.5f) + expansion;
            float halfY = (size.Y * 0.5f) + expansion;
            float halfZ = (size.Z * 0.5f) + expansion;
            float minimum = 0f;
            float maximum = 1f;
            return IntersectsAxis(
                    localOrigin.X,
                    localDestination.X - localOrigin.X,
                    localCenter.X - halfX,
                    localCenter.X + halfX,
                    ref minimum,
                    ref maximum)
                && IntersectsAxis(
                    localOrigin.Y,
                    localDestination.Y - localOrigin.Y,
                    localCenter.Y - halfY,
                    localCenter.Y + halfY,
                    ref minimum,
                    ref maximum)
                && IntersectsAxis(
                    localOrigin.Z,
                    localDestination.Z - localOrigin.Z,
                    localCenter.Z - halfZ,
                    localCenter.Z + halfZ,
                    ref minimum,
                    ref maximum);
        }

        private static GameplayPosition ToPropLocal(
            GameplayPosition world,
            GameplayPropPose pose)
        {
            double x = world.X - pose.Position.X;
            double y = world.Y - pose.Position.Y;
            double z = world.Z - pose.Position.Z;

            RotateY(ref x, ref z, -pose.YawDegrees);
            RotateX(ref y, ref z, -pose.PitchDegrees);
            RotateZ(ref x, ref y, -pose.RollDegrees);
            return new GameplayPosition((float)x, (float)y, (float)z);
        }

        private static GameplayPosition ToWorld(
            GameplayPosition local,
            GameplayPropPose pose)
        {
            double x = local.X;
            double y = local.Y;
            double z = local.Z;
            RotateZ(ref x, ref y, pose.RollDegrees);
            RotateX(ref y, ref z, pose.PitchDegrees);
            RotateY(ref x, ref z, pose.YawDegrees);
            return new GameplayPosition(
                (float)(x + pose.Position.X),
                (float)(y + pose.Position.Y),
                (float)(z + pose.Position.Z));
        }

        private static void RotateX(
            ref double y,
            ref double z,
            float degrees)
        {
            double radians = degrees * (Math.PI / 180d);
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            double rotatedY = (cosine * y) - (sine * z);
            z = (sine * y) + (cosine * z);
            y = rotatedY;
        }

        private static void RotateY(
            ref double x,
            ref double z,
            float degrees)
        {
            double radians = degrees * (Math.PI / 180d);
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            double rotatedX = (cosine * x) + (sine * z);
            z = (-sine * x) + (cosine * z);
            x = rotatedX;
        }

        private static void RotateZ(
            ref double x,
            ref double y,
            float degrees)
        {
            double radians = degrees * (Math.PI / 180d);
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            double rotatedX = (cosine * x) - (sine * y);
            y = (sine * x) + (cosine * y);
            x = rotatedX;
        }

        private static bool IntersectsAxis(
            float start,
            float delta,
            float lower,
            float upper,
            ref float minimum,
            ref float maximum)
        {
            if (Math.Abs(delta) <= 0.000001f)
                return start >= lower && start <= upper;
            float first = (lower - start) / delta;
            float second = (upper - start) / delta;
            if (first > second)
            {
                float swap = first;
                first = second;
                second = swap;
            }
            minimum = Math.Max(minimum, first);
            maximum = Math.Min(maximum, second);
            return maximum >= minimum;
        }

        private static string Format(GameplayPosition value) =>
            GameplayNumericPolicy.FormatCanonical(value.X) + ","
            + GameplayNumericPolicy.FormatCanonical(value.Y) + ","
            + GameplayNumericPolicy.FormatCanonical(value.Z);

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var result = new StringBuilder(digest.Length * 2);
                foreach (byte item in digest) result.Append(item.ToString("x2"));
                return result.ToString();
            }
        }

    }
}
