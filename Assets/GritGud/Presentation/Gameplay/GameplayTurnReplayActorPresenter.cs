using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
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
            TurnReplayActorActionState action)
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
            animation.PresentReplayAction(
                pose.Stance,
                MapAnimationAction(action?.Kind),
                action?.NormalizedProgress ?? 0f);
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

        private static ActorAnimationAction? MapAnimationAction(
            TurnReplayActorActionKind? kind)
        {
            switch (kind)
            {
                case TurnReplayActorActionKind.Attack:
                    return ActorAnimationAction.WeaponFire;
                case TurnReplayActorActionKind.Equipment:
                case TurnReplayActorActionKind.Displacement:
                    return ActorAnimationAction.Interact;
                case TurnReplayActorActionKind.Throw:
                    return ActorAnimationAction.Throw;
                case TurnReplayActorActionKind.Reaction:
                case TurnReplayActorActionKind.Pinned:
                    return ActorAnimationAction.HitReaction;
                case TurnReplayActorActionKind.GetUp:
                    return ActorAnimationAction.Interact;
                case TurnReplayActorActionKind.Jump:
                case TurnReplayActorActionKind.Vault:
                case TurnReplayActorActionKind.Mantle:
                    return ActorAnimationAction.Jump;
                default:
                    return null;
            }
        }
    }
}
