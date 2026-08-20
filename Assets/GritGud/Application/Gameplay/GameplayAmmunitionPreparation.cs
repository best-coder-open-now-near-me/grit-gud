using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public static class GameplayAmmunitionPreparation
    {
        public static bool HasLoadedRounds(
            ScenarioDefinition scenario,
            GameplayActorSnapshot actor)
        {
            InventoryItemDefinition weapon = GetEquippedWeapon(
                scenario,
                actor);
            if (weapon?.Ammunition == null) return true;
            WeaponMagazineSnapshot magazine = actor.Ammunition.GetMagazine(
                weapon.Id);
            return magazine.LoadedRounds >= weapon.Ammunition.RoundsPerUse;
        }

        public static bool TryPrepareSpend(
            ScenarioDefinition scenario,
            GameplayActorSnapshot actor,
            long actionSequence,
            out AmmunitionSpentActionOutcome outcome)
        {
            InventoryItemDefinition weapon = GetEquippedWeapon(
                scenario,
                actor);
            WeaponAmmunitionDefinition definition = weapon?.Ammunition;
            if (definition == null)
            {
                outcome = null;
                return true;
            }

            WeaponMagazineSnapshot magazine = actor.Ammunition.GetMagazine(
                weapon.Id);
            int reserve = actor.Ammunition.GetReserve(definition.AmmoTypeId);
            if (magazine.LoadedRounds < definition.RoundsPerUse)
            {
                outcome = null;
                return false;
            }

            outcome = new AmmunitionSpentActionOutcome(
                new WeaponAmmunitionDelta(
                    actionSequence,
                    actor.ActorId,
                    weapon.Id,
                    definition.AmmoTypeId,
                    WeaponAmmunitionChangeKind.Spend,
                    definition.MagazineCapacity,
                    magazine.LoadedRounds,
                    definition.RoundsPerUse,
                    magazine.LoadedRounds - definition.RoundsPerUse,
                    reserve,
                    reserve));
            return true;
        }

        public static ActorAmmunitionSnapshot Apply(
            ActorAmmunitionSnapshot ammunition,
            WeaponAmmunitionDelta change)
        {
            if (ammunition == null)
                throw new ArgumentNullException(nameof(ammunition));
            if (change == null) throw new ArgumentNullException(nameof(change));
            if (!string.Equals(
                    ammunition.ActorId,
                    change.ActorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Ammunition projection actor does not match its delta.");
            WeaponMagazineSnapshot current = ammunition.GetMagazine(
                change.WeaponItemId);
            int reserve = ammunition.GetReserve(change.AmmoTypeId);
            if (!string.Equals(
                    current.AmmoTypeId,
                    change.AmmoTypeId,
                    StringComparison.Ordinal)
                || current.Capacity != change.MagazineCapacity
                || current.LoadedRounds != change.PreviousLoadedRounds
                || reserve != change.PreviousReserveRounds)
                throw new InvalidOperationException(
                    "Ammunition projection no longer matches its recorded before state.");

            var magazines = new List<WeaponMagazineSnapshot>(
                ammunition.Magazines.Count);
            foreach (WeaponMagazineSnapshot magazine in ammunition.Magazines)
                magazines.Add(string.Equals(
                        magazine.WeaponItemId,
                        change.WeaponItemId,
                        StringComparison.Ordinal)
                    ? new WeaponMagazineSnapshot(
                        change.WeaponItemId,
                        change.AmmoTypeId,
                        change.MagazineCapacity,
                        change.ResultingLoadedRounds,
                        magazine.RoundsPerUse)
                    : magazine);
            var reserves = new List<AmmunitionReserveSnapshot>(
                ammunition.Reserves.Count);
            foreach (AmmunitionReserveSnapshot value in ammunition.Reserves)
                reserves.Add(string.Equals(
                        value.AmmoTypeId,
                        change.AmmoTypeId,
                        StringComparison.Ordinal)
                    ? new AmmunitionReserveSnapshot(
                        change.AmmoTypeId,
                        change.ResultingReserveRounds)
                    : value);
            return new ActorAmmunitionSnapshot(
                ammunition.ActorId,
                magazines,
                reserves);
        }

        public static InventoryItemDefinition GetEquippedWeapon(
            ScenarioDefinition scenario,
            GameplayActorSnapshot actor)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            ScenarioActorDefinition definition = scenario.GetActor(
                actor.ActorId);
            return actor.EquippedItemId == null
                ? null
                : definition.GetInventoryItem(actor.EquippedItemId);
        }
    }

    public static class GameplayWeaponActionOutcomes
    {
        public static GameplayActionOutcome RequirePrimary(
            GameplayActionRecord action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            GameplayActionOutcome primary = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (!(outcome is AttackResolvedActionOutcome)
                    && !(outcome is WeaponDischargedActionOutcome)
                    && !(outcome is ProjectileLaunchedActionOutcome))
                    continue;
                if (primary != null)
                    throw new ArgumentException(
                        "Weapon actions require exactly one primary firing outcome.",
                        nameof(action));
                primary = outcome;
            }
            return primary ?? throw new ArgumentException(
                "Weapon actions require one primary firing outcome.",
                nameof(action));
        }

        public static T RequirePrimary<T>(GameplayActionRecord action)
            where T : GameplayActionOutcome
        {
            GameplayActionOutcome primary = RequirePrimary(action);
            return primary as T ?? throw new ArgumentException(
                $"Weapon action primary outcome is not '{typeof(T).Name}'.",
                nameof(action));
        }

        public static bool TryGetPrimary<T>(
            GameplayActionRecord action,
            out T primary)
            where T : GameplayActionOutcome
        {
            primary = null;
            if (action == null) return false;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (!(outcome is T value)) continue;
                if (primary != null)
                {
                    primary = null;
                    return false;
                }
                primary = value;
            }
            return primary != null;
        }

        public static AmmunitionSpentActionOutcome GetAmmunitionSpend(
            GameplayActionRecord action)
        {
            AmmunitionSpentActionOutcome spend = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (!(outcome is AmmunitionSpentActionOutcome value))
                    continue;
                if (spend != null)
                    throw new ArgumentException(
                        "Weapon actions cannot contain multiple ammunition spends.",
                        nameof(action));
                spend = value;
            }
            return spend;
        }

        public static void ValidateFiringGrammar(
            GameplayActionRecord action,
            GameplayActorSnapshot actor)
        {
            RequirePrimary(action);
            AmmunitionSpentActionOutcome spend = GetAmmunitionSpend(action);
            int expectedCount = spend == null ? 1 : 2;
            if (action.Outcomes.Count != expectedCount)
                throw new ArgumentException(
                    "Weapon actions contain an unsupported additional outcome.",
                    nameof(action));

            bool requiresSpend = actor.EquippedItemId != null
                && actor.Ammunition.TryGetMagazine(
                    actor.EquippedItemId,
                    out _);
            if (requiresSpend != (spend != null))
                throw new InvalidOperationException(
                    requiresSpend
                        ? "The equipped weapon requires one ammunition spend."
                        : "The equipped weapon cannot spend ammunition.");
            if (spend == null) return;
            WeaponAmmunitionDelta change = spend.Change;
            if (change.Kind != WeaponAmmunitionChangeKind.Spend
                || change.ActionSequence != action.Sequence
                || !string.Equals(
                    change.ActorId,
                    actor.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    change.WeaponItemId,
                    actor.EquippedItemId,
                    StringComparison.Ordinal)
                || change.ChangedRounds
                    != actor.Ammunition.GetMagazine(
                        change.WeaponItemId).RoundsPerUse)
                throw new InvalidOperationException(
                    "Weapon ammunition spend does not match its action identity.");
            GameplayAmmunitionPreparation.Apply(
                actor.Ammunition,
                change);
        }
    }
}
