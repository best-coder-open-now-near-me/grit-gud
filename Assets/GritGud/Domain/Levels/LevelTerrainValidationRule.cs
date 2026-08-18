using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public sealed class LevelTerrainValidationRule : ILevelValidationRule
    {
        public const int MaximumSamplesPerAxis = 257;
        public const int MaximumSamplesPerSurface = 66049;
        public const int MaximumSurfaceCount = 16;
        public const int MaximumSamplesPerDocument = 262144;
        public const int MinimumQuantizedHeight = -1000000;
        public const int MaximumQuantizedHeight = 1000000;

        public void Evaluate(LevelValidationContext context)
        {
            if (context.Document.terrainSurfaces.Count > MaximumSurfaceCount)
            {
                context.Error(
                    "terrain.surface-limit",
                    $"The level contains {context.Document.terrainSurfaces.Count} terrain "
                    + $"surfaces; the limit is {MaximumSurfaceCount}.");
            }

            long totalSampleCount = context.Document.terrainSurfaces
                .Where(surface => surface != null
                    && surface.sampleCountX > 0
                    && surface.sampleCountZ > 0)
                .Sum(surface => (long)surface.sampleCountX * surface.sampleCountZ);
            if (totalSampleCount > MaximumSamplesPerDocument)
            {
                context.Error(
                    "terrain.document-sample-limit",
                    $"The level contains {totalSampleCount} terrain samples; the total limit "
                    + $"is {MaximumSamplesPerDocument}.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TerrainSurfaceData surface in context.Document.terrainSurfaces)
            {
                if (surface == null)
                {
                    context.Error("terrain.missing", "The terrain surface list contains an empty entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(surface.id) || !ids.Add(surface.id))
                {
                    context.Error("terrain.id", "Terrain surface IDs must be present and unique.");
                }

                bool dimensionsValid = surface.sampleCountX >= 2
                    && surface.sampleCountZ >= 2
                    && surface.sampleCountX <= MaximumSamplesPerAxis
                    && surface.sampleCountZ <= MaximumSamplesPerAxis
                    && (long)surface.sampleCountX * surface.sampleCountZ
                        <= MaximumSamplesPerSurface;
                if (!dimensionsValid)
                {
                    context.Error(
                        "terrain.dimensions",
                        $"Terrain '{surface.id}' needs between 2 and {MaximumSamplesPerAxis} "
                        + "samples per axis within the total sample limit.");
                }

                int expectedSamples = dimensionsValid
                    ? surface.sampleCountX * surface.sampleCountZ
                    : -1;
                if (surface.heightSamples.Count != expectedSamples)
                {
                    context.Error(
                        "terrain.samples",
                        $"Terrain '{surface.id}' has {surface.heightSamples.Count} height samples; "
                        + $"expected {expectedSamples}.");
                }

                if (surface.materialSamples.Count != expectedSamples)
                {
                    context.Error(
                        "terrain.material-samples",
                        $"Terrain '{surface.id}' has {surface.materialSamples.Count} material samples; "
                        + $"expected {expectedSamples}.");
                }
                else if (surface.materialSamples.Any(value =>
                    !TerrainMaterialKinds.IsSupported(value)))
                {
                    context.Error(
                        "terrain.material-range",
                        $"Terrain '{surface.id}' contains an unsupported painted material index.");
                }

                if (!LevelValidationMath.IsFinite(surface.origin)
                    || !LevelValidationMath.IsFinite(surface.sampleSpacing)
                    || !LevelValidationMath.IsFinite(surface.minimumElevation)
                    || !LevelValidationMath.IsFinite(surface.elevationIncrement)
                    || surface.sampleSpacing <= 0f
                    || surface.elevationIncrement <= 0f)
                {
                    context.Error(
                        "terrain.scale",
                        $"Terrain '{surface.id}' needs finite origins and positive sample scales.");
                }

                if (surface.heightSamples.Any(value =>
                    value < MinimumQuantizedHeight || value > MaximumQuantizedHeight))
                {
                    context.Error(
                        "terrain.height-range",
                        $"Terrain '{surface.id}' contains a height outside the quantized range.");
                }

                TerrainAppearanceData appearance = surface.appearance;
                if (appearance == null
                    || string.IsNullOrWhiteSpace(appearance.presetId)
                    || !ValidUnitColor(appearance.baseColor)
                    || !ValidUnitColor(appearance.steepColor)
                    || !LevelValidationMath.IsFinite(appearance.slopeBlendStartDegrees)
                    || !LevelValidationMath.IsFinite(appearance.slopeBlendEndDegrees)
                    || appearance.slopeBlendStartDegrees < 0f
                    || appearance.slopeBlendEndDegrees > 89f
                    || appearance.slopeBlendEndDegrees
                        <= appearance.slopeBlendStartDegrees
                    || !UnitInterval(appearance.smoothness)
                    || !UnitInterval(appearance.specularStrength))
                {
                    context.Error(
                        "terrain.appearance",
                        $"Terrain '{surface.id}' needs valid colors, a 0-89 degree slope blend, "
                        + "and unit-range surface response values.");
                }
            }
        }

        private static bool ValidUnitColor(FloatColorData color)
        {
            return LevelValidationMath.IsFinite(color)
                && UnitInterval(color.r)
                && UnitInterval(color.g)
                && UnitInterval(color.b)
                && UnitInterval(color.a);
        }

        private static bool UnitInterval(float value)
        {
            return LevelValidationMath.IsFinite(value) && value >= 0f && value <= 1f;
        }
    }
}
