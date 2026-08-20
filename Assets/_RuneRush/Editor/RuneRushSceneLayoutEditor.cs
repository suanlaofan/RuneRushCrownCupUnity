using System;
using System.Collections.Generic;
using RuneRush;
using RuneRush.UnityRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuneRush.Editor
{
    /// <summary>
    /// Editor-only authoring surface for the whole race scene.  It creates persistent
    /// GameObjects the artist can see, move, duplicate, delete and replace.  The
    /// runtime consumes their projected track-relative layout data, not these objects.
    /// </summary>
    [CustomEditor(typeof(RuneRushSceneBindings))]
    public sealed class RuneRushSceneLayoutEditor : UnityEditor.Editor
    {
        private const string MarkerVisualName = "EDITOR VISUAL - Replace With Your Model";

        private RuneRushSceneBindings Bindings => (RuneRushSceneBindings)target;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Scene Editing Workflow", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Put static terrain, buildings, lights and set dressing under AUTHORED ENVIRONMENT.\n" +
                "2. Use the buttons below to add race markers, then move the marker GameObjects directly in Scene view.\n" +
                "3. The Layout list is synchronized from marker positions when you save or press Synchronize.\n" +
                "4. Replace the child visual below any marker with your own model. Dynamic runtime prefabs are set in RuneRushArtProfile.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Pickup")) AddMarker(RuneRushLayoutMarkerKind.Pickup);
                if (GUILayout.Button("Add Obstacle")) AddMarker(RuneRushLayoutMarkerKind.Obstacle);
                if (GUILayout.Button("Add Boost Pad")) AddMarker(RuneRushLayoutMarkerKind.Pad, PadKind.Boost);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Repair Pad")) AddMarker(RuneRushLayoutMarkerKind.Pad, PadKind.Repair);
                if (GUILayout.Button("Create Default Layout")) CreateDefaultLayout();
                if (GUILayout.Button("Synchronize Markers → Layout")) SynchronizeMarkersToLayout();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame Editable Content")) FrameEditableContent();
                if (GUILayout.Button("Clear Race Markers")) ClearMarkers();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Persistent Sample Environment", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bake creates normal, saved GameObjects for the Highcrag road, gates, forest, towers, castle and lights. " +
                "They appear under AUTHORED ENVIRONMENT and can be selected, moved, deleted or replaced in the Scene and Hierarchy. " +
                "Your own objects belong under CUSTOM CONTENT, which is never deleted when the sample geometry is rebuilt.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake Editable Sample Environment"))
                {
                    RuneRushEditableSampleEnvironmentBaker.Bake(Bindings);
                }

                using (new EditorGUI.DisabledScope(!RuneRushEditableSampleEnvironmentBaker.HasBakedSample(Bindings)))
                {
                    if (GUILayout.Button("Frame Baked Sample"))
                    {
                        RuneRushEditableSampleEnvironmentBaker.FrameBakedSample(Bindings);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(!RuneRushEditableSampleEnvironmentBaker.HasBakedSample(Bindings)))
            {
                if (GUILayout.Button("Clear Baked Sample Geometry (Keep Custom Content)"))
                {
                    RuneRushEditableSampleEnvironmentBaker.ClearGeneratedGeometry(Bindings);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Youximoxing Imported Models", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build normalizes the FBX models under Assets/Youximoxing, creates explicit URP materials, assigns the runtime art profile, " +
                "adds an editable Unity Terrain and places fantasy landmarks under a rebuildable child of CUSTOM CONTENT. " +
                "Other hand-authored objects under CUSTOM CONTENT are preserved.",
                MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build / Rebuild Youximoxing Scene"))
                {
                    RuneRushYouximoxingSceneBuilder.BuildOpenRaceScene();
                }

                using (new EditorGUI.DisabledScope(!RuneRushYouximoxingSceneBuilder.HasGeneratedScene(Bindings)))
                {
                    if (GUILayout.Button("Frame Youximoxing Scene"))
                    {
                        RuneRushYouximoxingSceneBuilder.FrameGeneratedScene(Bindings);
                    }
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Safe persistent roots: AUTHORED ENVIRONMENT, CUSTOM CONTENT and RACE LAYOUT. Do not place hand-authored art under EnvironmentRoot, RaceObjectRoot or RacerRoot: those are runtime-managed containers.",
                MessageType.None);
        }

        [MenuItem("Rune Rush/Scene Editing/Create Default Editable Layout")]
        private static void CreateDefaultLayoutFromMenu()
        {
            RuneRushSceneBindings bindings = FindBindings();
            if (bindings == null)
            {
                EditorUtility.DisplayDialog("Rune Rush", "Open RuneRushRace and select its Scene Bindings object first.", "OK");
                return;
            }
            CreateDefaultLayoutFor(bindings);
        }

        [MenuItem("Rune Rush/Scene Editing/Synchronize Markers to Runtime Layout")]
        private static void SynchronizeFromMenu()
        {
            RuneRushSceneBindings bindings = FindBindings();
            if (bindings != null) Synchronize(bindings);
        }

        internal static void Synchronize(RuneRushSceneBindings bindings)
        {
            if (bindings == null) return;
            bindings.EnsureRoots();
            TrackPathData track = CreateTrack();
            if (track == null)
            {
                Debug.LogWarning("Rune Rush scene layout needs at least four valid Track Points before it can synchronize markers.");
                return;
            }

            Undo.RecordObject(bindings, "Synchronize Rune Rush Race Layout");
            RaceLayout layout = bindings.Layout ?? new RaceLayout();
            layout.Pickups.Clear();
            layout.Obstacles.Clear();
            layout.Pads.Clear();

            RuneRushRaceLayoutMarker[] markers = bindings.LayoutMarkerRoot.GetComponentsInChildren<RuneRushRaceLayoutMarker>(true);
            Array.Sort(markers, (left, right) => string.CompareOrdinal(left.name, right.name));
            int pickupIndex = 0;
            int obstacleIndex = 0;
            int padIndex = 0;
            foreach (RuneRushRaceLayoutMarker marker in markers)
            {
                if (marker == null) continue;
                UpdateMarkerFromPosition(track, marker, true);
                switch (marker.Kind)
                {
                    case RuneRushLayoutMarkerKind.Pickup:
                        layout.Pickups.Add(new RacePickupLayout
                        {
                            Id = "pickup-" + (pickupIndex++).ToString(),
                            ProgressRatio = marker.ProgressRatio,
                            Lane = marker.Lane
                        });
                        break;
                    case RuneRushLayoutMarkerKind.Obstacle:
                        layout.Obstacles.Add(new RaceObstacleLayout
                        {
                            Id = "obstacle-" + (obstacleIndex++).ToString(),
                            Kind = marker.ObstacleKind,
                            ProgressRatio = marker.ProgressRatio,
                            Lane = marker.Lane,
                            Phase = marker.Phase
                        });
                        break;
                    case RuneRushLayoutMarkerKind.Pad:
                        layout.Pads.Add(new RacePadLayout
                        {
                            Id = "pad-" + (padIndex++).ToString(),
                            Kind = marker.PadKind,
                            ProgressRatio = marker.ProgressRatio,
                            Lane = marker.Lane
                        });
                        break;
                }
            }

            // Once a marker root is authored it remains the source of truth, including
            // an intentionally empty category after the artist deletes every marker.
            layout.UseAuthoredPickups = true;
            layout.UseAuthoredObstacles = true;
            layout.UseAuthoredPads = true;
            bindings.Layout = layout;
            EditorUtility.SetDirty(bindings);
            EditorSceneManager.MarkSceneDirty(bindings.gameObject.scene);
            Debug.Log($"RUNE_RUSH_LAYOUT_SYNC_OK: {layout.Pickups.Count} pickups, {layout.Obstacles.Count} obstacles, {layout.Pads.Count} pads.");
        }

        private void AddMarker(RuneRushLayoutMarkerKind kind, PadKind padKind = PadKind.Boost)
        {
            Bindings.EnsureRoots();
            TrackPathData track = CreateTrack();
            if (track == null)
            {
                EditorUtility.DisplayDialog("Rune Rush", "Add at least four Track Points before adding a race marker.", "OK");
                return;
            }

            int existing = Bindings.LayoutMarkerRoot.GetComponentsInChildren<RuneRushRaceLayoutMarker>(true).Length;
            float ratio = Mathf.Repeat(0.08f + existing * 0.071f, 0.98f);
            float lane = kind == RuneRushLayoutMarkerKind.Obstacle ? (existing % 2 == 0 ? -3.5f : 3.5f) : 0f;
            RuneRushRaceLayoutMarker marker = CreateMarker(Bindings, kind, ratio, lane, padKind, ObstacleKind.Crate, existing * 1.71f);
            Selection.activeGameObject = marker.gameObject;
            SynchronizeMarkersToLayout();
        }

        private void CreateDefaultLayout()
        {
            CreateDefaultLayoutFor(Bindings);
        }

        private static void CreateDefaultLayoutFor(RuneRushSceneBindings bindings)
        {
            if (bindings == null) return;
            bindings.EnsureRoots();
            ClearMarkerChildren(bindings.LayoutMarkerRoot);

            float[] pickupLanes = { -5.7f, 0f, 5.7f, -2.8f, 3.2f, 0f, -5f, 5f, -1.5f, 2.4f, -4f, 4f };
            for (int index = 0; index < pickupLanes.Length; index += 1)
            {
                CreateMarker(bindings, RuneRushLayoutMarkerKind.Pickup, 0.055f + index * 0.078f, pickupLanes[index], PadKind.Boost, ObstacleKind.Crate, 0f);
            }

            (float ratio, float lane, ObstacleKind kind)[] obstacles =
            {
                (0.09f, -4.6f, ObstacleKind.Crate), (0.13f, 3.8f, ObstacleKind.Barricade), (0.17f, 0.5f, ObstacleKind.Oil),
                (0.22f, 0f, ObstacleKind.Spinner), (0.28f, -3.4f, ObstacleKind.Crate), (0.31f, 4.8f, ObstacleKind.Barricade),
                (0.37f, 0f, ObstacleKind.Gate), (0.43f, -5.2f, ObstacleKind.Oil), (0.48f, 2.4f, ObstacleKind.Crate),
                (0.54f, 0f, ObstacleKind.Spinner), (0.59f, 4.5f, ObstacleKind.Barricade), (0.64f, -3.5f, ObstacleKind.Crate),
                (0.69f, 0f, ObstacleKind.Gate), (0.74f, 4.2f, ObstacleKind.Oil), (0.79f, -4.8f, ObstacleKind.Barricade),
                (0.84f, 1.8f, ObstacleKind.Crate), (0.89f, 0f, ObstacleKind.Spinner), (0.94f, -2.4f, ObstacleKind.Crate)
            };
            for (int index = 0; index < obstacles.Length; index += 1)
            {
                (float ratio, float lane, ObstacleKind kind) item = obstacles[index];
                CreateMarker(bindings, RuneRushLayoutMarkerKind.Obstacle, item.ratio, item.lane, PadKind.Boost, item.kind, index * 1.71f);
            }

            CreateMarker(bindings, RuneRushLayoutMarkerKind.Pad, 0.185f, -3.2f, PadKind.Boost, ObstacleKind.Crate, 0f);
            CreateMarker(bindings, RuneRushLayoutMarkerKind.Pad, 0.505f, 3.2f, PadKind.Boost, ObstacleKind.Crate, 0f);
            CreateMarker(bindings, RuneRushLayoutMarkerKind.Pad, 0.815f, 0f, PadKind.Boost, ObstacleKind.Crate, 0f);
            CreateMarker(bindings, RuneRushLayoutMarkerKind.Pad, 0.345f, 5.8f, PadKind.Repair, ObstacleKind.Crate, 0f);
            CreateMarker(bindings, RuneRushLayoutMarkerKind.Pad, 0.675f, -5.8f, PadKind.Repair, ObstacleKind.Crate, 0f);

            Synchronize(bindings);
            Selection.activeGameObject = bindings.LayoutMarkerRoot.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void SynchronizeMarkersToLayout()
        {
            Synchronize(Bindings);
        }

        private void FrameEditableContent()
        {
            Bindings.EnsureRoots();
            Selection.objects = new UnityEngine.Object[] { Bindings.AuthoredEnvironmentRoot.gameObject, Bindings.LayoutMarkerRoot.gameObject };
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void ClearMarkers()
        {
            if (!EditorUtility.DisplayDialog("Clear Race Markers", "Delete the editable pickup, obstacle and pad markers? This can be undone immediately in Unity.", "Clear", "Cancel")) return;
            Bindings.EnsureRoots();
            Undo.RegisterFullObjectHierarchyUndo(Bindings.LayoutMarkerRoot.gameObject, "Clear Rune Rush Race Markers");
            ClearMarkerChildren(Bindings.LayoutMarkerRoot);
            Synchronize(Bindings);
        }

        private static void ClearMarkerChildren(Transform root)
        {
            for (int index = root.childCount - 1; index >= 0; index -= 1)
            {
                Undo.DestroyObjectImmediate(root.GetChild(index).gameObject);
            }
        }

        private static RuneRushRaceLayoutMarker CreateMarker(
            RuneRushSceneBindings bindings,
            RuneRushLayoutMarkerKind kind,
            float ratio,
            float lane,
            PadKind padKind,
            ObstacleKind obstacleKind,
            float phase)
        {
            TrackPathData track = CreateTrack();
            string label = kind == RuneRushLayoutMarkerKind.Pickup
                ? "Pickup Marker"
                : kind == RuneRushLayoutMarkerKind.Pad
                    ? (padKind == PadKind.Boost ? "Boost Pad Marker" : "Repair Pad Marker")
                    : obstacleKind + " Marker";
            var root = new GameObject(label);
            Undo.RegisterCreatedObjectUndo(root, "Create Rune Rush Race Marker");
            root.transform.SetParent(bindings.LayoutMarkerRoot, false);
            var marker = root.AddComponent<RuneRushRaceLayoutMarker>();
            marker.Kind = kind;
            marker.PadKind = padKind;
            marker.ObstacleKind = obstacleKind;
            marker.ProgressRatio = Mathf.Clamp01(ratio);
            marker.Lane = Mathf.Clamp(lane, -RaceSimulation.RoadHalfWidth, RaceSimulation.RoadHalfWidth);
            marker.Phase = phase;
            if (track != null)
            {
                TrackSample sample = track.Sample(marker.ProgressRatio * track.Length, marker.Lane, DefaultHeight(kind));
                root.transform.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(sample.Tangent, Vector3.up));
            }
            CreateMarkerVisual(root.transform, kind, padKind, obstacleKind);
            EditorUtility.SetDirty(bindings);
            EditorSceneManager.MarkSceneDirty(bindings.gameObject.scene);
            return marker;
        }

        private static void CreateMarkerVisual(Transform parent, RuneRushLayoutMarkerKind kind, PadKind padKind, ObstacleKind obstacleKind)
        {
            PrimitiveType primitive = kind == RuneRushLayoutMarkerKind.Pickup ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            GameObject visual = GameObject.CreatePrimitive(primitive);
            visual.name = MarkerVisualName;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = kind == RuneRushLayoutMarkerKind.Pickup ? Vector3.up * 1.2f : Vector3.up * 0.22f;
            visual.transform.localScale = kind == RuneRushLayoutMarkerKind.Pickup
                ? new Vector3(0.72f, 1.45f, 0.72f)
                : kind == RuneRushLayoutMarkerKind.Pad
                    ? new Vector3(3.4f, 0.18f, 5.4f)
                    : ObstacleScale(obstacleKind);
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = MarkerMaterial(kind, padKind, obstacleKind);
        }

        private static Vector3 ObstacleScale(ObstacleKind kind)
        {
            return kind switch
            {
                ObstacleKind.Barricade => new Vector3(4.2f, 1.1f, 0.55f),
                ObstacleKind.Oil => new Vector3(3.6f, 0.07f, 3.6f),
                ObstacleKind.Spinner => new Vector3(5.2f, 0.45f, 0.58f),
                ObstacleKind.Gate => new Vector3(4.4f, 2.2f, 0.42f),
                _ => new Vector3(1.7f, 1.7f, 1.7f)
            };
        }

        private static Material MarkerMaterial(RuneRushLayoutMarkerKind kind, PadKind padKind, ObstacleKind obstacleKind)
        {
            Color color = kind switch
            {
                RuneRushLayoutMarkerKind.Pickup => new Color(0.11f, 0.82f, 1f, 1f),
                RuneRushLayoutMarkerKind.Pad => padKind == PadKind.Boost
                    ? new Color(0.05f, 0.63f, 1f, 1f)
                    : new Color(0.18f, 0.92f, 0.46f, 1f),
                _ => obstacleKind == ObstacleKind.Oil
                    ? new Color(0.4f, 0.12f, 0.6f, 1f)
                    : new Color(1f, 0.35f, 0.14f, 1f)
            };
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "Rune Rush Editor Marker" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        private static float DefaultHeight(RuneRushLayoutMarkerKind kind)
        {
            return kind == RuneRushLayoutMarkerKind.Pickup ? 1.2f : 0.05f;
        }

        private static TrackPathData CreateTrack()
        {
            RuneRushTrackAuthoring authoring = UnityEngine.Object.FindFirstObjectByType<RuneRushTrackAuthoring>();
            if (authoring == null) return null;
            try { return authoring.CreatePath(); }
            catch (Exception) { return null; }
        }

        internal static void UpdateMarkerFromPosition(TrackPathData track, RuneRushRaceLayoutMarker marker, bool moveToTrack)
        {
            const int samples = 960;
            float bestDistance = float.MaxValue;
            float bestProgress = marker.ProgressRatio * track.Length;
            float bestLane = marker.Lane;
            Vector3 markerPosition = marker.transform.position;
            for (int index = 0; index < samples; index += 1)
            {
                float progress = index / (float)samples * track.Length;
                TrackSample sample = track.Sample(progress);
                Vector3 flat = markerPosition - sample.Position;
                flat.y = 0f;
                float distance = flat.sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestProgress = progress;
                bestLane = Mathf.Clamp(Vector3.Dot(flat, sample.Right), -RaceSimulation.RoadHalfWidth, RaceSimulation.RoadHalfWidth);
            }

            marker.ProgressRatio = Mathf.Repeat(bestProgress / track.Length, 1f);
            marker.Lane = bestLane;
            if (moveToTrack)
            {
                TrackSample projected = track.Sample(bestProgress, bestLane, DefaultHeight(marker.Kind));
                Undo.RecordObject(marker.transform, "Project Rune Rush Race Marker");
                marker.transform.SetPositionAndRotation(projected.Position, Quaternion.LookRotation(projected.Tangent, Vector3.up));
            }
            EditorUtility.SetDirty(marker);
        }

        private static RuneRushSceneBindings FindBindings()
        {
            return UnityEngine.Object.FindFirstObjectByType<RuneRushSceneBindings>();
        }
    }

    [CustomEditor(typeof(RuneRushRaceLayoutMarker))]
    public sealed class RuneRushRaceLayoutMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox(
                "Move this object in Scene view. Its location is automatically projected back to the race road when the scene is saved or you press Synchronize on Scene Bindings.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            var marker = (RuneRushRaceLayoutMarker)target;
            EditorGUI.BeginChangeCheck();
            Vector3 next = Handles.PositionHandle(marker.transform.position, marker.transform.rotation);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(marker.transform, "Move Rune Rush Race Marker");
            marker.transform.position = next;
            RuneRushSceneBindings bindings = UnityEngine.Object.FindFirstObjectByType<RuneRushSceneBindings>();
            RuneRushTrackAuthoring authoring = UnityEngine.Object.FindFirstObjectByType<RuneRushTrackAuthoring>();
            if (bindings != null && authoring != null)
            {
                try
                {
                    RuneRushSceneLayoutEditor.UpdateMarkerFromPosition(authoring.CreatePath(), marker, false);
                    RuneRushSceneLayoutEditor.Synchronize(bindings);
                }
                catch (Exception) { }
            }
        }
    }

    [InitializeOnLoad]
    internal static class RuneRushSceneAuthoringSaveHook
    {
        static RuneRushSceneAuthoringSaveHook()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            RuneRushSceneBindings bindings = UnityEngine.Object.FindFirstObjectByType<RuneRushSceneBindings>();
            if (bindings != null && bindings.gameObject.scene == scene)
            {
                RuneRushSceneLayoutEditor.Synchronize(bindings);
            }
        }
    }
}
