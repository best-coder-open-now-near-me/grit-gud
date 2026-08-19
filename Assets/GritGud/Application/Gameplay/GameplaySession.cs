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
            TurnActionPointEconomy? actionPointEconomy = null,
            float turnMovementAllowance = -1f,
            ActorPinState pinState = null,
            int emergencyActionPointAllowance = 0,
            TurnBudget? suspendedTurnBudget = null)
        {
            if (!string.Equals(actorId, wounds.ActorId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Actor snapshots and wound state must share an identifier.",
                    nameof(wounds));
            }
            if (maximumWounds <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumWounds));
            if (emergencyActionPointAllowance < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(emergencyActionPointAllowance));
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
            ActionPointEconomy = actionPointEconomy
                ?? new TurnActionPointEconomy(
                    turnBudget.ActionPoints,
                    turnBudget.ActionPoints,
                    Math.Max(1, turnBudget.ActionPoints));
            TurnMovementAllowance = turnMovementAllowance < 0f
                ? turnBudget.MovementOpportunity + wounds.MovementPenalty
                : turnMovementAllowance;
            PinState = pinState;
            EmergencyActionPointAllowance = emergencyActionPointAllowance;
            SuspendedTurnBudget = suspendedTurnBudget;
            if (float.IsNaN(TurnMovementAllowance)
                || float.IsInfinity(TurnMovementAllowance)
                || ActionPointEconomy.MaximumHeldActionPoints
                    < turnBudget.ActionPoints
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

        public TurnActionPointEconomy ActionPointEconomy { get; }

        public float TurnMovementAllowance { get; }

        public ActorPinState PinState { get; }

        public int EmergencyActionPointAllowance { get; }

        public TurnBudget? SuspendedTurnBudget { get; }

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
            TurnActionPointEconomy actionPointEconomy,
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
            ActionPointEconomy = actionPointEconomy;
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

        public TurnActionPointEconomy ActionPointEconomy { get; }

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
            IEnumerable<GameplayActorSnapshot> actors,
            IEnumerable<PersonalTurnStartRecord> personalTurnStarts = null)
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
            PersonalTurnStarts = new List<PersonalTurnStartRecord>(
                personalTurnStarts
                    ?? Array.Empty<PersonalTurnStartRecord>()).AsReadOnly();
        }

        public long Sequence { get; }

        public IReadOnlyList<GameplayActorSnapshot> Actors { get; }

        public IReadOnlyList<PersonalTurnStartRecord> PersonalTurnStarts { get; }
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

    public sealed partial class GameplaySession : IGameplayTurnLifecycleHost
    {
        private readonly Dictionary<string, GameplayActorState> actors =
            new Dictionary<string, GameplayActorState>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameplayObjectiveState> objectives =
            new Dictionary<string, GameplayObjectiveState>(StringComparer.Ordinal);
        private readonly List<GameplayActionRecord> resolvedActions =
            new List<GameplayActionRecord>();
        private readonly List<string> initiativeOrder;
        private readonly IReadOnlyList<string> allInitiativeOrder;
        private readonly IReadOnlyList<string> allActorIds;
        private readonly IReadOnlyList<GameplayInitiativeResult>
            initiativeResults;
        private readonly IReadOnlyList<GameplayActionRecord> readOnlyResolvedActions;
        private readonly GameplayTurnLifecycle turnLifecycle;
        private readonly GameplayActionCommitValidator actionCommitValidator;
        private readonly GameplayActionOutcomeApplier actionOutcomeApplier;
        private MovementRouteRecord pendingMovementRoute;
        private GameplayEncounterStateSnapshot encounterState;

        public GameplaySession(
            ScenarioDefinition scenario,
            GameplayJournal journal = null,
            uint scenarioSeed = 0u,
            GameplayPartySave restoredParty = null)
            : this(
                scenario,
                new ScenarioRunIdentity(
                    (scenario ?? throw new ArgumentNullException(nameof(scenario))).Id
                        + ".run",
                    scenarioSeed),
                journal,
                restoredParty)
        {
        }

        public GameplaySession(
            ScenarioDefinition scenario,
            ScenarioRunIdentity runIdentity,
            GameplayJournal journal = null,
            GameplayPartySave restoredParty = null)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            RunIdentity = runIdentity ?? throw new ArgumentNullException(
                nameof(runIdentity));
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
                    new GameplayActorState(
                        actor,
                        scenario.Timing,
                        restoredCharacter));
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

            initiativeOrder = new List<string>(order);
            allInitiativeOrder = new List<string>(order).AsReadOnly();
            allActorIds = new List<string>(order).AsReadOnly();
            initiativeResults = initiative.AsReadOnly();
            readOnlyResolvedActions = resolvedActions.AsReadOnly();
            turnLifecycle = new GameplayTurnLifecycle(this);
            actionCommitValidator = new GameplayActionCommitValidator(this);
            actionOutcomeApplier = new GameplayActionOutcomeApplier(this);
            var awareness = new List<EnemyAwarenessSnapshot>();
            foreach (ScenarioActorDefinition actor in scenario.Actors)
            {
                if (actor.Combat.EnemyBehavior != null)
                {
                    awareness.Add(new EnemyAwarenessSnapshot(
                        actor.Id,
                        EncounterAwarenessState.Unaware,
                        suspicion: 0));
                }
            }
            encounterState = new GameplayEncounterStateSnapshot(awareness);
        }

        public ScenarioDefinition Scenario { get; }

        public ScenarioRunIdentity RunIdentity { get; }

        public GameplayJournal Journal { get; }

        public GameplaySessionMode Mode => turnLifecycle.Mode;

        public GameplaySessionOperation Operation { get; private set; } =
            GameplaySessionOperation.None;

        public long Revision { get; private set; }

        public long LastTransitionSequence { get; private set; }

        public void RecordSemanticTransition(
            GameplayTransitionIdentity identity)
        {
            if (!actors.ContainsKey(identity.ActorId))
                throw new InvalidOperationException(
                    "Semantic transition actors must belong to the session.");
            RecordSemanticTransition(identity.Sequence);
        }

        private void RecordSemanticTransition(long sequence)
        {
            if (sequence != LastTransitionSequence + 1L)
                throw new InvalidOperationException(
                    "Semantic transitions must commit in sequence.");
            LastTransitionSequence = sequence;
        }

        public TurnModeContext TurnContext => turnLifecycle.TurnContext;

        public bool EncounterActive => turnLifecycle.EncounterActive;

        public bool EncounterCompletionRequested =>
            turnLifecycle.EncounterCompletionRequested;

        public IReadOnlyList<string> InitiativeOrder => initiativeOrder;

        public IReadOnlyList<string> AllInitiativeOrder => allInitiativeOrder;

        public IReadOnlyList<string> AllActorIds => allActorIds;

        public GameplayEncounterStateSnapshot EncounterState => encounterState;

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

        internal long NextActionSequence => LastResolvedAction == null
            ? 1L
            : LastResolvedAction.Sequence + 1L;

        internal IReadOnlyCollection<Type> ValidatedActionOutcomeTypes =>
            actionCommitValidator.SupportedOutcomeTypes;

        internal IReadOnlyCollection<Type> AppliedActionOutcomeTypes =>
            actionOutcomeApplier.SupportedOutcomeTypes;

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

        /// <summary>
        /// Raised after an authoritative action has committed. Exploration
        /// systems use this to capture transient world evidence such as sound;
        /// they never infer it from presentation effects.
        /// </summary>
        public event Action<GameplayActionRecord> ActionResolved;

        public bool EnterTurnMode()
        {
            return TryEnterTurnMode(out _);
        }

        public bool TryEnterTurnMode(out TurnModeEntryFailure failure) =>
            turnLifecycle.TryEnterTurnMode(out failure);

        public void AdvanceContinuousTime(float elapsedSeconds) =>
            turnLifecycle.AdvanceContinuousTime(elapsedSeconds);

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

        public TurnActionPointEconomy GetActionPointEconomy(string actorId)
        {
            return RequireActor(actorId).ActionPointEconomy;
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

            GameplayActorState actor = RequireActionActor(record.Request.ActorId);
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
                actionOutcomeApplier.Apply(
                    actor,
                    outcome,
                    notifications,
                    ActorCapabilityChanged,
                    EquipmentChanged);
            }

            resolvedActions.Add(record);
            Journal.RecordActionResolved(record);
            notifications.Add(ActionResolved, record);
            MarkStateChanged();
        }

        internal void ValidateActionCommit(GameplayActionRecord record)
            => actionCommitValidator.Validate(record);

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

        internal ScenarioActorDefinition RequireActorDefinition(string actorId)
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

        PersonalTurnStartRecord
            IGameplayTurnLifecycleHost.StartPersonalTurnForTurnLifecycle(
                string actorId) => RequireActor(actorId).StartPersonalTurn();

        IReadOnlyList<PersonalTurnStartRecord>
            IGameplayTurnLifecycleHost.StartCapablePersonalTurnsForTurnLifecycle()
        {
            var starts = new List<PersonalTurnStartRecord>();
            foreach (string actorId in initiativeOrder)
                if (!actors[actorId].IsIncapacitated)
                    starts.Add(actors[actorId].StartPersonalTurn());
            return starts.AsReadOnly();
        }

        void IGameplayTurnLifecycleHost.BeginEmergencyTurnForTurnLifecycle(
            string actorId,
            int actionPointAllowance) =>
            RequireActor(actorId).BeginEmergencyTurn(actionPointAllowance);

        void IGameplayTurnLifecycleHost.EndEmergencyTurnForTurnLifecycle(
            string actorId) => RequireActor(actorId).EndEmergencyTurn();

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

        internal GameplayActorState RequireActionActor(string actorId) =>
            Mode == GameplaySessionMode.TurnBased
                ? RequireActiveActor(actorId)
                : RequireActor(actorId);

        internal GameplayActorState RequireActor(string actorId)
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

        internal GameplayObjectiveState RequireObjective(string objectiveId)
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
