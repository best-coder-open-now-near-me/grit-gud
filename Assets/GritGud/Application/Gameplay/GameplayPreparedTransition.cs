using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayPreparedTransition<TRecord>
    {
        public GameplayPreparedTransition(
            TRecord record,
            GameplayCombatStateSnapshot previous,
            GameplayCombatStateSnapshot predicted)
        {
            Record = record;
            Previous = previous ?? throw new ArgumentNullException(nameof(previous));
            Predicted = predicted ?? throw new ArgumentNullException(nameof(predicted));
            IReadOnlyList<GameplayInvariantViolation> violations =
                GameplayCombatInvariantValidator.Validate(predicted);
            if (violations.Count > 0)
                throw new ArgumentException(
                    $"The predicted transition violates '{violations[0].Code}' at "
                    + $"'{violations[0].Path}'.",
                    nameof(predicted));
        }

        public TRecord Record { get; }
        public GameplayCombatStateSnapshot Previous { get; }
        public GameplayCombatStateSnapshot Predicted { get; }
    }

    public sealed class GameplayTransitionCommitResult
    {
        public GameplayTransitionCommitResult(
            GameplayCombatStateSnapshot actual,
            IReadOnlyList<GameplayStateDifference> differences)
        {
            Actual = actual ?? throw new ArgumentNullException(nameof(actual));
            Differences = differences ?? throw new ArgumentNullException(nameof(differences));
        }

        public GameplayCombatStateSnapshot Actual { get; }
        public IReadOnlyList<GameplayStateDifference> Differences { get; }
        public bool MatchesPrediction => Differences.Count == 0;
    }

    public static class GameplayTransitionCoordinator
    {
        public static GameplayTransitionCommitResult Commit<TRecord>(
            GameplayPreparedTransition<TRecord> prepared,
            Func<GameplayCombatStateSnapshot> capture,
            Action<TRecord> commit)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            if (commit == null) throw new ArgumentNullException(nameof(commit));

            GameplayCombatStateSnapshot current = capture();
            if (!string.Equals(
                    current.CanonicalHash,
                    prepared.Previous.CanonicalHash,
                    StringComparison.Ordinal))
            {
                IReadOnlyList<GameplayStateDifference> staleDifferences =
                    GameplayCombatStateDiffer.Compare(prepared.Previous, current);
                string path = staleDifferences.Count == 0
                    ? "state.hash"
                    : staleDifferences[0].Path;
                throw new InvalidOperationException(
                    $"Prepared transition is stale at '{path}'.");
            }

            commit(prepared.Record);
            GameplayCombatStateSnapshot actual = capture();
            IReadOnlyList<GameplayInvariantViolation> violations =
                GameplayCombatInvariantValidator.Validate(actual);
            if (violations.Count > 0)
                throw new InvalidOperationException(
                    $"Committed transition violates '{violations[0].Code}' at "
                    + $"'{violations[0].Path}'.");
            return new GameplayTransitionCommitResult(
                actual,
                GameplayCombatStateDiffer.Compare(prepared.Predicted, actual));
        }
    }

    public sealed class GameplayReplayVerificationResult
    {
        public GameplayReplayVerificationResult(
            GameplayCombatStateSnapshot expected,
            GameplayCombatStateSnapshot replayed)
        {
            Expected = expected ?? throw new ArgumentNullException(nameof(expected));
            Replayed = replayed ?? throw new ArgumentNullException(nameof(replayed));
            Differences = GameplayCombatStateDiffer.Compare(expected, replayed);
            InvariantViolations = GameplayCombatInvariantValidator.Validate(replayed);
        }

        public GameplayCombatStateSnapshot Expected { get; }
        public GameplayCombatStateSnapshot Replayed { get; }
        public IReadOnlyList<GameplayStateDifference> Differences { get; }
        public IReadOnlyList<GameplayInvariantViolation> InvariantViolations { get; }
        public bool IsVerified => Differences.Count == 0
            && InvariantViolations.Count == 0;
    }
}
