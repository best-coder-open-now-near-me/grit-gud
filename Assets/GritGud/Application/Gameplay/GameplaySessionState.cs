using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayActorState
    {
        private readonly TurnBudget turnBudgetAllowance;
        private readonly TurnActionPointEconomy actionPointEconomy;
        private readonly ScenarioActorDefinition definition;
        private readonly Dictionary<string, int> inventoryQuantities =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, WeaponMagazineSnapshot>
            weaponMagazines =
                new Dictionary<string, WeaponMagazineSnapshot>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, int> ammunitionReserves =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private GameplayActorPose pose;
        private TurnBudget turnBudget;
        private ActorWoundSnapshot wounds;
        private ActorPinState pinState;
        private TurnBudget? suspendedTurnBudget;
        private int attacksCommittedThisTurn;
        private ActorInventorySnapshot cachedInventory;
        private ActorAmmunitionSnapshot cachedAmmunition;
        private GameplayActorSnapshot cachedSnapshot;
        private bool inventorySnapshotDirty = true;
        private bool ammunitionSnapshotDirty = true;
        private bool actorSnapshotDirty = true;

        public GameplayActorState(
            ScenarioActorDefinition definition,
            ScenarioTimingDefinition timing,
            GameplayPartyCharacterSave restoredCharacter = null)
        {
            this.definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            ActorId = definition.Id;
            pose = definition.StartingPose;
            MaximumWounds = definition.Combat.MaximumWounds;
            wounds = new ActorWoundSnapshot(definition.Id, 0, 0f);
            turnBudget = new TurnBudget(
                definition.StartingTurnBudget.ActionPoints,
                definition.StartingTurnBudget.MovementOpportunity);
            EquippedItemId = restoredCharacter != null
                ? restoredCharacter.EquippedItemId
                : definition.InitiallyEquippedItemId;
            EquipmentEffects = definition.GetInventoryItem(
                    EquippedItemId)?.EquippedEffects
                ?? EquipmentEffectSet.None;
            foreach (InventoryItemDefinition item in definition.Inventory)
            {
                if (item.Kind == InventoryItemKind.Consumable)
                    inventoryQuantities.Add(item.Id, item.InitialQuantity);
                if (item.Ammunition != null)
                {
                    weaponMagazines.Add(
                        item.Id,
                        new WeaponMagazineSnapshot(
                            item.Id,
                            item.Ammunition.AmmoTypeId,
                            item.Ammunition.MagazineCapacity,
                            item.Ammunition.InitialLoadedRounds,
                            item.Ammunition.RoundsPerUse));
                }
            }
            foreach (AmmunitionReserveDefinition reserve in
                definition.AmmunitionReserves)
                ammunitionReserves.Add(reserve.AmmoTypeId, reserve.Rounds);
            if (restoredCharacter != null)
            {
                foreach (GameplayPartyWeaponMagazineSave saved in
                    restoredCharacter.WeaponMagazines)
                {
                    WeaponMagazineSnapshot authored =
                        weaponMagazines[saved.WeaponItemId];
                    weaponMagazines[saved.WeaponItemId] =
                        new WeaponMagazineSnapshot(
                            authored.WeaponItemId,
                            authored.AmmoTypeId,
                            authored.Capacity,
                            saved.LoadedRounds,
                            authored.RoundsPerUse);
                }
                foreach (GameplayPartyAmmunitionReserveSave saved in
                    restoredCharacter.AmmunitionReserves)
                    ammunitionReserves[saved.AmmoTypeId] = saved.Rounds;
            }
            turnBudgetAllowance = definition.StartingTurnBudget;
            actionPointEconomy = new TurnActionPointEconomy(
                definition.StartingTurnBudget.ActionPoints,
                timing.ActionPointEconomy.IncomePerPersonalTurn,
                timing.ActionPointEconomy.MaximumHeldActionPoints);
            if (turnBudget.ActionPoints
                > actionPointEconomy.MaximumHeldActionPoints)
                throw new InvalidOperationException(
                    "Restored AP exceeds the scenario held cap.");
        }

        public string ActorId { get; }

        public GameplayActorPose Pose
        {
            get => pose;
            set
            {
                pose = value;
                actorSnapshotDirty = true;
            }
        }

        public TurnBudget TurnBudget
        {
            get => turnBudget;
            set
            {
                turnBudget = value;
                actorSnapshotDirty = true;
            }
        }

        public int EmergencyActionPointAllowance { get; private set; }

        public TurnActionPointEconomy ActionPointEconomy =>
            actionPointEconomy;

        public ActorWoundSnapshot Wounds => wounds;

        public int MaximumWounds { get; }

        public bool IsIncapacitated => Wounds.WoundCount >= MaximumWounds;

        public string EquippedItemId { get; private set; }

        public EquipmentEffectSet EquipmentEffects { get; private set; }

        public ActorPinState PinState
        {
            get => pinState;
            set
            {
                pinState = value;
                actorSnapshotDirty = true;
            }
        }

        public void ApplyEquipment(InventoryItemDefinition item)
        {
            EquippedItemId = item?.Id;
            EquipmentEffects = item?.EquippedEffects
                ?? EquipmentEffectSet.None;
            actorSnapshotDirty = true;
        }

        public int GetInventoryQuantity(string itemId)
        {
            if (inventoryQuantities.TryGetValue(itemId, out int quantity))
                return quantity;

            throw new KeyNotFoundException(
                $"Consumable quantity '{itemId}' is not part of actor '{ActorId}'.");
        }

        public void ApplyInventoryQuantity(
            InventoryQuantityChangeRecord change)
        {
            inventoryQuantities[change.ItemId] = change.ResultingQuantity;
            inventorySnapshotDirty = true;
            actorSnapshotDirty = true;
        }

        public void ApplyAmmunition(WeaponAmmunitionDelta change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));
            if (!string.Equals(change.ActorId, ActorId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Ammunition change actor does not match this actor state.");
            if (!weaponMagazines.TryGetValue(
                    change.WeaponItemId,
                    out WeaponMagazineSnapshot magazine)
                || !string.Equals(
                    magazine.AmmoTypeId,
                    change.AmmoTypeId,
                    StringComparison.Ordinal)
                || magazine.Capacity != change.MagazineCapacity
                || magazine.LoadedRounds != change.PreviousLoadedRounds
                || (change.Kind == WeaponAmmunitionChangeKind.Spend
                    && change.ChangedRounds != magazine.RoundsPerUse)
                || !ammunitionReserves.TryGetValue(
                    change.AmmoTypeId,
                    out int reserve)
                || reserve != change.PreviousReserveRounds)
                throw new InvalidOperationException(
                    "Ammunition change no longer matches canonical actor state.");

            weaponMagazines[change.WeaponItemId] =
                new WeaponMagazineSnapshot(
                    change.WeaponItemId,
                    change.AmmoTypeId,
                    change.MagazineCapacity,
                    change.ResultingLoadedRounds,
                    magazine.RoundsPerUse);
            ammunitionReserves[change.AmmoTypeId] =
                change.ResultingReserveRounds;
            ammunitionSnapshotDirty = true;
            actorSnapshotDirty = true;
        }

        public PersonalTurnStartRecord StartPersonalTurn()
        {
            attacksCommittedThisTurn = 0;
            actorSnapshotDirty = true;
            PersonalTurnActionPointGrant grant =
                PersonalTurnActionPointRules.Grant(
                    TurnBudget.ActionPoints,
                    actionPointEconomy);
            TurnBudget = new TurnBudget(
                grant.ResultingActionPoints,
                WoundedMovementAllowance);
            return new PersonalTurnStartRecord(
                ActorId,
                grant,
                WoundedMovementAllowance);
        }

        public void BeginEmergencyTurn(int actionPoints)
        {
            if (suspendedTurnBudget.HasValue)
                throw new InvalidOperationException(
                    "Actor already has a suspended normal-turn budget.");
            suspendedTurnBudget = TurnBudget;
            EmergencyActionPointAllowance = actionPoints;
            TurnBudget = new TurnBudget(
                actionPoints,
                WoundedMovementAllowance);
        }

        public void EndEmergencyTurn()
        {
            if (!suspendedTurnBudget.HasValue)
                throw new InvalidOperationException(
                    "Actor has no suspended normal-turn budget.");
            TurnBudget = suspendedTurnBudget.Value;
            suspendedTurnBudget = null;
            EmergencyActionPointAllowance = 0;
        }

        public void ApplyAttack(AttackResolutionRecord attack)
        {
            if (!attack.Hit)
                return;

            wounds = attack.TargetWoundsAfter;
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
            if (movementPenalty <= 0f)
                return;
            wounds = region.HasValue
                ? wounds.AddWound(region.Value, movementPenalty)
                : wounds.AddUnlocalizedWound(movementPenalty);
            TurnBudget = new TurnBudget(
                TurnBudget.ActionPoints,
                Math.Min(
                    TurnBudget.MovementOpportunity,
                    WoundedMovementAllowance));
        }

        public void ApplyConcussion(ConcussiveActionPointEffectRecord effect)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            if (!string.Equals(effect.ActorId, ActorId, StringComparison.Ordinal)
                || TurnBudget.ActionPoints != effect.PreviousActionPoints)
                throw new InvalidOperationException(
                    "Concussive AP consequence no longer matches actor state.");
            TurnBudget = new TurnBudget(
                effect.ResultingActionPoints,
                TurnBudget.MovementOpportunity);
        }

        public void CommitWeaponAttack()
        {
            attacksCommittedThisTurn = checked(
                attacksCommittedThisTurn + 1);
            actorSnapshotDirty = true;
        }

        internal void ValidateCanonicalSnapshot(GameplayActorSnapshot snapshot)
        {
            if (!string.Equals(
                    snapshot.ActorId,
                    ActorId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Canonical actor projection has a different identity.",
                    nameof(snapshot));
            if (snapshot.MaximumWounds != MaximumWounds
                || snapshot.ActionPointEconomy.StartingActionPoints
                    != actionPointEconomy.StartingActionPoints
                || snapshot.ActionPointEconomy.IncomePerPersonalTurn
                    != actionPointEconomy.IncomePerPersonalTurn
                || snapshot.ActionPointEconomy.MaximumHeldActionPoints
                    != actionPointEconomy.MaximumHeldActionPoints
                || snapshot.TurnMovementAllowance
                    != turnBudgetAllowance.MovementOpportunity)
                throw new InvalidOperationException(
                    $"Canonical actor '{ActorId}' changed authored allowances.");
            ValidateAmmunitionShape(snapshot.Ammunition);
        }

        internal void InstallCanonicalSnapshot(GameplayActorSnapshot snapshot)
        {
            ValidateCanonicalSnapshot(snapshot);

            pose = snapshot.Pose;
            turnBudget = snapshot.TurnBudget;
            wounds = snapshot.Wounds;
            EquippedItemId = snapshot.EquippedItemId;
            EquipmentEffects = snapshot.EquipmentEffects;
            pinState = snapshot.PinState;
            EmergencyActionPointAllowance =
                snapshot.EmergencyActionPointAllowance;
            suspendedTurnBudget = snapshot.SuspendedTurnBudget;
            attacksCommittedThisTurn = snapshot.AttacksCommittedThisTurn;
            inventoryQuantities.Clear();
            foreach (InventoryQuantitySnapshot quantity in
                snapshot.Inventory.Quantities)
                inventoryQuantities.Add(quantity.ItemId, quantity.Quantity);
            weaponMagazines.Clear();
            foreach (WeaponMagazineSnapshot magazine in
                snapshot.Ammunition.Magazines)
                weaponMagazines.Add(magazine.WeaponItemId, magazine);
            ammunitionReserves.Clear();
            foreach (AmmunitionReserveSnapshot reserve in
                snapshot.Ammunition.Reserves)
                ammunitionReserves.Add(reserve.AmmoTypeId, reserve.Rounds);
            cachedInventory = snapshot.Inventory;
            cachedAmmunition = snapshot.Ammunition;
            cachedSnapshot = snapshot;
            inventorySnapshotDirty = false;
            ammunitionSnapshotDirty = false;
            actorSnapshotDirty = false;
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
            if (!actorSnapshotDirty)
                return cachedSnapshot;

            if (inventorySnapshotDirty)
            {
                var quantities = new List<InventoryQuantitySnapshot>(
                    inventoryQuantities.Count);
                foreach (KeyValuePair<string, int> entry in
                    inventoryQuantities)
                {
                    quantities.Add(new InventoryQuantitySnapshot(
                        entry.Key,
                        entry.Value));
                }
                quantities.Sort((left, right) =>
                    StringComparer.Ordinal.Compare(
                        left.ItemId,
                        right.ItemId));
                cachedInventory = new ActorInventorySnapshot(
                    ActorId,
                    quantities);
                inventorySnapshotDirty = false;
            }

            if (ammunitionSnapshotDirty)
            {
                var magazines = new List<WeaponMagazineSnapshot>(
                    weaponMagazines.Values);
                var reserves = new List<AmmunitionReserveSnapshot>(
                    ammunitionReserves.Count);
                foreach (KeyValuePair<string, int> entry in ammunitionReserves)
                    reserves.Add(new AmmunitionReserveSnapshot(
                        entry.Key,
                        entry.Value));
                cachedAmmunition = new ActorAmmunitionSnapshot(
                    ActorId,
                    magazines,
                    reserves);
                ammunitionSnapshotDirty = false;
            }

            cachedSnapshot = new GameplayActorSnapshot(
                ActorId,
                Pose,
                TurnBudget,
                Wounds,
                EquippedItemId,
                EquipmentEffects,
                MaximumWounds,
                cachedInventory,
                actionPointEconomy,
                turnBudgetAllowance.MovementOpportunity,
                PinState,
                EmergencyActionPointAllowance,
                suspendedTurnBudget,
                attacksCommittedThisTurn,
                cachedAmmunition);
            actorSnapshotDirty = false;
            return cachedSnapshot;
        }

        public GameplayActorStateSnapshot CreateStateSnapshot() =>
            new GameplayActorStateSnapshot(
                ActorId,
                Pose,
                TurnBudget,
                Wounds,
                EquippedItemId,
                EquipmentEffects,
                MaximumWounds,
                actionPointEconomy,
                turnBudgetAllowance.MovementOpportunity,
                PinState,
                attacksCommittedThisTurn);

        private float WoundedMovementAllowance => Math.Max(
            0f,
            turnBudgetAllowance.MovementOpportunity
                - Wounds.MovementPenalty);

        private void ValidateAmmunitionShape(
            ActorAmmunitionSnapshot ammunition)
        {
            if (ammunition == null
                || !string.Equals(
                    ammunition.ActorId,
                    ActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Canonical actor '{ActorId}' has invalid ammunition identity.");

            int expectedMagazineCount = 0;
            foreach (InventoryItemDefinition item in definition.Inventory)
            {
                WeaponAmmunitionDefinition authored = item.Ammunition;
                if (authored == null) continue;
                expectedMagazineCount++;
                if (!ammunition.TryGetMagazine(
                        item.Id,
                        out WeaponMagazineSnapshot magazine)
                    || !string.Equals(
                        magazine.AmmoTypeId,
                        authored.AmmoTypeId,
                        StringComparison.Ordinal)
                    || magazine.Capacity != authored.MagazineCapacity
                    || magazine.RoundsPerUse != authored.RoundsPerUse)
                    throw new InvalidOperationException(
                        $"Canonical actor '{ActorId}' changed authored magazine '{item.Id}'.");
            }
            if (ammunition.Magazines.Count != expectedMagazineCount)
                throw new InvalidOperationException(
                    $"Canonical actor '{ActorId}' changed its authored magazine set.");

            if (ammunition.Reserves.Count
                != definition.AmmunitionReserves.Count)
                throw new InvalidOperationException(
                    $"Canonical actor '{ActorId}' changed its authored reserve set.");
            foreach (AmmunitionReserveDefinition reserve in
                definition.AmmunitionReserves)
                if (!ammunition.TryGetReserve(reserve.AmmoTypeId, out _))
                    throw new InvalidOperationException(
                        $"Canonical actor '{ActorId}' removed reserve '{reserve.AmmoTypeId}'.");
        }

        private static ActorWoundSnapshot RebindWounds(
            ActorWoundSnapshot wounds,
            string actorId) =>
            new ActorWoundSnapshot(
                actorId,
                wounds.HeadWounds,
                wounds.TorsoWounds,
                wounds.LeftArmWounds,
                wounds.RightArmWounds,
                wounds.LeftLegWounds,
                wounds.RightLegWounds,
                wounds.UnlocalizedWounds,
                wounds.MovementPenalty);
    }

    internal sealed class GameplayObjectiveState
    {
        public GameplayObjectiveState(ScenarioObjectiveDefinition definition)
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

        internal void ValidateCanonicalSnapshot(
            GameplayObjectiveSnapshot snapshot)
        {
            if (!string.Equals(
                    snapshot.ObjectiveId,
                    ObjectiveId,
                    StringComparison.Ordinal)
                || snapshot.Position.DistanceTo(Position) > 0f
                || snapshot.InteractionRadius != InteractionRadius
                || !string.Equals(
                    GameplayCanonicalValueDigest.Calculate(
                        snapshot.Interaction),
                    GameplayCanonicalValueDigest.Calculate(Interaction),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Canonical objective projection changed authored identity or geometry.");
        }

        internal void InstallCanonicalSnapshot(
            GameplayObjectiveSnapshot snapshot)
        {
            ValidateCanonicalSnapshot(snapshot);
            IsCompleted = snapshot.IsCompleted;
        }

        public GameplayObjectiveSnapshot CreateSnapshot() =>
            new GameplayObjectiveSnapshot(
                ObjectiveId,
                Position,
                InteractionRadius,
                Interaction,
                IsCompleted);
    }
}
