using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal static class GameplayActorFactory
    {
        public static GameObject CreateActor(
            ScenarioActorRuntimeDefinition actor,
            ActorPresentationCatalog catalog)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            ActorPresentationDefinition presentation = catalog.Get(
                actor.PresentationId);
            GameObject instance = UnityEngine.Object.Instantiate(
                presentation.Prefab);
            try
            {
                ConfigureRequiredComponents(instance, presentation);
            }
            catch
            {
                GameplayObjectLifecycle.Destroy(instance);
                throw;
            }

            instance.name = actor.DisplayName;
            GameplayActorPose pose = actor.GameplayDefinition.StartingPose;
            instance.transform.SetPositionAndRotation(
                new Vector3(
                    pose.Position.X,
                    pose.Position.Y,
                    pose.Position.Z),
                Quaternion.Euler(0f, pose.FacingDegrees, 0f));
            return instance;
        }

        private static void ConfigureRequiredComponents(
            GameObject instance,
            ActorPresentationDefinition presentation)
        {
            ThirdPersonMotor motor = instance.GetComponent<ThirdPersonMotor>();
            ExplorationMovementInput input =
                instance.GetComponent<ExplorationMovementInput>();
            ActorStancePresenter stance =
                instance.GetComponent<ActorStancePresenter>();
            ActorCelShadingPresenter celShading =
                instance.GetComponent<ActorCelShadingPresenter>();
            if (motor == null || input == null || stance == null ||
                celShading == null)
            {
                throw new InvalidOperationException(
                    $"Actor prefab '{presentation.Prefab.name}' must contain "
                    + $"{nameof(ThirdPersonMotor)}, "
                    + $"{nameof(ExplorationMovementInput)}, "
                    + $"{nameof(ActorStancePresenter)}, and "
                    + $"{nameof(ActorCelShadingPresenter)} components.");
            }

            input.SetInputEnabled(presentation.MovementInputEnabled);
            celShading.Apply();
        }
    }
}
