using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public static class DisplacementActionTooltipFormatter
    {
        public static string Format(
            DisplacementActionDefinition action,
            DisplacementActionAvailabilityFailure failure,
            bool turnBased,
            ActionCost? resolvedCost = null,
            string autoStowItemName = null,
            CloseQuartersControlProfile? controlProfile = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            string tooltip = action.DisplayName.ToUpperInvariant()
                + "\nCOST - "
                + (turnBased
                    ? (resolvedCost ?? action.Cost).ActionPoints + " AP"
                    : "FREE OUT OF TURN MODE")
                + "\nTARGETS - "
                + FormatAcceptedSubjects(action.AcceptedSubjects)
                + "\nREACH - "
                + action.Reach.ToString("0.#")
                + " M"
                + "\n"
                + FormatDistance(action)
                + "\nMASS - UP TO "
                + action.MaximumSubjectMass.ToString("0.#")
                + " KG"
                + "\nSIZE - UP TO "
                + action.MaximumSubjectSize.ToString().ToUpperInvariant()
                + "\nHANDS - "
                + FormatHandRequirement(action.HandRequirement);
            if (action.ContestPolicy ==
                DisplacementContestPolicy.CloseQuartersControl)
            {
                tooltip += "\n" + FormatContest(controlProfile);
            }
            if (!string.IsNullOrWhiteSpace(autoStowItemName))
            {
                tooltip += "\nAUTO-STOW - "
                    + autoStowItemName.ToUpperInvariant();
            }
            string unavailable = FormatAvailabilityFailure(failure);
            return string.IsNullOrEmpty(unavailable)
                ? tooltip
                : tooltip + "\nUNAVAILABLE - " + unavailable;
        }

        private static string FormatDistance(
            DisplacementActionDefinition action)
        {
            if (action.DistanceDecay == null)
            {
                return "DISTANCE - UP TO "
                    + action.MaximumDistance.ToString("0.#")
                    + " M";
            }

            return "DISTANCE - WEIGHT ADJUSTED "
                + action.DistanceDecay.MinimumDistance.ToString("0.#")
                + "-"
                + action.MaximumDistance.ToString("0.#")
                + " M"
                + "\nFULL DISTANCE - UP TO "
                + action.DistanceDecay.FullDistanceMass.ToString("0.#")
                + " KG";
        }

        private static string FormatContest(
            CloseQuartersControlProfile? profile)
        {
            if (!profile.HasValue)
            {
                return "CONTEST - D20 + STRENGTH + CONTROL + TALENT";
            }

            CloseQuartersControlProfile value = profile.Value;
            return "CONTEST - D20 + STR "
                + value.StrengthRating
                + " + CONTROL "
                + value.SkillRating
                + " + TALENT "
                + value.TalentModifier;
        }

        private static string FormatAcceptedSubjects(
            DisplacementSubjectKinds subjects)
        {
            bool props = (subjects & DisplacementSubjectKinds.Prop) != 0;
            bool combatants =
                (subjects & DisplacementSubjectKinds.Combatant) != 0;
            if (props && combatants) return "PROPS + COMBATANTS";
            return props ? "PROPS" : "COMBATANTS";
        }

        private static string FormatHandRequirement(
            DisplacementHandRequirement requirement)
        {
            switch (requirement)
            {
                case DisplacementHandRequirement.None:
                    return "NO FREE-HAND REQUIREMENT";
                case DisplacementHandRequirement.OneHandFree:
                    return "REQUIRES ONE FREE HAND";
                case DisplacementHandRequirement.BothHandsFree:
                    return "REQUIRES BOTH HANDS FREE";
                default:
                    throw new ArgumentOutOfRangeException(nameof(requirement));
            }
        }

        private static string FormatAvailabilityFailure(
            DisplacementActionAvailabilityFailure failure)
        {
            switch (failure)
            {
                case DisplacementActionAvailabilityFailure.None:
                    return string.Empty;
                case DisplacementActionAvailabilityFailure.ActorUnavailable:
                    return "ACTOR UNAVAILABLE";
                case DisplacementActionAvailabilityFailure.ActionUnavailable:
                    return "ACTION UNAVAILABLE";
                case DisplacementActionAvailabilityFailure.OperationInProgress:
                    return "ANOTHER ACTION IS RESOLVING";
                case DisplacementActionAvailabilityFailure.ActorNotActive:
                    return "WAIT FOR YOUR TURN";
                case DisplacementActionAvailabilityFailure.HandsOccupied:
                    return "REQUIRED HANDS ARE OCCUPIED";
                case DisplacementActionAvailabilityFailure.InsufficientTurnBudget:
                    return "INSUFFICIENT AP";
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }
    }
}
