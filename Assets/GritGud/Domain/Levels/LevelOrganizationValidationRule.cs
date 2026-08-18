using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public sealed class LevelOrganizationValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            if (context.Document.groups.Count > LevelDocument.MaximumEntityGroupCount)
            {
                context.Error(
                    "groups.limit",
                    $"The level contains {context.Document.groups.Count} groups; the limit is "
                    + $"{LevelDocument.MaximumEntityGroupCount}.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LevelEntityGroupData group in context.Document.groups)
            {
                if (group == null)
                {
                    context.Error("group.missing", "The entity-group list contains an empty entry.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(group.id) || !ids.Add(group.id))
                {
                    context.Error(
                        "group.id",
                        "Entity-group IDs must be present and unique.");
                }
                if (string.IsNullOrWhiteSpace(group.displayName))
                {
                    context.Error(
                        "group.name.missing",
                        $"Entity group '{group.id}' needs a display name.");
                }
                else if (!names.Add(group.displayName.Trim()))
                {
                    context.Warning(
                        "group.name.duplicate",
                        $"More than one entity group is named '{group.displayName.Trim()}'.");
                }
            }

            foreach (LevelEntity entity in context.Document.entities)
            {
                if (entity != null
                    && !string.IsNullOrWhiteSpace(entity.groupId)
                    && !ids.Contains(entity.groupId))
                {
                    context.Error(
                        "entity.group.unknown",
                        $"Entity '{entity.id}' references missing group '{entity.groupId}'.",
                        entity.id);
                }
            }
        }
    }
}
