using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace GritGud.Presentation.Supabase
{
    public sealed class SupabaseClient
    {
        private readonly SupabaseConfiguration configuration;

        public SupabaseClient(SupabaseConfiguration configuration)
        {
            this.configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            if (!configuration.TryValidate(out string error))
                throw new ArgumentException(error, nameof(configuration));
        }

        public IEnumerator SignInAnonymously(
            Action<SupabaseSession> succeeded,
            Action<string> failed)
        {
            if (succeeded == null)
                throw new ArgumentNullException(nameof(succeeded));
            if (failed == null)
                throw new ArgumentNullException(nameof(failed));

            using UnityWebRequest request = CreateRequest(
                "/auth/v1/signup",
                UnityWebRequest.kHttpVerbPOST,
                "{\"data\":{},\"gotrue_meta_security\":{}}");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                failed(DescribeFailure(request));
                yield break;
            }

            AnonymousSignInResponse response;
            try
            {
                response = JsonUtility.FromJson<AnonymousSignInResponse>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                failed("Supabase returned an invalid anonymous-session response: " + exception.Message);
                yield break;
            }

            if (response?.user == null
                || string.IsNullOrWhiteSpace(response.access_token)
                || string.IsNullOrWhiteSpace(response.user.id))
            {
                failed("Supabase did not return an anonymous session.");
                yield break;
            }

            succeeded(CreateSession(response));
        }

        public IEnumerator RefreshSession(string refreshToken, Action<SupabaseSession> succeeded, Action<string> failed)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) throw new ArgumentException("A refresh token is required.", nameof(refreshToken));
            using UnityWebRequest request = CreateRequest("/auth/v1/token?grant_type=refresh_token", UnityWebRequest.kHttpVerbPOST, "{\"refresh_token\":\"" + refreshToken.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) { failed(DescribeFailure(request)); yield break; }
            AnonymousSignInResponse response = JsonUtility.FromJson<AnonymousSignInResponse>(request.downloadHandler.text);
            if (response?.user == null || string.IsNullOrWhiteSpace(response.access_token) || string.IsNullOrWhiteSpace(response.user.id)) { failed("Supabase did not return a refreshed session."); yield break; }
            succeeded(CreateSession(response));
        }

        public IEnumerator UpsertDocument(
            string table,
            string conflictColumns,
            string rowJson,
            SupabaseSession session,
            Action succeeded,
            Action<string> failed)
        {
            if (string.IsNullOrWhiteSpace(table))
                throw new ArgumentException("A table is required.", nameof(table));
            if (string.IsNullOrWhiteSpace(conflictColumns))
                throw new ArgumentException("Conflict columns are required.", nameof(conflictColumns));
            if (string.IsNullOrWhiteSpace(rowJson))
                throw new ArgumentException("A row document is required.", nameof(rowJson));
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (succeeded == null)
                throw new ArgumentNullException(nameof(succeeded));
            if (failed == null)
                throw new ArgumentNullException(nameof(failed));

            using UnityWebRequest request = CreateRequest(
                "/rest/v1/" + table + "?on_conflict=" + Uri.EscapeDataString(conflictColumns),
                UnityWebRequest.kHttpVerbPOST,
                rowJson);
            request.SetRequestHeader("Authorization", "Bearer " + session.AccessToken);
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
                succeeded();
            else
                failed(DescribeFailure(request));
        }

        private UnityWebRequest CreateRequest(string relativePath, string method, string body)
        {
            var request = new UnityWebRequest(configuration.ProjectUrl + relativePath, method)
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("apikey", configuration.PublishableKey);
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static string DescribeFailure(UnityWebRequest request)
        {
            string detail = request.downloadHandler?.text;
            return string.IsNullOrWhiteSpace(detail)
                ? "Supabase request failed: " + request.error
                : "Supabase request failed: " + detail;
        }

        private static SupabaseSession CreateSession(AnonymousSignInResponse response) =>
            new SupabaseSession(response.access_token, response.refresh_token, response.user.id);

        [Serializable]
        private sealed class AnonymousSignInResponse
        {
            public string access_token;
            public string refresh_token;
            public User user;
        }

        [Serializable]
        private sealed class User
        {
            public string id;
        }
    }
}
