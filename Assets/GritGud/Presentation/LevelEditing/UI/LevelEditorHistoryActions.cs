using System;
using GritGud.Application.Levels;

namespace GritGud.Presentation.LevelEditing.UI
{
    internal sealed class LevelEditorHistoryActions : ILevelEditorHistoryActions
    {
        private readonly LevelEditorWorkspace workspace;

        public LevelEditorHistoryActions(LevelEditorWorkspace workspace)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(
                nameof(workspace));
        }

        public void Undo() => workspace.Undo();
        public void Redo() => workspace.Redo();
    }
}
