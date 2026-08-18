using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayPropAssembler
    {
        internal static Dictionary<string, ScenarioPropContentData> Index(
            IReadOnlyList<ScenarioPropContentData> props,
            LevelDocument level)
        {
            var levelEntityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelEntity entity in level.entities)
            {
                levelEntityIds.Add(entity.id);
            }

            var index = new Dictionary<string, ScenarioPropContentData>(
                StringComparer.Ordinal);
            foreach (ScenarioPropContentData prop in props)
            {
                Require(prop != null, "Scenario props cannot contain null entries.");
                RequireText(prop.entityId, "Scenario prop entity ID");
                Require(
                    levelEntityIds.Contains(prop.entityId),
                    $"Prop '{prop.entityId}' is missing from level '{level.levelId}'.");
                RequireFinitePositive(prop.mass, $"Prop '{prop.entityId}' mass");
                GameplayDisplacementAssembler.ParseSize(prop.sizeClass);
                GameplayDisplacementAssembler.ValidateProp(prop);
                Require(
                    index.TryAdd(prop.entityId, prop),
                    $"Prop '{prop.entityId}' is defined more than once.");
            }

            return index;
        }

        private static void RequireText(string value, string label) =>
            GameplayScenarioAssemblyValidation.RequireText(value, label);

        private static void RequireFinitePositive(float value, string label) =>
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                value,
                label);

        private static void Require(bool condition, string message) =>
            GameplayScenarioAssemblyValidation.Require(condition, message);
    }
}
