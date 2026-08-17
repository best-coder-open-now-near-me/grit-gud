using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;
using GritGud.Presentation.LevelEditing.Tools;

namespace GritGud.Presentation.LevelEditing.UI
{
    public interface ILevelEditorFileActions
    {
        bool HasDraft { get; }
        bool HasCloudDraftContext { get; }
        bool CloudOperationRunning { get; }
        int RecoveryGenerationCount { get; }
        bool HasRecovery(int generation);
        bool UsesBrowserFileDialog { get; }
        string DesktopImportPath { get; set; }
        void SaveDraft();
        void SaveToCloud();
        void LoadFromCloud();
        void LoadDraft();
        void LoadRecovery(int generation);
        void Export();
        void RequestImport();
        void CreateNewLevel();
        void ReloadSourceLevel();
    }

    public interface ILevelEditorHistoryActions
    {
        void Undo();
        void Redo();
    }

    public interface ILevelEditorSelectionGroupActions
    {
        LevelEditorCameraView CameraView { get; }
        string IsolatedGroupId { get; }
        string SelectionCategoryFilter { get; }
        string SelectionGroupFilter { get; }
        void FrameSelection();
        void FrameLevel();
        void FocusEntity(string entityId);
        void SetCameraView(LevelEditorCameraView view);
        void DuplicateArray(LevelArrayAuthoringRequest request);
        void CreateEntityGroup(string displayName);
        void RenameEntityGroup(string groupId, string displayName);
        void SetEntityGroupLocked(string groupId, bool locked);
        void SetEntityGroupHidden(string groupId, bool hidden);
        void AssignSelectionToGroup(string groupId);
        void DeleteEntityGroup(string groupId);
        void IsolateEntityGroup(string groupId);
        void SetSelectionCategoryFilter(string category);
        void SetSelectionGroupFilter(string groupId);
        void SelectMatchingEntities();
    }

    public interface ILevelEditorEnvironmentDressingActions
    {
        bool AudioZonePreviewEnabled { get; }
        void ApplyEnvironment(LevelEnvironmentAuthoringRequest request);
        void AddPracticalLight();
        void ApplyPracticalLight(LevelPracticalLightAuthoringRequest request);
        void DeletePracticalLight(string lightId);
        void AddDecal();
        void ApplyDecal(LevelDecalAuthoringRequest request);
        void DeleteDecal(string decalId);
        void AddAmbientVfx();
        void ApplyAmbientVfx(LevelAmbientVfxAuthoringRequest request);
        void DeleteAmbientVfx(string effectId);
        void AddAudioZone();
        void ApplyAudioZone(LevelAudioZoneAuthoringRequest request);
        void DeleteAudioZone(string zoneId);
        void SetAudioZonePreviewEnabled(bool enabled);
    }

    public interface ILevelEditorSpatialPlacementActions
    {
        bool PhysicsPlacementRunning { get; }
        void ApplyLevelDisplayName(string displayName);
        void ApplyLevelBounds(LevelBoundsAuthoringRequest request);
        void ConfigureGrid(LevelGridAuthoringRequest request);
        void QueueSpatialPlacement(LevelSpatialPlacementKind kind);
        void QueueSpatialRelocation(
            LevelSpatialPlacementKind kind,
            string targetId);
        void ApplyEntityTransform(
            string x,
            string y,
            string z,
            string pitch,
            string yaw,
            string roll);
        void DropAndSettleSelection(string dropHeight, bool keepUpright);
        void CancelPhysicsPlacement();
        void SetEntityRotationPivot(float normalizedX, float normalizedZ);
        void ResetEntityRotationPivot();
        void ApplyPlayerStart(string x, string y, string z, string yaw);
        void AddInteractionPoint();
        void ApplyInteractionPoint(
            string type,
            string x,
            string y,
            string z,
            string radius);
        void DeleteInteractionPoint();
        void ApplyDestructibleDefaults(
            string enabled,
            string state,
            string integrity);
        void AddScenarioActor(string templateId);
        void ApplyScenarioActorCharacter(string actorId, string characterId);
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
            bool startsEncounter,
            bool topplingEnabled,
            string topplingPitch,
            string topplingRoll,
            string topplingElevation,
            bool pinningEnabled,
            string maximumPinnedActorMass,
            string minimumPinContactDepth);
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

    public interface ILevelEditorPreviewTestActions
    {
        LevelPlayabilityReport PlayabilityReport { get; }
        bool PlayabilityReportIsStale { get; }
        bool SlopeOverlayEnabled { get; }
        void ReturnToMenu();
        void TogglePreview();
        void StartTestPlay();
        void RunPlayabilityDiagnostics();
        void SetSlopeOverlayEnabled(bool enabled);
    }

    public interface ILevelEditorGuiActions :
        ILevelEditorFileActions,
        ILevelEditorHistoryActions,
        ILevelEditorSelectionGroupActions,
        ILevelEditorEnvironmentDressingActions,
        ILevelEditorSpatialPlacementActions,
        ILevelEditorPreviewTestActions
    {
    }
}
