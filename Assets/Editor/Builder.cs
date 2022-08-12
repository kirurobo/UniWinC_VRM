using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kirurobo
{
    public static class Builder
    {
        [MenuItem("Build/OSX")]
        public static void BuildOSX()
        {
            Build("macOS", BuildTarget.StandaloneOSX);
        }

        [MenuItem("Build/Windows64")]
        public static void BuildWindows64()
        {
            Build("Win64", BuildTarget.StandaloneWindows64, ".exe");
        }

        public static void Build(string label, BuildTarget target, string binaryExtension = "")
        {
            var appName = PlayerSettings.productName;

            var projectDir = Directory.GetCurrentDirectory();
            var buildDir = Path.Join(projectDir, "Build", label);
            var outputPath = Path.Join(buildDir, appName + binaryExtension);

            var buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            buildPlayerOptions.locationPathName = outputPath;
            buildPlayerOptions.target = target;
            buildPlayerOptions.options = BuildOptions.None;
            
            Debug.Log($"Building {outputPath}");

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build succeeded: {report.summary.totalSize} bytes");
            }
            else if (report.summary.result == BuildResult.Failed)
            {
                Debug.Log($"Build failed: {report.summary.totalErrors} errors");
            }
        }
    }
}