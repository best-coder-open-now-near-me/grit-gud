using System.Collections;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayBootstrapTeardownLifecycleTests
    {
        [UnityTest]
        public IEnumerator ReturningToMenuUnbindsGameplayPresentationExactly()
        {
            using var runtime = new GameplayRuntimeTestHarness();
            yield return runtime.Start();

            GameplayController gameplay = runtime.Gameplay;
            GameplayInputController inputController = runtime.InputController;
            GameplayHud hud = runtime.Hud;
            GameplaySessionPresenter sessionPresenter = runtime.SessionPresenter;
            TurnMovementController turnMovement = runtime.TurnMovement;
            GameplayActionController actions = runtime.Actions;
            GameplayAttackController attacks = runtime.Attacks;
            GameplayObjectivePresenter objectivePresenter =
                runtime.ObjectivePresenter;

            runtime.Bootstrap.ReturnToMenu();

            Assert.That(runtime.Bootstrap.CurrentMode,
                Is.EqualTo(ApplicationMode.Menu));
            Assert.That(gameplay.IsRunning, Is.False);
            Assert.That(RenderSettings.fog, Is.EqualTo(runtime.OriginalFog));
            Assert.That(GameObject.Find("Gameplay Environment Lighting"), Is.Null);
            Assert.That(GameObject.Find("Gameplay Post Processing"), Is.Null);
            Assert.That(inputController.IsActive, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            Assert.That(hud.IsVisible, Is.False);
            Assert.That(hud.IsFlyoutExpanded, Is.False);
            Assert.That(hud.Session, Is.Null);
            Assert.That(hud.IsCommandBarVisible, Is.False);
            Assert.That(sessionPresenter.Session, Is.Null);
            Assert.That(turnMovement.Session, Is.Null);
            Assert.That(hud.TurnMovement, Is.Null);
            Assert.That(actions.Session, Is.Null);
            Assert.That(attacks.Session, Is.Null);
            Assert.That(hud.ActionController, Is.Null);
            Assert.That(hud.AttackController, Is.Null);
            Assert.That(objectivePresenter.Session, Is.Null);
            Assert.That(objectivePresenter.IsPresented, Is.False);
            Assert.That(runtime.SceneCamera.gameObject.activeSelf, Is.True);
            Assert.That(
                Shader.GetGlobalVector("_GritGudPlayerCutout"),
                Is.EqualTo(Vector4.zero));
            Assert.That(
                Shader.GetGlobalFloat("_GritGudPlayerCutoutLeftExtension"),
                Is.Zero);
            Assert.That(
                Shader.GetGlobalFloat("_GritGudPlayerCutoutVerticalRadius"),
                Is.Zero);
            Assert.That(
                Shader.GetGlobalVector("_GritGudPlayerCutoutRayStart"),
                Is.EqualTo(Vector4.zero));
            Assert.That(
                Shader.GetGlobalVector("_GritGudPlayerCutoutRayEnd"),
                Is.EqualTo(Vector4.zero));
            Assert.That(
                Shader.GetGlobalVector("_GritGudPlayerCutoutCameraRight"),
                Is.EqualTo(Vector4.zero));
            Assert.That(
                Shader.GetGlobalVector("_GritGudPlayerCutoutCameraUp"),
                Is.EqualTo(Vector4.zero));
            Assert.That(
                Shader.GetGlobalVector(
                    "_GritGudPlayerCutoutCorridorWidths"),
                Is.EqualTo(Vector4.zero));
        }
    }
}
