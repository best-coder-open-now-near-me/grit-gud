using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;

namespace GritGud.Application.Levels
{
    public sealed class LevelEditorWorkspaceChangedEventArgs : EventArgs
    {
        internal LevelEditorWorkspaceChangedEventArgs(
            LevelSessionChangedEventArgs sessionChange,
            IReadOnlyList<LevelValidationIssue> validationIssues)
        {
            SessionChange = sessionChange ?? throw new ArgumentNullException(nameof(sessionChange));
            ValidationIssues = validationIssues ?? Array.Empty<LevelValidationIssue>();
        }

        public LevelSessionChangedEventArgs SessionChange { get; }

        public IReadOnlyList<LevelValidationIssue> ValidationIssues { get; }
    }

    public sealed class LevelEditorWorkspace : IDisposable
    {
        private readonly LevelSession session;
        private readonly LevelValidationContent validationContent;
        private IReadOnlyList<LevelValidationIssue> validationIssues;
        private bool disposed;

        public LevelEditorWorkspace(LevelDocument document, ISet<string> knownArchetypeIds = null)
            : this(document, new LevelValidationContent(knownArchetypeIds))
        {
        }

        public LevelEditorWorkspace(
            LevelDocument document,
            LevelValidationContent validationContent)
        {
            session = new LevelSession(document);
            this.validationContent = validationContent
                ?? throw new ArgumentNullException(nameof(validationContent));
            validationIssues = Validate(LevelValidationProfile.Authoring);
            session.Changed += HandleSessionChanged;
        }

        public event EventHandler<LevelEditorWorkspaceChangedEventArgs> Changed;

        public bool CanUndo => session.CanUndo;

        public bool CanRedo => session.CanRedo;

        public bool IsDirty => session.IsDirty;

        public int Revision => session.Revision;

        public IReadOnlyList<LevelValidationIssue> ValidationIssues => validationIssues;

        public void Execute(ILevelEditCommand command)
        {
            ThrowIfDisposed();
            session.Execute(command);
        }

        public void ExecuteTransaction(string description, IEnumerable<ILevelEditCommand> commands)
        {
            ThrowIfDisposed();
            session.ExecuteTransaction(description, commands);
        }

        public bool Undo()
        {
            ThrowIfDisposed();
            return session.Undo();
        }

        public bool Redo()
        {
            ThrowIfDisposed();
            return session.Redo();
        }

        public void ReplaceDocument(LevelDocument document)
        {
            ThrowIfDisposed();
            session.ReplaceDocument(document);
        }

        public void MarkSaved()
        {
            ThrowIfDisposed();
            session.MarkSaved();
        }

        public LevelDocument CreateSnapshot()
        {
            ThrowIfDisposed();
            return session.CreateSnapshot();
        }

        public LevelEntity FindEntitySnapshot(string entityId)
        {
            ThrowIfDisposed();
            return session.FindEntitySnapshot(entityId);
        }

        public TerrainSurfaceData FindTerrainSurfaceSnapshot(string surfaceId)
        {
            ThrowIfDisposed();
            return session.FindTerrainSurfaceSnapshot(surfaceId);
        }

        public IReadOnlyList<LevelValidationIssue> Validate(LevelValidationProfile profile)
        {
            ThrowIfDisposed();
            return LevelValidator.Validate(session.CreateSnapshot(), validationContent, profile);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            session.Changed -= HandleSessionChanged;
            disposed = true;
        }

        private void HandleSessionChanged(object sender, LevelSessionChangedEventArgs args)
        {
            validationIssues = Validate(LevelValidationProfile.Authoring);
            Changed?.Invoke(
                this,
                new LevelEditorWorkspaceChangedEventArgs(args, validationIssues));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LevelEditorWorkspace));
            }
        }
    }
}
