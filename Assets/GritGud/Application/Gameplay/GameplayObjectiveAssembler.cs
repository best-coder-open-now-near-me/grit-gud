using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayObjectiveAssembler
    {
        public static ScenarioObjectiveDefinition Create(
            LevelDocument level,
            ScenarioObjectiveContentData objective)
        {
            RequireText(
                objective.levelInteractionPointId,
                $"Objective '{objective.id}' interaction-point ID");
            RequireText(
                objective.levelInteractionPointType,
                $"Objective '{objective.id}' interaction-point type");
            RequireText(objective.actionId, $"Objective '{objective.id}' action ID");
            RequireText(
                objective.displayName,
                $"Objective '{objective.id}' display name");
            RequireText(
                objective.activeHudText,
                $"Objective '{objective.id}' active HUD text");
            RequireText(
                objective.completedHudText,
                $"Objective '{objective.id}' completed HUD text");

            LevelEntity matchedEntity = null;
            InteractionPointData matchedPoint = null;
            foreach (LevelEntity entity in level.entities)
            {
                foreach (InteractionPointData point in entity.interactionPoints)
                {
                    if (!string.Equals(
                            point.id,
                            objective.levelInteractionPointId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            point.type,
                            objective.levelInteractionPointType,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Require(
                        matchedPoint == null,
                        $"Level '{level.levelId}' defines interaction point "
                        + $"'{point.id}' of type '{point.type}' more than once.");
                    matchedEntity = entity;
                    matchedPoint = point;
                }
            }

            Require(
                matchedPoint != null,
                $"Level '{level.levelId}' does not define interaction point "
                + $"'{objective.levelInteractionPointId}' of type "
                + $"'{objective.levelInteractionPointType}'.");
            GameplayPosition worldPosition = TransformPoint(
                matchedEntity.transform,
                matchedPoint.localPosition);
            ScenarioActionCostData cost = objective.turnCost
                ?? throw new InvalidOperationException(
                    $"Objective '{objective.id}' does not define an action cost.");
            return new ScenarioObjectiveDefinition(
                objective.id,
                worldPosition,
                matchedPoint.radius,
                new GameplayInteractionDefinition(
                    objective.actionId,
                    objective.displayName,
                    new ActionCost(
                        cost.actionPoints,
                        cost.movementOpportunity,
                        GameplayScenarioAssemblyValidation.ParseMobility(
                            cost.mobility))));
        }

        private static GameplayPosition TransformPoint(
            LevelTransformData transform,
            Float3Data local)
        {
            double radians = transform.yawDegrees * (Math.PI / 180d);
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            return new GameplayPosition(
                transform.position.x
                    + (float)((local.x * cosine) + (local.z * sine)),
                transform.position.y + local.y,
                transform.position.z
                    + (float)((-local.x * sine) + (local.z * cosine)));
        }

        private static void RequireText(string value, string label) =>
            GameplayScenarioAssemblyValidation.RequireText(value, label);

        private static void Require(bool condition, string message) =>
            GameplayScenarioAssemblyValidation.Require(condition, message);
    }
}
