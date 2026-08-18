using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Bootstrap;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class CloudDraftNavigationCommandsTests
    {
        [Test]
        public void PresentationAssemblyContainsNoAsyncVoidMethods()
        {
            string[] asyncVoidMethods = typeof(GameBootstrap)
                .Assembly
                .GetTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.NonPublic))
                .Where(method => method.ReturnType == typeof(void)
                    && method.GetCustomAttribute<AsyncStateMachineAttribute>()
                        != null)
                .Select(method => method.DeclaringType.FullName + "." + method.Name)
                .ToArray();

            Assert.That(asyncVoidMethods, Is.Empty);
        }

        [Test]
        public async Task NewNavigationCancelsAndSupersedesPendingNavigation()
        {
            var first = PendingRecord();
            var second = PendingRecord();
            var gateway = new StubGateway(first.Task, second.Task);
            var host = new StubHost();
            using var commands = new CloudDraftNavigationCommands(gateway, host);

            Task play = commands.PlayAsync(
                new LevelDraftId("first"),
                host.ReportStatus);
            Task edit = commands.OpenEditorAsync(
                new LevelDraftId("second"),
                host.ReportStatus);

            Assert.That(play.IsCompleted, Is.False);
            Assert.That(edit.IsCompleted, Is.False);
            Assert.That(gateway.Tokens[0].IsCancellationRequested, Is.True);
            second.SetResult(Record("second"));
            await edit;
            first.SetResult(Record("first"));
            await play;

            Assert.That(host.EditCount, Is.EqualTo(1));
            Assert.That(host.PlayCount, Is.Zero);
            Assert.That(commands.IsRunning, Is.False);
        }

        [Test]
        public async Task CancelSuppressesLateCompletion()
        {
            var pending = PendingRecord();
            var gateway = new StubGateway(pending.Task);
            var host = new StubHost();
            using var commands = new CloudDraftNavigationCommands(gateway, host);

            Task navigation = commands.OpenEditorAsync(
                new LevelDraftId("draft"),
                host.ReportStatus);
            commands.Cancel();
            pending.SetResult(Record("draft"));
            await navigation;

            Assert.That(gateway.Tokens[0].IsCancellationRequested, Is.True);
            Assert.That(host.EditCount, Is.Zero);
            Assert.That(commands.IsRunning, Is.False);
        }

        [Test]
        public async Task NavigationFailureIsReportedThroughTheUiBoundary()
        {
            var failure = new InvalidOperationException("cloud offline");
            var gateway = new StubGateway(
                Task.FromException<LevelDraftRecord>(failure));
            var host = new StubHost();
            using var commands = new CloudDraftNavigationCommands(gateway, host);

            await commands.OpenEditorAsync(
                new LevelDraftId("draft"),
                host.ReportStatus);

            Assert.That(host.Status, Is.EqualTo("cloud offline"));
            Assert.That(host.EditCount, Is.Zero);
        }

        [Test]
        public void PlayGuardReturnsACompletedTaskWithoutLoading()
        {
            var gateway = new StubGateway(RecordTask("draft"));
            var host = new StubHost { CanStartGameplay = false };
            using var commands = new CloudDraftNavigationCommands(gateway, host);

            Task play = commands.PlayAsync(
                new LevelDraftId("draft"),
                host.ReportStatus);

            Assert.That(play.IsCompleted, Is.True);
            Assert.That(gateway.LoadCount, Is.Zero);
        }

        private static TaskCompletionSource<LevelDraftRecord> PendingRecord() =>
            new TaskCompletionSource<LevelDraftRecord>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private static Task<LevelDraftRecord> RecordTask(string id) =>
            Task.FromResult(Record(id));

        private static LevelDraftRecord Record(string id) =>
            new LevelDraftRecord(
                new LevelDraftSummary(
                    new LevelDraftId(id),
                    id,
                    1,
                    DateTimeOffset.UtcNow),
                new LevelDocument
                {
                    levelId = "level-" + id,
                    displayName = id,
                });

        private sealed class StubGateway : ICloudDraftNavigationGateway
        {
            private readonly Queue<Task<LevelDraftRecord>> results;

            public StubGateway(params Task<LevelDraftRecord>[] results)
            {
                this.results = new Queue<Task<LevelDraftRecord>>(results);
            }

            public bool IsAvailable { get; set; } = true;
            public string UnavailableStatus { get; set; } = "Unavailable.";
            public List<CancellationToken> Tokens { get; } =
                new List<CancellationToken>();
            public int LoadCount { get; private set; }

            public Task<LevelDraftRecord> LoadAsync(
                LevelDraftId id,
                CancellationToken cancellationToken)
            {
                LoadCount++;
                Tokens.Add(cancellationToken);
                return results.Dequeue();
            }
        }

        private sealed class StubHost : ICloudDraftNavigationHost
        {
            public bool CanStartGameplay { get; set; } = true;
            public bool IsMenuActive { get; set; } = true;
            public int PlayCount { get; private set; }
            public int EditCount { get; private set; }
            public string Status { get; private set; }

            public void Play(LevelDraftRecord draft) => PlayCount++;
            public void Edit(LevelDraftRecord draft) => EditCount++;
            public void ReportStatus(string status) => Status = status;
        }
    }
}
