using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public sealed class LevelScenarioValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            LevelScenarioData scenario = context.Document.scenario;
            if (scenario == null)
            {
                context.Error("scenario.missing", "The level needs scenario settings.");
                return;
            }

            if (!ScenarioTimingDefinition.IsValidMinimumVoluntaryTurnSeconds(
                    scenario.minimumVoluntaryTurnSeconds))
            {
                context.Error(
                    "scenario.timing.invalid",
                    "The scenario minimum turn duration must be finite and greater than zero.");
            }

            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            int playerCount = 0;
            int selectedPlayerCount = 0;
            int primaryTargetCount = 0;
            foreach (LevelScenarioActorData actor in scenario.actors)
            {
                if (actor == null)
                {
                    context.Error("scenario.actor.missing", "The scenario contains an empty actor.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(actor.id))
                {
                    context.Error("scenario.actor.id.missing", "A scenario actor needs a stable ID.");
                }
                else if (!actorIds.Add(actor.id))
                {
                    context.Error(
                        "scenario.actor.id.duplicate",
                        $"Scenario actor ID '{actor.id}' is duplicated.");
                }

                if (string.IsNullOrWhiteSpace(actor.templateId))
                {
                    context.Error(
                        "scenario.actor.template.missing",
                        $"Scenario actor '{actor.id}' needs an actor template.");
                }
                else
                {
                    ValidateActorTemplate(context, actor);
                }

                if (!string.IsNullOrWhiteSpace(actor.characterId)
                    && context.Content?.HasCharacterCatalog == true
                    && !context.Content.KnownCharacterIds.Contains(actor.characterId))
                {
                    ReportRuntimeContentIssue(
                        context,
                        "scenario.actor.character.unknown",
                        $"Scenario actor '{actor.id}' references unavailable character "
                        + $"'{actor.characterId}'.",
                        actor.id);
                }

                if (!LevelValidationMath.IsFinite(actor.transform.position)
                    || !LevelValidationMath.IsFinite(actor.transform.yawDegrees))
                {
                    context.Error(
                        "scenario.actor.transform.not-finite",
                        $"Scenario actor '{actor.id}' must have a finite transform.");
                }
                else if (!LevelValidationMath.Contains(
                             context.Document.bounds,
                             actor.transform.position))
                {
                    context.Warning(
                        "scenario.actor.outside-bounds",
                        $"Scenario actor '{actor.id}' is outside the authored level bounds.");
                }

                if (actor.playerControlled)
                    playerCount++;
                if (actor.initiallySelected)
                {
                    selectedPlayerCount++;
                    if (!actor.playerControlled)
                    {
                        context.Error(
                            "scenario.actor.selection.not-player",
                            $"Initially selected actor '{actor.id}' must be player controlled.");
                    }
                }

                if (actor.primaryTarget)
                {
                    primaryTargetCount++;
                    if (actor.playerControlled)
                    {
                        context.Error(
                            "scenario.actor.target.player",
                            $"Player actor '{actor.id}' cannot be the primary target.");
                    }
                }
            }

            if (playerCount == 0)
            {
                context.Error(
                    "scenario.party.empty",
                    "The scenario needs at least one player-controlled actor.");
            }

            if (selectedPlayerCount != 1)
            {
                context.Error(
                    "scenario.party.selection",
                    "The scenario needs exactly one initially selected player actor.");
            }

            if (primaryTargetCount > 1)
            {
                context.Error(
                    "scenario.target.multiple",
                    "The scenario can define at most one primary target actor.");
            }

            ValidateReinforcementLinks(context, scenario.actors, actorIds);
            ValidateEntityLinks(context, scenario, actorIds);
        }

        private static void ValidateReinforcementLinks(
            LevelValidationContext context,
            IEnumerable<LevelScenarioActorData> actors,
            HashSet<string> actorIds)
        {
            foreach (LevelScenarioActorData actor in actors.Where(actor => actor != null))
            {
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (string reinforcementId in actor.reinforcementActorIds
                    ?? Enumerable.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(reinforcementId))
                    {
                        context.Error(
                            "scenario.actor.reinforcement.id.missing",
                            $"Scenario actor '{actor.id}' has an empty reinforcement ID.");
                    }
                    else if (!unique.Add(reinforcementId))
                    {
                        context.Error(
                            "scenario.actor.reinforcement.id.duplicate",
                            $"Scenario actor '{actor.id}' repeats reinforcement "
                            + $"'{reinforcementId}'.");
                    }
                    else if (string.Equals(actor.id, reinforcementId,
                                 StringComparison.Ordinal))
                    {
                        context.Error(
                            "scenario.actor.reinforcement.self",
                            $"Scenario actor '{actor.id}' cannot reinforce itself.");
                    }
                    else if (!actorIds.Contains(reinforcementId))
                    {
                        context.Error(
                            "scenario.actor.reinforcement.unknown",
                            $"Scenario actor '{actor.id}' references unknown reinforcement "
                            + $"'{reinforcementId}'.");
                    }
                }
            }
        }

        private static void ValidateActorTemplate(
            LevelValidationContext context,
            LevelScenarioActorData actor)
        {
            LevelValidationContent content = context.Content;
            if (content?.HasActorTemplateCatalog != true)
            {
                return;
            }

            if (!content.TryGetActorPresentationId(
                    actor.templateId,
                    out string presentationId))
            {
                ReportRuntimeContentIssue(
                    context,
                    "scenario.actor.template.unknown",
                    $"Scenario actor '{actor.id}' references unavailable template "
                    + $"'{actor.templateId}'.",
                    actor.id);
                return;
            }

            if (string.IsNullOrWhiteSpace(presentationId))
            {
                ReportRuntimeContentIssue(
                    context,
                    "scenario.actor.presentation.missing",
                    $"Actor template '{actor.templateId}' does not define a presentation.",
                    actor.id);
            }
            else if (content.HasActorPresentationCatalog
                && !content.KnownActorPresentationIds.Contains(presentationId))
            {
                ReportRuntimeContentIssue(
                    context,
                    "scenario.actor.presentation.unknown",
                    $"Actor template '{actor.templateId}' references unavailable presentation "
                    + $"'{presentationId}'.",
                    actor.id);
            }
        }

        private static void ReportRuntimeContentIssue(
            LevelValidationContext context,
            string code,
            string message,
            string actorId)
        {
            if (context.Profile == LevelValidationProfile.Authoring)
            {
                context.Warning(code, message, actorId);
            }
            else
            {
                context.Error(code, message, actorId);
            }
        }

        private static void ValidateEntityLinks(
            LevelValidationContext context,
            LevelScenarioData scenario,
            ISet<string> actorIds)
        {
            var entities = context.Document.entities
                .Where(entity => entity != null && !string.IsNullOrWhiteSpace(entity.id))
                .GroupBy(entity => entity.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var linkedProps = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelScenarioPropData prop in scenario.props)
            {
                if (prop == null || string.IsNullOrWhiteSpace(prop.entityId))
                {
                    context.Error("scenario.prop.entity.missing", "A scenario prop needs an entity link.");
                    continue;
                }

                if (!entities.ContainsKey(prop.entityId))
                {
                    context.Error(
                        "scenario.prop.entity.unknown",
                        $"Scenario prop entity '{prop.entityId}' does not exist.",
                        prop.entityId);
                }
                else if (!linkedProps.Add(prop.entityId))
                {
                    context.Error(
                        "scenario.prop.entity.duplicate",
                        $"Entity '{prop.entityId}' is linked as a scenario prop more than once.",
                        prop.entityId);
                }

                if (!LevelValidationMath.IsFinite(prop.mass) || prop.mass <= 0f)
                {
                    context.Error(
                        "scenario.prop.mass",
                        $"Scenario prop '{prop.entityId}' needs a positive finite mass.",
                        prop.entityId);
                }
                if (!IsSupportedSizeClass(prop.sizeClass))
                {
                    context.Error(
                        "scenario.prop.size",
                        $"Scenario prop '{prop.entityId}' has unsupported size "
                        + $"'{prop.sizeClass}'.",
                        prop.entityId);
                }


                LevelScenarioPropTopplingData toppling = prop.toppling;
                if (toppling != null)
                {
                    if (!LevelValidationMath.IsFinite(
                            toppling.pitchOffsetDegrees)
                        || !LevelValidationMath.IsFinite(
                            toppling.rollOffsetDegrees))
                    {
                        context.Error(
                            "scenario.prop.toppling.rotation",
                            $"Scenario prop '{prop.entityId}' needs finite toppling rotation offsets.",
                            prop.entityId);
                    }
                    else if (toppling.enabled
                        && toppling.pitchOffsetDegrees == 0f
                        && toppling.rollOffsetDegrees == 0f)
                    {
                        context.Error(
                            "scenario.prop.toppling.rotation.zero",
                            $"Scenario prop '{prop.entityId}' needs a non-zero toppling pitch or roll offset.",
                            prop.entityId);
                    }

                    if (!LevelValidationMath.IsFinite(
                            toppling.elevationOffset)
                        || toppling.elevationOffset < 0f)
                    {
                        context.Error(
                            "scenario.prop.toppling.elevation",
                            $"Scenario prop '{prop.entityId}' needs a finite non-negative toppling elevation offset.",
                            prop.entityId);
                    }
                }

                LevelScenarioPropPinningData pinning = prop.pinning;
                if (pinning != null)
                {
                    if (!LevelValidationMath.IsFinite(
                            pinning.maximumActorMass)
                        || pinning.maximumActorMass < 0f
                        || (pinning.enabled
                            && pinning.maximumActorMass <= 0f))
                    {
                        context.Error(
                            "scenario.prop.pinning.maximumActorMass",
                            $"Scenario prop '{prop.entityId}' needs a positive finite maximum pinned actor mass when pinning is enabled.",
                            prop.entityId);
                    }
                    if (!LevelValidationMath.IsFinite(
                            pinning.minimumContactDepth)
                        || pinning.minimumContactDepth < 0f)
                    {
                        context.Error(
                            "scenario.prop.pinning.minimumContactDepth",
                            $"Scenario prop '{prop.entityId}' needs a finite non-negative minimum pin contact depth.",
                            prop.entityId);
                    }
                    if (pinning.enabled
                        && (toppling == null || !toppling.enabled))
                    {
                        context.Error(
                            "scenario.prop.pinning.topplingRequired",
                            $"Scenario prop '{prop.entityId}' must enable toppling before it can pin actors.",
                            prop.entityId);
                    }
                }
            }

            var linkedVehicles = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelScenarioVehicleData vehicle in scenario.vehicles)
            {
                if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.entityId))
                {
                    context.Error(
                        "scenario.vehicle.entity.missing",
                        "A scenario vehicle needs an entity link.");
                    continue;
                }

                if (!entities.ContainsKey(vehicle.entityId))
                {
                    context.Error(
                        "scenario.vehicle.entity.unknown",
                        $"Scenario vehicle entity '{vehicle.entityId}' does not exist.",
                        vehicle.entityId);
                }
                else if (!linkedVehicles.Add(vehicle.entityId))
                {
                    context.Error(
                        "scenario.vehicle.entity.duplicate",
                        $"Entity '{vehicle.entityId}' is linked as a vehicle more than once.",
                        vehicle.entityId);
                }

                if (!string.IsNullOrWhiteSpace(vehicle.startingOccupantActorId)
                    && !actorIds.Contains(vehicle.startingOccupantActorId))
                {
                    context.Error(
                        "scenario.vehicle.occupant.unknown",
                        $"Vehicle '{vehicle.entityId}' references unknown actor "
                        + $"'{vehicle.startingOccupantActorId}'.",
                        vehicle.entityId);
                }

                if (!VehicleMomentumProfile.TryValidate(
                        vehicle.maximumSpeed,
                        vehicle.accelerationPerTurn,
                        vehicle.brakingPerTurn,
                        vehicle.lowSpeedTurnDegrees,
                        vehicle.highSpeedTurnDegrees,
                        vehicle.baseTurningRadius,
                        vehicle.speedTurningRadiusFactor,
                        out _)
                    || !VehicleMomentumProfile.IsValidSpeed(
                        vehicle.startingSpeed,
                        vehicle.maximumSpeed))
                {
                    context.Error(
                        "scenario.vehicle.motion",
                        $"Scenario vehicle '{vehicle.entityId}' has invalid motion settings.",
                        vehicle.entityId);
                }
            }

            var objectiveIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelScenarioObjectiveData objective in scenario.objectives)
            {
                if (objective == null || string.IsNullOrWhiteSpace(objective.id))
                {
                    context.Error("scenario.objective.id.missing", "A scenario objective needs a stable ID.");
                    continue;
                }

                if (!objectiveIds.Add(objective.id))
                {
                    context.Error(
                        "scenario.objective.id.duplicate",
                        $"Scenario objective ID '{objective.id}' is duplicated.");
                }

                if (!entities.TryGetValue(objective.entityId ?? string.Empty, out LevelEntity entity))
                {
                    context.Error(
                        "scenario.objective.entity.unknown",
                        $"Objective '{objective.id}' references an unknown entity "
                        + $"'{objective.entityId}'.",
                        objective.entityId);
                    continue;
                }

                bool pointExists = entity.interactionPoints.Any(point =>
                    point != null
                    && string.Equals(
                        point.id,
                        objective.interactionPointId,
                        StringComparison.Ordinal));
                if (!pointExists)
                {
                    context.Error(
                        "scenario.objective.interaction.unknown",
                        $"Objective '{objective.id}' references unknown interaction point "
                        + $"'{objective.interactionPointId}'.",
                        objective.entityId);
                }

                if (objective.actionPointCost < 0
                    || !LevelValidationMath.IsFinite(
                        objective.movementOpportunityCost)
                    || objective.movementOpportunityCost < 0f
                    || !IsSupportedMobility(objective.mobility))
                {
                    context.Error(
                        "scenario.objective.cost",
                        $"Objective '{objective.id}' has an invalid action cost.",
                        objective.entityId);
                }
                if (string.IsNullOrWhiteSpace(objective.actionId)
                    || string.IsNullOrWhiteSpace(objective.displayName)
                    || string.IsNullOrWhiteSpace(objective.activeHudText)
                    || string.IsNullOrWhiteSpace(objective.completedHudText))
                {
                    context.Error(
                        "scenario.objective.presentation",
                        $"Objective '{objective.id}' needs an action, display name, and active and completed HUD text.",
                        objective.entityId);
                }
            }
        }

        private static bool IsSupportedSizeClass(string value)
        {
            return string.Equals(value, "small", StringComparison.Ordinal)
                || string.Equals(value, "medium", StringComparison.Ordinal)
                || string.Equals(value, "large", StringComparison.Ordinal)
                || string.Equals(value, "huge", StringComparison.Ordinal);
        }

        private static bool IsSupportedMobility(string value)
        {
            return ActionMobilityCodec.TryParse(value, out _);
        }
    }
}
