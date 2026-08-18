using System;
using System.Collections.Generic;

namespace GritGud.Application.Gameplay
{
    internal interface IGameplayTurnLifecycleHost
    {
        GameplayJournal Journal { get; }

        GameplaySessionOperation Operation { get; set; }

        IReadOnlyList<string> InitiativeOrder { get; }

        float MinimumVoluntaryTurnSeconds { get; }

        void RequireActorForTurnLifecycle(string actorId);

        bool IsActorIncapacitatedForTurnLifecycle(string actorId);

        void RefreshTurnBudgetForTurnLifecycle(string actorId);

        void RefreshAllTurnBudgetsForTurnLifecycle();

        void BeginEmergencyTurnForTurnLifecycle(
            string actorId,
            int actionPointAllowance);

        int GetEmergencyActionPointAllowanceForTurnLifecycle(string actorId);

        VoluntaryTurnCycleRecord CreateVoluntaryTurnCycleRecordForTurnLifecycle();

        void MarkStateChangedForTurnLifecycle();
    }

    internal sealed class GameplayTurnLifecycle
    {
        private readonly IGameplayTurnLifecycleHost host;
        private string activeActorId;
        private VoluntaryTurnCycleRecord pendingVoluntaryTurnCycle;
        private float voluntaryTurnReentrySecondsRemaining;
        private IReadOnlyList<string> emergencyResponders;
        private int emergencyResponderIndex = -1;
        private string emergencyResumeActorId;

        public GameplayTurnLifecycle(IGameplayTurnLifecycleHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public GameplaySessionMode Mode { get; private set; } =
            GameplaySessionMode.Exploration;

        public TurnModeContext TurnContext { get; private set; } =
            TurnModeContext.None;

        public bool EncounterActive { get; private set; }

        public bool EncounterCompletionRequested { get; private set; }

        public IReadOnlyList<string> EmergencyResponders =>
            emergencyResponders ?? Array.Empty<string>();

        public int EmergencyResponderIndex => emergencyResponderIndex;

        public string EmergencyResumeActorId =>
            emergencyResumeActorId ?? string.Empty;

        public string ActiveActorId => activeActorId;

        public GameplayTurnPhase TurnPhase { get; private set; } =
            GameplayTurnPhase.Normal;

        public float VoluntaryTurnReentrySecondsRemaining =>
            voluntaryTurnReentrySecondsRemaining;

        public bool CanEnterTurnMode =>
            Mode == GameplaySessionMode.Exploration
            && (EncounterActive || voluntaryTurnReentrySecondsRemaining <= 0f);

        public VoluntaryTurnCycleRecord PendingVoluntaryTurnCycle =>
            pendingVoluntaryTurnCycle;

        public VoluntaryTurnCycleRecord LastCompletedVoluntaryTurnCycle
        {
            get;
            private set;
        }

        public TurnEndRecord LastEndedTurn { get; private set; }

        public event Action<VoluntaryTurnCycleRecord> VoluntaryTurnCycleCompleted;

        public event Action<TurnEndRecord> TurnEnded;

        public event Action<GameplayActiveActorChange> ActiveActorChanged;

        public event Action<GameplayModeChange> ModeChanged;

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

            var notifications = new GameplayNotificationBatch();

            if (!EncounterActive || activeActorId == null)
            {
                SetActiveActor(
                    FindNextCapableActor(startingAfterIndex: -1)
                        ?? host.InitiativeOrder[0],
                    notifications);
            }

            GameplaySessionMode previousMode = Mode;
            SetMode(GameplaySessionMode.TurnBased, notifications);
            host.Operation = GameplaySessionOperation.None;
            TurnContext = EncounterActive
                ? TurnModeContext.InitiatedEncounter
                : TurnModeContext.Voluntary;
            host.Journal.RecordTurnModeChanged(
                previousMode,
                Mode,
                TurnContext,
                activeActorId);
            failure = TurnModeEntryFailure.None;
            PublishCommitted(notifications);
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
            host.MarkStateChangedForTurnLifecycle();
        }

