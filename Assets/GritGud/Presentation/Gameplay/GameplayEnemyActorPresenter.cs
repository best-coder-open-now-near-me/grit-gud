using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnemyActorPresenter : IDisposable
    {
        private readonly GameplayWeaponPresenter weaponPresenter;
        private bool disposed;

        public GameplayEnemyActorPresenter(
            GameplaySession session,
            GameplayWorldRegistry registry,
            GameplayAttackController attackController,
            GameplayProjectileController projectileController,
            ScenarioActorDefinition definition,
            GameplayActorView view,
            EnemyPresentationDefinition presentationDefinition)
        {
            Definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            View = view ?? throw new ArgumentNullException(nameof(view));
            PresentationDefinition = presentationDefinition
                ?? throw new ArgumentNullException(
                    nameof(presentationDefinition));
            if (view.Motor == null)
                throw new InvalidOperationException(
                    $"Enemy '{definition.Id}' requires "
                    + $"{nameof(ThirdPersonMotor)}.");
            ActorAnimationCoordinator animationCoordinator =
                view.Root.GetComponent<ActorAnimationCoordinator>();
            if (animationCoordinator == null)
                throw new InvalidOperationException(
                    $"Enemy '{definition.Id}' requires "
                    + $"{nameof(ActorAnimationCoordinator)}.");

            weaponPresenter =
                view.Root.AddComponent<GameplayWeaponPresenter>();
            weaponPresenter.Bind(
                session,
                registry,
                attackController,
                projectileController,
                animationCoordinator,
                definition.Id,
                targetAcquisition: null,
                presentAsLocalPlayer: false);
            view.MovementInput?.SetInputEnabled(false);
            Playback = new MovementRoutePlaybackPresenter(view.Motor);
        }

        public ScenarioActorDefinition Definition { get; }

        public GameplayActorView View { get; }

        public EnemyPresentationDefinition PresentationDefinition { get; }

        public MovementRoutePlaybackPresenter Playback { get; }

        public bool PresentIncapacitation()
        {
            if (IncapacitationPresented)
                return false;
            IncapacitationPresented = true;
            ActorAnimationCoordinator animationCoordinator = View.Root
                .GetComponent<ActorAnimationCoordinator>();
            animationCoordinator?.PresentIncapacitation(
                PresentationDefinition.IncapacitationLocalRotation,
                PresentationDefinition.IncapacitationLocalOffset);
            if (animationCoordinator?.LastRequestedAction ==
                    ActorAnimationAction.Incapacitate ||
                animationCoordinator?.LastRequestedAction ==
                    ActorAnimationAction.IncapacitateShoulder)
            {
                View.Root.GetComponent<ActorRagdollPresenter>()
                    ?.ArmIncapacitation(
                        journalSequence: 0L,
                        hitRegion: null,
                        impulseDirection: View.Transform.forward);
            }
            return true;
        }

        public bool IncapacitationPresented { get; private set; }

        public void Dispose()
        {
            if (disposed)
                return;
            Playback.Cancel();
            weaponPresenter?.Unbind();
            disposed = true;
        }
    }
}
