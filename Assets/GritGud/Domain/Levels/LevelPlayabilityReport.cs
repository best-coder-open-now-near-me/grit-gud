using System;
using System.Collections.Generic;
using System.Linq;

namespace GritGud.Domain.Levels
{
    public enum LevelPlayabilityDiagnosticSeverity
    {
        Info,
        Warning,
    }

    public sealed class LevelPlayabilityDiagnostic
    {
        public LevelPlayabilityDiagnostic(
            LevelPlayabilityDiagnosticSeverity severity,
            string code,
            string message,
            string entityId = null)
        {
            Severity = severity;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            EntityId = entityId ?? string.Empty;
        }

        public LevelPlayabilityDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string EntityId { get; }
    }

    public sealed class TerrainPlayabilitySurfaceReport
    {
        internal TerrainPlayabilitySurfaceReport(
            string surfaceId,
            int sampleCount,
            int walkableSampleCount,
            int connectedRegionCount,
            float maximumSlopeDegrees)
        {
            SurfaceId = surfaceId ?? string.Empty;
            SampleCount = sampleCount;
            WalkableSampleCount = walkableSampleCount;
            ConnectedRegionCount = connectedRegionCount;
            MaximumSlopeDegrees = maximumSlopeDegrees;
        }

        public string SurfaceId { get; }
        public int SampleCount { get; }
        public int WalkableSampleCount { get; }
        public int ConnectedRegionCount { get; }
        public float MaximumSlopeDegrees { get; }
        public float WalkablePercent => SampleCount == 0
            ? 0f
            : WalkableSampleCount * 100f / SampleCount;
    }

    public sealed class LevelPlayabilityReport
    {
        internal LevelPlayabilityReport(
            float maximumWalkableSlopeDegrees,
            float maximumStepHeight,
            IReadOnlyList<TerrainPlayabilitySurfaceReport> surfaces,
            IReadOnlyList<LevelPlayabilityDiagnostic> diagnostics,
            int anchorCount,
            int unsupportedAnchorCount)
        {
            MaximumWalkableSlopeDegrees = maximumWalkableSlopeDegrees;
            MaximumStepHeight = maximumStepHeight;
            Surfaces = surfaces ?? Array.Empty<TerrainPlayabilitySurfaceReport>();
            Diagnostics = diagnostics ?? Array.Empty<LevelPlayabilityDiagnostic>();
            AnchorCount = anchorCount;
            UnsupportedAnchorCount = unsupportedAnchorCount;
        }

        public float MaximumWalkableSlopeDegrees { get; }
        public float MaximumStepHeight { get; }
        public IReadOnlyList<TerrainPlayabilitySurfaceReport> Surfaces { get; }
        public IReadOnlyList<LevelPlayabilityDiagnostic> Diagnostics { get; }
        public int AnchorCount { get; }
        public int UnsupportedAnchorCount { get; }
        public int WarningCount => Diagnostics.Count(item =>
            item.Severity == LevelPlayabilityDiagnosticSeverity.Warning);
    }
}
