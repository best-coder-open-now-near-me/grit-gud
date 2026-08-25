using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayFireFieldControllerTests
    {
        [Test]
        public void AuthoritativeRadiusDrivesNonPhysicalVisualScale()
        {
            var host = new GameObject("Fire Controller Host");
            var projectile = new GameObject("Incendiary Prefab");
            var persistent = new GameObject("Fire Prefab");
            persistent.AddComponent<BoxCollider>();
            var catalog = ConsumablePresentationCatalog.CreateRuntime(
                new ThrownExplosivePresentationDefinition(
                    "item.incendiary-grenade",
                    projectile,
                    Vector3.zero,
                    1f,
                    Vector3.one,
                    0.2f,
                    0.8f,
                    3f,
                    0.035f,
                    0.035f,
                    0.018f,
                    Color.yellow,
                    Color.red,
                    null,
                    Vector3.zero,
                    0f,
                    0.2f,
                    persistent,
                    persistentScalePerRadius: 0.5f));
            GameplaySession gameplay = CreateGameplay();
            var destructibles = new DestructiblePropSession(
                Array.Empty<DestructiblePropDefinition>(),
                gameplay.Journal);
            using var fire = new GameplayFireFieldSession(
                gameplay,
                destructibles);
            GameplayFireFieldController controller =
                host.AddComponent<GameplayFireFieldController>();

            try
            {
                controller.Bind(fire, catalog);
                fire.Deploy(CreateField());
                Transform visual = host.transform.GetChild(0);
                Assert.That(controller.ActiveVisualCount, Is.EqualTo(1));
                Assert.That(visual.localScale.x, Is.EqualTo(0.5f));
                Assert.That(visual.GetComponent<BoxCollider>().enabled, Is.False);

                fire.AdvanceContinuousTime(2f);

                Assert.That(visual.localScale.x, Is.GreaterThan(0.5f));
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
                new GameplayActorPose(new GameplayPosition(4f, 0f, 0f), 0f),
                new TurnBudget(4, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "fire-presentation-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                Array.Empty<ScenarioObjectiveDefinition>(),
                Array.Empty<AttackResponseDefinition>()));
        }

        private static FireFieldRecord CreateField() => new FireFieldRecord(
            "fire.player.1",
            "player",
            "item.incendiary-grenade",
            new GameplayPosition(0f, 0f, 0f),
            new FireFieldDefinition(1f, 2f, 2f, 6f, 3, 2f, 1f, 1f, 0.5f));
    }
}
