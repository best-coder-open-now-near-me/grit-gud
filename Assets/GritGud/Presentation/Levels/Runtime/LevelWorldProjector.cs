using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Levels.Runtime
{
    public sealed class LevelWorldProjector : IDisposable
    {
        private readonly LevelLoader loader;
        private readonly LevelArchetypeCatalog catalog;
        private readonly Transform parent;

        public LevelWorldProjector(LevelArchetypeCatalog catalog, Transform parent)
        {
            this.catalog = catalog != null
                ? catalog
                : throw new ArgumentNullException(nameof(catalog));
            loader = new LevelLoader(catalog);
            this.parent = parent;
        }

        public LevelWorld World { get; private set; }

        public void Replace(LevelDocument document)
        {
            LevelWorld replacement = loader.Load(document, parent);
            World?.Dispose();
            World = replacement;
        }

        public void Apply(LevelDocument document, LevelSessionChangedEventArgs change)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (change == null || change.RequiresFullProjection || World == null)
            {
                Replace(document);
                return;
            }

            IReadOnlyList<LevelValidationIssue> issues = LevelValidator.Validate(
                document,
                catalog.CreateKnownIdSet(),
                LevelValidationProfile.Runtime);
            if (LevelValidator.HasErrors(issues))
            {
                throw new LevelLoadException("The changed level failed validation and was not projected.", issues);
            }

            var entitiesById = document.entities
                .Where(entity => entity != null)
                .ToDictionary(entity => entity.id, StringComparer.Ordinal);
            foreach (string entityId in change.AffectedEntityIds)
            {
                if (entitiesById.TryGetValue(entityId, out LevelEntity entity))
                {
                    loader.ProjectEntity(World, entity);
                }
                else
                {
                    loader.RemoveEntity(World, entityId);
                }
            }
        }

        public bool TryGetEntity(string entityId, out LevelEntityView view)
        {
            if (World == null)
            {
                view = null;
                return false;
            }

            return World.TryGetEntity(entityId, out view);
        }

        public void SetVisible(bool visible)
        {
            if (World?.Root != null)
            {
                World.Root.SetActive(visible);
            }
        }

        public void Dispose()
        {
            World?.Dispose();
            World = null;
        }
    }
}
