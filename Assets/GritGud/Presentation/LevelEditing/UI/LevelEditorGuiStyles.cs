using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed class LevelEditorGuiStyles
    {
        private GUISkin sourceSkin;
        private GUIStyle sectionHeader;

        public GUIStyle SectionHeader
        {
            get
            {
                EnsureCurrentSkin();
                return sectionHeader;
            }
        }

        private void EnsureCurrentSkin()
        {
            if (ReferenceEquals(sourceSkin, GUI.skin) && sectionHeader != null)
                return;

            sourceSkin = GUI.skin;
            sectionHeader = new GUIStyle(sourceSkin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(
                    LevelEditorGuiMetrics.SectionHeaderLeftPadding,
                    LevelEditorGuiMetrics.SectionHeaderRightPadding,
                    LevelEditorGuiMetrics.SectionHeaderVerticalPadding,
                    LevelEditorGuiMetrics.SectionHeaderVerticalPadding),
            };
            sectionHeader.normal.textColor = LevelEditorTheme.SectionHeaderText;
        }
    }
}
