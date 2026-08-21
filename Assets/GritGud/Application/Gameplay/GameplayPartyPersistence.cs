using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public interface IGameplayPartySaveStore
    {
        bool TryLoad(out GameplayPartySave save);

        void Save(GameplayPartySave save);

        void Delete();
    }

    public readonly struct GameplayPartyWeaponMagazineSave
    {
        public GameplayPartyWeaponMagazineSave(
            string weaponItemId,
            int loadedRounds)
        {
            if (string.IsNullOrWhiteSpace(weaponItemId))
                throw new ArgumentException(
                    "Saved magazines require a weapon item identifier.",
                    nameof(weaponItemId));
            if (loadedRounds < 0)
                throw new ArgumentOutOfRangeException(nameof(loadedRounds));
            WeaponItemId = weaponItemId;
            LoadedRounds = loadedRounds;
        }

        public string WeaponItemId { get; }
        public int LoadedRounds { get; }
    }

    public readonly struct GameplayPartyAmmunitionReserveSave
    {
        public GameplayPartyAmmunitionReserveSave(
            string ammoTypeId,
            int rounds)
        {
            if (string.IsNullOrWhiteSpace(ammoTypeId))
                throw new ArgumentException(
                    "Saved ammunition reserves require an ammunition type.",
                    nameof(ammoTypeId));
            if (rounds < 0)
                throw new ArgumentOutOfRangeException(nameof(rounds));
            AmmoTypeId = ammoTypeId;
            Rounds = rounds;
        }

        public string AmmoTypeId { get; }
        public int Rounds { get; }
    }

    public sealed class GameplayPartyCharacterSave
    {
        private readonly Dictionary<string, GameplayPartyWeaponMagazineSave>
            magazinesByWeapon;
        private readonly Dictionary<string, GameplayPartyAmmunitionReserveSave>
            reservesByType;

        public GameplayPartyCharacterSave(
            string identityId,
            string equippedItemId,
            IEnumerable<GameplayPartyWeaponMagazineSave> weaponMagazines = null,
            IEnumerable<GameplayPartyAmmunitionReserveSave>
                ammunitionReserves = null)
        {
            if (string.IsNullOrWhiteSpace(identityId))
            {
                throw new ArgumentException(
                    "Persistent party equipment requires a character identity.",
                    nameof(identityId));
            }

            IdentityId = identityId;
            EquippedItemId = equippedItemId;
            var magazines = new List<GameplayPartyWeaponMagazineSave>(
                weaponMagazines
                    ?? Array.Empty<GameplayPartyWeaponMagazineSave>());
            magazines.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.WeaponItemId,
                right.WeaponItemId));
            magazinesByWeapon = new Dictionary<
                string,
                GameplayPartyWeaponMagazineSave>(StringComparer.Ordinal);
            foreach (GameplayPartyWeaponMagazineSave magazine in magazines)
                if (!magazinesByWeapon.TryAdd(
                    magazine.WeaponItemId,
                    magazine))
                    throw new ArgumentException(
                        $"Saved magazine '{magazine.WeaponItemId}' is duplicated.",
                        nameof(weaponMagazines));
            WeaponMagazines = magazines.AsReadOnly();

            var reserves = new List<GameplayPartyAmmunitionReserveSave>(
                ammunitionReserves
                    ?? Array.Empty<GameplayPartyAmmunitionReserveSave>());
            reserves.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.AmmoTypeId,
                right.AmmoTypeId));
            reservesByType = new Dictionary<
                string,
                GameplayPartyAmmunitionReserveSave>(StringComparer.Ordinal);
            foreach (GameplayPartyAmmunitionReserveSave reserve in reserves)
                if (!reservesByType.TryAdd(reserve.AmmoTypeId, reserve))
                    throw new ArgumentException(
                        $"Saved reserve '{reserve.AmmoTypeId}' is duplicated.",
                        nameof(ammunitionReserves));
            AmmunitionReserves = reserves.AsReadOnly();
        }

        public string IdentityId { get; }

        public string EquippedItemId { get; }

        public IReadOnlyList<GameplayPartyWeaponMagazineSave> WeaponMagazines
        {
            get;
        }

        public IReadOnlyList<GameplayPartyAmmunitionReserveSave>
            AmmunitionReserves { get; }

        public bool TryGetMagazine(
            string weaponItemId,
            out GameplayPartyWeaponMagazineSave magazine) =>
            magazinesByWeapon.TryGetValue(
                weaponItemId ?? string.Empty,
                out magazine);

        public bool TryGetReserve(
            string ammoTypeId,
            out GameplayPartyAmmunitionReserveSave reserve) =>
            reservesByType.TryGetValue(
                ammoTypeId ?? string.Empty,
                out reserve);
    }

    public sealed class GameplayPartySave
    {
        public const int CurrentSchemaVersion = 4;

        private readonly Dictionary<string, GameplayPartyCharacterSave>
            charactersByIdentity;

        public GameplayPartySave(
            int schemaVersion,
            IEnumerable<GameplayPartyCharacterSave> characters)
        {
            if (schemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (characters == null)
                throw new ArgumentNullException(nameof(characters));

            SchemaVersion = schemaVersion;
            var copy = new List<GameplayPartyCharacterSave>();
            charactersByIdentity =
                new Dictionary<string, GameplayPartyCharacterSave>(
                    StringComparer.Ordinal);
            foreach (GameplayPartyCharacterSave character in characters)
            {
                if (character == null)
                {
                    throw new ArgumentException(
                        "Party saves cannot contain null characters.",
                        nameof(characters));
                }
                if (!charactersByIdentity.TryAdd(
                        character.IdentityId,
                        character))
                {
                    throw new ArgumentException(
                        $"Party save identity '{character.IdentityId}' is duplicated.",
                        nameof(characters));
                }
                copy.Add(character);
            }
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "Party saves require at least one character.",
                    nameof(characters));
            }

            Characters = copy.AsReadOnly();
        }

        public int SchemaVersion { get; }

        public IReadOnlyList<GameplayPartyCharacterSave> Characters { get; }

        public bool TryGetCharacter(
            string identityId,
            out GameplayPartyCharacterSave character) =>
            charactersByIdentity.TryGetValue(
                identityId ?? string.Empty,
                out character);

        public static GameplayPartySave Capture(GameplaySession gameplay)
        {
            if (gameplay == null)
                throw new ArgumentNullException(nameof(gameplay));
            PlayerPartyDefinition party = gameplay.Scenario.PlayerParty
                ?? throw new InvalidOperationException(
                    "Party persistence requires an authored player party.");
            var characters = new List<GameplayPartyCharacterSave>(
                party.ActorIds.Count);
            foreach (string actorId in party.ActorIds)
            {
                ScenarioActorDefinition definition =
                    gameplay.Scenario.GetActor(actorId);
                CharacterProfileDefinition profile = definition.CharacterProfile
                    ?? throw new InvalidOperationException(
                        $"Party actor '{actorId}' has no character identity.");
                GameplayActorSnapshot actor = gameplay.GetActor(actorId);
                var magazines = new List<GameplayPartyWeaponMagazineSave>(
                    actor.Ammunition.Magazines.Count);
                foreach (WeaponMagazineSnapshot magazine in
                    actor.Ammunition.Magazines)
                    magazines.Add(new GameplayPartyWeaponMagazineSave(
                        magazine.WeaponItemId,
                        magazine.LoadedRounds));
                var reserves = new List<GameplayPartyAmmunitionReserveSave>(
                    actor.Ammunition.Reserves.Count);
                foreach (AmmunitionReserveSnapshot reserve in
                    actor.Ammunition.Reserves)
                    reserves.Add(new GameplayPartyAmmunitionReserveSave(
                        reserve.AmmoTypeId,
                        reserve.Rounds));
                characters.Add(new GameplayPartyCharacterSave(
                    profile.IdentityId,
                    actor.EquippedItemId,
                    magazines,
                    reserves));
            }
            return new GameplayPartySave(
                CurrentSchemaVersion,
                characters);
        }
    }

    public static class GameplayPartySaveMigrator
    {
        public static GameplayPartySave Migrate(
            GameplayPartySave save,
            ScenarioDefinition scenario)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (save.SchemaVersion == GameplayPartySave.CurrentSchemaVersion)
                return save;
            if (save.SchemaVersion < 1
                || save.SchemaVersion > GameplayPartySave.CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Party save schema {save.SchemaVersion} is unsupported.");

            var migrated = new List<GameplayPartyCharacterSave>(
                save.Characters.Count);
            foreach (GameplayPartyCharacterSave character in save.Characters)
            {
                ScenarioActorDefinition actor = FindActor(
                    scenario,
                    character.IdentityId);
                var magazines = new List<GameplayPartyWeaponMagazineSave>();
                foreach (InventoryItemDefinition item in actor.Inventory)
                    if (item.Ammunition != null)
                        magazines.Add(new GameplayPartyWeaponMagazineSave(
                            item.Id,
                            item.Ammunition.InitialLoadedRounds));
                var reserves = new List<GameplayPartyAmmunitionReserveSave>();
                foreach (AmmunitionReserveDefinition reserve in
                    actor.AmmunitionReserves)
                    reserves.Add(new GameplayPartyAmmunitionReserveSave(
                        reserve.AmmoTypeId,
                        reserve.Rounds));
                migrated.Add(new GameplayPartyCharacterSave(
                    character.IdentityId,
                    character.EquippedItemId,
                    magazines,
                    reserves));
            }
            return new GameplayPartySave(
                GameplayPartySave.CurrentSchemaVersion,
                migrated);
        }

        private static ScenarioActorDefinition FindActor(
            ScenarioDefinition scenario,
            string identityId)
        {
            foreach (ScenarioActorDefinition actor in scenario.Actors)
                if (string.Equals(
                    actor.CharacterProfile?.IdentityId,
                    identityId,
                    StringComparison.Ordinal))
                    return actor;
            throw new InvalidOperationException(
                $"Saved character identity '{identityId}' is not authored by the scenario.");
        }
    }

    public static class GameplayPartySaveValidator
    {
        public static void Validate(
            GameplayPartySave save,
            ScenarioDefinition scenario)
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (save.SchemaVersion != GameplayPartySave.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Party save schema {save.SchemaVersion} is unsupported; "
                    + $"expected {GameplayPartySave.CurrentSchemaVersion}.");
            }

            PlayerPartyDefinition party = scenario.PlayerParty
                ?? throw new InvalidOperationException(
                    "Party saves require an authored player party.");
            if (save.Characters.Count != party.ActorIds.Count)
            {
                throw new InvalidOperationException(
                    "The saved party roster does not match the authored party.");
            }

            foreach (string actorId in party.ActorIds)
            {
                ScenarioActorDefinition actor = scenario.GetActor(actorId);
                CharacterProfileDefinition profile = actor.CharacterProfile
                    ?? throw new InvalidOperationException(
                        $"Party actor '{actorId}' has no character identity.");
                if (!save.TryGetCharacter(
                        profile.IdentityId,
                        out GameplayPartyCharacterSave character))
                {
                    throw new InvalidOperationException(
                        $"Party save is missing identity '{profile.IdentityId}'.");
                }

                if (character.EquippedItemId != null)
                {
                    InventoryItemDefinition item = actor.GetInventoryItem(
                        character.EquippedItemId);
                    if (item == null || !item.IsEquippable)
                    {
                        throw new InvalidOperationException(
                            $"Saved equipment '{character.EquippedItemId}' is "
                            + $"unavailable to '{profile.IdentityId}'.");
                    }
                }

                int authoredMagazineCount = 0;
                foreach (InventoryItemDefinition item in actor.Inventory)
                {
                    if (item.Ammunition == null) continue;
                    authoredMagazineCount++;
                    if (!character.TryGetMagazine(
                            item.Id,
                            out GameplayPartyWeaponMagazineSave magazine)
                        || magazine.LoadedRounds
                            > item.Ammunition.MagazineCapacity)
                        throw new InvalidOperationException(
                            $"Saved magazine '{item.Id}' is missing or invalid for "
                            + $"'{profile.IdentityId}'.");
                }
                if (character.WeaponMagazines.Count != authoredMagazineCount)
                    throw new InvalidOperationException(
                        $"Saved magazines do not match '{profile.IdentityId}'.");

                foreach (AmmunitionReserveDefinition reserve in
                    actor.AmmunitionReserves)
                    if (!character.TryGetReserve(
                        reserve.AmmoTypeId,
                        out _))
                        throw new InvalidOperationException(
                            $"Saved reserve '{reserve.AmmoTypeId}' is missing for "
                            + $"'{profile.IdentityId}'.");
                if (character.AmmunitionReserves.Count
                    != actor.AmmunitionReserves.Count)
                    throw new InvalidOperationException(
                        $"Saved reserves do not match '{profile.IdentityId}'.");
            }
        }
    }

    public sealed class GameplayPartyPersistenceSession : IDisposable
    {
        private readonly IGameplayPartySaveStore store;
        private GameplaySession gameplay;
        private bool dirty;
        private bool disposed;

        public GameplayPartyPersistenceSession(
            IGameplayPartySaveStore partySaveStore)
        {
            store = partySaveStore
                ?? throw new ArgumentNullException(nameof(partySaveStore));
        }

        public event Action<string> StatusChanged;

        public string Status { get; private set; } = string.Empty;

        public GameplayPartySave Load(ScenarioDefinition scenario)
        {
            ThrowIfDisposed();
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (gameplay != null)
            {
                throw new InvalidOperationException(
                    "Party persistence must load before binding gameplay.");
            }

            GameplayPartySave save;
            try
            {
                if (!store.TryLoad(out save))
                {
                    Report("No saved party was found; using authored character state.");
                    return null;
                }
                save = GameplayPartySaveMigrator.Migrate(save, scenario);
                GameplayPartySaveValidator.Validate(save, scenario);
            }
            catch (Exception exception)
            {
                Report($"Ignored an invalid party save: {exception.Message}");
                return null;
            }

            Report("Loaded the saved party.");
            return save;
        }

        public void Bind(GameplaySession gameplaySession)
        {
            ThrowIfDisposed();
            if (gameplay != null)
                throw new InvalidOperationException(
                    "Party persistence is already bound.");
            gameplay = gameplaySession
                ?? throw new ArgumentNullException(nameof(gameplaySession));
            gameplay.EquipmentChanged += HandleEquipmentChanged;
            gameplay.AmmunitionChanged += HandleAmmunitionChanged;
            dirty = false;
        }

        public bool Flush()
        {
            ThrowIfDisposed();
            if (!dirty)
                return true;
            RequireBound();
            try
            {
                store.Save(GameplayPartySave.Capture(gameplay));
                dirty = false;
            }
            catch (Exception exception)
            {
                Report($"Party save failed: {exception.Message}");
                return false;
            }

            Report("Saved party equipment and ammunition.");
            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            GameplaySession boundGameplay = gameplay;
            try
            {
                if (boundGameplay != null)
                    Flush();
            }
            finally
            {
                if (boundGameplay != null)
                {
                    boundGameplay.EquipmentChanged -=
                        HandleEquipmentChanged;
                    boundGameplay.AmmunitionChanged -=
                        HandleAmmunitionChanged;
                }
                gameplay = null;
                disposed = true;
            }
        }

        private void HandleEquipmentChanged(EquipmentChangeRecord _) =>
            MarkDirtyAndFlush();

        private void HandleAmmunitionChanged(WeaponAmmunitionDelta _) =>
            MarkDirtyAndFlush();

        private void MarkDirtyAndFlush()
        {
            dirty = true;
            Flush();
        }

        private void Report(string value)
        {
            Status = value ?? string.Empty;
            Delegate[] observers = StatusChanged?.GetInvocationList();
            if (observers == null)
                return;

            foreach (Action<string> observer in observers)
            {
                try
                {
                    observer(Status);
                }
                catch
                {
                    // Status observers must not alter persistence semantics.
                }
            }
        }

        private void RequireBound()
        {
            ThrowIfDisposed();
            if (gameplay == null)
                throw new InvalidOperationException(
                    "Party persistence is not bound to gameplay.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(GameplayPartyPersistenceSession));
        }
    }
}
