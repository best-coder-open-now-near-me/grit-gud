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
            int roll,
            int rollMaximum)
        {
            ActorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException(
                    "Initiative requires an actor ID.",
                    nameof(actorId))
                : actorId;
            if (rollMaximum <= 0 || roll < 1 || roll > rollMaximum)
            {
                throw new ArgumentOutOfRangeException(nameof(roll));
            }
            Dexterity = dexterity;
            Roll = roll;
            RollMaximum = rollMaximum;
            Total = (long)dexterity + roll;
        }

        public string ActorId { get; }
        public int Dexterity { get; }
        public int Roll { get; }
        public int RollMaximum { get; }
        public long Total { get; }
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
            ActorInventorySnapshot inventory = null)
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

            ActorId = actorId;
            Pose = pose;
            TurnBudget = turnBudget;
            Wounds = wounds;
            EquippedItemId = equippedItemId;
            EquipmentEffects = equipmentEffects;
            MaximumWounds = maximumWounds;
            Inventory = resolvedInventory;
        }

        public string ActorId { get; }

        public GameplayActorPose Pose { get; }

        public TurnBudget TurnBudget { get; }

        public ActorWoundSnapshot Wounds { get; }

        public string EquippedItemId { get; }

        public EquipmentEffectSet EquipmentEffects { get; }

        public int MaximumWounds { get; }

        public ActorInventorySnapshot Inventory { get; }

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
            string nextActorId)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            EndingActorId = RequireActorId(endingActorId, nameof(endingActorId));
            NextActorId = RequireActorId(nextActorId, nameof(nextActorId));
            Sequence = sequence;
        }

        public long Sequence { get; }

        public string EndingActorId { get; }

        public string NextActorId { get; }

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

    public sealed class GameplaySession
    {
        private readonly Dictionary<string, ActorState> actors =
            new Dictionary<string, ActorState>(StringComparer.Ordinal);
        private readonly Dictionary<string, ObjectiveState> objectives =
            new Dictionary<string, ObjectiveState>(StringComparer.Ordinal);
        private readonly List<GameplayActionRecord> resolvedActions =
            new List<GameplayActionRecord>();
        private readonly IReadOnlyList<string> initiativeOrder;
        private readonly IReadOnlyList<GameplayInitiativeResult>
            initiativeResults;
        private readonly IReadOnlyList<GameplayActionRecord> readOnlyResolvedActions;
        private string activeActorId;
        private MovementRouteRecord pendingMovementRoute;
        private VoluntaryTurnCycleRecord pendingVoluntaryTurnCycle;
        private float voluntaryTurnReentrySecondsRemaining;
        private IReadOnlyList<string> emergencyResponders;
        private int emergencyResponderIndex = -1;
        private string emergencyResumeActorId;

        public GameplaySession(
            ScenarioDefinition scenario,
            GameplayJournal journal = null,
            uint scenarioSeed = 0u)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Journal = journal ?? new GameplayJournal();
            int participantCount = scenario.Actors.Count;
            var initiative = new List<GameplayInitiativeResult>(participantCount);
            foreach (ScenarioActorDefinition actor in scenario.Actors)
            {
                actors.Add(actor.Id, new ActorState(actor));
                initiative.Add(ResolveInitiative(
                    actor,
                    participantCount,
                    scenarioSeed));
            }
            initiative.Sort(CompareInitiative);
            var order = new List<string>(initiative.Count);
            foreach (GameplayInitiativeResult result in initiative)
                order.Add(result.ActorId);

            foreach (ScenarioObjectiveDefinition objective in scenario.Objectives)
            {
                objectives.Add(objective.Id, new ObjectiveState(objective));
            }

            initiativeOrder = order.AsReadOnly();
            initiativeResults = initiative.AsReadOnly();
            readOnlyResolvedActions = resolvedActions.AsReadOnly();
        }

        public ScenarioDefinition Scenario { get; }

        public GameplayJournal Journal { get; }

        public GameplaySessionMode Mode { get; private set; } =
            GameplaySessionMode.Exploration;

        public GameplaySessionOperation Operation { get; private set; } =
            GameplaySessionOperation.None;

        public TurnModeContext TurnContext { get; private set; } =
            TurnModeContext.None;

        public bool EncounterActive { get; private set; }

        public bool EncounterCompletionRequested { get; private set; }

        public IReadOnlyList<string> InitiativeOrder => initiativeOrder;

        public IReadOnlyList<GameplayInitiativeResult> InitiativeResults =>
            initiativeResults;

        public string ActiveActorId => activeActorId;

        public GameplayTurnPhase TurnPhase { get; private set; } =
            GameplayTurnPhase.Normal;

        public float VoluntaryTurnReentrySecondsRemaining =>
            voluntaryTurnReentrySecondsRemaining;

        public bool CanEnterTurnMode =>
            Mode == GameplaySessionMode.Exploration
            && (EncounterActive || voluntaryTurnReentrySecondsRemaining <= 0f);

        public MovementRouteRecord PendingMovementRoute => pendingMovementRoute;

        public VoluntaryTurnCycleRecord PendingVoluntaryTurnCycle =>
            pendingVoluntaryTurnCycle;

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
            get;
            private set;
        }

        public TurnEndRecord LastEndedTurn { get; private set; }

        public event Action<VoluntaryTurnCycleRecord> VoluntaryTurnCycleCompleted;

        public event Action<TurnEndRecord> TurnEnded;

        public event Action<EquipmentChangeRecord> EquipmentChanged;

        public event Action<GameplayActiveActorChange> ActiveActorChanged;

        public event Action<GameplayModeChange> ModeChanged;

        public event Action<string> ActorCapabilityChanged;

        public bool EnterTurnMode()
        {
            return TryEnterTurnMode(out _);
        }

        public bool TryEnterTurnMode(out TurnModeEntryFailure failure)
        {
            if (Mode == GameplaySessionMode.TurnBased)
            {
                failure = TurnModeEntryFailure.AlreadyInTurnMode;
                return false;
            }

            if (!EncounterActive && voluntaryTurnReentrySecondsRemaining > 0f)
            {
                failure = TurnModeEntryFailure.VoluntaryReentryLocked;
                return false;
            }

            if (!EncounterActive || activeActorId == null)
            {
                SetActiveActor(
                    FindNextCapableActor(startingAfterIndex: -1)
                        ?? initiativeOrder[0]);
            }

            GameplaySessionMode previousMode = Mode;
            SetMode(GameplaySessionMode.TurnBased);
            Operation = GameplaySessionOperation.None;
            TurnContext = EncounterActive
                ? TurnModeContext.InitiatedEncounter
                : TurnModeContext.Voluntary;
            Journal.RecordTurnModeChanged(
                previousMode,
                Mode,
                TurnContext,
                activeActorId);
            failure = TurnModeEntryFailure.None;
            return true;
        }

        public void AdvanceContinuousTime(float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds)
                || float.IsInfinity(elapsedSeconds)
                || elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (Mode != GameplaySessionMode.Exploration
                || EncounterActive
                || voluntaryTurnReentrySecondsRemaining <= 0f
                || elapsedSeconds == 0f)
            {
                return;
            }

            voluntaryTurnReentrySecondsRemaining = Math.Max(
                0f,
                voluntaryTurnReentrySecondsRemaining - elapsedSeconds);
        }

        public bool BeginEncounter()
        {
            if (EncounterActive)
            {
                return false;
            }

            EncounterActive = true;
            EncounterCompletionRequested = false;
            Journal.RecordEncounterChanged(isActive: true);
            if (Mode == GameplaySessionMode.Exploration)
            {
                return EnterTurnMode();
            }

            TurnContext = TurnModeContext.InitiatedEncounter;
            return true;
        }

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

        public bool CompleteEncounter()
        {
            if (!EncounterActive)
            {
                return false;
            }

            EncounterActive = false;
            EncounterCompletionRequested = false;
            if (Mode == GameplaySessionMode.TurnBased)
            {
                TurnContext = TurnModeContext.Voluntary;
            }

            Journal.RecordEncounterChanged(isActive: false);

            return true;
        }

        public bool RequestEncounterCompletionAtTurnEnd()
        {
            if (!EncounterActive || EncounterCompletionRequested)
            {
                return false;
            }

            EncounterCompletionRequested = true;
            return true;
        }

        public bool TryExitTurnMode(out TurnModeExitFailure failure)
        {
            if (Mode != GameplaySessionMode.TurnBased)
            {
                failure = TurnModeExitFailure.NotInTurnMode;
                return false;
            }

            if (Operation != GameplaySessionOperation.None)
            {
                failure = TurnModeExitFailure.OperationInProgress;
                return false;
            }

            if (EncounterActive)
            {
                failure = TurnModeExitFailure.EncounterActive;
                return false;
            }

            CompleteVoluntaryTurnCycleAndExit();
            failure = TurnModeExitFailure.None;
            return true;
        }

        public bool TryEndTurn(string actorId, out TurnEndFailure failure)
        {
            if (TurnPhase == GameplayTurnPhase.EmergencyReaction)
            {
                return TryEndEmergencyTurn(actorId, out _, out failure);
            }
            if (Mode != GameplaySessionMode.TurnBased)
            {
                failure = TurnEndFailure.NotInTurnMode;
                return false;
            }

            if (Operation != GameplaySessionOperation.None)
            {
                failure = TurnEndFailure.OperationInProgress;
                return false;
            }

            if (!string.Equals(activeActorId, actorId, StringComparison.Ordinal))
            {
                failure = TurnEndFailure.ActorNotActive;
                return false;
            }

            string endingActorId = activeActorId;
            if (!EncounterActive)
            {
                BeginVoluntaryWorldTurn();
                RecordTurnEnd(endingActorId, activeActorId);
                failure = TurnEndFailure.None;
                return true;
            }

            int activeIndex = 0;
            while (activeIndex < initiativeOrder.Count
                && !string.Equals(
                    initiativeOrder[activeIndex],
                    activeActorId,
                    StringComparison.Ordinal))
            {
                activeIndex++;
            }

            if (activeIndex >= initiativeOrder.Count)
            {
                throw new InvalidOperationException(
                    "The active actor is missing from initiative order.");
            }

            if (EncounterCompletionRequested)
            {
                RecordTurnEnd(endingActorId, endingActorId);
                CompleteEncounter();
                CompleteVoluntaryTurnCycleAndExit();
                failure = TurnEndFailure.None;
                return true;
            }

            string nextActorId = FindNextCapableActor(activeIndex)
                ?? endingActorId;
            actors[nextActorId].RefreshTurnBudget();
            SetActiveActor(nextActorId);
            RecordTurnEnd(endingActorId, activeActorId);
            failure = TurnEndFailure.None;
            return true;
        }

        public void BeginEmergencyReaction(
            string attackerId,
            IReadOnlyList<string> responderIds,
            int actionPointAllowance)
        {
            if (Mode != GameplaySessionMode.TurnBased || !EncounterActive
                || Operation != GameplaySessionOperation.None)
            {
                throw new InvalidOperationException("Emergency reactions require an idle encounter turn.");
            }
            if (TurnPhase != GameplayTurnPhase.Normal)
            {
                throw new InvalidOperationException("An emergency reaction is already active.");
            }
            if (responderIds == null || responderIds.Count == 0)
            {
                throw new ArgumentException(
                    "Emergency reactions require responders.",
                    nameof(responderIds));
            }
            if (actionPointAllowance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointAllowance));
            }
            RequireActor(attackerId);
            var responders = new List<string>(responderIds.Count);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string responderId in responderIds)
            {
                RequireActor(responderId);
                if (string.Equals(attackerId, responderId, StringComparison.Ordinal)
                    || !unique.Add(responderId))
                {
                    throw new ArgumentException(
                        "Emergency responders must be unique and cannot include the attacker.",
                        nameof(responderIds));
                }
                responders.Add(responderId);
            }
            emergencyResponders = responders.AsReadOnly();
            emergencyResponderIndex = 0;
            emergencyResumeActorId = attackerId;
            TurnPhase = GameplayTurnPhase.EmergencyReaction;
            string firstResponderId = emergencyResponders[0];
            actors[firstResponderId].BeginEmergencyTurn(actionPointAllowance);
            SetActiveActor(firstResponderId);
        }

        public bool TryEndEmergencyTurn(
            string actorId,
            out bool responsePassCompleted,
            out TurnEndFailure failure)
        {
            responsePassCompleted = false;
            if (TurnPhase != GameplayTurnPhase.EmergencyReaction)
            {
                return TryEndTurn(actorId, out failure);
            }
            if (Operation != GameplaySessionOperation.None)
            {
                failure = TurnEndFailure.OperationInProgress;
                return false;
            }
            if (!string.Equals(activeActorId, actorId, StringComparison.Ordinal))
            {
                failure = TurnEndFailure.ActorNotActive;
                return false;
            }
            string endingActorId = activeActorId;
            emergencyResponderIndex++;
            responsePassCompleted =
                emergencyResponderIndex >= emergencyResponders.Count;
            if (!responsePassCompleted)
            {
                string nextResponderId =
                    emergencyResponders[emergencyResponderIndex];
                actors[nextResponderId].BeginEmergencyTurn(
                    actors[endingActorId].EmergencyActionPointAllowance);
                SetActiveActor(nextResponderId);
            }
            RecordTurnEnd(
                endingActorId,
                responsePassCompleted ? emergencyResumeActorId : activeActorId);
            failure = TurnEndFailure.None;
            return true;
        }

        public void CompleteEmergencyReaction(string resumeActorId)
        {
            if (TurnPhase != GameplayTurnPhase.EmergencyReaction)
            {
                throw new InvalidOperationException("No emergency reaction is active.");
            }
            if (!string.Equals(
                    resumeActorId,
                    emergencyResumeActorId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Emergency reactions must resume their triggering attacker.");
            }
            ActorState actor = RequireActor(resumeActorId);
            emergencyResponders = null;
            emergencyResponderIndex = -1;
            emergencyResumeActorId = null;
            TurnPhase = GameplayTurnPhase.Normal;
            actor.RefreshTurnBudget();
            SetActiveActor(resumeActorId);
        }

        public bool CompleteVoluntaryWorldTurn()
        {
            if (Mode != GameplaySessionMode.TurnBased
                || EncounterActive
                || Operation != GameplaySessionOperation.ResolvingWorldTurn
                || pendingVoluntaryTurnCycle == null)
            {
                return false;
            }

            VoluntaryTurnCycleRecord completedCycle =
                pendingVoluntaryTurnCycle;
            pendingVoluntaryTurnCycle = null;
            LastCompletedVoluntaryTurnCycle = completedCycle;
            RefreshTurnBudgets();
            SetActiveActor(
                FindNextCapableActor(startingAfterIndex: -1)
                    ?? initiativeOrder[0]);
            Operation = GameplaySessionOperation.None;
            TurnContext = TurnModeContext.Voluntary;
            Journal.RecordVoluntaryTurnCycleCompleted(completedCycle);
            VoluntaryTurnCycleCompleted?.Invoke(completedCycle);
            return true;
        }

        public GameplayActorSnapshot GetActor(string actorId)
        {
            return RequireActor(actorId).CreateSnapshot();
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
                && actors.TryGetValue(actorId, out ActorState state))
            {
                actor = state.CreateSnapshot();
                return true;
            }

            actor = default(GameplayActorSnapshot);
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
            ActorState actor = RequireActor(actorId);
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
                && objectives.TryGetValue(objectiveId, out ObjectiveState state))
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

            RequireActor(actorId).Pose = pose;
        }

        public void SpendMovement(string actorId, float amount)
        {
            ActorState actor = RequireActiveActor(actorId);
            TurnBudget previousBudget = actor.TurnBudget;
            actor.TurnBudget = actor.TurnBudget.SpendMovement(amount);
            Journal.RecordMovementBudgetSpent(
                actorId,
                amount,
                previousBudget,
                actor.TurnBudget);
        }

        public void CommitStanceChange(StanceChangeRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            ActorState actor = Mode == GameplaySessionMode.TurnBased
                ? RequireActiveActor(record.ActorId)
                : RequireActor(record.ActorId);
            if (!PosesMatch(actor.Pose, record.PreviousPose))
            {
                throw new InvalidOperationException(
                    "The stance change no longer begins at the actor's authoritative pose.");
            }

            actor.Pose = record.ResultingPose;
            Journal.RecordStanceChanged(record);
        }

        public void CommitMovementRoute(MovementRouteRecord route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            ActorState actor = RequireActiveActor(route.ActorId);
            if (!PosesMatch(actor.Pose, route.OriginPose))
            {
                throw new InvalidOperationException(
                    "The movement route no longer begins at the actor's authoritative pose.");
            }

            TurnBudget updatedBudget =
                actor.TurnBudget.SpendMovement(route.TotalCost);
            actor.TurnBudget = updatedBudget;
            pendingMovementRoute = route;
            Operation = GameplaySessionOperation.ResolvingMovement;
            Journal.RecordMovementRouteCommitted(route);
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

            ActorState actor = RequireActor(record.Request.SubjectId);
            if (actor.Pose.Position.DistanceTo(record.PreviousPosition) > 0f)
            {
                throw new InvalidOperationException(
                    "Combatant displacement no longer starts at its authoritative pose.");
            }

            actor.Pose = new GameplayActorPose(
                record.ResultingPosition,
                actor.Pose.FacingDegrees,
                actor.Pose.Stance);
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
            ActorState actor = RequireActor(completedRoute.ActorId);
            actor.Pose = new GameplayActorPose(
                completedRoute.Destination,
                completedRoute.FinalFacingDegrees,
                actor.Pose.Stance);
            pendingMovementRoute = null;
            Operation = GameplaySessionOperation.None;
            Journal.RecordMovementRouteCompleted(completedRoute);
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

            ActorState actor = Mode == GameplaySessionMode.TurnBased
                ? RequireActiveActor(record.Request.ActorId)
                : RequireActor(record.Request.ActorId);
            actor.TurnBudget = record.ResultingBudget;
            foreach (GameplayActionOutcome outcome in record.Outcomes)
            {
                ApplyActionFacing(actor, outcome);
                ApplyActionOutcome(outcome, notifications);
            }

            resolvedActions.Add(record);
            Journal.RecordActionResolved(record);
        }

        private void ApplyActionFacing(
            ActorState actor,
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

            ActorState actor = Mode == GameplaySessionMode.TurnBased
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
            int participantCount,
            uint scenarioSeed)
        {
            uint seed = GameplayRandomStreams.DeriveSeed(
                scenarioSeed,
                GameplayRandomStreams.Initiative
                + "." + participantCount
                + "." + actor.Id);
            int roll = (int)(seed % (uint)participantCount) + 1;
            return new GameplayInitiativeResult(
                actor.Id,
                actor.Initiative,
                roll,
                participantCount);
        }

        private static int CompareInitiative(
            GameplayInitiativeResult left,
            GameplayInitiativeResult right)
        {
            int initiativeComparison = right.Total.CompareTo(left.Total);
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
                        ObjectiveState objective = RequireObjective(
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
                    ActorState actor = RequireActor(change.ActorId);
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

            ActorState target = RequireActor(attack.TargetId);
            ActorState attacker = RequireActor(attack.AttackerId);
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
            ActorState attacker = RequireActor(discharge.AttackerId);
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
            ActorState actor = RequireActor(thrown.ThrowerId);
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
            ActorState actor = RequireActor(change.ActorId);
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

            ActorState attacker = RequireActor(launch.AttackerId);
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

            ActorState actor = RequireActor(change.ActorId);
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

        private void BeginVoluntaryWorldTurn()
        {
            pendingVoluntaryTurnCycle = CreateVoluntaryTurnCycleRecord();
            Operation = GameplaySessionOperation.ResolvingWorldTurn;
        }

        private void CompleteVoluntaryTurnCycleAndExit()
        {
            VoluntaryTurnCycleRecord completedCycle =
                CreateVoluntaryTurnCycleRecord();
            LastCompletedVoluntaryTurnCycle = completedCycle;
            RefreshTurnBudgets();
            GameplaySessionMode previousMode = Mode;
            SetMode(GameplaySessionMode.Exploration);
            TurnContext = TurnModeContext.None;
            voluntaryTurnReentrySecondsRemaining =
                Scenario.Timing.MinimumVoluntaryTurnSeconds;
            Journal.RecordVoluntaryTurnCycleCompleted(completedCycle);
            Journal.RecordTurnModeChanged(
                previousMode,
                Mode,
                TurnContext,
                activeActorId);
            VoluntaryTurnCycleCompleted?.Invoke(completedCycle);
        }

        private void RecordTurnEnd(string endingActorId, string nextActorId)
        {
            var record = new TurnEndRecord(
                LastEndedTurn == null ? 1 : LastEndedTurn.Sequence + 1,
                endingActorId,
                nextActorId);
            LastEndedTurn = record;
            Journal.RecordTurnEnded(record);
            TurnEnded?.Invoke(record);
        }

        private void SetActiveActor(string actorId)
        {
            if (string.Equals(activeActorId, actorId, StringComparison.Ordinal))
                return;

            string previousActorId = activeActorId;
            activeActorId = actorId;
            ActiveActorChanged?.Invoke(new GameplayActiveActorChange(
                previousActorId,
                activeActorId));
        }

        private void SetMode(GameplaySessionMode mode)
        {
            if (Mode == mode)
                return;

            GameplaySessionMode previousMode = Mode;
            Mode = mode;
            ModeChanged?.Invoke(new GameplayModeChange(previousMode, Mode));
        }

        private void RefreshTurnBudgets()
        {
            foreach (string actorId in initiativeOrder)
            {
                actors[actorId].RefreshTurnBudget();
            }
        }

        private string FindNextCapableActor(int startingAfterIndex)
        {
            for (int offset = 1; offset <= initiativeOrder.Count; offset++)
            {
                int index = (startingAfterIndex + offset)
                    % initiativeOrder.Count;
                string candidateId = initiativeOrder[index];
                if (!actors[candidateId].IsIncapacitated)
                {
                    return candidateId;
                }
            }

            return null;
        }

        private ActorState RequireActiveActor(string actorId)
        {
            if (Mode != GameplaySessionMode.TurnBased)
            {
                throw new InvalidOperationException(
                    "Turn resources can only be used while turn mode is active.");
            }

            if (!string.Equals(activeActorId, actorId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Only the active actor can use turn resources.");
            }

            if (Operation != GameplaySessionOperation.None)
            {
                throw new InvalidOperationException(
                    "Turn commands cannot begin while another operation is resolving.");
            }

            ActorState actor = RequireActor(actorId);
            if (actor.IsIncapacitated)
            {
                throw new InvalidOperationException(
                    $"Incapacitated actor '{actorId}' cannot begin a turn command.");
            }

            return actor;
        }

        private ActorState RequireActor(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Actor identifiers cannot be empty.",
                    nameof(actorId));
            }

            if (!actors.TryGetValue(actorId, out ActorState actor))
            {
                throw new KeyNotFoundException(
                    $"Actor '{actorId}' does not belong to scenario '{Scenario.Id}'.");
            }

            return actor;
        }

        private ObjectiveState RequireObjective(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                throw new ArgumentException(
                    "Objective identifiers cannot be empty.",
                    nameof(objectiveId));
            }

            if (!objectives.TryGetValue(objectiveId, out ObjectiveState objective))
            {
                throw new KeyNotFoundException(
                    $"Objective '{objectiveId}' does not belong to scenario '{Scenario.Id}'.");
            }

            return objective;
        }

        private sealed class ActorState
        {
            private readonly TurnBudget turnBudgetAllowance;
            private readonly Dictionary<string, int> inventoryQuantities =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public ActorState(ScenarioActorDefinition definition)
            {
                ActorId = definition.Id;
                Pose = definition.StartingPose;
                TurnBudget = definition.StartingTurnBudget;
                Wounds = new ActorWoundSnapshot(definition.Id, 0, 0f);
                MaximumWounds = definition.Combat.MaximumWounds;
                EquippedItemId = definition.InitiallyEquippedItemId;
                EquipmentEffects = definition.GetInventoryItem(
                        EquippedItemId)?.EquippedEffects
                    ?? EquipmentEffectSet.None;
                foreach (InventoryItemDefinition item in definition.Inventory)
                {
                    if (item.Kind == InventoryItemKind.Consumable)
                    {
                        inventoryQuantities.Add(item.Id, item.InitialQuantity);
                    }
                }
                turnBudgetAllowance = definition.StartingTurnBudget;
            }

            public string ActorId { get; }

            public GameplayActorPose Pose { get; set; }

            public TurnBudget TurnBudget { get; set; }

            public int EmergencyActionPointAllowance { get; private set; }

            public int TurnActionPointAllowance =>
                turnBudgetAllowance.ActionPoints;

            public ActorWoundSnapshot Wounds { get; private set; }

            public int MaximumWounds { get; }

            public bool IsIncapacitated =>
                Wounds.WoundCount >= MaximumWounds;

            public string EquippedItemId { get; private set; }

            public EquipmentEffectSet EquipmentEffects { get; private set; }

            public void ApplyEquipment(InventoryItemDefinition item)
            {
                EquippedItemId = item?.Id;
                EquipmentEffects = item?.EquippedEffects
                    ?? EquipmentEffectSet.None;
            }

            public int GetInventoryQuantity(string itemId)
            {
                if (inventoryQuantities.TryGetValue(itemId, out int quantity))
                {
                    return quantity;
                }

                throw new KeyNotFoundException(
                    $"Consumable quantity '{itemId}' is not part of actor '{ActorId}'.");
            }

            public void ApplyInventoryQuantity(
                InventoryQuantityChangeRecord change)
            {
                inventoryQuantities[change.ItemId] = change.ResultingQuantity;
            }

            public void RefreshTurnBudget()
            {
                TurnBudget = new TurnBudget(
                    turnBudgetAllowance.ActionPoints,
                    WoundedMovementAllowance);
            }

            public void BeginEmergencyTurn(int actionPoints)
            {
                EmergencyActionPointAllowance = actionPoints;
                TurnBudget = new TurnBudget(
                    actionPoints,
                    WoundedMovementAllowance);
            }

            public void ApplyAttack(AttackResolutionRecord attack)
            {
                if (!attack.Hit)
                {
                    return;
                }

                Wounds = attack.TargetWoundsAfter;
                TurnBudget = new TurnBudget(
                    TurnBudget.ActionPoints,
                    Math.Min(
                        TurnBudget.MovementOpportunity,
                        WoundedMovementAllowance));
            }

            public void ApplyBlast(
                TargetRegionId? region,
                float movementPenalty)
            {
                if (movementPenalty <= 0f) return;
                Wounds = region.HasValue
                    ? Wounds.AddWound(region.Value, movementPenalty)
                    : Wounds.AddUnlocalizedWound(movementPenalty);
                TurnBudget = new TurnBudget(
                    TurnBudget.ActionPoints,
                    Math.Min(TurnBudget.MovementOpportunity, WoundedMovementAllowance));
            }

            public void FaceToward(GameplayPosition target)
            {
                double deltaX = (double)target.X - Pose.Position.X;
                double deltaZ = (double)target.Z - Pose.Position.Z;
                if (Math.Abs(deltaX) <= 0.0001
                    && Math.Abs(deltaZ) <= 0.0001)
                {
                    return;
                }

                float facingDegrees = (float)(
                    Math.Atan2(deltaX, deltaZ) * (180d / Math.PI));
                Pose = new GameplayActorPose(
                    Pose.Position,
                    facingDegrees,
                    Pose.Stance);
            }

            public GameplayActorSnapshot CreateSnapshot()
            {
                var quantities = new List<InventoryQuantitySnapshot>(
                    inventoryQuantities.Count);
                foreach (KeyValuePair<string, int> entry in inventoryQuantities)
                {
                    quantities.Add(new InventoryQuantitySnapshot(
                        entry.Key,
                        entry.Value));
                }
                quantities.Sort((left, right) => StringComparer.Ordinal.Compare(
                    left.ItemId,
                    right.ItemId));
                return new GameplayActorSnapshot(
                    ActorId,
                    Pose,
                    TurnBudget,
                    Wounds,
                    EquippedItemId,
                    EquipmentEffects,
                    MaximumWounds,
                    new ActorInventorySnapshot(ActorId, quantities));
            }

            private float WoundedMovementAllowance => Math.Max(
                0f,
                turnBudgetAllowance.MovementOpportunity
                    - Wounds.MovementPenalty);
        }

        private sealed class ObjectiveState
        {
            public ObjectiveState(ScenarioObjectiveDefinition definition)
            {
                ObjectiveId = definition.Id;
                Position = definition.Position;
                InteractionRadius = definition.InteractionRadius;
                Interaction = definition.Interaction;
            }

            public string ObjectiveId { get; }

            public GameplayPosition Position { get; }

            public float InteractionRadius { get; }

            public GameplayInteractionDefinition Interaction { get; }

            public bool IsCompleted { get; set; }

            public GameplayObjectiveSnapshot CreateSnapshot()
            {
                return new GameplayObjectiveSnapshot(
                    ObjectiveId,
                    Position,
                    InteractionRadius,
                    Interaction,
                    IsCompleted);
            }
        }
    }
}
