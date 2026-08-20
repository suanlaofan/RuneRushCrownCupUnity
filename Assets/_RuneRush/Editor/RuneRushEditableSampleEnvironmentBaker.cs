using System;
using RuneRush;
using RuneRush.UnityRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuneRush.Editor
{
    /// <summary>
    /// Converts the built-in Highcrag sample presentation into ordinary, persistent scene objects.
    /// It deliberately uses Unity primitives rather than hidden preview meshes so every road piece,
    /// landmark and light remains selectable and replaceable in the Hierarchy and Scene view.
    /// </summary>
    internal static class RuneRushEditableSampleEnvironmentBaker
    {
        private const string SampleRootName = "BAKED SAMPLE ENVIRONMENT - Edit or Replace";
        private const string CustomContentName = "CUSTOM CONTENT - Safe From Rebuild";
        private const string GeneratedContentName = "GENERATED SAMPLE GEOMETRY - Rebuildable";
        private const string GeneratedMaterialFolder = "Assets/_RuneRush/Settings/Generated";

        internal static bool HasBakedSample(RuneRushSceneBindings bindings)
        {
            return FindBakedSample(bindings) != null;
        }

        [MenuItem("Rune Rush/Scene Editing/Bake Editable Sample Environment")]
        private static void BakeFromMenu()
        {
            RuneRushSceneBindings bindings = UnityEngine.Object.FindAnyObjectByType<RuneRushSceneBindings>();
            if (bindings == null)
            {
                Debug.LogWarning("Rune Rush: open RuneRushRace before baking the editable sample environment.");
                return;
            }

            Bake(bindings);
        }

        [MenuItem("Rune Rush/Scene Editing/Clear Baked Sample Geometry (Keep Custom Content)")]
        private static void ClearFromMenu()
        {
            RuneRushSceneBindings bindings = UnityEngine.Object.FindAnyObjectByType<RuneRushSceneBindings>();
            if (bindings != null) ClearGeneratedGeometry(bindings);
        }

        internal static void Bake(RuneRushSceneBindings bindings)
        {
            if (bindings == null) return;
            bindings.EnsureRoots();

            TrackPathData track = CreateTrack();
            if (track == null)
            {
                Debug.LogWarning("Rune Rush: add at least four valid Track Points before baking the sample environment.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Bake Rune Rush Editable Sample Environment");
            int undoGroup = Undo.GetCurrentGroup();

            RuneRushBakedSampleEnvironment sample = FindOrCreateSample(bindings);
            EnsureCustomContentRoot(sample);
            ClearGeneratedGeometryInternal(sample);

            SampleMaterials materials = SampleMaterials.CreateOrLoad();
            Transform generated = CreateGeneratedGroup(sample.transform);
            CreateTrackGeometry(generated, track, materials);
            CreateLandmarks(generated, track, materials);
            CreateLighting(generated, materials);

            Undo.RecordObject(sample, "Configure Rune Rush Baked Sample Environment");
            sample.BakeVersion = 1;
            sample.UseBakedTrackAtRuntime = true;
            sample.UseBakedEnvironmentAtRuntime = true;
            sample.UseBakedLightingAtRuntime = true;
            sample.gameObject.SetActive(true);
            EditorUtility.SetDirty(sample);
            EditorUtility.SetDirty(bindings);
            Scene scene = bindings.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
            AssetDatabase.SaveAssets();

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = sample.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            SceneView.RepaintAll();
            Debug.Log("RUNE_RUSH_EDITABLE_SAMPLE_BAKE_OK: persistent road, landmarks and lights created under AUTHORED ENVIRONMENT. Custom Content was preserved.");
        }

        internal static void ClearGeneratedGeometry(RuneRushSceneBindings bindings)
        {
            RuneRushBakedSampleEnvironment sample = FindBakedSample(bindings);
            if (sample == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clear Rune Rush Baked Sample Geometry");
            int undoGroup = Undo.GetCurrentGroup();
            ClearGeneratedGeometryInternal(sample);
            EnsureCustomContentRoot(sample);
            EditorUtility.SetDirty(sample);
            if (bindings != null)
            {
                EditorUtility.SetDirty(bindings);
                Scene scene = bindings.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = sample.gameObject;
            SceneView.RepaintAll();
            Debug.Log("RUNE_RUSH_EDITABLE_SAMPLE_CLEAR_OK: generated sample geometry was removed; CUSTOM CONTENT was kept.");
        }

        internal static void FrameBakedSample(RuneRushSceneBindings bindings)
        {
            RuneRushBakedSampleEnvironment sample = FindBakedSample(bindings);
            if (sample == null) return;
            Selection.activeGameObject = sample.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static RuneRushBakedSampleEnvironment FindBakedSample(RuneRushSceneBindings bindings)
        {
            if (bindings == null) return null;
            bindings.EnsureRoots();
            return bindings.AuthoredEnvironmentRoot.GetComponentInChildren<RuneRushBakedSampleEnvironment>(true);
        }

        private static RuneRushBakedSampleEnvironment FindOrCreateSample(RuneRushSceneBindings bindings)
        {
            RuneRushBakedSampleEnvironment existing = FindBakedSample(bindings);
            if (existing != null) return existing;

            var root = new GameObject(SampleRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Rune Rush Editable Sample Environment");
            root.transform.SetParent(bindings.AuthoredEnvironmentRoot, false);
            return root.AddComponent<RuneRushBakedSampleEnvironment>();
        }

        private static Transform EnsureCustomContentRoot(RuneRushBakedSampleEnvironment sample)
        {
            if (sample == null) return null;
            if (sample.CustomContentRoot != null && sample.CustomContentRoot.parent == sample.transform)
            {
                return sample.CustomContentRoot;
            }

            Transform existing = sample.transform.Find(CustomContentName);
            if (existing != null)
            {
                Undo.RecordObject(sample, "Assign Rune Rush Custom Content Root");
                sample.CustomContentRoot = existing;
                return existing;
            }

            var root = new GameObject(CustomContentName);
            Undo.RegisterCreatedObjectUndo(root, "Create Rune Rush Custom Content Root");
            root.transform.SetParent(sample.transform, false);
            Undo.RecordObject(sample, "Assign Rune Rush Custom Content Root");
            sample.CustomContentRoot = root.transform;
            return sample.CustomContentRoot;
        }

        private static void ClearGeneratedGeometryInternal(RuneRushBakedSampleEnvironment sample)
        {
            if (sample == null) return;
            for (int index = sample.transform.childCount - 1; index >= 0; index -= 1)
            {
                Transform child = sample.transform.GetChild(index);
                if (child.GetComponent<RuneRushBakedSampleGeneratedGroup>() == null) continue;
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static Transform CreateGeneratedGroup(Transform parent)
        {
            var root = new GameObject(GeneratedContentName);
            Undo.RegisterCreatedObjectUndo(root, "Create Rune Rush Sample Geometry");
            root.transform.SetParent(parent, false);
            RuneRushBakedSampleGeneratedGroup marker = root.AddComponent<RuneRushBakedSampleGeneratedGroup>();
            marker.GroupId = "highcrag-sample-v1";
            return root.transform;
        }

        private static TrackPathData CreateTrack()
        {
            RuneRushTrackAuthoring authoring = UnityEngine.Object.FindAnyObjectByType<RuneRushTrackAuthoring>();
            if (authoring == null) return null;
            try
            {
                return authoring.CreatePath();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void CreateTrackGeometry(Transform generated, TrackPathData track, SampleMaterials materials)
        {
            Transform terrain = CreateGroup(generated, "01 Terrain - Editable");
            CreatePrimitive(terrain, PrimitiveType.Cube, "Highcrag Ground", new Vector3(0f, -3.2f, 0f),
                new Vector3(620f, 6f, 520f), Quaternion.identity, materials.Grass);

            Transform shoulder = CreateGroup(generated, "02 Stone Shoulders - Editable Segments");
            Transform road = CreateGroup(generated, "03 Royal Road - Editable Segments");
            Transform markings = CreateGroup(generated, "04 Mithril Lane Runes - Editable");
            Transform rails = CreateGroup(generated, "05 Guardrails - Editable");

            const int roadSegments = 96;
            for (int index = 0; index < roadSegments; index += 1)
            {
                float beginDistance = track.Length * index / roadSegments;
                float endDistance = track.Length * (index + 1) / roadSegments;
                TrackSample first = track.Sample(beginDistance);
                TrackSample second = track.Sample(endDistance);
                Vector3 direction = second.Position - first.Position;
                float length = Mathf.Max(0.5f, direction.magnitude);
                Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                Vector3 midpoint = Vector3.Lerp(first.Position, second.Position, 0.5f);

                CreatePrimitive(shoulder, PrimitiveType.Cube, $"Shoulder Segment {index + 1:000}",
                    midpoint + Vector3.down * 0.18f, new Vector3(24.4f, 0.26f, length + 1.4f), rotation, materials.Shoulder);
                CreatePrimitive(road, PrimitiveType.Cube, $"Road Segment {index + 1:000}",
                    midpoint + Vector3.up * 0.02f, new Vector3(18.2f, 0.25f, length + 1.1f), rotation, materials.Road);

                if (index % 2 == 0)
                {
                    CreatePrimitive(markings, PrimitiveType.Cube, $"Lane Rune {index / 2 + 1:000}",
                        midpoint + Vector3.up * 0.18f, new Vector3(0.16f, 0.035f, Mathf.Max(2f, length * 0.48f)), rotation, materials.Rune);
                }

                if (index % 2 == 0)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        TrackSample railSample = track.Sample((beginDistance + endDistance) * 0.5f, side * 10.35f, 0.72f);
                        CreatePrimitive(rails, PrimitiveType.Cube, $"Guardrail {(side < 0 ? "L" : "R")} {index / 2 + 1:000}",
                            railSample.Position, new Vector3(0.26f, 0.62f, length * 2.1f), rotation, materials.Iron);
                    }
                }
            }

            Transform gates = CreateGroup(generated, "06 Race Gates - Editable");
            CreateGate(gates, track, 0f, "Crown Start Gate", true, materials);
            CreateGate(gates, track, track.Length * 0.25f, "Milestone Gate I", false, materials);
            CreateGate(gates, track, track.Length * 0.5f, "Milestone Gate II", false, materials);
            CreateGate(gates, track, track.Length * 0.75f, "Milestone Gate III", false, materials);
        }

        private static void CreateLandmarks(Transform generated, TrackPathData track, SampleMaterials materials)
        {
            Transform forest = CreateGroup(generated, "07 Highcrag Forest - Editable Trees");
            var random = new System.Random(0x48494748);
            for (int index = 0; index < 48; index += 1)
            {
                float distance = (float)random.NextDouble() * track.Length;
                float side = random.Next(0, 2) == 0 ? -1f : 1f;
                float lane = side * Mathf.Lerp(21f, 58f, (float)random.NextDouble());
                TrackSample sample = track.Sample(distance, lane, -0.1f);
                CreateTree(forest, $"Highcrag Fir {index + 1:000}", sample.Position,
                    Mathf.Lerp(0.8f, 1.55f, (float)random.NextDouble()), random.Next(0, 3) == 0,
                    (float)random.NextDouble() * 360f, materials);
            }

            Transform towers = CreateGroup(generated, "08 Kingdom Watchtowers - Editable");
            for (int index = 0; index < 12; index += 1)
            {
                float distance = index / 12f * track.Length + 18f;
                float lane = (index % 2 == 0 ? -1f : 1f) * (39f + index % 3 * 8f);
                TrackSample sample = track.Sample(distance, lane, 0f);
                CreateWatchtower(towers, $"Kingdom Watchtower {index + 1:00}", sample.Position,
                    0.86f + index % 3 * 0.14f, materials);
            }

            Transform mountains = CreateGroup(generated, "09 Highcrag Mountains - Editable");
            for (int index = 0; index < 20; index += 1)
            {
                float angle = index / 20f * Mathf.PI * 2f;
                float radius = 290f + Mathf.Sin(index * 1.7f) * 26f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, -13f, Mathf.Sin(angle) * radius * 0.82f);
                CreatePrimitive(mountains, PrimitiveType.Cube, $"Highcrag Peak {index + 1:00}", position,
                    new Vector3(44f, 70f + index % 5 * 14f, 44f), Quaternion.Euler(18f + index % 4 * 4f, angle * Mathf.Rad2Deg, 45f), materials.Mountain);
            }

            Transform castle = CreateGroup(generated, "10 Highcrag Crown Keep - Editable");
            CreateCastle(castle, materials);

            Transform sky = CreateGroup(generated, "11 Sky Landmark - Editable");
            CreatePrimitive(sky, PrimitiveType.Sphere, "Silver Moon", new Vector3(-245f, 190f, -330f),
                Vector3.one * 38f, Quaternion.identity, materials.Moon);
        }

        private static void CreateLighting(Transform generated, SampleMaterials materials)
        {
            Transform lights = CreateGroup(generated, "12 Lighting - Edit or Replace");
            var sunRoot = new GameObject("Moonlit Directional Light");
            sunRoot.transform.SetParent(lights, false);
            sunRoot.transform.localRotation = Quaternion.Euler(38f, -42f, 0f);
            Light sun = sunRoot.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.72f, 0.82f, 0.92f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;

            var fillRoot = new GameObject("Crown Keep Rune Fill Light");
            fillRoot.transform.SetParent(lights, false);
            fillRoot.transform.localPosition = new Vector3(0f, 26f, -18f);
            Light fill = fillRoot.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.18f, 0.7f, 1f);
            fill.intensity = 7f;
            fill.range = 68f;

            CreatePrimitive(lights, PrimitiveType.Sphere, "Rune Fill Visual", fillRoot.transform.localPosition,
                Vector3.one * 1.2f, Quaternion.identity, materials.Rune);
        }

        private static void CreateGate(Transform parent, TrackPathData track, float distance, string name, bool royal, SampleMaterials materials)
        {
            TrackSample sample = track.Sample(distance);
            Transform root = CreateGroup(parent, name);
            root.localPosition = sample.Position;
            root.localRotation = Quaternion.LookRotation(sample.Tangent, Vector3.up);
            CreatePrimitive(root, PrimitiveType.Cube, "Left Pier", new Vector3(-11f, 3.4f, 0f), new Vector3(1.7f, 6.8f, 1.7f), Quaternion.identity, materials.Stone);
            CreatePrimitive(root, PrimitiveType.Cube, "Right Pier", new Vector3(11f, 3.4f, 0f), new Vector3(1.7f, 6.8f, 1.7f), Quaternion.identity, materials.Stone);
            CreatePrimitive(root, PrimitiveType.Cube, "Arch", new Vector3(0f, 6.2f, 0f), new Vector3(23f, 1.05f, 1.5f), Quaternion.identity, royal ? materials.Gold : materials.Iron);
            CreatePrimitive(root, PrimitiveType.Cylinder, "Rune Seal", new Vector3(0f, 6.1f, -0.85f), new Vector3(1.2f, 0.09f, 1.2f), Quaternion.Euler(90f, 0f, 0f), materials.Rune);
        }

        private static void CreateTree(Transform parent, string name, Vector3 position, float scale, bool lighter, float yaw, SampleMaterials materials)
        {
            Transform root = CreateGroup(parent, name);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.localScale = Vector3.one * scale;
            CreatePrimitive(root, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 2.4f, 0f), new Vector3(0.45f, 2.4f, 0.45f), Quaternion.identity, materials.Wood);
            CreatePrimitive(root, PrimitiveType.Sphere, "Crown Low", new Vector3(0f, 5.2f, 0f), new Vector3(3.2f, 4.4f, 3.2f), Quaternion.identity, lighter ? materials.FoliageLight : materials.FoliageDark);
            CreatePrimitive(root, PrimitiveType.Sphere, "Crown High", new Vector3(0f, 8.1f, 0f), new Vector3(2.15f, 3.35f, 2.15f), Quaternion.identity, lighter ? materials.FoliageLight : materials.FoliageDark);
        }

        private static void CreateWatchtower(Transform parent, string name, Vector3 position, float scale, SampleMaterials materials)
        {
            Transform root = CreateGroup(parent, name);
            root.localPosition = position;
            root.localScale = Vector3.one * scale;
            CreatePrimitive(root, PrimitiveType.Cylinder, "Tower", new Vector3(0f, 6.2f, 0f), new Vector3(3.8f, 6.2f, 3.8f), Quaternion.identity, materials.Stone);
            CreatePrimitive(root, PrimitiveType.Cylinder, "Battlement", new Vector3(0f, 12.5f, 0f), new Vector3(4.8f, 0.9f, 4.8f), Quaternion.identity, materials.Iron);
            CreatePrimitive(root, PrimitiveType.Cylinder, "Beacon", new Vector3(0f, 14.1f, 0f), new Vector3(0.55f, 0.7f, 0.55f), Quaternion.identity, materials.Gold);
        }

        private static void CreateCastle(Transform parent, SampleMaterials materials)
        {
            Transform root = CreateGroup(parent, "Crown Keep Root");
            root.localPosition = new Vector3(0f, 3f, 0f);
            CreatePrimitive(root, PrimitiveType.Cube, "Keep", new Vector3(0f, 16f, 0f), new Vector3(28f, 32f, 24f), Quaternion.identity, materials.Stone);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreatePrimitive(root, PrimitiveType.Cylinder, $"Crown Tower {x}_{z}", new Vector3(x * 18f, 15f, z * 15f), new Vector3(7f, 15f, 7f), Quaternion.identity, materials.Stone);
                    CreatePrimitive(root, PrimitiveType.Cylinder, $"Tower Crown {x}_{z}", new Vector3(x * 18f, 31f, z * 15f), new Vector3(8.3f, 1.2f, 8.3f), Quaternion.identity, materials.Gold);
                }
            }
        }

        private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 localPosition,
            Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = localRotation;
            primitive.transform.localScale = localScale;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            primitive.isStatic = true;
            return primitive;
        }

        private sealed class SampleMaterials
        {
            public Material Road;
            public Material Shoulder;
            public Material Grass;
            public Material Stone;
            public Material Iron;
            public Material Wood;
            public Material FoliageDark;
            public Material FoliageLight;
            public Material Mountain;
            public Material Gold;
            public Material Rune;
            public Material Moon;

            public static SampleMaterials CreateOrLoad()
            {
                EnsureFolder(GeneratedMaterialFolder);
                return new SampleMaterials
                {
                    Road = GetOrCreateMaterial("RuneRush_Baked_Road", "Baked Royal Road", new Color(0.12f, 0.145f, 0.145f), 0.05f, 0.38f),
                    Shoulder = GetOrCreateMaterial("RuneRush_Baked_Shoulder", "Baked Stone Shoulder", new Color(0.27f, 0.28f, 0.26f), 0.03f, 0.2f),
                    Grass = GetOrCreateMaterial("RuneRush_Baked_Grass", "Baked Highcrag Grass", new Color(0.055f, 0.145f, 0.08f), 0f, 0.08f),
                    Stone = GetOrCreateMaterial("RuneRush_Baked_Stone", "Baked Castle Stone", new Color(0.34f, 0.36f, 0.35f), 0.03f, 0.2f),
                    Iron = GetOrCreateMaterial("RuneRush_Baked_Iron", "Baked Mithril Iron", new Color(0.14f, 0.17f, 0.18f), 0.76f, 0.4f),
                    Wood = GetOrCreateMaterial("RuneRush_Baked_Wood", "Baked Oak", new Color(0.19f, 0.085f, 0.035f), 0.02f, 0.2f),
                    FoliageDark = GetOrCreateMaterial("RuneRush_Baked_FoliageDark", "Baked Fir Dark", new Color(0.045f, 0.18f, 0.105f), 0f, 0.18f),
                    FoliageLight = GetOrCreateMaterial("RuneRush_Baked_FoliageLight", "Baked Fir Light", new Color(0.08f, 0.29f, 0.15f), 0f, 0.2f),
                    Mountain = GetOrCreateMaterial("RuneRush_Baked_Mountain", "Baked Mountain", new Color(0.18f, 0.22f, 0.23f), 0f, 0.13f),
                    Gold = GetOrCreateMaterial("RuneRush_Baked_Gold", "Baked Royal Gold", new Color(0.82f, 0.57f, 0.18f), 0.68f, 0.5f),
                    Rune = GetOrCreateMaterial("RuneRush_Baked_Rune", "Baked Mithril Rune", new Color(0.19f, 0.71f, 0.92f), 0.12f, 0.82f, true),
                    Moon = GetOrCreateMaterial("RuneRush_Baked_Moon", "Baked Silver Moon", new Color(0.82f, 0.9f, 0.96f), 0f, 0.1f, true)
                };
            }

            private static Material GetOrCreateMaterial(string fileName, string displayName, Color color, float metallic, float smoothness, bool emission = false)
            {
                string path = GeneratedMaterialFolder + "/" + fileName + ".mat";
                Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) return existing;

                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Sprites/Default");
                var material = new Material(shader) { name = displayName };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
                if (emission)
                {
                    material.EnableKeyword("_EMISSION");
                    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2.6f);
                }
                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            private static void EnsureFolder(string path)
            {
                if (AssetDatabase.IsValidFolder(path)) return;
                int separator = path.LastIndexOf('/');
                if (separator <= 0) return;
                string parent = path.Substring(0, separator);
                string child = path.Substring(separator + 1);
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
