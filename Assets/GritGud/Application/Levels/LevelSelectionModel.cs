using System;
using System.Collections.Generic;
using System.Linq;

namespace GritGud.Application.Levels
{
    public enum LevelSelectionKind
    {
        Entity,
        InteractionPoint,
    }

    public readonly struct LevelSelectionTarget : IEquatable<LevelSelectionTarget>
    {
        public LevelSelectionTarget(
            string entityId,
            LevelSelectionKind kind = LevelSelectionKind.Entity,
            string elementId = null)
        {
            EntityId = string.IsNullOrWhiteSpace(entityId)
                ? throw new ArgumentException("A selected target needs an entity ID.", nameof(entityId))
                : entityId;
            Kind = kind;
            ElementId = elementId;
        }

        public string EntityId { get; }

        public LevelSelectionKind Kind { get; }

        public string ElementId { get; }

        public bool Equals(LevelSelectionTarget other)
        {
            return string.Equals(EntityId, other.EntityId, StringComparison.Ordinal)
                && Kind == other.Kind
                && string.Equals(ElementId, other.ElementId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is LevelSelectionTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(EntityId ?? string.Empty);
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ElementId ?? string.Empty);
                return hash;
            }
        }
    }

    public sealed class LevelSelectionModel
    {
        private readonly List<LevelSelectionTarget> targets = new List<LevelSelectionTarget>();

        public event Action Changed;

        public IReadOnlyList<LevelSelectionTarget> Targets => targets;

        public LevelSelectionTarget? Primary => targets.Count > 0 ? targets[0] : null;

        public string PrimaryEntityId => Primary?.EntityId;

        public void SetSingle(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                Clear();
                return;
            }

            Set(new[] { new LevelSelectionTarget(entityId) });
        }

        public void Set(IEnumerable<LevelSelectionTarget> selection)
        {
            LevelSelectionTarget[] replacement = selection?
                .Distinct()
                .ToArray() ?? Array.Empty<LevelSelectionTarget>();
            if (targets.SequenceEqual(replacement))
            {
                return;
            }

            targets.Clear();
            targets.AddRange(replacement);
            Changed?.Invoke();
        }

        public void Toggle(LevelSelectionTarget target)
        {
            int index = targets.IndexOf(target);
            if (index >= 0)
            {
                targets.RemoveAt(index);
            }
            else
            {
                targets.Add(target);
            }

            Changed?.Invoke();
        }

        public void Clear()
        {
            if (targets.Count == 0)
            {
                return;
            }

            targets.Clear();
            Changed?.Invoke();
        }
    }
}
