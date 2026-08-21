using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Characters;
using UnityEngine;

namespace GritGud.Presentation.Characters
{
    public sealed class PublishedCharacterEntry
    {
        private readonly CharacterDocument document;

        internal PublishedCharacterEntry(string resourceKey, CharacterDocument source)
        {
            ResourceKey = resourceKey;
            document = source.DeepCopy();
        }

        public string ResourceKey { get; }
        public string CharacterId => document.characterId;
        public string DisplayName => document.displayName;
        public CharacterDocument CreateSnapshot() => document.DeepCopy();
    }

    public sealed class UnityCharacterLibrary
    {
        public const string PublishedResourceFolder = "Characters/Published";

        private readonly PublishedCharacterEntry[] entries;
        private readonly Dictionary<string, PublishedCharacterEntry> byId;

        private UnityCharacterLibrary(PublishedCharacterEntry[] characterEntries)
        {
            entries = characterEntries;
            byId = entries.ToDictionary(entry => entry.CharacterId, StringComparer.Ordinal);
        }

        public IReadOnlyList<PublishedCharacterEntry> Entries => entries;

        public PublishedCharacterEntry Find(string characterId) =>
            characterId != null && byId.TryGetValue(characterId, out var entry)
                ? entry
                : null;

        public ISet<string> CreateKnownIdSet() =>
            new HashSet<string>(byId.Keys, StringComparer.Ordinal);

        public static UnityCharacterLibrary LoadDefault(
            CharacterAppearanceCatalog appearanceCatalog = null)
        {
            CharacterAppearanceCatalog catalog = appearanceCatalog
                ?? CharacterAppearanceCatalog.LoadDefault();
            var serializer = new UnityCharacterJsonSerializer();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            PublishedCharacterEntry[] loaded = Resources
                .LoadAll<TextAsset>(PublishedResourceFolder)
                .OrderBy(asset => asset.name, StringComparer.Ordinal)
                .Select(asset =>
                {
                    CharacterDocument document = serializer.Deserialize(asset.text);
                    IReadOnlyList<string> issues = CharacterValidator.Validate(
                        document,
                        catalog.CreateValidationContent());
                    if (issues.Count > 0)
                        throw new InvalidOperationException(
                            $"Published character '{asset.name}' is invalid: {issues[0]}");
                    if (!ids.Add(document.characterId))
                        throw new InvalidOperationException(
                            $"Published character ID '{document.characterId}' is duplicated.");
                    return new PublishedCharacterEntry(
                        PublishedResourceFolder + "/" + asset.name,
                        document);
                })
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new UnityCharacterLibrary(loaded);
        }
    }
}
