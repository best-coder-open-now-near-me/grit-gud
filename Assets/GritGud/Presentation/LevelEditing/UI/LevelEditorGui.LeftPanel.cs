using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Tools;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed partial class LevelEditorGui
    {
        private void DrawPalette(LevelDocument document)
        {
            GUILayout.BeginArea(
                new Rect(
                    0f,
                    LevelEditorGuiMetrics.ToolbarHeight,
                    LevelEditorGuiMetrics.LeftPanelWidth,
                    Screen.height
                    - LevelEditorGuiMetrics.ToolbarHeight
                    - LevelEditorGuiMetrics.StatusBarHeight),
                styles.Panel);
            paletteScroll = GUILayout.BeginScrollView(paletteScroll);
            GUILayout.BeginHorizontal();
            Color panelToggleColor = GUI.backgroundColor;
            DrawLeftPanelTab("CREATE", LevelEditorWorkspacePage.Create, panelToggleColor);
            DrawLeftPanelTab("OUTLINE", LevelEditorWorkspacePage.Outline, panelToggleColor);
            DrawLeftPanelTab("SCENARIO", LevelEditorWorkspacePage.Scenario, panelToggleColor);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawLeftPanelTab("ENV", LevelEditorWorkspacePage.Environment, panelToggleColor);
            DrawLeftPanelTab("DRESSING", LevelEditorWorkspacePage.Dressing, panelToggleColor);
            GUI.backgroundColor = panelToggleColor;
            GUILayout.EndHorizontal();

            if (presentationState.Page == LevelEditorWorkspacePage.Outline)
            {
                DrawHierarchy(document);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            if (presentationState.Page == LevelEditorWorkspacePage.Scenario)
            {
                DrawScenario(document);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            if (presentationState.Page == LevelEditorWorkspacePage.Environment)
            {
                DrawEnvironment(document);
                GUILayout.EndScrollView();
                DrawEnvironmentApplyFooter();
                GUILayout.EndArea();
                return;
            }

            if (presentationState.Page == LevelEditorWorkspacePage.Dressing)
            {
                DrawDressing(document);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            DrawCreateModeTabs();
            switch (presentationState.CreateMode)
            {
                case LevelEditorCreateMode.Select:
                    DrawSelectCreatePanel(document);
                    break;
                case LevelEditorCreateMode.Terrain:
                    DrawTerrainCreatePanel(document);
                    break;
                default:
                    DrawPlacementCreatePanel();
                    break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }


        private void DrawCreateModeTabs()
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("CREATE MODE");
            GUILayout.BeginHorizontal();
            DrawCreateModeTab("SELECT", LevelEditorCreateMode.Select);
            DrawCreateModeTab("PLACE", LevelEditorCreateMode.Place);
            DrawCreateModeTab("TERRAIN", LevelEditorCreateMode.Terrain);
            GUILayout.EndHorizontal();
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
        }


        private void DrawCreateModeTab(string label, LevelEditorCreateMode mode)
        {
            Color previous = GUI.backgroundColor;
            if (presentationState.CreateMode == mode)
                GUI.backgroundColor = LevelEditorTheme.Active;
            if (GUILayout.Button(label, PanelButtonLayout()))
            {
                presentationState.ShowCreateMode(mode);
                switch (mode)
                {
                    case LevelEditorCreateMode.Select:
                        toolManager.Activate(SelectionLevelEditorTool.ToolId);
                        break;
                    case LevelEditorCreateMode.Place:
                        if (placementTool.Archetype != null)
                            toolManager.Activate(PlacementLevelEditorTool.ToolId);
                        break;
                    case LevelEditorCreateMode.Terrain:
                        terrainPanel.Activate();
                        break;
                }
            }
            GUI.backgroundColor = previous;
        }


        private void DrawSelectCreatePanel(LevelDocument document)
        {
            DrawSectionHeader("SELECT AND ARRANGE");
            GUILayout.Label("Click an object to select it. Ctrl-click adds or removes it.");
            GUILayout.Label("Drag selected objects in the world; use the Inspector for exact values.");
            GUI.enabled = selection.Primary != null;
            if (GUILayout.Button("FRAME SELECTION", PanelButtonLayout()))
                actions.FrameSelection();
            GUI.enabled = selection.Targets.Count > 0;
            if (GUILayout.Button("DUPLICATE SELECTION", PanelButtonLayout()))
                selectionTool.DuplicateSelection();
            GUI.enabled = true;
            DrawLevelLayoutPanel(document);
        }


        private void DrawTerrainCreatePanel(LevelDocument document)
        {
            terrainPanel.Synchronize(document);
            DrawSectionHeader("TERRAIN SURFACE");
            if (!terrainPanel.HasTerrain)
            {
                GUILayout.Label("This level has no terrain surface.");
                if (GUILayout.Button("ADD FLAT TERRAIN", PanelPrimaryButtonLayout()))
                    terrainPanel.CreateFlatTerrain();
                GUILayout.Label("The new surface covers the current level bounds.");
                return;
            }

            if (terrainPanel.SurfaceIds.Count > 1)
            {
                GUILayout.Label("Surface");
                terrainPanel.SelectedSurfaceIndex = GUILayout.SelectionGrid(
                    terrainPanel.SelectedSurfaceIndex,
                    terrainPanel.SurfaceIds.ToArray(),
                    1);
            }
            else
            {
                GUILayout.Label($"Surface: {terrainPanel.SelectedSurfaceId}");
            }

            string widthText = terrainPanel.WidthText;
            string depthText = terrainPanel.DepthText;
            string spacingText = terrainPanel.SampleSpacingText;
            DrawLabeledField("Width (m)", ref widthText);
            DrawLabeledField("Depth (m)", ref depthText);
            DrawLabeledField("Grid (m)", ref spacingText);
            terrainPanel.WidthText = widthText;
            terrainPanel.DepthText = depthText;
            terrainPanel.SampleSpacingText = spacingText;
            if (GUILayout.Button("RESIZE TERRAIN", PanelApplyButtonLayout()))
                terrainPanel.ResizeTerrain();
            GUILayout.Label("Width and depth must be whole multiples of the grid size.");
            if (GUILayout.Button("FRAME TERRAIN", PanelButtonLayout()))
                terrainPanel.FrameTerrain();

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("SURFACE-WIDE APPEARANCE");
            GUILayout.Label("Applies to the entire selected terrain surface. This is not a paint brush.");
            string[] appearancePresetIds = new[] { string.Empty }
                .Concat(terrainPanel.AppearancePresetIds)
                .ToArray();
            string[] appearancePresets = new[] { "CUSTOM" }
                .Concat(terrainPanel.AppearancePresetIds.Select(value => value.ToUpperInvariant()))
                .ToArray();
            int appearanceIndex = Array.FindIndex(
                appearancePresetIds,
                value => string.Equals(
                    value,
                    terrainPanel.PendingAppearancePresetId,
                    StringComparison.OrdinalIgnoreCase));
            int selectedAppearance = GUILayout.SelectionGrid(
                Mathf.Max(0, appearanceIndex),
                appearancePresets,
                2);
            if (selectedAppearance > 0)
            {
                terrainPanel.PendingAppearancePresetId =
                    appearancePresetIds[selectedAppearance];
            }
            GUI.enabled = !string.IsNullOrWhiteSpace(
                terrainPanel.PendingAppearancePresetId);
            if (GUILayout.Button("APPLY PRESET TO ENTIRE SURFACE", PanelApplyButtonLayout()))
                terrainPanel.ApplyAppearancePreset();
            GUI.enabled = true;
            DrawColorFields("Base RGB (0-1)", terrainPanel.BaseColor);
            DrawColorFields("Steep RGB (0-1)", terrainPanel.SteepColor);
            string slopeStart = terrainPanel.SlopeBlendStartText;
            string slopeEnd = terrainPanel.SlopeBlendEndText;
            string smoothness = terrainPanel.SmoothnessText;
            string specular = terrainPanel.SpecularStrengthText;
            DrawLabeledField("Slope start", ref slopeStart);
            DrawLabeledField("Slope end", ref slopeEnd);
            DrawLabeledField("Smoothness", ref smoothness);
            DrawLabeledField("Specular", ref specular);
            terrainPanel.SlopeBlendStartText = slopeStart;
            terrainPanel.SlopeBlendEndText = slopeEnd;
            terrainPanel.SmoothnessText = smoothness;
            terrainPanel.SpecularStrengthText = specular;
            if (GUILayout.Button("APPLY CUSTOM APPEARANCE", PanelApplyButtonLayout()))
                terrainPanel.ApplyAppearance();

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("MATERIAL PAINTING");
            GUILayout.Label("Choose a material, then drag on the map. Existing painted regions stay unchanged.");
            string[] materialNames = TerrainToolPanelModel.PaintMaterials
                .Select(option => option.Label)
                .ToArray();
            int selectedMaterial = GUILayout.SelectionGrid(
                terrainPanel.PaintMaterialIndex,
                materialNames,
                2);
            if (selectedMaterial != terrainPanel.PaintMaterialIndex)
                terrainPanel.ActivatePaint(selectedMaterial);
            if (GUILayout.Button("ACTIVATE MATERIAL BRUSH", PanelPrimaryButtonLayout()))
                terrainPanel.ActivatePaint(selectedMaterial);
            GUILayout.Label($"Paint radius: {terrainPanel.RadiusInSamples} samples");
            terrainPanel.RadiusInSamples = Mathf.RoundToInt(GUILayout.HorizontalSlider(
                terrainPanel.RadiusInSamples,
                1f,
                16f));
            GUILayout.Label("SURFACE erases regional paint and reveals the surface-wide appearance.");

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("HEIGHT SCULPTING");
            GUILayout.Label("The brush below changes elevation only.");
            Color previous = GUI.backgroundColor;
            GUILayout.BeginHorizontal();
            if (terrainPanel.IsRaiseActive)
                GUI.backgroundColor = LevelEditorTheme.Positive;
            if (GUILayout.Button("RAISE", PanelButtonLayout()))
                terrainPanel.ActivateRaise();
            GUI.backgroundColor = previous;

            if (terrainPanel.IsLowerActive)
                GUI.backgroundColor = LevelEditorTheme.Warning;
            if (GUILayout.Button("LOWER", PanelButtonLayout()))
                terrainPanel.ActivateLower();
            GUI.backgroundColor = previous;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (terrainPanel.IsSmoothActive)
                GUI.backgroundColor = LevelEditorTheme.SmoothTerrain;
            if (GUILayout.Button("SMOOTH", PanelButtonLayout()))
                terrainPanel.ActivateSmooth();
            GUI.backgroundColor = previous;

            if (terrainPanel.IsFlattenActive)
                GUI.backgroundColor = LevelEditorTheme.FlattenTerrain;
            if (GUILayout.Button("FLATTEN", PanelButtonLayout()))
                terrainPanel.ActivateFlatten();
            GUI.backgroundColor = previous;
            GUILayout.EndHorizontal();
            GUILayout.Label($"Radius: {terrainPanel.RadiusInSamples} samples");
            terrainPanel.RadiusInSamples = Mathf.RoundToInt(GUILayout.HorizontalSlider(
                terrainPanel.RadiusInSamples,
                1f,
                16f));
            GUILayout.Label($"Strength: {terrainPanel.QuantizedStrength} steps");
            terrainPanel.QuantizedStrength = Mathf.RoundToInt(GUILayout.HorizontalSlider(
                terrainPanel.QuantizedStrength,
                1f,
                20f));
            GUILayout.Label(
                "Drag to apply one undoable stroke. Flatten captures its target height on press.");
        }


        private void DrawPlacementCreatePanel()
        {
            DrawSectionHeader("PLACEMENT LIBRARY");
            GUILayout.Label("Choose a piece, then click in the world.");
            if (placementTool.Archetype != null)
            {
                GUILayout.Space(LevelEditorGuiMetrics.SpaceSmall);
                DrawSectionHeader("ACTIVE STAMP");
                GUILayout.Label(
                    $"{placementTool.Archetype.DisplayName} · {placementTool.YawDegrees:0.#}°");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("↺", PanelIconButtonLayout()))
                    placementTool.RotatePreview(-1f);
                if (GUILayout.Button("↻", PanelIconButtonLayout()))
                    placementTool.RotatePreview();
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(LevelEditorGuiMetrics.SearchLabelWidth));
            paletteSearch = GUILayout.TextField(paletteSearch ?? string.Empty);
            GUILayout.EndHorizontal();

            Color previous = GUI.backgroundColor;

            IReadOnlyList<string> categories = catalog.Entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Category))
                .Select(entry => entry.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] categoryOptions = new[] { "ALL" }.Concat(categories).ToArray();
            int categoryIndex = string.IsNullOrWhiteSpace(paletteCategory)
                ? 0
                : Array.FindIndex(categoryOptions, option => string.Equals(
                    option,
                    paletteCategory,
                    StringComparison.OrdinalIgnoreCase));
            int selectedCategory = GUILayout.SelectionGrid(
                Mathf.Max(0, categoryIndex),
                categoryOptions,
                2);
            paletteCategory = selectedCategory <= 0
                ? string.Empty
                : categoryOptions[selectedCategory];

            IReadOnlyList<LevelArchetypeDefinition> filteredEntries = catalog.Entries
                .Where(MatchesPaletteFilter)
                .ToArray();
            if (filteredEntries.Count == 0)
            {
                GUILayout.Label("No archetypes match this filter.");
            }

            bool searchIsActive = !string.IsNullOrWhiteSpace(paletteSearch);
            foreach (IGrouping<string, LevelArchetypeDefinition> group in filteredEntries
                .GroupBy(entry => string.IsNullOrWhiteSpace(entry.Category)
                    ? "Uncategorized"
                    : entry.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                string category = group.Key;
                bool isCollapsed = collapsedPaletteCategories.Contains(category);
                string header = $"{(isCollapsed ? "▶" : "▼")} {category.ToUpperInvariant()} ({group.Count()})";
                GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
                if (GUILayout.Button(header, GUI.skin.box, PanelCompactButtonLayout()))
                {
                    if (isCollapsed)
                        collapsedPaletteCategories.Remove(category);
                    else
                        collapsedPaletteCategories.Add(category);
                }

                if (isCollapsed && !searchIsActive)
                    continue;

                foreach (LevelArchetypeDefinition entry in group.OrderBy(
                    entry => entry.DisplayName,
                    StringComparer.OrdinalIgnoreCase))
                {
                    bool active = toolManager.ActiveTool == placementTool
                        && ReferenceEquals(placementTool.Archetype, entry);
                    previous = GUI.backgroundColor;
                    if (active)
                    {
                        GUI.backgroundColor = LevelEditorTheme.Placement;
                    }

                    if (GUILayout.Button(entry.DisplayName, PanelPrimaryButtonLayout()))
                    {
                        placementTool.SelectArchetype(entry);
                        toolManager.Activate(PlacementLevelEditorTool.ToolId);
                    }

                    GUI.backgroundColor = previous;
                }
            }
        }


        private void DrawLeftPanelTab(
            string label,
            LevelEditorWorkspacePage page,
            Color previous)
        {
            if (presentationState.Page == page)
                GUI.backgroundColor = LevelEditorTheme.Active;
            if (GUILayout.Button(label, PanelButtonLayout()))
                presentationState.ShowPage(page);
            GUI.backgroundColor = previous;
        }


        private void DrawScenario(LevelDocument document)
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("LEVEL DETAILS");
            GUILayout.Label("Display name");
            levelDisplayNameText = GUILayout.TextField(levelDisplayNameText ?? string.Empty);
            if (GUILayout.Button("APPLY NAME", PanelApplyButtonLayout()))
                actions.ApplyLevelDisplayName(levelDisplayNameText);
            GUILayout.Label($"Stable ID: {document.levelId}");
            GUILayout.Label("The name controls menus and export filenames; the ID stays stable.");

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("SCENARIO COMPOSITION");
            GUILayout.Label(
                "Actors and gameplay links here are the exact data used by Test Play.");
            GUILayout.Space(LevelEditorGuiMetrics.SpaceGroup);
            DrawPlayerStartInspector();
            DrawSectionHeader($"ACTORS ({document.scenario.actors.Count})");
            foreach (LevelScenarioActorData actor in document.scenario.actors
                .Where(actor => actor != null)
                .OrderByDescending(actor => actor.playerControlled)
                .ThenBy(actor => actor.id, StringComparer.Ordinal))
            {
                Color previous = GUI.backgroundColor;
                if (string.Equals(
                        SelectedScenarioActorId,
                        actor.id,
                        StringComparison.Ordinal))
                {
                    GUI.backgroundColor = LevelEditorTheme.Active;
                }

                string role = actor.playerControlled
                    ? actor.initiallySelected ? "PLAYER • SELECTED" : "PLAYER"
                    : actor.primaryTarget ? "TARGET" : "ENEMY";
                if (GUILayout.Button(
                        $"{ScenarioActorDisplayName(actor)}\n{role}",
                        GUILayout.Height(LevelEditorGuiMetrics.PanelActorButtonHeight)))
                {
                    selection.Clear();
                    SyncScenarioActorFields(actor);
                }
                GUI.backgroundColor = previous;
            }

            LevelScenarioActorData selected = document.scenario.actors.FirstOrDefault(actor =>
                string.Equals(actor?.id, SelectedScenarioActorId, StringComparison.Ordinal));
            if (selected != null)
            {
                GUILayout.Label("Selected actor details are shown in the Inspector.");
            }

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            if (GUILayout.Button(
                    showActorTemplates ? "HIDE ACTOR LIBRARY" : "+ ADD ACTOR",
                    PanelButtonLayout()))
            {
                showActorTemplates = !showActorTemplates;
            }

            if (showActorTemplates)
            {
                string previousGroup = null;
                foreach (ScenarioActorTemplateDefinition template
                    in scenarioCatalog.ActorTemplates)
                {
                    string group = template.PlayerTemplate ? "PLAYER PARTY" : "OPPONENTS";
                    if (!string.Equals(previousGroup, group, StringComparison.Ordinal))
                    {
                        previousGroup = group;
                        DrawSectionHeader(group);
                    }

                    if (GUILayout.Button($"+ {template.DisplayName}", PanelButtonLayout()))
                        actions.AddScenarioActor(template.TemplateId);
                }
            }

            DrawScenarioLinkSummary(document.scenario);
        }


        private string ScenarioActorDisplayName(LevelScenarioActorData actor)
        {
            if (scenarioCatalog.TryGetActor(
                    actor?.templateId,
                    out ScenarioActorTemplateDefinition template))
            {
                return template.DisplayName;
            }

            string templateId = string.IsNullOrWhiteSpace(actor?.templateId)
                ? "missing template"
                : actor.templateId;
            return $"{templateId} (unavailable)";
        }


        private void DrawScenarioLinkSummary(LevelScenarioData scenario)
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceInspectorSection);
            DrawSectionHeader("GAMEPLAY LINKS");
            GUILayout.Label($"Objectives: {scenario.objectives.Count}");
            GUILayout.Label($"Physics props: {scenario.props.Count}");
            GUILayout.Label($"Vehicles: {scenario.vehicles.Count}");
        }


        private void DrawHierarchy(LevelDocument document)
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("OUTLINE");
            GUILayout.Label("Search all authored world and scenario objects.");
            hierarchySearch = GUILayout.TextField(hierarchySearch ?? string.Empty);

            DrawPlayability();
            DrawOrganization(document);

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("SCENARIO");
            LevelScenarioActorData selectedPlayer = document.scenario
                .FindInitiallySelectedPlayer();
            if (selectedPlayer != null)
            {
                LevelTransformData start = selectedPlayer.transform;
                GUILayout.Label(
                    $"PLAYER START  ({start.position.x:0.##}, {start.position.y:0.##}, {start.position.z:0.##})");
            }
            else
            {
                GUILayout.Label("PLAYER START  (NOT CONFIGURED)");
            }
            DrawSectionHeader($"ACTORS ({document.scenario.actors.Count})");
            foreach (LevelScenarioActorData actor in document.scenario.actors
                .Where(actor => actor != null && MatchesOutlineSearch(
                    actor.id,
                    actor.templateId,
                    actor.playerControlled ? "player" : "enemy")))
            {
                Color previous = GUI.backgroundColor;
                if (presentationState.InspectorTarget.Kind
                    == LevelEditorInspectorTargetKind.ScenarioActor
                    && string.Equals(
                        presentationState.InspectorTarget.TargetId,
                        actor.id,
                        StringComparison.Ordinal))
                {
                    GUI.backgroundColor = LevelEditorTheme.Active;
                }

                if (GUILayout.Button(
                        ScenarioActorDisplayName(actor),
                        PanelCompactButtonLayout()))
                {
                    selection.Clear();
                    SyncScenarioActorFields(actor);
                }
                GUI.backgroundColor = previous;
            }

            DrawScenarioEntityLinks("OBJECTIVES", document.scenario.objectives
                .Select(objective => objective?.entityId), document);
            DrawScenarioEntityLinks("PHYSICS PROPS", document.scenario.props
                .Select(prop => prop?.entityId), document);
            DrawScenarioEntityLinks("VEHICLES", document.scenario.vehicles
                .Select(vehicle => vehicle?.entityId), document);
            if (GUILayout.Button("OPEN SCENARIO", PanelIconButtonLayout()))
                presentationState.ShowPage(LevelEditorWorkspacePage.Scenario);

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader($"LEVEL GEOMETRY ({document.entities.Count})");
            GUILayout.Label($"Terrain surfaces: {document.terrainSurfaces.Count}");
            string previousCategory = null;
            int matches = 0;
            foreach (LevelEntity entity in document.entities
                .Where(MatchesHierarchyFilter)
                .OrderBy(EntityCategory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(EntityDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entity => entity.id, StringComparer.Ordinal))
            {
                string category = EntityCategory(entity);
                if (!string.Equals(previousCategory, category, StringComparison.Ordinal))
                {
                    previousCategory = category;
                    DrawSectionHeader(category.ToUpperInvariant());
                }

                matches++;
                Color previous = GUI.backgroundColor;
                if (string.Equals(selection.PrimaryEntityId, entity.id, StringComparison.Ordinal))
                {
                    GUI.backgroundColor = LevelEditorTheme.Active;
                }

                if (GUILayout.Button(EntityDisplayName(entity), PanelCompactButtonLayout()))
                {
                    actions.FocusEntity(entity.id);
                }

                GUI.backgroundColor = previous;
            }

            if (matches == 0)
            {
                GUILayout.Label("No entities match this filter.");
            }
        }


        private void DrawScenarioEntityLinks(
            string label,
            IEnumerable<string> entityIds,
            LevelDocument document)
        {
            string[] ids = entityIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            DrawSectionHeader($"{label} ({ids.Length})");
            foreach (string entityId in ids)
            {
                LevelEntity entity = document.entities.FirstOrDefault(candidate =>
                    string.Equals(candidate?.id, entityId, StringComparison.Ordinal));
                string displayName = entity == null ? entityId : EntityDisplayName(entity);
                if (!MatchesOutlineSearch(displayName, entityId, label))
                    continue;
                if (GUILayout.Button(displayName, PanelCompactButtonLayout()))
                    actions.FocusEntity(entityId);
            }
        }


        private bool MatchesOutlineSearch(params string[] values)
        {
            string search = hierarchySearch?.Trim();
            return string.IsNullOrEmpty(search)
                || values.Any(value => value != null
                    && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }


        private bool MatchesHierarchyFilter(LevelEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            return MatchesOutlineSearch(
                EntityDisplayName(entity),
                entity.id,
                EntityCategory(entity));
        }


        private string EntityDisplayName(LevelEntity entity)
        {
            return catalog.TryGet(entity.archetypeId, out LevelArchetypeDefinition archetype)
                ? archetype.DisplayName
                : entity.archetypeId;
        }


        private string EntityCategory(LevelEntity entity)
        {
            return catalog.TryGet(entity.archetypeId, out LevelArchetypeDefinition archetype)
                && !string.IsNullOrWhiteSpace(archetype.Category)
                ? archetype.Category
                : "Unknown";
        }



        private bool MatchesPaletteFilter(LevelArchetypeDefinition entry)
        {
            if (entry == null || !string.Equals(
                    paletteCategory,
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Category, paletteCategory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string search = paletteSearch?.Trim();
            return string.IsNullOrEmpty(search)
                || entry.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.ArchetypeId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }


    }
}
