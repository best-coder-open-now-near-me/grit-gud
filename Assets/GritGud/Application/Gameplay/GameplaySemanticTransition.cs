using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public readonly struct GameplaySubjectReference : IEquatable<
        GameplaySubjectReference>
    {
        public GameplaySubjectReference(
            GameplaySemanticSubjectKind kind,
            string id)
        {
            if (!Enum.IsDefined(typeof(GameplaySemanticSubjectKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            Id = GameplayContentIdentity.RequireText(id, nameof(id));
        }

        public GameplaySemanticSubjectKind Kind { get; }
        public string Id { get; }

        public bool Equals(GameplaySubjectReference other) =>
            Kind == other.Kind
            && string.Equals(Id, other.Id, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is GameplaySubjectReference other && Equals(other);

        public override int GetHashCode() => unchecked(
            ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Id));
    }

    public sealed class GameplayEvidenceRecord
    {
        public GameplayEvidenceRecord(
            string evidenceType,
            long worldRevision,
            string evidenceDigest)
        {
            EvidenceType = GameplayContentIdentity.RequireText(
                evidenceType,
                nameof(evidenceType));
            if (worldRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(worldRevision));
            WorldRevision = worldRevision;
            EvidenceDigest = GameplayContentIdentity.RequireDigest(
                evidenceDigest,
                nameof(evidenceDigest));
        }

        public string EvidenceType { get; }
        public long WorldRevision { get; }
        public string EvidenceDigest { get; }
    }

    public abstract class GameplayTransitionPayload
    {
        protected GameplayTransitionPayload(
            GameplayCapabilityProfile profile,
            string actorId,
            string subjectId)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            ActorId = GameplayContentIdentity.RequireText(
                actorId,
                nameof(actorId));
            SubjectId = GameplayContentIdentity.RequireText(
                subjectId,
                nameof(subjectId));
            SubjectKind = GameplayCapabilityProfiles.GetSubjectKind(profile);
        }

        public GameplayCapabilityProfile Profile { get; }
        public string ActorId { get; }
        public string SubjectId { get; }
        public GameplaySemanticSubjectKind SubjectKind { get; }
    }

    public sealed class GameplaySemanticTransition
    {
        public GameplaySemanticTransition(
            GameplayTransitionIdentity identity,
            string previousStateHash,
            GameplayTransitionPayload payload,
            IEnumerable<GameplayEvidenceRecord> evidence = null)
        {
            if (identity.Sequence <= 0)
                throw new ArgumentException(
                    "Semantic transitions require a complete identity.",
                    nameof(identity));
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            if (!string.Equals(
                    identity.Kind,
                    payload.Profile.Capability.ToString(),
                    StringComparison.Ordinal)
                || !string.Equals(
                    identity.ActorId,
                    payload.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    identity.SubjectId,
                    payload.SubjectId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Transition identity must match its semantic payload.",
                    nameof(identity));
            Identity = identity;
            PreviousStateHash = GameplayContentIdentity.RequireDigest(
                previousStateHash,
                nameof(previousStateHash));
            Evidence = CopyEvidence(evidence);
        }

        public GameplayTransitionIdentity Identity { get; }
        public string PreviousStateHash { get; }
        public GameplayTransitionPayload Payload { get; }
        public GameplayCapabilityProfile Profile => Payload.Profile;
        public IReadOnlyList<GameplayEvidenceRecord> Evidence { get; }

        private static IReadOnlyList<GameplayEvidenceRecord> CopyEvidence(
            IEnumerable<GameplayEvidenceRecord> evidence)
        {
            var copy = new List<GameplayEvidenceRecord>(
                evidence ?? Array.Empty<GameplayEvidenceRecord>());
            foreach (GameplayEvidenceRecord item in copy)
                if (item == null)
                    throw new ArgumentException(
                        "Transition evidence cannot contain null entries.",
                        nameof(evidence));
            copy.Sort((left, right) =>
            {
                int comparison = StringComparer.Ordinal.Compare(
                    left.EvidenceType,
                    right.EvidenceType);
                return comparison != 0
                    ? comparison
                    : left.WorldRevision.CompareTo(right.WorldRevision);
            });
            for (int index = 0; index < copy.Count; index++)
            {
                if (index > 0 && string.Equals(
                    copy[index - 1].EvidenceType,
                    copy[index].EvidenceType,
                    StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"Transition evidence '{copy[index].EvidenceType}' is duplicated.",
                        nameof(evidence));
            }
            return copy.AsReadOnly();
        }
    }

    public sealed class GameplayCandidate
    {
        public GameplayCandidate(
            string candidateId,
            GameplayCapabilityProfile profile,
            string actorId,
            GameplaySubjectReference subject,
            object intent)
            : this(candidateId, profile, actorId, subject.Id, intent)
        {
            if (SubjectKind != subject.Kind)
                throw new ArgumentException(
                    "Candidate subjects must match the capability profile.",
                    nameof(subject));
        }

        public GameplayCandidate(
            string candidateId,
            GameplayCapabilityProfile profile,
            string actorId,
            string subjectId,
            object intent)
        {
            CandidateId = GameplayContentIdentity.RequireText(
                candidateId,
                nameof(candidateId));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            ActorId = GameplayContentIdentity.RequireText(
                actorId,
                nameof(actorId));
            SubjectId = GameplayContentIdentity.RequireText(
                subjectId,
                nameof(subjectId));
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            SubjectKind = GameplayCapabilityProfiles.GetSubjectKind(profile);
        }

        public string CandidateId { get; }
        public GameplayCapabilityProfile Profile { get; }
        public string ActorId { get; }
        public string SubjectId { get; }
        public GameplaySemanticSubjectKind SubjectKind { get; }
        public GameplaySubjectReference Subject => new GameplaySubjectReference(
            SubjectKind,
            SubjectId);
        public object Intent { get; }
    }

    public abstract class GameplayDomainEvent
    {
        protected GameplayDomainEvent(
            GameplayTransitionIdentity transition,
            string eventType,
            string subjectId)
        {
            Transition = transition;
            EventType = GameplayContentIdentity.RequireText(
                eventType,
                nameof(eventType));
            SubjectId = GameplayContentIdentity.RequireText(
                subjectId,
                nameof(subjectId));
        }

        public GameplayTransitionIdentity Transition { get; }
        public string EventType { get; }
        public string SubjectId { get; }
    }

    public sealed class GameplayTransitionReducedEvent : GameplayDomainEvent
    {
        public GameplayTransitionReducedEvent(
            GameplayTransitionIdentity transition,
            string subjectId,
            object semanticRecord)
            : base(transition, "transition-reduced", subjectId)
        {
            SemanticRecord = semanticRecord ?? throw new ArgumentNullException(
                nameof(semanticRecord));
        }

        public object SemanticRecord { get; }
    }

    public sealed class GameplayReductionResult
    {
        public GameplayReductionResult(
            GameplayCombatStateSnapshot previous,
            GameplayCombatStateSnapshot resulting,
            IEnumerable<GameplayDomainEvent> domainEvents)
        {
            Previous = previous ?? throw new ArgumentNullException(nameof(previous));
            Resulting = resulting ?? throw new ArgumentNullException(nameof(resulting));
            var events = new List<GameplayDomainEvent>(
                domainEvents ?? throw new ArgumentNullException(
                    nameof(domainEvents)));
            foreach (GameplayDomainEvent domainEvent in events)
                if (domainEvent == null)
                    throw new ArgumentException(
                        "Reduction events cannot contain null entries.",
                        nameof(domainEvents));
            DomainEvents = events.AsReadOnly();
        }

        public GameplayCombatStateSnapshot Previous { get; }
        public GameplayCombatStateSnapshot Resulting { get; }
        public IReadOnlyList<GameplayDomainEvent> DomainEvents { get; }
    }

    public interface IGameplaySemanticTransitionReducer
    {
        bool Supports(GameplayCapabilityProfile profile);

        GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition);
    }

    public sealed class GameplayTransitionReducerRegistry
    {
        private readonly List<IGameplaySemanticTransitionReducer> reducers =
            new List<IGameplaySemanticTransitionReducer>();

        public void Register(IGameplaySemanticTransitionReducer reducer)
        {
            if (reducer == null) throw new ArgumentNullException(nameof(reducer));
            reducers.Add(reducer);
        }

        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return Find(profile, throwIfMissing: false) != null;
        }

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            if (!string.Equals(
                state.CanonicalHash,
                transition.PreviousStateHash,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Semantic transition was prepared from a different state.");
            if (transition.Identity.Sequence
                != state.Session.LastTransitionSequence + 1L)
                throw new InvalidOperationException(
                    "Semantic transition is not the next transition sequence.");
            IGameplaySemanticTransitionReducer reducer = Find(
                transition.Profile,
                throwIfMissing: true);
            GameplayReductionResult result = reducer.Reduce(state, transition);
            if (result.Resulting.Session.LastTransitionSequence
                != transition.Identity.Sequence)
                throw new InvalidOperationException(
                    "Reducers must advance the canonical transition sequence exactly once.");
            return result;
        }

        private IGameplaySemanticTransitionReducer Find(
            GameplayCapabilityProfile profile,
            bool throwIfMissing)
        {
            IGameplaySemanticTransitionReducer match = null;
            foreach (IGameplaySemanticTransitionReducer reducer in reducers)
            {
                if (!reducer.Supports(profile)) continue;
                if (match != null)
                    throw new InvalidOperationException(
                        $"Capability '{profile.Signature}' has multiple reducers.");
                match = reducer;
            }
            if (match == null && throwIfMissing)
                throw new NotSupportedException(
                    $"Capability '{profile.Signature}' has no registered reducer.");
            return match;
        }
    }
}
