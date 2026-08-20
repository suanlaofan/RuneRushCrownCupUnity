using System;
using System.Collections.Generic;
using RuneRush;
using RuneRush.UnityRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RuneRush.Editor
{
    /// <summary>
    /// Editor-only authoring surface for the logical racing spline.  The preview GameObjects created here
    /// deliberately use HideAndDontSave so they can never be serialized into RuneRushRace or included in a build.
    /// </summary>
    [CustomEditor(typeof(RuneRushTrackAuthoring))]
    [InitializeOnLoad]
    public sealed class RuneRushTrackAuthoringEditor : UnityEditor.Editor
    {
        private const float RoadHalfWidth = 9f;
        private const float ShoulderHalfWidth = 12f;
        private const int PreviewSamples = 192;

        private static readonly HideFlags PreviewHideFlags =
            HideFlags.HideAndDontSave | HideFlags.DontSaveInBuild;

        private static readonly Dictionary<int, PreviewState> PreviewStates = new Dictionary<int, PreviewState>();

        private static readonly Color CenterLineColor = new Color(0.18f, 0.84f, 1f, 0.96f);
        private static readonly Color RoadLineColor = new Color(0.95f, 0.75f, 0.23f, 0.9f);
        private static readonly Color ShoulderLineColor = new Color(0.47f, 0.65f, 0.77f, 0.72f);
        private static readonly Color PointColor = new Color(1f, 0.77f, 0.23f, 1f);
        private static readonly Color ActivePointColor = new Color(0.22f, 0.95f, 1f, 1f);

        private SerializedProperty _controlPoints;
        private SerializedProperty _closed;
        private SerializedProperty _arcLengthDivisions;

        static RuneRushTrackAuthoringEditor()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearAllPreviews;
            EditorApplication.quitting += ClearAllPreviews;
            EditorApplication.playModeStateChanged += ClearPreviewBeforePlayMode;
        }

        private void OnEnable()
        {
            _controlPoints = serializedObject.FindProperty("ControlPoints");
            _closed = serializedObject.FindProperty("Closed");
            _arcLengthDivisions = serializedObject.FindProperty("ArcLengthDivisions");
        }

        public override void OnInspectorGUI()
        {
            var authoring = (RuneRushTrackAuthoring)target;
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Move the numbered handles in Scene view to reshape the logical race path. " +
                "The explicit Control Points array is the race direction and source of truth.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_closed);
            EditorGUILayout.PropertyField(_arcLengthDivisions);
            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(_controlPoints, new GUIContent("Control Points (race order)"), true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                MarkTrackDirty(authoring);
                RefreshPreview(authoring);
            }

            if (!authoring.Closed)
            {
                EditorGUILayout.HelpBox(
                    "Rune Rush currently runs closed-circuit races only. Re-enable Closed before Play.",
                    MessageType.Warning);
            }

            int pointCount = GetOrderedControlPoints(authoring).Count;
            if (pointCount < 4)
            {
                EditorGUILayout.HelpBox(
                    "At least four valid control points are required for a preview and for the race simulation.",
                    MessageType.Error);
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Focus", "Frame the authored spline in Scene view.")))
                {
                    Focus(authoring);
                }

                if (GUILayout.Button(new GUIContent("Refresh Preview", "Rebuild the unsaved editor-only road preview.")))
                {
                    RefreshPreview(authoring);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Clear Preview", "Remove the unsaved editor-only preview mesh.")))
                {
                    ClearPreview(authoring);
                }

                if (GUILayout.Button(new GUIContent("Add Point", "Insert a control point after the selected point, or before the loop seam.")))
                {
                    AddPoint(authoring);
                }
            }
        }

        private void OnSceneGUI()
        {
            var authoring = (RuneRushTrackAuthoring)target;
            PreviewData data = GetPreviewData(authoring);
            if (data == null)
            {
                return;
            }

            DrawRoadOutline(data, true);

            List<Transform> points = GetOrderedControlPoints(authoring);
            Transform active = Selection.activeTransform;
            for (int index = 0; index < points.Count; index += 1)
            {
                Transform point = points[index];
                if (point == null)
                {
                    continue;
                }

                Vector3 current = point.position;
                bool selected = point == active;
                Handles.color = selected ? ActivePointColor : PointColor;
                float size = HandleUtility.GetHandleSize(current) * (selected ? 0.115f : 0.09f);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(
                    current,
                    size,
                    Vector3.zero,
                    Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(point, "Move Rune Rush Track Point");
                    point.position = moved;
                    EditorUtility.SetDirty(point);
                    MarkTrackDirty(authoring);
                    RefreshPreview(authoring);
                }

                Handles.Label(
                    current + Vector3.up * (HandleUtility.GetHandleSize(current) * 0.22f),
                    (index + 1).ToString("00"),
                    EditorStyles.miniBoldLabel);
            }
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
        private static void DrawTrackGizmo(RuneRushTrackAuthoring authoring, GizmoType gizmoType)
        {
            PreviewData data = GetPreviewData(authoring);
            if (data == null)
            {
                return;
            }

            DrawRoadOutline(data, false);
        }

        private static void DrawRoadOutline(PreviewData data, bool highlighted)
        {
            if (data.Samples == null || data.Samples.Length < 2)
            {
                return;
            }

            var center = new Vector3[data.Samples.Length];
            var roadLeft = new Vector3[data.Samples.Length];
            var roadRight = new Vector3[data.Samples.Length];
            var shoulderLeft = new Vector3[data.Samples.Length];
            var shoulderRight = new Vector3[data.Samples.Length];

            for (int index = 0; index < data.Samples.Length; index += 1)
            {
                TrackSample sample = data.Samples[index];
                Vector3 offset = Vector3.up * 0.04f;
                center[index] = sample.Position + offset;
                roadLeft[index] = sample.Position - sample.Right * RoadHalfWidth + offset;
                roadRight[index] = sample.Position + sample.Right * RoadHalfWidth + offset;
                shoulderLeft[index] = sample.Position - sample.Right * ShoulderHalfWidth + offset;
                shoulderRight[index] = sample.Position + sample.Right * ShoulderHalfWidth + offset;
            }

            if (highlighted)
            {
                Handles.color = CenterLineColor;
                Handles.DrawAAPolyLine(3.5f, center);
                Handles.color = RoadLineColor;
                Handles.DrawAAPolyLine(3f, roadLeft);
                Handles.DrawAAPolyLine(3f, roadRight);
                Handles.color = ShoulderLineColor;
                Handles.DrawAAPolyLine(1.5f, shoulderLeft);
                Handles.DrawAAPolyLine(1.5f, shoulderRight);
            }
            else
            {
                Color oldColor = Gizmos.color;
                Gizmos.color = new Color(ShoulderLineColor.r, ShoulderLineColor.g, ShoulderLineColor.b, 0.28f);
                for (int index = 1; index < shoulderLeft.Length; index += 1)
                {
                    Gizmos.DrawLine(shoulderLeft[index - 1], shoulderLeft[index]);
                    Gizmos.DrawLine(shoulderRight[index - 1], shoulderRight[index]);
                }

                Gizmos.color = new Color(CenterLineColor.r, CenterLineColor.g, CenterLineColor.b, 0.42f);
                for (int index = 1; index < center.Length; index += 1)
                {
                    Gizmos.DrawLine(center[index - 1], center[index]);
                }

                Gizmos.color = oldColor;
            }
        }

        private static void AddPoint(RuneRushTrackAuthoring authoring)
        {
            List<Transform> points = GetOrderedControlPoints(authoring);
            int insertIndex = points.Count;
            Vector3 position = authoring.transform.position;

            if (points.Count == 1)
            {
                position = points[0].position + authoring.transform.forward * 25f;
            }
            else if (points.Count >= 2)
            {
                int selectedIndex = points.IndexOf(Selection.activeTransform);
                int previousIndex = selectedIndex >= 0 ? selectedIndex : points.Count - 1;
                int nextIndex = (previousIndex + 1) % points.Count;
                insertIndex = previousIndex + 1;
                position = Vector3.Lerp(points[previousIndex].position, points[nextIndex].position, 0.5f);
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add Rune Rush Track Point");
            int undoGroup = Undo.GetCurrentGroup();

            var pointObject = new GameObject("Track Point " + (insertIndex + 1).ToString("00"));
            Undo.RegisterCreatedObjectUndo(pointObject, "Add Rune Rush Track Point");
            Undo.SetTransformParent(pointObject.transform, authoring.transform, "Add Rune Rush Track Point");
            pointObject.transform.position = position;

            Undo.RecordObject(authoring, "Add Rune Rush Track Point");
            points.Insert(insertIndex, pointObject.transform);
            authoring.ControlPoints = points.ToArray();
            EditorUtility.SetDirty(authoring);
            MarkTrackDirty(authoring);
            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = pointObject;
            RefreshPreview(authoring);
        }

        private static void Focus(RuneRushTrackAuthoring authoring)
        {
            PreviewData data = GetPreviewData(authoring);
            if (data == null)
            {
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            Bounds bounds = data.Bounds;
            bounds.Expand(new Vector3(ShoulderHalfWidth * 2.4f, 28f, ShoulderHalfWidth * 2.4f));
            sceneView.Frame(bounds, false);
            sceneView.Repaint();
        }

        private static void RefreshPreview(RuneRushTrackAuthoring authoring)
        {
            PreviewData data = GetPreviewData(authoring);
            if (data == null)
            {
                ClearPreview(authoring);
                SceneView.RepaintAll();
                return;
            }

            PreviewState state = GetOrCreateState(authoring);
            DestroyPreviewVisual(state);

            var root = new GameObject("RuneRush Track Preview (Editor Only)");
            root.hideFlags = PreviewHideFlags;
            if (authoring.gameObject.scene.IsValid() && authoring.gameObject.scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(root, authoring.gameObject.scene);
            }

            AddPreviewRibbon(
                root.transform,
                "Shoulder Preview",
                data,
                ShoulderHalfWidth,
                -0.025f,
                new Color(0.23f, 0.43f, 0.56f, 0.28f),
                out state.ShoulderMesh,
                out state.ShoulderMaterial);
            AddPreviewRibbon(
                root.transform,
                "Road Preview",
                data,
                RoadHalfWidth,
                0f,
                new Color(0.11f, 0.58f, 0.82f, 0.36f),
                out state.RoadMesh,
                out state.RoadMaterial);

            state.Root = root;
            SceneView.RepaintAll();
        }

        private static void ClearPreview(RuneRushTrackAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            int key = authoring.GetEntityId().GetHashCode();
            if (!PreviewStates.TryGetValue(key, out PreviewState state))
            {
                return;
            }

            DestroyPreviewVisual(state);
            PreviewStates.Remove(key);
            SceneView.RepaintAll();
        }

        private static void ClearAllPreviews()
        {
            foreach (PreviewState state in PreviewStates.Values)
            {
                DestroyPreviewVisual(state);
            }

            PreviewStates.Clear();
        }

        private static void ClearPreviewBeforePlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ClearAllPreviews();
            }
        }

        private static PreviewState GetOrCreateState(RuneRushTrackAuthoring authoring)
        {
            int key = authoring.GetEntityId().GetHashCode();
            if (PreviewStates.TryGetValue(key, out PreviewState state) && state.Owner == authoring)
            {
                return state;
            }

            state = new PreviewState { Owner = authoring };
            PreviewStates[key] = state;
            return state;
        }

        private static PreviewData GetPreviewData(RuneRushTrackAuthoring authoring)
        {
            if (authoring == null || !authoring.Closed)
            {
                return null;
            }

            List<Transform> points = GetOrderedControlPoints(authoring);
            if (points.Count < 4)
            {
                return null;
            }

            int hash = CalculatePathHash(authoring, points);
            PreviewState state = GetOrCreateState(authoring);
            if (state.Data != null && state.PathHash == hash)
            {
                return state.Data;
            }

            try
            {
                TrackPathData path = authoring.CreatePath();
                var samples = new TrackSample[PreviewSamples + 1];
                Bounds bounds = new Bounds(path.Sample(0f).Position, Vector3.zero);
                for (int index = 0; index <= PreviewSamples; index += 1)
                {
                    TrackSample sample = path.Sample(path.Length * index / PreviewSamples);
                    samples[index] = sample;
                    bounds.Encapsulate(sample.Position);
                }

                state.PathHash = hash;
                state.Data = new PreviewData(samples, bounds);
                return state.Data;
            }
            catch (Exception)
            {
                // The runtime will present its own validation when Play is pressed.  Editor authoring stays non-fatal.
                return null;
            }
        }

        private static List<Transform> GetOrderedControlPoints(RuneRushTrackAuthoring authoring)
        {
            var points = new List<Transform>();
            if (authoring == null)
            {
                return points;
            }

            if (authoring.ControlPoints != null && authoring.ControlPoints.Length >= 4)
            {
                for (int index = 0; index < authoring.ControlPoints.Length; index += 1)
                {
                    Transform point = authoring.ControlPoints[index];
                    if (point != null)
                    {
                        points.Add(point);
                    }
                }

                return points;
            }

            for (int index = 0; index < authoring.transform.childCount; index += 1)
            {
                Transform point = authoring.transform.GetChild(index);
                if (point != null)
                {
                    points.Add(point);
                }
            }

            return points;
        }

        private static int CalculatePathHash(RuneRushTrackAuthoring authoring, List<Transform> points)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + authoring.ArcLengthDivisions;
                hash = hash * 31 + (authoring.Closed ? 1 : 0);
                hash = hash * 31 + points.Count;
                for (int index = 0; index < points.Count; index += 1)
                {
                    Transform point = points[index];
                    hash = hash * 31 + point.GetEntityId().GetHashCode();
                    hash = hash * 31 + point.position.GetHashCode();
                }

                return hash;
            }
        }

        private static void AddPreviewRibbon(
            Transform parent,
            string name,
            PreviewData data,
            float halfWidth,
            float height,
            Color color,
            out Mesh mesh,
            out Material material)
        {
            var child = new GameObject(name);
            child.hideFlags = PreviewHideFlags;
            child.transform.SetParent(parent, false);

            mesh = CreateRibbonMesh(name + " Mesh", data.Samples, halfWidth, height);
            material = CreatePreviewMaterial(name + " Material", color);

            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh CreateRibbonMesh(string name, TrackSample[] samples, float halfWidth, float height)
        {
            int count = samples.Length;
            var vertices = new Vector3[count * 2];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(count - 1) * 6];

            for (int index = 0; index < count; index += 1)
            {
                TrackSample sample = samples[index];
                int vertex = index * 2;
                vertices[vertex] = sample.Position - sample.Right * halfWidth + Vector3.up * height;
                vertices[vertex + 1] = sample.Position + sample.Right * halfWidth + Vector3.up * height;
                normals[vertex] = Vector3.up;
                normals[vertex + 1] = Vector3.up;
                uv[vertex] = new Vector2(0f, index / (float)(count - 1));
                uv[vertex + 1] = new Vector2(1f, index / (float)(count - 1));

                if (index == count - 1)
                {
                    continue;
                }

                int triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 2;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 1;
            }

            var mesh = new Mesh { name = name, hideFlags = PreviewHideFlags };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreatePreviewMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = name,
                hideFlags = PreviewHideFlags,
                renderQueue = (int)RenderQueue.Transparent
            };

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            return material;
        }

        private static void DestroyPreviewVisual(PreviewState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(state.Root);
                state.Root = null;
            }

            DestroyPreviewAsset(state.RoadMesh);
            DestroyPreviewAsset(state.ShoulderMesh);
            DestroyPreviewAsset(state.RoadMaterial);
            DestroyPreviewAsset(state.ShoulderMaterial);
            state.RoadMesh = null;
            state.ShoulderMesh = null;
            state.RoadMaterial = null;
            state.ShoulderMaterial = null;
        }

        private static void DestroyPreviewAsset(UnityEngine.Object asset)
        {
            if (asset != null)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static void MarkTrackDirty(RuneRushTrackAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            EditorUtility.SetDirty(authoring);
            Scene scene = authoring.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private sealed class PreviewState
        {
            public RuneRushTrackAuthoring Owner;
            public int PathHash;
            public PreviewData Data;
            public GameObject Root;
            public Mesh RoadMesh;
            public Mesh ShoulderMesh;
            public Material RoadMaterial;
            public Material ShoulderMaterial;
        }

        private sealed class PreviewData
        {
            public readonly TrackSample[] Samples;
            public readonly Bounds Bounds;

            public PreviewData(TrackSample[] samples, Bounds bounds)
            {
                Samples = samples;
                Bounds = bounds;
            }
        }
    }
}
