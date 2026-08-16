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
                Assert.That(actor.GetComponent<
                    ActorLocomotionAnimationPresenter>().enabled,
                    Is.EqualTo(locomotionEnabled));
            }
            finally
            {
                registry?.Dispose();
                world?.Dispose();
                if (registry == null)
                    Object.Destroy(actor);
            }
        }
    }
}
