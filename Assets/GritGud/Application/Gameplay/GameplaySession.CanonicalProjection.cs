using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed partial class GameplaySession
    {
        private long canonicalJournalSequence;
        private bool canonicalProjectionBound;
        private Func<
            GameplayTransitionPayload,
            IEnumerable<GameplayEvidenceRecord>,
            GameplayReductionResult> canonicalExecutor;
        private Func<
            GameplayActionRecord,
            GameplayReductionResult> canonicalActionExecutor;
        private Func<bool> canonicalContinuousWorldState;

        internal bool IsCanonicalProjectionBound => canonicalProjectionBound;

        internal void BindCanonicalExecutor(
            Func<
                GameplayTransitionPayload,
                IEnumerable<GameplayEvidenceRecord>,
                GameplayReductionResult> executor,
            Func<GameplayActionRecord, GameplayReductionResult> actionExecutor,
            Func<bool> hasContinuousWorldState)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));
            if (actionExecutor == null)
                throw new ArgumentNullException(nameof(actionExecutor));
            if (hasContinuousWorldState == null)
                throw new ArgumentNullException(nameof(hasContinuousWorldState));
            if (canonicalExecutor != null || canonicalActionExecutor != null)
                throw new InvalidOperationException(
                    "The gameplay session already has a canonical executor.");
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Bind the canonical executor before binding its projection.");
            canonicalExecutor = executor;
            canonicalActionExecutor = actionExecutor;
            canonicalContinuousWorldState = hasContinuousWorldState;
        }

        private bool HasCanonicalContinuousWorldState() =>
            canonicalContinuousWorldState?.Invoke() == true;

        internal GameplayReductionResult ExecuteCanonical(
            GameplayActionRecord action)
        {
            if (!canonicalProjectionBound || canonicalActionExecutor == null)
                throw new InvalidOperationException(
                    "The semantic runtime does not own this gameplay session.");
            return canonicalActionExecutor(
                action ?? throw new ArgumentNullException(nameof(action)));
        }

        internal GameplayReductionResult ExecuteCanonical(
            GameplayTransitionPayload payload,
            IEnumerable<GameplayEvidenceRecord> evidence = null)
        {
            if (!canonicalProjectionBound || canonicalExecutor == null)
                throw new InvalidOperationException(
                    "The semantic runtime does not own this gameplay session.");
            return canonicalExecutor(
                payload ?? throw new ArgumentNullException(nameof(payload)),
                evidence);
        }

        internal long JournalSequence => canonicalProjectionBound
            ? canonicalJournalSequence
            : Journal.LastEntry?.Sequence ?? 0L;

        internal void BindCanonicalProjection(
            GameplaySessionStateSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    "The gameplay session already has a canonical runtime projection.");
            ValidateCanonicalProjection(snapshot, semanticRecord: null);
            GameplayCombatStateSnapshot current =
                GameplayCombatStateCapture.Capture(this);
            GameplayCombatStateSnapshot expected =
                new GameplayCombatStateSnapshot(snapshot);
            if (!string.Equals(
                    current.CanonicalHash,
                    expected.CanonicalHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The gameplay session does not match the runtime's initial canonical state.");
            }
            canonicalJournalSequence = snapshot.JournalSequence;
            canonicalProjectionBound = true;
        }

        internal void InstallCanonicalProjection(
            GameplaySessionStateSnapshot snapshot,
            object semanticRecord,
            GameplayNotificationBatch notifications)
        {
            if (!canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Canonical state cannot be projected into an unbound gameplay session.");
            if (notifications == null)
                throw new ArgumentNullException(nameof(notifications));
            ValidateCanonicalProjection(snapshot, semanticRecord);

            var changedActors = new List<string>();
            foreach (GameplayActorSnapshot projected in snapshot.Actors)
            {
                GameplayActorSnapshot current =
                    actors[projected.ActorId].CreateSnapshot();
                if (!string.Equals(
                        GameplayCanonicalValueDigest.Calculate(current),
                        GameplayCanonicalValueDigest.Calculate(projected),
                        StringComparison.Ordinal))
                {
                    changedActors.Add(projected.ActorId);
                }
            }

            initiativeOrder.Clear();
            initiativeOrder.AddRange(snapshot.InitiativeOrder);
            foreach (GameplayActorSnapshot actor in snapshot.Actors)
                actors[actor.ActorId].InstallCanonicalSnapshot(actor);
            foreach (GameplayObjectiveSnapshot objective in snapshot.Objectives)
                objectives[objective.ObjectiveId]
                    .InstallCanonicalSnapshot(objective);

            long previousActionSequence = LastActionSequence;
            Operation = snapshot.Operation;
            pendingMovementRoute = snapshot.PendingMovementRoute;
            encounterState = snapshot.EncounterState;
            Revision = snapshot.Revision;
            LastTransitionSequence = snapshot.LastTransitionSequence;
            canonicalJournalSequence = snapshot.JournalSequence;

            if (snapshot.LastActionSequence > previousActionSequence)
            {
                if (semanticRecord is GameplayActionRecord action)
                {
                    resolvedActions.Add(action);
                    notifications.Add(ActionResolved, action);
                    AmmunitionSpentActionOutcome spend =
                        GameplayWeaponActionOutcomes.GetAmmunitionSpend(
                            action);
                    if (spend != null)
                        notifications.Add(
                            AmmunitionChanged,
                            spend.Change);
                }
                else
                {
                    lastAuxiliaryActionSequence =
                        snapshot.LastActionSequence;
                }
            }
            turnLifecycle.InstallCanonicalSnapshot(
                snapshot,
                semanticRecord,
                notifications);
            if (semanticRecord is EnemyAwarenessTransitionRecord awareness)
                notifications.Add(EnemyAwarenessChanged, awareness);
            if (semanticRecord is PatrolAdvanceRecord patrol)
                notifications.Add(PatrolAdvanced, patrol);
            foreach (string actorId in changedActors)
                notifications.Add(ActorCapabilityChanged, actorId);

        }

        internal void ValidateCanonicalProjection(
            GameplaySessionStateSnapshot snapshot,
            object semanticRecord)
        {
            if (!string.Equals(
                    snapshot.ScenarioId,
                    Scenario.Id,
                    StringComparison.Ordinal)
                || !RunIdentity.HasSameIdentity(snapshot.RunIdentity))
                throw new InvalidOperationException(
                    "Canonical projection identity does not match the gameplay session.");
            if (snapshot.Actors.Count != actors.Count
                || snapshot.Objectives.Count != objectives.Count)
                throw new InvalidOperationException(
                    "Canonical projection changed the scenario's actor or objective set.");
            RequireSameOrder(
                allInitiativeOrder,
                snapshot.AllInitiativeOrder,
                "all-actor initiative");
            foreach (string actorId in snapshot.InitiativeOrder)
                if (!actors.ContainsKey(actorId))
                    throw new InvalidOperationException(
                        $"Canonical initiative contains unknown actor '{actorId}'.");
            foreach (GameplayActorSnapshot actor in snapshot.Actors)
            {
                if (!actors.TryGetValue(
                        actor.ActorId,
                        out GameplayActorState state))
                    throw new InvalidOperationException(
                        $"Canonical projection contains unknown actor '{actor.ActorId}'.");
                state.ValidateCanonicalSnapshot(actor);
            }
            foreach (GameplayObjectiveSnapshot objective in snapshot.Objectives)
            {
                if (!objectives.TryGetValue(
                        objective.ObjectiveId,
                        out GameplayObjectiveState state))
                    throw new InvalidOperationException(
                        $"Canonical projection contains unknown objective '{objective.ObjectiveId}'.");
                state.ValidateCanonicalSnapshot(objective);
            }
            turnLifecycle.ValidateCanonicalSnapshot(snapshot, semanticRecord);
            if (canonicalProjectionBound
                && snapshot.LastTransitionSequence
                    != LastTransitionSequence + 1L)
                throw new InvalidOperationException(
                    "Canonical live projections must install the next transition exactly once.");
            if (snapshot.LastActionSequence < LastActionSequence
                || snapshot.LastActionSequence > LastActionSequence + 1L)
                throw new InvalidOperationException(
                    "Canonical action sequence moved backwards or skipped a value.");
            if (snapshot.LastActionSequence > LastActionSequence
                && GetCanonicalActionSequence(semanticRecord)
                    != snapshot.LastActionSequence)
                throw new InvalidOperationException(
                    "Canonical action state and semantic action record disagree.");
            if (canonicalProjectionBound
                && (snapshot.JournalSequence < canonicalJournalSequence
                    || snapshot.Revision < Revision))
                throw new InvalidOperationException(
                    "Canonical journal or revision state moved backwards.");
        }

        private static void RequireSameOrder(
            IReadOnlyList<string> expected,
            IReadOnlyList<string> actual,
            string label)
        {
            if (expected.Count == actual.Count)
            {
                for (int index = 0; index < expected.Count; index++)
                    if (!string.Equals(
                            expected[index],
                            actual[index],
                            StringComparison.Ordinal))
                        break;
                    else if (index == expected.Count - 1)
                        return;
                if (expected.Count == 0) return;
            }
            throw new InvalidOperationException(
                $"Canonical projection changed {label} ordering.");
        }

        private static long GetCanonicalActionSequence(object semanticRecord)
        {
            switch (semanticRecord)
            {
                case GameplayActionRecord action:
                    return action.Sequence;
                case ActorDroneAttackRecord actorDroneAttack:
                    return actorDroneAttack.Sequence;
                default:
                    return 0L;
            }
        }

        private void RequireLegacyMutationAllowed(string operation)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    $"Legacy mutation '{operation}' is disabled while the semantic runtime owns gameplay state.");
        }
    }
}
