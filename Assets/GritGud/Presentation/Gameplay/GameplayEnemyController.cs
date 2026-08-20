using System;
using System.Collections.Generic;
using System.Threading;
using GritGud.Application.Gameplay;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayEnemyController : MonoBehaviour
    {
        private GameplaySession session;
        private GameplayPartyControlSession partyControl;
        private GameplayEnemyRuntimeRegistry enemies;
        private GameplayCommittedActionConsequenceCoordinator committedConsequences;
        private GameplayEnemyExplorationCoordinator exploration;
        private GameplayEnemyCombatTurnExecutor combatTurns;
        private GameplayEnemyOutcomePresenter outcomes;

        internal int EnemyCount => enemies?.Count ?? 0;

        internal void Bind(
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            GameplaySessionPresenter modePresenter,
            GameplayActionController actions,
            GameplayAttackController attacks,
            GameplayProjectileController projectiles,
            GameplayDisplacementController displacements,
            GameplayPartyControlSession controlledParty,
            GameplayDroneController droneController,
            GameplayDialogueLog dialogueLog,
            Func<IReadOnlyList<string>, bool> onEncounterStartRequested,
            GameplayTacticalTransitionPresenter tacticalTransition,
            EnemyPresentationCatalog enemyPresentationCatalog = null,
            ISightObscuranceQuery obscuranceQuery = null,
            IEnumerable<LevelTraversalLinkData> traversalLinks = null)
        {
            Unbind();
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            GameplayWorldRegistry registry = worldRegistry
                ?? throw new ArgumentNullException(nameof(worldRegistry));
            GameplaySessionPresenter sessionPresenter = modePresenter
                ?? throw new ArgumentNullException(nameof(modePresenter));
            GameplayActionController actionController = actions
                ?? throw new ArgumentNullException(nameof(actions));
            GameplayAttackController attackController = attacks
                ?? throw new ArgumentNullException(nameof(attacks));
            GameplayProjectileController projectileController = projectiles
                ?? throw new ArgumentNullException(nameof(projectiles));
            GameplayDisplacementController displacementController =
                displacements
                ?? throw new ArgumentNullException(nameof(displacements));
            partyControl = controlledParty ?? throw new ArgumentNullException(
                nameof(controlledParty));
            GameplayDialogueLog dialogue = dialogueLog
                ?? throw new ArgumentNullException(nameof(dialogueLog));
            Func<IReadOnlyList<string>, bool> beginEncounter =
                onEncounterStartRequested
                ?? throw new ArgumentNullException(
                    nameof(onEncounterStartRequested));
            EnemyPresentationCatalog presentationCatalog =
                enemyPresentationCatalog
                ?? EnemyPresentationCatalog.LoadDefault();
            enemies = new GameplayEnemyRuntimeRegistry(
                session,
                registry,
                attackController,
                projectileController,
                presentationCatalog,
                obscuranceQuery,
                traversalLinks);
            committedConsequences =
                new GameplayCommittedActionConsequenceCoordinator(
                    session,
                    new GameplayEnemyCommittedActionSoundQuery(enemies),
                    beginEncounter);
            exploration = new GameplayEnemyExplorationCoordinator(
                session,
                enemies,
                sessionPresenter,
                actionController,
                partyControl,
                beginEncounter,
                tacticalTransition,
                presentationCatalog.DetectionIntervalSeconds);
            outcomes = new GameplayEnemyOutcomePresenter(
                session,
                enemies,
                sessionPresenter,
                actionController,
                partyControl,
                dialogue);
            enabled = true;
        }

        internal void BindSemanticRuntime(
            GameplayLiveSessionRuntime runtime,
            GameplayScenarioAssembly assembly,
            LevelDocument level,
            GameplayCapabilityRegistry capabilities,
            GameplaySessionPresenter sessionPresenter,
            GameplayActionController actionController,
            GameplayAttackController attackController,
            GameplayProjectileController projectileController,
            GameplayDisplacementController displacementController,
            GameplayDroneController droneController,
            GameplayDialogueLog dialogue)
        {
            if (session == null || enemies == null)
                throw new InvalidOperationException(
                    "Bind enemy presentation before its semantic runtime.");
            if (combatTurns != null)
                throw new InvalidOperationException(
                    "Enemy semantic runtime is already bound.");
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (capabilities == null)
                throw new ArgumentNullException(nameof(capabilities));

            var spatial = new GameplayHeadlessSpatialEvidence(
                level,
                runtime.ExecutionIdentity.Spatial);
            IReadOnlyList<GameplayReachableInput> reachable =
                GameplayReachableInputEnumerator.Enumerate(assembly, level);
            GameplayCandidateExecutionRouteRegistry routes =
                GameplayCurrentCandidateExecutionRoutes.Create(
                    assembly,
                    spatial,
                    capabilities);
            var source = new GameplayHeadlessDecisionCandidateSource(
                new GameplayHeadlessCandidateBuilder(
                    capabilities,
                    spatial,
                    scenarioDefinition: assembly.Scenario,
                    authoredTraversalLinks: level.traversalLinks),
                reachable,
                routes);
            var runner = new GameplayPolicyDecisionRunner(
                source,
                routes,
                GameplayBaselineCombatPolicy.Create(assembly.Scenario),
                installationBoundary:
                    new GameplaySynchronizationContextRuntimeInstallationBoundary(
                        SynchronizationContext.Current));
            combatTurns = new GameplayEnemyCombatTurnExecutor(
                session,
                runtime,
                runner,
                enemies,
                sessionPresenter,
                actionController,
                attackController,
                projectileController,
                displacementController,
                droneController,
                dialogue);
        }

        internal void Unbind()
        {
            combatTurns?.Dispose();
            committedConsequences?.Dispose();
            enemies?.Dispose();
            outcomes = null;
            combatTurns = null;
            exploration = null;
            committedConsequences = null;
            enemies = null;
            partyControl = null;
            session = null;
            enabled = false;
        }

        private void Update()
        {
            if (session == null)
                return;

            outcomes.PresentNewIncapacitations();
            if (partyControl.IsPartyDefeated)
            {
                outcomes.ResolvePartyIncapacitation();
                return;
            }

            if (session.Mode == GameplaySessionMode.Exploration)
            {
                exploration.Tick(Time.unscaledDeltaTime);
                return;
            }

            if (!session.EncounterActive)
                return;

            if (!partyControl.HasCapableHostileActor())
            {
                outcomes.RequestEncounterCompletion();
                return;
            }

            combatTurns?.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
