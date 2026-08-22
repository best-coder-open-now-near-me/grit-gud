using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public static class ScenarioContentMigrator
    {
        private const int OldestSupportedSchemaVersion = 15;

        public static ScenarioContentDocument Migrate(
            ScenarioContentDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.schemaVersion > ScenarioContentDocument.CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Scenario schema {document.schemaVersion} is newer than supported schema {ScenarioContentDocument.CurrentSchemaVersion}.");
            if (document.schemaVersion < OldestSupportedSchemaVersion)
                throw new InvalidOperationException(
                    $"Scenario schema {document.schemaVersion} is older than supported migration schema {OldestSupportedSchemaVersion}.");

            document.Normalize();
            if (document.schemaVersion < 17)
                InstallLegacyActionPointEconomy(document);
            if (document.schemaVersion < 22)
                RenameLegacyDroneSummoners(document);
            if (document.schemaVersion < 23)
                SplitLegacyDroneDefinitions(document);
            document.schemaVersion = ScenarioContentDocument.CurrentSchemaVersion;
            return document;
        }

        private static void SplitLegacyDroneDefinitions(
            ScenarioContentDocument document)
        {
            var archetypeIds = new HashSet<string>(StringComparer.Ordinal);
            var abilityIds = new HashSet<string>(StringComparer.Ordinal);
            int index = 0;
            foreach (ScenarioDroneContentData legacy in document.drones)
            {
                if (legacy == null) continue;
                index++;
                string archetypeId = "drone-archetype."
                    + (legacy.entityId ?? string.Empty);
                string abilityId = index == 1
                    ? "ability.summon-drone"
                    : "ability.summon-drone." + index;
                if (!archetypeIds.Add(archetypeId)
                    || !abilityIds.Add(abilityId))
                    throw new InvalidOperationException(
                        "Legacy drone migration produced duplicate archetype or ability IDs.");
                document.droneArchetypes.Add(
                    new ScenarioDroneArchetypeContentData
                    {
                        archetypeId = archetypeId,
                        presentationId = "presentation.drone.scout",
                        maximumIntegrity = legacy.maximumIntegrity,
                        maximumMoveDistance = legacy.maximumMoveDistance,
                        moveCost = legacy.moveCost,
                        sensorRange = legacy.sensorRange,
                        sensorViewAngleDegrees =
                            legacy.sensorViewAngleDegrees,
                        attackCapability = legacy.attackCapability,
                        crash = new ScenarioDroneCrashData
                        {
                            impactRadius = 2.5f,
                            injuryMovementPenalty = 0.75f,
                            destructibleIntegrityDamage = 1f,
                            maximumActionPointReduction = 1,
                            maximumDriftDistance = 0.75f,
                            impactPlaybackSeconds = 0.7f,
                        },
                    });
                document.droneSummonAbilities.Add(
                    new ScenarioDroneSummonAbilityContentData
                    {
                        abilityId = abilityId,
                        summonerActorId = legacy.summonerActorId,
                        droneArchetypeId = archetypeId,
                        summonCost = new ScenarioActionCostData
                        {
                            actionPoints = 1,
                            movementOpportunity = 0f,
                            mobility = "Set",
                        },
                        maximumSpawnDistance = 5f,
                        maximumActiveInstances = 1,
                        durationTurns = 0,
                        spawnHeight = 2f,
                    });
            }
            document.drones.Clear();
        }

        private static void RenameLegacyDroneSummoners(
            ScenarioContentDocument document)
        {
            foreach (ScenarioDroneContentData drone in document.drones)
            {
                if (drone == null) continue;
                if (string.IsNullOrWhiteSpace(drone.summonerActorId))
                    drone.summonerActorId = drone.controllerActorId
                        ?? string.Empty;
                drone.controllerActorId = string.Empty;
            }
        }

        private static void InstallLegacyActionPointEconomy(
            ScenarioContentDocument document)
        {
            if (document.actors.Count == 0)
                throw new InvalidOperationException(
                    "Legacy scenario migration requires at least one actor.");
            int starting = document.actors[0]?.turnBudget?.actionPoints
                ?? throw new InvalidOperationException(
                    "Legacy scenario actors require turn budgets.");
            foreach (ScenarioActorContentData actor in document.actors)
            {
                if (actor?.turnBudget == null
                    || actor.turnBudget.actionPoints != starting)
                    throw new InvalidOperationException(
                        "Legacy scenarios with different actor AP allowances require explicit economy authoring.");
            }
            document.timing.startingActionPoints = starting;
            document.timing.actionPointIncome = starting;
            document.timing.maximumHeldActionPoints = starting;
        }
    }
}
