using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class LevelDraftLibraryTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void DraftIdRejectsMissingValues(string value) =>
            Assert.Throws<ArgumentException>(() => new LevelDraftId(value));

        [Test]
        public void DraftNameNormalizesWhitespaceAndRejectsControlCharacters()
        {
            Assert.That(LevelDraftName.Normalize("  Depot Night  "), Is.EqualTo("Depot Night"));
            Assert.Throws<ArgumentException>(() => LevelDraftName.Normalize("Depot\nNight"));
        }

        [Test]
        public async Task ListOrdersNewestFirstAndNameBreaksTies()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var repository = new RecordingRepository
            {
                Drafts = new[]
                {
                    Summary("b", "Bravo", 1, now),
                    Summary("c", "Charlie", 1, now.AddMinutes(1)),
                    Summary("a", "Alpha", 1, now),
                },
            };

            IReadOnlyList<LevelDraftSummary> result =
                await new LevelDraftLibraryService(repository).ListAsync();

            Assert.That(result[0].Name, Is.EqualTo("Charlie"));
            Assert.That(result[1].Name, Is.EqualTo("Alpha"));
            Assert.That(result[2].Name, Is.EqualTo("Bravo"));
        }

        [Test]
        public void SaveRejectsAnInvalidExpectedRevision()
        {
            var service = new LevelDraftLibraryService(new RecordingRepository());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                service.SaveAsync(new LevelDraftId("draft"), 0, NewLevel()));
        }

        [Test]
        public void RecordReturnsDetachedDocumentSnapshots()
        {
            var record = new LevelDraftRecord(
                Summary("draft", "Draft", 1, DateTimeOffset.UtcNow),
                NewLevel("Original"));

            LevelDocument first = record.CreateDocumentSnapshot();
            first.displayName = "Changed";

            Assert.That(record.CreateDocumentSnapshot().displayName, Is.EqualTo("Original"));
        }

        private static LevelDraftSummary Summary(string id, string name, long revision, DateTimeOffset updatedAt) =>
            new LevelDraftSummary(new LevelDraftId(id), name, revision, updatedAt);

        private static LevelDocument NewLevel(string name = "Draft") =>
            new LevelDocument { levelId = "level.test", displayName = name };

        private sealed class RecordingRepository : ILevelDraftRepository
        {
            public IReadOnlyList<LevelDraftSummary> Drafts { get; set; } = Array.Empty<LevelDraftSummary>();
            public Task<IReadOnlyList<LevelDraftSummary>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(Drafts);
            public Task<LevelDraftRecord> CreateAsync(string name, LevelDocument document, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<LevelDraftRecord> LoadAsync(LevelDraftId id, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<LevelDraftSummary> SaveAsync(LevelDraftId id, long expectedRevision, LevelDocument document, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<LevelDraftSummary> RenameAsync(LevelDraftId id, string name, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<LevelDraftRecord> DuplicateAsync(LevelDraftId id, string name, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task DeleteAsync(LevelDraftId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        }
    }
}
