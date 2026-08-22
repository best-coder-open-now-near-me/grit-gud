using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayWeaponPresenterTests
    {
        [Test]
        public void DefaultCatalogMapsProductionWeaponModelsAndEffects()
        {
            WeaponPresentationCatalog catalog =
                WeaponPresentationCatalog.LoadDefault();

            WeaponPresentationDefinition rifle = catalog.Get("weapon.rifle");
            WeaponPresentationDefinition launcher =
                catalog.Get("weapon.rocket-launcher");
            WeaponPresentationDefinition knife =
                catalog.Get("weapon.combat-knife");

            Assert.That(rifle.Prefab, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(rifle.Prefab),
                Does.EndWith("/RifleWeaponRig.prefab"));
            Assert.That(
                rifle.AnimationSetId,
                Is.EqualTo(ActorAnimationPoseIds.Rifle));
            Assert.That(rifle.RigSockets, Is.Not.Null);
            Assert.That(rifle.RigSockets.Muzzle, Is.Not.Null);
            Assert.That(rifle.RigSockets.SupportHand, Is.Not.Null);
            Assert.That(rifle.RigSockets.SupportElbowHint, Is.Not.Null);
            Assert.That(rifle.RigSockets.SupportPositionWeight, Is.EqualTo(1f));
            Assert.That(rifle.RigSockets.SupportRotationWeight, Is.EqualTo(1f));
            Assert.That(rifle.MuzzleEffectPrefab, Is.Not.Null);
            Assert.That(rifle.InstantTracer, Is.True);
            Assert.That(launcher.Prefab, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(launcher.Prefab),
                Does.EndWith("/LauncherWeaponRig.prefab"));
            Assert.That(
                launcher.AnimationSetId,
                Is.EqualTo(ActorAnimationPoseIds.Launcher));
            Assert.That(launcher.RigSockets, Is.Not.Null);
            Assert.That(launcher.RigSockets.SupportHand, Is.Not.Null);
            Assert.That(launcher.MuzzleEffectPrefab, Is.Not.Null);
            Assert.That(launcher.InstantTracer, Is.False);
            Assert.That(knife.Prefab, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(knife.Prefab),
                Does.EndWith("/KnifeWeaponRig.prefab"));
            Assert.That(
                knife.AnimationSetId,
                Is.EqualTo(ActorAnimationPoseIds.Melee));
            Assert.That(knife.AttackPresentation,
                Is.EqualTo(WeaponAttackPresentationKind.ContactStrike));
            Assert.That(knife.MuzzleEffectPrefab, Is.Null);
            Assert.That(knife.InstantTracer, Is.False);
            Assert.That(
                knife.ContactStrikeSeconds,
                Is.EqualTo(GameplayCloseQuartersPresentationTiming
                    .ContactStrikeSeconds));
            Assert.That(
                knife.ContactImpactNormalizedTime,
                Is.EqualTo(GameplayCloseQuartersPresentationTiming
                    .ContactImpactNormalizedTime));
        }

        [Test]
        public void MeleePoseKeepsUpperBodyIkActiveWithoutFirearmState()
        {
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.Update(0f);

                presenter.PresentWeaponPose(ActorAnimationPoseIds.Melee);
                animator.Update(0.25f);

                int layerIndex = animator.GetLayerIndex("Weapon Upper Body");
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(
                        ActorAnimationParameters.KnifeIdleStateName),
                    Is.True);
                Assert.That(
                    animator.GetLayerWeight(layerIndex),
                    Is.EqualTo(
                        presenter.Profile.GetWeaponAnimationSet(
                            ActorAnimationPoseIds.Melee)
                            .PoseLayerWeight)
                        .Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void EmptyHandsReleasesTheWeaponPoseLayer()
        {
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);

                presenter.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                animator.Update(0.25f);
                int layerIndex = animator.GetLayerIndex(
                    ActorAnimationParameters.WeaponLayerName);
                Assert.That(
                    animator.GetLayerWeight(layerIndex),
                    Is.GreaterThan(0f));

                presenter.PresentWeaponPose(ActorAnimationPoseIds.Empty);
                animator.Update(0f);

                Assert.That(
                    presenter.CurrentWeaponAnimationSetId,
                    Is.EqualTo(ActorAnimationPoseIds.Empty));
                Assert.That(
                    presenter.Profile.GetWeaponAnimationSet(
                        ActorAnimationPoseIds.Empty).PoseLayerWeight,
                    Is.Zero);
                Assert.That(animator.GetLayerWeight(layerIndex), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void WeaponPoseRecoilAndActionsUseSeparateOwnedLayers()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    "Assets/GritGud/Presentation/Actors/Animation/"
                    + "DefaultPlayerLocomotion.controller");
            ActorAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<ActorAnimationProfile>(
                    "Assets/GritGud/Presentation/Actors/Animation/"
                    + "DefaultPlayerAnimationProfile.asset");

            Assert.That(controller, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(
                controller.parameters.Select(parameter => parameter.name),
                Does.Contain(ActorAnimationParameters.WeaponPoseName));
            Assert.That(
                controller.parameters.Select(parameter => parameter.name),
                Does.Not.Contain("Fire"));
            AnimatorControllerLayer poseLayer = controller.layers.Single(
                candidate => candidate.name ==
                    ActorAnimationParameters.WeaponLayerName);
            Assert.That(poseLayer.avatarMask, Is.Not.Null);
            Assert.That(poseLayer.defaultWeight, Is.EqualTo(1f));
            Assert.That(poseLayer.blendingMode,
                Is.EqualTo(AnimatorLayerBlendingMode.Override));
            Assert.That(poseLayer.iKPass, Is.True);
            string[] poseStates = poseLayer.stateMachine.states
                .Select(child => child.state.name)
                .ToArray();
            Assert.That(poseStates, Does.Contain("Empty Hands"));
            Assert.That(poseStates, Does.Contain("Rifle Aim"));
            Assert.That(poseStates, Does.Contain("Launcher Aim"));
            Assert.That(poseStates, Does.Not.Contain("Rifle Recoil"));

            AnimatorControllerLayer recoilLayer = controller.layers.Single(
                candidate => candidate.name ==
                    ActorAnimationParameters.RecoilLayerName);
            Assert.That(recoilLayer.avatarMask, Is.Not.Null);
            Assert.That(recoilLayer.defaultWeight, Is.Zero);
            Assert.That(recoilLayer.blendingMode,
                Is.EqualTo(AnimatorLayerBlendingMode.Additive));
            Assert.That(recoilLayer.iKPass, Is.False);
            string[] recoilStates = recoilLayer.stateMachine.states
                .Select(child => child.state.name)
                .ToArray();
            Assert.That(recoilStates, Does.Contain("No Recoil"));
            Assert.That(recoilStates, Does.Contain("Rifle Recoil"));
            Assert.That(recoilStates, Does.Contain("Launcher Recoil"));
            Assert.That(
                recoilLayer.stateMachine.states
                    .Where(child => child.state.name != "No Recoil")
                    .All(child => child.state.motion != null),
                Is.True);
            AnimatorState rifleRecoil = recoilLayer.stateMachine.states
                .Single(child => child.state.name == "Rifle Recoil")
                .state;
            AnimatorState launcherRecoil = recoilLayer.stateMachine.states
                .Single(child => child.state.name == "Launcher Recoil")
                .state;
            Assert.That(
                rifleRecoil.speed,
                Is.EqualTo(
                    profile.GetWeaponAnimationSet(
                        ActorAnimationPoseIds.Rifle)
                        .RecoilPlaybackSpeed));
            Assert.That(
                launcherRecoil.speed,
                Is.EqualTo(
                    profile.GetWeaponAnimationSet(
                        ActorAnimationPoseIds.Launcher)
                        .RecoilPlaybackSpeed));
            Assert.That(
                new[] { rifleRecoil, launcherRecoil }
                    .SelectMany(state => state.transitions)
                    .All(transition =>
                        Mathf.Abs(
                            transition.exitTime -
                            profile.RecoilExitNormalizedTime) <
                            0.001f &&
                        Mathf.Abs(
                            transition.duration -
                            profile.RecoilReturnTransitionSeconds) < 0.001f),
                Is.True);

            AnimatorControllerLayer actionLayer = controller.layers.Single(
                candidate => candidate.name ==
                    ActorAnimationParameters.ActionLayerName);
            Assert.That(
                actionLayer.avatarMask,
                Is.SameAs(poseLayer.avatarMask));
            Assert.That(actionLayer.defaultWeight, Is.Zero);
            Assert.That(
                actionLayer.blendingMode,
                Is.EqualTo(AnimatorLayerBlendingMode.Override));
            Assert.That(actionLayer.iKPass, Is.False);
            AnimatorState noActionState = actionLayer.stateMachine.states
                .Single(child => child.state.name ==
                    ActorAnimationParameters.NoActionStateName)
                .state;
            Assert.That(
                noActionState.behaviours,
                Has.Exactly(1).InstanceOf<
                    ActorActionLayerReleaseBehaviour>());
            AnimatorState rifleFireState = actionLayer.stateMachine.states
                .Single(child => child.state.name ==
                    ActorAnimationParameters.RifleFireStateName)
                .state;
            AnimatorState launcherFireState = actionLayer.stateMachine.states
                .Single(child => child.state.name ==
                    ActorAnimationParameters.LauncherFireStateName)
                .state;
            Assert.That(
                AssetDatabase.GetAssetPath(rifleFireState.motion),
                Is.EqualTo("Assets/Basic Shooter Pack/firing rifle.fbx"));
            Assert.That(
                AssetDatabase.GetAssetPath(launcherFireState.motion),
                Is.EqualTo(
                    "Assets/Kevin Iglesias/Human Animations/Animations/"
                    + "Male/Combat/Bazooka/"
                    + "HumanM@Bazooka_Aim01_Shoot01.fbx"));
            Assert.That(
                profile.TryGetActionBinding(
                    ActorAnimationAction.WeaponFire,
                    ActorAnimationPoseIds.Rifle,
                    out ActorAnimationActionBinding rifleFireBinding),
                Is.True);
            Assert.That(
                rifleFireBinding.ContextId,
                Is.EqualTo(ActorAnimationPoseIds.Rifle));
            Assert.That(
                rifleFireBinding.StateName,
                Is.EqualTo(ActorAnimationParameters.RifleFireStateName));
            Assert.That(
                profile.TryGetActionBinding(
                    ActorAnimationAction.WeaponFire,
                    ActorAnimationPoseIds.Launcher,
                    out ActorAnimationActionBinding launcherFireBinding),
                Is.True);
            Assert.That(
                launcherFireBinding.ContextId,
                Is.EqualTo(ActorAnimationPoseIds.Launcher));
            Assert.That(
                launcherFireBinding.StateName,
                Is.EqualTo(ActorAnimationParameters.LauncherFireStateName));
            AnimatorState throwState = actionLayer.stateMachine.states
                .Single(child => child.state.name ==
                    ActorAnimationParameters.ThrowStateName)
                .state;
            Assert.That(throwState.motion, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(throwState.motion),
                Is.EqualTo(
                    "Assets/Kevin Iglesias/Human Animations/Animations/"
                    + "Male/Combat/Grenade/HumanM@ThrowGrenade01_L.fbx"));
            Assert.That(
                profile.TryGetActionBinding(
                    ActorAnimationAction.Throw,
                    out ActorAnimationActionBinding throwBinding),
                Is.True);
            Assert.That(
                throwBinding.LayerName,
                Is.EqualTo(ActorAnimationParameters.ActionLayerName));
            Assert.That(
                throwBinding.StateName,
                Is.EqualTo(ActorAnimationParameters.ThrowStateName));
        }

        [TestCase("Assets/Basic Shooter Pack/firing rifle.fbx")]
        [TestCase(
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/"
            + "Bazooka/HumanM@Bazooka_Aim01_Shoot01.fbx")]
        public void RecoilClipsUseTheirRecoveredPoseAsAdditiveReference(
            string clipPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(clipPath)
                as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            Assert.That(clips, Is.Not.Empty);
            Assert.That(
                clips.All(clip =>
                    clip.hasAdditiveReferencePose &&
                    Mathf.Abs(
                        clip.additiveReferencePoseFrame
                        - clip.lastFrame) < 0.001f),
                Is.True,
                "A recovered final pose makes the first frames read as kick, "
                + "rather than subtracting the kick and showing only recovery.");
        }

        [TestCase(
            ActorAnimationPoseIds.Rifle,
            ActorAnimationParameters.RifleAimStateName,
            ActorAnimationParameters.RifleFireStateName,
            ActorAnimationParameters.RifleRecoilStateName,
            0.8f,
            9f,
            0.08f,
            0.42f)]
        [TestCase(
            ActorAnimationPoseIds.Launcher,
            ActorAnimationParameters.LauncherAimStateName,
            ActorAnimationParameters.LauncherFireStateName,
            ActorAnimationParameters.LauncherRecoilStateName,
            1f,
            14f,
            0.1f,
            0.6f)]
        public void WeaponFireComposesAuthoredActionAndAdditiveRecoil(
            string animationSetId,
            string poseState,
            string fireState,
            string recoilState,
            float recoilWeight,
            float recoilKickDegrees,
            float recoilHoldSeconds,
            float recoilReturnSeconds)
        {
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);

                presenter.PresentWeaponPose(animationSetId);
                animator.Update(0.25f);
                presenter.PresentWeaponFire();
                animator.Update(0.05f);

                int poseLayer = animator.GetLayerIndex(
                    ActorAnimationParameters.WeaponLayerName);
                int actionLayer = animator.GetLayerIndex(
                    ActorAnimationParameters.ActionLayerName);
                int recoilLayer = animator.GetLayerIndex(
                    ActorAnimationParameters.RecoilLayerName);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(poseLayer).IsName(
                        poseState),
                    Is.True);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(actionLayer).IsName(
                        fireState),
                    Is.True);
                Assert.That(
                    animator.GetLayerWeight(actionLayer),
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(recoilLayer).IsName(
                        recoilState),
                    Is.True);
                Assert.That(
                    animator.GetLayerWeight(recoilLayer),
                    Is.EqualTo(recoilWeight).Within(0.001f));
                ActorWeaponAnimationSet animationSet =
                    presenter.Profile.GetWeaponAnimationSet(
                        presenter.CurrentWeaponAnimationSetId);
                Assert.That(
                    animationSet.RecoilTransitionSeconds,
                    Is.Zero,
                    "The recoil clip's opening kick must not be hidden by a "
                    + "crossfade.");
                Assert.That(
                    animationSet.RecoilKickDegrees,
                    Is.EqualTo(recoilKickDegrees).Within(0.001f));
                Assert.That(
                    animationSet.RecoilHoldSeconds,
                    Is.EqualTo(recoilHoldSeconds).Within(0.001f));
                Assert.That(
                    animationSet.RecoilReturnSeconds,
                    Is.EqualTo(recoilReturnSeconds).Within(0.001f));
                Assert.That(
                    presenter.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.WeaponFire));
                Assert.That(presenter.ActionSequence, Is.EqualTo(1));

                presenter.PresentWeaponFire();
                Assert.That(
                    presenter.ActionSequence,
                    Is.EqualTo(2),
                    "Rapid fire must restart and count a second recoil pulse.");

                presenter.PresentWeaponPose(ActorAnimationPoseIds.Empty);
                Assert.That(
                    animator.GetLayerWeight(recoilLayer),
                    Is.Zero,
                    "Changing equipment must clear an in-flight recoil.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void EquipmentEventsReplaceAndRemoveHeldWeapon()
        {
            var host = new GameObject("Weapon Presenter Test Host");
            var actor = new GameObject("Weapon Presenter Test Actor");
            var riflePrefab = new GameObject("Test Rifle");
            var launcherPrefab = new GameObject("Test Launcher");
            var gripObject = new GameObject("Test Weapon Grip");
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            WeaponPresentationCatalog catalog = null;
            try
            {
                ConfigureTestRig(riflePrefab, supportHand: true);
                ConfigureTestRig(launcherPrefab, supportHand: true);
                actor.AddComponent<CharacterController>();
                actor.AddComponent<ActorStancePresenter>();
                gripObject.transform.SetParent(actor.transform, false);
                world = new LevelWorld(
                    new GameObject("Weapon Presenter Test World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "player",
                    "test",
                    targetable: false,
                    actor);
                GameplaySession session = CreateSession();
                catalog = WeaponPresentationCatalog.CreateRuntime(
                    CreateDefinition(
                        "rifle",
                        riflePrefab,
                        ActorAnimationPoseIds.Rifle),
                    CreateDefinition(
                        "launcher",
                        launcherPrefab,
                        ActorAnimationPoseIds.Launcher));
                GameplayWeaponPresenter presenter =
                    host.AddComponent<GameplayWeaponPresenter>();
                presenter.Bind(
                    session,
                    registry,
                    host.AddComponent<GameplayAttackController>(),
                    host.AddComponent<GameplayProjectileController>(),
                    CreateUnboundAnimationCoordinator(actor),
                    "player",
                    catalog,
                    gripObject.transform);

                Assert.That(
                    host.GetComponent<WeaponMountPresenter>(),
                    Is.Not.Null);
                Assert.That(
                    host.GetComponent<WeaponActionEffectsPresenter>(),
                    Is.Not.Null);
                Assert.That(
                    host.GetComponent<WeaponAimPresenter>(),
                    Is.Not.Null);
                Assert.That(presenter.CurrentItemId, Is.EqualTo("rifle"));
                Assert.That(presenter.HeldWeapon, Is.Not.Null);
                Assert.That(presenter.HeldWeapon.name,
                    Is.EqualTo("Test Rifle - Held"));
                Assert.That(presenter.HeldWeapon.transform.parent,
                    Is.SameAs(gripObject.transform));
                Assert.That(presenter.HeldWeapon.transform.localPosition,
                    Is.EqualTo(Vector3.zero));

                var equipment = new GameplayEquipmentSession(session);
                Assert.That(
                    equipment.TryResolve(
                        "player",
                        "rifle",
                        equip: false,
                        out _,
                        out EquipmentChangeFailure unequipFailure),
                    Is.True,
                    unequipFailure.ToString());
                Assert.That(presenter.CurrentItemId, Is.Null);
                Assert.That(presenter.HeldWeapon, Is.Null);

                Assert.That(
                    equipment.TryResolve(
                        "player",
                        "launcher",
                        equip: true,
                        out _,
                        out EquipmentChangeFailure equipFailure),
                    Is.True,
                    equipFailure.ToString());
                Assert.That(presenter.CurrentItemId, Is.EqualTo("launcher"));
                Assert.That(presenter.HeldWeapon.name,
                    Is.EqualTo("Test Launcher - Held"));
                Assert.That(presenter.HeldWeapon.transform.localPosition,
                    Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(riflePrefab);
                Object.DestroyImmediate(launcherPrefab);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void RangedWeaponPoseOnlyTracksPointerAfterShotIsArmed()
        {
            var host = new GameObject("Pre-Arm Weapon Aim Host");
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            Assert.That(playerPrefab, Is.Not.Null);
            GameObject player = Object.Instantiate(playerPrefab);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            WeaponPresentationCatalog catalog = null;
            try
            {
                world = new LevelWorld(
                    new GameObject("Pre-Arm Weapon Aim World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "player",
                    "test",
                    targetable: false,
                    player);
                GameplaySession session = CreateSession();
                TargetAcquisitionPresenter acquisition =
                    host.AddComponent<TargetAcquisitionPresenter>();
                acquisition.Bind(session, registry, "player");
                WeaponPresentationDefinition source =
                    WeaponPresentationCatalog.LoadDefault().Get(
                        "weapon.rifle");
                catalog = WeaponPresentationCatalog.CreateRuntime(
                    CreateDefinition(
                        "rifle",
                        source.Prefab,
                        ActorAnimationPoseIds.Rifle));
                ActorAnimationCoordinator animation = player.GetComponent<
                    ActorAnimationCoordinator>();
                GameplayWeaponPresenter presenter =
                    host.AddComponent<GameplayWeaponPresenter>();
                presenter.Bind(
                    session,
                    registry,
                    host.AddComponent<GameplayAttackController>(),
                    host.AddComponent<GameplayProjectileController>(),
                    animation,
                    "player",
                    catalog,
                    targetAcquisition: acquisition);
                acquisition.SetWeaponAimOriginProvider(
                    () => presenter.Muzzle.position);
                Physics.SyncTransforms();
                acquisition.RefreshNow(new Ray(
                    new Vector3(3f, 1.2f, 0f),
                    Vector3.forward));
                WeaponAimPresenter aim =
                    host.GetComponent<WeaponAimPresenter>();
                WeaponAimRig rig = animation.TargetAnimator.GetComponent<
                    WeaponAimRig>();
                Quaternion initialRotation = player.transform.rotation;

                Assert.That(acquisition.WeaponTargetingActive, Is.False);
                Assert.That(rig, Is.Not.Null);
                Assert.That(rig.HasAimPoint, Is.False);

                aim.Tick(1f);

                Assert.That(
                    rig.HasAimPoint,
                    Is.False,
                    "Ordinary pointer preview must not aim the character.");
                Assert.That(
                    Quaternion.Angle(
                        initialRotation,
                        player.transform.rotation),
                    Is.LessThan(0.001f),
                    "Ordinary pointer preview must not rotate the character.");
                Assert.That(
                    acquisition.WeaponTargetingActive,
                    Is.False,
                    "Pointer preview must not implicitly arm the attack.");

                acquisition.SetWeaponTargetingActive(true);
                aim.Tick(1f);

                Assert.That(rig.HasAimPoint, Is.True);
                Assert.That(
                    Quaternion.Angle(
                        initialRotation,
                        player.transform.rotation),
                    Is.GreaterThan(0.1f),
                    "Explicit weapon targeting should still turn the actor.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ReplayEquipmentProjectionRestoresLiveHeldModel()
        {
            var host = new GameObject("Replay Weapon Presenter Host");
            var actor = new GameObject("Replay Weapon Presenter Actor");
            var riflePrefab = new GameObject("Replay Rifle");
            var launcherPrefab = new GameObject("Replay Launcher");
            var gripObject = new GameObject("Replay Weapon Grip");
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            WeaponPresentationCatalog catalog = null;
            try
            {
                ConfigureTestRig(riflePrefab, supportHand: true);
                ConfigureTestRig(launcherPrefab, supportHand: true);
                actor.AddComponent<CharacterController>();
                actor.AddComponent<ActorStancePresenter>();
                gripObject.transform.SetParent(actor.transform, false);
                world = new LevelWorld(
                    new GameObject("Replay Weapon World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "player",
                    "test",
                    targetable: false,
                    actor);
                GameplaySession session = CreateSession();
                catalog = WeaponPresentationCatalog.CreateRuntime(
                    CreateDefinition(
                        "rifle",
                        riflePrefab,
                        ActorAnimationPoseIds.Rifle),
                    CreateDefinition(
                        "launcher",
                        launcherPrefab,
                        ActorAnimationPoseIds.Launcher));
                GameplayWeaponPresenter presenter =
                    host.AddComponent<GameplayWeaponPresenter>();
                presenter.Bind(
                    session,
                    registry,
                    host.AddComponent<GameplayAttackController>(),
                    host.AddComponent<GameplayProjectileController>(),
                    CreateUnboundAnimationCoordinator(actor),
                    "player",
                    catalog,
                    gripObject.transform);

                presenter.BeginReplayPresentation();
                presenter.PresentReplayEquipment("launcher");
                Assert.That(presenter.CurrentItemId, Is.EqualTo("launcher"));
                Assert.That(presenter.HeldWeapon.name,
                    Is.EqualTo("Replay Launcher - Held"));

                var shot = new ReplayCombatPresentationEvent(
                    transitionSequence: 12,
                    ReplayCombatPresentationEventKind.WeaponDischarge,
                    "player",
                    "target",
                    new GameplayPosition(12f, 3f, -5f),
                    new GameplayPosition(12f, 3f, 8f),
                    GameplaySemanticReplayPresentationTiming
                        .ActionResolutionProgress,
                    presentationId: "launcher");
                presenter.PresentReplayEquipment("rifle");
                Assert.That(presenter.CurrentItemId, Is.EqualTo("rifle"));
                var cursor = new ReplayTimedPresentationEventCursor();
                if (cursor.TryCross(
                    shot.StableKey,
                    shot.NormalizedTime,
                    0f,
                    1f))
                {
                    presenter.PresentReplayEvent(shot);
                }
                Assert.That(
                    presenter.CurrentItemId,
                    Is.EqualTo("launcher"),
                    "A crossed event must use its recorded presentation item "
                    + "instead of the final sampled equipment.");
                Assert.That(
                    host.GetComponentsInChildren<Light>().Length,
                    Is.EqualTo(1),
                    "A crossed replay discharge must emit one muzzle light.");
                Assert.That(
                    host.GetComponentsInChildren<LineRenderer>().Length,
                    Is.EqualTo(1),
                    "A crossed hitscan discharge must emit one tracer.");
                Assert.That(
                    host.GetComponentInChildren<LineRenderer>().GetPosition(0),
                    Is.EqualTo(new Vector3(12f, 3f, -5f)),
                    "A historical shot outside muzzle tolerance must use its "
                    + "recorded origin without querying current-world physics.");
                Assert.That(
                    cursor.TryCross(
                        shot.StableKey,
                        shot.NormalizedTime,
                        0f,
                        1f),
                    Is.False,
                    "The same forward threshold crossing cannot emit twice.");
                Assert.That(presenter.TransientVisualCount, Is.EqualTo(2));
                presenter.ClearReplayTransients();
                Assert.That(presenter.TransientVisualCount, Is.Zero);

                presenter.PresentReplayEquipment(null);
                Assert.That(presenter.HeldWeapon, Is.Null);

                presenter.EndReplayPresentation();
                Assert.That(presenter.CurrentItemId, Is.EqualTo("rifle"));
                Assert.That(presenter.HeldWeapon.name,
                    Is.EqualTo("Replay Rifle - Held"));
                Assert.That(session.GetActor("player").EquippedItemId,
                    Is.EqualTo("rifle"));
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(riflePrefab);
                Object.DestroyImmediate(launcherPrefab);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void CommittedContactAttackTriggersStrikeWithoutFirearmEffects()
        {
            var host = new GameObject("Contact Weapon Presenter Host");
            GameObject actorPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject playerObject = Object.Instantiate(actorPrefab);
            GameObject targetObject = Object.Instantiate(actorPrefab);
            var knifePrefab = new GameObject("Test Knife");
            var gripObject = new GameObject("Contact Weapon Grip");
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            WeaponPresentationCatalog catalog = null;
            try
            {
                ConfigureTestRig(knifePrefab, supportHand: false);
                gripObject.transform.SetParent(playerObject.transform, false);
                targetObject.transform.position = new Vector3(0f, 0f, 1.5f);
                world = new LevelWorld(
                    new GameObject("Contact Weapon World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "player",
                    "test",
                    targetable: false,
                    playerObject);
                registry.RegisterActor(
                    "target",
                    "test",
                    targetable: true,
                    targetObject);
                GameplaySession session = CreateContactSession();
                session.EnterTurnMode();
                TargetAcquisitionPresenter acquisition =
                    host.AddComponent<TargetAcquisitionPresenter>();
                GameplayAttackController attacks =
                    host.AddComponent<GameplayAttackController>();
                attacks.Bind(
                    session,
                    acquisition,
                    new GameplayDialogueLog(),
                    "player");
                catalog = WeaponPresentationCatalog.CreateRuntime(
                    new WeaponPresentationDefinition(
                        "knife",
                        knifePrefab,
                        ActorAnimationPoseIds.Melee,
                        null,
                        drawsInstantTracer: false,
                        effectSeconds: 0.1f,
                        lineWidth: 0.02f,
                        attackPresentationKind:
                            WeaponAttackPresentationKind.ContactStrike,
                        contactDurationSeconds: 0.4f,
                        contactImpactTime: 0.4f));
                ActorAnimationCoordinator playerAnimation = playerObject
                    .GetComponent<ActorAnimationCoordinator>();
                ActorAnimationCoordinator targetAnimation = targetObject
                    .GetComponent<ActorAnimationCoordinator>();
                GameplayWeaponPresenter presenter =
                    host.AddComponent<GameplayWeaponPresenter>();
                presenter.Bind(
                    session,
                    registry,
                    attacks,
                    host.AddComponent<GameplayProjectileController>(),
                    playerAnimation,
                    "player",
                    catalog,
                    gripObject.transform);
                GameplayCombatReactionPresenter reactions =
                    host.AddComponent<GameplayCombatReactionPresenter>();
                reactions.Bind(session, registry, attacks, catalog);

                Assert.That(attacks.TryAttack(CreateContactExposure()), Is.True);
                Assert.That(presenter.ContactStrikeActive, Is.True);
                Assert.That(presenter.TransientVisualCount, Is.Zero);
                Assert.That(
                    playerAnimation.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.ContactStrike));
                Assert.That(targetAnimation.LastRequestedAction, Is.Null);
                Assert.That(reactions.PendingReactionCount, Is.EqualTo(1));

                playerAnimation.TargetAnimator.Update(0.1f);
                int actionLayer = playerAnimation.TargetAnimator.GetLayerIndex(
                    ActorAnimationParameters.ActionLayerName);
                Assert.That(
                    playerAnimation.TargetAnimator
                        .GetCurrentAnimatorStateInfo(actionLayer)
                        .IsName(ActorAnimationParameters.KnifeStrikeStateName),
                    Is.True);

                reactions.Tick(0.15f);
                Assert.That(targetAnimation.LastRequestedAction, Is.Null);
                reactions.Tick(0.02f);
                Assert.That(
                    targetAnimation.LastRequestedAction,
                    Is.Null);
                Assert.That(
                    targetObject.GetComponent<
                        ActorInjuryAnimationOverlayPresenter>()
                        .HitReactionActive,
                    Is.True);
                Assert.That(reactions.PendingReactionCount, Is.Zero);

                presenter.TickContactStrike(0.5f);
                Assert.That(presenter.ContactStrikeActive, Is.False);
                Assert.That(presenter.TransientVisualCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(knifePrefab);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void IncapacitatingContactAttackArmsRagdollWithJournalEvidence()
        {
            var host = new GameObject("Contact Ragdoll Host");
            GameObject actorPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject playerObject = Object.Instantiate(actorPrefab);
            GameObject targetObject = Object.Instantiate(actorPrefab);
            var knifePrefab = new GameObject("Contact Ragdoll Knife");
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            WeaponPresentationCatalog catalog = null;
            try
            {
                ConfigureTestRig(knifePrefab, supportHand: false);
                targetObject.transform.position = new Vector3(0f, 0f, 1.5f);
                world = new LevelWorld(
                    new GameObject("Contact Ragdoll World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "player", "test", targetable: false, playerObject);
                registry.RegisterActor(
                    "target", "test", targetable: true, targetObject);
                GameplaySession session = CreateContactSession(
                    targetMaximumWounds: 1);
                session.EnterTurnMode();
                GameplayAttackController attacks =
                    host.AddComponent<GameplayAttackController>();
                attacks.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player");
                catalog = WeaponPresentationCatalog.CreateRuntime(
                    new WeaponPresentationDefinition(
                        "knife",
                        knifePrefab,
                        ActorAnimationPoseIds.Melee,
                        null,
                        drawsInstantTracer: false,
                        effectSeconds: 0.1f,
                        lineWidth: 0.02f,
                        attackPresentationKind:
                            WeaponAttackPresentationKind.ContactStrike,
                        contactDurationSeconds: 0.4f,
                        contactImpactTime: 0.4f));
                GameplayCombatReactionPresenter reactions =
                    host.AddComponent<GameplayCombatReactionPresenter>();
                reactions.Bind(session, registry, attacks, catalog);
                ActorAnimationCoordinator animation = targetObject
                    .GetComponent<ActorAnimationCoordinator>();
                ActorRagdollPresenter ragdoll = targetObject.GetComponent<
                    ActorRagdollPresenter>();
                animation.TargetAnimator.cullingMode =
                    AnimatorCullingMode.AlwaysAnimate;
                animation.TargetAnimator.Update(0f);

                Assert.That(attacks.TryAttack(CreateContactExposure()), Is.True);
                reactions.Tick(0.17f);

                Assert.That(session.IsActorIncapacitated("target"), Is.True);
                Assert.That(
                    animation.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.IncapacitateShoulder));
                Assert.That(ragdoll.HasPendingActivation, Is.True);
                long journalSequence = session.Journal.Entries
                    .OfType<ActionResolvedJournalEntry>()
                    .Single().Sequence;
                bool activated = false;
                for (int index = 0; index < 120 && !activated; index++)
                {
                    animation.TargetAnimator.Update(0.05f);
                    activated = ragdoll.TryActivateAtAuthoredHandoff();
                }

                Assert.That(activated, Is.True);
                Assert.That(
                    ragdoll.TryGetTrace(journalSequence, out var trace),
                    Is.True);
                Assert.That(
                    trace.HandoffEventNormalizedTime,
                    Is.EqualTo(Mathf.Lerp(
                        0.4f,
                        1f,
                        ragdoll.Profile.HandoffNormalizedTime))
                        .Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(knifePrefab);
                Object.DestroyImmediate(catalog);
            }
        }

        [TestCase("weapon.rifle", ActorAnimationPoseIds.Rifle)]
        [TestCase("weapon.rocket-launcher", ActorAnimationPoseIds.Launcher)]
        public void ProductionWeaponPoseUsesTheMatchingModelAndAnimationPack(
            string itemId,
            string animationSetId)
        {
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.Update(0f);
                presenter.PresentWeaponPose(animationSetId);
                animator.Update(0.25f);

                int layerIndex = animator.GetLayerIndex("Weapon Upper Body");
                Assert.That(layerIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(
                        animationSetId == ActorAnimationPoseIds.Rifle
                            ? "Rifle Aim"
                            : "Launcher Aim"),
                    Is.True);
                Assert.That(
                    animator.GetLayerWeight(layerIndex),
                    Is.EqualTo(
                        presenter.Profile.GetWeaponAnimationSet(
                            animationSetId)
                            .PoseLayerWeight)
                        .Within(0.001f));

                WeaponPresentationDefinition definition =
                    WeaponPresentationCatalog.LoadDefault().Get(itemId);
                string prefabPath = AssetDatabase.GetAssetPath(
                    definition.Prefab);
                var animatorController =
                    (AnimatorController)animator.runtimeAnimatorController;
                AnimatorControllerLayer weaponLayer =
                    animatorController.layers.Single(candidate =>
                        candidate.name == "Weapon Upper Body");
                Motion poseMotion = weaponLayer.stateMachine.states
                    .Single(child => child.state.name == (
                        animationSetId == ActorAnimationPoseIds.Rifle
                            ? "Rifle Aim"
                            : "Launcher Aim"))
                    .state.motion;
                string motionPath = AssetDatabase.GetAssetPath(poseMotion);
                Assert.That(
                    prefabPath,
                    Does.StartWith("Assets/GritGud/Content/Resources/Gameplay/WeaponRigs/"));
                if (animationSetId == ActorAnimationPoseIds.Rifle)
                {
                    Assert.That(poseMotion, Is.TypeOf<BlendTree>());
                    Assert.That(
                        ((BlendTree)poseMotion).children
                            .Select(child => AssetDatabase.GetAssetPath(child.motion))
                            .All(path => path.StartsWith(
                                "Assets/Basic Shooter Pack/",
                                StringComparison.Ordinal)),
                        Is.True);
                }
                else
                {
                    Assert.That(
                        motionPath,
                        Does.StartWith(
                            "Assets/Kevin Iglesias/Human Animations/"));
                }
                Transform hand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                GameObject weapon = Object.Instantiate(
                    definition.Prefab,
                    hand,
                    false);
                weapon.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                WeaponRigSocketSet sockets =
                    weapon.GetComponent<WeaponRigSocketSet>();
                Vector3 muzzlePosition = sockets.Muzzle.position;
                Assert.That(float.IsNaN(muzzlePosition.x), Is.False);
                Assert.That(float.IsNaN(muzzlePosition.y), Is.False);
                Assert.That(float.IsNaN(muzzlePosition.z), Is.False);
                Assert.That(
                    Vector3.Distance(muzzlePosition, hand.position),
                    Is.GreaterThan(0.05f));
                var actorView = new GameplayActorView(
                    "player",
                    "test",
                    targetable: false,
                    player);
                Physics.SyncTransforms();
                Assert.That(
                    new UnityWeaponDischargeOriginResolver()
                        .TryBuildDischargeLine(
                            actorView,
                            sockets.Muzzle,
                            out WeaponDischargeLine dischargeLine),
                    Is.True,
                    $"{itemId} muzzle backward must intersect the owning "
                        + "character capsule.");
                Assert.That(
                    Vector3.Distance(
                        dischargeLine.AntiMuzzlePosition,
                        dischargeLine.MuzzlePosition),
                    Is.GreaterThan(0.05f));
                if (animationSetId == ActorAnimationPoseIds.Rifle)
                {
                    Vector3 barrelDirection = sockets.Muzzle.forward;
                    Assert.That(
                        Mathf.Abs(Vector3.Dot(
                            barrelDirection.normalized,
                            player.transform.up)),
                        Is.LessThan(0.14f),
                        "The rifle barrel must remain horizontal in the "
                        + "authored rifle pose.");
                }
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ProceduralAimCorrectionAlignsAndRespectsItsLimit()
        {
            Quaternion unrestricted = WeaponAimProjector.CalculateCorrection(
                Vector3.forward,
                Vector3.right,
                120f);
            Vector3 aligned = unrestricted * Vector3.forward;
            Assert.That(Vector3.Angle(aligned, Vector3.right),
                Is.LessThan(0.01f));

            Quaternion limited = WeaponAimProjector.CalculateCorrection(
                Vector3.forward,
                Vector3.right,
                35f);
            Assert.That(Quaternion.Angle(Quaternion.identity, limited),
                Is.EqualTo(35f).Within(0.01f));
        }

        [Test]
        public void LiveWeaponAimUsesClosestBoundedPoseOutsideCorrectionCone()
        {
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                WeaponPresentationDefinition rifle =
                    WeaponPresentationCatalog.LoadDefault().Get(
                        "weapon.rifle");
                GameObject weapon = Object.Instantiate(
                    rifle.Prefab,
                    rightHand,
                    false);
                weapon.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                WeaponRigSocketSet sockets =
                    weapon.GetComponent<WeaponRigSocketSet>();
                WeaponAimRig driver = animator.gameObject
                    .AddComponent<WeaponAimRig>();
                driver.Bind(
                    player.transform,
                    weapon.transform,
                    sockets.Muzzle,
                    sockets.SupportHand,
                    sockets.SupportElbowHint,
                    sockets.SupportPositionWeight,
                    sockets.SupportRotationWeight,
                    sockets.SupportElbowHintWeight,
                    sockets.SupportBlendSeconds,
                    rifle.MaximumAimCorrectionDegrees,
                    maxBodyAimCorrectionDegrees: 0f,
                    presenter.Profile.BodyAimDegreesPerSecond,
                    presenter.Profile.WeaponAimDegreesPerSecond);
                presenter.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                animator.Update(0.25f);
                driver.SynchronizeAfterAnimation(0f);

                Vector3 initialForward = sockets.Muzzle.forward;
                float requestedDegrees =
                    rifle.MaximumAimCorrectionDegrees + 20f;
                Vector3 requestedDirection = Quaternion.AngleAxis(
                    requestedDegrees,
                    player.transform.up) * initialForward;
                Vector3 aimPoint = sockets.Muzzle.position
                    + requestedDirection * 100f;

                driver.SetAimPoint(aimPoint);
                driver.SynchronizeAfterAnimation(1f);

                float appliedDegrees = Vector3.Angle(
                    initialForward,
                    sockets.Muzzle.forward);
                float remainingError = Vector3.Angle(
                    sockets.Muzzle.forward,
                    aimPoint - sockets.Muzzle.position);
                Assert.That(
                    appliedDegrees,
                    Is.EqualTo(rifle.MaximumAimCorrectionDegrees)
                        .Within(0.1f),
                    "Out-of-cone aim must use the closest authored pose instead "
                    + "of disabling correction.");
                Assert.That(
                    remainingError,
                    Is.LessThan(requestedDegrees - 17.5f),
                    "The live muzzle must reduce aim error even when the target "
                    + "starts outside the authored correction cone.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void TorsoAssistedShotAimClosesErrorBeyondWeaponCone()
        {
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                Transform aimBody = animator.GetBoneTransform(
                    HumanBodyBones.UpperChest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                WeaponPresentationDefinition rifle =
                    WeaponPresentationCatalog.LoadDefault().Get(
                        "weapon.rifle");
                GameObject weapon = Object.Instantiate(
                    rifle.Prefab,
                    rightHand,
                    false);
                weapon.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                WeaponRigSocketSet sockets =
                    weapon.GetComponent<WeaponRigSocketSet>();
                WeaponAimRig driver = animator.gameObject
                    .AddComponent<WeaponAimRig>();
                driver.Bind(
                    player.transform,
                    weapon.transform,
                    sockets.Muzzle,
                    sockets.SupportHand,
                    sockets.SupportElbowHint,
                    sockets.SupportPositionWeight,
                    sockets.SupportRotationWeight,
                    sockets.SupportElbowHintWeight,
                    sockets.SupportBlendSeconds,
                    rifle.MaximumAimCorrectionDegrees,
                    presenter.Profile.MaximumBodyAimCorrectionDegrees,
                    presenter.Profile.BodyAimDegreesPerSecond,
                    presenter.Profile.WeaponAimDegreesPerSecond);
                presenter.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                animator.Update(0.25f);
                driver.SynchronizeAfterAnimation(0f);

                Quaternion initialBodyRotation = aimBody.rotation;
                Vector3 requestedDirection = Quaternion.AngleAxis(
                    rifle.MaximumAimCorrectionDegrees + 24f,
                    player.transform.right) * sockets.Muzzle.forward;
                Vector3 aimPoint = sockets.Muzzle.position
                    + requestedDirection * 100f;

                float residualError = driver.SynchronizeAimForShot(aimPoint);

                Assert.That(
                    residualError,
                    Is.LessThan(
                        presenter.Profile.ShotAlignmentToleranceDegrees),
                    "The torso and wrist stages must close the barrel-to-shot "
                    + "error together.");
                Assert.That(
                    Quaternion.Angle(initialBodyRotation, aimBody.rotation),
                    Is.GreaterThan(20f),
                    "Aim beyond the wrist cone must be owned by the torso "
                    + "before fine weapon correction.");
                Assert.That(
                    Vector3.Distance(
                        rightHand.position,
                        sockets.RightHandGrip.position),
                    Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RecoilImpulseKicksTheSolvedBarrelBeforeReturning()
        {
            Assert.That(
                WeaponAimRig.EvaluateRecoilWeight(0f, 0.06f, 0.36f),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                WeaponAimRig.EvaluateRecoilWeight(0.06f, 0.06f, 0.36f),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                WeaponAimRig.EvaluateRecoilWeight(0.24f, 0.06f, 0.36f),
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(
                WeaponAimRig.EvaluateRecoilWeight(0.42f, 0.06f, 0.36f),
                Is.Zero.Within(0.001f));

            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Transform aimBody = animator.GetBoneTransform(
                    HumanBodyBones.UpperChest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                WeaponPresentationDefinition rifle =
                    WeaponPresentationCatalog.LoadDefault().Get(
                        "weapon.rifle");
                GameObject weapon = Object.Instantiate(
                    rifle.Prefab,
                    rightHand,
                    false);
                WeaponRigSocketSet sockets =
                    weapon.GetComponent<WeaponRigSocketSet>();
                WeaponAimRig driver = animator.gameObject
                    .AddComponent<WeaponAimRig>();
                driver.Bind(
                    player.transform,
                    weapon.transform,
                    sockets.Muzzle,
                    sockets.SupportHand,
                    sockets.SupportElbowHint,
                    sockets.SupportPositionWeight,
                    sockets.SupportRotationWeight,
                    sockets.SupportElbowHintWeight,
                    sockets.SupportBlendSeconds,
                    rifle.MaximumAimCorrectionDegrees,
                    presenter.Profile.MaximumBodyAimCorrectionDegrees,
                    presenter.Profile.BodyAimDegreesPerSecond,
                    presenter.Profile.WeaponAimDegreesPerSecond);
                presenter.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                animator.Update(0.25f);
                driver.SynchronizeAfterAnimation(0f);
                Vector3 aimPoint = sockets.Muzzle.position
                    + sockets.Muzzle.forward * 100f;
                driver.SynchronizeAimForShot(aimPoint);
                Vector3 solvedDirection = sockets.Muzzle.forward;
                Quaternion solvedBodyRotation = aimBody.rotation;
                ActorWeaponAnimationSet animationSet =
                    presenter.Profile.GetWeaponAnimationSet(
                        ActorAnimationPoseIds.Rifle);

                driver.TriggerRecoil(
                    animationSet.RecoilKickDegrees,
                    animationSet.RecoilHoldSeconds,
                    animationSet.RecoilReturnSeconds);
                driver.ClearAimPointWhenSettled();
                Assert.That(
                    driver.HasAimPoint,
                    Is.True,
                    "Closing targeting must retain the solved shot direction "
                    + "until recoil finishes.");
                driver.SynchronizeAfterAnimation(0f);

                Assert.That(
                    Vector3.Angle(solvedDirection, sockets.Muzzle.forward),
                    Is.EqualTo(animationSet.RecoilKickDegrees).Within(0.2f),
                    "The first recoil frame must visibly lift the barrel.");
                Assert.That(
                    Quaternion.Angle(solvedBodyRotation, aimBody.rotation),
                    Is.LessThan(0.2f),
                    "The procedural barrel kick must not compete with the "
                    + "additive animator's upper-body response.");
                Assert.That(driver.RecoilWeight, Is.EqualTo(1f));
                Assert.That(
                    Vector3.Distance(
                        rightHand.position,
                        sockets.RightHandGrip.position),
                    Is.LessThan(0.01f),
                    "Recoil must preserve the authored primary-hand contact.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RecoilPitchAxisComesFromTheSolvedBarrelDirection()
        {
            Vector3 barrelDirection = Vector3.right;
            Vector3 pitchAxis = WeaponAimRig.CalculateRecoilPitchAxis(
                barrelDirection,
                Vector3.up,
                Vector3.right);
            Vector3 kickedDirection = Quaternion.AngleAxis(
                -9f,
                pitchAxis) * barrelDirection;

            Assert.That(
                Vector3.Angle(barrelDirection, kickedDirection),
                Is.EqualTo(9f).Within(0.001f));
            Assert.That(
                kickedDirection.y,
                Is.GreaterThan(0f),
                "A barrel aligned with the actor's right axis must still "
                + "receive a visible upward kick.");
        }

        [Test]
        public void InstantTracerIsStraightFromMuzzleToResolvedDestination()
        {
            var host = new GameObject("Straight Tracer Test Host");
            var grip = new GameObject("Straight Tracer Test Grip");
            var weaponPrefab = new GameObject("Straight Tracer Test Weapon");
            try
            {
                ConfigureTestRig(weaponPrefab, supportHand: false);
                WeaponPresentationDefinition definition = CreateDefinition(
                    "test.rifle",
                    weaponPrefab,
                    ActorAnimationPoseIds.Rifle);
                WeaponMountPresenter mount =
                    host.AddComponent<WeaponMountPresenter>();
                mount.Bind(grip.transform, presentAsLocalPlayer: false);
                WeaponRigSocketSet sockets = mount.Mount(definition);
                WeaponActionEffectsPresenter effects =
                    host.AddComponent<WeaponActionEffectsPresenter>();
                effects.Bind(mount);
                Vector3 mountedPosition = mount.HeldWeapon.transform.localPosition;
                Quaternion mountedRotation =
                    mount.HeldWeapon.transform.localRotation;
                Vector3 origin = sockets.Muzzle.position;
                Vector3 destination = origin + new Vector3(8f, 2f, 5f);

                effects.PresentShot(
                    definition,
                    origin,
                    destination,
                    drawTracer: true);
                effects.Tick(0.07f);

                LineRenderer tracer =
                    host.GetComponentInChildren<LineRenderer>();
                Assert.That(tracer, Is.Not.Null);
                Assert.That(tracer.positionCount, Is.EqualTo(2));
                Assert.That(tracer.GetPosition(0), Is.EqualTo(origin));
                Assert.That(tracer.GetPosition(1), Is.EqualTo(destination));
                Assert.That(
                    mount.HeldWeapon.transform.localPosition,
                    Is.EqualTo(mountedPosition),
                    "Shot effects must not add root recoil that the grip solver "
                    + "will cancel.");
                Assert.That(
                    Quaternion.Angle(
                        mountedRotation,
                        mount.HeldWeapon.transform.localRotation),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(grip);
                Object.DestroyImmediate(weaponPrefab);
            }
        }

        [Test]
        public void WeaponAimReplayRestoresExactLiveTransientState()
        {
            var actor = new GameObject("Replay Restore Actor");
            var weapon = new GameObject("Replay Restore Weapon");
            Transform muzzle = new GameObject("Replay Restore Muzzle").transform;
            Transform support = new GameObject("Replay Restore Support").transform;
            Transform hint = new GameObject("Replay Restore Hint").transform;
            try
            {
                actor.AddComponent<Animator>();
                WeaponAimRig rig = actor.AddComponent<WeaponAimRig>();
                muzzle.SetParent(weapon.transform, false);
                support.SetParent(weapon.transform, false);
                hint.SetParent(weapon.transform, false);
                rig.Bind(
                    actor.transform,
                    weapon.transform,
                    muzzle,
                    support,
                    hint,
                    handPositionWeight: 1f,
                    handRotationWeight: 1f,
                    elbowHintWeight: 0.5f,
                    handBlendSeconds: 0.2f,
                    maxAimCorrectionDegrees: 18f,
                    maxBodyAimCorrectionDegrees: 12f,
                    bodyCorrectionDegreesPerSecond: 120f,
                    weaponCorrectionDegreesPerSecond: 180f);
                rig.TickSupportBlend(0.05f);
                var liveAimPoint = new Vector3(4f, 1.5f, 7f);
                rig.SetAimPoint(liveAimPoint);
                rig.TriggerRecoil(6f, 0.1f, 0.3f);
                float liveBlend = rig.SupportBlendWeight;
                float liveRecoilElapsed = rig.RecoilElapsed;
                float liveRecoilWeight = rig.RecoilWeight;
                Quaternion liveBodyCorrection = rig.LocalBodyAimCorrection;
                Quaternion liveWeaponCorrection = rig.LocalAimCorrection;
                bool liveEnabled = rig.enabled;

                rig.BeginReplayPresentation();
                Assert.That(rig.HasAimPoint, Is.False);
                Assert.That(rig.IsRecoiling, Is.False);
                rig.SetReplaySupportWeightImmediate();
                rig.SetReplayRecoil(12f, 0.2f, 0.5f, 0.3f);

                rig.EndReplayPresentation();

                Assert.That(rig.HasAimPoint, Is.True);
                Assert.That(rig.CurrentAimPoint, Is.EqualTo(liveAimPoint));
                Assert.That(rig.SupportBlendWeight, Is.EqualTo(liveBlend));
                Assert.That(rig.RecoilElapsed, Is.EqualTo(liveRecoilElapsed));
                Assert.That(rig.RecoilWeight, Is.EqualTo(liveRecoilWeight));
                Assert.That(
                    rig.LocalBodyAimCorrection,
                    Is.EqualTo(liveBodyCorrection));
                Assert.That(
                    rig.LocalAimCorrection,
                    Is.EqualTo(liveWeaponCorrection));
                Assert.That(rig.enabled, Is.EqualTo(liveEnabled));
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void SupportHandSocketBlendsInAndClearsImmediately()
        {
            var actor = new GameObject("IK Blend Actor");
            var weapon = new GameObject("IK Blend Weapon");
            var muzzle = new GameObject("IK Blend Muzzle").transform;
            var support = new GameObject("IK Blend Support").transform;
            var hint = new GameObject("IK Blend Hint").transform;
            try
            {
                Animator animator = actor.AddComponent<Animator>();
                WeaponAimRig driver =
                    animator.gameObject.AddComponent<WeaponAimRig>();
                ActorAnimationProfile profile = LoadDefaultAnimationProfile();
                muzzle.SetParent(weapon.transform, false);
                support.SetParent(weapon.transform, false);
                hint.SetParent(weapon.transform, false);
                driver.Bind(
                    actor.transform,
                    weapon.transform,
                    muzzle,
                    support,
                    hint,
                    handPositionWeight: 1f,
                    handRotationWeight: 1f,
                    elbowHintWeight: 0.5f,
                    handBlendSeconds: 0.2f,
                    maxAimCorrectionDegrees: 18f,
                    maxBodyAimCorrectionDegrees:
                        profile.MaximumBodyAimCorrectionDegrees,
                    bodyCorrectionDegreesPerSecond:
                        profile.BodyAimDegreesPerSecond,
                    weaponCorrectionDegreesPerSecond:
                        profile.WeaponAimDegreesPerSecond);

                driver.TickSupportBlend(0.1f);
                Assert.That(driver.SupportBlendWeight,
                    Is.EqualTo(0.5f).Within(0.001f));
                driver.TickSupportBlend(0.1f);
                Assert.That(driver.SupportBlendWeight,
                    Is.EqualTo(1f).Within(0.001f));

                driver.ClearTarget();
                Assert.That(driver.SupportBlendWeight,
                    Is.Zero.Within(0.001f));
                driver.TickSupportBlend(0.1f);
                Assert.That(driver.SupportBlendWeight,
                    Is.Zero.Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void WeaponRigUsesOnePostAnimationSupportSolver()
        {
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                WeaponPresentationDefinition rifle =
                    WeaponPresentationCatalog.LoadDefault().Get(
                        "weapon.rifle");
                GameObject weapon = Object.Instantiate(
                    rifle.Prefab,
                    rightHand,
                    false);
                weapon.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                WeaponRigSocketSet sockets =
                    weapon.GetComponent<WeaponRigSocketSet>();
                WeaponAimRig driver = animator.gameObject
                    .AddComponent<WeaponAimRig>();
                driver.Bind(
                    player.transform,
                    weapon.transform,
                    sockets.Muzzle,
                    sockets.SupportHand,
                    sockets.SupportElbowHint,
                    sockets.SupportPositionWeight,
                    sockets.SupportRotationWeight,
                    sockets.SupportElbowHintWeight,
                    sockets.SupportBlendSeconds,
                    rifle.MaximumAimCorrectionDegrees,
                    presenter.Profile.MaximumBodyAimCorrectionDegrees,
                    presenter.Profile.BodyAimDegreesPerSecond,
                    presenter.Profile.WeaponAimDegreesPerSecond);

                Assert.That(
                    driver.FollowsAnimatedPrimaryGrip,
                    Is.True,
                    "The primary animation must remain authoritative while "
                    + "the support arm is solved once after animation.");
                Assert.That(
                    animator.GetComponent("RigBuilder"),
                    Is.Null,
                    "Weapon binding must not install a second animation "
                    + "graph that can overwrite the post-animation solve.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ProceduralBodyAimCorrectsPitchWithoutTwistingTowardYaw()
        {
            Quaternion yawOnly = WeaponAimProjector.CalculatePitchCorrection(
                Vector3.forward,
                Vector3.right,
                Vector3.up,
                maximumDegrees: 20f);
            Assert.That(
                Quaternion.Angle(Quaternion.identity, yawOnly),
                Is.LessThan(0.01f));

            Vector3 raisedAim = new Vector3(0f, 1f, 1f).normalized;
            Quaternion raised = WeaponAimProjector.CalculatePitchCorrection(
                Vector3.forward,
                raisedAim,
                Vector3.up,
                maximumDegrees: 12f);
            Assert.That(
                Quaternion.Angle(Quaternion.identity, raised),
                Is.EqualTo(12f).Within(0.01f));
            Assert.That(
                Vector3.Dot(raised * Vector3.forward, Vector3.up),
                Is.GreaterThan(0f));

            Vector3 loweredAim = new Vector3(0f, -1f, 1f).normalized;
            Quaternion lowered = WeaponAimProjector.CalculatePitchCorrection(
                Vector3.forward,
                loweredAim,
                Vector3.up,
                maximumDegrees: 12f);
            Assert.That(
                Vector3.Dot(lowered * Vector3.forward, Vector3.up),
                Is.LessThan(0f));
        }

        [Test]
        public void ProceduralWeaponAimCorrectsHorizontalModelOffset()
        {
            Quaternion correction = WeaponAimProjector.CalculateYawCorrection(
                Vector3.left,
                Vector3.forward,
                Vector3.up);

            Vector3 corrected = correction * Vector3.left;
            Assert.That(
                Vector3.Angle(corrected, Vector3.forward),
                Is.LessThan(0.01f));
            Assert.That(
                Vector3.Dot(corrected, Vector3.up),
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ProceduralBodyYawFollowsAimWithoutConsumingTheFullCameraTurn()
        {
            Quaternion correction = WeaponAimProjector.CalculateYawCorrection(
                Vector3.forward,
                Vector3.right,
                Vector3.up,
                maximumDegrees: 18f);

            Assert.That(
                Quaternion.Angle(Quaternion.identity, correction),
                Is.EqualTo(18f).Within(0.01f));
            Assert.That(
                Vector3.SignedAngle(
                    Vector3.forward,
                    correction * Vector3.forward,
                    Vector3.up),
                Is.EqualTo(18f).Within(0.01f));
        }

        private static WeaponPresentationDefinition CreateDefinition(
            string itemId,
            GameObject prefab,
            string animationSetId) =>
            new WeaponPresentationDefinition(
                itemId,
                prefab,
                animationSetId,
                null,
                drawsInstantTracer: true,
                effectSeconds: 0.1f,
                lineWidth: 0.02f);

        private static ActorAnimationCoordinator CreateUnboundAnimationCoordinator(
            GameObject actor)
        {
            ActorAnimationCoordinator presenter =
                actor.AddComponent<ActorAnimationCoordinator>();
            ActorAnimationProfile profile = LoadDefaultAnimationProfile();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("profile").objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return presenter;
        }

        private static ActorAnimationProfile LoadDefaultAnimationProfile()
        {
            ActorAnimationProfile profile = AssetDatabase.LoadAssetAtPath<
                ActorAnimationProfile>(
                "Assets/GritGud/Presentation/Actors/Animation/"
                + "DefaultPlayerAnimationProfile.asset");
            Assert.That(profile, Is.Not.Null);
            return profile;
        }

        private static void ConfigureTestRig(
            GameObject rig,
            bool supportHand)
        {
            WeaponRigSocketSet sockets = rig.AddComponent<WeaponRigSocketSet>();
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(rig.transform, false);
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(rig.transform, false);
            muzzle.localPosition = Vector3.forward;
            Transform hand = null;
            Transform hint = null;
            if (supportHand)
            {
                hand = new GameObject("Support Hand").transform;
                hand.SetParent(rig.transform, false);
                hint = new GameObject("Support Elbow Hint").transform;
                hint.SetParent(rig.transform, false);
            }

            var serialized = new SerializedObject(sockets);
            serialized.FindProperty("visualRoot").objectReferenceValue = visual;
            serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
            serialized.FindProperty("supportHand").objectReferenceValue = hand;
            serialized.FindProperty("supportElbowHint").objectReferenceValue = hint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameplaySession CreateSession()
        {
            var cost = new ActionCost(1, 0f, ActionMobility.Set);
            var rifle = new InventoryItemDefinition(
                "rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                cost,
                EquipmentEffectSet.None,
                CreateAttack("attack.rifle"));
            var launcher = new InventoryItemDefinition(
                "launcher",
                "Launcher",
                2,
                InventoryItemKind.Weapon,
                cost,
                EquipmentEffectSet.None,
                CreateAttack("attack.launcher"));
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[] { rifle, launcher },
                "rifle");
            return new GameplaySession(new ScenarioDefinition(
                "weapon-presenter-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static GameplaySession CreateContactSession(
            int targetMaximumWounds = int.MaxValue)
        {
            var knife = new InventoryItemDefinition(
                "knife",
                "Knife",
                5,
                InventoryItemKind.Weapon,
                new ActionCost(1, 0f, ActionMobility.Mobile),
                EquipmentEffectSet.None,
                new AttackDefinition(
                    "attack.knife",
                    "Knife strike",
                    new ActionCost(1, 0f, ActionMobility.Mobile),
                    2f,
                    contact: new ContactAttackDefinition(2f)));
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[] { knife },
                "knife");
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 1.5f),
                    0f),
                new TurnBudget(0, 8f),
                combat: new ActorCombatDefinition(
                    "neutral",
                    Array.Empty<string>(),
                    targetMaximumWounds));
            return new GameplaySession(new ScenarioDefinition(
                "contact-presentation-test",
                new ScenarioTimingDefinition(1f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static TargetExposureSnapshot CreateContactExposure() =>
            new TargetExposureSnapshot(
                "player",
                "target",
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Torso, 1, 1),
                });

        private static AttackDefinition CreateAttack(string id) =>
            new AttackDefinition(
                id,
                id,
                new ActionCost(1, 0f, ActionMobility.Set),
                2f,
                accuracyDecay: AccuracyDecayDefinition.None);
    }
}
