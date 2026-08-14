using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal static class GameplayGuidanceIds
    {
        public const string VoluntaryEntry = "turn.voluntary.entry";
        public const string VoluntaryActive = "turn.voluntary.active";
        public const string RoutePlanning = "turn.route.planning";
        public const string RoutePlayback = "turn.route.playback";
        public const string EncounterActive = "turn.encounter.active";
        public const string InteractReady = "action.interact.ready";
        public const string ObjectiveCompleted = "objective.raised-deck.completed";
    }

    internal sealed class GameplayGuidanceEntry
    {
        public GameplayGuidanceEntry(
            string id,
            string title,
            string expectedBehavior,
            string rationale,
            string playerTip)
        {
            Id = RequireText(id, nameof(id));
            Title = RequireText(title, nameof(title));
            ExpectedBehavior = RequireText(
                expectedBehavior,
                nameof(expectedBehavior));
            Rationale = RequireText(rationale, nameof(rationale));
            PlayerTip = RequireText(playerTip, nameof(playerTip));
        }

        public string Id { get; }

        public string Title { get; }

        public string ExpectedBehavior { get; }

        public string Rationale { get; }

        public string PlayerTip { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Gameplay guidance fields cannot be empty.",
                    parameterName);
            }

            return value;
        }
    }

    internal sealed class GameplayGuidanceCatalog
    {
        private const string DefaultResourceName =
            "Guidance/gameplay-guidance";

        private readonly Dictionary<string, GameplayGuidanceEntry> entries;

        private GameplayGuidanceCatalog(
            Dictionary<string, GameplayGuidanceEntry> entries)
        {
            this.entries = entries;
        }

        public int Count => entries.Count;

        public static GameplayGuidanceCatalog LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(DefaultResourceName);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Gameplay guidance resource '{DefaultResourceName}' was not found.");
            }

            return FromJson(asset.text);
        }

        internal static GameplayGuidanceCatalog FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException(
                    "Gameplay guidance JSON cannot be empty.",
                    nameof(json));
            }

            GameplayGuidanceDocument document =
                JsonUtility.FromJson<GameplayGuidanceDocument>(json);
            if (document?.entries == null || document.entries.Length == 0)
            {
                throw new ArgumentException(
                    "Gameplay guidance requires at least one entry.",
                    nameof(json));
            }

            var entriesById = new Dictionary<string, GameplayGuidanceEntry>(
                StringComparer.Ordinal);
            foreach (GameplayGuidanceData data in document.entries)
            {
                if (data == null)
                {
                    throw new ArgumentException(
                        "Gameplay guidance cannot contain null entries.",
                        nameof(json));
                }

                var entry = new GameplayGuidanceEntry(
                    data.id,
                    data.title,
                    data.expectedBehavior,
                    data.rationale,
                    data.playerTip);
                if (!entriesById.TryAdd(entry.Id, entry))
                {
                    throw new ArgumentException(
                        $"Gameplay guidance ID '{entry.Id}' is duplicated.",
                        nameof(json));
                }
            }

            return new GameplayGuidanceCatalog(entriesById);
        }

        public GameplayGuidanceEntry Require(string id)
        {
            if (string.IsNullOrWhiteSpace(id)
                || !entries.TryGetValue(id, out GameplayGuidanceEntry entry))
            {
                throw new KeyNotFoundException(
                    $"Gameplay guidance ID '{id}' is not defined.");
            }

            return entry;
        }

        [Serializable]
        private sealed class GameplayGuidanceDocument
        {
            public GameplayGuidanceData[] entries;
        }

        [Serializable]
        private sealed class GameplayGuidanceData
        {
            public string id;
            public string title;
            public string expectedBehavior;
            public string rationale;
            public string playerTip;
        }
    }

    internal static class GameplayGuidanceSelector
    {
        public static string Select(
            GameplaySession session,
            TurnMovementController turnMovement)
        {
            if (session == null)
            {
                return null;
            }

            if (session.Operation == GameplaySessionOperation.ResolvingMovement
                || turnMovement?.IsPlaying == true)
            {
                return GameplayGuidanceIds.RoutePlayback;
            }

            if (session.Mode == GameplaySessionMode.TurnBased
                && turnMovement?.PlanPointCount > 1)
            {
                return GameplayGuidanceIds.RoutePlanning;
            }

            if (session.EncounterActive)
            {
                return GameplayGuidanceIds.EncounterActive;
            }

            string contextualActorId = session.Mode == GameplaySessionMode.TurnBased
                ? session.ActiveActorId
                : session.InitiativeOrder[0];
            GameplayActorSnapshot? activeActor = contextualActorId == null
                ? (GameplayActorSnapshot?)null
                : session.GetActor(contextualActorId);
            foreach (ScenarioObjectiveDefinition definition in
                session.Scenario.Objectives)
            {
                GameplayObjectiveSnapshot objective =
                    session.GetObjective(definition.Id);
                if (objective.IsCompleted)
                {
                    return GameplayGuidanceIds.ObjectiveCompleted;
                }

                bool canAffordInteraction = session.Mode ==
                    GameplaySessionMode.Exploration
                    || (activeActor.HasValue
                        && activeActor.Value.TurnBudget.CanAfford(
                            objective.Interaction.TurnCost));
                if (activeActor.HasValue
                    && canAffordInteraction
                    && activeActor.Value.Pose.Position.DistanceTo(
                        objective.Position) <= objective.InteractionRadius)
                {
                    return GameplayGuidanceIds.InteractReady;
                }
            }

            return session.Mode == GameplaySessionMode.TurnBased
                ? GameplayGuidanceIds.VoluntaryActive
                : GameplayGuidanceIds.VoluntaryEntry;
        }
    }
}
