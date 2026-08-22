using System;
using System.Collections.Generic;
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
        private readonly ThirdPersonMotor motor;
        private readonly ExplorationMovementInput movementInput;
        private readonly HashSet<string> failedOptionalPresentation =
            new HashSet<string>(StringComparer.Ordinal);
        private bool locomotionEnabled;
        private bool aimEnabled;
        private bool aimRigEnabled;
        private bool motorEnabled;
        private bool movementInputEnabled;
        private bool presenting;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private ActorStance originalStance;
        private ActorPinState originalPinState;

        public GameplayTurnReplayActorPresenter(GameplayActorView actorView)
        {
            view = actorView ?? throw new ArgumentNullException(nameof(actorView));
            animation = view.Root.GetComponent<ActorAnimationCoordinator>();
            weapon = view.Root.GetComponent<GameplayWeaponPresenter>();
            locomotion = view.Root.GetComponent<
                ActorLocomotionAnimationPresenter>();
            aim = view.Root.GetComponent<WeaponAimPresenter>();
            aimRig = animation?.TargetAnimator != null
                ? animation.TargetAnimator.GetComponent<WeaponAimRig>()
                : null;
            ragdoll = view.Root.GetComponent<ActorRagdollPresenter>();
            motor = view.Motor;
            movementInput = view.MovementInput;
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
            motorEnabled = motor != null && motor.enabled;
            movementInputEnabled = movementInput != null && movementInput.enabled;
            originalPosition = view.Transform.position;
            originalRotation = view.Transform.rotation;
            originalStance = view.Stance.Stance;
            originalPinState = view.ReplayActions.CurrentPinState;
            failedOptionalPresentation.Clear();
            presenting = true;
            TryOptional(
                "ragdoll",
                () => ragdoll?.BeginReplayPresentation());
            BeginRequired("animation", () => animation?.BeginReplayPresentation());
            BeginRequired("weapon", () => weapon?.BeginReplayPresentation());
            BeginRequired("weapon aim", () => aim?.BeginReplayPresentation());
            BeginRequired(
                "weapon aim rig",
                () => aimRig?.BeginReplayPresentation());
            TryOptional(
                "wounds",
                view.Wounds.BeginReplayPresentation);
            if (locomotion != null)
                locomotion.enabled = false;
            if (aim != null)
                aim.enabled = false;
            motor?.StopPlanarMovement();
            // Replay is the transform authority. The normal motor and
            // movement-input pair otherwise continue their frame updates
            // alongside the replay projection, which can overwrite a
            // sampled pose or restart a locomotion blend.
            if (movementInput != null)
                movementInput.enabled = false;
            if (motor != null)
                motor.enabled = false;
        }

        internal void Present(
            GameplayActorSnapshot snapshot,
            TurnReplayActorActionState action,
            GameplaySemanticReplayPlaybackPosition? playback = null,
            Vector3? replayVelocity = null,
            bool replayGrounded = true)
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
            long transitionSequence = action?.TransitionSequence
                ?? playback?.Frame.Transition.Identity.Sequence
                ?? 0L;
            string replayRecord = playback?.Frame.SemanticRecord
                    ?.GetType().Name
                ?? action?.Kind.ToString()
                ?? "state sample";
            float presentedFacing = action?.TargetFacingPhase
                ?.SampleFacingDegrees(action.NormalizedProgress)
                ?? pose.FacingDegrees;
            view.Transform.SetPositionAndRotation(
                new Vector3(
                    pose.Position.X,
                    pose.Position.Y,
                    pose.Position.Z),
                Quaternion.Euler(0f, presentedFacing, 0f));
            if (view.Stance.Stance != pose.Stance)
                view.Stance.ApplyResolved(pose.Stance);
            TryOptional(
                "wounds",
                () => view.Wounds.PresentReplay(snapshot.Wounds));
            if (!string.IsNullOrWhiteSpace(snapshot.EquippedItemId)
                && weapon == null)
                throw RequiredFailure(
                    "weapon mount",
                    transitionSequence,
                    replayRecord,
                    $"armed actor equips '{snapshot.EquippedItemId}' but has no GameplayWeaponPresenter");
            if (!string.IsNullOrWhiteSpace(snapshot.EquippedItemId)
                && aimRig == null)
                throw RequiredFailure(
                    "weapon aim rig",
                    transitionSequence,
                    replayRecord,
                    "armed actor has no post-animation WeaponAimRig");
            if (weapon != null)
                PresentRequired(
                    "weapon",
                    transitionSequence,
                    replayRecord,
                    () =>
                    {
                        weapon.PresentReplayEquipment(snapshot.EquippedItemId);
                        weapon.PresentReplayAction(
                            action,
                            playback?.PlaybackFrame.DurationSeconds ?? 0f);
                    });
            TryOptional(
                "actor-state hooks",
                () =>
                {
                    view.ReplayActions.Present(action);
                    view.ReplayActions.PresentPinState(snapshot.PinState);
                });
            bool requiresAnimation = action != null
                || replayVelocity.GetValueOrDefault().sqrMagnitude > 0.000001f
                || snapshot.IsIncapacitated;
            if (requiresAnimation && animation == null)
                throw RequiredFailure(
                    "combat animation",
                    transitionSequence,
                    replayRecord,
                    "actor has no ActorAnimationCoordinator");
            if (animation != null)
                PresentRequired(
                    "animation",
                    transitionSequence,
                    replayRecord,
                    () =>
                    {
                    ResolveAnimationProjection(
                        snapshot,
                        action,
                        out ActorAnimationAction? animationAction,
                        out float animationProgress);
                    animation.PresentReplayLocomotion(
                        pose.Stance,
                        replayVelocity ?? Vector3.zero,
                        replayGrounded);
                    animation.PresentReplayAction(
                        pose.Stance,
                        animationAction,
                        animationProgress);
                    });
            if (aimRig != null)
                PresentRequired(
                    "weapon aim rig",
                    transitionSequence,
                    replayRecord,
                    () =>
                    {
                    aimRig.SetReplaySupportWeightImmediate();
                    aimRig.SynchronizeAfterAnimation(0f);
                    });
            // Semantic replay uses the seekable authored incapacitation clip
            // above for every source, including portable artifacts. Runtime
            // ragdoll traces are deliberately not mixed into this path: they
            // are session-local data and cannot produce the same terminal pose
            // in a fresh artifact viewer.
        }

        internal void ClearTransients()
        {
            TryOptional(
                "weapon",
                () => weapon?.ClearReplayTransients());
        }

        internal void PresentEvent(
            ReplayCombatPresentationEvent presentationEvent)
        {
            if (!presenting)
                throw new InvalidOperationException(
                    "Begin actor replay presentation before projecting events.");
            if (weapon == null)
                throw RequiredFailure(
                    "weapon discharge",
                    presentationEvent.TransitionSequence,
                    presentationEvent.Kind.ToString(),
                    "actor has no GameplayWeaponPresenter");
            PresentRequired(
                "weapon discharge",
                presentationEvent.TransitionSequence,
                presentationEvent.Kind.ToString(),
                () => weapon.PresentReplayEvent(presentationEvent));
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
            TryRestore(() => aimRig?.EndReplayPresentation(), ref failure);
            TryRestore(() => aim?.EndReplayPresentation(), ref failure);
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
            TryRestore(
                () => animation?.EndReplayPresentation(),
                ref failure);
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
                    if (movementInput != null)
                        movementInput.enabled = movementInputEnabled;
                    if (motor != null)
                        motor.enabled = motorEnabled;
                },
                ref failure);
            presenting = false;
            failedOptionalPresentation.Clear();
            if (failure != null)
            {
                Debug.LogWarning(
                    $"Replay actor '{view.ActorId}' could not restore every "
                    + $"optional presentation detail: {failure.Message}",
                    view.Root);
            }
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

        private void TryOptional(string feature, Action present)
        {
            if (present == null
                || failedOptionalPresentation.Contains(feature))
            {
                return;
            }
            try
            {
                present();
            }
            catch (Exception exception)
            {
                failedOptionalPresentation.Add(feature);
                Debug.LogWarning(
                    $"Replay actor '{view.ActorId}' disabled optional "
                    + $"{feature} presentation: {exception.Message}",
                    view.Root);
            }
        }

        private void BeginRequired(string feature, Action begin)
        {
            try
            {
                begin?.Invoke();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Replay actor '{view.ActorId}' could not begin required "
                    + $"{feature} presentation: {exception.Message}",
                    exception);
            }
        }

        private void PresentRequired(
            string feature,
            long transitionSequence,
            string replayRecord,
            Action present)
        {
            try
            {
                present();
            }
            catch (Exception exception)
            {
                throw RequiredFailure(
                    feature,
                    transitionSequence,
                    replayRecord,
                    exception.Message,
                    exception);
            }
        }

        private InvalidOperationException RequiredFailure(
            string feature,
            long transitionSequence,
            string replayRecord,
            string detail,
            Exception inner = null)
        {
            string transition = transitionSequence > 0
                ? transitionSequence.ToString()
                : "initial";
            return new InvalidOperationException(
                $"Replay transition {transition} actor '{view.ActorId}' "
                + $"cannot project required {feature} for {replayRecord}: "
                + detail,
                inner);
        }

        private void ResolveAnimationProjection(
            GameplayActorSnapshot snapshot,
            TurnReplayActorActionState state,
            out ActorAnimationAction? action,
            out float progress)
        {
            progress = state?.NormalizedProgress ??
                (snapshot.IsIncapacitated ? 1f : 0f);
            if (state?.TargetFacingPhase != null)
            {
                progress = state.TargetFacingPhase.SampleActionProgress(
                    progress);
            }
            if (state == null)
            {
                action = snapshot.IsIncapacitated
                    ? ActorAnimationAction.Incapacitate
                    : view.TargetProfile.ProfileKind
                        == ActorTargetProfileKind.PinnedDown
                        ? ActorAnimationAction.Incapacitate
                        : (ActorAnimationAction?)null;
                return;
            }

            switch (state.Kind)
            {
                case TurnReplayActorActionKind.Attack:
                    action = weapon?.ResolveReplayAttackAnimation(
                            state.IsContactAttack)
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
                    float eventTime = state.EventNormalizedTime;
                    if (progress < eventTime)
                    {
                        action = null;
                        progress = 0f;
                        return;
                    }
                    progress = Mathf.InverseLerp(eventTime, 1f, progress);
                    action = (state.ResultingLifeState
                            ?? snapshot.LifeState)
                        != ActorLifeState.Active
                        ? ActorAnimationCoordinator
                            .SelectIncapacitationAction(state.HitRegion)
                        : ActorAnimationAction.HitReaction;
                    return;
                case TurnReplayActorActionKind.Pinned:
                    action = ActorAnimationAction.Incapacitate;
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
