using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayBattleArtifactProvenance
    {
        public GameplayBattleArtifactProvenance(
            string sourceRevision,
            string sourceBranch,
            string label,
            IEnumerable<string> parentArtifactIds = null)
        {
            SourceRevision = GameplayContentIdentity.RequireText(
                sourceRevision,
                nameof(sourceRevision));
            SourceBranch = GameplayContentIdentity.RequireText(
                sourceBranch,
                nameof(sourceBranch));
            Label = label?.Trim() ?? string.Empty;
            var parents = new List<string>(
                parentArtifactIds ?? Array.Empty<string>());
            parents.Sort(StringComparer.Ordinal);
            for (int index = 0; index < parents.Count; index++)
            {
                parents[index] = GameplayContentIdentity.RequireDigest(
                    parents[index],
                    nameof(parentArtifactIds));
                if (index > 0 && string.Equals(
                        parents[index - 1],
                        parents[index],
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Battle artifact parents must be unique.",
                        nameof(parentArtifactIds));
            }
            ParentArtifactIds = parents.AsReadOnly();
        }

        public string SourceRevision { get; }
        public string SourceBranch { get; }
        public string Label { get; }
        public IReadOnlyList<string> ParentArtifactIds { get; }
    }

    public sealed class GameplayBattleArtifactTransition
    {
        public GameplayBattleArtifactTransition(
            long sequence,
            string kind,
            string actorId,
            string subjectId,
            string previousStateHash,
            string resultingStateHash,
            string transitionPayloadDigest,
            string transitionCanonical,
            int? decisionIndex,
            IEnumerable<string> domainEventTypes,
            IEnumerable<string> domainEventPayloadDigests,
            IEnumerable<string> domainEventPayloadsCanonical,
            string resultingStateCanonical)
        {
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            if (decisionIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(decisionIndex));
            Sequence = sequence;
            Kind = GameplayContentIdentity.RequireText(kind, nameof(kind));
            ActorId = GameplayContentIdentity.RequireText(
                actorId,
                nameof(actorId));
            SubjectId = GameplayContentIdentity.RequireText(
                subjectId,
                nameof(subjectId));
            PreviousStateHash = GameplayContentIdentity.RequireDigest(
                previousStateHash,
                nameof(previousStateHash));
            ResultingStateHash = GameplayContentIdentity.RequireDigest(
                resultingStateHash,
                nameof(resultingStateHash));
            TransitionPayloadDigest = GameplayContentIdentity.RequireDigest(
                transitionPayloadDigest,
                nameof(transitionPayloadDigest));
            TransitionCanonical = RequireJson(
                transitionCanonical,
                nameof(transitionCanonical));
            if (!string.Equals(
                    TransitionPayloadDigest,
                    GameplayCanonicalValueDigest.CalculateCanonicalJson(
                        TransitionCanonical),
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Battle artifact transition canonical JSON does not match its digest.",
                    nameof(transitionCanonical));
            DecisionIndex = decisionIndex;
            DomainEventTypes = CopyText(domainEventTypes, nameof(
                domainEventTypes));
            DomainEventPayloadDigests = CopyDigests(
                domainEventPayloadDigests,
                nameof(domainEventPayloadDigests));
            DomainEventPayloadsCanonical = CopyJson(
                domainEventPayloadsCanonical,
                nameof(domainEventPayloadsCanonical));
            if (DomainEventTypes.Count != DomainEventPayloadDigests.Count
                || DomainEventTypes.Count
                    != DomainEventPayloadsCanonical.Count)
                throw new ArgumentException(
                    "Battle artifact event types, digests, and payloads must align.");
            for (int index = 0; index < DomainEventPayloadDigests.Count; index++)
                if (!string.Equals(
                        DomainEventPayloadDigests[index],
                        GameplayCanonicalValueDigest.CalculateCanonicalJson(
                            DomainEventPayloadsCanonical[index]),
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Battle artifact domain-event canonical JSON does not match its digest.",
                        nameof(domainEventPayloadsCanonical));
            ResultingStateCanonical = RequireJson(
                resultingStateCanonical,
                nameof(resultingStateCanonical));
        }

        public long Sequence { get; }
        public string Kind { get; }
        public string ActorId { get; }
        public string SubjectId { get; }
        public string PreviousStateHash { get; }
        public string ResultingStateHash { get; }
        public string TransitionPayloadDigest { get; }
        public string TransitionCanonical { get; }
        public int? DecisionIndex { get; }
        public IReadOnlyList<string> DomainEventTypes { get; }
        public IReadOnlyList<string> DomainEventPayloadDigests { get; }
        public IReadOnlyList<string> DomainEventPayloadsCanonical { get; }
        public string ResultingStateCanonical { get; }

        private static IReadOnlyList<string> CopyText(
            IEnumerable<string> values,
            string name)
        {
            var copy = new List<string>(values
                ?? throw new ArgumentNullException(name));
            for (int index = 0; index < copy.Count; index++)
                copy[index] = GameplayContentIdentity.RequireText(
                    copy[index],
                    name);
            return copy.AsReadOnly();
        }

        private static IReadOnlyList<string> CopyDigests(
            IEnumerable<string> values,
            string name)
        {
            var copy = new List<string>(values
                ?? throw new ArgumentNullException(name));
            for (int index = 0; index < copy.Count; index++)
                copy[index] = GameplayContentIdentity.RequireDigest(
                    copy[index],
                    name);
            return copy.AsReadOnly();
        }

        private static IReadOnlyList<string> CopyJson(
            IEnumerable<string> values,
            string name)
        {
            var copy = new List<string>(values
                ?? throw new ArgumentNullException(name));
            for (int index = 0; index < copy.Count; index++)
                copy[index] = RequireJson(copy[index], name);
            return copy.AsReadOnly();
        }

        private static string RequireJson(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Canonical artifact JSON cannot be empty.",
                    name);
            return value;
        }
    }

    public sealed class GameplayBattleArtifactDecision
    {
        public GameplayBattleArtifactDecision(
            int decisionIndex,
            string policyId,
            int policyVersion,
            string actorId,
            string previousStateHash,
            string candidateSetDigest,
            IEnumerable<string> candidateIds,
            IEnumerable<string> legalCandidateIds,
            string selectedCandidateId,
            GameplayPolicySelectionReason selectionReason,
            float score,
            IEnumerable<GameplayPolicyScoreComponent> scoreComponents,
            long transitionSequence,
            string transitionPayloadDigest,
            string resultingStateHash)
        {
            if (decisionIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(decisionIndex));
            if (policyVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(policyVersion));
            if (transitionSequence <= 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(transitionSequence));
            if (!Enum.IsDefined(
                    typeof(GameplayPolicySelectionReason),
                    selectionReason))
                throw new ArgumentOutOfRangeException(nameof(selectionReason));
            GameplayNumericPolicy.RequireFinite(score, nameof(score));
            DecisionIndex = decisionIndex;
            PolicyId = GameplayContentIdentity.RequireText(
                policyId,
                nameof(policyId));
            PolicyVersion = policyVersion;
            ActorId = GameplayContentIdentity.RequireText(
                actorId,
                nameof(actorId));
            PreviousStateHash = GameplayContentIdentity.RequireDigest(
                previousStateHash,
                nameof(previousStateHash));
            CandidateSetDigest = GameplayContentIdentity.RequireDigest(
                candidateSetDigest,
                nameof(candidateSetDigest));
            CandidateIds = CopySortedIds(candidateIds, nameof(candidateIds));
            LegalCandidateIds = CopySortedIds(
                legalCandidateIds,
                nameof(legalCandidateIds));
            SelectedCandidateId = GameplayContentIdentity.RequireText(
                selectedCandidateId,
                nameof(selectedCandidateId));
            if (!Contains(LegalCandidateIds, SelectedCandidateId))
                throw new ArgumentException(
                    "Selected artifact candidates must be legal.",
                    nameof(selectedCandidateId));
            SelectionReason = selectionReason;
            Score = GameplayNumericPolicy.Normalize(score);
            ScoreComponents = CopyScoreComponents(scoreComponents);
            TransitionSequence = transitionSequence;
            TransitionPayloadDigest = GameplayContentIdentity.RequireDigest(
                transitionPayloadDigest,
                nameof(transitionPayloadDigest));
            ResultingStateHash = GameplayContentIdentity.RequireDigest(
                resultingStateHash,
                nameof(resultingStateHash));
        }

        public int DecisionIndex { get; }
        public string PolicyId { get; }
        public int PolicyVersion { get; }
        public string ActorId { get; }
        public string PreviousStateHash { get; }
        public string CandidateSetDigest { get; }
        public IReadOnlyList<string> CandidateIds { get; }
        public IReadOnlyList<string> LegalCandidateIds { get; }
        public string SelectedCandidateId { get; }
        public GameplayPolicySelectionReason SelectionReason { get; }
        public float Score { get; }
        public IReadOnlyList<GameplayPolicyScoreComponent> ScoreComponents
        {
            get;
        }
        public long TransitionSequence { get; }
        public string TransitionPayloadDigest { get; }
        public string ResultingStateHash { get; }

        private static IReadOnlyList<string> CopySortedIds(
            IEnumerable<string> values,
            string name)
        {
            var copy = new List<string>(values
                ?? throw new ArgumentNullException(name));
            copy.Sort(StringComparer.Ordinal);
            for (int index = 0; index < copy.Count; index++)
            {
                copy[index] = GameplayContentIdentity.RequireText(
                    copy[index],
                    name);
                if (index > 0 && string.Equals(
                        copy[index - 1],
                        copy[index],
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Artifact candidate identifiers must be unique.",
                        name);
            }
            return copy.AsReadOnly();
        }

        private static IReadOnlyList<GameplayPolicyScoreComponent>
            CopyScoreComponents(
                IEnumerable<GameplayPolicyScoreComponent> values)
        {
            var copy = new List<GameplayPolicyScoreComponent>(values
                ?? throw new ArgumentNullException(nameof(values)));
            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left?.FeatureId,
                right?.FeatureId));
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null)
                    throw new ArgumentException(
                        "Artifact score components cannot contain null entries.",
                        nameof(values));
                if (index > 0 && string.Equals(
                        copy[index - 1].FeatureId,
                        copy[index].FeatureId,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Artifact score components must be unique.",
                        nameof(values));
            }
            return copy.AsReadOnly();
        }

        private static bool Contains(
            IEnumerable<string> values,
            string expected)
        {
            foreach (string value in values)
                if (string.Equals(value, expected, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }

    public sealed class GameplayBattleArtifactTerminal
    {
        public GameplayBattleArtifactTerminal(
            GameplayBattleTerminalKind kind,
            long transitionSequence,
            string finalStateHash,
            IEnumerable<string> capablePartyActorIds,
            IEnumerable<string> capableHostileActorIds,
            GameplayDecisionFailureKind? failureKind,
            string failureMessage)
        {
            var terminal = new GameplayBattleTerminalResult(
                kind,
                transitionSequence,
                finalStateHash,
                capablePartyActorIds,
                capableHostileActorIds,
                failureKind,
                failureMessage);
            Kind = terminal.Kind;
            TransitionSequence = terminal.TransitionSequence;
            FinalStateHash = terminal.FinalStateHash;
            CapablePartyActorIds = terminal.CapablePartyActorIds;
            CapableHostileActorIds = terminal.CapableHostileActorIds;
            FailureKind = terminal.FailureKind;
            FailureMessage = terminal.FailureMessage;
        }

        public GameplayBattleTerminalKind Kind { get; }
        public long TransitionSequence { get; }
        public string FinalStateHash { get; }
        public IReadOnlyList<string> CapablePartyActorIds { get; }
        public IReadOnlyList<string> CapableHostileActorIds { get; }
        public GameplayDecisionFailureKind? FailureKind { get; }
        public string FailureMessage { get; }
    }

    public sealed class GameplayBattleActorScore
    {
        public GameplayBattleActorScore(
            string actorId,
            int decisions,
            int turnsCompleted,
            int moves,
            float movementDistance,
            int attacks,
            int hits,
            int woundsDealt,
            int explosiveThrows,
            int concussiveTargets,
            int fireDeployments,
            int droneMoves,
            int droneAttacks,
            int reloads,
            int roundsSpent,
            int roundsReloaded,
            int finalLoadedRounds,
            int finalReserveRounds,
            int finalWounds,
            bool incapacitated)
        {
            ActorId = GameplayContentIdentity.RequireText(
                actorId,
                nameof(actorId));
            Decisions = NonNegative(decisions, nameof(decisions));
            TurnsCompleted = NonNegative(
                turnsCompleted,
                nameof(turnsCompleted));
            Moves = NonNegative(moves, nameof(moves));
            GameplayNumericPolicy.RequireFinite(
                movementDistance,
                nameof(movementDistance));
            if (movementDistance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(movementDistance));
            MovementDistance = GameplayNumericPolicy.Normalize(
                movementDistance);
            Attacks = NonNegative(attacks, nameof(attacks));
            Hits = NonNegative(hits, nameof(hits));
            WoundsDealt = NonNegative(woundsDealt, nameof(woundsDealt));
            ExplosiveThrows = NonNegative(
                explosiveThrows,
                nameof(explosiveThrows));
            ConcussiveTargets = NonNegative(
                concussiveTargets,
                nameof(concussiveTargets));
            FireDeployments = NonNegative(
                fireDeployments,
                nameof(fireDeployments));
            DroneMoves = NonNegative(droneMoves, nameof(droneMoves));
            DroneAttacks = NonNegative(droneAttacks, nameof(droneAttacks));
            Reloads = NonNegative(reloads, nameof(reloads));
            RoundsSpent = NonNegative(roundsSpent, nameof(roundsSpent));
            RoundsReloaded = NonNegative(
                roundsReloaded,
                nameof(roundsReloaded));
            FinalLoadedRounds = NonNegative(
                finalLoadedRounds,
                nameof(finalLoadedRounds));
            FinalReserveRounds = NonNegative(
                finalReserveRounds,
                nameof(finalReserveRounds));
            FinalWounds = NonNegative(finalWounds, nameof(finalWounds));
            Incapacitated = incapacitated;
        }

        public string ActorId { get; }
        public int Decisions { get; }
        public int TurnsCompleted { get; }
        public int Moves { get; }
        public float MovementDistance { get; }
        public int Attacks { get; }
        public int Hits { get; }
        public int WoundsDealt { get; }
        public int ExplosiveThrows { get; }
        public int ConcussiveTargets { get; }
        public int FireDeployments { get; }
        public int DroneMoves { get; }
        public int DroneAttacks { get; }
        public int Reloads { get; }
        public int RoundsSpent { get; }
        public int RoundsReloaded { get; }
        public int FinalLoadedRounds { get; }
        public int FinalReserveRounds { get; }
        public int FinalWounds { get; }
        public bool Incapacitated { get; }

        private static int NonNegative(int value, string name)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public sealed class GameplayBattleAmmunitionScore
    {
        public GameplayBattleAmmunitionScore(
            string ammoTypeId,
            int reloads,
            int roundsSpent,
            int roundsReloaded,
            int finalLoadedRounds,
            int finalReserveRounds)
        {
            AmmoTypeId = GameplayContentIdentity.RequireText(
                ammoTypeId,
                nameof(ammoTypeId));
            Reloads = NonNegative(reloads, nameof(reloads));
            RoundsSpent = NonNegative(roundsSpent, nameof(roundsSpent));
            RoundsReloaded = NonNegative(
                roundsReloaded,
                nameof(roundsReloaded));
            FinalLoadedRounds = NonNegative(
                finalLoadedRounds,
                nameof(finalLoadedRounds));
            FinalReserveRounds = NonNegative(
                finalReserveRounds,
                nameof(finalReserveRounds));
        }

        public string AmmoTypeId { get; }
        public int Reloads { get; }
        public int RoundsSpent { get; }
        public int RoundsReloaded { get; }
        public int FinalLoadedRounds { get; }
        public int FinalReserveRounds { get; }

        private static int NonNegative(int value, string name)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public sealed class GameplayBattleScoreboard
    {
        public GameplayBattleScoreboard(
            int decisions,
            int transitions,
            int turnsCompleted,
            int attacks,
            int hits,
            int wounds,
            int explosiveThrows,
            int concussiveTargets,
            int fireDeployments,
            int droneMoves,
            int droneAttacks,
            int reloads,
            int roundsSpent,
            int roundsReloaded,
            int finalLoadedRounds,
            int finalReserveRounds,
            IEnumerable<GameplayBattleActorScore> actors,
            IEnumerable<GameplayBattleAmmunitionScore> ammunition)
        {
            Decisions = NonNegative(decisions, nameof(decisions));
            Transitions = NonNegative(transitions, nameof(transitions));
            TurnsCompleted = NonNegative(
                turnsCompleted,
                nameof(turnsCompleted));
            Attacks = NonNegative(attacks, nameof(attacks));
            Hits = NonNegative(hits, nameof(hits));
            Wounds = NonNegative(wounds, nameof(wounds));
            ExplosiveThrows = NonNegative(
                explosiveThrows,
                nameof(explosiveThrows));
            ConcussiveTargets = NonNegative(
                concussiveTargets,
                nameof(concussiveTargets));
            FireDeployments = NonNegative(
                fireDeployments,
                nameof(fireDeployments));
            DroneMoves = NonNegative(droneMoves, nameof(droneMoves));
            DroneAttacks = NonNegative(droneAttacks, nameof(droneAttacks));
            Reloads = NonNegative(reloads, nameof(reloads));
            RoundsSpent = NonNegative(roundsSpent, nameof(roundsSpent));
            RoundsReloaded = NonNegative(
                roundsReloaded,
                nameof(roundsReloaded));
            FinalLoadedRounds = NonNegative(
                finalLoadedRounds,
                nameof(finalLoadedRounds));
            FinalReserveRounds = NonNegative(
                finalReserveRounds,
                nameof(finalReserveRounds));
            var copied = new List<GameplayBattleActorScore>(actors
                ?? throw new ArgumentNullException(nameof(actors)));
            copied.Sort((left, right) => StringComparer.Ordinal.Compare(
                left?.ActorId,
                right?.ActorId));
            for (int index = 0; index < copied.Count; index++)
            {
                if (copied[index] == null)
                    throw new ArgumentException(
                        "Battle scoreboard actors cannot be null.",
                        nameof(actors));
                if (index > 0 && string.Equals(
                        copied[index - 1].ActorId,
                        copied[index].ActorId,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Battle scoreboard actors must be unique.",
                        nameof(actors));
            }
            Actors = copied.AsReadOnly();
            var copiedAmmunition = new List<GameplayBattleAmmunitionScore>(
                ammunition ?? throw new ArgumentNullException(
                    nameof(ammunition)));
            copiedAmmunition.Sort((left, right) => StringComparer.Ordinal
                .Compare(left?.AmmoTypeId, right?.AmmoTypeId));
            for (int index = 0; index < copiedAmmunition.Count; index++)
            {
                if (copiedAmmunition[index] == null)
                    throw new ArgumentException(
                        "Battle scoreboard ammunition entries cannot be null.",
                        nameof(ammunition));
                if (index > 0 && string.Equals(
                        copiedAmmunition[index - 1].AmmoTypeId,
                        copiedAmmunition[index].AmmoTypeId,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Battle scoreboard ammunition types must be unique.",
                        nameof(ammunition));
            }
            Ammunition = copiedAmmunition.AsReadOnly();
        }

        public int Decisions { get; }
        public int Transitions { get; }
        public int TurnsCompleted { get; }
        public int Attacks { get; }
        public int Hits { get; }
        public int Wounds { get; }
        public int ExplosiveThrows { get; }
        public int ConcussiveTargets { get; }
        public int FireDeployments { get; }
        public int DroneMoves { get; }
        public int DroneAttacks { get; }
        public int Reloads { get; }
        public int RoundsSpent { get; }
        public int RoundsReloaded { get; }
        public int FinalLoadedRounds { get; }
        public int FinalReserveRounds { get; }
        public IReadOnlyList<GameplayBattleActorScore> Actors { get; }
        public IReadOnlyList<GameplayBattleAmmunitionScore> Ammunition { get; }

        private static int NonNegative(int value, string name)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public sealed class GameplayBattleArtifactContent
    {
        public GameplayBattleArtifactContent(
            int numericPolicyVersion,
            GameplayExecutionIdentity executionIdentity,
            GameplayBattleArtifactProvenance provenance,
            string initialStateHash,
            string initialStateCanonical,
            IEnumerable<GameplayBattleArtifactTransition> transitions,
            IEnumerable<GameplayBattleArtifactDecision> decisions,
            GameplayBattleArtifactTerminal terminal,
            GameplayBattleScoreboard scoreboard)
        {
            if (numericPolicyVersion <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(numericPolicyVersion));
            NumericPolicyVersion = numericPolicyVersion;
            ExecutionIdentity = executionIdentity
                ?? throw new ArgumentNullException(nameof(executionIdentity));
            Provenance = provenance ?? throw new ArgumentNullException(
                nameof(provenance));
            InitialStateHash = GameplayContentIdentity.RequireDigest(
                initialStateHash,
                nameof(initialStateHash));
            InitialStateCanonical = string.IsNullOrWhiteSpace(
                    initialStateCanonical)
                ? throw new ArgumentException(
                    "Initial canonical state JSON cannot be empty.",
                    nameof(initialStateCanonical))
                : initialStateCanonical;
            Transitions = new List<GameplayBattleArtifactTransition>(
                transitions ?? throw new ArgumentNullException(
                    nameof(transitions))).AsReadOnly();
            Decisions = new List<GameplayBattleArtifactDecision>(decisions
                ?? throw new ArgumentNullException(nameof(decisions)))
                .AsReadOnly();
            Terminal = terminal ?? throw new ArgumentNullException(
                nameof(terminal));
            Scoreboard = scoreboard ?? throw new ArgumentNullException(
                nameof(scoreboard));
            ValidateContinuity();
        }

        public int NumericPolicyVersion { get; }
        public GameplayExecutionIdentity ExecutionIdentity { get; }
        public GameplayBattleArtifactProvenance Provenance { get; }
        public string InitialStateHash { get; }
        public string InitialStateCanonical { get; }
        public IReadOnlyList<GameplayBattleArtifactTransition> Transitions
        {
            get;
        }
        public IReadOnlyList<GameplayBattleArtifactDecision> Decisions { get; }
        public GameplayBattleArtifactTerminal Terminal { get; }
        public GameplayBattleScoreboard Scoreboard { get; }

        private void ValidateContinuity()
        {
            string previousHash = InitialStateHash;
            long priorSequence = -1L;
            var decisionTransitions = new GameplayBattleArtifactTransition[
                Decisions.Count];
            for (int index = 0; index < Transitions.Count; index++)
            {
                GameplayBattleArtifactTransition transition = Transitions[index]
                    ?? throw new ArgumentException(
                        "Battle artifact transitions cannot be null.",
                        nameof(Transitions));
                if (!string.Equals(
                        transition.PreviousStateHash,
                        previousHash,
                        StringComparison.Ordinal)
                    || (priorSequence >= 0L
                        && transition.Sequence != priorSequence + 1L))
                    throw new ArgumentException(
                        "Battle artifact transitions are not contiguous.",
                        nameof(Transitions));
                if (transition.DecisionIndex.HasValue)
                {
                    int decisionIndex = transition.DecisionIndex.Value;
                    if (decisionIndex >= Decisions.Count)
                        throw new ArgumentException(
                            "Battle artifact transitions cannot reference an absent decision.",
                            nameof(Transitions));
                    if (decisionTransitions[decisionIndex] != null)
                        throw new ArgumentException(
                            "Battle artifact decisions must identify exactly one transition.",
                            nameof(Transitions));
                    decisionTransitions[decisionIndex] = transition;
                }
                previousHash = transition.ResultingStateHash;
                priorSequence = transition.Sequence;
            }
            if (!string.Equals(
                    Terminal.FinalStateHash,
                    previousHash,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Battle artifact terminal hash does not end its trajectory.",
                    nameof(Terminal));
            if (Transitions.Count > 0
                && Terminal.TransitionSequence != priorSequence)
                throw new ArgumentException(
                    "Battle artifact terminal sequence does not identify its final transition.",
                    nameof(Terminal));
            for (int index = 0; index < Decisions.Count; index++)
            {
                GameplayBattleArtifactDecision decision = Decisions[index]
                    ?? throw new ArgumentException(
                        "Battle artifact decisions cannot be null.",
                        nameof(Decisions));
                if (decision.DecisionIndex != index)
                    throw new ArgumentException(
                        "Battle artifact decision indexes must be contiguous.",
                        nameof(Decisions));
                GameplayBattleArtifactTransition transition =
                    decisionTransitions[index]
                    ?? throw new ArgumentException(
                        "Battle artifact decisions must identify an artifact transition.",
                        nameof(Decisions));
                if (decision.TransitionSequence != transition.Sequence
                    || !string.Equals(
                        decision.ActorId,
                        transition.ActorId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        decision.PreviousStateHash,
                        transition.PreviousStateHash,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        decision.TransitionPayloadDigest,
                        transition.TransitionPayloadDigest,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        decision.ResultingStateHash,
                        transition.ResultingStateHash,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Battle artifact decisions do not match their transitions.",
                        nameof(Decisions));
            }
            if (Scoreboard.Decisions != Decisions.Count
                || Scoreboard.Transitions != Transitions.Count)
                throw new ArgumentException(
                    "Battle artifact scoreboard counts do not match its trajectory.",
                    nameof(Scoreboard));
        }
    }

    public sealed class GameplayBattleArtifact
    {
        public const int CurrentSchemaVersion = 3;
        public const string FormatId = "grit-gud-battle-artifact";

        public GameplayBattleArtifact(
            int schemaVersion,
            string artifactId,
            GameplayBattleArtifactContent content)
        {
            if (schemaVersion != CurrentSchemaVersion)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            SchemaVersion = schemaVersion;
            ArtifactId = GameplayContentIdentity.RequireDigest(
                artifactId,
                nameof(artifactId));
            Content = content ?? throw new ArgumentNullException(
                nameof(content));
            string actual = GameplayCanonicalValueDigest.Calculate(content);
            if (!string.Equals(ArtifactId, actual, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Battle artifact identifier does not match its content.",
                    nameof(artifactId));
        }

        public int SchemaVersion { get; }
        public string ArtifactId { get; }
        public GameplayBattleArtifactContent Content { get; }

        public string ToPortableJson() => GameplayBattleArtifactCodec.Format(
            this);
    }

    public static class GameplayBattleArtifactFactory
    {
        public static GameplayBattleArtifact Create(
            GameplayBattleRunResult run,
            GameplayBattleArtifactProvenance provenance)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (provenance == null) throw new ArgumentNullException(
                nameof(provenance));
            var replay = new GameplaySemanticReplayTimeline(
                run.InitialState,
                run.CreateTrajectory(),
                GameplaySimulationReducers.CreateCurrent());
            if (!string.Equals(
                    replay.FinalState.CanonicalHash,
                    run.FinalState.CanonicalHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Artifact creation requires an exact reducer replay.");
            var transitions = new List<GameplayBattleArtifactTransition>(
                run.Transitions.Count);
            for (int index = 0; index < run.Transitions.Count; index++)
            {
                GameplayBattleTransitionRecord source = run.Transitions[index];
                GameplaySemanticReplayFrame frame = replay.Frames[index];
                var eventPayloads = new List<string>(
                    source.DomainEvents.Count);
                foreach (GameplayDomainEvent domainEvent in source.DomainEvents)
                    eventPayloads.Add(GameplayReproBundleFormatter
                        .FormatCanonicalValue(domainEvent));
                transitions.Add(new GameplayBattleArtifactTransition(
                    source.Transition.Identity.Sequence,
                    source.Transition.Identity.Kind,
                    source.Transition.Identity.ActorId,
                    source.Transition.Identity.SubjectId,
                    source.Transition.PreviousStateHash,
                    source.Step.ResultingStateHash,
                    source.Step.TransitionPayloadDigest,
                    GameplayReproBundleFormatter.FormatCanonicalValue(
                        source.Transition),
                    source.DecisionIndex,
                    source.Step.DomainEventTypes,
                    source.DomainEventPayloadDigests,
                    eventPayloads,
                    GameplayReproBundleFormatter.FormatCanonicalValue(
                        frame.Resulting)));
            }
            var decisions = new List<GameplayBattleArtifactDecision>(
                run.Decisions.Count);
            foreach (GameplayBattleDecisionRecord decision in run.Decisions)
                decisions.Add(new GameplayBattleArtifactDecision(
                    decision.DecisionIndex,
                    decision.PolicyId,
                    decision.PolicyVersion,
                    decision.ActorId,
                    decision.PreviousStateHash,
                    decision.CandidateSetDigest,
                    decision.CandidateIds,
                    decision.LegalCandidateIds,
                    decision.SelectedCandidateId,
                    decision.SelectionReason,
                    decision.Score,
                    decision.ScoreComponents,
                    decision.TransitionSequence,
                    decision.TransitionPayloadDigest,
                    decision.ResultingStateHash));
            GameplayBattleTerminalResult terminal = run.Terminal;
            var content = new GameplayBattleArtifactContent(
                GameplayNumericPolicy.CurrentVersion,
                run.ExecutionIdentity,
                provenance,
                run.InitialState.CanonicalHash,
                GameplayReproBundleFormatter.FormatCanonicalValue(
                    run.InitialState),
                transitions,
                decisions,
                new GameplayBattleArtifactTerminal(
                    terminal.Kind,
                    terminal.TransitionSequence,
                    terminal.FinalStateHash,
                    terminal.CapablePartyActorIds,
                    terminal.CapableHostileActorIds,
                    terminal.FailureKind,
                    terminal.FailureMessage),
                GameplayBattleScoreboardBuilder.Build(run));
            return new GameplayBattleArtifact(
                GameplayBattleArtifact.CurrentSchemaVersion,
                GameplayCanonicalValueDigest.Calculate(content),
                content);
        }
    }

    /// <summary>
    /// Verifies a fresh permanent-run result against every authoritative field
    /// in a strict artifact while retaining only one additional canonical state
    /// string at a time. The returned timeline is the same exact reducer replay
    /// consumed by live presentation.
    /// </summary>
    public static class GameplayBattleArtifactVerifier
    {
        public static GameplaySemanticReplayTimeline VerifyRun(
            GameplayBattleRunResult run,
            GameplayBattleArtifact expected)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (expected == null) throw new ArgumentNullException(
                nameof(expected));
            GameplayBattleArtifactContent content = expected.Content;
            Require(
                run.ExecutionIdentity.HasSameIdentity(
                    content.ExecutionIdentity),
                "execution identity");
            Require(
                content.NumericPolicyVersion
                    == GameplayNumericPolicy.CurrentVersion,
                "numeric policy version");
            Require(
                string.Equals(
                    run.InitialState.CanonicalHash,
                    content.InitialStateHash,
                    StringComparison.Ordinal),
                "initial state hash");
            RequireCanonical(
                run.InitialState,
                content.InitialStateCanonical,
                "initial state");
            Require(
                run.Transitions.Count == content.Transitions.Count,
                "transition count");
            Require(
                run.Decisions.Count == content.Decisions.Count,
                "decision count");

            var replay = new GameplaySemanticReplayTimeline(
                run.InitialState,
                run.CreateTrajectory(),
                GameplaySimulationReducers.CreateCurrent());
            for (int index = 0; index < run.Transitions.Count; index++)
            {
                GameplayBattleTransitionRecord actual = run.Transitions[index];
                GameplayBattleArtifactTransition recorded = content
                    .Transitions[index];
                GameplayTransitionIdentity identity = actual.Transition
                    .Identity;
                string path = "transition[" + index + "]";
                Require(identity.Sequence == recorded.Sequence,
                    path + ".sequence");
                RequireEqual(identity.Kind, recorded.Kind,
                    path + ".kind");
                RequireEqual(identity.ActorId, recorded.ActorId,
                    path + ".actor");
                RequireEqual(identity.SubjectId, recorded.SubjectId,
                    path + ".subject");
                RequireEqual(
                    actual.Transition.PreviousStateHash,
                    recorded.PreviousStateHash,
                    path + ".previous-state");
                RequireEqual(
                    actual.Step.ResultingStateHash,
                    recorded.ResultingStateHash,
                    path + ".resulting-state");
                RequireEqual(
                    actual.Step.TransitionPayloadDigest,
                    recorded.TransitionPayloadDigest,
                    path + ".digest");
                Require(
                    actual.DecisionIndex == recorded.DecisionIndex,
                    path + ".decision-index");
                RequireCanonical(
                    actual.Transition,
                    recorded.TransitionCanonical,
                    path + ".canonical-transition");
                Require(
                    actual.DomainEvents.Count
                        == recorded.DomainEventTypes.Count,
                    path + ".event-count");
                for (int eventIndex = 0;
                    eventIndex < actual.DomainEvents.Count;
                    eventIndex++)
                {
                    GameplayDomainEvent domainEvent = actual.DomainEvents[
                        eventIndex];
                    string eventPath = path + ".event[" + eventIndex + "]";
                    RequireEqual(
                        domainEvent.EventType,
                        recorded.DomainEventTypes[eventIndex],
                        eventPath + ".type");
                    RequireEqual(
                        actual.DomainEventPayloadDigests[eventIndex],
                        recorded.DomainEventPayloadDigests[eventIndex],
                        eventPath + ".digest");
                    RequireCanonical(
                        domainEvent,
                        recorded.DomainEventPayloadsCanonical[eventIndex],
                        eventPath + ".canonical");
                }
                RequireCanonical(
                    replay.Frames[index].Resulting,
                    recorded.ResultingStateCanonical,
                    path + ".canonical-result");
            }

            for (int index = 0; index < run.Decisions.Count; index++)
            {
                GameplayBattleDecisionRecord source = run.Decisions[index];
                var actual = new GameplayBattleArtifactDecision(
                    source.DecisionIndex,
                    source.PolicyId,
                    source.PolicyVersion,
                    source.ActorId,
                    source.PreviousStateHash,
                    source.CandidateSetDigest,
                    source.CandidateIds,
                    source.LegalCandidateIds,
                    source.SelectedCandidateId,
                    source.SelectionReason,
                    source.Score,
                    source.ScoreComponents,
                    source.TransitionSequence,
                    source.TransitionPayloadDigest,
                    source.ResultingStateHash);
                RequireCanonical(
                    actual,
                    GameplayReproBundleFormatter.FormatCanonicalValue(
                        content.Decisions[index]),
                    "decision[" + index + "]");
            }

            GameplayBattleTerminalResult terminal = run.Terminal;
            var actualTerminal = new GameplayBattleArtifactTerminal(
                terminal.Kind,
                terminal.TransitionSequence,
                terminal.FinalStateHash,
                terminal.CapablePartyActorIds,
                terminal.CapableHostileActorIds,
                terminal.FailureKind,
                terminal.FailureMessage);
            RequireCanonical(
                actualTerminal,
                GameplayReproBundleFormatter.FormatCanonicalValue(
                    content.Terminal),
                "terminal");
            RequireCanonical(
                GameplayBattleScoreboardBuilder.Build(run),
                GameplayReproBundleFormatter.FormatCanonicalValue(
                    content.Scoreboard),
                "scoreboard");
            RequireEqual(
                replay.FinalState.CanonicalHash,
                content.Terminal.FinalStateHash,
                "replay final state");
            return replay;
        }

        private static void RequireCanonical(
            object actual,
            string expected,
            string path) => RequireEqual(
                GameplayReproBundleFormatter.FormatCanonicalValue(actual),
                expected,
                path);

        private static void RequireEqual(
            string actual,
            string expected,
            string path) => Require(
                string.Equals(actual, expected, StringComparison.Ordinal),
                path);

        private static void Require(bool condition, string path)
        {
            if (!condition)
                throw new InvalidOperationException(
                    "Fresh battle execution diverged from artifact at '"
                    + path + "'.");
        }
    }

    internal static class GameplayBattleScoreboardBuilder
    {
        public static GameplayBattleScoreboard Build(
            GameplayBattleRunResult run)
        {
            var actorScores = new Dictionary<string, MutableActorScore>(
                StringComparer.Ordinal);
            var ammunitionScores = new Dictionary<
                string,
                MutableAmmunitionScore>(StringComparer.Ordinal);
            foreach (GameplayActorSnapshot actor in run.InitialState.Session
                .Actors)
                actorScores.Add(actor.ActorId, new MutableActorScore(
                    actor.ActorId));
            foreach (GameplayBattleDecisionRecord decision in run.Decisions)
                actorScores[decision.ActorId].Decisions++;
            int turns = 0;
            int attacks = 0;
            int hits = 0;
            int wounds = 0;
            int throws = 0;
            int concussive = 0;
            int fires = 0;
            int droneMoves = 0;
            int droneAttacks = 0;
            int reloads = 0;
            int roundsSpent = 0;
            int roundsReloaded = 0;
            foreach (GameplayBattleTransitionRecord transition in
                run.Transitions)
            {
                if (transition.Transition.Payload
                    is GameplayEndTurnTransitionPayload)
                {
                    turns++;
                    actorScores[transition.Transition.Identity.ActorId]
                        .TurnsCompleted++;
                }
                foreach (GameplayDomainEvent domainEvent in
                    transition.DomainEvents)
                {
                    if (!(domainEvent is GameplayTransitionReducedEvent reduced))
                        continue;
                    object record = reduced.SemanticRecord;
                    if (record is MovementRouteRecord movement)
                    {
                        MutableActorScore score = actorScores[movement.ActorId];
                        score.Moves++;
                        score.MovementDistance += movement.TotalCost;
                    }
                    else if (record is DroneMoveRecord droneMove)
                    {
                        droneMoves++;
                        MutableActorScore score = actorScores[
                            droneMove.ControllerActorId];
                        score.DroneMoves++;
                    }
                    else if (record is DroneAttackRecord droneAttack)
                    {
                        droneAttacks++;
                        attacks++;
                        MutableActorScore score = actorScores[
                            droneAttack.ControllerActorId];
                        score.DroneAttacks++;
                        score.Attacks++;
                        if (droneAttack.Consequence
                            is AttackResolutionRecord resolution)
                            AddResolution(score, resolution, ref hits, ref wounds);
                    }
                    else if (record is ActorDroneAttackRecord actorDrone)
                    {
                        attacks++;
                        MutableActorScore score = actorScores[
                            actorDrone.AttackerId];
                        score.Attacks++;
                        if (actorDrone.Hit)
                        {
                            hits++;
                            score.Hits++;
                        }
                    }
                    else if (record is GameplayActionRecord action)
                    {
                        MutableActorScore score = actorScores[
                            action.Request.ActorId];
                        foreach (GameplayActionOutcome outcome in
                            action.Outcomes)
                        {
                            if (outcome is AttackResolvedActionOutcome attack)
                            {
                                attacks++;
                                score.Attacks++;
                                AddResolution(
                                    score,
                                    attack.Attack,
                                    ref hits,
                                    ref wounds);
                            }
                            else if (outcome
                                is ThrownExplosiveActionOutcome thrown)
                            {
                                throws++;
                                score.ExplosiveThrows++;
                                int affected = thrown.Record.ConcussiveEffects
                                    .Count;
                                concussive += affected;
                                score.ConcussiveTargets += affected;
                                if (thrown.Record.FireField != null)
                                {
                                    fires++;
                                    score.FireDeployments++;
                                }
                            }
                            else if (outcome
                                is AmmunitionSpentActionOutcome spent)
                            {
                                RegisterAmmunitionChange(
                                    spent.Change,
                                    score,
                                    ammunitionScores,
                                    ref reloads,
                                    ref roundsSpent,
                                    ref roundsReloaded);
                            }
                            else if (outcome
                                is WeaponReloadedActionOutcome reloaded)
                            {
                                RegisterAmmunitionChange(
                                    reloaded.Change,
                                    score,
                                    ammunitionScores,
                                    ref reloads,
                                    ref roundsSpent,
                                    ref roundsReloaded);
                            }
                        }
                    }
                }
            }
            var finalActors = new Dictionary<string, GameplayActorSnapshot>(
                StringComparer.Ordinal);
            foreach (GameplayActorSnapshot actor in run.FinalState.Session
                .Actors)
                finalActors.Add(actor.ActorId, actor);
            var actors = new List<GameplayBattleActorScore>(actorScores.Count);
            foreach (MutableActorScore score in actorScores.Values)
            {
                GameplayActorSnapshot final = finalActors[score.ActorId];
                actors.Add(score.Build(final));
            }
            int finalLoadedRounds = 0;
            int finalReserveRounds = 0;
            foreach (GameplayActorSnapshot actor in finalActors.Values)
            {
                foreach (WeaponMagazineSnapshot magazine in actor.Ammunition
                    .Magazines)
                {
                    MutableAmmunitionScore score = GetAmmunitionScore(
                        ammunitionScores,
                        magazine.AmmoTypeId);
                    score.FinalLoadedRounds += magazine.LoadedRounds;
                    finalLoadedRounds += magazine.LoadedRounds;
                }
                foreach (AmmunitionReserveSnapshot reserve in actor.Ammunition
                    .Reserves)
                {
                    MutableAmmunitionScore score = GetAmmunitionScore(
                        ammunitionScores,
                        reserve.AmmoTypeId);
                    score.FinalReserveRounds += reserve.Rounds;
                    finalReserveRounds += reserve.Rounds;
                }
            }
            var ammunition = new List<GameplayBattleAmmunitionScore>(
                ammunitionScores.Count);
            foreach (MutableAmmunitionScore score in ammunitionScores.Values)
                ammunition.Add(score.Build());
            return new GameplayBattleScoreboard(
                run.Decisions.Count,
                run.Transitions.Count,
                turns,
                attacks,
                hits,
                wounds,
                throws,
                concussive,
                fires,
                droneMoves,
                droneAttacks,
                reloads,
                roundsSpent,
                roundsReloaded,
                finalLoadedRounds,
                finalReserveRounds,
                actors,
                ammunition);
        }

        private static void RegisterAmmunitionChange(
            WeaponAmmunitionDelta change,
            MutableActorScore actor,
            IDictionary<string, MutableAmmunitionScore> ammunitionScores,
            ref int reloads,
            ref int roundsSpent,
            ref int roundsReloaded)
        {
            MutableAmmunitionScore ammunition = GetAmmunitionScore(
                ammunitionScores,
                change.AmmoTypeId);
            if (change.Kind == WeaponAmmunitionChangeKind.Spend)
            {
                actor.RoundsSpent += change.ChangedRounds;
                ammunition.RoundsSpent += change.ChangedRounds;
                roundsSpent += change.ChangedRounds;
                return;
            }

            actor.Reloads++;
            actor.RoundsReloaded += change.ChangedRounds;
            ammunition.Reloads++;
            ammunition.RoundsReloaded += change.ChangedRounds;
            reloads++;
            roundsReloaded += change.ChangedRounds;
        }

        private static MutableAmmunitionScore GetAmmunitionScore(
            IDictionary<string, MutableAmmunitionScore> scores,
            string ammoTypeId)
        {
            if (scores.TryGetValue(
                    ammoTypeId,
                    out MutableAmmunitionScore score))
                return score;
            score = new MutableAmmunitionScore(ammoTypeId);
            scores.Add(ammoTypeId, score);
            return score;
        }

        private static void AddResolution(
            MutableActorScore score,
            AttackResolutionRecord resolution,
            ref int hits,
            ref int wounds)
        {
            if (!resolution.Hit) return;
            hits++;
            score.Hits++;
            if (resolution.Wound == null) return;
            wounds++;
            score.WoundsDealt++;
        }

        private sealed class MutableActorScore
        {
            public MutableActorScore(string actorId) => ActorId = actorId;

            public string ActorId { get; }
            public int Decisions { get; set; }
            public int TurnsCompleted { get; set; }
            public int Moves { get; set; }
            public float MovementDistance { get; set; }
            public int Attacks { get; set; }
            public int Hits { get; set; }
            public int WoundsDealt { get; set; }
            public int ExplosiveThrows { get; set; }
            public int ConcussiveTargets { get; set; }
            public int FireDeployments { get; set; }
            public int DroneMoves { get; set; }
            public int DroneAttacks { get; set; }
            public int Reloads { get; set; }
            public int RoundsSpent { get; set; }
            public int RoundsReloaded { get; set; }

            public GameplayBattleActorScore Build(GameplayActorSnapshot final)
            {
                int loaded = 0;
                foreach (WeaponMagazineSnapshot magazine in final.Ammunition
                    .Magazines)
                    loaded += magazine.LoadedRounds;
                int reserve = 0;
                foreach (AmmunitionReserveSnapshot ammunitionReserve in final
                    .Ammunition.Reserves)
                    reserve += ammunitionReserve.Rounds;
                return new GameplayBattleActorScore(
                    ActorId,
                    Decisions,
                    TurnsCompleted,
                    Moves,
                    MovementDistance,
                    Attacks,
                    Hits,
                    WoundsDealt,
                    ExplosiveThrows,
                    ConcussiveTargets,
                    FireDeployments,
                    DroneMoves,
                    DroneAttacks,
                    Reloads,
                    RoundsSpent,
                    RoundsReloaded,
                    loaded,
                    reserve,
                    final.Wounds.WoundCount,
                    final.IsIncapacitated);
            }
        }

        private sealed class MutableAmmunitionScore
        {
            public MutableAmmunitionScore(string ammoTypeId) =>
                AmmoTypeId = ammoTypeId;

            public string AmmoTypeId { get; }
            public int Reloads { get; set; }
            public int RoundsSpent { get; set; }
            public int RoundsReloaded { get; set; }
            public int FinalLoadedRounds { get; set; }
            public int FinalReserveRounds { get; set; }

            public GameplayBattleAmmunitionScore Build() =>
                new GameplayBattleAmmunitionScore(
                    AmmoTypeId,
                    Reloads,
                    RoundsSpent,
                    RoundsReloaded,
                    FinalLoadedRounds,
                    FinalReserveRounds);
        }
    }
}
