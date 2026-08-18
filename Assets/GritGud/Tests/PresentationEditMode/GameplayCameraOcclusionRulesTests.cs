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
        public void CutoutWallOnlyMovesCameraWhenDesiredPositionTouchesIt()
        {
            var wall = new GameObject("Camera Cutout Wall");
            try
            {
                var view = wall.AddComponent<LevelEntityView>();
                view.Apply(new LevelEntity
                {
                    id = "camera-wall",
                    archetypeId = "structure.wall.standard",
                });
                BoxCollider collider = wall.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.2f, 2f, 4f);
                Physics.SyncTransforms();

                Assert.That(
                    GameplayCameraOcclusionRules.ShouldMoveCamera(
                        collider,
                        null,
                        new Vector3(1f, 0f, 0f),
                        0.2f),
                    Is.False,
                    "A wall between the player and camera should use the cutout.");
                Assert.That(
                    GameplayCameraOcclusionRules.ShouldMoveCamera(
                        collider,
                        null,
                        new Vector3(0.15f, 0f, 0f),
                        0.2f),
                    Is.True,
                    "The camera must not settle inside the cutout wall mesh.");
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }
    }
}
