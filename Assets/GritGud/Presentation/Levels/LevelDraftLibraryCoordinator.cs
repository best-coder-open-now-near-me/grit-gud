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
        private int mutationVersion;
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

        public Task Refresh() => RefreshAsync();

        public async Task RefreshAsync()
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
            int version = ApplyUpsert(created.Summary, created.Summary.Id);
            await RefreshAfterMutation(
                version,
                created.Summary.Id,
                "Created cloud draft.",
                committed: created.Summary);
            return created;
        }

        public async Task<LevelDraftSummary> SaveAsync(LevelDraftId id, long expectedRevision, LevelDocument document)
        {
            ThrowIfDisposed();
            LevelDraftSummary saved = await service.SaveAsync(id, expectedRevision, document, lifetime.Token);
            int version = ApplyUpsert(saved, saved.Id);
            await RefreshAfterMutation(
                version,
                saved.Id,
                "Saved cloud draft.",
                committed: saved);
            return saved;
        }

        public async Task RenameAsync(LevelDraftId id, string name)
        {
            ThrowIfDisposed();
            LevelDraftSummary renamed = await service.RenameAsync(id, name, lifetime.Token);
            int version = ApplyUpsert(renamed, renamed.Id);
            await RefreshAfterMutation(
                version,
                renamed.Id,
                "Renamed cloud draft.",
                committed: renamed);
        }

        public async Task<LevelDraftRecord> DuplicateAsync(LevelDraftId id, string name)
        {
            ThrowIfDisposed();
            LevelDraftRecord duplicate = await service.DuplicateAsync(id, name, lifetime.Token);
            int version = ApplyUpsert(
                duplicate.Summary,
                duplicate.Summary.Id);
            await RefreshAfterMutation(
                version,
                duplicate.Summary.Id,
                "Duplicated cloud draft.",
                committed: duplicate.Summary);
            return duplicate;
        }

        public async Task DeleteAsync(LevelDraftId id)
        {
            ThrowIfDisposed();
            await service.DeleteAsync(id, lifetime.Token);
            int version = ApplyDelete(id);
            await RefreshAfterMutation(
                version,
                selection: null,
                status: "Archived cloud draft.",
                deletedId: id);
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

        private async Task RefreshAfterMutation(
            int version,
            LevelDraftId? selection,
            string status,
            LevelDraftSummary committed = null,
            LevelDraftId? deletedId = null)
        {
            try
            {
                IReadOnlyList<LevelDraftSummary> refreshed =
                    await service.ListAsync(lifetime.Token);
                if (disposed || version != mutationVersion)
                    return;
                drafts = ReconcileRefresh(refreshed, committed, deletedId);
                selectedId = selection;
                HasLoaded = true;
                Status = status;
                Publish();
            }
            catch (Exception exception)
            {
                if (disposed || version != mutationVersion)
                    return;
                Status = status + " The write succeeded, but the cloud draft list "
                    + "could not be refreshed: " + DescribeRefreshFailure(exception);
                Publish();
            }
        }

        private int ApplyUpsert(
            LevelDraftSummary committed,
            LevelDraftId selection)
        {
            var updated = new List<LevelDraftSummary>(drafts.Count + 1);
            bool replaced = false;
            foreach (LevelDraftSummary draft in drafts)
            {
                if (draft.Id.Equals(committed.Id))
                {
                    if (!replaced)
                        updated.Add(committed);
                    replaced = true;
                    continue;
                }

                updated.Add(draft);
            }

            if (!replaced)
                updated.Add(committed);
            drafts = SortDrafts(updated);
            selectedId = selection;
            HasLoaded = true;
            CancelPendingRefresh();
            Status = string.Empty;
            int version = ++mutationVersion;
            Publish();
            return version;
        }

        private int ApplyDelete(LevelDraftId deletedId)
        {
            drafts = drafts
                .Where(draft => !draft.Id.Equals(deletedId))
                .ToArray();
            if (selectedId.HasValue && selectedId.Value.Equals(deletedId))
                selectedId = null;
            HasLoaded = true;
            CancelPendingRefresh();
            Status = string.Empty;
            int version = ++mutationVersion;
            Publish();
            return version;
        }

        private static IReadOnlyList<LevelDraftSummary> ReconcileRefresh(
            IReadOnlyList<LevelDraftSummary> refreshed,
            LevelDraftSummary committed,
            LevelDraftId? deletedId)
        {
            var reconciled = new List<LevelDraftSummary>(
                refreshed ?? Array.Empty<LevelDraftSummary>());
            if (deletedId.HasValue)
            {
                reconciled.RemoveAll(draft => draft.Id.Equals(deletedId.Value));
            }
            if (committed != null)
            {
                int index = reconciled.FindIndex(draft =>
                    draft.Id.Equals(committed.Id));
                if (index < 0)
                {
                    reconciled.Add(committed);
                }
                else if (reconciled[index].Revision <= committed.Revision)
                {
                    reconciled[index] = committed;
                }
            }

            return SortDrafts(reconciled);
        }

        private static IReadOnlyList<LevelDraftSummary> SortDrafts(
            IEnumerable<LevelDraftSummary> values) =>
            values
                .OrderByDescending(draft => draft.UpdatedAt)
                .ThenBy(draft => draft.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        private static string DescribeRefreshFailure(Exception exception) =>
            exception is OperationCanceledException
                ? "refresh cancelled."
                : (string.IsNullOrWhiteSpace(exception.Message)
                    ? "unknown refresh failure."
                    : exception.Message);

        private void CancelPendingRefresh()
        {
            operationVersion++;
            IsBusy = false;
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
