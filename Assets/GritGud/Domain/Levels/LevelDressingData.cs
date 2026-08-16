using System;
using System.Collections.Generic;

namespace GritGud.Domain.Levels
{
    [Serializable]
    public sealed class LevelDecalData
    {
        public string id = string.Empty;
        public string displayName = "Decal";
        public string styleId = "grime";
        public Float3Data position;
        public Float3Data rotationEuler = new Float3Data(-90f, 0f, 0f);
        public Float3Data size = new Float3Data(2f, 2f, 1f);
        public FloatColorData color = new FloatColorData(0.06f, 0.07f, 0.08f, 0.55f);

        public void Normalize()
        {
            id = id ?? string.Empty;
            displayName = displayName ?? string.Empty;
            styleId = styleId ?? string.Empty;
        }

        public LevelDecalData DeepCopy() => new LevelDecalData
        {
            id = id ?? string.Empty,
            displayName = displayName ?? string.Empty,
            styleId = styleId ?? string.Empty,
            position = position,
            rotationEuler = rotationEuler,
            size = size,
            color = color,
        };
    }

    [Serializable]
    public sealed class LevelAmbientVfxData
    {
        public string id = string.Empty;
        public string displayName = "Ambient VFX";
        public string effectId = "dust-air";
        public Float3Data position;
        public Float3Data rotationEuler;
        public Float3Data scale = new Float3Data(1f, 1f, 1f);

        public void Normalize()
        {
            id = id ?? string.Empty;
            displayName = displayName ?? string.Empty;
            effectId = effectId ?? string.Empty;
        }

        public LevelAmbientVfxData DeepCopy() => new LevelAmbientVfxData
        {
            id = id ?? string.Empty,
            displayName = displayName ?? string.Empty,
            effectId = effectId ?? string.Empty,
            position = position,
            rotationEuler = rotationEuler,
            scale = scale,
        };
    }

    [Serializable]
    public sealed class LevelAudioZoneData
    {
        public string id = string.Empty;
        public string displayName = "Audio Zone";
        public string soundId = "industrial-hum";
        public Float3Data center;
        public Float3Data size = new Float3Data(10f, 5f, 10f);
        public float volume = 0.25f;
        public float fadeDistance = 5f;

        public void Normalize()
        {
            id = id ?? string.Empty;
            displayName = displayName ?? string.Empty;
            soundId = soundId ?? string.Empty;
        }

        public LevelAudioZoneData DeepCopy() => new LevelAudioZoneData
        {
            id = id ?? string.Empty,
            displayName = displayName ?? string.Empty,
            soundId = soundId ?? string.Empty,
            center = center,
            size = size,
            volume = volume,
            fadeDistance = fadeDistance,
        };
    }

    [Serializable]
    public sealed class LevelDressingData
    {
        public const int MaximumDecalCount = 128;
        public const int MaximumAmbientVfxCount = 64;
        public const int MaximumAudioZoneCount = 16;

        public List<LevelDecalData> decals = new List<LevelDecalData>();
        public List<LevelAmbientVfxData> ambientVfx = new List<LevelAmbientVfxData>();
        public List<LevelAudioZoneData> audioZones = new List<LevelAudioZoneData>();

        public void Normalize()
        {
            decals = decals ?? new List<LevelDecalData>();
            ambientVfx = ambientVfx ?? new List<LevelAmbientVfxData>();
            audioZones = audioZones ?? new List<LevelAudioZoneData>();
            foreach (LevelDecalData decal in decals)
                decal?.Normalize();
            foreach (LevelAmbientVfxData effect in ambientVfx)
                effect?.Normalize();
            foreach (LevelAudioZoneData zone in audioZones)
                zone?.Normalize();
        }

        public LevelDressingData DeepCopy()
        {
            var copy = new LevelDressingData();
            if (decals != null)
            {
                foreach (LevelDecalData decal in decals)
                    copy.decals.Add(decal?.DeepCopy());
            }
            if (ambientVfx != null)
            {
                foreach (LevelAmbientVfxData effect in ambientVfx)
                    copy.ambientVfx.Add(effect?.DeepCopy());
            }
            if (audioZones != null)
            {
                foreach (LevelAudioZoneData zone in audioZones)
                    copy.audioZones.Add(zone?.DeepCopy());
            }
            return copy;
        }
    }

    public static class LevelDressingIds
    {
        private static readonly string[] DecalStyleValues =
            { "grime", "oil", "hazard", "arrow" };
        private static readonly string[] AmbientEffectValues =
            { "dust-air", "ground-haze" };
        private static readonly string[] AmbientSoundValues =
            { "industrial-hum", "wind", "ventilation" };

        public static IReadOnlyList<string> DecalStyles => DecalStyleValues;
        public static IReadOnlyList<string> AmbientEffects => AmbientEffectValues;
        public static IReadOnlyList<string> AmbientSounds => AmbientSoundValues;

        public static bool IsDecalStyle(string value) => Contains(DecalStyleValues, value);
        public static bool IsAmbientEffect(string value) => Contains(AmbientEffectValues, value);
        public static bool IsAmbientSound(string value) => Contains(AmbientSoundValues, value);

        private static bool Contains(IEnumerable<string> values, string value)
        {
            foreach (string candidate in values)
            {
                if (string.Equals(candidate, value, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
