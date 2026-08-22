using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayDroneExpiredEvent : GameplayDomainEvent
    {
        public GameplayDroneExpiredEvent(
            GameplayTransitionIdentity transition,
            ExpireDroneRecord expiration)
            : base(
                transition,
                "drone-expired",
                (expiration ?? throw new ArgumentNullException(
                    nameof(expiration))).DroneId)
        {
            Expiration = expiration;
        }

        public ExpireDroneRecord Expiration { get; }
    }

    public sealed class GameplaySummonDroneTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplaySummonDroneTransitionPayload(
            SummonDroneRecord summon,
            string subjectId = null)
            : base(
                GameplayCapabilityProfiles.SummonDrone(),
                (summon ?? throw new ArgumentNullException(nameof(summon)))
                    .SummonerActorId,
                subjectId ?? summon.Ability.AbilityId)
        {
            Summon = summon;
        }

        public SummonDroneRecord Summon { get; }
    }

    public sealed class GameplayDismissDroneTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayDismissDroneTransitionPayload(DismissDroneRecord dismiss)
            : base(
                GameplayCapabilityProfiles.DismissDrone(),
                (dismiss ?? throw new ArgumentNullException(nameof(dismiss)))
                    .SummonerActorId,
                dismiss.DroneId)
        {
            Dismiss = dismiss;
        }

        public DismissDroneRecord Dismiss { get; }
    }

    public sealed class GameplayDroneCrashImpactTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayDroneCrashImpactTransitionPayload(
            string advancingActorId,
            DroneCrashImpactRecord impact)
            : base(
                GameplayCapabilityProfiles.AdvanceDroneCrash(),
                advancingActorId,
                (impact ?? throw new ArgumentNullException(nameof(impact)))
                    .DroneId)
        {
            Impact = impact;
        }

        public DroneCrashImpactRecord Impact { get; }
    }

    public sealed class GameplayDroneLifecycleTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && (profile.Equals(GameplayCapabilityProfiles.SummonDrone())
                || profile.Equals(GameplayCapabilityProfiles.DismissDrone())
                || profile.Equals(
                    GameplayCapabilityProfiles.AdvanceDroneCrash()));

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            if (!Supports(transition.Profile))
                throw new NotSupportedException(
                    "Drone lifecycle reducer requires an exact lifecycle profile.");
            return transition.Payload switch
            {
                GameplaySummonDroneTransitionPayload summon =>
                    ReduceSummon(state, transition, summon.Summon),
                GameplayDismissDroneTransitionPayload dismiss =>
                    ReduceDismiss(state, transition, dismiss.Dismiss),
                GameplayDroneCrashImpactTransitionPayload crash =>
                    ReduceCrash(state, transition, crash.Impact),
                _ => throw new NotSupportedException(
                    "Drone lifecycle payload is not supported."),
            };
        }

        private static GameplayReductionResult ReduceSummon(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            SummonDroneRecord summon)
        {
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            GameplaySessionStateSnapshot session = state.Session;
            GameplayActorSnapshot summoner = session.GetActor(
                summon.SummonerActorId);
            if (summon.Sequence != transition.Identity.Sequence
                || !string.Equals(
                    summon.DroneInstanceId,
                    SummonDroneRecord.CreateInstanceId(
                        summon.SummonerActorId,
                        transition.Identity.Sequence),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Drone summon identity is not derived from its transition sequence.");
            RequireCommandableTurn(session, summoner);
            if (!BudgetsMatch(summoner.TurnBudget, summon.PreviousBudget))
                throw new InvalidOperationException(
                    "Drone summon was prepared against a stale shared budget.");
            if (summoner.Pose.Position.DistanceTo(summon.SpawnPosition)
                > summon.Ability.MaximumSpawnDistance)
                throw new InvalidOperationException(
                    "Drone summon position exceeds its authored range.");
            int active = 0;
            foreach (SummonedDroneSnapshot drone in state.Drones)
            {
                if (string.Equals(
                        drone.DroneId,
                        summon.DroneInstanceId,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Deterministic summoned instance identity already exists.");
                if (string.Equals(
                        drone.SummonerActorId,
                        summon.SummonerActorId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        drone.SummonAbilityId,
                        summon.Ability.AbilityId,
                        StringComparison.Ordinal)
                    && (drone.Lifecycle == SummonLifecycleState.Active
                        || drone.Lifecycle == SummonLifecycleState.Crashing))
                    active++;
            }
            if (active >= summon.Ability.MaximumActiveInstances)
                throw new InvalidOperationException(
                    "Drone summon has reached its active-instance limit.");

            var mutation = BeginMutation(state, transition);
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                summoner,
                budget: summon.ResultingBudget));
            mutation.AddDrone(summon.Resulting);
            return Result(state, mutation.Build(), transition, summon);
        }

        private static GameplayReductionResult ReduceDismiss(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            DismissDroneRecord dismiss)
        {
            state.RequireCoverage(GameplayCombatStateCoverage.Drones);
            GameplaySessionStateSnapshot session = state.Session;
            GameplayActorSnapshot summoner = session.GetActor(
                dismiss.SummonerActorId);
            RequireCommandableTurn(session, summoner);
            SummonedDroneSnapshot current = FindDrone(
                state,
                dismiss.DroneId);
            if (dismiss.Sequence != transition.Identity.Sequence
                || !StatesMatch(current, dismiss.Previous)
                || !BudgetsMatch(
                    summoner.TurnBudget,
                    dismiss.PreviousBudget))
                throw new InvalidOperationException(
                    "Drone dismissal was prepared from stale canonical state.");
            var mutation = BeginMutation(state, transition);
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                summoner,
                budget: dismiss.ResultingBudget));
            mutation.ReplaceDrone(dismiss.Resulting);
            return Result(state, mutation.Build(), transition, dismiss);
        }

        private static GameplayReductionResult ReduceCrash(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            DroneCrashImpactRecord impact)
        {
            state.RequireCoverage(
                GameplayCombatStateCoverage.Drones
                | GameplayCombatStateCoverage.Destructibles);
            SummonedDroneSnapshot current = FindDrone(state, impact.DroneId);
            if (!StatesMatch(current, impact.Previous)
                || impact.Sequence != transition.Identity.Sequence)
                throw new InvalidOperationException(
                    "Drone crash impact was prepared from stale canonical state.");
            var mutation = BeginMutation(state, transition);
            GameplayBlastProjectionCounts counts = GameplayBlastStateProjector
                .Apply(
                    mutation,
                    impact.Effects,
                    impact.Definition.InjuryMovementPenalty,
                    impact.Definition.DestructibleIntegrityDamage,
                    impact.SummonerActorId,
                    impact.DroneId + ".crash",
                    impact.Sequence);
            int concussed = GameplayBlastStateProjector
                .ApplyConcussiveEffects(
                    mutation,
                    impact.ConcussiveEffects);
            mutation.ReplaceDrone(impact.Resulting);
            mutation.JournalSequence = checked(
                mutation.JournalSequence + counts.DestructibleDamages);
            mutation.Revision = checked(
                mutation.Revision + counts.ActorInjuries + concussed);
            return Result(state, mutation.Build(), transition, impact);
        }

        private static GameplayCanonicalStateMutation BeginMutation(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition) =>
            new GameplayCanonicalStateMutation(state)
            {
                JournalSequence = checked(
                    state.Session.JournalSequence + 1L),
                Revision = checked(state.Session.Revision + 1L),
                LastTransitionSequence = transition.Identity.Sequence,
            };

        private static void RequireCommandableTurn(
            GameplaySessionStateSnapshot session,
            GameplayActorSnapshot summoner)
        {
            if (session.Mode != GameplaySessionMode.TurnBased
                || session.Operation != GameplaySessionOperation.None
                || !string.Equals(
                    session.ActiveActorId,
                    summoner.ActorId,
                    StringComparison.Ordinal)
                || summoner.IsIncapacitated)
                throw new InvalidOperationException(
                    "Drone lifecycle commands require the capable summoner's idle turn.");
        }

        private static SummonedDroneSnapshot FindDrone(
            GameplayCombatStateSnapshot state,
            string droneId)
        {
            foreach (SummonedDroneSnapshot drone in state.Drones)
                if (string.Equals(
                        drone.DroneId,
                        droneId,
                        StringComparison.Ordinal))
                    return drone;
            throw new InvalidOperationException(
                $"Drone '{droneId}' is absent from canonical state.");
        }

        private static bool StatesMatch(
            SummonedDroneSnapshot left,
            SummonedDroneSnapshot right) => string.Equals(
                GameplayCanonicalValueDigest.Calculate(left),
                GameplayCanonicalValueDigest.Calculate(right),
                StringComparison.Ordinal);

        private static bool BudgetsMatch(
            GritGud.Domain.Turns.TurnBudget left,
            GritGud.Domain.Turns.TurnBudget right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity;

        private static GameplayReductionResult Result(
            GameplayCombatStateSnapshot previous,
            GameplayCombatStateSnapshot resulting,
            GameplaySemanticTransition transition,
            object record) => new GameplayReductionResult(
                previous,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        transition.Payload.SubjectId,
                        record),
                });
    }
}
