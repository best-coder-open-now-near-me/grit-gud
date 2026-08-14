using GritGud.Editor;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class DefaultActorAssetValidationTests
    {
        [Test]
        public void GeneratedActorPassesFocusedArtifactValidators()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    DefaultActorAssetGenerator.ControllerPath);
            ActorAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<ActorAnimationProfile>(
                    DefaultActorAssetGenerator.ProfilePath);
            ActorMotionProfile motionProfile =
                AssetDatabase.LoadAssetAtPath<ActorMotionProfile>(
                    DefaultActorAssetGenerator.MotionProfilePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                DefaultActorAssetGenerator.PrefabPath);

            Assert.That(controller, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(motionProfile, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            ThirdPersonMotor motor = prefab.GetComponent<ThirdPersonMotor>();
            ExplorationMovementInput input =
                prefab.GetComponent<ExplorationMovementInput>();
            Assert.That(motor.MotionProfile, Is.SameAs(motionProfile));
            Assert.That(
                motor.MovementCommandSource as Object,
                Is.EqualTo(input));
            Assert.DoesNotThrow(
                DefaultActorAssetGenerator.ValidateGeneratedAssets);
        }
    }
}
