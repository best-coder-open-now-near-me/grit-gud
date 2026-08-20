using System;
using System.IO;
using System.Text.Json;
using GritGud.Application.Gameplay;
using GritGud.Application.Levels;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

internal static class SimulationRepositoryContent
{
    public static void LoadDepot(
        out GameplayScenarioAssembly assembly,
        out LevelDocument level)
    {
        string contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "Assets",
            "GritGud",
            "Content",
            "Resources");
        JsonSerializerOptions json = CreateJsonOptions();
        ScenarioContentDocument scenario = ReadJson<ScenarioContentDocument>(
            Path.Combine(contentRoot, "Scenarios", "depot-yard.json"),
            json);
        level = new LevelDocumentMigrator().MigrateToCurrent(
            ReadJson<LevelDocument>(
                Path.Combine(
                    contentRoot,
                    "Levels",
                    "Published",
                    "main-level.json"),
                json));
        scenario.Normalize();
        level.Normalize();
        GameplayScenarioAssembly authored = new GameplayScenarioAssembler()
            .Assemble(scenario, level);
        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            GameplayCanonicalValueDigest.Calculate(level));
        assembly = GameplayHeadlessScenarioGrounding.Resolve(
            authored,
            new GameplayHeadlessSpatialEvidence(level, spatialIdentity));
    }

    public static string FindRepositoryRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(
            Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "ProjectSettings",
                    "ProjectVersion.txt")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException(
            "The simulation tool could not locate the repository root.");
    }

    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
    };

    private static T ReadJson<T>(
        string path,
        JsonSerializerOptions options)
    {
        T value = JsonSerializer.Deserialize<T>(
            File.ReadAllText(path),
            options);
        return value == null
            ? throw new InvalidOperationException(
                "Content file '" + path + "' did not deserialize.")
            : value;
    }
}
