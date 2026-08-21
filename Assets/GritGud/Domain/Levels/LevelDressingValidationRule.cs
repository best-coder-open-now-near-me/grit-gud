using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public sealed class LevelDressingValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            LevelDressingData dressing = context.Document.dressing;
            if (dressing == null)
            {
                context.Error("dressing.missing", "The level needs dressing data.");
                return;
            }
            CheckLimit(
                context,
                "dressing.decals.limit",
                "decals",
                dressing.decals.Count,
                LevelDressingData.MaximumDecalCount);
            CheckLimit(
                context,
                "dressing.vfx.limit",
                "ambient VFX placements",
                dressing.ambientVfx.Count,
                LevelDressingData.MaximumAmbientVfxCount);
            CheckLimit(
                context,
                "dressing.audio.limit",
                "audio zones",
                dressing.audioZones.Count,
                LevelDressingData.MaximumAudioZoneCount);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelDecalData decal in dressing.decals)
            {
                if (decal == null)
                {
                    context.Error("dressing.decal.missing", "The decal list contains an empty entry.");
                    continue;
                }
                CheckIdentity(context, ids, "decal", decal.id, decal.displayName);
                if (!LevelDressingIds.IsDecalStyle(decal.styleId)
                    || !LevelValidationMath.IsFinite(decal.position)
                    || !LevelValidationMath.IsFinite(decal.rotationEuler)
                    || !Positive(decal.size.x)
                    || !Positive(decal.size.y)
                    || !UnitColor(decal.color))
                {
                    context.Error(
                        "dressing.decal.invalid",
                        $"Decal '{decal.id}' has an unsupported style or invalid transform, size, or color.");
                }
                WarnOutsideBounds(context, "Decal", decal.id, decal.position);
            }

            foreach (LevelAmbientVfxData effect in dressing.ambientVfx)
            {
                if (effect == null)
                {
                    context.Error("dressing.vfx.missing", "The ambient-VFX list contains an empty entry.");
                    continue;
                }
                CheckIdentity(context, ids, "ambient VFX", effect.id, effect.displayName);
                if (!LevelDressingIds.IsAmbientEffect(effect.effectId)
                    || !LevelValidationMath.IsFinite(effect.position)
                    || !LevelValidationMath.IsFinite(effect.rotationEuler)
                    || !Positive(effect.scale.x)
                    || !Positive(effect.scale.y)
                    || !Positive(effect.scale.z))
                {
                    context.Error(
                        "dressing.vfx.invalid",
                        $"Ambient VFX '{effect.id}' has an unsupported effect or invalid transform or scale.");
                }
                WarnOutsideBounds(context, "Ambient VFX", effect.id, effect.position);
            }

            foreach (LevelAudioZoneData zone in dressing.audioZones)
            {
                if (zone == null)
                {
                    context.Error("dressing.audio.missing", "The audio-zone list contains an empty entry.");
                    continue;
                }
                CheckIdentity(context, ids, "audio zone", zone.id, zone.displayName);
                if (!LevelDressingIds.IsAmbientSound(zone.soundId)
                    || !LevelValidationMath.IsFinite(zone.center)
                    || !Positive(zone.size.x)
                    || !Positive(zone.size.y)
                    || !Positive(zone.size.z)
                    || !UnitInterval(zone.volume)
                    || !LevelValidationMath.IsFinite(zone.fadeDistance)
                    || zone.fadeDistance < 0f)
                {
                    context.Error(
                        "dressing.audio.invalid",
                        $"Audio zone '{zone.id}' has an unsupported sound or invalid bounds, volume, or fade.");
                }
                WarnOutsideBounds(context, "Audio zone", zone.id, zone.center);
            }
        }

        private static void CheckLimit(
            LevelValidationContext context,
            string code,
            string label,
            int count,
            int maximum)
        {
            if (count > maximum)
                context.Error(code, $"The level contains {count} {label}; the limit is {maximum}.");
        }

        private static void CheckIdentity(
            LevelValidationContext context,
            ISet<string> ids,
            string label,
            string id,
            string displayName)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                context.Error("dressing.id", "Dressing IDs must be present and unique.");
            if (string.IsNullOrWhiteSpace(displayName))
                context.Error("dressing.name", $"The {label} '{id}' needs a display name.");
        }

        private static void WarnOutsideBounds(
            LevelValidationContext context,
            string label,
            string id,
            Float3Data position)
        {
            if (LevelValidationMath.IsFinite(position)
                && !LevelValidationMath.Contains(context.Document.bounds, position))
            {
                context.Warning(
                    "dressing.outside-bounds",
                    $"{label} '{id}' is outside the authored level bounds.");
            }
        }

        private static bool Positive(float value) =>
            LevelValidationMath.IsFinite(value) && value > 0f;

        private static bool UnitInterval(float value) =>
            LevelValidationMath.IsFinite(value) && value >= 0f && value <= 1f;

        private static bool UnitColor(FloatColorData value) =>
            LevelValidationMath.IsFinite(value)
            && UnitInterval(value.r)
            && UnitInterval(value.g)
            && UnitInterval(value.b)
            && UnitInterval(value.a);
    }
}
