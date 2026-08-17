using System;
using System.Linq;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Tools;
using GritGud.Presentation.Levels.Runtime;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class LevelEditorOrganizationModel : ILevelEditorSelectionPolicy
    {
        public const string UngroupedFilter = "__ungrouped";

        private readonly LevelArchetypeCatalog catalog;
        private LevelDocument document;

        public LevelEditorOrganizationModel(LevelArchetypeCatalog catalog)
        {
            this.catalog = catalog != null
                ? catalog
                : throw new ArgumentNullException(nameof(catalog));
        }

        public event Action Changed;

        public string IsolatedGroupId { get; private set; } = string.Empty;

        public string CategoryFilter { get; private set; } = string.Empty;

        public string GroupFilter { get; private set; } = string.Empty;

        public void Synchronize(LevelDocument snapshot)
        {
            document = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            bool changed = false;
            if (!string.IsNullOrEmpty(IsolatedGroupId) && FindGroup(IsolatedGroupId) == null)
            {
                IsolatedGroupId = string.Empty;
                changed = true;
            }
            if (!string.IsNullOrEmpty(GroupFilter)
                && !string.Equals(GroupFilter, UngroupedFilter, StringComparison.Ordinal)
                && FindGroup(GroupFilter) == null)
            {
                GroupFilter = string.Empty;
                changed = true;
            }
            if (changed)
                Changed?.Invoke();
        }

        public void SetIsolation(string groupId)
        {
            string normalized = groupId ?? string.Empty;
            if (!string.IsNullOrEmpty(normalized) && FindGroup(normalized) == null)
                throw new InvalidOperationException($"Entity group '{normalized}' does not exist.");
            if (string.Equals(IsolatedGroupId, normalized, StringComparison.Ordinal))
                return;
            IsolatedGroupId = normalized;
            Changed?.Invoke();
        }

        public void SetCategoryFilter(string category)
        {
            string normalized = category?.Trim() ?? string.Empty;
            if (string.Equals(CategoryFilter, normalized, StringComparison.OrdinalIgnoreCase))
                return;
            CategoryFilter = normalized;
            Changed?.Invoke();
        }

        public void SetGroupFilter(string groupId)
        {
            string normalized = groupId ?? string.Empty;
            if (!string.IsNullOrEmpty(normalized)
                && !string.Equals(normalized, UngroupedFilter, StringComparison.Ordinal)
                && FindGroup(normalized) == null)
            {
                throw new InvalidOperationException($"Entity group '{normalized}' does not exist.");
            }
            if (string.Equals(GroupFilter, normalized, StringComparison.Ordinal))
                return;
            GroupFilter = normalized;
            Changed?.Invoke();
        }

        public bool CanSelect(string entityId)
        {
            LevelEntity entity = FindEntity(entityId);
            if (entity == null || !IsVisible(entity))
                return false;
            LevelEntityGroupData group = FindGroup(entity.groupId);
            if (group?.locked == true)
                return false;
            if (!string.IsNullOrEmpty(GroupFilter))
            {
                bool groupMatches = string.Equals(GroupFilter, UngroupedFilter, StringComparison.Ordinal)
                    ? string.IsNullOrEmpty(entity.groupId)
                    : string.Equals(entity.groupId, GroupFilter, StringComparison.Ordinal);
                if (!groupMatches)
                    return false;
            }
            if (!string.IsNullOrEmpty(CategoryFilter))
            {
                if (!catalog.TryGet(entity.archetypeId, out LevelArchetypeDefinition archetype)
                    || !string.Equals(
                        archetype.Category,
                        CategoryFilter,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsVisible(LevelEntity entity)
        {
            if (entity == null)
                return false;
            LevelEntityGroupData group = FindGroup(entity.groupId);
            if (group?.hidden == true)
                return false;
            return string.IsNullOrEmpty(IsolatedGroupId)
                || string.Equals(entity.groupId, IsolatedGroupId, StringComparison.Ordinal);
        }

        public void ApplyProjection(LevelWorldProjector projector)
        {
            if (projector == null || document == null)
                return;
            foreach (LevelEntity entity in document.entities.Where(entity => entity != null))
            {
                if (projector.TryGetEntity(entity.id, out LevelEntityView view))
                    view.gameObject.SetActive(IsVisible(entity));
            }
        }

        private LevelEntity FindEntity(string entityId) => document?.entities.FirstOrDefault(entity =>
            string.Equals(entity?.id, entityId, StringComparison.Ordinal));

        private LevelEntityGroupData FindGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return null;
            return document?.groups.FirstOrDefault(group => string.Equals(
                group?.id,
                groupId,
                StringComparison.Ordinal));
        }
    }
}
