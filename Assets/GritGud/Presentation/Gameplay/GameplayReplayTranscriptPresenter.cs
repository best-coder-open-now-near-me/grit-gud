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
        private readonly List<GameplayDialogueEntry> projectedEntries =
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
        internal int ProjectionPassCount { get; private set; }

        public void Bind(ReplayCombatTranscript transcript)
        {
            Transcript = transcript ?? throw new ArgumentNullException(
                nameof(transcript));
            projectedEntries.Clear();
            foreach (ReplayCombatTranscriptEntry entry in Transcript.Entries)
                projectedEntries.Add(Project(entry));
            ProjectionPassCount++;
            entries.Clear();
            SetPlayhead(0f);
        }

        public void SetPlayhead(float timeSeconds)
        {
            if (Transcript == null)
                throw new InvalidOperationException(
                    "Bind a replay transcript before setting its playhead.");
            int targetCount = Transcript.CountEntriesAtOrBefore(timeSeconds);
            if (targetCount < entries.Count)
            {
                entries.RemoveRange(
                    targetCount,
                    entries.Count - targetCount);
            }
            else
                for (int index = entries.Count; index < targetCount; index++)
                    entries.Add(projectedEntries[index]);
        }

        public int CountVisible(GameplayDialogueChannel filters) =>
            (filters & GameplayDialogueChannel.CombatDiagnostics) == 0
                ? 0
                : entries.Count;

        private static GameplayDialogueEntry Project(
            ReplayCombatTranscriptEntry entry)
        {
            string timestamp = entry.TimeSeconds.ToString(
                "00.000",
                CultureInfo.InvariantCulture);
            return new GameplayDialogueEntry(
                entry.Sequence,
                GameplayDialogueChannel.CombatDiagnostics,
                timestamp + "  " + entry.DisplayTitle,
                string.Join(Environment.NewLine, entry.DisplayLines));
        }
    }

    internal sealed class GameplayReplayTranscriptPresenter
    {
        private GameplayTurnReplayHud hud;
        private GameplayDialogueDrawer drawer;
        private GameplayDialogueLog liveSource;
        private Action liveExportRequested;
        private GameplaySemanticReplayPlaybackTimeline projectedPlayback;
        private GameplayReplayTranscriptSource replaySource;
        private bool drawerStateCaptured;
        private bool drawerWasExpanded;
        private GameplayDialogueChannel drawerFilters;
        private string drawerHeader;
        private string drawerEmptyMessage;
        private string drawerContextStatus;

        internal ReplayCombatTranscript Transcript => replaySource?.Transcript;
        internal IGameplayDialogueEntrySource VisibleSource => drawer?.Source;
        internal int TranscriptProjectionPassCount =>
            replaySource?.ProjectionPassCount ?? 0;

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
                RestoreLiveDrawer();
            hud = null;
            drawer = null;
            liveSource = null;
            liveExportRequested = null;
            projectedPlayback = null;
            replaySource = null;
            drawerStateCaptured = false;
        }

        private void HandleOpenChanged(bool open)
        {
            if (!open)
            {
                RestoreLiveDrawer();
                return;
            }
            CaptureDrawerState();
            EnsureTranscript();
            replaySource.SetPlayhead(hud.TimeSeconds);
            drawer.Bind(replaySource);
            drawer.ConfigurePresentation(
                "REPLAY COMBAT TRANSCRIPT",
                replaySource.Transcript.Entries.Count == 0
                    ? "REPLAY CONTAINS ZERO COMBAT TRANSCRIPT EVENTS."
                    : "NO REPLAY COMBAT EVENT HAS OCCURRED AT THIS PLAYHEAD.",
                hud.ContentSummary?.ToDisplayText());
            drawer.SetFilters(GameplayDialogueChannel.CombatDiagnostics);
            drawer.SetExpanded(true);
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
            replaySource.Bind(
                hud.ContentSummary?.Transcript
                ?? new ReplayCombatTranscript(playback));
        }

        private void CaptureDrawerState()
        {
            if (drawerStateCaptured) return;
            drawerStateCaptured = true;
            drawerWasExpanded = drawer.IsExpanded;
            drawerFilters = drawer.ActiveFilters;
            drawerHeader = drawer.HeaderLabel;
            drawerEmptyMessage = drawer.EmptyMessage;
            drawerContextStatus = drawer.ContextStatus;
        }

        private void RestoreLiveDrawer()
        {
            drawer.Bind(liveSource, liveExportRequested);
            if (!drawerStateCaptured) return;
            drawer.ConfigurePresentation(
                drawerHeader,
                drawerEmptyMessage,
                drawerContextStatus);
            drawer.SetFilters(drawerFilters);
            drawer.SetExpanded(drawerWasExpanded);
            drawerStateCaptured = false;
        }
    }
}
