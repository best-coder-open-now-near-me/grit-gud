using System.Linq;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayVisualPresentationCatalogTests
    {
        [Test]
        public void DefaultThemeOwnsGlobalVisualCadence()
        {
            GameplayVisualTheme theme = GameplayVisualTheme.LoadDefault();

            Assert.That(theme.PostProcessing.BloomIntensity, Is.GreaterThan(0f));
            Assert.That(theme.CelSurface.Softness, Is.GreaterThan(0f));
            Assert.That(theme.Outlines.EnvironmentWidth, Is.GreaterThan(0f));
            Assert.That(theme.Grounding.Enabled, Is.True);
            Assert.That(
                theme.TacticalTransition.DurationSeconds,
                Is.GreaterThan(0f));
            Assert.That(
                theme.TacticalTransition.CombatEntryDelaySeconds,
                Is.EqualTo(2f));
        }

        [Test]
        public void DressingCatalogOwnsPortableAmbientEffectReferences()
        {
            LevelDressingCatalog catalog = LevelDressingCatalog.LoadDefault();

            Assert.That(catalog.AmbientEffects.Count, Is.EqualTo(2));
            Assert.That(
                catalog.AmbientEffects.All(effect => effect.Prefab != null),
                Is.True);
            Assert.That(
                catalog.TryGetAmbientEffect("dust-air", out _),
                Is.True);
            Assert.That(
                catalog.TryGetAmbientEffect("ground-haze", out _),
                Is.True);
        }

        [Test]
        public void SurfaceCatalogSeparatesMaterialAndImpactResponse()
        {
            SurfacePresentationCatalog catalog =
                SurfacePresentationCatalog.LoadDefault();
            SurfacePresentationDefinition concrete = catalog.Get("surface.concrete");
            SurfacePresentationDefinition metal = catalog.Get("surface.metal");
            SurfacePresentationDefinition actor = catalog.Get("surface.actor");

            Assert.That(metal.Smoothness, Is.GreaterThan(concrete.Smoothness));
            Assert.That(
                metal.SpecularStrength,
                Is.GreaterThan(concrete.SpecularStrength));
            Assert.That(concrete.ImpactEffectPrefab, Is.Not.Null);
            Assert.That(metal.ImpactEffectPrefab, Is.Not.Null);
            Assert.That(actor.ImpactEffectPrefab, Is.Not.Null);
            Assert.That(actor.DecalDiameter, Is.Zero);
        }

        [Test]
        public void RifleUsesACompactWideSurfaceImpactEffect()
        {
            WeaponPresentationCatalog weapons =
                WeaponPresentationCatalog.LoadDefault();

            Assert.That(
                weapons.Get("weapon.rifle").ImpactEffectScaleMultiplier,
                Is.EqualTo(0.2f));
            Assert.That(
                weapons.Get("weapon.rifle").ImpactEffectWidthMultiplier,
                Is.EqualTo(4f));
            Assert.That(
                weapons.Get("weapon.rocket-launcher")
                    .ImpactEffectScaleMultiplier,
                Is.EqualTo(1f));
            Assert.That(
                weapons.Get("weapon.rocket-launcher")
                    .ImpactEffectWidthMultiplier,
                Is.EqualTo(1f));
        }

        [Test]
        public void InstantiatedRifleImpactAppliesWidthAfterClearingParticles()
        {
            SurfacePresentationDefinition surface =
                SurfacePresentationCatalog.LoadDefault().Get("surface.concrete");
            WeaponPresentationDefinition rifle =
                WeaponPresentationCatalog.LoadDefault().Get("weapon.rifle");
            var root = new GameObject("Impact Scale Test Root");
            GameObject effect = null;
            try
            {
                effect = GameplaySurfaceImpactPresenter.CreateImpactVisual(
                    surface,
                    Vector3.zero,
                    Quaternion.identity,
                    root.transform,
                    rifle.ImpactEffectScaleMultiplier,
                    rifle.ImpactEffectWidthMultiplier);

                Vector3 uniformScale =
                    surface.ImpactEffectPrefab.transform.localScale
                    * surface.ImpactScale
                    * 0.2f;
                Vector3 expectedScale = Vector3.Scale(
                    uniformScale,
                    new Vector3(4f, 4f, 1f));
                Assert.That(
                    Vector3.Distance(effect.transform.localScale, expectedScale),
                    Is.LessThan(0.000001f));
                ParticleSystem[] systems = effect.GetComponentsInChildren<
                    ParticleSystem>(true);
                Assert.That(systems, Is.Not.Empty);
                foreach (ParticleSystem particles in systems)
                {
                    Assert.That(particles.main.playOnAwake, Is.False);
                    Assert.That(
                        particles.main.scalingMode,
                        Is.EqualTo(ParticleSystemScalingMode.Hierarchy));
                }
            }
            finally
            {
                if (effect != null)
                    Object.DestroyImmediate(effect);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SmokeGrenadeOwnsSparsePersistentPresentation()
        {
            ThrownExplosivePresentationDefinition smoke =
                ConsumablePresentationCatalog.LoadDefault()
                    .GetThrownExplosive("item.smoke-grenade");

            Assert.That(smoke.ProjectilePrefab, Is.Not.Null);
            Assert.That(smoke.ImpactEffectPrefab, Is.Null);
            Assert.That(smoke.PersistentAreaEffectPrefab, Is.Not.Null);
            Assert.That(smoke.PersistentEffectScalePerRadius,
                Is.GreaterThan(0f));
            Assert.That(smoke.PersistentParticleEmissionMultiplier,
                Is.InRange(0.1f, 0.5f));
            Assert.That(smoke.HideParticlesWhenCameraInside, Is.True);
            Assert.That(smoke.PersistentParticlesCastShadows, Is.False);
            Assert.That(smoke.PersistentParticlesReceiveShadows, Is.True);
            Assert.That(smoke.InsideOverlayMaximumAlpha,
                Is.InRange(0.05f, 0.15f));
        }

        [Test]
        public void EveryLevelArchetypeSelectsAnAvailableSurface()
        {
            LevelArchetypeCatalog archetypes = LevelArchetypeCatalog.LoadDefault();
            SurfacePresentationCatalog surfaces =
                SurfacePresentationCatalog.LoadDefault();

            Assert.That(archetypes.Entries, Is.Not.Empty);
            foreach (LevelArchetypeDefinition archetype in archetypes.Entries)
            {
                Assert.That(
                    surfaces.TryGet(archetype.SurfacePresentationId, out _),
                    Is.True,
                    archetype.ArchetypeId);
            }
        }

        [Test]
        public void DefaultBreakableCoverOwnsStableBakedFractureProfiles()
        {
            LevelArchetypeCatalog archetypes = LevelArchetypeCatalog.LoadDefault();
            foreach (string archetypeId in new[]
            {
                "prop.crate.standard",
                "prop.barrel.metal",
            })
            {
                Assert.That(archetypes.TryGet(archetypeId, out var archetype),
                    Is.True);
                DestructibleFractureProfile fracture = archetype.FractureProfile;
                Assert.That(fracture, Is.Not.Null, archetypeId);
                Assert.That(fracture.ChunkCount, Is.EqualTo(12), archetypeId);
                Assert.That(fracture.FracturedPrefab, Is.Not.Null, archetypeId);
                DestructibleFractureChunk[] chunks = fracture.FracturedPrefab
                    .GetComponentsInChildren<DestructibleFractureChunk>(true);
                Assert.That(chunks.Length, Is.EqualTo(fracture.ChunkCount));
                Assert.That(
                    chunks.Select(chunk => chunk.ChunkIndex).Distinct().Count(),
                    Is.EqualTo(fracture.ChunkCount));
                Assert.That(
                    chunks.All(chunk =>
                        chunk.GetComponent<MeshCollider>()?.convex == true),
                    Is.True);
                var spatial = fracture.CreateSpatialProfile();
                Assert.That(spatial.ProfileId, Is.EqualTo(fracture.ProfileId));
                Assert.That(spatial.ChunkCount, Is.EqualTo(fracture.ChunkCount));
                Assert.That(
                    spatial.ChunkVolumes.All(volume =>
                        volume.Size.X > 0f
                        && volume.Size.Y > 0f
                        && volume.Size.Z > 0f),
                    Is.True,
                    archetypeId);
            }
        }

        [Test]
        public void ActorCatalogOwnsPrefabAndInitialInputPolicy()
        {
            ActorPresentationCatalog catalog =
                ActorPresentationCatalog.LoadDefault();

            ActorPresentationDefinition player = catalog.Get(
                ActorPresentationIds.DefaultPlayer);
            ActorPresentationDefinition rifleman = catalog.Get(
                ActorPresentationIds.RiflemanEnemy);

            Assert.That(player.Prefab, Is.Not.Null);
            Assert.That(player.MovementInputEnabled, Is.True);
            Assert.That(rifleman.Prefab, Is.Not.Null);
            Assert.That(rifleman.MovementInputEnabled, Is.False);
        }

    }
}
