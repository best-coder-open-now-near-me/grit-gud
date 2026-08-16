using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;

namespace GritGud.Presentation.Levels
{
    public sealed class LevelDraftLibraryCoordinator : IDisposable
    {
        private readonly LevelDraftLibraryService service;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private IReadOnlyList<LevelDraftSummary> drafts = Array.Empty<LevelDraftSummary>();
        private LevelDraftId? selectedId;
        private int operationVersion;
        private bool disposed;

        public LevelDraftLibraryCoordinator(LevelDraftLibraryService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public event Action Changed;

        public IReadOnlyList<LevelDraftSummary> Drafts => drafts;
        public LevelDraftId? SelectedId => selectedId;
        public LevelDraftSummary Selected => selectedId.HasValue
            ? drafts.FirstOrDefault(draft => draft.Id.Equals(selectedId.Value))
            : null;
        public bool IsBusy { get; private set; }
        public bool HasLoaded { get; private set; }
        public string Status { get; private set; } = "Cloud drafts have not loaded.";

        public async void Refresh()
        {
            int version = BeginOperation("Loading cloud drafts…");
            try
            {
                IReadOnlyList<LevelDraftSummary> loaded = await service.ListAsync(lifetime.Token);
                if (!IsCurrent(version)) return;
                drafts = loaded ?? Array.Empty<LevelDraftSummary>();
                HasLoaded = true;
                if (selectedId.HasValue && drafts.All(draft => !draft.Id.Equals(selectedId.Value)))
                    selectedId = null;
                CompleteOperation(version, drafts.Count == 0 ? "No cloud drafts yet." : $"Loaded {drafts.Count} cloud draft{(drafts.Count == 1 ? string.Empty : "s")}.");
            }
            catch (Exception exception) { FailOperation(version, exception); }
        }

        public void Select(LevelDraftId id)
        {
            ThrowIfDisposed();
            if (drafts.All(draft => !draft.Id.Equals(id)))
                throw new ArgumentException("The selected cloud draft is not in the library.", nameof(id));
            selectedId = id;
            Status = string.Empty;
            Publish();
        }

        public async Task<LevelDraftRecord> LoadAsync(LevelDraftId id, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!cancellationToken.CanBeCanceled)
                return await service.LoadAsync(id, lifetime.Token);
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken))
                return await service.LoadAsync(id, linked.Token);
        }

        public async Task<LevelDraftRecord> CreateAsync(string name, LevelDocument document)
        {
            ThrowIfDisposed();
            LevelDraftRecord created = await service.CreateAsync(name, document, lifetime.Token);
            selectedId = created.Summary.Id;
            await RefreshAfterMutation(created.Summary.Id, "Created cloud draft.");
            return created;
        }

        public async Task<LevelDraftSummary> SaveAsync(LevelDraftId id, long expectedRevision, LevelDocument document)
        {
            ThrowIfDisposed();
            LevelDraftSummary saved = await service.SaveAsync(id, expectedRevision, document, lifetime.Token);
            selectedId = saved.Id;
            await RefreshAfterMutation(saved.Id, "Saved cloud draft.");
            return saved;
        }

        public async Task RenameAsync(LevelDraftId id, string name)
        {
            ThrowIfDisposed();
            LevelDraftSummary renamed = await service.RenameAsync(id, name, lifetime.Token);
            await RefreshAfterMutation(renamed.Id, "Renamed cloud draft.");
        }

        public async Task<LevelDraftRecord> DuplicateAsync(LevelDraftId id, string name)
        {
            ThrowIfDisposed();
            LevelDraftRecord duplicate = await service.DuplicateAsync(id, name, lifetime.Token);
            selectedId = duplicate.Summary.Id;
            await RefreshAfterMutation(duplicate.Summary.Id, "Duplicated cloud draft.");
            return duplicate;
        }

        public async Task DeleteAsync(LevelDraftId id)
        {
            ThrowIfDisposed();
            await service.DeleteAsync(id, lifetime.Token);
            if (selectedId.HasValue && selectedId.Value.Equals(id)) selectedId = null;
            await RefreshAfterMutation(null, "Archived cloud draft.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            operationVersion++;
            lifetime.Cancel();
            lifetime.Dispose();
            Changed = null;
        }

        private async Task RefreshAfterMutation(LevelDraftId? selection, string status)
        {
            drafts = await service.ListAsync(lifetime.Token);
            selectedId = selection;
            HasLoaded = true;
            IsBusy = false;
            Status = status;
            Publish();
        }

        private int BeginOperation(string status)
        {
            ThrowIfDisposed();
            int version = ++operationVersion;
            IsBusy = true;
            Status = status;
            Publish();
            return version;
        }

        private void CompleteOperation(int version, string status)
        {
            if (!IsCurrent(version)) return;
            IsBusy = false;
            Status = status;
            Publish();
        }

        private void FailOperation(int version, Exception exception)
        {
            if (!IsCurrent(version) || exception is OperationCanceledException) return;
            IsBusy = false;
            Status = exception.Message;
            Publish();
        }

        private bool IsCurrent(int version) => !disposed && version == operationVersion;
        private void Publish() => Changed?.Invoke();
        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LevelDraftLibraryCoordinator));
        }
    }
}
