using System;
using System.Collections.Generic;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;
using GritGud.Presentation.Levels.Runtime;

namespace GritGud.Presentation.LevelEditing.Tools
{
    public interface ILevelEditorSelectionPolicy
    {
        bool CanSelect(string entityId);
    }

    internal sealed class AllowAllLevelEditorSelectionPolicy : ILevelEditorSelectionPolicy
    {
        public static readonly AllowAllLevelEditorSelectionPolicy Instance =
            new AllowAllLevelEditorSelectionPolicy();

        public bool CanSelect(string entityId) => true;
    }

    public sealed class LevelEditorToolContext
    {
        public LevelEditorToolContext(
            LevelEditorWorkspace workspace,
            LevelSelectionModel selection,
            LevelWorldProjector projector,
            TerrainWorldProjector terrainProjector,
            LevelEditorSceneQuery sceneQuery,
            LevelSnapSettings snapSettings,
            Action<string> setStatus,
            Action<LevelTransformData> previewTransformChanged,
            ILevelEditorSelectionPolicy selectionPolicy = null)
        {
            Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            Projector = projector ?? throw new ArgumentNullException(nameof(projector));
            TerrainProjector = terrainProjector
                ?? throw new ArgumentNullException(nameof(terrainProjector));
            SceneQuery = sceneQuery ?? throw new ArgumentNullException(nameof(sceneQuery));
            SnapSettings = snapSettings ?? throw new ArgumentNullException(nameof(snapSettings));
            SetStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
            PreviewTransformChanged = previewTransformChanged
                ?? throw new ArgumentNullException(nameof(previewTransformChanged));
            SelectionPolicy = selectionPolicy
                ?? AllowAllLevelEditorSelectionPolicy.Instance;
        }

        public LevelEditorWorkspace Workspace { get; }

        public LevelSelectionModel Selection { get; }

        public LevelWorldProjector Projector { get; }

        public TerrainWorldProjector TerrainProjector { get; }

        public LevelEditorSceneQuery SceneQuery { get; }

        public LevelSnapSettings SnapSettings { get; }

        public Action<string> SetStatus { get; }

        public Action<LevelTransformData> PreviewTransformChanged { get; }

        public ILevelEditorSelectionPolicy SelectionPolicy { get; }
    }

    public interface ILevelEditorTool
    {
        string Id { get; }

        string DisplayName { get; }

        void Activate(LevelEditorToolContext context);

        void Deactivate();

        void Tick(LevelEditorInputState input);

        bool Cancel();
    }

    public sealed class LevelEditorToolManager : IDisposable
    {
        private readonly Dictionary<string, ILevelEditorTool> tools =
            new Dictionary<string, ILevelEditorTool>(StringComparer.Ordinal);
        private readonly LevelEditorToolContext context;
        private readonly string defaultToolId;

        public LevelEditorToolManager(LevelEditorToolContext context, string defaultToolId)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.defaultToolId = string.IsNullOrWhiteSpace(defaultToolId)
                ? throw new ArgumentException("A default tool ID is required.", nameof(defaultToolId))
                : defaultToolId;
        }

        public event Action<ILevelEditorTool> ActiveToolChanged;

        public ILevelEditorTool ActiveTool { get; private set; }

        public void Register(ILevelEditorTool tool)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            if (!tools.TryAdd(tool.Id, tool))
            {
                throw new InvalidOperationException($"Tool ID '{tool.Id}' is already registered.");
            }
        }

        public void Activate(string toolId)
        {
            if (!tools.TryGetValue(toolId ?? string.Empty, out ILevelEditorTool replacement))
            {
                throw new InvalidOperationException($"Tool ID '{toolId}' is not registered.");
            }

            if (ReferenceEquals(ActiveTool, replacement))
            {
                return;
            }

            ActiveTool?.Deactivate();
            ActiveTool = replacement;
            ActiveTool.Activate(context);
            ActiveToolChanged?.Invoke(ActiveTool);
        }

        public void ActivateDefault()
        {
            Activate(defaultToolId);
        }

        public void Tick(LevelEditorInputState input)
        {
            ActiveTool?.Tick(input);
        }

        public void CancelActive()
        {
            if (ActiveTool == null || !ActiveTool.Cancel())
            {
                ActivateDefault();
            }
        }

        public void Dispose()
        {
            ActiveTool?.Deactivate();
            ActiveTool = null;
            tools.Clear();
        }
    }
}
