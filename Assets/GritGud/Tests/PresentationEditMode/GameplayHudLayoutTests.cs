using NUnit.Framework;
using GritGud.Presentation.Gameplay;
using UnityEngine;

namespace GritGud.Tests.PresentationEditMode
{
    public sealed class GameplayHudLayoutTests
    {
        [Test]
        public void HitTestingUsesTheSameScaledCanvasAsRendering()
        {
            const float screenWidth = 1920f;
            const float screenHeight = 1080f;
            float scale = GameplayHudLayout.CalculateUiScale(screenHeight);
            float canvasWidth = screenWidth / scale;
            float canvasHeight = screenHeight / scale;
            Rect commandBar = GameplayHudLayout.CalculateCommandBarRectangle(
                canvasWidth,
                canvasHeight);

            Assert.That(
                Contains(
                    ToScreenPoint(commandBar.center, screenHeight, scale),
                    screenWidth,
                    screenHeight),
                Is.True);
            Assert.That(
                Contains(
                    ToScreenPoint(
                        new Vector2(canvasWidth * 0.5f, canvasHeight * 0.4f),
                        screenHeight,
                        scale),
                    screenWidth,
                    screenHeight),
                Is.False);
        }

        [Test]
        public void HitTestingIncludesTransientMenusAndModalCapture()
        {
            const float screenWidth = 1600f;
            const float screenHeight = 900f;
            const float scale = 1f;
            var choice = new Rect(500f, 300f, 100f, 80f);
            var actorFlyout = new Rect(720f, 250f, 120f, 90f);

            Assert.That(
                GameplayHudLayout.ContainsInteractiveScreenPoint(
                    ToScreenPoint(choice.center, screenHeight, scale),
                    screenWidth,
                    screenHeight,
                    modalOpen: false,
                    hotbarChoiceOpen: true,
                    hotbarChoiceRectangle: choice,
                    actorAbilityFlyoutReveal: 0f,
                    actorAbilityFlyoutRectangle: actorFlyout,
                    flyoutExpanded: false),
                Is.True);
            Assert.That(
                GameplayHudLayout.ContainsInteractiveScreenPoint(
                    ToScreenPoint(actorFlyout.center, screenHeight, scale),
                    screenWidth,
                    screenHeight,
                    modalOpen: false,
                    hotbarChoiceOpen: false,
                    hotbarChoiceRectangle: choice,
                    actorAbilityFlyoutReveal: 0.5f,
                    actorAbilityFlyoutRectangle: actorFlyout,
                    flyoutExpanded: false),
                Is.True);
            Assert.That(
                GameplayHudLayout.ContainsInteractiveScreenPoint(
                    new Vector2(screenWidth * 0.5f, screenHeight * 0.5f),
                    screenWidth,
                    screenHeight,
                    modalOpen: true,
                    hotbarChoiceOpen: false,
                    hotbarChoiceRectangle: choice,
                    actorAbilityFlyoutReveal: 0f,
                    actorAbilityFlyoutRectangle: actorFlyout,
                    flyoutExpanded: false),
                Is.True);
        }

        private static bool Contains(
            Vector2 point,
            float width,
            float height) =>
            GameplayHudLayout.ContainsInteractiveScreenPoint(
                point,
                width,
                height,
                modalOpen: false,
                hotbarChoiceOpen: false,
                hotbarChoiceRectangle: default,
                actorAbilityFlyoutReveal: 0f,
                actorAbilityFlyoutRectangle: default,
                flyoutExpanded: false);

        private static Vector2 ToScreenPoint(
            Vector2 guiPoint,
            float screenHeight,
            float scale) =>
            new Vector2(
                guiPoint.x * scale,
                screenHeight - (guiPoint.y * scale));
    }
}
