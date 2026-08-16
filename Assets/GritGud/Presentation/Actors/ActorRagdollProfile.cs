using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Actors
{
    public enum ActorRagdollColliderShape
    {
        Capsule = 0,
        Sphere = 1,
    }

    [Serializable]
    public sealed class ActorRagdollBoneDefinition
    {
        [SerializeField]
        private HumanBodyBones bone = HumanBodyBones.Hips;

        [SerializeField]
        private HumanBodyBones connectedBone = HumanBodyBones.LastBone;

        [SerializeField]
        private HumanBodyBones endBone = HumanBodyBones.Spine;

        [SerializeField]
        private ActorRagdollColliderShape colliderShape =
            ActorRagdollColliderShape.Capsule;

        [SerializeField, Min(0.001f)]
        private float massFraction = 0.1f;

        [SerializeField, Min(0.01f)]
        private float radiusScale = 0.28f;

        [SerializeField, Min(0.1f)]
        private float lengthScale = 0.9f;

        [SerializeField, Range(-90f, 0f)]
        private float lowTwistDegrees = -25f;

        [SerializeField, Range(0f, 90f)]
        private float highTwistDegrees = 25f;

        [SerializeField, Range(0f, 90f)]
        private float swingOneDegrees = 35f;

        [SerializeField, Range(0f, 90f)]
        private float swingTwoDegrees = 35f;

        public HumanBodyBones Bone => bone;

        public HumanBodyBones ConnectedBone => connectedBone;

        public HumanBodyBones EndBone => endBone;

        public ActorRagdollColliderShape ColliderShape => colliderShape;

        public float MassFraction => Mathf.Max(0.001f, massFraction);

        public float RadiusScale => Mathf.Max(0.01f, radiusScale);

        public float LengthScale => Mathf.Max(0.1f, lengthScale);

        public float LowTwistDegrees => Mathf.Clamp(
            lowTwistDegrees,
            -90f,
            0f);

        public float HighTwistDegrees => Mathf.Clamp(
            highTwistDegrees,
            0f,
            90f);

        public float SwingOneDegrees => Mathf.Clamp(
            swingOneDegrees,
            0f,
            90f);

        public float SwingTwoDegrees => Mathf.Clamp(
            swingTwoDegrees,
            0f,
            90f);
    }

    [CreateAssetMenu(
        fileName = "ActorRagdollProfile",
        menuName = "Grit Gud/Actors/Ragdoll Profile")]
    public sealed class ActorRagdollProfile : ScriptableObject
    {
        private const float MinimumPositiveValue = 0.001f;

        [SerializeField]
        private string traceSchemaId = "default-humanoid-v1";

        [SerializeField, Min(1)]
        private int traceSchemaVersion = 1;

        [SerializeField]
        private List<ActorRagdollBoneDefinition> bones = new();

        [SerializeField, Min(1f)]
        private float totalMass = 72f;

        [SerializeField, Range(0f, 1f)]
        private float handoffNormalizedTime = 0.72f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float sampleIntervalSeconds = 0.05f;

        [SerializeField, Min(0f)]
        private float minimumActiveSeconds = 0.45f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float settleHoldSeconds = 0.35f;

        [SerializeField, Min(MinimumPositiveValue)]
        private float maximumActiveSeconds = 2.25f;

        [SerializeField, Min(0f)]
        private float settleLinearSpeed = 0.12f;

        [SerializeField, Min(0f)]
        private float settleAngularSpeed = 0.3f;

        [SerializeField, Min(0f)]
        private float maximumImpulseSpeed = 2.4f;

        [SerializeField, Range(0f, 1f)]
        private float upwardImpulseFraction = 0.22f;

        [SerializeField, Min(1)]
        private int maximumStoredTraces = 4;

        [SerializeField, Min(0f)]
        private float linearDamping = 0.08f;

        [SerializeField, Min(0f)]
        private float angularDamping = 0.12f;

        public string TraceSchemaId => traceSchemaId ?? string.Empty;

        public int TraceSchemaVersion => Mathf.Max(1, traceSchemaVersion);

        public IReadOnlyList<ActorRagdollBoneDefinition> Bones => bones;

        public float TotalMass => Mathf.Max(1f, totalMass);

        public float HandoffNormalizedTime => Mathf.Clamp01(
            handoffNormalizedTime);

        public float SampleIntervalSeconds => Mathf.Max(
            MinimumPositiveValue,
            sampleIntervalSeconds);

        public float MinimumActiveSeconds => Mathf.Max(
            0f,
            minimumActiveSeconds);

        public float SettleHoldSeconds => Mathf.Max(
            MinimumPositiveValue,
            settleHoldSeconds);

        public float MaximumActiveSeconds => Mathf.Max(
            MinimumActiveSeconds + MinimumPositiveValue,
            maximumActiveSeconds);

        public float SettleLinearSpeed => Mathf.Max(0f, settleLinearSpeed);

        public float SettleAngularSpeed => Mathf.Max(0f, settleAngularSpeed);

        public float MaximumImpulseSpeed => Mathf.Max(
            0f,
            maximumImpulseSpeed);

        public float UpwardImpulseFraction => Mathf.Clamp01(
            upwardImpulseFraction);

        public int MaximumStoredTraces => Mathf.Max(1, maximumStoredTraces);

        public float LinearDamping => Mathf.Max(0f, linearDamping);

        public float AngularDamping => Mathf.Max(0f, angularDamping);

        private void OnValidate()
        {
            traceSchemaVersion = Mathf.Max(1, traceSchemaVersion);
            totalMass = Mathf.Max(1f, totalMass);
            handoffNormalizedTime = Mathf.Clamp01(handoffNormalizedTime);
            sampleIntervalSeconds = Mathf.Max(
                MinimumPositiveValue,
                sampleIntervalSeconds);
            minimumActiveSeconds = Mathf.Max(0f, minimumActiveSeconds);
            settleHoldSeconds = Mathf.Max(
                MinimumPositiveValue,
                settleHoldSeconds);
            maximumActiveSeconds = Mathf.Max(
                minimumActiveSeconds + MinimumPositiveValue,
                maximumActiveSeconds);
            settleLinearSpeed = Mathf.Max(0f, settleLinearSpeed);
            settleAngularSpeed = Mathf.Max(0f, settleAngularSpeed);
            maximumImpulseSpeed = Mathf.Max(0f, maximumImpulseSpeed);
            upwardImpulseFraction = Mathf.Clamp01(upwardImpulseFraction);
            maximumStoredTraces = Mathf.Max(1, maximumStoredTraces);
            linearDamping = Mathf.Max(0f, linearDamping);
            angularDamping = Mathf.Max(0f, angularDamping);
        }
    }
}
