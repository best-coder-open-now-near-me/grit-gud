using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum TurnReplayEventBoundary
    {
        Start = 0,
        End = 1,
    }

    public sealed class TurnReplayEventCrossing
    {
        public TurnReplayEventCrossing(
            TurnReplayTimedEvent timedEvent,
            TurnReplayEventBoundary boundary)
        {
            TimedEvent = timedEvent ?? throw new ArgumentNullException(
                nameof(timedEvent));
            if (!Enum.IsDefined(typeof(TurnReplayEventBoundary), boundary))
                throw new ArgumentOutOfRangeException(nameof(boundary));
            Boundary = boundary;
        }

        public TurnReplayTimedEvent TimedEvent { get; }

        public TurnReplayEventBoundary Boundary { get; }

        public float TimeSeconds => Boundary == TurnReplayEventBoundary.Start
            ? TimedEvent.StartSeconds
            : TimedEvent.EndSeconds;
    }

    /// <summary>
    /// Tracks continuous forward playback separately from direct seeks. A seek
    /// only establishes a new cursor; it can never emit a one-shot boundary.
    /// </summary>
    public sealed class TurnReplayEventCrossingDetector
    {
        private readonly TurnReplayEventTimeline timeline;
        private float previousSeconds;
        private bool includePreviousBoundary;

        public TurnReplayEventCrossingDetector(
            TurnReplayEventTimeline eventTimeline,
            float initialTimeSeconds = 0f)
        {
            timeline = eventTimeline ?? throw new ArgumentNullException(
                nameof(eventTimeline));
            Seek(initialTimeSeconds);
        }

        public float PreviousSeconds => previousSeconds;

        public void Seek(float timeSeconds)
        {
            previousSeconds = Clamp(timeSeconds);
            includePreviousBoundary = true;
        }

        public IReadOnlyList<TurnReplayEventCrossing> Advance(
            float timeSeconds)
        {
            float current = Clamp(timeSeconds);
            if (current <= previousSeconds)
            {
                Seek(current);
                return Array.Empty<TurnReplayEventCrossing>();
            }

            var crossings = new List<TurnReplayEventCrossing>();
            foreach (TurnReplayTimedEvent timedEvent in timeline.Events)
            {
                AddIfCrossed(
                    crossings,
                    timedEvent,
                    TurnReplayEventBoundary.Start,
                    timedEvent.StartSeconds,
                    current);
                AddIfCrossed(
                    crossings,
                    timedEvent,
                    TurnReplayEventBoundary.End,
                    timedEvent.EndSeconds,
                    current);
            }
            crossings.Sort(CompareCrossings);
            previousSeconds = current;
            includePreviousBoundary = false;
            return crossings.AsReadOnly();
        }

        private void AddIfCrossed(
            ICollection<TurnReplayEventCrossing> crossings,
            TurnReplayTimedEvent timedEvent,
            TurnReplayEventBoundary boundary,
            float boundarySeconds,
            float currentSeconds)
        {
            bool afterPrevious = includePreviousBoundary
                ? boundarySeconds >= previousSeconds
                : boundarySeconds > previousSeconds;
            if (afterPrevious && boundarySeconds <= currentSeconds)
            {
                crossings.Add(new TurnReplayEventCrossing(
                    timedEvent,
                    boundary));
            }
        }

        private float Clamp(float timeSeconds) => Math.Max(
            0f,
            Math.Min(timeline.TotalDurationSeconds, timeSeconds));

        private static int CompareCrossings(
            TurnReplayEventCrossing left,
            TurnReplayEventCrossing right)
        {
            int time = left.TimeSeconds.CompareTo(right.TimeSeconds);
            if (time != 0)
                return time;
            int sequence = left.TimedEvent.Entry.Sequence.CompareTo(
                right.TimedEvent.Entry.Sequence);
            if (sequence != 0)
                return sequence;
            return left.Boundary.CompareTo(right.Boundary);
        }
    }

    public enum TurnReplayActorActionKind
    {
        Attack = 0,
        Equipment = 1,
        Throw = 2,
        Displacement = 3,
        Reaction = 4,
        Pinned = 5,
        GetUp = 6,
    }

    public sealed class TurnReplayActorActionState
    {
        public TurnReplayActorActionState(
            string actorId,
            TurnReplayActorActionKind kind,
            long journalSequence,
            float normalizedProgress)
        {
            ActorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException(
                    "Replay actor-action states require an actor identifier.",
                    nameof(actorId))
                : actorId;
            if (!Enum.IsDefined(typeof(TurnReplayActorActionKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (journalSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(journalSequence));
            if (float.IsNaN(normalizedProgress)
                || float.IsInfinity(normalizedProgress))
                throw new ArgumentOutOfRangeException(nameof(normalizedProgress));
            Kind = kind;
            JournalSequence = journalSequence;
            NormalizedProgress = Math.Max(
                0f,
                Math.Min(1f, normalizedProgress));
        }

        public string ActorId { get; }

        public TurnReplayActorActionKind Kind { get; }

        public long JournalSequence { get; }

        public float NormalizedProgress { get; }
    }

    /// <summary>
    /// Projects seekable semantic actor action states from the frozen replay
    /// timeline. These states are presentation intent and never mutate combat.
    /// </summary>
    public static class TurnReplayActorActionProjector
    {
        public static IReadOnlyList<TurnReplayActorActionState> Project(
            TurnReplayEventTimeline timeline,
            float timeSeconds)
        {
            if (timeline == null)
                throw new ArgumentNullException(nameof(timeline));
            float time = Math.Max(
                0f,
                Math.Min(timeline.TotalDurationSeconds, timeSeconds));
            foreach (TurnReplayTimedEvent timedEvent in timeline.Events)
            {
                if (timedEvent.DurationSeconds <= 0f
                    || time < timedEvent.StartSeconds
                    || time >= timedEvent.EndSeconds)
                    continue;
                float progress = (time - timedEvent.StartSeconds)
                    / timedEvent.DurationSeconds;
                return Project(timedEvent.Entry, progress);
            }
            return Array.Empty<TurnReplayActorActionState>();
        }

        public static IReadOnlyList<TurnReplayActorActionState> Project(
            TurnReplayTimedEvent timedEvent,
            float normalizedProgress)
        {
            if (timedEvent == null)
                throw new ArgumentNullException(nameof(timedEvent));
            if (float.IsNaN(normalizedProgress)
                || float.IsInfinity(normalizedProgress))
                throw new ArgumentOutOfRangeException(nameof(normalizedProgress));
            return Project(
                timedEvent.Entry,
                Math.Max(0f, Math.Min(1f, normalizedProgress)));
        }

        private static IReadOnlyList<TurnReplayActorActionState> Project(
            GameplayJournalEntry entry,
            float progress)
        {
            var states = new List<TurnReplayActorActionState>();
            if (entry is ActionResolvedJournalEntry resolved)
            {
                ProjectAction(resolved, progress, states);
            }
            else if (entry is DisplacementResolvedJournalEntry displaced)
            {
                ActorPinTransition pin =
                    displaced.Displacement.PinTransition;
                Add(
                    states,
                    displaced.Displacement.Request.ActorId,
                    pin != null && pin.ReleasesPin
                        ? TurnReplayActorActionKind.GetUp
                        : TurnReplayActorActionKind.Displacement,
                    entry.Sequence,
                    progress);
                if (displaced.Displacement.Succeeded
                    && displaced.Displacement.Request.SubjectKind
                        == DisplacementSubjectKind.Combatant)
                {
                    Add(
                        states,
                        displaced.Displacement.Request.SubjectId,
                        TurnReplayActorActionKind.Reaction,
                        entry.Sequence,
                        progress);
                }
                if (pin != null
                    && pin.EstablishesPin)
                {
                    Add(
                        states,
                        pin.ActorId,
                        TurnReplayActorActionKind.Pinned,
                        entry.Sequence,
                        progress);
                }
            }
            else if (entry is EmergencyReactionChangedJournalEntry reaction
                && reaction.Window.Status
                    == EmergencyReactionWindowStatus.Active)
            {
                foreach (string responderId in reaction.Window.ResponderIds)
                {
                    Add(
                        states,
                        responderId,
                        TurnReplayActorActionKind.Reaction,
                        entry.Sequence,
                        progress);
                }
            }
            return states.Count == 0
                ? Array.Empty<TurnReplayActorActionState>()
                : states.AsReadOnly();
        }

        private static void ProjectAction(
            ActionResolvedJournalEntry resolved,
            float progress,
            ICollection<TurnReplayActorActionState> states)
        {
            TurnReplayActorActionKind? primary = null;
            var reactions = new Dictionary<
                string,
                TurnReplayActorActionKind>(StringComparer.Ordinal);
            foreach (GameplayActionOutcome outcome in resolved.Action.Outcomes)
            {
                if (outcome is ThrownExplosiveActionOutcome)
                {
                    primary = TurnReplayActorActionKind.Throw;
                }
                else if (outcome is DisplacementActionOutcome displacement)
                {
                    ActorPinTransition pin =
                        displacement.Displacement.PinTransition;
                    primary = pin != null && pin.ReleasesPin
                        ? TurnReplayActorActionKind.GetUp
                        : TurnReplayActorActionKind.Displacement;
                    if (displacement.Displacement.Succeeded
                        && displacement.Displacement.Request.SubjectKind
                            == DisplacementSubjectKind.Combatant)
                    {
                        reactions[
                            displacement.Displacement.Request.SubjectId] =
                            TurnReplayActorActionKind.Reaction;
                    }
                    if (pin != null && pin.EstablishesPin)
                        reactions[pin.ActorId] =
                            TurnReplayActorActionKind.Pinned;
                }
                else if (outcome is AttackResolvedActionOutcome attack)
                {
                    primary ??= TurnReplayActorActionKind.Attack;
                    if (attack.Attack.Wound != null)
                        reactions[attack.Attack.TargetId] =
                            TurnReplayActorActionKind.Reaction;
                }
                else if (outcome is WeaponDischargedActionOutcome
                    || outcome is ProjectileLaunchedActionOutcome)
                {
                    primary ??= TurnReplayActorActionKind.Attack;
                }
                else if (outcome is EquipmentChangedActionOutcome)
                {
                    primary ??= TurnReplayActorActionKind.Equipment;
                }
            }

            if (primary.HasValue)
            {
                Add(
                    states,
                    resolved.Action.Request.ActorId,
                    primary.Value,
                    resolved.Sequence,
                    progress);
            }
            foreach (KeyValuePair<string, TurnReplayActorActionKind> reaction
                in reactions)
            {
                Add(
                    states,
                    reaction.Key,
                    reaction.Value,
                    resolved.Sequence,
                    progress);
            }
        }

        private static void Add(
            ICollection<TurnReplayActorActionState> states,
            string actorId,
            TurnReplayActorActionKind kind,
            long sequence,
            float progress) => states.Add(new TurnReplayActorActionState(
                actorId,
                kind,
                sequence,
                progress));
    }
}
