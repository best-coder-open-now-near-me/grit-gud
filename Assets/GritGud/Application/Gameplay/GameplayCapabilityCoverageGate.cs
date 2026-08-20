using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    public static class GameplayCapabilityCoverageGate
    {
        public static GameplayCapabilityCoverageReport ValidateCurrent(
            GameplayScenarioAssembly assembly,
            LevelDocument level)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (level == null) throw new ArgumentNullException(nameof(level));
            GameplayTransitionReducerRegistry reducers =
                GameplaySimulationReducers.CreateCurrent();
            GameplayCapabilityRegistry capabilities =
                GameplayCurrentCapabilityCatalog.Create(
                    reducers,
                    assembly,
                    level);
            return GameplayCapabilityCoverageValidator.Validate(
                assembly,
                level,
                capabilities);
        }

        public static GameplayCapabilityCoverageReport RequireCurrent(
            GameplayScenarioAssembly assembly,
            LevelDocument level)
        {
            GameplayCapabilityCoverageReport report = ValidateCurrent(
                assembly,
                level);
            report.RequireComplete(assembly.Scenario.Id);
            IReadOnlyList<GameplayReachableInput> reachable =
                GameplayReachableInputEnumerator.Enumerate(assembly, level);
            GameplayTransitionReducerRegistry reducers =
                GameplaySimulationReducers.CreateCurrent();
            GameplayCapabilityRegistry capabilities =
                GameplayCurrentCapabilityCatalog.Create(reducers, reachable);
            var spatial = new GameplayHeadlessSpatialEvidence(
                level,
                new SpatialContentIdentity(
                    level.levelId,
                    level.schemaVersion,
                    evidenceAlgorithmVersion: 1,
                    new string('0', 64)));
            GameplayExecutableRouteCoverageValidator.Validate(
                reachable,
                GameplayCurrentCandidateExecutionRoutes.Create(
                    assembly,
                    spatial,
                    capabilities)).RequireComplete();
            GameplayTacticalRuleSupportRegistry tacticalSupport =
                GameplayCurrentTacticalRuleSupport.Create(
                    assembly.TacticalRules,
                    "UnityTacticalContextQuery");
            GameplayTacticalRuleCoverageReport tacticalReport =
                GameplayTacticalRuleCoverageValidator.Validate(
                    assembly.TacticalRules,
                    reachable,
                    tacticalSupport);
            tacticalReport.RequireComplete(assembly.Scenario.Id);
            return report;
        }
    }
}
