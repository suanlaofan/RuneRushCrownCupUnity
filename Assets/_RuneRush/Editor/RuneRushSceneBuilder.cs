using System.IO;
using ByteDance.PICO.XR;
using RuneRush.UnityRuntime;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RuneRush.Editor
{
    public static class RuneRushSceneBuilder
    {
        public const string RaceScenePath = "Assets/_RuneRush/Scenes/RuneRushRace.unity";
        public const string ArtProfilePath = "Assets/_RuneRush/Settings/RuneRushArtProfile.asset";

        [MenuItem("Rune Rush/Project/Create or Refresh Race Scene")]
        public static void CreateOrUpdateProjectContent()
        {
            EnsureFolder("Assets/_RuneRush/Scenes");
            EnsureFolder("Assets/_RuneRush/Settings");

            RuneRushArtProfile artProfile = AssetDatabase.LoadAssetAtPath<RuneRushArtProfile>(ArtProfilePath);
            if (artProfile == null)
            {
                artProfile = ScriptableObject.CreateInstance<RuneRushArtProfile>();
                artProfile.name = "Rune Rush Replaceable Art Profile";
                artProfile.UseProceduralFallback = true;
                AssetDatabase.CreateAsset(artProfile, ArtProfilePath);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateXrOrigin();
            CreateTrackAuthoring();
            CreateSceneBindings(artProfile);
            new GameObject("Scene Authoring - Edit Here", typeof(RuneRushSceneAuthoring));
            new GameObject("RuneRush Game Runtime", typeof(RuneRushGame));

            EditorSceneManager.SaveScene(scene, RaceScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RaceScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("RUNE_RUSH_SCENE_BUILD_OK: " + RaceScenePath);
        }

        private static void CreateXrOrigin()
        {
            var originObject = new GameObject("XR Origin (PICO)");
            XROrigin origin = originObject.AddComponent<XROrigin>();
            originObject.AddComponent<PXR_Manager>();

            var offsetObject = new GameObject("Camera Offset");
            offsetObject.transform.SetParent(originObject.transform, false);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offsetObject.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.065f, 0.085f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 690f;
            camera.fieldOfView = 72f;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;

            TrackedPoseDriver poseDriver = cameraObject.AddComponent<TrackedPoseDriver>();
            poseDriver.positionInput = new InputActionProperty(
                new InputAction("Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition"));
            poseDriver.rotationInput = new InputActionProperty(
                new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            poseDriver.trackingStateInput = new InputActionProperty(
                new InputAction("Head Tracking State", InputActionType.Value, "<XRHMD>/trackingState"));

            origin.CameraFloorOffsetObject = offsetObject;
            origin.Camera = camera;
            origin.CameraYOffset = 0f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        }

        private static void CreateTrackAuthoring()
        {
            var trackObject = new GameObject("Track Authoring - Replace Control Points");
            RuneRushTrackAuthoring authoring = trackObject.AddComponent<RuneRushTrackAuthoring>();
            TrackPathData path = TrackPathData.CreateHighcrag(authoring.ArcLengthDivisions);
            var points = new Transform[path.ControlPoints.Count];
            for (int index = 0; index < path.ControlPoints.Count; index += 1)
            {
                var point = new GameObject($"Track Point {index + 1:00}");
                point.transform.SetParent(trackObject.transform, false);
                point.transform.localPosition = path.ControlPoints[index];
                points[index] = point.transform;
            }

            authoring.ControlPoints = points;
            authoring.Closed = true;
        }

        private static void CreateSceneBindings(RuneRushArtProfile artProfile)
        {
            var root = new GameObject("Scene Bindings - Replace Prefabs Here");
            RuneRushSceneBindings bindings = root.AddComponent<RuneRushSceneBindings>();
            bindings.ArtProfile = artProfile;
            bindings.EnsureRoots();

            var spawn = new GameObject("Player Spawn Reference");
            spawn.transform.SetParent(root.transform, false);
            TrackSample sample = TrackPathData.CreateHighcrag(720).Sample(0f, 0f, 0.28f);
            spawn.transform.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(sample.Tangent, Vector3.up));
            bindings.PlayerSpawn = spawn.transform;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
