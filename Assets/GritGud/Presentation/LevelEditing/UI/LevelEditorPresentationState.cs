using System;
using GritGud.Application.Levels;

namespace GritGud.Presentation.LevelEditing.UI
{
    public enum LevelEditorWorkspacePage
    {
        Create,
        Outline,
        Scenario,
        Environment,
        Dressing,
    }

    public enum LevelEditorCreateMode
    {
        Select,
        Place,
        Terrain,
    }

    public enum LevelEditorInspectorTargetKind
    {
        None,
        Entity,
        InteractionPoint,
        ScenarioActor,
    }

    public enum LevelEditorInspectorPage
    {
        Selection,
        Gameplay,
        Level,
    }

    public readonly struct LevelEditorInspectorTarget : IEquatable<LevelEditorInspectorTarget>
    {
        public LevelEditorInspectorTarget(
            LevelEditorInspectorTargetKind kind,
            string targetId,
            string ownerId = null)
        {
            Kind = kind;
            TargetId = targetId ?? string.Empty;
            OwnerId = ownerId ?? string.Empty;
        }

        public LevelEditorInspectorTargetKind Kind { get; }

        public string TargetId { get; }

        public string OwnerId { get; }

        public bool Equals(LevelEditorInspectorTarget other)
        {
            return Kind == other.Kind
                && string.Equals(TargetId, other.TargetId, StringComparison.Ordinal)
                && string.Equals(OwnerId, other.OwnerId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is LevelEditorInspectorTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TargetId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(OwnerId);
                return hash;
            }
        }
    }

    public sealed class LevelEditorPresentationState
    {
        public event Action Changed;

        public LevelEditorWorkspacePage Page { get; private set; } =
            LevelEditorWorkspacePage.Create;

        public LevelEditorCreateMode CreateMode { get; private set; } =
            LevelEditorCreateMode.Select;

        public LevelEditorInspectorTarget InspectorTarget { get; private set; }

        public LevelEditorInspectorPage InspectorPage { get; private set; } =
            LevelEditorInspectorPage.Selection;

        public void ShowPage(LevelEditorWorkspacePage page)
        {
            if (Page == page)
                return;
            Page = page;
            Changed?.Invoke();
        }

        public void ShowCreateMode(LevelEditorCreateMode mode)
        {
            bool changed = Page != LevelEditorWorkspacePage.Create || CreateMode != mode;
            Page = LevelEditorWorkspacePage.Create;
            CreateMode = mode;
            if (changed)
                Changed?.Invoke();
        }

        public void SynchronizeCreateMode(LevelEditorCreateMode mode)
        {
            if (CreateMode == mode)
                return;
            CreateMode = mode;
            Changed?.Invoke();
        }

        public void FocusWorldSelection(LevelSelectionTarget? target)
        {
            LevelEditorInspectorTarget replacement = target == null
                ? default
                : target.Value.Kind == LevelSelectionKind.InteractionPoint
                    ? new LevelEditorInspectorTarget(
                        LevelEditorInspectorTargetKind.InteractionPoint,
                        target.Value.ElementId,
                        target.Value.EntityId)
                    : new LevelEditorInspectorTarget(
                        LevelEditorInspectorTargetKind.Entity,
                        target.Value.EntityId);
            SetInspectorTarget(replacement);
            ShowInspectorPage(LevelEditorInspectorPage.Selection);
        }

        public void FocusScenarioActor(string actorId)
        {
            SetInspectorTarget(string.IsNullOrWhiteSpace(actorId)
                ? default
                : new LevelEditorInspectorTarget(
                    LevelEditorInspectorTargetKind.ScenarioActor,
                    actorId));
            ShowInspectorPage(LevelEditorInspectorPage.Gameplay);
        }

        public void ShowInspectorPage(LevelEditorInspectorPage page)
        {
            if (InspectorPage == page)
                return;
            InspectorPage = page;
            Changed?.Invoke();
        }

        public void ClearInspectorFocus()
        {
            SetInspectorTarget(default);
        }

        private void SetInspectorTarget(LevelEditorInspectorTarget replacement)
        {
            if (InspectorTarget.Equals(replacement))
                return;
            InspectorTarget = replacement;
            Changed?.Invoke();
        }
    }
}
