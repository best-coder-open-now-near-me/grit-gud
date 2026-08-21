using System;
using GritGud.Presentation.LevelEditing.Tools;

namespace GritGud.Presentation.LevelEditing.UI
{
    internal sealed class LevelEditorSpatialPlacementActions :
        ILevelEditorSpatialPlacementActions
    {
        private readonly LevelEditorLayoutCoordinator layout;
        private readonly SpatialRecordPlacementTool spatialPlacement;
        private readonly LevelEditorToolManager toolManager;
        private readonly LevelEditorPhysicsPlacementCoordinator physicsPlacement;
        private readonly ScenarioAuthoringCoordinator scenario;
        private readonly Action<string> applyLevelDisplayName;
        private readonly Action<string, string, string, string, string, string>
            applyEntityTransform;
        private readonly Action<string, bool> dropAndSettleSelection;
        private readonly Action<float, float> setEntityRotationPivot;
        private readonly Action resetEntityRotationPivot;
        private readonly Action addInteractionPoint;
        private readonly Action<string, string, string, string, string>
            applyInteractionPoint;
        private readonly Action deleteInteractionPoint;
        private readonly Action<string, string, string> applyDestructibleDefaults;

        public LevelEditorSpatialPlacementActions(
            LevelEditorLayoutCoordinator layout,
            SpatialRecordPlacementTool spatialPlacement,
            LevelEditorToolManager toolManager,
            LevelEditorPhysicsPlacementCoordinator physicsPlacement,
            ScenarioAuthoringCoordinator scenario,
            Action<string> applyLevelDisplayName,
            Action<string, string, string, string, string, string>
                applyEntityTransform,
            Action<string, bool> dropAndSettleSelection,
            Action<float, float> setEntityRotationPivot,
            Action resetEntityRotationPivot,
            Action addInteractionPoint,
            Action<string, string, string, string, string>
                applyInteractionPoint,
            Action deleteInteractionPoint,
            Action<string, string, string> applyDestructibleDefaults)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.spatialPlacement = spatialPlacement
                ?? throw new ArgumentNullException(nameof(spatialPlacement));
            this.toolManager = toolManager ?? throw new ArgumentNullException(
                nameof(toolManager));
            this.physicsPlacement = physicsPlacement
                ?? throw new ArgumentNullException(nameof(physicsPlacement));
            this.scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            this.applyLevelDisplayName = applyLevelDisplayName
                ?? throw new ArgumentNullException(nameof(applyLevelDisplayName));
            this.applyEntityTransform = applyEntityTransform
                ?? throw new ArgumentNullException(nameof(applyEntityTransform));
            this.dropAndSettleSelection = dropAndSettleSelection
                ?? throw new ArgumentNullException(nameof(dropAndSettleSelection));
            this.setEntityRotationPivot = setEntityRotationPivot
                ?? throw new ArgumentNullException(nameof(setEntityRotationPivot));
            this.resetEntityRotationPivot = resetEntityRotationPivot
                ?? throw new ArgumentNullException(nameof(resetEntityRotationPivot));
            this.addInteractionPoint = addInteractionPoint
                ?? throw new ArgumentNullException(nameof(addInteractionPoint));
            this.applyInteractionPoint = applyInteractionPoint
                ?? throw new ArgumentNullException(nameof(applyInteractionPoint));
            this.deleteInteractionPoint = deleteInteractionPoint
                ?? throw new ArgumentNullException(nameof(deleteInteractionPoint));
            this.applyDestructibleDefaults = applyDestructibleDefaults
                ?? throw new ArgumentNullException(nameof(applyDestructibleDefaults));
        }

        public bool PhysicsPlacementRunning => physicsPlacement.IsRunning;

        public void ApplyLevelDisplayName(string displayName) =>
            applyLevelDisplayName(displayName);
        public void ApplyLevelBounds(LevelBoundsAuthoringRequest request) =>
            layout.ApplyBounds(request);
        public void ConfigureGrid(LevelGridAuthoringRequest request) =>
            layout.ConfigureGrid(request);

