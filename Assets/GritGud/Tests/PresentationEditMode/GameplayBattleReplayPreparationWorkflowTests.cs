using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayBattleReplayPreparationWorkflowTests
    {
        public enum Stage
        {
            ArtifactLoad,
            ContentMatch,
            InitialState,
            Simulation,
            Verification,
        }

        [Test]
        public async Task CancellationBeforePreparationRunsNoStage()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var stages = new StageRecorder(cancellation, null);

            await AssertCanceledAsync(
                PrepareAsync(stages, cancellation.Token));
            Assert.That(stages.Completed, Is.Empty);
        }

        [TestCase(Stage.ArtifactLoad)]
        [TestCase(Stage.ContentMatch)]
        [TestCase(Stage.InitialState)]
        [TestCase(Stage.Simulation)]
        [TestCase(Stage.Verification)]
        public async Task CancellationAtStageBoundaryStopsEveryLaterStage(
            Stage cancellationStage)
        {
            using var cancellation = new CancellationTokenSource();
            var stages = new StageRecorder(
                cancellation,
                cancellationStage);

            await AssertCanceledAsync(
                PrepareAsync(stages, cancellation.Token));

            var expected = new List<Stage>();
            foreach (Stage stage in Enum.GetValues(typeof(Stage)))
            {
                expected.Add(stage);
                if (stage == cancellationStage)
                    break;
            }
            Assert.That(stages.Completed, Is.EqualTo(expected));
        }

        [Test]
        public async Task CancellationWhileSimulationIsPendingCannotVerify()
        {
            using var cancellation = new CancellationTokenSource();
            var simulation = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var stages = new StageRecorder(
                cancellation,
                cancellationStage: null,
                simulation.Task);

            Task<GameplayBattleReplayPreparationResult<object, object>> task =
                PrepareAsync(stages, cancellation.Token);
            await stages.SimulationStarted.Task;
            cancellation.Cancel();
            simulation.SetResult(stages.Run);

            await AssertCanceledAsync(task);
            Assert.That(
                stages.Completed,
                Is.EqualTo(new[]
                {
                    Stage.ArtifactLoad,
                    Stage.ContentMatch,
                    Stage.InitialState,
                    Stage.Simulation,
                }));
        }

        [Test]
        public async Task ContentMismatchStopsBeforeStateConstruction()
        {
            using var cancellation = new CancellationTokenSource();
            var stages = new StageRecorder(cancellation, null)
            {
                ContentMatches = false,
            };

            GameplayBattleReplayPreparationResult<object, object> result =
                await PrepareAsync(stages, cancellation.Token);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Artifact, Is.SameAs(stages.Artifact));
            Assert.That(result.Replay, Is.Null);
            Assert.That(
                stages.Completed,
                Is.EqualTo(new[]
                {
                    Stage.ArtifactLoad,
                    Stage.ContentMatch,
                }));
        }

        [Test]
        public async Task SuccessfulPreparationReturnsOnlyVerifiedReplay()
        {
            using var cancellation = new CancellationTokenSource();
            var stages = new StageRecorder(cancellation, null);

            GameplayBattleReplayPreparationResult<object, object> result =
                await PrepareAsync(stages, cancellation.Token);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Artifact, Is.SameAs(stages.Artifact));
            Assert.That(result.Replay, Is.SameAs(stages.Replay));
            Assert.That(
                stages.Completed,
                Is.EqualTo((Stage[])Enum.GetValues(typeof(Stage))));
        }

        [Test]
        public void MissingHistoricalWeaponVisualRefusesReplayWithIdentity()
        {
            WeaponPresentationCatalog catalog =
                WeaponPresentationCatalog.CreateRuntime();
            try
            {
                InvalidOperationException failure = Assert.Throws<
                    InvalidOperationException>(() =>
                    GameplayFirstSimulationPreparationService
                        .RequireHistoricalWeaponPresentation(
                            catalog,
                            "historical-actor",
                            "weapon.removed",
                            transitionSequence: 42));

                Assert.That(failure.Message, Does.Contain("transition 42"));
                Assert.That(failure.Message,
                    Does.Contain("historical-actor"));
                Assert.That(failure.Message, Does.Contain("weapon.removed"));
                Assert.That(failure.Message,
                    Does.Contain("presentation catalog"));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        private static Task<GameplayBattleReplayPreparationResult<
            object,
            object>> PrepareAsync(
            StageRecorder stages,
            CancellationToken cancellationToken) =>
            GameplayBattleReplayPreparationWorkflow.PrepareAsync(
                stages.LoadArtifact,
                stages.MatchesContent,
                stages.CreateInitialState,
                stages.RunSimulationAsync,
                stages.Verify,
                cancellationToken);

        private static async Task AssertCanceledAsync(Task task)
        {
            try
            {
                await task;
                Assert.Fail("Expected replay preparation to be cancelled.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        private sealed class StageRecorder
        {
            private readonly CancellationTokenSource cancellation;
            private readonly Stage? cancellationStage;
            private readonly Task<object> simulation;

            public StageRecorder(
                CancellationTokenSource cancellation,
                Stage? cancellationStage,
                Task<object> simulation = null)
            {
                this.cancellation = cancellation;
                this.cancellationStage = cancellationStage;
                this.simulation = simulation;
            }

            public object Artifact { get; } = new object();
            public object InitialState { get; } = new object();
            public object Run { get; } = new object();
            public object Replay { get; } = new object();
            public bool ContentMatches { get; set; } = true;
            public List<Stage> Completed { get; } = new List<Stage>();
            public TaskCompletionSource<bool> SimulationStarted { get; } =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public object LoadArtifact()
            {
                Complete(Stage.ArtifactLoad);
                return Artifact;
            }

            public bool MatchesContent(object artifact)
            {
                Assert.That(artifact, Is.SameAs(Artifact));
                Complete(Stage.ContentMatch);
                return ContentMatches;
            }

            public object CreateInitialState()
            {
                Complete(Stage.InitialState);
                return InitialState;
            }

            public async Task<object> RunSimulationAsync(
                object initialState,
                object artifact,
                CancellationToken cancellationToken)
            {
                Assert.That(initialState, Is.SameAs(InitialState));
                Assert.That(artifact, Is.SameAs(Artifact));
                Complete(Stage.Simulation);
                SimulationStarted.TrySetResult(true);
                return simulation == null
                    ? Run
                    : await simulation;
            }

            public object Verify(object run, object artifact)
            {
                Assert.That(run, Is.SameAs(Run));
                Assert.That(artifact, Is.SameAs(Artifact));
                Complete(Stage.Verification);
                return Replay;
            }

            private void Complete(Stage stage)
            {
                Completed.Add(stage);
                if (cancellationStage == stage)
                    cancellation.Cancel();
            }
        }
    }
}
