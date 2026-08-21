using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayObservationSnapshot
    {
        public GameplayObservationSnapshot(
            string observerActorId,
            string authoritativeStateHash,
            long observationRevision,
            IEnumerable<string> observedActorIds)
        {
            ObserverActorId = GameplayContentIdentity.RequireText(
                observerActorId,
                nameof(observerActorId));
            AuthoritativeStateHash = GameplayContentIdentity.RequireDigest(
                authoritativeStateHash,
                nameof(authoritativeStateHash));
            if (observationRevision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(observationRevision));
            ObservationRevision = observationRevision;
            ObservedActorIds = CopyIds(observedActorIds);
        }

        public string ObserverActorId { get; }
        public string AuthoritativeStateHash { get; }
        public long ObservationRevision { get; }
        public IReadOnlyList<string> ObservedActorIds { get; }

        public bool ObservesActor(string actorId)
        {
            foreach (string observed in ObservedActorIds)
                if (string.Equals(observed, actorId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static GameplayObservationSnapshot FullState(
            string observerActorId,
            GameplayCombatStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var actorIds = new List<string>(state.Session.Actors.Count);
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
                actorIds.Add(actor.ActorId);
            return new GameplayObservationSnapshot(
                observerActorId,
                state.CanonicalHash,
                state.Session.Revision,
                actorIds);
        }

        private static IReadOnlyList<string> CopyIds(
            IEnumerable<string> actorIds)
        {
            if (actorIds == null)
                throw new ArgumentNullException(nameof(actorIds));
            var copy = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string actorId in actorIds)
            {
                string id = GameplayContentIdentity.RequireText(
                    actorId,
                    nameof(actorIds));
                if (!unique.Add(id))
                    throw new ArgumentException(
                        $"Observed actor '{id}' is duplicated.",
                        nameof(actorIds));
                copy.Add(id);
            }
            copy.Sort(StringComparer.Ordinal);
            return copy.AsReadOnly();
        }
    }

    public sealed class GameplayDecisionContext
    {
        public GameplayDecisionContext(
            GameplayCombatStateSnapshot state,
            GameplayObservationSnapshot observation)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Observation = observation ?? throw new ArgumentNullException(
                nameof(observation));
            if (!string.Equals(
                state.CanonicalHash,
                observation.AuthoritativeStateHash,
                StringComparison.Ordinal))
                throw new ArgumentException(
                    "Decision observations must describe the supplied canonical state.",
                    nameof(observation));
            if (!observation.ObservesActor(observation.ObserverActorId))
                throw new ArgumentException(
                    "A decision observation must include its observer.",
                    nameof(observation));
        }

        public GameplayCombatStateSnapshot State { get; }
        public GameplayObservationSnapshot Observation { get; }
        public string ActorId => Observation.ObserverActorId;
    }
}
