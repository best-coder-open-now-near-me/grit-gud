using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public sealed class LevelDocumentValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            LevelDocument document = context.Document;
            if (document.schemaVersion != LevelDocument.CurrentSchemaVersion)
            {
                context.Error(
                    "schema.unsupported",
                    $"Schema version {document.schemaVersion} is not supported; expected "
                    + $"{LevelDocument.CurrentSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(document.levelId))
            {
                context.Error("level.id.missing", "The level needs a stable ID.");
            }

            if (string.IsNullOrWhiteSpace(document.displayName))
            {
                context.Error("level.name.missing", "The level needs a display name.");
            }

            if (!LevelValidationMath.IsFinite(document.bounds.center)
                || !LevelValidationMath.IsFinite(document.bounds.size))
            {
                context.Error("bounds.not-finite", "Level bounds must contain finite coordinates.");
            }
            else if (document.bounds.size.x <= 0f
                || document.bounds.size.y <= 0f
                || document.bounds.size.z <= 0f)
            {
                context.Error("bounds.size", "Every level-bounds dimension must be greater than zero.");
            }

            if (document.entities.Count > LevelValidator.MaximumEntityCount)
            {
                context.Error(
                    "entities.limit",
                    $"The level contains {document.entities.Count} entities; the limit is "
                    + $"{LevelValidator.MaximumEntityCount}.");
            }
        }
    }
}
