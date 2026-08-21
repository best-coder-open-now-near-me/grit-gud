using System;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Levels;
using GritGud.Presentation.Levels;

namespace GritGud.Presentation.Bootstrap
{
    internal interface ICloudDraftNavigationGateway
    {
        bool IsAvailable { get; }
        string UnavailableStatus { get; }
        Task<LevelDraftRecord> LoadAsync(
            LevelDraftId id,
            CancellationToken cancellationToken);
    }

    internal interface ICloudDraftNavigationHost
    {
        bool CanStartGameplay { get; }
        bool IsMenuActive { get; }
        void Play(LevelDraftRecord draft);
        void Edit(LevelDraftRecord draft);
    }

    internal sealed class GameBootstrapCloudDraftNavigationGateway :
        ICloudDraftNavigationGateway
    {
        private readonly Func<LevelDraftLibraryCoordinator> resolveLibrary;
        private readonly Func<string> resolveUnavailableStatus;

        public GameBootstrapCloudDraftNavigationGateway(
            Func<LevelDraftLibraryCoordinator> resolveLibrary,
            Func<string> resolveUnavailableStatus)
        {
            this.resolveLibrary = resolveLibrary ?? throw new ArgumentNullException(
                nameof(resolveLibrary));
            this.resolveUnavailableStatus = resolveUnavailableStatus
                ?? throw new ArgumentNullException(nameof(resolveUnavailableStatus));
        }

        private LevelDraftLibraryCoordinator Library => resolveLibrary();

        public bool IsAvailable => Library != null;
        public string UnavailableStatus =>
            resolveUnavailableStatus() ?? "Cloud saves are not configured.";

        public Task<LevelDraftRecord> LoadAsync(
            LevelDraftId id,
            CancellationToken cancellationToken) =>
            (Library ?? throw new InvalidOperationException(UnavailableStatus))
                .LoadAsync(id, cancellationToken);
    }

    internal sealed class GameBootstrapCloudDraftNavigationHost :
        ICloudDraftNavigationHost
    {
        private readonly Func<bool> canStartGameplay;
        private readonly Func<bool> isMenuActive;
        private readonly Action<LevelDraftRecord> play;
        private readonly Action<LevelDraftRecord> edit;

        public GameBootstrapCloudDraftNavigationHost(
            Func<bool> canStartGameplay,
            Func<bool> isMenuActive,
            Action<LevelDraftRecord> play,
            Action<LevelDraftRecord> edit)
        {
            this.canStartGameplay = canStartGameplay
                ?? throw new ArgumentNullException(nameof(canStartGameplay));
            this.isMenuActive = isMenuActive
                ?? throw new ArgumentNullException(nameof(isMenuActive));
            this.play = play ?? throw new ArgumentNullException(nameof(play));
            this.edit = edit ?? throw new ArgumentNullException(nameof(edit));
        }

        public bool CanStartGameplay => canStartGameplay();
        public bool IsMenuActive => isMenuActive();
        public void Play(LevelDraftRecord draft) => play(draft);
        public void Edit(LevelDraftRecord draft) => edit(draft);
    }

    internal sealed class CloudDraftNavigationCommands : IDisposable
    {
        private readonly ICloudDraftNavigationGateway gateway;
        private readonly ICloudDraftNavigationHost host;
        private CancellationTokenSource pending;
        private int operationVersion;
        private bool disposed;

        public CloudDraftNavigationCommands(
            ICloudDraftNavigationGateway gateway,
            ICloudDraftNavigationHost host)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(
                nameof(gateway));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool IsRunning { get; private set; }

        public Task PlayAsync(LevelDraftId id, Action<string> reportStatus)
        {
            if (!host.CanStartGameplay)
                return Task.CompletedTask;
            return NavigateAsync(id, reportStatus, host.Play);
        }

        public Task OpenEditorAsync(LevelDraftId id, Action<string> reportStatus) =>
            NavigateAsync(id, reportStatus, host.Edit);

        public void Cancel()
        {
            operationVersion++;
            IsRunning = false;
            pending?.Cancel();
            pending?.Dispose();
            pending = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Cancel();
        }

        private async Task NavigateAsync(
            LevelDraftId id,
            Action<string> reportStatus,
            Action<LevelDraftRecord> navigate)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(CloudDraftNavigationCommands));
            if (!gateway.IsAvailable)
            {
                reportStatus?.Invoke(gateway.UnavailableStatus);
                return;
            }

            int version = BeginOperation(out CancellationToken token);
            try
            {
                LevelDraftRecord draft = await gateway.LoadAsync(id, token);
                if (!IsCurrent(version, token) || !host.IsMenuActive)
                    return;
                navigate(draft);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (IsCurrent(version, token))
                    reportStatus?.Invoke(exception.Message);
            }
            finally
            {
                Complete(version);
            }
        }

        private int BeginOperation(out CancellationToken token)
        {
            Cancel();
            int version = ++operationVersion;
            pending = new CancellationTokenSource();
            token = pending.Token;
            IsRunning = true;
            return version;
        }

        private bool IsCurrent(int version, CancellationToken token) =>
            !disposed
            && !token.IsCancellationRequested
            && version == operationVersion;

        private void Complete(int version)
        {
            if (version != operationVersion)
                return;
            IsRunning = false;
            pending?.Dispose();
            pending = null;
        }
    }
}
