using System;
using GritGud.Application.Levels;
using UnityEngine;

namespace GritGud.Presentation.Supabase
{
    public sealed class SupabaseRuntime : MonoBehaviour
    {
        private const string ConfigurationResourceKey = "SupabaseConfiguration";
        private const string RefreshTokenKey = "grit-gud.supabase.refresh-token";
        private SupabaseClient client;
        private string pendingRefreshToken = string.Empty;
        private bool authRequestRunning;
        private bool anonymousSignInRequired;
        private float nextAuthAttemptAt;

        public SupabaseSession Session { get; private set; }

        public SupabaseDocumentStore Documents { get; private set; }

        public LevelDraftLibraryService DraftLibrary { get; private set; }

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
            pendingRefreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
            anonymousSignInRequired = string.IsNullOrWhiteSpace(pendingRefreshToken);
            BeginAuthentication();
        }

        private void Update()
        {
            if (client == null || authRequestRunning || Time.unscaledTime < nextAuthAttemptAt)
                return;
            if (Session != null
                && !Session.NeedsRefresh(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2)))
                return;
            BeginAuthentication();
        }

        private void BeginAuthentication()
        {
            if (authRequestRunning) return;
            authRequestRunning = true;
            if (!anonymousSignInRequired && !string.IsNullOrWhiteSpace(pendingRefreshToken))
                StartCoroutine(client.RefreshSession(pendingRefreshToken, HandleSignedIn, HandleRefreshFailed));
            else
                StartCoroutine(client.SignInAnonymously(HandleSignedIn, HandleSignInFailed));
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

        public void LoadCharacter(string characterId, Action<string> succeeded, Action<string> failed)
        {
            if (!IsReady) { failed?.Invoke(Status); return; }
            StartCoroutine(Documents.LoadCharacter(characterId, Session, succeeded, failed));
        }

        private void HandleSignedIn(SupabaseSession session)
        {
            authRequestRunning = false;
            Session = session;
            if (!string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                PlayerPrefs.SetString(RefreshTokenKey, session.RefreshToken);
                PlayerPrefs.Save();
            }
            pendingRefreshToken = session.RefreshToken;
            anonymousSignInRequired = false;
            if (Documents == null)
                Documents = new SupabaseDocumentStore(client);
            if (DraftLibrary == null)
            {
                DraftLibrary = new LevelDraftLibraryService(
                    new SupabaseLevelDraftRepository(this, client, () => Session));
            }
            Status = "Cloud saves connected.";
        }

        private void HandleSignInFailed(string error)
        {
            authRequestRunning = false;
            Status = error;
            nextAuthAttemptAt = Time.unscaledTime + 15f;
        }

        private void HandleRefreshFailed(string error)
        {
            authRequestRunning = false;
            Status = error;
            if (IsInvalidRefreshFailure(error))
            {
                PlayerPrefs.DeleteKey(RefreshTokenKey);
                PlayerPrefs.Save();
                pendingRefreshToken = string.Empty;
                anonymousSignInRequired = true;
                BeginAuthentication();
                return;
            }
            nextAuthAttemptAt = Time.unscaledTime + 15f;
        }

        private static bool IsInvalidRefreshFailure(string error) =>
            !string.IsNullOrWhiteSpace(error)
            && (error.IndexOf("refresh_token_not_found", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("invalid refresh token", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("already used", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
