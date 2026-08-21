using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
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
        private IReadOnlyList<LevelTraversalLinkData> traversalLinks =
            Array.Empty<LevelTraversalLinkData>();

        public GameplaySession Session { get; private set; }

        public float PlannedCost => planner?.TotalCost ?? 0f;

        public int PlannedActionPointCost =>
            planner?.TotalActionPointCost ?? 0;

        public int PlanPointCount => planner?.Points.Count ?? 0;

        public bool IsPlaying => playbackPresenter?.IsPlaying == true;

        public float CommittedCost => playbackPresenter?.CommittedCost ?? 0f;

        internal float PlanningMaximumCost => planner?.MaximumCost ?? 0f;

        internal int PlanningMaximumActionPoints =>
            planner?.MaximumActionPoints ?? 0;

        public RoutePlanFailure LastPlanFailure { get; private set; }

        public string StatusMessage { get; private set; } = string.Empty;

        public void Bind(
            GameplaySession session,
            ExplorationMovementInput cameraRelativeInput,
            IGameplayInputSource gameplayInput,
            ThirdPersonMotor actorMotor,
            string authoritativeActorId,
            IEnumerable<LevelTraversalLinkData> authoredTraversalLinks = null)
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
            var links = new List<LevelTraversalLinkData>();
            foreach (LevelTraversalLinkData link in authoredTraversalLinks
                ?? Array.Empty<LevelTraversalLinkData>())
            {
                if (link != null)
                    links.Add(link.DeepCopy());
            }
            traversalLinks = links.AsReadOnly();
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
            SegmentValidator = new UnityMovementRouteSegmentValidator(
                controller,
                traversalLinks);
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
            traversalLinks = Array.Empty<LevelTraversalLinkData>();
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
            AdvanceFrame(Time.deltaTime);
        }

        /// <summary>
        /// Advances the route presentation before accepting another planning
        /// input. Canonical movement reduces its final gameplay state
        /// immediately, whereas legacy movement remains in
        /// ResolvingMovement until its visual route completes. The visual
        /// lifetime is therefore owned here rather than inferred solely from
        /// the session operation.
        /// </summary>
        internal void AdvanceFrame(float deltaTime)
        {
            if (Session == null)
            {
                return;
            }

            if (playbackPresenter?.IsPlaying == true)
            {
                TickPlayback(deltaTime);
                return;
            }

            if (Session.Operation == GameplaySessionOperation.ResolvingMovement)
            {
                MovementRouteRecord pendingRoute = Session.PendingMovementRoute;
                if (pendingRoute == null || playbackPresenter == null)
                {
                    StatusMessage = "Movement is waiting for its route playback.";
                    return;
                }

                playbackPresenter.Begin(pendingRoute);
                ghostPresenter?.Hide();
                planner = null;
                pendingPlanDistance = 0f;
                LastPlanFailure = RoutePlanFailure.None;
                StatusMessage = "Resolving movement...";
                TickPlayback(deltaTime);
                return;
            }

            if (!SynchronizePlanningState())
            {
                return;
            }

            HandlePlanningInput(deltaTime);
            ghostPresenter?.Present(planner, deltaTime);
        }

        internal bool SynchronizePlanningState()
        {
            if (Session == null)
            {
                return false;
            }

            if (Session.Mode != GameplaySessionMode.TurnBased ||
                Session.Operation != GameplaySessionOperation.None ||
                Session.GetActor(actorId).IsPinned ||
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
            GameplayActorSnapshot actor = Session.GetActor(actorId);
            if (planner != null
                && plannerTurnSequence == currentTurnSequence
                && PlannerMatchesActorState(planner, actor))
            {
                return;
            }

            planner?.Cancel();
            planner = new MovementRoutePlanner(
                actor,
                SegmentValidator);
            plannerTurnSequence = currentTurnSequence;
        }

        private static bool PlannerMatchesActorState(
            MovementRoutePlanner currentPlanner,
            GameplayActorSnapshot actor)
        {
            GameplayActorPose origin = currentPlanner.OriginPose;
            GameplayActorPose current = actor.Pose;
            return string.Equals(
                    currentPlanner.ActorId,
                    actor.ActorId,
                    StringComparison.Ordinal)
                && origin.Position.X == current.Position.X
                && origin.Position.Y == current.Position.Y
                && origin.Position.Z == current.Position.Z
                && origin.FacingDegrees == current.FacingDegrees
                && origin.Stance == current.Stance
                && currentPlanner.MaximumActionPoints
                    == actor.TurnBudget.ActionPoints
                && currentPlanner.MaximumCost
                    == actor.TurnBudget.MovementOpportunity;
        }

        private void HandlePlanningInput(float deltaTime)
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
                if (playbackPresenter.IsPlaying)
                {
                    StatusMessage = "Movement is already resolving.";
                    return;
                }
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

            pendingPlanDistance += PlanningSpeed * Mathf.Max(0f, deltaTime);
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
                MovementRouteSegmentRecord appended =
                    planner.Segments[planner.Segments.Count - 1];
                StatusMessage = appended.IsTraversal
                    ? $"{appended.Kind.ToString().ToUpperInvariant()}"
                        + $" - {appended.MovementCost:0.##} MOVE"
                        + $" - {appended.ActionPointCost} AP"
                    : string.Empty;
            }
        }

        private void TickPlayback(float deltaTime)
        {
            if (playbackPresenter == null || !playbackPresenter.IsPlaying)
            {
                return;
            }

            if (!playbackPresenter.Tick(deltaTime))
            {
                return;
            }

            if (Session.Operation == GameplaySessionOperation.ResolvingMovement)
            {
                Session.CompleteMovementResolution();
            }
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
