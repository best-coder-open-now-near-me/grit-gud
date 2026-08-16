using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.LevelEditing.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class LevelEditorGridPresenter : IDisposable
    {
        private const int MaximumLinesPerAxis = 201;

        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;

        public LevelEditorGridPresenter(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            root = new GameObject("Level Editor Grid");
            root.transform.SetParent(parent, false);
            var filter = root.AddComponent<MeshFilter>();
            var renderer = root.AddComponent<MeshRenderer>();
            mesh = new Mesh { name = "Level Editor Grid Mesh" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            if (shader == null)
                throw new InvalidOperationException("No unlit shader is available for the editor grid.");
            material = new Material(shader)
            {
                name = "Level Editor Grid Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
            Color color = new Color(0.16f, 0.54f, 0.72f, 0.55f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void Refresh(LevelBoundsData bounds, LevelEditorGridSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            root.SetActive(settings.Visible);
            if (!settings.Visible)
                return;

            float minX = bounds.center.x - (bounds.size.x * 0.5f);
            float maxX = bounds.center.x + (bounds.size.x * 0.5f);
            float minZ = bounds.center.z - (bounds.size.z * 0.5f);
            float maxZ = bounds.center.z + (bounds.size.z * 0.5f);
            IReadOnlyList<float> xLines = BuildAxisLines(minX, maxX, settings.Spacing);
            IReadOnlyList<float> zLines = BuildAxisLines(minZ, maxZ, settings.Spacing);
            var vertices = new List<Vector3>((xLines.Count + zLines.Count) * 2);
            float displayElevation = settings.Elevation + 0.015f;
            foreach (float x in xLines)
            {
                vertices.Add(new Vector3(x, displayElevation, minZ));
                vertices.Add(new Vector3(x, displayElevation, maxZ));
            }
            foreach (float z in zLines)
            {
                vertices.Add(new Vector3(minX, displayElevation, z));
                vertices.Add(new Vector3(maxX, displayElevation, z));
            }

            int[] indices = new int[vertices.Count];
            for (int index = 0; index < indices.Length; index++)
                indices[index] = index;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }

        public void SetVisible(bool visible) => root.SetActive(visible);

        private static IReadOnlyList<float> BuildAxisLines(
            float minimum,
            float maximum,
            float spacing)
        {
            var values = new List<float> { minimum };
            int firstIndex = Mathf.CeilToInt(minimum / spacing);
            int lastIndex = Mathf.FloorToInt(maximum / spacing);
            int available = Mathf.Max(0, lastIndex - firstIndex + 1);
            int stride = Mathf.Max(1, Mathf.CeilToInt(
                available / (float)(MaximumLinesPerAxis - 2)));
            for (int index = firstIndex; index <= lastIndex; index += stride)
            {
                float value = index * spacing;
                if (value > minimum + 0.0001f && value < maximum - 0.0001f)
                    values.Add(value);
            }
            if (maximum > minimum + 0.0001f)
                values.Add(maximum);
            return values;
        }

        public void Dispose()
        {
            GameplayObjectLifecycle.Destroy(root);
            GameplayObjectLifecycle.Destroy(mesh);
            GameplayObjectLifecycle.Destroy(material);
        }
    }
}
