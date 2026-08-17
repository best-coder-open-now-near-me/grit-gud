using System;
using GritGud.Domain.Gameplay;
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
        private readonly LineRenderer getUpRing;
        private readonly GameObject propBoundsObject;
        private readonly Mesh propBoundsMesh;
        private readonly Material material;

        internal bool IsPropBoundsVisible => propBoundsObject.activeSelf;

        internal bool IsGetUpRingVisible => getUpRing.enabled;

        internal Vector3 PropBoundsCenter => propBoundsObject.transform.position;

        internal Vector3 PropBoundsHalfExtents =>
            propBoundsObject.transform.localScale;

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
            getUpRing = CreateLine(
                "Push Off Get-Up Clearance",
                RingWidth,
                RingSegments,
                true);
            propBoundsObject = new GameObject("Push Off Cover Bounds");
            propBoundsObject.transform.SetParent(root.transform, false);
            propBoundsMesh = CreateBoundsMesh();
            propBoundsObject.AddComponent<MeshFilter>().sharedMesh =
                propBoundsMesh;
            MeshRenderer boundsRenderer =
                propBoundsObject.AddComponent<MeshRenderer>();
            boundsRenderer.sharedMaterial = material;
            boundsRenderer.shadowCastingMode = ShadowCastingMode.Off;
            boundsRenderer.receiveShadows = false;
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
            propBoundsObject.SetActive(false);
            getUpRing.enabled = false;
        }

        public void ShowPushOff(
            Vector3 origin,
            Vector3 destination,
            bool valid,
            Transform subjectRoot,
            Transform actorRoot)
        {
            if (subjectRoot == null)
                throw new ArgumentNullException(nameof(subjectRoot));
            if (actorRoot == null)
                throw new ArgumentNullException(nameof(actorRoot));

            Show(origin, destination, valid);
            Color color = valid
                ? GameplayVisualPalette.DisplacementPreview
                : GameplayVisualPalette.DisplacementPreviewInvalid;
            ApplyColor(color);

            UnityDisplacementOrientedBounds bounds =
                UnityDisplacementGeometry.ResolveOrientedBounds(
                    subjectRoot,
                    destination,
                    subjectRoot.rotation);
            propBoundsObject.transform.SetPositionAndRotation(
                bounds.Center,
                bounds.Rotation);
            propBoundsObject.transform.localScale = bounds.HalfExtents;
            propBoundsObject.SetActive(true);

            CharacterController controller =
                actorRoot.GetComponent<CharacterController>();
            float radius = controller != null
                ? controller.radius * Mathf.Max(
                    Mathf.Abs(actorRoot.lossyScale.x),
                    Mathf.Abs(actorRoot.lossyScale.z))
                : RingRadius;
            Vector3 center = actorRoot.position
                + (Vector3.up * SurfaceOffset);
            for (int index = 0; index < RingSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / RingSegments;
                getUpRing.SetPosition(
                    index,
                    center + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0f,
                        Mathf.Sin(angle) * radius));
            }
            getUpRing.enabled = true;
        }

        public void Hide()
        {
            path.enabled = false;
            destinationRing.enabled = false;
            getUpRing.enabled = false;
            propBoundsObject.SetActive(false);
        }

        public void Dispose()
        {
            GameplayObjectLifecycle.Destroy(root);
            GameplayObjectLifecycle.Destroy(propBoundsMesh);
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

        private static Mesh CreateBoundsMesh()
        {
            var mesh = new Mesh
            {
                name = "Push Off Cover Bounds Mesh",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-1f, -1f, -1f),
                    new Vector3(1f, -1f, -1f),
                    new Vector3(1f, -1f, 1f),
                    new Vector3(-1f, -1f, 1f),
                    new Vector3(-1f, 1f, -1f),
                    new Vector3(1f, 1f, -1f),
                    new Vector3(1f, 1f, 1f),
                    new Vector3(-1f, 1f, 1f),
                },
            };
            mesh.SetIndices(
                new[]
                {
                    0, 1, 1, 2, 2, 3, 3, 0,
                    4, 5, 5, 6, 6, 7, 7, 4,
                    0, 4, 1, 5, 2, 6, 3, 7,
                },
                MeshTopology.Lines,
                0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
