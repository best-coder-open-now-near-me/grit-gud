using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public enum GameplayReplayWindowClosureReason
    {
        TurnEnded = 0,
        EncounterEnded = 1,
        TerminalCapability = 2,
        ArtifactTerminal = 3,
    }

    /// <summary>
    /// A verified contiguous section of the semantic trajectory. Its initial
    /// state is the canonical root immediately before <see cref="StartTrajectoryIndex"/>,
    /// so a replay never needs to infer scope from the runtime object's age.
    /// </summary>
    public sealed class GameplayReplayWindow
    {
        public GameplayReplayWindow(
            string actorId,
            long turnSequence,
            GameplayCombatStateSnapshot initialState,
            int startTrajectoryIndex,
            int endTrajectoryIndex,
            GameplayReplayWindowClosureReason closureReason =
                GameplayReplayWindowClosureReason.TurnEnded)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Replay windows require an actor identifier.",
                    nameof(actorId));
            if (turnSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(turnSequence));
            if (initialState == null)
                throw new ArgumentNullException(nameof(initialState));
            if (startTrajectoryIndex < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(startTrajectoryIndex));
            if (endTrajectoryIndex < startTrajectoryIndex)
                throw new ArgumentOutOfRangeException(
                    nameof(endTrajectoryIndex));
            if (!Enum.IsDefined(
                    typeof(GameplayReplayWindowClosureReason),
                    closureReason))
                throw new ArgumentOutOfRangeException(nameof(closureReason));

            ActorId = actorId;
            TurnSequence = turnSequence;
            InitialState = initialState;
            StartTrajectoryIndex = startTrajectoryIndex;
            EndTrajectoryIndex = endTrajectoryIndex;
            ClosureReason = closureReason;
        }

        public string ActorId { get; }

        public long TurnSequence { get; }

        public GameplayCombatStateSnapshot InitialState { get; }

        public int StartTrajectoryIndex { get; }

        public int EndTrajectoryIndex { get; }

        public GameplayReplayWindowClosureReason ClosureReason { get; }

        public int TransitionCount => checked(
            EndTrajectoryIndex - StartTrajectoryIndex + 1);
    }

    /// <summary>
    /// The complete chronological interval during which control was away from
    /// one player character. The character's own completed turn is the
    /// boundary, not part of the replay; every subsequently completed actor
    /// turn is retained until that character receives control again.
    /// </summary>
    public sealed class GameplayPlayerAwayReplayInterval
    {
        private readonly IReadOnlyList<GameplayReplayWindow> windows;
        private readonly IReadOnlyList<string> actorIds;

        internal GameplayPlayerAwayReplayInterval(
            GameplayReplayWindow controlledActorBoundary,
            IEnumerable<GameplayReplayWindow> completedWindows)
        {
            ControlledActorBoundary = controlledActorBoundary
                ?? throw new ArgumentNullException(
                    nameof(controlledActorBoundary));
            var copied = new List<GameplayReplayWindow>(completedWindows
                ?? throw new ArgumentNullException(nameof(completedWindows)));
            if (copied.Count == 0)
                throw new ArgumentException(
                    "Player-away replay intervals require at least one completed turn.",
                    nameof(completedWindows));
            if (copied[0].StartTrajectoryIndex
                != controlledActorBoundary.EndTrajectoryIndex + 1)
                throw new ArgumentException(
                    "Player-away replay must begin immediately after the controlled actor boundary.",
                    nameof(completedWindows));

            var orderedActors = new List<string>();
            int expectedStart = copied[0].StartTrajectoryIndex;
            foreach (GameplayReplayWindow window in copied)
            {
                if (window == null)
                    throw new ArgumentException(
                        "Player-away replay intervals cannot contain null windows.",
                        nameof(completedWindows));
                if (window.StartTrajectoryIndex != expectedStart)
                    throw new ArgumentException(
                        "Player-away replay windows must be contiguous and chronological.",
                        nameof(completedWindows));
                if (string.Equals(
                        window.ActorId,
                        controlledActorBoundary.ActorId,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Player-away replay cannot cross another completed turn for its controlled actor.",
                        nameof(completedWindows));
                if (!orderedActors.Contains(window.ActorId))
                    orderedActors.Add(window.ActorId);
                expectedStart = checked(window.EndTrajectoryIndex + 1);
            }

            windows = copied.AsReadOnly();
            actorIds = orderedActors.AsReadOnly();
        }

        public string ControlledActorId => ControlledActorBoundary.ActorId;

        public GameplayReplayWindow ControlledActorBoundary { get; }

        public IReadOnlyList<GameplayReplayWindow> Windows => windows;

        public IReadOnlyList<string> ActorIds => actorIds;

        public int StartTrajectoryIndex => windows[0].StartTrajectoryIndex;

        public int EndTrajectoryIndex => windows[windows.Count - 1]
            .EndTrajectoryIndex;

        public int TransitionCount => checked(
            EndTrajectoryIndex - StartTrajectoryIndex + 1);
    }
}
