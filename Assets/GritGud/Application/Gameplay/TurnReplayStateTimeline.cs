using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayCombatStateCheckpoint
    {
        public GameplayCombatStateCheckpoint(
            long journalSequence,
            GameplayCombatStateSnapshot state)
        {
            if (journalSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(journalSequence));
            State = state ?? throw new ArgumentNullException(nameof(state));
            if (state.Session.JournalSequence != journalSequence)
                throw new ArgumentException(
                    "Checkpoint sequence must match its canonical state.",
                    nameof(state));
            JournalSequence = journalSequence;
        }

        public long JournalSequence { get; }

        public GameplayCombatStateSnapshot State { get; }
    }

    public sealed class GameplayCombatStateTimeline : IDisposable
    {
        private readonly GameplaySession gameplay;
        private readonly Func<GameplayCombatStateSnapshot> capture;
        private readonly List<GameplayCombatStateCheckpoint> checkpoints =
            new List<GameplayCombatStateCheckpoint>();
        private readonly IReadOnlyList<GameplayCombatStateCheckpoint>
            readOnlyCheckpoints;
        private readonly int capacity;
        private bool disposed;

        public GameplayCombatStateTimeline(
            GameplaySession session,
            Func<GameplayCombatStateSnapshot> captureState,
            int checkpointCapacity = 0)
        {
            gameplay = session ?? throw new ArgumentNullException(nameof(session));
            capture = captureState ?? throw new ArgumentNullException(
                nameof(captureState));
            capacity = checkpointCapacity > 0
                ? checkpointCapacity
                : Math.Max(8, gameplay.InitiativeOrder.Count * 3 + 4);
            readOnlyCheckpoints = checkpoints.AsReadOnly();
            RecordCurrent();
            gameplay.TurnEnded += HandleTurnEnded;
        }

        public IReadOnlyList<GameplayCombatStateCheckpoint> Checkpoints =>
            readOnlyCheckpoints;

        public GameplayCombatStateSnapshot CaptureCurrent()
        {
            ThrowIfDisposed();
            return capture();
        }

        public bool TryGet(
            long journalSequence,
            out GameplayCombatStateCheckpoint checkpoint)
        {
            ThrowIfDisposed();
            for (int index = checkpoints.Count - 1; index >= 0; index--)
            {
                if (checkpoints[index].JournalSequence == journalSequence)
                {
                    checkpoint = checkpoints[index];
                    return true;
                }
            }
            checkpoint = null;
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            gameplay.TurnEnded -= HandleTurnEnded;
        }

        private void HandleTurnEnded(TurnEndRecord turn)
        {
            if (turn.Kind == GameplayTurnKind.Normal)
                RecordCurrent();
        }

        private void RecordCurrent()
        {
            GameplayCombatStateSnapshot state = capture();
            long sequence = state.Session.JournalSequence;
            if (checkpoints.Count > 0
                && checkpoints[checkpoints.Count - 1].JournalSequence == sequence)
                checkpoints[checkpoints.Count - 1] =
                    new GameplayCombatStateCheckpoint(sequence, state);
            else
                checkpoints.Add(new GameplayCombatStateCheckpoint(sequence, state));
            while (checkpoints.Count > capacity)
                checkpoints.RemoveAt(0);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(GameplayCombatStateTimeline));
        }
    }

    public sealed class TurnReplayStateWindow
    {
        public TurnReplayStateWindow(
            TurnReplayWindow replay,
            GameplayCombatStateCheckpoint start,
            IReadOnlyList<GameplayCombatStateCheckpoint> segmentEnds)
        {
            Replay = replay ?? throw new ArgumentNullException(nameof(replay));
            Start = start ?? throw new ArgumentNullException(nameof(start));
            if (segmentEnds == null
                || segmentEnds.Count != replay.Segments.Count)
                throw new ArgumentException(
                    "Replay state requires one endpoint per segment.",
                    nameof(segmentEnds));
            var copy = new List<GameplayCombatStateCheckpoint>(segmentEnds.Count);
            long previousSequence = start.JournalSequence;
            for (int index = 0; index < segmentEnds.Count; index++)
            {
                GameplayCombatStateCheckpoint checkpoint = segmentEnds[index]
                    ?? throw new ArgumentException(
                        "Replay checkpoints cannot contain null.",
                        nameof(segmentEnds));
                long expectedSequence = replay.Segments[index].Entries[
                    replay.Segments[index].Entries.Count - 1].Sequence;
                if (checkpoint.JournalSequence != expectedSequence
                    || checkpoint.JournalSequence <= previousSequence)
                    throw new ArgumentException(
                        "Replay checkpoint boundaries do not match the window.",
                        nameof(segmentEnds));
                copy.Add(checkpoint);
                previousSequence = checkpoint.JournalSequence;
            }
            SegmentEnds = copy.AsReadOnly();
        }

        public TurnReplayWindow Replay { get; }

        public GameplayCombatStateCheckpoint Start { get; }

        public IReadOnlyList<GameplayCombatStateCheckpoint> SegmentEnds { get; }

        public GameplayCombatStateCheckpoint End =>
            SegmentEnds[SegmentEnds.Count - 1];
    }

    public static class TurnReplayStateWindowProjector
    {
        public static bool TryProject(
            TurnReplayWindow replay,
            GameplayCombatStateTimeline timeline,
            out TurnReplayStateWindow stateWindow)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));
            long startSequence = replay.Segments[0].Entries[0].Sequence - 1L;
            if (!timeline.TryGet(startSequence, out GameplayCombatStateCheckpoint start))
            {
                stateWindow = null;
                return false;
            }
            var ends = new List<GameplayCombatStateCheckpoint>(
                replay.Segments.Count);
            foreach (TurnReplaySegment segment in replay.Segments)
            {
                long endSequence = segment.Entries[segment.Entries.Count - 1].Sequence;
                if (!timeline.TryGet(endSequence, out GameplayCombatStateCheckpoint end))
                {
                    stateWindow = null;
                    return false;
                }
                ends.Add(end);
            }
            stateWindow = new TurnReplayStateWindow(replay, start, ends);
            return true;
        }

        public static GameplayReplayVerificationResult VerifyCurrentEndpoint(
            TurnReplayStateWindow window,
            GameplayCombatStateTimeline timeline)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));
            return new GameplayReplayVerificationResult(
                window.End.State,
                timeline.CaptureCurrent());
        }
    }
}
