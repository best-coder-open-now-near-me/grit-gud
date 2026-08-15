using System;
using System.Collections.Generic;
using GritGud.Application.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.LevelEditing
{
    internal sealed class LevelEditorOutlinePresenter : IDisposable
    {
        private readonly Transform parent;
        private readonly RuntimeBoundsOutline selectionOutline;
        private readonly RuntimeBoundsOutline hoverOutline;
        private readonly RuntimeBoundsOutline placementOutline;
        private readonly Transform rotationPivotMarker;
        private readonly Material rotationPivotMaterial;
        private readonly Dictionary<string, RuntimeBoundsOutline> secondaryOutlines =
            new Dictionary<string, RuntimeBoundsOutline>(StringComparer.Ordinal);
        private readonly HashSet<string> visibleSecondaryIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> staleSecondaryIds = new List<string>();

        public LevelEditorOutlinePresenter(Transform parent)
        {
            this.parent = parent != null
                ? parent
                : throw new ArgumentNullException(nameof(parent));
            selectionOutline = Create("Selection Outline", LevelEditorTheme.SelectionOutline);
            hoverOutline = Create("Hover Outline", LevelEditorTheme.HoverOutline);
            placementOutline = Create("Placement Outline", LevelEditorTheme.PlacementOutline);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Rotation Pivot Marker";
            marker.transform.SetParent(parent, false);
            marker.transform.localScale = Vector3.one * 0.22f;
            Destroy(marker.GetComponent<Collider>());
            rotationPivotMaterial = RuntimeMaterialFactory.CreateColor(
                LevelEditorTheme.SelectionOutline,
                "Rotation Pivot Marker Material");
            marker.GetComponent<Renderer>().sharedMaterial = rotationPivotMaterial;
            rotationPivotMarker = marker.transform;
            rotationPivotMarker.gameObject.SetActive(false);
        }

        public void PresentSelection(
            LevelEntityView primaryView,
            IReadOnlyList<LevelSelectionTarget> targets,
            LevelWorldProjector projector,
            bool visible)
        {
            selectionOutline.gameObject.SetActive(visible && primaryView != null);
            if (visible && primaryView != null)
            {
                selectionOutline.SetBounds(primaryView.GetWorldBounds());
                rotationPivotMarker.position = primaryView.GetRotationPivotWorld();
            }
            rotationPivotMarker.gameObject.SetActive(visible && primaryView != null);

            visibleSecondaryIds.Clear();
            if (visible && targets != null && projector != null)
            {
                for (int index = 1; index < targets.Count; index++)
                {
                    LevelSelectionTarget target = targets[index];
                    if (!projector.TryGetEntity(
                            target.EntityId,
                            out LevelEntityView view)
                        || !visibleSecondaryIds.Add(target.EntityId))
                    {
                        continue;
                    }

                    if (!secondaryOutlines.TryGetValue(
                        target.EntityId,
                        out RuntimeBoundsOutline outline))
                    {
                        outline = Create(
                            "Secondary Selection Outline",
                            LevelEditorTheme.SecondarySelectionOutline);
                        secondaryOutlines.Add(target.EntityId, outline);
                    }

                    outline.gameObject.SetActive(true);
                    outline.SetBounds(view.GetWorldBounds());
                }
            }

            staleSecondaryIds.Clear();
            foreach (KeyValuePair<string, RuntimeBoundsOutline> entry in secondaryOutlines)
            {
                if (!visibleSecondaryIds.Contains(entry.Key))
                {
                    if (visible)
                    {
                        staleSecondaryIds.Add(entry.Key);
                    }
                    else
                    {
                        entry.Value.gameObject.SetActive(false);
                    }
                }
            }

            foreach (string entityId in staleSecondaryIds)
            {
                Destroy(secondaryOutlines[entityId].gameObject);
                secondaryOutlines.Remove(entityId);
            }
        }

        public void PresentHover(LevelEntityView view)
        {
            hoverOutline.gameObject.SetActive(view != null);
            if (view != null)
            {
                hoverOutline.SetBounds(view.GetWorldBounds());
            }
        }

        public void PresentPlacement(Bounds? bounds)
        {
            placementOutline.gameObject.SetActive(bounds.HasValue);
            if (bounds.HasValue)
            {
                placementOutline.SetBounds(bounds.Value);
            }
        }

        public void HideAll()
        {
            selectionOutline.gameObject.SetActive(false);
            hoverOutline.gameObject.SetActive(false);
            placementOutline.gameObject.SetActive(false);
            rotationPivotMarker.gameObject.SetActive(false);
            foreach (RuntimeBoundsOutline outline in secondaryOutlines.Values)
            {
                outline.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            Destroy(selectionOutline?.gameObject);
            Destroy(hoverOutline?.gameObject);
            Destroy(placementOutline?.gameObject);
            Destroy(rotationPivotMarker?.gameObject);
            Destroy(rotationPivotMaterial);
            foreach (RuntimeBoundsOutline outline in secondaryOutlines.Values)
            {
                Destroy(outline?.gameObject);
            }

            secondaryOutlines.Clear();
        }

        private RuntimeBoundsOutline Create(string name, Color color)
        {
            var outlineObject = new GameObject(name);
            outlineObject.transform.SetParent(parent, false);
            RuntimeBoundsOutline outline =
                outlineObject.AddComponent<RuntimeBoundsOutline>();
            outline.Initialize(color);
            outline.gameObject.SetActive(false);
            return outline;
        }

        private static void Destroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
