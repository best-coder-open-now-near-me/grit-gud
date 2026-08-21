using System;

namespace GritGud.Domain.Turns
{
    public readonly struct TurnActionPointEconomy : IEquatable<TurnActionPointEconomy>
    {
        public TurnActionPointEconomy(
            int startingActionPoints,
            int incomePerPersonalTurn,
            int maximumHeldActionPoints)
        {
            if (startingActionPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(startingActionPoints));
            if (incomePerPersonalTurn < 0)
                throw new ArgumentOutOfRangeException(nameof(incomePerPersonalTurn));
            if (maximumHeldActionPoints <= 0
                || startingActionPoints > maximumHeldActionPoints
                || incomePerPersonalTurn > maximumHeldActionPoints)
                throw new ArgumentOutOfRangeException(nameof(maximumHeldActionPoints));
            StartingActionPoints = startingActionPoints;
            IncomePerPersonalTurn = incomePerPersonalTurn;
            MaximumHeldActionPoints = maximumHeldActionPoints;
        }

        public int StartingActionPoints { get; }
        public int IncomePerPersonalTurn { get; }
        public int MaximumHeldActionPoints { get; }

        public bool Equals(TurnActionPointEconomy other) =>
            StartingActionPoints == other.StartingActionPoints
            && IncomePerPersonalTurn == other.IncomePerPersonalTurn
            && MaximumHeldActionPoints == other.MaximumHeldActionPoints;

        public override bool Equals(object obj) =>
            obj is TurnActionPointEconomy other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StartingActionPoints;
                hash = (hash * 397) ^ IncomePerPersonalTurn;
                return (hash * 397) ^ MaximumHeldActionPoints;
            }
        }
    }

    public readonly struct PersonalTurnActionPointGrant
    {
        public PersonalTurnActionPointGrant(
            int previousActionPoints,
            int requestedIncome,
            int grantedActionPoints,
            int capWaste,
            int resultingActionPoints)
        {
            PreviousActionPoints = previousActionPoints;
            RequestedIncome = requestedIncome;
            GrantedActionPoints = grantedActionPoints;
            CapWaste = capWaste;
            ResultingActionPoints = resultingActionPoints;
        }

        public int PreviousActionPoints { get; }
        public int RequestedIncome { get; }
        public int GrantedActionPoints { get; }
        public int CapWaste { get; }
        public int ResultingActionPoints { get; }
    }

    public static class PersonalTurnActionPointRules
    {
        public static PersonalTurnActionPointGrant Grant(
            int currentActionPoints,
            TurnActionPointEconomy economy)
        {
            if (currentActionPoints < 0
                || currentActionPoints > economy.MaximumHeldActionPoints)
                throw new ArgumentOutOfRangeException(nameof(currentActionPoints));
            int availableCapacity = economy.MaximumHeldActionPoints
                - currentActionPoints;
            int granted = Math.Min(
                availableCapacity,
                economy.IncomePerPersonalTurn);
            return new PersonalTurnActionPointGrant(
                currentActionPoints,
                economy.IncomePerPersonalTurn,
                granted,
                economy.IncomePerPersonalTurn - granted,
                currentActionPoints + granted);
        }
    }
}
