using System;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Tools
{
    public sealed class TerrainBrushFootprint : IDisposable
    {
        private readonly GameObject root;
        private readonly LineRenderer line;
        private readonly Material material;

        public TerrainBrushFootprint()
        {
            root = new GameObject("Terrain Brush Footprint");
            line = root.AddComponent<LineRenderer>();
            material = RuntimeMaterialFactory.CreateColor(
                Color.white,
                "Terrain Brush Preview Material");
            line.sharedMaterial = material;
            line.loop = true;
            line.useWorldSpace = true;
            line.startWidth = 0.06f;
            line.endWidth = 0.06f;
            Hide();
        }

        public bool IsVisible => root != null && root.activeSelf;

        public void Show(
            TerrainSurfaceData surface,
            Vector3 point,
            int radiusInSamples,
            TerrainBrushMode mode)
        {
            Vector3[] points = BuildPoints(
                point + Vector3.up * 0.05f,
                Mathf.Clamp(radiusInSamples, 1, 16) * surface.sampleSpacing);
            line.positionCount = points.Length;
            line.SetPositions(points);
            Color color = mode switch
            {
                TerrainBrushMode.Lower => LevelEditorTheme.LowerTerrain,
                TerrainBrushMode.Smooth => LevelEditorTheme.SmoothTerrain,
                TerrainBrushMode.Flatten => LevelEditorTheme.FlattenTerrain,
                TerrainBrushMode.Paint => Color.cyan,
                _ => LevelEditorTheme.RaiseTerrain,
            };
            line.startColor = line.endColor = color;
            if (material != null)
            {
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
            }

            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Dispose()
        {
            Destroy(root);
            Destroy(material);
        }

        public static Vector3[] BuildPoints(
            Vector3 center,
            float radius,
            int segmentCount = 48)
        {
            segmentCount = Mathf.Max(8, segmentCount);
            var points = new Vector3[segmentCount];
            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                points[index] = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);
            }

            return points;
        }

        private static void Destroy(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
