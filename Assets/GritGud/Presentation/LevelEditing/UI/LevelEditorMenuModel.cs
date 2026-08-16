using System;
using System.Collections.Generic;

namespace GritGud.Presentation.LevelEditing.UI
{
    public enum LevelEditorMenuKind
    {
        None,
        File,
        Edit,
        View,
        Camera,
    }

    public sealed class LevelEditorMenuItem
    {
        private LevelEditorMenuItem(bool separator)
        {
            IsSeparator = separator;
            Label = string.Empty;
            Enabled = false;
            Execute = null;
        }

        public LevelEditorMenuItem(
            string label,
            bool enabled,
            Action execute,
            bool selected = false,
            bool destructive = false,
            string shortcut = "")
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Enabled = enabled;
            Execute = execute;
            Selected = selected;
            Destructive = destructive;
            Shortcut = shortcut ?? string.Empty;
        }

        public static LevelEditorMenuItem Separator { get; } =
            new LevelEditorMenuItem(true);

        public string Label { get; }

        public bool Enabled { get; }

        public Action Execute { get; }

        public bool Selected { get; }

        public bool Destructive { get; }

        public string Shortcut { get; }

        public bool IsSeparator { get; }
    }

    public sealed class LevelEditorMenuModel
    {
        public LevelEditorMenuModel(
            LevelEditorMenuKind kind,
            IReadOnlyList<LevelEditorMenuItem> items)
        {
            Kind = kind;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public LevelEditorMenuKind Kind { get; }

        public IReadOnlyList<LevelEditorMenuItem> Items { get; }
    }
}
