using System.Collections;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Editor;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayActorPresentationLifecycleTests
    {
        [UnityTest]
        public IEnumerator MainLevelRestoresActorAnimationMaterialsAndCameraContracts()
        {
            using var runtime = new GameplayRuntimeTestHarness();
            yield return runtime.Start();

            GameplayController gameplay = runtime.Gameplay;
            GameObject player = GameObject.Find("Player Actor");
            GameObject target = GameObject.Find("Depot Rifleman");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.transform.position.z, Is.GreaterThan(-10f));
            Assert.That(player.transform.position.y, Is.GreaterThanOrEqualTo(0f));

            ThirdPersonMotor motor = player.GetComponent<ThirdPersonMotor>();
            ExplorationMovementInput input =
                player.GetComponent<ExplorationMovementInput>();
            ActorAnimationCoordinator presenter =
                player.GetComponent<ActorAnimationCoordinator>();
            ActorLocomotionAnimationPresenter locomotionPresenter =
                player.GetComponent<ActorLocomotionAnimationPresenter>();
            ActorCelShadingPresenter celShadingPresenter =
                player.GetComponent<ActorCelShadingPresenter>();
            Animator animator = player.GetComponentInChildren<Animator>();
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.MovementSpeedMultiplier, Is.EqualTo(0.9f));
            Assert.That(input, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(locomotionPresenter, Is.Not.Null);
            Assert.That(celShadingPresenter, Is.Not.Null);
            Assert.That(celShadingPresenter.IsApplied, Is.True);
            Assert.That(celShadingPresenter.IsOutlineApplied, Is.True);
            Assert.That(animator, Is.Not.Null);

            Material[] playerMaterials = player
                .GetComponentsInChildren<Renderer>()
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .ToArray();
            Assert.That(playerMaterials, Is.Not.Empty);
            Material[] playerCelMaterials = playerMaterials
                .Where(material =>
                    material.shader.name == GameplayCelMaterialStyle.ShaderName)
                .ToArray();
            Assert.That(playerCelMaterials, Is.Not.Empty);
            Assert.That(playerCelMaterials.All(material =>
                material.GetTexture("_BaseMap") != null), Is.True);
            Assert.That(playerCelMaterials.All(material =>
                material.GetFloat("_AmbientStrength") > 1f), Is.True);
            Assert.That(playerMaterials.Any(material =>
                material.shader.name == ActorCelShadingPresenter.OutlineShaderName),
                Is.True);
            Assert.That(motor.MovementCommandSource, Is.SameAs(input));
            Assert.That(input.ViewTransform, Is.Not.Null);
            Assert.That(input.InputEnabled, Is.True);
            Assert.That(locomotionPresenter.Motor, Is.SameAs(motor));
            Assert.That(
                locomotionPresenter.AnimationCoordinator,
                Is.SameAs(presenter));
            Assert.That(presenter.Profile, Is.Not.Null);
            Assert.That(presenter.Profile.AnimatorController, Is.Not.Null);
            Assert.That(
                animator.runtimeAnimatorController,
                Is.SameAs(presenter.Profile.AnimatorController));

            AnimatorController generatedController =
                presenter.Profile.AnimatorController as AnimatorController;
            Assert.That(generatedController, Is.Not.Null);
            AnimatorStateMachine locomotionStateMachine =
                generatedController.layers[0].stateMachine;
            AnimatorState standingLocomotion = locomotionStateMachine.states
                .Single(state => state.state.name == "Standing Locomotion")
                .state;
            AnimatorState crouchedLocomotion = locomotionStateMachine.states
                .Single(state => state.state.name == "Crouched Locomotion")
                .state;
            Assert.That(
                locomotionStateMachine.defaultState,
                Is.SameAs(standingLocomotion));
            Assert.That(standingLocomotion.motion, Is.TypeOf<BlendTree>());
            var standingBlendTree = (BlendTree)standingLocomotion.motion;
            Assert.That(
                standingBlendTree.children
                    .Single(child => child.position == Vector2.zero)
                    .motion,
                Is.TypeOf<AnimationClip>());

            AnimatorControllerLayer turnLayer = generatedController.layers
                .Single(layer =>
                    layer.name == ActorAnimationParameters.TurnLayerName);
            Assert.That(turnLayer.avatarMask, Is.Not.Null);
            Assert.That(turnLayer.defaultWeight, Is.Zero);
            Assert.That(turnLayer.iKPass, Is.False);
            Assert.That(
                turnLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.LeftLeg),
                Is.True);
            Assert.That(
                turnLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.RightLeg),
                Is.True);
            Assert.That(
                turnLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.Body),
                Is.False);
            Assert.That(
                turnLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.LeftArm),
                Is.False);
            Assert.That(
                turnLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.RightArm),
                Is.False);

            string hipsPath = AnimationUtility.CalculateTransformPath(
                animator.GetBoneTransform(HumanBodyBones.Hips),
                animator.transform);
            string spinePath = AnimationUtility.CalculateTransformPath(
                animator.GetBoneTransform(HumanBodyBones.Spine),
                animator.transform);
            int hipsMaskIndex = Enumerable.Range(
                    0,
                    turnLayer.avatarMask.transformCount)
                .Single(index =>
                    turnLayer.avatarMask.GetTransformPath(index) == hipsPath);
            int spineMaskIndex = Enumerable.Range(
                    0,
                    turnLayer.avatarMask.transformCount)
                .Single(index =>
                    turnLayer.avatarMask.GetTransformPath(index) == spinePath);
            Assert.That(
                turnLayer.avatarMask.GetTransformActive(hipsMaskIndex),
                Is.True);
            Assert.That(
                turnLayer.avatarMask.GetTransformActive(spineMaskIndex),
                Is.False);

            AnimatorState turnState = turnLayer.stateMachine.defaultState;
            ActorAnimationProfile animationProfile =
                AssetDatabase.LoadAssetAtPath<ActorAnimationProfile>(
                    DefaultActorAssetGenerator.ProfilePath);
            Assert.That(
                turnState.name,
                Is.EqualTo(ActorAnimationParameters.TurnInPlaceStateName));
            Assert.That(
                turnState.speed,
                Is.EqualTo(animationProfile.TurnInPlace.PlaybackSpeed)
                    .Within(0.001f));
            BlendTree turnInPlaceBlendTree = turnState.motion as BlendTree;
            Assert.That(turnInPlaceBlendTree, Is.Not.Null);
            Assert.That(
                turnInPlaceBlendTree.blendType,
                Is.EqualTo(BlendTreeType.Simple1D));
            Assert.That(
                turnInPlaceBlendTree.blendParameter,
                Is.EqualTo(ActorAnimationParameters.TurnRateName));
            Assert.That(
                turnInPlaceBlendTree.children.Select(child => child.threshold),
                Is.EqualTo(new[] { -1f, 0f, 1f }));
            Assert.That(crouchedLocomotion.motion, Is.TypeOf<BlendTree>());
            var crouchedBlendTree = (BlendTree)crouchedLocomotion.motion;
            Assert.That(
                crouchedBlendTree.blendType,
                Is.EqualTo(BlendTreeType.Simple1D));
            Assert.That(
                crouchedBlendTree.blendParameter,
                Is.EqualTo(ActorAnimationParameters.SpeedName));
            Assert.That(
                crouchedBlendTree.children.Select(child => child.motion.name),
                Is.EqualTo(new[]
                {
                    "1Hand_Up_Crouch_Idle_1",
                    "1Hand_Up_Crouch_F_InPlace",
                }));
            Assert.That(standingLocomotion.transitions.Any(transition =>
                transition.destinationState == crouchedLocomotion
                && transition.conditions.Any(condition =>
                    condition.parameter == ActorAnimationParameters.StanceName
                    && condition.mode == AnimatorConditionMode.Equals
                    && condition.threshold == (int)ActorStance.Crouched)), Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.True);

            Assert.That(target, Is.Not.Null);
            ActorCelShadingPresenter targetCelShading =
                target.GetComponent<ActorCelShadingPresenter>();
            Assert.That(targetCelShading, Is.Not.Null);
            Assert.That(targetCelShading.IsApplied, Is.True);
            Material[] targetMaterials = target
                .GetComponentsInChildren<Renderer>()
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .ToArray();
            Assert.That(targetMaterials, Is.Not.Empty);
            Assert.That(targetMaterials.Any(material =>
                material.shader.name == GameplayCelMaterialStyle.ShaderName),
                Is.True);
            GameplayEnemyController enemies =
                runtime.Bootstrap.GetComponent<GameplayEnemyController>();
            Assert.That(enemies, Is.Not.Null);
            Assert.That(enemies.EnemyCount, Is.EqualTo(1));
            TargetAcquisitionPresenter targetAcquisition =
                runtime.Bootstrap.GetComponent<TargetAcquisitionPresenter>();
            Assert.That(targetAcquisition, Is.Not.Null);
            Assert.That(targetAcquisition.IsBound, Is.True);
            Assert.That(targetAcquisition.HasPointerTarget, Is.False);
            Assert.That(targetAcquisition.TargetOutlineVisible, Is.False);
            Assert.That(targetAcquisition.GroundHaloVisible, Is.False);

            Camera gameplayCamera = GameObject.Find("Gameplay Camera")
                ?.GetComponent<Camera>();
            Assert.That(gameplayCamera, Is.Not.Null);
            Assert.That(gameplayCamera.orthographic, Is.False);
            GameplayCameraController cameraController = gameplayCamera
                .GetComponent<GameplayCameraController>();
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(
                cameraController.View,
                Is.EqualTo(GameplayCameraView.ThirdPerson));
            GameplayPlayerCutoutPresenter playerCutout = gameplayCamera
                .GetComponent<GameplayPlayerCutoutPresenter>();
            Assert.That(playerCutout, Is.Not.Null);
            Assert.That(playerCutout.IsBound, Is.True);
            Assert.That(playerCutout.Target, Is.SameAs(player.transform));
            playerCutout.RefreshNow();
            Assert.That(
                playerCutout.CurrentShaderData.z,
                Is.EqualTo(GameplayPlayerCutoutPresenter.ViewportRadius));
            Assert.That(playerCutout.CurrentShaderData.w, Is.GreaterThan(0f));
            Assert.That(
                playerCutout.CurrentLeftExtension,
                Is.EqualTo(GameplayPlayerCutoutPresenter.LeftViewportExtension));
            Assert.That(playerCutout.PresentationEnabled, Is.True);
            Assert.That(
                Vector3.Distance(
                    gameplayCamera.transform.position,
                    player.transform.position),
                Is.InRange(2.75f, 3.75f));
            Vector3 playerViewport = gameplayCamera.WorldToViewportPoint(
                player.transform.position + Vector3.up);
            Assert.That(playerViewport.z, Is.GreaterThan(0f));
            Assert.That(playerViewport.x, Is.InRange(0f, 1f));
            Assert.That(playerViewport.x, Is.GreaterThan(0.5f));
            Assert.That(playerViewport.y, Is.InRange(0f, 1f));
            int localPlayerLayer = LayerMask.NameToLayer(
                GameplayCameraController.LocalPlayerLayerName);
            int localPlayerMask = 1 << localPlayerLayer;
            Assert.That(localPlayerLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(player.GetComponentsInChildren<Renderer>()
                .All(renderer => renderer.gameObject.layer == localPlayerLayer),
                Is.True);
            Assert.That(
                gameplayCamera.cullingMask & localPlayerMask,
                Is.EqualTo(localPlayerMask));

            cameraController.ToggleView();
            cameraController.RefreshNow();

            ActorStancePresenter stancePresenter =
                player.GetComponent<ActorStancePresenter>();
            Assert.That(
                cameraController.View,
                Is.EqualTo(GameplayCameraView.FirstPerson));
            Assert.That(
                gameplayCamera.transform.position,
                Is.EqualTo(stancePresenter.FirstPersonEyePosition)
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(gameplayCamera.cullingMask & localPlayerMask, Is.Zero);
            Assert.That(playerCutout.PresentationEnabled, Is.False);
            Assert.That(playerCutout.CurrentShaderData, Is.EqualTo(Vector4.zero));
            Assert.That(playerCutout.CurrentLeftExtension, Is.Zero);

            cameraController.ToggleView();
            cameraController.RefreshNow();

            Assert.That(
                cameraController.View,
                Is.EqualTo(GameplayCameraView.ThirdPerson));
            Assert.That(
                gameplayCamera.cullingMask & localPlayerMask,
                Is.EqualTo(localPlayerMask));
            Assert.That(playerCutout.PresentationEnabled, Is.True);
            Assert.That(
                Vector3.Distance(
                    gameplayCamera.transform.position,
                    player.transform.position),
                Is.InRange(2.75f, 3.75f));
            Assert.That(runtime.SceneCamera.gameObject.activeSelf, Is.False);

            LevelEntityView[] levelEntities =
                Object.FindObjectsByType<LevelEntityView>(
                    FindObjectsInactive.Exclude);
            Renderer wallRenderer = levelEntities
                .Single(entity => entity.EntityId == "wall-south-01")
                .GetComponentsInChildren<Renderer>()
                .First(renderer => renderer.sharedMaterial.shader.name
                    == "GritGud/CelSurface");
            Renderer floorRenderer = levelEntities
                .Single(entity => entity.EntityId == "floor-r01-c01")
                .GetComponentsInChildren<Renderer>()
                .First(renderer => renderer.sharedMaterial.shader.name
                    == "GritGud/CelSurface");
            Assert.That(
                wallRenderer.sharedMaterial.GetFloat("_PlayerCutoutEnabled"),
                Is.EqualTo(1f));
            Assert.That(
                floorRenderer.sharedMaterial.GetFloat("_PlayerCutoutEnabled"),
                Is.EqualTo(0f));
        }
    }
}
