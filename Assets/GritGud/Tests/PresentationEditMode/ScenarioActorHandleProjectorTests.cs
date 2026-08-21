using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class ScenarioActorHandleProjectorTests
    {
        [Test]
        public void RefreshUpdatesExistingHandleAndOnlyRemovesMissingActors()
        {
            var root = new GameObject("Scenario Handle Test Root");
            var projector = new ScenarioActorHandleProjector(root.transform);
            try
            {
                LevelDocument document = LevelDocumentFactory.CreateEmpty();
                LevelScenarioActorData player = document.scenario.actors[0];
                projector.Refresh(document);
                Assert.That(projector.TryGetHandle(player.id, out GameObject original), Is.True);

                player.transform = new LevelTransformData(
                    new Float3Data(8f, 1f, -2f),
                    90f);
                document.scenario.actors.Add(new LevelScenarioActorData
                {
                    id = "enemy",
                    templateId = "enemy-template",
                });
                projector.Refresh(document);

                Assert.That(projector.HandleCount, Is.EqualTo(2));
                Assert.That(projector.TryGetHandle(player.id, out GameObject updated), Is.True);
                Assert.That(updated, Is.SameAs(original));
                Assert.That(updated.transform.position.x, Is.EqualTo(8f));

                document.scenario.actors.Remove(player);
                projector.Refresh(document);
                Assert.That(projector.HandleCount, Is.EqualTo(1));
                Assert.That(projector.TryGetHandle(player.id, out _), Is.False);
            }
            finally
            {
                projector.Dispose();
                Object.DestroyImmediate(root);
            }
        }
    }
}
