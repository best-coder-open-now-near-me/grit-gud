namespace GritGud.Presentation.LevelEditing.UI
{
    public interface ILevelEditorGuiActions
    {
        void ReturnToMenu();

        void TogglePreview();

        void StartTestPlay();

        void CreateNewLevel();

        void ReloadMainLevel();

        void FrameSelection();

        void FrameLevel();

        void FocusEntity(string entityId);

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
            string actionPointCost);

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
