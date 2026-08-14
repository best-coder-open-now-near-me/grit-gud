using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Levels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Levels.Runtime
{
    public sealed class LevelLoadException : Exception
    {
        public LevelLoadException(string message, IReadOnlyList<LevelValidationIssue> issues)
            : base(message)
        {
            Issues = issues ?? Array.Empty<LevelValidationIssue>();
        }

        public LevelLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
            Issues = Array.Empty<LevelValidationIssue>();
        }

        public IReadOnlyList<LevelValidationIssue> Issues { get; }
    }

    public sealed class LevelWorld : IDisposable
    {
        private readonly Dictionary<string, LevelEntityView> entities;
        private readonly TerrainWorldProjector terrainProjector;

        internal LevelWorld(
            GameObject root,
            Dictionary<string, LevelEntityView> entities,
            TerrainWorldProjector terrainProjector)
        {
            Root = root;
            this.entities = entities;
            this.terrainProjector = terrainProjector;
        }

        public GameObject Root { get; private set; }

        public IReadOnlyDictionary<string, LevelEntityView> Entities => entities;

        public bool TryGetEntity(string entityId, out LevelEntityView view)
        {
            return entities.TryGetValue(entityId ?? string.Empty, out view);
        }

        internal void SetEntity(string entityId, LevelEntityView view)
        {
            entities[entityId] = view;
        }

        internal bool RemoveEntity(string entityId, out LevelEntityView view)
        {
            if (!entities.TryGetValue(entityId ?? string.Empty, out view))
            {
                return false;
            }

            entities.Remove(entityId);
            return true;
        }

        public void Dispose()
        {
            if (Root == null)
            {
                return;
            }

            Root.SetActive(false);
            terrainProjector?.Dispose();
            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(Root);
            }
            else
            {
                Object.DestroyImmediate(Root);
            }

            Root = null;
            entities.Clear();
        }
    }

    public sealed class LevelLoader
    {
        private readonly LevelArchetypeCatalog catalog;

        public LevelLoader(LevelArchetypeCatalog catalog)
        {
            this.catalog = catalog != null
                ? catalog
                : throw new ArgumentNullException(nameof(catalog));
        }

        public LevelWorld Load(LevelDocument source, Transform parent = null)
        {
            LevelDocument document = source?.DeepCopy()
                ?? throw new ArgumentNullException(nameof(source));
            IReadOnlyList<LevelValidationIssue> issues = LevelValidator.Validate(
                document,
                catalog.CreateKnownIdSet(),
                LevelValidationProfile.Runtime);
            if (LevelValidator.HasErrors(issues))
            {
                string details = string.Join(
                    " ",
                    issues
                        .Where(issue => issue.Severity == LevelValidationSeverity.Error)
                        .Take(4)
                        .Select(issue => issue.Message));
                throw new LevelLoadException(
                    $"The level failed validation and was not loaded. {details}",
                    issues);
            }

            var stagingRoot = new GameObject($"Level - {document.displayName}");
            stagingRoot.SetActive(false);
            if (parent != null)
            {
                stagingRoot.transform.SetParent(parent, false);
            }

            var views = new Dictionary<string, LevelEntityView>(StringComparer.Ordinal);
            TerrainWorldProjector terrainProjector = null;
            try
            {
                foreach (LevelEntity entity in document.entities)
                {
                    if (!catalog.TryGet(entity.archetypeId, out LevelArchetypeDefinition definition))
                    {
                        throw new InvalidOperationException(
                            $"Archetype '{entity.archetypeId}' disappeared after validation.");
                    }

                    if (definition.Presentation.Prefab == null)
                    {
                        throw new InvalidOperationException(
                            $"Archetype '{definition.ArchetypeId}' does not reference a prefab.");
                    }

                    LevelEntityView view = CreateEntityView(entity, definition, stagingRoot.transform);
                    views.Add(entity.id, view);
                }

                terrainProjector = new TerrainWorldProjector(stagingRoot.transform);
                terrainProjector.Replace(document);

                stagingRoot.SetActive(true);
                return new LevelWorld(stagingRoot, views, terrainProjector);
            }
            catch (Exception exception)
            {
                terrainProjector?.Dispose();
                if (UnityEngine.Application.isPlaying)
                {
                    Object.Destroy(stagingRoot);
                }
                else
                {
                    Object.DestroyImmediate(stagingRoot);
                }

                throw new LevelLoadException("The level could not be constructed.", exception);
            }
        }

        public LevelEntityView ProjectEntity(LevelWorld world, LevelEntity entity)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!catalog.TryGet(entity.archetypeId, out LevelArchetypeDefinition definition))
            {
                throw new InvalidOperationException(
                    $"Archetype '{entity.archetypeId}' is not in the active catalog.");
            }

            if (definition.Presentation.Prefab == null)
            {
                throw new InvalidOperationException(
                    $"Archetype '{definition.ArchetypeId}' does not reference a prefab.");
            }

            if (world.TryGetEntity(entity.id, out LevelEntityView current)
                && string.Equals(current.ArchetypeId, entity.archetypeId, StringComparison.Ordinal))
            {
                current.Apply(entity);
                return current;
            }

            RemoveEntity(world, entity.id);
            LevelEntityView replacement = CreateEntityView(entity, definition, world.Root.transform);
            world.SetEntity(entity.id, replacement);
            return replacement;
        }

        public bool RemoveEntity(LevelWorld world, string entityId)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (!world.RemoveEntity(entityId, out LevelEntityView view))
            {
                return false;
            }

            Destroy(view.gameObject);
            return true;
        }

        private static LevelEntityView CreateEntityView(
            LevelEntity entity,
            LevelArchetypeDefinition definition,
            Transform parent)
        {
            var entityRoot = new GameObject();
            entityRoot.transform.SetParent(parent, false);
            var view = entityRoot.AddComponent<LevelEntityView>();
            view.Initialize(entity, definition);
            Object.Instantiate(definition.Presentation.Prefab, entityRoot.transform, false);
            return view;
        }

        private static void Destroy(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(false);
            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
