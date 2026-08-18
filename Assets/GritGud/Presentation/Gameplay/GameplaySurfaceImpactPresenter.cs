using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class GameplaySurfaceImpactPresenter : MonoBehaviour
    {
        private static readonly int DecalColor = Shader.PropertyToID("_Color");

        private GameplayAttackController attacks;
        private GameplayWorldRegistry registry;
        private SurfacePresentationCatalog catalog;
        private WeaponPresentationCatalog weapons;
        private GameObject root;
        private Material decalMaterial;

        public void Bind(
            GameplayAttackController attackController,
            GameplayWorldRegistry worldRegistry,
            SurfacePresentationCatalog surfaceCatalog,
            Transform parent,
            WeaponPresentationCatalog weaponCatalog = null)
        {
            if (attackController == null)
            {
                throw new ArgumentNullException(nameof(attackController));
            }
            if (worldRegistry == null)
            {
                throw new ArgumentNullException(nameof(worldRegistry));
            }
            if (surfaceCatalog == null)
            {
                throw new ArgumentNullException(nameof(surfaceCatalog));
            }
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            Unbind();
            attacks = attackController;
            registry = worldRegistry;
            catalog = surfaceCatalog;
            weapons = weaponCatalog ?? WeaponPresentationCatalog.LoadDefault();
            root = new GameObject("Gameplay Surface Impacts");
            root.transform.SetParent(parent, false);
            attacks.AttackResolved += HandleAttackResolved;
            attacks.WeaponDischarged += HandleWeaponDischarged;
            enabled = true;
        }

        public void Unbind()
        {
            if (attacks != null)
            {
                attacks.AttackResolved -= HandleAttackResolved;
                attacks.WeaponDischarged -= HandleWeaponDischarged;
            }

            attacks = null;
            registry = null;
            catalog = null;
            weapons = null;
            GameplayObjectLifecycle.Destroy(root);
            GameplayObjectLifecycle.Destroy(decalMaterial);
            root = null;
            decalMaterial = null;
            enabled = false;
        }

        private void HandleAttackResolved(GameplayActionRecord action)
        {
            if (!TryGetAttackResolution(action, out AttackResolutionRecord resolution)
                || !resolution.Hit
                || !registry.TryGetActor(resolution.TargetId, out GameplayActorView target))
            {
                return;
            }

            Vector3 position = ResolveActorImpactPosition(target, resolution.HitRegion);
            Vector3 attackerPosition = registry.TryGetActor(
                    resolution.AttackerId,
                    out GameplayActorView attacker)
                ? attacker.Transform.position + Vector3.up
                : position - Vector3.forward;
            Vector3 normal = (attackerPosition - position).normalized;
            PresentImpact(
                SurfacePresentationCatalog.ActorSurfaceId,
                position,
                normal.sqrMagnitude > 0.0001f ? normal : Vector3.up,
                createDecal: false,
                ResolveImpactScaleMultiplier(resolution.AttackerId));
        }

        private void HandleWeaponDischarged(GameplayActionRecord action)
        {
            if (!TryGetWeaponDischarge(action, out WeaponDischargeRecord discharge))
            {
                return;
            }

            Vector3 origin = ToVector3(discharge.Origin);
            Vector3 aimPoint = ToVector3(discharge.AimPoint);
            Vector3 direction = aimPoint - origin;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            string surfaceId = discharge.Impact?.SurfaceId
                ?? ResolveSurfaceId(discharge.TargetId);
            Vector3 position = aimPoint;
            Vector3 normal = discharge.Impact == null
                ? -direction.normalized
                : new Vector3(
                    discharge.Impact.NormalX,
                    discharge.Impact.NormalY,
                    discharge.Impact.NormalZ).normalized;
            if (discharge.Impact == null
                && Physics.Raycast(
                    origin,
                    direction.normalized,
                    out RaycastHit hit,
                    direction.magnitude + 0.15f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                normal = hit.normal;
                if (registry.TryGetLevelEntityContaining(
                        hit.collider.transform,
                        out LevelEntityView entity))
                {
                    surfaceId = entity.Archetype.SurfacePresentationId;
                }
            }

            PresentImpact(
                surfaceId,
                position,
                normal,
                createDecal: true,
                ResolveImpactScaleMultiplier(discharge.AttackerId));
        }

        private string ResolveSurfaceId(string targetId)
        {
            return registry.TryGetLevelEntity(targetId, out LevelEntityView entity)
                ? entity.Archetype.SurfacePresentationId
                : SurfacePresentationCatalog.DefaultSurfaceId;
        }

        private void PresentImpact(
            string surfaceId,
            Vector3 position,
            Vector3 normal,
            bool createDecal,
            float scaleMultiplier)
        {
            SurfacePresentationDefinition definition = catalog.Get(surfaceId);
            if (definition.ImpactEffectPrefab != null)
            {
                Quaternion orientation = Quaternion.LookRotation(normal)
                    * definition.ImpactRotation;
                GameObject effect = Instantiate(
                    definition.ImpactEffectPrefab,
                    position + (normal * 0.012f),
                    orientation,
                    root.transform);
                effect.name = definition.SurfaceId + " Impact";
                effect.transform.localScale =
                    definition.ImpactEffectPrefab.transform.localScale
                    * definition.ImpactScale
                    * scaleMultiplier;
                foreach (ParticleSystem particles in
                    effect.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particles.Play(withChildren: true);
                }
                Destroy(effect, definition.ImpactLifetimeSeconds);
            }

            if (createDecal && definition.DecalDiameter > 0f)
            {
                CreateDecal(definition, position, normal);
            }
        }

        private float ResolveImpactScaleMultiplier(string attackerId)
        {
            string equippedItemId = attacks?.Session?.GetActor(attackerId)
                .EquippedItemId;
            return equippedItemId != null
                && weapons != null
                && weapons.TryGet(
                    equippedItemId,
                    out WeaponPresentationDefinition weapon)
                ? weapon.ImpactEffectScaleMultiplier
                : 1f;
        }

        private void CreateDecal(
            SurfacePresentationDefinition definition,
            Vector3 position,
            Vector3 normal)
        {
            if (decalMaterial == null)
            {
                Shader shader = Shader.Find("GritGud/SurfaceDecal");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "The surface-decal shader could not be loaded.");
                }

                decalMaterial = new Material(shader)
                {
                    name = "Gameplay Surface Decals",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decal.name = definition.SurfaceId + " Impact Mark";
            decal.transform.SetParent(root.transform, false);
            decal.transform.SetPositionAndRotation(
                position + (normal * 0.006f),
                Quaternion.LookRotation(normal));
            decal.transform.localScale = Vector3.one * definition.DecalDiameter;
            Collider collider = decal.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                GameplayObjectLifecycle.Destroy(collider);
            }
            MeshRenderer renderer = decal.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = decalMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor(DecalColor, definition.DecalColor);
            renderer.SetPropertyBlock(properties);
            Destroy(decal, definition.DecalLifetimeSeconds);
        }

        private static Vector3 ResolveActorImpactPosition(
            GameplayActorView actor,
            TargetRegionId? region)
        {
            TargetRegionId requested = region ?? TargetRegionId.Torso;
            IReadOnlyList<ActorTargetRegionSample> samples =
                actor.TargetProfile.GetTargetRegionSamples();
            foreach (ActorTargetRegionSample sample in samples)
            {
                if (sample.Id == requested)
                {
                    return sample.WorldCenter;
                }
            }

            return actor.Transform.position + Vector3.up;
        }

        private static bool TryGetAttackResolution(
            GameplayActionRecord action,
            out AttackResolutionRecord resolution)
        {
            if (action != null
                && action.Outcomes.Count == 1
                && action.Outcomes[0] is AttackResolvedActionOutcome outcome)
            {
                resolution = outcome.Attack;
                return true;
            }

            resolution = null;
            return false;
        }

        private static bool TryGetWeaponDischarge(
            GameplayActionRecord action,
            out WeaponDischargeRecord discharge)
        {
            if (action != null
                && action.Outcomes.Count == 1
                && action.Outcomes[0] is WeaponDischargedActionOutcome outcome)
            {
                discharge = outcome.Discharge;
                return true;
            }

            discharge = null;
            return false;
        }

        private static Vector3 ToVector3(GameplayPosition value) =>
            new Vector3(value.X, value.Y, value.Z);

        private void OnDestroy() => Unbind();
    }
}
