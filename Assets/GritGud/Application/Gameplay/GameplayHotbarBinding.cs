using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public static class GameplayCoreActorAbilities
    {
        public const string StanceId = "ability.stance";
        public const int StanceHotbarSlot = 7;
    }

    public enum GameplayHotbarBindingKind
    {
        InventoryItem,
        ActorAbility,
    }

    public readonly struct GameplayHotbarBinding :
        IEquatable<GameplayHotbarBinding>
    {
        public GameplayHotbarBinding(
            GameplayHotbarBindingKind kind,
            string contentId)
        {
            if (!Enum.IsDefined(typeof(GameplayHotbarBindingKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (string.IsNullOrWhiteSpace(contentId))
            {
                throw new ArgumentException(
                    "Hotbar bindings require a stable content ID.",
                    nameof(contentId));
            }

            Kind = kind;
            ContentId = contentId;
        }

        public GameplayHotbarBindingKind Kind { get; }

        public string ContentId { get; }

        public bool Equals(GameplayHotbarBinding other) =>
            Kind == other.Kind
            && string.Equals(ContentId, other.ContentId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is GameplayHotbarBinding other && Equals(other);

        public override int GetHashCode() =>
            ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(ContentId);
    }

    public sealed class GameplayActorAbilityHotbarDefinition
    {
        public GameplayActorAbilityHotbarDefinition(
            string id,
            string displayName,
            int authoredSlot,
            IEnumerable<GameplayActorAbilityOptionDefinition> options = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Actor abilities require stable identifiers.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Actor abilities require display names.",
                    nameof(displayName));
            }

            if (authoredSlot < 0
                || authoredSlot > GameplayHotbarRules.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(authoredSlot));
            }

            Id = id;
            DisplayName = displayName;
            AuthoredSlot = authoredSlot;
            Options = CopyOptions(options);
        }

        public string Id { get; }

        public string DisplayName { get; }

        public int AuthoredSlot { get; }

        public IReadOnlyList<GameplayActorAbilityOptionDefinition> Options
        { get; }

        private static IReadOnlyList<GameplayActorAbilityOptionDefinition>
            CopyOptions(
                IEnumerable<GameplayActorAbilityOptionDefinition> options)
        {
            if (options == null)
            {
                return Array.Empty<GameplayActorAbilityOptionDefinition>();
            }

            var copy = new List<GameplayActorAbilityOptionDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayActorAbilityOptionDefinition option in options)
            {
                if (option == null)
                {
                    throw new ArgumentException(
                        "Actor ability options cannot contain null entries.",
                        nameof(options));
                }

                if (!ids.Add(option.Id))
                {
                    throw new ArgumentException(
                        $"Actor ability option '{option.Id}' is registered more than once.",
                        nameof(options));
                }

                copy.Add(option);
            }

            return copy.AsReadOnly();
        }
    }

    public sealed class GameplayActorAbilityOptionDefinition
    {
        public GameplayActorAbilityOptionDefinition(
            string id,
            string displayName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Actor ability options require stable identifiers.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Actor ability options require display names.",
                    nameof(displayName));
            }

            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; }

        public string DisplayName { get; }
    }

    public sealed class GameplayActorAbilityOptionHotbarState
    {
        public GameplayActorAbilityOptionHotbarState(
            GameplayActorAbilityOptionDefinition definition,
            bool enabled,
            bool pending,
            string tooltip,
            string selectionLabel = null)
        {
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            Enabled = enabled;
            Pending = pending;
            Tooltip = tooltip ?? string.Empty;
            SelectionLabel = string.IsNullOrWhiteSpace(selectionLabel)
                ? Definition.DisplayName
                : selectionLabel;
        }

        public GameplayActorAbilityOptionDefinition Definition { get; }

        public bool Enabled { get; }

        public bool Pending { get; }

        public string Tooltip { get; }

        public string SelectionLabel { get; }
    }

    public sealed class GameplayActorAbilityHotbarState
    {
        public GameplayActorAbilityHotbarState(
            GameplayActorAbilityHotbarDefinition definition,
            bool enabled,
            bool pending,
            string tooltip,
            IEnumerable<GameplayActorAbilityOptionHotbarState> options = null)
        {
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            Enabled = enabled;
            Pending = pending;
            Tooltip = tooltip ?? string.Empty;
            Options = CopyOptions(options);
            if (Options.Count != Definition.Options.Count)
            {
                throw new ArgumentException(
                    "Actor ability option states must match the authored options.",
                    nameof(options));
            }

            for (int index = 0; index < Options.Count; index++)
            {
                if (!string.Equals(
                        Options[index].Definition.Id,
                        Definition.Options[index].Id,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Actor ability option states must preserve authored option order.",
                        nameof(options));
                }
            }
        }

        public GameplayActorAbilityHotbarDefinition Definition { get; }

        public bool Enabled { get; }

        public bool Pending { get; }

        public string Tooltip { get; }

        public IReadOnlyList<GameplayActorAbilityOptionHotbarState> Options
        { get; }

        private static IReadOnlyList<GameplayActorAbilityOptionHotbarState>
            CopyOptions(
                IEnumerable<GameplayActorAbilityOptionHotbarState> options)
        {
            if (options == null)
            {
                return Array.Empty<GameplayActorAbilityOptionHotbarState>();
            }

            var copy = new List<GameplayActorAbilityOptionHotbarState>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayActorAbilityOptionHotbarState option in options)
            {
                if (option == null)
                {
                    throw new ArgumentException(
                        "Actor ability option states cannot contain null entries.",
                        nameof(options));
                }

                if (!ids.Add(option.Definition.Id))
                {
                    throw new ArgumentException(
                        $"Actor ability option state '{option.Definition.Id}' is registered more than once.",
                        nameof(options));
                }

                copy.Add(option);
            }

            return copy.AsReadOnly();
        }
    }
}
