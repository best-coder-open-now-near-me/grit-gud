using System;
using System.Collections;
using System.Threading;
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
            var completion = new SupabaseCallbackCompletion<
                SupabaseSession,
                string>(succeeded, failed);

            using UnityWebRequest request = CreateRequest(
                "/auth/v1/signup",
                UnityWebRequest.kHttpVerbPOST,
                "{\"data\":{},\"gotrue_meta_security\":{}}");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                completion.Fail(DescribeFailure(request));
                yield break;
            }

            if (!SupabaseResponseParser.TryParseSession(
                    request.downloadHandler?.text,
                    "anonymous-session response",
                    DateTimeOffset.UtcNow,
                    out SupabaseSession session,
                    out string error))
            {
                completion.Fail(error);
                yield break;
            }

            completion.Succeed(session);
        }

        public IEnumerator RefreshSession(
            string refreshToken,
            Action<SupabaseSession> succeeded,
            Action<string> failed)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ArgumentException(
                    "A refresh token is required.",
                    nameof(refreshToken));
            }
            var completion = new SupabaseCallbackCompletion<
                SupabaseSession,
                string>(succeeded, failed);
            using UnityWebRequest request = CreateRequest(
                "/auth/v1/token?grant_type=refresh_token",
                UnityWebRequest.kHttpVerbPOST,
                "{\"refresh_token\":\""
                    + refreshToken.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    + "\"}");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                completion.Fail(DescribeFailure(request));
                yield break;
            }

            if (!SupabaseResponseParser.TryParseSession(
                    request.downloadHandler?.text,
                    "refreshed-session response",
                    DateTimeOffset.UtcNow,
                    out SupabaseSession session,
                    out string error))
            {
                completion.Fail(error);
                yield break;
            }

            completion.Succeed(session);
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
            var completion = new SupabaseCallbackCompletion<bool, string>(
                _ => succeeded(),
                failed);

            using UnityWebRequest request = CreateRequest(
                "/rest/v1/" + table + "?on_conflict=" + Uri.EscapeDataString(conflictColumns),
                UnityWebRequest.kHttpVerbPOST,
                rowJson);
            request.SetRequestHeader("Authorization", "Bearer " + session.AccessToken);
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
                completion.Succeed(true);
            else
                completion.Fail(DescribeFailure(request));
        }

        public IEnumerator LoadDocument(string functionName, string argumentsJson, SupabaseSession session, Action<string> succeeded, Action<string> failed)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("A function name is required.", nameof(functionName));
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            var completion = new SupabaseCallbackCompletion<string, string>(
                succeeded,
                failed);
            using UnityWebRequest request = CreateRequest(
                "/rest/v1/rpc/" + functionName,
                UnityWebRequest.kHttpVerbPOST,
                argumentsJson ?? "{}");
            request.SetRequestHeader("Authorization", "Bearer " + session.AccessToken);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                completion.Fail(DescribeFailure(request));
                yield break;
            }

            if (!SupabaseResponseParser.TryParseDocument(
                    request.downloadHandler?.text,
                    out string document,
                    out string error))
            {
                completion.Fail(error);
                yield break;
            }

            completion.Succeed(document);
        }

        public IEnumerator InvokeRpc(
            string functionName,
            string argumentsJson,
            SupabaseSession session,
            CancellationToken cancellationToken,
            Action<string> succeeded,
            Action<SupabaseRequestFailure> failed)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("A function name is required.", nameof(functionName));
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            var completion = new SupabaseCallbackCompletion<
                string,
                SupabaseRequestFailure>(succeeded, failed);
            if (cancellationToken.IsCancellationRequested)
            {
                completion.Fail(SupabaseRequestFailure.Cancelled());
                yield break;
            }

            using UnityWebRequest request = CreateRequest(
                "/rest/v1/rpc/" + functionName,
                UnityWebRequest.kHttpVerbPOST,
                argumentsJson ?? "{}");
            request.SetRequestHeader("Authorization", "Bearer " + session.AccessToken);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    completion.Fail(SupabaseRequestFailure.Cancelled());
                    yield break;
                }
                yield return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                completion.Fail(SupabaseRequestFailure.Cancelled());
                yield break;
            }
            if (request.result == UnityWebRequest.Result.Success)
                completion.Succeed(request.downloadHandler?.text ?? string.Empty);
            else
                completion.Fail(CreateFailure(request));
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

        private static SupabaseRequestFailure CreateFailure(UnityWebRequest request)
        {
            string body = request.downloadHandler?.text ?? string.Empty;
            SupabaseResponseParser.TryDeserialize(
                body,
                "error response",
                out SupabaseErrorResponse response,
                out _);
            return new SupabaseRequestFailure(
                response?.code ?? string.Empty,
                string.IsNullOrWhiteSpace(response?.message) ? (request.error ?? "Supabase request failed.") : response.message,
                request.responseCode);
        }

        [Serializable] private sealed class SupabaseErrorResponse { public string code; public string message; }
    }

    public sealed class SupabaseRequestFailure
    {
        public SupabaseRequestFailure(
            string code,
            string message,
            long statusCode,
            bool isCancelled = false)
        {
            Code = code ?? string.Empty;
            Message = message ?? "Supabase request failed.";
            StatusCode = statusCode;
            IsCancelled = isCancelled;
        }

        public string Code { get; }
        public string Message { get; }
        public long StatusCode { get; }
        public bool IsCancelled { get; }

        public static SupabaseRequestFailure Cancelled() =>
            new SupabaseRequestFailure(
                "cancelled",
                "Supabase request cancelled.",
                statusCode: 0,
                isCancelled: true);
    }
}
