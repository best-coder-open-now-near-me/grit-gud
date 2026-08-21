using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public enum ActorTargetProfileKind
    {
        Standing = 0,
        Crouched = 1,
        PinnedDown = 2,
    }

    public enum ActorTargetVolumeAxis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    public readonly struct ActorLocalPoint
    {
        public ActorLocalPoint(float x, float y, float z)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
                throw new ArgumentException(
                    "Actor-local points must contain only finite values.");
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct ActorTargetAcquisitionVolume
    {
        public ActorTargetAcquisitionVolume(
            ActorLocalPoint localCenter,
            float radius,
            float height,
            ActorTargetVolumeAxis axis)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (float.IsNaN(height) || float.IsInfinity(height)
                || height < radius * 2f)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }
            if (!Enum.IsDefined(typeof(ActorTargetVolumeAxis), axis))
                throw new ArgumentOutOfRangeException(nameof(axis));

            LocalCenter = localCenter;
            Radius = radius;
            Height = height;
            Axis = axis;
        }

        public ActorLocalPoint LocalCenter { get; }

        public float Radius { get; }

        public float Height { get; }

        public ActorTargetVolumeAxis Axis { get; }
    }

    public readonly struct ActorTargetRegionProfile
    {
        public ActorTargetRegionProfile(
            TargetRegionId id,
            ActorLocalPoint localCenter,
            float radius)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), id))
                throw new ArgumentOutOfRangeException(nameof(id));
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));

            Id = id;
            LocalCenter = localCenter;
            Radius = radius;
        }

        public TargetRegionId Id { get; }

        public ActorLocalPoint LocalCenter { get; }

        public float Radius { get; }
    }

    public sealed class ActorTargetProfile
    {
        private readonly IReadOnlyList<ActorTargetRegionProfile> regions;

        public ActorTargetProfile(
            ActorTargetProfileKind kind,
            ActorTargetAcquisitionVolume acquisitionVolume,
            IEnumerable<ActorTargetRegionProfile> targetRegions)
        {
            if (!Enum.IsDefined(typeof(ActorTargetProfileKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (targetRegions == null)
                throw new ArgumentNullException(nameof(targetRegions));

            var copied = new List<ActorTargetRegionProfile>();
            var ids = new HashSet<TargetRegionId>();
            foreach (ActorTargetRegionProfile region in targetRegions)
            {
                if (!ids.Add(region.Id))
                    throw new ArgumentException(
                        $"Actor target profile '{kind}' repeats region '{region.Id}'.",
                        nameof(targetRegions));
                copied.Add(region);
            }
            if (copied.Count == 0)
                throw new ArgumentException(
                    "Actor target profiles require target regions.",
                    nameof(targetRegions));

            Kind = kind;
            AcquisitionVolume = acquisitionVolume;
            regions = copied.AsReadOnly();
        }

        public ActorTargetProfileKind Kind { get; }

        public ActorTargetAcquisitionVolume AcquisitionVolume { get; }

        public IReadOnlyList<ActorTargetRegionProfile> Regions => regions;

        public ActorTargetRegionProfile GetRegion(TargetRegionId id)
        {
            foreach (ActorTargetRegionProfile region in regions)
            {
                if (region.Id == id)
                    return region;
            }
            throw new InvalidOperationException(
                $"Actor target profile '{Kind}' does not define region '{id}'.");
        }
    }

    /// <summary>
    /// The portable actor silhouette contract shared by player acquisition,
    /// live and headless exposure, and pin-pose presentation. Movement
    /// collision is deliberately not part of this contract.
    /// </summary>
    public static class ActorTargetProfileCatalog
    {
        private static readonly ActorTargetProfile Standing = CreateStanding();
        private static readonly ActorTargetProfile Crouched = CreateCrouched();
        private static readonly ActorTargetProfile PinnedDown = CreatePinnedDown();

        public static ActorTargetProfile Resolve(
            ActorStance stance,
            bool pinned)
        {
            if (!Enum.IsDefined(typeof(ActorStance), stance))
                throw new ArgumentOutOfRangeException(nameof(stance));
            if (pinned)
                return PinnedDown;
            return stance == ActorStance.Crouched ? Crouched : Standing;
        }

        public static IReadOnlyList<TargetRegionSample> CreateWorldSamples(
            GameplayActorPose pose,
            bool pinned)
        {
            ActorTargetProfile profile = Resolve(pose.Stance, pinned);
            var samples = new List<TargetRegionSample>(profile.Regions.Count);
            foreach (ActorTargetRegionProfile region in profile.Regions)
            {
                samples.Add(new TargetRegionSample(
                    region.Id,
                    TransformPoint(
                        pose.Position,
                        pose.FacingDegrees,
                        region.LocalCenter),
                    region.Radius));
            }
            return samples.AsReadOnly();
        }

        public static GameplayPosition TransformPoint(
            GameplayPosition origin,
            float facingDegrees,
            ActorLocalPoint localPoint)
        {
            if (float.IsNaN(facingDegrees) || float.IsInfinity(facingDegrees))
                throw new ArgumentOutOfRangeException(nameof(facingDegrees));
            double radians = facingDegrees * (Math.PI / 180d);
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            return new GameplayPosition(
                origin.X + (float)((localPoint.X * cosine)
                    + (localPoint.Z * sine)),
                origin.Y + localPoint.Y,
                origin.Z + (float)((-localPoint.X * sine)
                    + (localPoint.Z * cosine)));
        }

        private static ActorTargetProfile CreateStanding() => new(
            ActorTargetProfileKind.Standing,
            new ActorTargetAcquisitionVolume(
                new ActorLocalPoint(0f, 0.9f, 0f),
                radius: 0.35f,
                height: 1.8f,
                ActorTargetVolumeAxis.Y),
            new[]
            {
                Region(TargetRegionId.Head, 0f, 1.62f, 0f, 0.14f),
                Region(TargetRegionId.Torso, 0f, 1.18f, 0f, 0.2f),
                Region(TargetRegionId.LeftArm, -0.32f, 1.2f, 0f, 0.12f),
                Region(TargetRegionId.RightArm, 0.32f, 1.2f, 0f, 0.12f),
                Region(TargetRegionId.LeftLeg, -0.16f, 0.55f, 0f, 0.15f),
                Region(TargetRegionId.RightLeg, 0.16f, 0.55f, 0f, 0.15f),
            });

        private static ActorTargetProfile CreateCrouched() => new(
            ActorTargetProfileKind.Crouched,
            new ActorTargetAcquisitionVolume(
                new ActorLocalPoint(0f, 0.56f, 0f),
                radius: 0.35f,
                height: 1.12f,
                ActorTargetVolumeAxis.Y),
            new[]
            {
                Region(TargetRegionId.Head, 0f, 1.08f, 0f, 0.14f),
                Region(TargetRegionId.Torso, 0f, 0.78f, 0f, 0.2f),
                Region(TargetRegionId.LeftArm, -0.3f, 0.8f, 0f, 0.12f),
                Region(TargetRegionId.RightArm, 0.3f, 0.8f, 0f, 0.12f),
                Region(TargetRegionId.LeftLeg, -0.18f, 0.38f, 0f, 0.15f),
                Region(TargetRegionId.RightLeg, 0.18f, 0.38f, 0f, 0.15f),
            });

        private static ActorTargetProfile CreatePinnedDown() => new(
            ActorTargetProfileKind.PinnedDown,
            new ActorTargetAcquisitionVolume(
                new ActorLocalPoint(0f, 0.3f, -0.78f),
                radius: 0.32f,
                height: 1.8f,
                ActorTargetVolumeAxis.Z),
            new[]
            {
                Region(TargetRegionId.Head, 0f, 0.3f, -1.52f, 0.14f),
                Region(TargetRegionId.Torso, 0f, 0.3f, -0.92f, 0.2f),
                Region(TargetRegionId.LeftArm, -0.32f, 0.3f, -0.92f, 0.12f),
                Region(TargetRegionId.RightArm, 0.32f, 0.3f, -0.92f, 0.12f),
                Region(TargetRegionId.LeftLeg, -0.16f, 0.24f, -0.28f, 0.15f),
                Region(TargetRegionId.RightLeg, 0.16f, 0.24f, -0.28f, 0.15f),
            });

        private static ActorTargetRegionProfile Region(
            TargetRegionId id,
            float x,
            float y,
            float z,
            float radius) => new(
                id,
                new ActorLocalPoint(x, y, z),
                radius);
    }
}
