using System;

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

    public static class ActionMobilityCodec
    {
        public const string MobileValue = "mobile";
        public const string MomentumValue = "momentum";
        public const string SetValue = "set";

        public static bool TryParse(string value, out ActionMobility mobility)
        {
            if (string.Equals(value, MobileValue, StringComparison.OrdinalIgnoreCase))
            {
                mobility = ActionMobility.Mobile;
                return true;
            }

            if (string.Equals(value, MomentumValue, StringComparison.OrdinalIgnoreCase))
            {
                mobility = ActionMobility.Momentum;
                return true;
            }

            if (string.Equals(value, SetValue, StringComparison.OrdinalIgnoreCase))
            {
                mobility = ActionMobility.Set;
                return true;
            }

            mobility = default;
            return false;
        }

        public static string Format(ActionMobility mobility)
        {
            switch (mobility)
            {
                case ActionMobility.Mobile:
                    return MobileValue;
                case ActionMobility.Momentum:
                    return MomentumValue;
                case ActionMobility.Set:
                    return SetValue;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mobility));
            }
        }
    }
}
