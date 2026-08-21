using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayExecutableRouteCoverageIssue
    {
        internal GameplayExecutableRouteCoverageIssue(
            string sourceId,
            string profileSignature)
        {
            SourceId = sourceId ?? string.Empty;
            ProfileSignature = profileSignature ?? string.Empty;
        }

        public string SourceId { get; }
        public string ProfileSignature { get; }
    }

    /// <summary>
    /// Complements capability metadata with proof that an exact concrete route
    /// owns legality, frozen evidence, and transition-payload preparation.
    /// </summary>
    public sealed class GameplayExecutableRouteCoverageReport
    {
        internal GameplayExecutableRouteCoverageReport(
            int reachableInputCount,
            IEnumerable<GameplayExecutableRouteCoverageIssue> issues)
        {
            if (reachableInputCount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(reachableInputCount));
            ReachableInputCount = reachableInputCount;
            Issues = new List<GameplayExecutableRouteCoverageIssue>(
                issues ?? throw new ArgumentNullException(nameof(issues)))
                .AsReadOnly();
        }

        public int ReachableInputCount { get; }
        public IReadOnlyList<GameplayExecutableRouteCoverageIssue> Issues
        {
            get;
        }
        public bool IsComplete => Issues.Count == 0;

        public void RequireComplete()
        {
            if (IsComplete) return;
            var details = new List<string>();
            foreach (GameplayExecutableRouteCoverageIssue issue in Issues)
                details.Add(issue.SourceId + " -> "
                    + issue.ProfileSignature);
            throw new InvalidOperationException(
                "Reachable inputs lack exact executable candidate routes: "
                + string.Join(" | ", details));
        }
    }

    public static class GameplayExecutableRouteCoverageValidator
    {
        public static GameplayExecutableRouteCoverageReport Validate(
            IEnumerable<GameplayReachableInput> reachableInputs,
            GameplayCandidateExecutionRouteRegistry routes)
        {
            if (reachableInputs == null)
                throw new ArgumentNullException(nameof(reachableInputs));
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            var issues = new List<GameplayExecutableRouteCoverageIssue>();
            int count = 0;
            foreach (GameplayReachableInput input in reachableInputs)
            {
                if (input == null)
                    throw new ArgumentException(
                        "Reachable inputs cannot contain null entries.",
                        nameof(reachableInputs));
                count++;
                if (!routes.Supports(input.Profile))
                    issues.Add(new GameplayExecutableRouteCoverageIssue(
                        input.SourceId,
                        input.Profile.Signature));
            }
            return new GameplayExecutableRouteCoverageReport(count, issues);
        }
    }
}
