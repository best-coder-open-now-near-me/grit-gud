using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class TurnReplayTimedEvent
    {
        public TurnReplayTimedEvent(int segmentIndex, GameplayJournalEntry entry,
            float startSeconds, float durationSeconds)
        {
            SegmentIndex = segmentIndex;
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            StartSeconds = startSeconds;
            DurationSeconds = durationSeconds;
        }

        public int SegmentIndex { get; }
        public GameplayJournalEntry Entry { get; }
        public float StartSeconds { get; }
        public float DurationSeconds { get; }
        public float EndSeconds => StartSeconds + DurationSeconds;
    }

    /// <summary>Deterministic presentation clock derived only from journal evidence.</summary>
    public sealed class TurnReplayEventTimeline
    {
        private const float MinimumSegmentSeconds = 0.2f;
        private readonly IReadOnlyList<TurnReplayTimedEvent> events;
        private readonly IReadOnlyList<float> segmentStarts;
        private readonly IReadOnlyList<float> segmentDurations;

        public TurnReplayEventTimeline(TurnReplayWindow window)
        {
            Replay = window ?? throw new ArgumentNullException(nameof(window));
            var timedEvents = new List<TurnReplayTimedEvent>();
            var starts = new List<float>(window.Segments.Count);
            var durations = new List<float>(window.Segments.Count);
            float cursor = 0f;
            for (int segmentIndex = 0; segmentIndex < window.Segments.Count; segmentIndex++)
            {
                starts.Add(cursor);
                float segmentStart = cursor;
                foreach (GameplayJournalEntry entry in window.Segments[segmentIndex].Entries)
                {
                    float duration = GetDurationSeconds(entry);
                    timedEvents.Add(new TurnReplayTimedEvent(
                        segmentIndex, entry, cursor, duration));
                    cursor += duration;
                }
                float durationSeconds = Math.Max(MinimumSegmentSeconds, cursor - segmentStart);
                cursor = segmentStart + durationSeconds;
                durations.Add(durationSeconds);
            }
            events = timedEvents.AsReadOnly();
            segmentStarts = starts.AsReadOnly();
            segmentDurations = durations.AsReadOnly();
            TotalDurationSeconds = cursor;
        }

        public TurnReplayWindow Replay { get; }
        public IReadOnlyList<TurnReplayTimedEvent> Events => events;
        public IReadOnlyList<float> SegmentStarts => segmentStarts;
        public IReadOnlyList<float> SegmentDurations => segmentDurations;
        public float TotalDurationSeconds { get; }
        public float DefaultTimeSeconds => GetSegmentEndSeconds(
            Math.Min(Replay.DefaultPlayheadBoundary - 1, Replay.Segments.Count - 1));

        public float GetSegmentEndSeconds(int segmentIndex) =>
            segmentStarts[segmentIndex] + segmentDurations[segmentIndex];

        public float ToSegmentPlayhead(float timeSeconds)
        {
            float time = Math.Max(0f, Math.Min(TotalDurationSeconds, timeSeconds));
            if (time >= TotalDurationSeconds) return Replay.Segments.Count;
            for (int index = Replay.Segments.Count - 1; index >= 0; index--)
            {
                if (time < segmentStarts[index]) continue;
                return index + ((time - segmentStarts[index]) / segmentDurations[index]);
            }
            return 0f;
        }

        public int GetSegmentIndex(float timeSeconds) => Math.Min(
            Replay.Segments.Count - 1,
            (int)Math.Floor(ToSegmentPlayhead(timeSeconds)));

        public TurnReplayTimedEvent GetActiveEvent(float timeSeconds)
        {
            float time = Math.Max(0f, Math.Min(TotalDurationSeconds, timeSeconds));
            TurnReplayTimedEvent last = null;
            foreach (TurnReplayTimedEvent timedEvent in events)
            {
                if (timedEvent.StartSeconds > time) break;
                last = timedEvent;
                if (time < timedEvent.EndSeconds) return timedEvent;
            }
            return last;
        }

        public static float GetDurationSeconds(GameplayJournalEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry is MovementRouteCommittedJournalEntry movement)
                return Math.Max(
                    0.3f,
                    movement.Route.TotalPlaybackDurationSeconds);
            if (entry is DisplacementResolvedJournalEntry displacement)
                return Math.Max(0.3f,
                    displacement.Displacement.PreviousPosition.DistanceTo(
                        displacement.Displacement.ResultingPosition) / 5f);
            if (entry is ProjectileAdvancedJournalEntry projectile)
                return Math.Max(0.2f,
                    projectile.Advance.RequestedTurnTime * 0.65f);
            if (entry is ActionResolvedJournalEntry action)
                return GetActionDuration(action.Action);
            if (entry is VehicleMomentumResolvedJournalEntry) return 0.65f;
            if (entry is DestructibleDamagedJournalEntry) return 0.25f;
            if (entry is StanceChangedJournalEntry) return 0.3f;
            if (entry is EmergencyReactionChangedJournalEntry) return 0.2f;
            if (entry is TurnEndedJournalEntry) return 0.15f;
            if (entry is MovementBudgetSpentJournalEntry) return 0.1f;
            if (entry is MovementRouteCompletedJournalEntry
                || entry is EnemyDecisionCommittedJournalEntry) return 0f;
            return 0.1f;
        }

        private static float GetActionDuration(GameplayActionRecord action)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is ProjectileLaunchedActionOutcome) return 0.65f;
                if (outcome is WeaponDischargedActionOutcome) return 0.65f;
                if (outcome is AttackResolvedActionOutcome) return 0.8f;
                if (outcome is ThrownExplosiveActionOutcome) return 0.8f;
                if (outcome is DisplacementActionOutcome) return 0.75f;
                if (outcome is EquipmentChangedActionOutcome) return 0.4f;
            }
            return 0.35f;
        }
    }
}
