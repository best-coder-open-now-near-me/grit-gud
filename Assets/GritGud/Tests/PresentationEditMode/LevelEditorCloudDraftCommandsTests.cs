using System;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Persistence;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorCloudDraftCommandsTests
    {
        [Test]
        public async Task SaveIsTaskReturningAndRejectsConcurrentCommands()
        {
            var gateway = new StubGateway();
            var host = new StubHost();
            var pending = new TaskCompletionSource<LevelDraftRecord>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            gateway.CreateTask = pending.Task;
            using var commands = new LevelEditorCloudDraftCommands(gateway, host);

            Task first = commands.SaveAsync();
            Task rejected = commands.SaveAsync();

            Assert.That(commands.IsRunning, Is.True);
            Assert.That(gateway.CreateCalls, Is.EqualTo(1));
            Assert.That(rejected.IsCompleted, Is.True);

            LevelDraftRecord created = Record("draft-1", "First", 1);
            pending.SetResult(created);
            await first;

            Assert.That(commands.IsRunning, Is.False);
            Assert.That(gateway.Adopted, Is.SameAs(created));
            Assert.That(host.SavedSourceCount, Is.EqualTo(1));
            Assert.That(host.SavedSourceLabel, Is.EqualTo("cloud draft: First"));
            Assert.That(host.MarkSavedCount, Is.EqualTo(1));
            Assert.That(host.Status, Is.EqualTo(gateway.Status));
        }

        [Test]
        public async Task SaveDoesNotMarkNewerWorkspaceRevisionAsSaved()
        {
            var gateway = new StubGateway
            {
                ActiveDraft = Record("draft-1", "First", 1),
            };
            var host = new StubHost();
            var pending = new TaskCompletionSource<LevelDraftSummary>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            gateway.SaveTask = pending.Task;
            using var commands = new LevelEditorCloudDraftCommands(gateway, host);

            Task save = commands.SaveAsync();
            host.Revision++;
            pending.SetResult(Summary("draft-1", "First", 2));
            await save;

            Assert.That(host.SavedSourceCount, Is.EqualTo(1));
            Assert.That(host.MarkSavedCount, Is.Zero);
            Assert.That(gateway.Adopted.Summary.Revision, Is.EqualTo(2));
        }

        [Test]
        public async Task DisposeCancelsPendingLoadAndSuppressesLateMutation()
        {
            var gateway = new StubGateway
            {
                ActiveDraft = Record("draft-1", "First", 1),
            };
            var host = new StubHost();
            var pending = new TaskCompletionSource<LevelDraftRecord>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            gateway.LoadTask = pending.Task;
            var commands = new LevelEditorCloudDraftCommands(gateway, host);

            Task load = commands.LoadAsync();
            Assert.That(gateway.LoadToken.CanBeCanceled, Is.True);
            commands.Dispose();
            pending.SetCanceled();
            await load;

            Assert.That(gateway.LoadToken.IsCancellationRequested, Is.True);
            Assert.That(gateway.Adopted, Is.Null);
            Assert.That(host.LoadedSourceCount, Is.Zero);
            Assert.That(commands.IsRunning, Is.False);
        }

        [Test]
        public async Task LateSaveCompletionAfterDisposeCannotAdoptOrChangeSource()
        {
            var gateway = new StubGateway();
            var host = new StubHost();
            var pending = new TaskCompletionSource<LevelDraftRecord>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            gateway.CreateTask = pending.Task;
            var commands = new LevelEditorCloudDraftCommands(gateway, host);

            Task save = commands.SaveAsync();
            commands.Dispose();
            pending.SetResult(Record("draft-1", "First", 1));
            await save;

            Assert.That(gateway.Adopted, Is.Null);
            Assert.That(host.SavedSourceCount, Is.Zero);
            Assert.That(host.MarkSavedCount, Is.Zero);
        }

        private static LevelDraftSummary Summary(
            string id,
            string name,
            long revision) =>
            new LevelDraftSummary(
                new LevelDraftId(id),
                name,
                revision,
                DateTimeOffset.UtcNow);

        private static LevelDraftRecord Record(
            string id,
            string name,
            long revision) =>
            new LevelDraftRecord(
                Summary(id, name, revision),
                new LevelDocument
                {
                    levelId = "level-" + id,
                    displayName = name,
                });

        private sealed class StubGateway : ILevelEditorCloudDraftGateway
        {
            public bool IsAvailable { get; set; } = true;
            public string UnavailableStatus { get; set; } =
                "Cloud unavailable.";
            public string Status { get; set; } = "Cloud command complete.";
            public LevelDraftRecord ActiveDraft { get; set; }
            public LevelDraftRecord Adopted { get; private set; }
            public int CreateCalls { get; private set; }
            public Task<LevelDraftRecord> CreateTask { get; set; }
            public Task<LevelDraftSummary> SaveTask { get; set; }
            public Task<LevelDraftRecord> LoadTask { get; set; }
            public CancellationToken LoadToken { get; private set; }

            public Task<LevelDraftRecord> CreateAsync(
                string name,
                LevelDocument document)
            {
                CreateCalls++;
                return CreateTask ?? Task.FromResult(
                    Record("created", name, 1));
            }

            public Task<LevelDraftSummary> SaveAsync(
                LevelDraftId id,
                long expectedRevision,
                LevelDocument document) =>
                SaveTask ?? Task.FromResult(
                    Summary(id.Value, ActiveDraft.Summary.Name, expectedRevision + 1));

            public Task<LevelDraftRecord> LoadAsync(
                LevelDraftId id,
                CancellationToken cancellationToken)
            {
                LoadToken = cancellationToken;
                return LoadTask ?? Task.FromResult(ActiveDraft);
            }

            public void Adopt(LevelDraftRecord draft)
            {
                Adopted = draft;
                ActiveDraft = draft;
            }
        }

        private sealed class StubHost : ILevelEditorCloudDraftHost
        {
            public bool IsReady { get; set; } = true;
            public int Revision { get; set; } = 1;
            public int MarkSavedCount { get; private set; }
            public int SavedSourceCount { get; private set; }
            public int LoadedSourceCount { get; private set; }
            public string SavedSourceLabel { get; private set; }
            public string Status { get; private set; }
            public LevelDocument Snapshot { get; } = new LevelDocument
            {
                levelId = "working-level",
                displayName = "Working Level",
            };

            public LevelDocument CreateSnapshot() => Snapshot.DeepCopy();
            public void MarkSaved() => MarkSavedCount++;

            public void ApplySavedSource(
                LevelDocument document,
                string sourceLabel)
            {
                SavedSourceCount++;
                SavedSourceLabel = sourceLabel;
            }

            public void ApplyLoadedSource(
                LevelDocument document,
                string sourceLabel) =>
                LoadedSourceCount++;

            public void SetStatus(string message) => Status = message;
        }
    }
}
