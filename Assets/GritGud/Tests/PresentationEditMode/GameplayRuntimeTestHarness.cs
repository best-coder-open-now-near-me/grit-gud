using System;
using System.Collections;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.LevelEditing;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    internal sealed class GameplayRuntimeTestHarness : IDisposable
    {
        private GameObject ownedApplication;
        private GameObject ownedCamera;

        public GameBootstrap Bootstrap { get; private set; }

        public Camera SceneCamera { get; private set; }

        public bool OriginalFog { get; private set; }

        public GameplayController Gameplay { get; private set; }

        public GameplayInputController InputController { get; private set; }

        public GameplayHud Hud { get; private set; }

        public GameplayPartyHud PartyHud { get; private set; }

        public GameplayDialogueDrawer DialogueDrawer { get; private set; }

        public GameplaySessionPresenter SessionPresenter { get; private set; }

        public TurnMovementController TurnMovement { get; private set; }

        public GameplayActionController Actions { get; private set; }

        public GameplayAttackController Attacks { get; private set; }

        public GameplayEquipmentController Equipment { get; private set; }

        public GameplayHotbarController Hotbar { get; private set; }

        public GameplayProjectileController Projectiles { get; private set; }

        public GameplayObjectivePresenter ObjectivePresenter { get; private set; }

        public LevelEditorController Editor { get; private set; }

        public IEnumerator Start() => StartSession(watchSimulation: false);

        public IEnumerator StartSimulation() =>
            StartSession(watchSimulation: true);

        private IEnumerator StartSession(bool watchSimulation)
        {
            Bootstrap = GameBootstrap.Instance;
            if (Bootstrap == null)
            {
                ownedApplication = new GameObject("Gameplay Runtime Test");
                Bootstrap = ownedApplication.AddComponent<GameBootstrap>();
            }

            Bootstrap.ReturnToMenu();
            OriginalFog = RenderSettings.fog;
            SceneCamera = Camera.main;
            if (SceneCamera == null)
            {
                ownedCamera = new GameObject("Main Camera");
                ownedCamera.tag = "MainCamera";
                SceneCamera = ownedCamera.AddComponent<Camera>();
            }

            if (watchSimulation)
                Bootstrap.WatchFirstSimulation();
            else
                Bootstrap.PlayMainLevel();
            yield return null;
            if (watchSimulation)
            {
                float deadline = Time.realtimeSinceStartup + 10f;
                while (Bootstrap.IsPreparingSimulation
                    && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
                if (Bootstrap.IsPreparingSimulation)
                    throw new TimeoutException(
                        "Simulation playback did not load within ten seconds.");
            }

            Gameplay = Bootstrap.GetComponent<GameplayController>();
            InputController = Bootstrap.GetComponent<GameplayInputController>();
            Hud = Bootstrap.GetComponent<GameplayHud>();
            PartyHud = Bootstrap.GetComponent<GameplayPartyHud>();
            DialogueDrawer = Bootstrap.GetComponent<GameplayDialogueDrawer>();
            SessionPresenter = Bootstrap.GetComponent<GameplaySessionPresenter>();
            TurnMovement = Bootstrap.GetComponent<TurnMovementController>();
            Actions = Bootstrap.GetComponent<GameplayActionController>();
            Attacks = Bootstrap.GetComponent<GameplayAttackController>();
            Equipment = Bootstrap.GetComponent<GameplayEquipmentController>();
            Hotbar = Bootstrap.GetComponent<GameplayHotbarController>();
            Projectiles = Bootstrap.GetComponent<GameplayProjectileController>();
            ObjectivePresenter = Bootstrap.GetComponent<GameplayObjectivePresenter>();
            Editor = Bootstrap.GetComponent<LevelEditorController>();
        }

        public void Dispose()
        {
            if (Bootstrap != null)
                Bootstrap.ReturnToMenu();
            if (ownedApplication != null)
                UnityEngine.Object.DestroyImmediate(ownedApplication);
            if (ownedCamera != null)
                UnityEngine.Object.DestroyImmediate(ownedCamera);
        }
    }
}