        public void QueueSpatialPlacement(LevelSpatialPlacementKind kind)
        {
            spatialPlacement.Queue(kind);
            toolManager.Activate(SpatialRecordPlacementTool.ToolId);
        }

        public void QueueSpatialRelocation(
            LevelSpatialPlacementKind kind,
            string targetId)
        {
            spatialPlacement.Queue(kind, targetId);
            toolManager.Activate(SpatialRecordPlacementTool.ToolId);
        }

        public void ApplyEntityTransform(
            string x,
            string y,
            string z,
            string pitch,
            string yaw,
            string roll) =>
            applyEntityTransform(x, y, z, pitch, yaw, roll);

        public void DropAndSettleSelection(
            string dropHeight,
            bool keepUpright) =>
            dropAndSettleSelection(dropHeight, keepUpright);

        public void CancelPhysicsPlacement() => physicsPlacement.Cancel();
        public void SetEntityRotationPivot(
            float normalizedX,
            float normalizedZ) =>
            setEntityRotationPivot(normalizedX, normalizedZ);
        public void ResetEntityRotationPivot() => resetEntityRotationPivot();
        public void ApplyPlayerStart(
            string x,
            string y,
            string z,
            string yaw) =>
            scenario.ApplyPlayerStart(x, y, z, yaw);
        public void AddInteractionPoint() => addInteractionPoint();
        public void ApplyInteractionPoint(
            string type,
            string x,
            string y,
            string z,
            string radius) =>
            applyInteractionPoint(type, x, y, z, radius);
        public void DeleteInteractionPoint() => deleteInteractionPoint();
        public void ApplyDestructibleDefaults(
            string enabled,
            string state,
            string integrity) =>
            applyDestructibleDefaults(enabled, state, integrity);
        public void AddScenarioActor(string templateId) =>
            scenario.AddActor(templateId);
        public void ApplyScenarioActorCharacter(
            string actorId,
            string characterId) =>
            scenario.ApplyActorCharacter(actorId, characterId);
        public void ApplyScenarioActor(
            string actorId,
            string x,
            string y,
            string z,
            string yaw,
            bool playerControlled,
            bool initiallySelected,
            bool primaryTarget) =>
            scenario.ApplyActor(
                actorId,
                x,
                y,
                z,
                yaw,
                playerControlled,
                initiallySelected,
                primaryTarget);
        public void DeleteScenarioActor(string actorId) =>
            scenario.DeleteActor(actorId);
        public void PlaceScenarioActorAtView(string actorId) =>
            scenario.PlaceActorAtView(actorId);
        public void ApplyScenarioProp(
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
            string minimumPinContactDepth) =>
            scenario.ApplyProp(
                entityId,
                enabled,
                mass,
                sizeClass,
                startsEncounter,
                topplingEnabled,
                topplingPitch,
                topplingRoll,
                topplingElevation,
                pinningEnabled,
                maximumPinnedActorMass,
                minimumPinContactDepth);
        public void ApplyScenarioObjective(
            string entityId,
            string pointId,
            bool enabled,
            string displayName,
            string activeText,
            string completedText,
            string actionPointCost,
            string movementOpportunityCost,
            string mobility) =>
            scenario.ApplyObjective(
                entityId,
                pointId,
                enabled,
                displayName,
                activeText,
                completedText,
                actionPointCost,
                movementOpportunityCost,
                mobility);
        public void ApplyScenarioVehicle(
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
            bool startsEncounter) =>
            scenario.ApplyVehicle(
                entityId,
                enabled,
                maximumSpeed,
                acceleration,
                braking,
                lowSpeedTurn,
                highSpeedTurn,
                baseRadius,
                radiusFactor,
                startingSpeed,
                occupantActorId,
                startsEncounter);
    }
}
