using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    public interface IGameplayConsumablePowerHandler
    {
        bool IsPending { get; }

        string PendingItemId { get; }

        bool CanHandle(ConsumablePowerDefinition power);

        bool TryToggle(string itemId);

        bool TryConfirm();

        bool Cancel();
    }

    internal sealed class GameplayConsumableController
    {
        private readonly GameplaySession session;
        private readonly IReadOnlyList<IGameplayConsumablePowerHandler> handlers;

        public GameplayConsumableController(
            GameplaySession gameplaySession,
            params IGameplayConsumablePowerHandler[] powerHandlers)
        {
            session = gameplaySession
                ?? throw new ArgumentNullException(nameof(gameplaySession));
            if (powerHandlers == null)
            {
                throw new ArgumentNullException(nameof(powerHandlers));
            }

            var validated = new List<IGameplayConsumablePowerHandler>(
                powerHandlers.Length);
            foreach (IGameplayConsumablePowerHandler handler in powerHandlers)
            {
                validated.Add(handler
                    ?? throw new ArgumentException(
                        "Consumable handlers cannot contain null entries.",
                        nameof(powerHandlers)));
            }

            handlers = validated.AsReadOnly();
        }

        public bool IsPending => FindPendingHandler() != null;

        public string PendingItemId => FindPendingHandler()?.PendingItemId;

        public bool TryToggle(string actorId, string itemId)
        {
            InventoryItemDefinition item = session.GetInventoryItem(
                actorId,
                itemId);
            if (item.ConsumablePower == null)
            {
                return false;
            }

            IGameplayConsumablePowerHandler requested = ResolveHandler(
                item.ConsumablePower);
            IGameplayConsumablePowerHandler pending = FindPendingHandler();
            if (pending != null && !ReferenceEquals(pending, requested))
            {
                pending.Cancel();
            }

            return requested.TryToggle(itemId);
        }

        public bool TryConfirmPending()
        {
            IGameplayConsumablePowerHandler pending = FindPendingHandler();
            return pending != null && pending.TryConfirm();
        }

        public bool CancelPending()
        {
            IGameplayConsumablePowerHandler pending = FindPendingHandler();
            return pending != null && pending.Cancel();
        }

        private IGameplayConsumablePowerHandler ResolveHandler(
            ConsumablePowerDefinition power)
        {
            IGameplayConsumablePowerHandler match = null;
            foreach (IGameplayConsumablePowerHandler handler in handlers)
            {
                if (!handler.CanHandle(power))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        $"Consumable power '{power.PowerTypeId}' has multiple handlers.");
                }

                match = handler;
            }

            return match ?? throw new InvalidOperationException(
                $"Consumable power '{power.PowerTypeId}' has no registered handler.");
        }

        private IGameplayConsumablePowerHandler FindPendingHandler()
        {
            IGameplayConsumablePowerHandler pending = null;
            foreach (IGameplayConsumablePowerHandler handler in handlers)
            {
                if (!handler.IsPending)
                {
                    continue;
                }

                if (pending != null)
                {
                    throw new InvalidOperationException(
                        "Only one consumable power can be pending at a time.");
                }

                pending = handler;
            }

            return pending;
        }
    }
}
