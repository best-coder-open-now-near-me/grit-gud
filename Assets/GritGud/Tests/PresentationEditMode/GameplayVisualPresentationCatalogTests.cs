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
