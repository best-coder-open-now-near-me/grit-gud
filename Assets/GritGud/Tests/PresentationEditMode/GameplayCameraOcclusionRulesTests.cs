using GritGud.Domain.Levels;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayCameraOcclusionRulesTests
    {
        [TestCase("structure.wall.standard")]
        [TestCase("structure.wall.doorway")]
        public void WallArchetypesUsePlayerCutout(string archetypeId)
        {
            Assert.That(
                GameplayCameraOcclusionRules.UsesPlayerCutout(archetypeId),
                Is.True);
        }

        [TestCase("structure.floor.standard")]
        [TestCase("structure.stairs.standard")]
        [TestCase("prop.crate.standard")]
        [TestCase("")]
        [TestCase(null)]
        public void NonWallArchetypesDoNotUsePlayerCutout(string archetypeId)
        {
            Assert.That(
                GameplayCameraOcclusionRules.UsesPlayerCutout(archetypeId),
                Is.False);
        }

        [Test]
        public void PlayerCutoutOnlyEnablesWallIntersectingCameraPath()
        {
            var cameraObject = new GameObject("Cutout Selection Camera");
            var target = new GameObject("Cutout Selection Target");
            GameObject blockingWall = CreateWall(
                "Blocking Wall",
                new Vector3(0f, 1.3f, -1.5f));
            GameObject adjacentWall = CreateWall(
                "Adjacent Wall",
                new Vector3(2f, 1.3f, -1.5f));
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 1.3f, -3f);
                Renderer blockingRenderer =
                    blockingWall.GetComponent<Renderer>();
                Renderer adjacentRenderer =
                    adjacentWall.GetComponent<Renderer>();
                Physics.SyncTransforms();

                var presenter = cameraObject.AddComponent<
                    GameplayPlayerCutoutPresenter>();
                presenter.Bind(
                    camera,
                    target.transform,
                    null,
                    new[] { blockingRenderer, adjacentRenderer });

                Assert.That(presenter.ActiveOccluderCount, Is.EqualTo(1));
                Assert.That(ReadCutoutEnabled(blockingRenderer), Is.EqualTo(1f));
                Assert.That(ReadCutoutEnabled(adjacentRenderer), Is.Zero);

                blockingWall.transform.position =
                    new Vector3(-2f, 1.3f, -1.5f);
                Physics.SyncTransforms();
                presenter.RefreshNow();

                Assert.That(presenter.ActiveOccluderCount, Is.Zero);
                Assert.That(ReadCutoutEnabled(blockingRenderer), Is.Zero);
                Assert.That(ReadCutoutEnabled(adjacentRenderer), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(adjacentWall);
                Object.DestroyImmediate(blockingWall);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static GameObject CreateWall(string name, Vector3 position)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            var view = wall.AddComponent<LevelEntityView>();
            view.Apply(new LevelEntity
            {
                id = name,
                archetypeId = "structure.wall.standard",
            });
            wall.transform.position = position;
            wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
            return wall;
        }

        private static float ReadCutoutEnabled(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetFloat("_PlayerCutoutEnabled");
        }
    }
}
