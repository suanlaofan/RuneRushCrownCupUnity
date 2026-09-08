using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RuneRush.Editor
{
    public static class PicoBuildConfigurator
    {
        private const string PackageIdentifier = "com.runerush.crowncup";
        private const string PicoPackagePath = "Packages/com.bytedance.pico.xr/package.json";
        private const string PicoPackageVersion = "0.13.1";
        private const string PicoLoaderTypeName = "ByteDance.PICO.XR.PXR_Loader";
        private const string XrSettingsKey = "com.unity.xr.management.loader_settings";
        private const string PicoSettingsKey = "ByteDance.PICO.XR.Settings";
        private const string GeneratedSettingsFolder = "Assets/_RuneRush/Scripts/XR/Generated";
        private const string DevelopmentKeyAlias = "runerush";
        private const string DevelopmentKeystorePasswordEnvironmentVariable = "RUNE_RUSH_ANDROID_KEYSTORE_PASSWORD";

        [MenuItem("Rune Rush/PICO/Configure Android Project")]
        public static void ConfigureAndroidPico()
        {
            EnsurePicoPackageAvailable();
            // A scene can now contain hand-authored environment art and editable
            // layout markers.  Only create the sample scene when it is absent;
            // configuring or building for PICO must never overwrite an artist's work.
            if (!File.Exists(RuneRushSceneBuilder.RaceScenePath))
            {
                RuneRushSceneBuilder.CreateOrUpdateProjectContent();
            }

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException("Unable to switch the active build target to Android.");
            }

            ConfigurePlayerSettings();
            ConfigureUrpForPico();
            ConfigureDevelopmentSigning();
            ConfigurePicoLoader();
            ConfigurePicoSettings();
            AddAndroidDefine("ENABLE_PICO_XR_SDK");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateAndroidPico();
            Debug.Log("RUNE_RUSH_PICO_CONFIG_OK: Android, ARM64, IL2CPP, Vulkan and PICO XR are configured.");
        }

        [MenuItem("Rune Rush/PICO/Validate Android Project")]
        public static void ValidateAndroidPico()
        {
            EnsurePicoPackageAvailable();

            var failures = new List<string>();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                failures.Add("active build target is not Android");
            }

            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29)
            {
                failures.Add("minimum Android API level is below 29");
            }

            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            {
                failures.Add("ARM64 is not enabled");
            }

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            {
                failures.Add("Android scripting backend is not IL2CPP");
            }

            if (!PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Contains(GraphicsDeviceType.Vulkan))
            {
                failures.Add("Vulkan is not enabled for Android");
            }

            if (!IsPicoLoaderAssigned())
            {
                failures.Add("PICO XR loader is not assigned to Android");
            }

            if (!HasAndroidDefine("ENABLE_PICO_XR_SDK"))
            {
                failures.Add("ENABLE_PICO_XR_SDK is not defined for Android");
            }

            UniversalRenderPipelineAsset currentUrp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset
                ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (currentUrp != null && currentUrp.supportsHDR)
            {
                failures.Add("URP HDR is enabled");
            }

            if (!PlayerSettings.Android.useCustomKeystore ||
                string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName) ||
                string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
            {
                failures.Add("Android development signing is not configured");
            }

            ValidatePicoScene(failures);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("PICO configuration validation failed: " + string.Join("; ", failures));
            }

            Debug.Log("RUNE_RUSH_PICO_VALIDATE_OK");
        }

        [MenuItem("Rune Rush/PICO/Build Android APK")]
        public static void BuildAndroidPicoApk()
        {
            ConfigureAndroidPico();

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are present in EditorBuildSettings.");
            }

            string outputPath = ResolveApkOutputPath();
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            bool development = string.Equals(
                Environment.GetEnvironmentVariable("RUNE_RUSH_DEVELOPMENT_BUILD"),
                "1",
                StringComparison.Ordinal);

            EditorUserBuildSettings.buildAppBundle = false;
            var options = development
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.CompressWithLz4HC;

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"PICO Android build failed: {report.summary.result}");
            }

            Debug.Log($"RUNE_RUSH_PICO_BUILD_OK: {outputPath} (apkBytes={new FileInfo(outputPath).Length})");
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Rune Rush Studio";
            PlayerSettings.productName = "Rune Rush: Crown Cup";
            PlayerSettings.bundleVersion = "1.1.1";
            PlayerSettings.Android.bundleVersionCode = 2;
            RuneRushApplicationIcon.Configure();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        private static void ConfigureUrpForPico()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipeline == null) continue;
                pipeline.supportsHDR = false;
                EditorUtility.SetDirty(pipeline);
            }
        }

        private static void ConfigureDevelopmentSigning()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new DirectoryNotFoundException("Unable to resolve the Unity project root.");
            string keystorePath = Path.Combine(projectRoot, "BuildArtifacts", "Signing", "runerush-dev.keystore");
            if (!File.Exists(keystorePath))
            {
                throw new FileNotFoundException(
                    "The development keystore is missing. Generate it before running the PICO build.",
                    keystorePath);
            }

            string keystorePassword = Environment.GetEnvironmentVariable(DevelopmentKeystorePasswordEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(keystorePassword))
            {
                throw new InvalidOperationException(
                    $"Set {DevelopmentKeystorePasswordEnvironmentVariable} in the local build environment before creating a signed PICO APK.");
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasName = DevelopmentKeyAlias;
            PlayerSettings.Android.keyaliasPass = keystorePassword;
        }

        private static void ValidatePicoScene(List<string> failures)
        {
            string scenePath = RuneRushSceneBuilder.RaceScenePath;
            if (!File.Exists(scenePath))
            {
                failures.Add("Rune Rush PICO race scene is missing");
                return;
            }

            string contents = File.ReadAllText(scenePath);
            if (!contents.Contains("Unity.XR.CoreUtils::Unity.XR.CoreUtils.XROrigin", StringComparison.Ordinal))
            {
                failures.Add("PICO race scene has no XROrigin");
            }

            if (!contents.Contains("ByteDance.PICO.XR::ByteDance.PICO.XR.PXR_Manager", StringComparison.Ordinal))
            {
                failures.Add("PICO race scene has no PXR_Manager");
            }

            if (!contents.Contains("m_TagString: MainCamera", StringComparison.Ordinal))
            {
                failures.Add("PICO race scene has no tagged MainCamera");
            }
        }

        private static void ConfigurePicoLoader()
        {
            Type perBuildTargetType = RequireType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
            Type generalSettingsType = RequireType("UnityEngine.XR.Management.XRGeneralSettings");
            Type managerSettingsType = RequireType("UnityEngine.XR.Management.XRManagerSettings");
            Type metadataStoreType = RequireType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");

            UnityEngine.Object perBuildTarget = LoadOrCreateAsset(
                perBuildTargetType,
                GeneratedSettingsFolder + "/XRGeneralSettingsPerBuildTarget.asset");

            MethodInfo settingsForBuildTarget = RequireMethod(
                perBuildTargetType,
                "SettingsForBuildTarget",
                BindingFlags.Public | BindingFlags.Instance,
                1);
            MethodInfo setSettingsForBuildTarget = RequireMethod(
                perBuildTargetType,
                "SetSettingsForBuildTarget",
                BindingFlags.Public | BindingFlags.Instance,
                2);

            var generalSettings = settingsForBuildTarget.Invoke(
                perBuildTarget,
                new object[] { BuildTargetGroup.Android }) as UnityEngine.Object;

            if (generalSettings == null)
            {
                generalSettings = ScriptableObject.CreateInstance(generalSettingsType);
                generalSettings.name = "PICO Android XR General Settings";
                AssetDatabase.AddObjectToAsset(generalSettings, perBuildTarget);
                setSettingsForBuildTarget.Invoke(
                    perBuildTarget,
                    new object[] { BuildTargetGroup.Android, generalSettings });
            }

            PropertyInfo managerProperty = generalSettingsType.GetProperty(
                "Manager",
                BindingFlags.Public | BindingFlags.Instance);
            if (managerProperty == null)
            {
                throw new MissingMemberException(generalSettingsType.FullName, "Manager");
            }

            var managerSettings = managerProperty.GetValue(generalSettings) as UnityEngine.Object;
            if (managerSettings == null)
            {
                managerSettings = ScriptableObject.CreateInstance(managerSettingsType);
                managerSettings.name = "PICO Android XR Manager";
                AssetDatabase.AddObjectToAsset(managerSettings, perBuildTarget);
                managerProperty.SetValue(generalSettings, managerSettings);
            }

            SetBooleanProperty(managerSettingsType, managerSettings, "automaticLoading", true);
            SetBooleanProperty(managerSettingsType, managerSettings, "automaticRunning", true);
            SetBooleanProperty(generalSettingsType, generalSettings, "InitManagerOnStart", true);

            RemoveNonPicoLoaders(metadataStoreType, managerSettingsType, managerSettings);

            MethodInfo assignLoader = RequireMethod(
                metadataStoreType,
                "AssignLoader",
                BindingFlags.Public | BindingFlags.Static,
                3);
            bool assigned = (bool)assignLoader.Invoke(
                null,
                new object[] { managerSettings, PicoLoaderTypeName, BuildTargetGroup.Android });

            if (!assigned && !ManagerHasPicoLoader(managerSettingsType, managerSettings))
            {
                assigned = (bool)assignLoader.Invoke(
                    null,
                    new object[] { managerSettings, "PXR_Loader", BuildTargetGroup.Android });
            }

            if (!assigned && !ManagerHasPicoLoader(managerSettingsType, managerSettings))
            {
                throw new InvalidOperationException("Unable to assign the PICO XR loader for Android.");
            }

            EditorBuildSettings.AddConfigObject(XrSettingsKey, perBuildTarget, true);
            EditorUtility.SetDirty(managerSettings);
            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(perBuildTarget);
        }

        private static void ConfigurePicoSettings()
        {
            Type picoSettingsType = RequireType("ByteDance.PICO.XR.PXR_Settings");
            UnityEngine.Object picoSettings = LoadOrCreateAsset(
                picoSettingsType,
                GeneratedSettingsFolder + "/PXR_Settings.asset");

            var serializedSettings = new SerializedObject(picoSettings);
            SetInteger(serializedSettings, "appMode", 0);
            SetInteger(serializedSettings, "stereoRenderingModeAndroid", 1);
            SetInteger(serializedSettings, "systemDisplayFrequency", 0);
            SetBoolean(serializedSettings, "optimizeBufferDiscards", true);
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorBuildSettings.AddConfigObject(PicoSettingsKey, picoSettings, true);
            EditorUtility.SetDirty(picoSettings);

            Type projectSettingsType = RequireType("ByteDance.PICO.XR.PXR_ProjectSetting");
            UnityEngine.Object projectSettings = LoadOrCreateAsset(
                projectSettingsType,
                "Assets/_RuneRush/Scripts/XR/Resources/PXR_ProjectSetting.asset");

            var serializedProjectSettings = new SerializedObject(projectSettings);
            SetBoolean(serializedProjectSettings, "isSpatialAdapter", false);
            SetBoolean(serializedProjectSettings, "portalInited", true);
            SetInteger(serializedProjectSettings, "portalFirstSelected", 1);
            SetBooleanIfPresent(serializedProjectSettings, "validationFFREnabled", false);
            SetBooleanIfPresent(serializedProjectSettings, "validationETFREnabled", false);
            serializedProjectSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(projectSettings);
        }

        private static void RemoveNonPicoLoaders(
            Type metadataStoreType,
            Type managerSettingsType,
            UnityEngine.Object managerSettings)
        {
            PropertyInfo activeLoadersProperty = managerSettingsType.GetProperty(
                "activeLoaders",
                BindingFlags.Public | BindingFlags.Instance);
            if (activeLoadersProperty?.GetValue(managerSettings) is not IEnumerable activeLoaders)
            {
                return;
            }

            var loaderTypeNames = activeLoaders
                .Cast<object>()
                .Where(loader => loader != null)
                .Select(loader => loader.GetType().FullName)
                .Where(typeName => !string.Equals(typeName, PicoLoaderTypeName, StringComparison.Ordinal))
                .ToArray();

            MethodInfo removeLoader = RequireMethod(
                metadataStoreType,
                "RemoveLoader",
                BindingFlags.Public | BindingFlags.Static,
                3);

            foreach (string loaderTypeName in loaderTypeNames)
            {
                removeLoader.Invoke(
                    null,
                    new object[] { managerSettings, loaderTypeName, BuildTargetGroup.Android });
            }
        }

        private static bool IsPicoLoaderAssigned()
        {
            Type perBuildTargetType = FindType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
            Type managerSettingsType = FindType("UnityEngine.XR.Management.XRManagerSettings");
            if (perBuildTargetType == null || managerSettingsType == null)
            {
                return false;
            }

            UnityEngine.Object perBuildTarget = FindAsset(perBuildTargetType);
            if (perBuildTarget == null)
            {
                return false;
            }

            MethodInfo settingsForBuildTarget = perBuildTargetType.GetMethod(
                "SettingsForBuildTarget",
                BindingFlags.Public | BindingFlags.Instance);
            object generalSettings = settingsForBuildTarget?.Invoke(
                perBuildTarget,
                new object[] { BuildTargetGroup.Android });
            object managerSettings = generalSettings?.GetType()
                .GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(generalSettings);

            return managerSettings is UnityEngine.Object managerObject &&
                   ManagerHasPicoLoader(managerSettingsType, managerObject);
        }

        private static bool ManagerHasPicoLoader(Type managerSettingsType, UnityEngine.Object managerSettings)
        {
            PropertyInfo activeLoadersProperty = managerSettingsType.GetProperty(
                "activeLoaders",
                BindingFlags.Public | BindingFlags.Instance);
            if (activeLoadersProperty?.GetValue(managerSettings) is not IEnumerable activeLoaders)
            {
                return false;
            }

            return activeLoaders.Cast<object>().Any(loader =>
                loader != null && string.Equals(
                    loader.GetType().FullName,
                    PicoLoaderTypeName,
                    StringComparison.Ordinal));
        }

        private static UnityEngine.Object LoadOrCreateAsset(Type type, string assetPath)
        {
            UnityEngine.Object existing = FindAsset(type);
            if (existing != null)
            {
                return existing;
            }

            EnsureAssetFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            var instance = ScriptableObject.CreateInstance(type);
            instance.name = type.Name;
            AssetDatabase.CreateAsset(instance, assetPath);
            return instance;
        }

        private static UnityEngine.Object FindAsset(Type type)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, type);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static Type RequireType(string fullName)
        {
            return FindType(fullName) ?? throw new TypeLoadException(
                $"Required XR type '{fullName}' is unavailable. Wait for Package Manager to finish resolving PICO XR 0.13.1.");
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            BindingFlags flags,
            int parameterCount)
        {
            MethodInfo method = type.GetMethods(flags)
                .FirstOrDefault(candidate =>
                    candidate.Name == name && candidate.GetParameters().Length == parameterCount);
            return method ?? throw new MissingMethodException(type.FullName, name);
        }

        private static void SetBooleanProperty(Type type, object target, string propertyName, bool value)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property?.CanWrite == true)
            {
                property.SetValue(target, value);
            }
        }

        private static void SetInteger(SerializedObject serializedObject, string name, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property == null)
            {
                throw new MissingMemberException(serializedObject.targetObject.GetType().FullName, name);
            }

            property.intValue = value;
        }

        private static void SetBoolean(SerializedObject serializedObject, string name, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property == null)
            {
                throw new MissingMemberException(serializedObject.targetObject.GetType().FullName, name);
            }

            property.boolValue = value;
        }

        private static void SetBooleanIfPresent(SerializedObject serializedObject, string name, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void AddAndroidDefine(string define)
        {
            var defines = new HashSet<string>(
                PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                    .Split(';', StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
            if (defines.Add(define))
            {
                PlayerSettings.SetScriptingDefineSymbols(
                    NamedBuildTarget.Android,
                    string.Join(";", defines.OrderBy(value => value, StringComparer.Ordinal)));
            }
        }

        private static bool HasAndroidDefine(string define)
        {
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Contains(define, StringComparer.Ordinal);
        }

        private static void EnsurePicoPackageAvailable()
        {
            string absolutePackagePath = Path.GetFullPath(PicoPackagePath);
            if (!File.Exists(absolutePackagePath))
            {
                throw new FileNotFoundException("The embedded PICO XR package is missing.", absolutePackagePath);
            }

            string packageJson = File.ReadAllText(absolutePackagePath);
            if (!packageJson.Contains($"\"version\": \"{PicoPackageVersion}\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"PICO XR {PicoPackageVersion} is required.");
            }
        }

        private static string ResolveApkOutputPath()
        {
            string configured = Environment.GetEnvironmentVariable("RUNE_RUSH_APK_PATH");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured);
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new DirectoryNotFoundException("Unable to resolve the Unity project root.");
            return Path.Combine(projectRoot, "Builds", "PICO", "RuneRushCrownCup.apk");
        }
    }
}
