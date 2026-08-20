using System;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// The sole production composition root for executable semantic routes.
    /// Content validation, headless battles, live controllers, and replay
    /// verification must all request this registry instead of assembling a
    /// partial private route list.
    /// </summary>
    public static class GameplayCurrentCandidateExecutionRoutes
    {
        public static GameplayCandidateExecutionRouteRegistry Create(
            GameplayScenarioAssembly assembly,
            GameplayHeadlessSpatialEvidence spatial,
            GameplayCapabilityRegistry capabilities)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (spatial == null)
                throw new ArgumentNullException(nameof(spatial));
            if (capabilities == null)
                throw new ArgumentNullException(nameof(capabilities));

            var routes = new GameplayCandidateExecutionRouteRegistry(
                capabilities);
            routes.Register(new GameplayGroundedMoveCandidateExecutionRoute(
                assembly.Scenario,
                spatial));
            routes.Register(new GameplayTraversalCandidateExecutionRoute(
                assembly.Scenario,
                spatial));
            routes.Register(new GameplayDroneMoveCandidateExecutionRoute(
                assembly.Scenario,
                spatial));
            routes.Register(new GameplayStanceCandidateExecutionRoute());
            routes.Register(new GameplayEquipmentCandidateExecutionRoute(
                assembly.Scenario));
            routes.Register(new GameplayActorAttackCandidateExecutionRoute(
                assembly,
                spatial));
            routes.Register(new GameplayDirectAttackCandidateExecutionRoute(
                assembly,
                spatial));
            routes.Register(new GameplayActorDroneAttackCandidateExecutionRoute(
                assembly,
                spatial));
            routes.Register(new GameplayDroneAttackCandidateExecutionRoute(
                assembly,
                spatial));
            routes.Register(
                new GameplayProjectileLaunchCandidateExecutionRoute(
                    assembly,
                    spatial));
            routes.Register(
                new GameplayProjectileAdvanceCandidateExecutionRoute(spatial));
            routes.Register(
                new GameplayThrownExplosiveCandidateExecutionRoute(
                    assembly,
                    spatial));
            routes.Register(new GameplayDisplacementCandidateExecutionRoute(
                assembly,
                spatial));
            routes.Register(new GameplayInteractionCandidateExecutionRoute());
            routes.Register(new GameplayEndTurnCandidateExecutionRoute(
                assembly.Scenario));
            routes.Register(new GameplayEncounterObservationCandidateExecutionRoute(
                assembly.Scenario,
                spatial));
            routes.Register(new GameplayPatrolCandidateExecutionRoute(
                assembly.Scenario,
                spatial));
            routes.Register(new GameplayLifecycleCandidateExecutionRoute(
                assembly.Scenario));
            return routes;
        }
    }
}
