using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayFeatureInstallationPipelineTests
    {
        [Test]
        public void PipelineInstallsEveryStageInDeclaredOrder()
        {
            var installed = new List<GameplayFeatureStage>();
            IGameplayFeatureInstaller[] installers = CreateInstallers(
                stage => installed.Add(stage));
            var pipeline = new GameplayFeatureInstallationPipeline(
                installers,
                () => Assert.Fail("Successful installation must not roll back."));

            pipeline.InstallAll();

            Assert.That(
                installed,
                Is.EqualTo(Enum.GetValues(typeof(GameplayFeatureStage))));
        }

        [Test]
        public void FailureStopsLaterStagesAndRollsBackImmediately()
        {
            var installed = new List<GameplayFeatureStage>();
            int rollbackCount = 0;
            IGameplayFeatureInstaller[] installers = CreateInstallers(stage =>
            {
                installed.Add(stage);
                if (stage == GameplayFeatureStage.Hotbar)
                    throw new InvalidOperationException("bind failed");
            });
            var pipeline = new GameplayFeatureInstallationPipeline(
                installers,
                () => rollbackCount++);

            GameplayFeatureInstallationException exception = Assert.Throws<
                GameplayFeatureInstallationException>(pipeline.InstallAll);

            Assert.That(exception.Stage, Is.EqualTo(GameplayFeatureStage.Hotbar));
            Assert.That(exception.InnerException.Message, Is.EqualTo("bind failed"));
            Assert.That(rollbackCount, Is.EqualTo(1));
            Assert.That(
                installed,
                Is.EqualTo(new[]
                {
                    GameplayFeatureStage.TargetingAndMovement,
                    GameplayFeatureStage.ActorActions,
                    GameplayFeatureStage.ProjectileAndConsumableDelivery,
                    GameplayFeatureStage.Hotbar,
                }));
        }

        [Test]
        public void PipelineRejectsMissingOrMisorderedStagesBeforeBinding()
        {
            IGameplayFeatureInstaller[] installers = CreateInstallers(_ => { });
            IGameplayFeatureInstaller displaced = installers[0];
            installers[0] = installers[1];
            installers[1] = displaced;

            Assert.Throws<ArgumentException>(() =>
                new GameplayFeatureInstallationPipeline(installers, () => { }));
        }

        [Test]
        public void EveryStageHasAFocusedProductionInstaller()
        {
            Type[] installerTypes = typeof(GameplayController)
                .Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract
                    && typeof(IGameplayFeatureInstaller).IsAssignableFrom(type))
                .ToArray();

            Assert.That(installerTypes, Has.Length.EqualTo(
                Enum.GetValues(typeof(GameplayFeatureStage)).Length));
            Assert.That(
                installerTypes,
                Has.None.EqualTo(typeof(GameplayController)));
        }

        private static IGameplayFeatureInstaller[] CreateInstallers(
            Action<GameplayFeatureStage> install) =>
            ((GameplayFeatureStage[])Enum.GetValues(typeof(GameplayFeatureStage)))
                .Select(stage =>
                    (IGameplayFeatureInstaller)new StubInstaller(stage, install))
                .ToArray();

        private sealed class StubInstaller : IGameplayFeatureInstaller
        {
            private readonly Action<GameplayFeatureStage> install;

            public StubInstaller(
                GameplayFeatureStage stage,
                Action<GameplayFeatureStage> install)
            {
                Stage = stage;
                this.install = install;
            }

            public GameplayFeatureStage Stage { get; }
            public void Install() => install(Stage);
        }
    }
}
