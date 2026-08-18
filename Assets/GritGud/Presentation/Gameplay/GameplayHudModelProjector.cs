using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayHudModelProjector
    {
        private readonly GameplayHudBindings bindings;
        private GameplayHudProjectionInputs cachedInputs;
        private GameplayHudModel cachedModel;
        private bool hasCachedModel;

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
                hasCachedModel = false;
                cachedModel = null;
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
            GameplayWarningHintModel warningHint = ResolveWarningHint();
            string actionStatus = ResolveActionStatus();
            string pendingWeaponItemId =
                bindings.WeaponTargetingController?.IsTargeting == true
                    ? session.GetActorState(actorId).EquippedItemId
                    : null;
            var inputs = new GameplayHudProjectionInputs(
                session,
                scenario,
                actorId,
                bindings.Revision,
                session.Revision,
                route,
                interactionAvailable,
                bindings.ActionController?.CanExitTurnMode == true,
                bindings.EquipmentController?.PendingItemId,
                bindings.ConsumableController?.PendingItemId,
                pendingWeaponItemId,
                actionStatus,
                warningHint,
                new GameplayHotbarRevision(
                    bindings.HotbarController?.Bindings),
                bindings.DisplacementController?.SelectedActionId);
            if (hasCachedModel && inputs.HasSameRevision(cachedInputs))
                return cachedModel;

            cachedModel = GameplayHudModelBuilder.Build(
                session,
                actorId,
                scenario.DisplayName,
                scenario.PrimaryObjective,
                interactionAvailable,
                route,
                actionStatus,
                turnModeExitAvailable: inputs.TurnModeExitAvailable,
                pendingEquipmentItemId: inputs.PendingEquipmentItemId,
                warningHint: warningHint,
                hotbarBindings: bindings.HotbarController?.Bindings,
                pendingConsumableItemId: inputs.PendingConsumableItemId,
                pendingWeaponItemId: pendingWeaponItemId,
                actorAbilities: BuildActorAbilityStates());
            cachedInputs = inputs;
            hasCachedModel = true;
            return cachedModel;
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
            GameplayActorStateSnapshot actor = session.GetActorState(actorId);
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

        private readonly struct GameplayHotbarRevision
        {
            public GameplayHotbarRevision(
                IReadOnlyDictionary<int, GameplayHotbarBinding> bindings)
            {
                Slot1 = Get(bindings, 1);
                Slot2 = Get(bindings, 2);
                Slot3 = Get(bindings, 3);
                Slot4 = Get(bindings, 4);
                Slot5 = Get(bindings, 5);
                Slot6 = Get(bindings, 6);
                Slot7 = Get(bindings, 7);
                Slot8 = Get(bindings, 8);
            }

            private GameplayHotbarBinding? Slot1 { get; }
            private GameplayHotbarBinding? Slot2 { get; }
            private GameplayHotbarBinding? Slot3 { get; }
            private GameplayHotbarBinding? Slot4 { get; }
            private GameplayHotbarBinding? Slot5 { get; }
            private GameplayHotbarBinding? Slot6 { get; }
            private GameplayHotbarBinding? Slot7 { get; }
            private GameplayHotbarBinding? Slot8 { get; }

            public bool HasSameRevision(GameplayHotbarRevision other) =>
                Nullable.Equals(Slot1, other.Slot1)
                && Nullable.Equals(Slot2, other.Slot2)
                && Nullable.Equals(Slot3, other.Slot3)
                && Nullable.Equals(Slot4, other.Slot4)
                && Nullable.Equals(Slot5, other.Slot5)
                && Nullable.Equals(Slot6, other.Slot6)
                && Nullable.Equals(Slot7, other.Slot7)
                && Nullable.Equals(Slot8, other.Slot8);

            private static GameplayHotbarBinding? Get(
                IReadOnlyDictionary<int, GameplayHotbarBinding> bindings,
                int slotNumber) =>
                bindings != null
                    && bindings.TryGetValue(slotNumber, out var binding)
                        ? binding
                        : default(GameplayHotbarBinding?);
        }

        private readonly struct GameplayHudProjectionInputs
        {
            public GameplayHudProjectionInputs(
                GameplaySession session,
                GameplayScenarioAssembly scenario,
                string actorId,
                long bindingRevision,
                long sessionRevision,
                GameplayRouteCommandBarState route,
                bool interactionAvailable,
                bool turnModeExitAvailable,
                string pendingEquipmentItemId,
                string pendingConsumableItemId,
                string pendingWeaponItemId,
                string actionStatus,
                GameplayWarningHintModel warningHint,
                GameplayHotbarRevision hotbarRevision,
                string selectedDisplacementActionId)
            {
                Session = session;
                Scenario = scenario;
                ActorId = actorId;
                BindingRevision = bindingRevision;
                SessionRevision = sessionRevision;
                Route = route;
                InteractionAvailable = interactionAvailable;
                TurnModeExitAvailable = turnModeExitAvailable;
                PendingEquipmentItemId = pendingEquipmentItemId;
                PendingConsumableItemId = pendingConsumableItemId;
                PendingWeaponItemId = pendingWeaponItemId;
                ActionStatus = actionStatus;
                WarningHint = warningHint;
                HotbarRevision = hotbarRevision;
                SelectedDisplacementActionId = selectedDisplacementActionId;
            }

            public GameplaySession Session { get; }
            public GameplayScenarioAssembly Scenario { get; }
            public string ActorId { get; }
            public long BindingRevision { get; }
            public long SessionRevision { get; }
            public GameplayRouteCommandBarState Route { get; }
            public bool InteractionAvailable { get; }
            public bool TurnModeExitAvailable { get; }
            public string PendingEquipmentItemId { get; }
            public string PendingConsumableItemId { get; }
            public string PendingWeaponItemId { get; }
            public string ActionStatus { get; }
            public GameplayWarningHintModel WarningHint { get; }
            public GameplayHotbarRevision HotbarRevision { get; }
            public string SelectedDisplacementActionId { get; }

            public bool HasSameRevision(GameplayHudProjectionInputs other) =>
                ReferenceEquals(Session, other.Session)
                && ReferenceEquals(Scenario, other.Scenario)
                && string.Equals(ActorId, other.ActorId, StringComparison.Ordinal)
                && BindingRevision == other.BindingRevision
                && SessionRevision == other.SessionRevision
                && Route.PlanPointCount == other.Route.PlanPointCount
                && Route.PlannedCost == other.Route.PlannedCost
                && Route.IsPlaying == other.Route.IsPlaying
                && Route.CommittedCost == other.Route.CommittedCost
                && string.Equals(
                    Route.StatusMessage,
                    other.Route.StatusMessage,
                    StringComparison.Ordinal)
                && InteractionAvailable == other.InteractionAvailable
                && TurnModeExitAvailable == other.TurnModeExitAvailable
                && string.Equals(
                    PendingEquipmentItemId,
                    other.PendingEquipmentItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    PendingConsumableItemId,
                    other.PendingConsumableItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    PendingWeaponItemId,
                    other.PendingWeaponItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    ActionStatus,
                    other.ActionStatus,
                    StringComparison.Ordinal)
                && WarningHintsMatch(WarningHint, other.WarningHint)
                && HotbarRevision.HasSameRevision(other.HotbarRevision)
                && string.Equals(
                    SelectedDisplacementActionId,
                    other.SelectedDisplacementActionId,
                    StringComparison.Ordinal);

            private static bool WarningHintsMatch(
                GameplayWarningHintModel left,
                GameplayWarningHintModel right) =>
                ReferenceEquals(left, right)
                || (left != null
                    && right != null
                    && left.Priority == right.Priority
                    && string.Equals(
                        left.SourceId,
                        right.SourceId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        left.Text,
                        right.Text,
                        StringComparison.Ordinal));
        }
    }
}
