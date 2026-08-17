using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class DestructiblePropPresenter : MonoBehaviour
    {
        private readonly List<ComponentEnabledState<Collider>> colliderStates =
            new List<ComponentEnabledState<Collider>>();
        private readonly List<ComponentEnabledState<Renderer>> rendererStates =
            new List<ComponentEnabledState<Renderer>>();
        private readonly List<GameObject> transientDebris =
            new List<GameObject>();
        private readonly List<ComponentEnabledState<Renderer>>
            displacementRendererStates =
                new List<ComponentEnabledState<Renderer>>();
        private readonly List<Mesh> displacementMeshes = new List<Mesh>();
        private DestructibleFractureProfile fractureProfile;
        private DestructibleFractureChunk[] fractureChunks =
            Array.Empty<DestructibleFractureChunk>();
        private GameObject fractureInstance;
        private GameObject displacementVisual;
        private Vector3 displacementStartPosition;
        private Vector3 displacementTargetPosition;
        private Quaternion displacementStartRotation;
        private Quaternion displacementTargetRotation;
        private DisplacementActionKind displacementActionKind;
        private float displacementElapsed;
        private float displacementDuration;
        private string propId;

        public bool IsBound => !string.IsNullOrEmpty(propId);

        public DestructiblePropState State { get; private set; }

        internal int ActiveTransientDebrisCount => transientDebris.Count;

        internal bool IsPresentingDisplacement => displacementVisual != null;

        internal Vector3 DisplacementVisualPosition =>
            displacementVisual == null
                ? transform.position
                : displacementVisual.transform.position;

        public void Bind(DestructiblePropSnapshot snapshot) =>
            Bind(snapshot, fracture: null);

        public void Bind(
            DestructiblePropSnapshot snapshot,
            DestructibleFractureProfile fracture)
        {
            Unbind();
            ValidateProfile(snapshot, fracture);
            propId = snapshot.PropId;
            fractureProfile = fracture;

            foreach (Collider propCollider in GetComponentsInChildren<Collider>(true))
            {
                colliderStates.Add(new ComponentEnabledState<Collider>(
                    propCollider,
                    propCollider.enabled));
            }

            foreach (Renderer propRenderer in GetComponentsInChildren<Renderer>(true))
            {
                rendererStates.Add(new ComponentEnabledState<Renderer>(
                    propRenderer,
                    propRenderer.enabled));
            }

            if (fractureProfile != null)
            {
                fractureInstance = Instantiate(
                    fractureProfile.FracturedPrefab,
                    transform,
                    worldPositionStays: false);
                fractureInstance.name =
                    $"{fractureProfile.ProfileId} [Fractured]";
                fractureChunks = IndexChunks(
                    fractureInstance,
                    snapshot.FractureChunkCount);
            }

            Present(snapshot);
        }

        public void Present(DestructiblePropSnapshot snapshot)
        {
            ValidateIdentity(snapshot);
            CompleteDisplacementPresentation();
            transform.SetPositionAndRotation(
                new Vector3(
                    snapshot.Pose.Position.X,
                    snapshot.Pose.Position.Y,
                    snapshot.Pose.Position.Z),
                Quaternion.Euler(
                    snapshot.Pose.PitchDegrees,
                    snapshot.Pose.YawDegrees,
                    snapshot.Pose.RollDegrees));

            bool fractured = snapshot.DetachedFractureChunks != 0UL
                && fractureInstance != null;
            bool destroyedWithoutProfile =
                snapshot.State == DestructiblePropState.Destroyed
                && fractureInstance == null;
            SetOriginalEnabled(!fractured && !destroyedWithoutProfile);

            if (fractureInstance != null)
            {
                fractureInstance.SetActive(fractured);
                for (int index = 0; index < fractureChunks.Length; index++)
                {
                    bool attached =
                        (snapshot.DetachedFractureChunks & (1UL << index)) == 0UL;
                    fractureChunks[index].gameObject.SetActive(attached);
                }
            }

            State = snapshot.State;
        }

        internal void PresentDisplacement(
            DisplacementRecord record,
            float durationSeconds)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (!record.Succeeded ||
                record.Request.SubjectKind != DisplacementSubjectKind.Prop ||
                record.PreviousPropState == null ||
                record.ResultingPropState == null)
            {
                throw new ArgumentException(
                    "Prop displacement presentation requires a successful "
                    + "prop-state record.",
                    nameof(record));
            }
            if (!string.Equals(
                    propId,
                    record.Request.SubjectId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A displacement record cannot be presented by another prop.",
                    nameof(record));
            }
            if (float.IsNaN(durationSeconds) ||
                float.IsInfinity(durationSeconds) ||
                durationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            }

            Vector3 startPosition = IsPresentingDisplacement
                ? displacementVisual.transform.position
                : ToVector3(record.PreviousPropState.Pose.Position);
            Quaternion startRotation = IsPresentingDisplacement
                ? displacementVisual.transform.rotation
                : ToQuaternion(record.PreviousPropState.Pose);
            CompleteDisplacementPresentation();
            CreateDisplacementVisual();

            GameplayPropPose target = record.ResultingPropState.Pose;
            transform.SetPositionAndRotation(
                ToVector3(target.Position),
                ToQuaternion(target));
            displacementStartPosition = startPosition;
            displacementTargetPosition = transform.position;
            displacementStartRotation = startRotation;
            displacementTargetRotation = transform.rotation;
            displacementActionKind = record.Request.ActionKind;
            displacementElapsed = 0f;
            displacementDuration = durationSeconds;
            if (displacementVisual != null)
            {
                displacementVisual.transform.SetPositionAndRotation(
                    displacementStartPosition,
                    displacementStartRotation);
            }
        }

        internal void TickDisplacement(float deltaTime)
        {
            if (displacementVisual == null)
                return;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)
                || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            displacementElapsed = Mathf.Min(
                displacementDuration,
                displacementElapsed + deltaTime);
            float normalized = displacementDuration <= 0f
                ? 1f
                : displacementElapsed / displacementDuration;
            float progress = GameplayDisplacementPresentationTiming
                .EvaluateSubjectProgress(
                    displacementActionKind,
                    normalized);
            displacementVisual.transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(
                    displacementStartPosition,
                    displacementTargetPosition,
                    progress),
                Quaternion.SlerpUnclamped(
                    displacementStartRotation,
                    displacementTargetRotation,
                    progress));
            if (displacementElapsed >= displacementDuration)
                CompleteDisplacementPresentation();
        }

        internal void PresentDamage(
            DestructibleDamageRecord record,
            bool spawnTransientDebris)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            Present(record.Resulting);
            if (spawnTransientDebris)
            {
                SpawnTransientDebris(
                    record.NewlyDetachedFractureChunks,
                    record.Sequence);
            }
        }

        internal void ClearTransientDebris()
        {
            for (int index = transientDebris.Count - 1; index >= 0; index--)
            {
                GameObject debris = transientDebris[index];
                transientDebris.RemoveAt(index);
                DestroyOwnedObject(debris);
            }
        }

        public void Unbind()
        {
            CompleteDisplacementPresentation();
            ClearTransientDebris();
            SetOriginalEnabled(enabled: true);
            if (fractureInstance != null)
            {
                DestroyOwnedObject(fractureInstance);
            }

            propId = null;
            fractureProfile = null;
            fractureInstance = null;
            fractureChunks = Array.Empty<DestructibleFractureChunk>();
            colliderStates.Clear();
            rendererStates.Clear();
        }

        private void CreateDisplacementVisual()
        {
            displacementVisual = new GameObject(
                propId + " [Displacement Visual]");
            displacementVisual.layer = LayerMask.NameToLayer("Ignore Raycast");
            displacementVisual.transform.SetPositionAndRotation(
                transform.position,
                transform.rotation);
            displacementVisual.transform.localScale = transform.lossyScale;

            foreach (Renderer source in GetComponentsInChildren<Renderer>(false))
            {
                if (source == null || !source.enabled ||
                    !TryCloneRenderer(source, displacementVisual.transform))
                {
                    continue;
                }

                displacementRendererStates.Add(
                    new ComponentEnabledState<Renderer>(source, true));
                source.enabled = false;
            }

            if (displacementRendererStates.Count == 0)
            {
                displacementVisual.SetActive(false);
                DestroyOwnedObject(displacementVisual);
                displacementVisual = null;
            }
        }

        private bool TryCloneRenderer(Renderer source, Transform visualRoot)
        {
            Mesh sharedMesh = null;
            if (source is MeshRenderer)
            {
                MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
                sharedMesh = sourceFilter?.sharedMesh;
            }
            else if (source is SkinnedMeshRenderer skinned)
            {
                sharedMesh = new Mesh
                {
                    name = source.name + " Displacement Mesh",
                };
                skinned.BakeMesh(sharedMesh);
                displacementMeshes.Add(sharedMesh);
            }

            if (sharedMesh == null)
                return false;

            var clone = new GameObject(source.name + " [Displacement]");
            clone.layer = visualRoot.gameObject.layer;
            clone.transform.SetPositionAndRotation(
                source.transform.position,
                source.transform.rotation);
            clone.transform.localScale = source.transform.lossyScale;
            clone.transform.SetParent(visualRoot, worldPositionStays: true);
            clone.AddComponent<MeshFilter>().sharedMesh = sharedMesh;
            MeshRenderer renderer = clone.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            renderer.lightProbeUsage = source.lightProbeUsage;
            renderer.reflectionProbeUsage = source.reflectionProbeUsage;
            renderer.motionVectorGenerationMode =
                source.motionVectorGenerationMode;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            return true;
        }

        private void CompleteDisplacementPresentation()
        {
            foreach (ComponentEnabledState<Renderer> state in
                displacementRendererStates)
            {
                if (state.Component != null)
                    state.Component.enabled = state.Enabled;
            }
            displacementRendererStates.Clear();

            if (displacementVisual != null)
            {
                displacementVisual.SetActive(false);
                DestroyOwnedObject(displacementVisual);
            }
            displacementVisual = null;
            foreach (Mesh mesh in displacementMeshes)
                DestroyOwnedObject(mesh);
            displacementMeshes.Clear();
            displacementElapsed = 0f;
            displacementDuration = 0f;
        }

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);

        private static Quaternion ToQuaternion(GameplayPropPose pose) =>
            Quaternion.Euler(
                pose.PitchDegrees,
                pose.YawDegrees,
                pose.RollDegrees);

        private void SpawnTransientDebris(ulong detachedMask, long sequence)
        {
            if (fractureProfile == null || detachedMask == 0UL)
            {
                return;
            }

            for (int index = 0; index < fractureChunks.Length; index++)
            {
                if ((detachedMask & (1UL << index)) == 0UL)
                {
                    continue;
                }

                DestructibleFractureChunk source = fractureChunks[index];
                GameObject debris = Instantiate(
                    source.gameObject,
                    source.transform.position,
                    source.transform.rotation);
                debris.name = $"{propId} fracture debris {index}";
                debris.SetActive(true);
                foreach (Collider debrisCollider in
                    debris.GetComponentsInChildren<Collider>(true))
                {
                    debrisCollider.enabled = false;
                }

                Vector3 radial = source.transform.position - transform.position;
                radial.y = Mathf.Max(0.18f, radial.y);
                if (radial.sqrMagnitude <= 0.0001f)
                {
                    radial = DeterministicHorizontal(sequence, index);
                }

                Vector3 velocity =
                    (radial.normalized + (Vector3.up * 0.65f)).normalized
                    * fractureProfile.DebrisImpulse;
                var transient = debris.AddComponent<DestructibleDebrisTransient>();
                transient.Initialize(
                    velocity,
                    DeterministicHorizontal(sequence + 17L, index) * 220f,
                    fractureProfile.DebrisLifetime,
                    () => transientDebris.Remove(debris));
                transientDebris.Add(debris);
            }
        }

        private void SetOriginalEnabled(bool enabled)
        {
            foreach (ComponentEnabledState<Collider> state in colliderStates)
            {
                if (state.Component != null)
                {
                    state.Component.enabled = enabled && state.Enabled;
                }
            }

            foreach (ComponentEnabledState<Renderer> state in rendererStates)
            {
                if (state.Component != null)
                {
                    state.Component.enabled = enabled && state.Enabled;
                }
            }
        }

        private void ValidateIdentity(DestructiblePropSnapshot snapshot)
        {
            if (!IsBound)
            {
                throw new InvalidOperationException(
                    "Destructible presentation must be bound before use.");
            }

            if (!string.Equals(propId, snapshot.PropId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A destructible presenter cannot change prop identity.",
                    nameof(snapshot));
            }

            ValidateProfile(snapshot, fractureProfile);
        }

        private static void ValidateProfile(
            DestructiblePropSnapshot snapshot,
            DestructibleFractureProfile fracture)
        {
            int profileChunkCount = fracture?.ChunkCount ?? 0;
            if (profileChunkCount != snapshot.FractureChunkCount)
            {
                throw new InvalidOperationException(
                    $"Prop '{snapshot.PropId}' expects {snapshot.FractureChunkCount} "
                    + $"fracture chunks but its profile provides {profileChunkCount}.");
            }
        }

        private static DestructibleFractureChunk[] IndexChunks(
            GameObject root,
            int expectedCount)
        {
            var indexed = new DestructibleFractureChunk[expectedCount];
            foreach (DestructibleFractureChunk chunk in
                root.GetComponentsInChildren<DestructibleFractureChunk>(true))
            {
                if (chunk.ChunkIndex < 0
                    || chunk.ChunkIndex >= expectedCount
                    || indexed[chunk.ChunkIndex] != null)
                {
                    throw new InvalidOperationException(
                        "Baked fracture chunks require unique contiguous indices.");
                }

                indexed[chunk.ChunkIndex] = chunk;
            }

            for (int index = 0; index < indexed.Length; index++)
            {
                if (indexed[index] == null)
                {
                    throw new InvalidOperationException(
                        $"Baked fracture profile is missing chunk {index}.");
                }
            }

            return indexed;
        }

        private static Vector3 DeterministicHorizontal(long sequence, int index)
        {
            float angle = Mathf.Repeat(
                (sequence * 137.50777f) + (index * 97.31f),
                360f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        private static void DestroyOwnedObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private readonly struct ComponentEnabledState<T>
            where T : Component
        {
            public ComponentEnabledState(T component, bool enabled)
            {
                Component = component;
                Enabled = enabled;
            }

            public T Component { get; }

            public bool Enabled { get; }
        }
    }

    internal sealed class DestructibleDebrisTransient : MonoBehaviour
    {
        private Vector3 velocity;
        private Vector3 angularVelocity;
        private float remainingLifetime;
        private Action expired;

        internal void Initialize(
            Vector3 initialVelocity,
            Vector3 degreesPerSecond,
            float lifetime,
            Action onExpired)
        {
            velocity = initialVelocity;
            angularVelocity = degreesPerSecond;
            remainingLifetime = Mathf.Max(0.1f, lifetime);
            expired = onExpired;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            velocity += Physics.gravity * deltaTime;
            transform.position += velocity * deltaTime;
            transform.Rotate(angularVelocity * deltaTime, Space.Self);
            remainingLifetime -= deltaTime;
            if (remainingLifetime > 0f)
            {
                return;
            }

            expired?.Invoke();
            expired = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            expired?.Invoke();
            expired = null;
        }
    }
}
