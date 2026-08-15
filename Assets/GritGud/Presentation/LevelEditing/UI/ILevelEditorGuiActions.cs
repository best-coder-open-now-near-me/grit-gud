using GritGud.Presentation.LevelEditing.Core;

namespace GritGud.Presentation.LevelEditing.UI
{
    public interface ILevelEditorGuiActions
    {
        bool HasDraft { get; }

        int RecoveryGenerationCount { get; }

        bool HasRecovery(int generation);

        bool UsesBrowserFileDialog { get; }

        string DesktopImportPath { get; set; }

        LevelEditorCameraView CameraView { get; }

        void ReturnToMenu();

        void Undo();

        void Redo();

        void SaveDraft();

        void LoadDraft();

        void LoadRecovery(int generation);

        void Export();

        void RequestImport();

        void TogglePreview();

        void StartTestPlay();

        void CreateNewLevel();

        void ReloadSourceLevel();

        void FrameSelection();

        void FrameLevel();

        void FocusEntity(string entityId);

        void ApplyLevelDisplayName(string displayName);

        void ApplyLevelBounds(LevelBoundsAuthoringRequest request);

        void ConfigureGrid(LevelGridAuthoringRequest request);

        void SetCameraView(LevelEditorCameraView view);

        void DuplicateArray(LevelArrayAuthoringRequest request);

        void ApplyEnvironment(LevelEnvironmentAuthoringRequest request);

        void AddPracticalLight();

        void ApplyPracticalLight(LevelPracticalLightAuthoringRequest request);

        void DeletePracticalLight(string lightId);

        void ApplyEntityTransform(string x, string y, string z, string yaw);

        void ApplyPlayerStart(string x, string y, string z, string yaw);

        void AddInteractionPoint();

        void ApplyInteractionPoint(string type, string x, string y, string z, string radius);

        void DeleteInteractionPoint();

        void ApplyDestructibleDefaults(string enabled, string state, string integrity);

        void AddScenarioActor(string templateId);

        void ApplyScenarioActor(
            string actorId,
            string x,
            string y,
            string z,
            string yaw,
            bool playerControlled,
            bool initiallySelected,
            bool primaryTarget);

        void DeleteScenarioActor(string actorId);

        void PlaceScenarioActorAtView(string actorId);

        void ApplyScenarioProp(
            string entityId,
            bool enabled,
            string mass,
            string sizeClass,
            bool startsEncounter);

        void ApplyScenarioObjective(
            string entityId,
            string pointId,
            bool enabled,
            string displayName,
            string activeText,
            string completedText,
            string actionPointCost,
            string movementOpportunityCost,
            string mobility);

        void ApplyScenarioVehicle(
            string entityId,
            bool enabled,
            string maximumSpeed,
            string acceleration,
            string braking,
            string lowSpeedTurn,
            string highSpeedTurn,
            string baseRadius,
            string radiusFactor,
            string startingSpeed,
            string occupantActorId,
            bool startsEncounter);
    }
}
