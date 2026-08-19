using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class TurnActionPointEconomyTests
    {
        [TestCase(0, 4, 0, 4)]
        [TestCase(1, 4, 0, 5)]
        [TestCase(2, 4, 0, 6)]
        [TestCase(5, 1, 3, 6)]
        [TestCase(6, 0, 4, 6)]
        public void GrantRecordsIncomeCapWasteAndResult(
            int previous,
            int granted,
            int waste,
            int resulting)
        {
            PersonalTurnActionPointGrant record =
                PersonalTurnActionPointRules.Grant(
                    previous,
                    new TurnActionPointEconomy(4, 4, 6));

            Assert.That(record.PreviousActionPoints, Is.EqualTo(previous));
            Assert.That(record.RequestedIncome, Is.EqualTo(4));
            Assert.That(record.GrantedActionPoints, Is.EqualTo(granted));
            Assert.That(record.CapWaste, Is.EqualTo(waste));
            Assert.That(record.ResultingActionPoints, Is.EqualTo(resulting));
        }
    }
}
