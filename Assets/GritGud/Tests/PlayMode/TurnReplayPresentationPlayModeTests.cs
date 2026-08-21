using System.Collections;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GritGud.PlayMode.Tests
{
    public sealed class TurnReplayPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator ActorReplayLifecycleRestoresLivePresentation()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = Object.Instantiate(prefab);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                yield return null;
                world = new LevelWorld(
                    new GameObject("Replay Lifecycle World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "player",
                    ActorPresentationIds.DefaultPlayer,
                    targetable: false,
                    actor);
                GameplayActorView view = registry.GetActor("player");
                var clear = new GameObject("Clear Torso");
                var wounded = new GameObject("Wounded Torso");
                clear.transform.SetParent(actor.transform, false);
                wounded.transform.SetParent(actor.transform, false);
                view.Wounds.Configure(new ActorWoundVariantBinding(
                    TargetRegionId.Torso,
                    clear,
                    wounded));
                view.Wounds.PresentAuthoritative(
                    new ActorWoundSnapshot("player", 0, 0f));
                ActorPinState livePin = CreatePinState(
                    "player",
                    "live-crate",
                    displacementSequence: 4);
                view.ReplayActions.PresentPinState(livePin);

                ActorAnimationCoordinator animation =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animation.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                Assert.That(animation.TryPresentWeaponFire(), Is.True);
                yield return null;
                int liveActionSequence = animation.ActionSequence;
                Vector3 livePosition = actor.transform.position;
                Quaternion liveRotation = actor.transform.rotation;
                bool locomotionEnabled = actor.GetComponent<
                    ActorLocomotionAnimationPresenter>().enabled;
                ThirdPersonMotor motor = actor.GetComponent<ThirdPersonMotor>();
                ExplorationMovementInput movementInput = actor.GetComponent<
                    ExplorationMovementInput>();
                bool motorEnabled = motor.enabled;
                bool movementInputEnabled = movementInput.enabled;

                using (var replay =
                    new GameplayTurnReplayActorPresenter(view))
                {
                    replay.Begin();
                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(4f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(2, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f)),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Throw,
                            journalSequence: 1,
                            normalizedProgress: 0.5f));

                    Assert.That(actor.transform.position,
                        Is.EqualTo(new Vector3(4f, 0f, 3f)));
                    Assert.That(view.Stance.Stance,
                        Is.EqualTo(ActorStance.Crouched));
                    Assert.That(clear.activeSelf, Is.False);
                    Assert.That(wounded.activeSelf, Is.True);
                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Throw));
                    Assert.That(view.ReplayActions.CurrentState.Kind,
                        Is.EqualTo(TurnReplayActorActionKind.Throw));
                    Assert.That(animator.speed, Is.Zero);
                    Assert.That(actor.GetComponent<
                        ActorLocomotionAnimationPresenter>().enabled,
                        Is.False);
                    Assert.That(motor.enabled, Is.False);
                    Assert.That(movementInput.enabled, Is.False);

                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(4.5f, 1.25f, 3f),
                                90f,
                                ActorStance.Standing),
                            new TurnBudget(2, 4f)),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Jump,
                            journalSequence: 2,
                            normalizedProgress: 0.5f));

                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Jump));
                    Assert.That(view.ReplayActions.CurrentState.Kind,
                        Is.EqualTo(TurnReplayActorActionKind.Jump));

                    ActorPinState replayPin = CreatePinState(
                        "player",
                        "replay-crate",
                        displacementSequence: 9);
                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(5f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(2, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f),
                            null,
                            EquipmentEffectSet.None,
                            pinState: replayPin),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Pinned,
                            journalSequence: 3,
                            normalizedProgress: 0.75f));

                    Assert.That(view.ReplayActions.CurrentPinState,
                        Is.SameAs(replayPin));
                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Incapacitate));
                    Assert.That(
                        view.TargetProfile.ProfileKind,
                        Is.EqualTo(ActorTargetProfileKind.PinnedDown));

                    GameplayActorSnapshot contactSnapshot =
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(5f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(2, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f));
                    replay.Present(
                        contactSnapshot,
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Reaction,
                            journalSequence: 4,
                            normalizedProgress: 0.2f,
                            contactReaction: true,
                            resultingWoundCount: 1,
                            hitRegion: TargetRegionId.Torso));
                    Assert.That(animation.ReplayAction, Is.Null);

                    replay.Present(
                        contactSnapshot,
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Reaction,
                            journalSequence: 4,
                            normalizedProgress: 0.7f,
                            contactReaction: true,
                            resultingWoundCount: 1,
                            hitRegion: TargetRegionId.Torso));
                    Assert.That(
                        animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.HitReaction));
                    Assert.That(
                        animation.ReplayActionProgress,
                        Is.EqualTo(0.5f).Within(0.001f));

                    GameplayActorSnapshot incapacitatedSnapshot =
                        new GameplayActorSnapshot(
                            "player",
                            contactSnapshot.Pose,
                            contactSnapshot.TurnBudget,
                            contactSnapshot.Wounds,
                            equippedItemId: null,
                            equipmentEffects: EquipmentEffectSet.None,
                            maximumWounds: 1);
                    replay.Present(
                        incapacitatedSnapshot,
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Reaction,
                            journalSequence: 5,
                            normalizedProgress: 0.7f,
                            contactReaction: true,
                            resultingWoundCount: 1,
                            hitRegion: TargetRegionId.Torso));
                    Assert.That(
                        animation.ReplayAction,
                        Is.EqualTo(
                            ActorAnimationAction.IncapacitateShoulder));

                    replay.Present(incapacitatedSnapshot, action: null);
                    Assert.That(
                        animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Incapacitate));
                    Assert.That(animation.ReplayActionProgress, Is.EqualTo(1f));

                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(5f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(0, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f)),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.GetUp,
                            journalSequence: 6,
                            normalizedProgress: 0.25f));

                    Assert.That(view.ReplayActions.CurrentPinState, Is.Null);
                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Interact));
                    Assert.That(
                        view.TargetProfile.ProfileKind,
                        Is.EqualTo(ActorTargetProfileKind.Crouched));

                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(5f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(0, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f)),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Push,
                            journalSequence: 7,
                            normalizedProgress: 0.4f));

                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Push));
                    Assert.That(animation.ReplayActionProgress,
                        Is.EqualTo(0.4f).Within(0.001f));
                }

                Assert.That(actor.transform.position,
                    Is.EqualTo(livePosition));
                Assert.That(actor.transform.rotation,
                    Is.EqualTo(liveRotation));
                Assert.That(view.Stance.Stance,
                    Is.EqualTo(ActorStance.Standing));
                Assert.That(clear.activeSelf, Is.True);
                Assert.That(wounded.activeSelf, Is.False);
                Assert.That(animation.ActionSequence,
                    Is.EqualTo(liveActionSequence));
                Assert.That(animation.ReplayAction, Is.Null);
                Assert.That(view.ReplayActions.CurrentState, Is.Null);
                Assert.That(view.ReplayActions.CurrentPinState,
                    Is.SameAs(livePin));
                Assert.That(
                    view.TargetProfile.ProfileKind,
                    Is.EqualTo(ActorTargetProfileKind.PinnedDown));
                Assert.That(actor.GetComponent<
                    ActorLocomotionAnimationPresenter>().enabled,
                    Is.EqualTo(locomotionEnabled));
                Assert.That(motor.enabled, Is.EqualTo(motorEnabled));
                Assert.That(movementInput.enabled,
                    Is.EqualTo(movementInputEnabled));
            }
            finally
            {
                registry?.Dispose();
                world?.Dispose();
                if (registry == null)
                    Object.Destroy(actor);
            }
        }

        [UnityTest]
        public IEnumerator TraversalPlaybackUsesAuthoredJumpAndRestoresMotorState()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = Object.Instantiate(prefab);
            try
            {
                yield return null;
                ThirdPersonMotor motor = actor.GetComponent<ThirdPersonMotor>();
                CharacterController controller =
                    actor.GetComponent<CharacterController>();
                ActorLocomotionAnimationPresenter locomotion = actor.GetComponent<
                    ActorLocomotionAnimationPresenter>();
                ActorAnimationCoordinator animation = actor.GetComponent<
                    ActorAnimationCoordinator>();
                bool motorEnabled = motor.enabled;
                bool controllerEnabled = controller.enabled;
                bool locomotionEnabled = locomotion.enabled;
                var route = new MovementRouteRecord(
                    "player",
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 0f),
                        0f),
                    new TurnBudget(4, 8f),
                    new[]
                    {
                        new MovementRouteSegmentRecord(
                            new GameplayPosition(0f, 0f, 0f),
                            new GameplayPosition(0f, 0f, 2f),
                            MovementRouteSegmentKind.Jump,
                            "jump.playmode",
                            "traversal.jump",
                            2f,
                            0,
                            1.25f,
                            0.8f),
                    });
                var playback = new MovementRoutePlaybackPresenter(motor);

                playback.Begin(route);

                Assert.That(motor.enabled, Is.False);
                Assert.That(controller.enabled, Is.False);
                Assert.That(locomotion.enabled, Is.False);
                Assert.That(playback.Tick(0.4f), Is.False);
                Assert.That(actor.transform.position.y,
                    Is.EqualTo(1.25f).Within(0.001f));
                Assert.That(animation.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.Jump));
                Assert.That(playback.Tick(0.4f), Is.True);

                Assert.That(actor.transform.position,
                    Is.EqualTo(new Vector3(0f, 0f, 2f)));
                Assert.That(motor.enabled, Is.EqualTo(motorEnabled));
                Assert.That(controller.enabled, Is.EqualTo(controllerEnabled));
                Assert.That(locomotion.enabled, Is.EqualTo(locomotionEnabled));
            }
            finally
            {
                Object.Destroy(actor);
            }
        }

        private static ActorPinState CreatePinState(
            string actorId,
            string propId,
            long displacementSequence)
        {
            return new ActorPinState(
                actorId,
                propId,
                displacementSequence,
                new DisplacementContactEvidence(
                    actorId,
                    new GameplayPosition(0f, 0.5f, 0f),
                    new GameplayPosition(0f, 1f, 0f),
                    0.1f));
        }
    }
}
