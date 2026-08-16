using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelDraftLibraryCoordinatorTests
    {
        [Test]
        public void RefreshPublishesAndSelectsOneDraftByStableId()
        {
            var repository = new StubRepository
            {
                Drafts = new[] { Summary("one", "One"), Summary("two", "Two") },
            };
            using var coordinator = new LevelDraftLibraryCoordinator(
                new LevelDraftLibraryService(repository));
            int changes = 0;
            coordinator.Changed += () => changes++;

            coordinator.Refresh();
            coordinator.Select(new LevelDraftId("two"));

            Assert.That(coordinator.HasLoaded, Is.True);
            Assert.That(coordinator.Selected.Name, Is.EqualTo("Two"));
            Assert.That(changes, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void SelectingAnUnknownDraftIsRejected()
        {
            var repository = new StubRepository { Drafts = new[] { Summary("one", "One") } };
            using var coordinator = new LevelDraftLibraryCoordinator(
                new LevelDraftLibraryService(repository));
            coordinator.Refresh();

            Assert.Throws<ArgumentException>(() => coordinator.Select(new LevelDraftId("missing")));
        }

        [Test]
        public async Task CreatingADraftRefreshesAndSelectsItsStableId()
        {
            LevelDraftSummary createdSummary = Summary("created", "Created Draft");
            var repository = new StubRepository
            {
                CreateResult = new LevelDraftRecord(
                    createdSummary,
                    new LevelDocument { levelId = "level.created", displayName = "Created" }),
                Drafts = new[] { createdSummary },
            };
            using var coordinator = new LevelDraftLibraryCoordinator(
                new LevelDraftLibraryService(repository));

            LevelDraftRecord result = await coordinator.CreateAsync(
                "Created Draft",
                new LevelDocument { levelId = "level.created", displayName = "Created" });

            Assert.That(result.Summary.Id.Value, Is.EqualTo("created"));
            Assert.That(coordinator.SelectedId?.Value, Is.EqualTo("created"));
            Assert.That(coordinator.Status, Is.EqualTo("Created cloud draft."));
        }

        private static LevelDraftSummary Summary(string id, string name) =>
            new LevelDraftSummary(new LevelDraftId(id), name, 1, DateTimeOffset.UtcNow);

        private sealed class StubRepository : ILevelDraftRepository
        {
            public IReadOnlyList<LevelDraftSummary> Drafts { get; set; } = Array.Empty<LevelDraftSummary>();
            public LevelDraftRecord CreateResult { get; set; }
            public Task<IReadOnlyList<LevelDraftSummary>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(Drafts);
            public Task<LevelDraftRecord> CreateAsync(string name, LevelDocument document, CancellationToken cancellationToken) => Task.FromResult(CreateResult);
            public Task<LevelDraftRecord> LoadAsync(LevelDraftId id, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<LevelDraftSummary> SaveAsync(LevelDraftId id, long expectedRevision, LevelDocument document, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<LevelDraftSummary> RenameAsync(LevelDraftId id, string name, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<LevelDraftRecord> DuplicateAsync(LevelDraftId id, string name, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task DeleteAsync(LevelDraftId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        }
    }
}
