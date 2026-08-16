using System;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayTurnReplayHud : MonoBehaviour
    {
        private const float ReferenceHeight = 900f;
        private const float Margin = 14f;
        private const float BarHeight = 82f;
        private const float MaximumBarWidth = 980f;
        private const float PlaybackSegmentsPerSecond = 0.65f;

        private GameplaySession gameplay;
        private GameplayPartyControlSession partyControl;
        private TurnReplayWindow window;
        private bool isOpen;
        private bool isPlaying;
        private float playhead;
        private float speed = 1f;
        private GUIStyle titleStyle;
        private GUIStyle segmentStyle;
        private GUIStyle activeSegmentStyle;

        public bool IsOpen => isOpen;

        internal TurnReplayWindow Window => window;

        internal float Playhead => playhead;

        internal event Action<bool> OpenChanged;

        internal event Action<float> PlayheadChanged;

        public bool IsAvailable
        {
            get
            {
                RefreshWindow();
                return window != null;
            }
        }

        public void Bind(
            GameplaySession session,
            GameplayPartyControlSession control)
        {
            Unbind();
            gameplay = session ?? throw new ArgumentNullException(nameof(session));
            partyControl = control ?? throw new ArgumentNullException(nameof(control));
            enabled = true;
        }

        public void Unbind()
        {
            gameplay = null;
            partyControl = null;
            window = null;
            isOpen = false;
            isPlaying = false;
            playhead = 0f;
            enabled = false;
        }

        public void Toggle()
        {
            RefreshWindow();
            if (window == null)
                return;
            isOpen = !isOpen;
            isPlaying = false;
            if (isOpen)
                playhead = window.DefaultPlayheadBoundary;
            OpenChanged?.Invoke(isOpen);
            if (isOpen)
                PlayheadChanged?.Invoke(playhead);
        }

        internal bool ContainsInteractiveScreenPoint(Vector2 screenPoint)
        {
            if (!isOpen || window == null)
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
            if (!isOpen || !isPlaying || window == null)
                return;
            playhead = Mathf.Min(
                window.Segments.Count,
                playhead + (Time.unscaledDeltaTime
                    * PlaybackSegmentsPerSecond
                    * speed));
            if (playhead >= window.Segments.Count)
                isPlaying = false;
            PlayheadChanged?.Invoke(playhead);
        }

        private void OnGUI()
        {
            if (!isOpen || window == null)
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
            GUI.Box(bar, GUIContent.none);
            GUI.Label(
                new Rect(bar.x + 10f, bar.y + 5f, 150f, 20f),
                "TURN REPLAY",
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
            float segmentWidth = timeline.width / window.Segments.Count;
            int selectedSegment = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Min(
                    playhead,
                    window.Segments.Count - 0.0001f)),
                0,
                window.Segments.Count - 1);
            for (int index = 0; index < window.Segments.Count; index++)
            {
                TurnReplaySegment segment = window.Segments[index];
                string displayName = gameplay.Scenario.GetActor(segment.ActorId)
                    .CharacterProfile?.DisplayName ?? segment.ActorId;
                Rect segmentRect = new Rect(
                    timeline.x + (segmentWidth * index),
                    timeline.y,
                    segmentWidth,
                    timeline.height);
                if (GUI.Button(
                    segmentRect,
                    displayName.ToUpperInvariant(),
                    index == selectedSegment
                        ? activeSegmentStyle
                        : segmentStyle))
                {
                    playhead = index;
                    isPlaying = false;
                }
                DrawEventMarkers(segment, segmentRect);
            }

            float railX = timeline.x + (timeline.width
                * Mathf.Clamp01(playhead / window.Segments.Count));
            GUI.DrawTexture(
                new Rect(railX - 1f, timeline.y, 2f, timeline.height),
                Texture2D.whiteTexture);

            float controlsY = bar.y + 56f;
            if (GUI.Button(
                new Rect(bar.x + 10f, controlsY, 30f, 20f),
                "|<"))
            {
                playhead = Mathf.Max(0f, Mathf.Ceil(playhead) - 1f);
                isPlaying = false;
            }
            if (GUI.Button(
                new Rect(bar.x + 44f, controlsY, 52f, 20f),
                isPlaying ? "PAUSE" : "PLAY"))
            {
                if (playhead >= window.Segments.Count)
                    playhead = window.DefaultPlayheadBoundary;
                isPlaying = !isPlaying;
            }
            if (GUI.Button(
                new Rect(bar.x + 100f, controlsY, 30f, 20f),
                ">|"))
            {
                playhead = Mathf.Min(
                    window.Segments.Count,
                    Mathf.Floor(playhead) + 1f);
                isPlaying = false;
            }
            if (GUI.Button(
                new Rect(bar.x + 136f, controlsY, 42f, 20f),
                $"{speed:0.#}×"))
            {
                speed = speed >= 2f ? 0.5f : speed * 2f;
            }

            Rect scrubber = new Rect(
                bar.x + 188f,
                controlsY + 1f,
                bar.width - 198f,
                18f);
            playhead = GUI.HorizontalSlider(
                scrubber,
                playhead,
                0f,
                window.Segments.Count);
            if (!Mathf.Approximately(previousPlayhead, playhead))
                PlayheadChanged?.Invoke(playhead);
        }

        private void RefreshWindow()
        {
            string actorId = partyControl?.CommandActorId;
            if (gameplay == null
                || gameplay.Mode != GameplaySessionMode.TurnBased
                || string.IsNullOrWhiteSpace(actorId)
                || !TurnReplayWindowProjector.TryProject(
                    gameplay.Journal,
                    actorId,
                    out window)
                || !window.IsAtJournalTip(gameplay.Journal))
            {
                window = null;
                isOpen = false;
                isPlaying = false;
            }
        }

        private static void DrawEventMarkers(
            TurnReplaySegment segment,
            Rect segmentRectangle)
        {
            int markerCount = 0;
            foreach (GameplayJournalEntry entry in segment.Entries)
                if (IsVisibleEvent(entry))
                    markerCount++;
            if (markerCount == 0)
                return;
            int markerIndex = 0;
            foreach (GameplayJournalEntry entry in segment.Entries)
            {
                if (!IsVisibleEvent(entry))
                    continue;
                float x = segmentRectangle.x
                    + (segmentRectangle.width
                        * ((markerIndex + 1f) / (markerCount + 1f)));
                GUI.DrawTexture(
                    new Rect(x - 1f, segmentRectangle.yMax - 5f, 2f, 4f),
                    Texture2D.whiteTexture);
                markerIndex++;
            }
        }

        private static bool IsVisibleEvent(GameplayJournalEntry entry) =>
            entry is MovementRouteCommittedJournalEntry
            || entry is StanceChangedJournalEntry
            || entry is ActionResolvedJournalEntry
            || entry is DisplacementResolvedJournalEntry
            || entry is ProjectileAdvancedJournalEntry
            || entry is DestructibleDamagedJournalEntry;

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
