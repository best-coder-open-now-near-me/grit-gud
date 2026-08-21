using System;
using System.Collections.Generic;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    public sealed class WeaponAmmunitionDefinition
    {
        public WeaponAmmunitionDefinition(
            string ammoTypeId,
            int magazineCapacity,
            int initialLoadedRounds,
            int roundsPerUse,
            ActionCost reloadTurnCost,
            bool consumesRemainingMovement,
            int reloadPolicyVersion)
        {
            if (string.IsNullOrWhiteSpace(ammoTypeId))
                throw new ArgumentException(
                    "Weapon ammunition requires an ammunition type.",
                    nameof(ammoTypeId));
            if (magazineCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(magazineCapacity));
            if (initialLoadedRounds < 0
                || initialLoadedRounds > magazineCapacity)
                throw new ArgumentOutOfRangeException(
                    nameof(initialLoadedRounds));
            if (roundsPerUse <= 0 || roundsPerUse > magazineCapacity)
                throw new ArgumentOutOfRangeException(nameof(roundsPerUse));
            if (reloadPolicyVersion <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(reloadPolicyVersion));

            AmmoTypeId = ammoTypeId;
            MagazineCapacity = magazineCapacity;
            InitialLoadedRounds = initialLoadedRounds;
            RoundsPerUse = roundsPerUse;
            ReloadTurnCost = reloadTurnCost;
            ConsumesRemainingMovement = consumesRemainingMovement;
            ReloadPolicyVersion = reloadPolicyVersion;
        }

        public string AmmoTypeId { get; }
        public int MagazineCapacity { get; }
        public int InitialLoadedRounds { get; }
        public int RoundsPerUse { get; }
        public ActionCost ReloadTurnCost { get; }
        public bool ConsumesRemainingMovement { get; }
        public int ReloadPolicyVersion { get; }
    }

    public readonly struct AmmunitionReserveDefinition
    {
        public AmmunitionReserveDefinition(string ammoTypeId, int rounds)
        {
            if (string.IsNullOrWhiteSpace(ammoTypeId))
                throw new ArgumentException(
                    "Ammunition reserves require an ammunition type.",
                    nameof(ammoTypeId));
            if (rounds < 0)
                throw new ArgumentOutOfRangeException(nameof(rounds));

            AmmoTypeId = ammoTypeId;
            Rounds = rounds;
        }

        public string AmmoTypeId { get; }
        public int Rounds { get; }
    }

    public readonly struct WeaponMagazineSnapshot
    {
        public WeaponMagazineSnapshot(
            string weaponItemId,
            string ammoTypeId,
            int capacity,
            int loadedRounds,
            int roundsPerUse = 1)
        {
            if (string.IsNullOrWhiteSpace(weaponItemId))
                throw new ArgumentException(
                    "Weapon magazines require a weapon item identifier.",
                    nameof(weaponItemId));
            if (string.IsNullOrWhiteSpace(ammoTypeId))
                throw new ArgumentException(
                    "Weapon magazines require an ammunition type.",
                    nameof(ammoTypeId));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (loadedRounds < 0 || loadedRounds > capacity)
                throw new ArgumentOutOfRangeException(nameof(loadedRounds));
            if (roundsPerUse <= 0 || roundsPerUse > capacity)
                throw new ArgumentOutOfRangeException(nameof(roundsPerUse));

            WeaponItemId = weaponItemId;
            AmmoTypeId = ammoTypeId;
            Capacity = capacity;
            LoadedRounds = loadedRounds;
            RoundsPerUse = roundsPerUse;
        }

        public string WeaponItemId { get; }
        public string AmmoTypeId { get; }
        public int Capacity { get; }
        public int LoadedRounds { get; }
        public int RoundsPerUse { get; }
    }

    public readonly struct AmmunitionReserveSnapshot
    {
        public AmmunitionReserveSnapshot(string ammoTypeId, int rounds)
        {
            if (string.IsNullOrWhiteSpace(ammoTypeId))
                throw new ArgumentException(
                    "Ammunition reserves require an ammunition type.",
                    nameof(ammoTypeId));
            if (rounds < 0)
                throw new ArgumentOutOfRangeException(nameof(rounds));

            AmmoTypeId = ammoTypeId;
            Rounds = rounds;
        }

        public string AmmoTypeId { get; }
        public int Rounds { get; }
    }

    public sealed class ActorAmmunitionSnapshot
    {
        private readonly IReadOnlyList<WeaponMagazineSnapshot> magazines;
        private readonly IReadOnlyList<AmmunitionReserveSnapshot> reserves;

        public ActorAmmunitionSnapshot(
            string actorId,
            IEnumerable<WeaponMagazineSnapshot> weaponMagazines,
            IEnumerable<AmmunitionReserveSnapshot> ammunitionReserves)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Actor ammunition requires an actor identifier.",
                    nameof(actorId));
            if (weaponMagazines == null)
                throw new ArgumentNullException(nameof(weaponMagazines));
            if (ammunitionReserves == null)
                throw new ArgumentNullException(nameof(ammunitionReserves));

            var magazineCopy = new List<WeaponMagazineSnapshot>();
            var weaponIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WeaponMagazineSnapshot magazine in weaponMagazines)
            {
                if (!weaponIds.Add(magazine.WeaponItemId))
                    throw new ArgumentException(
                        $"Weapon magazine '{magazine.WeaponItemId}' is duplicated.",
                        nameof(weaponMagazines));
                magazineCopy.Add(magazine);
            }
            magazineCopy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.WeaponItemId,
                right.WeaponItemId));

            var reserveCopy = new List<AmmunitionReserveSnapshot>();
            var ammoTypeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AmmunitionReserveSnapshot reserve in ammunitionReserves)
            {
                if (!ammoTypeIds.Add(reserve.AmmoTypeId))
                    throw new ArgumentException(
                        $"Ammunition reserve '{reserve.AmmoTypeId}' is duplicated.",
                        nameof(ammunitionReserves));
                reserveCopy.Add(reserve);
            }
            reserveCopy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.AmmoTypeId,
                right.AmmoTypeId));

            ActorId = actorId;
            magazines = magazineCopy.AsReadOnly();
            reserves = reserveCopy.AsReadOnly();
        }

        public string ActorId { get; }
        public IReadOnlyList<WeaponMagazineSnapshot> Magazines => magazines;
        public IReadOnlyList<AmmunitionReserveSnapshot> Reserves => reserves;

        public bool TryGetMagazine(
            string weaponItemId,
            out WeaponMagazineSnapshot magazine)
        {
            foreach (WeaponMagazineSnapshot value in magazines)
            {
                if (string.Equals(
                        value.WeaponItemId,
                        weaponItemId,
                        StringComparison.Ordinal))
                {
                    magazine = value;
                    return true;
                }
            }

            magazine = default;
            return false;
        }

        public WeaponMagazineSnapshot GetMagazine(string weaponItemId)
        {
            if (TryGetMagazine(weaponItemId, out WeaponMagazineSnapshot value))
                return value;
            throw new KeyNotFoundException(
                $"Weapon magazine '{weaponItemId}' is not part of actor '{ActorId}'.");
        }

        public bool TryGetReserve(string ammoTypeId, out int rounds)
        {
            foreach (AmmunitionReserveSnapshot reserve in reserves)
            {
                if (string.Equals(
                        reserve.AmmoTypeId,
                        ammoTypeId,
                        StringComparison.Ordinal))
                {
                    rounds = reserve.Rounds;
                    return true;
                }
            }

            rounds = 0;
            return false;
        }

        public int GetReserve(string ammoTypeId)
        {
            if (TryGetReserve(ammoTypeId, out int rounds))
                return rounds;
            throw new KeyNotFoundException(
                $"Ammunition reserve '{ammoTypeId}' is not part of actor '{ActorId}'.");
        }
    }

    public enum WeaponAmmunitionChangeKind
    {
        Spend,
        Reload,
    }

    public sealed class WeaponAmmunitionDelta
    {
        public WeaponAmmunitionDelta(
            long actionSequence,
            string actorId,
            string weaponItemId,
            string ammoTypeId,
            WeaponAmmunitionChangeKind kind,
            int magazineCapacity,
            int previousLoadedRounds,
            int changedRounds,
            int resultingLoadedRounds,
            int previousReserveRounds,
            int resultingReserveRounds)
        {
            if (actionSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(actionSequence));
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException(
                    "Ammunition changes require an actor identifier.",
                    nameof(actorId));
            if (string.IsNullOrWhiteSpace(weaponItemId))
                throw new ArgumentException(
                    "Ammunition changes require a weapon item identifier.",
                    nameof(weaponItemId));
            if (string.IsNullOrWhiteSpace(ammoTypeId))
                throw new ArgumentException(
                    "Ammunition changes require an ammunition type.",
                    nameof(ammoTypeId));
            if (!Enum.IsDefined(typeof(WeaponAmmunitionChangeKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (magazineCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(magazineCapacity));
            if (previousLoadedRounds < 0
                || previousLoadedRounds > magazineCapacity)
                throw new ArgumentOutOfRangeException(
                    nameof(previousLoadedRounds));
            if (resultingLoadedRounds < 0
                || resultingLoadedRounds > magazineCapacity)
                throw new ArgumentOutOfRangeException(
                    nameof(resultingLoadedRounds));
            if (changedRounds <= 0)
                throw new ArgumentOutOfRangeException(nameof(changedRounds));
            if (previousReserveRounds < 0 || resultingReserveRounds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(previousReserveRounds));

            bool valid = kind == WeaponAmmunitionChangeKind.Spend
                ? previousLoadedRounds - changedRounds
                        == resultingLoadedRounds
                    && previousReserveRounds == resultingReserveRounds
                : previousLoadedRounds + changedRounds
                        == resultingLoadedRounds
                    && previousReserveRounds - changedRounds
                        == resultingReserveRounds;
            if (!valid)
                throw new ArgumentException(
                    "The ammunition change does not conserve its recorded magazine and reserve state.",
                    nameof(changedRounds));

            ActionSequence = actionSequence;
            ActorId = actorId;
            WeaponItemId = weaponItemId;
            AmmoTypeId = ammoTypeId;
            Kind = kind;
            MagazineCapacity = magazineCapacity;
            PreviousLoadedRounds = previousLoadedRounds;
            ChangedRounds = changedRounds;
            ResultingLoadedRounds = resultingLoadedRounds;
            PreviousReserveRounds = previousReserveRounds;
            ResultingReserveRounds = resultingReserveRounds;
        }

        public long ActionSequence { get; }
        public string ActorId { get; }
        public string WeaponItemId { get; }
        public string AmmoTypeId { get; }
        public WeaponAmmunitionChangeKind Kind { get; }
        public int MagazineCapacity { get; }
        public int PreviousLoadedRounds { get; }
        public int ChangedRounds { get; }
        public int ResultingLoadedRounds { get; }
        public int PreviousReserveRounds { get; }
        public int ResultingReserveRounds { get; }
    }

    public sealed class AmmunitionSpentActionOutcome : GameplayActionOutcome
    {
        public AmmunitionSpentActionOutcome(WeaponAmmunitionDelta change)
            : base((change ?? throw new ArgumentNullException(nameof(change)))
                .WeaponItemId)
        {
            if (change.Kind != WeaponAmmunitionChangeKind.Spend)
                throw new ArgumentException(
                    "Spent-ammunition outcomes require a spend delta.",
                    nameof(change));
            Change = change;
        }

        public WeaponAmmunitionDelta Change { get; }
    }

    public sealed class WeaponReloadedActionOutcome : GameplayActionOutcome
    {
        public WeaponReloadedActionOutcome(WeaponAmmunitionDelta change)
            : base((change ?? throw new ArgumentNullException(nameof(change)))
                .WeaponItemId)
        {
            if (change.Kind != WeaponAmmunitionChangeKind.Reload)
                throw new ArgumentException(
                    "Reload outcomes require a reload delta.",
                    nameof(change));
            Change = change;
        }

        public WeaponAmmunitionDelta Change { get; }
    }

    public static class AmmunitionActionIds
    {
        public const string Reload = "ammunition.reload";
    }
}
