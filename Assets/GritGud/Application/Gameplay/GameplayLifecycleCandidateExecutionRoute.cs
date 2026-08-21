using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Frozen external facts required by mandatory lifecycle continuations.
    /// Detection/projectile coordinators create this intent; the route never
    /// guesses encounter scope or emergency responders.
    /// </summary>
    public sealed class GameplayLifecycleCandidateIntent
    {
        public GameplayLifecycleCandidateIntent(
            GameplayReachableInput input,
            float elapsedSeconds = 0f,
            IEnumerable<string> participantIds = null,
            IEnumerable<string> responderIds = null,
            int emergencyActionPointAllowance = 0)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            GameplayNumericPolicy.RequireFinite(
                elapsedSeconds,
                nameof(elapsedSeconds));
            if (elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (emergencyActionPointAllowance < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(emergencyActionPointAllowance));
            ElapsedSeconds = elapsedSeconds;
            ParticipantIds = Copy(participantIds);
            ResponderIds = Copy(responderIds);
            EmergencyActionPointAllowance = emergencyActionPointAllowance;
        }

        public GameplayReachableInput Input { get; }
        public float ElapsedSeconds { get; }
        public IReadOnlyList<string> ParticipantIds { get; }
        public IReadOnlyList<string> ResponderIds { get; }
        public int EmergencyActionPointAllowance { get; }

        private static IReadOnlyList<string> Copy(IEnumerable<string> values)
        {
            var result = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
            {
                string id = GameplayContentIdentity.RequireText(
                    value,
                    nameof(values));
                if (!unique.Add(id))
                    throw new ArgumentException(
                        "Lifecycle intent identifiers must be unique.",
                        nameof(values));
                result.Add(id);
            }
            return result.AsReadOnly();
        }
    }

    public sealed class GameplayLifecycleCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "lifecycle.v1";
        private readonly float minimumVoluntaryTurnSeconds;
        private readonly float continuousTimeStepSeconds;

        public GameplayLifecycleCandidateExecutionRoute(
            ScenarioDefinition scenario,
            float continuousTimeStepSeconds = 0.1f)
        {
            minimumVoluntaryTurnSeconds = (scenario
                ?? throw new ArgumentNullException(nameof(scenario)))
                .Timing.MinimumVoluntaryTurnSeconds;
            GameplayNumericPolicy.RequireFinite(
                continuousTimeStepSeconds,
                nameof(continuousTimeStepSeconds));
            if (continuousTimeStepSeconds <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(continuousTimeStepSeconds));
            this.continuousTimeStepSeconds = continuousTimeStepSeconds;
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null) return false;
            try
            {
                switch (profile.Capability)
                {
                    case GameplaySemanticCapability.AdvanceWorld:
                        return profile.GetTrait("mode") == "continuous-time"
                            || profile.GetTrait("mode") == "voluntary-cycle";
                    case GameplaySemanticCapability.EmergencyReaction:
                        return profile.GetTrait("phase") == "begin"
                            || profile.GetTrait("phase") == "complete";
                    case GameplaySemanticCapability.ChangeTurnMode:
                        return profile.GetTrait("mode") == "enter"
                            || profile.GetTrait("mode") == "exit";
                    case GameplaySemanticCapability.ChangeEncounter:
                        string mode = profile.GetTrait("mode");
                        return mode == "begin"
                            || mode == "request-completion"
                            || mode == "complete";
                    default:
                        return false;
                }
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            GameplaySessionStateSnapshot session = context.State.Session;
            GameplayLifecycleCandidateIntent intent = candidate.Intent
                as GameplayLifecycleCandidateIntent;
            string failure;
            GameplayTransitionPayload payload = Prepare(
                session,
                candidate,
                intent,
                out failure);
            bool legal = payload != null;
            return GameplayBasicCandidateRouteUtility.Result(
                Id,
                context,
                candidate,
                legal,
                failure,
                new GameplayCandidateOutcomeEstimate(new[]
                {
                    new GameplayCandidateOutcomeFeature(
                        "lifecycle.mandatory",
                        1f),
                    new GameplayCandidateOutcomeFeature(
                        "world.elapsed-seconds",
                        payload is GameplayWorldAdvanceTransitionPayload world
                            ? world.ElapsedSeconds
                            : 0f),
                }),
                payload);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            evaluation?.FrozenPreparation as GameplayTransitionPayload
            ?? throw new ArgumentException(
                "Lifecycle preparation is missing.",
                nameof(evaluation));

        private GameplayTransitionPayload Prepare(
            GameplaySessionStateSnapshot session,
            GameplayCandidate candidate,
            GameplayLifecycleCandidateIntent intent,
            out string failure)
        {
            switch (candidate.Profile.Capability)
            {
                case GameplaySemanticCapability.AdvanceWorld:
                    return PrepareWorld(session, candidate, intent, out failure);
                case GameplaySemanticCapability.EmergencyReaction:
                    return PrepareEmergency(
                        session,
                        candidate,
                        intent,
                        out failure);
                case GameplaySemanticCapability.ChangeTurnMode:
                    return PrepareTurnMode(session, candidate, out failure);
                case GameplaySemanticCapability.ChangeEncounter:
                    return PrepareEncounter(
                        session,
                        candidate,
                        intent,
                        out failure);
                default:
                    throw new ArgumentOutOfRangeException(nameof(candidate));
            }
        }

        private GameplayTransitionPayload PrepareWorld(
            GameplaySessionStateSnapshot session,
            GameplayCandidate candidate,
            GameplayLifecycleCandidateIntent intent,
            out string failure)
        {
            string mode = candidate.Profile.GetTrait("mode");
            if (mode == "continuous-time")
            {
                if (session.Mode != GameplaySessionMode.Exploration)
                {
                    failure = "exploration-required";
                    return null;
                }
                failure = string.Empty;
                return new GameplayWorldAdvanceTransitionPayload(
                    candidate.ActorId,
                    mode,
                    intent?.ElapsedSeconds > 0f
                        ? intent.ElapsedSeconds
                        : continuousTimeStepSeconds);
            }
            if (session.Mode != GameplaySessionMode.TurnBased
                || session.EncounterActive
                || session.Operation
                    != GameplaySessionOperation.ResolvingWorldTurn
                || session.PendingVoluntaryTurnCycle == null)
            {
                failure = "voluntary-world-cycle-not-pending";
                return null;
            }
            failure = string.Empty;
            return new GameplayWorldAdvanceTransitionPayload(
                candidate.ActorId,
                mode);
        }

        private static GameplayTransitionPayload PrepareEmergency(
            GameplaySessionStateSnapshot session,
            GameplayCandidate candidate,
            GameplayLifecycleCandidateIntent intent,
            out string failure)
        {
            string phase = candidate.Profile.GetTrait("phase");
            if (phase == "begin")
            {
                if (session.Mode != GameplaySessionMode.TurnBased
                    || !session.EncounterActive
                    || session.Operation != GameplaySessionOperation.None
                    || session.TurnPhase != GameplayTurnPhase.Normal)
                {
                    failure = "emergency-window-not-available";
                    return null;
                }
                if (intent == null
                    || intent.ResponderIds.Count == 0
                    || intent.EmergencyActionPointAllowance <= 0)
                {
                    failure = "emergency-evidence-required";
                    return null;
                }
                failure = string.Empty;
                return new GameplayEmergencyReactionTransitionPayload(
                    candidate.ActorId,
                    phase,
                    intent.ResponderIds,
                    intent.EmergencyActionPointAllowance);
            }
            if (session.TurnPhase != GameplayTurnPhase.EmergencyReaction
                || session.EmergencyResponderIndex
                    < session.EmergencyResponders.Count)
            {
                failure = "emergency-response-pass-incomplete";
                return null;
            }
            failure = string.Empty;
            return new GameplayEmergencyReactionTransitionPayload(
                candidate.ActorId,
                phase);
        }

        private GameplayTransitionPayload PrepareTurnMode(
            GameplaySessionStateSnapshot session,
            GameplayCandidate candidate,
            out string failure)
        {
            string mode = candidate.Profile.GetTrait("mode");
            if (mode == "enter")
            {
                if (session.Mode != GameplaySessionMode.Exploration
                    || (!session.EncounterActive
                        && session.VoluntaryTurnReentrySecondsRemaining > 0f))
                {
                    failure = "turn-mode-entry-unavailable";
                    return null;
                }
                failure = string.Empty;
                return new GameplaySessionControlTransitionPayload(
                    candidate.ActorId,
                    GameplaySemanticCapability.ChangeTurnMode,
                    mode);
            }
            if (session.Mode != GameplaySessionMode.TurnBased
                || session.Operation != GameplaySessionOperation.None
                || session.EncounterActive)
            {
                failure = "turn-mode-exit-unavailable";
                return null;
            }
            failure = string.Empty;
            return new GameplaySessionControlTransitionPayload(
                candidate.ActorId,
                GameplaySemanticCapability.ChangeTurnMode,
                mode,
                minimumVoluntaryTurnSeconds);
        }

        private static GameplayTransitionPayload PrepareEncounter(
            GameplaySessionStateSnapshot session,
            GameplayCandidate candidate,
            GameplayLifecycleCandidateIntent intent,
            out string failure)
        {
            string mode = candidate.Profile.GetTrait("mode");
            if (mode == "begin")
            {
                if (session.EncounterActive)
                {
                    failure = "encounter-already-active";
                    return null;
                }
                if (intent == null || intent.ParticipantIds.Count == 0)
                {
                    failure = "encounter-scope-required";
                    return null;
                }
                failure = string.Empty;
                return new GameplaySessionControlTransitionPayload(
                    candidate.ActorId,
                    GameplaySemanticCapability.ChangeEncounter,
                    mode,
                    encounterParticipantIds: intent.ParticipantIds);
            }
            if (mode == "request-completion")
            {
                if (!session.EncounterActive
                    || session.EncounterCompletionRequested)
                {
                    failure = "encounter-completion-request-unavailable";
                    return null;
                }
            }
            else if (!session.EncounterActive)
            {
                failure = "encounter-not-active";
                return null;
            }
            failure = string.Empty;
            return new GameplaySessionControlTransitionPayload(
                candidate.ActorId,
                GameplaySemanticCapability.ChangeEncounter,
                mode);
        }
    }
}
