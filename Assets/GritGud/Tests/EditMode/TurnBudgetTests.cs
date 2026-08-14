using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests
{
    public sealed class TurnBudgetTests
    {
        [Test]
        public void MovementDoesNotSpendActionPoints()
        {
            var budget = new TurnBudget(4, 8f);

            TurnBudget result = budget.SpendMovement(3f);

            Assert.That(result.ActionPoints, Is.EqualTo(4));
            Assert.That(result.MovementOpportunity, Is.EqualTo(5f));
        }

        [Test]
        public void MobileActionCanLeaveMovementUntouched()
        {
            var budget = new TurnBudget(4, 8f);
            var cost = new ActionCost(2, 0f, ActionMobility.Mobile);

            TurnBudget result = budget.SpendAction(cost);

            Assert.That(result.ActionPoints, Is.EqualTo(2));
            Assert.That(result.MovementOpportunity, Is.EqualTo(8f));
        }

        [Test]
        public void SetActionCanSpendBothResources()
        {
            var budget = new TurnBudget(4, 8f);
            var cost = new ActionCost(2, 3f, ActionMobility.Set);

            TurnBudget result = budget.SpendAction(cost);

            Assert.That(result.ActionPoints, Is.EqualTo(2));
            Assert.That(result.MovementOpportunity, Is.EqualTo(5f));
        }

        [Test]
        public void UnaffordableActionDoesNotProduceAResult()
        {
            var budget = new TurnBudget(1, 2f);
            var cost = new ActionCost(2, 3f, ActionMobility.Set);

            Assert.That(budget.CanAfford(cost), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => budget.SpendAction(cost));
        }

        [Test]
        public void ActionCostRejectsUnknownMobilityProfiles()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new ActionCost(1, 0f, (ActionMobility)999));
        }
    }
}
