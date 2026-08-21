using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayDisplacementAssembler
    {
        internal static void ValidateProp(ScenarioPropContentData prop)
        {
            ValidatePropToppling(prop.entityId, prop.toppling);
            ValidatePropPinning(
                prop.entityId,
                prop.toppling,
                prop.pinning);
        }

        internal static Dictionary<string, DisplacementSubjectDefinition>
            CreateSubjects(
                IReadOnlyDictionary<string, ScenarioActorContentData> actors,
                IReadOnlyDictionary<string, ScenarioPropContentData> props)
        {
            var subjects =
                new Dictionary<string, DisplacementSubjectDefinition>(
                    StringComparer.Ordinal);
            foreach (ScenarioActorContentData actor in actors.Values)
            {
                subjects.Add(
                    actor.id,
                    new DisplacementSubjectDefinition(
                        actor.id,
                        DisplacementSubjectKind.Combatant,
                        actor.mass,
                        ParseSize(actor.sizeClass)));
            }

            foreach (ScenarioPropContentData prop in props.Values)
            {
                Require(
                    !subjects.ContainsKey(prop.entityId),
                    $"Displacement subject '{prop.entityId}' is defined as both an actor and a prop.");
                subjects.Add(
                    prop.entityId,
                    new DisplacementSubjectDefinition(
                        prop.entityId,
                        DisplacementSubjectKind.Prop,
                        prop.mass,
                        ParseSize(prop.sizeClass),
                        CreatePropToppling(prop.toppling),
                        CreatePropPinning(prop.pinning)));
            }

            return subjects;
        }


        private static void ValidatePropToppling(
            string propId,
            ScenarioPropTopplingData toppling)
        {
            if (toppling == null)
                return;

            Require(
                !float.IsNaN(toppling.pitchOffsetDegrees)
                    && !float.IsInfinity(toppling.pitchOffsetDegrees),
                $"Prop '{propId}' toppling pitch offset must be finite.");
            Require(
                !float.IsNaN(toppling.rollOffsetDegrees)
                    && !float.IsInfinity(toppling.rollOffsetDegrees),
                $"Prop '{propId}' toppling roll offset must be finite.");
            RequireFiniteNonNegative(
                toppling.elevationOffset,
                $"Prop '{propId}' toppling elevation offset");
            if (toppling.enabled)
            {
                Require(
                    toppling.pitchOffsetDegrees != 0f
                        || toppling.rollOffsetDegrees != 0f,
                    $"Prop '{propId}' enabled toppling requires a non-zero pitch or roll offset.");
            }
        }

        private static PropTopplingDefinition CreatePropToppling(
            ScenarioPropTopplingData toppling) =>
            toppling != null && toppling.enabled
                ? new PropTopplingDefinition(
                    toppling.pitchOffsetDegrees,
                    toppling.rollOffsetDegrees,
                    toppling.elevationOffset)
                : null;

        private static void ValidatePropPinning(
            string propId,
            ScenarioPropTopplingData toppling,
            ScenarioPropPinningData pinning)
        {
            if (pinning == null)
                return;

            RequireFiniteNonNegative(
                pinning.maximumActorMass,
                $"Prop '{propId}' maximum pinned actor mass");
            RequireFiniteNonNegative(
                pinning.minimumContactDepth,
                $"Prop '{propId}' minimum pin contact depth");
            if (pinning.enabled)
            {
                Require(
                    toppling != null && toppling.enabled,
                    $"Prop '{propId}' pinning requires enabled toppling.");
                RequireFinitePositive(
                    pinning.maximumActorMass,
                    $"Prop '{propId}' maximum pinned actor mass");
            }
        }

        private static PropPinningDefinition CreatePropPinning(
            ScenarioPropPinningData pinning) =>
            pinning != null && pinning.enabled
                ? new PropPinningDefinition(
                    pinning.maximumActorMass,
                    pinning.minimumContactDepth)
                : null;


        internal static DisplacementAbilityDefinition
            CreateAbility(ScenarioActorContentData actor)
        {
            ScenarioDisplacementAbilityData ability =
                actor?.displacementAbility;
            if (!HasAuthoredDisplacementAbility(ability))
            {
                return null;
            }

            var definitions = new List<DisplacementActionDefinition>();
            foreach (ScenarioDisplacementActionData action in
                ability.actions
                    ?? new List<ScenarioDisplacementActionData>())
            {
                DisplacementActionKind intent = ParseDisplacementIntent(
                    action.intent);
                definitions.Add(new DisplacementActionDefinition(
                    action.id,
                    action.displayName,
                    intent,
                    new ActionCost(
                        action.cost.actionPoints,
                        action.cost.movementOpportunity,
                        ParseMobility(action.cost.mobility)),
                    ParseAcceptedSubjects(action.acceptedSubjectKinds),
                    action.reach,
                    action.maximumDistance,
                    action.maximumSubjectMass,
                    ParseHandRequirement(action.handRequirement),
                    ParseAutoStowPolicy(action.autoStowPolicy),
                    ParseContestPolicy(action.contestPolicy),
                    ParseAllowedResults(action.allowedResults),
                    ParseSize(action.maximumSubjectSize),
                    intent == DisplacementActionKind.Throw
                        ? CreateDistanceDecay(action.distanceDecay)
                        : null));
            }

            return new DisplacementAbilityDefinition(
                ability.id,
                ability.displayName,
                ability.hotbarSlot,
                definitions);
        }

        internal static void ValidateActor(
            ScenarioActorContentData actor)
        {
            ScenarioDisplacementAbilityData ability = actor.displacementAbility;
            if (!HasAuthoredDisplacementAbility(ability))
            {
                return;
            }

            RequireText(
                ability.id,
                $"Actor '{actor.id}' displacement ability ID");
            RequireText(
                ability.displayName,
                $"Actor '{actor.id}' displacement ability display name");
            Require(
                ability.hotbarSlot >= 1
                    && ability.hotbarSlot <= GameplayHotbarRules.SlotCount,
                $"Actor '{actor.id}' displacement ability hotbar slot must be between 1 and {GameplayHotbarRules.SlotCount}.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var hotbarSlots = new HashSet<int>();
            foreach (ScenarioInventoryItemData item in
                actor.inventory ?? new List<ScenarioInventoryItemData>())
            {
                if (item != null && item.hotbarSlot > 0)
                {
                    hotbarSlots.Add(item.hotbarSlot);
                }
            }
            Require(
                hotbarSlots.Add(ability.hotbarSlot),
                $"Actor '{actor.id}' hotbar slot {ability.hotbarSlot} is assigned more than once.");
            Require(
                ability.actions != null && ability.actions.Count > 0,
                $"Actor '{actor.id}' displacement ability requires at least one action.");
            foreach (ScenarioDisplacementActionData action in
                ability.actions
                    ?? new List<ScenarioDisplacementActionData>())
            {
                Require(action != null,
                    $"Actor '{actor.id}' displacement actions cannot contain null entries.");
                RequireText(action.id,
                    $"Actor '{actor.id}' displacement action ID");
                Require(ids.Add(action.id),
                    $"Actor '{actor.id}' displacement action '{action.id}' is duplicated.");
                RequireText(action.displayName,
                    $"Actor '{actor.id}' displacement action '{action.id}' display name");
                Require(action.cost != null,
                    $"Actor '{actor.id}' displacement action '{action.id}' requires a cost.");
                Require(action.cost.actionPoints > 0,
                    $"Actor '{actor.id}' displacement action '{action.id}' must cost at least one AP.");
                ParseMobility(action.cost.mobility);
                DisplacementActionKind intent = ParseDisplacementIntent(
                    action.intent);
                ParseAcceptedSubjects(action.acceptedSubjectKinds);
                RequireFinitePositive(action.reach,
                    $"Actor '{actor.id}' displacement action '{action.id}' reach");
                RequireFinitePositive(action.maximumDistance,
                    $"Actor '{actor.id}' displacement action '{action.id}' maximum distance");
                RequireFinitePositive(action.maximumSubjectMass,
                    $"Actor '{actor.id}' displacement action '{action.id}' maximum subject mass");
                ParseSize(action.maximumSubjectSize);
                if (intent == DisplacementActionKind.Throw)
                {
                    Require(action.distanceDecay != null,
                        $"Actor '{actor.id}' throw action '{action.id}' requires distance decay.");
                    RequireFinitePositive(
                        action.distanceDecay.fullDistanceMass,
                        $"Actor '{actor.id}' displacement action '{action.id}' full-distance mass");
                    Require(
                        action.distanceDecay.fullDistanceMass
                            < action.maximumSubjectMass,
                        $"Actor '{actor.id}' displacement action '{action.id}' full-distance mass must be below its maximum mass.");
                    RequireFinitePositive(
                        action.distanceDecay.minimumDistance,
                        $"Actor '{actor.id}' displacement action '{action.id}' minimum distance");
                    Require(
                        action.distanceDecay.minimumDistance
                            <= action.maximumDistance,
                        $"Actor '{actor.id}' displacement action '{action.id}' minimum distance cannot exceed its maximum distance.");
                    RequireFinitePositive(
                        action.distanceDecay.exponent,
                        $"Actor '{actor.id}' displacement action '{action.id}' distance-decay exponent");
                }
                ParseHandRequirement(action.handRequirement);
                ParseAutoStowPolicy(action.autoStowPolicy);
                ParseContestPolicy(action.contestPolicy);
                ParseAllowedResults(action.allowedResults);
            }

            _ = CreateAbility(actor);
        }

        private static bool HasAuthoredDisplacementAbility(
            ScenarioDisplacementAbilityData ability) =>
            ability != null
            && (!string.IsNullOrWhiteSpace(ability.id)
                || !string.IsNullOrWhiteSpace(ability.displayName)
                || ability.hotbarSlot != 0
                || (ability.actions != null && ability.actions.Count > 0));


        private static DisplacementActionKind ParseDisplacementIntent(
            string value)
        {
            if (string.Equals(value, "push", StringComparison.OrdinalIgnoreCase))
                return DisplacementActionKind.Push;
            if (string.Equals(value, "lift", StringComparison.OrdinalIgnoreCase))
                return DisplacementActionKind.Lift;
            if (string.Equals(value, "throw", StringComparison.OrdinalIgnoreCase))
                return DisplacementActionKind.Throw;
            if (string.Equals(
                value,
                "push-off",
                StringComparison.OrdinalIgnoreCase))
                return DisplacementActionKind.PushOff;
            throw new InvalidOperationException(
                $"Unknown displacement intent '{value}'.");
        }

        internal static DisplacementSizeClass ParseSize(
            string value)
        {
            if (string.Equals(value, "tiny", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Tiny;
            if (string.Equals(value, "small", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Small;
            if (string.Equals(value, "medium", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Medium;
            if (string.Equals(value, "large", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Large;
            if (string.Equals(value, "huge", StringComparison.OrdinalIgnoreCase))
                return DisplacementSizeClass.Huge;
            throw new InvalidOperationException(
                $"Unknown displacement size class '{value}'.");
        }

        private static DisplacementDistanceDecayDefinition CreateDistanceDecay(
            ScenarioDisplacementDistanceDecayData data) =>
            data == null
                ? null
                : new DisplacementDistanceDecayDefinition(
                    data.fullDistanceMass,
                    data.minimumDistance,
                    data.exponent);

        private static DisplacementSubjectKinds ParseAcceptedSubjects(
            IEnumerable<string> values)
        {
            DisplacementSubjectKinds result = DisplacementSubjectKinds.None;
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.Equals(
                    value,
                    "prop",
                    StringComparison.OrdinalIgnoreCase))
                {
                    result |= DisplacementSubjectKinds.Prop;
                }
                else if (string.Equals(
                    value,
                    "combatant",
                    StringComparison.OrdinalIgnoreCase))
                {
                    result |= DisplacementSubjectKinds.Combatant;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unknown displacement subject kind '{value}'.");
                }
            }

            if (result == DisplacementSubjectKinds.None)
            {
                throw new InvalidOperationException(
                    "Displacement actions require at least one accepted subject kind.");
            }

            return result;
        }

        private static DisplacementHandRequirement ParseHandRequirement(
            string value)
        {
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                return DisplacementHandRequirement.None;
            if (string.Equals(
                value,
                "one-hand-free",
                StringComparison.OrdinalIgnoreCase))
                return DisplacementHandRequirement.OneHandFree;
            if (string.Equals(
                value,
                "both-hands-free",
                StringComparison.OrdinalIgnoreCase))
                return DisplacementHandRequirement.BothHandsFree;
            throw new InvalidOperationException(
                $"Unknown displacement hand requirement '{value}'.");
        }

        private static DisplacementAutoStowPolicy ParseAutoStowPolicy(
            string value)
        {
            if (string.Equals(value, "never", StringComparison.OrdinalIgnoreCase))
                return DisplacementAutoStowPolicy.Never;
            if (string.Equals(value, "allowed", StringComparison.OrdinalIgnoreCase))
                return DisplacementAutoStowPolicy.Allowed;
            throw new InvalidOperationException(
                $"Unknown displacement auto-stow policy '{value}'.");
        }

        private static DisplacementContestPolicy ParseContestPolicy(
            string value)
        {
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                return DisplacementContestPolicy.None;
            if (string.Equals(
                value,
                "close-quarters-control",
                StringComparison.OrdinalIgnoreCase))
                return DisplacementContestPolicy.CloseQuartersControl;
            throw new InvalidOperationException(
                $"Unknown displacement contest policy '{value}'.");
        }

        private static DisplacementResultPolicies ParseAllowedResults(
            IEnumerable<string> values)
        {
            DisplacementResultPolicies result =
                DisplacementResultPolicies.None;
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.Equals(
                    value,
                    "topple",
                    StringComparison.OrdinalIgnoreCase))
                    result |= DisplacementResultPolicies.Topple;
                else if (string.Equals(
                    value,
                    "release",
                    StringComparison.OrdinalIgnoreCase))
                    result |= DisplacementResultPolicies.Release;
                else if (string.Equals(
                    value,
                    "collision-damage",
                    StringComparison.OrdinalIgnoreCase))
                    result |= DisplacementResultPolicies.CollisionDamage;
                else if (string.Equals(
                    value,
                    "pin",
                    StringComparison.OrdinalIgnoreCase))
                    result |= DisplacementResultPolicies.Pin;
                else
                    throw new InvalidOperationException(
                        $"Unknown displacement result policy '{value}'.");
            }

            return result;
        }


        private static ActionMobility ParseMobility(string value) =>
            GameplayScenarioAssemblyValidation.ParseMobility(value);

        private static void RequireText(string value, string label) =>
            GameplayScenarioAssemblyValidation.RequireText(value, label);

        private static void RequireFinitePositive(float value, string label) =>
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                value,
                label);

        private static void RequireFiniteNonNegative(
            float value,
            string label) =>
            GameplayScenarioAssemblyValidation.RequireFiniteNonNegative(
                value,
                label);

        private static void Require(bool condition, string message) =>
            GameplayScenarioAssemblyValidation.Require(condition, message);
    }
}
