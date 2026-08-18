using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
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
            foreach (SmokeFieldSnapshot smoke in state.SmokeFields)
            {
                Append(text, "smoke", smoke.Field.Id);
                Append(text, "origin", smoke.Field.Origin);
                Append(text, "remaining", smoke.RemainingFraction);
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
                string propId,
                IReadOnlyList<CoverVolumeData> volumes)
            {
                PropId = propId;
                Volumes = volumes;
            }

            public string PropId { get; }
            public IReadOnlyList<CoverVolumeData> Volumes { get; }
        }

        private readonly SpatialContentIdentity spatialIdentity;
        private readonly IReadOnlyList<ObstacleDefinition> obstacles;

        public GameplayHeadlessSpatialEvidence(
            LevelDocument level,
            SpatialContentIdentity identity)
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
            foreach (LevelEntity entity in level.entities)
            {
                if (entity?.destructible?.enabled != true
                    || entity.coverVolumes.Count == 0)
                    continue;
                result.Add(new ObstacleDefinition(
                    entity.id,
                    new List<CoverVolumeData>(
                        entity.coverVolumes).AsReadOnly()));
            }
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.PropId,
                right.PropId));
            obstacles = result.AsReadOnly();
        }

        public GameplaySpatialEvidenceStamp Stamp(
            GameplayCombatStateSnapshot state) => new GameplaySpatialEvidenceStamp(
                spatialIdentity,
                GameplayDynamicSpatialFingerprint.Hash(state),
                state?.Session.Revision
                    ?? throw new ArgumentNullException(nameof(state)));

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
            state.RequireCoverage(GameplayCombatStateCoverage.Destructibles);
            foreach (ObstacleDefinition obstacle in obstacles)
            {
                DestructiblePropSnapshot prop = FindProp(
                    state.Destructibles,
                    obstacle.PropId);
                if (prop.State == DestructiblePropState.Destroyed) continue;
                foreach (CoverVolumeData volume in obstacle.Volumes)
                {
                    Bounds bounds = CreateBounds(prop.Pose, volume, expansion);
                    if (Intersects(origin, destination, bounds)) return true;
                }
            }
            return false;
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

        private static Bounds CreateBounds(
            GameplayPropPose pose,
            CoverVolumeData volume,
            float expansion)
        {
            double radians = pose.YawDegrees * (Math.PI / 180d);
            float cosine = (float)Math.Cos(radians);
            float sine = (float)Math.Sin(radians);
            float localX = volume.localCenter.x;
            float localZ = volume.localCenter.z;
            var center = new GameplayPosition(
                pose.Position.X + (localX * cosine) + (localZ * sine),
                pose.Position.Y + volume.localCenter.y,
                pose.Position.Z - (localX * sine) + (localZ * cosine));
            float halfX = volume.size.x * 0.5f;
            float halfZ = volume.size.z * 0.5f;
            float worldHalfX = Math.Abs(halfX * cosine)
                + Math.Abs(halfZ * sine);
            float worldHalfZ = Math.Abs(halfX * sine)
                + Math.Abs(halfZ * cosine);
            return new Bounds(
                center,
                worldHalfX + expansion,
                (volume.size.y * 0.5f) + expansion,
                worldHalfZ + expansion);
        }

        private static bool Intersects(
            GameplayPosition origin,
            GameplayPosition destination,
            Bounds bounds)
        {
            float minimum = 0f;
            float maximum = 1f;
            return IntersectsAxis(
                    origin.X,
                    destination.X - origin.X,
                    bounds.Center.X - bounds.HalfX,
                    bounds.Center.X + bounds.HalfX,
                    ref minimum,
                    ref maximum)
                && IntersectsAxis(
                    origin.Y,
                    destination.Y - origin.Y,
                    bounds.Center.Y - bounds.HalfY,
                    bounds.Center.Y + bounds.HalfY,
                    ref minimum,
                    ref maximum)
                && IntersectsAxis(
                    origin.Z,
                    destination.Z - origin.Z,
                    bounds.Center.Z - bounds.HalfZ,
                    bounds.Center.Z + bounds.HalfZ,
                    ref minimum,
                    ref maximum);
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

        private readonly struct Bounds
        {
            public Bounds(
                GameplayPosition center,
                float halfX,
                float halfY,
                float halfZ)
            {
                Center = center;
                HalfX = halfX;
                HalfY = halfY;
                HalfZ = halfZ;
            }

            public GameplayPosition Center { get; }
            public float HalfX { get; }
            public float HalfY { get; }
            public float HalfZ { get; }
        }
    }
}
