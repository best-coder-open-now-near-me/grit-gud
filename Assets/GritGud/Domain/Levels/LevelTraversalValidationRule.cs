using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Levels
{
    public sealed class LevelTraversalValidationRule : ILevelValidationRule
    {
        public void Evaluate(LevelValidationContext context)
        {
            IReadOnlyList<LevelTraversalLinkData> links =
                context.Document.traversalLinks;
            if (links.Count > LevelDocument.MaximumTraversalLinkCount)
            {
                context.Error(
                    "traversal.links.limit",
                    $"The level contains {links.Count} traversal links; the limit is "
                    + $"{LevelDocument.MaximumTraversalLinkCount}.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelTraversalLinkData link in links)
            {
                if (link == null)
                {
                    context.Error(
                        "traversal.link.missing",
                        "The traversal-link list contains an empty entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(link.id) || !ids.Add(link.id))
                {
                    context.Error(
                        "traversal.link.id",
                        "Traversal-link IDs must be present and unique.",
                        link.id);
                }
                if (string.IsNullOrWhiteSpace(link.actionId))
                {
                    context.Error(
                        "traversal.link.action",
                        $"Traversal link '{link.id}' needs a stable action ID.",
                        link.id);
                }
                if (link.kind != LevelTraversalLinkData.JumpKind
                    && link.kind != LevelTraversalLinkData.VaultKind
                    && link.kind != LevelTraversalLinkData.MantleKind)
                {
                    context.Error(
                        "traversal.link.kind",
                        $"Traversal link '{link.id}' must be jump, vault, or mantle.",
                        link.id);
                }
                if (!LevelValidationMath.IsFinite(link.takeoff)
                    || !LevelValidationMath.IsFinite(link.landing)
                    || DistanceSquared(link.takeoff, link.landing) <= 0.0001f)
                {
                    context.Error(
                        "traversal.link.endpoints",
                        $"Traversal link '{link.id}' needs distinct finite endpoints.",
                        link.id);
                }
                else if (!LevelValidationMath.Contains(
                        context.Document.bounds,
                        link.takeoff)
                    || !LevelValidationMath.Contains(
                        context.Document.bounds,
                        link.landing))
                {
                    context.Error(
                        "traversal.link.outside-bounds",
                        $"Traversal link '{link.id}' leaves the authored level bounds.",
                        link.id);
                }
                if (!PositiveFinite(link.activationRadius)
                    || !PositiveFinite(link.movementCost)
                    || link.actionPointCost < 0
                    || !NonNegativeFinite(link.arcHeight)
                    || !PositiveFinite(link.playbackDurationSeconds)
                    || !NonNegativeFinite(link.clearancePadding))
                {
                    context.Error(
                        "traversal.link.cost-or-trajectory",
                        $"Traversal link '{link.id}' has invalid activation, cost, arc, duration, or clearance values.",
                        link.id);
                }
            }
        }

        private static float DistanceSquared(Float3Data left, Float3Data right)
        {
            float x = right.x - left.x;
            float y = right.y - left.y;
            float z = right.z - left.z;
            return (x * x) + (y * y) + (z * z);
        }

        private static bool PositiveFinite(float value) =>
            LevelValidationMath.IsFinite(value) && value > 0f;

        private static bool NonNegativeFinite(float value) =>
            LevelValidationMath.IsFinite(value) && value >= 0f;
    }
}
