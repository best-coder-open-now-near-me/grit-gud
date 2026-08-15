using System;

namespace GritGud.Presentation.Supabase
{
    public sealed class SupabaseSession
    {
        public SupabaseSession(string accessToken, string refreshToken, string userId)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("An access token is required.", nameof(accessToken));
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("A user ID is required.", nameof(userId));
            AccessToken = accessToken;
            RefreshToken = refreshToken ?? string.Empty;
            UserId = userId;
        }

        public string AccessToken { get; }

        public string RefreshToken { get; }

        public string UserId { get; }
    }
}
