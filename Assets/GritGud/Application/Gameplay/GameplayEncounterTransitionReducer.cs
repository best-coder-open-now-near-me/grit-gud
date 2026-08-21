using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayEncounterObservationTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayEncounterObservationTransitionPayload(
            string actorId,
            EnemyBehaviorDefinition behavior,
            EncounterObservation observation)
            : base(GameplayCapabilityProfiles.ObserveEncounter(), actorId, actorId)
        {
            Behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
            Observation = observation ?? throw new ArgumentNullException(
                nameof(observation));
            if (!string.Equals(actorId, observation.ObserverId,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Encounter observation identity must match its observer.",
                    nameof(observation));
            }
        }

        public EnemyBehaviorDefinition Behavior { get; }

        public EncounterObservation Observation { get; }
    }

    public sealed class GameplayPatrolTransitionPayload : GameplayTransitionPayload
    {
        public GameplayPatrolTransitionPayload(
            string actorId,
            EnemyBehaviorDefinition behavior,
            PatrolAdvanceRecord advance)
            : base(GameplayCapabilityProfiles.Patrol(), actorId, actorId)
        {
            Behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
            Advance = advance ?? throw new ArgumentNullException(nameof(advance));
            if (!string.Equals(actorId, advance.ActorId,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Patrol transition identity must match its advancing actor.",
                    nameof(advance));
            }
        }

        public EnemyBehaviorDefinition Behavior { get; }

        public PatrolAdvanceRecord Advance { get; }
    }

    public sealed class GameplayEncounterTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && (profile.Equals(GameplayCapabilityProfiles.ObserveEncounter())
                || profile.Equals(GameplayCapabilityProfiles.Patrol()));

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition == null) throw new ArgumentNullException(nameof(transition));
            var mutation = new GameplayCanonicalStateMutation(state);
            object record;
            if (transition.Payload
                is GameplayEncounterObservationTransitionPayload observation)
            {
                record = ReduceObservation(state, mutation, observation);
            }
            else if (transition.Payload is GameplayPatrolTransitionPayload patrol)
            {
                record = ReducePatrol(state, mutation, patrol);
            }
            else
            {
                throw new ArgumentException(
                    "Encounter transition payload is unsupported.",
                    nameof(transition));
            }
            mutation.LastTransitionSequence = transition.Identity.Sequence;
            GameplayCombatStateSnapshot resulting = mutation.Build();
            return new GameplayReductionResult(
                state,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        transition.Payload.SubjectId,
                        record),
                });
        }

        private static EnemyAwarenessTransitionRecord ReduceObservation(
            GameplayCombatStateSnapshot state,
            GameplayCanonicalStateMutation mutation,
            GameplayEncounterObservationTransitionPayload payload)
        {
            EnemyAwarenessSnapshot previous = state.Session.EncounterState
                .GetAwareness(payload.ActorId);
            if (payload.Observation.Sight != null)
            {
                GameplayActorSnapshot target = state.Session.GetActor(
                    payload.Observation.Sight.TargetId);
                if (!PositionsMatch(
                        target.Pose.Position,
                        payload.Observation.SightTargetPosition.Value))
                {
                    throw new InvalidOperationException(
                        "Sight evidence no longer describes the canonical target pose.");
                }
            }
            EnemyAwarenessSnapshot resulting = EncounterAwarenessRules.Evaluate(
                payload.Behavior,
                state.Session.GetActor(payload.ActorId).Pose,
                previous,
                payload.Observation);
            long sequence = checked(state.Session.EncounterState
                .LastTransitionSequence + 1L);
            var record = new EnemyAwarenessTransitionRecord(
                sequence,
                payload.ActorId,
                previous,
                resulting,
                payload.Observation);
            mutation.EncounterState = state.Session.EncounterState
                .WithAwareness(resulting, sequence);
            mutation.JournalSequence = checked(mutation.JournalSequence + 1L);
            mutation.Revision = checked(mutation.Revision + 1L);
            return record;
        }

        private static PatrolAdvanceRecord ReducePatrol(
            GameplayCombatStateSnapshot state,
            GameplayCanonicalStateMutation mutation,
            GameplayPatrolTransitionPayload payload)
        {
            if (state.Session.Mode != GameplaySessionMode.Exploration
                || state.Session.EncounterActive)
            {
                throw new InvalidOperationException(
                    "Patrol advances only while continuous exploration owns time.");
            }
            PatrolRouteDefinition patrol = payload.Behavior.PatrolRoute
                ?? throw new InvalidOperationException(
                    "Patrol transitions require an authored patrol route.");
            EnemyAwarenessSnapshot awareness = state.Session.EncounterState
                .GetAwareness(payload.ActorId);
            if (awareness.State != EncounterAwarenessState.Unaware)
            {
                throw new InvalidOperationException(
                    "Only unaware actors may advance an authored patrol.");
            }
            PatrolAdvanceRecord record = payload.Advance;
            long sequence = checked(state.Session.EncounterState
                .LastTransitionSequence + 1L);
            if (record.Sequence != sequence
                || record.PreviousWaypointIndex != awareness.PatrolWaypointIndex)
            {
                throw new InvalidOperationException(
                    "Patrol advance sequence or waypoint does not match canonical state.");
            }
            GameplayActorSnapshot actor = state.Session.GetActor(payload.ActorId);
            if (!PosesMatch(actor.Pose, record.Route.OriginPose))
            {
                throw new InvalidOperationException(
                    "Patrol route no longer begins at the canonical actor pose.");
            }
            int expectedIndex = patrol.GetNextWaypointIndex(
                awareness.PatrolWaypointIndex);
            if (expectedIndex == awareness.PatrolWaypointIndex
                || record.ResultingWaypointIndex != expectedIndex
                || !PositionsMatch(
                    record.Route.Destination,
                    patrol.GetWaypoint(expectedIndex)))
            {
                throw new InvalidOperationException(
                    "Patrol route does not end at the authored next waypoint.");
            }
            EnemyAwarenessSnapshot resultingAwareness = new EnemyAwarenessSnapshot(
                payload.ActorId,
                awareness.State,
                awareness.Suspicion,
                string.IsNullOrEmpty(awareness.LastKnownHostileId)
                    ? null
                    : awareness.LastKnownHostileId,
                awareness.LastKnownHostilePosition,
                expectedIndex);
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                pose: new GameplayActorPose(
                    record.Route.Destination,
                    record.Route.FinalFacingDegrees,
                    actor.Pose.Stance)));
            mutation.EncounterState = state.Session.EncounterState
                .WithAwareness(resultingAwareness, sequence);
            mutation.JournalSequence = checked(mutation.JournalSequence + 1L);
            mutation.Revision = checked(mutation.Revision + 1L);
            return record;
        }

        private static bool PosesMatch(
            GameplayActorPose left,
            GameplayActorPose right) =>
            PositionsMatch(left.Position, right.Position)
            && left.FacingDegrees == right.FacingDegrees
            && left.Stance == right.Stance;

        private static bool PositionsMatch(
            GameplayPosition left,
            GameplayPosition right) =>
            left.X == right.X && left.Y == right.Y && left.Z == right.Z;
    }
}
