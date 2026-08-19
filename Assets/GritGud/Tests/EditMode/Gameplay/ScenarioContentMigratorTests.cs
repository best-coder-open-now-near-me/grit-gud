using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class ScenarioContentMigratorTests
    {
        [TestCase(15)]
        [TestCase(16)]
        public void LegacySchemaReceivesExplicitEquivalentEconomy(int schema)
        {
            var document = new ScenarioContentDocument
            {
                schemaVersion = schema,
                timing = new ScenarioTimingData
                {
                    minimumVoluntaryTurnSeconds = 1f,
                },
                actors = new List<ScenarioActorContentData>
                {
                    new ScenarioActorContentData
                    {
                        id = "actor",
                        turnBudget = new ScenarioTurnBudgetData
                        {
                            actionPoints = 3,
                        },
                    },
                },
            };

            ScenarioContentMigrator.Migrate(document);

            Assert.That(document.schemaVersion,
                Is.EqualTo(ScenarioContentDocument.CurrentSchemaVersion));
            Assert.That(document.timing.startingActionPoints, Is.EqualTo(3));
            Assert.That(document.timing.actionPointIncome, Is.EqualTo(3));
            Assert.That(document.timing.maximumHeldActionPoints, Is.EqualTo(3));
        }

        [Test]
        public void LegacyActorsWithDifferentAllowancesFailClosed()
        {
            var document = new ScenarioContentDocument
            {
                schemaVersion = 16,
                actors = new List<ScenarioActorContentData>
                {
                    Actor("one", 3),
                    Actor("two", 4),
                },
            };

            Assert.Throws<System.InvalidOperationException>(() =>
                ScenarioContentMigrator.Migrate(document));
        }

        private static ScenarioActorContentData Actor(string id, int ap) =>
            new ScenarioActorContentData
            {
                id = id,
                turnBudget = new ScenarioTurnBudgetData
                {
                    actionPoints = ap,
                },
            };
    }
}
