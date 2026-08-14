namespace GritGud.Domain.Turns
{
    /// <summary>
    /// Describes how an action relates to an actor's movement opportunity.
    /// The profile is descriptive; individual actions still define their costs.
    /// </summary>
    public enum ActionMobility
    {
        Mobile,
        Set,
        Momentum,
    }
}
