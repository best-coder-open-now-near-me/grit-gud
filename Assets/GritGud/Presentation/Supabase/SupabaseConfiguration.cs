using System;
using UnityEngine;

namespace GritGud.Presentation.Supabase
{
    [CreateAssetMenu(
        fileName = "SupabaseConfiguration",
        menuName = "Grit Gud/Supabase Configuration")]
    public sealed class SupabaseConfiguration : ScriptableObject
    {
        [SerializeField] private string projectUrl = string.Empty;
        [SerializeField] private string publishableKey = string.Empty;

        public string ProjectUrl => projectUrl?.Trim().TrimEnd('/') ?? string.Empty;

        public string PublishableKey => publishableKey?.Trim() ?? string.Empty;

        public bool IsConfigured => TryValidate(out _);

        public bool TryValidate(out string error)
        {
            if (!Uri.TryCreate(ProjectUrl, UriKind.Absolute, out Uri uri)
                || uri.Scheme != Uri.UriSchemeHttps)
            {
                error = "Supabase project URL must be an absolute HTTPS URL.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PublishableKey))
            {
                error = "A Supabase publishable key is required.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
