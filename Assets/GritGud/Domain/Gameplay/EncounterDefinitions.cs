using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    /// <summary>
    /// Authored sensing policy for an AI-controlled actor. Values are expressed
    /// in whole suspicion points so an observation has the same outcome in live,
    /// replay, and headless execution.
    /// </summary>
    public sealed class EncounterAwarenessPolicyDefinition
    {
        public EncounterAwarenessPolicyDefinition(
            float hearingRange,
            int sightSuspicionGain,
            int soundSuspicionGain,
            int suspicionDecayPerTick,
            int alertThreshold)
        {
            RequireFinitePositive(hearingRange, nameof(hearingRange));
            RequirePercent(sightSuspicionGain, nameof(sightSuspicionGain));
            RequirePercent(soundSuspicionGain, nameof(soundSuspicionGain));
            RequirePercent(suspicionDecayPerTick, nameof(suspicionDecayPerTick));
            if (alertThreshold <= 0 || alertThreshold > 100)
                throw new ArgumentOutOfRangeException(nameof(alertThreshold));

            HearingRange = hearingRange;
            SightSuspicionGain = sightSuspicionGain;
            SoundSuspicionGain = soundSuspicionGain;
            SuspicionDecayPerTick = suspicionDecayPerTick;
            AlertThreshold = alertThreshold;
        }

        public float HearingRange { get; }

        public int SightSuspicionGain { get; }

        public int SoundSuspicionGain { get; }

        public int SuspicionDecayPerTick { get; }

        public int AlertThreshold { get; }

        internal static EncounterAwarenessPolicyDefinition CreateLegacyDefault() =>
            new EncounterAwarenessPolicyDefinition(
                hearingRange: 12f,
                sightSuspicionGain: 100,
                soundSuspicionGain: 50,
                suspicionDecayPerTick: 10,
                alertThreshold: 100);

        private static void RequireFinitePositive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(name);
        }

        private static void RequirePercent(int value, string name)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>
    /// Portable authored patrol points. Unity validates the live route before it
    /// is presented; canonical state records the accepted advance.
    /// </summary>
    public sealed class PatrolRouteDefinition
    {
        private readonly IReadOnlyList<GameplayPosition> waypoints;

        public PatrolRouteDefinition(
            IEnumerable<GameplayPosition> patrolWaypoints,
            bool loops)
        {
            if (patrolWaypoints == null)
                throw new ArgumentNullException(nameof(patrolWaypoints));
            var copy = new List<GameplayPosition>(patrolWaypoints);
            if (copy.Count < 2)
                throw new ArgumentException(
                    "Patrol routes require at least two waypoints.",
                    nameof(patrolWaypoints));
            for (int index = 1; index < copy.Count; index++)
            {
                if (copy[index - 1].DistanceTo(copy[index]) <= 0.0001f)
                {
                    throw new ArgumentException(
                        "Adjacent patrol waypoints must not overlap.",
                        nameof(patrolWaypoints));
                }
            }

            waypoints = copy.AsReadOnly();
            Loops = loops;
        }

        public IReadOnlyList<GameplayPosition> Waypoints => waypoints;

        public bool Loops { get; }

        public GameplayPosition GetWaypoint(int index)
        {
            if (index < 0 || index >= waypoints.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return waypoints[index];
        }

        public int GetNextWaypointIndex(int currentIndex)
        {
            if (currentIndex < 0 || currentIndex >= waypoints.Count)
                throw new ArgumentOutOfRangeException(nameof(currentIndex));
            if (currentIndex + 1 < waypoints.Count)
                return currentIndex + 1;
            return Loops ? 0 : currentIndex;
        }
    }

    public enum EncounterAwarenessState
    {
        Unaware = 0,
        Suspicious = 1,
        Alert = 2,
    }

    /// <summary>
    /// Immutable canonical awareness for one actor. Last-known evidence is kept
    /// even after sight is lost, so later investigation policy never needs to
    /// query the live world again.
    /// </summary>
    public sealed class EnemyAwarenessSnapshot
    {
        public EnemyAwarenessSnapshot(
            string actorId,
            EncounterAwarenessState state,
            int suspicion,
            string lastKnownHostileId = null,
            GameplayPosition? lastKnownHostilePosition = null,
            int patrolWaypointIndex = 0)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Awareness requires an actor identifier.", nameof(actorId));
            if (!Enum.IsDefined(typeof(EncounterAwarenessState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (suspicion < 0 || suspicion > 100)
                throw new ArgumentOutOfRangeException(nameof(suspicion));
            if (patrolWaypointIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(patrolWaypointIndex));
            bool hasTargetId = !string.IsNullOrWhiteSpace(lastKnownHostileId);
            if (hasTargetId != lastKnownHostilePosition.HasValue)
            {
                throw new ArgumentException(
                    "Last-known hostile identity and position must be supplied together.",
                    nameof(lastKnownHostileId));
            }

            ActorId = actorId;
            State = state;
            Suspicion = suspicion;
            LastKnownHostileId = lastKnownHostileId ?? string.Empty;
            LastKnownHostilePosition = lastKnownHostilePosition;
            PatrolWaypointIndex = patrolWaypointIndex;
        }

        public string ActorId { get; }

        public EncounterAwarenessState State { get; }

        public int Suspicion { get; }

        public string LastKnownHostileId { get; }

        public GameplayPosition? LastKnownHostilePosition { get; }

        public int PatrolWaypointIndex { get; }
    }

    public sealed class GameplayEncounterStateSnapshot
    {
        private readonly IReadOnlyList<EnemyAwarenessSnapshot> awareness;
        private readonly IReadOnlyList<string> participantIds;

        public GameplayEncounterStateSnapshot(
            IEnumerable<EnemyAwarenessSnapshot> awarenessSnapshots = null,
            IEnumerable<string> encounterParticipantIds = null)
        {
            var awarenessCopy = new List<EnemyAwarenessSnapshot>(
                awarenessSnapshots ?? Array.Empty<EnemyAwarenessSnapshot>());
            awarenessCopy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left?.ActorId,
                right?.ActorId));
            for (int index = 0; index < awarenessCopy.Count; index++)
            {
                EnemyAwarenessSnapshot value = awarenessCopy[index];
                if (value == null)
                    throw new ArgumentException(
                        "Awareness snapshots cannot contain null entries.",
                        nameof(awarenessSnapshots));
                if (index > 0 && string.Equals(
                        awarenessCopy[index - 1].ActorId,
                        value.ActorId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Awareness snapshots must be unique by actor.",
                        nameof(awarenessSnapshots));
                }
            }
            awareness = awarenessCopy.AsReadOnly();
            participantIds = CopyIds(encounterParticipantIds);
        }

        public IReadOnlyList<EnemyAwarenessSnapshot> Awareness => awareness;

        public IReadOnlyList<string> ParticipantIds => participantIds;

        public EnemyAwarenessSnapshot GetAwareness(string actorId)
        {
            foreach (EnemyAwarenessSnapshot value in awareness)
            {
                if (string.Equals(value.ActorId, actorId, StringComparison.Ordinal))
                    return value;
            }
            throw new KeyNotFoundException(
                $"Encounter awareness '{actorId}' was not found.");
        }

        public bool TryGetAwareness(
            string actorId,
            out EnemyAwarenessSnapshot value)
        {
            foreach (EnemyAwarenessSnapshot candidate in awareness)
            {
                if (string.Equals(candidate.ActorId, actorId,
                    StringComparison.Ordinal))
                {
                    value = candidate;
                    return true;
                }
            }
            value = null;
            return false;
        }

        public GameplayEncounterStateSnapshot WithAwareness(
            EnemyAwarenessSnapshot replacement) =>
            new GameplayEncounterStateSnapshot(
                ReplaceAwareness(replacement), participantIds);

        public GameplayEncounterStateSnapshot WithParticipants(
            IEnumerable<string> participants) =>
            new GameplayEncounterStateSnapshot(awareness, participants);

        private IEnumerable<EnemyAwarenessSnapshot> ReplaceAwareness(
            EnemyAwarenessSnapshot replacement)
        {
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));
            bool replaced = false;
            foreach (EnemyAwarenessSnapshot value in awareness)
            {
                if (string.Equals(value.ActorId, replacement.ActorId,
                    StringComparison.Ordinal))
                {
                    yield return replacement;
                    replaced = true;
                }
                else
                {
                    yield return value;
                }
            }
            if (!replaced)
            {
                throw new KeyNotFoundException(
                    $"Encounter awareness '{replacement.ActorId}' was not found.");
            }
        }

        private static IReadOnlyList<string> CopyIds(IEnumerable<string> values)
        {
            var copy = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(value) || !unique.Add(value))
                {
                    throw new ArgumentException(
                        "Encounter participants must be unique non-empty identifiers.",
                        nameof(values));
                }
                copy.Add(value);
            }
            return copy.AsReadOnly();
        }
    }
}
