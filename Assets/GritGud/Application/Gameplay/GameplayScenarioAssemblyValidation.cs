using System;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayScenarioAssemblyValidation
    {
        public static ActionMobility ParseMobility(string value)
        {
            if (string.Equals(
                    value,
                    "mobile",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ActionMobility.Mobile;
            }

            if (string.Equals(
                    value,
                    "set",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ActionMobility.Set;
            }

            throw new InvalidOperationException(
                $"Unknown action mobility '{value}'.");
        }

        public static void RequireText(string value, string label)
        {
            Require(
                !string.IsNullOrWhiteSpace(value),
                label + " cannot be empty.");
        }

        public static void RequireFinitePositive(float value, string label)
        {
            Require(
                !float.IsNaN(value)
                    && !float.IsInfinity(value)
                    && value > 0f,
                label + " must be finite and greater than zero.");
        }

        public static void RequireFiniteNonNegative(
            float value,
            string label)
        {
            Require(
                !float.IsNaN(value)
                    && !float.IsInfinity(value)
                    && value >= 0f,
                label + " must be finite and non-negative.");
        }

        public static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
