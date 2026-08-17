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

        [Test]
        public async Task CreateKeepsCommittedDraftWhenRefreshFails()
        {
            LevelDraftSummary created = Summary(
                "created",
                "Created Draft",
                revision: 1);
            var repository = new StubRepository
            {
                CreateResult = Record(created),
                ListException = new InvalidOperationException("list unavailable"),
            };
            using var coordinator = CreateCoordinator(repository);

            LevelDraftRecord result = await coordinator.CreateAsync(
                "Created Draft",
                NewLevel("level.created"));

            Assert.That(result.Summary, Is.SameAs(created));
            Assert.That(coordinator.Drafts, Is.EqualTo(new[] { created }));
            Assert.That(coordinator.SelectedId, Is.EqualTo(created.Id));
            Assert.That(coordinator.Status, Does.StartWith("Created cloud draft."));
            Assert.That(coordinator.Status, Does.Contain("write succeeded"));
            Assert.That(coordinator.Status, Does.Contain("list unavailable"));
        }

        [Test]
        public async Task SaveKeepsReturnedRevisionWhenRefreshFails()
        {
            LevelDraftSummary original = Summary("draft", "Draft", revision: 1);
            LevelDraftSummary saved = Summary("draft", "Draft", revision: 2);
            var repository = new StubRepository
            {
                Drafts = new[] { original },
                SaveResult = saved,
            };
            using var coordinator = CreateCoordinator(repository);
            coordinator.Refresh();
            repository.ListException = new InvalidOperationException("offline");

            LevelDraftSummary result = await coordinator.SaveAsync(
                original.Id,
                expectedRevision: 1,
                NewLevel("level.saved"));

            Assert.That(result, Is.SameAs(saved));
            Assert.That(coordinator.SelectedId, Is.EqualTo(saved.Id));
            Assert.That(coordinator.Drafts.Count, Is.EqualTo(1));
            Assert.That(coordinator.Drafts[0].Revision, Is.EqualTo(2));
            Assert.That(coordinator.Status, Does.Contain("write succeeded"));
        }

        [Test]
        public async Task StaleRefreshCannotRegressReturnedRevision()
        {
            LevelDraftSummary original = Summary("draft", "Draft", revision: 1);
            LevelDraftSummary saved = Summary("draft", "Draft", revision: 2);
            var repository = new StubRepository
            {
                Drafts = new[] { original },
                SaveResult = saved,
            };
            using var coordinator = CreateCoordinator(repository);

            await coordinator.SaveAsync(
                original.Id,
                expectedRevision: 1,
                NewLevel("level.saved"));

            Assert.That(coordinator.Drafts.Count, Is.EqualTo(1));
            Assert.That(coordinator.Drafts[0].Revision, Is.EqualTo(2));
            Assert.That(coordinator.Status, Is.EqualTo("Saved cloud draft."));
        }

        [Test]
        public async Task RenameKeepsCommittedSummaryWhenRefreshFails()
        {
            LevelDraftSummary original = Summary("draft", "Old Name", revision: 1);
            LevelDraftSummary renamed = Summary("draft", "New Name", revision: 2);
            var repository = new StubRepository
            {
                Drafts = new[] { original },
                RenameResult = renamed,
            };
            using var coordinator = CreateCoordinator(repository);
            coordinator.Refresh();
            repository.ListException = new InvalidOperationException("offline");

            await coordinator.RenameAsync(original.Id, "New Name");

            Assert.That(coordinator.Drafts.Count, Is.EqualTo(1));
            Assert.That(coordinator.Drafts[0].Name, Is.EqualTo("New Name"));
            Assert.That(coordinator.Drafts[0].Revision, Is.EqualTo(2));
            Assert.That(coordinator.Status, Does.Contain("write succeeded"));
        }

        [Test]
        public async Task DuplicateKeepsCommittedCopyWhenRefreshFails()
        {
            LevelDraftSummary original = Summary("draft", "Draft", revision: 1);
            LevelDraftSummary duplicate = Summary("copy", "Draft Copy", revision: 1);
            var repository = new StubRepository
            {
                Drafts = new[] { original },
                DuplicateResult = Record(duplicate),
            };
            using var coordinator = CreateCoordinator(repository);
            coordinator.Refresh();
            repository.ListException = new InvalidOperationException("offline");

            LevelDraftRecord result = await coordinator.DuplicateAsync(
                original.Id,
                "Draft Copy");

            Assert.That(result.Summary, Is.SameAs(duplicate));
            Assert.That(coordinator.Drafts.Count, Is.EqualTo(2));
            Assert.That(coordinator.Drafts, Does.Contain(duplicate));
            Assert.That(coordinator.SelectedId, Is.EqualTo(duplicate.Id));
            Assert.That(coordinator.Status, Does.Contain("write succeeded"));
        }

        [Test]
        public async Task DeleteKeepsCommittedRemovalWhenRefreshFails()
        {
            LevelDraftSummary original = Summary("draft", "Draft", revision: 1);
            var repository = new StubRepository
            {
                Drafts = new[] { original },
            };
            using var coordinator = CreateCoordinator(repository);
            coordinator.Refresh();
            coordinator.Select(original.Id);
            repository.ListException = new InvalidOperationException("offline");

            await coordinator.DeleteAsync(original.Id);

            Assert.That(repository.DeletedId, Is.EqualTo(original.Id));
            Assert.That(coordinator.Drafts, Is.Empty);
            Assert.That(coordinator.SelectedId, Is.Null);
            Assert.That(coordinator.Status, Does.Contain("write succeeded"));
        }

        [Test]
        public void RevisionConflictPropagatesWithoutChangingLocalRevision()
        {
            LevelDraftSummary original = Summary("draft", "Draft", revision: 1);
            var repository = new StubRepository
            {
                Drafts = new[] { original },
                SaveException = new LevelDraftOperationException(
                    LevelDraftFailure.RevisionConflict,
                    "revision conflict"),
            };
            using var coordinator = CreateCoordinator(repository);
            coordinator.Refresh();

            LevelDraftOperationException exception = Assert.ThrowsAsync<
                LevelDraftOperationException>(async () => await coordinator.SaveAsync(
                    original.Id,
                    expectedRevision: 1,
                    NewLevel("level.conflict")));

            Assert.That(exception.Failure,
                Is.EqualTo(LevelDraftFailure.RevisionConflict));
            Assert.That(coordinator.Drafts[0].Revision, Is.EqualTo(1));
            Assert.That(repository.ListCallCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelledMutationDoesNotApplyOrRefresh()
        {
            var repository = new StubRepository
            {
                CreateException = new OperationCanceledException(
                    "create cancelled"),
            };
            using var coordinator = CreateCoordinator(repository);

            Assert.That(
                Assert.CatchAsync<OperationCanceledException>(async () =>
                    await coordinator.CreateAsync(
                        "Cancelled",
                        NewLevel("level.cancelled"))),
                Is.InstanceOf<OperationCanceledException>());

            Assert.That(coordinator.Drafts, Is.Empty);
            Assert.That(coordinator.SelectedId, Is.Null);
            Assert.That(repository.ListCallCount, Is.Zero);
        }

        [Test]
        public async Task CancelledRefreshAfterMutationIsReportedAsWarning()
        {
            LevelDraftSummary saved = Summary("draft", "Draft", revision: 2);
            var repository = new StubRepository
            {
                SaveResult = saved,
                ListException = new OperationCanceledException(
                    "refresh cancelled"),
            };
            using var coordinator = CreateCoordinator(repository);

            LevelDraftSummary result = await coordinator.SaveAsync(
                saved.Id,
                expectedRevision: 1,
                NewLevel("level.saved"));

            Assert.That(result, Is.SameAs(saved));
            Assert.That(coordinator.Drafts[0].Revision, Is.EqualTo(2));
            Assert.That(coordinator.Status, Does.Contain("write succeeded"));
            Assert.That(coordinator.Status, Does.Contain("refresh cancelled"));
        }

        private static LevelDraftLibraryCoordinator CreateCoordinator(
            StubRepository repository) =>
            new LevelDraftLibraryCoordinator(
                new LevelDraftLibraryService(repository));

        private static LevelDraftSummary Summary(
            string id,
            string name,
            long revision = 1) =>
            new LevelDraftSummary(
                new LevelDraftId(id),
                name,
                revision,
                DateTimeOffset.UtcNow.AddMinutes(revision));

        private static LevelDraftRecord Record(LevelDraftSummary summary) =>
            new LevelDraftRecord(
                summary,
                NewLevel("level." + summary.Id.Value));

        private static LevelDocument NewLevel(string levelId) =>
            new LevelDocument
            {
                levelId = levelId,
                displayName = levelId,
            };

        private sealed class StubRepository : ILevelDraftRepository
        {
            public IReadOnlyList<LevelDraftSummary> Drafts { get; set; } = Array.Empty<LevelDraftSummary>();
            public LevelDraftRecord CreateResult { get; set; }
            public LevelDraftSummary SaveResult { get; set; }
            public LevelDraftSummary RenameResult { get; set; }
            public LevelDraftRecord DuplicateResult { get; set; }
            public Exception ListException { get; set; }
            public Exception CreateException { get; set; }
            public Exception SaveException { get; set; }
            public LevelDraftId? DeletedId { get; private set; }
            public int ListCallCount { get; private set; }

            public Task<IReadOnlyList<LevelDraftSummary>> ListAsync(
                CancellationToken cancellationToken)
            {
                ListCallCount++;
                return ListException == null
                    ? Task.FromResult(Drafts)
                    : Task.FromException<IReadOnlyList<LevelDraftSummary>>(
                        ListException);
            }

            public Task<LevelDraftRecord> CreateAsync(
                string name,
                LevelDocument document,
                CancellationToken cancellationToken) =>
                CreateException == null
                    ? Task.FromResult(CreateResult)
                    : Task.FromException<LevelDraftRecord>(CreateException);

            public Task<LevelDraftRecord> LoadAsync(LevelDraftId id, CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<LevelDraftSummary> SaveAsync(
                LevelDraftId id,
                long expectedRevision,
                LevelDocument document,
                CancellationToken cancellationToken) =>
                SaveException == null
                    ? Task.FromResult(SaveResult)
                    : Task.FromException<LevelDraftSummary>(SaveException);

            public Task<LevelDraftSummary> RenameAsync(
                LevelDraftId id,
                string name,
                CancellationToken cancellationToken) =>
                Task.FromResult(RenameResult);

            public Task<LevelDraftRecord> DuplicateAsync(
                LevelDraftId id,
                string name,
                CancellationToken cancellationToken) =>
                Task.FromResult(DuplicateResult);

            public Task DeleteAsync(
                LevelDraftId id,
                CancellationToken cancellationToken)
            {
                DeletedId = id;
                return Task.CompletedTask;
            }
        }
    }
}
