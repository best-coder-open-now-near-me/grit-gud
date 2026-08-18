using System;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public static class GameplayCameraOcclusionRules
    {
        private const string WallArchetypePrefix = "structure.wall.";

        public static bool UsesPlayerCutout(string archetypeId)
        {
            return !string.IsNullOrEmpty(archetypeId)
                && archetypeId.StartsWith(
                    WallArchetypePrefix,
                    StringComparison.Ordinal);
        }

        public static bool ShouldMoveCamera(Collider collider, Transform player)
        {
            return ShouldMoveCamera(
                collider,
                player,
                default,
                0f);
        }

        public static bool ShouldMoveCamera(
            Collider collider,
            Transform player,
            Vector3 desiredCameraPosition,
            float cameraClearance)
        {
            if (collider == null)
            {
                return false;
            }

            Transform source = collider.transform;
            if (player != null
                && (source == player || source.IsChildOf(player)))
            {
                return false;
            }

            LevelEntityView entity = source.GetComponentInParent<LevelEntityView>();
            if (entity == null || !UsesPlayerCutout(entity.ArchetypeId))
            {
                return true;
            }

            if (cameraClearance <= 0f)
            {
                return false;
            }

            Vector3 closestPoint = collider.ClosestPoint(
                desiredCameraPosition);
            return (closestPoint - desiredCameraPosition).sqrMagnitude
                < cameraClearance * cameraClearance;
        }
    }
}
