using System;
using GritGud.Application.Levels;
using UnityEngine;

namespace GritGud.Presentation.Supabase
{
    public sealed class SupabaseRuntime : MonoBehaviour
    {
        private const string ConfigurationResourceKey = "SupabaseConfiguration";
        private const string RefreshTokenKey = "grit-gud.supabase.refresh-token";
        private readonly SupabaseAuthenticationState authentication =
            new SupabaseAuthenticationState();
        private SupabaseClient client;

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
            authentication.Initialize(PlayerPrefs.GetString(
                RefreshTokenKey,
                string.Empty));
            BeginAuthentication();
        }

        private void Update()
        {
            if (client == null
                || authentication.RequestRunning
                || Time.unscaledTime < authentication.NextAttemptAt)
                return;
            if (Session != null
                && !Session.NeedsRefresh(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2)))
                return;
            BeginAuthentication();
        }

        private void BeginAuthentication()
        {
            if (!authentication.TryBegin())
                return;
            bool refreshing = authentication.ShouldRefresh;
            try
            {
                if (refreshing)
                {
                    StartCoroutine(client.RefreshSession(
                        authentication.PendingRefreshToken,
                        HandleSignedIn,
                        HandleRefreshFailed));
                }
                else
                {
                    StartCoroutine(client.SignInAnonymously(
                        HandleSignedIn,
                        HandleSignInFailed));
                }
            }
            catch (Exception exception)
            {
                string error = "Cloud authentication could not start: "
                    + exception.Message;
                if (refreshing)
                    HandleRefreshFailed(error);
                else
                    HandleSignInFailed(error);
            }
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
            authentication.Complete(session);
            Session = session;
            if (!string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                PlayerPrefs.SetString(RefreshTokenKey, session.RefreshToken);
                PlayerPrefs.Save();
            }
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
            authentication.FailSignIn(Time.unscaledTime);
            Status = error;
        }

        private void HandleRefreshFailed(string error)
        {
            Status = error;
            if (authentication.FailRefresh(error, Time.unscaledTime))
            {
                PlayerPrefs.DeleteKey(RefreshTokenKey);
                PlayerPrefs.Save();
                BeginAuthentication();
            }
        }
    }
}
