using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class TurnMovementController : MonoBehaviour
    {
        private const float PlanningSpeed = 4f;
        private const float RouteSampleDistance = 0.35f;
        private const float MinimumRouteDistance = 0.0001f;

        private ExplorationMovementInput movementInput;
        private IGameplayInputSource inputSource;
        private ThirdPersonMotor motor;
        private Transform actorTransform;
        private string actorId;
        private MovementRoutePlanner planner;
        private MovementRouteGhostPresenter ghostPresenter;
        private MovementRoutePlaybackPresenter playbackPresenter;
        private float pendingPlanDistance;
        private long plannerTurnSequence = -1L;

        public GameplaySession Session { get; private set; }

        public float PlannedCost => planner?.TotalCost ?? 0f;

        public int PlanPointCount => planner?.Points.Count ?? 0;

        public bool IsPlaying => playbackPresenter?.IsPlaying == true;

        public float CommittedCost => playbackPresenter?.CommittedCost ?? 0f;

        internal float PlanningMaximumCost => planner?.MaximumCost ?? 0f;

        public RoutePlanFailure LastPlanFailure { get; private set; }

        public string StatusMessage { get; private set; } = string.Empty;

        public void Bind(
            GameplaySession session,
            ExplorationMovementInput cameraRelativeInput,
            IGameplayInputSource gameplayInput,
            ThirdPersonMotor actorMotor,
            string authoritativeActorId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (gameplayInput == null)
            {
                throw new ArgumentNullException(nameof(gameplayInput));
            }

            Unbind();
            Session = session;
            inputSource = gameplayInput;
            SetActor(
                cameraRelativeInput,
                actorMotor,
                authoritativeActorId);
        }

        public void SetActor(
            ExplorationMovementInput cameraRelativeInput,
            ThirdPersonMotor actorMotor,
            string authoritativeActorId)
        {
            if (Session == null || inputSource == null)
            {
                throw new InvalidOperationException(
                    "Bind turn movement before changing actors.");
            }
            if (cameraRelativeInput == null)
                throw new ArgumentNullException(nameof(cameraRelativeInput));
            if (actorMotor == null)
                throw new ArgumentNullException(nameof(actorMotor));
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Authoritative actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            Session.GetActor(authoritativeActorId);
            CharacterController controller =
                actorMotor.GetComponent<CharacterController>();
            if (controller == null)
            {
                throw new InvalidOperationException(
                    "Turn movement requires the actor's character controller.");
            }

            planner?.Cancel();
            planner = null;
            playbackPresenter?.Cancel();
            ghostPresenter?.Dispose();
            movementInput = cameraRelativeInput;
            motor = actorMotor;
            actorTransform = actorMotor.transform;
            actorId = authoritativeActorId;
            ghostPresenter = new MovementRouteGhostPresenter(actorTransform);
            playbackPresenter = new MovementRoutePlaybackPresenter(actorMotor);
            SegmentValidator = new UnityMovementRouteSegmentValidator(controller);
            pendingPlanDistance = 0f;
            plannerTurnSequence = -1L;
            LastPlanFailure = RoutePlanFailure.None;
            StatusMessage = string.Empty;
            enabled = true;
        }

        internal IMovementRouteSegmentValidator SegmentValidator { get; private set; }

        public void Unbind()
        {
            planner?.Cancel();
            planner = null;
            playbackPresenter?.Cancel();
            playbackPresenter = null;
            ghostPresenter?.Dispose();
            ghostPresenter = null;
            SegmentValidator = null;
            movementInput = null;
            inputSource = null;
            motor = null;
            actorTransform = null;
            actorId = null;
            Session = null;
            pendingPlanDistance = 0f;
            plannerTurnSequence = -1L;
            LastPlanFailure = RoutePlanFailure.None;
            StatusMessage = string.Empty;
            enabled = false;
        }

        private void OnDisable()
        {
            ghostPresenter?.Hide();
        }

        private void Update()
        {
            if (Session == null)
            {
                return;
            }

            if (Session.Operation == GameplaySessionOperation.ResolvingMovement)
            {
                TickPlayback();
                return;
            }

            if (!SynchronizePlanningState())
            {
                return;
            }

            HandlePlanningInput();
            ghostPresenter?.Present(planner, Time.deltaTime);
        }

        internal bool SynchronizePlanningState()
        {
            if (Session == null)
            {
                return false;
            }

            if (Session.Mode != GameplaySessionMode.TurnBased ||
                Session.Operation != GameplaySessionOperation.None ||
                !string.Equals(
                    Session.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                ClearProvisionalPlan();
                return false;
            }

            EnsurePlanner();
            return true;
        }

        private void EnsurePlanner()
        {
            long currentTurnSequence = Session.LastEndedTurn?.Sequence ?? 0L;
            if (planner != null && plannerTurnSequence == currentTurnSequence)
            {
                return;
            }

            planner?.Cancel();
            planner = new MovementRoutePlanner(
                Session.GetActor(actorId),
                SegmentValidator);
            plannerTurnSequence = currentTurnSequence;
        }

        private void HandlePlanningInput()
        {
            GameplayInputFrame input = inputSource?.CurrentFrame ?? default;

            if (input.WasPressed(GameplayControl.CancelRoute))
            {
                planner.Cancel();
                pendingPlanDistance = 0f;
                LastPlanFailure = RoutePlanFailure.None;
                StatusMessage = "Route canceled.";
                return;
            }

            if (input.WasPressed(GameplayControl.UndoRoute))
            {
                bool revised = planner.UndoLastSegment();
                pendingPlanDistance = 0f;
                LastPlanFailure = RoutePlanFailure.None;
                StatusMessage = revised
                    ? "Removed the last route segment."
                    : "The route is already empty.";
                return;
            }

            if (input.WasPressed(GameplayControl.ConfirmRoute) && planner.CanConfirm)
            {
                MovementRouteRecord route = planner.Confirm();
                Session.CommitMovementRoute(route);
                playbackPresenter.Begin(route);
                ghostPresenter.Hide();
                planner = null;
                pendingPlanDistance = 0f;
                LastPlanFailure = RoutePlanFailure.None;
                StatusMessage = "Resolving movement...";
                return;
            }

            ActorMovementCommand command =
                movementInput.ReadCameraRelativeCommand();
            Vector3 direction = command.WorldDirection;
            if (direction.sqrMagnitude <= MinimumRouteDistance)
            {
                pendingPlanDistance = 0f;
                return;
            }

            pendingPlanDistance += PlanningSpeed * Time.deltaTime;
            while (pendingPlanDistance >= RouteSampleDistance)
            {
                float remainingBudget = planner.MaximumCost - planner.TotalCost;
                float stepDistance = Mathf.Min(RouteSampleDistance, remainingBudget);
                if (stepDistance <= MinimumRouteDistance)
                {
                    LastPlanFailure = RoutePlanFailure.ExceedsMovementBudget;
                    StatusMessage = "No movement remains for this turn.";
                    pendingPlanDistance = 0f;
                    return;
                }

                GameplayPosition from = planner.Destination;
                var requested = new GameplayPosition(
                    from.X + (direction.x * stepDistance),
                    from.Y,
                    from.Z + (direction.z * stepDistance));
                if (!planner.TryAppend(requested, out RoutePlanFailure failure))
                {
                    LastPlanFailure = failure;
                    StatusMessage = planner.LastFailureReason;
                    pendingPlanDistance = 0f;
                    return;
                }

                pendingPlanDistance -= RouteSampleDistance;
                LastPlanFailure = RoutePlanFailure.None;
                StatusMessage = string.Empty;
            }
        }

        private void TickPlayback()
        {
            if (playbackPresenter == null || !playbackPresenter.IsPlaying)
            {
                return;
            }

            if (!playbackPresenter.Tick(Time.deltaTime))
            {
                return;
            }

            Session.CompleteMovementResolution();
            GameplayActorSnapshot resolvedActor = Session.GetActor(actorId);
            actorTransform.SetPositionAndRotation(
                MovementRouteSampling.ToVector3(resolvedActor.Pose.Position),
                Quaternion.Euler(0f, resolvedActor.Pose.FacingDegrees, 0f));
            StatusMessage = "Movement resolved. Plan another route or press T.";
        }

        private void ClearProvisionalPlan()
        {
            planner?.Cancel();
            planner = null;
            plannerTurnSequence = -1L;
            pendingPlanDistance = 0f;
            LastPlanFailure = RoutePlanFailure.None;
            StatusMessage = string.Empty;
            ghostPresenter?.Hide();
        }
    }
}
