using System;
using UnityEngine;

namespace GritGud.Presentation.Persistence
{
    public sealed class TextFileImportReceiver : MonoBehaviour
    {
        private TextFileImportController controller;

        public bool UsesBrowserFileDialog => GetController().UsesBrowserFileDialog;

        private void Awake()
        {
            GetController();
        }

        public long RequestImport(
            string desktopImportPath,
            Action<string> succeeded,
            Action<string> failed) =>
            GetController().RequestImport(
                desktopImportPath,
                succeeded,
                failed);

        public void CancelImport(long requestId)
        {
            GetController().Cancel(requestId);
        }

        public void ReceiveImportedText(string payload)
        {
            if (!TextFileImportMessage.TryDecode(
                    payload,
                    out long requestId,
                    out string text))
            {
                Debug.LogWarning(
                    "Ignored a malformed browser text-import callback.",
                    this);
                return;
            }

            GetController().Complete(requestId, text);
        }

        public void ReceiveTextImportError(string payload)
        {
            if (!TextFileImportMessage.TryDecode(
                    payload,
                    out long requestId,
                    out string message))
            {
                Debug.LogWarning(
                    "Ignored a malformed browser text-import failure.",
                    this);
                return;
            }

            GetController().Fail(
                requestId,
                string.IsNullOrWhiteSpace(message)
                    ? "Browser import failed."
                    : message);
        }

        internal void UsePlatformForTests(ITextFileImportPlatform platform)
        {
            controller = new TextFileImportController(
                platform,
                gameObject.name);
        }

        private TextFileImportController GetController() =>
            controller ??= new TextFileImportController(
                TextFileImportPlatformFactory.Create(),
                gameObject.name);
    }
}
