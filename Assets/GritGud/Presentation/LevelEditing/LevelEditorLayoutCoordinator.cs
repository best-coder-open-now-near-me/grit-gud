using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;

namespace GritGud.Presentation.LevelEditing
{
    public sealed class LevelBoundsAuthoringRequest
    {
        public string centerX = string.Empty;
        public string centerY = string.Empty;
        public string centerZ = string.Empty;
        public string sizeX = string.Empty;
        public string sizeY = string.Empty;
        public string sizeZ = string.Empty;
    }

    public sealed class LevelGridAuthoringRequest
    {
        public bool visible;
        public string spacing = string.Empty;
        public string elevation = string.Empty;
    }

    public sealed class LevelArrayAuthoringRequest
    {
        public string countX = string.Empty;
        public string countZ = string.Empty;
        public string spacingX = string.Empty;
        public string spacingZ = string.Empty;
    }

    public sealed class LevelEditorLayoutCoordinator
    {
        public const int MaximumArrayCountPerAxis = 32;
        public const int MaximumArrayCopiesPerOperation = 256;

        private readonly LevelEditorWorkspace workspace;
        private readonly LevelSelectionModel selection;
        private readonly LevelEditorCameraController camera;
        private readonly LevelEditorGridSettings grid;

        public LevelEditorLayoutCoordinator(
            LevelEditorWorkspace workspace,
            LevelSelectionModel selection,
            LevelEditorCameraController camera,
            LevelEditorGridSettings grid)
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public event Action<string> StatusChanged;

        public void ApplyBounds(LevelBoundsAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!TryFloat(request.centerX, out float centerX)
                || !TryFloat(request.centerY, out float centerY)
                || !TryFloat(request.centerZ, out float centerZ)
                || !TryFloat(request.sizeX, out float sizeX)
                || !TryFloat(request.sizeY, out float sizeY)
                || !TryFloat(request.sizeZ, out float sizeZ))
            {
                Report("Level-bound values must be finite numbers.");
                return;
            }
            if (sizeX <= 0f || sizeY <= 0f || sizeZ <= 0f)
            {
                Report("Every level-bound size must be greater than zero.");
                return;
            }

            LevelBoundsData before = workspace.CreateSnapshot().bounds;
            var after = new LevelBoundsData(
                new Float3Data(centerX, centerY, centerZ),
                new Float3Data(sizeX, sizeY, sizeZ));
            workspace.Execute(new SetLevelBoundsCommand(before, after));
            Report("Updated the authored level bounds.");
        }

        public void ConfigureGrid(LevelGridAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!TryFloat(request.spacing, out float spacing)
                || !TryFloat(request.elevation, out float elevation))
            {
                Report("Grid spacing and elevation must be finite numbers.");
                return;
            }
            if (spacing <= 0f)
            {
                Report("Grid spacing must be greater than zero.");
                return;
            }

            grid.Configure(request.visible, spacing, elevation);
            Report(request.visible ? "Updated the editor grid." : "Hid the editor grid.");
        }

        public void SetCameraView(LevelEditorCameraView view)
        {
            camera.SetView(view);
            Report(view == LevelEditorCameraView.Perspective
                ? "Switched to the perspective view."
                : $"Switched to the {view.ToString().ToLowerInvariant()} orthographic view.");
        }

        public void DuplicateArray(LevelArrayAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!TryInt(request.countX, out int countX)
                || !TryInt(request.countZ, out int countZ)
                || !TryFloat(request.spacingX, out float spacingX)
                || !TryFloat(request.spacingZ, out float spacingZ))
            {
                Report("Array counts must be whole numbers and spacing must be finite.");
                return;
            }
            if (countX < 1 || countZ < 1
                || countX > MaximumArrayCountPerAxis
                || countZ > MaximumArrayCountPerAxis)
            {
                Report($"Array counts must be from 1 to {MaximumArrayCountPerAxis} per axis.");
                return;
            }
            if (countX == 1 && countZ == 1)
            {
                Report("Increase at least one array count to create copies.");
                return;
            }

            LevelDocument snapshot = workspace.CreateSnapshot();
            string[] selectedIds = selection.Targets
                .Where(target => target.Kind == LevelSelectionKind.Entity)
                .Select(target => target.EntityId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            LevelEntity[] sources = selectedIds
                .Select(id => snapshot.entities.FirstOrDefault(entity => string.Equals(
                    entity?.id,
                    id,
                    StringComparison.Ordinal)))
                .Where(entity => entity != null)
                .ToArray();
            if (sources.Length == 0)
            {
                Report("Select one or more entities before creating an array.");
                return;
            }

            long additionalCount = (long)sources.Length * ((countX * countZ) - 1);
            if (additionalCount > MaximumArrayCopiesPerOperation)
            {
                Report($"One array operation can create at most {MaximumArrayCopiesPerOperation} copies.");
                return;
            }
            if (snapshot.entities.Count + additionalCount > LevelValidator.MaximumEntityCount)
            {
                Report($"The array would exceed the {LevelValidator.MaximumEntityCount}-entity limit.");
                return;
            }

            var commands = new List<ILevelEditCommand>((int)additionalCount);
            var replacementSelection = new List<LevelSelectionTarget>((int)additionalCount);
            for (int z = 0; z < countZ; z++)
            {
                for (int x = 0; x < countX; x++)
                {
                    if (x == 0 && z == 0)
                        continue;
                    foreach (LevelEntity source in sources)
                    {
                        LevelEntity copy = source.DeepCopy();
                        copy.id = LevelDocumentFactory.NewStableId();
                        copy.transform.position = new Float3Data(
                            source.transform.position.x + (x * spacingX),
                            source.transform.position.y,
                            source.transform.position.z + (z * spacingZ));
                        commands.Add(new AddEntityCommand(copy));
                        replacementSelection.Add(new LevelSelectionTarget(copy.id));
                    }
                }
            }

            workspace.ExecuteTransaction("Create entity array", commands);
            selection.Set(replacementSelection);
            Report($"Created {commands.Count} array copies in one undo step.");
        }

        private void Report(string message) => StatusChanged?.Invoke(message ?? string.Empty);

        private static bool TryFloat(string text, out float value)
        {
            bool parsed = float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
            return parsed && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryInt(string text, out int value) => int.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }
}
