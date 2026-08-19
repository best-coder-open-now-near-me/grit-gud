using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnemyCommittedActionSoundQuery :
        IGameplayCommittedActionSoundQuery
    {
        private readonly GameplayEnemyRuntimeRegistry enemies;

        public GameplayEnemyCommittedActionSoundQuery(
            GameplayEnemyRuntimeRegistry enemyRegistry)
        {
            enemies = enemyRegistry ?? throw new ArgumentNullException(
                nameof(enemyRegistry));
        }

        public EncounterSoundEvidence Capture(
            string observerActorId,
            string sourceActorId,
            GameplayPosition origin,
            float soundSignature)
        {
            if (!enemies.TryGet(observerActorId, out var observer))
                throw new InvalidOperationException(
                    $"Enemy '{observerActorId}' has no installed tactical sound query.");
            return observer.TacticalQuery.CaptureSound(
                sourceActorId,
                origin,
                soundSignature);
        }
    }
}
