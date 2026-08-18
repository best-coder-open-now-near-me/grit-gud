using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayWorldAdvanceTransitionPayload :
        GameplayTransitionPayload
    {
        public const string Subject = "world";

        public GameplayWorldAdvanceTransitionPayload(
            string actorId,
            string mode,
            float elapsedSeconds = 0f)
            : base(
                GameplayCapabilityProfiles.AdvanceWorld(mode),
                actorId,
                Subject)
        {
            GameplayNumericPolicy.RequireFinite(
                elapsedSeconds,
                nameof(elapsedSeconds));
            if (elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            ElapsedSeconds = elapsedSeconds;
        }

        public float ElapsedSeconds { get; }
    }

    public sealed class GameplayEmergencyReactionTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayEmergencyReactionTransitionPayload(
            string actorId,
            string phase,
            IEnumerable<string> responderIds = null,
            int actionPointAllowance = 0)
            : base(
                GameplayCapabilityProfiles.EmergencyReaction(phase),
                actorId,
                actorId)
        {
            Phase = GameplayContentIdentity.RequireText(phase, nameof(phase));
            Responders = CopyIds(actorId, responderIds);
            if (phase == "begin" && actionPointAllowance <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(actionPointAllowance));
            ActionPointAllowance = actionPointAllowance;
        }

        public string Phase { get; }
        public IReadOnlyList<string> Responders { get; }
        public int ActionPointAllowance { get; }

        private static IReadOnlyList<string> CopyIds(
            string attackerId,
            IEnumerable<string> responderIds)
        {
            var result = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in responderIds ?? Array.Empty<string>())
            {
                string responderId = GameplayContentIdentity.RequireText(
                    id,
                    nameof(responderIds));
                if (string.Equals(
                        responderId,
                        attackerId,
                        StringComparison.Ordinal)
                    || !unique.Add(responderId))
                {
                    throw new ArgumentException(
                        "Emergency responders must be unique and cannot include the attacker.",
                        nameof(responderIds));
                }
                result.Add(responderId);
            }
            return result.AsReadOnly();
        }
    }

    public sealed class GameplaySessionControlTransitionPayload :
        GameplayTransitionPayload
    {
        public const string Subject = "session";

        public GameplaySessionControlTransitionPayload(
            string actorId,
            GameplaySemanticCapability capability,
            string mode,
            float minimumVoluntaryTurnSeconds = 0f,
            IEnumerable<string> encounterParticipantIds = null)
            : base(
                CreateProfile(capability, mode),
                actorId,
                Subject)
        {
            GameplayNumericPolicy.RequireFinite(
                minimumVoluntaryTurnSeconds,
                nameof(minimumVoluntaryTurnSeconds));
            if (minimumVoluntaryTurnSeconds < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumVoluntaryTurnSeconds));
            Mode = GameplayContentIdentity.RequireText(mode, nameof(mode));
            MinimumVoluntaryTurnSeconds = minimumVoluntaryTurnSeconds;
            EncounterParticipantIds = CopyIds(encounterParticipantIds);
        }

        public string Mode { get; }
        public float MinimumVoluntaryTurnSeconds { get; }

        public IReadOnlyList<string> EncounterParticipantIds { get; }

        private static IReadOnlyList<string> CopyIds(IEnumerable<string> values)
        {
            var result = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
            {
                string id = GameplayContentIdentity.RequireText(
                    value, nameof(values));
                if (!unique.Add(id))
                {
                    throw new ArgumentException(
                        "Encounter participants must be unique.",
                        nameof(values));
                }
                result.Add(id);
            }
            return result.AsReadOnly();
        }

        private static GameplayCapabilityProfile CreateProfile(
            GameplaySemanticCapability capability,
            string mode)
        {
            if (capability == GameplaySemanticCapability.ChangeTurnMode)
                return GameplayCapabilityProfiles.ChangeTurnMode(mode);
            if (capability == GameplaySemanticCapability.ChangeEncounter)
                return GameplayCapabilityProfiles.ChangeEncounter(mode);
            throw new ArgumentOutOfRangeException(nameof(capability));
        }
    }

    public sealed class GameplayLifecycleTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null) return false;
            try
            {
                if (profile.Capability == GameplaySemanticCapability.AdvanceWorld)
                {
                    string mode = profile.GetTrait("mode");
                    return mode == "voluntary-cycle"
                        || mode == "continuous-time";
                }
                if (profile.Capability
                    == GameplaySemanticCapability.EmergencyReaction)
                {
                    string phase = profile.GetTrait("phase");
                    return phase == "begin" || phase == "complete";
                }
                if (profile.Capability
                    == GameplaySemanticCapability.ChangeTurnMode)
                {
                    string mode = profile.GetTrait("mode");
                    return mode == "enter" || mode == "exit";
                }
                if (profile.Capability
                    == GameplaySemanticCapability.ChangeEncounter)
                {
                    string mode = profile.GetTrait("mode");
                    return mode == "begin"
                        || mode == "complete"
                        || mode == "request-completion";
                }
                return false;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            var mutation = new GameplayCanonicalStateMutation(state);
            object record;
            if (transition.Payload
                is GameplayWorldAdvanceTransitionPayload world)
            {
                record = ReduceWorld(state, mutation, world);
            }
            else if (transition.Payload
                is GameplayEmergencyReactionTransitionPayload emergency)
            {
                record = ReduceEmergency(state.Session, mutation, emergency);
            }
            else if (transition.Payload
                is GameplaySessionControlTransitionPayload control)
            {
                record = ReduceSessionControl(
                    state.Session,
                    mutation,
                    control);
            }
            else
            {
                throw new ArgumentException(
                    "Lifecycle transition payload is unsupported.",
                    nameof(transition));
            }
            mutation.LastTransitionSequence = transition.Identity.Sequence;
            return new GameplayReductionResult(
                state,
                mutation.Build(),
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        transition.Payload.SubjectId,
                        record),
                });
        }

        private static object ReduceSessionControl(
            GameplaySessionStateSnapshot session,
            GameplayCanonicalStateMutation mutation,
            GameplaySessionControlTransitionPayload payload)
        {
            if (payload.Profile.Capability
                == GameplaySemanticCapability.ChangeTurnMode)
            {
                if (payload.Mode == "enter")
                {
                    if (session.Mode != GameplaySessionMode.Exploration
                        || (!session.EncounterActive
                            && session.VoluntaryTurnReentrySecondsRemaining > 0f))
                        throw new InvalidOperationException(
                            "Turn mode cannot be entered from this state.");
                    mutation.Mode = GameplaySessionMode.TurnBased;
                    mutation.Operation = GameplaySessionOperation.None;
                    mutation.TurnContext = session.EncounterActive
                        ? TurnModeContext.InitiatedEncounter
                        : TurnModeContext.Voluntary;
                    if (string.IsNullOrWhiteSpace(session.ActiveActorId))
                        mutation.ActiveActorId = FirstCapable(session);
                    mutation.JournalSequence = checked(
                        mutation.JournalSequence + 1L);
                    mutation.Revision = checked(mutation.Revision + 1L);
                    return payload;
                }
                if (payload.Mode != "exit"
                    || session.Mode != GameplaySessionMode.TurnBased
                    || session.Operation != GameplaySessionOperation.None
                    || session.EncounterActive
                    || payload.MinimumVoluntaryTurnSeconds <= 0f)
                    throw new InvalidOperationException(
                        "Turn mode cannot be exited from this state.");
                var cycle = new VoluntaryTurnCycleRecord(
                    checked(session.LastVoluntaryTurnCycleSequence + 1L),
                    session.Actors);
                RefreshAll(mutation, session.Actors);
                mutation.Mode = GameplaySessionMode.Exploration;
                mutation.TurnContext = TurnModeContext.None;
                mutation.VoluntaryTurnReentrySecondsRemaining =
                    payload.MinimumVoluntaryTurnSeconds;
                mutation.LastVoluntaryTurnCycleSequence = cycle.Sequence;
                mutation.JournalSequence = checked(
                    mutation.JournalSequence + 2L);
                mutation.Revision = checked(mutation.Revision + 1L);
                return cycle;
            }

            if (payload.Mode == "begin")
            {
                if (session.EncounterActive)
                    throw new InvalidOperationException(
                        "Encounter is already active.");
                IReadOnlyList<string> scope = ResolveScope(
                    session,
                    payload.EncounterParticipantIds);
                mutation.EncounterActive = true;
                mutation.EncounterCompletionRequested = false;
                mutation.InitiativeOrder = scope;
                mutation.EncounterState = session.EncounterState
                    .WithParticipants(scope);
                mutation.JournalSequence = checked(
                    mutation.JournalSequence + 1L);
                if (session.Mode == GameplaySessionMode.Exploration)
                {
                    mutation.Mode = GameplaySessionMode.TurnBased;
                    mutation.Operation = GameplaySessionOperation.None;
                    mutation.TurnContext = TurnModeContext.InitiatedEncounter;
                    if (string.IsNullOrWhiteSpace(session.ActiveActorId))
                        mutation.ActiveActorId = FirstCapable(session, scope);
                    mutation.JournalSequence = checked(
                        mutation.JournalSequence + 1L);
                }
                else
                {
                    mutation.TurnContext = TurnModeContext.InitiatedEncounter;
                }
                mutation.Revision = checked(mutation.Revision + 1L);
                return payload;
            }
            if (payload.Mode == "complete")
            {
                if (!session.EncounterActive)
                    throw new InvalidOperationException(
                        "No encounter is active.");
                mutation.EncounterActive = false;
                mutation.EncounterCompletionRequested = false;
                mutation.InitiativeOrder = session.AllInitiativeOrder;
                mutation.EncounterState = session.EncounterState
                    .WithParticipants(Array.Empty<string>());
                if (session.Mode == GameplaySessionMode.TurnBased)
                    mutation.TurnContext = TurnModeContext.Voluntary;
                mutation.JournalSequence = checked(
                    mutation.JournalSequence + 1L);
                mutation.Revision = checked(mutation.Revision + 1L);
                return payload;
            }
            if (payload.Mode != "request-completion"
                || !session.EncounterActive
                || session.EncounterCompletionRequested)
                throw new InvalidOperationException(
                    "Encounter completion cannot be requested from this state.");
            mutation.EncounterCompletionRequested = true;
            mutation.Revision = checked(mutation.Revision + 1L);
            return payload;
        }

        private static object ReduceWorld(
            GameplayCombatStateSnapshot state,
            GameplayCanonicalStateMutation mutation,
            GameplayWorldAdvanceTransitionPayload payload)
        {
            string mode = payload.Profile.GetTrait("mode");
            if (mode == "voluntary-cycle")
            {
                GameplaySessionStateSnapshot session = state.Session;
                if (session.Mode != GameplaySessionMode.TurnBased
                    || session.EncounterActive
                    || session.Operation
                        != GameplaySessionOperation.ResolvingWorldTurn
                    || session.PendingVoluntaryTurnCycle == null)
                    throw new InvalidOperationException(
                        "No voluntary world turn is pending.");
                VoluntaryTurnCycleRecord cycle =
                    session.PendingVoluntaryTurnCycle;
                mutation.PendingVoluntaryTurnCycle = null;
                mutation.LastVoluntaryTurnCycleSequence = cycle.Sequence;
                RefreshAll(mutation, session.Actors);
                mutation.ActiveActorId = FirstCapable(session);
                mutation.Operation = GameplaySessionOperation.None;
                mutation.TurnContext = TurnModeContext.Voluntary;
                mutation.JournalSequence = checked(
                    mutation.JournalSequence + 1L);
                mutation.Revision = checked(mutation.Revision + 1L);
                return cycle;
            }

            if (mode != "continuous-time")
                throw new NotSupportedException(
                    $"World advance mode '{mode}' is unsupported.");
            if (state.Session.Mode != GameplaySessionMode.Exploration)
                throw new InvalidOperationException(
                    "Continuous world time advances only in exploration.");
            bool changedGameplayClock = !state.Session.EncounterActive
                && state.Session.VoluntaryTurnReentrySecondsRemaining > 0f
                && payload.ElapsedSeconds > 0f;
            if (changedGameplayClock)
            {
                mutation.VoluntaryTurnReentrySecondsRemaining = Math.Max(
                    0f,
                    state.Session.VoluntaryTurnReentrySecondsRemaining
                        - payload.ElapsedSeconds);
                mutation.Revision = checked(mutation.Revision + 1L);
            }
            if (state.Covers(GameplayCombatStateCoverage.SmokeFields)
                && payload.ElapsedSeconds > 0f)
            {
                var fields = new List<SmokeFieldSnapshot>();
                foreach (SmokeFieldSnapshot smoke in state.SmokeFields)
                {
                    float remaining = Math.Max(
                        0f,
                        smoke.RemainingFraction
                            - (payload.ElapsedSeconds
                                / smoke.Field.Definition
                                    .ExplorationDurationSeconds));
                    if (remaining > 0f)
                        fields.Add(new SmokeFieldSnapshot(
                            smoke.Field,
                            remaining));
                }
                mutation.ReplaceSmokeFields(fields);
            }
            return payload.ElapsedSeconds;
        }

        private static object ReduceEmergency(
            GameplaySessionStateSnapshot session,
            GameplayCanonicalStateMutation mutation,
            GameplayEmergencyReactionTransitionPayload payload)
        {
            if (payload.Phase == "begin")
            {
                if (session.Mode != GameplaySessionMode.TurnBased
                    || !session.EncounterActive
                    || session.Operation != GameplaySessionOperation.None
                    || session.TurnPhase != GameplayTurnPhase.Normal
                    || payload.Responders.Count == 0)
                    throw new InvalidOperationException(
                        "Emergency reaction cannot begin from this state.");
                string first = payload.Responders[0];
                GameplayActorSnapshot actor = mutation.GetActor(first);
                var budget = new TurnBudget(
                    payload.ActionPointAllowance,
                    Math.Max(
                        0f,
                        actor.TurnMovementAllowance
                            - actor.Wounds.MovementPenalty));
                mutation.ReplaceActor(
                    GameplayCanonicalStateMutation.CopyActor(
                        actor,
                        budget: budget,
                        emergencyActionPointAllowance:
                            payload.ActionPointAllowance));
                mutation.EmergencyResponders = payload.Responders;
                mutation.EmergencyResponderIndex = 0;
                mutation.EmergencyResumeActorId = payload.ActorId;
                mutation.TurnPhase = GameplayTurnPhase.EmergencyReaction;
                mutation.ActiveActorId = first;
                mutation.Revision = checked(mutation.Revision + 1L);
                return payload;
            }

            if (payload.Phase != "complete"
                || session.TurnPhase != GameplayTurnPhase.EmergencyReaction
                || session.EmergencyResponderIndex
                    < session.EmergencyResponders.Count)
                throw new InvalidOperationException(
                    "Emergency reaction is not ready to complete.");
            string resume = session.EmergencyResumeActorId;
            GameplayActorSnapshot resuming = mutation.GetActor(resume);
            Refresh(mutation, resuming);
            mutation.EmergencyResponders = Array.Empty<string>();
            mutation.EmergencyResponderIndex = -1;
            mutation.EmergencyResumeActorId = string.Empty;
            mutation.TurnPhase = GameplayTurnPhase.Normal;
            mutation.ActiveActorId = resume;
            mutation.Revision = checked(mutation.Revision + 1L);
            return payload;
        }

        private static void RefreshAll(
            GameplayCanonicalStateMutation mutation,
            IEnumerable<GameplayActorSnapshot> actors)
        {
            foreach (GameplayActorSnapshot actor in actors)
                Refresh(mutation, actor);
        }

        private static void Refresh(
            GameplayCanonicalStateMutation mutation,
            GameplayActorSnapshot actor)
        {
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                budget: new TurnBudget(
                    actor.TurnActionPointAllowance,
                    Math.Max(
                        0f,
                        actor.TurnMovementAllowance
                            - actor.Wounds.MovementPenalty))));
        }

        private static IReadOnlyList<string> ResolveScope(
            GameplaySessionStateSnapshot session,
            IReadOnlyList<string> requested)
        {
            var selected = new HashSet<string>(
                requested ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (selected.Count == 0)
            {
                foreach (string actorId in session.AllInitiativeOrder)
                    selected.Add(actorId);
            }
            var result = new List<string>();
            foreach (string actorId in session.AllInitiativeOrder)
            {
                if (selected.Remove(actorId))
                    result.Add(actorId);
            }
            if (selected.Count > 0)
            {
                throw new InvalidOperationException(
                    "Encounter scope references an actor absent from initiative.");
            }
            return result.AsReadOnly();
        }

        private static string FirstCapable(
            GameplaySessionStateSnapshot session,
            IReadOnlyList<string> scope = null)
        {
            foreach (string actorId in scope ?? session.InitiativeOrder)
                if (!session.GetActor(actorId).IsIncapacitated)
                    return actorId;
            IReadOnlyList<string> fallback = scope ?? session.InitiativeOrder;
            return fallback[0];
        }
    }
}
