using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Owns the initiative and budget lifecycle shared by every emergency cycle.
    /// Trigger-specific adapters decide how the emergency is advanced and resolved.
    /// </summary>
    public sealed class GameplayEmergencyCycleSession
    {
        private readonly GameplaySession gameplay;
        private EmergencyReactionWindowRecord window;
        private IEmergencyCycleResolution resolution;

        public GameplayEmergencyCycleSession(GameplaySession gameplaySession)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(nameof(gameplaySession));
        }

        public EmergencyReactionWindowRecord CurrentWindow => window;
        public bool HasPendingOrActiveWindow => window != null
            && window.Status != EmergencyReactionWindowStatus.Completed;
        public event Action<EmergencyReactionWindowRecord> WindowChanged;

        public bool TryOpen(
            string triggerType,
            string triggerId,
            string initiatorActorId,
            int actionPointAllowance,
            IEmergencyCycleResolution cycleResolution)
        {
            if (HasPendingOrActiveWindow) return false;
            if (cycleResolution == null) throw new ArgumentNullException(nameof(cycleResolution));

            IReadOnlyList<string> responders = BuildResponderOrder(initiatorActorId);
            if (responders.Count == 0) return false;
            window = new EmergencyReactionWindowRecord(
                window == null ? 1L : window.Sequence + 1L,
                triggerType,
                triggerId,
                initiatorActorId,
                responders,
                actionPointAllowance,
                EmergencyReactionWindowStatus.Pending);
            resolution = cycleResolution;
            Publish();
            return true;
        }

        public bool TryEndTurn(string actorId, out TurnEndFailure failure)
        {
            if (!HasPendingOrActiveWindow) return gameplay.TryEndTurn(actorId, out failure);
            if (window.Status == EmergencyReactionWindowStatus.Pending)
            {
                if (!gameplay.TryEndTurn(actorId, out failure)) return false;
                if (!string.Equals(actorId, window.InitiatorActorId, StringComparison.Ordinal)) return true;
                gameplay.BeginEmergencyReaction(
                    window.InitiatorActorId,
                    window.ResponderIds,
                    window.ActionPointAllowance);
                SetStatus(EmergencyReactionWindowStatus.Active);
                return true;
            }

            bool completesResponsePass =
                gameplay.TurnPhase == GameplayTurnPhase.EmergencyReaction
                && gameplay.Operation == GameplaySessionOperation.None
                && string.Equals(
                    gameplay.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal)
                && gameplay.EmergencyResponderIndex
                    == gameplay.EmergencyResponders.Count - 1;
            if (completesResponsePass && !resolution.IsResolved)
                resolution.ResolveAfterResponsePass();
            if (!gameplay.TryEndEmergencyTurn(
                    actorId,
                    out bool passCompleted,
                    out failure))
                return false;
            if (passCompleted)
            {
                if (!completesResponsePass)
                    throw new InvalidOperationException(
                        "Emergency response completion was not predicted "
                        + "from canonical turn state.");
                gameplay.CompleteEmergencyReaction(window.InitiatorActorId);
                SetStatus(EmergencyReactionWindowStatus.Completed);
                resolution = null;
            }
            return true;
        }

        private void SetStatus(EmergencyReactionWindowStatus status)
        {
            window = window.WithStatus(status);
            Publish();
        }

        private void Publish()
        {
            gameplay.Journal.RecordEmergencyReactionChanged(window);
            WindowChanged?.Invoke(window);
        }

        private IReadOnlyList<string> BuildResponderOrder(string initiatorActorId)
        {
            var responders = new List<string>();
            int initiatorIndex = -1;
            for (int i = 0; i < gameplay.InitiativeOrder.Count; i++)
                if (string.Equals(gameplay.InitiativeOrder[i], initiatorActorId, StringComparison.Ordinal))
                    initiatorIndex = i;
            if (initiatorIndex < 0)
                throw new InvalidOperationException("The emergency initiator is missing from initiative.");
            for (int offset = 1; offset < gameplay.InitiativeOrder.Count; offset++)
            {
                string actorId = gameplay.InitiativeOrder[
                    (initiatorIndex + offset)
                    % gameplay.InitiativeOrder.Count];
                if (!gameplay.IsActorIncapacitated(actorId))
                    responders.Add(actorId);
            }
            return responders.AsReadOnly();
        }
    }

    public interface IEmergencyCycleResolution
    {
        bool IsResolved { get; }
        void ResolveAfterResponsePass();
    }
}
