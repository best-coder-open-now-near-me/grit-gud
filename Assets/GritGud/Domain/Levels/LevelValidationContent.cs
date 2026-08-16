using System;
using System.Collections.Generic;
using System.Linq;

namespace GritGud.Domain.Levels
{
    public sealed class LevelValidationContent
    {
        private readonly Dictionary<string, string> actorPresentationsByTemplateId;

        public LevelValidationContent(
            IEnumerable<string> knownArchetypeIds = null,
            IEnumerable<KeyValuePair<string, string>> actorPresentationsByTemplateId = null,
            IEnumerable<string> knownActorPresentationIds = null,
            IEnumerable<string> knownCharacterIds = null)
        {
            KnownArchetypeIds = CopyIds(knownArchetypeIds);
            KnownActorPresentationIds = CopyIds(knownActorPresentationIds);
            KnownCharacterIds = CopyIds(knownCharacterIds);
            HasActorTemplateCatalog = actorPresentationsByTemplateId != null;
            this.actorPresentationsByTemplateId = actorPresentationsByTemplateId?
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value ?? string.Empty,
                    StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public ISet<string> KnownArchetypeIds { get; }

        public ISet<string> KnownActorPresentationIds { get; }

        public ISet<string> KnownCharacterIds { get; }

        public bool HasActorTemplateCatalog { get; }

        public bool HasActorPresentationCatalog => KnownActorPresentationIds != null;

        public bool HasCharacterCatalog => KnownCharacterIds != null;

        public bool TryGetActorPresentationId(
            string templateId,
            out string presentationId)
        {
            return actorPresentationsByTemplateId.TryGetValue(
                templateId ?? string.Empty,
                out presentationId);
        }

        private static ISet<string> CopyIds(IEnumerable<string> source)
        {
            return source == null
                ? null
                : new HashSet<string>(
                    source.Where(value => !string.IsNullOrWhiteSpace(value)),
                    StringComparer.Ordinal);
        }
    }
}
