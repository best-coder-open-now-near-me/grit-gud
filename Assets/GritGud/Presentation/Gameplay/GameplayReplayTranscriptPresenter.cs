using System;
using System.Collections.Generic;
using System.Globalization;
using GritGud.Application.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayReplayTranscriptSource :
        IGameplayDialogueEntrySource
    {
        private readonly List<GameplayDialogueEntry> entries =
            new List<GameplayDialogueEntry>();
        private readonly IReadOnlyList<GameplayDialogueEntry> readOnlyEntries;

        public GameplayReplayTranscriptSource()
        {
            readOnlyEntries = entries.AsReadOnly();
        }

        public ReplayCombatTranscript Transcript { get; private set; }
        public IReadOnlyList<GameplayDialogueEntry> Entries => readOnlyEntries;
        public long LatestSequence => entries.Count == 0
            ? 0
            : entries[entries.Count - 1].Sequence;
        public long HighlightedSequence => LatestSequence;

        public void Bind(ReplayCombatTranscript transcript)
        {
            Transcript = transcript ?? throw new ArgumentNullException(
                nameof(transcript));
            SetPlayhead(0f);
        }

        public void SetPlayhead(float timeSeconds)
        {
            if (Transcript == null)
                throw new InvalidOperationException(
                    "Bind a replay transcript before setting its playhead.");
            entries.Clear();
            foreach (ReplayCombatTranscriptEntry entry in
                Transcript.GetEntriesAtOrBefore(timeSeconds))
            {
                string timestamp = entry.TimeSeconds.ToString(
                    "00.000",
                    CultureInfo.InvariantCulture);
                entries.Add(new GameplayDialogueEntry(
                    entry.Sequence,
                    GameplayDialogueChannel.CombatDiagnostics,
                    timestamp + "  " + entry.DisplayTitle,
                    string.Join(Environment.NewLine, entry.DisplayLines)));
            }
        }

        public int CountVisible(GameplayDialogueChannel filters) =>
            (filters & GameplayDialogueChannel.CombatDiagnostics) == 0
                ? 0
                : entries.Count;
    }

    internal sealed class GameplayReplayTranscriptPresenter
    {
        private GameplayTurnReplayHud hud;
        private GameplayDialogueDrawer drawer;
        private GameplayDialogueLog liveSource;
        private Action liveExportRequested;
        private GameplaySemanticReplayPlaybackTimeline projectedPlayback;
        private GameplayReplayTranscriptSource replaySource;

        internal ReplayCombatTranscript Transcript => replaySource?.Transcript;
        internal IGameplayDialogueEntrySource VisibleSource => drawer?.Source;

        public void Bind(
            GameplayTurnReplayHud replayHud,
            GameplayDialogueDrawer dialogueDrawer,
            GameplayDialogueLog liveDialogue,
            Action onLiveExportRequested)
        {
            Unbind();
            hud = replayHud ?? throw new ArgumentNullException(nameof(replayHud));
            drawer = dialogueDrawer ?? throw new ArgumentNullException(
                nameof(dialogueDrawer));
            liveSource = liveDialogue ?? throw new ArgumentNullException(
                nameof(liveDialogue));
            liveExportRequested = onLiveExportRequested;
            replaySource = new GameplayReplayTranscriptSource();
            hud.OpenChanged += HandleOpenChanged;
            hud.PlayheadChanged += HandlePlayheadChanged;
        }

        public void Unbind()
        {
            if (hud != null)
            {
                hud.OpenChanged -= HandleOpenChanged;
                hud.PlayheadChanged -= HandlePlayheadChanged;
            }
            if (drawer != null
                && liveSource != null
                && ReferenceEquals(drawer.Source, replaySource))
                drawer.Bind(liveSource, liveExportRequested);
            hud = null;
            drawer = null;
            liveSource = null;
            liveExportRequested = null;
            projectedPlayback = null;
            replaySource = null;
        }

        private void HandleOpenChanged(bool open)
        {
            if (!open)
            {
                drawer.Bind(liveSource, liveExportRequested);
                return;
            }
            EnsureTranscript();
            replaySource.SetPlayhead(hud.TimeSeconds);
            drawer.Bind(replaySource);
        }

        private void HandlePlayheadChanged(GameplayReplayPlayheadChange change)
        {
            if (!hud.IsOpen) return;
            EnsureTranscript();
            replaySource.SetPlayhead(change.TimeSeconds);
        }

        private void EnsureTranscript()
        {
            GameplaySemanticReplayPlaybackTimeline playback = hud.Playback
                ?? throw new InvalidOperationException(
                    "Replay transcript projection requires an active playback timeline.");
            if (ReferenceEquals(projectedPlayback, playback)) return;
            projectedPlayback = playback;
            replaySource.Bind(new ReplayCombatTranscript(playback));
        }
    }
}
