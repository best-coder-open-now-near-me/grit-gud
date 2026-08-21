using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class ProjectileFlightPresenter : IDisposable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color = Shader.PropertyToID("_Color");
        private static readonly int LineColor = Shader.PropertyToID("_LineColor");
        private static readonly int FillColor = Shader.PropertyToID("_FillColor");

        private const float MinimumPlaybackSeconds = 0.05f;

        private readonly ProjectilePresentationDefinition presentation;
        private readonly Transform effectParent;
        private readonly Quaternion trajectoryRotation;
        private readonly GameObject solidRoot;
        private readonly Transform solidSpinPivot;
        private readonly GameObject ghostRoot;
        private readonly Transform ghostSpinPivot;
        private readonly Material ghostMaterial;
        private readonly GameplayCelMaterialStyle solidStyle;
        private readonly ProjectileEffectPresenter effects;
        private readonly Queue<ScheduledAdvance> scheduledAdvances =
            new Queue<ScheduledAdvance>();

        private ProjectileFlightSnapshot snapshot;
        private ProjectileFlightSnapshot scheduledSnapshot;
        private ProjectileAdvanceRecord playback;
        private float playbackDuration;
        private float playbackElapsed;
        private Vector3 playbackStartPosition;
        private Vector3? previewEndpoint;
        private float ghostDistance;
        private float ghostEndpointHoldRemaining;
        private bool disposed;
        private bool presentationSuppressed;
        private GameObject replayImpactEffect;
        private float replayImpactRemainingSeconds;

        public ProjectileFlightPresenter(
            ProjectileFlightSnapshot initialSnapshot,
            ProjectilePresentationDefinition definition,
            Transform parent = null,
            Vector3? visualLaunchOrigin = null)
        {
            if (initialSnapshot.Launch == null)
            {
                throw new ArgumentException(
                    "Projectile presentation requires a launched flight.",
                    nameof(initialSnapshot));
            }

            presentation = definition ?? throw new ArgumentNullException(
                nameof(definition));
            effectParent = parent;
            if (presentation.Prefab == null)
            {
                throw new ArgumentException(
                    "Projectile presentation requires a model prefab.",
                    nameof(definition));
            }

            Vector3 presentationOrigin = visualLaunchOrigin
                ?? ToVector3(initialSnapshot.Launch.Origin);
            Vector3 direction = ToVector3(initialSnapshot.Launch.AimPoint)
                - presentationOrigin;
            trajectoryRotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

            solidRoot = CreateRoot(
                initialSnapshot.ProjectileId + " Solid Rocket",
                parent,
                presentation.Prefab,
                presentation,
                out solidSpinPivot,
                out GameObject solidVisual);
            solidStyle = GameplayCelMaterialStyle.Create(solidVisual.transform);

            ghostMaterial = CreateGhostMaterial();
            ghostRoot = CreateRoot(
                initialSnapshot.ProjectileId + " Trajectory Ghost",
                parent,
                presentation.Prefab,
                presentation,
                out ghostSpinPivot,
                out GameObject ghostVisual);
            ApplyGhostStyle(ghostVisual, ghostMaterial);

            effects = new ProjectileEffectPresenter(
                presentation,
                solidRoot.transform);
            snapshot = initialSnapshot;
            scheduledSnapshot = initialSnapshot;
            ApplySnapshot(initialSnapshot);
            if (initialSnapshot.Status == ProjectileFlightStatus.InFlight
                && initialSnapshot.DistanceTraveled <= 0.0001f)
            {
                solidRoot.transform.SetPositionAndRotation(
                    presentationOrigin,
                    trajectoryRotation);
            }
            UpdateGhost(0f);
        }

        internal Vector3 SolidPosition => solidRoot.transform.position;

        internal Vector3 GhostPosition => ghostRoot.transform.position;

        internal bool SolidVisible => solidRoot.activeSelf;

        internal bool GhostVisible => ghostRoot.activeSelf;

        internal bool TrailEmitting => effects.TrailEmitting;

        internal bool ReplayImpactVisible => replayImpactEffect != null;

        internal bool IsAdvancePlaying => playback != null
            || scheduledAdvances.Count > 0;

        internal Material GhostMaterial => ghostMaterial;

        internal Transform SolidSpinPivot => solidSpinPivot;

        internal void SetPreviewEndpoint(GameplayPosition endpoint)
        {
            ThrowIfDisposed();
            previewEndpoint = ToVector3(endpoint);
            ghostDistance = 0f;
            ghostEndpointHoldRemaining = 0f;
            UpdateGhost(0f);
        }

        internal void PresentReplay(ProjectileFlightSnapshot value)
        {
            ThrowIfDisposed();
            scheduledAdvances.Clear();
            playback = null;
            scheduledSnapshot = value;
            previewEndpoint = null;
            ApplySnapshot(value, createImpactEffect: false);
        }

        internal void PresentReplayImpact(GameplayPosition position)
        {
            ThrowIfDisposed();
            if (replayImpactEffect == null)
                replayImpactEffect = effects.CreateImpact(
                    ToVector3(position),
                    Mathf.Max(0.6f, snapshot.Launch.Definition.BlastRadius),
                    effectParent);
            replayImpactRemainingSeconds = presentation.ImpactEffectSeconds;
        }

        internal void TickReplayImpact(float deltaTime)
        {
            if (replayImpactEffect == null) return;
            replayImpactRemainingSeconds -= Mathf.Max(0f, deltaTime);
            if (replayImpactRemainingSeconds <= 0f) ClearReplayImpact();
        }

        internal void ClearReplayImpact()
        {
            GameplayObjectLifecycle.Destroy(replayImpactEffect);
            replayImpactEffect = null;
            replayImpactRemainingSeconds = 0f;
        }

        internal void SetPresentationSuppressed(bool suppressed)
        {
            ThrowIfDisposed();
            presentationSuppressed = suppressed;
            solidRoot.SetActive(
                !suppressed
                && snapshot.Status == ProjectileFlightStatus.InFlight);
            if (suppressed)
                ghostRoot.SetActive(false);
            else
                UpdateGhost(0f);
        }

        public void PlayAdvance(
            ProjectileAdvanceRecord advance,
            float durationSeconds)
        {
            ThrowIfDisposed();
            if (advance == null)
            {
                throw new ArgumentNullException(nameof(advance));
            }

            if (!string.Equals(
                    advance.ProjectileId,
                    scheduledSnapshot.ProjectileId,
                    StringComparison.Ordinal)
                || advance.Previous.Position.DistanceTo(
                    scheduledSnapshot.Position) > 0.0001f)
            {
                throw new InvalidOperationException(
                    "Projectile playback must continue from scheduled flight state.");
            }

            scheduledAdvances.Enqueue(new ScheduledAdvance(
                advance,
                Mathf.Max(MinimumPlaybackSeconds, durationSeconds)));
            scheduledSnapshot = advance.Resulting;
            if (playback == null)
            {
                StartNextAdvance();
            }
        }

        private void StartNextAdvance()
        {
            ScheduledAdvance scheduled = scheduledAdvances.Dequeue();
            playback = scheduled.Advance;
            playbackDuration = scheduled.DurationSeconds;
            playbackElapsed = 0f;
            playbackStartPosition = solidRoot.transform.position;
            previewEndpoint = null;
            ghostDistance = 0f;
            ghostEndpointHoldRemaining = 0f;
            ghostRoot.SetActive(false);
            effects.SetTrailEmission(true);
        }

        public void Tick(float deltaTime)
        {
            ThrowIfDisposed();
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (solidRoot.activeSelf)
            {
                Spin(solidSpinPivot, safeDeltaTime);
            }

            if (playback != null)
            {
                playbackElapsed += safeDeltaTime;
                float linearProgress = Mathf.Clamp01(
                    playbackElapsed / playbackDuration);
                float progress = CalculateAcceleratedProgress(
                    linearProgress,
                    presentation.PlaybackAccelerationFraction);
                solidRoot.transform.position = Vector3.Lerp(
                    playbackStartPosition,
                    ToVector3(playback.Resulting.Position),
                    progress);
                if (linearProgress >= 1f)
                {
                    ProjectileFlightSnapshot resulting = playback.Resulting;
                    playback = null;
                    ApplySnapshot(resulting);
                    if (scheduledAdvances.Count > 0)
                    {
                        StartNextAdvance();
                    }
                }
            }

            if (playback == null)
            {
                UpdateGhost(safeDeltaTime);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            scheduledAdvances.Clear();
            ClearReplayImpact();
            effects.Dispose();
            solidStyle?.Dispose();
            GameplayObjectLifecycle.Destroy(solidRoot);
            GameplayObjectLifecycle.Destroy(ghostRoot);
            GameplayObjectLifecycle.Destroy(ghostMaterial);
        }

        private void ApplySnapshot(
            ProjectileFlightSnapshot value,
            bool createImpactEffect = true)
        {
            snapshot = value;
            solidRoot.transform.SetPositionAndRotation(
                ToVector3(value.Position),
                trajectoryRotation);
            solidRoot.SetActive(
                !presentationSuppressed
                && value.Status == ProjectileFlightStatus.InFlight);
            if (createImpactEffect
                && value.Status == ProjectileFlightStatus.Impacted)
            {
                effects.CreateImpact(
                    ToVector3(value.Position),
                    Mathf.Max(0.6f, value.Launch.Definition.BlastRadius),
                    effectParent);
            }
            effects.SetTrailEmission(
                value.Status == ProjectileFlightStatus.InFlight
                && presentation.EmitsTrailWhileHolding);
            ghostDistance = 0f;
            ghostEndpointHoldRemaining = 0f;
        }

        private void UpdateGhost(float deltaTime)
        {
            if (presentationSuppressed
                || playback != null
                || snapshot.Status != ProjectileFlightStatus.InFlight)
            {
                ghostRoot.SetActive(false);
                return;
            }

            Vector3 segmentStart = solidRoot.transform.position;
            Vector3 segmentEnd;
            float segmentLength;
            if (previewEndpoint.HasValue)
            {
                segmentEnd = previewEndpoint.Value;
                segmentLength = Vector3.Distance(segmentStart, segmentEnd);
            }
            else
            {
                float remainingRange = snapshot.Launch.Definition.MaximumRange
                    - snapshot.DistanceTraveled;
                segmentLength = Mathf.Min(
                    snapshot.Launch.Definition.SpeedPerTurn,
                    remainingRange);
                segmentEnd = ToVector3(snapshot.Launch.GetPosition(
                    snapshot.DistanceTraveled + segmentLength));
            }
            if (segmentLength <= 0.0001f)
            {
                ghostRoot.SetActive(false);
                return;
            }

            ghostRoot.SetActive(true);
            if (ghostEndpointHoldRemaining > 0f)
            {
                ghostEndpointHoldRemaining -= deltaTime;
                if (ghostEndpointHoldRemaining <= 0f)
                {
                    ghostDistance = 0f;
                    ghostEndpointHoldRemaining = 0f;
                }
            }
            else
            {
                ghostDistance = Mathf.Min(
                    segmentLength,
                    ghostDistance
                        + (snapshot.Launch.Definition.SpeedPerTurn * deltaTime));
                if (ghostDistance >= segmentLength)
                {
                    ghostEndpointHoldRemaining =
                        presentation.GhostEndpointHoldSeconds;
                }
            }

            ghostRoot.transform.SetPositionAndRotation(
                Vector3.Lerp(
                    segmentStart,
                    segmentEnd,
                    Mathf.Clamp01(ghostDistance / segmentLength)),
                trajectoryRotation);
            Spin(ghostSpinPivot, deltaTime);
        }

        internal static float CalculateAcceleratedProgress(
            float linearProgress,
            float accelerationFraction) =>
            GameplayProjectilePresentationSampler.EvaluateProgress(
                linearProgress,
                accelerationFraction);

        private void Spin(Transform pivot, float deltaTime)
        {
            if (pivot == null || presentation.SpinDegreesPerSecond <= 0f)
            {
                return;
            }

            pivot.Rotate(
                0f,
                0f,
                presentation.SpinDegreesPerSecond * deltaTime,
                Space.Self);
        }

        private static GameObject CreateRoot(
            string name,
            Transform parent,
            GameObject prefab,
            ProjectilePresentationDefinition definition,
            out Transform spinPivot,
            out GameObject visual)
        {
            var root = new GameObject(name);
            if (parent != null)
            {
                root.transform.SetParent(parent, worldPositionStays: true);
            }

            var pivotObject = new GameObject("Spin Pivot");
            spinPivot = pivotObject.transform;
            spinPivot.SetParent(root.transform, worldPositionStays: false);
            visual = UnityEngine.Object.Instantiate(prefab, spinPivot);
            visual.name = prefab.name + " Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = definition.VisualRotation;
            visual.transform.localScale = Vector3.one * definition.VisualScale;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            return root;
        }

        private static Material CreateGhostMaterial()
        {
            Shader shader = Shader.Find(MovementRouteGhostPresenter.GhostShaderName)
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible projectile ghost shader is available.");
            }

            var material = new Material(shader)
            {
                name = "Rocket Trajectory Ghost",
                hideFlags = HideFlags.HideAndDontSave,
            };
            SetColor(material, BaseColor, GameplayVisualPalette.ProjectileGhost);
            SetColor(material, Color, GameplayVisualPalette.ProjectileGhost);
            SetColor(
                material,
                LineColor,
                GameplayVisualPalette.ProjectileGhostLine);
            SetColor(
                material,
                FillColor,
                GameplayVisualPalette.ProjectileGhostFill);
            return material;
        }

        private static void ApplyGhostStyle(GameObject root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                int materialCount = ResolveSubMeshCount(renderer);
                var materials = new Material[materialCount];
                for (int index = 0; index < materialCount; index++)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
                renderer.SetPropertyBlock(null);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static int ResolveSubMeshCount(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer
                && skinnedRenderer.sharedMesh != null)
            {
                return Mathf.Max(1, skinnedRenderer.sharedMesh.subMeshCount);
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null && meshFilter.sharedMesh != null
                ? Mathf.Max(1, meshFilter.sharedMesh.subMeshCount)
                : Mathf.Max(1, renderer.sharedMaterials.Length);
        }

        private static void SetColor(Material material, int property, Color color)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, color);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ProjectileFlightPresenter));
            }
        }

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);

        private readonly struct ScheduledAdvance
        {
            public ScheduledAdvance(
                ProjectileAdvanceRecord advance,
                float durationSeconds)
            {
                Advance = advance;
                DurationSeconds = durationSeconds;
            }

            public ProjectileAdvanceRecord Advance { get; }

            public float DurationSeconds { get; }
        }
    }
}
