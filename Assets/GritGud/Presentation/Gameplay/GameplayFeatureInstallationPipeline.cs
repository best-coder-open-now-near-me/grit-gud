using System;
using System.Collections.Generic;

namespace GritGud.Presentation.Gameplay
{
    internal enum GameplayFeatureStage
    {
        TargetingAndMovement,
        ActorActions,
        ProjectileAndConsumableDelivery,
        Hotbar,
        EncounterActors,
        AimingPresentation,
        Objective,
        ControlRouting,
        ReplayPresentation,
        HudPresentation,
    }

    internal interface IGameplayFeatureInstaller
    {
        GameplayFeatureStage Stage { get; }
        void Install();
    }

    internal sealed class GameplayFeatureInstallationException : Exception
    {
        public GameplayFeatureInstallationException(
            GameplayFeatureStage stage,
            Exception innerException)
            : base($"Gameplay feature installation failed during '{stage}'.",
                innerException)
        {
            Stage = stage;
        }

        public GameplayFeatureStage Stage { get; }
    }

    internal sealed class GameplayFeatureInstallationPipeline
    {
        private readonly IReadOnlyList<IGameplayFeatureInstaller> installers;
        private readonly Action rollback;

        public GameplayFeatureInstallationPipeline(
            IReadOnlyList<IGameplayFeatureInstaller> installers,
            Action rollback)
        {
            this.installers = installers ?? throw new ArgumentNullException(
                nameof(installers));
            this.rollback = rollback ?? throw new ArgumentNullException(
                nameof(rollback));
            ValidateOrder(installers);
        }

        public void InstallAll()
        {
            foreach (IGameplayFeatureInstaller installer in installers)
            {
                try
                {
                    installer.Install();
                }
                catch (Exception exception)
                {
                    rollback();
                    throw new GameplayFeatureInstallationException(
                        installer.Stage,
                        exception);
                }
            }
        }

        private static void ValidateOrder(
            IReadOnlyList<IGameplayFeatureInstaller> values)
        {
            GameplayFeatureStage[] required =
                (GameplayFeatureStage[])Enum.GetValues(
                    typeof(GameplayFeatureStage));
            if (values.Count != required.Length)
            {
                throw new ArgumentException(
                    "Gameplay startup requires exactly one installer per feature stage.",
                    nameof(values));
            }

            for (int index = 0; index < required.Length; index++)
            {
                if (values[index] == null)
                    throw new ArgumentException(
                        "Gameplay feature installers cannot be null.",
                        nameof(values));
                if (values[index].Stage != required[index])
                {
                    throw new ArgumentException(
                        $"Gameplay feature stage '{required[index]}' must be installed at position {index}.",
                        nameof(values));
                }
            }
        }
    }
}