        public bool BeginEncounter(
            IEnumerable<string> participantIds = null)
        {
            if (EncounterActive)
                return false;

            EncounterActive = true;
            EncounterCompletionRequested = false;
            host.Journal.RecordEncounterChanged(
                isActive: true,
                participantIds: participantIds ?? host.InitiativeOrder);
            if (Mode == GameplaySessionMode.Exploration)
                return TryEnterTurnMode(out _);

            var notifications = new GameplayNotificationBatch();
            if (string.IsNullOrWhiteSpace(activeActorId)
                || !Contains(host.InitiativeOrder, activeActorId))
            {
                SetActiveActor(
                    FindNextCapableActor(startingAfterIndex: -1)
                        ?? host.InitiativeOrder[0],
                    notifications);
            }
            TurnContext = TurnModeContext.InitiatedEncounter;
            host.MarkStateChangedForTurnLifecycle();
            notifications.Publish();
            return true;
        }

        public bool CompleteEncounter()
        {
            if (!EncounterActive)
                return false;

            EncounterActive = false;
            EncounterCompletionRequested = false;
            if (Mode == GameplaySessionMode.TurnBased)
                TurnContext = TurnModeContext.Voluntary;

            host.Journal.RecordEncounterChanged(
                isActive: false,
                participantIds: host.InitiativeOrder);
            host.MarkStateChangedForTurnLifecycle();
            return true;
        }

        public bool RequestEncounterCompletionAtTurnEnd()
        {
            if (!EncounterActive || EncounterCompletionRequested)
                return false;

            EncounterCompletionRequested = true;
            host.MarkStateChangedForTurnLifecycle();
            return true;
        }

        public bool TryExitTurnMode(out TurnModeExitFailure failure)
        {
            if (Mode != GameplaySessionMode.TurnBased)
            {
                failure = TurnModeExitFailure.NotInTurnMode;
                return false;
            }

            if (host.Operation != GameplaySessionOperation.None)
            {
                failure = TurnModeExitFailure.OperationInProgress;
                return false;
            }

            if (EncounterActive)
            {
                failure = TurnModeExitFailure.EncounterActive;
                return false;
            }

            var notifications = new GameplayNotificationBatch();
            CompleteVoluntaryTurnCycleAndExit(notifications);
            failure = TurnModeExitFailure.None;
            PublishCommitted(notifications);
            return true;
        }

        public bool TryEndTurn(string actorId, out TurnEndFailure failure)
        {
            if (TurnPhase == GameplayTurnPhase.EmergencyReaction)
                return TryEndEmergencyTurn(actorId, out _, out failure);
            if (Mode != GameplaySessionMode.TurnBased)
            {
                failure = TurnEndFailure.NotInTurnMode;
                return false;
            }

            if (host.Operation != GameplaySessionOperation.None)
            {
                failure = TurnEndFailure.OperationInProgress;
                return false;
            }

            if (!string.Equals(activeActorId, actorId, StringComparison.Ordinal))
            {
                failure = TurnEndFailure.ActorNotActive;
                return false;
            }

            var notifications = new GameplayNotificationBatch();
            string endingActorId = activeActorId;
            if (!EncounterActive)
            {
                BeginVoluntaryWorldTurn();
                RecordTurnEnd(
                    endingActorId,
                    activeActorId,
                    notifications);
                failure = TurnEndFailure.None;
                PublishCommitted(notifications);
                return true;
            }

            int activeIndex = 0;
            while (activeIndex < host.InitiativeOrder.Count
                && !string.Equals(
                    host.InitiativeOrder[activeIndex],
                    activeActorId,
                    StringComparison.Ordinal))
            {
                activeIndex++;
            }

            if (activeIndex >= host.InitiativeOrder.Count)
            {
                throw new InvalidOperationException(
                    "The active actor is missing from initiative order.");
            }

            if (EncounterCompletionRequested)
            {
                RecordTurnEnd(
                    endingActorId,
                    endingActorId,
                    notifications);
                CompleteEncounter();
                CompleteVoluntaryTurnCycleAndExit(notifications);
                failure = TurnEndFailure.None;
                PublishCommitted(notifications);
                return true;
            }

            string nextActorId = FindNextCapableActor(activeIndex)
                ?? endingActorId;
            host.RefreshTurnBudgetForTurnLifecycle(nextActorId);
            SetActiveActor(nextActorId, notifications);
            RecordTurnEnd(
                endingActorId,
                activeActorId,
                notifications);
            failure = TurnEndFailure.None;
            PublishCommitted(notifications);
            return true;
        }

