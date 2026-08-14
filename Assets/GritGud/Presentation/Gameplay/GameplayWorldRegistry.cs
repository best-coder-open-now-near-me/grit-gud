using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayActorView
    {
        public GameplayActorView(
            string actorId,
            string presentationId,
            bool targetable,
            GameObject root)
        {
            ActorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException(
                    "Actor view identifiers cannot be empty.",
                    nameof(actorId))
                : actorId;
            PresentationId = presentationId ?? string.Empty;
            Targetable = targetable;
            Root = root != null
                ? root
                : throw new ArgumentNullException(nameof(root));
            Stance = root.GetComponent<ActorStancePresenter>() ??
                throw new InvalidOperationException(
                    $"Actor '{actorId}' requires {nameof(ActorStancePresenter)}.");
            Motor = root.GetComponent<ThirdPersonMotor>();
            MovementInput = root.GetComponent<ExplorationMovementInput>();
        }

        public string ActorId { get; }

        public string PresentationId { get; }

        public bool Targetable { get; }

        public GameObject Root { get; }

        public Transform Transform => Root.transform;

        public ActorStancePresenter Stance { get; }

        public ThirdPersonMotor Motor { get; }

        public ExplorationMovementInput MovementInput { get; }
    }

    internal sealed class GameplayWorldRegistry : IDisposable
    {
        private readonly Dictionary<string, GameplayActorView> actors =
            new Dictionary<string, GameplayActorView>(StringComparer.Ordinal);
        private readonly Dictionary<Transform, GameplayActorView> actorRoots =
            new Dictionary<Transform, GameplayActorView>();
        private readonly LevelWorld levelWorld;
        private bool disposed;

        public GameplayWorldRegistry(LevelWorld world)
        {
            levelWorld = world ?? throw new ArgumentNullException(nameof(world));
        }

        public IReadOnlyCollection<GameplayActorView> Actors => actors.Values;

        public IEnumerable<LevelEntityView> LevelEntities =>
            levelWorld.Entities.Values;

        public void RegisterActor(
            ScenarioActorRuntimeDefinition actor,
            GameObject actorRoot)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            RegisterActor(
                actor.Id,
                actor.PresentationId,
                actor.Targetable,
                actorRoot);
        }

        public void RegisterActor(
            string actorId,
            string presentationId,
            bool targetable,
            GameObject actorRoot)
        {
            ThrowIfDisposed();
            var view = new GameplayActorView(
                actorId,
                presentationId,
                targetable,
                actorRoot);
            if (actorRoots.ContainsKey(view.Transform))
            {
                throw new InvalidOperationException(
                    $"Actor root '{view.Root.name}' is already registered.");
            }

            if (!actors.TryAdd(view.ActorId, view))
            {
                throw new InvalidOperationException(
                    $"Actor view '{view.ActorId}' is registered more than once.");
            }

            actorRoots.Add(view.Transform, view);
        }

        public GameplayActorView GetActor(string actorId)
        {
            ThrowIfDisposed();
            if (!actors.TryGetValue(actorId ?? string.Empty, out var actor))
            {
                throw new KeyNotFoundException(
                    $"Actor view '{actorId}' is not registered.");
            }

            return actor;
        }

        public bool TryGetActor(
            string actorId,
            out GameplayActorView actor)
        {
            ThrowIfDisposed();
            return actors.TryGetValue(actorId ?? string.Empty, out actor);
        }

        public bool TryGetActorContaining(
            Transform candidate,
            out GameplayActorView actor)
        {
            ThrowIfDisposed();
            while (candidate != null)
            {
                if (actorRoots.TryGetValue(candidate, out actor))
                {
                    return true;
                }

                candidate = candidate.parent;
            }

            actor = null;
            return false;
        }

        public LevelEntityView GetLevelEntity(string entityId)
        {
            ThrowIfDisposed();
            if (!levelWorld.TryGetEntity(entityId, out LevelEntityView entity))
            {
                throw new KeyNotFoundException(
                    $"Level entity view '{entityId}' is not registered.");
            }

            return entity;
        }

        public bool TryGetLevelEntity(
            string entityId,
            out LevelEntityView entity)
        {
            ThrowIfDisposed();
            return levelWorld.TryGetEntity(entityId ?? string.Empty, out entity);
        }

        public bool TryGetLevelEntityContaining(
            Transform candidate,
            out LevelEntityView entity)
        {
            ThrowIfDisposed();
            while (candidate != null)
            {
                LevelEntityView view = candidate.GetComponent<LevelEntityView>();
                if (view != null
                    && levelWorld.TryGetEntity(
                        view.EntityId,
                        out LevelEntityView registered)
                    && ReferenceEquals(view, registered))
                {
                    entity = view;
                    return true;
                }

                candidate = candidate.parent;
            }

            entity = null;
            return false;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            foreach (GameplayActorView actor in actors.Values)
            {
                GameplayObjectLifecycle.Destroy(actor.Root);
            }

            actors.Clear();
            actorRoots.Clear();
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(GameplayWorldRegistry));
            }
        }
    }
}
