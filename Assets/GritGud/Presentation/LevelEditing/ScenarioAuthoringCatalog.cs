using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class ScenarioActorTemplateDefinition
    {
        public ScenarioActorTemplateDefinition(
            string templateId,
            string displayName,
            bool playerTemplate)
        {
            TemplateId = string.IsNullOrWhiteSpace(templateId)
                ? throw new ArgumentException("An actor template ID is required.", nameof(templateId))
                : templateId;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? templateId
                : displayName;
            PlayerTemplate = playerTemplate;
        }

        public string TemplateId { get; }

        public string DisplayName { get; }

        public bool PlayerTemplate { get; }
    }

    public sealed class ScenarioAuthoringCatalog
    {
        internal const string DefaultTemplateResource = "Scenarios/depot-yard";

        private readonly Dictionary<string, ScenarioActorTemplateDefinition> actors;

        private ScenarioAuthoringCatalog(
            IEnumerable<ScenarioActorTemplateDefinition> actorTemplates)
        {
            actors = actorTemplates.ToDictionary(
                actor => actor.TemplateId,
                StringComparer.Ordinal);
            ActorTemplates = actors.Values
                .OrderByDescending(actor => actor.PlayerTemplate)
                .ThenBy(actor => actor.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<ScenarioActorTemplateDefinition> ActorTemplates { get; }

        public static ScenarioAuthoringCatalog LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(DefaultTemplateResource);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Scenario actor templates '{DefaultTemplateResource}' were not found.");
            }

            ScenarioContentDocument document =
                JsonUtility.FromJson<ScenarioContentDocument>(asset.text)
                ?? throw new InvalidOperationException(
                    $"Scenario actor templates '{DefaultTemplateResource}' are invalid JSON.");
            document.Normalize();
            var playerTemplateIds = new HashSet<string>(
                document.playerParty.actorIds,
                StringComparer.Ordinal);
            return new ScenarioAuthoringCatalog(document.actors
                .Where(actor => actor != null && !string.IsNullOrWhiteSpace(actor.id))
                .Select(actor => new ScenarioActorTemplateDefinition(
                    actor.id,
                    actor.displayName,
                    playerTemplateIds.Contains(actor.id))));
        }

        public ScenarioActorTemplateDefinition GetActor(string templateId)
        {
            if (!actors.TryGetValue(templateId ?? string.Empty, out var actor))
            {
                throw new KeyNotFoundException(
                    $"Scenario actor template '{templateId}' is not defined.");
            }

            return actor;
        }

        public bool ContainsActor(string templateId)
        {
            return actors.ContainsKey(templateId ?? string.Empty);
        }
    }
}
