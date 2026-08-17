using System;
using System.Globalization;

namespace GritGud.Presentation.Persistence
{
    internal enum TextFileImportStartStatus
    {
        Failed,
        Pending,
        Succeeded,
    }

    internal readonly struct TextFileImportStartResult
    {
        private TextFileImportStartResult(
            TextFileImportStartStatus status,
            string value)
        {
            Status = status;
            Value = value ?? string.Empty;
        }

        public TextFileImportStartStatus Status { get; }

        public string Value { get; }

        public static TextFileImportStartResult Pending() =>
            new TextFileImportStartResult(
                TextFileImportStartStatus.Pending,
                string.Empty);

        public static TextFileImportStartResult Succeeded(string text) =>
            new TextFileImportStartResult(
                TextFileImportStartStatus.Succeeded,
                text);

        public static TextFileImportStartResult Failed(string message) =>
            new TextFileImportStartResult(
                TextFileImportStartStatus.Failed,
                message);
    }

    internal sealed class TextFileImportController
    {
        private readonly ITextFileImportPlatform platform;
        private readonly string receiverGameObjectName;
        private long nextRequestId;
        private long pendingRequestId;
        private TextFileImportCompletion pendingCompletion;

        public TextFileImportController(
            ITextFileImportPlatform platform,
            string receiverGameObjectName)
        {
            this.platform = platform ?? throw new ArgumentNullException(
                nameof(platform));
            this.receiverGameObjectName = string.IsNullOrWhiteSpace(
                receiverGameObjectName)
                ? throw new ArgumentException(
                    "A browser import receiver name is required.",
                    nameof(receiverGameObjectName))
                : receiverGameObjectName;
        }

        public bool UsesBrowserFileDialog => platform.UsesBrowserFileDialog;

        public bool HasPendingRequest => pendingCompletion != null;

        public long RequestImport(
            string desktopImportPath,
            Action<string> succeeded,
            Action<string> failed)
        {
            var completion = new TextFileImportCompletion(succeeded, failed);
            if (HasPendingRequest)
            {
                completion.Fail("A text-file import is already in progress.");
                return 0;
            }

            long requestId = ++nextRequestId;
            pendingRequestId = requestId;
            pendingCompletion = completion;
            TextFileImportStartResult result;
            try
            {
                result = platform.Start(
                    requestId,
                    desktopImportPath,
                    receiverGameObjectName);
            }
            catch (Exception exception)
            {
                Fail(requestId, exception.Message);
                return 0;
            }

            switch (result.Status)
            {
                case TextFileImportStartStatus.Pending:
                    return requestId;
                case TextFileImportStartStatus.Succeeded:
                    Complete(requestId, result.Value);
                    return 0;
                default:
                    Fail(requestId, result.Value);
                    return 0;
            }
        }

        public bool Complete(long requestId, string text)
        {
            TextFileImportCompletion completion = Take(requestId);
            if (completion == null)
                return false;
            completion.Succeed(text ?? string.Empty);
            return true;
        }

        public bool Fail(long requestId, string message)
        {
            TextFileImportCompletion completion = Take(requestId);
            if (completion == null)
                return false;
            completion.Fail(string.IsNullOrWhiteSpace(message)
                ? "Text-file import failed."
                : message);
            return true;
        }

        public bool Cancel(long requestId) =>
            Fail(requestId, "Text-file import was cancelled.");

        private TextFileImportCompletion Take(long requestId)
        {
            if (pendingCompletion == null || requestId != pendingRequestId)
                return null;
            TextFileImportCompletion completion = pendingCompletion;
            pendingCompletion = null;
            pendingRequestId = 0;
            return completion;
        }
    }

    internal sealed class TextFileImportCompletion
    {
        private readonly Action<string> succeeded;
        private readonly Action<string> failed;

        public TextFileImportCompletion(
            Action<string> succeeded,
            Action<string> failed)
        {
            this.succeeded = succeeded ?? throw new ArgumentNullException(
                nameof(succeeded));
            this.failed = failed ?? throw new ArgumentNullException(
                nameof(failed));
        }

        public bool IsCompleted { get; private set; }

        public void Succeed(string text)
        {
            if (IsCompleted)
                return;
            IsCompleted = true;
            succeeded(text);
        }

        public void Fail(string message)
        {
            if (IsCompleted)
                return;
            IsCompleted = true;
            failed(message);
        }
    }

    internal static class TextFileImportMessage
    {
        public static bool TryDecode(
            string payload,
            out long requestId,
            out string value)
        {
            requestId = 0;
            value = string.Empty;
            int separator = payload?.IndexOf('\n') ?? -1;
            if (separator <= 0
                || !long.TryParse(
                    payload.Substring(0, separator),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out requestId)
                || requestId <= 0)
            {
                return false;
            }

            value = payload.Substring(separator + 1);
            return true;
        }
    }
}
