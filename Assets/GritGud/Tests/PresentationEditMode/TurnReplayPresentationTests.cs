using System;
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
        public void ArmedReplayActorWithoutWeaponMountFailsWithIdentity()
        {
            var actor = new GameObject("Armed Replay Actor Missing Weapon");
            try
            {
                actor.AddComponent<CharacterController>();
                actor.AddComponent<ActorStancePresenter>();
                var view = new GameplayActorView(
                    "party.missing-weapon",
                    string.Empty,
                    targetable: false,
                    actor);
                var presenter = new GameplayTurnReplayActorPresenter(view);
                presenter.Begin();

                InvalidOperationException exception = Assert.Throws<
                    InvalidOperationException>(() => presenter.Present(
                        new GameplayActorSnapshot(
                            "party.missing-weapon",
                            new GameplayActorPose(
                                new GameplayPosition(0f, 0f, 0f),
                                0f,
                                ActorStance.Standing),
                            new TurnBudget(2, 3f),
                            new ActorWoundSnapshot(
                                "party.missing-weapon",
                                0,
                                0f),
                            "weapon.missing",
                            EquipmentEffectSet.None),
                        action: null));

                Assert.That(exception.Message, Does.Contain("transition initial"));
                Assert.That(exception.Message, Does.Contain("party.missing-weapon"));
                Assert.That(exception.Message, Does.Contain("weapon mount"));
                Assert.That(exception.Message, Does.Contain("weapon.missing"));
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void BackwardScrubbingRebuildsTimedEventCursorWithoutDuplicates()
        {
            var cursor = new ReplayTimedPresentationEventCursor();
            const string shot =
                "replay-combat:12:0:WeaponDischarge:Actor:party.scout:Actor:target:";

            Assert.That(cursor.TryCross(shot, 0.65f, 0f, 1f), Is.True);
            Assert.That(cursor.TryCross(shot, 0.65f, 0f, 1f), Is.False);

            cursor.Clear();
            cursor.RebuildMark(shot, 0.65f, 0.8f);
            Assert.That(cursor.TryCross(shot, 0.65f, 0.8f, 1f), Is.False);

            cursor.Clear();
            cursor.RebuildMark(shot, 0.65f, 0.2f);
            Assert.That(cursor.TryCross(shot, 0.65f, 0.2f, 1f), Is.True);
            Assert.That(cursor.TryCross(shot, 0.65f, 0.2f, 1f), Is.False);
        }

        [Test]
        public void FatalNonSelectedActorReactsAtEventThenUsesAuthoredIncapacitation()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = Object.Instantiate(prefab);
            try
            {
                var view = new GameplayActorView(
                    "party.support",
                    string.Empty,
                    targetable: true,
                    actor);
                ActorAnimationCoordinator animation = actor.GetComponent<
                    ActorAnimationCoordinator>();
                animation.TargetAnimator.cullingMode =
                    AnimatorCullingMode.AlwaysAnimate;
                using var presenter = new GameplayTurnReplayActorPresenter(view);
                presenter.Begin();

                presenter.Present(
                    CreateActorSnapshot(
                        "party.support",
                        woundCount: 2,
                        maximumWounds: 3),
                    new TurnReplayActorActionState(
                        "party.support",
                        TurnReplayActorActionKind.Reaction,
                        journalSequence: 42,
                        normalizedProgress: 0.649f,
                        contactReaction: false,
                        resultingWoundCount: 3,
                        hitRegion: TargetRegionId.Torso));
                Assert.That(
                    animation.ReplayAction,
                    Is.Null,
                    "A ranged reaction cannot precede its discharge event.");

                presenter.Present(
                    CreateActorSnapshot(
                        "party.support",
                        woundCount: 3,
                        maximumWounds: 3),
                    new TurnReplayActorActionState(
                        "party.support",
                        TurnReplayActorActionKind.Reaction,
                        journalSequence: 42,
                        normalizedProgress: 0.65f,
                        contactReaction: false,
                        resultingWoundCount: 3,
                        hitRegion: TargetRegionId.Torso));
                Assert.That(
                    animation.ReplayAction,
                    Is.EqualTo(ActorAnimationAction.IncapacitateShoulder));
                Assert.That(animation.ReplayActionProgress, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void SelfHitKeepsPrimaryAttackAndIndependentReactionOverlay()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = Object.Instantiate(prefab);
            try
            {
                var view = new GameplayActorView(
                    "self-hit-actor",
                    string.Empty,
                    targetable: true,
                    actor);
                ActorAnimationCoordinator animation = actor.GetComponent<
                    ActorAnimationCoordinator>();
                animation.TargetAnimator.cullingMode =
                    AnimatorCullingMode.AlwaysAnimate;
                using var presenter = new GameplayTurnReplayActorPresenter(view);
                presenter.Begin();
                var primary = new TurnReplayActorActionState(
                    "self-hit-actor",
                    TurnReplayActorActionKind.Attack,
                    journalSequence: 52,
                    normalizedProgress: 0.8f,
                    eventNormalizedTime: 0.65f,
                    origin: new GameplayPosition(0f, 1f, 0f),
                    destination: new GameplayPosition(0f, 1f, 0f));
                var reaction = new TurnReplayActorActionState(
                    "self-hit-actor",
                    TurnReplayActorActionKind.Reaction,
                    journalSequence: 52,
                    normalizedProgress: 0.8f,
                    contactReaction: false,
                    resultingWoundCount: 1,
                    hitRegion: TargetRegionId.Torso,
                    resultingLifeState: ActorLifeState.Active);
                var channels = new ReplayActorActionChannels();
                channels.Add(reaction);
                channels.Add(primary);

                presenter.Present(
                    CreateActorSnapshot(
                        "self-hit-actor",
                        woundCount: 1,
                        maximumWounds: 3),
                    channels.Primary,
                    reaction: channels.Reaction);

                Assert.That(
                    animation.ReplayAction,
                    Is.EqualTo(ActorAnimationAction.WeaponFire));
                Assert.That(view.InjuryOverlay.HitReactionActive, Is.True);
                Assert.That(
                    view.ReplayActions.CurrentState,
                    Is.SameAs(primary));
                Assert.That(
                    view.ReplayActions.CurrentReactionState,
                    Is.SameAs(reaction));
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

        [Test]
        public void AnimatorReplaySeekSamplesFireActionAndRecoilTogether()
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

                animation.BeginReplayPresentation();
                animation.PresentReplayAction(
                    ActorStance.Standing,
                    ActorAnimationAction.WeaponFire,
                    0.5f);

                int actionLayer = animator.GetLayerIndex(
                    ActorAnimationParameters.ActionLayerName);
                int recoilLayer = animator.GetLayerIndex(
                    ActorAnimationParameters.RecoilLayerName);
                AnimatorStateInfo action =
                    animator.GetCurrentAnimatorStateInfo(actionLayer);
                AnimatorStateInfo recoil =
                    animator.GetCurrentAnimatorStateInfo(recoilLayer);
                Assert.That(
                    action.IsName(ActorAnimationParameters.RifleFireStateName),
                    Is.True);
                Assert.That(
                    recoil.IsName(
                        ActorAnimationParameters.RifleRecoilStateName),
                    Is.True);
                Assert.That(action.normalizedTime, Is.EqualTo(0.5f)
                    .Within(0.001f));
                Assert.That(recoil.normalizedTime, Is.EqualTo(0.5f)
                    .Within(0.001f));
                Assert.That(
                    animator.GetLayerWeight(actionLayer),
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(
                    animator.GetLayerWeight(recoilLayer),
                    Is.EqualTo(0.8f).Within(0.001f));

                animation.EndReplayPresentation();
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        private static GameplayActorSnapshot CreateActorSnapshot(
            string actorId,
            int woundCount,
            int maximumWounds) => new GameplayActorSnapshot(
                actorId,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f,
                    ActorStance.Standing),
                new TurnBudget(2, 3f),
                new ActorWoundSnapshot(actorId, woundCount, 0f),
                equippedItemId: null,
                equipmentEffects: EquipmentEffectSet.None,
                maximumWounds: maximumWounds);

    }
}
