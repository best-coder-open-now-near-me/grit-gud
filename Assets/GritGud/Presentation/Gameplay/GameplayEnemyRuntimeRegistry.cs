using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEnemyRuntimeRegistry : IDisposable
    {
        internal sealed class Entry
        {
            public Entry(
                ScenarioActorDefinition definition,
                GameplayActorView view,
                GameplayEnemyActorPresenter presentation,
                UnityEnemyTacticalQuery tacticalQuery)
            {
                Definition = definition;
                View = view;
                Presentation = presentation;
                TacticalQuery = tacticalQuery;
            }

            public ScenarioActorDefinition Definition { get; }

            public GameplayActorView View { get; }

            public GameplayEnemyActorPresenter Presentation { get; }

            public UnityEnemyTacticalQuery TacticalQuery { get; }

            public MovementRoutePlaybackPresenter Playback =>
                Presentation.Playback;

            public PatrolAdvanceRecord PendingPatrolAdvance { get; set; }
        }

        private readonly Dictionary<string, Entry> enemies =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<string> orderedEnemyIds = new List<string>();

        public GameplayEnemyRuntimeRegistry(
            GameplaySession session,
            GameplayWorldRegistry worldRegistry,
            GameplayAttackController attackController,
            GameplayProjectileController projectileController,
            EnemyPresentationCatalog presentationCatalog,
            ISightObscuranceQuery obscuranceQuery,
            IEnumerable<LevelTraversalLinkData> traversalLinks)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (worldRegistry == null)
                throw new ArgumentNullException(nameof(worldRegistry));
            if (attackController == null)
                throw new ArgumentNullException(nameof(attackController));
            if (projectileController == null)
                throw new ArgumentNullException(nameof(projectileController));
            if (presentationCatalog == null)
                throw new ArgumentNullException(nameof(presentationCatalog));

            foreach (ScenarioActorDefinition definition in
                session.Scenario.Actors)
            {
                if (definition.Combat.EnemyBehavior == null)
                    continue;
                GameplayActorView view = worldRegistry.GetActor(definition.Id);
                var presentation = new GameplayEnemyActorPresenter(
                    session,
                    worldRegistry,
                    attackController,
                    projectileController,
                    definition,
                    view,
                    presentationCatalog.Get(view.PresentationId));
                var query = new UnityEnemyTacticalQuery(
                    session,
                    worldRegistry,
                    definition,
                    view,
                    obscuranceQuery,
                    traversalLinks);
                enemies.Add(
                    definition.Id,
                    new Entry(definition, view, presentation, query));
                orderedEnemyIds.Add(definition.Id);
            }
        }

        public int Count => enemies.Count;

        public IEnumerable<Entry> Entries => enemies.Values;

        public IEnumerable<Entry> OrderedEntries
        {
            get
            {
                foreach (string enemyId in orderedEnemyIds)
                    yield return enemies[enemyId];
            }
        }

        public bool TryGet(string actorId, out Entry enemy) =>
            enemies.TryGetValue(actorId ?? string.Empty, out enemy);

        public void Dispose()
        {
            foreach (Entry enemy in enemies.Values)
            {
                enemy.PendingPatrolAdvance = null;
                enemy.Presentation.Dispose();
            }
            enemies.Clear();
            orderedEnemyIds.Clear();
        }
    }
}
