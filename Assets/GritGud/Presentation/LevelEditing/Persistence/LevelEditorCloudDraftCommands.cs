using System;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Levels;

namespace GritGud.Presentation.LevelEditing.Persistence
{
    internal interface ILevelEditorCloudDraftGateway
    {
        bool IsAvailable { get; }
        string UnavailableStatus { get; }
        string Status { get; }
        LevelDraftRecord ActiveDraft { get; }

        Task<LevelDraftRecord> CreateAsync(
            string name,
            LevelDocument document);

        Task<LevelDraftSummary> SaveAsync(
            LevelDraftId id,
            long expectedRevision,
            LevelDocument document);

        Task<LevelDraftRecord> LoadAsync(
            LevelDraftId id,
            CancellationToken cancellationToken);

        void Adopt(LevelDraftRecord draft);
    }

    internal interface ILevelEditorCloudDraftHost
    {
        bool IsReady { get; }
        int Revision { get; }

        LevelDocument CreateSnapshot();
        void MarkSaved();
        void ApplySavedSource(
            LevelDocument document,
            string sourceLabel);
        void ApplyLoadedSource(
            LevelDocument document,
            string sourceLabel);
        void SetStatus(string message);
    }

    internal sealed class GameBootstrapCloudDraftGateway :
        ILevelEditorCloudDraftGateway
    {
        private readonly GameBootstrap bootstrap;

        public GameBootstrapCloudDraftGateway(GameBootstrap bootstrap)
        {
            this.bootstrap = bootstrap;
        }

        private LevelDraftLibraryCoordinator Library => bootstrap?.DraftLibrary;

        public bool IsAvailable => Library != null;
        public string UnavailableStatus =>
            bootstrap?.Supabase?.Status ?? "Cloud saves are not configured.";
        public string Status => Library?.Status ?? UnavailableStatus;
        public LevelDraftRecord ActiveDraft => bootstrap?.ActiveCloudDraft;

        public Task<LevelDraftRecord> CreateAsync(
            string name,
            LevelDocument document) =>
            RequireLibrary().CreateAsync(name, document);

        public Task<LevelDraftSummary> SaveAsync(
            LevelDraftId id,
            long expectedRevision,
            LevelDocument document) =>
            RequireLibrary().SaveAsync(id, expectedRevision, document);

        public Task<LevelDraftRecord> LoadAsync(
            LevelDraftId id,
            CancellationToken cancellationToken) =>
            RequireLibrary().LoadAsync(id, cancellationToken);

        public void Adopt(LevelDraftRecord draft) =>
            bootstrap.AdoptActiveCloudDraft(draft);

        private LevelDraftLibraryCoordinator RequireLibrary() =>
            Library ?? throw new InvalidOperationException(UnavailableStatus);
    }

    internal sealed class LevelEditorCloudDraftHost :
        ILevelEditorCloudDraftHost
    {
        private readonly LevelEditorWorkspace workspace;
        private readonly Func<bool> isReady;
        private readonly Action<LevelDocument, string> applySavedSource;
        private readonly Action<LevelDocument, string> applyLoadedSource;
        private readonly Action<string> setStatus;

        public LevelEditorCloudDraftHost(
            LevelEditorWorkspace workspace,
            Func<bool> isReady,
            Action<LevelDocument, string> applySavedSource,
            Action<LevelDocument, string> applyLoadedSource,
            Action<string> setStatus)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(
                nameof(workspace));
            this.isReady = isReady ?? throw new ArgumentNullException(
                nameof(isReady));
            this.applySavedSource = applySavedSource
                ?? throw new ArgumentNullException(nameof(applySavedSource));
            this.applyLoadedSource = applyLoadedSource
                ?? throw new ArgumentNullException(nameof(applyLoadedSource));
            this.setStatus = setStatus ?? throw new ArgumentNullException(
                nameof(setStatus));
        }

