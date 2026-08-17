using GritGud.Presentation.LevelEditing.UI;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorShellLayoutTests
    {
        [Test]
        public void EmbeddedWebLayoutKeepsInspectorClosedViewportUsable()
        {
            LevelEditorShellLayout layout = LevelEditorShellLayout.Calculate(
                960f,
                previewMode: false,
                showLeftPanel: true,
                showInspector: false);

            Assert.That(layout.IsCompact, Is.True);
            Assert.That(layout.LeftPanelWidth, Is.EqualTo(280f));
            Assert.That(layout.InspectorWidth, Is.Zero);
            Assert.That(layout.ViewportWidth, Is.EqualTo(680f));
        }

        [Test]
        public void WideLayoutSupportsBothPanelsWithoutStarvingViewport()
        {
            LevelEditorShellLayout layout = LevelEditorShellLayout.Calculate(
                1920f,
                previewMode: false,
                showLeftPanel: true,
                showInspector: true);

            Assert.That(layout.IsCompact, Is.False);
            Assert.That(layout.ViewportWidth, Is.EqualTo(1300f));
        }

        [Test]
        public void PreviewUsesTheCompleteScreenWidth()
        {
            LevelEditorShellLayout layout = LevelEditorShellLayout.Calculate(
                1024f,
                previewMode: true,
                showLeftPanel: true,
                showInspector: true);

            Assert.That(layout.LeftPanelWidth, Is.Zero);
            Assert.That(layout.InspectorWidth, Is.Zero);
            Assert.That(layout.ViewportWidth, Is.EqualTo(1024f));
        }
    }
}
