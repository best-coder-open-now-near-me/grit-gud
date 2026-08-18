using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayInitiativeResult
    {
        public GameplayInitiativeResult(
            string actorId,
            int dexterity,
            int reactionAdvance,
            int participantCount)
        {
            ActorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException(
                    "Initiative requires an actor ID.",
                    nameof(actorId))
                : actorId;
            if (participantCount <= 0
                || reactionAdvance < 1
                || reactionAdvance > participantCount)
            {
                throw new ArgumentOutOfRangeException(nameof(reactionAdvance));
            }
            Dexterity = dexterity;
            ReactionAdvance = reactionAdvance;
            ParticipantCount = participantCount;
        }

        public string ActorId { get; }
        public int Dexterity { get; }
        public int ReactionAdvance { get; }
        public int ParticipantCount { get; }
    }

    public enum GameplaySessionMode
    {
        Exploration,
        TurnBased,
    }

    public enum GameplaySessionOperation
    {
        None,
        ResolvingMovement,
        ResolvingWorldTurn,
    }

    public enum TurnModeContext
    {
        None,
        Voluntary,
        InitiatedEncounter,
    }

    public enum GameplayTurnPhase
    {
        Normal,
        EmergencyReaction,
    }

    public enum GameplayTurnKind
    {
        Normal,
        EmergencyReaction,
    }

    public enum TurnModeEntryFailure
    {
        None,
        AlreadyInTurnMode,
        VoluntaryReentryLocked,
    }

    public enum TurnModeExitFailure
    {
        None,
        NotInTurnMode,
        OperationInProgress,
        EncounterActive,
    }

    public enum TurnEndFailure
    {
        None,
        NotInTurnMode,
        OperationInProgress,
        ActorNotActive,
    }

    public readonly struct GameplayActorSnapshot
    {
        public GameplayActorSnapshot(
            string actorId,
            GameplayActorPose pose,
            TurnBudget turnBudget)
            : this(
                actorId,
                pose,
                turnBudget,
                new ActorWoundSnapshot(actorId, 0, 0f))
        {
        }

        public GameplayActorSnapshot(
            string actorId,
            GameplayActorPose pose,
            TurnBudget turnBudget,
            ActorWoundSnapshot wounds)
            : this(
                actorId,
                pose,
                turnBudget,
                wounds,
                equippedItemId: null,
                EquipmentEffectSet.None)
        {
        }

        public GameplayActorSnapshot(
            string actorId,
            GameplayActorPose pose,
            TurnBudget turnBudget,
            ActorWoundSnapshot wounds,
            string equippedItemId,
            EquipmentEffectSet equipmentEffects,
            int maximumWounds = int.MaxValue,
            ActorInventorySnapshot inventory = null,
            int turnActionPointAllowance = -1,
            float turnMovementAllowance = -1f,
            ActorPinState pinState = null)
        {
            if (!string.Equals(actorId, wounds.ActorId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Actor snapshots and wound state must share an identifier.",
                    nameof(wounds));
            }
            if (maximumWounds <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumWounds));
            ActorInventorySnapshot resolvedInventory = inventory
                ?? new ActorInventorySnapshot(
                    actorId,
                    Array.Empty<InventoryQuantitySnapshot>());
            if (!string.Equals(
                    actorId,
                    resolvedInventory.ActorId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Actor snapshots and inventory state must share an identifier.",
                    nameof(inventory));
            }
            if (pinState != null
                && !string.Equals(
                    actorId,
                    pinState.ActorId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Actor snapshots and pin state must share an identifier.",
                    nameof(pinState));
            }

            ActorId = actorId;
            Pose = pose;
            TurnBudget = turnBudget;
            Wounds = wounds;
            EquippedItemId = equippedItemId;
            EquipmentEffects = equipmentEffects;
            MaximumWounds = maximumWounds;
            Inventory = resolvedInventory;
            TurnActionPointAllowance = turnActionPointAllowance < 0
                ? turnBudget.ActionPoints
                : turnActionPointAllowance;
            TurnMovementAllowance = turnMovementAllowance < 0f
                ? turnBudget.MovementOpportunity + wounds.MovementPenalty
                : turnMovementAllowance;
            PinState = pinState;
            if (float.IsNaN(TurnMovementAllowance)
                || float.IsInfinity(TurnMovementAllowance)
                || TurnActionPointAllowance < turnBudget.ActionPoints
                || TurnMovementAllowance + 0.0001f
                    < turnBudget.MovementOpportunity + wounds.MovementPenalty)
                throw new ArgumentException(
                    "Actor allowances cannot be below the represented state.");
        }

        public string ActorId { get; }

        public GameplayActorPose Pose { get; }

        public TurnBudget TurnBudget { get; }

        public ActorWoundSnapshot Wounds { get; }

        public string EquippedItemId { get; }

        public EquipmentEffectSet EquipmentEffects { get; }

        public int MaximumWounds { get; }

        public ActorInventorySnapshot Inventory { get; }

        public int TurnActionPointAllowance { get; }

        public float TurnMovementAllowance { get; }

        public ActorPinState PinState { get; }

        public bool IsPinned => PinState != null;

        public bool IsIncapacitated => Wounds.WoundCount >= MaximumWounds;

    }

    public readonly struct GameplayActorStateSnapshot
    {
        internal GameplayActorStateSnapshot(
            string actorId,
            GameplayActorPose pose,
            TurnBudget turnBudget,
            ActorWoundSnapshot wounds,
            string equippedItemId,
            EquipmentEffectSet equipmentEffects,
            int maximumWounds,
            int turnActionPointAllowance,
            float turnMovementAllowance,
            ActorPinState pinState)
        {
            ActorId = actorId;
            Pose = pose;
            TurnBudget = turnBudget;
            Wounds = wounds;
            EquippedItemId = equippedItemId;
            EquipmentEffects = equipmentEffects;
            MaximumWounds = maximumWounds;
            TurnActionPointAllowance = turnActionPointAllowance;
            TurnMovementAllowance = turnMovementAllowance;
            PinState = pinState;
        }

        public string ActorId { get; }

        public GameplayActorPose Pose { get; }

        public TurnBudget TurnBudget { get; }

        public ActorWoundSnapshot Wounds { get; }

        public string EquippedItemId { get; }

        public EquipmentEffectSet EquipmentEffects { get; }

        public int MaximumWounds { get; }

        public int TurnActionPointAllowance { get; }

        public float TurnMovementAllowance { get; }

        public ActorPinState PinState { get; }

        public bool IsPinned => PinState != null;

        public bool IsIncapacitated => Wounds.WoundCount >= MaximumWounds;
    }

    public readonly struct GameplayObjectiveSnapshot
    {
        public GameplayObjectiveSnapshot(
            string objectiveId,
            GameplayPosition position,
            float interactionRadius,
            GameplayInteractionDefinition interaction,
            bool isCompleted)
        {
            ObjectiveId = objectiveId;
            Position = position;
            InteractionRadius = interactionRadius;
            Interaction = interaction ??
                throw new ArgumentNullException(nameof(interaction));
            IsCompleted = isCompleted;
        }

        public string ObjectiveId { get; }

        public GameplayPosition Position { get; }

        public float InteractionRadius { get; }

        public GameplayInteractionDefinition Interaction { get; }

        public bool IsCompleted { get; }
    }

    public sealed class VoluntaryTurnCycleRecord
    {
        public VoluntaryTurnCycleRecord(
            long sequence,
            IEnumerable<GameplayActorSnapshot> actors)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (actors == null)
            {
                throw new ArgumentNullException(nameof(actors));
            }

            var actorSnapshots = new List<GameplayActorSnapshot>(actors);
            if (actorSnapshots.Count == 0)
            {
                throw new ArgumentException(
                    "A completed voluntary turn cycle requires actor state.",
                    nameof(actors));
            }

            Sequence = sequence;
            Actors = actorSnapshots.AsReadOnly();
        }

        public long Sequence { get; }

        public IReadOnlyList<GameplayActorSnapshot> Actors { get; }
    }

    public sealed class TurnEndRecord
    {
        public TurnEndRecord(
            long sequence,
            string endingActorId,
            string nextActorId,
            GameplayTurnKind kind = GameplayTurnKind.Normal,
            string interruptedActorId = null)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            EndingActorId = RequireActorId(endingActorId, nameof(endingActorId));
            NextActorId = RequireActorId(nextActorId, nameof(nextActorId));
            if (!Enum.IsDefined(typeof(GameplayTurnKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == GameplayTurnKind.EmergencyReaction
                && string.IsNullOrWhiteSpace(interruptedActorId))
            {
                throw new ArgumentException(
                    "Emergency turns require the interrupted actor identifier.",
                    nameof(interruptedActorId));
            }
            Sequence = sequence;
            Kind = kind;
            InterruptedActorId = interruptedActorId ?? string.Empty;
        }

        public long Sequence { get; }

        public string EndingActorId { get; }

        public string NextActorId { get; }

        public GameplayTurnKind Kind { get; }

        public string InterruptedActorId { get; }

        private static string RequireActorId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Turn records require actor identifiers.",
                    parameterName);
            }

            return value;
        }
    }

    public readonly struct GameplayActiveActorChange
    {
        public GameplayActiveActorChange(
            string previousActorId,
            string currentActorId)
        {
            PreviousActorId = previousActorId;
            CurrentActorId = currentActorId;
        }

        public string PreviousActorId { get; }

        public string CurrentActorId { get; }
    }

    public readonly struct GameplayModeChange
    {
        public GameplayModeChange(
            GameplaySessionMode previousMode,
            GameplaySessionMode currentMode)
        {
            PreviousMode = previousMode;
            CurrentMode = currentMode;
        }

        public GameplaySessionMode PreviousMode { get; }

        public GameplaySessionMode CurrentMode { get; }
    }

    public sealed class GameplaySession : IGameplayTurnLifecycleHost
    {
        private readonly Dictionary<string, GameplayActorState> actors =
            new Dictionary<string, GameplayActorState>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameplayObjectiveState> objectives =
            new Dictionary<string, GameplayObjectiveState>(StringComparer.Ordinal);
        private readonly List<GameplayActionRecord> resolvedActions =
            new List<GameplayActionRecord>();
        private readonly IReadOnlyList<string> initiativeOrder;
        private readonly IReadOnlyList<GameplayInitiativeResult>
            initiativeResults;
        private readonly IReadOnlyList<GameplayActionRecord> readOnlyResolvedActions;
        private readonly GameplayTurnLifecycle turnLifecycle;
        private MovementRouteRecord pendingMovementRoute;

        public GameplaySession(
            ScenarioDefinition scenario,
            GameplayJournal journal = null,
            uint scenarioSeed = 0u,
            GameplayPartySave restoredParty = null)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Journal = journal ?? new GameplayJournal();
            if (restoredParty != null)
                GameplayPartySaveValidator.Validate(restoredParty, scenario);
            int participantCount = scenario.Actors.Count;
            var initiative = new List<GameplayInitiativeResult>(participantCount);
            foreach (ScenarioActorDefinition actor in scenario.Actors)
            {
                CharacterPersistenceSnapshot restoredCharacter = null;
                if (restoredParty != null
                    && actor.CharacterProfile != null)
                {
                    restoredParty.TryGetCharacter(
                        actor.CharacterProfile.IdentityId,
                        out restoredCharacter);
                }
                actors.Add(
                    actor.Id,
                    new GameplayActorState(actor, restoredCharacter));
                initiative.Add(ResolveInitiative(actor, participantCount));
            }
            initiative.Sort(CompareInitiative);
            var order = new List<string>(initiative.Count);
            foreach (GameplayInitiativeResult result in initiative)
                order.Add(result.ActorId);

            foreach (ScenarioObjectiveDefinition objective in scenario.Objectives)
            {
                objectives.Add(
                    objective.Id,
                    new GameplayObjectiveState(objective));
            }

            initiativeOrder = order.AsReadOnly();
            initiativeResults = initiative.AsReadOnly();
            readOnlyResolvedActions = resolvedActions.AsReadOnly();
            turnLifecycle = new GameplayTurnLifecycle(this);
        }

        public ScenarioDefinition Scenario { get; }

        public GameplayJournal Journal { get; }

        public GameplaySessionMode Mode => turnLifecycle.Mode;

        public GameplaySessionOperation Operation { get; private set; } =
            GameplaySessionOperation.None;

        public long Revision { get; private set; }

        public TurnModeContext TurnContext => turnLifecycle.TurnContext;

        public bool EncounterActive => turnLifecycle.EncounterActive;

        public bool EncounterCompletionRequested =>
            turnLifecycle.EncounterCompletionRequested;

        public IReadOnlyList<string> InitiativeOrder => initiativeOrder;

        public IReadOnlyList<string> EmergencyResponders =>
            turnLifecycle.EmergencyResponders;

        public int EmergencyResponderIndex =>
            turnLifecycle.EmergencyResponderIndex;

        public string EmergencyResumeActorId =>
            turnLifecycle.EmergencyResumeActorId;

        public IReadOnlyList<GameplayInitiativeResult> InitiativeResults =>
            initiativeResults;

        public string ActiveActorId => turnLifecycle.ActiveActorId;

        public GameplayTurnPhase TurnPhase => turnLifecycle.TurnPhase;

        public float VoluntaryTurnReentrySecondsRemaining =>
            turnLifecycle.VoluntaryTurnReentrySecondsRemaining;

        public bool CanEnterTurnMode => turnLifecycle.CanEnterTurnMode;

        public MovementRouteRecord PendingMovementRoute => pendingMovementRoute;

        public VoluntaryTurnCycleRecord PendingVoluntaryTurnCycle =>
            turnLifecycle.PendingVoluntaryTurnCycle;

        public IReadOnlyList<GameplayActionRecord> ResolvedActions =>
            readOnlyResolvedActions;

        public GameplayActionRecord LastResolvedAction =>
            resolvedActions.Count == 0
                ? null
                : resolvedActions[resolvedActions.Count - 1];

        public bool IsActorIncapacitated(string actorId) =>
            RequireActor(actorId).IsIncapacitated;

        public bool IsHostile(string observerId, string targetId)
        {
            ActorCombatDefinition observer = Scenario.GetActor(observerId).Combat;
            ActorCombatDefinition target = Scenario.GetActor(targetId).Combat;
            return observer.IsHostileTo(target.AllegianceId);
        }

        public bool HasCapableHostileActor(string observerId)
        {
            RequireActor(observerId);
            foreach (string candidateId in initiativeOrder)
                if (!string.Equals(candidateId, observerId, StringComparison.Ordinal)
                    && !actors[candidateId].IsIncapacitated
                    && IsHostile(observerId, candidateId))
                    return true;
            return false;
        }

        public bool AttackStartsEncounter(string targetId) =>
            Scenario.TryGetAttackResponse(targetId, out var response)
            && response.StartsEncounter;

        public bool ThrownExplosiveStartsEncounter(
            ThrownExplosiveRecord thrown)
        {
            if (thrown == null)
            {
                throw new ArgumentNullException(nameof(thrown));
            }

            foreach (BlastEffectRecord effect in thrown.BlastEffects)
            {
                if (effect.Exposure > 0f
                    && AttackStartsEncounter(effect.EntityId))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ActionStartsEncounter(GameplayActionRecord action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is ThrownExplosiveActionOutcome thrown)
                {
                    if (ThrownExplosiveStartsEncounter(thrown.Record))
                        return true;
                    continue;
                }

                if (outcome is AttackResolvedActionOutcome
                    || outcome is WeaponDischargedActionOutcome
                    || outcome is ProjectileLaunchedActionOutcome
                    || outcome is DisplacementActionOutcome)
                {
                    if (AttackStartsEncounter(outcome.TargetId))
                        return true;
                }
            }

            return false;
        }

        public VoluntaryTurnCycleRecord LastCompletedVoluntaryTurnCycle
        {
            get => turnLifecycle.LastCompletedVoluntaryTurnCycle;
        }

        public TurnEndRecord LastEndedTurn => turnLifecycle.LastEndedTurn;

        public event Action<VoluntaryTurnCycleRecord> VoluntaryTurnCycleCompleted
        {
            add => turnLifecycle.VoluntaryTurnCycleCompleted += value;
            remove => turnLifecycle.VoluntaryTurnCycleCompleted -= value;
        }

        public event Action<TurnEndRecord> TurnEnded
        {
            add => turnLifecycle.TurnEnded += value;
            remove => turnLifecycle.TurnEnded -= value;
        }

        public event Action<EquipmentChangeRecord> EquipmentChanged;

        public event Action<GameplayActiveActorChange> ActiveActorChanged
        {
            add => turnLifecycle.ActiveActorChanged += value;
            remove => turnLifecycle.ActiveActorChanged -= value;
        }

        public event Action<GameplayModeChange> ModeChanged
        {
            add => turnLifecycle.ModeChanged += value;
            remove => turnLifecycle.ModeChanged -= value;
        }

        public event Action<string> ActorCapabilityChanged;

        public bool EnterTurnMode()
        {
            return TryEnterTurnMode(out _);
        }

        public bool TryEnterTurnMode(out TurnModeEntryFailure failure) =>
            turnLifecycle.TryEnterTurnMode(out failure);

        public void AdvanceContinuousTime(float elapsedSeconds) =>
            turnLifecycle.AdvanceContinuousTime(elapsedSeconds);

        public bool BeginEncounter() => turnLifecycle.BeginEncounter();

        public bool BeginEncounterFromAction(GameplayActionRecord action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (EncounterActive
                || !ReferenceEquals(action, LastResolvedAction)
                || !ActionStartsEncounter(action))
            {
                return false;
            }

            return BeginEncounter();
        }

        public bool CompleteEncounter() => turnLifecycle.CompleteEncounter();

        public bool RequestEncounterCompletionAtTurnEnd() =>
            turnLifecycle.RequestEncounterCompletionAtTurnEnd();

        public bool TryExitTurnMode(out TurnModeExitFailure failure) =>
            turnLifecycle.TryExitTurnMode(out failure);

        public bool TryEndTurn(string actorId, out TurnEndFailure failure) =>
            turnLifecycle.TryEndTurn(actorId, out failure);

        public void BeginEmergencyReaction(
            string attackerId,
            IReadOnlyList<string> responderIds,
            int actionPointAllowance) =>
            turnLifecycle.BeginEmergencyReaction(
                attackerId,
                responderIds,
                actionPointAllowance);

        public bool TryEndEmergencyTurn(
            string actorId,
            out bool responsePassCompleted,
            out TurnEndFailure failure) =>
            turnLifecycle.TryEndEmergencyTurn(
                actorId,
                out responsePassCompleted,
                out failure);

        public void CompleteEmergencyReaction(string resumeActorId) =>
            turnLifecycle.CompleteEmergencyReaction(resumeActorId);

        public bool CompleteVoluntaryWorldTurn() =>
            turnLifecycle.CompleteVoluntaryWorldTurn();

        public GameplayActorSnapshot GetActor(string actorId)
        {
            return RequireActor(actorId).CreateSnapshot();
        }

        public GameplayActorStateSnapshot GetActorState(string actorId)
        {
            return RequireActor(actorId).CreateStateSnapshot();
        }

        public int GetTurnActionPointAllowance(string actorId)
        {
            return RequireActor(actorId).TurnActionPointAllowance;
        }

        public bool TryGetActor(
            string actorId,
            out GameplayActorSnapshot actor)
        {
            if (!string.IsNullOrWhiteSpace(actorId)
                && actors.TryGetValue(actorId, out GameplayActorState state))
            {
                actor = state.CreateSnapshot();
                return true;
            }

            actor = default(GameplayActorSnapshot);
            return false;
        }

        public bool TryGetActorState(
            string actorId,
            out GameplayActorStateSnapshot actor)
        {
            if (!string.IsNullOrWhiteSpace(actorId)
                && actors.TryGetValue(actorId, out GameplayActorState state))
            {
                actor = state.CreateStateSnapshot();
                return true;
            }

            actor = default(GameplayActorStateSnapshot);
            return false;
        }

        public IReadOnlyList<InventoryItemDefinition> GetInventory(
            string actorId) => RequireActorDefinition(actorId).Inventory;

        public IReadOnlyList<DisplacementActionDefinition>
            GetDisplacementActions(string actorId) =>
            RequireActorDefinition(actorId).DisplacementActions;

        public bool TryGetDisplacementAction(
            string actorId,
            string actionId,
            out DisplacementActionDefinition action)
        {
            action = string.IsNullOrWhiteSpace(actionId)
                ? null
                : RequireActorDefinition(actorId).GetDisplacementAction(
                    actionId);
            return action != null;
        }

        public bool CanActorUseAction(
            string actorId,
            string actionId)
        {
            GameplayActorState actor = RequireActor(actorId);
            return actor.PinState == null
                || IsPushOffAction(actorId, actionId);
        }

        private bool IsPushOffAction(string actorId, string actionId) =>
            TryGetDisplacementAction(actorId, actionId, out var action)
            && action.Intent == DisplacementActionKind.PushOff;

        public InventoryItemDefinition GetInventoryItem(
            string actorId,
            string itemId) => RequireActorDefinition(actorId).GetInventoryItem(
                itemId);

        public int GetInventoryQuantity(string actorId, string itemId)
        {
            InventoryItemDefinition item = GetInventoryItem(actorId, itemId);
            if (item == null || item.Kind != InventoryItemKind.Consumable)
            {
                throw new InvalidOperationException(
                    $"Inventory item '{itemId}' is not a finite consumable owned by actor '{actorId}'.");
            }

            return RequireActor(actorId).GetInventoryQuantity(itemId);
        }

        public InventoryItemDefinition GetEquippedItem(string actorId)
        {
            GameplayActorState actor = RequireActor(actorId);
            return actor.EquippedItemId == null
                ? null
                : RequireActorDefinition(actorId).GetInventoryItem(
                    actor.EquippedItemId);
        }

        public AttackDefinition GetEquippedAttack(string actorId)
        {
            ScenarioActorDefinition definition = RequireActorDefinition(actorId);
            if (definition.Inventory.Count == 0)
            {
                return definition.Attack;
            }

            return GetEquippedItem(actorId)?.Attack;
        }

        public EquipmentEffectSet GetEquipmentEffects(string actorId) =>
            RequireActor(actorId).EquipmentEffects;

        public GameplayObjectiveSnapshot GetObjective(string objectiveId)
        {
            return RequireObjective(objectiveId).CreateSnapshot();
        }

        public bool TryGetObjective(
            string objectiveId,
            out GameplayObjectiveSnapshot objective)
        {
            if (!string.IsNullOrWhiteSpace(objectiveId)
                && objectives.TryGetValue(
                    objectiveId,
                    out GameplayObjectiveState state))
            {
                objective = state.CreateSnapshot();
                return true;
            }

            objective = default(GameplayObjectiveSnapshot);
            return false;
        }

        public void UpdateExplorationPose(
            string actorId,
            GameplayActorPose pose)
        {
            if (Mode != GameplaySessionMode.Exploration)
            {
                throw new InvalidOperationException(
                    "Exploration poses cannot be changed while turn mode is active.");
            }

            GameplayActorState actor = RequireActor(actorId);
            if (actor.PinState != null)
            {
                throw new InvalidOperationException(
                    $"Pinned actor '{actorId}' cannot move in exploration.");
            }
            actor.Pose = pose;
            MarkStateChanged();
        }

        public void SpendMovement(string actorId, float amount)
        {
            GameplayActorState actor = RequireActiveActor(actorId);
            if (actor.PinState != null)
            {
                throw new InvalidOperationException(
                    $"Pinned actor '{actorId}' cannot spend movement.");
            }
            TurnBudget previousBudget = actor.TurnBudget;
            actor.TurnBudget = actor.TurnBudget.SpendMovement(amount);
            Journal.RecordMovementBudgetSpent(
                actorId,
                amount,
                previousBudget,
                actor.TurnBudget);
            MarkStateChanged();
        }

        public void CommitStanceChange(StanceChangeRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            GameplayActorState actor = Mode == GameplaySessionMode.TurnBased
                ? RequireActiveActor(record.ActorId)
                : RequireActor(record.ActorId);
            if (actor.PinState != null)
            {
                throw new InvalidOperationException(
                    $"Pinned actor '{record.ActorId}' cannot change stance.");
            }
            if (!PosesMatch(actor.Pose, record.PreviousPose))
            {
                throw new InvalidOperationException(
                    "The stance change no longer begins at the actor's authoritative pose.");
            }

            actor.Pose = record.ResultingPose;
            Journal.RecordStanceChanged(record);
            MarkStateChanged();
        }

        public void CommitMovementRoute(MovementRouteRecord route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            GameplayActorState actor = RequireActiveActor(route.ActorId);
            if (actor.PinState != null)
            {
                throw new InvalidOperationException(
                    $"Pinned actor '{route.ActorId}' cannot commit movement.");
            }
            if (!PosesMatch(actor.Pose, route.OriginPose))
            {
                throw new InvalidOperationException(
                    "The movement route no longer begins at the actor's authoritative pose.");
            }

            if (route.HasFrozenBudget
                && (actor.TurnBudget.ActionPoints
                        != route.PreviousBudget.ActionPoints
                    || actor.TurnBudget.MovementOpportunity
                        != route.PreviousBudget.MovementOpportunity))
            {
                throw new InvalidOperationException(
                    "The movement route was planned against a stale turn budget.");
            }

            TurnBudget updatedBudget = actor.TurnBudget.SpendAction(
                new ActionCost(
                    route.TotalActionPointCost,
                    route.TotalCost,
                    ActionMobility.Mobile));
            actor.TurnBudget = updatedBudget;
            pendingMovementRoute = route;
            Operation = GameplaySessionOperation.ResolvingMovement;
            Journal.RecordMovementRouteCommitted(route);
            MarkStateChanged();
        }

        public void CommitForcedDisplacement(DisplacementRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (!record.Succeeded
                || record.Request.SubjectKind != DisplacementSubjectKind.Combatant)
            {
                throw new ArgumentException(
                    "Only successful combatant displacement can update actor pose.",
                    nameof(record));
            }

            GameplayActorState actor = RequireActor(record.Request.SubjectId);
            if (actor.Pose.Position.DistanceTo(record.PreviousPosition) > 0f)
            {
                throw new InvalidOperationException(
                    "Combatant displacement no longer starts at its authoritative pose.");
            }

            actor.Pose = new GameplayActorPose(
                record.ResultingPosition,
                actor.Pose.FacingDegrees,
                actor.Pose.Stance);
            MarkStateChanged();
        }

        internal void ValidatePinTransition(ActorPinTransition transition)
        {
            if (transition == null)
                return;

            GameplayActorState actor = RequireActor(transition.ActorId);
            if (!PosesMatch(actor.Pose, transition.PreviousPose)
                || !PinStatesMatch(actor.PinState, transition.PreviousState))
            {
                throw new InvalidOperationException(
                    "The pin transition no longer starts from authoritative actor state.");
            }
        }

        internal void CommitPinTransition(
            ActorPinTransition transition,
            GameplayNotificationBatch notifications,
            bool validatePrevious = true)
        {
            if (transition == null)
                return;

            if (notifications == null)
                throw new ArgumentNullException(nameof(notifications));

            if (validatePrevious)
                ValidatePinTransition(transition);
            GameplayActorState actor = RequireActor(transition.ActorId);
            actor.Pose = transition.ResultingPose;
            actor.PinState = transition.ResultingState;
            notifications.Add(ActorCapabilityChanged, transition.ActorId);
            MarkStateChanged();
        }

        public void CompleteMovementResolution()
        {
            if (Operation != GameplaySessionOperation.ResolvingMovement
                || pendingMovementRoute == null)
            {
                throw new InvalidOperationException(
                    "No movement resolution is currently in progress.");
            }

            MovementRouteRecord completedRoute = pendingMovementRoute;
            GameplayActorState actor = RequireActor(completedRoute.ActorId);
            actor.Pose = new GameplayActorPose(
                completedRoute.Destination,
                completedRoute.FinalFacingDegrees,
                actor.Pose.Stance);
            pendingMovementRoute = null;
            Operation = GameplaySessionOperation.None;
            Journal.RecordMovementRouteCompleted(completedRoute);
            MarkStateChanged();
        }

        public void CommitAction(GameplayActionRecord record)
        {
            var notifications = new GameplayNotificationBatch();
            CommitAction(record, notifications);
            notifications.Publish();
        }

        internal void CommitAction(
            GameplayActionRecord record,
            GameplayNotificationBatch notifications)
        {
            if (notifications == null)
            {
                throw new ArgumentNullException(nameof(notifications));
            }

            ValidateActionCommit(record);

            GameplayActorState actor = Mode == GameplaySessionMode.TurnBased
                ? RequireActiveActor(record.Request.ActorId)
                : RequireActor(record.Request.ActorId);
            if (actor.PinState != null
                && !IsPushOffAction(
                    record.Request.ActorId,
                    record.Request.ActionId))
            {
                throw new InvalidOperationException(
                    $"Pinned actor '{record.Request.ActorId}' can only Push Off its pinning prop.");
            }
            actor.TurnBudget = record.ResultingBudget;
            foreach (GameplayActionOutcome outcome in record.Outcomes)
            {
                ApplyActionFacing(actor, outcome);
                ApplyActionOutcome(outcome, notifications);
            }

            resolvedActions.Add(record);
            Journal.RecordActionResolved(record);
            MarkStateChanged();
        }

        private void ApplyActionFacing(
            GameplayActorState actor,
            GameplayActionOutcome outcome)
        {
            switch (outcome)
            {
                case AttackResolvedActionOutcome attackResolved:
                    actor.FaceToward(
                        RequireActor(attackResolved.TargetId).Pose.Position);
                    break;

                case WeaponDischargedActionOutcome weaponDischarged:
                    actor.FaceToward(weaponDischarged.Discharge.AimPoint);
                    break;

                case ProjectileLaunchedActionOutcome projectileLaunched:
                    actor.FaceToward(projectileLaunched.Launch.AimPoint);
                    break;

                case ThrownExplosiveActionOutcome thrownExplosive:
                    actor.FaceToward(
                        thrownExplosive.Record.IntendedLanding);
                    break;

                case DisplacementActionOutcome displacement:
                    actor.FaceToward(
                        displacement.Displacement.PreviousPosition);
                    break;
            }
        }

        internal void ValidateActionCommit(GameplayActionRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            GameplayActorState actor = Mode == GameplaySessionMode.TurnBased
                ? RequireActiveActor(record.Request.ActorId)
                : RequireActor(record.Request.ActorId);
            long expectedSequence = resolvedActions.Count == 0
                ? 1
                : resolvedActions[resolvedActions.Count - 1].Sequence + 1;
            if (record.Sequence != expectedSequence)
            {
                throw new InvalidOperationException(
                    "The action record is not the next authoritative sequence.");
            }

            if (!TurnBudgetsMatch(actor.TurnBudget, record.PreviousBudget))
            {
                throw new InvalidOperationException(
                    "The action no longer begins at the actor's authoritative budget.");
            }

            TurnBudget expectedBudget = actor.TurnBudget.SpendAction(record.Cost);
            if (!TurnBudgetsMatch(expectedBudget, record.ResultingBudget))
            {
                throw new InvalidOperationException(
                    "The action record's resulting budget does not match its cost.");
            }

            ValidateActionOutcomes(record);
        }

        private static GameplayInitiativeResult ResolveInitiative(
            ScenarioActorDefinition actor,
            int participantCount)
        {
            int boundedDexterity = Math.Max(1, Math.Min(5, actor.Initiative));
            int reactionAdvance = 1 + ((boundedDexterity - 1)
                * (participantCount - 1) / 4);
            return new GameplayInitiativeResult(
                actor.Id,
                actor.Initiative,
                reactionAdvance,
                participantCount);
        }

        private static int CompareInitiative(
            GameplayInitiativeResult left,
            GameplayInitiativeResult right)
        {
            int initiativeComparison = right.ReactionAdvance.CompareTo(
                left.ReactionAdvance);
            if (initiativeComparison == 0)
            {
                initiativeComparison = right.Dexterity.CompareTo(left.Dexterity);
            }
            return initiativeComparison != 0
                ? initiativeComparison
                : StringComparer.Ordinal.Compare(left.ActorId, right.ActorId);
        }

        private static bool PosesMatch(
            GameplayActorPose left,
            GameplayActorPose right)
        {
            return left.Position.X == right.Position.X
                && left.Position.Y == right.Position.Y
                && left.Position.Z == right.Position.Z
                && left.FacingDegrees == right.FacingDegrees
                && left.Stance == right.Stance;
        }

        private static bool PinStatesMatch(
            ActorPinState left,
            ActorPinState right) =>
            ReferenceEquals(left, right)
            || (left != null && left.HasSameState(right));

        private static bool TurnBudgetsMatch(
            TurnBudget left,
            TurnBudget right)
        {
            return left.ActionPoints == right.ActionPoints
                && left.MovementOpportunity == right.MovementOpportunity;
        }

        private void ValidateActionOutcomes(GameplayActionRecord record)
        {
            var outcomeKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayActionOutcome outcome in record.Outcomes)
            {
                string outcomeKey = outcome.GetType().FullName
                    + ":"
                    + (outcome.TargetId ?? string.Empty);
                if (!outcomeKeys.Add(outcomeKey))
                {
                    throw new InvalidOperationException(
                        "An action record cannot repeat the same authoritative outcome.");
                }

                switch (outcome)
                {
                    case ObjectiveCompletedActionOutcome objectiveCompleted:
                        GameplayObjectiveState objective = RequireObjective(
                            objectiveCompleted.ObjectiveId);
                        if (objective.IsCompleted)
                        {
                            throw new InvalidOperationException(
                                "The objective is already complete.");
                        }

                        break;

                    case AttackResolvedActionOutcome attackResolved:
                        ValidateAttackOutcome(record, attackResolved.Attack);
                        break;

                    case WeaponDischargedActionOutcome weaponDischarged:
                        ValidateWeaponDischargeOutcome(
                            record,
                            weaponDischarged.Discharge);
                        break;

                    case ProjectileLaunchedActionOutcome projectileLaunched:
                        ValidateProjectileLaunchOutcome(
                            record,
                            projectileLaunched.Launch);
                        break;

                    case EquipmentChangedActionOutcome equipmentChanged:
                        ValidateEquipmentChangeOutcome(
                            record,
                            equipmentChanged.Change);
                        break;

                    case ThrownExplosiveActionOutcome thrownExplosive:
                        ValidateThrownExplosiveOutcome(record, thrownExplosive.Record);
                        break;

                    case InventoryQuantityChangedActionOutcome inventory:
                        ValidateInventoryQuantityChangeOutcome(
                            record,
                            inventory.Change);
                        break;

                    case DisplacementActionOutcome displacement:
                        ValidateDisplacementActionOutcome(
                            record,
                            displacement.Displacement);
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unsupported action outcome '{outcome.GetType().Name}'.");
                }
            }
        }

        private void ApplyActionOutcome(
            GameplayActionOutcome outcome,
            GameplayNotificationBatch notifications)
        {
            switch (outcome)
            {
                case ObjectiveCompletedActionOutcome objectiveCompleted:
                    RequireObjective(objectiveCompleted.ObjectiveId).IsCompleted = true;
                    break;

                case AttackResolvedActionOutcome attackResolved:
                    RequireActor(attackResolved.TargetId).ApplyAttack(
                        attackResolved.Attack);
                    notifications.Add(
                        ActorCapabilityChanged,
                        attackResolved.TargetId);
                    break;

                case WeaponDischargedActionOutcome _:
                    // A world-point discharge spends the weapon cost and changes
                    // facing, but has no target state to mutate.
                    break;

                case ProjectileLaunchedActionOutcome _:
                    // The projectile session owns flight state. Launch only spends
                    // the action's authored weapon cost in the gameplay session.
                    break;

                case EquipmentChangedActionOutcome equipmentChanged:
                    EquipmentChangeRecord change = equipmentChanged.Change;
                    GameplayActorState actor = RequireActor(change.ActorId);
                    InventoryItemDefinition item = change.ResultingEquippedItemId
                        == null
                            ? null
                            : RequireActorDefinition(change.ActorId)
                                .GetInventoryItem(change.ResultingEquippedItemId);
                    actor.ApplyEquipment(item);
                    notifications.Add(EquipmentChanged, change);
                    break;

                case ThrownExplosiveActionOutcome thrownExplosive:
                    // The focused thrown-explosive session validates and commits
                    // shared blast consequences after the action is accepted.
                    break;

                case InventoryQuantityChangedActionOutcome inventory:
                    RequireActor(inventory.Change.ActorId)
                        .ApplyInventoryQuantity(inventory.Change);
                    break;

                case DisplacementActionOutcome _:
                    // The displacement session commits the resolved world move
                    // after the ordinary action budget has been accepted.
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported action outcome '{outcome.GetType().Name}'.");
            }
        }

        private VoluntaryTurnCycleRecord CreateVoluntaryTurnCycleRecord()
        {
            long sequence = LastCompletedVoluntaryTurnCycle == null
                ? 1
                : LastCompletedVoluntaryTurnCycle.Sequence + 1;
            var actorSnapshots = new List<GameplayActorSnapshot>(
                initiativeOrder.Count);
            foreach (string actorId in initiativeOrder)
            {
                actorSnapshots.Add(actors[actorId].CreateSnapshot());
            }

            return new VoluntaryTurnCycleRecord(sequence, actorSnapshots);
        }

        private void ValidateAttackOutcome(
            GameplayActionRecord action,
            AttackResolutionRecord attack)
        {
            if (attack == null)
            {
                throw new InvalidOperationException(
                    "Attack outcomes require a resolution record.");
            }

            if (!string.Equals(
                    action.Request.ActorId,
                    attack.AttackerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    attack.TargetId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The attack record does not match its action request.");
            }

            AttackDefinition equippedAttack = GetEquippedAttack(
                attack.AttackerId);
            if (equippedAttack == null
                || !string.Equals(
                    equippedAttack.ActionId,
                    action.Request.ActionId,
                    StringComparison.Ordinal)
                || !ActionCostsMatch(
                    action.Cost,
                    GetAttackActionCost(
                        equippedAttack,
                        action))
                || !AccuracyDecayDefinitionsMatch(
                    equippedAttack.AccuracyDecay,
                    attack.AccuracyDecay))
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded attack action.");
            }

            GameplayActorState target = RequireActor(attack.TargetId);
            GameplayActorState attacker = RequireActor(attack.AttackerId);
            if (attacker.Pose.Position.DistanceTo(target.Pose.Position)
                != attack.Distance)
            {
                throw new InvalidOperationException(
                    "The attack distance no longer matches the authoritative actor positions.");
            }

            if (!WoundsMatch(target.Wounds, attack.TargetWoundsBefore))
            {
                throw new InvalidOperationException(
                    "The attack no longer begins at the target's authoritative wound state.");
            }
        }

        private void ValidateWeaponDischargeOutcome(
            GameplayActionRecord action,
            WeaponDischargeRecord discharge)
        {
            if (discharge == null
                || !string.Equals(
                    action.Request.ActorId,
                    discharge.AttackerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.ActionId,
                    discharge.ActionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    discharge.TargetId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The weapon discharge does not match its action request.");
            }

            AttackDefinition equippedAttack = GetEquippedAttack(
                discharge.AttackerId);
            GameplayActorState attacker = RequireActor(discharge.AttackerId);
            if (equippedAttack == null
                || equippedAttack.Projectile != null
                || !ActionCostsMatch(
                    action.Cost,
                    GetAttackActionCost(
                        equippedAttack,
                        action))
                || !string.Equals(
                    equippedAttack.ActionId,
                    discharge.ActionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    discharge.TargetId,
                    StringComparison.Ordinal)
                || attacker.Pose.Position.DistanceTo(discharge.Origin) > 0f)
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded immediate weapon discharge.");
            }
        }

        private void ValidateThrownExplosiveOutcome(
            GameplayActionRecord action,
            ThrownExplosiveRecord thrown)
        {
            if (thrown == null
                || !string.Equals(action.Request.ActorId, thrown.ThrowerId, StringComparison.Ordinal)
                || !string.Equals(action.Request.ActionId, thrown.Definition.Id, StringComparison.Ordinal)
                || !string.Equals(action.Request.TargetId, thrown.Definition.Id, StringComparison.Ordinal)
                || !ActionCostsMatch(
                    action.Cost,
                    GetThrownExplosiveActionCost(
                        thrown.Definition,
                        action)))
                throw new InvalidOperationException("The thrown explosive does not match its action request.");
            GameplayActorState actor = RequireActor(thrown.ThrowerId);
            InventoryItemDefinition item = RequireActorDefinition(thrown.ThrowerId)
                .GetInventoryItem(thrown.Definition.Id);
            if (!ThrownExplosiveDefinitionsMatch(
                    item.ConsumablePower as ThrownExplosiveDefinition,
                    thrown.Definition))
                throw new InvalidOperationException(
                    "The actor does not own the recorded thrown explosive.");
            if (actor.Pose.Position.DistanceTo(thrown.Origin) > 0f)
                throw new InvalidOperationException("The throw no longer starts at the actor's position.");
            if (thrown.Definition.GetLaunchOrigin(actor.Pose)
                    .DistanceTo(thrown.LaunchOrigin) > 0f)
                throw new InvalidOperationException(
                    "The throw no longer starts at its authored launch origin.");

            InventoryQuantityChangeRecord quantity =
                FindInventoryQuantityChange(action, thrown.Definition.Id);
            if (quantity == null
                || !string.Equals(
                    quantity.ActorId,
                    thrown.ThrowerId,
                    StringComparison.Ordinal)
                || quantity.ConsumedQuantity != 1)
            {
                throw new InvalidOperationException(
                    "A thrown explosive must consume exactly one matching inventory item in the same action.");
            }
        }

        private void ValidateInventoryQuantityChangeOutcome(
            GameplayActionRecord action,
            InventoryQuantityChangeRecord change)
        {
            if (change == null
                || !string.Equals(
                    action.Request.ActorId,
                    change.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    change.ItemId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The inventory quantity change does not match its action request.");
            }

            InventoryItemDefinition item = RequireActorDefinition(
                change.ActorId).GetInventoryItem(change.ItemId);
            GameplayActorState actor = RequireActor(change.ActorId);
            int pairedThrowCount = 0;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is ThrownExplosiveActionOutcome thrown
                    && string.Equals(
                        thrown.Record.Definition.Id,
                        change.ItemId,
                        StringComparison.Ordinal))
                {
                    pairedThrowCount++;
                }
            }

            if (item == null
                || item.Kind != InventoryItemKind.Consumable
                || pairedThrowCount != 1
                || change.ConsumedQuantity != 1
                || actor.GetInventoryQuantity(change.ItemId)
                    != change.PreviousQuantity)
            {
                throw new InvalidOperationException(
                    "The inventory quantity change is not valid for the actor's authoritative state.");
            }
        }

        private static InventoryQuantityChangeRecord
            FindInventoryQuantityChange(
                GameplayActionRecord action,
                string itemId)
        {
            InventoryQuantityChangeRecord matched = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is InventoryQuantityChangedActionOutcome inventory
                    && string.Equals(
                        inventory.Change.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    if (matched != null)
                    {
                        throw new InvalidOperationException(
                            "A thrown explosive action must contain exactly one matching inventory quantity change.");
                    }

                    matched = inventory.Change;
                }
            }

            return matched;
        }

        private void ValidateDisplacementActionOutcome(
            GameplayActionRecord action,
            DisplacementRecord displacement)
        {
            if (displacement == null)
            {
                DisplacementActionCommitValidator.Validate(
                    action,
                    displacement,
                    definition: null,
                    equippedItem: null,
                    chargesTurnCost: ShouldChargeTurnCost(action));
                return;
            }

            DisplacementActionDefinition definition = RequireActorDefinition(
                displacement.Request.ActorId).GetDisplacementAction(
                    displacement.Request.ActionId);
            RequireActor(displacement.Request.ActorId);
            DisplacementActionCommitValidator.Validate(
                action,
                displacement,
                definition,
                GetEquippedItem(displacement.Request.ActorId),
                ShouldChargeTurnCost(action));
        }

        private static bool ThrownExplosiveDefinitionsMatch(
            ThrownExplosiveDefinition left,
            ThrownExplosiveDefinition right) =>
            left != null && right != null
                && string.Equals(left.Id, right.Id, StringComparison.Ordinal)
                && ActionCostsMatch(left.TurnCost, right.TurnCost)
                && left.MaximumRange == right.MaximumRange
                && left.StandingLaunchHeight == right.StandingLaunchHeight
                && left.CrouchedLaunchHeight == right.CrouchedLaunchHeight
                && left.BaseUncertaintyRadius == right.BaseUncertaintyRadius
                && left.UncertaintyPerMeter == right.UncertaintyPerMeter
                && left.BlastRadius == right.BlastRadius
                && left.BlastWoundMovementPenalty
                    == right.BlastWoundMovementPenalty
                && left.BlastIntegrityDamage
                    == right.BlastIntegrityDamage
                && SmokeFieldDefinitionsMatch(
                    left.SmokeField,
                    right.SmokeField);

        private static bool SmokeFieldDefinitionsMatch(
            SmokeFieldDefinition left,
            SmokeFieldDefinition right) =>
            left == null
                ? right == null
                : left.Matches(right);

        private void ValidateProjectileLaunchOutcome(
            GameplayActionRecord action,
            ProjectileLaunchRecord launch)
        {
            if (launch == null
                || !string.Equals(
                    action.Request.ActorId,
                    launch.AttackerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    launch.IntendedTargetId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.ActionId,
                    launch.ActionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The projectile launch does not match its action request.");
            }

            AttackDefinition weapon = GetEquippedAttack(launch.AttackerId);
            if (weapon?.Projectile == null
                || !string.Equals(
                    weapon.ActionId,
                    launch.ActionId,
                    StringComparison.Ordinal)
                || !ActionCostsMatch(action.Cost, weapon.TurnCost)
                || !ProjectileDefinitionsMatch(
                    launch.Definition,
                    weapon.Projectile))
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded projectile weapon.");
            }

            GameplayActorState attacker = RequireActor(launch.AttackerId);
            GameplayPosition expectedOrigin = weapon.Projectile.GetLaunchOrigin(
                attacker.Pose);
            if (expectedOrigin.DistanceTo(launch.Origin) > 0f)
            {
                throw new InvalidOperationException(
                    "The projectile launch no longer starts at the attacker's authored launch point.");
            }

            // Projectile attacks are aimed at a world point.  The reference id may
            // identify an actor, destructible, or unregistered patch of terrain;
            // collision at arrival remains authoritative.
        }

        internal void ApplyBlastInjury(
            string actorId,
            TargetRegionId? region,
            float woundMovementPenalty)
        {
            var notifications = new GameplayNotificationBatch();
            ApplyBlastInjury(
                actorId,
                region,
                woundMovementPenalty,
                notifications);
            notifications.Publish();
        }

        internal void ApplyBlastInjury(
            string actorId,
            TargetRegionId? region,
            float woundMovementPenalty,
            GameplayNotificationBatch notifications)
        {
            if (notifications == null)
            {
                throw new ArgumentNullException(nameof(notifications));
            }

            RequireActor(actorId).ApplyBlast(region, woundMovementPenalty);
            notifications.Add(ActorCapabilityChanged, actorId);
            MarkStateChanged();
        }

        private void ValidateEquipmentChangeOutcome(
            GameplayActionRecord action,
            EquipmentChangeRecord change)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is DisplacementActionOutcome)
                {
                    // The focused displacement validator owns its automatic
                    // equipment transition as part of the composite action.
                    return;
                }
            }

            if (change == null
                || !string.Equals(
                    action.Request.ActorId,
                    change.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    action.Request.TargetId,
                    change.ItemId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The equipment change does not match its action request.");
            }

            GameplayActorState actor = RequireActor(change.ActorId);
            ScenarioActorDefinition definition = RequireActorDefinition(
                change.ActorId);
            InventoryItemDefinition item = definition.GetInventoryItem(
                change.ItemId);
            if (item == null)
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded equipment change.");
            }

            ActionCost expectedCost = Mode == GameplaySessionMode.TurnBased
                ? item.EquipmentCost
                : new ActionCost(
                    0,
                    0f,
                    item.EquipmentCost.Mobility);
            if (!item.IsEquippable
                || !string.Equals(
                    actor.EquippedItemId,
                    change.PreviousEquippedItemId,
                    StringComparison.Ordinal)
                || !ActionCostsMatch(action.Cost, expectedCost))
            {
                throw new InvalidOperationException(
                    "The actor does not own the recorded equipment change.");
            }

            string expectedActionId = change.Kind == EquipmentChangeKind.Equip
                ? EquipmentActionIds.Equip
                : EquipmentActionIds.Unequip;
            string expectedResult = change.Kind == EquipmentChangeKind.Equip
                ? item.Id
                : null;
            if (!string.Equals(
                    action.Request.ActionId,
                    expectedActionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    change.ResultingEquippedItemId,
                    expectedResult,
                    StringComparison.Ordinal)
                || (change.Kind == EquipmentChangeKind.Equip
                    && change.PreviousEquippedItemId != null))
            {
                throw new InvalidOperationException(
                    "The recorded equipment transition is invalid.");
            }
        }

        private ScenarioActorDefinition RequireActorDefinition(string actorId)
        {
            foreach (ScenarioActorDefinition actor in Scenario.Actors)
            {
                if (string.Equals(actor.Id, actorId, StringComparison.Ordinal))
                {
                    return actor;
                }
            }

            throw new KeyNotFoundException(
                $"Actor definition '{actorId}' is not part of the scenario.");
        }

        private static bool WoundsMatch(
            ActorWoundSnapshot left,
            ActorWoundSnapshot right)
        {
            return left.HasSameState(right);
        }

        private static bool ActionCostsMatch(ActionCost left, ActionCost right)
        {
            return left.ActionPoints == right.ActionPoints
                && left.MovementOpportunity == right.MovementOpportunity
                && left.Mobility == right.Mobility;
        }

        private ActionCost GetAttackActionCost(
            AttackDefinition attack,
            GameplayActionRecord action) =>
            ShouldChargeTurnCost(action)
                ? attack.TurnCost
                : new ActionCost(
                    0,
                    0f,
                    attack.TurnCost.Mobility);

        private ActionCost GetThrownExplosiveActionCost(
            ThrownExplosiveDefinition definition,
            GameplayActionRecord action) =>
            ShouldChargeTurnCost(action)
                ? definition.TurnCost
                : new ActionCost(
                    0,
                    0f,
                    definition.TurnCost.Mobility);

        private bool ShouldChargeTurnCost(GameplayActionRecord action) =>
            Mode == GameplaySessionMode.TurnBased
            || (!EncounterActive && ActionStartsEncounter(action));

        private static bool AccuracyDecayDefinitionsMatch(
            AccuracyDecayDefinition left,
            AccuracyDecayDefinition right) =>
            left != null
                && right != null
                && left.HalfLifeDistance == right.HalfLifeDistance
                && left.MinimumAccuracyPercent
                    == right.MinimumAccuracyPercent;

        private static bool ProjectileDefinitionsMatch(
            ProjectileFlightDefinition left,
            ProjectileFlightDefinition right)
        {
            return left != null
                && right != null
                && string.Equals(left.Id, right.Id, StringComparison.Ordinal)
                && left.SpeedPerTurn == right.SpeedPerTurn
                && left.Radius == right.Radius
                && left.MaximumRange == right.MaximumRange
                && left.StandingLaunchHeight == right.StandingLaunchHeight
                && left.CrouchedLaunchHeight == right.CrouchedLaunchHeight
                && left.OpensEmergencyReactionWindow
                    == right.OpensEmergencyReactionWindow
                && left.BlastRadius == right.BlastRadius
                && left.BlastWoundMovementPenalty
                    == right.BlastWoundMovementPenalty
                && left.BlastIntegrityDamage
                    == right.BlastIntegrityDamage;
        }

        GameplayJournal IGameplayTurnLifecycleHost.Journal => Journal;

        GameplaySessionOperation IGameplayTurnLifecycleHost.Operation
        {
            get => Operation;
            set => Operation = value;
        }

        IReadOnlyList<string> IGameplayTurnLifecycleHost.InitiativeOrder =>
            initiativeOrder;

        float IGameplayTurnLifecycleHost.MinimumVoluntaryTurnSeconds =>
            Scenario.Timing.MinimumVoluntaryTurnSeconds;

        void IGameplayTurnLifecycleHost.RequireActorForTurnLifecycle(
            string actorId) => RequireActor(actorId);

        bool IGameplayTurnLifecycleHost.IsActorIncapacitatedForTurnLifecycle(
            string actorId) => RequireActor(actorId).IsIncapacitated;

        void IGameplayTurnLifecycleHost.RefreshTurnBudgetForTurnLifecycle(
            string actorId) => RequireActor(actorId).RefreshTurnBudget();

        void IGameplayTurnLifecycleHost.RefreshAllTurnBudgetsForTurnLifecycle()
        {
            foreach (string actorId in initiativeOrder)
                actors[actorId].RefreshTurnBudget();
        }

        void IGameplayTurnLifecycleHost.BeginEmergencyTurnForTurnLifecycle(
            string actorId,
            int actionPointAllowance) =>
            RequireActor(actorId).BeginEmergencyTurn(actionPointAllowance);

        int IGameplayTurnLifecycleHost
            .GetEmergencyActionPointAllowanceForTurnLifecycle(
                string actorId) =>
            RequireActor(actorId).EmergencyActionPointAllowance;

        VoluntaryTurnCycleRecord IGameplayTurnLifecycleHost
            .CreateVoluntaryTurnCycleRecordForTurnLifecycle() =>
            CreateVoluntaryTurnCycleRecord();

        void IGameplayTurnLifecycleHost.MarkStateChangedForTurnLifecycle() =>
            MarkStateChanged();

        private void MarkStateChanged()
        {
            Revision++;
        }

        private GameplayActorState RequireActiveActor(string actorId)
        {
            if (Mode != GameplaySessionMode.TurnBased)
            {
                throw new InvalidOperationException(
                    "Turn resources can only be used while turn mode is active.");
            }

            if (!string.Equals(ActiveActorId, actorId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Only the active actor can use turn resources.");
            }

            if (Operation != GameplaySessionOperation.None)
            {
                throw new InvalidOperationException(
                    "Turn commands cannot begin while another operation is resolving.");
            }

            GameplayActorState actor = RequireActor(actorId);
            if (actor.IsIncapacitated)
            {
                throw new InvalidOperationException(
                    $"Incapacitated actor '{actorId}' cannot begin a turn command.");
            }

            return actor;
        }

        private GameplayActorState RequireActor(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Actor identifiers cannot be empty.",
                    nameof(actorId));
            }

            if (!actors.TryGetValue(actorId, out GameplayActorState actor))
            {
                throw new KeyNotFoundException(
                    $"Actor '{actorId}' does not belong to scenario '{Scenario.Id}'.");
            }

            return actor;
        }

        private GameplayObjectiveState RequireObjective(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                throw new ArgumentException(
                    "Objective identifiers cannot be empty.",
                    nameof(objectiveId));
            }

            if (!objectives.TryGetValue(
                    objectiveId,
                    out GameplayObjectiveState objective))
            {
                throw new KeyNotFoundException(
                    $"Objective '{objectiveId}' does not belong to scenario '{Scenario.Id}'.");
            }

            return objective;
        }

    }
}
