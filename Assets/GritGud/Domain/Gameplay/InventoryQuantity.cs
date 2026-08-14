using System;
using System.Collections.Generic;

namespace GritGud.Domain.Gameplay
{
    public readonly struct InventoryQuantitySnapshot
    {
        public InventoryQuantitySnapshot(string itemId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException(
                    "Inventory quantities require an item identifier.",
                    nameof(itemId));
            }
            if (quantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            ItemId = itemId;
            Quantity = quantity;
        }

        public string ItemId { get; }

        public int Quantity { get; }
    }

    public sealed class ActorInventorySnapshot
    {
        private readonly IReadOnlyList<InventoryQuantitySnapshot> quantities;

        public ActorInventorySnapshot(
            string actorId,
            IEnumerable<InventoryQuantitySnapshot> itemQuantities)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Inventory snapshots require an actor identifier.",
                    nameof(actorId));
            }
            if (itemQuantities == null)
            {
                throw new ArgumentNullException(nameof(itemQuantities));
            }

            var copy = new List<InventoryQuantitySnapshot>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (InventoryQuantitySnapshot quantity in itemQuantities)
            {
                if (!ids.Add(quantity.ItemId))
                {
                    throw new ArgumentException(
                        $"Inventory quantity '{quantity.ItemId}' is duplicated.",
                        nameof(itemQuantities));
                }
                copy.Add(quantity);
            }

            ActorId = actorId;
            quantities = copy.AsReadOnly();
        }

        public string ActorId { get; }

        public IReadOnlyList<InventoryQuantitySnapshot> Quantities => quantities;

        public bool TryGetQuantity(string itemId, out int quantity)
        {
            foreach (InventoryQuantitySnapshot item in quantities)
            {
                if (string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    quantity = item.Quantity;
                    return true;
                }
            }

            quantity = 0;
            return false;
        }

        public int GetQuantity(string itemId)
        {
            if (TryGetQuantity(itemId, out int quantity))
            {
                return quantity;
            }

            throw new KeyNotFoundException(
                $"Consumable quantity '{itemId}' is not part of actor '{ActorId}'.");
        }
    }

    public sealed class InventoryQuantityChangeRecord
    {
        public InventoryQuantityChangeRecord(
            string actorId,
            string itemId,
            int previousQuantity,
            int consumedQuantity,
            int resultingQuantity)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Inventory changes require an actor identifier.",
                    nameof(actorId));
            }
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException(
                    "Inventory changes require an item identifier.",
                    nameof(itemId));
            }
            if (previousQuantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(previousQuantity));
            }
            if (consumedQuantity <= 0
                || resultingQuantity < 0
                || previousQuantity - consumedQuantity != resultingQuantity)
            {
                throw new ArgumentException(
                    "Consumed inventory quantities must exactly match the recorded before and after state.",
                    nameof(resultingQuantity));
            }

            ActorId = actorId;
            ItemId = itemId;
            PreviousQuantity = previousQuantity;
            ConsumedQuantity = consumedQuantity;
            ResultingQuantity = resultingQuantity;
        }

        public string ActorId { get; }

        public string ItemId { get; }

        public int PreviousQuantity { get; }

        public int ConsumedQuantity { get; }

        public int ResultingQuantity { get; }
    }

    public sealed class InventoryQuantityChangedActionOutcome :
        GameplayActionOutcome
    {
        public InventoryQuantityChangedActionOutcome(
            InventoryQuantityChangeRecord change)
            : base((change ?? throw new ArgumentNullException(nameof(change)))
                .ItemId)
        {
            Change = change;
        }

        public InventoryQuantityChangeRecord Change { get; }
    }
}
