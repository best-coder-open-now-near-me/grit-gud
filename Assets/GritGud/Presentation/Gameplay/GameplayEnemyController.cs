using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayEnemyController : MonoBehaviour
    {
        private sealed class EnemyRuntime
        {
            public EnemyRuntime(
                ScenarioActorDefinition definition,
                GameplayActorView view,
                GameplayEnemyActorPresenter presentation,
                UnityEnemyTacticalQuery tacticalQuery)
            {
                Definition = definition;
                View = view;
                Presentation = presentation;
                TacticalQuery = tacticalQuery;
            }

            public ScenarioActorDefinition Definition { get; }

            public GameplayActorView View { get; }

            public GameplayEnemyActorPresenter Presentation { get; }

            public UnityEnemyTacticalQuery TacticalQuery { get; }

            public MovementRoutePlaybackPresenter Playback =>
                Presentation.Playback;

            public int AttacksCommittedThisTurn { get; set; }

        }

        private readonly Dictionary<string, EnemyRuntime> enemies =
            new Dictionary<string, EnemyRuntime>(StringComparer.Ordinal);
        private readonly List<string> orderedEnemyIds = new List<string>();
        private GameplaySession session;
        private GameplayWorldRegistry registry;
        private GameplaySessionPresenter sessionPresenter;
        private GameplayActionController actionController;
        private GameplayAttackController attackController;
        private GameplayProjectileController projectileController;
        private GameplayDisplacementController displacementController;
        private GameplayEmergencyCycleSession emergencyCycle;
        private GameplayPartyControlSession partyControl;
        private GameplayEnemyDecisionSession decisions;
        private GameplayDialogueLog dialogue;
        private EnemyPresentationCatalog presentationCatalog;
        private Func<bool> beginEncounter;
        private string observedActiveActorId;
        private long observedTurnSequence = -1L;
        private float decisionDelaySeconds;
        private float detectionDelaySeconds;
        private readonly HashSet<string> reportedPartyIncapacitations =
            new HashSet<string>(StringComparer.Ordinal);
        private bool partyIncapacitationReported;

        internal int EnemyCount => enemies.Count;

        internal void Bind(
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            GameplaySessionPresenter modePresenter,
            GameplayActionController actions,
            GameplayAttackController attacks,
            GameplayProjectileController projectiles,
            GameplayDisplacementController displacements,
            GameplayEmergencyCycleSession cycle,
            GameplayPartyControlSession controlledParty,
            GameplayDialogueLog dialogueLog,
            Func<bool> onEncounterStartRequested,
            EnemyPresentationCatalog enemyPresentationCatalog = null,
            ISightObscuranceQuery obscuranceQuery = null)
        {
            Unbind();
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            sessionPresenter = modePresenter ?? throw new ArgumentNullException(
                nameof(modePresenter));
            actionController = actions ?? throw new ArgumentNullException(
                nameof(actions));
            attackController = attacks ?? throw new ArgumentNullException(
                nameof(attacks));
            projectileController = projectiles ?? throw new ArgumentNullException(
                nameof(projectiles));
            displacementController = displacements
                ?? throw new ArgumentNullException(nameof(displacements));
            emergencyCycle = cycle ?? throw new ArgumentNullException(
                nameof(cycle));
            partyControl = controlledParty ?? throw new ArgumentNullException(
                nameof(controlledParty));
            dialogue = dialogueLog ?? throw new ArgumentNullException(
                nameof(dialogueLog));
            presentationCatalog = enemyPresentationCatalog
                ?? EnemyPresentationCatalog.LoadDefault();
            beginEncounter = onEncounterStartRequested
                ?? throw new ArgumentNullException(
                    nameof(onEncounterStartRequested));
            decisions = new GameplayEnemyDecisionSession(session);

            foreach (ScenarioActorDefinition definition in
                session.Scenario.Actors)
            {
                if (definition.Combat.EnemyBehavior == null)
                    continue;
                GameplayActorView view = registry.GetActor(definition.Id);
                var presentation = new GameplayEnemyActorPresenter(
                    session,
                    registry,
                    attackController,
                    projectileController,
                    definition,
                    view,
                    presentationCatalog.Get(
                        view.PresentationId));
                var query = new UnityEnemyTacticalQuery(
                    session,
                    registry,
                    definition,
                    view,
                    obscuranceQuery);
                enemies.Add(
                    definition.Id,
                    new EnemyRuntime(
                        definition,
                        view,
                        presentation,
                        query));
                orderedEnemyIds.Add(definition.Id);
            }

            detectionDelaySeconds = 0f;
            decisionDelaySeconds = 0f;
            reportedPartyIncapacitations.Clear();
            partyIncapacitationReported = false;
            enabled = true;
        }

        internal void Unbind()
        {
            foreach (EnemyRuntime enemy in enemies.Values)
            {
                enemy.Presentation.Dispose();
            }

            enemies.Clear();
            orderedEnemyIds.Clear();
            session = null;
            registry = null;
            sessionPresenter = null;
            actionController = null;
            attackController = null;
            projectileController = null;
            displacementController = null;
            emergencyCycle = null;
            partyControl = null;
            decisions = null;
            dialogue = null;
            presentationCatalog = null;
            beginEncounter = null;
            observedActiveActorId = null;
            observedTurnSequence = -1L;
            decisionDelaySeconds = 0f;
            detectionDelaySeconds = 0f;
            reportedPartyIncapacitations.Clear();
            partyIncapacitationReported = false;
            enabled = false;
        }

        private void Update()
        {
            if (session == null)
                return;

            PresentNewIncapacitations();
            if (partyControl.IsPartyDefeated)
            {
                ResolvePartyIncapacitation();
                return;
            }

            if (session.Mode == GameplaySessionMode.Exploration)
            {
                TickDetection();
                return;
            }

            if (!session.EncounterActive)
                return;

            if (!partyControl.HasCapableHostileActor())
            {
                RequestEncounterCompletion();
                return;
            }

            RefreshTurnIdentity();
            if (!enemies.TryGetValue(
                    session.ActiveActorId ?? string.Empty,
                    out EnemyRuntime activeEnemy))
                return;

            if (activeEnemy.Playback.IsPlaying)
            {
                TickMovement(activeEnemy);
                return;
            }

            if (session.Operation != GameplaySessionOperation.None)
                return;

            decisionDelaySeconds = Mathf.Max(
                0f,
                decisionDelaySeconds - Time.unscaledDeltaTime);
            if (decisionDelaySeconds > 0f)
                return;

            ExecuteDecision(activeEnemy);
        }

        private void TickDetection()
        {
            detectionDelaySeconds -= Time.unscaledDeltaTime;
            if (detectionDelaySeconds > 0f)
                return;
            detectionDelaySeconds =
                presentationCatalog.DetectionIntervalSeconds;

            foreach (string enemyId in orderedEnemyIds)
            {
                EnemyRuntime enemy = enemies[enemyId];
                if (session.IsActorIncapacitated(enemyId))
                    continue;
                EnemyTacticalDecisionRecord detection =
                    decisions.EvaluateBestDetection(
                        enemyId,
                        partyControl.ActorIds,
                        enemy.TacticalQuery.CaptureExposure);
                if (detection == null)
                    continue;

                decisions.Commit(detection);
                AppendDecisionDiagnostic(detection);
                if (beginEncounter())
                {
                    actionController.PresentExternalStatus(
                        $"{enemy.Definition.Id} detected {detection.TargetId}. Combat initiated.");
                    sessionPresenter.RefreshModePresentation();
                }
                return;
            }
        }

        private void ExecuteDecision(EnemyRuntime enemy)
        {
            if (session.IsActorIncapacitated(enemy.Definition.Id))
            {
                EndActiveTurn(enemy, "incapacitated actor skipped");
                return;
            }

            GameplayActorSnapshot actor = session.GetActor(
                enemy.Definition.Id);
            if (actor.IsPinned)
            {
                ExecutePushOff(enemy, actor.PinState.PropId);
                return;
            }

            EnemyTargetSelection target = decisions.SelectBestTarget(
                enemy.Definition.Id,
                partyControl.ActorIds,
                enemy.TacticalQuery.CaptureExposure);
            if (target == null)
            {
                EndActiveTurn(enemy, "no capable hostile target remains");
                return;
            }

            string targetId = target.TargetId;
            TargetExposureSnapshot exposure = target.Exposure;
            IReadOnlyList<EnemyMovementOption> movementOptions =
                decisions.RequiresMovementSearch(
                    enemy.Definition.Id,
                    targetId,
                    exposure)
                    ? enemy.TacticalQuery.BuildMovementOptions(targetId)
                    : Array.Empty<EnemyMovementOption>();
            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                enemy.Definition.Id,
                targetId,
                exposure,
                movementOptions,
                enemy.AttacksCommittedThisTurn);
            decisions.Commit(decision);
            AppendDecisionDiagnostic(decision);

            switch (decision.Kind)
            {
                case EnemyTacticalDecisionKind.Attack:
                    ExecuteAttack(enemy, decision);
                    break;
                case EnemyTacticalDecisionKind.Move:
                    session.CommitMovementRoute(decision.MovementRoute);
                    enemy.Playback.Begin(decision.MovementRoute);
                    actionController.PresentExternalStatus(
                        $"{enemy.Definition.Id} is repositioning.");
                    break;
                case EnemyTacticalDecisionKind.PushOff:
                    ExecutePushOff(
                        enemy,
                        decision.TargetId,
                        recordDecision: false);
                    break;
                case EnemyTacticalDecisionKind.EndTurn:
                    EndActiveTurn(enemy, decision.Rationale);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Decision '{decision.Kind}' cannot execute during a turn.");
            }
        }

        private void ExecutePushOff(
            EnemyRuntime enemy,
            string propId,
            bool recordDecision = true)
        {
            DisplacementActionDefinition pushOff = null;
            foreach (DisplacementActionDefinition candidate in
                enemy.Definition.DisplacementActions)
            {
                if (candidate.Intent == DisplacementActionKind.PushOff)
                {
                    pushOff = candidate;
                    break;
                }
            }
            if (pushOff == null)
            {
                actionController.PresentExternalStatus(
                    $"{enemy.Definition.Id} has no authored Push Off action.");
                EndActiveTurn(enemy, "no authored push off action");
                return;
            }

            if (recordDecision)
            {
                EnemyTacticalDecisionRecord decision =
                    decisions.EvaluatePushOff(
                        enemy.Definition.Id,
                        propId);
                decisions.Commit(decision);
                AppendDecisionDiagnostic(decision);
            }
            if (displacementController.TryExecuteIntent(
                    enemy.Definition.Id,
                    pushOff.Id,
                    propId,
                    out _,
                    out _,
                    out DisplacementResolutionFailure failure))
            {
                actionController.PresentExternalStatus(
                    $"{enemy.Definition.Id} pushed off the pinning prop.");
                decisionDelaySeconds = enemy.Presentation
                    .PresentationDefinition.PostDecisionDelaySeconds;
                return;
            }

            actionController.PresentExternalStatus(
                $"{enemy.Definition.Id} could not push off: {failure}.");
            EndActiveTurn(enemy, $"push off failed: {failure}");
        }

        private void ExecuteAttack(
            EnemyRuntime enemy,
            EnemyTacticalDecisionRecord decision)
        {
            if (attackController.TryResolveActorAttack(
                    enemy.Definition.Id,
                    decision.Exposure,
                    out GameplayActionRecord action,
                    out AttackResolutionFailure failure))
            {
                enemy.AttacksCommittedThisTurn++;
                AttackResolutionRecord resolution =
                    ((AttackResolvedActionOutcome)action.Outcomes[0]).Attack;
                actionController.PresentExternalStatus(
                    resolution.Hit
                        ? $"{enemy.Definition.Id} hit {resolution.TargetId}."
                        : $"{enemy.Definition.Id} missed {resolution.TargetId}.");
                decisionDelaySeconds = enemy.Presentation
                    .PresentationDefinition.PostAttackDelaySeconds;
                return;
            }

            actionController.PresentExternalStatus(
                $"{enemy.Definition.Id} attack failed: {failure}.");
            EndActiveTurn(enemy, $"attack failed: {failure}");
        }

        private void TickMovement(EnemyRuntime enemy)
        {
            if (!enemy.Playback.Tick(Time.deltaTime))
                return;
            session.CompleteMovementResolution();
            GameplayActorSnapshot actor = session.GetActor(enemy.Definition.Id);
            enemy.View.Transform.SetPositionAndRotation(
                MovementRouteSampling.ToVector3(actor.Pose.Position),
                Quaternion.Euler(0f, actor.Pose.FacingDegrees, 0f));
            decisionDelaySeconds = enemy.Presentation
                .PresentationDefinition.PostDecisionDelaySeconds;
        }

        private void EndActiveTurn(EnemyRuntime enemy, string rationale)
        {
            if (!emergencyCycle.TryEndTurn(
                    enemy.Definition.Id,
                    out TurnEndFailure failure))
            {
                actionController.PresentExternalStatus(
                    $"Enemy turn could not end: {failure}.");
                return;
            }
            actionController.PresentExternalStatus(
                $"{enemy.Definition.Id} ended its turn - {rationale}.");
            sessionPresenter.RefreshModePresentation();
            decisionDelaySeconds = enemy.Presentation
                .PresentationDefinition.PostDecisionDelaySeconds;
        }

        private void RefreshTurnIdentity()
        {
            long turnSequence = session.LastEndedTurn?.Sequence ?? 0L;
            if (string.Equals(
                    observedActiveActorId,
                    session.ActiveActorId,
                    StringComparison.Ordinal)
                && observedTurnSequence == turnSequence)
                return;
            observedActiveActorId = session.ActiveActorId;
            observedTurnSequence = turnSequence;
            decisionDelaySeconds = 0f;
            if (enemies.TryGetValue(
                    observedActiveActorId ?? string.Empty,
                    out EnemyRuntime enemy))
            {
                enemy.AttacksCommittedThisTurn = 0;
                decisionDelaySeconds = enemy.Presentation
                    .PresentationDefinition.PostDecisionDelaySeconds;
            }
        }

        private void PresentNewIncapacitations()
        {
            foreach (EnemyRuntime enemy in enemies.Values)
            {
                if (!session.IsActorIncapacitated(enemy.Definition.Id)
                    || !enemy.Presentation.PresentIncapacitation())
                    continue;
                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "HOSTILE INCAPACITATED",
                    $"{enemy.Definition.Id} can no longer act or respond.");
            }

            foreach (string actorId in partyControl.ActorIds)
            {
                if (!session.IsActorIncapacitated(actorId)
                    || !reportedPartyIncapacitations.Add(actorId))
                {
                    continue;
                }

                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "PARTY MEMBER INCAPACITATED",
                    $"{GetActorDisplayName(actorId)} can no longer act or respond.");
            }
        }

        private void ResolvePartyIncapacitation()
        {
            if (!partyIncapacitationReported)
            {
                partyIncapacitationReported = true;
                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "PARTY INCAPACITATED",
                    "No party member can continue. Reload or return to the menu to reset the scenario.");
                actionController.PresentExternalStatus(
                    "Party incapacitated. Reload or return to the menu to reset.");
            }
            CompleteEncounter("The party is incapacitated.");
        }

        private string GetActorDisplayName(string actorId) =>
            session.Scenario.GetActor(actorId).CharacterProfile?.DisplayName
            ?? actorId;

        private void CompleteEncounter(string message)
        {
            if (!session.EncounterActive)
                return;
            session.CompleteEncounter();
            dialogue.Append(
                GameplayDialogueChannel.System,
                "ENCOUNTER COMPLETE",
                message);
            actionController.PresentExternalStatus(message);
            sessionPresenter.RefreshModePresentation();
        }

        private void RequestEncounterCompletion()
        {
            if (!session.RequestEncounterCompletionAtTurnEnd())
                return;
            const string message =
                "All hostile actors are incapacitated. End the current turn to conclude the encounter.";
            dialogue.Append(
                GameplayDialogueChannel.System,
                "HOSTILES INCAPACITATED",
                message);
            actionController.PresentExternalStatus(message);
            sessionPresenter.RefreshModePresentation();
        }

        private void AppendDecisionDiagnostic(
            EnemyTacticalDecisionRecord decision)
        {
            dialogue.AppendCombatDiagnostic(
                GameplayCombatDiagnosticFormatter.FormatEnemyDecision(
                    decision));
        }

        private void OnDestroy()
        {
            Unbind();
        }

    }
}
