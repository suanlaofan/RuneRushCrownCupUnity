using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RuneRush.Editor
{
    public static class RuneRushPreviewBuild
    {
        private static bool _queued;
        [MenuItem("Rune Rush/Revision/Build Separate PICO Preview")]
        public static void QueueBuild()
        {
            if (_queued || BuildPipeline.isBuildingPlayer) return;
            _queued = true;
            EditorApplication.delayCall += Build;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Switch to Android before building the preview.");
            string oldId=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string oldName=PlayerSettings.productName;
            string oldVersion=PlayerSettings.bundleVersion;
            bool oldSigning=PlayerSettings.Android.useCustomKeystore;
            string path=Path.GetFullPath("Builds/PICO/RuneRushCrownCup-UI-Western-Preview.apk");
            try
            {
                // A separately installable review build never replaces the signed public release.
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.runerush.crowncup.preview");
                PlayerSettings.productName="Rune Rush Crown Cup Preview";
                PlayerSettings.bundleVersion="1.1.1-preview";
                PlayerSettings.Android.useCustomKeystore=false;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[]{"Assets/_RuneRush/Scenes/RuneRushRace.unity"},
                    locationPathName=path,
                    target=BuildTarget.Android,
                    options=BuildOptions.CompressWithLz4HC|BuildOptions.DetailedBuildReport
                });
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Preview build: "+report.summary.result);
                Debug.Log("RUNE_RUSH_PREVIEW_BUILD_OK: "+path+"; apkBytes="+new FileInfo(path).Length);
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,oldId);
                PlayerSettings.productName=oldName;
                PlayerSettings.bundleVersion=oldVersion;
                PlayerSettings.Android.useCustomKeystore=oldSigning;
                _queued = false;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
