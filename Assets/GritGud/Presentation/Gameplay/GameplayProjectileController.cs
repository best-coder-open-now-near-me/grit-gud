using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayProjectileController : MonoBehaviour,
        IGameplayTurnModeExitConstraint
    {
        private const string WorldAimReferenceId = "world.aim-point";

        private readonly Dictionary<string, ProjectileFlightPresenter> presenters =
            new Dictionary<string, ProjectileFlightPresenter>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProjectileFlightPresenter>
            replayPresenters = new Dictionary<string, ProjectileFlightPresenter>(
                StringComparer.Ordinal);

        private GameplayProjectileSession projectiles;
        private GameplayImpactCycleSession impactCycle;
        private TargetAcquisitionPresenter acquisition;
        private GameplayWorldRegistry registry;
        private GameplayDialogueLog dialogue;
        private ProjectilePresentationCatalog presentationCatalog;
        private string actorId;
        private Func<bool> beginTurnMode;
        private Func<GameplayActionRecord, bool> beginEncounter;
        private Func<Vector3?> getVisualLaunchOrigin;
        private bool advancedDuringPendingVoluntaryCycle;

        public GameplaySession Session { get; private set; }

        public bool HasProjectileWeapon => Session != null
            && actorId != null
            && Session.GetEquippedAttack(actorId)?.Projectile != null;

        public ProjectileLaunchFailure LastFailure { get; private set; }

        public GameplayActionRecord LastResolvedAction { get; private set; }

        public ProjectileLaunchRecord LastLaunch { get; private set; }

        public ProjectileAdvanceRecord LastAdvance { get; private set; }

        public string StatusMessage { get; private set; } = string.Empty;

        public event Action<GameplayActionRecord> ProjectileLaunched;

        public event Action<ProjectileAdvanceRecord> ProjectileAdvanced;

        internal GameplayProjectileSession ProjectileSession => projectiles;

        internal GameplayImpactCycleSession ImpactCycle => impactCycle;

        internal int PresenterCount => presenters.Count;

        internal bool HasUnresolvedProjectileFlight
        {
            get
            {
                if (projectiles?.HasActiveProjectiles == true)
                {
                    return true;
                }

                foreach (ProjectileFlightPresenter presenter in
                    presenters.Values)
                {
                    if (presenter.IsAdvancePlaying)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        bool IGameplayTurnModeExitConstraint.BlocksTurnModeExit =>
            HasUnresolvedProjectileFlight;

        string IGameplayTurnModeExitConstraint.TurnModeExitBlockedMessage =>
            "Rocket in flight. End turns until impact before leaving turn mode.";

        internal void Bind(
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            IBlastWorldQuery blastWorldQuery,
            GameplayBlastConsequenceResolver consequenceResolver,
            TargetAcquisitionPresenter targetAcquisition,
            GameplayDialogueLog dialogueLog,
            string authoritativeActorId,
            ProjectilePresentationCatalog catalog = null,
            Func<bool> onTurnModeStartRequested = null,
            Func<GameplayActionRecord, bool> onEncounterStartRequested = null,
            GameplayEmergencyCycleSession emergencyCycle = null)
        {
            Unbind();
            Session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            acquisition = targetAcquisition ?? throw new ArgumentNullException(
                nameof(targetAcquisition));
            dialogue = dialogueLog ?? throw new ArgumentNullException(
                nameof(dialogueLog));
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Projectile-controller actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            beginTurnMode = onTurnModeStartRequested ?? Session.EnterTurnMode;
            beginEncounter = onEncounterStartRequested
                ?? Session.BeginEncounterFromAction;
            Session.GetActor(authoritativeActorId);
            presentationCatalog = catalog
                ?? ProjectilePresentationCatalog.LoadDefault();
            var query = new UnityProjectileSegmentQuery(
                registry,
                () => Session?.WorldStateRevision ?? 0L,
                blastWorldQuery ?? throw new ArgumentNullException(
                    nameof(blastWorldQuery)));
            projectiles = new GameplayProjectileSession(
                Session,
                query,
                consequenceResolver ?? throw new ArgumentNullException(
                    nameof(consequenceResolver)));
            impactCycle = emergencyCycle == null
                ? new GameplayImpactCycleSession(Session, projectiles)
                : new GameplayImpactCycleSession(Session, projectiles, emergencyCycle);
            impactCycle.ProjectileAdvanced += HandleImpactCycleAdvance;
            impactCycle.ReactionPredicted += HandleReactionPredicted;
            Session.TurnEnded += HandleTurnEnded;
            Session.VoluntaryTurnCycleCompleted +=
                HandleVoluntaryTurnCycleCompleted;
            LastFailure = ProjectileLaunchFailure.None;
            LastResolvedAction = null;
            LastLaunch = null;
            LastAdvance = null;
            StatusMessage = string.Empty;
            advancedDuringPendingVoluntaryCycle = false;
            enabled = true;
            SetActor(authoritativeActorId);
        }

        public void SetActor(string authoritativeActorId)
        {
            if (Session == null || projectiles == null)
            {
                throw new InvalidOperationException(
                    "Bind gameplay projectiles before changing actors.");
            }
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Projectile-controller actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            Session.GetActor(authoritativeActorId);
            actorId = authoritativeActorId;
            LastFailure = ProjectileLaunchFailure.None;
            LastResolvedAction = null;
            LastLaunch = null;
            StatusMessage = string.Empty;
        }

        public void Unbind()
        {
            if (Session != null)
            {
                Session.TurnEnded -= HandleTurnEnded;
                Session.VoluntaryTurnCycleCompleted -=
                    HandleVoluntaryTurnCycleCompleted;
            }

            foreach (ProjectileFlightPresenter presenter in presenters.Values)
            {
                presenter.Dispose();
            }

            presenters.Clear();
            EndReplayPresentation();
            Session = null;
            projectiles = null;
            if (impactCycle != null)
            {
                impactCycle.ProjectileAdvanced -= HandleImpactCycleAdvance;
                impactCycle.ReactionPredicted -= HandleReactionPredicted;
            }
            impactCycle = null;
            acquisition = null;
            registry = null;
            dialogue = null;
            presentationCatalog = null;
            actorId = null;
            beginTurnMode = null;
            beginEncounter = null;
            getVisualLaunchOrigin = null;
            LastFailure = ProjectileLaunchFailure.None;
            LastResolvedAction = null;
            LastLaunch = null;
            LastAdvance = null;
            StatusMessage = string.Empty;
            advancedDuringPendingVoluntaryCycle = false;
            ProjectileLaunched = null;
            ProjectileAdvanced = null;
            enabled = false;
        }

        internal void BindVisualLaunchOrigin(Func<Vector3?> originProvider)
        {
            getVisualLaunchOrigin = originProvider ?? throw new ArgumentNullException(
                nameof(originProvider));
        }

        internal void BeginReplayPresentation()
        {
            EndReplayPresentation();
            foreach (ProjectileFlightPresenter presenter in presenters.Values)
                presenter.SetPresentationSuppressed(true);
        }

        internal void PresentReplay(
            IReadOnlyList<ProjectileFlightSnapshot> snapshots)
        {
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));
            var retained = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProjectileFlightSnapshot snapshot in snapshots)
            {
                retained.Add(snapshot.ProjectileId);
                if (!replayPresenters.TryGetValue(
                    snapshot.ProjectileId,
                    out ProjectileFlightPresenter presenter))
                {
                    presenter = new ProjectileFlightPresenter(
                        snapshot,
                        presentationCatalog.Get(snapshot.Launch.Definition.Id),
                        transform);
                    replayPresenters.Add(snapshot.ProjectileId, presenter);
                }
                presenter.PresentReplay(snapshot);
            }
            var removed = new List<string>();
            foreach (string projectileId in replayPresenters.Keys)
                if (!retained.Contains(projectileId)) removed.Add(projectileId);
            foreach (string projectileId in removed)
            {
                replayPresenters[projectileId].Dispose();
                replayPresenters.Remove(projectileId);
            }
        }

        internal void EndReplayPresentation()
        {
            foreach (ProjectileFlightPresenter presenter in replayPresenters.Values)
                presenter.Dispose();
            replayPresenters.Clear();
            foreach (ProjectileFlightPresenter presenter in presenters.Values)
                presenter.SetPresentationSuppressed(false);
        }

        public bool TryLaunch()
        {
            if (projectiles == null || actorId == null || !HasProjectileWeapon)
            {
                return Fail(ProjectileLaunchFailure.ProjectileUnavailable);
            }

            string targetId = acquisition?.CurrentTargetActorId
                ?? WorldAimReferenceId;
            if (!TryGetAimPoint(
                    actorId,
                    targetId,
                    out GameplayPosition aimPoint))
            {
                return Fail(ProjectileLaunchFailure.TargetNotFound);
            }

            if (!TryEnterRequiredLaunchMode(targetId))
            {
                return Fail(ProjectileLaunchFailure.TurnModeRequired);
            }

            return TryLaunchResolved(
                actorId,
                targetId,
                aimPoint,
                getVisualLaunchOrigin?.Invoke());
        }

        internal bool TryLaunchActorAtTarget(
            string attackerId,
            string targetId,
            Vector3? visualLaunchOrigin = null)
        {
            if (projectiles == null
                || string.IsNullOrWhiteSpace(attackerId)
                || Session.GetEquippedAttack(attackerId)?.Projectile == null)
            {
                return Fail(ProjectileLaunchFailure.ProjectileUnavailable);
            }

            if (!TryGetAimPoint(
                    attackerId,
                    targetId,
                    out GameplayPosition aimPoint))
            {
                return Fail(ProjectileLaunchFailure.TargetNotFound);
            }

            if (projectiles.GetLaunchModeRequirement(targetId)
                != ProjectileLaunchModeRequirement.None)
            {
                return Fail(ProjectileLaunchFailure.TurnModeRequired);
            }

            return TryLaunchResolved(
                attackerId,
                targetId,
                aimPoint,
                visualLaunchOrigin);
        }

        private bool TryLaunchResolved(
            string attackerId,
            string targetId,
            GameplayPosition aimPoint,
            Vector3? visualLaunchOrigin)
        {
            if (!projectiles.TryLaunch(
                    attackerId,
                    targetId,
                    aimPoint,
                    out GameplayActionRecord action,
                    out ProjectileLaunchFailure failure))
            {
                return Fail(failure);
            }

            GameplayEncounterActionTransition.BeginAfterCommittedAction(
                Session,
                action,
                beginEncounter,
                "projectile launch");

            PresentResolvedAction(action, visualLaunchOrigin);
            bool reactionOpened = impactCycle.ObserveLaunch(LastLaunch);
            ProjectileFlightSnapshot stagedFlight = projectiles.GetProjectile(
                LastLaunch.ProjectileId);
            if (stagedFlight.Status == ProjectileFlightStatus.InFlight)
            {
                StatusMessage = reactionOpened
                    ? $"{LastLaunch.ProjectileId} staged. "
                        + $"{impactCycle.CurrentWindow.ActionPointAllowance} AP reaction armed."
                    : $"{LastLaunch.ProjectileId} staged through remaining turn time.";
            }
            return true;
        }

        internal void PresentResolvedAction(
            GameplayActionRecord action,
            Vector3? visualLaunchOrigin = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            ProjectileLaunchRecord launch = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
                if (outcome is ProjectileLaunchedActionOutcome launched)
                {
                    launch = launched.Launch;
                    break;
                }
            if (launch == null)
                throw new ArgumentException(
                    "Projectile presentation requires a launch outcome.",
                    nameof(action));

            LastFailure = ProjectileLaunchFailure.None;
            LastResolvedAction = action;
            LastLaunch = launch;
            ProjectileLaunched?.Invoke(action);

            ProjectileFlightSnapshot flight = projectiles.GetProjectile(
                launch.ProjectileId);
            ProjectilePresentationDefinition presentation =
                presentationCatalog.Get(launch.Definition.Id);
            if (!presenters.ContainsKey(launch.ProjectileId))
            {
                var presenter = new ProjectileFlightPresenter(
                    flight,
                    presentation,
                    transform,
                    visualLaunchOrigin);
                presenters.Add(launch.ProjectileId, presenter);
            }
            StatusMessage = $"{launch.ProjectileId} staged for canonical advance.";
            if (GameplayCombatDiagnosticFormatter.TryFormatAction(
                    action,
                    out GameplayDiagnosticProjection diagnostic))
            {
                dialogue.AppendCombatDiagnostic(diagnostic);
            }
        }

        internal void PresentResolvedAdvance(ProjectileAdvanceRecord advance)
        {
            if (advance == null) throw new ArgumentNullException(nameof(advance));
            HandleImpactCycleAdvance(advance);
        }

        private bool TryEnterRequiredLaunchMode(string targetId)
        {
            switch (projectiles.GetLaunchModeRequirement(targetId))
            {
                case ProjectileLaunchModeRequirement.None:
                    return true;
                case ProjectileLaunchModeRequirement.VoluntaryTurnMode:
                    return Session.CanEnterTurnMode
                        && beginTurnMode != null
                        && beginTurnMode();
                case ProjectileLaunchModeRequirement.Encounter:
                    // The committed launch itself opens the encounter.  A
                    // voluntary-turn cooldown is not an input lock and must
                    // not prevent an opening shot.
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void ClearStatus()
        {
            StatusMessage = string.Empty;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            foreach (ProjectileFlightPresenter presenter in presenters.Values)
            {
                presenter.Tick(deltaTime);
            }
        }

        private void HandleTurnEnded(TurnEndRecord turn)
        {
            if (impactCycle != null && impactCycle.HasPendingOrActiveWindow)
            {
                ConsumeProjectedLaunchTurns(turn.EndingActorId);
                return;
            }
            bool voluntaryWorldTurn = Session != null
                && !Session.EncounterActive
                && Session.Operation == GameplaySessionOperation.ResolvingWorldTurn;
            AdvanceFlights(voluntaryWorldTurn, turn.EndingActorId);
            advancedDuringPendingVoluntaryCycle = voluntaryWorldTurn;
        }

        private void HandleImpactCycleAdvance(ProjectileAdvanceRecord advance)
        {
            LastAdvance = advance;
            if (presenters.TryGetValue(
                    advance.ProjectileId,
                    out ProjectileFlightPresenter presenter))
            {
                ProjectilePresentationDefinition presentation =
                    presentationCatalog.Get(advance.Previous.Launch.Definition.Id);
                presenter.PlayAdvance(
                    advance,
                    GetPlaybackDuration(
                        voluntaryWorldTurn: false,
                        presentation: presentation));
            }
            StatusMessage = DescribeAdvance(advance);
            dialogue.AppendCombatDiagnostic(
                GameplayCombatDiagnosticFormatter.FormatProjectileAdvance(
                    advance));
            ProjectileAdvanced?.Invoke(advance);
        }

        private void HandleReactionPredicted(
            ProjectileAdvancePrediction prediction)
        {
            if (presenters.TryGetValue(
                    prediction.ProjectileId,
                    out ProjectileFlightPresenter presenter))
            {
                presenter.SetPreviewEndpoint(prediction.CollisionPosition);
            }

            EmergencyReactionWindowRecord window = impactCycle.CurrentWindow;
            dialogue.AppendCombatDiagnostic(
                GameplayCombatDiagnosticFormatter.FormatReactionPrediction(
                    prediction,
                    window));
        }

        private void HandleVoluntaryTurnCycleCompleted(
            VoluntaryTurnCycleRecord _)
        {
            if (advancedDuringPendingVoluntaryCycle)
            {
                advancedDuringPendingVoluntaryCycle = false;
                return;
            }

            AdvanceFlights(voluntaryWorldTurn: true, endingActorId: null);
        }

        private void AdvanceFlights(
            bool voluntaryWorldTurn,
            string endingActorId)
        {
            if (projectiles == null || presenters.Count == 0)
            {
                return;
            }

            var projectileIds = new List<string>(presenters.Keys);
            foreach (string projectileId in projectileIds)
            {
                if (endingActorId != null
                    && impactCycle != null
                    && impactCycle.ConsumeProjectedLaunchTurn(
                        projectileId,
                        endingActorId))
                {
                    continue;
                }

                ProjectileFlightSnapshot flight = projectiles.GetProjectile(
                    projectileId);
                if (flight.Status != ProjectileFlightStatus.InFlight)
                {
                    continue;
                }

                ProjectileAdvanceRecord advance = projectiles.Advance(
                    projectileId,
                    turnTime: 1f);
                ProjectilePresentationDefinition presentation =
                    presentationCatalog.Get(flight.Launch.Definition.Id);
                LastAdvance = advance;
                presenters[projectileId].PlayAdvance(
                    advance,
                    GetPlaybackDuration(
                        voluntaryWorldTurn,
                        presentation));
                StatusMessage = DescribeAdvance(advance);
                dialogue.AppendCombatDiagnostic(
                    GameplayCombatDiagnosticFormatter
                        .FormatProjectileAdvance(advance));
                ProjectileAdvanced?.Invoke(advance);
            }
        }

        private void ConsumeProjectedLaunchTurns(string endingActorId)
        {
            if (impactCycle == null || endingActorId == null)
            {
                return;
            }

            foreach (string projectileId in presenters.Keys)
            {
                impactCycle.ConsumeProjectedLaunchTurn(
                    projectileId,
                    endingActorId);
            }
        }

        private bool TryGetAimPoint(
            string attackerId,
            string targetId,
            out GameplayPosition aimPoint)
        {
            if (!registry.TryGetActor(targetId, out GameplayActorView target))
            {
                if (acquisition != null
                    && acquisition.TryGetPresentationAimPoint(
                        Session.GetEquippedAttack(attackerId)
                            .Projectile.MaximumRange,
                        out Vector3 worldAimPoint))
                {
                    aimPoint = ToGameplayPosition(worldAimPoint);
                    return true;
                }

                aimPoint = default;
                return false;
            }

            IReadOnlyList<ActorTargetRegionSample> regions =
                target.TargetProfile.GetTargetRegionSamples();
            foreach (ActorTargetRegionSample region in regions)
            {
                if (region.Id == TargetRegionId.Torso)
                {
                    aimPoint = ToGameplayPosition(region.WorldCenter);
                    return true;
                }
            }

            if (regions.Count > 0)
            {
                aimPoint = ToGameplayPosition(regions[0].WorldCenter);
                return true;
            }

            aimPoint = ToGameplayPosition(target.Transform.position);
            return true;
        }

        private float GetPlaybackDuration(
            bool voluntaryWorldTurn,
            ProjectilePresentationDefinition presentation)
        {
            return voluntaryWorldTurn && Session != null
                ? Session.Scenario.Timing.MinimumVoluntaryTurnSeconds
                : presentation.EncounterPlaybackSeconds;
        }

        private bool Fail(ProjectileLaunchFailure failure)
        {
            LastFailure = failure;
            StatusMessage = DescribeFailure(failure);
            return false;
        }

        private static string DescribeAdvance(ProjectileAdvanceRecord advance)
        {
            if (advance.Resulting.Status == ProjectileFlightStatus.Impacted)
            {
                return $"{advance.ProjectileId} impacted {advance.Resulting.Impact.HitEntityId}.";
            }

            if (advance.Resulting.Status == ProjectileFlightStatus.Expired)
            {
                return $"{advance.ProjectileId} reached maximum range.";
            }

            return $"{advance.ProjectileId} advanced to {advance.Resulting.DistanceTraveled:0.0} m.";
        }

        private static string DescribeFailure(ProjectileLaunchFailure failure)
        {
            switch (failure)
            {
                case ProjectileLaunchFailure.TurnModeRequired:
                    return "Enter turn mode before launching.";
                case ProjectileLaunchFailure.ActorNotActive:
                    return "Only the active actor can launch.";
                case ProjectileLaunchFailure.ActorIncapacitated:
                    return "An incapacitated actor cannot launch.";
                case ProjectileLaunchFailure.ActorPinned:
                    return "Push off the pinning prop before launching.";
                case ProjectileLaunchFailure.OperationInProgress:
                    return "Wait for the current movement to resolve.";
                case ProjectileLaunchFailure.WeaponUnavailable:
                case ProjectileLaunchFailure.ProjectileUnavailable:
                    return "No slow-projectile weapon is equipped.";
                case ProjectileLaunchFailure.TargetNotFound:
                    return "Click a visible target or world point.";
                case ProjectileLaunchFailure.InvalidAimPoint:
                    return "The projectile requires a distinct aim point.";
                case ProjectileLaunchFailure.InsufficientActionPoints:
                    return "Not enough AP remains for this launch.";
                case ProjectileLaunchFailure.InsufficientMovementOpportunity:
                    return "Not enough movement remains for this launch.";
                case ProjectileLaunchFailure.InsufficientLoadedAmmunition:
                    return "The equipped launcher is empty. Reload before launching.";
                case ProjectileLaunchFailure.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private static GameplayPosition ToGameplayPosition(Vector3 position) =>
            new GameplayPosition(position.x, position.y, position.z);
    }
}
