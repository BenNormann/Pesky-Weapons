using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pesky.Editor
{
    /// <summary>
    /// Owns the Web player settings and the GitHub Pages build. It applies every Web setting
    /// from code (so nobody has to remember them), builds whatever the build settings list
    /// into &lt;repo&gt;/Builds/Web - where publish.ps1 expects it - and can also produce the
    /// development Windows player the two-instance test needs.
    ///
    /// Ported from ATCK's Editor/BuildTools.cs. The differences: the output folder (ATCK
    /// publishes from its repository root, Pesky from Builds/Web), the scene list (read from
    /// the build settings instead of hard-coded), the colour space (Pesky stays Linear) and
    /// the extra Windows menu item.
    ///
    /// CLI:
    ///   Unity.exe -batchmode -projectPath "&lt;repo&gt;/Pesky Weapons Unity" -buildTarget WebGL
    ///             -executeMethod Pesky.Editor.BuildTools.BuildFromCommandLine -logFile build.log
    /// </summary>
    public static class BuildTools
    {
        public const string CompanyName = "Ben Normann";
        public const string ProductName = "Pesky Weapons";
        public const string TemplateName = "PROJECT:Pesky";

        const long SizeBudgetBytes = 60L * 1024 * 1024;

        [MenuItem("Pesky/Apply Player Settings")]
        public static void ApplyPlayerSettingsMenu()
        {
            ApplyPlayerSettings();
            Debug.Log("[Pesky] player settings applied");
        }

        const string LastBuildEndKey = "Pesky.BuildTools.lastBuildEnd";
        const double MenuBuildCooldownSeconds = 300;

        [MenuItem("Pesky/Build Web")]
        public static void BuildMenu()
        {
            // An automation channel that times out can replay this menu item several
            // times in a row; a build is expensive, so one is allowed per cooldown and
            // never while another is running. SessionState survives the domain reload.
            if (BuildPipeline.isBuildingPlayer)
            {
                Debug.LogWarning("[Pesky] build skipped: a build is already running");
                return;
            }
            var lastEnd = SessionState.GetFloat(LastBuildEndKey, -1f);
            if (lastEnd >= 0f && EditorApplication.timeSinceStartup - lastEnd < MenuBuildCooldownSeconds)
            {
                Debug.LogWarning("[Pesky] build skipped: one finished less than five minutes ago");
                return;
            }
            var report = Build();
            SessionState.SetFloat(LastBuildEndKey, (float)EditorApplication.timeSinceStartup);
            if (report != null && report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(Path.Combine(WebOutputDir(), "index.html"));
        }

        [MenuItem("Pesky/Build Windows (two-player test)")]
        public static void BuildWindowsMenu()
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                Debug.LogWarning("[Pesky] build skipped: a build is already running");
                return;
            }
            var report = BuildWindows();
            if (report != null && report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(WindowsOutputPath());
        }

        /// <summary>Batch-mode entry point; exits the editor with 0 on success, 1 otherwise.</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                var report = Build();
                var ok = report != null && report.summary.result == BuildResult.Succeeded;
                EditorApplication.Exit(ok ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Pesky] build threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Applies the settings and builds every enabled scene in the build settings into
        /// &lt;repo&gt;/Builds/Web, which is the folder publish.ps1 copies to the Pages repository.
        /// </summary>
        public static BuildReport Build()
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                Debug.LogWarning("[Pesky] build skipped: a build is already running");
                return null;
            }

            if (!EnsureTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL)) return null;

            var scenes = BuildScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Pesky] the build settings list no usable scenes, not building");
                return null;
            }

            ApplyPlayerSettings();

            var outputDir = WebOutputDir();
            Directory.CreateDirectory(outputDir);
            EnsureNoJekyll(outputDir);
            Debug.Log($"[Pesky] building WebGL into {outputDir} ({scenes.Length} scenes)");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Pesky] build result: {summary.result}, size {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings");
            if (summary.result == BuildResult.Succeeded && summary.totalSize > SizeBudgetBytes)
                Debug.LogWarning($"[Pesky] the build is over the {SizeBudgetBytes / (1024 * 1024)} MB budget");
            return report;
        }

        /// <summary>
        /// A development Windows x64 player at &lt;repo&gt;/Builds/Windows/PeskyWeapons.exe: the
        /// second instance for the two-player test in docs/TEST-CHECKLIST.md. It leaves the
        /// active build target on Windows; Pesky/Build Web switches it back.
        /// </summary>
        public static BuildReport BuildWindows()
        {
            if (!EnsureTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)) return null;

            var scenes = BuildScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Pesky] the build settings list no usable scenes, not building");
                return null;
            }

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            // Two instances on one desktop both have to keep ticking while unfocused.
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            // Mono links in a minute where IL2CPP takes twenty, and this player is only ever a test.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();

            var path = WindowsOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Debug.Log($"[Pesky] building Windows x64 (development) into {path}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
            Debug.Log($"[Pesky] Windows build result: {report.summary.result}, " +
                      $"{report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings");
            return report;
        }

        /// <summary>
        /// The Web settings, applied from code so nobody has to remember them: Brotli with
        /// the decompression fallback (GitHub Pages cannot set Content-Encoding), Run In
        /// Background, WebGL 2 only, no threads, full exceptions with stack traces, engine
        /// stripping plus low managed stripping with Assets/link.xml, data caching and the
        /// Pesky template.
        ///
        /// The colour space is deliberately NOT touched. Pesky is Linear, which WebGL 2
        /// (OpenGLES3) supports; ATCK forces Gamma only because it was authored that way.
        /// </summary>
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;

            // GitHub Pages cannot set Content-Encoding; the fallback embeds a JS
            // decompressor so a Brotli build still loads there.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;

            // Critical: without this the tab pauses when unfocused and every
            // connection dies the moment a player alt-tabs to Discord.
            PlayerSettings.runInBackground = true;

            // WebGL2 only; WebGPU stays off. No COOP/COEP on Pages means no
            // SharedArrayBuffer, so threading stays off too.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.threadsSupport = false;

            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            // Size-tuned IL2CPP claws back a lot of the wasm that full stack
            // traces cost, without giving the traces up.
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = TemplateName;

            // Eight tumbling rigidbodies and a labyrinth want room to grow, but the page
            // should not ask the browser for all of it up front.
            PlayerSettings.WebGL.initialMemorySize = 64;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Every enabled scene in the build settings, in order: that list is the project's
        /// single source of truth (Boot, MainMenu, Tutorial, Labyrinth today), so adding a
        /// scene in the Build Profiles window is all it takes to ship it.
        /// </summary>
        public static string[] BuildScenes()
        {
            var list = new List<string>(8);
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                if (File.Exists(scene.path)) list.Add(scene.path);
                else Debug.LogError($"[Pesky] build scene file is missing: {scene.path}");
            }
            return list.ToArray();
        }

        /// <summary>
        /// True when the wanted platform is already the active build target. When it is not,
        /// it switches and returns false: switching reimports assets and reloads the domain,
        /// which would pull the ground out from under a build started in the same call, so
        /// the menu item is simply run twice.
        /// </summary>
        static bool EnsureTarget(BuildTargetGroup group, BuildTarget target)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target) return true;
            Debug.Log($"[Pesky] switching the active build target to {target}");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                Debug.LogError($"[Pesky] could not switch to {target} - is that platform module installed?");
                return false;
            }
            Debug.LogWarning($"[Pesky] active build target is now {target}; run the menu item again to build");
            return false;
        }

        static void EnsureNoJekyll(string dir)
        {
            var path = Path.Combine(dir, ".nojekyll");
            if (File.Exists(path)) return;
            File.WriteAllText(path, "");
            Debug.Log("[Pesky] wrote .nojekyll (Pages drops everything under Build/ without it)");
        }

        /// <summary>The repository root: two levels above Assets/ ("Pesky Weapons Unity" sits inside it).</summary>
        public static string RepoRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>&lt;repo&gt;/Builds/Web - exactly where publish.ps1 looks for index.html.</summary>
        public static string WebOutputDir() => Path.Combine(RepoRoot(), "Builds", "Web");

        /// <summary>&lt;repo&gt;/Builds/Windows/PeskyWeapons.exe - the two-player test player.</summary>
        public static string WindowsOutputPath() =>
            Path.Combine(RepoRoot(), "Builds", "Windows", "PeskyWeapons.exe");
    }
}
