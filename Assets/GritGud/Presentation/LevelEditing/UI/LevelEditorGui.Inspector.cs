using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed partial class LevelEditorGui
    {
        private void DrawInspector(LevelEditorViewState state)
        {
            LevelEntityView selectedView = state.SelectedView;
            IReadOnlyList<LevelValidationIssue> validationIssues = state.ValidationIssues;
            float left = Screen.width - LevelEditorGuiMetrics.InspectorWidth;
            GUILayout.BeginArea(
                new Rect(
                    left,
                    LevelEditorGuiMetrics.ToolbarHeight,
                    LevelEditorGuiMetrics.InspectorWidth,
                    Screen.height
                    - LevelEditorGuiMetrics.ToolbarHeight
                    - LevelEditorGuiMetrics.StatusBarHeight),
                GUI.skin.box);
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);
            DrawSectionHeader("INSPECTOR");
            if (presentationState.InspectorTarget.Kind
                == LevelEditorInspectorTargetKind.ScenarioActor)
            {
                LevelScenarioActorData actor = state.Document.scenario.actors
                    .FirstOrDefault(candidate => string.Equals(
                        candidate?.id,
                        presentationState.InspectorTarget.TargetId,
                        StringComparison.Ordinal));
                DrawScenarioActorInspector(actor);
            }
            else if (selectedView == null)
            {
                GUILayout.Label("Select an entity, interaction point, or scenario actor to edit it.");
            }
            else
            {
                GUILayout.Label(selectedView.Archetype.DisplayName);
                GUILayout.Label($"ID: {selection.PrimaryEntityId}");
                LevelEntity entity = state.Document.entities.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate?.id,
                        selection.PrimaryEntityId,
                        StringComparison.Ordinal));
                LevelSelectionTarget? primary = selection.Primary;
                if (selection.Targets.Count > 1)
                {
                    GUILayout.Label($"{selection.Targets.Count} entities selected");
                }
                GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
                DrawLabeledField("X", ref xText);
                DrawLabeledField("Y", ref yText);
                DrawLabeledField("Z", ref zText);
                DrawLabeledField("Yaw", ref yawText);
                if (GUILayout.Button("APPLY", PanelPrimaryButtonLayout()))
                {
                    actions.ApplyEntityTransform(xText, yText, zText, yawText);
                }

                GUILayout.BeginHorizontal();
                float angleSnap = selectedView.Archetype.PlacementRules.AngleSnap;
                if (GUILayout.Button($"↺ {angleSnap:0.#}°"))
                {
                    selectionTool.RotateSelection(-angleSnap);
                }

                if (GUILayout.Button($"{angleSnap:0.#}° ↻"))
                {
                    selectionTool.RotateSelection(angleSnap);
                }
                GUILayout.EndHorizontal();
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = LevelEditorTheme.Destructive;
                if (GUILayout.Button("DELETE", PanelPrimaryButtonLayout()))
                {
                    selectionTool.DeleteSelection();
                }
                GUI.backgroundColor = previous;

                GUILayout.Space(LevelEditorGuiMetrics.SpaceInspectorSection);
                DrawInteractionInspector(entity, primary, state.Document.scenario);
                DrawDestructibleInspector(selectedView, entity);
                DrawScenarioPropInspector(selectedView, entity, state.Document.scenario);
                DrawScenarioVehicleInspector(selectedView, entity, state.Document.scenario);
            }

            GUILayout.Space(LevelEditorGuiMetrics.SpaceMajor);
            if (DrawSectionExpander(
                    $"VALIDATION ({validationIssues?.Count ?? 0})",
                    ref showValidation))
            {
                if (validationIssues == null || validationIssues.Count == 0)
                {
                    GUILayout.Label("No validation issues.");
                }
                else
                {
                    foreach (LevelValidationIssue issue in validationIssues.Take(8))
                    {
                        string issueText = $"{issue.Severity}: {issue.Message}";
                        if (string.IsNullOrWhiteSpace(issue.EntityId))
                        {
                            GUILayout.Label(issueText);
                        }
                        else if (GUILayout.Button(issueText))
                        {
                            actions.FocusEntity(issue.EntityId);
                        }
                    }

                    if (validationIssues.Count > 8)
                    {
                        GUILayout.Label($"…and {validationIssues.Count - 8} more.");
                    }
                }
            }

            GUILayout.Space(LevelEditorGuiMetrics.SpaceMajor);
            if (DrawSectionExpander("PORTABLE FILES", ref showPortableFiles))
            {
                if (actions.UsesBrowserFileDialog)
                {
                    GUILayout.Label(
                        "Import opens the browser file picker. Export downloads a JSON file.");
                }
                else
                {
                    GUILayout.Label("Desktop import path:");
                    actions.DesktopImportPath = GUILayout.TextField(actions.DesktopImportPath);
                    GUILayout.Label(
                        "Exports are written beneath the application's persistent-data folder.");
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }


        private void DrawScenarioActorInspector(LevelScenarioActorData actor)
        {
            if (actor == null)
            {
                GUILayout.Label("The selected scenario actor is no longer available.");
                return;
            }

            DrawSectionHeader("SCENARIO ACTOR");
            GUILayout.Label($"ID: {actor.id}");
            GUILayout.Label($"Template: {actor.templateId}");
            DrawLabeledField("X", ref scenarioXText);
            DrawLabeledField("Y", ref scenarioYText);
            DrawLabeledField("Z", ref scenarioZText);
            DrawLabeledField("Yaw", ref scenarioYawText);
            scenarioPlayerControlled = GUILayout.Toggle(
                scenarioPlayerControlled,
                "Player controlled");
            if (scenarioPlayerControlled)
                scenarioPrimaryTarget = false;
            else
                scenarioInitiallySelected = false;
            GUI.enabled = scenarioPlayerControlled;
            scenarioInitiallySelected = GUILayout.Toggle(
                scenarioInitiallySelected,
                "Initially selected party actor");
            GUI.enabled = !scenarioPlayerControlled;
            scenarioPrimaryTarget = GUILayout.Toggle(
                scenarioPrimaryTarget,
                "Primary target");
            GUI.enabled = true;

            if (GUILayout.Button("APPLY", PanelApplyButtonLayout()))
            {
                actions.ApplyScenarioActor(
                    actor.id,
                    scenarioXText,
                    scenarioYText,
                    scenarioZText,
                    scenarioYawText,
                    scenarioPlayerControlled,
                    scenarioInitiallySelected,
                    scenarioPrimaryTarget);
            }

            if (GUILayout.Button("PLACE AT VIEW", PanelButtonLayout()))
                actions.PlaceScenarioActorAtView(actor.id);

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = LevelEditorTheme.Destructive;
            if (GUILayout.Button("REMOVE ACTOR", PanelButtonLayout()))
                actions.DeleteScenarioActor(actor.id);
            GUI.backgroundColor = previous;
        }


        private void DrawPlayerStartInspector()
        {
            DrawSectionHeader("SCENARIO PLAYER START");
            DrawLabeledField("X", ref playerStartXText);
            DrawLabeledField("Y", ref playerStartYText);
            DrawLabeledField("Z", ref playerStartZText);
            DrawLabeledField("Yaw", ref playerStartYawText);
            if (GUILayout.Button("SET START", PanelButtonLayout()))
            {
                actions.ApplyPlayerStart(
                    playerStartXText,
                    playerStartYText,
                    playerStartZText,
                    playerStartYawText);
            }

            GUILayout.Space(LevelEditorGuiMetrics.SpaceInspectorSection);
        }


        private void DrawInteractionInspector(
            LevelEntity entity,
            LevelSelectionTarget? primary,
            LevelScenarioData scenario)
        {
            DrawSectionHeader("INTERACTION POINTS");
            if (entity == null)
            {
                return;
            }

            if (primary != null && primary.Value.Kind == LevelSelectionKind.InteractionPoint)
            {
                InteractionPointData point = entity.interactionPoints.FirstOrDefault(candidate =>
                    string.Equals(candidate?.id, primary.Value.ElementId, StringComparison.Ordinal));
                if (point != null)
                {
                    SyncInteractionFields(point);
                    GUILayout.Label($"ID: {point.id}");
                    interactionType = GUILayout.SelectionGrid(
                        interactionType == "doorway" ? 1 : 0,
                        new[] { "OBJECTIVE", "DOORWAY" },
                        2) == 1 ? "doorway" : "objective";
                    DrawLabeledField("X", ref interactionXText);
                    DrawLabeledField("Y", ref interactionYText);
                    DrawLabeledField("Z", ref interactionZText);
                    DrawLabeledField("Radius", ref interactionRadiusText);
                    if (GUILayout.Button("APPLY POINT", PanelButtonLayout()))
                    {
                        actions.ApplyInteractionPoint(
                            interactionType,
                            interactionXText,
                            interactionYText,
                            interactionZText,
                            interactionRadiusText);
                    }

                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = LevelEditorTheme.Destructive;
                    if (GUILayout.Button("REMOVE POINT", PanelButtonLayout()))
                    {
                        actions.DeleteInteractionPoint();
                    }
                    GUI.backgroundColor = previous;
                    DrawScenarioObjectiveInspector(entity, point, scenario);
                    return;
                }
            }

            if (GUILayout.Button("+ POINT", PanelButtonLayout()))
            {
                actions.AddInteractionPoint();
            }
            GUILayout.Label("Select a pink world handle to edit an existing point.");
        }


        private void DrawDestructibleInspector(LevelEntityView view, LevelEntity entity)
        {
            if ((view.Archetype.Capabilities & LevelArchetypeCapabilities.Destructible) == 0)
            {
                return;
            }

            GUILayout.Space(LevelEditorGuiMetrics.SpaceInspectorSection);
            DrawSectionHeader("DESTRUCTIBLE DEFAULTS");
            DestructibleInstanceData data = entity?.destructible;
            if (!string.Equals(lastDestructibleEntityId, entity?.id, StringComparison.Ordinal))
            {
                lastDestructibleEntityId = entity?.id ?? string.Empty;
                if (data != null)
                {
                    destructibleEnabled = data.enabled;
                    destructibleState = data.initialState;
                    destructibleIntegrity = data.integrity.ToString("0.###", CultureInfo.InvariantCulture);
                }
                else
                {
                    destructibleEnabled = true;
                    destructibleState = "intact";
                    destructibleIntegrity = "10";
                }
            }

            destructibleEnabled = GUILayout.Toggle(destructibleEnabled, "ENABLED");
            destructibleState = GUILayout.SelectionGrid(
                DestructibleStateIndex(destructibleState),
                new[] { "INTACT", "DAMAGED", "DESTROYED" },
                3) switch
            {
                1 => "damaged",
                2 => "destroyed",
                _ => "intact",
            };
            DrawLabeledField("Integrity", ref destructibleIntegrity);
            if (GUILayout.Button("APPLY DAMAGE", PanelButtonLayout()))
            {
                actions.ApplyDestructibleDefaults(
                    destructibleEnabled ? "true" : "false",
                    destructibleState,
                    destructibleIntegrity);
            }
        }


        private void DrawScenarioPropInspector(
            LevelEntityView view,
            LevelEntity entity,
            LevelScenarioData scenario)
        {
            if ((view.Archetype.Capabilities & LevelArchetypeCapabilities.Destructible) == 0)
                return;

            LevelScenarioPropData configured = scenario.props
                .FirstOrDefault(prop => string.Equals(
                    prop?.entityId,
                    entity.id,
                    StringComparison.Ordinal));
            if (!string.Equals(lastScenarioPropEntityId, entity.id, StringComparison.Ordinal))
            {
                lastScenarioPropEntityId = entity.id;
                scenarioPropEnabled = configured != null;
                scenarioPropMassText = (configured?.mass ?? 25f).ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
                scenarioPropSize = configured?.sizeClass ?? "medium";
                scenarioPropStartsEncounter = configured?.startsEncounterOnAttack ?? false;
            }

            GUILayout.Space(LevelEditorGuiMetrics.SpaceInspectorSection);
            DrawSectionHeader("SCENARIO PHYSICS PROP");
            scenarioPropEnabled = GUILayout.Toggle(
                scenarioPropEnabled,
                "Physics / combat prop");
            GUI.enabled = scenarioPropEnabled;
            DrawLabeledField("Mass", ref scenarioPropMassText);
            scenarioPropSize = GUILayout.SelectionGrid(
                ScenarioSizeIndex(scenarioPropSize),
                new[] { "SMALL", "MEDIUM", "LARGE", "HUGE" },
                4) switch
            {
                0 => "small",
                2 => "large",
                3 => "huge",
                _ => "medium",
            };
            scenarioPropStartsEncounter = GUILayout.Toggle(
                scenarioPropStartsEncounter,
                "Attack starts encounter");
            GUI.enabled = true;
            if (GUILayout.Button("APPLY PROP", PanelButtonLayout()))
            {
                actions.ApplyScenarioProp(
                    entity.id,
                    scenarioPropEnabled,
                    scenarioPropMassText,
                    scenarioPropSize,
                    scenarioPropStartsEncounter);
            }
        }


        private void DrawScenarioObjectiveInspector(
            LevelEntity entity,
            InteractionPointData point,
            LevelScenarioData scenario)
        {
            string key = entity.id + ":" + point.id;
            LevelScenarioObjectiveData configured = scenario.objectives
                .FirstOrDefault(objective =>
                    string.Equals(objective?.entityId, entity.id, StringComparison.Ordinal)
                    && string.Equals(
                        objective?.interactionPointId,
                        point.id,
                        StringComparison.Ordinal));
            if (!string.Equals(lastScenarioObjectiveKey, key, StringComparison.Ordinal))
            {
                lastScenarioObjectiveKey = key;
                scenarioObjectiveEnabled = configured != null;
                scenarioObjectiveDisplayName = configured?.displayName ?? "Objective";
                scenarioObjectiveActiveText = configured?.activeHudText
                    ?? "Complete the objective";
                scenarioObjectiveCompletedText = configured?.completedHudText
                    ?? "Objective complete";
                scenarioObjectiveCostText = (configured?.actionPointCost ?? 1).ToString(
                    CultureInfo.InvariantCulture);
                scenarioObjectiveMovementCostText =
                    (configured?.movementOpportunityCost ?? 0f).ToString(
                        CultureInfo.InvariantCulture);
                scenarioObjectiveMobility = configured?.mobility ?? "set";
            }

            GUILayout.Space(LevelEditorGuiMetrics.SpaceInspectorSection);
            DrawSectionHeader("SCENARIO OBJECTIVE");
            GUI.enabled = string.Equals(point.type, "objective", StringComparison.Ordinal);
            scenarioObjectiveEnabled = GUILayout.Toggle(
                scenarioObjectiveEnabled,
                "Use as objective");
            GUI.enabled = scenarioObjectiveEnabled
                && string.Equals(point.type, "objective", StringComparison.Ordinal);
            GUILayout.Label("Display name");
            scenarioObjectiveDisplayName = GUILayout.TextField(scenarioObjectiveDisplayName);
            GUILayout.Label("Active HUD text");
            scenarioObjectiveActiveText = GUILayout.TextField(scenarioObjectiveActiveText);
            GUILayout.Label("Completed HUD text");
            scenarioObjectiveCompletedText = GUILayout.TextField(scenarioObjectiveCompletedText);
            DrawLabeledField("AP cost", ref scenarioObjectiveCostText);
            DrawLabeledField(
                "Move cost",
                ref scenarioObjectiveMovementCostText);
            GUILayout.Label("Mobility");
            int mobilityIndex = Array.IndexOf(
                ObjectiveMobilityValues,
                scenarioObjectiveMobility);
            mobilityIndex = GUILayout.Toolbar(
                Mathf.Max(0, mobilityIndex),
                ObjectiveMobilityLabels);
            scenarioObjectiveMobility = ObjectiveMobilityValues[mobilityIndex];
            GUI.enabled = true;
            if (GUILayout.Button("APPLY GOAL", PanelButtonLayout()))
            {
                actions.ApplyScenarioObjective(
                    entity.id,
                    point.id,
                    scenarioObjectiveEnabled,
                    scenarioObjectiveDisplayName,
                    scenarioObjectiveActiveText,
                    scenarioObjectiveCompletedText,
                    scenarioObjectiveCostText,
                    scenarioObjectiveMovementCostText,
                    scenarioObjectiveMobility);
            }
            if (!string.Equals(point.type, "objective", StringComparison.Ordinal))
                GUILayout.Label("Set point type to Objective to enable this link.");
        }


        private void DrawScenarioVehicleInspector(
            LevelEntityView view,
            LevelEntity entity,
            LevelScenarioData scenario)
        {
            if ((view.Archetype.Capabilities & LevelArchetypeCapabilities.Vehicle) == 0)
                return;

            LevelScenarioVehicleData configured = scenario.vehicles
                .FirstOrDefault(vehicle => string.Equals(
                    vehicle?.entityId,
                    entity.id,
                    StringComparison.Ordinal));
            if (!string.Equals(lastScenarioVehicleEntityId, entity.id, StringComparison.Ordinal))
            {
                lastScenarioVehicleEntityId = entity.id;
                scenarioVehicleEnabled = configured != null;
                scenarioVehicleMaximumSpeedText = (configured?.maximumSpeed ?? 12f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleAccelerationText = (configured?.accelerationPerTurn ?? 3f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleBrakingText = (configured?.brakingPerTurn ?? 4f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleLowTurnText = (configured?.lowSpeedTurnDegrees ?? 45f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleHighTurnText = (configured?.highSpeedTurnDegrees ?? 15f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleBaseRadiusText = (configured?.baseTurningRadius ?? 2f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleRadiusFactorText = (configured?.speedTurningRadiusFactor ?? 0.25f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleStartingSpeedText = (configured?.startingSpeed ?? 0f)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                scenarioVehicleOccupantId = configured?.startingOccupantActorId ?? string.Empty;
                scenarioVehicleStartsEncounter = configured?.startsEncounterOnAttack ?? false;
            }

            GUILayout.Space(LevelEditorGuiMetrics.SpaceInspectorSection);
            DrawSectionHeader("SCENARIO VEHICLE");
            scenarioVehicleEnabled = GUILayout.Toggle(
                scenarioVehicleEnabled,
                "Driveable in test play");
            GUI.enabled = scenarioVehicleEnabled;
            DrawLabeledField("Max", ref scenarioVehicleMaximumSpeedText);
            DrawLabeledField("Accel", ref scenarioVehicleAccelerationText);
            DrawLabeledField("Brake", ref scenarioVehicleBrakingText);
            DrawLabeledField("Low turn", ref scenarioVehicleLowTurnText);
            DrawLabeledField("High turn", ref scenarioVehicleHighTurnText);
            DrawLabeledField("Radius", ref scenarioVehicleBaseRadiusText);
            DrawLabeledField("Radius ×", ref scenarioVehicleRadiusFactorText);
            DrawLabeledField("Start", ref scenarioVehicleStartingSpeedText);
            GUILayout.Label("Occupant actor ID (optional)");
            scenarioVehicleOccupantId = GUILayout.TextField(scenarioVehicleOccupantId);
            scenarioVehicleStartsEncounter = GUILayout.Toggle(
                scenarioVehicleStartsEncounter,
                "Attack starts encounter");
            GUI.enabled = true;
            if (GUILayout.Button("APPLY VEHICLE", PanelButtonLayout()))
            {
                actions.ApplyScenarioVehicle(
                    entity.id,
                    scenarioVehicleEnabled,
                    scenarioVehicleMaximumSpeedText,
                    scenarioVehicleAccelerationText,
                    scenarioVehicleBrakingText,
                    scenarioVehicleLowTurnText,
                    scenarioVehicleHighTurnText,
                    scenarioVehicleBaseRadiusText,
                    scenarioVehicleRadiusFactorText,
                    scenarioVehicleStartingSpeedText,
                    scenarioVehicleOccupantId,
                    scenarioVehicleStartsEncounter);
            }
        }


        private void SyncInteractionFields(InteractionPointData point)
        {
            if (string.Equals(lastInteractionSelectionId, point.id, StringComparison.Ordinal))
            {
                return;
            }

            lastInteractionSelectionId = point.id;
            interactionType = point.type;
            interactionXText = point.localPosition.x.ToString("0.###", CultureInfo.InvariantCulture);
            interactionYText = point.localPosition.y.ToString("0.###", CultureInfo.InvariantCulture);
            interactionZText = point.localPosition.z.ToString("0.###", CultureInfo.InvariantCulture);
            interactionRadiusText = point.radius.ToString("0.###", CultureInfo.InvariantCulture);
        }


        private static int DestructibleStateIndex(string value)
        {
            if (string.Equals(value, "damaged", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return string.Equals(value, "destroyed", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        }


        private static int ScenarioSizeIndex(string value)
        {
            if (string.Equals(value, "small", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(value, "large", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (string.Equals(value, "huge", StringComparison.OrdinalIgnoreCase))
                return 3;
            return 1;
        }


        private static void DrawLabeledField(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LevelEditorGuiMetrics.FieldLabelWidth));
            value = GUILayout.TextField(value);
            GUILayout.EndHorizontal();
        }


    }
}
