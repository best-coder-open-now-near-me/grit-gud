using UnityEngine;

namespace GritGud.Presentation
{
    /// <summary>
    /// Central color language for the tactical gameplay presentation.
    /// Keep semantic meaning at call sites, but source every rendered color here so
    /// the full grade can be shifted without hunting through individual presenters.
    /// </summary>
    internal static class GameplayVisualPalette
    {
        // Interface foundations.
        internal static readonly Color Backdrop = new Color(0.004f, 0.014f, 0.038f, 1f);
        internal static readonly Color CameraClear = new Color(0.006f, 0.018f, 0.055f, 1f);
        internal static readonly Color Panel = new Color(0.008f, 0.025f, 0.06f, 0.5f);
        internal static readonly Color Border = new Color(0.14f, 0.5f, 1f, 0.28f);
        internal static readonly Color ButtonNormal = new Color(0.012f, 0.052f, 0.11f, 0.44f);
        internal static readonly Color ButtonHover = new Color(0.02f, 0.15f, 0.31f, 0.62f);
        internal static readonly Color ButtonActive = new Color(0.035f, 0.27f, 0.54f, 0.78f);
        internal static readonly Color MeterTrack = new Color(0.07f, 0.2f, 0.38f, 0.48f);

        // Electric-blue signal family.
        internal static readonly Color SignalBlue = new Color(0.12f, 0.74f, 1f, 1f);
        internal static readonly Color SignalBlueGlow = new Color(0.08f, 0.82f, 1f, 1f);
        internal static readonly Color SignalBlueBright = new Color(0.34f, 0.88f, 1f, 1f);

        // Fluorescent semantic accents.
        internal static readonly Color SignalOrange = new Color(1f, 0.4f, 0.015f, 1f);
        internal static readonly Color SignalOrangeGlow = new Color(1.5f, 0.48f, 0.02f, 1f);
        internal static readonly Color SignalGreen = new Color(0.18f, 1f, 0.52f, 1f);

        // Typography.
        internal static readonly Color TextBright = new Color(0.9f, 0.97f, 1f, 1f);
        internal static readonly Color TextPrimary = new Color(0.78f, 0.89f, 1f, 1f);
        internal static readonly Color TextSecondary = new Color(0.44f, 0.62f, 0.82f, 1f);

        // Shared screen-space HUD semantics. Every gameplay menu uses these
        // tokens so separate panels cannot drift into subtly different grades.
        internal static readonly Color HudPanel = Panel;
        internal static readonly Color HudBorder = WithAlpha(Border, 0.24f);
        internal static readonly Color HudPrimarySignal = SignalBlue;
        internal static readonly Color HudSecondarySignal = SignalOrangeGlow;
        internal static readonly Color HudTextBright = TextBright;
        internal static readonly Color HudTextPrimary = TextPrimary;
        internal static readonly Color HudTextSecondary = TextSecondary;

        // Pointer-targeting semantics shared by labels, outlines, and world
        // previews. Blue confirms an actionable aim; orange explains rejection.
        internal static readonly Color TargetingValid = SignalBlueBright;
        internal static readonly Color TargetingInvalid = SignalOrangeGlow;

        // World-space tactical feedback.
        internal static readonly Color EmissionBase = new Color(0.002f, 0.014f, 0.042f, 1f);
        internal static readonly Color RouteGhost = new Color(0.08f, 0.84f, 1f, 0.76f);
        internal static readonly Color RouteLine = new Color(0.2f, 0.84f, 1f, 0.98f);
        internal static readonly Color RouteFill = new Color(0.01f, 0.18f, 0.42f, 0.18f);
        internal static readonly Color DisplacementPreview = TargetingValid;
        internal static readonly Color DisplacementPreviewInvalid =
            TargetingInvalid;
        internal static readonly Color ProjectileGhost = new Color(1f, 0.38f, 0.01f, 0.82f);
        internal static readonly Color ProjectileGhostLine = new Color(1.6f, 0.5f, 0.015f, 1f);
        internal static readonly Color ProjectileGhostFill = new Color(0.72f, 0.13f, 0.005f, 0.16f);
        internal static readonly Color ProjectileSmoke = new Color(0.44f, 0.51f, 0.58f, 0.34f);
        internal static readonly Color TargetBody = new Color(0.22f, 0.29f, 0.38f, 1f);
        internal static readonly Color ActorShadow = new Color(0.38f, 0.5f, 0.68f, 1f);
        internal static readonly Color OutlineDark = new Color(0.003f, 0.009f, 0.028f, 1f);

        // Environment and global grade.
        internal static readonly Color FixtureHousing = new Color(0.035f, 0.065f, 0.11f, 1f);
        internal static readonly Color GateFlood = new Color(0.12f, 0.6f, 1f, 1f);
        internal static readonly Color DeckFlood = new Color(0.08f, 0.7f, 1f, 1f);
        internal static readonly Color AmbientSky = new Color(0.055f, 0.12f, 0.24f, 1f);
        internal static readonly Color AmbientEquator = new Color(0.028f, 0.07f, 0.14f, 1f);
        internal static readonly Color AmbientGround = new Color(0.009f, 0.027f, 0.065f, 1f);
        internal static readonly Color SubtractiveShadow = new Color(0.022f, 0.055f, 0.125f, 1f);
        internal static readonly Color Fog = new Color(0.016f, 0.045f, 0.11f, 1f);
        internal static readonly Color MoonKey = new Color(0.34f, 0.62f, 1f, 1f);
        internal static readonly Color GradeFilter = new Color(0.89f, 0.95f, 1f, 1f);
        internal static readonly Color BloomTint = new Color(0.5f, 0.82f, 1f, 1f);
        internal static readonly Color Vignette = new Color(0.004f, 0.012f, 0.038f, 1f);

        internal static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}