        public void BeginEmergencyReaction(
            string attackerId,
            IReadOnlyList<string> responderIds,
            int actionPointAllowance)
        {
            if (Mode != GameplaySessionMode.TurnBased || !EncounterActive
                || host.Operation != GameplaySessionOperation.None)
            {
                throw new InvalidOperationException(
                    "Emergency reactions require an idle encounter turn.");
            }
            if (TurnPhase != GameplayTurnPhase.Normal)
            {
                throw new InvalidOperationException(
                    "An emergency reaction is already active.");
            }
            if (responderIds == null || responderIds.Count == 0)
            {
                throw new ArgumentException(
                    "Emergency reactions require responders.",
                    nameof(responderIds));
            }
            if (actionPointAllowance <= 0)
                throw new ArgumentOutOfRangeException(nameof(actionPointAllowance));

            host.RequireActorForTurnLifecycle(attackerId);
            var responders = new List<string>(responderIds.Count);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string responderId in responderIds)
            {
                host.RequireActorForTurnLifecycle(responderId);
                if (string.Equals(
                        attackerId,
                        responderId,
                        StringComparison.Ordinal)
                    || !unique.Add(responderId))
                {
                    throw new ArgumentException(
                        "Emergency responders must be unique and cannot include the attacker.",
                        nameof(responderIds));
                }
                responders.Add(responderId);
            }

            var notifications = new GameplayNotificationBatch();
            emergencyResponders = responders.AsReadOnly();
            emergencyResponderIndex = 0;
            emergencyResumeActorId = attackerId;
            TurnPhase = GameplayTurnPhase.EmergencyReaction;
            string firstResponderId = emergencyResponders[0];
            host.BeginEmergencyTurnForTurnLifecycle(
                firstResponderId,
                actionPointAllowance);
            SetActiveActor(firstResponderId, notifications);
            PublishCommitted(notifications);
        }

        public bool TryEndEmergencyTurn(
            string actorId,
            out bool responsePassCompleted,
            out TurnEndFailure failure)
        {
            responsePassCompleted = false;
            if (TurnPhase != GameplayTurnPhase.EmergencyReaction)
                return TryEndTurn(actorId, out failure);
            if (host.Operation != GameplaySessionOperation.None)
            {
                failure = TurnEndFailure.OperationInProgress;
                return false;
            }
            if (!string.Equals(activeActorId, actorId, StringComparison.Ordinal))
            {
                failure = TurnEndFailure.ActorNotActive;
                return false;
            }

            var notifications = new GameplayNotificationBatch();
            string endingActorId = activeActorId;
            emergencyResponderIndex++;
            responsePassCompleted =
                emergencyResponderIndex >= emergencyResponders.Count;
            if (!responsePassCompleted)
            {
                string nextResponderId =
                    emergencyResponders[emergencyResponderIndex];
                host.BeginEmergencyTurnForTurnLifecycle(
                    nextResponderId,
                    host.GetEmergencyActionPointAllowanceForTurnLifecycle(
                        endingActorId));
                SetActiveActor(nextResponderId, notifications);
            }
            RecordTurnEnd(
                endingActorId,
                responsePassCompleted ? emergencyResumeActorId : activeActorId,
                notifications,
                GameplayTurnKind.EmergencyReaction,
                emergencyResumeActorId);
            failure = TurnEndFailure.None;
            PublishCommitted(notifications);
            return true;
        }

