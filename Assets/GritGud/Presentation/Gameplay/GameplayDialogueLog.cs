using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GritGud.Application.Gameplay;
using GritGud.Presentation.Persistence;

namespace GritGud.Presentation.Gameplay
{
    [Flags]
    public enum GameplayDialogueChannel
    {
        None = 0,
        Dialogue = 1 << 0,
        System = 1 << 1,
        CombatDiagnostics = 1 << 2,
        All = Dialogue | System | CombatDiagnostics,
    }

    public sealed class GameplayDialogueEntry
    {
        internal GameplayDialogueEntry(
            long sequence,
            GameplayDialogueChannel channel,
            string title,
            string message)
        {
            Sequence = sequence;
            Channel = channel;
            Title = title;
            Message = message;
        }

        public long Sequence { get; }

        public GameplayDialogueChannel Channel { get; }

        public string Title { get; }

        public string Message { get; }
    }

    public interface IGameplayDialogueEntrySource
    {
        IReadOnlyList<GameplayDialogueEntry> Entries { get; }
        long LatestSequence { get; }
        long HighlightedSequence { get; }
        int CountVisible(GameplayDialogueChannel filters);
    }

    public sealed class GameplayDialogueLog : IGameplayDialogueEntrySource
    {
        private const int MaximumEntries = 256;

        private readonly List<GameplayDialogueEntry> entries =
            new List<GameplayDialogueEntry>();
        private readonly IReadOnlyList<GameplayDialogueEntry> readOnlyEntries;
        private long nextSequence = 1;

        public GameplayDialogueLog()
        {
            readOnlyEntries = entries.AsReadOnly();
        }

        public IReadOnlyList<GameplayDialogueEntry> Entries => readOnlyEntries;

        public long LatestSequence =>
            entries.Count == 0 ? 0 : entries[entries.Count - 1].Sequence;

        public long HighlightedSequence => 0;

        public GameplayDialogueEntry Append(
            GameplayDialogueChannel channel,
            string title,
            string message)
        {
            RequireSingleChannel(channel, nameof(channel));
            string normalizedTitle = RequireText(title, nameof(title));
            string normalizedMessage = RequireText(message, nameof(message));
            var entry = new GameplayDialogueEntry(
                nextSequence++,
                channel,
                normalizedTitle,
                normalizedMessage);
            if (entries.Count == MaximumEntries)
            {
                entries.RemoveAt(0);
            }

            entries.Add(entry);
            return entry;
        }

        public GameplayDialogueEntry AppendCombatDiagnostic(
            string title,
            params string[] formulaLines)
        {
            if (formulaLines == null)
            {
                throw new ArgumentNullException(nameof(formulaLines));
            }

            var normalizedLines = new List<string>(formulaLines.Length);
            foreach (string line in formulaLines)
            {
                normalizedLines.Add(RequireText(line, nameof(formulaLines)));
            }

            if (normalizedLines.Count == 0)
            {
                throw new ArgumentException(
                    "Combat diagnostics require at least one formula line.",
                    nameof(formulaLines));
            }

            return Append(
                GameplayDialogueChannel.CombatDiagnostics,
                title,
                string.Join(Environment.NewLine, normalizedLines));
        }

        public GameplayDialogueEntry AppendCombatDiagnostic(
            GameplayDiagnosticProjection projection)
        {
            if (projection == null)
            {
                throw new ArgumentNullException(nameof(projection));
            }

            var lines = new string[projection.Lines.Count];
            for (int index = 0; index < lines.Length; index++)
            {
                lines[index] = projection.Lines[index];
            }

            return AppendCombatDiagnostic(projection.Title, lines);
        }

        public int CountVisible(GameplayDialogueChannel filters)
        {
            int count = 0;
            foreach (GameplayDialogueEntry entry in entries)
            {
                if ((filters & entry.Channel) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        public void Clear()
        {
            entries.Clear();
            nextSequence = 1;
        }

        internal static void RequireSingleChannel(
            GameplayDialogueChannel channel,
            string parameterName)
        {
            int value = (int)channel;
            int knownChannels = (int)GameplayDialogueChannel.All;
            if (value == 0
                || (value & ~knownChannels) != 0
                || (value & (value - 1)) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    channel,
                    "Dialogue entries require one known channel.");
            }
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Dialogue log text cannot be empty.",
                    parameterName);
            }

            return value.Trim();
        }
    }

    internal static class GameplayDialogueExporter
    {
        public static string Export(GameplayDialogueLog log)
        {
            string transcript = Format(log);
            string fileName = "grit-gud-dialogue-"
                + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss'Z'",
                    CultureInfo.InvariantCulture) + ".txt";
            return TextFileTransfer.Export(
                fileName, transcript, "text/plain;charset=utf-8");
        }

        internal static string Format(GameplayDialogueLog log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            var text = new StringBuilder();
            text.AppendLine("GRIT GUD DIALOGUE TRANSCRIPT");
            text.AppendLine("===========================");
            foreach (GameplayDialogueEntry entry in log.Entries)
            {
                text.Append('#').Append(entry.Sequence.ToString(
                        "0000", CultureInfo.InvariantCulture))
                    .Append("  ").Append(entry.Channel.ToString().ToUpperInvariant())
                    .Append(" - ").AppendLine(entry.Title.ToUpperInvariant());
                text.AppendLine(entry.Message).AppendLine();
            }
            return text.ToString();
        }
    }
}
