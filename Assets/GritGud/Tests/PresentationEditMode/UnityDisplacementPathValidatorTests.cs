using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class UnityDisplacementPathValidatorTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void ClearContinuousPathIsAllowed()
        {
            GameObject actor = CreateObject("Actor", new Vector3(-1f, 0f, 0f));
            GameObject subject = CreatePrimitive("Subject", Vector3.zero, Vector3.one * 0.5f);
            Physics.SyncTransforms();
            var validator = CreateValidator(actor, subject);
            var request = CreateRequest(new GameplayPosition(2f, 0f, 0f));

            var result = validator.Validate(
                request,
                new GameplayPosition(0f, 0f, 0f),
                resultingPropState: null);

            Assert.That(result.Accepted, Is.True);
        }

        [Test]
        public void ColliderAlongContinuousPathBlocksDisplacement()
        {
            GameObject actor = CreateObject("Actor", new Vector3(-1f, 0f, 0f));
            GameObject subject = CreatePrimitive("Subject", Vector3.zero, Vector3.one * 0.5f);
            CreatePrimitive(
                "Obstacle",
                new Vector3(1f, 0.3f, 0f),
                new Vector3(0.2f, 1f, 1f));
            Physics.SyncTransforms();
            var validator = CreateValidator(actor, subject);
            var request = CreateRequest(new GameplayPosition(2f, 0f, 0f));

            var result = validator.Validate(
                request,
                new GameplayPosition(0f, 0f, 0f),
                resultingPropState: null);

            Assert.That(result.Accepted, Is.False);
            Assert.That(
                result.FailureCode,
                Is.EqualTo("displacement.path-blocked"));
        }

        [Test]
        public void ObstacleInsideToppledFootprintBlocksDestination()
        {
            GameObject actor = CreateObject("Actor", new Vector3(-1f, 0f, 0f));
            GameObject subject = CreatePrimitive(
                "Subject",
                Vector3.zero,
                new Vector3(0.4f, 2f, 0.4f));
            CreatePrimitive(
                "Obstacle",
                new Vector3(2.8f, 0f, 0f),
                new Vector3(0.2f, 0.5f, 0.5f));
            Physics.SyncTransforms();
            var validator = CreateValidator(actor, subject);
            var destination = new GameplayPosition(2f, 0f, 0f);
            var resultingState = new PropDisplacementState(
                new GameplayPropPose(destination, 0f, 0f, 90f),
                DestructiblePropPosture.Toppled);

            DisplacementPathValidation result = validator.Validate(
                CreateRequest(destination),
                new GameplayPosition(0f, 0f, 0f),
                resultingState);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.FailureCode,
                Is.EqualTo("displacement.destination-blocked"));
        }

        [Test]
        public void RegisteredActorInsideToppledFootprintReturnsContactEvidence()
        {
            GameObject actor = CreateObject("Actor", new Vector3(-1f, 0f, 0f));
            GameObject subject = CreatePrimitive(
                "Subject",
                Vector3.zero,
                new Vector3(0.4f, 2f, 0.4f));
            GameObject contactedActor = CreatePrimitive(
                "ContactedActor",
                new Vector3(2.65f, 0.55f, 0f),
                new Vector3(0.5f, 1.1f, 0.5f));
            Physics.SyncTransforms();
            var validator = new UnityDisplacementPathValidator(
                new Dictionary<string, Transform>
                {
                    ["actor"] = actor.transform,
                    ["subject"] = subject.transform,
                    ["contacted"] = contactedActor.transform,
                });
            var destination = new GameplayPosition(2f, 0f, 0f);
            var resultingState = new PropDisplacementState(
                new GameplayPropPose(destination, 0f, 0f, 90f),
                DestructiblePropPosture.Toppled);

            DisplacementPathValidation result = validator.Validate(
                CreateRequest(destination),
                new GameplayPosition(0f, 0f, 0f),
                resultingState);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Contacts.Count, Is.EqualTo(1));
            Assert.That(result.Contacts[0].EntityId, Is.EqualTo("contacted"));
            Assert.That(result.Contacts[0].OverlapDepth, Is.GreaterThan(0f));
        }

        [Test]
        public void PushOffAcceptsClearFinalPropAndStandingVolumes()
        {
            GameObject actor = CreateActorWithController(
                "Actor",
                new Vector3(0f, 0f, -1.5f));
            GameObject subject = CreatePrimitive(
                "Subject",
                Vector3.zero,
                Vector3.one * 0.5f);
            Physics.SyncTransforms();
            var validator = CreateValidator(actor, subject);
            var destination = new GameplayPosition(2f, 0f, 0f);
            var resultingState = new PropDisplacementState(
                new GameplayPropPose(destination, 0f, 0f, 90f),
                DestructiblePropPosture.Toppled);

            DisplacementPathValidation result = validator.Validate(
                CreatePushOffRequest(destination),
                new GameplayPosition(0f, 0f, 0f),
                resultingState);

            Assert.That(result.Accepted, Is.True);
        }

        [Test]
        public void PushOffRejectsObstacleInsideStandingVolume()
        {
            GameObject actor = CreateActorWithController(
                "Actor",
                new Vector3(0f, 0f, -1.5f));
            GameObject subject = CreatePrimitive(
                "Subject",
                Vector3.zero,
                Vector3.one * 0.5f);
            CreatePrimitive(
                "Get Up Obstacle",
                new Vector3(0.25f, 1f, -1.5f),
                new Vector3(0.2f, 1f, 0.2f));
            Physics.SyncTransforms();
            var validator = CreateValidator(actor, subject);
            var destination = new GameplayPosition(2f, 0f, 0f);
            var resultingState = new PropDisplacementState(
                new GameplayPropPose(destination, 0f, 0f, 90f),
                DestructiblePropPosture.Toppled);

            DisplacementPathValidation result = validator.Validate(
                CreatePushOffRequest(destination),
                new GameplayPosition(0f, 0f, 0f),
                resultingState);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.FailureCode,
                Is.EqualTo(
                    DisplacementPathValidation
                        .GetUpSpaceBlockedFailureCode));
        }

        [Test]
        public void PushOffRejectsFinalPropFootprintOverlappingActor()
        {
            GameObject actor = CreateActorWithController(
                "Actor",
                new Vector3(2f, 0f, 0f));
            GameObject subject = CreatePrimitive(
                "Subject",
                Vector3.zero,
                Vector3.one * 0.5f);
            Physics.SyncTransforms();
            var validator = CreateValidator(actor, subject);
            var destination = new GameplayPosition(2f, 0f, 0f);
            var resultingState = new PropDisplacementState(
                new GameplayPropPose(destination, 0f, 0f, 90f),
                DestructiblePropPosture.Toppled);

            DisplacementPathValidation result = validator.Validate(
                CreatePushOffRequest(destination),
                new GameplayPosition(0f, 0f, 0f),
                resultingState);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.FailureCode,
                Is.EqualTo(
                    DisplacementPathValidation
                        .GetUpSpaceBlockedFailureCode));
        }

        [Test]
        public void PushOffPreviewShowsFinalCoverBoundsAndGetUpFootprint()
        {
            GameObject parent = CreateObject("Preview", Vector3.zero);
            GameObject actor = CreateActorWithController(
                "Actor",
                new Vector3(0f, 0f, -1.5f));
            GameObject subject = CreatePrimitive(
                "Subject",
                Vector3.zero,
                new Vector3(0.4f, 2f, 0.6f));
            Physics.SyncTransforms();
            var presenter = new DisplacementPreviewPresenter(
                parent.transform);
            try
            {
                presenter.ShowPushOff(
                    Vector3.zero,
                    new Vector3(2f, 0f, 0f),
                    valid: true,
                    subject.transform,
                    actor.transform);

                Assert.That(presenter.IsPropBoundsVisible, Is.True);
                Assert.That(presenter.IsGetUpRingVisible, Is.True);
                Assert.That(presenter.PropBoundsCenter.x,
                    Is.EqualTo(2f).Within(0.001f));
                Assert.That(presenter.PropBoundsHalfExtents.x,
                    Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(presenter.PropBoundsHalfExtents.y,
                    Is.EqualTo(1f).Within(0.001f));

                presenter.Hide();
                Assert.That(presenter.IsPropBoundsVisible, Is.False);
                Assert.That(presenter.IsGetUpRingVisible, Is.False);
            }
            finally
            {
                presenter.Dispose();
            }
        }

        private static DisplacementRequest CreateRequest(
            GameplayPosition destination) =>
            new DisplacementRequest(
                "actor",
                "close-quarters.throw-prop",
                "subject",
                DisplacementSubjectKind.Prop,
                10f,
                destination);

        private static DisplacementRequest CreatePushOffRequest(
            GameplayPosition destination) =>
            new DisplacementRequest(
                "actor",
                "close-quarters.push-off",
                "subject",
                DisplacementSubjectKind.Prop,
                10f,
                destination,
                DisplacementActionKind.PushOff);

        private static UnityDisplacementPathValidator CreateValidator(
            GameObject actor,
            GameObject subject) =>
            new UnityDisplacementPathValidator(
                new Dictionary<string, Transform>
                {
                    ["actor"] = actor.transform,
                    ["subject"] = subject.transform,
                });

        private GameObject CreateObject(string name, Vector3 position)
        {
            var created = new GameObject(name);
            created.transform.position = position;
            createdObjects.Add(created);
            return created;
        }

        private GameObject CreateActorWithController(
            string name,
            Vector3 position)
        {
            GameObject actor = CreateObject(name, position);
            CharacterController controller =
                actor.AddComponent<CharacterController>();
            controller.center = Vector3.up;
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.skinWidth = 0.08f;
            return actor;
        }

        private GameObject CreatePrimitive(
            string name,
            Vector3 position,
            Vector3 scale)
        {
            GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.name = name;
            created.transform.position = position;
            created.transform.localScale = scale;
            createdObjects.Add(created);
            return created;
        }
    }
}
