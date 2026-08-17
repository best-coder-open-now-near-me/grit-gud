using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayTurnReplayActorPresenter : IDisposable
    {
        private readonly GameplayActorView view;
        private readonly ActorAnimationCoordinator animation;
        private readonly GameplayWeaponPresenter weapon;
        private readonly ActorLocomotionAnimationPresenter locomotion;
        private readonly WeaponAimPresenter aim;
        private readonly WeaponAimRig aimRig;
        private readonly ActorRagdollPresenter ragdoll;
        private bool locomotionEnabled;
        private bool aimEnabled;
        private bool aimRigEnabled;
        private bool presenting;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private ActorStance originalStance;
        private ActorPinState originalPinState;

        public GameplayTurnReplayActorPresenter(GameplayActorView actorView)
        {
            view = actorView ?? throw new ArgumentNullException(nameof(actorView));
            animation = view.Root.GetComponent<ActorAnimationCoordinator>()
                ?? throw new InvalidOperationException(
                    $"Actor '{view.ActorId}' requires an animation coordinator for replay.");
            weapon = view.Root.GetComponent<GameplayWeaponPresenter>();
            locomotion = view.Root.GetComponent<
                ActorLocomotionAnimationPresenter>();
            aim = view.Root.GetComponent<WeaponAimPresenter>();
            aimRig = animation.TargetAnimator != null
                ? animation.TargetAnimator.GetComponent<WeaponAimRig>()
                : null;
            ragdoll = view.Root.GetComponent<ActorRagdollPresenter>();
        }

        internal bool IsPresenting => presenting;

        internal void Begin()
        {
            if (presenting)
            {
                throw new InvalidOperationException(
                    "Actor replay presentation is already active.");
            }
            locomotionEnabled = locomotion != null && locomotion.enabled;
            aimEnabled = aim != null && aim.enabled;
            aimRigEnabled = aimRig != null && aimRig.enabled;
            originalPosition = view.Transform.position;
            originalRotation = view.Transform.rotation;
            originalStance = view.Stance.Stance;
            originalPinState = view.ReplayActions.CurrentPinState;
            presenting = true;
            try
            {
                ragdoll?.BeginReplayPresentation();
                animation.BeginReplayPresentation();
                weapon?.BeginReplayPresentation();
                view.Wounds.BeginReplayPresentation();
                if (locomotion != null)
                    locomotion.enabled = false;
                if (aim != null)
                    aim.enabled = false;
                if (aimRig != null)
                    aimRig.enabled = false;
                view.Motor?.StopPlanarMovement();
            }
            catch
            {
                End();
                throw;
            }
        }

        internal void Present(
            GameplayActorSnapshot snapshot,
            TurnReplayActorActionState action,
            TurnReplayEventTimeline timeline = null,
            float timeSeconds = 0f)
        {
            if (!presenting)
            {
                throw new InvalidOperationException(
                    "Begin actor replay presentation before projecting state.");
            }
            if (!string.Equals(
                    snapshot.ActorId,
                    view.ActorId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Replay actor state must match its presentation owner.",
                    nameof(snapshot));
            }

            GameplayActorPose pose = snapshot.Pose;
            view.Transform.SetPositionAndRotation(
                new Vector3(
                    pose.Position.X,
                    pose.Position.Y,
                    pose.Position.Z),
                Quaternion.Euler(0f, pose.FacingDegrees, 0f));
            if (view.Stance.Stance != pose.Stance)
                view.Stance.ApplyResolved(pose.Stance);
            view.Wounds.PresentReplay(snapshot.Wounds);
            weapon?.PresentReplayEquipment(snapshot.EquippedItemId);
            weapon?.PresentReplayAction(action);
            view.ReplayActions.Present(action);
            view.ReplayActions.PresentPinState(snapshot.PinState);
            ResolveAnimationProjection(
                snapshot,
                action,
                out ActorAnimationAction? animationAction,
                out float animationProgress);
            animation.PresentReplayAction(
                pose.Stance,
                animationAction,
                animationProgress);
            if (timeline != null)
                ragdoll?.PresentReplay(timeline, timeSeconds);
        }

        internal void PresentTransient(
            GameplayTurnReplayTransientCue cue)
        {
            if (!presenting)
                return;
            view.ReplayTransients.Present(cue);
            if (cue.Crossing.Boundary == TurnReplayEventBoundary.Start
                && cue.Crossing.TimedEvent.Entry
                    is ActionResolvedJournalEntry resolved)
            {
                weapon?.PresentReplayTransient(resolved);
            }
        }

        internal void ClearTransients()
        {
            weapon?.ClearReplayTransients();
            view.ReplayTransients.Clear();
        }

        public void Dispose() => End();

        private void End()
        {
            if (!presenting)
                return;
            Exception failure = null;
            TryRestore(ClearTransients, ref failure);
            TryRestore(view.ReplayActions.Clear, ref failure);
            TryRestore(
                () => view.ReplayActions.PresentPinState(originalPinState),
                ref failure);
            TryRestore(view.Wounds.EndReplayPresentation, ref failure);
            TryRestore(() => weapon?.EndReplayPresentation(), ref failure);
            TryRestore(
                () => view.Transform.SetPositionAndRotation(
                    originalPosition,
                    originalRotation),
                ref failure);
            TryRestore(
                () =>
                {
                    if (view.Stance.Stance != originalStance)
                        view.Stance.ApplyResolved(originalStance);
                },
                ref failure);
            TryRestore(animation.EndReplayPresentation, ref failure);
            TryRestore(
                () => ragdoll?.EndReplayPresentation(),
                ref failure);
            TryRestore(
                () =>
                {
                    if (locomotion != null)
                        locomotion.enabled = locomotionEnabled;
                    if (aim != null)
                        aim.enabled = aimEnabled;
                    if (aimRig != null)
                        aimRig.enabled = aimRigEnabled;
                },
                ref failure);
            presenting = false;
            if (failure != null)
                throw failure;
        }

        private static void TryRestore(Action restore, ref Exception failure)
        {
            try
            {
                restore();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        private void ResolveAnimationProjection(
            GameplayActorSnapshot snapshot,
            TurnReplayActorActionState state,
            out ActorAnimationAction? action,
            out float progress)
        {
            progress = state?.NormalizedProgress ??
                (snapshot.IsIncapacitated ? 1f : 0f);
            if (state == null)
            {
                action = snapshot.IsIncapacitated
                    ? ActorAnimationAction.Incapacitate
                    : (ActorAnimationAction?)null;
                return;
            }

            switch (state.Kind)
            {
                case TurnReplayActorActionKind.Attack:
                    action = weapon?.ResolveReplayAttackAnimation()
                        ?? ActorAnimationAction.WeaponFire;
                    return;
                case TurnReplayActorActionKind.Equipment:
                case TurnReplayActorActionKind.Displacement:
                    action = ActorAnimationAction.Interact;
                    return;
                case TurnReplayActorActionKind.Throw:
                    action = ActorAnimationAction.Throw;
                    return;
                case TurnReplayActorActionKind.Reaction:
                    if (state.IsContactReaction)
                    {
                        float impact = GameplayCloseQuartersPresentationTiming
                            .ContactImpactNormalizedTime;
                        if (progress < impact)
                        {
                            action = null;
                            progress = 0f;
                            return;
                        }
                        progress = Mathf.InverseLerp(impact, 1f, progress);
                    }
                    action = state.ResultingWoundCount >=
                        snapshot.MaximumWounds
                        ? ActorAnimationCoordinator
                            .SelectIncapacitationAction(state.HitRegion)
                        : ActorAnimationAction.HitReaction;
                    return;
                case TurnReplayActorActionKind.Pinned:
                    action = ActorAnimationAction.HitReaction;
                    return;
                case TurnReplayActorActionKind.GetUp:
                    action = ActorAnimationAction.Interact;
                    return;
                case TurnReplayActorActionKind.Push:
                    action = ActorAnimationAction.Push;
                    return;
                case TurnReplayActorActionKind.Jump:
                case TurnReplayActorActionKind.Vault:
                case TurnReplayActorActionKind.Mantle:
                    action = ActorAnimationAction.Jump;
                    return;
                default:
                    action = null;
                    return;
            }
        }
    }
}
