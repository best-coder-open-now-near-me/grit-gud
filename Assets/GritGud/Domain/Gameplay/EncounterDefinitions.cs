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
            IEnumerable<string> encounterParticipantIds = null,
            long lastTransitionSequence = 0L)
        {
            if (lastTransitionSequence < 0L)
                throw new ArgumentOutOfRangeException(nameof(lastTransitionSequence));
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
            LastTransitionSequence = lastTransitionSequence;
        }

        public IReadOnlyList<EnemyAwarenessSnapshot> Awareness => awareness;

        public IReadOnlyList<string> ParticipantIds => participantIds;

        public long LastTransitionSequence { get; }

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
            EnemyAwarenessSnapshot replacement,
            long? lastTransitionSequence = null) =>
            new GameplayEncounterStateSnapshot(
                ReplaceAwareness(replacement),
                participantIds,
                lastTransitionSequence ?? LastTransitionSequence);

        public GameplayEncounterStateSnapshot WithParticipants(
            IEnumerable<string> participants) =>
            new GameplayEncounterStateSnapshot(
                awareness,
                participants,
                LastTransitionSequence);

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

    /// <summary>
    /// Frozen sound-world evidence after Unity or a headless spatial query has
    /// accounted for distance and occlusion. The reducer never queries Physics.
    /// </summary>
    public sealed class EncounterSoundEvidence
    {
        public EncounterSoundEvidence(
            string sourceId,
            GameplayPosition origin,
            float audibility)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException(
                    "Sound evidence requires a stable source identifier.",
                    nameof(sourceId));
            if (float.IsNaN(audibility) || float.IsInfinity(audibility)
                || audibility < 0f || audibility > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(audibility));
            }
            SourceId = sourceId;
            Origin = origin;
            Audibility = audibility;
        }

        public string SourceId { get; }

        public GameplayPosition Origin { get; }

        public float Audibility { get; }
    }

    /// <summary>
    /// One portable perception sample for an enemy. Sight is an existing frozen
    /// target-exposure sample; sound is a separately frozen world query.
    /// </summary>
    public sealed class EncounterObservation
    {
        public EncounterObservation(
            string observerId,
            TargetExposureSnapshot sight = null,
            GameplayPosition? sightTargetPosition = null,
            EncounterSoundEvidence sound = null)
        {
            if (string.IsNullOrWhiteSpace(observerId))
                throw new ArgumentException(
                    "Encounter observations require an observer.",
                    nameof(observerId));
            if (sight != null
                && !string.Equals(sight.ObserverId, observerId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Sight evidence must belong to the observing actor.",
                    nameof(sight));
            }
            if ((sight == null) != !sightTargetPosition.HasValue)
            {
                throw new ArgumentException(
                    "Sight target position must accompany sight evidence.",
                    nameof(sightTargetPosition));
            }

            ObserverId = observerId;
            Sight = sight;
            SightTargetPosition = sightTargetPosition;
            Sound = sound;
        }

        public string ObserverId { get; }

        public TargetExposureSnapshot Sight { get; }

        public GameplayPosition? SightTargetPosition { get; }

        public EncounterSoundEvidence Sound { get; }

        public bool HasVisibleSight => Sight != null
            && Sight.VisibleSampleCount > 0;

        public bool HasAudibleSound => Sound != null && Sound.Audibility > 0f;
    }

    public sealed class EnemyAwarenessTransitionRecord
    {
        public EnemyAwarenessTransitionRecord(
            long sequence,
            string actorId,
            EnemyAwarenessSnapshot previous,
            EnemyAwarenessSnapshot resulting,
            EncounterObservation observation)
        {
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Awareness transitions require an actor.", nameof(actorId));
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (resulting == null) throw new ArgumentNullException(nameof(resulting));
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            if (!string.Equals(previous.ActorId, actorId, StringComparison.Ordinal)
                || !string.Equals(resulting.ActorId, actorId,
                    StringComparison.Ordinal)
                || !string.Equals(observation.ObserverId, actorId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Awareness transition identity must agree with its record.",
                    nameof(actorId));
            }

            Sequence = sequence;
            ActorId = actorId;
            Previous = previous;
            Resulting = resulting;
            Observation = observation;
        }

        public long Sequence { get; }

        public string ActorId { get; }

        public EnemyAwarenessSnapshot Previous { get; }

        public EnemyAwarenessSnapshot Resulting { get; }

        public EncounterObservation Observation { get; }
    }

    public sealed class PatrolAdvanceRecord
    {
        public PatrolAdvanceRecord(
            long sequence,
            string actorId,
            MovementRouteRecord route,
            int previousWaypointIndex,
            int resultingWaypointIndex)
        {
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Patrol advances require an actor.", nameof(actorId));
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (!string.Equals(route.ActorId, actorId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Patrol routes must belong to their advancing actor.",
                    nameof(route));
            }
            if (previousWaypointIndex < 0 || resultingWaypointIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(previousWaypointIndex));

            Sequence = sequence;
            ActorId = actorId;
            Route = route;
            PreviousWaypointIndex = previousWaypointIndex;
            ResultingWaypointIndex = resultingWaypointIndex;
        }

        public long Sequence { get; }

        public string ActorId { get; }

        public MovementRouteRecord Route { get; }

        public int PreviousWaypointIndex { get; }

        public int ResultingWaypointIndex { get; }
    }

    public static class EncounterAwarenessRules
    {
        public static EnemyAwarenessSnapshot Evaluate(
            EnemyBehaviorDefinition behavior,
            GameplayActorPose observerPose,
            EnemyAwarenessSnapshot previous,
            EncounterObservation observation)
        {
            if (behavior == null) throw new ArgumentNullException(nameof(behavior));
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            if (!string.Equals(previous.ActorId, observation.ObserverId,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Observation must evaluate the matching awareness state.",
                    nameof(observation));
            }

            int gain = 0;
            string lastKnownId = previous.LastKnownHostileId;
            GameplayPosition? lastKnownPosition = previous.LastKnownHostilePosition;
            if (observation.HasVisibleSight)
            {
                float distance = observerPose.Position.DistanceTo(
                    observation.SightTargetPosition.Value);
                if (distance <= behavior.PerceptionRange + 0.0001f
                    && CalculateViewAngle(
                        observerPose,
                        observation.SightTargetPosition.Value)
                        <= (behavior.ViewAngleDegrees * 0.5f) + 0.0001f)
                {
                    gain = checked(gain + Scale(
                        behavior.AwarenessPolicy.SightSuspicionGain,
                        observation.Sight.VisibleFraction));
                    lastKnownId = observation.Sight.TargetId;
                    lastKnownPosition = observation.SightTargetPosition;
                }
            }
            if (observation.HasAudibleSound)
            {
                float distance = observerPose.Position.DistanceTo(
                    observation.Sound.Origin);
                if (distance <= behavior.AwarenessPolicy.HearingRange + 0.0001f)
                {
                    gain = checked(gain + Scale(
                        behavior.AwarenessPolicy.SoundSuspicionGain,
                        observation.Sound.Audibility));
                    if (string.IsNullOrEmpty(lastKnownId)
                        || !observation.HasVisibleSight)
                    {
                        lastKnownId = observation.Sound.SourceId;
                        lastKnownPosition = observation.Sound.Origin;
                    }
                }
            }

            int suspicion = gain == 0
                ? Math.Max(0, previous.Suspicion
                    - behavior.AwarenessPolicy.SuspicionDecayPerTick)
                : Math.Min(100, checked(previous.Suspicion + gain));
            EncounterAwarenessState state = suspicion >= behavior.AwarenessPolicy
                .AlertThreshold
                    ? EncounterAwarenessState.Alert
                    : suspicion > 0
                        ? EncounterAwarenessState.Suspicious
                        : EncounterAwarenessState.Unaware;
            return new EnemyAwarenessSnapshot(
                previous.ActorId,
                state,
                suspicion,
                lastKnownId,
                lastKnownPosition,
                previous.PatrolWaypointIndex);
        }

        private static int Scale(int value, float factor) =>
            (int)Math.Round(
                value * Math.Max(0f, Math.Min(1f, factor)),
                MidpointRounding.AwayFromZero);

        private static float CalculateViewAngle(
            GameplayActorPose observer,
            GameplayPosition target)
        {
            float deltaX = target.X - observer.Position.X;
            float deltaZ = target.Z - observer.Position.Z;
            if ((deltaX * deltaX) + (deltaZ * deltaZ) <= 0.000001f)
                return 0f;
            float bearing = (float)(Math.Atan2(deltaX, deltaZ)
                * (180d / Math.PI));
            float delta = ((bearing - observer.FacingDegrees + 540f) % 360f)
                - 180f;
            return Math.Abs(delta);
        }
    }
}
