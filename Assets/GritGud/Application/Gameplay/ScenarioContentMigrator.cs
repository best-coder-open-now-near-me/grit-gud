using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public static class ScenarioContentMigrator
    {
        private const int OldestSupportedSchemaVersion = 15;

        public static ScenarioContentDocument Migrate(
            ScenarioContentDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.schemaVersion > ScenarioContentDocument.CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Scenario schema {document.schemaVersion} is newer than supported schema {ScenarioContentDocument.CurrentSchemaVersion}.");
            if (document.schemaVersion < OldestSupportedSchemaVersion)
                throw new InvalidOperationException(
                    $"Scenario schema {document.schemaVersion} is older than supported migration schema {OldestSupportedSchemaVersion}.");

            document.Normalize();
            if (document.schemaVersion < 17)
                InstallLegacyActionPointEconomy(document);
            document.schemaVersion = ScenarioContentDocument.CurrentSchemaVersion;
            return document;
        }

        private static void InstallLegacyActionPointEconomy(
            ScenarioContentDocument document)
        {
            if (document.actors.Count == 0)
                throw new InvalidOperationException(
                    "Legacy scenario migration requires at least one actor.");
            int starting = document.actors[0]?.turnBudget?.actionPoints
                ?? throw new InvalidOperationException(
                    "Legacy scenario actors require turn budgets.");
            foreach (ScenarioActorContentData actor in document.actors)
            {
                if (actor?.turnBudget == null
                    || actor.turnBudget.actionPoints != starting)
                    throw new InvalidOperationException(
                        "Legacy scenarios with different actor AP allowances require explicit economy authoring.");
            }
            document.timing.startingActionPoints = starting;
            document.timing.actionPointIncome = starting;
            document.timing.maximumHeldActionPoints = starting;
        }
    }
}
