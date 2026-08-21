using System;

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
}
