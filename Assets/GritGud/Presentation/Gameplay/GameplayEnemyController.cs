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
        private bool replayPaused;

        internal int EnemyCount => enemies?.Count ?? 0;

        internal bool ReplayPaused => replayPaused;

        internal void SetReplayPaused(bool paused)
        {
            replayPaused = paused;
            exploration?.SetPaused(paused);
            combatTurns?.SetPaused(paused);
        }

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
            EnemyPresentationCatalog enemyPresentationCatalog = null)
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
                presentationCatalog);
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
                dialogue,
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
            GameplayStaticSpatialContent spatialContent,
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
            if (spatialContent == null)
                throw new ArgumentNullException(nameof(spatialContent));
            if (capabilities == null)
                throw new ArgumentNullException(nameof(capabilities));

            if (!runtime.ExecutionIdentity.Spatial.HasSameIdentity(
                    spatialContent.Identity))
                throw new ArgumentException(
                    "Enemy runtime and static spatial content differ.",
                    nameof(spatialContent));
            LevelDocument level = spatialContent.Level;
            GameplayHeadlessSpatialEvidence spatial =
                spatialContent.CreateEvidence();
            IReadOnlyList<GameplayReachableInput> reachable =
                GameplayReachableInputEnumerator.Enumerate(assembly, level);
            GameplayCandidateExecutionRouteRegistry routes =
                GameplayCurrentCandidateExecutionRoutes.Create(
                    assembly,
                    spatial,
                    capabilities);
            var candidateBuilder = new GameplayHeadlessCandidateBuilder(
                capabilities,
                spatial,
                scenarioDefinition: assembly.Scenario,
                authoredTraversalLinks: level.traversalLinks);
            var source = new GameplayHeadlessDecisionCandidateSource(
                candidateBuilder,
                reachable,
                routes);
            var installationBoundary =
                new GameplaySynchronizationContextRuntimeInstallationBoundary(
                    SynchronizationContext.Current);
            var runner = new GameplayPolicyDecisionRunner(
                source,
                routes,
                GameplayBaselineCombatPolicy.Create(assembly.Scenario),
                installationBoundary: installationBoundary);
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

            var observationInputs = new List<GameplayReachableInput>();
            var patrolInputs = new List<GameplayReachableInput>();
            foreach (GameplayReachableInput input in reachable)
            {
                if (input.Profile.Equals(
                        GameplayCapabilityProfiles.ObserveEncounter()))
                    observationInputs.Add(input);
                else if (input.Profile.Equals(
                        GameplayCapabilityProfiles.Patrol()))
                    patrolInputs.Add(input);
            }
            var neutralPolicy = new GameplayWeightedOutcomePolicy(
                Array.Empty<GameplayOutcomeFeatureWeight>());
            exploration.BindSemanticRuntime(
                runtime,
                new GameplayPolicyDecisionRunner(
                    new GameplayHeadlessDecisionCandidateSource(
                        candidateBuilder,
                        observationInputs,
                        routes),
                    routes,
                    neutralPolicy,
                    installationBoundary: installationBoundary),
                new GameplayPolicyDecisionRunner(
                    new GameplayHeadlessDecisionCandidateSource(
                        candidateBuilder,
                        patrolInputs,
                        routes),
                    routes,
                    neutralPolicy,
                    installationBoundary: installationBoundary));
        }

        internal void Unbind()
        {
            exploration?.Dispose();
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
            replayPaused = false;
            enabled = false;
        }

        private void Update()
        {
            if (session == null || replayPaused)
                return;

            outcomes.PresentNewIncapacitations();
            if (partyControl.IsPartyDefeated)
            {
                outcomes.ResolvePartyIncapacitation();
                return;
            }

            if (session.Mode == GameplaySessionMode.Exploration)
            {
                combatTurns?.ResetBattleScope();
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
