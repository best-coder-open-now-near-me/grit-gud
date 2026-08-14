using System.Collections.Generic;
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
                new GameplayPosition(0f, 0f, 0f));

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
                new GameplayPosition(0f, 0f, 0f));

            Assert.That(result.Accepted, Is.False);
            Assert.That(
                result.FailureCode,
                Is.EqualTo("displacement.path-blocked"));
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
