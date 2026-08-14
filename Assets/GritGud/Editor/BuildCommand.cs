using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace GritGud.Editor
{
    public static class BuildCommand
    {
        public static void BuildWindows()
        {
            Build(
                BuildTarget.StandaloneWindows64,
                "Builds/Windows/GritGud.exe",
                BuildOptions.None);
        }

        public static void BuildWebPreview()
        {
            Build(
                BuildTarget.WebGL,
                "Builds/Web",
                BuildOptions.None);
        }

        private static void Build(BuildTarget target, string outputPath, BuildOptions options)
        {
            DefaultActorAssetGenerator.EnsureGeneratedAssets();

            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                throw new InvalidOperationException(
                    $"Build support for {target} is not installed on this worker.");
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured for the build.");
            }

            string outputDirectory = Path.HasExtension(outputPath)
                ? Path.GetDirectoryName(outputPath)
                : outputPath;

            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = options,
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{target} build failed with result {report.summary.result}.");
            }
        }
    }
}
