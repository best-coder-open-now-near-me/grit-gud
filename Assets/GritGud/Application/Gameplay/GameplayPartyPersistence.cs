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

    public sealed class GameplayPartySave
    {
        public const int CurrentSchemaVersion = 1;

        private readonly Dictionary<string, CharacterPersistenceSnapshot>
            charactersByIdentity;

        public GameplayPartySave(
            int schemaVersion,
            IEnumerable<CharacterPersistenceSnapshot> characters)
        {
            if (schemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (characters == null)
                throw new ArgumentNullException(nameof(characters));

            SchemaVersion = schemaVersion;
            var copy = new List<CharacterPersistenceSnapshot>();
            charactersByIdentity =
                new Dictionary<string, CharacterPersistenceSnapshot>(
                    StringComparer.Ordinal);
            foreach (CharacterPersistenceSnapshot character in characters)
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

        public IReadOnlyList<CharacterPersistenceSnapshot> Characters { get; }

        public bool TryGetCharacter(
            string identityId,
            out CharacterPersistenceSnapshot character) =>
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
            var characters = new List<CharacterPersistenceSnapshot>(
                party.ActorIds.Count);
            foreach (string actorId in party.ActorIds)
            {
                ScenarioActorDefinition definition =
                    gameplay.Scenario.GetActor(actorId);
                CharacterProfileDefinition profile = definition.CharacterProfile
                    ?? throw new InvalidOperationException(
                        $"Party actor '{actorId}' has no character identity.");
                GameplayActorSnapshot actor = gameplay.GetActor(actorId);
                characters.Add(new CharacterPersistenceSnapshot(
                    profile.IdentityId,
                    actor.EquippedItemId,
                    actor.Wounds));
            }
            return new GameplayPartySave(
                CurrentSchemaVersion,
                characters);
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
                        out CharacterPersistenceSnapshot character))
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

            try
            {
                if (!store.TryLoad(out GameplayPartySave save))
                {
                    Report("No saved party was found; using authored character state.");
                    return null;
                }
                GameplayPartySaveValidator.Validate(save, scenario);
                Report("Loaded the saved party.");
                return save;
            }
            catch (Exception exception)
            {
                Report($"Ignored an invalid party save: {exception.Message}");
                return null;
            }
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
            gameplay.ActorCapabilityChanged += HandleActorCapabilityChanged;
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
                Report("Saved party equipment and wounds.");
                return true;
            }
            catch (Exception exception)
            {
                Report($"Party save failed: {exception.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            if (gameplay != null)
            {
                Flush();
                gameplay.EquipmentChanged -= HandleEquipmentChanged;
                gameplay.ActorCapabilityChanged -= HandleActorCapabilityChanged;
            }
            gameplay = null;
            disposed = true;
        }

        private void HandleEquipmentChanged(EquipmentChangeRecord _) =>
            MarkDirtyAndFlush();

        private void HandleActorCapabilityChanged(string _) =>
            MarkDirtyAndFlush();

        private void MarkDirtyAndFlush()
        {
            dirty = true;
            Flush();
        }

        private void Report(string value)
        {
            Status = value ?? string.Empty;
            StatusChanged?.Invoke(Status);
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
