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
            return entity == null || !UsesPlayerCutout(entity.ArchetypeId);
        }
    }
}
