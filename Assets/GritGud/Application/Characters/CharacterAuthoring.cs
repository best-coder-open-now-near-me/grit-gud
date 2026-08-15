using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Characters;

namespace GritGud.Application.Characters
{
    public interface ICharacterSerializer
    {
        string Serialize(CharacterDocument document, bool prettyPrint = true);
        CharacterDocument Deserialize(string text);
    }

    public sealed class CharacterSerializationException : Exception
    {
        public CharacterSerializationException(string message) : base(message) { }
        public CharacterSerializationException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class CharacterAuthoringOption
    {
        public CharacterAuthoringOption(
            string id,
            string slotId,
            string compatibilityTag)
        {
            Id = id ?? string.Empty;
            SlotId = slotId ?? string.Empty;
            CompatibilityTag = compatibilityTag ?? string.Empty;
        }

        public string Id { get; }
        public string SlotId { get; }
        public string CompatibilityTag { get; }
    }

    public static class CharacterAppearanceGenerator
    {
        public static CharacterAppearanceData Generate(
            int seed,
            IReadOnlyList<CharacterAuthoringOption> bodies,
            IReadOnlyList<CharacterAuthoringOption> accessories)
        {
            if (bodies == null || bodies.Count == 0)
                throw new ArgumentException("At least one character body is required.", nameof(bodies));
            var random = new Random(seed);
            CharacterAuthoringOption body = bodies[random.Next(bodies.Count)];
            var result = new CharacterAppearanceData { bodyId = body.Id };
            IEnumerable<IGrouping<string, CharacterAuthoringOption>> slots =
                (accessories ?? Array.Empty<CharacterAuthoringOption>())
                .Where(option => option != null
                    && (string.IsNullOrEmpty(option.CompatibilityTag)
                        || string.Equals(
                            option.CompatibilityTag,
                            body.CompatibilityTag,
                            StringComparison.Ordinal)))
                .GroupBy(option => option.SlotId, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal);
            foreach (IGrouping<string, CharacterAuthoringOption> slot in slots)
            {
                CharacterAuthoringOption[] candidates = slot
                    .OrderBy(option => option.Id, StringComparer.Ordinal)
                    .ToArray();
                int choice = random.Next(candidates.Length + 1);
                if (choice < candidates.Length)
                    result.SetAccessory(slot.Key, candidates[choice].Id);
            }
            return result;
        }
    }

    public sealed class CharacterAuthoringSession
    {
        private sealed class Revision
        {
            public string Description;
            public CharacterDocument Before;
            public CharacterDocument After;
        }

        private readonly List<Revision> history = new List<Revision>();
        private CharacterDocument document;
        private int position;
        private int savedPosition;

        public CharacterAuthoringSession(CharacterDocument document, bool initiallySaved = true)
        {
            this.document = document?.DeepCopy() ?? throw new ArgumentNullException(nameof(document));
            savedPosition = initiallySaved ? 0 : -1;
        }

        public event Action Changed;
        public bool CanUndo => position > 0;
        public bool CanRedo => position < history.Count;
        public bool IsDirty => savedPosition < 0 || position != savedPosition;

        public CharacterDocument CreateSnapshot() => document.DeepCopy();

        public void Apply(string description, CharacterDocument replacement)
        {
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));
            if (position < history.Count)
            {
                if (savedPosition > position)
                    savedPosition = -1;
                history.RemoveRange(position, history.Count - position);
            }
            var revision = new Revision
            {
                Description = description ?? "Edit character",
                Before = document.DeepCopy(),
                After = replacement.DeepCopy(),
            };
            document = revision.After.DeepCopy();
            history.Add(revision);
            position++;
            Changed?.Invoke();
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;
            position--;
            document = history[position].Before.DeepCopy();
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
                return false;
            document = history[position].After.DeepCopy();
            position++;
            Changed?.Invoke();
            return true;
        }

        public void Replace(CharacterDocument replacement, bool saved)
        {
            document = replacement?.DeepCopy() ?? throw new ArgumentNullException(nameof(replacement));
            history.Clear();
            position = 0;
            savedPosition = saved ? 0 : -1;
            Changed?.Invoke();
        }

        public void MarkSaved() => savedPosition = position;
    }
}
