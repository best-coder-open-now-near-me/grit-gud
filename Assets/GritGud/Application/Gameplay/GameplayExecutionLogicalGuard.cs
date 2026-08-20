using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public static class GameplayMandatoryWorkRules
    {
        public static bool HasPending(GameplayCombatStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            GameplaySessionStateSnapshot session = state.Session;
            if (session.Operation != GameplaySessionOperation.None
                || session.PendingMovementRoute != null
                || session.PendingVoluntaryTurnCycle != null)
                return true;
            if (state.Covers(GameplayCombatStateCoverage.Projectiles))
                foreach (ProjectileFlightSnapshot projectile
                    in state.Projectiles)
                    if (projectile.Status == ProjectileFlightStatus.InFlight)
                        return true;
            return false;
        }
    }

    /// <summary>
    /// Hashes material battle state while deliberately excluding causal
    /// sequences, journal/revision counters, and timing telemetry. This makes
    /// repeated/no-progress guards meaningful even though canonical identities
    /// correctly advance on every reduced transition.
    /// </summary>
    public static class GameplayMaterialStateDigest
    {
        public static string Calculate(GameplayCombatStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var text = new StringBuilder();
            GameplaySessionStateSnapshot session = state.Session;
            Add(text, state.Coverage.ToString());
            Add(text, session.Mode.ToString());
            Add(text, session.Operation.ToString());
            Add(text, session.TurnContext.ToString());
            Add(text, session.EncounterActive.ToString());
            Add(text, session.EncounterCompletionRequested.ToString());
            Add(text, session.ActiveActorId);
            Add(text, session.TurnPhase.ToString());
            Add(text, session.EmergencyResponderIndex.ToString());
            Add(text, session.EmergencyResumeActorId);
            Add(text, GameplayNumericPolicy.FormatCanonical(
                session.VoluntaryTurnReentrySecondsRemaining));
            AddValues(text, session.InitiativeOrder);
            AddValues(text, session.AllInitiativeOrder);
            AddValues(text, session.EmergencyResponders);
            AddDigests(text, session.Actors);
            AddDigests(text, session.Objectives);
            Add(text, session.PendingMovementRoute == null
                ? string.Empty
                : GameplayCanonicalValueDigest.Calculate(
                    session.PendingMovementRoute));
            Add(text, session.PendingVoluntaryTurnCycle == null
                ? string.Empty
                : GameplayCanonicalValueDigest.Calculate(
                    session.PendingVoluntaryTurnCycle));
            AddDigests(text, session.EncounterState.Awareness);
            AddValues(text, session.EncounterState.ParticipantIds);
            AddDigests(text, state.Destructibles);
            AddDigests(text, state.Vehicles);
            AddDigests(text, state.Projectiles);
            AddDigests(text, state.SmokeFields);
            AddDigests(text, state.FireFields);
            AddDigests(text, state.Drones);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(text.ToString()));
                var result = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                    result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }

        private static void AddDigests<T>(
            StringBuilder text,
            IEnumerable<T> values)
        {
            foreach (T value in values)
                Add(text, GameplayCanonicalValueDigest.Calculate(value));
        }

        private static void AddValues(
            StringBuilder text,
            IEnumerable<string> values)
        {
            foreach (string value in values) Add(text, value);
        }

        private static void Add(StringBuilder text, string value)
        {
            string normalized = value ?? string.Empty;
            text.Append(normalized.Length);
            text.Append(':');
            text.Append(normalized);
            text.Append(';');
        }
    }

    public sealed class GameplayExecutionLogicalGuard
    {
        private readonly GameplayExecutionLogicalGuardPolicy policy;
        private readonly Dictionary<string, int> materialStateVisits =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private string currentTurnActorId = string.Empty;
        private string currentTurnStartDigest = string.Empty;
        private int transitionCount;
        private int noProgressTurns;

        public GameplayExecutionLogicalGuard(
            GameplayCombatStateSnapshot initialState,
            GameplayExecutionLogicalGuardPolicy guardPolicy = null)
        {
            if (initialState == null)
                throw new ArgumentNullException(nameof(initialState));
            policy = guardPolicy ?? new GameplayExecutionLogicalGuardPolicy();
            materialStateVisits.Add(
                GameplayMaterialStateDigest.Calculate(initialState),
                1);
        }

        public int TransitionCount => transitionCount;
        public int NoProgressTurnCount => noProgressTurns;

        public void BeginTurn(
            string actorId,
            GameplayCombatStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            currentTurnActorId = GameplayContentIdentity.RequireText(
                actorId,
                nameof(actorId));
            currentTurnStartDigest = GameplayMaterialStateDigest.Calculate(
                state);
        }

        /// <summary>
        /// Called before the EndTurn transition. Mandatory continuations must be
        /// resolved, and the actor must have changed material state often enough
        /// to prevent an unattended end-turn loop.
        /// </summary>
        public void CompleteTurn(
            GameplayCombatStateSnapshot state,
            bool mandatoryWorkRemaining)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (currentTurnStartDigest.Length == 0)
                throw new InvalidOperationException(
                    "A logical turn guard must begin before it completes.");
            if (mandatoryWorkRemaining)
                throw Failure(
                    GameplayDecisionFailureKind.UnresolvedMandatoryWork,
                    "A turn cannot end while mandatory work remains.",
                    currentTurnActorId,
                    state.CanonicalHash);
            string resulting = GameplayMaterialStateDigest.Calculate(state);
            noProgressTurns = string.Equals(
                    currentTurnStartDigest,
                    resulting,
                    StringComparison.Ordinal)
                ? checked(noProgressTurns + 1)
                : 0;
            currentTurnStartDigest = string.Empty;
            if (noProgressTurns >= policy.MaximumNoProgressTurns)
                throw Failure(
                    GameplayDecisionFailureKind.NoProgressTurn,
                    "Maximum consecutive no-progress turns was reached.",
                    currentTurnActorId,
                    state.CanonicalHash);
        }

        public void ValidatePreparedTransition(
            GameplayReductionResult reduction)
        {
            if (reduction == null)
                throw new ArgumentNullException(nameof(reduction));
            transitionCount = checked(transitionCount + 1);
            if (transitionCount > policy.MaximumTransitions)
                throw Failure(
                    GameplayDecisionFailureKind.MaximumTransitionsExceeded,
                    "Maximum battle transition count was exceeded.",
                    reduction.Resulting.Session.ActiveActorId,
                    reduction.Resulting.CanonicalHash);
            string digest = GameplayMaterialStateDigest.Calculate(
                reduction.Resulting);
            materialStateVisits.TryGetValue(digest, out int visits);
            visits = checked(visits + 1);
            materialStateVisits[digest] = visits;
            if (visits > policy.MaximumRepeatedMaterialStates)
                throw Failure(
                    GameplayDecisionFailureKind.RepeatedCanonicalState,
                    "A material battle state repeated beyond its guard limit.",
                    reduction.Resulting.Session.ActiveActorId,
                    reduction.Resulting.CanonicalHash);
        }

        private static GameplayDecisionFailureException Failure(
            GameplayDecisionFailureKind kind,
            string message,
            string actorId,
            string stateHash) => new GameplayDecisionFailureException(
                kind,
                message,
                new GameplayDecisionDiagnostic(
                    actorId,
                    stateHash,
                    activeStage: null,
                    candidateIds: null,
                    legalCandidateIds: null,
                    selectedCandidateId: null,
                    timings: null));
    }
}
