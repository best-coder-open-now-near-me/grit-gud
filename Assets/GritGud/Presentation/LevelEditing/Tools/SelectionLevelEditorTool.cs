using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.LevelEditing.Core;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Tools
{
    public sealed class SelectionLevelEditorTool : ILevelEditorTool
    {
        public const string ToolId = "select";

        private LevelEditorToolContext context;
        private bool dragging;
        private Plane dragPlane;
        private Vector3 dragOffset;
        private readonly Dictionary<string, LevelTransformData> dragBefore =
            new Dictionary<string, LevelTransformData>();
        private readonly List<LevelEntity> clipboard = new List<LevelEntity>();
        private int pasteCount;

        public string Id => ToolId;

        public string DisplayName => "Select";

        public bool IsDragging => dragging;

        public string DragFeedback { get; private set; } = string.Empty;

        public void Activate(LevelEditorToolContext context)
        {
            this.context = context;
        }

        public void Deactivate()
        {
            RestoreDragPreview();
            context = null;
        }

        public void Tick(LevelEditorInputState input)
        {
            if (context == null)
            {
                return;
            }

            if (input.DeletePressed)
            {
                DeleteSelection();
            }

            if (input.RotatePressed)
            {
                RotateSelection();
            }

            if (input.CopyPressed)
            {
                CopySelection();
            }

            if (input.PastePressed)
            {
                PasteClipboard();
            }

            if (input.DuplicatePressed)
            {
                DuplicateSelection();
            }

            if (input.PrimaryPressed && !input.PointerBlocked)
            {
                if (context.SceneQuery.TryPickInteractionPoint(
                    input.PointerPosition,
                    out InteractionPointHandle handle,
                    out _))
                {
                    context.Selection.Set(new[]
                    {
                        new LevelSelectionTarget(
                            handle.EntityId,
                            LevelSelectionKind.InteractionPoint,
                            handle.PointId),
                    });
                    context.SetStatus("Selected interaction point.");
                }
                else if (context.SceneQuery.TryPickEntity(
                    input.PointerPosition,
                    out LevelEntityView view,
                    out Ray ray))
                {
                    if (input.AdditiveSelection)
                    {
                        context.Selection.Toggle(new LevelSelectionTarget(view.EntityId));
                        context.SetStatus("Updated multi-selection.");
                    }
                    else
                    {
                        if (context.Selection.Targets.All(target =>
                            !string.Equals(target.EntityId, view.EntityId, System.StringComparison.Ordinal)
                            || target.Kind != LevelSelectionKind.Entity))
                        {
                            context.Selection.SetSingle(view.EntityId);
                        }

                        BeginDrag(view, ray);
                    }
                }
                else if (!input.AdditiveSelection)
                {
                    context.Selection.Clear();
                }
            }

            if (dragging && input.PrimaryHeld)
            {
                UpdateDrag(context.SceneQuery.CreateRay(input.PointerPosition));
            }

            if (dragging && input.PrimaryReleased)
            {
                CommitDrag();
            }
        }

        public bool Cancel()
        {
            if (dragging)
            {
                RestoreDragPreview();
                context.SetStatus("Cancelled move.");
                return true;
            }

            if (context?.Selection.Primary != null)
            {
                context.Selection.Clear();
                return true;
            }

            return false;
        }

        private void BeginDrag(LevelEntityView view, Ray ray)
        {
            LevelEntity entity = context.Workspace.FindEntitySnapshot(view.EntityId);
            if (entity == null)
            {
                return;
            }

            dragBefore.Clear();
            foreach (string entityId in SelectedEntityIds())
            {
                LevelEntity selected = context.Workspace.FindEntitySnapshot(entityId);
                if (selected != null)
                {
                    dragBefore[entityId] = selected.transform;
                }
            }

            if (dragBefore.Count == 0)
            {
                return;
            }

            dragPlane = new Plane(
                Vector3.up,
                new Vector3(0f, entity.transform.position.y, 0f));
            if (!context.SceneQuery.TryProjectToPlane(ray, dragPlane, out Vector3 point))
            {
                return;
            }

            dragOffset = view.transform.position - point;
            dragging = true;
            DragFeedback = BuildDragFeedback(Vector3.zero, view.Archetype.PlacementRules.PositionSnap);
            context.SetStatus("Moving on the X/Z plane. Release to apply; Esc to cancel.");
        }

        private void UpdateDrag(Ray ray)
        {
            string entityId = context.Selection.PrimaryEntityId;
            if (!context.Projector.TryGetEntity(entityId, out LevelEntityView primaryView)
                || !context.SceneQuery.TryProjectToPlane(ray, dragPlane, out Vector3 point))
            {
                return;
            }

            Vector3 primaryPosition = context.SnapSettings.SnapPosition(
                point + dragOffset,
                primaryView.Archetype.PlacementRules.PositionSnap);
            LevelTransformData primaryBefore = dragBefore[entityId];
            Vector3 delta = primaryPosition - new Vector3(
                primaryBefore.position.x,
                primaryBefore.position.y,
                primaryBefore.position.z);
            DragFeedback = BuildDragFeedback(delta, primaryView.Archetype.PlacementRules.PositionSnap);
            foreach (KeyValuePair<string, LevelTransformData> entry in dragBefore)
            {
                if (!context.Projector.TryGetEntity(entry.Key, out LevelEntityView view))
                {
                    continue;
                }

                LevelTransformData preview = entry.Value;
                preview.position = new Float3Data(
                    entry.Value.position.x + delta.x,
                    entry.Value.position.y + delta.y,
                    entry.Value.position.z + delta.z);
                view.ApplyTransform(preview);
            }

            context.PreviewTransformChanged(primaryView.ReadTransform());
        }

        private void CommitDrag()
        {
            dragging = false;
            DragFeedback = string.Empty;
            var commands = new List<ILevelEditCommand>();
            foreach (KeyValuePair<string, LevelTransformData> entry in dragBefore)
            {
                if (context.Projector.TryGetEntity(entry.Key, out LevelEntityView view))
                {
                    LevelTransformData after = view.ReadTransform();
                    if (!TransformsEqual(entry.Value, after))
                    {
                        commands.Add(new SetEntityTransformCommand(entry.Key, entry.Value, after));
                    }
                }
            }

            dragBefore.Clear();
            ExecuteSelectionCommands("Move entities", commands);
            if (commands.Count > 0)
            {
                context.SetStatus(commands.Count == 1 ? "Moved entity." : "Moved selected entities.");
            }
        }

        private void RestoreDragPreview()
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            DragFeedback = string.Empty;
            if (context != null)
            {
                foreach (KeyValuePair<string, LevelTransformData> entry in dragBefore)
                {
                    if (context.Projector.TryGetEntity(entry.Key, out LevelEntityView view))
                    {
                        view.ApplyTransform(entry.Value);
                    }
                }

                if (dragBefore.TryGetValue(context.Selection.PrimaryEntityId, out LevelTransformData primary))
                {
                    context.PreviewTransformChanged(primary);
                }
            }

            dragBefore.Clear();
        }

        private string BuildDragFeedback(Vector3 delta, float snapStep)
        {
            string snap = context.SnapSettings.Enabled
                ? $"SNAP {snapStep:0.###}"
                : "SNAP OFF";
            return $"MOVE X {delta.x:+0.###;-0.###;0}  Z {delta.z:+0.###;-0.###;0}  ·  {snap}  ·  ESC CANCEL";
        }

        public void RotateSelection(float? amount = null)
        {
            IReadOnlyList<string> entityIds = SelectedEntityIds();
            if (entityIds.Count == 0)
            {
                return;
            }

            var commands = new List<ILevelEditCommand>();
            foreach (string entityId in entityIds)
            {
                LevelEntity entity = context.Workspace.FindEntitySnapshot(entityId);
                if (entity == null
                    || !context.Projector.TryGetEntity(entityId, out LevelEntityView view))
                {
                    continue;
                }

                LevelTransformData after = entity.transform;
                after.yawDegrees = NormalizeYaw(
                    after.yawDegrees + (amount ?? view.Archetype.PlacementRules.AngleSnap));
                commands.Add(new SetEntityTransformCommand(entity.id, entity.transform, after));
            }

            if (commands.Count == 0)
            {
                return;
            }

            ExecuteSelectionCommands("Rotate entities", commands);
            context.SetStatus(commands.Count == 1 ? "Rotated entity." : "Rotated selected entities.");
        }

        public void DeleteSelection()
        {
            IReadOnlyList<string> entityIds = SelectedEntityIds();
            if (entityIds.Count == 0)
            {
                return;
            }

            context.Selection.Clear();
            var commands = entityIds
                .Select(entityId => (ILevelEditCommand)new DeleteEntityCommand(entityId))
                .ToArray();
            ExecuteSelectionCommands("Delete entities", commands);
            context.SetStatus(commands.Length == 1 ? "Deleted entity." : "Deleted selected entities.");
        }

        public void CopySelection()
        {
            clipboard.Clear();
            foreach (string entityId in SelectedEntityIds())
            {
                LevelEntity entity = context.Workspace.FindEntitySnapshot(entityId);
                if (entity != null)
                {
                    clipboard.Add(entity);
                }
            }

            pasteCount = 0;
            if (clipboard.Count > 0)
            {
                context.SetStatus(clipboard.Count == 1 ? "Copied entity." : "Copied selected entities.");
            }
        }

        public void PasteClipboard()
        {
            if (clipboard.Count == 0)
            {
                context.SetStatus("Copy one or more entities before pasting.");
                return;
            }

            PasteEntities(clipboard, ++pasteCount);
        }

        public void DuplicateSelection()
        {
            var source = new List<LevelEntity>();
            foreach (string entityId in SelectedEntityIds())
            {
                LevelEntity entity = context.Workspace.FindEntitySnapshot(entityId);
                if (entity != null)
                {
                    source.Add(entity);
                }
            }

            if (source.Count > 0)
            {
                PasteEntities(source, 1);
            }
        }

        private IReadOnlyList<string> SelectedEntityIds()
        {
            return context.Selection.Targets
                .Where(target => target.Kind == LevelSelectionKind.Entity)
                .Select(target => target.EntityId)
                .Distinct()
                .ToArray();
        }

        private void ExecuteSelectionCommands(
            string transactionDescription,
            IReadOnlyCollection<ILevelEditCommand> commands)
        {
            if (commands.Count == 0)
            {
                return;
            }

            if (commands.Count == 1)
            {
                context.Workspace.Execute(commands.First());
            }
            else
            {
                context.Workspace.ExecuteTransaction(transactionDescription, commands);
            }
        }

        private void PasteEntities(IReadOnlyList<LevelEntity> source, int offsetMultiplier)
        {
            var commands = new List<ILevelEditCommand>();
            var replacementSelection = new List<LevelSelectionTarget>();
            float offset = 0.5f * offsetMultiplier;
            foreach (LevelEntity sourceEntity in source)
            {
                LevelEntity copy = sourceEntity.DeepCopy();
                copy.id = LevelDocumentFactory.NewStableId();
                copy.transform.position = new Float3Data(
                    copy.transform.position.x + offset,
                    copy.transform.position.y,
                    copy.transform.position.z + offset);
                commands.Add(new AddEntityCommand(copy));
                replacementSelection.Add(new LevelSelectionTarget(copy.id));
            }

            ExecuteSelectionCommands("Paste entities", commands);
            context.Selection.Set(replacementSelection);
            context.SetStatus(commands.Count == 1 ? "Pasted entity." : "Pasted selected entities.");
        }

        private static float NormalizeYaw(float value)
        {
            return Mathf.Repeat(value + 180f, 360f) - 180f;
        }

        private static bool TransformsEqual(LevelTransformData left, LevelTransformData right)
        {
            return Mathf.Approximately(left.position.x, right.position.x)
                && Mathf.Approximately(left.position.y, right.position.y)
                && Mathf.Approximately(left.position.z, right.position.z)
                && Mathf.Approximately(left.yawDegrees, right.yawDegrees);
        }
    }
}
