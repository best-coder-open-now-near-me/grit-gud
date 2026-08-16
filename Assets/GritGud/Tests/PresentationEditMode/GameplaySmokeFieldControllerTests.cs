using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplaySmokeFieldControllerTests
    {
        [Test]
        public void ParticleConfigurationStopsBeforeAssigningDeterministicSeed()
        {
            var root = new GameObject("Playing Smoke Particle");
            ParticleSystem particles = root.AddComponent<ParticleSystem>();

            try
            {
                particles.Play(false);
                Assert.That(particles.isPlaying, Is.True);

                GameplaySmokeFieldController.ConfigureParticleSystems(
                    new[] { particles },
                    123u,
                    0.5f);

                Assert.That(particles.isPlaying, Is.False);
                Assert.That(particles.particleCount, Is.Zero);
                Assert.That(particles.useAutoRandomSeed, Is.False);
                Assert.That(particles.randomSeed, Is.EqualTo(123u));
                Assert.That(particles.main.playOnAwake, Is.False);
                Assert.That(particles.main.useUnscaledTime, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AuthoritativeDeploymentCreatesPersistentVisual()
        {
            var host = new GameObject("Smoke Controller Host");
            var projectile = new GameObject("Smoke Grenade Prefab");
            var persistent = new GameObject("Smoke Volume Prefab");
            persistent.AddComponent<ParticleSystem>();
            var catalog = ConsumablePresentationCatalog.CreateRuntime(
                new ThrownExplosivePresentationDefinition(
                    "item.smoke-grenade",
                    projectile,
                    Vector3.zero,
                    1f,
                    0.55f,
                    0.3f,
                    Vector3.one,
                    0.2f,
                    0.8f,
                    3f,
                    0.035f,
                    0.035f,
                    0.018f,
                    Color.yellow,
                    Color.cyan,
                    null,
                    Vector3.zero,
                    0f,
                    0.65f,
                    persistent,
                    0.5f,
                    0.35f,
                    1.5f));
            GameplaySession gameplay = CreateGameplay();
            using var smoke = new GameplaySmokeFieldSession(gameplay);
            GameplaySmokeFieldController controller =
                host.AddComponent<GameplaySmokeFieldController>();

            try
            {
                controller.Bind(smoke, catalog);
                smoke.Deploy(new SmokeFieldRecord(
                    "smoke.player.1",
                    "player",
                    "item.smoke-grenade",
                    new GameplayPosition(2f, 0f, 3f),
                    new SmokeFieldDefinition(4f, 2.8f, 24f, 4, 0.75f)));

                Assert.That(controller.ActiveVisualCount, Is.EqualTo(1));
                Assert.That(host.transform.childCount, Is.EqualTo(1));
                Assert.That(
                    host.transform.GetChild(0).gameObject.activeSelf,
                    Is.False,
                    "Smoke should wait for the grenade's authored release "
                    + "and flight time.");
                Assert.That(
                    catalog.GetThrownExplosive("item.smoke-grenade")
                        .ImpactDelaySeconds,
                    Is.EqualTo(0.85f).Within(0.001f));

                controller.BeginReplayPresentation();
                Assert.That(controller.ActiveVisualCount, Is.Zero);
                controller.PresentReplay(smoke.CaptureActiveFields());
                Assert.That(controller.ActiveVisualCount, Is.EqualTo(1));
                controller.PresentReplay(
                    System.Array.Empty<SmokeFieldSnapshot>());
                Assert.That(controller.ActiveVisualCount, Is.Zero);
                controller.EndReplayPresentation();
                Assert.That(controller.ActiveVisualCount, Is.EqualTo(1));
                Assert.That(smoke.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                controller.Unbind();
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(projectile);
                UnityEngine.Object.DestroyImmediate(persistent);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        private static GameplaySession CreateGameplay()
        {
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "smoke-presentation-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                Array.Empty<ScenarioObjectiveDefinition>(),
                Array.Empty<AttackResponseDefinition>()));
        }
    }
}