        public void CompleteEmergencyReaction(string resumeActorId)
        {
            if (TurnPhase != GameplayTurnPhase.EmergencyReaction)
            {
                throw new InvalidOperationException(
                    "No emergency reaction is active.");
            }
            if (!string.Equals(
                    resumeActorId,
                    emergencyResumeActorId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Emergency reactions must resume their triggering attacker.");
            }
            host.RequireActorForTurnLifecycle(resumeActorId);
            var notifications = new GameplayNotificationBatch();
            emergencyResponders = null;
            emergencyResponderIndex = -1;
            emergencyResumeActorId = null;
            TurnPhase = GameplayTurnPhase.Normal;
            host.RefreshTurnBudgetForTurnLifecycle(resumeActorId);
            SetActiveActor(resumeActorId, notifications);
            PublishCommitted(notifications);
        }

        public bool CompleteVoluntaryWorldTurn()
        {
            if (Mode != GameplaySessionMode.TurnBased
                || EncounterActive
                || host.Operation
                    != GameplaySessionOperation.ResolvingWorldTurn
                || pendingVoluntaryTurnCycle == null)
            {
                return false;
            }

            VoluntaryTurnCycleRecord completedCycle =
                pendingVoluntaryTurnCycle;
            var notifications = new GameplayNotificationBatch();
            pendingVoluntaryTurnCycle = null;
            LastCompletedVoluntaryTurnCycle = completedCycle;
            host.RefreshAllTurnBudgetsForTurnLifecycle();
            SetActiveActor(
                FindNextCapableActor(startingAfterIndex: -1)
                    ?? host.InitiativeOrder[0],
                notifications);
            host.Operation = GameplaySessionOperation.None;
            TurnContext = TurnModeContext.Voluntary;
            host.Journal.RecordVoluntaryTurnCycleCompleted(completedCycle);
            notifications.Add(VoluntaryTurnCycleCompleted, completedCycle);
            PublishCommitted(notifications);
            return true;
        }

        private void BeginVoluntaryWorldTurn()
        {
            pendingVoluntaryTurnCycle =
                host.CreateVoluntaryTurnCycleRecordForTurnLifecycle();
            host.Operation = GameplaySessionOperation.ResolvingWorldTurn;
        }

        private void CompleteVoluntaryTurnCycleAndExit(
            GameplayNotificationBatch notifications)
        {
            VoluntaryTurnCycleRecord completedCycle =
                host.CreateVoluntaryTurnCycleRecordForTurnLifecycle();
            LastCompletedVoluntaryTurnCycle = completedCycle;
            host.RefreshAllTurnBudgetsForTurnLifecycle();
            GameplaySessionMode previousMode = Mode;
            SetMode(GameplaySessionMode.Exploration, notifications);
            TurnContext = TurnModeContext.None;
            voluntaryTurnReentrySecondsRemaining =
                host.MinimumVoluntaryTurnSeconds;
            host.Journal.RecordVoluntaryTurnCycleCompleted(completedCycle);
            host.Journal.RecordTurnModeChanged(
                previousMode,
                Mode,
                TurnContext,
                activeActorId);
            notifications.Add(VoluntaryTurnCycleCompleted, completedCycle);
        }

        private void RecordTurnEnd(
            string endingActorId,
            string nextActorId,
            GameplayNotificationBatch notifications,
            GameplayTurnKind kind = GameplayTurnKind.Normal,
            string interruptedActorId = null)
        {
            var record = new TurnEndRecord(
                LastEndedTurn == null ? 1 : LastEndedTurn.Sequence + 1,
                endingActorId,
                nextActorId,
                kind,
                interruptedActorId);
            LastEndedTurn = record;
            host.Journal.RecordTurnEnded(record);
            notifications.Add(TurnEnded, record);
        }

        private void PublishCommitted(GameplayNotificationBatch notifications)
        {
            host.MarkStateChangedForTurnLifecycle();
            notifications.Publish();
        }

        private void SetActiveActor(
            string actorId,
            GameplayNotificationBatch notifications)
        {
            if (string.Equals(activeActorId, actorId, StringComparison.Ordinal))
                return;

            string previousActorId = activeActorId;
            activeActorId = actorId;
            notifications.Add(
                ActiveActorChanged,
                new GameplayActiveActorChange(
                    previousActorId,
                    activeActorId));
        }

        private void SetMode(
            GameplaySessionMode mode,
            GameplayNotificationBatch notifications)
        {
            if (Mode == mode)
                return;

            GameplaySessionMode previousMode = Mode;
            Mode = mode;
            notifications.Add(
                ModeChanged,
                new GameplayModeChange(previousMode, Mode));
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string value)
        {
            foreach (string candidate in values)
                if (string.Equals(candidate, value,
                    StringComparison.Ordinal)) return true;
            return false;
        }

        private string FindNextCapableActor(int startingAfterIndex)
        {
            for (int offset = 1; offset <= host.InitiativeOrder.Count; offset++)
            {
                int index = (startingAfterIndex + offset)
                    % host.InitiativeOrder.Count;
                string candidateId = host.InitiativeOrder[index];
                if (!host.IsActorIncapacitatedForTurnLifecycle(candidateId))
                    return candidateId;
            }

            return null;
        }
    }
}