        public bool IsReady => isReady();
        public int Revision => workspace.Revision;
        public LevelDocument CreateSnapshot() => workspace.CreateSnapshot();
        public void MarkSaved() => workspace.MarkSaved();

        public void ApplySavedSource(
            LevelDocument document,
            string sourceLabel) =>
            applySavedSource(document, sourceLabel);

        public void ApplyLoadedSource(
            LevelDocument document,
            string sourceLabel) =>
            applyLoadedSource(document, sourceLabel);

        public void SetStatus(string message) => setStatus(message);
    }

    internal sealed class LevelEditorCloudDraftCommands : IDisposable
    {
        private readonly ILevelEditorCloudDraftGateway gateway;
        private readonly ILevelEditorCloudDraftHost host;
        private readonly CancellationTokenSource lifetime =
            new CancellationTokenSource();
        private int operationVersion;
        private bool disposed;

        public LevelEditorCloudDraftCommands(
            ILevelEditorCloudDraftGateway gateway,
            ILevelEditorCloudDraftHost host)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(
                nameof(gateway));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool IsRunning { get; private set; }
        public bool HasActiveDraft => gateway.ActiveDraft != null;

        public async Task SaveAsync()
        {
            if (!TryBegin("Saving cloud draft…", out int version))
                return;

            int savedRevision = host.Revision;
            LevelDocument snapshot = host.CreateSnapshot();
            try
            {
                LevelDraftRecord active = gateway.ActiveDraft;
                string sourceLabel = null;
                if (active == null)
                {
                    string name = string.IsNullOrWhiteSpace(snapshot.displayName)
                        ? "Untitled Level"
                        : snapshot.displayName;
                    active = await gateway.CreateAsync(name, snapshot);
                    sourceLabel = "cloud draft: " + active.Summary.Name;
                }
                else
                {
                    LevelDraftSummary summary = await gateway.SaveAsync(
                        active.Summary.Id,
                        active.Summary.Revision,
                        snapshot);
                    active = new LevelDraftRecord(summary, snapshot);
                }

                if (!CanApply(version)) return;
                gateway.Adopt(active);
                host.ApplySavedSource(snapshot, sourceLabel);
                if (host.Revision == savedRevision)
                    host.MarkSaved();
                host.SetStatus(gateway.Status);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (CanApply(version))
                    host.SetStatus(exception.Message);
            }
            finally
            {
                Complete(version);
            }
        }

        public async Task LoadAsync()
        {
            LevelDraftRecord active = gateway.ActiveDraft;
            if (active == null)
            {
                if (!disposed)
                    host.SetStatus("Open a cloud draft before loading it.");
                return;
            }

            if (!TryBegin("Loading cloud draft…", out int version))
                return;

            try
            {
                LevelDraftRecord loaded = await gateway.LoadAsync(
                    active.Summary.Id,
                    lifetime.Token);
                if (!CanApply(version)) return;
                LevelDocument document = loaded.CreateDocumentSnapshot();
                gateway.Adopt(loaded);
                host.ApplyLoadedSource(
                    document,
                    "cloud draft: " + loaded.Summary.Name);
                host.SetStatus("Loaded cloud draft.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (CanApply(version))
                    host.SetStatus(exception.Message);
            }
            finally
            {
                Complete(version);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            operationVersion++;
            IsRunning = false;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private bool TryBegin(string status, out int version)
        {
            version = operationVersion;
            if (disposed || IsRunning)
                return false;
            if (!gateway.IsAvailable || !host.IsReady)
            {
                host.SetStatus(gateway.UnavailableStatus);
                return false;
            }

            version = ++operationVersion;
            IsRunning = true;
            host.SetStatus(status);
            return true;
        }

        private bool CanApply(int version) =>
            !disposed
            && host.IsReady
            && version == operationVersion;

        private void Complete(int version)
        {
            if (version == operationVersion && !disposed)
                IsRunning = false;
        }
    }
}
