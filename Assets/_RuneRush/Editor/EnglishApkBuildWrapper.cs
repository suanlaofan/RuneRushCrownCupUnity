using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RuneRush.Editor
{
    /// <summary>
    /// 英文版 APK 构建包装：复用工程现有场景列表与 Player 设置，
    /// 不重跑 PicoBuildConfigurator 的 XR/PICO 配置，也不改写版本号。
    /// 输出路径由环境变量 RUNE_RUSH_EN_APK_PATH 指定。
    /// </summary>
    public static class EnglishApkBuildWrapper
    {
        private const string OutputEnvironmentVariable = "RUNE_RUSH_EN_APK_PATH";
        private const string KeystorePasswordEnvironmentVariable = "RUNE_RUSH_ANDROID_KEYSTORE_PASSWORD";

        public static void BuildEnglishApk()
        {
            string outputPath = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new InvalidOperationException($"{OutputEnvironmentVariable} is not set.");
            }

            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException("Unable to switch the active build target to Android.");
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are present in EditorBuildSettings.");
            }

            // 签名：工程配置了 keystore 且文件存在、密码可用时使用；否则临时改用 debug 签名，构建后恢复原设置。
            bool originalUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            bool originalBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            string signing;
            string keystorePath = ResolveKeystorePath(PlayerSettings.Android.keystoreName);
            string keystorePassword = Environment.GetEnvironmentVariable(KeystorePasswordEnvironmentVariable);
            if (originalUseCustomKeystore && !string.IsNullOrEmpty(keystorePath) && File.Exists(keystorePath) &&
                !string.IsNullOrWhiteSpace(keystorePassword))
            {
                PlayerSettings.Android.keystorePass = keystorePassword;
                PlayerSettings.Android.keyaliasPass = keystorePassword;
                signing = $"custom keystore {keystorePath} alias {PlayerSettings.Android.keyaliasName}";
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                signing = "debug (custom keystore " +
                          (string.IsNullOrEmpty(keystorePath) || !File.Exists(keystorePath ?? string.Empty)
                              ? "file missing"
                              : "present but password env not set") + ")";
            }

            Debug.Log($"RUNE_RUSH_EN_BUILD_SIGNING: {signing}");
            Debug.Log($"RUNE_RUSH_EN_BUILD_SCENES: {string.Join(", ", scenes)}");
            Debug.Log($"RUNE_RUSH_EN_BUILD_VERSION: {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");

            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.CompressWithLz4HC
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log($"RUNE_RUSH_EN_BUILD_RESULT: {report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings}");
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException($"Android build failed: {report.summary.result}");
                }

                Debug.Log($"RUNE_RUSH_EN_BUILD_OK: {outputPath} (apkBytes={new FileInfo(outputPath).Length})");
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = originalUseCustomKeystore;
                EditorUserBuildSettings.buildAppBundle = originalBuildAppBundle;
            }
        }

        private static string ResolveKeystorePath(string keystoreName)
        {
            if (string.IsNullOrWhiteSpace(keystoreName))
            {
                return null;
            }

            const string inProjectPrefix = "{inproject}:";
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string trimmed = keystoreName.Trim();
            if (trimmed.StartsWith(inProjectPrefix, StringComparison.Ordinal))
            {
                return Path.Combine(projectRoot, trimmed.Substring(inProjectPrefix.Length).Trim());
            }

            return Path.IsPathRooted(trimmed) ? trimmed : Path.Combine(projectRoot, trimmed);
        }
    }
}
