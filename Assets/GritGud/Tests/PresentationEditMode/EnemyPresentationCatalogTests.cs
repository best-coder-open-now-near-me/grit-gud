using System.Collections.Generic;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class EnemyPresentationCatalogTests
    {
        [Test]
        public void DefaultCatalogDefinesTheDepotRifleman()
        {
            EnemyPresentationCatalog catalog =
                EnemyPresentationCatalog.LoadDefault();

            EnemyPresentationDefinition definition = catalog.Get(
                ActorPresentationIds.RiflemanEnemy);

            Assert.That(definition.PostAttackDelaySeconds,
                Is.EqualTo(0.7f));
            Assert.That(catalog.DetectionIntervalSeconds,
                Is.EqualTo(0.15f));
        }

        [Test]
        public void RuntimeCatalogCarriesAuthoredCadenceAndIncapacitationPose()
        {
            var definition = new EnemyPresentationDefinition(
                "actor.enemy.test",
                decisionDelaySeconds: 0.2f,
                attackDelaySeconds: 0.8f,
                incapacitationRotationEuler: new Vector3(0f, 0f, 70f),
                incapacitationOffset: new Vector3(0f, 0.1f, 0f));
            EnemyPresentationCatalog catalog =
                EnemyPresentationCatalog.CreateRuntime(0.25f, definition);
            try
            {
                EnemyPresentationDefinition resolved = catalog.Get(
                    "actor.enemy.test");

                Assert.That(catalog.DetectionIntervalSeconds,
                    Is.EqualTo(0.25f));
                Assert.That(resolved.PostDecisionDelaySeconds,
                    Is.EqualTo(0.2f));
                Assert.That(resolved.PostAttackDelaySeconds,
                    Is.EqualTo(0.8f));
                Assert.That(resolved.IncapacitationLocalOffset,
                    Is.EqualTo(new Vector3(0f, 0.1f, 0f)));
                Assert.That(resolved.IncapacitationLocalRotation.eulerAngles.z,
                    Is.EqualTo(70f).Within(0.001f));
                Assert.Throws<KeyNotFoundException>(() =>
                    catalog.Get("actor.enemy.missing"));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
