using System;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public enum GameplayReplaySource
    {
        LiveEncounter,
        VerifiedSimulation,
    }

    internal enum GameplayReplayCameraMode
    {
        Auto = 0,
        Subject = 1,
        Free = 2,
    }

    internal enum GameplayReplayCameraCommand
    {
        Auto = 0,
        PreviousSubject = 1,
        NextSubject = 2,
        Free = 3,
    }

    internal readonly struct GameplayReplayPlayheadChange
    {
        public GameplayReplayPlayheadChange(
            float previousTimeSeconds,
            float timeSeconds,
            bool presentsTimedEvents)
        {
            PreviousTimeSeconds = previousTimeSeconds;
            TimeSeconds = timeSeconds;
            PresentsTimedEvents = presentsTimedEvents;
        }

        public float PreviousTimeSeconds { get; }
        public float TimeSeconds { get; }
        public bool PresentsTimedEvents { get; }
    }

    [DisallowMultipleComponent]
    public sealed class GameplayTurnReplayHud : MonoBehaviour
    {
        private const float ReferenceHeight = 900f;
        private const float Margin = 14f;
        private const float BarHeight = 82f;
        private const float MaximumBarWidth = 980f;

        private GameplaySession gameplay;
        private GameplayLiveSessionRuntime runtime;
        private GameplaySemanticReplayTimeline replay;
        private GameplaySemanticReplayPlaybackTimeline playback;
        private GameplaySemanticReplayTimeline externalReplay;
        private string externalArtifactId;
        private GameplayBattleArtifactTerminal externalTerminal;
        private GameplayBattleScoreboard externalScoreboard;
        private Func<string> resolveControlledActorId;
        private Func<GameplaySemanticReplayTimeline,
            GameplayReplayPresentationCompatibility> presentationPreflight;
        private GameplayReplayContentSummary contentSummary;
        private string lastOpenFailure = string.Empty;
        private bool isOpen;
        private bool isPlaying;
        private bool scrubCaptured;
        private bool resumeAfterScrub;
        private GameplayReplaySource source;
        private float playhead;
        private float speed = 1f;
        private GameplayReplayCameraMode replayCameraMode;
        private string replayCameraLabel = "AUTO";
        private GUIStyle titleStyle;
        private GUIStyle segmentStyle;
        private GUIStyle activeSegmentStyle;

        public bool IsOpen => isOpen;

        internal GameplaySemanticReplayTimeline Replay => replay;

        internal GameplaySemanticReplayPlaybackTimeline Playback => playback;

        internal float TimeSeconds => playhead;

        internal bool IsPlaying => isPlaying;

        internal bool IsScrubCaptured => scrubCaptured;

        internal GameplayReplaySource Source => source;

        internal GameplayReplayCameraMode ReplayCameraMode =>
            replayCameraMode;

        internal string ReplayCameraLabel => replayCameraLabel;

        internal GameplayReplayContentSummary ContentSummary => contentSummary;

        internal string LastOpenFailure => lastOpenFailure;

        internal int LiveTransitionCount => runtime?.Trajectory.Count ?? 0;

        internal event Action<bool> OpenChanged;

        internal event Action<GameplayReplayPlayheadChange> PlayheadChanged;

        internal event Action<GameplayReplayCameraCommand>
            CameraCommandRequested;

        public bool IsAvailable => runtime != null
            && (externalReplay != null
                || (source == GameplayReplaySource.LiveEncounter
                    && gameplay?.Mode == GameplaySessionMode.TurnBased
                    && !string.IsNullOrWhiteSpace(ControlledActorId)
                    && runtime.HasReplaySinceActorLastTurn(
                        ControlledActorId)));

        internal string ActionLabel => externalReplay == null
            ? "REPLAY SINCE LAST TURN"
            : "WATCH BATTLE";

        public void Bind(
            GameplaySession session,
            GameplayLiveSessionRuntime liveRuntime,
            GameplayReplaySource replaySource,
            Func<string> controlledActorIdResolver = null)
        {
            Unbind();
            gameplay = session ?? throw new ArgumentNullException(
                nameof(session));
            runtime = liveRuntime ?? throw new ArgumentNullException(
                nameof(liveRuntime));
            source = replaySource;
            resolveControlledActorId = controlledActorIdResolver;
            enabled = true;
        }

        public void Unbind()
        {
            gameplay = null;
            runtime = null;
            replay = null;
            playback = null;
            externalReplay = null;
            externalArtifactId = null;
            externalTerminal = null;
            externalScoreboard = null;
            resolveControlledActorId = null;
            presentationPreflight = null;
            contentSummary = null;
            lastOpenFailure = string.Empty;
            isOpen = false;
            isPlaying = false;
            ResetScrubCapture();
            ResetReplayCameraState();
            source = default;
            playhead = 0f;
            enabled = false;
        }

        internal void BindPresentationPreflight(
            Func<GameplaySemanticReplayTimeline,
                GameplayReplayPresentationCompatibility> preflight)
        {
            presentationPreflight = preflight ?? throw new ArgumentNullException(
                nameof(preflight));
        }

        internal void ClearPresentationPreflight(
            Func<GameplaySemanticReplayTimeline,
                GameplayReplayPresentationCompatibility> preflight)
        {
            if (Equals(presentationPreflight, preflight))
                presentationPreflight = null;
        }

        internal void SetVerifiedExternalReplay(
            GameplaySemanticReplayTimeline timeline,
            GameplayBattleArtifact artifact)
        {
            GameplaySemanticReplayTimeline nextReplay = timeline
                ?? throw new ArgumentNullException(
                nameof(timeline));
            GameplayBattleArtifact nextArtifact = artifact
                ?? throw new ArgumentNullException(
                nameof(artifact));
            externalReplay = nextReplay;
            externalArtifactId = nextArtifact.ArtifactId;
            externalTerminal = nextArtifact.Content.Terminal;
            externalScoreboard = nextArtifact.Content.Scoreboard;
            replay = null;
            playback = null;
            contentSummary = null;
            lastOpenFailure = string.Empty;
            ResetScrubCapture();
            ResetReplayCameraState();
            if (isOpen)
            {
                RefreshPlayback();
                isPlaying = playback != null;
                playhead = 0f;
                if (!TryNotifyPlayheadChanged(
                        new GameplayReplayPlayheadChange(0f, 0f, false)))
                    AbortPlayback();
            }
        }

        public void Toggle()
        {
            if (isOpen)
            {
                isOpen = false;
                isPlaying = false;
                ResetScrubCapture();
                ResetReplayCameraState();
                TryNotifyOpenChanged(false);
                replay = null;
                playback = null;
                contentSummary = null;
                return;
            }

            RefreshPlayback();
            if (playback == null)
                return;
            isOpen = true;
            // REPLAY should replay. Live and verified timelines both begin at
            // their initial state and advance immediately; a final-state view
            // belongs in a history inspector, not this control.
            isPlaying = true;
            ResetScrubCapture();
            ResetReplayCameraState();
            playhead = 0f;
            if (!TryNotifyOpenChanged(true)
                || !TryNotifyPlayheadChanged(
                    new GameplayReplayPlayheadChange(0f, 0f, false)))
            {
                AbortPlayback();
            }
        }

        internal void OpenVerifiedExternalReplay()
        {
            if (externalReplay == null || isOpen)
                return;
            Toggle();
        }

        internal bool ContainsInteractiveScreenPoint(Vector2 screenPoint)
        {
            if (!isOpen || playback == null)
                return false;
            float scale = CalculateUiScale();
            Vector2 guiPoint = new Vector2(
                screenPoint.x / scale,
                (Screen.height - screenPoint.y) / scale);
            return CalculateBarRectangle(
                Screen.width / scale,
                Screen.height / scale).Contains(guiPoint);
        }

        private void Update()
        {
            AdvancePlayback(Time.unscaledDeltaTime);
        }

        internal void AdvancePlayback(float unscaledDeltaSeconds)
        {
            if (float.IsNaN(unscaledDeltaSeconds)
                || float.IsInfinity(unscaledDeltaSeconds)
                || unscaledDeltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unscaledDeltaSeconds));
            }
            if (!isOpen || !isPlaying || playback == null)
                return;
            float previousPlayhead = playhead;
            playhead = Mathf.Min(
                playback.TotalDurationSeconds,
                playhead + (unscaledDeltaSeconds * speed));
            if (!TryNotifyPlayheadChanged(new GameplayReplayPlayheadChange(
                    previousPlayhead,
                    playhead,
                    presentsTimedEvents: playhead > previousPlayhead)))
            {
                AbortPlayback();
                return;
            }
            if (playhead >= playback.TotalDurationSeconds)
                isPlaying = false;
        }

        private void OnGUI()
        {
            if (!isOpen || playback == null)
                return;
            EnsureStyles();
            float scale = CalculateUiScale();
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Draw(CalculateBarRectangle(
                Screen.width / scale,
                Screen.height / scale));
            GUI.matrix = previous;
        }

        private void Draw(Rect bar)
        {
            float previousPlayhead = playhead;
            GameplaySemanticReplayPlaybackPosition position = playback.Locate(
                playhead);
            int selectedFrame = position.Frame.Index;
            int selectedTurn = playback.LocateTurnGroupIndex(playhead);
            GUI.Box(bar, GUIContent.none);
            string actorId = playback.TurnGroups[selectedTurn].ActorId;
            string displayName = ResolveDisplayName(actorId);
            string capability = position.Frame.Transition.Payload.Profile
                .Capability.ToString();
            string mode = externalArtifactId == null
                ? "REPLAY"
                : "SIM " + externalArtifactId.Substring(0, 8)
                    .ToUpperInvariant();
            string detail = displayName.ToUpperInvariant() + " · " + capability;
            if (externalTerminal != null
                && playhead >= playback.TotalDurationSeconds)
            {
                GameplayBattleScoreboard score = externalScoreboard;
                detail = externalTerminal.Kind.ToString()
                    .ToUpperInvariant()
                    + " · " + score.TurnsCompleted + " TURNS"
                    + " · " + score.Hits + "/" + score.Attacks + " HITS"
                    + " · " + score.Wounds + " WOUNDS";
            }
            GUI.Label(
                new Rect(bar.x + 10f, bar.y + 5f, bar.width - 50f, 20f),
                $"{mode}  TURN {selectedTurn + 1}/"
                    + $"{playback.TurnGroups.Count} · EVENT "
                    + $"{selectedFrame + 1}/{playback.Frames.Count}  "
                    + detail,
                titleStyle);
            if (GUI.Button(
                new Rect(bar.xMax - 30f, bar.y + 4f, 24f, 22f),
                "×"))
            {
                Toggle();
                return;
            }

            Rect timeline = new Rect(
                bar.x + 10f,
                bar.y + 28f,
                bar.width - 20f,
                24f);
            for (int index = 0;
                index < playback.TurnGroups.Count;
                index++)
            {
                GameplaySemanticReplayTurnGroup group =
                    playback.TurnGroups[index];
                Rect segment = CalculateTimelineSegment(
                    timeline,
                    group.StartSeconds,
                    group.DurationSeconds);
                string label = segment.width >= 34f
                    ? "T" + (index + 1)
                    : string.Empty;
                if (GUI.Button(
                    segment,
                    label,
                    index == selectedTurn
                        ? activeSegmentStyle
                        : segmentStyle))
                {
                    playhead = group.StartSeconds;
                    isPlaying = false;
                }
            }
            DrawCombatEventMarkers(timeline);

            float railX = timeline.x + (timeline.width
                * Mathf.Clamp01(
                    playhead / playback.TotalDurationSeconds));
            GUI.DrawTexture(
                new Rect(railX - 1f, timeline.y, 2f, timeline.height),
                Texture2D.whiteTexture);

            float controlsY = bar.y + 56f;
            if (GUI.Button(
                new Rect(bar.x + 10f, controlsY, 30f, 20f),
                "|<"))
            {
                playhead = playback.TurnGroups[
                    Mathf.Max(0, selectedTurn - 1)].StartSeconds;
                isPlaying = false;
            }
            if (GUI.Button(
                new Rect(bar.x + 44f, controlsY, 52f, 20f),
                isPlaying ? "PAUSE" : "PLAY"))
            {
                if (playhead >= playback.TotalDurationSeconds)
                    playhead = 0f;
                isPlaying = !isPlaying;
            }
            if (GUI.Button(
                new Rect(bar.x + 100f, controlsY, 30f, 20f),
                ">|"))
            {
                int next = Mathf.Min(
                    playback.TurnGroups.Count - 1,
                    selectedTurn + 1);
                playhead = next == selectedTurn
                    ? playback.TotalDurationSeconds
                    : playback.TurnGroups[next].StartSeconds;
                isPlaying = false;
            }
            if (GUI.Button(
                new Rect(bar.x + 136f, controlsY, 42f, 20f),
                $"{speed:0.#}×"))
            {
                speed = speed >= 2f ? 0.5f : speed * 2f;
            }
            if (GUI.Button(
                new Rect(bar.x + 184f, controlsY, 46f, 20f),
                "AUTO"))
                RequestCameraCommand(GameplayReplayCameraCommand.Auto);
            if (GUI.Button(
                new Rect(bar.x + 234f, controlsY, 24f, 20f),
                "<"))
                RequestCameraCommand(
                    GameplayReplayCameraCommand.PreviousSubject);
            GUI.Label(
                new Rect(bar.x + 262f, controlsY, 100f, 20f),
                replayCameraLabel,
                titleStyle);
            if (GUI.Button(
                new Rect(bar.x + 366f, controlsY, 24f, 20f),
                ">"))
                RequestCameraCommand(
                    GameplayReplayCameraCommand.NextSubject);

            Rect scrubber = new Rect(
                bar.x + 398f,
                controlsY + 1f,
                Mathf.Max(40f, bar.width - 408f),
                18f);
            Event guiEvent = Event.current;
            if (guiEvent.rawType == EventType.MouseDown
                && guiEvent.button == 0
                && scrubber.Contains(guiEvent.mousePosition))
                BeginScrubCapture();
            float scrubbedPlayhead = GUI.HorizontalSlider(
                scrubber,
                playhead,
                0f,
                playback.TotalDurationSeconds);
            if (!Mathf.Approximately(previousPlayhead, scrubbedPlayhead))
                ApplyPlayheadChange(previousPlayhead, scrubbedPlayhead);
            // rawType survives IMGUI control consumption, so a drag released
            // outside the slider still restores the captured play state.
            if (scrubCaptured
                && guiEvent.rawType == EventType.MouseUp
                && guiEvent.button == 0)
                EndScrubCapture();
        }

        internal void BeginScrubCapture()
        {
            if (!isOpen || playback == null || scrubCaptured)
                return;
            scrubCaptured = true;
            resumeAfterScrub = isPlaying;
            isPlaying = false;
        }

        internal void SetScrubPlayhead(float timeSeconds)
        {
            if (float.IsNaN(timeSeconds) || float.IsInfinity(timeSeconds))
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            if (!isOpen || playback == null)
                return;
            ApplyPlayheadChange(playhead, timeSeconds);
        }

        private void ApplyPlayheadChange(
            float previousPlayhead,
            float timeSeconds)
        {
            playhead = Mathf.Clamp(
                timeSeconds,
                0f,
                playback.TotalDurationSeconds);
            if (Mathf.Approximately(previousPlayhead, playhead))
                return;
            if (!TryNotifyPlayheadChanged(
                    new GameplayReplayPlayheadChange(
                        previousPlayhead,
                        playhead,
                        false)))
                AbortPlayback();
        }

        internal void EndScrubCapture()
        {
            if (!scrubCaptured)
                return;
            bool shouldResume = resumeAfterScrub;
            ResetScrubCapture();
            isPlaying = isOpen
                && playback != null
                && shouldResume
                && playhead < playback.TotalDurationSeconds;
        }

        internal void SetReplayCameraState(
            GameplayReplayCameraMode mode,
            string label)
        {
            if (!Enum.IsDefined(typeof(GameplayReplayCameraMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            replayCameraMode = mode;
            replayCameraLabel = string.IsNullOrWhiteSpace(label)
                ? mode.ToString().ToUpperInvariant()
                : label.Trim().ToUpperInvariant();
        }

        internal void RequestCameraCommand(GameplayReplayCameraCommand command)
        {
            if (!Enum.IsDefined(typeof(GameplayReplayCameraCommand), command))
                throw new ArgumentOutOfRangeException(nameof(command));
            if (!isOpen || playback == null)
                return;
            if (!TryNotifyCameraCommand(command))
                AbortPlayback();
        }

        private Rect CalculateTimelineSegment(
            Rect timeline,
            float startSeconds,
            float durationSeconds) => new Rect(
                timeline.x + (timeline.width
                    * startSeconds / playback.TotalDurationSeconds),
                timeline.y,
                timeline.width * durationSeconds
                    / playback.TotalDurationSeconds,
                timeline.height);

        private void DrawCombatEventMarkers(Rect timeline)
        {
            foreach (GameplaySemanticReplayPlaybackFrame frame in
                playback.Frames)
            {
                foreach (ReplayCombatPresentationEvent presentationEvent in
                    ReplayCombatPresentationEventProjector.Project(
                        frame.Frame))
                {
                    float eventSeconds = frame.StartSeconds
                        + (frame.DurationSeconds
                            * presentationEvent.NormalizedTime);
                    float x = timeline.x + timeline.width
                        * eventSeconds / playback.TotalDurationSeconds;
                    GUI.DrawTexture(
                        new Rect(x - 1f, timeline.y + 2f, 2f,
                            timeline.height - 4f),
                        Texture2D.whiteTexture);
                }
            }
        }

        private void RefreshPlayback()
        {
            lastOpenFailure = string.Empty;
            contentSummary = null;
            if (!IsAvailable)
            {
                replay = null;
                playback = null;
                return;
            }
            GameplayPlayerAwayReplayInterval interval = null;
            if (externalReplay != null)
            {
                replay = externalReplay;
            }
            else if (!runtime.TryCreateReplaySinceActorLastTurn(
                ControlledActorId,
                out replay,
                out interval))
            {
                replay = null;
            }
            if (replay == null)
            {
                playback = null;
                return;
            }
            playback = new GameplaySemanticReplayPlaybackTimeline(replay);
            if (playback.Frames.Count == 0)
            {
                FailOpen("REPLAY SOURCE CONTAINS NO PLAYBACK FRAMES");
                return;
            }
            try
            {
                string sourceLabel = externalReplay != null
                    ? "REPLAY SOURCE: ARTIFACT " + externalArtifactId
                    : "REPLAY SOURCE: LIVE SINCE "
                        + ResolveDisplayName(ControlledActorId).ToUpperInvariant()
                        + "'S LAST TURN · " + interval.Windows.Count
                        + (interval.Windows.Count == 1 ? " TURN" : " TURNS");
                contentSummary = new GameplayReplayContentSummary(
                    sourceLabel,
                    playback);
                if (presentationPreflight != null)
                    contentSummary.SetPresentationCompatibility(
                        presentationPreflight(replay));
                if (!contentSummary.IsReadyToOpen)
                {
                    FailOpen(contentSummary.ValidationMessage);
                    return;
                }
                Debug.Log(contentSummary.ToDisplayText(), this);
            }
            catch (Exception exception)
            {
                FailOpen(exception.Message);
            }
        }

        private string ControlledActorId => resolveControlledActorId?.Invoke();

        private void FailOpen(string message)
        {
            lastOpenFailure = string.IsNullOrWhiteSpace(message)
                ? "REPLAY CONTENT PREFLIGHT FAILED"
                : message;
            replay = null;
            playback = null;
            Debug.LogError(lastOpenFailure, this);
        }

        private string ResolveDisplayName(string actorId)
        {
            if (gameplay?.Scenario == null
                || string.IsNullOrWhiteSpace(actorId)
                || !gameplay.TryGetActor(actorId, out _))
                return actorId ?? string.Empty;
            var actor = gameplay.Scenario.GetActor(actorId);
            return actor.CharacterProfile?.DisplayName ?? actorId;
        }

        private bool TryNotifyOpenChanged(bool open)
        {
            bool succeeded = true;
            Delegate[] subscribers = OpenChanged?.GetInvocationList();
            if (subscribers == null) return true;
            foreach (Delegate subscriber in subscribers)
            {
                try
                {
                    ((Action<bool>)subscriber)(open);
                }
                catch (Exception exception)
                {
                    succeeded = false;
                    Debug.LogError(
                        $"Replay could not {(open ? "open" : "close")}: "
                        + exception.Message,
                        this);
                }
            }
            return succeeded;
        }

        private bool TryNotifyPlayheadChanged(
            GameplayReplayPlayheadChange change)
        {
            bool succeeded = true;
            Delegate[] subscribers = PlayheadChanged?.GetInvocationList();
            if (subscribers == null) return true;
            foreach (Delegate subscriber in subscribers)
            {
                try
                {
                    ((Action<GameplayReplayPlayheadChange>)subscriber)(change);
                }
                catch (Exception exception)
                {
                    succeeded = false;
                    Debug.LogError(
                        "Replay world presentation could not apply the current "
                        + $"sample: {exception.Message}",
                        this);
                }
            }
            return succeeded;
        }

        private bool TryNotifyCameraCommand(
            GameplayReplayCameraCommand command)
        {
            bool succeeded = true;
            Delegate[] subscribers = CameraCommandRequested
                ?.GetInvocationList();
            if (subscribers == null) return true;
            foreach (Delegate subscriber in subscribers)
            {
                try
                {
                    ((Action<GameplayReplayCameraCommand>)subscriber)(command);
                }
                catch (Exception exception)
                {
                    succeeded = false;
                    Debug.LogError(
                        "Replay camera could not apply " + command + ": "
                        + exception.Message,
                        this);
                }
            }
            return succeeded;
        }

        private void AbortPlayback()
        {
            bool wasOpen = isOpen;
            isOpen = false;
            isPlaying = false;
            ResetScrubCapture();
            ResetReplayCameraState();
            playhead = 0f;
            if (wasOpen)
                TryNotifyOpenChanged(false);
            replay = null;
            playback = null;
        }

        private void ResetScrubCapture()
        {
            scrubCaptured = false;
            resumeAfterScrub = false;
        }

        private void ResetReplayCameraState()
        {
            replayCameraMode = GameplayReplayCameraMode.Auto;
            replayCameraLabel = "AUTO";
        }

        private static Rect CalculateBarRectangle(
            float canvasWidth,
            float canvasHeight)
        {
            float width = Mathf.Min(MaximumBarWidth, canvasWidth - (Margin * 2f));
            return new Rect(
                (canvasWidth - width) * 0.5f,
                Margin,
                width,
                Mathf.Min(BarHeight, canvasHeight - (Margin * 2f)));
        }

        private static float CalculateUiScale() => Mathf.Max(
            0.65f,
            Screen.height / ReferenceHeight);

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
            };
            segmentStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                clipping = TextClipping.Clip,
            };
            activeSegmentStyle = new GUIStyle(segmentStyle);
            activeSegmentStyle.normal.textColor =
                GameplayVisualPalette.SignalOrangeGlow;
        }
    }
}
