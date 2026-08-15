using System;

namespace GritGud.Presentation.Supabase
{
    public sealed class SupabaseSession
    {
        public SupabaseSession(
            string accessToken,
            string refreshToken,
            string userId,
            DateTimeOffset? expiresAt = null)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("An access token is required.", nameof(accessToken));
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("A user ID is required.", nameof(userId));
            AccessToken = accessToken;
            RefreshToken = refreshToken ?? string.Empty;
            UserId = userId;
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1);
        }

        public string AccessToken { get; }

        public string RefreshToken { get; }

        public string UserId { get; }

        public DateTimeOffset ExpiresAt { get; }

        public bool NeedsRefresh(DateTimeOffset now, TimeSpan buffer) =>
            now >= ExpiresAt - buffer;
    }
}
