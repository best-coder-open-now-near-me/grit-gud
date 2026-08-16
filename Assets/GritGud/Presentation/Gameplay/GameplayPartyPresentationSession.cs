using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayPartyPresentationSession : IDisposable
    {
        private sealed class ActorPresentation
        {
            public ActorPresentation(
                GameplayActorView view,
                ActorAnimationCoordinator animationCoordinator,
                GameplayWeaponPresenter weapon)
            {
                View = view;
                AnimationCoordinator = animationCoordinator;
                Weapon = weapon;
            }

            public GameplayActorView View { get; }

            public ActorAnimationCoordinator AnimationCoordinator { get; }

            public GameplayWeaponPresenter Weapon { get; }
        }

        private readonly Dictionary<string, ActorPresentation> actors =
            new Dictionary<string, ActorPresentation>(StringComparer.Ordinal);
        private readonly GameplaySession session;
        private readonly TargetAcquisitionPresenter targetAcquisition;
        private bool disposed;

        public GameplayPartyPresentationSession(
            GameplaySession session,
            PlayerPartyDefinition party,
            GameplayWorldRegistry registry,
            GameplayAttackController attackController,
            GameplayProjectileController projectileController,
            TargetAcquisitionPresenter acquisition,
            WeaponPresentationCatalog weaponCatalog = null)
        {
            this.session = session
                ?? throw new ArgumentNullException(nameof(session));
            if (party == null)
                throw new ArgumentNullException(nameof(party));
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (attackController == null)
                throw new ArgumentNullException(nameof(attackController));
            if (projectileController == null)
                throw new ArgumentNullException(nameof(projectileController));
            targetAcquisition = acquisition ??
                throw new ArgumentNullException(nameof(acquisition));

            WeaponPresentationCatalog catalog = weaponCatalog
                ?? WeaponPresentationCatalog.LoadDefault();
            try
            {
                foreach (string actorId in party.ActorIds)
                {
                    GameplayActorView view = registry.GetActor(actorId);
                    if (view.Motor == null || view.MovementInput == null)
                    {
                        throw new InvalidOperationException(
                            $"Party actor '{actorId}' requires movement components.");
                    }

                    ActorAnimationCoordinator animationCoordinator = view.Root
                        .GetComponent<ActorAnimationCoordinator>();
                    if (animationCoordinator == null)
                    {
                        throw new InvalidOperationException(
                            $"Party actor '{actorId}' requires an animation coordinator.");
                    }

                    GameplayWeaponPresenter weapon = view.Root
                        .GetComponent<GameplayWeaponPresenter>()
                        ?? view.Root.AddComponent<GameplayWeaponPresenter>();
                    try
                    {
                        weapon.Bind(
                            session,
                            registry,
                            attackController,
                            projectileController,
                            animationCoordinator,
                            actorId,
                            catalog,
                            targetAcquisition: null,
                            presentAsLocalPlayer: false);
                        view.MovementInput.SetInputEnabled(false);
                        actors.Add(
                            actorId,
                            new ActorPresentation(
                                view,
                                animationCoordinator,
                                weapon));
                        view.Wounds.PresentAuthoritative(
                            session.GetActor(actorId).Wounds);
                    }
                    catch
                    {
                        weapon.Unbind();
                        throw;
                    }
                }

                session.ActorCapabilityChanged += HandleActorCapabilityChanged;
                SetSelectedActor(party.InitiallySelectedActorId);
            }
            catch
            {
                this.session.ActorCapabilityChanged -=
                    HandleActorCapabilityChanged;
                CleanupActors();
                throw;
            }
        }

        public string SelectedActorId { get; private set; }

        public GameplayActorView SelectedView =>
            RequireSelected().View;

        public ActorAnimationCoordinator SelectedAnimationCoordinator =>
            RequireSelected().AnimationCoordinator;

        public GameplayWeaponPresenter SelectedWeapon =>
            RequireSelected().Weapon;

        public void SetSelectedActor(string actorId)
        {
            ThrowIfDisposed();
            if (!actors.TryGetValue(
                    actorId ?? string.Empty,
                    out ActorPresentation selected))
            {
                throw new ArgumentException(
                    $"Actor '{actorId}' is not part of the presented party.",
                    nameof(actorId));
            }

            SelectedActorId = actorId;
            foreach (KeyValuePair<string, ActorPresentation> actor in actors)
            {
                bool isSelected = string.Equals(
                    actor.Key,
                    SelectedActorId,
                    StringComparison.Ordinal);
                actor.Value.Weapon.SetLocalControl(
                    isSelected,
                    isSelected ? targetAcquisition : null);
                if (!isSelected)
                    actor.Value.View.MovementInput.SetInputEnabled(false);
            }

            selected.View.MovementInput.SetInputEnabled(false);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            session.ActorCapabilityChanged -= HandleActorCapabilityChanged;
            CleanupActors();
            SelectedActorId = null;
            disposed = true;
        }

        private void CleanupActors()
        {
            foreach (ActorPresentation actor in actors.Values)
            {
                actor.View.MovementInput.SetInputEnabled(false);
                actor.Weapon.Unbind();
            }

            actors.Clear();
        }

        private void HandleActorCapabilityChanged(string actorId)
        {
            if (actors.TryGetValue(
                    actorId ?? string.Empty,
                    out ActorPresentation actor))
            {
                actor.View.Wounds.PresentAuthoritative(
                    session.GetActor(actorId).Wounds);
            }
        }

        private ActorPresentation RequireSelected()
        {
            ThrowIfDisposed();
            if (SelectedActorId == null
                || !actors.TryGetValue(
                    SelectedActorId,
                    out ActorPresentation selected))
            {
                throw new InvalidOperationException(
                    "The party presentation has no selected actor.");
            }

            return selected;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(GameplayPartyPresentationSession));
        }
    }
}
