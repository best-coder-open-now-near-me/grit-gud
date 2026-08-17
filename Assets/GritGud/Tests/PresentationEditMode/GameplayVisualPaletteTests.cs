using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayVisualPaletteTests
    {
        [Test]
        public void AlphaVariationPreservesTheSharedHue()
        {
            Color source = GameplayVisualPalette.SignalBlue;
            Color variation = GameplayVisualPalette.WithAlpha(source, 0.25f);

            Assert.That(variation.r, Is.EqualTo(source.r));
            Assert.That(variation.g, Is.EqualTo(source.g));
            Assert.That(variation.b, Is.EqualTo(source.b));
            Assert.That(variation.a, Is.EqualTo(0.25f));
        }

        [Test]
        public void SignalFamiliesRemainFluorescentAndDistinct()
        {
            Color blue = GameplayVisualPalette.SignalBlue;
            Assert.That(blue.b, Is.EqualTo(1f));
            Assert.That(blue.g, Is.GreaterThan(0.7f));
            Assert.That(blue.r, Is.LessThan(0.2f));

            Color orange = GameplayVisualPalette.SignalOrangeGlow;
            Assert.That(orange.r, Is.GreaterThan(1f), "The target orange is HDR for bloom.");
            Assert.That(orange.g, Is.GreaterThan(0.35f));
            Assert.That(orange.b, Is.LessThan(0.1f));

            Color green = GameplayVisualPalette.SignalGreen;
            Assert.That(green.g, Is.EqualTo(1f));
            Assert.That(green.r, Is.LessThan(green.b));
        }

        [Test]
        public void DarkFoundationsCarryTheCoolBlueGrade()
        {
            AssertBlueLeaning(GameplayVisualPalette.Backdrop);
            AssertBlueLeaning(GameplayVisualPalette.CameraClear);
            AssertBlueLeaning(GameplayVisualPalette.Panel);
            AssertBlueLeaning(GameplayVisualPalette.Fog);
            AssertBlueLeaning(GameplayVisualPalette.Vignette);
        }

        [Test]
        public void GameplayMenusShareOneHudColorLanguage()
        {
            Assert.That(GameplayVisualPalette.HudPanel,
                Is.EqualTo(GameplayVisualPalette.Panel));
            Assert.That(GameplayVisualPalette.HudPrimarySignal,
                Is.EqualTo(GameplayVisualPalette.SignalBlue));
            Assert.That(GameplayVisualPalette.HudSecondarySignal,
                Is.EqualTo(GameplayVisualPalette.SignalOrangeGlow));
            Assert.That(GameplayVisualPalette.HudTextPrimary,
                Is.EqualTo(GameplayVisualPalette.TextPrimary));
        }

        [Test]
        public void TargetingUsesBlueForValidAndOrangeForInvalid()
        {
            Assert.That(
                GameplayVisualPalette.TargetingValid,
                Is.EqualTo(GameplayVisualPalette.SignalBlueBright));
            Assert.That(
                GameplayVisualPalette.TargetingInvalid,
                Is.EqualTo(GameplayVisualPalette.SignalOrangeGlow));
            Assert.That(
                GameplayVisualPalette.DisplacementPreview,
                Is.EqualTo(GameplayVisualPalette.TargetingValid));
            Assert.That(
                GameplayVisualPalette.DisplacementPreviewInvalid,
                Is.EqualTo(GameplayVisualPalette.TargetingInvalid));
        }

        private static void AssertBlueLeaning(Color color)
        {
            Assert.That(color.b, Is.GreaterThan(color.g));
            Assert.That(color.g, Is.GreaterThan(color.r));
        }
    }
}
