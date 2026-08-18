using System;
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
            return report;
        }
    }
}
