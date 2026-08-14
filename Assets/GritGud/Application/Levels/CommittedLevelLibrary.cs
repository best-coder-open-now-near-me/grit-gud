using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Levels;

namespace GritGud.Application.Levels
{
    public sealed class CommittedLevelSource
    {
        public CommittedLevelSource(
            string resourceKey,
            string fallbackDisplayName,
            string serializedDocument)
        {
            ResourceKey = string.IsNullOrWhiteSpace(resourceKey)
                ? throw new ArgumentException(
                    "A committed level source needs a resource key.",
                    nameof(resourceKey))
                : resourceKey.Trim();
            FallbackDisplayName = string.IsNullOrWhiteSpace(fallbackDisplayName)
                ? ResourceKey
                : fallbackDisplayName.Trim();
            SerializedDocument = serializedDocument ?? string.Empty;
        }

        public string ResourceKey { get; }

        public string FallbackDisplayName { get; }

        public string SerializedDocument { get; }
    }

    public sealed class CommittedLevelEntry
    {
        private readonly LevelDocument document;

        internal CommittedLevelEntry(
            string resourceKey,
            string levelId,
            string displayName,
            LevelDocument document,
            IReadOnlyList<LevelValidationIssue> authoringIssues,
            IReadOnlyList<LevelValidationIssue> publishIssues,
            string sourceError,
            string identityError)
        {
            ResourceKey = resourceKey;
            LevelId = levelId ?? string.Empty;
            DisplayName = displayName;
            this.document = document?.DeepCopy();
            AuthoringIssues = authoringIssues ?? Array.Empty<LevelValidationIssue>();
            PublishIssues = publishIssues ?? Array.Empty<LevelValidationIssue>();
            SourceError = sourceError ?? string.Empty;
            IdentityError = identityError ?? string.Empty;

            CanEdit = this.document != null
                && string.IsNullOrEmpty(SourceError)
                && string.IsNullOrEmpty(IdentityError)
                && !LevelValidator.HasErrors(AuthoringIssues);
            CanPlay = CanEdit && !LevelValidator.HasErrors(PublishIssues);
            StatusMessage = ResolveStatusMessage();
        }

        public string ResourceKey { get; }

        public string LevelId { get; }

        public string DisplayName { get; }

        public IReadOnlyList<LevelValidationIssue> AuthoringIssues { get; }

        public IReadOnlyList<LevelValidationIssue> PublishIssues { get; }

        public string SourceError { get; }

        public string IdentityError { get; }

        public bool CanEdit { get; }

        public bool CanPlay { get; }

        public string StatusMessage { get; }

        internal LevelDocument CreateSnapshot()
        {
            return document?.DeepCopy();
        }

        private string ResolveStatusMessage()
        {
            if (!string.IsNullOrEmpty(SourceError))
            {
                return SourceError;
            }

            if (!string.IsNullOrEmpty(IdentityError))
            {
                return IdentityError;
            }

            LevelValidationIssue authoringError = AuthoringIssues.FirstOrDefault(
                issue => issue?.Severity == LevelValidationSeverity.Error);
            if (authoringError != null)
            {
                return authoringError.Message;
            }

            LevelValidationIssue publishError = PublishIssues.FirstOrDefault(
                issue => issue?.Severity == LevelValidationSeverity.Error);
            return publishError != null
                ? publishError.Message
                : "Ready to play and edit.";
        }
    }

    public sealed class CommittedLevelLibrary
    {
        private sealed class Candidate
        {
            public CommittedLevelSource Source { get; set; }

            public LevelDocument Document { get; set; }

            public IReadOnlyList<LevelValidationIssue> AuthoringIssues { get; set; } =
                Array.Empty<LevelValidationIssue>();

            public IReadOnlyList<LevelValidationIssue> PublishIssues { get; set; } =
                Array.Empty<LevelValidationIssue>();

            public string SourceError { get; set; } = string.Empty;

            public string IdentityError { get; set; } = string.Empty;
        }

        private readonly CommittedLevelEntry[] entries;
        private readonly Dictionary<string, CommittedLevelEntry> entriesByResourceKey;

        public CommittedLevelLibrary(
            IEnumerable<CommittedLevelSource> sources,
            ILevelSerializer serializer,
            LevelValidationContent validationContent)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (validationContent?.HasActorTemplateCatalog != true)
            {
                throw new ArgumentException(
                    "Committed levels require an actor-template validation catalog.",
                    nameof(validationContent));
            }

