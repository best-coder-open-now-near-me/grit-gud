using System;
using System.Collections.Generic;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class WeaponActionEffectsPresenter : MonoBehaviour
    {
        private sealed class TransientVisual
        {
            public TransientVisual(
                GameObject root,
                Material material,
                float lifetime)
            {
                Root = root;
                Material = material;
                RemainingSeconds = lifetime;
            }

            public GameObject Root { get; }

            public Material Material { get; }

            public float RemainingSeconds { get; set; }
        }

        private readonly List<TransientVisual> transientVisuals = new();
        private WeaponMountPresenter mount;
        private WeaponPresentationDefinition contactDefinition;
        private float contactStrikeElapsed = -1f;

        internal int TransientVisualCount => transientVisuals.Count;

        internal bool ContactStrikeActive => contactStrikeElapsed >= 0f;

        internal void Bind(WeaponMountPresenter weaponMount)
        {
            Clear();
            mount = weaponMount ?? throw new ArgumentNullException(
                nameof(weaponMount));
        }

        internal void PresentShot(
            WeaponPresentationDefinition definition,
            Vector3 origin,
            Vector3 destination,
            bool drawTracer)
        {
            if (definition == null)
            {
                return;
            }

            // The authored muzzle is the only orientation authority.  The
            // destination is gameplay/tracer information, never a substitute
            // for a weapon model's barrel axis.
            Quaternion rotation = mount?.Muzzle != null
                ? mount.Muzzle.rotation
                : Quaternion.identity;
            CreateMuzzleEffect(definition, origin, rotation);
            CreateMuzzleLight(definition, origin);
            if (drawTracer && definition.InstantTracer)
            {
                CreateTracer(definition, origin, destination);
            }
        }

        internal void PresentContactStrike(
            WeaponPresentationDefinition definition,
            Vector3 destination)
        {
            contactDefinition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            contactStrikeElapsed = 0f;
        }

        internal void Tick(float deltaTime)
        {
            TickTransientVisuals(deltaTime);
            TickContactStrike(deltaTime);
        }

        internal void TickTransientVisuals(float deltaTime)
        {
            float elapsed = Mathf.Max(0f, deltaTime);
            for (int index = transientVisuals.Count - 1; index >= 0; index--)
            {
                TransientVisual visual = transientVisuals[index];
                visual.RemainingSeconds -= elapsed;
                if (visual.RemainingSeconds > 0f)
                {
                    continue;
                }

                DestroyTransientVisual(visual);
                transientVisuals.RemoveAt(index);
            }
        }

        internal void TickContactStrike(float deltaTime)
        {
            if (contactStrikeElapsed < 0f || contactDefinition == null)
            {
                return;
            }

            contactStrikeElapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(
                contactStrikeElapsed / contactDefinition.ContactStrikeSeconds);
            float weight = Mathf.Sin(progress * Mathf.PI);
            mount?.SetContactSwing(
                weight,
                contactDefinition.ContactSwingAxisLocal,
                contactDefinition.ContactSwingDegrees);
            if (progress >= 1f)
            {
                ClearWeaponAction();
            }
        }

        internal void ClearWeaponAction()
        {
            contactStrikeElapsed = -1f;
            contactDefinition = null;
            mount?.SetContactSwing(0f, Vector3.up, 0f);
        }

        internal void Clear()
        {
            ClearWeaponAction();
            foreach (TransientVisual visual in transientVisuals)
            {
                DestroyTransientVisual(visual);
            }

            transientVisuals.Clear();
            mount = null;
        }

        private void CreateMuzzleEffect(
            WeaponPresentationDefinition definition,
            Vector3 position,
            Quaternion rotation)
        {
            if (definition.MuzzleEffectPrefab == null)
            {
                return;
            }

            GameObject effect = Instantiate(
                definition.MuzzleEffectPrefab,
                position,
                rotation,
                transform);
            effect.name = definition.ItemId + " Muzzle Effect";
            foreach (ParticleSystem particles in
                effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Play(true);
            }

            transientVisuals.Add(new TransientVisual(
                effect,
                material: null,
                definition.ShotEffectSeconds));
        }

        private void CreateMuzzleLight(
            WeaponPresentationDefinition definition,
            Vector3 position)
        {
            if (definition.MuzzleLightIntensity <= 0f)
            {
                return;
            }

            var lightRoot = new GameObject(
                definition.ItemId + " Muzzle Light");
            lightRoot.transform.SetParent(transform, false);
            lightRoot.transform.position = position;
            Light light = lightRoot.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = definition.MuzzleLightColor;
            light.intensity = definition.MuzzleLightIntensity;
            light.range = definition.MuzzleLightRange;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            transientVisuals.Add(new TransientVisual(
                lightRoot,
                material: null,
                definition.MuzzleLightSeconds));
        }

        private void CreateTracer(
            WeaponPresentationDefinition definition,
            Vector3 origin,
            Vector3 destination)
        {
            var tracer = new GameObject("Instant Shot Tracer");
            tracer.transform.SetParent(transform, false);
            LineRenderer line = tracer.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, destination);
            line.startWidth = definition.TracerWidth;
            line.endWidth = definition.TracerWidth * 0.35f;
            line.numCapVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            Color color = GameplayVisualPalette.SignalOrangeGlow;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.45f);
            Material material = RuntimeMaterialFactory.CreateColor(
                color,
                "Instant Shot Tracer Material");
            line.sharedMaterial = material;
            transientVisuals.Add(new TransientVisual(
                tracer,
                material,
                definition.ShotEffectSeconds));
        }

        private static void DestroyTransientVisual(TransientVisual visual)
        {
            GameplayObjectLifecycle.Destroy(visual.Root);
            GameplayObjectLifecycle.Destroy(visual.Material);
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
