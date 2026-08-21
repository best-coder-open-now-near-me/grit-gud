using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayAttackResponseAssembler
    {
        internal static IReadOnlyList<AttackResponseDefinition> Create(
            IReadOnlyList<ScenarioActorContentData> actors,
            IReadOnlyList<ScenarioPropContentData> props,
            IReadOnlyList<ScenarioVehicleContentData> vehicles)
        {
            var responses = new List<AttackResponseDefinition>();
            foreach (ScenarioActorContentData actor in actors)
            {
                Add(responses, actor.id, actor.attackResponse);
            }

            foreach (ScenarioPropContentData prop in props)
            {
                Add(responses, prop.entityId, prop.attackResponse);
            }

            foreach (ScenarioVehicleContentData vehicle in vehicles)
            {
                Add(responses, vehicle.entityId, vehicle.attackResponse);
            }

            return responses;
        }

        private static void Add(
            ICollection<AttackResponseDefinition> responses,
            string targetId,
            ScenarioAttackResponseData response)
        {
            if (response != null)
            {
                responses.Add(new AttackResponseDefinition(
                    targetId,
                    response.startsEncounter));
            }
        }
    }
}
