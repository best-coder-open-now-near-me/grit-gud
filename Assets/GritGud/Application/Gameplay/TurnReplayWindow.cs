using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public sealed class TurnReplaySegment
    {
        public TurnReplaySegment(
            long turnSequence,
            string actorId,
            IReadOnlyList<GameplayJournalEntry> entries)
        {
            if (turnSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(turnSequence));
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Replay segments require an actor identifier.",
                    nameof(actorId));
            TurnSequence = turnSequence;
            ActorId = actorId;
            Entries = CopyEntries(entries);
        }

        public long TurnSequence { get; }

        public string ActorId { get; }

        public IReadOnlyList<GameplayJournalEntry> Entries { get; }

        private static IReadOnlyList<GameplayJournalEntry> CopyEntries(
            IReadOnlyList<GameplayJournalEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            var copy = new List<GameplayJournalEntry>(entries.Count);
            for (int index = 0; index < entries.Count; index++)
                copy.Add(entries[index] ?? throw new ArgumentException(
                    "Replay segment entries cannot contain null.",
                    nameof(entries)));
            return copy.AsReadOnly();
        }
    }

    public sealed class TurnReplayWindow
    {
        public TurnReplayWindow(
            string actorId,
            IReadOnlyList<TurnReplaySegment> segments)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Replay windows require an actor identifier.",
                    nameof(actorId));
            if (segments == null || segments.Count == 0)
                throw new ArgumentException(
                    "Replay windows require at least one completed turn.",
                    nameof(segments));
            ActorId = actorId;
            var copy = new List<TurnReplaySegment>(segments.Count);
            for (int index = 0; index < segments.Count; index++)
                copy.Add(segments[index] ?? throw new ArgumentException(
                    "Replay windows cannot contain null segments.",
                    nameof(segments)));
            Segments = copy.AsReadOnly();
        }

        public string ActorId { get; }

        public IReadOnlyList<TurnReplaySegment> Segments { get; }

        public long EndJournalSequence =>
            Segments[Segments.Count - 1].Entries[
                Segments[Segments.Count - 1].Entries.Count - 1].Sequence;

        public bool IsAtJournalTip(GameplayJournal journal)
        {
            if (journal == null)
                throw new ArgumentNullException(nameof(journal));
            return journal.LastEntry != null
                && journal.LastEntry.Sequence == EndJournalSequence;
        }

        // Segment zero is the active character's optional previous-turn context.
        public int DefaultPlayheadBoundary => 1;
    }

    public static class TurnReplayWindowProjector
    {
        public static bool TryProject(
            GameplayJournal journal,
            string activePlayerActorId,
            out TurnReplayWindow window)
        {
            if (journal == null)
                throw new ArgumentNullException(nameof(journal));
            if (string.IsNullOrWhiteSpace(activePlayerActorId))
                throw new ArgumentException(
                    "Replay projection requires an active player actor.",
                    nameof(activePlayerActorId));

            IReadOnlyList<GameplayJournalEntry> entries = journal.Entries;
            var normalTurnEnds = new List<int>();
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index] is TurnEndedJournalEntry ended
                    && ended.Turn.Kind == GameplayTurnKind.Normal)
                {
                    normalTurnEnds.Add(index);
                }
            }

            int closingTurn = FindClosingTurn(
                entries,
                normalTurnEnds,
                activePlayerActorId);
            int anchorTurn = FindAnchorTurn(
                entries,
                normalTurnEnds,
                closingTurn,
                activePlayerActorId);
            if (anchorTurn < 0)
            {
                window = null;
                return false;
            }

            int priorNormalEndIndex = anchorTurn > 0
                ? normalTurnEnds[anchorTurn - 1]
                : -1;
            var segments = new List<TurnReplaySegment>(
                closingTurn - anchorTurn + 1);
            for (int turnIndex = anchorTurn;
                turnIndex <= closingTurn;
                turnIndex++)
            {
                int endEntryIndex = normalTurnEnds[turnIndex];
                var segmentEntries = new List<GameplayJournalEntry>(
                    endEntryIndex - priorNormalEndIndex);
                for (int entryIndex = priorNormalEndIndex + 1;
                    entryIndex <= endEntryIndex;
                    entryIndex++)
                {
                    segmentEntries.Add(entries[entryIndex]);
                }
                var ended = (TurnEndedJournalEntry)entries[endEntryIndex];
                segments.Add(new TurnReplaySegment(
                    ended.Turn.Sequence,
                    ended.Turn.EndingActorId,
                    segmentEntries));
                priorNormalEndIndex = endEntryIndex;
            }

            window = new TurnReplayWindow(activePlayerActorId, segments);
            return true;
        }

        private static int FindClosingTurn(
            IReadOnlyList<GameplayJournalEntry> entries,
            IReadOnlyList<int> normalTurnEnds,
            string actorId)
        {
            for (int index = normalTurnEnds.Count - 1; index >= 0; index--)
            {
                var ended = (TurnEndedJournalEntry)entries[normalTurnEnds[index]];
                if (string.Equals(
                    ended.Turn.NextActorId,
                    actorId,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return -1;
        }

        private static int FindAnchorTurn(
            IReadOnlyList<GameplayJournalEntry> entries,
            IReadOnlyList<int> normalTurnEnds,
            int closingTurn,
            string actorId)
        {
            for (int index = closingTurn; index >= 0; index--)
            {
                var ended = (TurnEndedJournalEntry)entries[normalTurnEnds[index]];
                if (string.Equals(
                    ended.Turn.EndingActorId,
                    actorId,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
