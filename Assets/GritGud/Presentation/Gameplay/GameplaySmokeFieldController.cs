using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplaySmokeFieldController : MonoBehaviour
    {
        private sealed class SmokeVisual
        {
            private readonly ParticleSystem[] particles;
            private readonly ParticleSystemRenderer[] renderers;

            public SmokeVisual(
                SmokeFieldRecord field,
                ThrownExplosivePresentationDefinition presentation,
                GameObject root)
            {
                Field = field;
                Presentation = presentation;
                Root = root;
                particles = root.GetComponentsInChildren<ParticleSystem>(true);
                renderers = root.GetComponentsInChildren<
                    ParticleSystemRenderer>(true);
            }

            public SmokeFieldRecord Field { get; }

            public ThrownExplosivePresentationDefinition Presentation
            {
                get;
            }

            public GameObject Root { get; }

            public float FadeSecondsRemaining { get; set; } = -1f;

            public float ActivationSecondsRemaining { get; set; }

            public bool IsFading => FadeSecondsRemaining >= 0f;

            public bool IsActive => Root != null && Root.activeSelf;

            public void SetParticlesVisible(bool visible)
            {
                foreach (ParticleSystemRenderer renderer in renderers)
                {
                    if (renderer != null)
                        renderer.enabled = visible;
                }
            }

            public void StopEmission()
            {
                foreach (ParticleSystem system in particles)
                {
                    if (system != null)
                    {
                        system.Stop(
                            true,
                            ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
        }

        private readonly Dictionary<string, SmokeVisual> visuals =
            new Dictionary<string, SmokeVisual>(StringComparer.Ordinal);
        private readonly List<string> completedFades = new List<string>();
        private GameplaySmokeFieldSession smokeFields;
        private ConsumablePresentationCatalog presentationCatalog;
        private float insideOverlayAlpha;
        private Color insideOverlayColor;

        internal int ActiveVisualCount => visuals.Count;

        internal void Bind(
            GameplaySmokeFieldSession fields,
            ConsumablePresentationCatalog presentation = null)
        {
            Unbind();
            smokeFields = fields ?? throw new ArgumentNullException(
                nameof(fields));
            presentationCatalog = presentation
                ?? ConsumablePresentationCatalog.LoadDefault();
            smokeFields.FieldDeployed += HandleFieldDeployed;
            smokeFields.FieldExpired += HandleFieldExpired;
            foreach (SmokeFieldSnapshot snapshot
                in smokeFields.CaptureActiveFields())
            {
                CreateVisual(snapshot.Field);
            }
            enabled = true;
        }

        internal void Unbind()
        {
            if (smokeFields != null)
            {
                smokeFields.FieldDeployed -= HandleFieldDeployed;
                smokeFields.FieldExpired -= HandleFieldExpired;
            }

            foreach (SmokeVisual visual in visuals.Values)
                GameplayObjectLifecycle.Destroy(visual.Root);
            visuals.Clear();
            completedFades.Clear();
            smokeFields = null;
            presentationCatalog = null;
            insideOverlayAlpha = 0f;
            enabled = false;
        }

        private void Update()
        {
            if (smokeFields == null)
                return;

            smokeFields.AdvanceContinuousTime(Time.unscaledDeltaTime);
            UpdateFades(Time.unscaledDeltaTime);
            UpdateCameraInterior(Camera.main);
        }

        private void OnGUI()
        {
            if (insideOverlayAlpha <= 0.001f
                || Event.current.type != EventType.Repaint)
                return;

            Color previous = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = 1000;
            GUI.color = new Color(
                insideOverlayColor.r,
                insideOverlayColor.g,
                insideOverlayColor.b,
                insideOverlayAlpha);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.depth = previousDepth;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleFieldDeployed(SmokeFieldRecord field)
        {
            CreateVisual(field, delayForThrow: true);
        }

        private void HandleFieldExpired(SmokeFieldRecord field)
        {
            if (!visuals.TryGetValue(field.Id, out SmokeVisual visual))
                return;

            visual.StopEmission();
            visual.FadeSecondsRemaining =
                visual.Presentation.PersistentEffectFadeSeconds;
        }

        private void CreateVisual(
            SmokeFieldRecord field,
            bool delayForThrow = false)
        {
            if (visuals.ContainsKey(field.Id))
                throw new InvalidOperationException(
                    $"Smoke visual '{field.Id}' is already active.");

            ThrownExplosivePresentationDefinition presentation =
                presentationCatalog.GetThrownExplosive(field.SourceItemId);
            GameObject prefab = presentation.PersistentAreaEffectPrefab;
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Smoke presentation '{field.SourceItemId}' requires "
                    + "a persistent-area effect prefab.");
            }

            Vector3 origin = ToVector3(field.Origin) + Vector3.up * 0.03f;
            GameObject root = Instantiate(
                prefab,
                origin,
                Quaternion.identity,
                transform);
            root.name = field.Id + " Smoke Volume";
            float scale = field.Definition.Radius
                * presentation.PersistentEffectScalePerRadius;
            root.transform.localScale = Vector3.Scale(
                prefab.transform.localScale,
                Vector3.one * scale);

            ParticleSystem[] systems = root.GetComponentsInChildren<
                ParticleSystem>(true);
            uint seed = StableSeed(field.Id);
            ConfigureParticleSystems(
                systems,
                seed,
                presentation.PersistentParticleEmissionMultiplier);

            foreach (ParticleSystemRenderer renderer
                in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                renderer.shadowCastingMode =
                    presentation.PersistentParticlesCastShadows
                        ? UnityEngine.Rendering.ShadowCastingMode.On
                        : UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows =
                    presentation.PersistentParticlesReceiveShadows;
            }

            var visual = new SmokeVisual(field, presentation, root)
            {
                ActivationSecondsRemaining = delayForThrow
                    ? presentation.ImpactDelaySeconds
                    : 0f,
            };
            bool activateImmediately = visual.ActivationSecondsRemaining <= 0f;
            root.SetActive(activateImmediately);
            if (activateImmediately)
                PlayParticles(systems);
            visuals.Add(field.Id, visual);
        }

        internal static void ConfigureParticleSystems(
            IReadOnlyList<ParticleSystem> systems,
            uint seed,
            float emissionMultiplier)
        {
            if (systems == null)
                throw new ArgumentNullException(nameof(systems));

            foreach (ParticleSystem system in systems)
            {
                if (system == null)
                    continue;
                system.Stop(
                    false,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            for (int index = 0; index < systems.Count; index++)
            {
                ParticleSystem system = systems[index];
                if (system == null)
                    continue;

                uint systemSeed = unchecked(
                    seed + ((uint)index * 104729u));
                system.useAutoRandomSeed = false;
                system.randomSeed = systemSeed == 0u ? 1u : systemSeed;
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
                main.useUnscaledTime = true;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTimeMultiplier *= emissionMultiplier;
            }
        }

        private static void PlayParticles(
            IReadOnlyList<ParticleSystem> systems)
        {
            foreach (ParticleSystem system in systems)
            {
                if (system != null)
                    system.Play(false);
            }
        }

        private void UpdateFades(float deltaTime)
        {
            completedFades.Clear();
            foreach (KeyValuePair<string, SmokeVisual> entry in visuals)
            {
                SmokeVisual visual = entry.Value;
                if (!visual.IsActive
                    && visual.ActivationSecondsRemaining > 0f)
                {
                    visual.ActivationSecondsRemaining -= Mathf.Max(
                        0f,
                        deltaTime);
                    if (visual.ActivationSecondsRemaining <= 0f)
                    {
                        visual.Root.SetActive(true);
                        foreach (ParticleSystem particle
                            in visual.Root.GetComponentsInChildren<
                                ParticleSystem>(true))
                        {
                            particle.Play(false);
                        }
                    }
                }

                if (!visual.IsFading)
                    continue;

                visual.FadeSecondsRemaining -= Mathf.Max(0f, deltaTime);
                if (visual.FadeSecondsRemaining <= 0f)
                    completedFades.Add(entry.Key);
            }

            foreach (string id in completedFades)
            {
                GameplayObjectLifecycle.Destroy(visuals[id].Root);
                visuals.Remove(id);
            }
            completedFades.Clear();
        }

        private void UpdateCameraInterior(Camera camera)
        {
            insideOverlayAlpha = 0f;
            if (camera == null)
            {
                foreach (SmokeVisual visual in visuals.Values)
                    visual.SetParticlesVisible(true);
                return;
            }

            foreach (SmokeVisual visual in visuals.Values)
            {
                float depth = 0f;
                bool inside = visual.IsActive
                    && !visual.IsFading
                    && TryGetInteriorDepth(
                        camera.transform.position,
                        visual.Field,
                        out depth);
                visual.SetParticlesVisible(
                    !inside
                    || !visual.Presentation.HideParticlesWhenCameraInside);
                if (!inside)
                    continue;

                float alpha = Mathf.SmoothStep(0f, 1f, depth)
                    * visual.Presentation.InsideOverlayMaximumAlpha;
                if (alpha > insideOverlayAlpha)
                {
                    insideOverlayAlpha = alpha;
                    insideOverlayColor =
                        visual.Presentation.InsideOverlayColor;
                }
            }
        }

        private static bool TryGetInteriorDepth(
            Vector3 position,
            SmokeFieldRecord field,
            out float depth)
        {
            Vector3 origin = ToVector3(field.Origin);
            SmokeFieldDefinition definition = field.Definition;
            float vertical = position.y - origin.y;
            if (vertical < 0f || vertical > definition.Height)
            {
                depth = 0f;
                return false;
            }

            Vector2 horizontal = new Vector2(
                position.x - origin.x,
                position.z - origin.z);
            float radialDepth = 1f
                - (horizontal.magnitude / definition.Radius);
            if (radialDepth <= 0f)
            {
                depth = 0f;
                return false;
            }

            float verticalDepth = Mathf.Min(
                vertical,
                definition.Height - vertical)
                / Mathf.Max(0.001f, definition.Height * 0.5f);
            depth = Mathf.Clamp01(Mathf.Min(radialDepth, verticalDepth));
            return true;
        }

        private static uint StableSeed(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value)
                {
                    hash ^= character;
                    hash *= 16777619;
                }
                return hash == 0 ? 1u : hash;
            }
        }

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);
    }
}
