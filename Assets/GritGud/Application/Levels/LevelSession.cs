using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;

namespace GritGud.Application.Levels
{
    public enum LevelSessionChangeKind
    {
        Execute,
        Undo,
        Redo,
        ReplaceDocument,
    }

    public sealed class LevelSessionChangedEventArgs : EventArgs
    {
        public LevelSessionChangedEventArgs(
            LevelSessionChangeKind kind,
            int revision,
            ILevelEditCommand command = null)
        {
            Kind = kind;
            Revision = revision;
            Command = command;
        }

        public LevelSessionChangeKind Kind { get; }

        public int Revision { get; }

        public ILevelEditCommand Command { get; }

        public IReadOnlyCollection<string> AffectedEntityIds =>
            Command?.AffectedEntityIds ?? Array.Empty<string>();

        public bool RequiresFullProjection =>
            Kind == LevelSessionChangeKind.ReplaceDocument
            || Command == null
            || Command.RequiresFullProjection;
    }

    public sealed class LevelSession
    {
        private readonly List<ILevelEditCommand> history = new List<ILevelEditCommand>();
        private LevelDocument document;
        private int historyPosition;
        private int savedHistoryPosition;
        private int revision;

        public LevelSession(LevelDocument document, bool initiallySaved = true)
        {
            this.document = document?.DeepCopy() ?? throw new ArgumentNullException(nameof(document));
            savedHistoryPosition = initiallySaved ? 0 : -1;
        }

        public event EventHandler<LevelSessionChangedEventArgs> Changed;

        public bool CanUndo => historyPosition > 0;

        public bool CanRedo => historyPosition < history.Count;

        public bool IsDirty => savedHistoryPosition < 0 || historyPosition != savedHistoryPosition;

        public int Revision => revision;

        public int HistoryPosition => historyPosition;

        public void Execute(ILevelEditCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            command.Apply(document);
            if (historyPosition < history.Count)
            {
                if (savedHistoryPosition > historyPosition)
                {
                    savedHistoryPosition = -1;
                }

                history.RemoveRange(historyPosition, history.Count - historyPosition);
            }

            history.Add(command);
            historyPosition++;
            revision++;
            Changed?.Invoke(this, new LevelSessionChangedEventArgs(
                LevelSessionChangeKind.Execute,
                revision,
                command));
        }

        public void ExecuteTransaction(string description, IEnumerable<ILevelEditCommand> commands)
        {
            Execute(new CompositeLevelEditCommand(description, commands));
        }

        public bool Undo()
        {
            if (!CanUndo)
            {
                return false;
            }

            ILevelEditCommand command = history[historyPosition - 1];
            command.Revert(document);
            historyPosition--;
            revision++;
            Changed?.Invoke(this, new LevelSessionChangedEventArgs(
                LevelSessionChangeKind.Undo,
                revision,
                command));
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
            {
                return false;
            }

            ILevelEditCommand command = history[historyPosition];
            command.Apply(document);
            historyPosition++;
            revision++;
            Changed?.Invoke(this, new LevelSessionChangedEventArgs(
                LevelSessionChangeKind.Redo,
                revision,
                command));
            return true;
        }

        public void ReplaceDocument(LevelDocument document, bool isSaved = true)
        {
            this.document = document?.DeepCopy() ?? throw new ArgumentNullException(nameof(document));
            history.Clear();
            historyPosition = 0;
            savedHistoryPosition = isSaved ? 0 : -1;
            revision++;
            Changed?.Invoke(this, new LevelSessionChangedEventArgs(
                LevelSessionChangeKind.ReplaceDocument,
                revision));
        }

        public void MarkSaved()
        {
            savedHistoryPosition = historyPosition;
        }

        public LevelDocument CreateSnapshot()
        {
            return document.DeepCopy();
        }

        public LevelEntity FindEntitySnapshot(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return null;
            }

            if (document.entities == null)
            {
                return null;
            }

            foreach (LevelEntity entity in document.entities)
            {
                if (string.Equals(entity?.id, entityId, StringComparison.Ordinal))
                {
                    return entity.DeepCopy();
                }
            }

            return null;
        }

        public TerrainSurfaceData FindTerrainSurfaceSnapshot(string surfaceId)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
            {
                return null;
            }

            if (document.terrainSurfaces == null)
            {
                return null;
            }

            foreach (TerrainSurfaceData surface in document.terrainSurfaces)
            {
                if (string.Equals(surface?.id, surfaceId, StringComparison.Ordinal))
                {
                    return surface.DeepCopy();
                }
            }

            return null;
        }
    }
}
