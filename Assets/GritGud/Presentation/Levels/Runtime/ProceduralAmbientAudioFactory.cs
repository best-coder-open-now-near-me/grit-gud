using System;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Levels.Runtime
{
    public static class ProceduralAmbientAudioFactory
    {
        private const int SampleRate = 22050;
        private const int DurationSeconds = 2;

        public static AudioClip Create(string soundId)
        {
            if (!LevelDressingIds.IsAmbientSound(soundId))
                throw new ArgumentException($"Ambient sound '{soundId}' is not supported.", nameof(soundId));
            int sampleCount = SampleRate * DurationSeconds;
            var samples = new float[sampleCount];
            uint noiseState = 0x9e3779b9u;
            float filteredNoise = 0f;
            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = ((noiseState >> 8) / 8388607.5f) - 1f;
                filteredNoise += (noise - filteredNoise) * 0.018f;
                samples[index] = Sample(soundId, time, filteredNoise);
            }
            AudioClip clip = AudioClip.Create(
                $"Ambient {soundId}",
                sampleCount,
                1,
                SampleRate,
                false);
            if (!clip.SetData(samples, 0))
            {
                UnityEngine.Object.Destroy(clip);
                throw new InvalidOperationException(
                    $"Could not populate procedural ambient sound '{soundId}'.");
            }
            return clip;
        }

        private static float Sample(string soundId, float time, float filteredNoise)
        {
            switch (soundId)
            {
                case "wind":
                    float gust = 0.55f + 0.45f * Mathf.Sin(time * Mathf.PI);
                    return filteredNoise * gust * 0.34f;
                case "ventilation":
                    return (Mathf.Sin(time * Mathf.PI * 2f * 82f) * 0.13f
                        + Mathf.Sin(time * Mathf.PI * 2f * 164f) * 0.035f
                        + filteredNoise * 0.09f);
                default:
                    float modulation = 0.82f + 0.18f * Mathf.Sin(time * Mathf.PI);
                    return modulation * (
                        Mathf.Sin(time * Mathf.PI * 2f * 55f) * 0.12f
                        + Mathf.Sin(time * Mathf.PI * 2f * 110f) * 0.045f
                        + Mathf.Sin(time * Mathf.PI * 2f * 220f) * 0.015f);
            }
        }
    }
}
