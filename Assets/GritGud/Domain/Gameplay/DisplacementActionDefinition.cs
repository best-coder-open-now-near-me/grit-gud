using System;
using System.Collections.Generic;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    [Flags]
    public enum DisplacementSubjectKinds
    {
        None = 0,
        Prop = 1 << 0,
        Combatant = 1 << 1,
    }

    public enum DisplacementHandRequirement
    {
        None,
        OneHandFree,
        BothHandsFree,
    }

    public enum DisplacementAutoStowPolicy
    {
        Never,
        Allowed,
    }

    public enum DisplacementContestPolicy
    {
        None,
        CloseQuartersControl,
    }

    public sealed class DisplacementDistanceDecayDefinition
    {
        public DisplacementDistanceDecayDefinition(
            float fullDistanceMass,
            float minimumDistance,
            float exponent)
        {
            if (!IsFinitePositive(fullDistanceMass))
                throw new ArgumentOutOfRangeException(nameof(fullDistanceMass));
            if (!IsFinitePositive(minimumDistance))
                throw new ArgumentOutOfRangeException(nameof(minimumDistance));
            if (!IsFinitePositive(exponent))
                throw new ArgumentOutOfRangeException(nameof(exponent));

            FullDistanceMass = fullDistanceMass;
            MinimumDistance = minimumDistance;
            Exponent = exponent;
        }

        public float FullDistanceMass { get; }

        public float MinimumDistance { get; }

        public float Exponent { get; }

        public float Evaluate(
            float subjectMass,
            float maximumDistance,
            float maximumSubjectMass)
        {
            if (!IsFinitePositive(subjectMass))
                throw new ArgumentOutOfRangeException(nameof(subjectMass));
            if (!IsFinitePositive(maximumDistance))
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            if (!IsFinitePositive(maximumSubjectMass))
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSubjectMass));
            if (FullDistanceMass > maximumSubjectMass
                || MinimumDistance > maximumDistance)
            {
                throw new ArgumentException(
                    "Distance decay must fit within the action's authored limits.");
            }

            if (subjectMass <= FullDistanceMass)
            {
                return maximumDistance;
            }

            float normalizedMass = Math.Min(
                1f,
                (subjectMass - FullDistanceMass)
                    / (maximumSubjectMass - FullDistanceMass));
            float decay = (float)Math.Pow(normalizedMass, Exponent);
            return maximumDistance
                + ((MinimumDistance - maximumDistance) * decay);
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    [Flags]
    public enum DisplacementResultPolicies
    {
        None = 0,
        Topple = 1 << 0,
        Release = 1 << 1,
        CollisionDamage = 1 << 2,
        Pin = 1 << 3,
    }

    public sealed class DisplacementActionDefinition
    {
        public DisplacementActionDefinition(
            string id,
            string displayName,
            DisplacementActionKind intent,
            ActionCost cost,
            DisplacementSubjectKinds acceptedSubjects,
            float reach,
            float maximumDistance,
            float maximumSubjectMass,
            DisplacementHandRequirement handRequirement,
            DisplacementAutoStowPolicy autoStowPolicy,
            DisplacementContestPolicy contestPolicy,
            DisplacementResultPolicies allowedResults,
            DisplacementSizeClass maximumSubjectSize =
                DisplacementSizeClass.Huge,
            DisplacementDistanceDecayDefinition distanceDecay = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Displacement actions require stable identifiers.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Displacement actions require display names.",
                    nameof(displayName));
            }

            if (!Enum.IsDefined(typeof(DisplacementActionKind), intent))
            {
                throw new ArgumentOutOfRangeException(nameof(intent));
            }

            DisplacementSubjectKinds knownSubjects =
                DisplacementSubjectKinds.Prop
                | DisplacementSubjectKinds.Combatant;
            if (acceptedSubjects == DisplacementSubjectKinds.None
                || (acceptedSubjects & ~knownSubjects) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(acceptedSubjects));
            }

            if (!IsFinitePositive(reach))
            {
                throw new ArgumentOutOfRangeException(nameof(reach));
            }

            if (!IsFinitePositive(maximumDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            }

            if (!IsFinitePositive(maximumSubjectMass))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSubjectMass));
            }

            if (!Enum.IsDefined(
                    typeof(DisplacementSizeClass),
                    maximumSubjectSize))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSubjectSize));
            }

            if (!Enum.IsDefined(
                    typeof(DisplacementHandRequirement),
                    handRequirement))
            {
                throw new ArgumentOutOfRangeException(nameof(handRequirement));
            }

            if (!Enum.IsDefined(
                    typeof(DisplacementAutoStowPolicy),
                    autoStowPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(autoStowPolicy));
            }

            if (!Enum.IsDefined(
                    typeof(DisplacementContestPolicy),
                    contestPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(contestPolicy));
            }

            DisplacementResultPolicies knownResults =
                DisplacementResultPolicies.Topple
                | DisplacementResultPolicies.Release
                | DisplacementResultPolicies.CollisionDamage
                | DisplacementResultPolicies.Pin;
            if ((allowedResults & ~knownResults) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(allowedResults));
            }

            if ((acceptedSubjects & DisplacementSubjectKinds.Combatant) != 0
                && contestPolicy !=
                    DisplacementContestPolicy.CloseQuartersControl)
            {
                throw new ArgumentException(
                    "Combatant displacement requires a Close-Quarters Control contest.",
                    nameof(contestPolicy));
            }

            if (allowedResults.HasFlag(DisplacementResultPolicies.Pin)
                && !allowedResults.HasFlag(DisplacementResultPolicies.Topple))
            {
                throw new ArgumentException(
                    "Pinning displacement actions must also allow toppling.",
                    nameof(allowedResults));
            }
            if (intent == DisplacementActionKind.PushOff
                && (acceptedSubjects != DisplacementSubjectKinds.Prop
                    || !allowedResults.HasFlag(
                        DisplacementResultPolicies.Release)))
            {
                throw new ArgumentException(
                    "Push Off must target props and allow pin release.",
                    nameof(allowedResults));
            }

            if (distanceDecay != null
                && (distanceDecay.FullDistanceMass >= maximumSubjectMass
                    || distanceDecay.MinimumDistance > maximumDistance))
            {
                throw new ArgumentException(
                    "Distance decay must fit within the action's mass and distance limits.",
                    nameof(distanceDecay));
            }

            Id = id;
            DisplayName = displayName;
            Intent = intent;
            Cost = cost;
            AcceptedSubjects = acceptedSubjects;
            Reach = reach;
            MaximumDistance = maximumDistance;
            MaximumSubjectMass = maximumSubjectMass;
            HandRequirement = handRequirement;
            AutoStowPolicy = autoStowPolicy;
            ContestPolicy = contestPolicy;
            AllowedResults = allowedResults;
            MaximumSubjectSize = maximumSubjectSize;
            DistanceDecay = distanceDecay;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public DisplacementActionKind Intent { get; }

        public ActionCost Cost { get; }

        public DisplacementSubjectKinds AcceptedSubjects { get; }

        public float Reach { get; }

        public float MaximumDistance { get; }

        public float MaximumSubjectMass { get; }

        public DisplacementHandRequirement HandRequirement { get; }

        public DisplacementAutoStowPolicy AutoStowPolicy { get; }

        public DisplacementContestPolicy ContestPolicy { get; }

        public DisplacementResultPolicies AllowedResults { get; }

        public DisplacementSizeClass MaximumSubjectSize { get; }

        public DisplacementDistanceDecayDefinition DistanceDecay { get; }

        public int RequiredFreeHands
        {
            get
            {
                switch (HandRequirement)
                {
                    case DisplacementHandRequirement.None:
                        return 0;
                    case DisplacementHandRequirement.OneHandFree:
                        return 1;
                    case DisplacementHandRequirement.BothHandsFree:
                        return 2;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(HandRequirement));
                }
            }
        }

        public bool HasRequiredFreeHands(int occupiedHands)
        {
            if (occupiedHands < 0 || occupiedHands > 2)
                throw new ArgumentOutOfRangeException(nameof(occupiedHands));
            return 2 - occupiedHands >= RequiredFreeHands;
        }

        public float GetMaximumDistance(
            float subjectMass,
            DisplacementSizeClass subjectSize)
        {
            if (!Enum.IsDefined(typeof(DisplacementSizeClass), subjectSize))
                throw new ArgumentOutOfRangeException(nameof(subjectSize));
            return DistanceDecay == null
                ? MaximumDistance
                : DistanceDecay.Evaluate(
                    subjectMass,
                    MaximumDistance,
                    MaximumSubjectMass);
        }

        public float GetMaximumDistance(DisplacementSubjectDefinition subject)
        {
            if (subject == null)
                throw new ArgumentNullException(nameof(subject));
            return GetMaximumDistance(subject.Mass, subject.Size);
        }

        public bool Accepts(DisplacementSubjectKind subjectKind)
        {
            switch (subjectKind)
            {
                case DisplacementSubjectKind.Prop:
                    return (AcceptedSubjects
                        & DisplacementSubjectKinds.Prop) != 0;
                case DisplacementSubjectKind.Combatant:
                    return (AcceptedSubjects
                        & DisplacementSubjectKinds.Combatant) != 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(subjectKind));
            }
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    public sealed class DisplacementAbilityDefinition
    {
        public DisplacementAbilityDefinition(
            string id,
            string displayName,
            int hotbarSlot,
            IEnumerable<DisplacementActionDefinition> actions)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Displacement abilities require stable identifiers.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Displacement abilities require display names.",
                    nameof(displayName));
            }

            if (hotbarSlot < 1
                || hotbarSlot > GameplayHotbarRules.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(hotbarSlot));
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            var copy = new List<DisplacementActionDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (DisplacementActionDefinition action in actions)
            {
                if (action == null)
                {
                    throw new ArgumentException(
                        "Displacement abilities cannot contain null actions.",
                        nameof(actions));
                }

                if (!ids.Add(action.Id))
                {
                    throw new ArgumentException(
                        $"Displacement action '{action.Id}' is defined more than once.",
                        nameof(actions));
                }

                copy.Add(action);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "Displacement abilities require at least one action.",
                    nameof(actions));
            }

            Id = id;
            DisplayName = displayName;
            HotbarSlot = hotbarSlot;
            Actions = copy.AsReadOnly();
        }

        public string Id { get; }

        public string DisplayName { get; }

        public int HotbarSlot { get; }

        public IReadOnlyList<DisplacementActionDefinition> Actions { get; }
    }
}
