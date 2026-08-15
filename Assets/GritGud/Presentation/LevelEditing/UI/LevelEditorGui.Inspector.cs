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
            float left = Screen.width - LevelEditorGuiMetrics.InspectorWidth;
            GUILayout.BeginArea(
                new Rect(
                    left,
                    LevelEditorGuiMetrics.ToolbarHeight,
                    LevelEditorGuiMetrics.InspectorWidth,
                    Screen.height
                    - LevelEditorGuiMetrics.ToolbarHeight
                    - LevelEditorGuiMetrics.StatusBarHeight),
                styles.Panel);
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);
            DrawSectionHeader("INSPECTOR");
            DrawInspectorTabs();
            if (presentationState.InspectorPage == LevelEditorInspectorPage.Level)
            {
                DrawLevelInspector(state);
            }
            else if (presentationState.InspectorPage
                == LevelEditorInspectorPage.Gameplay)
            {
                DrawGameplayInspector(state, selectedView);
            }
            else
            {
                DrawSelectionInspector(state, selectedView);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawInspectorTabs()
        {
            int selected = GUILayout.Toolbar(
                (int)presentationState.InspectorPage,
                new[] { "SELECTION", "GAMEPLAY", "LEVEL" });
            presentationState.ShowInspectorPage((LevelEditorInspectorPage)selected);
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
        }

        private void DrawSelectionInspector(
            LevelEditorViewState state,
            LevelEntityView selectedView)
        {
            if (selectedView == null)
            {
                GUILayout.Label("Select a world object or interaction point to edit it.");
                return;
            }

            GUILayout.Label(selectedView.Archetype.DisplayName);
            GUILayout.Label($"ID: {selection.PrimaryEntityId}");
            LevelEntity entity = FindSelectedEntity(state);
            LevelSelectionTarget? primary = selection.Primary;
            if (selection.Targets.Count > 1)
                GUILayout.Label($"{selection.Targets.Count} entities selected");
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawLabeledField("X", ref xText);
            DrawLabeledField("Y", ref yText);
            DrawLabeledField("Z", ref zText);
            DrawLabeledField("Pitch X", ref pitchText);
            DrawLabeledField("Yaw Y", ref yawText);
            DrawLabeledField("Roll Z", ref rollText);
            if (GUILayout.Button("APPLY", PanelPrimaryButtonLayout()))
                actions.ApplyEntityTransform(
                    xText,
                    yText,
                    zText,
                    pitchText,
                    yawText,
                    rollText);

            float angleSnap = selectedView.Archetype.PlacementRules.AngleSnap;
            DrawAxisRotationButtons("PITCH X", Vector3.right, angleSnap);
            DrawAxisRotationButtons("YAW Y", Vector3.up, angleSnap);
            DrawAxisRotationButtons("ROLL Z", Vector3.forward, angleSnap);
            DrawRotationPivotPicker(entity, selectedView);

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            GUILayout.Label("PHYSICS PLACEMENT");
            DrawLabeledField("Drop height", ref physicsDropHeightText);
            physicsKeepUpright = GUILayout.Toggle(physicsKeepUpright, "Keep upright");
            GUILayout.Label(
                selection.Targets.Count > 1
                    ? $"Settles {selection.Targets.Count} selected records together as one undo step."
                    : "Temporarily simulates the selected prop, then saves its settled transform.");
            if (actions.PhysicsPlacementRunning)
            {
                if (GUILayout.Button("CANCEL SETTLE", PanelPrimaryButtonLayout()))
                    actions.CancelPhysicsPlacement();
            }
            else if (GUILayout.Button("DROP & SETTLE", PanelPrimaryButtonLayout()))
            {
                actions.DropAndSettleSelection(physicsDropHeightText, physicsKeepUpright);
            }

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = LevelEditorTheme.Destructive;
            if (GUILayout.Button("DELETE", PanelPrimaryButtonLayout()))
                selectionTool.DeleteSelection();
            GUI.backgroundColor = previous;

            GUILayout.Space(LevelEditorGuiMetrics.SpaceInspectorSection);
            DrawInteractionInspector(entity, primary);
            DrawDestructibleInspector(selectedView, entity);
        }

        private void DrawRotationPivotPicker(LevelEntity entity, LevelEntityView selectedView)
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            GUILayout.Label("ROTATION PIVOT — TOP VIEW (X/Z)");
            GUILayout.Label("Choose the point that stays fixed while yaw rotates.");

            Bounds bounds = LevelEntityView.CalculateVisualLocalBounds(
                selectedView.Archetype.Presentation.Prefab,
                selectedView.Archetype.Presentation.LocalBounds);
            for (int zIndex = 1; zIndex >= -1; zIndex--)
            {
                GUILayout.BeginHorizontal();
                for (int xIndex = -1; xIndex <= 1; xIndex++)
                {
                    float normalizedX = xIndex;
                    float normalizedZ = zIndex;
                    Vector3 candidate = LevelEntityView.CalculateBoundsPivot(
                        bounds,
                        normalizedX,
                        normalizedZ);
                    bool selected = entity?.rotationPivot != null
                        && Approximately(entity.rotationPivot.localPosition, candidate);
                    Color previous = GUI.backgroundColor;
                    if (selected)
                        GUI.backgroundColor = LevelEditorTheme.SelectionOutline;
                    if (GUILayout.Button(selected ? "●" : "○"))
                        actions.SetEntityRotationPivot(normalizedX, normalizedZ);
                    GUI.backgroundColor = previous;
                }
                GUILayout.EndHorizontal();
            }

            bool usesAssetPivot = entity?.rotationPivot == null;
            Color assetPrevious = GUI.backgroundColor;
            if (usesAssetPivot)
                GUI.backgroundColor = LevelEditorTheme.SelectionOutline;
            if (GUILayout.Button(usesAssetPivot ? "● ASSET PIVOT" : "○ ASSET PIVOT"))
                actions.ResetEntityRotationPivot();
            GUI.backgroundColor = assetPrevious;
        }

        private static bool Approximately(Float3Data value, Vector3 candidate)
        {
            return Mathf.Approximately(value.x, candidate.x)
                && Mathf.Approximately(value.y, candidate.y)
                && Mathf.Approximately(value.z, candidate.z);
        }

        private void DrawAxisRotationButtons(string label, Vector3 axis, float angleSnap)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(62f));
            if (GUILayout.Button($"−{angleSnap:0.#}°"))
                RotateInspectorSelection(axis, -angleSnap);
            if (GUILayout.Button($"+{angleSnap:0.#}°"))
                RotateInspectorSelection(axis, angleSnap);
            GUILayout.EndHorizontal();
        }

        private void RotateInspectorSelection(Vector3 axis, float amount)
        {
            // A newly stamped object remains selected while the placement tool stays
            // active. Switch to selection before invoking its command so the inspector
            // controls work for both stamped and ordinarily selected objects.
            toolManager.ActivateDefault();
            selectionTool.RotateSelection(axis, amount);
        }

        private void DrawGameplayInspector(
            LevelEditorViewState state,
            LevelEntityView selectedView)
        {
            if (presentationState.InspectorTarget.Kind
                == LevelEditorInspectorTargetKind.ScenarioActor)
            {
                LevelScenarioActorData actor = state.Document.scenario.actors
                    .FirstOrDefault(candidate => string.Equals(
                        candidate?.id,
                        presentationState.InspectorTarget.TargetId,
                        StringComparison.Ordinal));
                DrawScenarioActorInspector(actor);
                return;
            }

            if (selectedView == null)
            {
                GUILayout.Label("Select a scenario actor or linked world object to edit gameplay settings.");
                return;
            }

            LevelEntity entity = FindSelectedEntity(state);
            LevelSelectionTarget? primary = selection.Primary;
            if (primary != null
                && primary.Value.Kind == LevelSelectionKind.InteractionPoint)
            {
                InteractionPointData point = entity?.interactionPoints
                    .FirstOrDefault(candidate => string.Equals(
                        candidate?.id,
                        primary.Value.ElementId,
                        StringComparison.Ordinal));
                if (point != null)
                    DrawScenarioObjectiveInspector(entity, point, state.Document.scenario);
            }
            DrawScenarioPropInspector(selectedView, entity, state.Document.scenario);
            DrawScenarioVehicleInspector(selectedView, entity, state.Document.scenario);
        }

        private LevelEntity FindSelectedEntity(LevelEditorViewState state) =>
            state.Document.entities.FirstOrDefault(candidate => string.Equals(
                candidate?.id,
                selection.PrimaryEntityId,
                StringComparison.Ordinal));

        private void DrawLevelInspector(LevelEditorViewState state)
        {
            IReadOnlyList<LevelValidationIssue> validationIssues =
                state.ValidationIssues;

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

                GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
                DrawSectionHeader("RECOVERY AUTOSAVES");
                GUILayout.Label(
                    "Unsaved work is retained in three rolling local snapshots after 15 seconds of inactivity.");
                for (int generation = 0;
                    generation < actions.RecoveryGenerationCount;
                    generation++)
                {
                    int capturedGeneration = generation;
                    GUI.enabled = actions.HasRecovery(capturedGeneration);
                    string age = capturedGeneration == 0
                        ? "NEWEST"
                        : $"OLDER {capturedGeneration}";
                    if (GUILayout.Button(
                            $"LOAD RECOVERY {capturedGeneration + 1} — {age}",
                            PanelButtonLayout()))
                    {
                        documentActionConfirmation.Request(
                            state.IsDirty,
                            "Load this recovery snapshot and discard the current unsaved changes?",
                            () => actions.LoadRecovery(capturedGeneration));
                    }
                }
                GUI.enabled = true;
            }

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
            LevelSelectionTarget? primary)
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
