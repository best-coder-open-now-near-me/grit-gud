using System;
using System.Collections.Generic;
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
        private GameplayExplorationSoundLedger explorationSounds;
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
            GameplayEmergencyCycleSession cycle,
            GameplayPartyControlSession controlledParty,
            GameplayDialogueLog dialogueLog,
            Func<IReadOnlyList<string>, bool> onEncounterStartRequested,
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
            GameplayEmergencyCycleSession emergencyCycle = cycle
                ?? throw new ArgumentNullException(nameof(cycle));
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
            var decisions = new GameplayEnemyDecisionSession(session);

            enemies = new GameplayEnemyRuntimeRegistry(
                session,
                registry,
                attackController,
                projectileController,
                presentationCatalog,
                obscuranceQuery,
                traversalLinks);
            explorationSounds = new GameplayExplorationSoundLedger(session);
            exploration = new GameplayEnemyExplorationCoordinator(
                session,
                enemies,
                sessionPresenter,
                actionController,
                partyControl,
                explorationSounds,
                beginEncounter,
                presentationCatalog.DetectionIntervalSeconds);
            combatTurns = new GameplayEnemyCombatTurnExecutor(
                session,
                enemies,
                sessionPresenter,
                actionController,
                attackController,
                displacementController,
                emergencyCycle,
                partyControl,
                decisions,
                dialogue);
            outcomes = new GameplayEnemyOutcomePresenter(
                session,
                enemies,
                sessionPresenter,
                actionController,
                partyControl,
                dialogue);
            enabled = true;
        }

        internal void Unbind()
        {
            explorationSounds?.Dispose();
            enemies?.Dispose();
            outcomes = null;
            combatTurns = null;
            exploration = null;
            explorationSounds = null;
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

            combatTurns.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
