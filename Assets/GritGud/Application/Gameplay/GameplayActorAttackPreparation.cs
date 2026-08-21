using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Frozen, policy-neutral evaluation of an actor-to-actor attack. Building
    /// this value never samples randomness; only Resolve may address the run's
    /// random stream after a caller has selected the candidate.
    /// </summary>
    public sealed class GameplayActorAttackEvaluation
    {
        internal GameplayActorAttackEvaluation(
            GameplayCombatStateSnapshot state,
            GameplayCapabilityProfile profile,
            AttackDefinition attack,
            GameplayActorSnapshot attacker,
            GameplayActorSnapshot target,
            ActionCost cost,
            TargetExposureSnapshot exposure,
            ResolvedTacticalContext context)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));
            Attacker = attacker;
            Target = target;
            Cost = cost;
            Exposure = exposure ?? throw new ArgumentNullException(
                nameof(exposure));
            Context = context;
            Distance = attacker.Pose.Position.DistanceTo(target.Pose.Position);
            FinalHitChancePercent =
                AttackHitChanceRules.CalculateFinalHitChancePercent(
                    exposure,
                    attack.AccuracyDecay,
                    Distance,
                    context?.AccuracyDeltaPercent ?? 0);
            Evidence = CreateEvidence(state, exposure, context);
        }

        public GameplayCombatStateSnapshot State { get; }
        public string StateHash => State.CanonicalHash;
        public GameplayCapabilityProfile Profile { get; }
        public AttackDefinition Attack { get; }
        public GameplayActorSnapshot Attacker { get; }
        public GameplayActorSnapshot Target { get; }
        public ActionCost Cost { get; }
        public TargetExposureSnapshot Exposure { get; }
        public ResolvedTacticalContext Context { get; }
        public float Distance { get; }
        public int FinalHitChancePercent { get; }
        public IReadOnlyList<GameplayEvidenceRecord> Evidence { get; }

        private static IReadOnlyList<GameplayEvidenceRecord> CreateEvidence(
            GameplayCombatStateSnapshot state,
            TargetExposureSnapshot exposure,
            ResolvedTacticalContext context)
        {
            var result = new List<GameplayEvidenceRecord>
            {
                new GameplayEvidenceRecord(
                    "target-exposure",
                    state.Session.Revision,
                    GameplayCanonicalValueDigest.Calculate(exposure)),
            };
            if (context != null)
                result.Add(new GameplayEvidenceRecord(
                    "tactical-context",
                    state.Session.Revision,
                    GameplayCanonicalValueDigest.Calculate(context)));
            return result.AsReadOnly();
        }
    }

    public static class GameplayCanonicalValueDigest
    {
        public static string Calculate(object value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            string canonical = GameplayReproBundleFormatter
                .FormatCanonicalValue(value);
            return CalculateCanonicalJson(canonical);
        }

        internal static string CalculateSerializableFields(object value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            string canonical = GameplayReproBundleFormatter
                .FormatCanonicalSerializableFields(value);
            return CalculateCanonicalJson(canonical);
        }

        internal static string CalculateCanonicalJson(string canonicalJson)
        {
            if (string.IsNullOrWhiteSpace(canonicalJson))
                throw new ArgumentException(
                    "Canonical JSON cannot be empty.",
                    nameof(canonicalJson));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(canonicalJson));
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte item in hash) text.Append(item.ToString("x2"));
                return text.ToString();
            }
        }
    }

    /// <summary>
    /// The single actor-attack rules adapter used by both mutable live-session
    /// installation and immutable semantic simulation.
    /// </summary>
    public sealed class GameplayActorAttackTransitionPreparer
    {
        private readonly ScenarioDefinition scenario;
        private readonly IGameplayTacticalContextQuery contextQuery;
        private readonly GameplayTacticalContextEvaluator contextEvaluator;

        public GameplayActorAttackTransitionPreparer(
            ScenarioDefinition scenarioDefinition,
            IGameplayTacticalContextQuery tacticalContextQuery = null,
            GameplayTacticalContextEvaluator tacticalContextEvaluator = null)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
            if ((tacticalContextQuery == null)
                != (tacticalContextEvaluator == null))
                throw new ArgumentException(
                    "Tactical evidence capture and rule evaluation must be installed together.",
                    nameof(tacticalContextQuery));
            contextQuery = tacticalContextQuery;
            contextEvaluator = tacticalContextEvaluator;
        }

        public bool TryEvaluate(
            GameplayCombatStateSnapshot state,
            string actorId,
            TargetExposureSnapshot exposure,
            bool canEnterTurnMode,
            out GameplayActorAttackEvaluation evaluation,
            out AttackResolutionFailure failure)
        {
            evaluation = null;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!string.Equals(
                    scenario.Id,
                    state.Session.ScenarioId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Attack rules and canonical state describe different scenarios.",
                    nameof(state));

            GameplaySessionStateSnapshot session = state.Session;
            if (session.Operation != GameplaySessionOperation.None)
            {
                failure = AttackResolutionFailure.OperationInProgress;
                return false;
            }

            if (session.Mode == GameplaySessionMode.TurnBased
                && !string.Equals(
                    session.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                failure = AttackResolutionFailure.ActorNotActive;
                return false;
            }

            if (!TryGetActor(session, actorId, out GameplayActorSnapshot actor))
            {
                failure = AttackResolutionFailure.ActorNotActive;
                return false;
            }
            if (actor.IsIncapacitated)
            {
                failure = AttackResolutionFailure.ActorIncapacitated;
                return false;
            }
            if (actor.IsPinned)
            {
                failure = AttackResolutionFailure.ActorPinned;
                return false;
            }

            bool startsEncounter = exposure != null
                && !string.IsNullOrWhiteSpace(exposure.TargetId)
                && scenario.TryGetAttackResponse(
                    exposure.TargetId,
                    out AttackResponseDefinition response)
                && response.StartsEncounter;
            if (startsEncounter
                && !session.EncounterActive
                && session.Mode == GameplaySessionMode.Exploration
                && !canEnterTurnMode)
            {
                failure = AttackResolutionFailure.TurnModeRequired;
                return false;
            }

            AttackDefinition attack = GetEquippedAttack(actor);
            if (attack == null || attack.Projectile != null)
            {
                failure = AttackResolutionFailure.AttackUnavailable;
                return false;
            }
            if (!GameplayAmmunitionPreparation.HasLoadedRounds(
                    scenario,
                    actor))
            {
                failure = AttackResolutionFailure
                    .InsufficientLoadedAmmunition;
                return false;
            }
            ActionCost cost = session.Mode == GameplaySessionMode.TurnBased
                    || startsEncounter
                ? attack.TurnCost
                : new ActionCost(0, 0f, attack.TurnCost.Mobility);
            if (actor.TurnBudget.ActionPoints < cost.ActionPoints)
            {
                failure = AttackResolutionFailure.InsufficientActionPoints;
                return false;
            }
            if (actor.TurnBudget.MovementOpportunity
                < cost.MovementOpportunity)
            {
                failure = AttackResolutionFailure
                    .InsufficientMovementOpportunity;
                return false;
            }

            if (exposure == null
                || !string.Equals(
                    exposure.ObserverId,
                    actorId,
                    StringComparison.Ordinal))
            {
                failure = AttackResolutionFailure.ExposureMismatch;
                return false;
            }
            if (string.Equals(actorId, exposure.TargetId, StringComparison.Ordinal)
                || !TryGetActor(
                    session,
                    exposure.TargetId,
                    out GameplayActorSnapshot target))
            {
                failure = AttackResolutionFailure.TargetNotFound;
                return false;
            }
            if (target.IsIncapacitated)
            {
                failure = AttackResolutionFailure.TargetIncapacitated;
                return false;
            }

            float distance = actor.Pose.Position.DistanceTo(target.Pose.Position);
            if (attack.Contact != null
                && distance > attack.Contact.MaximumReach + 0.0001f)
            {
                failure = AttackResolutionFailure.TargetOutOfReach;
                return false;
            }

            GameplayCapabilityProfile profile = GameplayCapabilityProfiles
                .Attack(attack, GameplaySemanticSubjectKind.Actor);
            ResolvedTacticalContext context = PrepareTacticalContext(
                state,
                profile,
                attack,
                actorId,
                target.ActorId,
                out failure);
            if (contextQuery != null && context == null) return false;
            evaluation = new GameplayActorAttackEvaluation(
                state,
                profile,
                attack,
                actor,
                target,
                cost,
                exposure,
                context);
            failure = AttackResolutionFailure.None;
            return true;
        }

        public GameplayPreparedTransition<GameplayActionRecord> Resolve(
            GameplayCombatStateSnapshot state,
            GameplayActorAttackEvaluation evaluation)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (evaluation == null)
                throw new ArgumentNullException(nameof(evaluation));
            if (!string.Equals(
                    state.CanonicalHash,
                    evaluation.StateHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Actor attack evaluation is stale for the canonical state.");

            long actionSequence = checked(state.Session.LastActionSequence + 1L);
            var randomIdentity = new GameplayTransitionIdentity(
                actionSequence,
                GameplaySemanticCapability.DirectAttack.ToString(),
                evaluation.Attacker.ActorId,
                evaluation.Target.ActorId);
            uint resolutionSeed = GameplayAddressedRandom.SampleUInt32(
                state.Session.RunIdentity,
                randomIdentity,
                "resolution");
            AttackResolutionRecord resolution = AttackResolutionRules.Resolve(
                actionSequence,
                resolutionSeed,
                evaluation.Exposure,
                evaluation.Attack.AccuracyDecay,
                evaluation.Distance,
                evaluation.Target.Wounds,
                evaluation.Attack.WoundMovementPenalty,
                evaluation.Attack.Contact,
                evaluation.Context);
            TurnBudget resultingBudget = evaluation.Attacker.TurnBudget
                .SpendAction(evaluation.Cost);
            var outcomes = new List<GameplayActionOutcome>
            {
                new AttackResolvedActionOutcome(resolution),
            };
            if (!GameplayAmmunitionPreparation.TryPrepareSpend(
                    scenario,
                    evaluation.Attacker,
                    actionSequence,
                    out AmmunitionSpentActionOutcome spend))
                throw new InvalidOperationException(
                    "Evaluated actor attack no longer has loaded ammunition.");
            if (spend != null) outcomes.Add(spend);
            var action = new GameplayActionRecord(
                actionSequence,
                new GameplayActionRequest(
                    evaluation.Attacker.ActorId,
                    evaluation.Attack.ActionId,
                    evaluation.Target.ActorId),
                evaluation.Cost,
                evaluation.Attacker.TurnBudget,
                resultingBudget,
                outcomes,
                evaluation.Context);
            return new GameplayPreparedTransition<GameplayActionRecord>(
                action,
                state,
                GameplayWeaponActionStateProjector.Project(state, action));
        }

        private ResolvedTacticalContext PrepareTacticalContext(
            GameplayCombatStateSnapshot state,
            GameplayCapabilityProfile profile,
            AttackDefinition attack,
            string attackerId,
            string targetId,
            out AttackResolutionFailure failure)
        {
            if (contextQuery == null)
            {
                failure = AttackResolutionFailure.None;
                return null;
            }
            TacticalContextSnapshot snapshot = contextQuery.Capture(
                state,
                new GameplayTacticalContextRequest(
                    profile,
                    attackerId,
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Actor,
                        targetId),
                    attack.SoundSignature));
            if (snapshot == null)
                throw new InvalidOperationException(
                    "Tactical context queries must return frozen evidence.");
            if (snapshot.StateRevision != state.Session.Revision)
            {
                failure = AttackResolutionFailure.WorldStateChanged;
                return null;
            }
            failure = AttackResolutionFailure.None;
            return contextEvaluator.Evaluate(snapshot);
        }

        private AttackDefinition GetEquippedAttack(GameplayActorSnapshot actor)
        {
            ScenarioActorDefinition definition = scenario.GetActor(
                actor.ActorId);
            if (definition.Inventory.Count == 0) return definition.Attack;
            return actor.EquippedItemId == null
                ? null
                : definition.GetInventoryItem(actor.EquippedItemId)?.Attack;
        }

        private static bool TryGetActor(
            GameplaySessionStateSnapshot session,
            string actorId,
            out GameplayActorSnapshot actor)
        {
            foreach (GameplayActorSnapshot candidate in session.Actors)
                if (string.Equals(
                    candidate.ActorId,
                    actorId,
                    StringComparison.Ordinal))
                {
                    actor = candidate;
                    return true;
                }
            actor = default;
            return false;
        }
    }
}
