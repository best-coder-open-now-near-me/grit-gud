using GritGud.Presentation.LevelEditing.UI;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorDocumentActionConfirmationTests
    {
        [Test]
        public void CleanDocumentActionRunsImmediately()
        {
            var confirmation = new LevelEditorDocumentActionConfirmation();
            bool invoked = false;

            confirmation.Request(false, "prompt", () => invoked = true);

            Assert.That(invoked, Is.True);
            Assert.That(confirmation.HasPendingAction, Is.False);
        }

        [Test]
        public void DirtyDocumentActionWaitsForExplicitDiscard()
        {
            var confirmation = new LevelEditorDocumentActionConfirmation();
            bool invoked = false;

            confirmation.Request(true, "Leave this level?", () => invoked = true);

            Assert.That(invoked, Is.False);
            Assert.That(confirmation.HasPendingAction, Is.True);
            Assert.That(confirmation.Prompt, Is.EqualTo("Leave this level?"));

            confirmation.ConfirmDiscard();

            Assert.That(invoked, Is.True);
            Assert.That(confirmation.HasPendingAction, Is.False);
        }

        [Test]
        public void CancelKeepsTheCurrentDocument()
        {
            var confirmation = new LevelEditorDocumentActionConfirmation();
            bool invoked = false;
            confirmation.Request(true, "Replace this level?", () => invoked = true);

            confirmation.Cancel();

            Assert.That(invoked, Is.False);
            Assert.That(confirmation.HasPendingAction, Is.False);
        }
    }
}
