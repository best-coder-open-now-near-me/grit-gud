using System;
using GritGud.Presentation.LevelEditing.Core;

namespace GritGud.Presentation.LevelEditing.UI
{
    internal sealed class LevelEditorSelectionGroupActions :
        ILevelEditorSelectionGroupActions
    {
        private readonly LevelEditorCameraController camera;
        private readonly LevelEditorOrganizationModel organization;
        private readonly LevelEditorLayoutCoordinator layout;
        private readonly LevelEditorOrganizationCoordinator organizationAuthoring;
        private readonly Action frameSelection;
        private readonly Action frameLevel;
        private readonly Action<string> focusEntity;

        public LevelEditorSelectionGroupActions(
            LevelEditorCameraController camera,
            LevelEditorOrganizationModel organization,
            LevelEditorLayoutCoordinator layout,
            LevelEditorOrganizationCoordinator organizationAuthoring,
            Action frameSelection,
            Action frameLevel,
            Action<string> focusEntity)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.organization = organization ?? throw new ArgumentNullException(
                nameof(organization));
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.organizationAuthoring = organizationAuthoring
                ?? throw new ArgumentNullException(nameof(organizationAuthoring));
            this.frameSelection = frameSelection
                ?? throw new ArgumentNullException(nameof(frameSelection));
            this.frameLevel = frameLevel
                ?? throw new ArgumentNullException(nameof(frameLevel));
            this.focusEntity = focusEntity
                ?? throw new ArgumentNullException(nameof(focusEntity));
        }

        public LevelEditorCameraView CameraView => camera.View;
        public string IsolatedGroupId => organization.IsolatedGroupId;
        public string SelectionCategoryFilter => organization.CategoryFilter;
        public string SelectionGroupFilter => organization.GroupFilter;

        public void FrameSelection() => frameSelection();
        public void FrameLevel() => frameLevel();
        public void FocusEntity(string entityId) => focusEntity(entityId);
        public void SetCameraView(LevelEditorCameraView view) =>
            layout.SetCameraView(view);
        public void DuplicateArray(LevelArrayAuthoringRequest request) =>
            layout.DuplicateArray(request);
        public void CreateEntityGroup(string displayName) =>
            organizationAuthoring.CreateGroup(displayName);
        public void RenameEntityGroup(string groupId, string displayName) =>
            organizationAuthoring.RenameGroup(groupId, displayName);
        public void SetEntityGroupLocked(string groupId, bool locked) =>
            organizationAuthoring.SetGroupLocked(groupId, locked);
        public void SetEntityGroupHidden(string groupId, bool hidden) =>
            organizationAuthoring.SetGroupHidden(groupId, hidden);
        public void AssignSelectionToGroup(string groupId) =>
            organizationAuthoring.AssignSelection(groupId);
        public void DeleteEntityGroup(string groupId) =>
            organizationAuthoring.DeleteGroup(groupId);
        public void IsolateEntityGroup(string groupId) =>
            organizationAuthoring.IsolateGroup(groupId);
        public void SetSelectionCategoryFilter(string category) =>
            organizationAuthoring.SetCategoryFilter(category);
        public void SetSelectionGroupFilter(string groupId) =>
            organizationAuthoring.SetGroupFilter(groupId);
        public void SelectMatchingEntities() =>
            organizationAuthoring.SelectMatching();
    }
}
