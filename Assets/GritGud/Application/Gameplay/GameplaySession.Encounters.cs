using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Encounter-owned session transitions. This partial keeps scoped
    /// initiative, awareness, and patrol separate from the core action and
    /// turn-resource responsibilities of <see cref="GameplaySession"/>.
    /// </summary>
    public sealed partial class GameplaySession
    {
        public event Action<EnemyAwarenessTransitionRecord>
            EnemyAwarenessChanged;

        public event Action<PatrolAdvanceRecord> PatrolAdvanced;

        public bool BeginEncounter() => BeginEncounter(allInitiativeOrder);

        public bool BeginEncounter(IEnumerable<string> participantIds)
        {
            if (EncounterActive)
                return false;
            IReadOnlyList<string> scope = NormalizeEncounterScope(
                participantIds);
            if (IsCanonicalProjectionBound)
            {
                ExecuteCanonical(new GameplaySessionControlTransitionPayload(
                    CanonicalControlActorId(),
                    GameplaySemanticCapability.ChangeEncounter,
                    "begin",
                    encounterParticipantIds: scope));
                return true;
            }
            RequireLegacyMutationAllowed(nameof(BeginEncounter));
            var previousScope = new List<string>(initiativeOrder);
            ReplaceInitiativeScope(scope);
            if (!turnLifecycle.BeginEncounter(scope))
            {
                ReplaceInitiativeScope(previousScope);
                return false;
            }

            encounterState = encounterState.WithParticipants(scope);
            return true;
        }

        public bool BeginEncounterFromAction(GameplayActionRecord action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (EncounterActive
                || !ReferenceEquals(action, LastResolvedAction)
                || !ActionStartsEncounter(action))
            {
                return false;
            }

            if (!IsCanonicalProjectionBound)
                RequireLegacyMutationAllowed(nameof(BeginEncounterFromAction));

            return BeginEncounter(CreateEncounterScope(
                action.Request.ActorId,
                action.Request.TargetId));
        }

        public bool CompleteEncounter()
        {
            if (IsCanonicalProjectionBound)
            {
                if (!EncounterActive)
                    return false;
                ExecuteCanonical(new GameplaySessionControlTransitionPayload(
                    CanonicalControlActorId(),
                    GameplaySemanticCapability.ChangeEncounter,
                    "complete"));
                return true;
            }
            RequireLegacyMutationAllowed(nameof(CompleteEncounter));
            if (!turnLifecycle.CompleteEncounter())
                return false;
            ReplaceInitiativeScope(allInitiativeOrder);
            encounterState = encounterState.WithParticipants(
                Array.Empty<string>());
            return true;
        }

        public IReadOnlyList<string> CreateEncounterScope(
            string initiatorActorId,
            string triggerSubjectId)
        {
            RequireActor(initiatorActorId);
            var scope = new HashSet<string>(StringComparer.Ordinal);
            if (Scenario.PlayerParty != null)
            {
                foreach (string actorId in Scenario.PlayerParty.ActorIds)
                    scope.Add(actorId);
            }
            scope.Add(initiatorActorId);
            if (!string.IsNullOrWhiteSpace(triggerSubjectId)
                && actors.ContainsKey(triggerSubjectId))
            {
                scope.Add(triggerSubjectId);
            }

            var pending = new Queue<string>(scope);
            while (pending.Count > 0)
            {
                string actorId = pending.Dequeue();
                EnemyBehaviorDefinition behavior = Scenario.GetActor(actorId)
                    .Combat.EnemyBehavior;
                if (behavior == null)
                    continue;
                foreach (string reinforcementId in
                    behavior.ReinforcementActorIds)
                {
                    if (scope.Add(reinforcementId))
                        pending.Enqueue(reinforcementId);
                }
            }
            return OrderByInitiative(scope);
        }

        public IReadOnlyList<string> CreateDetectionEncounterScope(
            string observerActorId,
            string detectedActorId)
        {
            RequireActor(observerActorId);
            RequireActor(detectedActorId);
            var scope = new HashSet<string>(StringComparer.Ordinal)
            {
                observerActorId,
                detectedActorId,
            };
            var pending = new Queue<string>(scope);
            while (pending.Count > 0)
            {
                EnemyBehaviorDefinition behavior = Scenario.GetActor(
                    pending.Dequeue()).Combat.EnemyBehavior;
                if (behavior == null)
                    continue;
                foreach (string reinforcementId in behavior.ReinforcementActorIds)
                {
                    if (scope.Add(reinforcementId))
                        pending.Enqueue(reinforcementId);
                }
            }
            return OrderByInitiative(scope);
        }

        public EnemyAwarenessTransitionRecord PrepareAwarenessTransition(
            string actorId,
            EncounterObservation observation)
        {
            ScenarioActorDefinition actor = RequireActorDefinition(actorId);
            EnemyBehaviorDefinition behavior = actor.Combat.EnemyBehavior
                ?? throw new InvalidOperationException(
                    $"Actor '{actorId}' does not author encounter awareness.");
            if (observation?.Sight != null)
            {
                GameplayActorState target = RequireActor(
                    observation.Sight.TargetId);
                if (!PositionsMatch(
                        target.Pose.Position,
                        observation.SightTargetPosition.Value))
                {
                    throw new InvalidOperationException(
                        "Sight evidence no longer describes the authoritative target pose.");
                }
            }
            EnemyAwarenessSnapshot previous = encounterState.GetAwareness(actorId);
            EnemyAwarenessSnapshot resulting = EncounterAwarenessRules.Evaluate(
                behavior,
                RequireActor(actorId).Pose,
                previous,
                observation);
            return new EnemyAwarenessTransitionRecord(
                checked(encounterState.LastTransitionSequence + 1L),
                actorId,
                previous,
                resulting,
                observation);
        }

        public void CommitAwarenessTransition(
            EnemyAwarenessTransitionRecord transition)
        {
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            if (IsCanonicalProjectionBound)
            {
                EnemyBehaviorDefinition behavior = Scenario.GetActor(
                    transition.ActorId).Combat.EnemyBehavior
                    ?? throw new InvalidOperationException(
                        $"Actor '{transition.ActorId}' does not author encounter awareness.");
                ExecuteCanonical(
                    new GameplayEncounterObservationTransitionPayload(
                        transition.ActorId,
                        behavior,
                        transition.Observation));
                return;
            }
            RequireLegacyMutationAllowed(nameof(CommitAwarenessTransition));
            EnemyAwarenessSnapshot current = encounterState.GetAwareness(
                transition.ActorId);
            if (transition.Sequence != encounterState.LastTransitionSequence + 1L
                || !AwarenessMatches(current, transition.Previous))
            {
                throw new InvalidOperationException(
                    "Awareness transition no longer begins at authoritative state.");
            }
            EnemyAwarenessTransitionRecord expected = PrepareAwarenessTransition(
                transition.ActorId,
                transition.Observation);
            if (!AwarenessMatches(expected.Resulting, transition.Resulting))
            {
                throw new InvalidOperationException(
                    "Awareness transition does not match the authored sensing policy.");
            }

            encounterState = encounterState.WithAwareness(
                transition.Resulting,
                transition.Sequence);
            Journal.RecordEnemyAwareness(transition);
            MarkStateChanged();
            EnemyAwarenessChanged?.Invoke(transition);
        }

        public PatrolAdvanceRecord PreparePatrolAdvance(
            string actorId,
            MovementRouteRecord route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (Mode != GameplaySessionMode.Exploration || EncounterActive)
            {
                throw new InvalidOperationException(
                    "Patrol advances only while continuous exploration owns time.");
            }
            ScenarioActorDefinition definition = RequireActorDefinition(actorId);
            PatrolRouteDefinition patrol = definition.Combat.EnemyBehavior
                ?.PatrolRoute;
            if (patrol == null)
                throw new InvalidOperationException(
                    $"Actor '{actorId}' does not author a patrol route.");
            EnemyAwarenessSnapshot awareness = encounterState.GetAwareness(actorId);
            if (awareness.State != EncounterAwarenessState.Unaware)
            {
                throw new InvalidOperationException(
                    "Only unaware actors can advance patrol routes.");
            }
            GameplayActorState actor = RequireActor(actorId);
            if (!PosesMatch(actor.Pose, route.OriginPose))
            {
                throw new InvalidOperationException(
                    "Patrol route no longer begins at the authoritative pose.");
            }
            int nextIndex = patrol.GetNextWaypointIndex(
                awareness.PatrolWaypointIndex);
            if (nextIndex == awareness.PatrolWaypointIndex)
            {
                throw new InvalidOperationException(
                    "The patrol has reached a non-looping terminal waypoint.");
            }
            if (route.Destination.DistanceTo(patrol.GetWaypoint(nextIndex))
                > 0.001f)
            {
                throw new InvalidOperationException(
                    "Patrol route must end at its authored next waypoint.");
            }
            return new PatrolAdvanceRecord(
                checked(encounterState.LastTransitionSequence + 1L),
                actorId,
                route,
                awareness.PatrolWaypointIndex,
                nextIndex);
        }

        public void CommitPatrolAdvance(PatrolAdvanceRecord advance)
        {
            if (advance == null) throw new ArgumentNullException(nameof(advance));
            if (IsCanonicalProjectionBound)
            {
                EnemyBehaviorDefinition behavior = Scenario.GetActor(
                    advance.ActorId).Combat.EnemyBehavior
                    ?? throw new InvalidOperationException(
                        $"Actor '{advance.ActorId}' does not author patrol behavior.");
                ExecuteCanonical(new GameplayPatrolTransitionPayload(
                    advance.ActorId,
                    behavior,
                    advance));
                return;
            }
            RequireLegacyMutationAllowed(nameof(CommitPatrolAdvance));
            if (advance.Sequence != encounterState.LastTransitionSequence + 1L)
            {
                throw new InvalidOperationException(
                    "Patrol advances must commit in encounter sequence.");
            }
            PatrolAdvanceRecord expected = PreparePatrolAdvance(
                advance.ActorId,
                advance.Route);
            if (expected.PreviousWaypointIndex != advance.PreviousWaypointIndex
                || expected.ResultingWaypointIndex != advance.ResultingWaypointIndex)
            {
                throw new InvalidOperationException(
                    "Patrol advance does not match its authored route.");
            }
            EnemyAwarenessSnapshot awareness = encounterState.GetAwareness(
                advance.ActorId);
            EnemyAwarenessSnapshot resultingAwareness = new EnemyAwarenessSnapshot(
                advance.ActorId,
                awareness.State,
                awareness.Suspicion,
                string.IsNullOrEmpty(awareness.LastKnownHostileId)
                    ? null
                    : awareness.LastKnownHostileId,
                awareness.LastKnownHostilePosition,
                advance.ResultingWaypointIndex);
            GameplayActorState actor = RequireActor(advance.ActorId);
            actor.Pose = new GameplayActorPose(
                advance.Route.Destination,
                advance.Route.FinalFacingDegrees,
                actor.Pose.Stance);
            encounterState = encounterState.WithAwareness(
                resultingAwareness,
                advance.Sequence);
            Journal.RecordPatrolAdvance(advance);
            MarkStateChanged();
            PatrolAdvanced?.Invoke(advance);
        }

        private IReadOnlyList<string> NormalizeEncounterScope(
            IEnumerable<string> participantIds)
        {
            if (participantIds == null)
                throw new ArgumentNullException(nameof(participantIds));
            var requested = new HashSet<string>(StringComparer.Ordinal);
            foreach (string actorId in participantIds)
            {
                RequireActor(actorId);
                if (!requested.Add(actorId))
                {
                    throw new ArgumentException(
                        "Encounter participants must be unique.",
                        nameof(participantIds));
                }
            }
            if (requested.Count == 0)
            {
                throw new ArgumentException(
                    "An encounter requires at least one participant.",
                    nameof(participantIds));
            }
            return OrderByInitiative(requested);
        }

        private IReadOnlyList<string> OrderByInitiative(
            ISet<string> actorIds)
        {
            var result = new List<string>();
            foreach (string actorId in allInitiativeOrder)
                if (actorIds.Contains(actorId))
                    result.Add(actorId);
            if (result.Count != actorIds.Count)
            {
                throw new InvalidOperationException(
                    "Encounter scope contains an actor absent from initiative.");
            }
            return result.AsReadOnly();
        }

        private void ReplaceInitiativeScope(IEnumerable<string> actorIds)
        {
            initiativeOrder.Clear();
            initiativeOrder.AddRange(actorIds);
        }

        private static bool AwarenessMatches(
            EnemyAwarenessSnapshot left,
            EnemyAwarenessSnapshot right)
        {
            if (left == null || right == null)
                return ReferenceEquals(left, right);
            return string.Equals(left.ActorId, right.ActorId,
                       StringComparison.Ordinal)
                && left.State == right.State
                && left.Suspicion == right.Suspicion
                && string.Equals(
                    left.LastKnownHostileId,
                    right.LastKnownHostileId,
                    StringComparison.Ordinal)
                && Nullable.Equals(
                    left.LastKnownHostilePosition,
                    right.LastKnownHostilePosition)
                && left.PatrolWaypointIndex == right.PatrolWaypointIndex;
        }

        private static bool PositionsMatch(
            GameplayPosition left,
            GameplayPosition right) => left.X == right.X
                && left.Y == right.Y
                && left.Z == right.Z;
    }
}
