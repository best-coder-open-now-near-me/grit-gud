using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public sealed class LevelGameplayMetadataValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            foreach (LevelEntity entity in context.Document.entities)
            {
                if (entity == null)
                {
                    continue;
                }

                ValidateCoverVolumes(context, entity);
                ValidateInteractionPoints(context, entity);
                ValidateDestructible(context, entity);
            }
        }

        private static void ValidateCoverVolumes(LevelValidationContext context, LevelEntity entity)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoverVolumeData volume in entity.coverVolumes)
            {
                if (volume == null)
                {
                    context.Error("cover.missing", "A cover-volume entry is empty.", entity.id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(volume.id) || !ids.Add(volume.id))
                {
                    context.Error(
                        "cover.id",
                        "Cover-volume IDs must be present and unique within an entity.",
                        entity.id);
                }

                if (!LevelValidationMath.IsFinite(volume.localCenter)
                    || !LevelValidationMath.IsFinite(volume.size)
                    || volume.size.x <= 0f
                    || volume.size.y <= 0f
                    || volume.size.z <= 0f)
                {
                    context.Error(
                        "cover.volume",
                        "Cover volumes need finite centers and positive dimensions.",
                        entity.id);
                }
            }
        }

        private static void ValidateInteractionPoints(
            LevelValidationContext context,
            LevelEntity entity)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (InteractionPointData point in entity.interactionPoints)
            {
                if (point == null)
                {
                    context.Error(
                        "interaction.missing",
                        "An interaction-point entry is empty.",
                        entity.id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(point.id) || !ids.Add(point.id))
                {
                    context.Error(
                        "interaction.id",
                        "Interaction-point IDs must be present and unique within an entity.",
                        entity.id);
                }

                if (string.IsNullOrWhiteSpace(point.type))
                {
                    context.Error(
                        "interaction.type",
                        "Every interaction point needs a type.",
                        entity.id);
                }

                if (!LevelValidationMath.IsFinite(point.localPosition)
                    || !LevelValidationMath.IsFinite(point.radius)
                    || point.radius <= 0f)
                {
                    context.Error(
                        "interaction.radius",
                        "Interaction points need finite positions and a positive radius.",
                        entity.id);
                }
            }
        }

        private static void ValidateDestructible(
            LevelValidationContext context,
            LevelEntity entity)
        {
            if (entity.destructible == null || !entity.destructible.enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(entity.destructible.initialState))
            {
                context.Error(
                    "destructible.state",
                    "A destructible entity needs an initial state.",
                    entity.id);
            }
            else if (!string.Equals(
                         entity.destructible.initialState,
                         "intact",
                         StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(
                         entity.destructible.initialState,
                         "damaged",
                         StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(
                         entity.destructible.initialState,
                         "destroyed",
                         StringComparison.OrdinalIgnoreCase))
            {
                context.Error(
                    "destructible.state",
                    "Destructible state must be intact, damaged, or destroyed.",
                    entity.id);
            }

            if (!LevelValidationMath.IsFinite(entity.destructible.integrity)
                || entity.destructible.integrity <= 0f)
            {
                context.Error(
                    "destructible.integrity",
                    "Destructible integrity must be finite and positive.",
                    entity.id);
            }
            if (string.IsNullOrWhiteSpace(entity.destructible.surfaceId))
            {
                context.Error(
                    "destructible.surface",
                    "A destructible entity needs an authoritative surface ID.",
                    entity.id);
            }
        }
    }
}
