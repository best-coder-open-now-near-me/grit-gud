using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
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
