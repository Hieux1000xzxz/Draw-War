using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArtWar.EditorTools
{
    // One WebGL upload that loads fast on both desktops and phones.
    //
    // Unity bakes one texture format into the build's .data file. Desktop GPUs read DXT, phones read
    // ASTC, and a browser handed the format it lacks unpacks every texture on the CPU at load - slow,
    // and several times the memory, which is what kills the game on a phone. So this builds twice and
    // ships both data files side by side: <name>.data (DXT) and <name>-astc.data (ASTC). The code is
    // the same for both; the DrawWar WebGL template asks the GPU which it reads and fetches that one.
    public static class WebGLDualBuild
    {
        private const string DefaultFolder = "Builds/WebGL";

        [MenuItem("Tools/Build/WebGL (Desktop + Mobile textures)", priority = 1)]
        private static void Build()
        {
            string output = EditorUtility.SaveFolderPanel("WebGL build folder", Path.GetDirectoryName(Path.GetFullPath(DefaultFolder)), "WebGL");

            if (!string.IsNullOrEmpty(output))
                BuildTo(output);
        }

        // The same build without the folder dialog, for scripted builds. True when both passes succeed.
        public static bool BuildTo(string output)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("WebGL build", "No scenes are enabled in Build Settings.", "OK");
                return false;
            }

            ApplyWebSettings();

            WebGLTextureSubtarget previous = EditorUserBuildSettings.webGLBuildSubtarget;
            string                astcTemp = output.TrimEnd('/', '\\') + "_astc_tmp";

            // Unity builds over an existing folder without clearing it, so a phone data file from an
            // earlier build would sit next to this build's code - and be picked up as the desktop one.
            DeleteOldAstcData(Path.Combine(output, "Build"));

            try
            {
                if (!BuildWith(WebGLTextureSubtarget.ASTC, scenes, astcTemp) ||
                    !BuildWith(WebGLTextureSubtarget.DXT, scenes, output))
                    return false;

                string dxtData  = FindDataFile(Path.Combine(output, "Build"));
                string astcData = FindDataFile(Path.Combine(astcTemp, "Build"));

                if (dxtData == null || astcData == null)
                {
                    Debug.LogError("WebGLDualBuild: could not find the .data files to pair up.");
                    return false;
                }

                // "Game.data.gz" -> "Game-astc.data.gz": the name the template asks for.
                string name   = Path.GetFileName(dxtData);
                int    dot    = name.IndexOf(".data", System.StringComparison.Ordinal);
                string target = Path.Combine(output, "Build", name.Substring(0, dot) + "-astc" + name.Substring(dot));

                File.Copy(astcData, target, true);

                Debug.Log($"WebGLDualBuild: done. Upload a zip of {output} - desktop loads {name}, phones load {Path.GetFileName(target)}.");
                EditorUtility.RevealInFinder(output);
                return true;
            }
            finally
            {
                EditorUserBuildSettings.webGLBuildSubtarget = previous;

                if (Directory.Exists(astcTemp))
                    FileUtil.DeleteFileOrDirectory(astcTemp);
            }
        }

        // The web player settings this game ships with: its own page template (GPU texture pick, phone
        // fullscreen and rotate hint), Brotli files that still load on hosts without the right headers,
        // cached data so a second visit starts at once, and a landscape 16:9 window on desktop.
        private static void ApplyWebSettings()
        {
            PlayerSettings.WebGL.template = "PROJECT:DrawWar";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true; // online: the other player keeps playing while this tab is in the back
        }

        private static bool BuildWith(WebGLTextureSubtarget subtarget, string[] scenes, string folder)
        {
            // Unity 6 takes the web texture format from Player Settings' format list, not the
            // subtarget, so both are set for this pass - and the list is put back afterwards.
            System.Array savedFormats = GetFormats();

            EditorUserBuildSettings.webGLBuildSubtarget = subtarget;
            SetFormats(subtarget == WebGLTextureSubtarget.ASTC ? "ASTC" : "DXTC");

            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes           = scenes,
                    locationPathName = folder,
                    target           = BuildTarget.WebGL,
                    options          = BuildOptions.None
                });

                if (report.summary.result == BuildResult.Succeeded)
                    return true;

                Debug.LogError($"WebGLDualBuild: {subtarget} build {report.summary.result}.");
                return false;
            }
            finally
            {
                if (savedFormats != null)
                    SetFormats(savedFormats);
            }
        }

        // PlayerSettings' texture-format list has no public API for the web target; these reach the
        // internal accessors the Player Settings window itself uses.
        private const BindingFlags Internal = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static System.Array GetFormats()
        {
            MethodInfo get = typeof(PlayerSettings).GetMethod("GetTextureCompressionFormatsImpl", Internal);
            return get?.Invoke(null, new object[] { BuildTarget.WebGL }) as System.Array;
        }

        private static void SetFormats(string format)
        {
            MethodInfo set = typeof(PlayerSettings).GetMethod("SetTextureCompressionFormatsImpl", Internal);

            if (set == null)
            {
                Debug.LogWarning("WebGLDualBuild: this Unity version has no texture format list; relying on the subtarget alone.");
                return;
            }

            System.Type  enumType = set.GetParameters()[1].ParameterType.GetElementType();
            System.Array formats  = System.Array.CreateInstance(enumType, 1);
            formats.SetValue(System.Enum.Parse(enumType, format), 0);

            SetFormats(formats);
        }

        private static void SetFormats(System.Array formats)
        {
            typeof(PlayerSettings).GetMethod("SetTextureCompressionFormatsImpl", Internal)?
                .Invoke(null, new object[] { BuildTarget.WebGL, formats });
        }

        // The build's own data file - never a "-astc" copy, which sorts before it ('-' < '.').
        private static string FindDataFile(string buildFolder)
        {
            return Directory.Exists(buildFolder)
                ? Directory.GetFiles(buildFolder).FirstOrDefault(f =>
                    Path.GetFileName(f).Contains(".data") && !Path.GetFileName(f).Contains("-astc"))
                : null;
        }

        private static void DeleteOldAstcData(string buildFolder)
        {
            if (!Directory.Exists(buildFolder))
                return;

            foreach (string file in Directory.GetFiles(buildFolder))
            {
                if (Path.GetFileName(file).Contains("-astc"))
                    File.Delete(file);
            }
        }
    }
}
