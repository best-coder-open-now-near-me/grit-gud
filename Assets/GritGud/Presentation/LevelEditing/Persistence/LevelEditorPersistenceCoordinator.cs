using System;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Levels.Persistence;

namespace GritGud.Presentation.LevelEditing.Persistence
{
    public sealed class LevelDocumentLoadedEventArgs : EventArgs
    {
        public LevelDocumentLoadedEventArgs(LevelDocument document, string sourceLabel)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            SourceLabel = sourceLabel ?? throw new ArgumentNullException(nameof(sourceLabel));
        }

        public LevelDocument Document { get; }

        public string SourceLabel { get; }
    }

    public sealed class LevelEditorPersistenceCoordinator : IDisposable
    {
        private const string DraftSlot = "active";

        private readonly UnityLevelJsonSerializer serializer;
        private readonly ILevelDraftStore draftStore;
        private readonly LevelTextTransfer textTransfer;
        private readonly LevelValidationContent validationContent;
        private bool disposed;

        public LevelEditorPersistenceCoordinator(
            UnityLevelJsonSerializer serializer,
            ILevelDraftStore draftStore,
            LevelTextTransfer textTransfer,
            LevelValidationContent validationContent)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
            this.textTransfer = textTransfer ?? throw new ArgumentNullException(nameof(textTransfer));
            this.validationContent = validationContent
                ?? throw new ArgumentNullException(nameof(validationContent));
            textTransfer.ImportCompleted += HandleImportedText;
            textTransfer.ImportFailed += HandleImportFailure;
        }

        public event EventHandler<LevelDocumentLoadedEventArgs> DocumentLoaded;

        public event Action<string> StatusChanged;

        public bool HasDraft => draftStore.HasDraft(DraftSlot);

        public bool UsesBrowserFileDialog => textTransfer.UsesBrowserFileDialog;

        public string DesktopImportPath
        {
            get => textTransfer.DesktopImportPath;
            set => textTransfer.DesktopImportPath = value;
        }

        public LevelDocument Deserialize(string text)
        {
            ThrowIfDisposed();
            return serializer.Deserialize(text);
        }

        public void SaveDraft(LevelEditorWorkspace workspace)
        {
            ThrowIfDisposed();
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            try
            {
                draftStore.SaveDraft(DraftSlot, serializer.Serialize(workspace.CreateSnapshot()));
                workspace.MarkSaved();
                Report("Saved the active browser/local draft.");
            }
            catch (Exception exception)
            {
                Report(exception.Message);
            }
        }

        public void LoadDraft()
        {
            ThrowIfDisposed();
            try
            {
                AdoptSerializedDocument(
                    draftStore.LoadDraft(DraftSlot),
                    "draft",
                    requireAuthoringValidity: false);
            }
            catch (Exception exception)
            {
                Report(exception.Message);
            }
        }

        public void Export(LevelEditorWorkspace workspace)
        {
            ThrowIfDisposed();
            if (!CanPublish(workspace, "exporting the level"))
            {
                return;
            }

            try
            {
                LevelDocument snapshot = workspace.CreateSnapshot();
                Report(textTransfer.Export(
                    snapshot.displayName,
                    serializer.Serialize(snapshot)));
            }
            catch (Exception exception)
            {
                Report(exception.Message);
            }
        }

        public void RequestImport()
        {
            ThrowIfDisposed();
            try
            {
                textTransfer.RequestImport();
                if (textTransfer.UsesBrowserFileDialog)
                {
                    Report("Choose a portable level JSON file.");
                }
            }
            catch (Exception exception)
            {
                Report(exception.Message);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            textTransfer.ImportCompleted -= HandleImportedText;
            textTransfer.ImportFailed -= HandleImportFailure;
            disposed = true;
        }

        private bool CanPublish(LevelEditorWorkspace workspace, string operation)
        {
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (!LevelValidator.HasErrors(workspace.Validate(LevelValidationProfile.Publish)))
            {
                return true;
            }

            Report($"Fix validation errors before {operation}.");
            return false;
        }

        private void HandleImportedText(string text)
        {
            try
            {
                AdoptSerializedDocument(
                    text,
                    "import",
                    requireAuthoringValidity: true);
            }
            catch (Exception exception)
            {
                Report(exception.Message);
            }
        }

        private void HandleImportFailure(string message)
        {
            Report(message);
        }

        private void AdoptSerializedDocument(
            string text,
            string sourceLabel,
            bool requireAuthoringValidity)
        {
            LevelDocument imported = serializer.Deserialize(text);
            if (requireAuthoringValidity)
            {
                var issues = LevelValidator.Validate(
                    imported,
                    validationContent,
                    LevelValidationProfile.Authoring);
                if (LevelValidator.HasErrors(issues))
                {
                    string firstError = issues
                        .First(issue => issue.Severity == LevelValidationSeverity.Error)
                        .Message;
                    throw new LevelSerializationException(
                        $"The {sourceLabel} was not loaded: {firstError}");
                }
            }

            DocumentLoaded?.Invoke(
                this,
                new LevelDocumentLoadedEventArgs(imported, sourceLabel));
        }

        private void Report(string message)
        {
            StatusChanged?.Invoke(message ?? string.Empty);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LevelEditorPersistenceCoordinator));
            }
        }
    }
}
