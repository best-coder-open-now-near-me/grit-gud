using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GritGud.Presentation.Gameplay
{
    public sealed partial class GameplayController
    {
        private static IReadOnlyList<GameplayActorAbilityHotbarDefinition>
            CreateActorAbilityHotbarDefinitions(
                DisplacementAbilityDefinition displacementAbility,
                bool hasControlledDrone)
        {
            var definitions = new List<GameplayActorAbilityHotbarDefinition>
            {
                new GameplayActorAbilityHotbarDefinition(
                    GameplayCoreActorAbilities.StanceId,
                    "Crouch / Stand",
                    GameplayCoreActorAbilities.StanceHotbarSlot),
            };
            if (hasControlledDrone)
            {
                definitions.Add(new GameplayActorAbilityHotbarDefinition(
                    GameplayDroneController.AbilityId,
                    "Scout Drone",
                    GameplayDroneController.HotbarSlot,
                    new[]
                    {
                        new GameplayActorAbilityOptionDefinition(
                            GameplayDroneController.MoveOptionId,
                            "Move Drone"),
                        new GameplayActorAbilityOptionDefinition(
                            GameplayDroneController.AttackOptionId,
                            "Drone Attack"),
                    }));
            }
            if (displacementAbility == null) return definitions;
            var options = new List<GameplayActorAbilityOptionDefinition>(
                displacementAbility.Actions.Count);
            foreach (DisplacementActionDefinition action in
                displacementAbility.Actions)
                options.Add(new GameplayActorAbilityOptionDefinition(
                    action.Id,
                    action.DisplayName));
            definitions.Add(new GameplayActorAbilityHotbarDefinition(
                displacementAbility.Id,
                displacementAbility.DisplayName,
                displacementAbility.HotbarSlot,
                options));
            return definitions;
        }

        private bool HasControlledDrone(string actorId)
        {
            if (scenarioAssembly == null) return false;
            foreach (DroneDefinition drone in scenarioAssembly.Drones)
                if (string.Equals(
                    drone.SummonerActorId,
                    actorId,
                    StringComparison.Ordinal)) return true;
            return false;
        }

        private static Vector2 ReadPointer() => Mouse.current == null
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : Mouse.current.position.ReadValue();

        private bool IsPointerOverGameplayInterface(Vector2 pointer) =>
            (hud?.ContainsInteractiveScreenPoint(pointer) ?? false)
            || (partyHud?.ContainsInteractiveScreenPoint(pointer) ?? false)
            || (turnReplayHud?.ContainsInteractiveScreenPoint(pointer) ?? false)
            || (dialogueDrawer?.ContainsInteractiveScreenPoint(pointer)
                ?? false);

        private void TryUseEquippedItemPower(string itemId)
        {
            string actorId = partyControl?.CommandActorId;
            if (string.IsNullOrWhiteSpace(actorId))
                return;

            InventoryItemDefinition item = Session.GetInventoryItem(
                actorId,
                itemId);
            if (item.ConsumablePower != null)
            {
                consumableController.TryToggle(
                    actorId,
                    itemId);
                return;
            }

            if (!string.Equals(
                    Session.GetActor(actorId).EquippedItemId,
                    itemId,
                    StringComparison.Ordinal))
            {
                return;
            }

            weaponTargetingController?.ToggleTargeting();
        }

        private bool CanRequestHotbarPower(string itemId)
        {
            string actorId = partyControl?.CommandActorId;
            if (string.IsNullOrWhiteSpace(actorId))
                return false;

            if (consumableController == null
                || !consumableController.IsPending)
            {
                return true;
            }

            InventoryItemDefinition item = Session.GetInventoryItem(
                actorId,
                itemId);
            return item.ConsumablePower != null;
        }

        private bool CanActivateHotbarBinding(GameplayHotbarBinding binding)
        {
            string actorId = partyControl?.CommandActorId;
            if (string.IsNullOrWhiteSpace(actorId))
                return false;

            if (displacementController != null
                && displacementController.IsTargeting)
            {
                return binding.Kind == GameplayHotbarBindingKind.ActorAbility;
            }

            if (droneController != null && droneController.IsTargeting)
                return binding.Kind == GameplayHotbarBindingKind.ActorAbility;

            if (weaponTargetingController != null
                && weaponTargetingController.IsTargeting)
            {
                return binding.Kind == GameplayHotbarBindingKind.InventoryItem
                    && string.Equals(
                        binding.ContentId,
                        Session.GetActor(actorId).EquippedItemId,
                        StringComparison.Ordinal);
            }

            if (consumableController == null
                || !consumableController.IsPending)
            {
                return true;
            }

            if (binding.Kind != GameplayHotbarBindingKind.InventoryItem)
            {
                return false;
            }

            InventoryItemDefinition item = Session.GetInventoryItem(
                actorId,
                binding.ContentId);
            return item?.ConsumablePower != null;
        }

        private bool TryActivateActorAbility(
            string abilityId,
            string optionId)
        {
            string actorId = partyControl?.CommandActorId;
            if (string.IsNullOrWhiteSpace(actorId))
                return false;

            if (string.Equals(
                    abilityId,
                    GameplayCoreActorAbilities.StanceId,
                    StringComparison.Ordinal)
                && optionId == null)
            {
                CancelPendingHotbarActions();
                return sessionPresenter.ToggleStance();
            }

            if (string.Equals(
                    abilityId,
                    GameplayDroneController.AbilityId,
                    StringComparison.Ordinal))
            {
                equipmentController?.CancelPending();
                displacementController?.CancelTargeting();
                weaponTargetingController?.CancelTargeting();
                return droneController != null
                    && droneController.TryToggle(actorId, optionId);
            }

            DisplacementAbilityDefinition displacementAbility =
                scenarioAssembly.GetActorDefinition(
                    actorId)
                    .DisplacementAbility;
            if (displacementAbility == null
                || !string.Equals(
                    displacementAbility.Id,
                    abilityId,
                    StringComparison.Ordinal)
                || !Session.TryGetDisplacementAction(
                    actorId,
                    optionId,
                    out _))
            {
                return false;
            }

            equipmentController?.CancelPending();
            return displacementController.TryToggleTargeting(optionId);
        }

        private void CancelPendingHotbarActions()
        {
            displacementController?.CancelTargeting();
            droneController?.CancelTargeting();
            weaponTargetingController?.CancelTargeting();
            hotbarController?.CloseActorAbilityFlyout();
            consumableController?.CancelPending();
            equipmentController?.CancelPending();
        }

        private bool ConfirmArmedWeaponFire()
        {
            Vector2 pointer = ReadPointer();
            if (IsPointerOverGameplayInterface(pointer))
                return false;
            targetAcquisitionPresenter?.RefreshAtScreenPoint(
                Camera.main,
                pointer);

            if (projectileController != null
                && projectileController.HasProjectileWeapon)
            {
                return projectileController.TryLaunch();
            }

            if (attackController != null && attackController.TryAttack())
                return true;
            return droneController != null
                && !string.IsNullOrWhiteSpace(partyControl?.CommandActorId)
                && Camera.main != null
                && droneController.TryAttackDroneAtPointer(
                    partyControl.CommandActorId,
                    Camera.main.ScreenPointToRay(pointer));
        }

        private bool ShouldShowTargetingCursor()
        {
            Vector2 pointer = ReadPointer();
            if (IsPointerOverGameplayInterface(pointer))
                return false;
            return CanHoverAttackPointerTarget()
                || weaponTargetingController?.IsTargeting == true
                || displacementController?.IsTargeting == true
                || consumableController?.IsPending == true;
        }

        private bool? ResolveTargetingCursorValidity()
        {
            if (targetAcquisitionPresenter != null
                && targetAcquisitionPresenter.TryGetPointerFeedback(
                    out TargetingPointerFeedback feedback))
            {
                return feedback.IsValid;
            }

            return null;
        }

        private bool CanHoverAttackPointerTarget() =>
            targetAcquisitionPresenter?.HasPointerTarget == true
            && Session != null
            && scenarioAssembly != null
            && !string.IsNullOrWhiteSpace(partyControl?.CommandActorId)
            && Session.GetEquippedAttack(partyControl.CommandActorId)
                != null;
    }
}
