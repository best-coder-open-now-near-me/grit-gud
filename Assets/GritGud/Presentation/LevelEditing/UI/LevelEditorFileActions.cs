using System;
using System.Threading.Tasks;
using GritGud.Application.Levels;
using GritGud.Presentation.LevelEditing.Persistence;

namespace GritGud.Presentation.LevelEditing.UI
{
    internal sealed class LevelEditorFileActions : ILevelEditorFileActions
    {
        private readonly LevelEditorPersistenceCoordinator persistence;
        private readonly LevelEditorWorkspace workspace;
        private readonly LevelEditorCloudDraftCommands cloudCommands;
        private readonly Func<bool> hasCloudDraftContext;
        private readonly Action createNewLevel;
        private readonly Action reloadSourceLevel;
        private Task pendingCloudOperation = Task.CompletedTask;

        public LevelEditorFileActions(
            LevelEditorPersistenceCoordinator persistence,
            LevelEditorWorkspace workspace,
            LevelEditorCloudDraftCommands cloudCommands,
            Func<bool> hasCloudDraftContext,
            Action createNewLevel,
            Action reloadSourceLevel)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(
                nameof(persistence));
            this.workspace = workspace ?? throw new ArgumentNullException(
                nameof(workspace));
            this.cloudCommands = cloudCommands ?? throw new ArgumentNullException(
                nameof(cloudCommands));
            this.hasCloudDraftContext = hasCloudDraftContext
                ?? throw new ArgumentNullException(nameof(hasCloudDraftContext));
            this.createNewLevel = createNewLevel
                ?? throw new ArgumentNullException(nameof(createNewLevel));
            this.reloadSourceLevel = reloadSourceLevel
                ?? throw new ArgumentNullException(nameof(reloadSourceLevel));
        }

        public bool HasDraft => persistence.HasDraft;
        public bool HasCloudDraftContext => hasCloudDraftContext();
        public bool CloudOperationRunning => cloudCommands.IsRunning;
        public int RecoveryGenerationCount =>
            LevelEditorPersistenceCoordinator.RecoveryGenerationCount;
        public bool UsesBrowserFileDialog => persistence.UsesBrowserFileDialog;

        public string DesktopImportPath
        {
            get => persistence.DesktopImportPath;
            set => persistence.DesktopImportPath = value;
        }

        internal Task PendingCloudOperation => pendingCloudOperation;

        public bool HasRecovery(int generation) =>
            persistence.HasRecovery(generation);

        public void SaveDraft() => persistence.SaveDraft(workspace);
        public void SaveToCloud() =>
            pendingCloudOperation = cloudCommands.SaveAsync();
        public void LoadFromCloud() =>
            pendingCloudOperation = cloudCommands.LoadAsync();
        public void LoadDraft() => persistence.LoadDraft();
        public void LoadRecovery(int generation) =>
            persistence.LoadRecovery(generation);
        public void Export() => persistence.Export(workspace);
        public void RequestImport() => persistence.RequestImport();
        public void CreateNewLevel() => createNewLevel();
        public void ReloadSourceLevel() => reloadSourceLevel();
    }
}