            Candidate[] candidates = sources
                .Where(source => source != null)
                .Select(source => BuildCandidate(source, serializer, validationContent))
                .ToArray();
            RejectDuplicateResourceKeys(candidates);
            RejectDuplicateLevelIds(candidates);

            entries = candidates
                .Select(CreateEntry)
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.ResourceKey, StringComparer.Ordinal)
                .ToArray();
            entriesByResourceKey = entries
                .GroupBy(entry => entry.ResourceKey, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single(),
                    StringComparer.Ordinal);
        }

        public IReadOnlyList<CommittedLevelEntry> Entries => entries;

        public CommittedLevelEntry Find(string resourceKey)
        {
            return resourceKey != null
                && entriesByResourceKey.TryGetValue(resourceKey, out CommittedLevelEntry entry)
                    ? entry
                    : null;
        }

        public LevelDocument OpenForEditing(string resourceKey)
        {
            CommittedLevelEntry entry = RequireEntry(resourceKey);
            if (!entry.CanEdit)
            {
                throw new InvalidOperationException(
                    $"Committed level '{entry.DisplayName}' cannot be edited: "
                    + entry.StatusMessage);
            }

            return entry.CreateSnapshot();
        }

        public LevelDocument OpenForPlay(string resourceKey)
        {
            CommittedLevelEntry entry = RequireEntry(resourceKey);
            if (!entry.CanPlay)
            {
                throw new InvalidOperationException(
                    $"Committed level '{entry.DisplayName}' cannot be played: "
                    + entry.StatusMessage);
            }

            return entry.CreateSnapshot();
        }

        private static Candidate BuildCandidate(
            CommittedLevelSource source,
            ILevelSerializer serializer,
            LevelValidationContent validationContent)
        {
            var candidate = new Candidate { Source = source };
            try
            {
                candidate.Document = serializer.Deserialize(source.SerializedDocument);
                candidate.AuthoringIssues = LevelValidator.Validate(
                    candidate.Document,
                    validationContent,
                    LevelValidationProfile.Authoring);
                candidate.PublishIssues = LevelValidator.Validate(
                    candidate.Document,
                    validationContent,
                    LevelValidationProfile.Publish);
            }
            catch (LevelSerializationException exception)
            {
                candidate.SourceError = exception.Message;
            }

            return candidate;
        }

        private static void RejectDuplicateResourceKeys(IEnumerable<Candidate> candidates)
        {
            foreach (IGrouping<string, Candidate> group in candidates.GroupBy(
                candidate => candidate.Source.ResourceKey,
                StringComparer.Ordinal))
            {
                if (group.Count() <= 1)
                {
                    continue;
                }

                foreach (Candidate candidate in group)
                {
                    candidate.IdentityError =
                        $"Committed resource key '{group.Key}' is duplicated.";
                }
            }
        }

        private static void RejectDuplicateLevelIds(IEnumerable<Candidate> candidates)
        {
            foreach (IGrouping<string, Candidate> group in candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Document?.levelId))
                .GroupBy(candidate => candidate.Document.levelId, StringComparer.Ordinal))
            {
                if (group.Count() <= 1)
                {
                    continue;
                }

                foreach (Candidate candidate in group)
                {
                    candidate.IdentityError =
                        $"Committed level ID '{group.Key}' is duplicated.";
                }
            }
        }

        private static CommittedLevelEntry CreateEntry(Candidate candidate)
        {
            string displayName = string.IsNullOrWhiteSpace(candidate.Document?.displayName)
                ? candidate.Source.FallbackDisplayName
                : candidate.Document.displayName.Trim();
            return new CommittedLevelEntry(
                candidate.Source.ResourceKey,
                candidate.Document?.levelId,
                displayName,
                candidate.Document,
                candidate.AuthoringIssues,
                candidate.PublishIssues,
                candidate.SourceError,
                candidate.IdentityError);
        }

        private CommittedLevelEntry RequireEntry(string resourceKey)
        {
            CommittedLevelEntry entry = Find(resourceKey);
            return entry ?? throw new InvalidOperationException(
                $"Committed level resource '{resourceKey}' was not found.");
        }
    }
}
