using System;
using System.Linq;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed partial class LevelEditorGui
    {
        private string newGroupName = "New Group";
        private string selectedGroupId = string.Empty;
        private string selectedGroupName = string.Empty;

        public void SyncOrganizationFields(LevelDocument document, bool force = false)
        {
            if (document == null)
                return;
            LevelEntityGroupData selected = document.groups.FirstOrDefault(group => string.Equals(
                group?.id,
                selectedGroupId,
                StringComparison.Ordinal));
            if (selected == null)
            {
                selected = document.groups.FirstOrDefault(group => group != null);
                selectedGroupId = selected?.id ?? string.Empty;
                force = true;
            }
            if (force && selected != null)
                selectedGroupName = selected.displayName;
        }

        public void SelectEntityGroup(string groupId, LevelDocument document)
        {
            selectedGroupId = groupId ?? string.Empty;
            SyncOrganizationFields(document, force: true);
            presentationState.ShowPage(LevelEditorWorkspacePage.Outline);
        }

        private void DrawOrganization(LevelDocument document)
        {
            SyncOrganizationFields(document);
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader($"GROUPS ({document.groups.Count})");
            foreach (LevelEntityGroupData group in document.groups.Where(group => group != null))
            {
                Color previous = GUI.backgroundColor;
                if (string.Equals(group.id, selectedGroupId, StringComparison.Ordinal))
                    GUI.backgroundColor = LevelEditorTheme.Active;
                string flags = (group.locked ? "  LOCKED" : string.Empty)
                    + (group.hidden ? "  HIDDEN" : string.Empty);
                int memberCount = document.entities.Count(entity => string.Equals(
                    entity?.groupId,
                    group.id,
                    StringComparison.Ordinal));
                if (GUILayout.Button(
                        $"{group.displayName} ({memberCount}){flags}",
                        PanelCompactButtonLayout()))
                {
                    selectedGroupId = group.id;
                    selectedGroupName = group.displayName;
                }
                GUI.backgroundColor = previous;
            }

            DrawLabeledField("New", ref newGroupName);
            GUI.enabled = document.groups.Count < LevelDocument.MaximumEntityGroupCount;
            if (GUILayout.Button("+ CREATE GROUP", PanelButtonLayout()))
                actions.CreateEntityGroup(newGroupName);
            GUI.enabled = true;

            LevelEntityGroupData selected = document.groups.FirstOrDefault(group => string.Equals(
                group?.id,
                selectedGroupId,
                StringComparison.Ordinal));
            if (selected != null)
            {
                DrawLabeledField("Name", ref selectedGroupName);
                if (GUILayout.Button("RENAME GROUP", PanelApplyButtonLayout()))
                    actions.RenameEntityGroup(selected.id, selectedGroupName);
                bool locked = GUILayout.Toggle(selected.locked, "Locked (cannot select or edit)");
                if (locked != selected.locked)
                    actions.SetEntityGroupLocked(selected.id, locked);
                bool hidden = GUILayout.Toggle(selected.hidden, "Hidden in editor");
                if (hidden != selected.hidden)
                    actions.SetEntityGroupHidden(selected.id, hidden);
                if (GUILayout.Button("ASSIGN SELECTION", PanelButtonLayout()))
                    actions.AssignSelectionToGroup(selected.id);
                if (string.Equals(
                        actions.IsolatedGroupId,
                        selected.id,
                        StringComparison.Ordinal))
                {
                    if (GUILayout.Button("SHOW ALL GROUPS", PanelButtonLayout()))
                        actions.IsolateEntityGroup(string.Empty);
                }
                else if (GUILayout.Button("ISOLATE GROUP", PanelButtonLayout()))
                {
                    actions.IsolateEntityGroup(selected.id);
                }
                Color deleteColor = GUI.backgroundColor;
                GUI.backgroundColor = LevelEditorTheme.Warning;
                if (GUILayout.Button("DELETE GROUP", PanelButtonLayout()))
                    actions.DeleteEntityGroup(selected.id);
                GUI.backgroundColor = deleteColor;
            }

            GUI.enabled = selection.Targets.Any(target =>
                target.Kind == GritGud.Application.Levels.LevelSelectionKind.Entity);
            if (GUILayout.Button("UNGROUP SELECTION", PanelButtonLayout()))
                actions.AssignSelectionToGroup(string.Empty);
            GUI.enabled = true;

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("SELECTION FILTERS");
            string[] categories = new[] { "ALL" }.Concat(catalog.Entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Category))
                .Select(entry => entry.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            int categoryIndex = string.IsNullOrEmpty(actions.SelectionCategoryFilter)
                ? 0
                : Math.Max(0, Array.FindIndex(categories, category => string.Equals(
                    category,
                    actions.SelectionCategoryFilter,
                    StringComparison.OrdinalIgnoreCase)));
            int selectedCategory = GUILayout.SelectionGrid(categoryIndex, categories, 2);
            if (selectedCategory != categoryIndex)
                actions.SetSelectionCategoryFilter(selectedCategory == 0
                    ? string.Empty
                    : categories[selectedCategory]);

            string[] groupIds = new[]
            {
                string.Empty,
                LevelEditorOrganizationModel.UngroupedFilter,
            }.Concat(document.groups.Where(group => group != null).Select(group => group.id)).ToArray();
            string[] groupLabels = new[] { "ALL GROUPS", "UNGROUPED" }
                .Concat(document.groups.Where(group => group != null).Select(group => group.displayName))
                .ToArray();
            int groupIndex = Math.Max(0, Array.FindIndex(groupIds, id => string.Equals(
                id,
                actions.SelectionGroupFilter,
                StringComparison.Ordinal)));
            int selectedFilterGroup = GUILayout.SelectionGrid(groupIndex, groupLabels, 2);
            if (selectedFilterGroup != groupIndex)
                actions.SetSelectionGroupFilter(groupIds[selectedFilterGroup]);
            if (GUILayout.Button("SELECT MATCHING", PanelPrimaryButtonLayout()))
                actions.SelectMatchingEntities();
        }
    }
}
