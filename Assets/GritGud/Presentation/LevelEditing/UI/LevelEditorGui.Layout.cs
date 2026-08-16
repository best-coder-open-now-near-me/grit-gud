using System;
using System.Globalization;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Core;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed partial class LevelEditorGui
    {
        private LevelBoundsAuthoringRequest boundsFields = new LevelBoundsAuthoringRequest();
        private LevelGridAuthoringRequest gridFields = new LevelGridAuthoringRequest
        {
            visible = true,
            spacing = "2.5",
            elevation = "0",
        };
        private LevelArrayAuthoringRequest arrayFields = new LevelArrayAuthoringRequest
        {
            countX = "2",
            countZ = "1",
            spacingX = "2.5",
            spacingZ = "2.5",
        };
        private string synchronizedBoundsIdentity = string.Empty;

        public void SyncLayoutFields(
            LevelDocument document,
            LevelEditorGridSettings grid,
            bool force = false)
        {
            if (document == null || grid == null)
                return;
            string identity = BoundsIdentity(document.bounds);
            if (force || !string.Equals(
                    identity,
                    synchronizedBoundsIdentity,
                    StringComparison.Ordinal))
            {
                synchronizedBoundsIdentity = identity;
                boundsFields.centerX = Format(document.bounds.center.x);
                boundsFields.centerY = Format(document.bounds.center.y);
                boundsFields.centerZ = Format(document.bounds.center.z);
                boundsFields.sizeX = Format(document.bounds.size.x);
                boundsFields.sizeY = Format(document.bounds.size.y);
                boundsFields.sizeZ = Format(document.bounds.size.z);
            }
            if (force)
            {
                gridFields.visible = grid.Visible;
                gridFields.spacing = Format(grid.Spacing);
                gridFields.elevation = Format(grid.Elevation);
            }
        }

        private void DrawLevelLayoutPanel(LevelDocument document)
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("LEVEL BOUNDS");
            DrawBoundVector("Center", ref boundsFields.centerX, ref boundsFields.centerY,
                ref boundsFields.centerZ);
            DrawBoundVector("Size", ref boundsFields.sizeX, ref boundsFields.sizeY,
                ref boundsFields.sizeZ);
            if (GUILayout.Button("APPLY BOUNDS", PanelApplyButtonLayout()))
                actions.ApplyLevelBounds(boundsFields);

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("LAYOUT GRID");
            gridFields.visible = GUILayout.Toggle(gridFields.visible, "Show grid");
            DrawLabeledField("Spacing", ref gridFields.spacing);
            DrawLabeledField("Elevation", ref gridFields.elevation);
            if (GUILayout.Button("APPLY GRID", PanelApplyButtonLayout()))
                actions.ConfigureGrid(gridFields);
            GUILayout.Label("The grid is a local editor preference clipped to the authored bounds.");

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("ENTITY ARRAY");
            GUILayout.Label("Counts include the selected source cell at X1 / Z1.");
            DrawLabeledField("Count X", ref arrayFields.countX);
            DrawLabeledField("Count Z", ref arrayFields.countZ);
            DrawLabeledField("Step X", ref arrayFields.spacingX);
            DrawLabeledField("Step Z", ref arrayFields.spacingZ);
            GUI.enabled = selection.Targets.Count > 0;
            if (GUILayout.Button("CREATE ARRAY", PanelPrimaryButtonLayout()))
                actions.DuplicateArray(arrayFields);
            GUI.enabled = true;
        }

        private static void DrawBoundVector(
            string label,
            ref string x,
            ref string y,
            ref string z)
        {
            GUILayout.Label(label + " XYZ");
            GUILayout.BeginHorizontal();
            x = GUILayout.TextField(x ?? string.Empty);
            y = GUILayout.TextField(y ?? string.Empty);
            z = GUILayout.TextField(z ?? string.Empty);
            GUILayout.EndHorizontal();
        }

        private static string BoundsIdentity(LevelBoundsData bounds) =>
            bounds.center.x.ToString("R", CultureInfo.InvariantCulture) + ":"
            + bounds.center.y.ToString("R", CultureInfo.InvariantCulture) + ":"
            + bounds.center.z.ToString("R", CultureInfo.InvariantCulture) + ":"
            + bounds.size.x.ToString("R", CultureInfo.InvariantCulture) + ":"
            + bounds.size.y.ToString("R", CultureInfo.InvariantCulture) + ":"
            + bounds.size.z.ToString("R", CultureInfo.InvariantCulture);
    }
}
