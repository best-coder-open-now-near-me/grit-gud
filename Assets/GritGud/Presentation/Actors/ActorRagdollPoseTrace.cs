using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Actors
{
    internal readonly struct QuantizedActorRagdollBonePose
    {
        private const float PositionScale = 1000f;
        private const float RotationScale = short.MaxValue;

        public QuantizedActorRagdollBonePose(
            Vector3 rootLocalPosition,
            Quaternion rootLocalRotation)
        {
            PositionX = Quantize(rootLocalPosition.x, PositionScale);
            PositionY = Quantize(rootLocalPosition.y, PositionScale);
            PositionZ = Quantize(rootLocalPosition.z, PositionScale);
            Quaternion normalized = Normalize(rootLocalRotation);
            RotationX = Quantize(normalized.x, RotationScale);
            RotationY = Quantize(normalized.y, RotationScale);
            RotationZ = Quantize(normalized.z, RotationScale);
            RotationW = Quantize(normalized.w, RotationScale);
        }

        public short PositionX { get; }
        public short PositionY { get; }
        public short PositionZ { get; }
        public short RotationX { get; }
        public short RotationY { get; }
        public short RotationZ { get; }
        public short RotationW { get; }

        public Vector3 DecodePosition() => new(
            PositionX / PositionScale,
            PositionY / PositionScale,
            PositionZ / PositionScale);

        public Quaternion DecodeRotation() => Normalize(new Quaternion(
            RotationX / RotationScale,
            RotationY / RotationScale,
            RotationZ / RotationScale,
            RotationW / RotationScale));

        private static short Quantize(float value, float scale) =>
            (short)Mathf.Clamp(
                Mathf.RoundToInt(value * scale),
                short.MinValue,
                short.MaxValue);

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                (value.x * value.x) +
                (value.y * value.y) +
                (value.z * value.z) +
                (value.w * value.w));
            if (magnitude <= 0.000001f)
                return Quaternion.identity;
            float inverse = 1f / magnitude;
            return new Quaternion(
                value.x * inverse,
                value.y * inverse,
                value.z * inverse,
                value.w * inverse);
        }
    }

    internal sealed class ActorRagdollPoseTrace
    {
        private sealed class Sample
        {
            public Sample(
                ushort milliseconds,
                QuantizedActorRagdollBonePose[] bones)
            {
                Milliseconds = milliseconds;
                Bones = bones;
            }

            public ushort Milliseconds { get; }
            public QuantizedActorRagdollBonePose[] Bones { get; }
        }

        private readonly List<Sample> samples = new();

        public ActorRagdollPoseTrace(
            long journalSequence,
            string schemaId,
            int schemaVersion,
            int boneCount,
            float handoffEventNormalizedTime)
        {
            if (journalSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(journalSequence));
            if (string.IsNullOrWhiteSpace(schemaId))
                throw new ArgumentException(
                    "Ragdoll traces require a schema identifier.",
                    nameof(schemaId));
            if (schemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (boneCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(boneCount));
            if (!IsFinite(handoffEventNormalizedTime))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(handoffEventNormalizedTime));
            }

            JournalSequence = journalSequence;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            BoneCount = boneCount;
            HandoffEventNormalizedTime = Mathf.Clamp01(
                handoffEventNormalizedTime);
        }

        public long JournalSequence { get; }

        public string SchemaId { get; }

        public int SchemaVersion { get; }

        public int BoneCount { get; }

        public float HandoffEventNormalizedTime { get; }

        public int SampleCount => samples.Count;

        public bool IsComplete { get; private set; }

        public float DurationSeconds => samples.Count == 0
            ? 0f
            : samples[samples.Count - 1].Milliseconds / 1000f;

        public void AddSample(
            float elapsedSeconds,
            Transform actorRoot,
            IReadOnlyList<Transform> bones)
        {
            if (actorRoot == null)
                throw new ArgumentNullException(nameof(actorRoot));
            if (bones == null || bones.Count != BoneCount)
                throw new ArgumentException(
                    "Ragdoll trace samples must match the trace bone schema.",
                    nameof(bones));
            var positions = new Vector3[BoneCount];
            var rotations = new Quaternion[BoneCount];
            Quaternion inverseRoot = Quaternion.Inverse(actorRoot.rotation);
            for (int index = 0; index < BoneCount; index++)
            {
                Transform bone = bones[index] ?? throw new ArgumentException(
                    "Ragdoll trace bones cannot contain null.",
                    nameof(bones));
                positions[index] = actorRoot.InverseTransformPoint(
                    bone.position);
                rotations[index] = inverseRoot * bone.rotation;
            }
            AddSample(elapsedSeconds, positions, rotations);
        }

        internal void AddSample(
            float elapsedSeconds,
            IReadOnlyList<Vector3> rootLocalPositions,
            IReadOnlyList<Quaternion> rootLocalRotations)
        {
            if (IsComplete)
                throw new InvalidOperationException(
                    "Completed ragdoll traces cannot accept new samples.");
            if (!IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (rootLocalPositions == null ||
                rootLocalRotations == null ||
                rootLocalPositions.Count != BoneCount ||
                rootLocalRotations.Count != BoneCount)
            {
                throw new ArgumentException(
                    "Ragdoll trace samples must match the trace bone schema.");
            }

            ushort milliseconds = (ushort)Mathf.Clamp(
                Mathf.RoundToInt(elapsedSeconds * 1000f),
                0,
                ushort.MaxValue);
            if (samples.Count > 0 &&
                milliseconds < samples[samples.Count - 1].Milliseconds)
            {
                throw new ArgumentException(
                    "Ragdoll trace samples must be chronological.",
                    nameof(elapsedSeconds));
            }

            var poses = new QuantizedActorRagdollBonePose[BoneCount];
            for (int index = 0; index < BoneCount; index++)
            {
                poses[index] = new QuantizedActorRagdollBonePose(
                    rootLocalPositions[index],
                    rootLocalRotations[index]);
            }
            var sample = new Sample(milliseconds, poses);
            if (samples.Count > 0 &&
                milliseconds == samples[samples.Count - 1].Milliseconds)
            {
                samples[samples.Count - 1] = sample;
            }
            else
            {
                samples.Add(sample);
            }
        }

        public void Complete()
        {
            if (samples.Count == 0)
                throw new InvalidOperationException(
                    "A ragdoll trace requires at least one sample.");
            IsComplete = true;
        }

        public void SampleAt(
            float elapsedSeconds,
            Vector3[] rootLocalPositions,
            Quaternion[] rootLocalRotations)
        {
            if (samples.Count == 0)
                throw new InvalidOperationException(
                    "A ragdoll trace must contain a sample before playback.");
            if (!IsFinite(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (rootLocalPositions == null ||
                rootLocalRotations == null ||
                rootLocalPositions.Length != BoneCount ||
                rootLocalRotations.Length != BoneCount)
            {
                throw new ArgumentException(
                    "Ragdoll playback buffers must match the trace schema.");
            }

            float milliseconds = Mathf.Clamp(
                elapsedSeconds * 1000f,
                0f,
                samples[samples.Count - 1].Milliseconds);
            int upperIndex = 0;
            while (upperIndex < samples.Count - 1 &&
                samples[upperIndex].Milliseconds < milliseconds)
            {
                upperIndex++;
            }
            int lowerIndex = Mathf.Max(0, upperIndex - 1);
            Sample lower = samples[lowerIndex];
            Sample upper = samples[upperIndex];
            float blend = upper.Milliseconds == lower.Milliseconds
                ? 0f
                : Mathf.InverseLerp(
                    lower.Milliseconds,
                    upper.Milliseconds,
                    milliseconds);
            for (int index = 0; index < BoneCount; index++)
            {
                rootLocalPositions[index] = Vector3.LerpUnclamped(
                    lower.Bones[index].DecodePosition(),
                    upper.Bones[index].DecodePosition(),
                    blend);
                rootLocalRotations[index] = Quaternion.SlerpUnclamped(
                    lower.Bones[index].DecodeRotation(),
                    upper.Bones[index].DecodeRotation(),
                    blend);
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
