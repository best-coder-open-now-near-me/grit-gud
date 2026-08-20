using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public static class GameplayHeadlessScenarioGrounding
    {
        public static GameplayScenarioAssembly Resolve(
            GameplayScenarioAssembly assembly,
            GameplayHeadlessSpatialEvidence spatial)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            var poses = new Dictionary<string, GameplayActorPose>(
                StringComparer.Ordinal);
            foreach (ScenarioActorRuntimeDefinition actor in assembly.Actors)
            {
                GameplayActorPose authored = actor.GameplayDefinition
                    .StartingPose;
                poses.Add(
                    actor.Id,
                    new GameplayActorPose(
                        spatial.ResolveSpawnPosition(authored.Position),
                        authored.FacingDegrees,
                        authored.Stance));
            }
            return assembly.WithResolvedActorPoses(poses);
        }
    }
}
