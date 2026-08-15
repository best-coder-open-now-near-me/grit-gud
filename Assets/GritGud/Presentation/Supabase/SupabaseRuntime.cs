using System;
using UnityEngine;

namespace GritGud.Presentation.Supabase
{
    public sealed class SupabaseRuntime : MonoBehaviour
    {
        private const string ConfigurationResourceKey = "SupabaseConfiguration";
        private const string RefreshTokenKey = "grit-gud.supabase.refresh-token";
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
            string refreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
            if (string.IsNullOrWhiteSpace(refreshToken))
                StartCoroutine(client.SignInAnonymously(HandleSignedIn, HandleSignInFailed));
            else
                StartCoroutine(client.RefreshSession(refreshToken, HandleSignedIn, HandleRefreshFailed));
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

        public void LoadLevelDraft(string slot, Action<string> succeeded, Action<string> failed)
        {
            if (!IsReady) { failed?.Invoke(Status); return; }
            StartCoroutine(Documents.LoadLevelDraft(slot, Session, succeeded, failed));
        }

        public void LoadCharacter(string characterId, Action<string> succeeded, Action<string> failed)
        {
            if (!IsReady) { failed?.Invoke(Status); return; }
            StartCoroutine(Documents.LoadCharacter(characterId, Session, succeeded, failed));
        }

        private void HandleSignedIn(SupabaseSession session)
        {
            Session = session;
            if (!string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                PlayerPrefs.SetString(RefreshTokenKey, session.RefreshToken);
                PlayerPrefs.Save();
            }
            Documents = new SupabaseDocumentStore(client);
            Status = "Cloud saves connected.";
        }

        private void HandleSignInFailed(string error)
        {
            Status = error;
        }

        private void HandleRefreshFailed(string error)
        {
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.Save();
            StartCoroutine(client.SignInAnonymously(HandleSignedIn, HandleSignInFailed));
        }
    }
}
