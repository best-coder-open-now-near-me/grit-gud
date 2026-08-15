using System;
using UnityEngine;

namespace GritGud.Presentation.Supabase
{
    public sealed class SupabaseRuntime : MonoBehaviour
    {
        private const string ConfigurationResourceKey = "SupabaseConfiguration";
        private SupabaseClient client;

        public SupabaseSession Session { get; private set; }

        public SupabaseDocumentStore Documents { get; private set; }

        public string Status { get; private set; } = "Cloud saves are not configured.";

        public bool IsReady => Session != null && Documents != null;

        private void Awake()
        {
            SupabaseConfiguration configuration = Resources.Load<SupabaseConfiguration>(
                ConfigurationResourceKey);
            if (configuration == null)
                return;
            if (!configuration.TryValidate(out string error))
            {
                Status = error;
                return;
            }

            client = new SupabaseClient(configuration);
            Status = "Signing in to cloud saves…";
            StartCoroutine(client.SignInAnonymously(HandleSignedIn, HandleSignInFailed));
        }

        public void SaveLevelDraft(string slot, string serializedLevel, Action<string> completed)
        {
            if (!IsReady)
            {
                completed?.Invoke(Status);
                return;
            }
            StartCoroutine(Documents.SaveLevelDraft(
                slot,
                serializedLevel,
                Session,
                () => completed?.Invoke("Saved the level draft to cloud."),
                error => completed?.Invoke(error)));
        }

        public void SaveCharacter(
            GritGud.Domain.Characters.CharacterDocument character,
            string serializedCharacter,
            Action<string> completed)
        {
            if (!IsReady)
            {
                completed?.Invoke(Status);
                return;
            }
            StartCoroutine(Documents.SaveCharacter(
                character,
                serializedCharacter,
                Session,
                () => completed?.Invoke("Saved the character to cloud."),
                error => completed?.Invoke(error)));
        }

        private void HandleSignedIn(SupabaseSession session)
        {
            Session = session;
            Documents = new SupabaseDocumentStore(client);
            Status = "Cloud saves connected.";
        }

        private void HandleSignInFailed(string error)
        {
            Status = error;
        }
    }
}
