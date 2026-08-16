using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed class LevelEditorViewState
    {
        public LevelEditorViewState(
            LevelDocument document,
            int revision,
            bool canUndo,
            bool canRedo,
            bool isDirty,
            bool previewMode,
            LevelEntityView selectedView,
            IReadOnlyList<LevelValidationIssue> validationIssues,
            string statusMessage)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Revision = revision;
            CanUndo = canUndo;
            CanRedo = canRedo;
            IsDirty = isDirty;
            PreviewMode = previewMode;
            SelectedView = selectedView;
            ValidationIssues = validationIssues ?? Array.Empty<LevelValidationIssue>();
            StatusMessage = statusMessage ?? string.Empty;
        }

        public LevelDocument Document { get; }
        public int Revision { get; }
        public bool CanUndo { get; }
        public bool CanRedo { get; }
        public bool IsDirty { get; }
        public bool PreviewMode { get; }
        public LevelEntityView SelectedView { get; }
        public IReadOnlyList<LevelValidationIssue> ValidationIssues { get; }
        public string StatusMessage { get; }
    }
}
