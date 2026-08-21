using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum GameplayJournalEntryKind
    {
        TurnModeChanged,
        EncounterChanged,
        MovementBudgetSpent,
        StanceChanged,
        MovementRouteCommitted,
        MovementRouteCompleted,
        ActionResolved,
        DisplacementResolved,
        DestructibleDamaged,
        VehicleMomentumResolved,
        ProjectileAdvanced,
        TurnEnded,
        VoluntaryTurnCycleCompleted,
        EmergencyReactionChanged,
        EnemyAwarenessChanged,
        PatrolAdvanced,
        DroneMoved,
        DroneAttackResolved,
        ActorDroneAttackResolved,
    }

    public sealed class DroneMovedJournalEntry : GameplayJournalEntry
    {
        public DroneMovedJournalEntry(long sequence, DroneMoveRecord movement)
            : base(sequence, GameplayJournalEntryKind.DroneMoved)
        {
            Movement = movement ?? throw new ArgumentNullException(
                nameof(movement));
        }

        public DroneMoveRecord Movement { get; }
    }

    public sealed class DroneAttackResolvedJournalEntry : GameplayJournalEntry
    {
        public DroneAttackResolvedJournalEntry(
            long sequence,
            DroneAttackRecord attack)
            : base(sequence, GameplayJournalEntryKind.DroneAttackResolved)
        {
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));
        }

        public DroneAttackRecord Attack { get; }
    }

    public sealed class ActorDroneAttackResolvedJournalEntry :
        GameplayJournalEntry
    {
        public ActorDroneAttackResolvedJournalEntry(
            long sequence,
            ActorDroneAttackRecord attack)
            : base(sequence, GameplayJournalEntryKind.ActorDroneAttackResolved)
        {
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));
        }

        public ActorDroneAttackRecord Attack { get; }
    }

    public sealed class EnemyAwarenessChangedJournalEntry :
        GameplayJournalEntry
    {
        public EnemyAwarenessChangedJournalEntry(
            long sequence,
            EnemyAwarenessTransitionRecord transition)
            : base(sequence, GameplayJournalEntryKind.EnemyAwarenessChanged)
        {
            Transition = transition ?? throw new ArgumentNullException(
                nameof(transition));
        }

        public EnemyAwarenessTransitionRecord Transition { get; }
    }

    public sealed class PatrolAdvancedJournalEntry : GameplayJournalEntry
    {
        public PatrolAdvancedJournalEntry(long sequence, PatrolAdvanceRecord advance)
            : base(sequence, GameplayJournalEntryKind.PatrolAdvanced)
        {
            Advance = advance ?? throw new ArgumentNullException(nameof(advance));
        }

        public PatrolAdvanceRecord Advance { get; }
    }

    public sealed class EmergencyReactionChangedJournalEntry : GameplayJournalEntry
    {
        public EmergencyReactionChangedJournalEntry(long sequence, EmergencyReactionWindowRecord window)
            : base(sequence, GameplayJournalEntryKind.EmergencyReactionChanged)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public EmergencyReactionWindowRecord Window { get; }
    }

    public abstract class GameplayJournalEntry
    {
        protected GameplayJournalEntry(
            long sequence,
            GameplayJournalEntryKind kind)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Sequence = sequence;
            Kind = kind;
        }

        public long Sequence { get; }

        public GameplayJournalEntryKind Kind { get; }
    }

    public sealed class TurnModeChangedJournalEntry : GameplayJournalEntry
    {
        public TurnModeChangedJournalEntry(
            long sequence,
            GameplaySessionMode previousMode,
            GameplaySessionMode resultingMode,
            TurnModeContext context,
            string activeActorId)
            : base(sequence, GameplayJournalEntryKind.TurnModeChanged)
        {
            if (previousMode == resultingMode)
            {
                throw new ArgumentException(
                    "A turn-mode journal entry requires a state change.",
                    nameof(resultingMode));
            }

            PreviousMode = previousMode;
            ResultingMode = resultingMode;
            Context = context;
            ActiveActorId = activeActorId ?? string.Empty;
        }

        public GameplaySessionMode PreviousMode { get; }

        public GameplaySessionMode ResultingMode { get; }

        public TurnModeContext Context { get; }

        public string ActiveActorId { get; }
    }

    public sealed class EncounterChangedJournalEntry : GameplayJournalEntry
    {
        public EncounterChangedJournalEntry(long sequence, bool isActive)
            : this(sequence, isActive, Array.Empty<string>())
        {
        }

        public EncounterChangedJournalEntry(
            long sequence,
            bool isActive,
            IEnumerable<string> participantIds)
            : base(sequence, GameplayJournalEntryKind.EncounterChanged)
        {
            IsActive = isActive;
            var copy = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string participantId in participantIds
                ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(participantId)
                    || !unique.Add(participantId))
                {
                    throw new ArgumentException(
                        "Encounter participant IDs must be unique and non-empty.",
                        nameof(participantIds));
                }
                copy.Add(participantId);
            }
            ParticipantIds = copy.AsReadOnly();
        }

        public bool IsActive { get; }

        public IReadOnlyList<string> ParticipantIds { get; }
    }

    public sealed class MovementBudgetSpentJournalEntry : GameplayJournalEntry
    {
        public MovementBudgetSpentJournalEntry(
            long sequence,
            string actorId,
            float amount,
            TurnBudget previousBudget,
            TurnBudget resultingBudget)
            : base(sequence, GameplayJournalEntryKind.MovementBudgetSpent)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Movement spending requires an actor identifier.",
                    nameof(actorId));
            }

            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            ActorId = actorId;
            Amount = amount;
            PreviousBudget = previousBudget;
            ResultingBudget = resultingBudget;
        }

        public string ActorId { get; }

        public float Amount { get; }

        public TurnBudget PreviousBudget { get; }

        public TurnBudget ResultingBudget { get; }
    }

    public sealed class StanceChangedJournalEntry : GameplayJournalEntry
    {
        public StanceChangedJournalEntry(long sequence, StanceChangeRecord stanceChange)
            : base(sequence, GameplayJournalEntryKind.StanceChanged)
        {
            StanceChange = stanceChange ??
                throw new ArgumentNullException(nameof(stanceChange));
        }

        public StanceChangeRecord StanceChange { get; }
    }

    public sealed class MovementRouteCommittedJournalEntry : GameplayJournalEntry
    {
        public MovementRouteCommittedJournalEntry(
            long sequence,
            MovementRouteRecord route)
            : base(sequence, GameplayJournalEntryKind.MovementRouteCommitted)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
        }

        public MovementRouteRecord Route { get; }
    }

    public sealed class MovementRouteCompletedJournalEntry : GameplayJournalEntry
    {
        public MovementRouteCompletedJournalEntry(
            long sequence,
            MovementRouteRecord route)
            : base(sequence, GameplayJournalEntryKind.MovementRouteCompleted)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
        }

        public MovementRouteRecord Route { get; }
    }

    public sealed class ActionResolvedJournalEntry : GameplayJournalEntry
    {
        public ActionResolvedJournalEntry(long sequence, GameplayActionRecord action)
            : base(sequence, GameplayJournalEntryKind.ActionResolved)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public GameplayActionRecord Action { get; }
    }

    public sealed class DisplacementResolvedJournalEntry : GameplayJournalEntry
    {
        public DisplacementResolvedJournalEntry(
            long sequence,
            DisplacementRecord displacement)
            : base(sequence, GameplayJournalEntryKind.DisplacementResolved)
        {
            Displacement = displacement ??
                throw new ArgumentNullException(nameof(displacement));
        }

        public DisplacementRecord Displacement { get; }
    }

    public sealed class DestructibleDamagedJournalEntry : GameplayJournalEntry
    {
        public DestructibleDamagedJournalEntry(
            long sequence,
            DestructibleDamageRecord damage)
            : base(sequence, GameplayJournalEntryKind.DestructibleDamaged)
        {
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
        }

        public DestructibleDamageRecord Damage { get; }
    }

    public sealed class VehicleMomentumResolvedJournalEntry : GameplayJournalEntry
    {
        public VehicleMomentumResolvedJournalEntry(
            long sequence,
            VehicleMomentumRecord momentum)
            : base(sequence, GameplayJournalEntryKind.VehicleMomentumResolved)
        {
            Momentum = momentum ?? throw new ArgumentNullException(nameof(momentum));
        }

        public VehicleMomentumRecord Momentum { get; }
    }

    public sealed class ProjectileAdvancedJournalEntry : GameplayJournalEntry
    {
        public ProjectileAdvancedJournalEntry(
            long sequence,
            ProjectileAdvanceRecord advance)
            : base(sequence, GameplayJournalEntryKind.ProjectileAdvanced)
        {
            Advance = advance ?? throw new ArgumentNullException(nameof(advance));
        }

        public ProjectileAdvanceRecord Advance { get; }
    }

    public sealed class TurnEndedJournalEntry : GameplayJournalEntry
    {
        public TurnEndedJournalEntry(long sequence, TurnEndRecord turn)
            : base(sequence, GameplayJournalEntryKind.TurnEnded)
        {
            Turn = turn ?? throw new ArgumentNullException(nameof(turn));
        }

        public TurnEndRecord Turn { get; }
    }

    public sealed class VoluntaryTurnCycleCompletedJournalEntry : GameplayJournalEntry
    {
        public VoluntaryTurnCycleCompletedJournalEntry(
            long sequence,
            VoluntaryTurnCycleRecord cycle)
            : base(sequence, GameplayJournalEntryKind.VoluntaryTurnCycleCompleted)
        {
            Cycle = cycle ?? throw new ArgumentNullException(nameof(cycle));
        }

        public VoluntaryTurnCycleRecord Cycle { get; }
    }

    public sealed class GameplayJournal
    {
        private readonly List<GameplayJournalEntry> entries =
            new List<GameplayJournalEntry>();
        private readonly IReadOnlyList<GameplayJournalEntry> readOnlyEntries;

        public GameplayJournal()
        {
            readOnlyEntries = entries.AsReadOnly();
        }

        public IReadOnlyList<GameplayJournalEntry> Entries => readOnlyEntries;

        public GameplayJournalEntry LastEntry => entries.Count == 0
            ? null
            : entries[entries.Count - 1];

        internal void RecordTurnModeChanged(
            GameplaySessionMode previousMode,
            GameplaySessionMode resultingMode,
            TurnModeContext context,
            string activeActorId) =>
            Append(new TurnModeChangedJournalEntry(
                NextSequence,
                previousMode,
                resultingMode,
                context,
                activeActorId));

        internal void RecordEncounterChanged(
            bool isActive,
            IEnumerable<string> participantIds = null) =>
            Append(new EncounterChangedJournalEntry(
                NextSequence,
                isActive,
                participantIds));

        internal void RecordMovementBudgetSpent(
            string actorId,
            float amount,
            TurnBudget previousBudget,
            TurnBudget resultingBudget) =>
            Append(new MovementBudgetSpentJournalEntry(
                NextSequence,
                actorId,
                amount,
                previousBudget,
                resultingBudget));

        internal void RecordStanceChanged(StanceChangeRecord record) =>
            Append(new StanceChangedJournalEntry(NextSequence, record));

        internal void RecordMovementRouteCommitted(MovementRouteRecord route) =>
            Append(new MovementRouteCommittedJournalEntry(NextSequence, route));

        internal void RecordMovementRouteCompleted(MovementRouteRecord route) =>
            Append(new MovementRouteCompletedJournalEntry(NextSequence, route));

        internal void RecordActionResolved(GameplayActionRecord action) =>
            Append(new ActionResolvedJournalEntry(NextSequence, action));

        internal void RecordDisplacementResolved(DisplacementRecord record) =>
            Append(new DisplacementResolvedJournalEntry(NextSequence, record));

        internal void RecordDestructibleDamaged(DestructibleDamageRecord record) =>
            Append(new DestructibleDamagedJournalEntry(NextSequence, record));

        internal void RecordVehicleMomentumResolved(VehicleMomentumRecord record) =>
            Append(new VehicleMomentumResolvedJournalEntry(NextSequence, record));

        internal void RecordDroneMoved(DroneMoveRecord record) =>
            Append(new DroneMovedJournalEntry(NextSequence, record));

        internal void RecordDroneAttackResolved(DroneAttackRecord record) =>
            Append(new DroneAttackResolvedJournalEntry(NextSequence, record));

        internal void RecordActorDroneAttackResolved(
            ActorDroneAttackRecord record) => Append(
                new ActorDroneAttackResolvedJournalEntry(NextSequence, record));

        internal void RecordProjectileAdvanced(ProjectileAdvanceRecord record) =>
            Append(new ProjectileAdvancedJournalEntry(NextSequence, record));

        internal void RecordEmergencyReactionChanged(EmergencyReactionWindowRecord window) =>
            Append(new EmergencyReactionChangedJournalEntry(NextSequence, window));

        internal void RecordTurnEnded(TurnEndRecord turn) =>
            Append(new TurnEndedJournalEntry(NextSequence, turn));

        internal void RecordVoluntaryTurnCycleCompleted(
            VoluntaryTurnCycleRecord cycle) =>
            Append(new VoluntaryTurnCycleCompletedJournalEntry(
                NextSequence,
                cycle));

        internal void RecordEnemyAwareness(
            EnemyAwarenessTransitionRecord transition) =>
            Append(new EnemyAwarenessChangedJournalEntry(
                NextSequence,
                transition));

        internal void RecordPatrolAdvance(PatrolAdvanceRecord advance) =>
            Append(new PatrolAdvancedJournalEntry(NextSequence, advance));

        private long NextSequence => entries.Count + 1L;

        private void Append(GameplayJournalEntry entry)
        {
            if (entry.Sequence != NextSequence)
            {
                throw new InvalidOperationException(
                    "The gameplay journal entry is out of sequence.");
            }

            entries.Add(entry);
        }
    }
}
