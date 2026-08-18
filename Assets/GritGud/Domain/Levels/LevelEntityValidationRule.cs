using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public sealed class LevelEntityValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            var entityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelEntity entity in context.Document.entities)
            {
                if (entity == null)
                {
                    context.Error("entity.missing", "The entity list contains an empty entry.");
                    continue;
                }

                string entityId = entity.id;
                if (string.IsNullOrWhiteSpace(entityId))
                {
                    context.Error("entity.id.missing", "An entity needs a stable ID.");
                }
                else if (!entityIds.Add(entityId))
                {
                    context.Error(
                        "entity.id.duplicate",
                        $"Entity ID '{entityId}' is duplicated.",
                        entityId);
                }

                if (string.IsNullOrWhiteSpace(entity.archetypeId))
                {
                    context.Error(
                        "entity.archetype.missing",
                        "The entity needs an archetype ID.",
                        entityId);
                }
                else if (context.KnownArchetypeIds != null
                    && !context.KnownArchetypeIds.Contains(entity.archetypeId))
                {
                    context.Error(
                        "entity.archetype.unknown",
                        $"Archetype '{entity.archetypeId}' is not in the active catalog.",
                        entityId);
                }

                if (!LevelValidationMath.IsFinite(entity.transform.position)
                    || !LevelValidationMath.IsFinite(entity.transform.pitchDegrees)
                    || !LevelValidationMath.IsFinite(entity.transform.yawDegrees)
                    || !LevelValidationMath.IsFinite(entity.transform.rollDegrees))
                {
                    context.Error(
                        "entity.transform.not-finite",
                        "Entity transforms must be finite.",
                        entityId);
                }
                else if (!LevelValidationMath.Contains(
                    context.Document.bounds,
                    entity.transform.position))
                {
                    context.Warning(
                        "entity.outside-bounds",
                        "The entity origin is outside the authored level bounds.",
                        entityId);
                }

                if (entity.rotationPivot != null
                    && (!string.Equals(entity.rotationPivot.mode, "bounds", StringComparison.Ordinal)
                    || !LevelValidationMath.IsFinite(entity.rotationPivot.localPosition)))
                {
                    context.Error(
                        "entity.rotation-pivot.invalid",
                        "Entity rotation pivots must use a finite bounds-relative position.",
                        entityId);
                }
            }
        }
    }
}
