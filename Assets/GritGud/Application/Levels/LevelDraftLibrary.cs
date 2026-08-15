using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Domain.Levels;

namespace GritGud.Application.Levels
{
    public readonly struct LevelDraftId : IEquatable<LevelDraftId>
    {
        public LevelDraftId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("A draft ID is required.", nameof(value))
                : value.Trim();
        }

        public string Value { get; }

        public bool Equals(LevelDraftId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LevelDraftId other && Equals(other);

        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;
    }

    public static class LevelDraftName
    {
        public const int MaximumLength = 64;

        public static string Normalize(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
                throw new ArgumentException("A draft name is required.", nameof(value));
            if (normalized.Length > MaximumLength)
                throw new ArgumentException($"Draft names support at most {MaximumLength} characters.", nameof(value));
            if (normalized.Any(char.IsControl))
                throw new ArgumentException("Draft names cannot contain control characters.", nameof(value));
            return normalized;
        }
    }

    public sealed class LevelDraftSummary
    {
        public LevelDraftSummary(
            LevelDraftId id,
            string name,
            long revision,
            DateTimeOffset updatedAt,
            string levelId = "",
            string displayName = "",
            int schemaVersion = 0)
        {
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            Id = id;
            Name = LevelDraftName.Normalize(name);
            Revision = revision;
            UpdatedAt = updatedAt;
            LevelId = levelId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SchemaVersion = schemaVersion;
        }

        public LevelDraftId Id { get; }
        public string Name { get; }
        public long Revision { get; }
        public DateTimeOffset UpdatedAt { get; }
        public string LevelId { get; }
        public string DisplayName { get; }
        public int SchemaVersion { get; }
    }

    public sealed class LevelDraftRecord
    {
        private readonly LevelDocument document;

        public LevelDraftRecord(LevelDraftSummary summary, LevelDocument document)
        {
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            this.document = document?.DeepCopy() ?? throw new ArgumentNullException(nameof(document));
        }

        public LevelDraftSummary Summary { get; }

        public LevelDocument CreateDocumentSnapshot() => document.DeepCopy();
    }

    public enum LevelDraftFailure
    {
        Unknown,
        Unauthenticated,
        NotFound,
        NameConflict,
        RevisionConflict,
        Unavailable,
    }

    public sealed class LevelDraftOperationException : Exception
    {
        public LevelDraftOperationException(LevelDraftFailure failure, string message, Exception inner = null)
            : base(message, inner)
        {
            Failure = failure;
        }

        public LevelDraftFailure Failure { get; }
    }

    public interface ILevelDraftRepository
    {
        Task<IReadOnlyList<LevelDraftSummary>> ListAsync(CancellationToken cancellationToken);
        Task<LevelDraftRecord> CreateAsync(string name, LevelDocument document, CancellationToken cancellationToken);
        Task<LevelDraftRecord> LoadAsync(LevelDraftId id, CancellationToken cancellationToken);
        Task<LevelDraftSummary> SaveAsync(LevelDraftId id, long expectedRevision, LevelDocument document, CancellationToken cancellationToken);
        Task<LevelDraftSummary> RenameAsync(LevelDraftId id, string name, CancellationToken cancellationToken);
        Task<LevelDraftRecord> DuplicateAsync(LevelDraftId id, string name, CancellationToken cancellationToken);
        Task DeleteAsync(LevelDraftId id, CancellationToken cancellationToken);
    }

    public sealed class LevelDraftLibraryService
    {
        private readonly ILevelDraftRepository repository;

        public LevelDraftLibraryService(ILevelDraftRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<IReadOnlyList<LevelDraftSummary>> ListAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<LevelDraftSummary> drafts = await repository.ListAsync(cancellationToken);
            return (drafts ?? Array.Empty<LevelDraftSummary>())
                .Where(draft => draft != null)
                .OrderByDescending(draft => draft.UpdatedAt)
                .ThenBy(draft => draft.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public Task<LevelDraftRecord> CreateAsync(string name, LevelDocument document, CancellationToken cancellationToken = default) =>
            repository.CreateAsync(LevelDraftName.Normalize(name), RequireDocument(document), cancellationToken);

        public Task<LevelDraftRecord> LoadAsync(LevelDraftId id, CancellationToken cancellationToken = default) =>
            repository.LoadAsync(id, cancellationToken);

        public Task<LevelDraftSummary> SaveAsync(LevelDraftId id, long expectedRevision, LevelDocument document, CancellationToken cancellationToken = default)
        {
            if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            return repository.SaveAsync(id, expectedRevision, RequireDocument(document), cancellationToken);
        }

        public Task<LevelDraftSummary> RenameAsync(LevelDraftId id, string name, CancellationToken cancellationToken = default) =>
            repository.RenameAsync(id, LevelDraftName.Normalize(name), cancellationToken);

        public Task<LevelDraftRecord> DuplicateAsync(LevelDraftId id, string name, CancellationToken cancellationToken = default) =>
            repository.DuplicateAsync(id, LevelDraftName.Normalize(name), cancellationToken);

        public Task DeleteAsync(LevelDraftId id, CancellationToken cancellationToken = default) =>
            repository.DeleteAsync(id, cancellationToken);

        private static LevelDocument RequireDocument(LevelDocument document) =>
            document?.DeepCopy() ?? throw new ArgumentNullException(nameof(document));
    }
}
