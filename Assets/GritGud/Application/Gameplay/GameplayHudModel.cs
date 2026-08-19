using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum GameplayControl
    {
        Move,
        Sprint,
        AimLook,
        CameraZoom,
        Attack,
        ToggleTurnMode,
        ToggleStance,
        ToggleCameraView,
        ExportBugReport,
        Interact,
        EndTurn,
        CancelRoute,
        UndoRoute,
        ConfirmRoute,
        CyclePartyMember,
        Hotbar1,
        Hotbar2,
        Hotbar3,
        Hotbar4,
        Hotbar5,
        Hotbar6,
        Hotbar7,
        Hotbar8,
        CancelPendingAction,
    }

    public readonly struct GameplayRouteCommandBarState
    {
        public GameplayRouteCommandBarState(
            int planPointCount,
            float plannedCost,
            bool isPlaying,
            float committedCost,
            string statusMessage)
        {
            if (planPointCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(planPointCount));
            }

            RequireNonNegativeFinite(plannedCost, nameof(plannedCost));
            RequireNonNegativeFinite(committedCost, nameof(committedCost));

            PlanPointCount = planPointCount;
            PlannedCost = plannedCost;
            IsPlaying = isPlaying;
            CommittedCost = committedCost;
            StatusMessage = statusMessage ?? string.Empty;
        }

        public int PlanPointCount { get; }

        public float PlannedCost { get; }

        public bool IsPlaying { get; }

        public float CommittedCost { get; }

        public string StatusMessage { get; }

        private static void RequireNonNegativeFinite(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class GameplayHotbarSlotModel
    {
        public GameplayHotbarSlotModel(int slotNumber, string label, bool enabled)
            : this(
                slotNumber,
                contentId: string.Empty,
                label,
                enabled,
                isEquipped: false,
                equipmentEnabled: false,
                equipmentLabel: string.Empty,
                awaitingConfirmation: false,
                isPowerPending: false,
                primaryClickRequestsPower: false,
                powerTooltip: string.Empty,
                equipmentTooltip: string.Empty,
                abilityOptions: null)
        {
        }

        public GameplayHotbarSlotModel(
            int slotNumber,
            string contentId,
            string label,
            bool enabled,
            bool isEquipped,
            bool equipmentEnabled,
            string equipmentLabel,
            bool awaitingConfirmation,
            bool isPowerPending,
            bool primaryClickRequestsPower,
            string powerTooltip,
            string equipmentTooltip,
            GameplayHotbarBindingKind bindingKind =
                GameplayHotbarBindingKind.InventoryItem,
            IEnumerable<GameplayHotbarAbilityOptionModel> abilityOptions = null)
        {
            if (slotNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotNumber));
            }

            SlotNumber = slotNumber;
            BindingKind = bindingKind;
            ContentId = contentId ?? string.Empty;
            Label = label ?? string.Empty;
            Enabled = enabled;
            IsEquipped = isEquipped;
            EquipmentEnabled = equipmentEnabled;
            EquipmentLabel = equipmentLabel ?? string.Empty;
            AwaitingConfirmation = awaitingConfirmation;
            IsPowerPending = isPowerPending;
            PrimaryClickRequestsPower = primaryClickRequestsPower;
            PowerTooltip = powerTooltip ?? string.Empty;
            EquipmentTooltip = equipmentTooltip ?? string.Empty;
            AbilityOptions = CopyAbilityOptions(abilityOptions);
        }

        public int SlotNumber { get; }

        public GameplayHotbarBindingKind BindingKind { get; }

        public string Label { get; }

        public bool Enabled { get; }

        public string ContentId { get; }

        public bool IsEquipped { get; }

        public bool EquipmentEnabled { get; }

        public string EquipmentLabel { get; }

        public bool AwaitingConfirmation { get; }

        public bool IsPowerPending { get; }

        public bool PrimaryClickRequestsPower { get; }

        public string PowerTooltip { get; }

        public string EquipmentTooltip { get; }

        public IReadOnlyList<GameplayHotbarAbilityOptionModel> AbilityOptions
        { get; }

        private static IReadOnlyList<GameplayHotbarAbilityOptionModel>
            CopyAbilityOptions(
                IEnumerable<GameplayHotbarAbilityOptionModel> options)
        {
            if (options == null)
            {
                return Array.Empty<GameplayHotbarAbilityOptionModel>();
            }

            var copy = new List<GameplayHotbarAbilityOptionModel>();
            foreach (GameplayHotbarAbilityOptionModel option in options)
            {
                copy.Add(option ?? throw new ArgumentException(
                    "Hotbar ability options cannot contain null entries.",
                    nameof(options)));
            }

            return copy.AsReadOnly();
        }
    }

    public sealed class GameplayHotbarAbilityOptionModel
    {
        public GameplayHotbarAbilityOptionModel(
            string id,
            string label,
            bool enabled,
            bool pending,
            string tooltip)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Hotbar ability options require stable identifiers.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException(
                    "Hotbar ability options require labels.",
                    nameof(label));
            }

            Id = id;
            Label = label;
            Enabled = enabled;
            Pending = pending;
            Tooltip = tooltip ?? string.Empty;
        }

        public string Id { get; }

        public string Label { get; }

        public bool Enabled { get; }

        public bool Pending { get; }

        public string Tooltip { get; }
    }

    public sealed class GameplayCommandButtonModel
    {
        public GameplayCommandButtonModel(
            GameplayControl control,
            string label,
            bool enabled)
        {
            RequireDefinedControl(control);
            Control = control;
            Label = RequireText(label, nameof(label));
            Enabled = enabled;
        }

        public GameplayControl Control { get; }

        public string Label { get; }

        public bool Enabled { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Command labels cannot be empty.",
                    parameterName);
            }

            return value;
        }

        private static void RequireDefinedControl(GameplayControl control)
        {
            if (!Enum.IsDefined(typeof(GameplayControl), control))
            {
                throw new ArgumentOutOfRangeException(nameof(control));
            }
        }
    }

    public sealed class GameplayCommandHintModel
    {
        public GameplayCommandHintModel(GameplayControl control, string label)
        {
            if (!Enum.IsDefined(typeof(GameplayControl), control))
            {
                throw new ArgumentOutOfRangeException(nameof(control));
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException(
                    "Command hint labels cannot be empty.",
                    nameof(label));
            }

            Control = control;
            Label = label;
        }

        public GameplayControl Control { get; }

        public string Label { get; }
    }

    public sealed class GameplayTurnResourceModel
    {
        public GameplayTurnResourceModel(
            string actorId,
            int actionPoints,
            int maximumActionPoints,
            float movementOpportunity,
            float maximumMovementOpportunity)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Turn resource models require an actor identifier.",
                    nameof(actorId));
            }

            if (actionPoints < 0 || maximumActionPoints < 0)
            {
                throw new ArgumentOutOfRangeException(
                    actionPoints < 0
                        ? nameof(actionPoints)
                        : nameof(maximumActionPoints));
            }

            RequireNonNegativeFinite(
                movementOpportunity,
                nameof(movementOpportunity));
            RequireNonNegativeFinite(
                maximumMovementOpportunity,
                nameof(maximumMovementOpportunity));

            ActorId = actorId;
            ActionPoints = actionPoints;
            MaximumActionPoints = maximumActionPoints;
            MovementOpportunity = movementOpportunity;
            MaximumMovementOpportunity = maximumMovementOpportunity;
        }

        public string ActorId { get; }

        public int ActionPoints { get; }

        public int MaximumActionPoints { get; }

        public float MovementOpportunity { get; }

        public float MaximumMovementOpportunity { get; }

        private static void RequireNonNegativeFinite(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class GameplayWarningHintModel
    {
        public GameplayWarningHintModel(
            string sourceId,
            string text,
            int priority)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException(
                    "Warning hints require a source identifier.",
                    nameof(sourceId));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "Warning hints require visible text.",
                    nameof(text));
            }

            SourceId = sourceId;
            Text = text;
            Priority = priority;
        }

        public string SourceId { get; }

        public string Text { get; }

        public int Priority { get; }
    }

    public sealed class GameplayCommandBarModel
    {
        public const int HotbarSlotCount = GameplayHotbarRules.SlotCount;

        public GameplayCommandBarModel(
            IEnumerable<GameplayHotbarSlotModel> hotbarSlots,
            IEnumerable<GameplayCommandButtonModel> primaryCommands,
            IEnumerable<GameplayCommandHintModel> hints,
            GameplayBodyStatusModel bodyStatus,
            GameplayTurnResourceModel resources,
            string status,
            GameplayWarningHintModel warningHint = null)
        {
            HotbarSlots = Copy(hotbarSlots, nameof(hotbarSlots));
            PrimaryCommands = Copy(primaryCommands, nameof(primaryCommands));
            Hints = Copy(hints, nameof(hints));
            if (HotbarSlots.Count != HotbarSlotCount)
            {
                throw new ArgumentException(
                    $"Command bars require exactly {HotbarSlotCount} hotbar slots.",
                    nameof(hotbarSlots));
            }

            for (int index = 0; index < HotbarSlots.Count; index++)
            {
                if (HotbarSlots[index].SlotNumber != index + 1)
                {
                    throw new ArgumentException(
                        "Hotbar slots must be ordered and numbered from one.",
                        nameof(hotbarSlots));
                }
            }

            RequireUniqueControls(PrimaryCommands, nameof(primaryCommands));
            RequireUniqueControls(Hints, nameof(hints));
            BodyStatus = bodyStatus ??
                throw new ArgumentNullException(nameof(bodyStatus));
            Resources = resources;
            Status = status ?? string.Empty;
            WarningHint = warningHint;
        }

        public IReadOnlyList<GameplayHotbarSlotModel> HotbarSlots { get; }

        public IReadOnlyList<GameplayCommandButtonModel> PrimaryCommands { get; }

        public IReadOnlyList<GameplayCommandHintModel> Hints { get; }

        public GameplayBodyStatusModel BodyStatus { get; }

        public GameplayTurnResourceModel Resources { get; }

        public string Status { get; }

        public GameplayWarningHintModel WarningHint { get; }

        public GameplayCommandButtonModel FindCommand(GameplayControl control)
        {
            foreach (GameplayCommandButtonModel command in PrimaryCommands)
            {
                if (command.Control == control)
                {
                    return command;
                }
            }

            return null;
        }

        private static IReadOnlyList<T> Copy<T>(
            IEnumerable<T> source,
            string parameterName)
            where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>();
            foreach (T item in source)
            {
                copy.Add(item ?? throw new ArgumentException(
                    "HUD model collections cannot contain null entries.",
                    parameterName));
            }

            return copy.AsReadOnly();
        }

        private static void RequireUniqueControls<T>(
            IReadOnlyList<T> source,
            string parameterName)
        {
            var controls = new HashSet<GameplayControl>();
            foreach (T item in source)
            {
                GameplayControl control;
                if (item is GameplayCommandButtonModel command)
                {
                    control = command.Control;
                }
                else if (item is GameplayCommandHintModel hint)
                {
                    control = hint.Control;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unsupported command-bar model type '{typeof(T).Name}'.");
                }

                if (!controls.Add(control))
                {
                    throw new ArgumentException(
                        $"Control '{control}' appears more than once.",
                        parameterName);
                }
            }
        }
    }

    public sealed class GameplayHudModel
    {
        public GameplayHudModel(
            bool turnBased,
            string scenarioDisplayName,
            string modeLabel,
            string objectiveSummary,
            bool interactionAvailable,
            GameplayCommandBarModel commandBar)
        {
            TurnBased = turnBased;
            ScenarioDisplayName = scenarioDisplayName ?? string.Empty;
            ModeLabel = modeLabel ?? string.Empty;
            ObjectiveSummary = objectiveSummary ?? string.Empty;
            InteractionAvailable = interactionAvailable;
            CommandBar = commandBar ??
                throw new ArgumentNullException(nameof(commandBar));
        }

        public bool TurnBased { get; }

        public string ScenarioDisplayName { get; }

        public string ModeLabel { get; }

        public string ObjectiveSummary { get; }

        public bool InteractionAvailable { get; }

        public GameplayCommandBarModel CommandBar { get; }
    }

    public static class GameplayHudModelBuilder
    {
        public static GameplayHudModel Build(
            GameplaySession session,
            string playerActorId,
            string scenarioDisplayName,
            ScenarioObjectiveRuntimeDefinition primaryObjective,
            bool interactionAvailable,
            GameplayRouteCommandBarState route,
            string actionStatus,
            bool turnModeExitAvailable,
            string pendingEquipmentItemId = null,
            GameplayWarningHintModel warningHint = null,
            IReadOnlyDictionary<int, GameplayHotbarBinding> hotbarBindings = null,
            string pendingConsumableItemId = null,
            string pendingWeaponItemId = null,
            IReadOnlyDictionary<string, GameplayActorAbilityHotbarState>
                actorAbilities = null,
            GameplayInventoryAvailabilitySession inventoryAvailability = null)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(playerActorId))
            {
                throw new ArgumentException(
                    "HUD models require a player actor identifier.",
                    nameof(playerActorId));
            }

            session.GetActor(playerActorId);
            if (string.IsNullOrWhiteSpace(scenarioDisplayName))
            {
                throw new ArgumentException(
                    "HUD models require a scenario display name.",
                    nameof(scenarioDisplayName));
            }

            bool turnBased = session.Mode == GameplaySessionMode.TurnBased;
            string modeLabel = turnBased ? "TURN MODE" : "EXPLORATION MODE";
            string objectiveSummary = BuildObjectiveSummary(
                session,
                primaryObjective);
            GameplayActorSnapshot actor = turnBased
                ? session.GetActor(session.ActiveActorId)
                : session.GetActor(playerActorId);
            GameplayTurnResourceModel resources = turnBased
                ? BuildResources(session, actor)
                : null;
            string status = BuildStatus(session, route, actionStatus);
            IReadOnlyList<GameplayHotbarSlotModel> hotbar = BuildHotbar(
                session,
                playerActorId,
                pendingEquipmentItemId,
                pendingConsumableItemId,
                pendingWeaponItemId,
                hotbarBindings,
                actorAbilities,
                inventoryAvailability
                    ?? new GameplayInventoryAvailabilitySession(session));
            GameplayBodyStatusModel bodyStatus = BuildBodyStatus(
                session.GetActor(playerActorId));

            var primaryCommands = new List<GameplayCommandButtonModel>();
            if (turnBased)
            {
                bool playerCanAct = session.Operation == GameplaySessionOperation.None
                    && string.Equals(
                        session.ActiveActorId,
                        playerActorId,
                        StringComparison.Ordinal);
                primaryCommands.Add(new GameplayCommandButtonModel(
                    GameplayControl.EndTurn,
                    "END TURN",
                    playerCanAct));
                primaryCommands.Add(new GameplayCommandButtonModel(
                    GameplayControl.ToggleTurnMode,
                    "EXIT TURN MODE",
                    turnModeExitAvailable));
            }
            else
            {
                primaryCommands.Add(new GameplayCommandButtonModel(
                    GameplayControl.ToggleTurnMode,
                    "ENTER TURN MODE",
                    session.CanEnterTurnMode));
            }

            var hints = turnBased
                ? CreateTurnHints()
                : CreateExplorationHints();
            return new GameplayHudModel(
                turnBased,
                scenarioDisplayName,
                modeLabel,
                objectiveSummary,
                interactionAvailable,
                new GameplayCommandBarModel(
                    hotbar,
                    primaryCommands,
                    hints,
                    bodyStatus,
                    resources,
                    status,
                    warningHint));
        }

        private static GameplayBodyStatusModel BuildBodyStatus(
            GameplayActorSnapshot actor)
        {
            ActorWoundSnapshot wounds = actor.Wounds;
            return new GameplayBodyStatusModel(
                actor.ActorId,
                new[]
                {
                    new GameplayBodyRegionModel(
                        TargetRegionId.Head,
                        "HEAD",
                        wounds.HeadWounds),
                    new GameplayBodyRegionModel(
                        TargetRegionId.LeftArm,
                        "L ARM",
                        wounds.LeftArmWounds),
                    new GameplayBodyRegionModel(
                        TargetRegionId.Torso,
                        "TORSO",
                        wounds.TorsoWounds),
                    new GameplayBodyRegionModel(
                        TargetRegionId.RightArm,
                        "R ARM",
                        wounds.RightArmWounds),
                    new GameplayBodyRegionModel(
                        TargetRegionId.LeftLeg,
                        "L LEG",
                        wounds.LeftLegWounds),
                    new GameplayBodyRegionModel(
                        TargetRegionId.RightLeg,
                        "R LEG",
                        wounds.RightLegWounds),
                },
                actor.MaximumWounds,
                wounds.MovementPenalty,
                wounds.UnlocalizedWounds);
        }

        private static IReadOnlyList<GameplayHotbarSlotModel> BuildHotbar(
            GameplaySession session,
            string playerActorId,
            string pendingEquipmentItemId,
            string pendingConsumableItemId,
            string pendingWeaponItemId,
            IReadOnlyDictionary<int, GameplayHotbarBinding> hotbarBindings,
            IReadOnlyDictionary<string, GameplayActorAbilityHotbarState>
                actorAbilities,
            GameplayInventoryAvailabilitySession inventoryAvailability)
        {
            var slots = new List<GameplayHotbarSlotModel>(
                GameplayCommandBarModel.HotbarSlotCount);
            GameplayActorSnapshot actor = session.GetActor(playerActorId);
            InventoryItemDefinition equipped = session.GetEquippedItem(
                playerActorId);
            bool turnBased = session.Mode == GameplaySessionMode.TurnBased;
            for (int slotNumber = 1;
                slotNumber <= GameplayCommandBarModel.HotbarSlotCount;
                slotNumber++)
            {
                GameplayHotbarBinding? binding = null;
                if (hotbarBindings != null)
                {
                    if (hotbarBindings.TryGetValue(
                            slotNumber,
                            out GameplayHotbarBinding assigned))
                    {
                        binding = assigned;
                    }
                }
                else
                {
                    InventoryItemDefinition authoredItem = FindHotbarItem(
                        session.GetInventory(playerActorId),
                        slotNumber);
                    if (authoredItem != null)
                    {
                        binding = new GameplayHotbarBinding(
                            GameplayHotbarBindingKind.InventoryItem,
                            authoredItem.Id);
                    }
                }

                if (!binding.HasValue)
                {
                    slots.Add(new GameplayHotbarSlotModel(
                        slotNumber,
                        string.Empty,
                        false));
                    continue;
                }

                if (binding.Value.Kind
                    == GameplayHotbarBindingKind.ActorAbility)
                {
                    if (actorAbilities == null
                        || !actorAbilities.TryGetValue(
                            binding.Value.ContentId,
                            out GameplayActorAbilityHotbarState ability)
                        || ability?.Definition == null)
                    {
                        slots.Add(new GameplayHotbarSlotModel(
                            slotNumber,
                            string.Empty,
                            false));
                        continue;
                    }

                    slots.Add(new GameplayHotbarSlotModel(
                        slotNumber,
                        ability.Definition.Id,
                        ability.Definition.DisplayName.ToUpperInvariant(),
                        ability.Enabled,
                        isEquipped: false,
                        equipmentEnabled: false,
                        equipmentLabel: string.Empty,
                        awaitingConfirmation: false,
                        isPowerPending: ability.Pending,
                        primaryClickRequestsPower: true,
                        powerTooltip: ability.Tooltip,
                        equipmentTooltip: string.Empty,
                        bindingKind: GameplayHotbarBindingKind.ActorAbility,
                        abilityOptions: BuildAbilityOptions(ability.Options)));
                    continue;
                }

                InventoryItemDefinition item = session.GetInventoryItem(
                    playerActorId,
                    binding.Value.ContentId);
                if (item == null)
                {
                    slots.Add(new GameplayHotbarSlotModel(
                        slotNumber,
                        string.Empty,
                        false));
                    continue;
                }

                bool isEquipped = string.Equals(
                    actor.EquippedItemId,
                    item.Id,
                    StringComparison.Ordinal);
                bool weaponPowerPending = item.Attack != null
                    && string.Equals(
                        pendingWeaponItemId,
                        item.Id,
                        StringComparison.Ordinal);
                InventoryPowerAvailability powerAvailability =
                    inventoryAvailability.EvaluatePower(
                        playerActorId,
                        item.Id);
                InventoryEquipmentAvailability equipmentAvailability =
                    inventoryAvailability.EvaluateEquipment(
                        playerActorId,
                        item.Id);
                int? remainingQuantity = item.ConsumablePower == null
                    ? null
                    : session.GetInventoryQuantity(playerActorId, item.Id);
                string equipmentLabel = !item.IsEquippable
                    ? string.Empty
                    : isEquipped
                        ? "v"
                        : "^";
                slots.Add(new GameplayHotbarSlotModel(
                    slotNumber,
                    item.Id,
                    item.DisplayName.ToUpperInvariant()
                        + (remainingQuantity.HasValue
                            ? "  x" + remainingQuantity.Value
                            : string.Empty),
                    powerAvailability.IsAvailable,
                    isEquipped,
                    equipmentAvailability.IsAvailable,
                    equipmentLabel,
                    string.Equals(
                        pendingEquipmentItemId,
                        item.Id,
                        StringComparison.Ordinal),
                    weaponPowerPending
                    || string.Equals(
                        pendingConsumableItemId,
                        item.Id,
                        StringComparison.Ordinal),
                    item.ConsumablePower != null || weaponPowerPending,
                    BuildPowerTooltip(
                        item,
                        powerAvailability,
                        turnBased,
                        remainingQuantity),
                    BuildEquipmentTooltip(
                        equipped,
                        item,
                        isEquipped,
                        equipmentAvailability,
                        turnBased)));
            }

            return slots.AsReadOnly();
        }

        private static IReadOnlyList<GameplayHotbarAbilityOptionModel>
            BuildAbilityOptions(
                IReadOnlyList<GameplayActorAbilityOptionHotbarState> options)
        {
            if (options == null || options.Count == 0)
            {
                return Array.Empty<GameplayHotbarAbilityOptionModel>();
            }

            var models = new List<GameplayHotbarAbilityOptionModel>(
                options.Count);
            foreach (GameplayActorAbilityOptionHotbarState option in options)
            {
                models.Add(new GameplayHotbarAbilityOptionModel(
                    option.Definition.Id,
                    option.SelectionLabel.ToUpperInvariant(),
                    option.Enabled,
                    option.Pending,
                    option.Tooltip));
            }

            return models.AsReadOnly();
        }

        private static string BuildPowerTooltip(
            InventoryItemDefinition item,
            InventoryPowerAvailability availability,
            bool turnBased,
            int? remainingQuantity)
        {
            string heading = item.DisplayName.ToUpperInvariant();
            if (item.ConsumablePower
                is ThrownExplosiveDefinition thrownExplosive)
            {
                string area = thrownExplosive.SmokeField == null
                    ? "\nBLAST - "
                        + thrownExplosive.BlastRadius.ToString("0.#")
                        + " M"
                    : "\nSMOKE - "
                        + thrownExplosive.SmokeField.Radius.ToString("0.#")
                        + " M RADIUS"
                        + "\nHEIGHT - "
                        + thrownExplosive.SmokeField.Height.ToString("0.#")
                        + " M"
                        + "\nDURATION - "
                        + (turnBased
                            ? thrownExplosive.SmokeField.DurationTurnEnds
                                + " TURN ENDS"
                            : thrownExplosive.SmokeField
                                .ExplorationDurationSeconds.ToString("0.#")
                                + " SEC")
                        + "\nSIGHT BLOCK - "
                        + thrownExplosive.SmokeField.MinimumObscuredPath
                            .ToString("0.#")
                        + " M THROUGH SMOKE";
                return AppendRequirement(heading
                    + "\nPOWER - THROW"
                    + "\nQUANTITY - " + remainingQuantity.GetValueOrDefault()
                    + "\nCOST - "
                    + FormatResolvedPowerCost(availability, turnBased)
                    + "\nRANGE - "
                    + thrownExplosive.MaximumRange.ToString("0.#")
                    + " M"
                    + area,
                    availability.Requirement);
            }
            if (item.ConsumablePower != null)
            {
                return AppendRequirement(heading
                    + "\nPOWER - "
                    + item.ConsumablePower.PowerTypeId.ToUpperInvariant()
                    + "\nQUANTITY - " + remainingQuantity.GetValueOrDefault()
                    + "\nCOST - "
                    + FormatResolvedPowerCost(availability, turnBased),
                    availability.Requirement);
            }
            if (item.Attack == null)
            {
                return AppendRequirement(heading, availability.Requirement);
            }

            string targeting = item.Attack.Contact == null
                ? string.Empty
                : "\nREACH - "
                    + item.Attack.Contact.MaximumReach.ToString("0.#")
                    + " M\nTARGET - ACTOR ONLY";
            return AppendRequirement(heading
                + "\nPOWER - "
                + item.Attack.DisplayName.ToUpperInvariant()
                + "\nCOST - "
                + FormatResolvedPowerCost(availability, turnBased)
                + targeting
                + "\nEQUIPPED MOVE SPEED - "
                + FormatMultiplier(item.EquippedEffects.MovementSpeedMultiplier),
                availability.Requirement);
        }

        private static string BuildEquipmentTooltip(
            InventoryItemDefinition equipped,
            InventoryItemDefinition requested,
            bool requestedIsEquipped,
            InventoryEquipmentAvailability availability,
            bool turnBased)
        {
            if (!requested.IsEquippable)
            {
                return string.Empty;
            }

            string operation;
            string cost;
            if (requestedIsEquipped)
            {
                operation = "UNEQUIP";
                cost = FormatResolvedCost(
                    availability.ResolvedCost,
                    turnBased);
            }
            else if (equipped == null)
            {
                operation = "EQUIP";
                cost = FormatResolvedCost(
                    availability.ResolvedCost,
                    turnBased);
            }
            else
            {
                operation = "SWITCH - UNEQUIP "
                    + equipped.DisplayName.ToUpperInvariant()
                    + " + EQUIP "
                    + requested.DisplayName.ToUpperInvariant();
                cost = FormatResolvedCost(
                    availability.ResolvedCost,
                    turnBased);
            }

            return AppendRequirement(operation
                + "\nCOST - "
                + cost
                + "\nEQUIPPED MOVE SPEED - "
                + FormatMultiplier(
                    requested.EquippedEffects.MovementSpeedMultiplier)
                + "\nEMPTY-HANDS MOVE SPEED - "
                + FormatMultiplier(
                    EquipmentEffectSet.None.MovementSpeedMultiplier),
                availability.Requirement);
        }

        private static string FormatCost(ActionCost cost)
        {
            string formatted = cost.ActionPoints + " AP";
            if (cost.MovementOpportunity > 0f)
            {
                formatted += " + "
                    + cost.MovementOpportunity.ToString("0.##")
                    + " MOVE";
            }

            return formatted;
        }

        private static string FormatResolvedCost(
            ActionCost cost,
            bool turnBased) =>
            !turnBased && cost.ActionPoints == 0
                && cost.MovementOpportunity == 0f
                    ? "FREE OUT OF TURN MODE"
                    : FormatCost(cost);

        private static string FormatResolvedPowerCost(
            InventoryPowerAvailability availability,
            bool turnBased) =>
            availability.ConditionalTurnCost
                ? FormatCost(availability.ResolvedCost)
                    + " IF COMBAT STARTS"
                : FormatResolvedCost(availability.ResolvedCost, turnBased);

        private static string AppendRequirement(
            string tooltip,
            string requirement) =>
            string.IsNullOrWhiteSpace(requirement)
                ? tooltip
                : tooltip + "\nREQUIRES - " + requirement;

        private static string FormatMultiplier(float multiplier) =>
            (multiplier * 100f).ToString("0") + "%";

        private static InventoryItemDefinition FindHotbarItem(
            IReadOnlyList<InventoryItemDefinition> inventory,
            int slotNumber)
        {
            foreach (InventoryItemDefinition item in inventory)
            {
                if (item.HotbarSlot == slotNumber)
                {
                    return item;
                }
            }

            return null;
        }

        private static string BuildObjectiveSummary(
            GameplaySession session,
            ScenarioObjectiveRuntimeDefinition objective)
        {
            if (objective == null
                || !session.TryGetObjective(
                    objective.Id,
                    out GameplayObjectiveSnapshot snapshot))
            {
                return "OBJECTIVES - NONE";
            }

            return snapshot.IsCompleted
                ? "OBJECTIVE - " + objective.CompletedHudText
                : "OBJECTIVE - " + objective.ActiveHudText;
        }

        private static GameplayTurnResourceModel BuildResources(
            GameplaySession session,
            GameplayActorSnapshot actor)
        {
            foreach (ScenarioActorDefinition definition in session.Scenario.Actors)
            {
                if (string.Equals(
                    definition.Id,
                    actor.ActorId,
                    StringComparison.Ordinal))
                {
                    return new GameplayTurnResourceModel(
                        actor.ActorId,
                        actor.TurnBudget.ActionPoints,
                        actor.ActionPointEconomy.MaximumHeldActionPoints,
                        actor.TurnBudget.MovementOpportunity,
                        definition.StartingTurnBudget.MovementOpportunity);
                }
            }

            throw new InvalidOperationException(
                $"Actor definition '{actor.ActorId}' is missing from the scenario.");
        }

        private static string BuildStatus(
            GameplaySession session,
            GameplayRouteCommandBarState route,
            string actionStatus)
        {
            if (session.Mode != GameplaySessionMode.TurnBased)
            {
                if (session.VoluntaryTurnReentrySecondsRemaining > 0f)
                {
                    return $"WORLD TURN - TURN MODE IN "
                        + $"{session.VoluntaryTurnReentrySecondsRemaining:0.0}S";
                }

                return string.Empty;
            }

            if (session.Operation == GameplaySessionOperation.ResolvingWorldTurn)
            {
                return "WORLD TURN - RESOLVING";
            }

            if (!string.IsNullOrWhiteSpace(route.StatusMessage))
            {
                return route.StatusMessage.ToUpperInvariant();
            }

            if (route.IsPlaying)
            {
                return $"MOVING - {route.CommittedCost:0.##}";
            }

            if (route.PlanPointCount > 1)
            {
                return $"ROUTE - {route.PlannedCost:0.##}";
            }

            return string.IsNullOrWhiteSpace(actionStatus)
                ? string.Empty
                : actionStatus.ToUpperInvariant();
        }

        private static IReadOnlyList<GameplayCommandHintModel>
            CreateExplorationHints() =>
            new[]
            {
                new GameplayCommandHintModel(GameplayControl.Move, "MOVE"),
                new GameplayCommandHintModel(GameplayControl.Sprint, "SPRINT"),
                new GameplayCommandHintModel(GameplayControl.AimLook, "DRAG LOOK"),
                new GameplayCommandHintModel(GameplayControl.CameraZoom, "ZOOM"),
                new GameplayCommandHintModel(GameplayControl.ToggleStance, "CROUCH/STAND"),
                new GameplayCommandHintModel(GameplayControl.ToggleCameraView, "CAMERA"),
                new GameplayCommandHintModel(GameplayControl.Interact, "INTERACT"),
                new GameplayCommandHintModel(GameplayControl.ToggleTurnMode, "TURN MODE"),
            };

        private static IReadOnlyList<GameplayCommandHintModel> CreateTurnHints() =>
            new[]
            {
                new GameplayCommandHintModel(GameplayControl.Attack, "FIRE"),
                new GameplayCommandHintModel(GameplayControl.Interact, "INTERACT"),
                new GameplayCommandHintModel(GameplayControl.EndTurn, "END TURN"),
                new GameplayCommandHintModel(GameplayControl.ToggleTurnMode, "EXIT TURN MODE"),
                new GameplayCommandHintModel(GameplayControl.ToggleCameraView, "CAMERA"),
                new GameplayCommandHintModel(GameplayControl.CameraZoom, "ZOOM"),
                new GameplayCommandHintModel(GameplayControl.Move, "PLAN"),
                new GameplayCommandHintModel(GameplayControl.UndoRoute, "RETRACT"),
                new GameplayCommandHintModel(GameplayControl.CancelRoute, "CLEAR ROUTE"),
                new GameplayCommandHintModel(GameplayControl.ConfirmRoute, "MOVE"),
            };
    }
}
