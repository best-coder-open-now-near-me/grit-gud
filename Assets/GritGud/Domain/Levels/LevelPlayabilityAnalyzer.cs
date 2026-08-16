using System;
using System.Collections.Generic;
using System.Linq;

namespace GritGud.Domain.Levels
{
    public static class LevelPlayabilityAnalyzer
    {
        public const float DefaultMaximumWalkableSlopeDegrees = 50f;
        public const float DefaultMaximumStepHeight = 0.35f;
        public const float TerrainSupportTolerance = 1.25f;
        private const float ActorOverlapDistance = 0.75f;
        private const float ActorOverlapHeight = 1.5f;

        public static LevelPlayabilityReport Analyze(
            LevelDocument source,
            float maximumWalkableSlopeDegrees = DefaultMaximumWalkableSlopeDegrees,
            float maximumStepHeight = DefaultMaximumStepHeight)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!Finite(maximumWalkableSlopeDegrees)
                || maximumWalkableSlopeDegrees <= 0f
                || maximumWalkableSlopeDegrees >= 90f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumWalkableSlopeDegrees));
            }
            if (!Finite(maximumStepHeight) || maximumStepHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maximumStepHeight));

            LevelDocument document = source.DeepCopy();
            document.Normalize();
            var diagnostics = new List<LevelPlayabilityDiagnostic>();
            var analyses = new List<TerrainPlayabilityAnalysis>();
            foreach (TerrainSurfaceData surface in document.terrainSurfaces.Where(
                TerrainPlayabilityAnalysisBuilder.CanAnalyze))
            {
                analyses.Add(TerrainPlayabilityAnalysisBuilder.Analyze(
                    surface,
                    maximumWalkableSlopeDegrees,
                    maximumStepHeight));
            }

            if (analyses.Count == 0)
            {
                diagnostics.Add(new LevelPlayabilityDiagnostic(
                    LevelPlayabilityDiagnosticSeverity.Info,
                    "playability.terrain.none",
                    "No valid heightfield is available; terrain slope and support checks were skipped."));
            }

            List<Anchor> anchors = CreateAnchors(document);
            var resolved = new List<ResolvedAnchor>();
            int unsupportedAnchorCount = 0;
            foreach (Anchor anchor in anchors)
            {
                if (!TryResolveAnchor(anchor, analyses, out ResolvedAnchor resolution))
                {
                    unsupportedAnchorCount++;
                    diagnostics.Add(new LevelPlayabilityDiagnostic(
                        LevelPlayabilityDiagnosticSeverity.Warning,
                        "playability.anchor.no-terrain",
                        $"{anchor.Label} has no heightfield beneath it; verify structural support manually.",
                        anchor.EntityId));
                    continue;
                }
                resolved.Add(resolution);
                if (anchor.Position.y < resolution.TerrainHeight - 0.25f)
                {
                    diagnostics.Add(new LevelPlayabilityDiagnostic(
                        LevelPlayabilityDiagnosticSeverity.Warning,
                        "playability.anchor.below-terrain",
                        $"{anchor.Label} is below terrain by "
                        + $"{resolution.TerrainHeight - anchor.Position.y:0.##} meters.",
                        anchor.EntityId));
                }
                else if (resolution.ComponentId < 0
                    && Math.Abs(anchor.Position.y - resolution.TerrainHeight)
                        <= TerrainSupportTolerance)
                {
                    diagnostics.Add(new LevelPlayabilityDiagnostic(
                        LevelPlayabilityDiagnosticSeverity.Warning,
                        "playability.anchor.steep",
                        $"{anchor.Label} is supported by terrain steeper than "
                        + $"{maximumWalkableSlopeDegrees:0.#} degrees.",
                        anchor.EntityId));
                }
            }

            ReportDisconnectedObjectives(resolved, diagnostics);
            ReportActorOverlaps(document.scenario?.actors, diagnostics);
            foreach (TerrainPlayabilityAnalysis analysis in analyses.Where(item =>
                item.Report.ConnectedRegionCount > 1))
            {
                diagnostics.Add(new LevelPlayabilityDiagnostic(
                    LevelPlayabilityDiagnosticSeverity.Info,
                    "playability.terrain.regions",
                    $"Terrain '{analysis.Report.SurfaceId}' has "
                    + $"{analysis.Report.ConnectedRegionCount} walkable heightfield regions; "
                    + "structures may connect them."));
            }

            return new LevelPlayabilityReport(
                maximumWalkableSlopeDegrees,
                maximumStepHeight,
                analyses.Select(item => item.Report).ToArray(),
                diagnostics.ToArray(),
                anchors.Count,
                unsupportedAnchorCount);
        }

        private static List<Anchor> CreateAnchors(LevelDocument document)
        {
            var anchors = new List<Anchor>();
            foreach (LevelScenarioActorData actor in document.scenario?.actors
                ?? Enumerable.Empty<LevelScenarioActorData>())
            {
                if (actor == null || !LevelValidationMath.IsFinite(actor.transform.position))
                    continue;
                string role = actor.playerControlled ? "Player actor" : "Scenario actor";
                anchors.Add(new Anchor(
                    string.Empty,
                    $"{role} '{actor.id}'",
                    actor.transform.position,
                    actor.initiallySelected && actor.playerControlled,
                    false));
            }

            var entities = document.entities
                .Where(entity => entity != null && !string.IsNullOrWhiteSpace(entity.id))
                .GroupBy(entity => entity.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (LevelScenarioObjectiveData objective in document.scenario?.objectives
                ?? Enumerable.Empty<LevelScenarioObjectiveData>())
            {
                if (objective == null
                    || !entities.TryGetValue(objective.entityId ?? string.Empty, out LevelEntity entity))
                {
                    continue;
                }
                InteractionPointData point = entity.interactionPoints.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate?.id,
                        objective.interactionPointId,
                        StringComparison.Ordinal));
                if (point == null)
                    continue;
                if (!LevelValidationMath.IsFinite(entity.transform.position)
                    || !LevelValidationMath.IsFinite(entity.transform.pitchDegrees)
                    || !LevelValidationMath.IsFinite(entity.transform.yawDegrees)
                    || !LevelValidationMath.IsFinite(entity.transform.rollDegrees)
                    || !LevelValidationMath.IsFinite(point.localPosition))
                {
                    continue;
                }
                anchors.Add(new Anchor(
                    entity.id,
                    $"Objective '{objective.displayName}'",
                    ToWorld(entity.transform, point.localPosition),
                    false,
                    true));
            }
            return anchors;
        }

        private static bool TryResolveAnchor(
            Anchor anchor,
            IReadOnlyList<TerrainPlayabilityAnalysis> surfaces,
            out ResolvedAnchor resolved)
        {
            TerrainPlayabilityAnalysis best = null;
            float bestHeight = 0f;
            float bestDistance = float.PositiveInfinity;
            foreach (TerrainPlayabilityAnalysis surface in surfaces)
            {
                if (!surface.TrySample(anchor.Position.x, anchor.Position.z, out float height))
                    continue;
                float distance = Math.Abs(anchor.Position.y - height);
                if (distance < bestDistance)
                {
                    best = surface;
                    bestHeight = height;
                    bestDistance = distance;
                }
            }
            if (best == null)
            {
                resolved = default;
                return false;
            }
            resolved = new ResolvedAnchor(
                anchor,
                best.Surface.id,
                bestHeight,
                best.ComponentAt(anchor.Position.x, anchor.Position.z));
            return true;
        }

        private static void ReportDisconnectedObjectives(
            IReadOnlyList<ResolvedAnchor> anchors,
            ICollection<LevelPlayabilityDiagnostic> diagnostics)
        {
            ResolvedAnchor? player = null;
            foreach (ResolvedAnchor anchor in anchors)
            {
                if (anchor.Anchor.IsPlayerStart)
                {
                    player = anchor;
                    break;
                }
            }
            if (!player.HasValue || player.Value.ComponentId < 0)
                return;
            foreach (ResolvedAnchor objective in anchors.Where(anchor => anchor.Anchor.IsObjective))
            {
                if (objective.ComponentId < 0
                    || !string.Equals(
                        objective.SurfaceId,
                        player.Value.SurfaceId,
                        StringComparison.Ordinal)
                    || objective.ComponentId != player.Value.ComponentId)
                {
                    diagnostics.Add(new LevelPlayabilityDiagnostic(
                        LevelPlayabilityDiagnosticSeverity.Warning,
                        "playability.objective.terrain-disconnected",
                        $"{objective.Anchor.Label} is not connected to the player through walkable "
                        + "heightfield samples; verify a structural route or adjust terrain.",
                        objective.Anchor.EntityId));
                }
            }
        }

        private static void ReportActorOverlaps(
            IEnumerable<LevelScenarioActorData> source,
            ICollection<LevelPlayabilityDiagnostic> diagnostics)
        {
            LevelScenarioActorData[] actors = source?.Where(actor => actor != null
                    && LevelValidationMath.IsFinite(actor.transform.position))
                .ToArray()
                ?? Array.Empty<LevelScenarioActorData>();
            for (int first = 0; first < actors.Length; first++)
            {
                for (int second = first + 1; second < actors.Length; second++)
                {
                    Float3Data a = actors[first].transform.position;
                    Float3Data b = actors[second].transform.position;
                    float dx = a.x - b.x;
                    float dz = a.z - b.z;
                    if (dx * dx + dz * dz <= ActorOverlapDistance * ActorOverlapDistance
                        && Math.Abs(a.y - b.y) <= ActorOverlapHeight)
                    {
                        diagnostics.Add(new LevelPlayabilityDiagnostic(
                            LevelPlayabilityDiagnosticSeverity.Warning,
                            "playability.actor.overlap",
                            $"Scenario actors '{actors[first].id}' and '{actors[second].id}' "
                            + "start in nearly the same space."));
                    }
                }
            }
        }

        private static Float3Data ToWorld(LevelTransformData transform, Float3Data local)
        {
            double radians = transform.yawDegrees * Math.PI / 180d;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            return new Float3Data(
                transform.position.x + (float)(local.x * cosine + local.z * sine),
                transform.position.y + local.y,
                transform.position.z + (float)(-local.x * sine + local.z * cosine));
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private readonly struct Anchor
        {
            public Anchor(
                string entityId,
                string label,
                Float3Data position,
                bool isPlayerStart,
                bool isObjective)
            {
                EntityId = entityId ?? string.Empty;
                Label = label ?? string.Empty;
                Position = position;
                IsPlayerStart = isPlayerStart;
                IsObjective = isObjective;
            }

            public string EntityId { get; }
            public string Label { get; }
            public Float3Data Position { get; }
            public bool IsPlayerStart { get; }
            public bool IsObjective { get; }
        }

        private readonly struct ResolvedAnchor
        {
            public ResolvedAnchor(
                Anchor anchor,
                string surfaceId,
                float terrainHeight,
                int componentId)
            {
                Anchor = anchor;
                SurfaceId = surfaceId ?? string.Empty;
                TerrainHeight = terrainHeight;
                ComponentId = componentId;
            }

            public Anchor Anchor { get; }
            public string SurfaceId { get; }
            public float TerrainHeight { get; }
            public int ComponentId { get; }
        }
    }
}
