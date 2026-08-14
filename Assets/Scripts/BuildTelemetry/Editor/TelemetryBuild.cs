using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FattoPrizzerva.BuildTelemetry.Editor
{
    public static class TelemetryBuild
    {
        private static readonly string[] TransientProjectFiles =
        {
            "Assets/System/_DefaultProjectParameters/PipelinesCustom/Universal Render Pipeline Asset.asset",
            "Assets/System/_DefaultProjectParameters/PipelinesCustom/UniversalRenderPipelineGlobalSettings.asset",
            "ProjectSettings/GraphicsSettings.asset",
            "ProjectSettings/ProjectSettings.asset",
            "Assets/Resources.meta",
            "Assets/Resources/PerformanceTestRunInfo.json",
            "Assets/Resources/PerformanceTestRunInfo.json.meta",
            "Assets/Resources/PerformanceTestRunSettings.json",
            "Assets/Resources/PerformanceTestRunSettings.json.meta"
        };

        private static Dictionary<string, byte[]> _transientFileSnapshots;
        private static string _projectRoot;
        private static bool _resourcesDirectoryExisted;

        [Serializable]
        private sealed class TelemetryBuildManifest
        {
            public int schemaVersion = 1;
            public string utcBuilt;
            public string unityVersion;
            public string applicationVersion;
            public string buildGuid;
            public string outputPath;
            public string buildType;
            public ulong totalSizeBytes;
            public string[] scenes;
        }

        public static void BuildWindows()
        {
            string outputPath = GetCommandLineValue("-telemetryBuildOutput");
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Missing required argument: -telemetryBuildOutput <path-to-exe>");

            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("Invalid build output path."));

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("There are no enabled scenes in Build Settings.");

            bool developmentBuild = HasCommandLineFlag("-telemetryDevelopmentBuild");
            BuildOptions buildOptions = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC;
            if (developmentBuild)
                buildOptions |= BuildOptions.Development;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = buildOptions,
                extraScriptingDefines = new[] { "BUILD_TELEMETRY" }
            };

            CaptureTransientProjectFiles();
            try
            {
                Debug.Log($"[BuildTelemetry] Building {scenes.Length} scenes to {outputPath}");
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                if (summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Telemetry build failed: {summary.result} ({summary.totalErrors} errors).");

                TelemetryBuildManifest manifest = new TelemetryBuildManifest
                {
                    utcBuilt = DateTime.UtcNow.ToString("O"),
                    unityVersion = Application.unityVersion,
                    applicationVersion = Application.version,
                    buildGuid = summary.guid.ToString(),
                    outputPath = outputPath,
                    buildType = developmentBuild ? "Development" : "Release",
                    totalSizeBytes = summary.totalSize,
                    scenes = scenes
                };
                File.WriteAllText(
                    Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, "telemetry-build.json"),
                    JsonUtility.ToJson(manifest, true),
                    new UTF8Encoding(false));

                Debug.Log($"[BuildTelemetry] Build completed ({summary.totalSize} bytes): {outputPath}");
            }
            finally
            {
                RestoreTransientProjectFiles();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void CaptureTransientProjectFiles()
        {
            _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _resourcesDirectoryExisted = Directory.Exists(Path.Combine(_projectRoot, "Assets", "Resources"));
            _transientFileSnapshots = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            foreach (string relativePath in TransientProjectFiles)
            {
                string fullPath = Path.Combine(_projectRoot, relativePath);
                _transientFileSnapshots[relativePath] = File.Exists(fullPath)
                    ? File.ReadAllBytes(fullPath)
                    : null;
            }

            EditorApplication.quitting -= RestoreTransientProjectFiles;
            EditorApplication.quitting += RestoreTransientProjectFiles;
        }

        private static void RestoreTransientProjectFiles()
        {
            if (_transientFileSnapshots == null || string.IsNullOrEmpty(_projectRoot))
                return;

            foreach (KeyValuePair<string, byte[]> snapshot in _transientFileSnapshots)
            {
                string fullPath = Path.Combine(_projectRoot, snapshot.Key);
                try
                {
                    if (snapshot.Value == null)
                    {
                        if (File.Exists(fullPath))
                            File.Delete(fullPath);
                        continue;
                    }

                    if (!File.Exists(fullPath) || !File.ReadAllBytes(fullPath).SequenceEqual(snapshot.Value))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? _projectRoot);
                        File.WriteAllBytes(fullPath, snapshot.Value);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[BuildTelemetry] Could not restore transient build file '{snapshot.Key}'.\n{exception}");
                }
            }

            string resourcesDirectory = Path.Combine(_projectRoot, "Assets", "Resources");
            if (!_resourcesDirectoryExisted &&
                Directory.Exists(resourcesDirectory) &&
                !Directory.EnumerateFileSystemEntries(resourcesDirectory).Any())
            {
                Directory.Delete(resourcesDirectory, false);
            }
        }

        private static string GetCommandLineValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }

            return string.Empty;
        }

        private static bool HasCommandLineFlag(string name)
        {
            return Environment.GetCommandLineArgs().Any(
                argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
