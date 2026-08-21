using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayHud : MonoBehaviour
    {
        internal const float CommandBarMargin =
            GameplayHudLayout.CommandBarMargin;
        internal const float CommandBarSideRailWidth =
            GameplayHudLayout.CommandBarSideRailWidth;
        internal const int CommandHintRowCapacity =
            GameplayHudLayout.CommandHintRowCapacity;
        internal const float CommandHintRowHeight =
            GameplayHudLayout.CommandHintRowHeight;
        internal const float CommandHintRowGap =
            GameplayHudLayout.CommandHintRowGap;
        internal const float SideRailSectionGap =
            GameplayHudLayout.SideRailSectionGap;
        internal const float TurnModeButtonTop =
            GameplayHudLayout.TurnModeButtonTop;
        internal const float TurnModeButtonHeight =
            GameplayHudLayout.TurnModeButtonHeight;
        internal const float TurnResourceTop =
            GameplayHudLayout.TurnResourceTop;
        internal const float EquipmentFlyoutTop =
            GameplayHudLayout.EquipmentFlyoutTop;
        internal const float WarningHintHeight =
            GameplayHudLayout.WarningHintHeight;
        internal const float WarningHintGap = GameplayHudLayout.WarningHintGap;
        internal const float EncounterNoticeTop = GameplayHudLayout.EncounterNoticeTop;
        internal const float EncounterNoticeHeight = GameplayHudLayout.EncounterNoticeHeight;
        internal const float EncounterNoticeMaximumWidth =
            GameplayHudLayout.EncounterNoticeMaximumWidth;
        internal const float PendingPowerPulseCyclesPerSecond =
            GameplayHudRenderer.PendingPowerPulseCyclesPerSecond;
        internal const float PendingPowerPulseMinimumAlpha =
            GameplayHudRenderer.PendingPowerPulseMinimumAlpha;
        internal const int HotbarSlotCount = GameplayHudLayout.HotbarSlotCount;

        private GameplayHudRenderer hudRenderer;
        private GameplayHudRenderer Renderer =>
            hudRenderer ??= new GameplayHudRenderer();
        private GameplayHudBindings Bindings => Renderer.BindingState;

        public GameplaySession Session => Renderer.Session;

        public TurnMovementController TurnMovement => Renderer.TurnMovement;

        public GameplayActionController ActionController =>
            Renderer.ActionController;

        public GameplayAttackController AttackController =>
            Renderer.AttackController;

        public GameplayEquipmentController EquipmentController =>
            Renderer.EquipmentController;

        public GameplayProjectileController ProjectileController =>
            Renderer.ProjectileController;

        public bool IsVisible => Renderer.IsVisible;

        internal bool IsFlyoutExpanded => Renderer.IsFlyoutExpanded;

        internal bool IsBugReportNoteOpen => Renderer.IsBugReportNoteOpen;

        internal bool IsHotbarChoiceOpen => Renderer.IsHotbarChoiceOpen;

        internal bool IsCommandBarVisible => Renderer.IsCommandBarVisible;

        internal bool AreTurnResourcesVisible =>
            Renderer.AreTurnResourcesVisible;

        internal bool IsInteractionPromptVisible =>
            Renderer.IsInteractionPromptVisible;

        internal bool IsEndTurnAvailable => Renderer.IsEndTurnAvailable;

        internal GameplayHudModel CurrentModel => Renderer.CurrentModel;

        internal string CurrentGuidanceId => Renderer.CurrentGuidanceId;

        internal GameplayGuidanceEntry CurrentGuidanceEntry =>
            Renderer.CurrentGuidanceEntry;

        internal bool ContainsInteractiveScreenPoint(Vector2 screenPoint) =>
            Renderer.ContainsInteractiveScreenPoint(screenPoint);

        private void Awake()
        {
            _ = Renderer;
        }

        public void BindSession(
            GameplaySession session,
            string authoritativePlayerActorId,
            GameplayScenarioAssembly scenarioAssembly) =>
            Bindings.BindSession(
                session,
                authoritativePlayerActorId,
                scenarioAssembly);

        public void SetActor(string authoritativePlayerActorId) =>
            Bindings.SetActor(authoritativePlayerActorId);

        public void UnbindSession() => Bindings.UnbindSession();

        public void BindTurnMovement(TurnMovementController controller) =>
            Bindings.BindTurnMovement(controller);

        public void UnbindTurnMovement() => Bindings.UnbindTurnMovement();

        public void BindGameplayActions(GameplayActionController controller) =>
            Bindings.BindGameplayActions(controller);

        public void UnbindGameplayActions() =>
            Bindings.UnbindGameplayActions();

        public void BindGameplayAttack(GameplayAttackController controller) =>
            Bindings.BindGameplayAttack(controller);

        public void UnbindGameplayAttack() =>
            Bindings.UnbindGameplayAttack();

        public void BindGameplayEquipment(
            GameplayEquipmentController controller) =>
            Bindings.BindGameplayEquipment(controller);

        public void UnbindGameplayEquipment() =>
            Bindings.UnbindGameplayEquipment();

        public void BindGameplayHotbar(GameplayHotbarController controller) =>
            Bindings.BindGameplayHotbar(controller);

        public void UnbindGameplayHotbar() =>
            Bindings.UnbindGameplayHotbar();

        internal void BindGameplayConsumables(
            GameplayConsumableController controller) =>
            Bindings.BindGameplayConsumables(controller);

        internal void UnbindGameplayConsumables() =>
            Bindings.UnbindGameplayConsumables();

        internal void BindGameplayWeaponTargeting(
            GameplayWeaponTargetingController controller) =>
            Bindings.BindGameplayWeaponTargeting(controller);

        internal void UnbindGameplayWeaponTargeting() =>
            Bindings.UnbindGameplayWeaponTargeting();

        public void BindWarningHintSource(IGameplayWarningHintSource source) =>
            Bindings.BindWarningHintSource(source);

        public void UnbindWarningHintSource(
            IGameplayWarningHintSource source) =>
            Bindings.UnbindWarningHintSource(source);

        public void BindGameplayProjectile(
            GameplayProjectileController controller) =>
            Bindings.BindGameplayProjectile(controller);

        public void UnbindGameplayProjectile() =>
            Bindings.UnbindGameplayProjectile();

        public void BindGameplayDisplacement(
            GameplayDisplacementController controller) =>
            Bindings.BindGameplayDisplacement(controller);

        public void UnbindGameplayDisplacement() =>
            Bindings.UnbindGameplayDisplacement();

        public void BindInputSource(IGameplayInputSource source) =>
            Bindings.BindInputSource(source);

        public void UnbindInputSource() => Bindings.UnbindInputSource();

        public void BindTurnModeToggle(Action toggleRequested) =>
            Bindings.BindTurnModeToggle(toggleRequested);

        public void UnbindTurnModeToggle() =>
            Bindings.UnbindTurnModeToggle();

        public void BindBugReportExport(Action<string> exportRequested) =>
            Bindings.BindBugReportExport(exportRequested);

        public void UnbindBugReportExport() =>
            Bindings.UnbindBugReportExport();

        public void SetBugReportStatus(string status) =>
            Bindings.SetBugReportStatus(status);

        public void OpenBugReportNote() => Bindings.OpenBugReportNote();

        internal void SubmitBugReportNote(string note) =>
            Bindings.SubmitBugReportNote(note);

        public void CancelBugReportNote() => Bindings.CancelBugReportNote();

        public void Show()
        {
            Renderer.Show();
            enabled = true;
        }

        public void Hide()
        {
            Renderer.Hide();
            enabled = false;
        }

        internal void ToggleFlyout() => Renderer.ToggleFlyout();

        internal void RequestTurnModeToggle() =>
            Renderer.RequestTurnModeToggle();

        internal void RequestEndTurn() => Renderer.RequestEndTurn();

        private void Update()
        {
            Renderer.Advance(Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            Renderer.Render();
        }

        internal static Rect CalculateCommandBarRectangle(
            float canvasWidth,
            float canvasHeight) =>
            GameplayHudLayout.CalculateCommandBarRectangle(
                canvasWidth,
                canvasHeight);

        internal static Rect CalculateDialogueButtonRectangle(
            float canvasWidth,
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateDialogueButtonRectangle(
                canvasWidth,
                commandBarRectangle);

        internal static Rect CalculateHotbarRectangle(
            Rect commandBarRectangle,
            float x,
            float width) =>
            GameplayHudLayout.CalculateHotbarRectangle(
                commandBarRectangle,
                x,
                width);

        internal static Rect CalculateHotbarLayoutRectangle(
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateHotbarLayoutRectangle(
                commandBarRectangle);

        internal static Rect CalculateEquipmentFlyoutRectangle(
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateEquipmentFlyoutRectangle(
                commandBarRectangle);

        internal static Rect CalculateWarningHintRectangle(
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateWarningHintRectangle(
                commandBarRectangle);

        internal static Rect CalculateEncounterNoticeRectangle(
            float canvasWidth) =>
            GameplayHudLayout.CalculateEncounterNoticeRectangle(canvasWidth);

        internal static Rect CalculateBodyStatusRectangle(
            float canvasWidth,
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateBodyStatusRectangle(
                canvasWidth,
                commandBarRectangle);

        internal static Rect CalculateCommandHintsRectangle(
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateCommandHintsRectangle(
                commandBarRectangle);

        internal static float CalculateCommandHintContentHeight(int rowCount) =>
            GameplayHudLayout.CalculateCommandHintContentHeight(rowCount);

        internal static Rect CalculateCommandHintRowRectangle(
            Rect rectangle,
            int rowIndex,
            int rowCount) =>
            GameplayHudLayout.CalculateCommandHintRowRectangle(
                rectangle,
                rowIndex,
                rowCount);

        internal static Rect CalculateBodyRegionRectangle(
            Rect bodyStatusRectangle,
            TargetRegionId region) =>
            GameplayHudLayout.CalculateBodyRegionRectangle(
                bodyStatusRectangle,
                region);

        internal static Rect CalculateHotbarSlotRectangle(
            Rect commandBarRectangle,
            int slotNumber) =>
            GameplayHudLayout.CalculateHotbarSlotRectangle(
                commandBarRectangle,
                slotNumber);

        internal static string FormatActorAbilityOptionLabel(
            int parentSlot,
            int optionIndex,
            string label) =>
            GameplayHudModelProjector.FormatActorAbilityOptionLabel(
                parentSlot,
                optionIndex,
                label);

        internal static Rect CalculateActorAbilityFlyoutRectangle(
            Rect slotRectangle,
            int optionCount) =>
            GameplayHudLayout.CalculateActorAbilityFlyoutRectangle(
                slotRectangle,
                optionCount);

        internal static bool IsHotbarChoiceRequest(
            Event current,
            Rect itemRectangle) =>
            GameplayHudLayout.IsHotbarChoiceRequest(
                current,
                itemRectangle);

        internal static float CalculatePendingPowerPulse(float unscaledTime) =>
            GameplayHudRenderer.CalculatePendingPowerPulse(unscaledTime);

        private void OnDestroy()
        {
            hudRenderer?.Dispose();
            hudRenderer = null;
        }
    }
}
