using GritGud.Presentation.LevelEditing.Core;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorInputRouterTests
    {
        [Test]
        public void SelectPointer_PrefersActiveTouchOverMouse()
        {
            var touch = new LevelEditorPointerState(
                new Vector2(20f, 30f), new Vector2(2f, 3f), true, true, false);
            var mouse = new LevelEditorPointerState(
                new Vector2(100f, 200f), Vector2.zero, false, false, false);

            LevelEditorPointerState result =
                LevelEditorInputRouter.SelectPointer(true, touch, mouse);

            Assert.That(result.Position, Is.EqualTo(touch.Position));
            Assert.That(result.Delta, Is.EqualTo(touch.Delta));
            Assert.That(result.Pressed, Is.True);
            Assert.That(result.Held, Is.True);
        }

        [Test]
        public void SelectPointer_PreservesTouchReleaseFrame()
        {
            var touch = new LevelEditorPointerState(
                new Vector2(40f, 50f), Vector2.zero, false, false, true);

            LevelEditorPointerState result =
                LevelEditorInputRouter.SelectPointer(true, touch, default);

            Assert.That(result.Position, Is.EqualTo(touch.Position));
            Assert.That(result.Released, Is.True);
        }

        [Test]
        public void SelectPointer_UsesMouseWhenTouchIsInactive()
        {
            var mouse = new LevelEditorPointerState(
                new Vector2(60f, 70f), new Vector2(6f, 7f), true, true, false);

            LevelEditorPointerState result =
                LevelEditorInputRouter.SelectPointer(false, default, mouse);

            Assert.That(result.Position, Is.EqualTo(mouse.Position));
            Assert.That(result.Pressed, Is.True);
        }
    }
}
