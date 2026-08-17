using System;

namespace GritGud.Presentation.Supabase
{
    internal sealed class SupabaseAuthenticationState
    {
        private const float RetryDelaySeconds = 15f;

        public string PendingRefreshToken { get; private set; } = string.Empty;

        public bool AnonymousSignInRequired { get; private set; }

        public bool RequestRunning { get; private set; }

        public float NextAttemptAt { get; private set; }

        public bool ShouldRefresh =>
            !AnonymousSignInRequired
            && !string.IsNullOrWhiteSpace(PendingRefreshToken);

        public void Initialize(string refreshToken)
        {
            PendingRefreshToken = refreshToken ?? string.Empty;
            AnonymousSignInRequired = string.IsNullOrWhiteSpace(
                PendingRefreshToken);
            RequestRunning = false;
            NextAttemptAt = 0f;
        }

        public bool TryBegin()
        {
            if (RequestRunning)
                return false;
            RequestRunning = true;
            return true;
        }

        public void Complete(SupabaseSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            RequestRunning = false;
            PendingRefreshToken = session.RefreshToken;
            AnonymousSignInRequired = false;
            NextAttemptAt = 0f;
        }

        public void FailSignIn(float now)
        {
            RequestRunning = false;
            NextAttemptAt = now + RetryDelaySeconds;
        }

        public bool FailRefresh(string error, float now)
        {
            RequestRunning = false;
            if (IsInvalidRefreshFailure(error))
            {
                PendingRefreshToken = string.Empty;
                AnonymousSignInRequired = true;
                NextAttemptAt = now;
                return true;
            }

            NextAttemptAt = now + RetryDelaySeconds;
            return false;
        }

        internal static bool IsInvalidRefreshFailure(string error) =>
            !string.IsNullOrWhiteSpace(error)
            && (error.IndexOf(
                    "refresh_token_not_found",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf(
                    "invalid refresh token",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf(
                    "already used",
                    StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
