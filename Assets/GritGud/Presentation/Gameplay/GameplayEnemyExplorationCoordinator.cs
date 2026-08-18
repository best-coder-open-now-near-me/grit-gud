using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnemyExplorationCoordinator
    {
        private readonly GameplaySession session;
        private readonly GameplayEnemyRuntimeRegistry enemies;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayActionController actionController;
        private readonly GameplayPartyControlSession partyControl;
        private readonly GameplayExplorationSoundLedger sounds;
        private readonly Func<IReadOnlyList<string>, bool> beginEncounter;
        private readonly GameplayTacticalTransitionPresenter tacticalTransition;
        private readonly float detectionIntervalSeconds;
        private float detectionDelaySeconds;
        private IReadOnlyList<string> pendingEncounterScope;
        private string pendingEncounterMessage = string.Empty;

        public GameplayEnemyExplorationCoordinator(
            GameplaySession session,
            GameplayEnemyRuntimeRegistry enemies,
            GameplaySessionPresenter sessionPresenter,
            GameplayActionController actionController,
            GameplayPartyControlSession partyControl,
            GameplayExplorationSoundLedger soundLedger,
            Func<IReadOnlyList<string>, bool> beginEncounter,
            GameplayTacticalTransitionPresenter tacticalTransition,
            float detectionIntervalSeconds)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            this.enemies = enemies ?? throw new ArgumentNullException(
                nameof(enemies));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.actionController = actionController
                ?? throw new ArgumentNullException(nameof(actionController));
            this.partyControl = partyControl ?? throw new ArgumentNullException(
                nameof(partyControl));
            sounds = soundLedger ?? throw new ArgumentNullException(
                nameof(soundLedger));
            this.beginEncounter = beginEncounter
                ?? throw new ArgumentNullException(nameof(beginEncounter));
            this.tacticalTransition = tacticalTransition
                ?? throw new ArgumentNullException(nameof(tacticalTransition));
            this.detectionIntervalSeconds = detectionIntervalSeconds;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (tacticalTransition.CombatEntryReady)
            {
                IReadOnlyList<string> scope = pendingEncounterScope;
                pendingEncounterScope = null;
                if (scope != null && beginEncounter(scope))
                {
                    actionController.PresentExternalStatus(
                        pendingEncounterMessage + " Combat initiated.");
                    sessionPresenter.RefreshModePresentation();
                }
                pendingEncounterMessage = string.Empty;
                tacticalTransition.CompleteCombatEntry();
                return;
            }
            if (pendingEncounterScope != null)
                return;

            sounds.Advance(unscaledDeltaTime);
            if (TickPatrolPlayback(unscaledDeltaTime))
                return;

            detectionDelaySeconds -= unscaledDeltaTime;
            if (detectionDelaySeconds > 0f)
                return;
            detectionDelaySeconds = detectionIntervalSeconds;

            foreach (GameplayEnemyRuntimeRegistry.Entry enemy in
                enemies.OrderedEntries)
            {
                if (session.IsActorIncapacitated(enemy.Definition.Id))
                    continue;
                EncounterObservation observation = CaptureObservation(enemy);
                EnemyAwarenessTransitionRecord transition = session
                    .PrepareAwarenessTransition(enemy.Definition.Id, observation);
                if (HasAwarenessChanged(transition))
                    session.CommitAwarenessTransition(transition);

                if (transition.Resulting.State == EncounterAwarenessState.Alert)
                {
                    pendingEncounterScope = session.CreateDetectionEncounterScope(
                        enemy.Definition.Id,
                        transition.Resulting.LastKnownHostileId);
                    pendingEncounterMessage = $"{enemy.Definition.Id} detected "
                        + $"{transition.Resulting.LastKnownHostileId}.";
                    tacticalTransition.BeginCombatEntry(
                        enemy.Definition.Id,
                        transition.Resulting.LastKnownHostileId);
                    return;
                }

                TryStartPatrol(enemy, transition.Resulting, observation);
            }
        }

        private bool TickPatrolPlayback(float deltaTime)
        {
            foreach (GameplayEnemyRuntimeRegistry.Entry enemy in
                enemies.OrderedEntries)
            {
                if (!enemy.Playback.IsPlaying)
                    continue;
                if (!enemy.Playback.Tick(deltaTime))
                    return true;

                PatrolAdvanceRecord completed = enemy.PendingPatrolAdvance
                    ?? throw new InvalidOperationException(
                        "A patrol playback completed without its canonical advance.");
                enemy.PendingPatrolAdvance = null;
                session.CommitPatrolAdvance(completed);
                return true;
            }

            return false;
        }

        private EncounterObservation CaptureObservation(
            GameplayEnemyRuntimeRegistry.Entry enemy)
        {
            TargetExposureSnapshot sight = null;
            GameplayPosition? targetPosition = null;
            foreach (string targetId in partyControl.ActorIds)
            {
                if (session.IsActorIncapacitated(targetId)
                    || !session.IsHostile(enemy.Definition.Id, targetId))
                {
                    continue;
                }

                TargetExposureSnapshot candidate = enemy.TacticalQuery
                    .CaptureExposure(targetId);
                if (candidate.VisibleSampleCount == 0
                    || (sight != null
                        && candidate.VisibleFraction
                            <= sight.VisibleFraction))
                {
                    continue;
                }

                sight = candidate;
                targetPosition = session.GetActor(targetId).Pose.Position;
            }

            EncounterSoundEvidence sound = null;
            if (sounds.TryConsume(
                    enemy.Definition.Id,
                    out string sourceId,
                    out GameplayPosition origin,
                    out float loudness))
            {
                sound = enemy.TacticalQuery.CaptureSound(
                    sourceId,
                    origin,
                    loudness);
            }

            return new EncounterObservation(
                enemy.Definition.Id,
                sight,
                targetPosition,
                sound);
        }

        private void TryStartPatrol(
            GameplayEnemyRuntimeRegistry.Entry enemy,
            EnemyAwarenessSnapshot awareness,
            EncounterObservation observation)
        {
            if (awareness.State != EncounterAwarenessState.Unaware
                || observation.HasVisibleSight
                || observation.HasAudibleSound
                || enemy.Playback.IsPlaying
                || enemy.PendingPatrolAdvance != null)
            {
                return;
            }

            PatrolRouteDefinition route = enemy.Definition.Combat
                .EnemyBehavior.PatrolRoute;
            if (route == null)
                return;
            int nextIndex = route.GetNextWaypointIndex(
                awareness.PatrolWaypointIndex);
            if (nextIndex == awareness.PatrolWaypointIndex
                || !enemy.TacticalQuery.TryBuildPatrolRoute(
                    route.GetWaypoint(nextIndex),
                    out MovementRouteRecord movementRoute))
            {
                return;
            }

            enemy.PendingPatrolAdvance = session.PreparePatrolAdvance(
                enemy.Definition.Id,
                movementRoute);
            enemy.Playback.Begin(movementRoute);
        }

        private static bool HasAwarenessChanged(
            EnemyAwarenessTransitionRecord transition)
        {
            EnemyAwarenessSnapshot previous = transition.Previous;
            EnemyAwarenessSnapshot resulting = transition.Resulting;
            return previous.State != resulting.State
                || previous.Suspicion != resulting.Suspicion
                || !string.Equals(
                    previous.LastKnownHostileId,
                    resulting.LastKnownHostileId,
                    StringComparison.Ordinal)
                || !Nullable.Equals(
                    previous.LastKnownHostilePosition,
                    resulting.LastKnownHostilePosition)
                || previous.PatrolWaypointIndex != resulting.PatrolWaypointIndex;
        }
    }
}
