using System;
using System.Linq;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed partial class LevelEditorGui
    {
        private void DrawPlayability()
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("PLAYABILITY CHECK");
            LevelPlayabilityReport report = actions.PlayabilityReport;
            string runLabel = report == null ? "RUN CHECK" : "REFRESH CHECK";
            if (GUILayout.Button(runLabel, PanelPrimaryButtonLayout()))
                actions.RunPlayabilityDiagnostics();

            bool overlay = GUILayout.Toggle(
                actions.SlopeOverlayEnabled,
                "Slope heatmap (green walkable / red steep)");
            if (overlay != actions.SlopeOverlayEnabled)
                actions.SetSlopeOverlayEnabled(overlay);

            if (report == null)
            {
                GUILayout.Label(
                    "Checks heightfield slope, connected terrain regions, scenario support, "
                    + "objective terrain routes, and overlapping actor starts.");
                return;
            }

            if (actions.PlayabilityReportIsStale)
                GUILayout.Label("STALE — the level changed after this report.");
            GUILayout.Label(
                $"Slope limit: {report.MaximumWalkableSlopeDegrees:0.#}°  •  "
                + $"Warnings: {report.WarningCount}");
            GUILayout.Label(
                $"Scenario anchors: {report.AnchorCount}  •  "
                + $"Without terrain: {report.UnsupportedAnchorCount}");
            foreach (TerrainPlayabilitySurfaceReport surface in report.Surfaces)
            {
                GUILayout.Label(
                    $"{surface.SurfaceId}: {surface.WalkablePercent:0.#}% walkable, "
                    + $"{surface.ConnectedRegionCount} region(s), "
                    + $"max {surface.MaximumSlopeDegrees:0.#}°");
            }

            LevelPlayabilityDiagnostic[] warnings = report.Diagnostics
                .Where(item => item.Severity == LevelPlayabilityDiagnosticSeverity.Warning)
                .Take(8)
                .ToArray();
            if (warnings.Length == 0)
            {
                GUILayout.Label("No playability warnings in this terrain-based pass.");
                return;
            }
            foreach (LevelPlayabilityDiagnostic warning in warnings)
            {
                string text = $"WARNING: {warning.Message}";
                if (string.IsNullOrWhiteSpace(warning.EntityId))
                    GUILayout.Label(text);
                else if (GUILayout.Button(text, PanelCompactButtonLayout()))
                    actions.FocusEntity(warning.EntityId);
            }
            int remaining = report.WarningCount - warnings.Length;
            if (remaining > 0)
                GUILayout.Label($"…and {remaining} more warnings.");
            GUILayout.Label(
                "This is a terrain-only diagnostic, not a navigation bake; structures may "
                + "provide valid routes or support.");
        }
    }
}
