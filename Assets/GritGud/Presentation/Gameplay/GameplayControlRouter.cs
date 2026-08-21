using System;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayControlRouter
    {
        private readonly GameplaySession session;
        private readonly GameplayHud hud;
        private readonly GameplayPartyHud partyHud;
        private readonly GameplayActionController actionController;
        private readonly GameplayAttackController attackController;
        private readonly GameplayProjectileController projectileController;
        private readonly GameplayEquipmentController equipmentController;
        private readonly GameplayHotbarController hotbarController;
        private readonly GameplayConsumableController consumableController;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayCameraRig cameraRig;
        private readonly GameplayPartyControlSession partyControl;
        private readonly TargetAcquisitionPresenter targetAcquisitionPresenter;
        private readonly GameplayDisplacementController displacementController;
        private readonly GameplayWeaponTargetingController
            weaponTargetingController;
        private readonly Func<Vector2, bool> isPointerBlocked;
        private readonly Func<Vector2> readPointer;
        private readonly Func<Camera> getCamera;

        public GameplayControlRouter(
            GameplaySession session,
            GameplayHud hud,
            GameplayPartyHud partyHud,
            GameplayActionController actionController,
            GameplayAttackController attackController,
            GameplayProjectileController projectileController,
            GameplayEquipmentController equipmentController,
            GameplayHotbarController hotbarController,
            GameplayConsumableController consumableController,
            GameplaySessionPresenter sessionPresenter,
            GameplayCameraRig cameraRig,
            GameplayPartyControlSession partyControl,
            TargetAcquisitionPresenter targetAcquisitionPresenter,
            GameplayDisplacementController displacementController,
            GameplayWeaponTargetingController weaponTargetingController,
            Func<Vector2, bool> isPointerBlocked,
            Func<Vector2> readPointer,
            Func<Camera> getCamera)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.partyHud = partyHud ?? throw new ArgumentNullException(
                nameof(partyHud));
            this.actionController = actionController
                ?? throw new ArgumentNullException(nameof(actionController));
            this.attackController = attackController
                ?? throw new ArgumentNullException(nameof(attackController));
            this.projectileController = projectileController
                ?? throw new ArgumentNullException(nameof(projectileController));
            this.equipmentController = equipmentController
                ?? throw new ArgumentNullException(nameof(equipmentController));
            this.hotbarController = hotbarController
                ?? throw new ArgumentNullException(nameof(hotbarController));
            this.consumableController = consumableController
                ?? throw new ArgumentNullException(nameof(consumableController));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.cameraRig = cameraRig ?? throw new ArgumentNullException(
                nameof(cameraRig));
            this.partyControl = partyControl ?? throw new ArgumentNullException(
                nameof(partyControl));
            this.targetAcquisitionPresenter = targetAcquisitionPresenter
                ?? throw new ArgumentNullException(
                    nameof(targetAcquisitionPresenter));
            this.displacementController = displacementController
                ?? throw new ArgumentNullException(nameof(displacementController));
            this.weaponTargetingController = weaponTargetingController
                ?? throw new ArgumentNullException(
                    nameof(weaponTargetingController));
            this.isPointerBlocked = isPointerBlocked
                ?? throw new ArgumentNullException(nameof(isPointerBlocked));
            this.readPointer = readPointer ?? throw new ArgumentNullException(
                nameof(readPointer));
            this.getCamera = getCamera ?? throw new ArgumentNullException(
                nameof(getCamera));
        }

        public void Handle(GameplayControl control)
        {
            if (hud.IsBugReportNoteOpen)
            {
                if (control == GameplayControl.CancelPendingAction)
                    hud.CancelBugReportNote();
                return;
            }

            bool hotbarControl = IsHotbarControl(control);
            if (!hotbarControl)
            {
                equipmentController.ClearStatus();
                hotbarController.ClearStatus();
            }

            if (control != GameplayControl.Attack)
            {
                attackController.ClearStatus();
                projectileController.ClearStatus();
            }

            switch (control)
            {
                case GameplayControl.ToggleTurnMode:
                    if (session.Mode == GameplaySessionMode.Exploration)
                        actionController.TryEnterTurnMode();
                    else
                        actionController.TryExitTurnMode();
                    break;
                case GameplayControl.Attack:
                    HandleAttack();
                    break;
                case GameplayControl.Reload:
                    actionController.TryReload();
                    break;
                case GameplayControl.ToggleStance:
                    sessionPresenter.ToggleStance();
                    break;
                case GameplayControl.ToggleCameraView:
                    cameraRig.ToggleView();
                    break;
                case GameplayControl.ExportBugReport:
                    hud.OpenBugReportNote();
                    break;
                case GameplayControl.Interact:
                    actionController.TryInteract();
                    break;
                case GameplayControl.EndTurn:
                    actionController.TryEndTurn();
                    break;
                case GameplayControl.CyclePartyMember:
                    if (!partyControl.TrySelectNextActor(
                            out GameplayPartySelectionFailure failure))
                    {
                        partyHud.PresentSelectionFailure(failure);
                    }
                    break;
                case GameplayControl.Hotbar1:
                case GameplayControl.Hotbar2:
                case GameplayControl.Hotbar3:
                case GameplayControl.Hotbar4:
                case GameplayControl.Hotbar5:
                case GameplayControl.Hotbar6:
                case GameplayControl.Hotbar7:
                case GameplayControl.Hotbar8:
                    HandleHotbar(control);
                    break;
                case GameplayControl.CancelPendingAction:
                    CancelPendingAction();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(control));
            }
        }

        private void HandleAttack()
        {
            Vector2 pointer = readPointer();
            if (isPointerBlocked(pointer))
                return;

            targetAcquisitionPresenter.RefreshAtScreenPoint(
                getCamera(),
                pointer);

            if (displacementController.IsTargeting)
                displacementController.TryConfirmTargeting();
            else if (consumableController.IsPending)
                consumableController.TryConfirmPending();
            else if (weaponTargetingController.IsTargeting)
                weaponTargetingController.ConfirmTargeting();
            else
                weaponTargetingController.BeginTargeting();
        }

        private void HandleHotbar(GameplayControl control)
        {
            int hotbarNumber = ResolveHotbarNumber(control);
            if (hotbarController.HasExpandedActorAbility)
            {
                hotbarController.TryHandleExpandedActorAbilityHotkey(
                    hotbarNumber);
                return;
            }

            hotbarController.TryActivateSlot(hotbarNumber);
        }

        internal static bool IsHotbarControl(GameplayControl control) =>
            control switch
            {
                GameplayControl.Hotbar1 => true,
                GameplayControl.Hotbar2 => true,
                GameplayControl.Hotbar3 => true,
                GameplayControl.Hotbar4 => true,
                GameplayControl.Hotbar5 => true,
                GameplayControl.Hotbar6 => true,
                GameplayControl.Hotbar7 => true,
                GameplayControl.Hotbar8 => true,
                _ => false,
            };

        internal static int ResolveHotbarNumber(GameplayControl control) =>
            control switch
            {
                GameplayControl.Hotbar1 => 1,
                GameplayControl.Hotbar2 => 2,
                GameplayControl.Hotbar3 => 3,
                GameplayControl.Hotbar4 => 4,
                GameplayControl.Hotbar5 => 5,
                GameplayControl.Hotbar6 => 6,
                GameplayControl.Hotbar7 => 7,
                GameplayControl.Hotbar8 => 8,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(control),
                    control,
                    "The control is not a hotbar slot."),
            };

        private void CancelPendingAction()
        {
            if (hotbarController.CloseActorAbilityFlyout())
                return;
            if (displacementController.CancelTargeting())
                return;
            if (weaponTargetingController.CancelTargeting())
                return;
            if (!consumableController.CancelPending())
                equipmentController.CancelPending();
        }
    }
}
