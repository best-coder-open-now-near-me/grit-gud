using System;
using System.Collections.Generic;
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
        private DestructibleFractureProfile fractureProfile;
        private DestructibleFractureChunk[] fractureChunks =
            Array.Empty<DestructibleFractureChunk>();
        private GameObject fractureInstance;
        private string propId;

        public bool IsBound => !string.IsNullOrEmpty(propId);

        public DestructiblePropState State { get; private set; }

        internal int ActiveTransientDebrisCount => transientDebris.Count;

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
