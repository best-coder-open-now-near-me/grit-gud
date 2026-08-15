using System;
using System.Collections;
using GritGud.Domain.Characters;

namespace GritGud.Presentation.Supabase
{
    public sealed class SupabaseDocumentStore
    {
        private readonly SupabaseClient client;

        public SupabaseDocumentStore(SupabaseClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public IEnumerator SaveLevelDraft(
            string slot,
            string serializedLevel,
            SupabaseSession session,
            Action succeeded,
            Action<string> failed)
        {
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("A level draft slot is required.", nameof(slot));
            return client.UpsertDocument(
                "level_drafts",
                "owner_id,slot",
                "{\"slot\":" + QuoteJsonString(slot.Trim())
                    + ",\"document\":" + RequireJson(serializedLevel) + "}",
                session,
                succeeded,
                failed);
        }

        public IEnumerator SaveCharacter(
            CharacterDocument character,
            string serializedCharacter,
            SupabaseSession session,
            Action succeeded,
            Action<string> failed)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            character.Normalize();
            if (string.IsNullOrWhiteSpace(character.characterId))
                throw new ArgumentException("A character ID is required.", nameof(character));
            return client.UpsertDocument(
                "character_documents",
                "owner_id,character_id",
                "{\"character_id\":" + QuoteJsonString(character.characterId)
                    + ",\"document\":" + RequireJson(serializedCharacter) + "}",
                session,
                succeeded,
                failed);
        }

        public IEnumerator LoadLevelDraft(string slot, SupabaseSession session, Action<string> succeeded, Action<string> failed) =>
            client.LoadDocument("load_level_draft", "{\"requested_slot\":" + QuoteJsonString(slot) + "}", session, succeeded, failed);

        public IEnumerator LoadCharacter(string characterId, SupabaseSession session, Action<string> succeeded, Action<string> failed) =>
            client.LoadDocument("load_character_document", "{\"requested_character_id\":" + QuoteJsonString(characterId) + "}", session, succeeded, failed);

        private static string RequireJson(string document)
        {
            if (string.IsNullOrWhiteSpace(document))
                throw new ArgumentException("A serialized document is required.", nameof(document));
            string trimmed = document.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal)
                || !trimmed.EndsWith("}", StringComparison.Ordinal))
                throw new ArgumentException("The serialized document must be a JSON object.", nameof(document));
            return trimmed;
        }

        private static string QuoteJsonString(string value)
        {
            return "\"" + (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\b", "\\b")
                .Replace("\f", "\\f")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t") + "\"";
        }
    }
}
