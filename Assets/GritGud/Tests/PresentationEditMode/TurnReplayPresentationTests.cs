using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class TurnReplayPresentationTests
    {
        [Test]
        public void ActorReplayAppliesCorePoseWithoutAnimationComponents()
        {
            var actor = new GameObject("Animation-Free Replay Actor");
            try
            {
                actor.AddComponent<CharacterController>();
                actor.AddComponent<ActorStancePresenter>();
                var view = new GameplayActorView(
                    "plain-actor",
                    string.Empty,
                    targetable: false,
                    actor);
                var presenter = new GameplayTurnReplayActorPresenter(view);

                presenter.Begin();
                presenter.Present(
                    new GameplayActorSnapshot(
                        "plain-actor",
                        new GameplayActorPose(
                            new GameplayPosition(4f, 0f, -2f),
                            90f,
                            ActorStance.Crouched),
                        new TurnBudget(2, 3f),
                        new ActorWoundSnapshot("plain-actor", 0, 0f)),
                    action: null);

                Assert.That(
                    actor.transform.position,
                    Is.EqualTo(new Vector3(4f, 0f, -2f)));
                Assert.That(view.Stance.Stance, Is.EqualTo(ActorStance.Crouched));

                presenter.Dispose();
                Assert.That(actor.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(view.Stance.Stance, Is.EqualTo(ActorStance.Standing));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void WoundVariantsSeekAndRestoreExactLivePresentation()
        {
            var actor = new GameObject("Replay Wound Actor");
            var clear = new GameObject("Clear Torso");
            var wounded = new GameObject("Wounded Torso");
            clear.transform.SetParent(actor.transform, false);
            wounded.transform.SetParent(actor.transform, false);
            ActorWoundVariantPresenter presenter =
                actor.AddComponent<ActorWoundVariantPresenter>();
            presenter.Configure(new ActorWoundVariantBinding(
                TargetRegionId.Torso,
                clear,
                wounded));
            var live = new ActorWoundSnapshot("actor", 0, 0f);
            var replay = new ActorWoundSnapshot(
                "actor",
                headWounds: 0,
                torsoWounds: 1,
                leftArmWounds: 0,
                rightArmWounds: 0,
                leftLegWounds: 0,
                rightLegWounds: 0,
                movementPenalty: 1f);
            try
            {
                presenter.PresentAuthoritative(live);
                Assert.That(clear.activeSelf, Is.True);
                Assert.That(wounded.activeSelf, Is.False);

                presenter.BeginReplayPresentation();
                presenter.PresentReplay(replay);
                Assert.That(clear.activeSelf, Is.False);
                Assert.That(wounded.activeSelf, Is.True);

                presenter.EndReplayPresentation();
                Assert.That(clear.activeSelf, Is.True);
                Assert.That(wounded.activeSelf, Is.False);
                Assert.That(presenter.CurrentWounds, Is.EqualTo(live));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void AnimatorReplaySeekRestoresLiveStateAndSequence()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = Object.Instantiate(prefab);
            try
            {
                ActorAnimationCoordinator animation =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);
                animation.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                Assert.That(animation.TryPresentWeaponFire(), Is.True);
                animator.Update(0.05f);
                int liveSequence = animation.ActionSequence;
                ActorAnimationAction? liveAction =
                    animation.LastRequestedAction;
                float liveSpeed = animator.speed;

                animation.BeginReplayPresentation();
                animation.PresentWeaponPose(ActorAnimationPoseIds.Empty);
                animation.PresentReplayAction(
                    ActorStance.Crouched,
                    ActorAnimationAction.Throw,
                    0.5f);

                Assert.That(animator.speed, Is.Zero);
                Assert.That(animation.ReplayAction,
                    Is.EqualTo(ActorAnimationAction.Throw));
                Assert.That(animation.ReplayActionProgress,
                    Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(animation.ActionSequence,
                    Is.EqualTo(liveSequence));
                Assert.That(animation.CurrentWeaponAnimationSetId,
                    Is.EqualTo(ActorAnimationPoseIds.Empty));

                animation.EndReplayPresentation();

                Assert.That(animator.speed, Is.EqualTo(liveSpeed));
                Assert.That(animation.ActionSequence,
                    Is.EqualTo(liveSequence));
                Assert.That(animation.LastRequestedAction,
                    Is.EqualTo(liveAction));
                Assert.That(animation.CurrentWeaponAnimationSetId,
                    Is.EqualTo(ActorAnimationPoseIds.Rifle));
                Assert.That(animation.ReplayAction, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

    }
}
