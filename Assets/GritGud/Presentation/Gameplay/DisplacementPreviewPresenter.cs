using System;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class DisplacementPreviewPresenter : IDisposable
    {
        private const int RingSegments = 48;
        private const float PathWidth = 0.045f;
        private const float RingWidth = 0.04f;
        private const float RingRadius = 0.28f;
        private const float SurfaceOffset = 0.04f;

        private readonly GameObject root;
        private readonly LineRenderer path;
        private readonly LineRenderer destinationRing;
        private readonly Material material;

        public DisplacementPreviewPresenter(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            root = new GameObject("Displacement Destination Preview");
            root.transform.SetParent(parent, false);
            material = RuntimeMaterialFactory.CreateColor(
                GameplayVisualPalette.DisplacementPreview,
                "Displacement Preview Material");
            path = CreateLine("Displacement Path", PathWidth, 2, false);
            destinationRing = CreateLine(
                "Displacement Destination",
                RingWidth,
                RingSegments,
                true);
            Hide();
        }

        public void Show(Vector3 origin, Vector3 destination, bool valid)
        {
            Color color = valid
                ? GameplayVisualPalette.DisplacementPreview
                : GameplayVisualPalette.DisplacementPreviewInvalid;
            ApplyColor(color);

            Vector3 lift = Vector3.up * SurfaceOffset;
            path.SetPosition(0, origin + lift);
            path.SetPosition(1, destination + lift);
            for (int index = 0; index < RingSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / RingSegments;
                destinationRing.SetPosition(
                    index,
                    destination + lift + new Vector3(
                        Mathf.Cos(angle) * RingRadius,
                        0f,
                        Mathf.Sin(angle) * RingRadius));
            }

            path.enabled = true;
            destinationRing.enabled = true;
        }

        public void Hide()
        {
            path.enabled = false;
            destinationRing.enabled = false;
        }

        public void Dispose()
        {
            GameplayObjectLifecycle.Destroy(root);
            GameplayObjectLifecycle.Destroy(material);
        }

        private LineRenderer CreateLine(
            string objectName,
            float width,
            int positionCount,
            bool loop)
        {
            var lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(root.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = positionCount;
            line.startWidth = width;
            line.endWidth = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void ApplyColor(Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }
    }
}
