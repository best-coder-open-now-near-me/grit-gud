using System;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed class LevelEditorDocumentActionConfirmation
    {
        private Action pendingAction;

        public bool HasPendingAction => pendingAction != null;

        public string Prompt { get; private set; } = string.Empty;

        public void Request(bool isDirty, string prompt, Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (!isDirty)
            {
                action();
                return;
            }

            pendingAction = action;
            Prompt = string.IsNullOrWhiteSpace(prompt)
                ? "Discard the unsaved changes?"
                : prompt.Trim();
        }

        public void ConfirmDiscard()
        {
            Action action = pendingAction;
            Cancel();
            action?.Invoke();
        }

        public void Cancel()
        {
            pendingAction = null;
            Prompt = string.Empty;
        }
    }
}
