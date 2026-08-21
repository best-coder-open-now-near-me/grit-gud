using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Defines encounter membership independently of how combat began. Both
    /// observation and committed actions supply only their directly involved
    /// actors; this resolver supplies the party and explicitly authored
    /// reinforcement closure in canonical initiative order.
    /// </summary>
    public static class GameplayEncounterScopeResolver
    {
        public static IReadOnlyList<string> Resolve(
            ScenarioDefinition scenario,
            IReadOnlyList<string> initiativeOrder,
            params string[] involvedActorIds)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (initiativeOrder == null)
                throw new ArgumentNullException(nameof(initiativeOrder));

            var scope = new HashSet<string>(StringComparer.Ordinal);
            if (scenario.PlayerParty != null)
            {
                foreach (string partyActorId in scenario.PlayerParty.ActorIds)
                    scope.Add(partyActorId);
            }

            foreach (string actorId in involvedActorIds
                ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(actorId)) continue;
                if (TryGetActor(scenario, actorId, out _))
                    scope.Add(actorId);
            }

            var pending = new Queue<string>(scope);
            while (pending.Count > 0)
            {
                ScenarioActorDefinition actor = scenario.GetActor(
                    pending.Dequeue());
                EnemyBehaviorDefinition behavior = actor.Combat.EnemyBehavior;
                if (behavior == null) continue;
                foreach (string reinforcementId in behavior.ReinforcementActorIds)
                {
                    if (TryGetActor(scenario, reinforcementId, out _)
                        && scope.Add(reinforcementId))
                    {
                        pending.Enqueue(reinforcementId);
                    }
                }
            }

            var ordered = new List<string>();
            foreach (string actorId in initiativeOrder)
                if (scope.Contains(actorId)) ordered.Add(actorId);
            return ordered.AsReadOnly();
        }

        private static bool TryGetActor(
            ScenarioDefinition scenario,
            string actorId,
            out ScenarioActorDefinition actor)
        {
            foreach (ScenarioActorDefinition candidate in scenario.Actors)
            {
                if (!string.Equals(candidate.Id, actorId,
                        StringComparison.Ordinal)) continue;
                actor = candidate;
                return true;
            }

            actor = null;
            return false;
        }
    }
}
