using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayDroneSummonIntent
    {
        public GameplayDroneSummonIntent(
            string abilityId,
            GameplayPosition position,
            float facingDegrees)
        {
            AbilityId = GameplayContentIdentity.RequireText(
                abilityId,
                nameof(abilityId));
            GameplayNumericPolicy.RequireFinite(
                facingDegrees,
                nameof(facingDegrees));
            Position = position;
            FacingDegrees = facingDegrees;
        }

        public string AbilityId { get; }
        public GameplayPosition Position { get; }
        public float FacingDegrees { get; }
    }

    public sealed class GameplayDroneDismissIntent
    {
        public GameplayDroneDismissIntent(string droneId)
        {
            DroneId = GameplayContentIdentity.RequireText(
                droneId,
                nameof(droneId));
        }

        public string DroneId { get; }
    }

    public sealed class GameplayDroneCrashIntent
    {
        public GameplayDroneCrashIntent(string droneId)
        {
            DroneId = GameplayContentIdentity.RequireText(
                droneId,
                nameof(droneId));
        }

        public string DroneId { get; }
    }

    public sealed class GameplayDroneSummonCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "drone-summon.v1";

        private readonly GameplayScenarioAssembly assembly;
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayDroneSummonCandidateExecutionRoute(
            GameplayScenarioAssembly scenarioAssembly,
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            assembly = scenarioAssembly ?? throw new ArgumentNullException(
                nameof(scenarioAssembly));
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.SummonDrone());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            Require(context, candidate, Supports);
            if (candidate.Intent is not GameplayDroneSummonIntent intent)
                throw new ArgumentException(
                    "Drone summon candidate has no summon intent.",
                    nameof(candidate));
            ScenarioDroneSummonRuntimeDefinition runtime;
            try
            {
                runtime = assembly.GetDroneSummonAbility(intent.AbilityId);
            }
            catch (KeyNotFoundException)
            {
                return Illegal(context, candidate, "summon-ability-not-found");
            }
            if (!string.Equals(
                    runtime.SummonerActorId,
                    candidate.ActorId,
                    StringComparison.Ordinal))
                return Illegal(context, candidate, "summoner-mismatch");
            GameplayActorSnapshot summoner = context.State.Session.GetActor(
                candidate.ActorId);
            string readiness = CommandFailure(context.State.Session, summoner);
            if (readiness.Length > 0)
                return Illegal(context, candidate, readiness);
            DroneSummonAbilityDefinition ability = runtime.Ability;
            if (summoner.TurnBudget.ActionPoints
                    < ability.SummonCost.ActionPoints
                || summoner.TurnBudget.MovementOpportunity
                    < ability.SummonCost.MovementOpportunity)
                return Illegal(context, candidate, "insufficient-budget");
            if (summoner.Pose.Position.DistanceTo(intent.Position)
                > ability.MaximumSpawnDistance)
                return Illegal(context, candidate, "spawn-out-of-range");
            if (spatial.BlocksPath(
                    context.State,
                    summoner.Pose.Position,
                    intent.Position,
                    clearanceRadius: 0.25f))
                return Illegal(context, candidate, "spawn-path-blocked");
            int active = 0;
            foreach (SummonedDroneSnapshot drone in context.State.Drones)
                if (string.Equals(
                        drone.SummonerActorId,
                        summoner.ActorId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        drone.SummonAbilityId,
                        ability.AbilityId,
                        StringComparison.Ordinal)
                    && (drone.Lifecycle == SummonLifecycleState.Active
                        || drone.Lifecycle == SummonLifecycleState.Crashing))
                    active++;
            if (active >= ability.MaximumActiveInstances)
                return Illegal(context, candidate, "active-instance-limit");
            long sequence = checked(
                context.State.Session.LastTransitionSequence + 1L);
            DroneArchetypeDefinition archetype = assembly.GetDroneArchetype(
                ability.DroneArchetypeId);
            var record = new SummonDroneRecord(
                sequence,
                summoner.ActorId,
                ability,
                archetype,
                intent.Position,
                intent.FacingDegrees,
                summoner.TurnBudget,
                summoner.TurnBudget.SpendAction(ability.SummonCost));
            return Legal(
                context,
                candidate,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "drone.summoned",
                        1f),
                    new GameplayCandidateOutcomeFeature(
                        "cost.action-points",
                        ability.SummonCost.ActionPoints),
                }),
                new[]
                {
                    spatial.CaptureEvidence(
                        "drone-summon-position",
                        context.State,
                        summoner.Pose.Position,
                        intent.Position),
                },
                record);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplaySummonDroneTransitionPayload(
                evaluation?.FrozenPreparation as SummonDroneRecord
                    ?? throw new ArgumentException(
                        "Drone summon preparation is missing.",
                        nameof(evaluation)),
                evaluation.Candidate.SubjectId);

        private static string CommandFailure(
            GameplaySessionStateSnapshot session,
            GameplayActorSnapshot summoner)
        {
            if (session.Mode != GameplaySessionMode.TurnBased)
                return "turn-mode-required";
            if (session.Operation != GameplaySessionOperation.None)
                return "operation-in-progress";
            if (!string.Equals(
                    session.ActiveActorId,
                    summoner.ActorId,
                    StringComparison.Ordinal))
                return "summoner-not-active";
            return summoner.IsIncapacitated
                ? "summoner-incapacitated"
                : string.Empty;
        }

        internal static void Require(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            Func<GameplayCapabilityProfile, bool> supports)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (!supports(candidate.Profile)
                || !string.Equals(
                    context.ActorId,
                    candidate.ActorId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Candidate does not match its drone lifecycle route.",
                    nameof(candidate));
        }

        internal static GameplayExecutableCandidateEvaluation Illegal(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            string failure) => new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                false,
                failure,
                new GameplayCandidateOutcomeEstimate(
                    Array.Empty<GameplayCandidateOutcomeFeature>()),
                Array.Empty<GameplayEvidenceRecord>(),
                null);

        internal static GameplayExecutableCandidateEvaluation Legal(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            GameplayCandidateOutcomeEstimate estimate,
            IEnumerable<GameplayEvidenceRecord> evidence,
            object preparation) => new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                true,
                string.Empty,
                estimate,
                evidence,
                preparation);
    }

    public sealed class GameplayDroneDismissCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "drone-dismiss.v1";
        private static readonly ActionCost Cost = new ActionCost(
            0,
            0f,
            ActionMobility.Mobile);

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(GameplayCapabilityProfiles.DismissDrone());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayDroneSummonCandidateExecutionRoute.Require(
                context,
                candidate,
                Supports);
            SummonedDroneSnapshot drone;
            try
            {
                drone = FindDrone(context.State, candidate.SubjectId);
            }
            catch (KeyNotFoundException)
            {
                return Illegal(context, candidate, "drone-not-found");
            }
            GameplayActorSnapshot summoner = context.State.Session.GetActor(
                candidate.ActorId);
            if (!drone.IsOperational
                || !string.Equals(
                    drone.SummonerActorId,
                    summoner.ActorId,
                    StringComparison.Ordinal))
                return Illegal(context, candidate, "drone-not-commandable");
            if (context.State.Session.Mode != GameplaySessionMode.TurnBased
                || context.State.Session.Operation
                    != GameplaySessionOperation.None
                || !string.Equals(
                    context.State.Session.ActiveActorId,
                    summoner.ActorId,
                    StringComparison.Ordinal)
                || summoner.IsIncapacitated)
                return Illegal(context, candidate, "summoner-not-commandable");
            SummonedDroneSnapshot dismissed = drone.WithLifecycle(
                SummonLifecycleState.Dismissed,
                drone.RemainingIntegrity,
                drone.RemainingDurationTurns);
            var record = new DismissDroneRecord(
                checked(context.State.Session.LastTransitionSequence + 1L),
                summoner.ActorId,
                Cost,
                summoner.TurnBudget,
                summoner.TurnBudget.SpendAction(Cost),
                drone,
                dismissed);
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                true,
                string.Empty,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "drone.dismissed",
                        1f),
                }),
                Array.Empty<GameplayEvidenceRecord>(),
                record);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayDismissDroneTransitionPayload(
                evaluation?.FrozenPreparation as DismissDroneRecord
                    ?? throw new ArgumentException(
                        "Drone dismissal preparation is missing.",
                        nameof(evaluation)));

        private static GameplayExecutableCandidateEvaluation Illegal(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            string failure) => new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                false,
                failure,
                new GameplayCandidateOutcomeEstimate(
                    Array.Empty<GameplayCandidateOutcomeFeature>()),
                Array.Empty<GameplayEvidenceRecord>(),
                null);

        internal static SummonedDroneSnapshot FindDrone(
            GameplayCombatStateSnapshot state,
            string droneId)
        {
            foreach (SummonedDroneSnapshot drone in state.Drones)
                if (string.Equals(
                        drone.DroneId,
                        droneId,
                        StringComparison.Ordinal))
                    return drone;
            throw new KeyNotFoundException(
                $"Drone '{droneId}' is absent from canonical state.");
        }
    }

    public sealed class GameplayDroneCrashCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "drone-crash-impact.v1";
        private readonly GameplayHeadlessSpatialEvidence spatial;

        public GameplayDroneCrashCandidateExecutionRoute(
            GameplayHeadlessSpatialEvidence spatialEvidence)
        {
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && profile.Equals(
                GameplayCapabilityProfiles.AdvanceDroneCrash());

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayDroneSummonCandidateExecutionRoute.Require(
                context,
                candidate,
                Supports);
            SummonedDroneSnapshot drone;
            try
            {
                drone = GameplayDroneDismissCandidateExecutionRoute.FindDrone(
                    context.State,
                    candidate.SubjectId);
            }
            catch (KeyNotFoundException)
            {
                return Illegal(context, candidate, "drone-not-found");
            }
            if (drone.Lifecycle != SummonLifecycleState.Crashing
                || drone.CrashTrajectory == null)
                return Illegal(context, candidate, "drone-not-crashing");
            DroneCrashDefinition definition = drone.Definition.Crash;
            IReadOnlyList<BlastEffectRecord> effects = spatial
                .CaptureBlastEffects(
                    context.State,
                    drone.CrashTrajectory.ImpactPosition,
                    definition.ImpactRadius);
            IReadOnlyList<ConcussiveActionPointEffectRecord> concussive =
                ResolveConcussive(
                    context.State,
                    effects,
                    definition.MaximumActionPointReduction);
            SummonedDroneSnapshot destroyed = drone.WithLifecycle(
                SummonLifecycleState.Destroyed,
                0f,
                drone.RemainingDurationTurns,
                drone.CrashTrajectory,
                drone.CrashTrajectory.ImpactPosition);
            long sequence = checked(
                context.State.Session.LastTransitionSequence + 1L);
            var record = new DroneCrashImpactRecord(
                sequence,
                drone,
                destroyed,
                definition,
                effects,
                concussive);
            return new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                true,
                string.Empty,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "lifecycle.mandatory",
                        1f),
                    new GameplayCandidateOutcomeFeature(
                        "drone.crash-impact",
                        1f),
                }),
                new[]
                {
                    spatial.CaptureEvidence(
                        "drone-crash-impact",
                        context.State,
                        drone.Position,
                        drone.CrashTrajectory.ImpactPosition),
                },
                record);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            new GameplayDroneCrashImpactTransitionPayload(
                context.ActorId,
                evaluation?.FrozenPreparation as DroneCrashImpactRecord
                    ?? throw new ArgumentException(
                        "Drone crash preparation is missing.",
                        nameof(evaluation)));

        private static IReadOnlyList<ConcussiveActionPointEffectRecord>
            ResolveConcussive(
                GameplayCombatStateSnapshot state,
                IEnumerable<BlastEffectRecord> effects,
                int maximumReduction)
        {
            var result = new List<ConcussiveActionPointEffectRecord>();
            if (maximumReduction <= 0) return result.AsReadOnly();
            foreach (BlastEffectRecord effect in effects)
            {
                if (effect.SubjectKind != BlastSubjectKind.Actor
                    || effect.Exposure <= 0f)
                    continue;
                GameplayActorSnapshot actor = state.Session.GetActor(
                    effect.EntityId);
                int requested = ConcussiveActionPointRules
                    .RequestedReduction(maximumReduction, effect.Exposure);
                int removed = Math.Min(
                    actor.TurnBudget.ActionPoints,
                    requested);
                result.Add(new ConcussiveActionPointEffectRecord(
                    actor.ActorId,
                    actor.TurnBudget.ActionPoints,
                    requested,
                    removed,
                    actor.TurnBudget.ActionPoints - removed));
            }
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ActorId,
                right.ActorId));
            return result.AsReadOnly();
        }

        private static GameplayExecutableCandidateEvaluation Illegal(
            GameplayDecisionContext context,
            GameplayCandidate candidate,
            string failure) => new GameplayExecutableCandidateEvaluation(
                Id,
                candidate,
                context.State.CanonicalHash,
                false,
                failure,
                new GameplayCandidateOutcomeEstimate(
                    Array.Empty<GameplayCandidateOutcomeFeature>()),
                Array.Empty<GameplayEvidenceRecord>(),
                null);
    }
}
