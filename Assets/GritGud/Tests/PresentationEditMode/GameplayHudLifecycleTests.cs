using System.Reflection;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayHudLifecycleTests
    {
        [Test]
        public void HideAndShowCloseHotbarChoiceState()
        {
            var root = new GameObject("HUD lifecycle test");
            try
            {
                GameplayHud hud = root.AddComponent<GameplayHud>();
                var state = (GameplayHotbarChoiceState)typeof(GameplayHud)
                    .GetField("hotbarChoice", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(hud);
                state.Open(3, new Rect(100f, 100f, 50f, 50f), 120f);
                Assert.That(hud.IsHotbarChoiceOpen, Is.True);

                hud.Hide();
                Assert.That(hud.IsHotbarChoiceOpen, Is.False);

                state.Open(4, new Rect(100f, 100f, 50f, 50f), 120f);
                hud.Show();
                Assert.That(hud.IsHotbarChoiceOpen, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GameplayControllerProvisioningAndTeardownAreIdempotent()
        {
            var root = new GameObject("Gameplay composition test");
            try
            {
                GameplayController controller = root.AddComponent<GameplayController>();
                typeof(GameplayController)
                    .GetMethod(
                        "EnsureDependencies",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                Assert.That(root.GetComponent<GameplayInputController>(), Is.Not.Null);
                Assert.That(root.GetComponent<GameplayHud>(), Is.Not.Null);
                Assert.That(
                    System.Array.ConvertAll(
                        root.GetComponents<MonoBehaviour>(),
                        component => component.GetType().Name),
                    Does.Not.Contain("GameplayAdvancementHud"));
                Assert.That(root.GetComponent<GameplaySmokeFieldController>(), Is.Not.Null);
                Assert.DoesNotThrow(controller.EndSession);
                Assert.DoesNotThrow(controller.EndSession);
                Assert.That(root.GetComponent<GameplayHud>().IsVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HotbarChoiceStateClampsAndClosesWithoutRetainingSessionData()
        {
            var state = new GameplayHotbarChoiceState();

            state.Open(7, new Rect(900f, 700f, 50f, 50f), 300f);
            state.ClampToCanvas(800f, 600f);

            Assert.That(state.IsOpen, Is.True);
            Assert.That(state.SlotNumber, Is.EqualTo(7));
            Assert.That(state.Rectangle.xMax, Is.LessThanOrEqualTo(790f));
            Assert.That(state.Rectangle.yMax, Is.LessThanOrEqualTo(590f));

            state.Close();

            Assert.That(state.IsOpen, Is.False);
            Assert.That(state.SlotNumber, Is.Zero);
            Assert.That(state.Rectangle, Is.EqualTo(default(Rect)));
        }

        [Test]
        public void TextureSetDisposesEveryOwnedTexture()
        {
            var textures = new GameplayHudTextureSet();
            Texture2D normal = textures.ButtonNormal;
            Texture2D hover = textures.ButtonHover;
            Texture2D active = textures.ButtonActive;
            Texture2D confirmation = textures.EquipmentConfirmation;

            textures.Dispose();

            Assert.That(normal == null, Is.True);
            Assert.That(hover == null, Is.True);
            Assert.That(active == null, Is.True);
            Assert.That(confirmation == null, Is.True);
            Assert.That(textures.ButtonNormal, Is.Null);
            Assert.That(textures.EquipmentConfirmation, Is.Null);
        }
    }
}
