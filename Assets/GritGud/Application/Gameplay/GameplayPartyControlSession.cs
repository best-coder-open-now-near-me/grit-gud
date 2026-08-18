using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum GameplayPartySelectionFailure
    {
        None,
        NotPartyMember,
        ActorIncapacitated,
        TurnBasedControlFollowsInitiative,
        NoAlternateCapableActor,
    }

    public readonly struct GameplayPartyControlSnapshot
    {
        public GameplayPartyControlSnapshot(
            string selectedActorId,
            string commandActorId)
        {
            SelectedActorId = selectedActorId;
            CommandActorId = commandActorId;
        }

        public string SelectedActorId { get; }

        public string CommandActorId { get; }
    }

    public sealed class GameplayPartyControlSession : IDisposable
    {
        private readonly GameplaySession gameplay;
        private readonly PlayerPartyDefinition party;
        private string selectedActorId;
        private string commandActorId;
        private bool disposed;

        public GameplayPartyControlSession(GameplaySession gameplaySession)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            party = gameplay.Scenario.PlayerParty
                ?? throw new ArgumentException(
                    "Gameplay party control requires an authored player party.",
                    nameof(gameplaySession));
            foreach (string actorId in party.ActorIds)
                gameplay.GetActor(actorId);

            selectedActorId = party.InitiallySelectedActorId;
            gameplay.ActiveActorChanged += HandleActiveActorChanged;
            gameplay.ModeChanged += HandleModeChanged;
            gameplay.ActorCapabilityChanged += HandleActorCapabilityChanged;
            Synchronize(notify: false);
        }

        public IReadOnlyList<string> ActorIds => party.ActorIds;

        public string SelectedActorId => selectedActorId;

        public string CommandActorId => commandActorId;

        public GameplayPartyControlSnapshot Snapshot =>
            new GameplayPartyControlSnapshot(
                selectedActorId,
                commandActorId);

        public bool IsPartyDefeated => FindFirstCapableActor() == null;

        public event Action<GameplayPartyControlSnapshot> ControlChanged;

        public bool IsPartyMember(string actorId) => party.Contains(actorId);

        public bool TrySelectActor(
            string actorId,
            out GameplayPartySelectionFailure failure)
        {
            ThrowIfDisposed();
            if (!party.Contains(actorId))
                return Fail(
                    GameplayPartySelectionFailure.NotPartyMember,
                    out failure);
            if (gameplay.Mode == GameplaySessionMode.TurnBased)
                return Fail(
                    GameplayPartySelectionFailure
                        .TurnBasedControlFollowsInitiative,
                    out failure);
            if (gameplay.IsActorIncapacitated(actorId))
                return Fail(
                    GameplayPartySelectionFailure.ActorIncapacitated,
                    out failure);

            SetControl(actorId, actorId);
            failure = GameplayPartySelectionFailure.None;
            return true;
        }

        public bool TrySelectNextActor(
            out GameplayPartySelectionFailure failure)
        {
            ThrowIfDisposed();
            if (gameplay.Mode == GameplaySessionMode.TurnBased)
            {
                return Fail(
                    GameplayPartySelectionFailure
                        .TurnBasedControlFollowsInitiative,
                    out failure);
            }

            int selectedIndex = -1;
            for (int index = 0; index < party.ActorIds.Count; index++)
            {
                if (string.Equals(
                        party.ActorIds[index],
                        selectedActorId,
                        StringComparison.Ordinal))
                {
                    selectedIndex = index;
                    break;
                }
            }

            for (int offset = 1; offset <= party.ActorIds.Count; offset++)
            {
                int index = (selectedIndex + offset) % party.ActorIds.Count;
                string candidateId = party.ActorIds[index];
                if (string.Equals(
                        candidateId,
                        selectedActorId,
                        StringComparison.Ordinal)
                    || gameplay.IsActorIncapacitated(candidateId))
                {
                    continue;
                }

                SetControl(candidateId, candidateId);
                failure = GameplayPartySelectionFailure.None;
                return true;
            }

            return Fail(
                GameplayPartySelectionFailure.NoAlternateCapableActor,
                out failure);
        }

        public bool HasCapableHostileActor()
        {
            ThrowIfDisposed();
            foreach (string partyActorId in party.ActorIds)
            {
                if (gameplay.IsActorIncapacitated(partyActorId))
                    continue;
                foreach (string candidateId in gameplay.InitiativeOrder)
                {
                    if (party.Contains(candidateId)
                        || gameplay.IsActorIncapacitated(candidateId))
                        continue;
                    if (gameplay.IsHostile(partyActorId, candidateId)
                        || gameplay.IsHostile(candidateId, partyActorId))
                        return true;
                }
            }

            return false;
        }

        public void Synchronize()
        {
            ThrowIfDisposed();
            Synchronize(notify: true);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            gameplay.ActiveActorChanged -= HandleActiveActorChanged;
            gameplay.ModeChanged -= HandleModeChanged;
            gameplay.ActorCapabilityChanged -= HandleActorCapabilityChanged;
            ControlChanged = null;
            disposed = true;
        }

        private void HandleActiveActorChanged(GameplayActiveActorChange _) =>
            Synchronize(notify: true);

        private void HandleModeChanged(GameplayModeChange _) =>
            Synchronize(notify: true);

        private void HandleActorCapabilityChanged(string _) =>
            Synchronize(notify: true);

        private void Synchronize(bool notify)
        {
            string nextSelectedActorId = selectedActorId;
            if (nextSelectedActorId == null
                || gameplay.IsActorIncapacitated(nextSelectedActorId))
            {
                nextSelectedActorId = FindFirstCapableActor();
            }

            string nextCommandActorId;
            if (gameplay.Mode == GameplaySessionMode.Exploration)
            {
                nextCommandActorId = nextSelectedActorId;
            }
            else
            {
                string activeActorId = gameplay.ActiveActorId;
                bool partyCanCommandActiveActor = party.Contains(activeActorId)
                    && !gameplay.IsActorIncapacitated(activeActorId);
                nextCommandActorId = partyCanCommandActiveActor
                    ? activeActorId
                    : null;
                if (partyCanCommandActiveActor)
                    nextSelectedActorId = activeActorId;
            }

            SetControl(
                nextSelectedActorId,
                nextCommandActorId,
                notify);
        }

        private void SetControl(
            string nextSelectedActorId,
            string nextCommandActorId,
            bool notify = true)
        {
            bool changed = !string.Equals(
                    selectedActorId,
                    nextSelectedActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    commandActorId,
                    nextCommandActorId,
                    StringComparison.Ordinal);
            selectedActorId = nextSelectedActorId;
            commandActorId = nextCommandActorId;
            if (changed && notify)
            {
                var notifications = new GameplayNotificationBatch();
                notifications.Add(ControlChanged, Snapshot);
                notifications.Publish();
            }
        }

        private string FindFirstCapableActor()
        {
            foreach (string actorId in party.ActorIds)
                if (!gameplay.IsActorIncapacitated(actorId))
                    return actorId;
            return null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(GameplayPartyControlSession));
        }

        private static bool Fail(
            GameplayPartySelectionFailure value,
            out GameplayPartySelectionFailure failure)
        {
            failure = value;
            return false;
        }
    }
}
