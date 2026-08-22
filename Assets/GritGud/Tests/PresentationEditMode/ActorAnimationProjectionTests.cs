using System;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class ActorAnimationProjectionTests
    {
        [Test]
        public void InjuryOverlayProjectsGaitAndImpairedSideDeterministically()
        {
            var mobility = new ActorMobilityCapability(
                ActorGait.SevereLimp,
                ActorImpairedSide.Left,
                35,
                40,
                canSprint: false,
                canStand: true);
            var capabilities = new ActorCapabilityState(
                35, 40, 80, 60, 60, 70,
                true, true, true, true,
                mobility,
                leftGripCapacity: 40,
                rightGripCapacity: 80,
                leftThrowCapacity: 40,
                rightThrowCapacity: 80,
                isActive: true);

            ActorInjuryAnimationOverlay first =
                ActorInjuryAnimationOverlayProjector.Project(capabilities);
            ActorInjuryAnimationOverlay repeated =
                ActorInjuryAnimationOverlayProjector.Project(capabilities);

            Assert.That(first.BodyRollDegrees, Is.LessThan(0f));
            Assert.That(first.ImpairedLegPitchDegrees,
                Is.GreaterThan(10f));
            Assert.That(first.LeftArmSagDegrees, Is.LessThan(0f));
            Assert.That(first.BodyPitchDegrees,
                Is.EqualTo(repeated.BodyPitchDegrees));
            Assert.That(first.BodyRollDegrees,
                Is.EqualTo(repeated.BodyRollDegrees));
        }

        [Test]
        public void AnimationChannelPlanDeclaresStableOwnershipAndOrder()
        {
            Assert.That(
                ActorAnimationChannelPlan.Channels.Count,
                Is.EqualTo(8));
            Assert.That(
                ActorAnimationChannelPlan.Locomotion.BodyRegion,
                Is.EqualTo(BodyRegion.WholeBody));
            Assert.That(
                ActorAnimationChannelPlan.Locomotion.BlendMode,
                Is.EqualTo(AnimationChannelBlendMode.Base));
            Assert.That(
                ActorAnimationChannelPlan.TurnInPlace.BodyRegion,
                Is.EqualTo(BodyRegion.PelvisAndLegs));
            Assert.That(
                ActorAnimationChannelPlan.WeaponPose.BodyRegion,
                Is.EqualTo(BodyRegion.TorsoAndArms));
            Assert.That(
                ActorAnimationChannelPlan.Recoil.BlendMode,
                Is.EqualTo(AnimationChannelBlendMode.Additive));
            Assert.That(
                ActorAnimationChannelPlan.Recoil.BodyRegion,
                Is.EqualTo(BodyRegion.TorsoAndArms));
            Assert.That(
                ActorAnimationChannelPlan.Actions.BlendMode,
                Is.EqualTo(AnimationChannelBlendMode.Override));
            Assert.That(
                ActorAnimationChannelPlan.Actions.BodyRegion,
                Is.EqualTo(BodyRegion.TorsoAndArms));
            Assert.That(
                ActorAnimationChannelPlan.Displacements.BodyRegion,
                Is.EqualTo(BodyRegion.WholeBody));
            Assert.That(
                ActorAnimationChannelPlan.Displacements.BlendMode,
                Is.EqualTo(AnimationChannelBlendMode.Override));
            Assert.That(
                ActorAnimationChannelPlan.Reactions.BodyRegion,
                Is.EqualTo(BodyRegion.WholeBody));
            Assert.That(
                ActorAnimationChannelPlan.Reactions.BlendMode,
                Is.EqualTo(AnimationChannelBlendMode.Override));
            Assert.That(
                ActorAnimationChannelPlan.WeaponAim.ExecutionStage,
                Is.EqualTo(AnimationExecutionStage.PostAnimation));
            Assert.That(
                ActorAnimationChannelPlan.Channels[0].Priority,
                Is.LessThan(ActorAnimationChannelPlan.Channels[1].Priority));
            Assert.That(
                ActorAnimationChannelPlan.Channels[1].Priority,
                Is.LessThan(ActorAnimationChannelPlan.Channels[2].Priority));
            Assert.That(
                ActorAnimationChannelPlan.Channels[2].Priority,
                Is.LessThan(ActorAnimationChannelPlan.Channels[3].Priority));
            Assert.That(
                ActorAnimationChannelPlan.Channels[3].Priority,
                Is.LessThan(ActorAnimationChannelPlan.Channels[4].Priority));
            Assert.That(
                ActorAnimationChannelPlan.Channels[4].Priority,
                Is.LessThan(ActorAnimationChannelPlan.Channels[5].Priority));
            Assert.That(
                ActorAnimationChannelPlan.Channels[5].Priority,
                Is.LessThan(ActorAnimationChannelPlan.Channels[6].Priority));
            Assert.That(
                ActorAnimationChannelPlan.Channels[6].Priority,
                Is.LessThan(ActorAnimationChannelPlan.Channels[7].Priority));
        }

        [Test]
        public void ThrowActionEntersTheAuthoredActionChannel()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = UnityEngine.Object.Instantiate(prefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);

                int layer = animator.GetLayerIndex(
                    ActorAnimationParameters.ActionLayerName);
                Assert.That(layer, Is.GreaterThanOrEqualTo(0));
                Assert.That(animator.GetLayerWeight(layer), Is.Zero);

                Assert.That(
                    presenter.TryPresentThrow(),
                    Is.True);
                animator.Update(0.1f);

                Assert.That(animator.GetLayerWeight(layer), Is.EqualTo(1f));
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(layer).IsName(
                        ActorAnimationParameters.ThrowStateName),
                    Is.True);
                Assert.That(
                    presenter.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.Throw));
                Assert.That(presenter.ActionSequence, Is.EqualTo(1));

                for (int frame = 0; frame < 100; frame++)
                {
                    animator.Update(0.05f);
                }

                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(layer).IsName(
                        ActorAnimationParameters.NoActionStateName),
                    Is.True);
                Assert.That(animator.GetLayerWeight(layer), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void PushActionUsesItsDedicatedFullBodyChannelAndReleases()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = UnityEngine.Object.Instantiate(prefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);

                int layer = animator.GetLayerIndex(
                    ActorAnimationParameters.DisplacementLayerName);
                Assert.That(layer, Is.GreaterThanOrEqualTo(0));
                Assert.That(animator.GetLayerWeight(layer), Is.Zero);
                Assert.That(
                    presenter.TryRequestAction(ActorAnimationAction.Push),
                    Is.True);
                animator.Update(0.1f);

                Assert.That(animator.GetLayerWeight(layer), Is.EqualTo(1f));
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(layer).IsName(
                        ActorAnimationParameters.PushStateName),
                    Is.True);

                for (int frame = 0; frame < 100; frame++)
                    animator.Update(0.05f);

                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(layer).IsName(
                        ActorAnimationParameters.NoDisplacementStateName),
                    Is.True);
                Assert.That(animator.GetLayerWeight(layer), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void DeferredIncapacitationUsesCommittedWoundRegionAtContact()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = UnityEngine.Object.Instantiate(prefab);
            try
            {
                ActorAnimationCoordinator animation =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);

                animation.DeferIncapacitationPresentation();
                animation.PresentIncapacitation(
                    Quaternion.Euler(0f, 0f, 90f),
                    Vector3.down);
                Assert.That(animation.LastRequestedAction, Is.Null);

                Assert.That(
                    animation.PresentWoundReaction(
                        TargetRegionId.RightArm,
                        incapacitated: true),
                    Is.True);
                animator.Update(0.1f);

                Assert.That(
                    animation.LastRequestedAction,
                    Is.EqualTo(
                        ActorAnimationAction.IncapacitateShoulder));
                int layer = animator.GetLayerIndex(
                    ActorAnimationParameters.ReactionLayerName);
                Assert.That(animator.GetLayerWeight(layer), Is.EqualTo(1f));
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(layer).IsName(
                        ActorAnimationParameters.ShoulderFallStateName),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void PinnedActorKeepsDownedPoseAfterNonIncapacitatingWound()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = UnityEngine.Object.Instantiate(prefab);
            try
            {
                ActorAnimationCoordinator animation =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Assert.That(
                    animation.TryRequestAction(
                        ActorAnimationAction.Incapacitate),
                    Is.True);

                Assert.That(
                    animation.PresentWoundReaction(
                        TargetRegionId.Torso,
                        incapacitated: false,
                        pinned: true),
                    Is.False);
                Assert.That(
                    animation.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.Incapacitate));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void KnifeActionCanBeInterruptedBackToTheOwnedIdleState()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = UnityEngine.Object.Instantiate(prefab);
            try
            {
                ActorAnimationCoordinator animation =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);

                Assert.That(
                    animation.TryRequestAction(
                        ActorAnimationAction.ContactStrike),
                    Is.True);
                animator.Update(0.1f);
                Assert.That(
                    animation.InterruptAction(
                        ActorAnimationAction.ContactStrike),
                    Is.True);
                animator.Update(0.2f);

                int layer = animator.GetLayerIndex(
                    ActorAnimationParameters.ActionLayerName);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(layer).IsName(
                        ActorAnimationParameters.NoActionStateName),
                    Is.True);
                Assert.That(animator.GetLayerWeight(layer), Is.Zero);
                Assert.That(animation.ActionSequence, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void ClearingPinnedProfileReleasesHeldFallPoseAndRestoresStanceTargeting()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = UnityEngine.Object.Instantiate(prefab);
            try
            {
                ActorAnimationCoordinator animation =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var hooks = actor.AddComponent<
                    GameplayTurnReplayActorStateHooks>();
                var targets = actor.AddComponent<
                    ActorTargetProfilePresenter>();
                targets.Bind(
                    actor.GetComponent<ActorStancePresenter>(),
                    hooks);
                animator.Update(0f);

                hooks.PresentPinState(new ActorPinState(
                    "actor",
                    "prop",
                    displacementSequence: 1,
                    new DisplacementContactEvidence(
                        "actor",
                        new GameplayPosition(0f, 0f, 0f),
                        new GameplayPosition(0f, 1f, 0f),
                        overlapDepth: 0.2f)));
                animator.Update(0.2f);

                Assert.That(
                    targets.ProfileKind,
                    Is.EqualTo(ActorTargetProfileKind.PinnedDown));
                Assert.That(
                    animation.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.Incapacitate));

                hooks.PresentPinState(null);
                animator.Update(0.2f);

                int layer = animator.GetLayerIndex(
                    ActorAnimationParameters.ReactionLayerName);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(layer).IsName(
                        ActorAnimationParameters.NoReactionStateName),
                    Is.True);
                Assert.That(animator.GetLayerWeight(layer), Is.Zero);
                Assert.That(
                    targets.ProfileKind,
                    Is.EqualTo(ActorTargetProfileKind.Standing));
                Assert.That(
                    animation.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.Interact));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void AnimatorOverrideChannelRequiresAnOwnedLayer()
        {
            Assert.Throws<ArgumentException>(() =>
                new AnimationChannelDefinition(
                    AnimationChannelId.WeaponPose,
                    AnimationMotionSource.AnimatorController,
                    BodyRegion.TorsoAndArms,
                    AnimationChannelBlendMode.Override,
                    priority: 1,
                    AnimationWeightPolicy.Profile,
                    AnimationExecutionStage.Animator));
        }

        [Test]
        public void ActorForwardVelocityMapsToForwardBlendInput()
        {
            ActorLocomotionAnimationState state = ActorLocomotionAnimationProjector.Project(
                new Vector3(5f, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                grounded: true,
                turnDegreesPerSecond: 0f,
                locomotionReferenceSpeed: 5f,
                turnReferenceDegreesPerSecond: 360f);

            Assert.That(state.MoveX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(state.MoveY, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(state.Speed, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(state.Grounded, Is.True);
        }

        [Test]
        public void DiagonalVelocityIsClampedToBlendTreeUnitCircle()
        {
            ActorLocomotionAnimationState state = ActorLocomotionAnimationProjector.Project(
                new Vector3(5f, 0f, 5f),
                Quaternion.identity,
                grounded: false,
                turnDegreesPerSecond: 0f,
                locomotionReferenceSpeed: 5f,
                turnReferenceDegreesPerSecond: 360f);

            float blendMagnitude = new Vector2(state.MoveX, state.MoveY).magnitude;
            Assert.That(blendMagnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(state.Speed, Is.EqualTo(Mathf.Sqrt(50f)).Within(0.0001f));
            Assert.That(state.Grounded, Is.False);
        }

        [Test]
        public void VerticalVelocityDoesNotDriveLocomotion()
        {
            ActorLocomotionAnimationState state = ActorLocomotionAnimationProjector.Project(
                new Vector3(0f, 20f, 0f),
                Quaternion.identity,
                grounded: false,
                turnDegreesPerSecond: -720f,
                locomotionReferenceSpeed: 5f,
                turnReferenceDegreesPerSecond: 360f);

            Assert.That(state.MoveX, Is.Zero);
            Assert.That(state.MoveY, Is.Zero);
            Assert.That(state.Speed, Is.Zero);
            Assert.That(state.TurnRate, Is.EqualTo(-1f));
        }

        [Test]
        public void InvalidReferenceSpeedIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ActorLocomotionAnimationProjector.Project(
                    Vector3.zero,
                    Quaternion.identity,
                    grounded: true,
                    turnDegreesPerSecond: 0f,
                    locomotionReferenceSpeed: 0f,
                    turnReferenceDegreesPerSecond: 360f));
        }

        [Test]
        public void RenderedYawDeltaProjectsAimDrivenRightTurnRate()
        {
            float turnRate = ActorRenderedTurnRateProjector.Project(
                Quaternion.Euler(0f, 350f, 0f),
                Quaternion.Euler(0f, 10f, 0f),
                0.1f);

            Assert.That(turnRate, Is.EqualTo(200f).Within(0.001f));
        }

        [Test]
        public void RenderedYawDeltaProjectsAimDrivenLeftTurnRate()
        {
            float turnRate = ActorRenderedTurnRateProjector.Project(
                Quaternion.Euler(0f, 10f, 0f),
                Quaternion.Euler(0f, 350f, 0f),
                0.1f);

            Assert.That(turnRate, Is.EqualTo(-200f).Within(0.001f));
        }

        [Test]
        public void RenderedYawDeltaIgnoresNonPositiveDeltaTime()
        {
            float turnRate = ActorRenderedTurnRateProjector.Project(
                Quaternion.identity,
                Quaternion.Euler(0f, 90f, 0f),
                0f);

            Assert.That(turnRate, Is.Zero);
        }

        [Test]
        public void TurnInPlaceSignalMakesOrdinaryAimRotationVisible()
        {
            var signal = new ActorTurnInPlaceSignal();
            ActorTurnInPlaceSettings settings = CreateTurnSettings();

            float value = signal.Update(
                measuredDegreesPerSecond: 30f,
                referenceDegreesPerSecond: 540f,
                deltaTime: 1f / 60f,
                settings);

            Assert.That(
                value,
                Is.GreaterThanOrEqualTo(
                    settings.MinimumActiveBlend));
        }

        [Test]
        public void TurnInPlaceSignalIgnoresDestinationJitterUntilActivationThreshold()
        {
            var signal = new ActorTurnInPlaceSignal();
            ActorTurnInPlaceSettings settings = CreateTurnSettings();

            float value = signal.Update(
                settings.ActivationDegreesPerSecond - 1f,
                540f,
                1f / 60f,
                settings);

            Assert.That(value, Is.Zero);
        }

        [Test]
        public void TurnInPlaceSignalUsesLowerThresholdWhileTurnContinues()
        {
            var signal = new ActorTurnInPlaceSignal();
            ActorTurnInPlaceSettings settings = CreateTurnSettings();
            signal.Update(30f, 540f, 1f / 60f, settings);

            float continued = signal.Update(
                settings.SustainDegreesPerSecond + 1f,
                540f,
                1f / 60f,
                settings);

            Assert.That(
                continued,
                Is.GreaterThanOrEqualTo(
                    settings.MinimumActiveBlend));
        }

        [Test]
        public void TurnInPlaceSignalRequiresRealInputToReverseDirection()
        {
            var signal = new ActorTurnInPlaceSignal();
            ActorTurnInPlaceSettings settings = CreateTurnSettings();
            signal.Update(30f, 540f, 1f / 60f, settings);

            float ignoredJitter = signal.Update(
                -(settings.ActivationDegreesPerSecond - 1f),
                540f,
                1f / 60f,
                settings);
            float reversed = signal.Update(
                -30f,
                540f,
                1f / 60f,
                settings);

            Assert.That(ignoredJitter, Is.GreaterThan(0f));
            Assert.That(reversed, Is.LessThan(0f));
        }

        [Test]
        public void TurnInPlaceSignalPreservesDirectionAndHoldsShortTurns()
        {
            var signal = new ActorTurnInPlaceSignal();
            ActorTurnInPlaceSettings settings = CreateTurnSettings();
            float active = signal.Update(
                -30f,
                540f,
                1f / 60f,
                settings);
            float held = signal.Update(0f, 540f, 0.05f, settings);

            Assert.That(active, Is.LessThan(0f));
            Assert.That(held, Is.EqualTo(active).Within(0.0001f));
        }

        [Test]
        public void TurnInPlaceSignalReleasesAfterRotationStops()
        {
            var signal = new ActorTurnInPlaceSignal();
            ActorTurnInPlaceSettings settings = CreateTurnSettings();
            signal.Update(30f, 540f, 1f / 60f, settings);
            signal.Update(
                0f,
                540f,
                settings.ReleaseDelaySeconds,
                settings);

            float released = signal.Update(
                0f,
                540f,
                settings.ReleaseSeconds,
                settings);

            Assert.That(released, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void TurnLayerActivatesOnlyForStationaryGroundedStandingActor()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = UnityEngine.Object.Instantiate(prefab);
            try
            {
                ActorAnimationCoordinator presenter =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                int turnLayer = animator.GetLayerIndex(
                    ActorAnimationParameters.TurnLayerName);

                presenter.PresentStance(ActorStance.Standing);
                presenter.PresentFrame(
                    new ActorAnimationFrame(
                        new ActorLocomotionAnimationState(
                        0f,
                        0f,
                        0f,
                        true,
                        1f),
                        ActorStance.Standing),
                    1f / 60f);
                Assert.That(
                    animator.GetLayerWeight(turnLayer),
                    Is.EqualTo(
                        presenter.Profile.TurnInPlace.MaximumLayerWeight));
                Assert.That(
                    animator.GetFloat(ActorAnimationParameters.TurnRate),
                    Is.EqualTo(
                        presenter.Profile.TurnInPlace.MaximumPoseBlend));

                presenter.PresentFrame(
                    new ActorAnimationFrame(
                        new ActorLocomotionAnimationState(
                        0f,
                        1f,
                        1f,
                        true,
                        presenter.Profile.TurnInPlace.MinimumActiveBlend),
                        ActorStance.Standing),
                    1f / 60f);
                Assert.That(animator.GetLayerWeight(turnLayer), Is.Zero);

                presenter.PresentStance(ActorStance.Crouched);
                presenter.PresentFrame(
                    new ActorAnimationFrame(
                        new ActorLocomotionAnimationState(
                        0f,
                        0f,
                        0f,
                        true,
                        presenter.Profile.TurnInPlace.MinimumActiveBlend),
                        ActorStance.Crouched),
                    1f / 60f);
                Assert.That(animator.GetLayerWeight(turnLayer), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void BindingAnimatorAlwaysDisablesRootMotion()
        {
            var root = new GameObject("Actor Animation Test");
            ActorAnimationProfile profile = Resources.Load<GameObject>(
                    "Actors/DefaultPlayerActor")
                .GetComponent<ActorAnimationCoordinator>()
                .Profile;

            try
            {
                Animator animator = root.AddComponent<Animator>();
                animator.applyRootMotion = true;
                ActorAnimationCoordinator presenter =
                    root.AddComponent<ActorAnimationCoordinator>();

                presenter.Bind(animator, profile);

                Assert.That(animator.applyRootMotion, Is.False);
                Assert.That(presenter.TargetAnimator, Is.SameAs(animator));
                Assert.That(presenter.Profile, Is.SameAs(profile));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ActorTurnInPlaceSettings CreateTurnSettings() =>
            new ActorTurnInPlaceSettings(
                activationSpeed: 18f,
                sustainSpeed: 6f,
                activeBlendFloor: 0.65f,
                releaseDelay: 0.12f,
                releaseDuration: 0.16f,
                stationarySpeedLimit: 0.1f,
                layerWeight: 1f,
                poseBlendLimit: 0.75f,
                statePlaybackSpeed: 0.65f);
    }
}
