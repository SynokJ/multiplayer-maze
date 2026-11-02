using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the two programs from the one project: the WebGL client (served by nginx)
/// and the headless Linux room server (started by the lobby). Needs the matching
/// Unity Hub modules: WebGL Build Support and Linux Dedicated Server Build Support.
/// </summary>
public static class BuildTools
{
    private const string _WEBGL_PATH = "Builds/WebGL";
    private const string _SERVER_PATH = "Builds/LinuxServer/MazeServer.x86_64";

    [MenuItem("Maze/Build/WebGL Client")]
    public static void BuildWebGL() => Build(BuildTarget.WebGL, null, _WEBGL_PATH);

    [MenuItem("Maze/Build/Linux Room Server")]
    public static void BuildLinuxServer()
        => Build(BuildTarget.StandaloneLinux64, (int)StandaloneBuildSubtarget.Server, _SERVER_PATH);

    private static void Build(BuildTarget target, int? subtarget, string path)
    {
        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled)
                scenes.Add(scene.path);

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = path,
            target = target,
            options = BuildOptions.None
        };

        if (subtarget.HasValue)
            options.subtarget = subtarget.Value;

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"{target} build: {summary.result}, {summary.totalErrors} error(s), {summary.totalSize / (1024 * 1024)} MB -> {path}");

        // In batch mode the exit code tells the script whether the build worked.
        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
