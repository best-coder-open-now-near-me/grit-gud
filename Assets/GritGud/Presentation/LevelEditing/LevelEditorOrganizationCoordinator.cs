using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class LevelEditorOrganizationCoordinator
    {
        private readonly LevelEditorWorkspace workspace;
        private readonly LevelSelectionModel selection;
        private readonly LevelEditorOrganizationModel model;

        public LevelEditorOrganizationCoordinator(
            LevelEditorWorkspace workspace,
            LevelSelectionModel selection,
            LevelEditorOrganizationModel model)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public event Action<string> StatusChanged;
        public event Action<string> GroupFocusRequested;

        public void CreateGroup(string displayName)
        {
            string name = displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                Report("A group needs a display name.");
                return;
            }
            LevelDocument snapshot = workspace.CreateSnapshot();
            if (snapshot.groups.Count >= LevelDocument.MaximumEntityGroupCount)
            {
                Report($"A level supports at most {LevelDocument.MaximumEntityGroupCount} groups.");
                return;
            }
            if (snapshot.groups.Any(group => string.Equals(
                    group?.displayName?.Trim(),
                    name,
                    StringComparison.OrdinalIgnoreCase)))
            {
                Report($"A group named '{name}' already exists.");
                return;
            }

            var group = new LevelEntityGroupData
            {
                id = "group-" + LevelDocumentFactory.NewStableId(),
                displayName = name,
            };
            workspace.Execute(new AddLevelGroupCommand(group));
            GroupFocusRequested?.Invoke(group.id);
            Report($"Created group '{name}'.");
        }

        public void RenameGroup(string groupId, string displayName)
        {
            string name = displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                Report("A group needs a display name.");
                return;
            }
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelEntityGroupData before = FindGroup(snapshot, groupId);
            if (before == null)
                return;
            if (snapshot.groups.Any(group => !string.Equals(group?.id, groupId, StringComparison.Ordinal)
                && string.Equals(group?.displayName?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                Report($"A group named '{name}' already exists.");
                return;
            }
            LevelEntityGroupData after = before.DeepCopy();
            after.displayName = name;
            workspace.Execute(new SetLevelGroupCommand(groupId, before, after));
            Report($"Renamed the group to '{name}'.");
        }

        public void SetGroupLocked(string groupId, bool locked)
        {
            EditGroup(groupId, group => group.locked = locked, locked ? "Locked group." : "Unlocked group.");
        }

        public void SetGroupHidden(string groupId, bool hidden)
        {
            EditGroup(groupId, group => group.hidden = hidden, hidden ? "Hid group." : "Showed group.");
        }

        public void AssignSelection(string groupId)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            if (!string.IsNullOrEmpty(groupId) && FindGroup(snapshot, groupId) == null)
            {
                Report("Choose an existing group.");
                return;
            }
            string[] ids = SelectedEntityIds();
            if (ids.Length == 0)
            {
                Report("Select one or more entities before assigning a group.");
                return;
            }
            var commands = new List<ILevelEditCommand>();
            foreach (string entityId in ids)
            {
                LevelEntity entity = snapshot.entities.FirstOrDefault(candidate => string.Equals(
                    candidate?.id,
                    entityId,
                    StringComparison.Ordinal));
                if (entity != null && !string.Equals(entity.groupId, groupId, StringComparison.Ordinal))
                {
                    commands.Add(new SetEntityGroupCommand(entity.id, entity.groupId, groupId));
                }
            }
            Execute("Assign entity group", commands);
            Report(string.IsNullOrEmpty(groupId)
                ? $"Removed {commands.Count} entities from their groups."
                : $"Assigned {commands.Count} entities to the group.");
        }

        public void DeleteGroup(string groupId)
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelEntityGroupData group = FindGroup(snapshot, groupId);
            if (group == null)
                return;
            var commands = snapshot.entities
                .Where(entity => string.Equals(entity?.groupId, groupId, StringComparison.Ordinal))
                .Select(entity => (ILevelEditCommand)new SetEntityGroupCommand(
                    entity.id,
                    groupId,
                    string.Empty))
                .ToList();
            commands.Add(new DeleteLevelGroupCommand(groupId));
            Execute("Delete entity group", commands);
            model.SetIsolation(string.Empty);
            model.SetGroupFilter(string.Empty);
            Report($"Deleted group '{group.displayName}' and kept its entities ungrouped.");
        }

        public void IsolateGroup(string groupId)
        {
            model.SetIsolation(groupId);
            RetainSelectableSelection();
            Report(string.IsNullOrEmpty(groupId) ? "Cleared group isolation." : "Isolated the group.");
        }

        public void SetCategoryFilter(string category)
        {
            model.SetCategoryFilter(category);
            RetainSelectableSelection();
            Report(string.IsNullOrWhiteSpace(category)
                ? "Cleared the category selection filter."
                : $"Selection is filtered to '{category}'.");
        }

        public void SetGroupFilter(string groupId)
        {
            model.SetGroupFilter(groupId);
            RetainSelectableSelection();
            Report(string.IsNullOrEmpty(groupId)
                ? "Cleared the group selection filter."
                : "Updated the group selection filter.");
        }

        public void SelectMatching()
        {
            LevelDocument snapshot = workspace.CreateSnapshot();
            LevelSelectionTarget[] matches = snapshot.entities
                .Where(entity => entity != null && model.CanSelect(entity.id))
                .Select(entity => new LevelSelectionTarget(entity.id))
                .ToArray();
            selection.Set(matches);
            Report(matches.Length == 0
                ? "No selectable entities match the active filters."
                : $"Selected {matches.Length} matching entities.");
        }

        private void EditGroup(
            string groupId,
            Action<LevelEntityGroupData> edit,
            string status)
        {
            LevelEntityGroupData before = FindGroup(workspace.CreateSnapshot(), groupId);
            if (before == null)
                return;
            LevelEntityGroupData after = before.DeepCopy();
            edit(after);
            workspace.Execute(new SetLevelGroupCommand(groupId, before, after));
            Report(status);
        }

        private void RetainSelectableSelection()
        {
            selection.Set(selection.Targets.Where(target => model.CanSelect(target.EntityId)));
        }

        private string[] SelectedEntityIds() => selection.Targets
            .Where(target => target.Kind == LevelSelectionKind.Entity)
            .Select(target => target.EntityId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        private void Execute(string description, IReadOnlyList<ILevelEditCommand> commands)
        {
            if (commands.Count == 0)
                return;
            if (commands.Count == 1)
                workspace.Execute(commands[0]);
            else
                workspace.ExecuteTransaction(description, commands);
        }

        private LevelEntityGroupData FindGroup(LevelDocument document, string groupId)
        {
            LevelEntityGroupData group = document.groups.FirstOrDefault(candidate => string.Equals(
                candidate?.id,
                groupId,
                StringComparison.Ordinal));
            if (group == null)
                Report("Choose an existing entity group.");
            return group;
        }

        private void Report(string message) => StatusChanged?.Invoke(message ?? string.Empty);
    }
}
