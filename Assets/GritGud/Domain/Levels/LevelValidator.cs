using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public enum LevelValidationSeverity
    {
        Warning,
        Error,
    }

    public enum LevelValidationProfile
    {
        Authoring,
        Publish,
        Runtime,
    }

    public sealed class LevelValidationIssue
    {
        public LevelValidationIssue(
            LevelValidationSeverity severity,
            string code,
            string message,
            string entityId = null)
        {
            Severity = severity;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            EntityId = entityId;
        }

        public LevelValidationSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public string EntityId { get; }
    }

    public sealed class LevelValidationContext
    {
        private readonly ICollection<LevelValidationIssue> issues;

        internal LevelValidationContext(
            LevelDocument document,
            LevelValidationContent content,
            LevelValidationProfile profile,
            ICollection<LevelValidationIssue> issues)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Content = content;
            Profile = profile;
            this.issues = issues ?? throw new ArgumentNullException(nameof(issues));
        }

        public LevelDocument Document { get; }

        public LevelValidationContent Content { get; }

        public ISet<string> KnownArchetypeIds => Content?.KnownArchetypeIds;

        public LevelValidationProfile Profile { get; }

        public void Report(
            LevelValidationSeverity severity,
            string code,
            string message,
            string entityId = null)
        {
            issues.Add(new LevelValidationIssue(severity, code, message, entityId));
        }

        public void Error(string code, string message, string entityId = null)
        {
            Report(LevelValidationSeverity.Error, code, message, entityId);
        }

        public void Warning(string code, string message, string entityId = null)
        {
            Report(LevelValidationSeverity.Warning, code, message, entityId);
        }
    }

    public interface ILevelValidationRule
    {
        void Evaluate(LevelValidationContext context);
    }

    public sealed class LevelValidationService
    {
        private readonly ILevelValidationRule[] rules;

        public LevelValidationService(IEnumerable<ILevelValidationRule> rules)
        {
            this.rules = rules?.Where(rule => rule != null).ToArray()
                ?? throw new ArgumentNullException(nameof(rules));
        }

        public IReadOnlyList<LevelValidationIssue> Validate(
            LevelDocument source,
            ISet<string> knownArchetypeIds = null,
            LevelValidationProfile profile = LevelValidationProfile.Authoring)
        {
            return Validate(
                source,
                new LevelValidationContent(knownArchetypeIds),
                profile);
        }

        public IReadOnlyList<LevelValidationIssue> Validate(
            LevelDocument source,
            LevelValidationContent content,
            LevelValidationProfile profile = LevelValidationProfile.Authoring)
        {
            var issues = new List<LevelValidationIssue>();
            if (source == null)
            {
                issues.Add(new LevelValidationIssue(
                    LevelValidationSeverity.Error,
                    "document.missing",
                    "The level document is missing."));
                return issues;
            }

            LevelDocument document = source.DeepCopy();
            document.Normalize();
            var context = new LevelValidationContext(document, content, profile, issues);
            foreach (ILevelValidationRule rule in rules)
            {
                rule.Evaluate(context);
            }

            return issues;
        }
    }

    public static class LevelValidator
    {
        public const int MaximumEntityCount = 2048;

        private static readonly LevelValidationService DefaultService = new LevelValidationService(
            new ILevelValidationRule[]
            {
                new LevelDocumentValidationRule(),
                new LevelEnvironmentValidationRule(),
                new LevelDressingValidationRule(),
                new LevelOrganizationValidationRule(),
                new LevelEntityValidationRule(),
                new LevelGameplayMetadataValidationRule(),
                new LevelTraversalValidationRule(),
                new LevelScenarioValidationRule(),
                new LevelTerrainValidationRule(),
            });

        public static IReadOnlyList<LevelValidationIssue> Validate(
            LevelDocument document,
            ISet<string> knownArchetypeIds = null,
            LevelValidationProfile profile = LevelValidationProfile.Authoring)
        {
            return DefaultService.Validate(document, knownArchetypeIds, profile);
        }

        public static IReadOnlyList<LevelValidationIssue> Validate(
            LevelDocument document,
            LevelValidationContent content,
            LevelValidationProfile profile = LevelValidationProfile.Authoring)
        {
            return DefaultService.Validate(document, content, profile);
        }

        public static bool HasErrors(IReadOnlyList<LevelValidationIssue> issues)
        {
            return issues != null
                && issues.Any(issue => issue?.Severity == LevelValidationSeverity.Error);
        }
    }

}
