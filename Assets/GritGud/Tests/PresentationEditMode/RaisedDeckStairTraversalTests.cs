using System.Linq;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class RaisedDeckStairTraversalTests
    {
        private LevelWorld world;
        private GameObject actor;

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
            world = null;
            if (actor != null)
            {
                Object.DestroyImmediate(actor);
                actor = null;
            }
        }

        [Test]
        public void MainLevelAuthorsTwoFlightRunsToDeckHeight()
        {
            LevelDocument document = LoadMainLevelDocument();

            AssertFlight(
                document,
                "deck-stairs-south",
                new Float3Data(5f, 0f, -2.5f),
                180f);
            AssertFlight(
                document,
                "deck-stairs-south-upper",
                new Float3Data(5f, 1.5f, 0f),
                180f);
            AssertFlight(
                document,
                "deck-stairs-west",
                new Float3Data(0f, 0f, 10f),
                -90f);
            AssertFlight(
                document,
                "deck-stairs-west-upper",
                new Float3Data(2.5f, 1.5f, 10f),
                -90f);
            Assert.That(
                document.entities.Any(entity => entity.id == "workshop-cross-03"),
                Is.False,
                "The former divider wall occupied the south stair connection.");
        }

        [Test]
        public void CharacterControllerCanClimbSouthRunOntoRaisedDeck()
        {
            CharacterController controller = CreateTraversalActor(
                new Vector3(6.25f, 0.02f, -2.85f));

            for (int step = 0; step < 125; step++)
            {
                controller.Move(new Vector3(0f, -0.03f, 0.05f));
            }

            Assert.That(actor.transform.position.z, Is.GreaterThan(2.5f));
            Assert.That(actor.transform.position.y, Is.InRange(2.9f, 3.2f));
        }

        [Test]
        public void CharacterControllerCanClimbWestRunOntoRaisedDeck()
        {
            CharacterController controller = CreateTraversalActor(
                new Vector3(-0.35f, 0.02f, 8.75f));

            for (int step = 0; step < 125; step++)
            {
                controller.Move(new Vector3(0.05f, -0.03f, 0f));
            }

            Assert.That(actor.transform.position.x, Is.GreaterThan(5f));
            Assert.That(actor.transform.position.y, Is.InRange(2.9f, 3.2f));
        }

        private static LevelDocument LoadMainLevelDocument()
        {
            TextAsset source = Resources.Load<TextAsset>("Levels/main-level");
            Assert.That(source, Is.Not.Null);
            return new UnityLevelJsonSerializer().Deserialize(source.text);
        }

        private CharacterController CreateTraversalActor(Vector3 start)
        {
            LevelDocument document = LoadMainLevelDocument();
            world = new LevelLoader(LevelArchetypeCatalog.LoadDefault()).Load(document);
            actor = new GameObject("Raised Deck Stair Test Actor");
            CharacterController controller = actor.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;
            actor.transform.position = start;
            Physics.SyncTransforms();
            return controller;
        }

        private static void AssertFlight(
            LevelDocument document,
            string entityId,
            Float3Data expectedPosition,
            float expectedYaw)
        {
            LevelEntity entity = document.entities.Single(candidate =>
                candidate.id == entityId);
            Assert.That(entity.archetypeId, Is.EqualTo("structure.stairs.standard"));
            Assert.That(entity.transform.position.x,
                Is.EqualTo(expectedPosition.x).Within(0.001f));
            Assert.That(entity.transform.position.y,
                Is.EqualTo(expectedPosition.y).Within(0.001f));
            Assert.That(entity.transform.position.z,
                Is.EqualTo(expectedPosition.z).Within(0.001f));
            Assert.That(entity.transform.yawDegrees,
                Is.EqualTo(expectedYaw).Within(0.001f));
        }
    }
}
