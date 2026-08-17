using System;
using UnityEngine;

namespace GritGud.Presentation.Supabase
{
    internal static class SupabaseResponseParser
    {
        public static bool TryParseSession(
            string json,
            string responseName,
            DateTimeOffset receivedAt,
            out SupabaseSession session,
            out string error)
        {
            session = null;
            if (!TryDeserialize(
                    json,
                    responseName,
                    out SupabaseSessionResponse response,
                    out error))
            {
                return false;
            }

            if (response.user == null
                || string.IsNullOrWhiteSpace(response.access_token)
                || string.IsNullOrWhiteSpace(response.user.id))
            {
                error = $"Supabase did not return a valid {responseName}.";
                return false;
            }

            session = new SupabaseSession(
                response.access_token,
                response.refresh_token,
                response.user.id,
                receivedAt.AddSeconds(Math.Max(60, response.expires_in)));
            error = string.Empty;
            return true;
        }

        public static bool TryParseDocument(
            string json,
            out string document,
            out string error)
        {
            document = null;
            if (!TryDeserialize(
                    WrapRows(json),
                    "cloud-document response",
                    out SupabaseDocumentRows rows,
                    out error))
            {
                return false;
            }

            if (rows.rows == null
                || rows.rows.Length == 0
                || string.IsNullOrWhiteSpace(rows.rows[0]?.document))
            {
                error = "No cloud document was found.";
                return false;
            }

            document = rows.rows[0].document;
            error = string.Empty;
            return true;
        }

        public static T DeserializeRpcRows<T>(
            string json,
            string responseName)
            where T : class
        {
            if (!TryDeserialize(
                    WrapRows(json),
                    responseName,
                    out T result,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }

            return result;
        }

        public static bool TryDeserialize<T>(
            string json,
            string responseName,
            out T result,
            out string error)
            where T : class
        {
            result = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = $"Supabase returned an empty {responseName}.";
                return false;
            }

            try
            {
                result = JsonUtility.FromJson<T>(json);
            }
            catch (Exception exception)
            {
                error = $"Supabase returned an invalid {responseName}: "
                    + exception.Message;
                return false;
            }

            if (result == null)
            {
                error = $"Supabase returned an invalid {responseName}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string WrapRows(string json) =>
            "{\"rows\":" + (string.IsNullOrWhiteSpace(json) ? "null" : json) + "}";

        [Serializable]
        private sealed class SupabaseSessionResponse
        {
            public string access_token;
            public string refresh_token;
            public int expires_in;
            public SupabaseUserResponse user;
        }

        [Serializable]
        private sealed class SupabaseUserResponse
        {
            public string id;
        }

        [Serializable]
        private sealed class SupabaseDocumentRows
        {
            public SupabaseDocumentRow[] rows;
        }

        [Serializable]
        private sealed class SupabaseDocumentRow
        {
            public string document;
        }
    }

    internal sealed class SupabaseCallbackCompletion<TSuccess, TFailure>
    {
        private readonly Action<TSuccess> succeeded;
        private readonly Action<TFailure> failed;

        public SupabaseCallbackCompletion(
            Action<TSuccess> onSucceeded,
            Action<TFailure> onFailed)
        {
            succeeded = onSucceeded ?? throw new ArgumentNullException(
                nameof(onSucceeded));
            failed = onFailed ?? throw new ArgumentNullException(
                nameof(onFailed));
        }

        public bool IsCompleted { get; private set; }

        public void Succeed(TSuccess value)
        {
            if (IsCompleted)
                return;
            IsCompleted = true;
            succeeded(value);
        }

        public void Fail(TFailure failure)
        {
            if (IsCompleted)
                return;
            IsCompleted = true;
            failed(failure);
        }
    }
}
