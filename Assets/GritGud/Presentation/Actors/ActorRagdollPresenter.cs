using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Actors
{
    internal enum ActorRagdollHandoffFallbackReason
    {
        None = 0,
        AuthoredActionUnavailable = 1,
        AuthoredHandoffTimedOut = 2,
        RigUnavailable = 3,
        CancelledByRecovery = 4,
    }

    /// <summary>
    /// Owns the presentation-only handoff from an authored incapacitation pose
    /// to bounded ragdoll physics. Gameplay position and collision authority
    /// remain on the actor root and are never derived from these bodies.
    /// </summary>
    [DefaultExecutionOrder(350)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AnimatorDriver))]
    public sealed class ActorRagdollPresenter : MonoBehaviour
    {
        private sealed class RuntimeBone
        {
            public RuntimeBone(
                ActorRagdollBoneDefinition definition,
                Transform transform,
                Rigidbody rigidbody,
                Collider collider)
            {
                Definition = definition;
                Transform = transform;
                Rigidbody = rigidbody;
                Collider = collider;
            }

            public ActorRagdollBoneDefinition Definition { get; }
            public Transform Transform { get; }
            public Rigidbody Rigidbody { get; }
            public Collider Collider { get; }
        }

        private sealed class ActivationRequest
        {
            public ActivationRequest(
                long sourceTransitionSequence,
                ActorAnimationAction expectedAction,
                TargetRegionId? hitRegion,
                Vector3 impulseDirection,
                float handoffEventNormalizedTime,
                float armedUnscaledTime)
            {
                SourceTransitionSequence = sourceTransitionSequence;
                ExpectedAction = expectedAction;
                HitRegion = hitRegion;
                ImpulseDirection = impulseDirection;
                HandoffEventNormalizedTime = handoffEventNormalizedTime;
                ArmedUnscaledTime = armedUnscaledTime;
            }

            public long SourceTransitionSequence { get; }
            public ActorAnimationAction ExpectedAction { get; }
            public TargetRegionId? HitRegion { get; }
            public Vector3 ImpulseDirection { get; }
            public float HandoffEventNormalizedTime { get; }
            public float ArmedUnscaledTime { get; }
            public float WaitSeconds { get; set; }
        }

        private sealed class ReplayBoneSnapshot
        {
            public ReplayBoneSnapshot(RuntimeBone bone)
            {
                Position = bone.Transform.position;
                Rotation = bone.Transform.rotation;
                IsKinematic = bone.Rigidbody.isKinematic;
                DetectCollisions = bone.Rigidbody.detectCollisions;
                LinearVelocity = bone.Rigidbody.linearVelocity;
                AngularVelocity = bone.Rigidbody.angularVelocity;
                ColliderEnabled = bone.Collider.enabled;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public bool IsKinematic { get; }
            public bool DetectCollisions { get; }
            public Vector3 LinearVelocity { get; }
            public Vector3 AngularVelocity { get; }
            public bool ColliderEnabled { get; }
        }

        [SerializeField]
        private ActorRagdollProfile profile;

        [SerializeField]
        private AnimatorDriver animatorDriver;

        private readonly List<RuntimeBone> runtimeBones = new();
        private readonly Dictionary<HumanBodyBones, RuntimeBone> indexedBones =
            new();
        private readonly List<ActorRagdollPoseTrace> traces = new();
        private readonly List<Transform> traceBones = new();
        private ActorAnimationCoordinator animationCoordinator;
        private ActivationRequest pendingActivation;
        private ActorRagdollPoseTrace activeTrace;
        private ReplayBoneSnapshot[] replaySnapshot;
        private Vector3[] replayPositions;
        private Quaternion[] replayRotations;
        private bool rigBuilt;
        private bool ragdollActive;
        private bool settled;
        private bool replaying;
        private float activeSeconds;
        private float quietSeconds;
        private float nextSampleSeconds;

        public ActorRagdollProfile Profile => profile;

        public bool IsRagdollActive => ragdollActive;

        public bool IsSettled => settled;

        public int RuntimeBoneCount => runtimeBones.Count;

        internal int StoredTraceCount => traces.Count;

        internal bool HasPendingActivation => pendingActivation != null;

        internal float PendingArmedUnscaledTime =>
            pendingActivation?.ArmedUnscaledTime ?? -1f;

        internal ActorAnimationAction? PendingExpectedAction =>
            pendingActivation?.ExpectedAction;

        internal long LastHandoffSourceTransitionSequence { get; private set; }

        internal ActorRagdollHandoffFallbackReason LastHandoffFallbackReason
        {
            get;
            private set;
        }

        private Animator TargetAnimator => animatorDriver?.TargetAnimator;

        private void Awake()
        {
            animatorDriver ??= GetComponent<AnimatorDriver>();
            animationCoordinator ??= GetComponent<ActorAnimationCoordinator>();
        }

        public void BindProfile(ActorRagdollProfile ragdollProfile)
        {
            profile = ragdollProfile ?? throw new ArgumentNullException(
                nameof(ragdollProfile));
            animatorDriver ??= GetComponent<AnimatorDriver>();
            animationCoordinator ??= GetComponent<ActorAnimationCoordinator>();
        }

        internal bool ArmIncapacitation(
            long sourceTransitionSequence,
            TargetRegionId? hitRegion,
            Vector3 impulseDirection,
            float reactionStartEventNormalizedTime = 0f)
        {
            if (ragdollActive)
                return false;
            if (!IsFinite(impulseDirection) ||
                !IsFinite(reactionStartEventNormalizedTime))
            {
                throw new ArgumentOutOfRangeException(nameof(impulseDirection));
            }
            if (sourceTransitionSequence < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceTransitionSequence));

            float handoff = Mathf.Lerp(
                Mathf.Clamp01(reactionStartEventNormalizedTime),
                1f,
                profile != null ? profile.HandoffNormalizedTime : 1f);
            var request = new ActivationRequest(
                sourceTransitionSequence,
                ActorAnimationCoordinator.SelectIncapacitationAction(
                    hitRegion),
                hitRegion,
                impulseDirection,
                handoff,
                Time.unscaledTime);
            if (pendingActivation == null ||
                (pendingActivation.SourceTransitionSequence == 0 &&
                    sourceTransitionSequence > 0))
            {
                pendingActivation = request;
                LastHandoffSourceTransitionSequence =
                    sourceTransitionSequence;
                LastHandoffFallbackReason =
                    ActorRagdollHandoffFallbackReason.None;
            }
            return true;
        }

        internal bool CancelPendingIncapacitation()
        {
            if (pendingActivation == null)
                return false;
            LastHandoffSourceTransitionSequence =
                pendingActivation.SourceTransitionSequence;
            LastHandoffFallbackReason =
                ActorRagdollHandoffFallbackReason.CancelledByRecovery;
            pendingActivation = null;
            return true;
        }

        internal void BeginReplayPresentation()
        {
            if (replaying)
            {
                throw new InvalidOperationException(
                    "Actor ragdoll replay presentation is already active.");
            }
            if (!rigBuilt && traces.Count > 0)
                EnsureRuntimeRig();
            replaying = true;
            if (!rigBuilt)
            {
                replaySnapshot = Array.Empty<ReplayBoneSnapshot>();
                return;
            }
            replaySnapshot = new ReplayBoneSnapshot[runtimeBones.Count];
            for (int index = 0; index < runtimeBones.Count; index++)
            {
                RuntimeBone bone = runtimeBones[index];
                replaySnapshot[index] = new ReplayBoneSnapshot(bone);
                bone.Rigidbody.isKinematic = true;
                bone.Rigidbody.detectCollisions = false;
                bone.Collider.enabled = false;
            }
        }

        internal bool PresentReplay(
            long transitionSequence,
            float normalizedProgress,
            float presentationDurationSeconds)
        {
            if (!replaying)
            {
                throw new InvalidOperationException(
                    "Begin ragdoll replay presentation before sampling traces.");
            }
            if (transitionSequence <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(transitionSequence));
            if (!IsFinite(normalizedProgress)
                || !IsFinite(presentationDurationSeconds)
                || presentationDurationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedProgress));
            if (!rigBuilt)
                return false;

            ActorRagdollPoseTrace selected = null;
            foreach (ActorRagdollPoseTrace trace in traces)
            {
                if (!TraceMatchesProfile(trace)
                    || trace.JournalSequence != transitionSequence)
                    continue;
                selected = trace;
                break;
            }
            if (selected == null
                || selected.SampleCount == 0
                || normalizedProgress
                    < selected.HandoffEventNormalizedTime)
                return false;

            EnsureReplayBuffers(selected.BoneCount);
            selected.SampleAt(
                Mathf.Max(
                    0f,
                    (normalizedProgress
                        - selected.HandoffEventNormalizedTime)
                        * presentationDurationSeconds),
                replayPositions,
                replayRotations);
            for (int index = 0; index < runtimeBones.Count; index++)
            {
                RuntimeBone bone = runtimeBones[index];
                Vector3 position = transform.TransformPoint(
                    replayPositions[index]);
                Quaternion rotation = transform.rotation *
                    replayRotations[index];
                bone.Rigidbody.position = position;
                bone.Rigidbody.rotation = rotation;
                bone.Transform.SetPositionAndRotation(position, rotation);
            }
            return true;
        }

        internal void EndReplayPresentation()
        {
            if (!replaying)
                return;
            for (int index = 0; index < runtimeBones.Count; index++)
            {
                RuntimeBone bone = runtimeBones[index];
                ReplayBoneSnapshot snapshot = replaySnapshot[index];
                bone.Rigidbody.isKinematic = true;
                bone.Rigidbody.detectCollisions = false;
                bone.Transform.SetPositionAndRotation(
                    snapshot.Position,
                    snapshot.Rotation);
                bone.Rigidbody.position = snapshot.Position;
                bone.Rigidbody.rotation = snapshot.Rotation;
                bone.Collider.enabled = snapshot.ColliderEnabled;
                bone.Rigidbody.detectCollisions = snapshot.DetectCollisions;
                bone.Rigidbody.isKinematic = snapshot.IsKinematic;
                if (!snapshot.IsKinematic)
                {
                    bone.Rigidbody.linearVelocity = snapshot.LinearVelocity;
                    bone.Rigidbody.angularVelocity = snapshot.AngularVelocity;
                }
            }
            replaySnapshot = null;
            replaying = false;
        }

        internal void EnsureRuntimeRig()
        {
            if (rigBuilt)
                return;
            animatorDriver ??= GetComponent<AnimatorDriver>();
            animationCoordinator ??= GetComponent<
                ActorAnimationCoordinator>();
            if (profile == null || animatorDriver == null ||
                TargetAnimator == null)
            {
                return;
            }
            Animator animator = TargetAnimator;
            if (!animator.isHuman || profile.Bones == null ||
                profile.Bones.Count == 0)
            {
                throw new InvalidOperationException(
                    "Actor ragdolls require a Humanoid animator and a non-empty profile.");
            }

            float totalFraction = 0f;
            var seen = new HashSet<HumanBodyBones>();
            foreach (ActorRagdollBoneDefinition definition in profile.Bones)
            {
                if (definition == null ||
                    definition.Bone == HumanBodyBones.LastBone ||
                    !seen.Add(definition.Bone))
                {
                    throw new InvalidOperationException(
                        "Actor ragdoll bone definitions must be unique and valid.");
                }
                totalFraction += definition.MassFraction;
            }

            foreach (ActorRagdollBoneDefinition definition in profile.Bones)
            {
                Transform bone = animator.GetBoneTransform(definition.Bone);
                Transform end = animator.GetBoneTransform(definition.EndBone);
                if (bone == null || end == null)
                {
                    throw new InvalidOperationException(
                        $"Ragdoll bone '{definition.Bone}' requires its " +
                        $"endpoint '{definition.EndBone}'.");
                }
                if (bone.GetComponent<Rigidbody>() != null)
                {
                    throw new InvalidOperationException(
                        $"Ragdoll bone '{definition.Bone}' already has a Rigidbody.");
                }

                Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
                body.mass = profile.TotalMass * definition.MassFraction /
                    totalFraction;
                body.useGravity = true;
                body.isKinematic = true;
                body.detectCollisions = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousSpeculative;
                body.linearDamping = profile.LinearDamping;
                body.angularDamping = profile.AngularDamping;
                Collider collider = CreateCollider(definition, bone, end);
                collider.enabled = false;
                var runtime = new RuntimeBone(
                    definition,
                    bone,
                    body,
                    collider);
                runtimeBones.Add(runtime);
                indexedBones.Add(definition.Bone, runtime);
                traceBones.Add(bone);
            }

            foreach (RuntimeBone bone in runtimeBones)
            {
                HumanBodyBones connected = bone.Definition.ConnectedBone;
                if (connected == HumanBodyBones.LastBone)
                    continue;
                if (!indexedBones.TryGetValue(connected, out RuntimeBone parent))
                {
                    throw new InvalidOperationException(
                        $"Ragdoll bone '{bone.Definition.Bone}' requires " +
                        $"connected body '{connected}'.");
                }
                ConfigureJoint(bone, parent);
            }

            replayPositions = new Vector3[runtimeBones.Count];
            replayRotations = new Quaternion[runtimeBones.Count];
            rigBuilt = true;
        }

        internal bool ActivateImmediatelyForTests(
            long sourceTransitionSequence,
            TargetRegionId? hitRegion,
            Vector3 impulseDirection,
            float handoffEventNormalizedTime = 0.75f)
        {
            EnsureRuntimeRig();
            if (!rigBuilt || ragdollActive)
                return false;
            Activate(new ActivationRequest(
                sourceTransitionSequence,
                ActorAnimationCoordinator.SelectIncapacitationAction(
                    hitRegion),
                hitRegion,
                impulseDirection,
                handoffEventNormalizedTime,
                Time.unscaledTime));
            return true;
        }

        internal bool TryGetTrace(
            long journalSequence,
            out ActorRagdollPoseTrace trace)
        {
            foreach (ActorRagdollPoseTrace candidate in traces)
            {
                if (candidate.JournalSequence == journalSequence)
                {
                    trace = candidate;
                    return true;
                }
            }
            trace = null;
            return false;
        }

        private void LateUpdate()
        {
            TickPendingHandoff(Time.unscaledDeltaTime);
        }

        internal bool TryActivateAtAuthoredHandoff()
        {
            if (replaying || pendingActivation == null || ragdollActive)
                return false;
            EnsureRuntimeRig();
            if (!rigBuilt || !HasReachedAuthoredHandoff(pendingActivation))
                return false;
            ActivationRequest request = pendingActivation;
            pendingActivation = null;
            Activate(request);
            return true;
        }

        internal bool TickPendingHandoff(float unscaledDeltaTime)
        {
            if (!IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(unscaledDeltaTime));
            if (replaying || pendingActivation == null || ragdollActive)
                return false;
            if (TryActivateAtAuthoredHandoff())
                return true;

            pendingActivation.WaitSeconds += unscaledDeltaTime;
            float maximumWait = profile != null
                ? profile.MaximumHandoffWaitSeconds
                : 0f;
            if (pendingActivation.WaitSeconds < maximumWait)
                return false;

            ActivationRequest request = pendingActivation;
            pendingActivation = null;
            LastHandoffSourceTransitionSequence =
                request.SourceTransitionSequence;
            LastHandoffFallbackReason = ResolveFallbackReason(request);
            if (rigBuilt)
                Activate(request);
            return true;
        }

        private void FixedUpdate()
        {
            if (!ragdollActive || settled || replaying)
                return;
            TickActiveRagdoll(Time.fixedDeltaTime);
        }

        internal void TickActiveRagdoll(float deltaTime)
        {
            if (!ragdollActive || settled || replaying)
                return;
            float elapsed = Mathf.Max(0f, deltaTime);
            activeSeconds += elapsed;
            if (activeTrace != null &&
                activeSeconds + 0.0001f >= nextSampleSeconds)
            {
                activeTrace.AddSample(
                    activeSeconds,
                    transform,
                    traceBones);
                nextSampleSeconds += profile.SampleIntervalSeconds;
            }

            if (activeSeconds >= profile.MinimumActiveSeconds &&
                IsQuiet())
            {
                quietSeconds += elapsed;
            }
            else
            {
                quietSeconds = 0f;
            }
            if (activeSeconds >= profile.MaximumActiveSeconds ||
                quietSeconds >= profile.SettleHoldSeconds)
            {
                FreezeAtRest();
            }
        }

        private bool HasReachedAuthoredHandoff(ActivationRequest request)
        {
            Animator animator = TargetAnimator;
            ActorAnimationAction? action = animationCoordinator
                ?.LastRequestedAction;
            if (animator == null || !animator.enabled ||
                action != request.ExpectedAction ||
                animationCoordinator.Profile == null ||
                !animationCoordinator.Profile.TryGetActionBinding(
                    request.ExpectedAction,
                    out ActorAnimationActionBinding binding))
            {
                return false;
            }

            int layer = animator.GetLayerIndex(binding.LayerName);
            if (layer < 0)
                return false;
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(
                layer);
            if (current.IsName(binding.StateName))
            {
                return current.normalizedTime >= profile.HandoffNormalizedTime;
            }
            if (animator.IsInTransition(layer))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
                return next.IsName(binding.StateName) &&
                    next.normalizedTime >= profile.HandoffNormalizedTime;
            }
            return false;
        }

        private ActorRagdollHandoffFallbackReason ResolveFallbackReason(
            ActivationRequest request)
        {
            if (!rigBuilt)
                return ActorRagdollHandoffFallbackReason.RigUnavailable;
            Animator animator = TargetAnimator;
            if (animator == null || !animator.enabled
                || animationCoordinator?.LastRequestedAction
                    != request.ExpectedAction
                || animationCoordinator.Profile == null
                || !animationCoordinator.Profile.TryGetActionBinding(
                    request.ExpectedAction,
                    out _))
            {
                return ActorRagdollHandoffFallbackReason
                    .AuthoredActionUnavailable;
            }
            return ActorRagdollHandoffFallbackReason
                .AuthoredHandoffTimedOut;
        }

        private void Activate(ActivationRequest request)
        {
            ConfigureCollisionIgnores();
            Animator animator = TargetAnimator;
            if (animator != null)
                animator.enabled = false;
            foreach (RuntimeBone bone in runtimeBones)
            {
                bone.Rigidbody.isKinematic = false;
                bone.Rigidbody.detectCollisions = true;
                bone.Collider.enabled = true;
                bone.Rigidbody.WakeUp();
            }
            Physics.SyncTransforms();

            ragdollActive = true;
            settled = false;
            activeSeconds = 0f;
            quietSeconds = 0f;
            nextSampleSeconds = profile.SampleIntervalSeconds;
            if (request.SourceTransitionSequence > 0)
            {
                activeTrace = new ActorRagdollPoseTrace(
                    request.SourceTransitionSequence,
                    profile.TraceSchemaId,
                    profile.TraceSchemaVersion,
                    runtimeBones.Count,
                    request.HandoffEventNormalizedTime);
                activeTrace.AddSample(0f, transform, traceBones);
                traces.Add(activeTrace);
                while (traces.Count > profile.MaximumStoredTraces)
                    traces.RemoveAt(0);
            }
            else
            {
                activeTrace = null;
            }

            RuntimeBone impulseBone = ResolveImpulseBone(request.HitRegion);
            Vector3 direction = request.ImpulseDirection.sqrMagnitude > 0.0001f
                ? request.ImpulseDirection.normalized
                : transform.forward;
            direction = (direction +
                (Vector3.up * profile.UpwardImpulseFraction)).normalized;
            Vector3 impulse = direction * profile.MaximumImpulseSpeed;
            impulseBone.Rigidbody.AddForceAtPosition(
                impulse,
                impulseBone.Transform.position,
                ForceMode.VelocityChange);
        }

        private RuntimeBone ResolveImpulseBone(TargetRegionId? region)
        {
            HumanBodyBones preferred;
            switch (region)
            {
                case TargetRegionId.Head:
                    preferred = HumanBodyBones.Head;
                    break;
                case TargetRegionId.LeftArm:
                    preferred = HumanBodyBones.LeftUpperArm;
                    break;
                case TargetRegionId.RightArm:
                    preferred = HumanBodyBones.RightUpperArm;
                    break;
                case TargetRegionId.LeftLeg:
                    preferred = HumanBodyBones.LeftUpperLeg;
                    break;
                case TargetRegionId.RightLeg:
                    preferred = HumanBodyBones.RightUpperLeg;
                    break;
                default:
                    preferred = HumanBodyBones.Chest;
                    break;
            }
            return indexedBones.TryGetValue(preferred, out RuntimeBone bone)
                ? bone
                : runtimeBones[0];
        }

        private bool IsQuiet()
        {
            float linearThreshold = profile.SettleLinearSpeed *
                profile.SettleLinearSpeed;
            float angularThreshold = profile.SettleAngularSpeed *
                profile.SettleAngularSpeed;
            foreach (RuntimeBone bone in runtimeBones)
            {
                if (bone.Rigidbody.linearVelocity.sqrMagnitude >
                        linearThreshold ||
                    bone.Rigidbody.angularVelocity.sqrMagnitude >
                        angularThreshold)
                {
                    return false;
                }
            }
            return true;
        }

        private void FreezeAtRest()
        {
            if (activeTrace != null)
            {
                activeTrace.AddSample(activeSeconds, transform, traceBones);
                activeTrace.Complete();
            }
            foreach (RuntimeBone bone in runtimeBones)
            {
                bone.Rigidbody.linearVelocity = Vector3.zero;
                bone.Rigidbody.angularVelocity = Vector3.zero;
                bone.Rigidbody.isKinematic = true;
                bone.Rigidbody.detectCollisions = false;
                bone.Collider.enabled = false;
            }
            settled = true;
        }

        private void ConfigureCollisionIgnores()
        {
            Collider[] sceneColliders = FindObjectsByType<Collider>(
                FindObjectsInactive.Include);
            foreach (RuntimeBone bone in runtimeBones)
            {
                foreach (Collider candidate in sceneColliders)
                {
                    if (candidate == null || candidate == bone.Collider)
                        continue;
                    bool ownRagdoll = candidate.transform.IsChildOf(transform);
                    bool dynamic = candidate.attachedRigidbody != null;
                    bool actorController = candidate is CharacterController;
                    if (ownRagdoll || dynamic || actorController)
                    {
                        Physics.IgnoreCollision(
                            bone.Collider,
                            candidate,
                            true);
                    }
                }
            }
        }

        private bool TraceMatchesProfile(ActorRagdollPoseTrace trace) =>
            trace != null &&
            trace.BoneCount == runtimeBones.Count &&
            trace.SchemaVersion == profile.TraceSchemaVersion &&
            string.Equals(
                trace.SchemaId,
                profile.TraceSchemaId,
                StringComparison.Ordinal);

        private void EnsureReplayBuffers(int boneCount)
        {
            if (replayPositions == null ||
                replayPositions.Length != boneCount)
            {
                replayPositions = new Vector3[boneCount];
                replayRotations = new Quaternion[boneCount];
            }
        }

        private static Collider CreateCollider(
            ActorRagdollBoneDefinition definition,
            Transform bone,
            Transform end)
        {
            Vector3 localEnd = bone.InverseTransformPoint(end.position);
            float referenceLength = Mathf.Max(0.01f, localEnd.magnitude);
            float radius = Mathf.Max(
                0.025f,
                referenceLength * definition.RadiusScale);
            if (definition.ColliderShape ==
                ActorRagdollColliderShape.Sphere)
            {
                SphereCollider sphere = bone.gameObject.AddComponent<
                    SphereCollider>();
                sphere.center = Vector3.zero;
                sphere.radius = radius;
                return sphere;
            }

            CapsuleCollider capsule = bone.gameObject.AddComponent<
                CapsuleCollider>();
            capsule.direction = ResolveCapsuleDirection(localEnd);
            capsule.center = localEnd * definition.LengthScale * 0.5f;
            capsule.radius = radius;
            capsule.height = Mathf.Max(
                radius * 2f,
                referenceLength * definition.LengthScale);
            return capsule;
        }

        private static int ResolveCapsuleDirection(Vector3 localDirection)
        {
            Vector3 absolute = new(
                Mathf.Abs(localDirection.x),
                Mathf.Abs(localDirection.y),
                Mathf.Abs(localDirection.z));
            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
                return 0;
            return absolute.y >= absolute.z ? 1 : 2;
        }

        private static void ConfigureJoint(
            RuntimeBone bone,
            RuntimeBone parent)
        {
            CharacterJoint joint = bone.Transform.gameObject.AddComponent<
                CharacterJoint>();
            joint.connectedBody = parent.Rigidbody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = parent.Transform.InverseTransformPoint(
                bone.Transform.position);

            Transform end = bone.Transform.GetComponentInParent<Animator>()
                ?.GetBoneTransform(bone.Definition.EndBone);
            Vector3 axis = end != null
                ? bone.Transform.InverseTransformDirection(
                    end.position - bone.Transform.position).normalized
                : Vector3.right;
            if (axis.sqrMagnitude <= 0.0001f)
                axis = Vector3.right;
            Vector3 swing = Vector3.Cross(axis, Vector3.up);
            if (swing.sqrMagnitude <= 0.0001f)
                swing = Vector3.Cross(axis, Vector3.forward);
            joint.axis = axis;
            joint.swingAxis = swing.normalized;
            joint.lowTwistLimit = new SoftJointLimit
            {
                limit = bone.Definition.LowTwistDegrees,
            };
            joint.highTwistLimit = new SoftJointLimit
            {
                limit = bone.Definition.HighTwistDegrees,
            };
            joint.swing1Limit = new SoftJointLimit
            {
                limit = bone.Definition.SwingOneDegrees,
            };
            joint.swing2Limit = new SoftJointLimit
            {
                limit = bone.Definition.SwingTwoDegrees,
            };
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.projectionDistance = 0.08f;
            joint.projectionAngle = 12f;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
