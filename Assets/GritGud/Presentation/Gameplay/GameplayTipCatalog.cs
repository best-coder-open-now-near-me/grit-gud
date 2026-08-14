using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayTipEntry
    {
        public GameplayTipEntry(
            string id,
            string category,
            string title,
            string text,
            bool showOnLoadingScreens,
            int tutorialOrder)
        {
            Id = RequireText(id, nameof(id));
            Category = RequireText(category, nameof(category));
            Title = RequireText(title, nameof(title));
            Text = RequireText(text, nameof(text));
            if (tutorialOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tutorialOrder));
            }

            ShowOnLoadingScreens = showOnLoadingScreens;
            TutorialOrder = tutorialOrder;
        }

        public string Id { get; }
        public string Category { get; }
        public string Title { get; }
        public string Text { get; }
        public bool ShowOnLoadingScreens { get; }
        public int TutorialOrder { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Tip fields cannot be empty.", parameterName);
            }
            return value;
        }
    }

    internal sealed class GameplayTipCatalog
    {
        private const string DefaultResourceName = "Guidance/gameplay-tips";
        private readonly List<GameplayTipEntry> entries;

        private GameplayTipCatalog(List<GameplayTipEntry> entries)
        {
            this.entries = entries;
        }

        public IReadOnlyList<GameplayTipEntry> Entries => entries;

        public static GameplayTipCatalog LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(DefaultResourceName);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Gameplay tip resource '{DefaultResourceName}' was not found.");
            }
            return FromJson(asset.text);
        }

        internal static GameplayTipCatalog FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Gameplay tip JSON cannot be empty.", nameof(json));
            }

            GameplayTipDocument document = JsonUtility.FromJson<GameplayTipDocument>(json);
            if (document?.entries == null || document.entries.Length == 0)
            {
                throw new ArgumentException("At least one gameplay tip is required.", nameof(json));
            }

            var result = new List<GameplayTipEntry>(document.entries.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayTipData data in document.entries)
            {
                if (data == null)
                {
                    throw new ArgumentException("Gameplay tips cannot contain null entries.", nameof(json));
                }
                var entry = new GameplayTipEntry(
                    data.id,
                    data.category,
                    data.title,
                    data.text,
                    data.showOnLoadingScreens,
                    data.tutorialOrder);
                if (!ids.Add(entry.Id))
                {
                    throw new ArgumentException(
                        $"Gameplay tip ID '{entry.Id}' is duplicated.", nameof(json));
                }
                result.Add(entry);
            }
            result.Sort((left, right) => left.TutorialOrder.CompareTo(right.TutorialOrder));
            return new GameplayTipCatalog(result);
        }

        public GameplayTipEntry GetLoadingScreenTip(int rotationIndex)
        {
            var loadingTips = new List<GameplayTipEntry>();
            foreach (GameplayTipEntry entry in entries)
            {
                if (entry.ShowOnLoadingScreens)
                {
                    loadingTips.Add(entry);
                }
            }
            if (loadingTips.Count == 0)
            {
                return null;
            }
            int normalized = ((rotationIndex % loadingTips.Count) + loadingTips.Count)
                % loadingTips.Count;
            return loadingTips[normalized];
        }

        [Serializable]
        private sealed class GameplayTipDocument
        {
            public GameplayTipData[] entries;
        }

        [Serializable]
        private sealed class GameplayTipData
        {
            public string id;
            public string category;
            public string title;
            public string text;
            public bool showOnLoadingScreens;
            public int tutorialOrder;
        }
    }
}
