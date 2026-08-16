using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class ScenarioAuthoringCoordinator
    {
        private readonly LevelEditorWorkspace workspace;
        private readonly ScenarioAuthoringCatalog catalog;
        private readonly Func<LevelEditorCameraState> captureCameraState;

        public ScenarioAuthoringCoordinator(
            LevelEditorWorkspace workspace,
            ScenarioAuthoringCatalog catalog,
            Func<LevelEditorCameraState> captureCameraState)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.captureCameraState = captureCameraState
                ?? throw new ArgumentNullException(nameof(captureCameraState));
        }

        public event Action<string> StatusChanged;
        public event Action<string> ActorFocusRequested;
        public event Action<LevelScenarioActorData> ActorChanged;
        public event Action<LevelTransformData> PlayerStartChanged;

        public void ApplyPlayerStart(string xText, string yText, string zText, string yawText)
        {
            if (!TryParse(xText, out float x)
                || !TryParse(yText, out float y)
                || !TryParse(zText, out float z)
                || !TryParse(yawText, out float yaw))
            {
                Report("Player-start values must be finite numbers.");
                return;
            }

            LevelScenarioActorData player = workspace.CreateSnapshot().scenario?
                .FindInitiallySelectedPlayer();
            if (player == null)
            {
                Report("Add or select a player actor before setting the player start.");
                return;
            }

            LevelTransformData before = player.transform;
            var after = new LevelTransformData(new Float3Data(x, y, z), NormalizeYaw(yaw));
            workspace.Execute(new SetPlayerStartCommand(before, after));
            PlayerStartChanged?.Invoke(after);
            Report("Updated the selected scenario player's start.");
        }

        public void AddActor(string templateId)
        {
            ScenarioActorTemplateDefinition template = catalog.GetActor(templateId);
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelEditorCameraState cameraState = captureCameraState();
            bool playerControlled = template.PlayerTemplate;
            var actor = new LevelScenarioActorData
            {
                id = "actor-" + LevelDocumentFactory.NewStableId(),
                templateId = template.TemplateId,
                transform = new LevelTransformData(
                    new Float3Data(
                        cameraState.target.x,
                        cameraState.target.y,
                        cameraState.target.z),
                    NormalizeYaw(cameraState.yaw)),
                playerControlled = playerControlled,
                initiallySelected = playerControlled
                    && snapshot.scenario.FindInitiallySelectedPlayer() == null,
                primaryTarget = !playerControlled
                    && !snapshot.scenario.actors.Any(candidate => candidate?.primaryTarget == true),
            };
            workspace.Execute(new AddScenarioActorCommand(actor));
            ActorFocusRequested?.Invoke(actor.id);
            Report($"Added {template.DisplayName} to the scenario at the camera focus.");
        }

        public void ApplyActor(
            string actorId,
            string xText,
            string yText,
            string zText,
            string yawText,
            bool playerControlled,
            bool initiallySelected,
            bool primaryTarget)
        {
            if (!TryParse(xText, out float x)
                || !TryParse(yText, out float y)
                || !TryParse(zText, out float z)
                || !TryParse(yawText, out float yaw))
            {
                Report("Scenario actor transforms must contain finite numbers.");
                return;
            }

            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioActorData before = FindActor(snapshot, actorId);
            if (before == null)
                return;
            if (initiallySelected && !playerControlled)
            {
                Report("The initially selected actor must be player controlled.");
                return;
            }
            if (primaryTarget && playerControlled)
            {
                Report("A player actor cannot also be the primary target.");
                return;
            }
            if (before.initiallySelected && !initiallySelected)
            {
                Report("Select another player actor before clearing the current selection.");
                return;
            }

            var commands = new List<ILevelEditCommand>();
            ClearExclusiveActorFlag(
                snapshot,
                actorId,
                initiallySelected,
                actor => actor.initiallySelected,
                actor => actor.initiallySelected = false,
                commands);
            ClearExclusiveActorFlag(
                snapshot,
                actorId,
                primaryTarget,
                actor => actor.primaryTarget,
                actor => actor.primaryTarget = false,
                commands);

            LevelScenarioActorData after = before.DeepCopy();
            after.transform = new LevelTransformData(
                new Float3Data(x, y, z),
                NormalizeYaw(yaw));
            after.playerControlled = playerControlled;
            after.initiallySelected = initiallySelected;
            after.primaryTarget = primaryTarget;
            commands.Add(new SetScenarioActorCommand(actorId, before, after));
            Execute("Edit scenario actor", commands);
            ActorChanged?.Invoke(after);
            if (after.initiallySelected)
                PlayerStartChanged?.Invoke(after.transform);
            Report($"Updated scenario actor '{actorId}'.");
        }

        public void DeleteActor(string actorId)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioActorData actor = FindActor(snapshot, actorId);
            if (actor == null)
                return;
            LevelScenarioActorData[] otherPlayers = snapshot.scenario.actors
                .Where(candidate => candidate != null
                    && candidate.playerControlled
                    && !string.Equals(candidate.id, actorId, StringComparison.Ordinal))
                .ToArray();
            if (actor.playerControlled && otherPlayers.Length == 0)
            {
                Report("A scenario must keep at least one player actor.");
                return;
            }

            var commands = new List<ILevelEditCommand>();
            LevelScenarioData linksAfter = snapshot.scenario.DeepCopy();
            bool clearedOccupant = false;
            foreach (LevelScenarioVehicleData vehicle in linksAfter.vehicles.Where(vehicle =>
                string.Equals(vehicle?.startingOccupantActorId, actorId, StringComparison.Ordinal)))
            {
                vehicle.startingOccupantActorId = string.Empty;
                clearedOccupant = true;
            }
            if (clearedOccupant)
            {
                commands.Add(new SetScenarioConfigurationCommand(
                    "Clear deleted vehicle occupant",
                    snapshot.scenario,
                    linksAfter,
                    linksAfter.vehicles.Select(vehicle => vehicle?.entityId)));
            }
            if (actor.initiallySelected)
            {
                LevelScenarioActorData next = otherPlayers[0];
                LevelScenarioActorData selected = next.DeepCopy();
                selected.initiallySelected = true;
                commands.Add(new SetScenarioActorCommand(next.id, next, selected));
            }
            commands.Add(new DeleteScenarioActorCommand(actorId));
            Execute("Delete scenario actor", commands);
            ActorFocusRequested?.Invoke(null);
            LevelScenarioActorData selectedPlayer = workspace.CreateSnapshot().scenario
                .FindInitiallySelectedPlayer();
            if (selectedPlayer != null)
                PlayerStartChanged?.Invoke(selectedPlayer.transform);
            Report($"Deleted scenario actor '{actorId}'.");
        }

        public void ApplyActorCharacter(string actorId, string characterId)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioActorData before = FindActor(snapshot, actorId);
            if (before == null)
                return;
            LevelScenarioActorData after = before.DeepCopy();
            after.characterId = characterId?.Trim() ?? string.Empty;
            workspace.Execute(new SetScenarioActorCommand(actorId, before, after));
            ActorChanged?.Invoke(after);
            Report(string.IsNullOrEmpty(after.characterId)
                ? $"Restored template appearance for '{actorId}'."
                : $"Assigned character '{after.characterId}' to '{actorId}'.");
        }

        public void PlaceActorAtView(string actorId)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioActorData before = FindActor(snapshot, actorId);
            if (before == null)
                return;
            LevelEditorCameraState cameraState = captureCameraState();
            LevelScenarioActorData after = before.DeepCopy();
            after.transform = new LevelTransformData(
                new Float3Data(
                    cameraState.target.x,
                    cameraState.target.y,
                    cameraState.target.z),
                after.transform.yawDegrees);
            workspace.Execute(new SetScenarioActorCommand(actorId, before, after));
            ActorChanged?.Invoke(after);
            if (after.initiallySelected)
                PlayerStartChanged?.Invoke(after.transform);
            Report($"Placed scenario actor '{actorId}' at the camera focus.");
        }

        public void ApplyProp(
            string entityId,
            bool enabled,
            string massText,
            string sizeClass,
            bool startsEncounter,
            bool topplingEnabled,
            string topplingPitchText,
            string topplingRollText,
            string topplingElevationText)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioData after = snapshot.scenario.DeepCopy();
            after.props.RemoveAll(prop => string.Equals(prop?.entityId, entityId, StringComparison.Ordinal));
            if (enabled)
            {
                if (!TryParse(massText, out float mass) || mass <= 0f)
                {
                    Report("Scenario prop mass must be a positive finite number.");
                    return;
                }
                if (!IsSupportedSizeClass(sizeClass))
                {
                    Report("Choose a supported scenario prop size.");
                    return;
                }
                if (!TryParse(topplingPitchText, out float topplingPitch)
                    || !TryParse(topplingRollText, out float topplingRoll)
                    || !TryParse(
                        topplingElevationText,
                        out float topplingElevation)
                    || topplingElevation < 0f)
                {
                    Report(
                        "Toppling offsets must be finite and elevation cannot be negative.");
                    return;
                }
                if (topplingEnabled
                    && topplingPitch == 0f
                    && topplingRoll == 0f)
                {
                    Report(
                        "Toppling needs a non-zero pitch or roll offset.");
                    return;
                }
                after.props.Add(new LevelScenarioPropData
                {
                    entityId = entityId,
                    mass = mass,
                    sizeClass = sizeClass,
                    startsEncounterOnAttack = startsEncounter,
                    toppling = new LevelScenarioPropTopplingData
                    {
                        enabled = topplingEnabled,
                        pitchOffsetDegrees = topplingPitch,
                        rollOffsetDegrees = topplingRoll,
                        elevationOffset = topplingElevation,
                    },
                });
            }
            workspace.Execute(new SetScenarioConfigurationCommand(
                enabled ? "Configure scenario prop" : "Remove scenario prop",
                snapshot.scenario,
                after,
                new[] { entityId }));
            Report(enabled
                ? "Linked the selected entity as a gameplay physics prop."
                : "Removed the selected entity's gameplay prop link.");
        }

        public void ApplyObjective(
            string entityId,
            string pointId,
            bool enabled,
            string displayName,
            string activeText,
            string completedText,
            string actionPointCostText,
            string movementOpportunityCostText,
            string mobility)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioData after = snapshot.scenario.DeepCopy();
            LevelScenarioObjectiveData existing = after.objectives.FirstOrDefault(objective =>
                string.Equals(objective?.entityId, entityId, StringComparison.Ordinal)
                && string.Equals(objective?.interactionPointId, pointId, StringComparison.Ordinal));
            after.objectives.Remove(existing);
            if (enabled)
            {
                if (!int.TryParse(
                        actionPointCostText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int actionPointCost) || actionPointCost < 0)
                {
                    Report("Objective action-point cost must be a non-negative whole number.");
                    return;
                }
                if (!float.TryParse(
                        movementOpportunityCostText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float movementOpportunityCost)
                    || float.IsNaN(movementOpportunityCost)
                    || float.IsInfinity(movementOpportunityCost)
                    || movementOpportunityCost < 0f)
                {
                    Report("Objective movement cost must be a non-negative finite number.");
                    return;
                }
                string normalizedMobility = mobility?.Trim().ToLowerInvariant()
                    ?? string.Empty;
                if (!string.Equals(normalizedMobility, "mobile", StringComparison.Ordinal)
                    && !string.Equals(normalizedMobility, "momentum", StringComparison.Ordinal)
                    && !string.Equals(normalizedMobility, "set", StringComparison.Ordinal))
                {
                    Report("Objective mobility must be Mobile, Momentum, or Set.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    Report("An enabled objective needs a display name.");
                    return;
                }
                after.objectives.Add(new LevelScenarioObjectiveData
                {
                    id = existing?.id ?? "objective-" + LevelDocumentFactory.NewStableId(),
                    entityId = entityId,
                    interactionPointId = pointId,
                    actionId = existing?.actionId ?? "interact",
                    displayName = displayName.Trim(),
                    activeHudText = activeText?.Trim() ?? string.Empty,
                    completedHudText = completedText?.Trim() ?? string.Empty,
                    actionPointCost = actionPointCost,
                    movementOpportunityCost = movementOpportunityCost,
                    mobility = normalizedMobility,
                });
            }
            workspace.Execute(new SetScenarioConfigurationCommand(
                enabled ? "Configure scenario objective" : "Remove scenario objective",
                snapshot.scenario,
                after,
                new[] { entityId }));
            Report(enabled
                ? "Linked the interaction point as a scenario objective."
                : "Removed the interaction point's scenario objective link.");
        }

        public void ApplyVehicle(
            string entityId,
            bool enabled,
            string maximumSpeedText,
            string accelerationText,
            string brakingText,
            string lowTurnText,
            string highTurnText,
            string baseRadiusText,
            string radiusFactorText,
            string startingSpeedText,
            string occupantActorId,
            bool startsEncounter)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelScenarioData after = snapshot.scenario.DeepCopy();
            after.vehicles.RemoveAll(vehicle =>
                string.Equals(vehicle?.entityId, entityId, StringComparison.Ordinal));
            if (enabled)
            {
                if (!TryParse(maximumSpeedText, out float maximumSpeed)
                    || !TryParse(accelerationText, out float acceleration)
                    || !TryParse(brakingText, out float braking)
                    || !TryParse(lowTurnText, out float lowTurn)
                    || !TryParse(highTurnText, out float highTurn)
                    || !TryParse(baseRadiusText, out float baseRadius)
                    || !TryParse(radiusFactorText, out float radiusFactor)
                    || !TryParse(startingSpeedText, out float startingSpeed)
                    || maximumSpeed <= 0f || acceleration < 0f || braking < 0f
                    || lowTurn < 0f || highTurn < 0f || baseRadius <= 0f
                    || radiusFactor < 0f || startingSpeed < 0f
                    || startingSpeed > maximumSpeed)
                {
                    Report("Vehicle values must be finite and non-negative; start speed cannot exceed max speed.");
                    return;
                }
                string occupant = occupantActorId?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(occupant) && FindActor(snapshot, occupant) == null)
                {
                    Report($"Vehicle occupant actor '{occupant}' does not exist.");
                    return;
                }
                after.vehicles.Add(new LevelScenarioVehicleData
                {
                    entityId = entityId,
                    maximumSpeed = maximumSpeed,
                    accelerationPerTurn = acceleration,
                    brakingPerTurn = braking,
                    lowSpeedTurnDegrees = lowTurn,
                    highSpeedTurnDegrees = highTurn,
                    baseTurningRadius = baseRadius,
                    speedTurningRadiusFactor = radiusFactor,
                    startingSpeed = startingSpeed,
                    startingOccupantActorId = occupant,
                    startsEncounterOnAttack = startsEncounter,
                });
            }
            workspace.Execute(new SetScenarioConfigurationCommand(
                enabled ? "Configure scenario vehicle" : "Remove scenario vehicle",
                snapshot.scenario,
                after,
                new[] { entityId }));
            Report(enabled
                ? "Linked the selected entity as a driveable scenario vehicle."
                : "Removed the selected entity's scenario vehicle link.");
        }

        private static void ClearExclusiveActorFlag(
            LevelDocument snapshot,
            string actorId,
            bool requested,
            Func<LevelScenarioActorData, bool> isSet,
            Action<LevelScenarioActorData> clear,
            ICollection<ILevelEditCommand> commands)
        {
            if (!requested)
                return;
            foreach (LevelScenarioActorData other in snapshot.scenario.actors.Where(actor =>
                actor != null && isSet(actor)
                && !string.Equals(actor.id, actorId, StringComparison.Ordinal)))
            {
                LevelScenarioActorData replacement = other.DeepCopy();
                clear(replacement);
                commands.Add(new SetScenarioActorCommand(other.id, other, replacement));
            }
        }

        private void Execute(string description, IReadOnlyList<ILevelEditCommand> commands)
        {
            if (commands.Count == 1)
                workspace.Execute(commands[0]);
            else
                workspace.ExecuteTransaction(description, commands);
        }

        private void Report(string message) => StatusChanged?.Invoke(message);

        private static LevelScenarioActorData FindActor(LevelDocument document, string actorId)
        {
            return document?.scenario?.actors.FirstOrDefault(actor =>
                string.Equals(actor?.id, actorId, StringComparison.Ordinal));
        }

        private static bool IsSupportedSizeClass(string value)
        {
            return string.Equals(value, "small", StringComparison.Ordinal)
                || string.Equals(value, "medium", StringComparison.Ordinal)
                || string.Equals(value, "large", StringComparison.Ordinal)
                || string.Equals(value, "huge", StringComparison.Ordinal);
        }

        private static bool TryParse(string text, out float value)
        {
            bool parsed = float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
            return parsed && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.Repeat(yaw + 180f, 360f) - 180f;
        }
    }
}
