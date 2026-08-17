using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayHudModelProjector
    {
        private readonly GameplayHudBindings bindings;

        public GameplayHudModelProjector(GameplayHudBindings bindings)
        {
            this.bindings = bindings ?? throw new ArgumentNullException(
                nameof(bindings));
        }

        public GameplayHudModel Build()
        {
            GameplaySession session = bindings.Session;
            GameplayScenarioAssembly scenario = bindings.Scenario;
            string actorId = bindings.PlayerActorId;
            if (session == null
                || scenario == null
                || string.IsNullOrWhiteSpace(actorId))
            {
                return null;
            }

            TurnMovementController turnMovement = bindings.TurnMovement;
            var route = turnMovement == null
                ? default(GameplayRouteCommandBarState)
                : new GameplayRouteCommandBarState(
                    turnMovement.PlanPointCount,
                    turnMovement.PlannedCost,
                    turnMovement.IsPlaying,
                    turnMovement.CommittedCost,
                    turnMovement.StatusMessage);
            bool interactionAvailable = bindings.ActionController != null
                && bindings.ActionController.EvaluateInteraction()
                    == GameplayActionFailure.None;
            return GameplayHudModelBuilder.Build(
                session,
                actorId,
                scenario.DisplayName,
                scenario.PrimaryObjective,
                interactionAvailable,
                route,
                ResolveActionStatus(),
                turnModeExitAvailable:
                    bindings.ActionController?.CanExitTurnMode == true,
                pendingEquipmentItemId:
                    bindings.EquipmentController?.PendingItemId,
                warningHint: ResolveWarningHint(),
                hotbarBindings: bindings.HotbarController?.Bindings,
                pendingConsumableItemId:
                    bindings.ConsumableController?.PendingItemId,
                pendingWeaponItemId:
                    bindings.WeaponTargetingController?.IsTargeting == true
                        ? session.GetActor(actorId).EquippedItemId
                        : null,
                actorAbilities: BuildActorAbilityStates());
        }

        public GameplayWarningHintModel ResolveWarningHint() =>
            GameplayWarningHintSelector.Select(bindings.WarningHintSources);

        public string FormatCommandHint(GameplayCommandHintModel hint)
        {
            string binding = bindings.GetBindingDisplay(hint.Control);
            return string.IsNullOrWhiteSpace(binding)
                ? hint.Label
                : binding + "  " + hint.Label;
        }

        public static string FormatActorAbilityOptionLabel(
            int parentSlot,
            int optionIndex,
            string label)
        {
            int hotbarNumber =
                GameplayHotbarController.ResolveOptionHotbarNumber(
                    parentSlot,
                    optionIndex);
            return hotbarNumber == 0
                ? label
                : "[" + hotbarNumber + "]  " + label;
        }

        private IReadOnlyDictionary<string, GameplayActorAbilityHotbarState>
            BuildActorAbilityStates()
        {
            GameplaySession session = bindings.Session;
            if (session == null)
                return null;

            string actorId = bindings.PlayerActorId;
            GameplayActorSnapshot actor = session.GetActor(actorId);
            bool stanceEnabled = !actor.IsIncapacitated && !actor.IsPinned;
            string stanceLabel = actor.Pose.Stance == ActorStance.Standing
                ? "Crouch"
                : "Stand";
            var stanceDefinition = new GameplayActorAbilityHotbarDefinition(
                GameplayCoreActorAbilities.StanceId,
                stanceLabel,
                GameplayCoreActorAbilities.StanceHotbarSlot);
            var states = new Dictionary<
                string,
                GameplayActorAbilityHotbarState>(StringComparer.Ordinal)
            {
                {
                    stanceDefinition.Id,
                    new GameplayActorAbilityHotbarState(
                        stanceDefinition,
                        stanceEnabled,
                        pending: false,
                        stanceLabel.ToUpperInvariant()
                            + "\nHOTKEY C")
                },
            };

            DisplacementAbilityDefinition ability =
                FindPlayerActorDefinition()?.DisplacementAbility;
            GameplayDisplacementController displacement =
                bindings.DisplacementController;
            if (displacement == null || ability == null)
                return states;

            var definitions = new List<GameplayActorAbilityOptionDefinition>(
                ability.Actions.Count);
            var options = new List<GameplayActorAbilityOptionHotbarState>(
                ability.Actions.Count);
            bool enabled = false;
            bool pending = false;
            foreach (DisplacementActionDefinition action in ability.Actions)
            {
                DisplacementActionAvailability availability =
                    displacement.EvaluateActionAvailability(action.Id);
                bool selected = string.Equals(
                    displacement.SelectedActionId,
                    action.Id,
                    StringComparison.Ordinal);
                enabled |= selected || availability.IsAvailable;
                pending |= selected;
                var definition = new GameplayActorAbilityOptionDefinition(
                    action.Id,
                    action.DisplayName);
                definitions.Add(definition);
                options.Add(new GameplayActorAbilityOptionHotbarState(
                    definition,
                    selected || availability.IsAvailable,
                    selected,
                    displacement.GetActionTooltip(action.Id),
                    action.DisplayName
                        + "  -  "
                        + action.Cost.ActionPoints
                        + " AP"));
            }

            states.Add(
                ability.Id,
                new GameplayActorAbilityHotbarState(
                    new GameplayActorAbilityHotbarDefinition(
                        ability.Id,
                        ability.DisplayName,
                        ability.HotbarSlot,
                        definitions),
                    enabled,
                    pending,
                    ability.DisplayName.ToUpperInvariant()
                        + "\nSELECT A DISPLACEMENT INTENT",
                    options));
            return states;
        }

        private ScenarioActorDefinition FindPlayerActorDefinition()
        {
            foreach (ScenarioActorDefinition actor in
                bindings.Session.Scenario.Actors)
            {
                if (string.Equals(
                        actor.Id,
                        bindings.PlayerActorId,
                        StringComparison.Ordinal))
                {
                    return actor;
                }
            }

            return null;
        }

        private string ResolveActionStatus()
        {
            if (!string.IsNullOrWhiteSpace(
                    bindings.HotbarController?.StatusMessage))
            {
                return bindings.HotbarController.StatusMessage;
            }

            if (!string.IsNullOrWhiteSpace(
                    bindings.EquipmentController?.StatusMessage))
            {
                return bindings.EquipmentController.StatusMessage;
            }

            if (!string.IsNullOrWhiteSpace(
                    bindings.DisplacementController?.StatusMessage))
            {
                return bindings.DisplacementController.StatusMessage;
            }

            if (!string.IsNullOrWhiteSpace(
                    bindings.ProjectileController?.StatusMessage))
            {
                return bindings.ProjectileController.StatusMessage;
            }

            return string.IsNullOrWhiteSpace(
                    bindings.AttackController?.StatusMessage)
                ? bindings.ActionController?.StatusMessage
                : bindings.AttackController.StatusMessage;
        }
    }
}
