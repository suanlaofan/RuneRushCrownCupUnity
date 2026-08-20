using System;
using System.Collections.Generic;
using System.IO;
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
    /// Converts the imported Assets/Youximoxing FBX collection into normalized prefabs,
    /// runtime art slots and a persistent, editable fantasy race environment.
    /// </summary>
    internal static class RuneRushYouximoxingSceneBuilder
    {
        private const string SourceRoot = "Assets/Youximoxing";
        private const string GeneratedAssetRoot = "Assets/_RuneRush/Settings/YouximoxingGenerated";
        private const string PrefabRoot = GeneratedAssetRoot + "/Prefabs";
        private const string MaterialRoot = GeneratedAssetRoot + "/Materials";
        private const string TerrainRoot = GeneratedAssetRoot + "/Terrain";
        private const string GeneratedSceneRootName = "YOXIMOXING GENERATED - Rebuildable";
        private const string GeneratedMarkerVisualName = "YOXIMOXING EDITOR VISUAL";
        private const string DefaultMarkerVisualName = "EDITOR VISUAL - Replace With Your Model";


        private const string PickupId = "06fba1f1-6bb7-4b3b-8d30-81849d419850";
        private const string MineId = "928d6e6e-976c-4d4f-895b-4a5359b236b2";
        private const string CrateId = "d4f6e0ea-c46a-419b-a088-1f434c345be8";
        private const string BarricadeId = "482b83bb-5ee8-4ee2-922e-faa64abda184";
        private const string BrazierId = "a02c8442-1cbf-4c1c-adef-3f077218c4da";
        private const string SpinnerId = "c632a524-237a-4b6f-acdc-d293f857ff1e";
        private const string GateId = "0eb48092-b616-4fc3-ad6d-fa3807f3d40f";
        private const string BoostPadId = "757a4559-c5ec-456e-8856-d98f74a2e762";
        private const string BranchRoadId = "e26db037-a81d-492e-91de-45a3f3c46b75";
        private const string RepairPadId = "eebb9f04-7263-4490-acf9-f085e5de8c9f";
        private const string FenceBarrierId = "7db38d40-facd-4765-8cb5-2142c6a45549";
        private const string KeepTowerId = "c6654e13-85f5-4b8d-89a0-dc805d49bf8f";
        private static readonly Quaternion VehicleForwardRotation = Quaternion.Euler(0f, 90f, 0f);

        private static readonly string[] VehicleIds =
        {
            "0d34fcdb-f6bd-45fc-aad2-1308addc08b8",
            "2e3548e9-e8d3-425c-be31-357627402c9a",
            "3ae5f651-78c4-4c37-949c-b3ccc0d32cb0",
            "42f6e9cc-49e5-44e4-8746-b9ba38b4b8f9",
            "6144d475-05fc-4dde-a6d0-bc1de5444136",
            "c22ac981-38d0-49eb-8647-d14055d8e740",
            "d098abc5-58b3-44cf-b686-e66986d10017",
            "d4275677-62be-4d89-b960-4c7360fc1366"
        };

        private static readonly string[] TreeIds =
        {
            "12ac5a87-fcd3-4eff-9768-cd3d885d4922",
            "1d10e098-4205-49b0-ae51-91fbd6879b02"
        };

        private static readonly string[] TowerIds =
        {
            "95c5d6b5-4646-4cfc-80e0-1f29d6dfeba0",
            KeepTowerId,
            "6b3275a8-ce7c-4ccb-91eb-15e3f7e24084"
        };

        private static readonly string[] BridgeIds =
        {
            "2f6d6d89-af5c-4feb-a602-18363ac52336",
            "75988623-2cb8-4e75-a5dc-5ea10dc79119",
            "79de1514-554d-4fa3-b03f-3fd42f7a0004"
        };

        private static readonly string[] FenceIds =
        {
            "19036be9-b920-47cd-9195-fdb45b801a91",
            BarricadeId,
            FenceBarrierId
        };

        [MenuItem("Rune Rush/Scene Editing/Open Race Scene and Build Youximoxing Fantasy Scene")]
        internal static void BuildRaceSceneFromMenu()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != RuneRushSceneBuilder.RaceScenePath)
            {
                if (activeScene.isDirty && !string.IsNullOrEmpty(activeScene.path))
                {
                    Debug.LogWarning("Rune Rush: save or close the modified scene before opening RuneRushRace for Youximoxing assembly.");
                    return;
                }

                EditorSceneManager.OpenScene(RuneRushSceneBuilder.RaceScenePath, OpenSceneMode.Single);
            }

            BuildOpenRaceScene();
        }

        internal static void BuildOpenRaceScene()
        {
            RuneRushSceneBindings bindings = UnityEngine.Object.FindFirstObjectByType<RuneRushSceneBindings>();
            RuneRushTrackAuthoring trackAuthoring = UnityEngine.Object.FindFirstObjectByType<RuneRushTrackAuthoring>();
            if (bindings == null || trackAuthoring == null)
            {
                Debug.LogError("Rune Rush: RuneRushRace is missing Scene Bindings or Track Authoring.");
                return;
            }

            bindings.EnsureRoots();
            RuneRushBakedSampleEnvironment bakedSample = bindings.AuthoredEnvironmentRoot
                .GetComponentInChildren<RuneRushBakedSampleEnvironment>(true);
            if (bakedSample == null)
            {
                RuneRushEditableSampleEnvironmentBaker.Bake(bindings);
                bakedSample = bindings.AuthoredEnvironmentRoot
                    .GetComponentInChildren<RuneRushBakedSampleEnvironment>(true);
            }

            if (bakedSample == null)
            {
                Debug.LogError("Rune Rush: failed to create the persistent sample environment.");
                return;
            }

            TrackPathData track;
            try
            {
                track = trackAuthoring.CreatePath();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            EnsureFolders();
            BuildAssets assets = CreateGeneratedAssets();
            if (!assets.IsValid)
            {
                Debug.LogError("Rune Rush: one or more required Youximoxing FBX assets could not be loaded.");
                return;
            }

            Transform customContent = EnsureCustomContentRoot(bakedSample);
            Transform generatedRoot = RecreateGeneratedSceneRoot(customContent);
            Terrain terrain = CreateTerrain(generatedRoot, track);
            DisablePrimitiveGround(bakedSample);
            AssembleEnvironment(generatedRoot, track, terrain, assets);
            ReplaceBakedGuardrailsAndTowers(bakedSample, track, assets);
            ConfigureArtProfile(bindings, assets);
            ReplaceLayoutMarkerVisuals(bindings, assets);
            RuneRushSceneLayoutEditor.Synchronize(bindings);

            EditorUtility.SetDirty(bindings);
            EditorUtility.SetDirty(bakedSample);
            EditorSceneManager.MarkSceneDirty(bindings.gameObject.scene);
            EditorSceneManager.SaveScene(bindings.gameObject.scene, RuneRushSceneBuilder.RaceScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = generatedRoot.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            SceneView.RepaintAll();
            Debug.Log("RUNE_RUSH_YOUXIMOXING_BUILD_OK: normalized prefabs, Terrain, runtime art slots and persistent scene models were saved to RuneRushRace.");
        }

        internal static bool HasGeneratedScene(RuneRushSceneBindings bindings)
        {
            if (bindings == null || bindings.AuthoredEnvironmentRoot == null) return false;
            return FindDeepChild(bindings.AuthoredEnvironmentRoot, GeneratedSceneRootName) != null;
        }

        internal static void FrameGeneratedScene(RuneRushSceneBindings bindings)
        {
            if (bindings == null || bindings.AuthoredEnvironmentRoot == null) return;
            Transform root = FindDeepChild(bindings.AuthoredEnvironmentRoot, GeneratedSceneRootName);
            if (root == null) return;
            Selection.activeGameObject = root.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static BuildAssets CreateGeneratedAssets()
        {
            var assets = new BuildAssets
            {
                Pickup = CreateNormalizedPrefab("Runtime_Pickup", PickupId, 2.1f, PrefabShape.Standard, false, true),
                Mine = CreateNormalizedPrefab("Runtime_Mine", MineId, 1.75f, PrefabShape.Standard, false, true),
                Crate = CreateNormalizedPrefab("Runtime_Crate", CrateId, 1.9f, PrefabShape.Standard, true),
                Barricade = CreateNormalizedPrefab("Runtime_Barricade", BarricadeId, 5.15f, PrefabShape.Standard, true),
                Oil = CreateOilPrefab(),
                Spinner = CreateNormalizedPrefab("Runtime_Spinner", SpinnerId, 5.2f, PrefabShape.Spinner, true),
                Gate = CreateNormalizedPrefab("Runtime_Gate", GateId, 5.4f, PrefabShape.Standard, true),
                BoostPad = CreateNormalizedPrefab("Runtime_BoostPad", BoostPadId, 5.4f, PrefabShape.Standard, true, true),
                RepairPad = CreateNormalizedPrefab("Runtime_RepairPad", RepairPadId, 5.4f, PrefabShape.Standard, true, true)
            };

            assets.RacerVehicles = new GameObject[VehicleIds.Length];
            for (int index = 0; index < VehicleIds.Length; index += 1)
            {
                string id = VehicleIds[index];
                assets.RacerVehicles[index] = CreateNormalizedPrefab($"Runtime_Racer_{index + 1:00}", id, 4.8f, PrefabShape.Vehicle, true);
                assets.SceneModels[id] = CreateNormalizedPrefab("Scene_" + id, id, 1f, PrefabShape.Vehicle, true);
            }
            assets.PlayerVehicle = assets.RacerVehicles[0];
            assets.AiVehicle = assets.RacerVehicles[1];
            foreach (string id in TreeIds) assets.SceneModels[id] = CreateNormalizedPrefab("Scene_" + id, id, 1f, PrefabShape.Standard, false);
            foreach (string id in TowerIds) assets.SceneModels[id] = CreateNormalizedPrefab("Scene_" + id, id, 1f, PrefabShape.Standard, true);
            foreach (string id in BridgeIds) assets.SceneModels[id] = CreateNormalizedPrefab("Scene_" + id, id, 1f, PrefabShape.Standard, true);
            foreach (string id in FenceIds) assets.SceneModels[id] = CreateNormalizedPrefab("Scene_" + id, id, 1f, PrefabShape.Fence, true);

            string[] additionalSceneIds =
            {
                GateId,
                "9d56a215-6369-463e-8dc5-bf0ac85140bb",
                "c7e3b510-c143-4c89-ae1f-82bb48221e37",
                "22dbc1df-2952-4666-b147-6a78fb83de54",
                "29fb5809-578c-4540-a2d8-c2696a230913",
                "1916f80d-6c14-4ce4-b651-5ce4464cfa4c",
                "96e654f9-13c5-4447-ad1f-6edb63ae0083",
                "af710e56-960f-4661-a9cb-dd0f1bc14908",
                "dec765dd-174c-4012-ae0d-f8c70f71a61e",
                "5fe34376-698d-40e4-b190-57ae4b881ac1",
                "76026aaf-d753-4245-97b6-bf6d05ffc2d4",
                BoostPadId,
                BranchRoadId,
                RepairPadId,
                "e8576261-f246-4d51-8ecb-37918aa81f86",
                "ef7c977c-7c4d-49bc-be06-b97777a40795",
                BrazierId
            };
            foreach (string id in additionalSceneIds)
            {
                if (!assets.SceneModels.ContainsKey(id))
                {
                    assets.SceneModels[id] = CreateNormalizedPrefab("Scene_" + id, id, 1f, PrefabShape.Standard, true);
                }
            }

            return assets;
        }

        private static GameObject CreateNormalizedPrefab(
            string outputName,
            string id,
            float targetMaxDimension,
            PrefabShape shape,
            bool castShadows,
            bool emission = false)
        {
            string modelPath = ModelPath(id);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogError("Rune Rush: missing Youximoxing model at " + modelPath);
                return null;
            }

            string prefabPath = PrefabRoot + "/" + outputName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) AssetDatabase.DeleteAsset(prefabPath);

            var root = new GameObject(outputName);
            Transform contentRoot = root.transform;
            if (shape == PrefabShape.Vehicle)
            {
                contentRoot = new GameObject("VisualSlot").transform;
                contentRoot.SetParent(root.transform, false);
            }
            else if (shape == PrefabShape.Spinner)
            {
                contentRoot = new GameObject("MotionRoot").transform;
                contentRoot.SetParent(root.transform, false);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (instance == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                return null;
            }

            Quaternion importedAxisRotation = instance.transform.localRotation;
            instance.name = "Model";
            instance.transform.SetParent(contentRoot, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = importedAxisRotation;
            instance.transform.localScale = Vector3.one;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (shape == PrefabShape.Vehicle)
            {
                instance.transform.localRotation = VehicleForwardRotation * importedAxisRotation;
            }
            else if (shape == PrefabShape.Fence && TryCalculateBounds(renderers, out Bounds fenceBounds)
                && fenceBounds.size.x > fenceBounds.size.z)
            {
                instance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * importedAxisRotation;
            }
            if (!TryCalculateBounds(renderers, out Bounds bounds))
            {
                Debug.LogError("Rune Rush: model has no renderable bounds: " + modelPath);
                UnityEngine.Object.DestroyImmediate(root);
                return null;
            }

            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = maxDimension > 0.0001f ? targetMaxDimension / maxDimension : 1f;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = new Vector3(
                -bounds.center.x * scale,
                -bounds.min.y * scale,
                -bounds.center.z * scale);

            Material material = CreateOrUpdateMaterial(outputName, id, emission);
            foreach (Renderer renderer in renderers)
            {
                int materialCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                var materials = new Material[materialCount];
                for (int index = 0; index < materials.Length; index += 1) materials[index] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = castShadows;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject CreateOilPrefab()
        {
            const string outputName = "Runtime_Oil";
            string prefabPath = PrefabRoot + "/" + outputName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) AssetDatabase.DeleteAsset(prefabPath);

            var root = new GameObject(outputName);
            GameObject mire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mire.name = "Mire";
            mire.transform.SetParent(root.transform, false);
            mire.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            mire.transform.localScale = new Vector3(2.25f, 0.04f, 2.25f);
            Collider collider = mire.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);

            Material material = CreateOrUpdateSolidMaterial(outputName, new Color(0.055f, 0.075f, 0.045f), 0f, 0.74f);
            Renderer renderer = mire.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static Material CreateOrUpdateMaterial(string outputName, string id, bool emission)
        {
            string path = MaterialRoot + "/" + outputName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = outputName + " Material" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Texture2D texture = FindBaseColorTexture(id);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.05f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", emission ? 0.62f : 0.28f);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", new Color(0.12f, 0.52f, 0.72f) * 1.8f);
                if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", texture);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateSolidMaterial(string outputName, Color color, float metallic, float smoothness)
        {
            string path = MaterialRoot + "/" + outputName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = outputName + " Material" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D FindBaseColorTexture(string id)
        {
            string folder = SourceRoot + "/tripo_convert_" + id;
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".JPEG", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".JPG", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".PNG", StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
            return null;
        }

        private static Terrain CreateTerrain(Transform parent, TrackPathData track)
        {
            const int heightResolution = 257;
            const int alphaResolution = 128;
            Vector3 size = new Vector3(640f, 54f, 560f);
            Vector3 origin = new Vector3(-320f, -14f, -280f);
            string terrainPath = TerrainRoot + "/RuneRush_Youximoxing_Terrain.asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
            if (data == null)
            {
                data = new TerrainData { name = "Rune Rush Youximoxing Terrain" };
                AssetDatabase.CreateAsset(data, terrainPath);
            }

            data.heightmapResolution = heightResolution;
            data.alphamapResolution = alphaResolution;
            data.baseMapResolution = 256;
            data.size = size;

            var trackSamples = new TrackSample[192];
            for (int index = 0; index < trackSamples.Length; index += 1)
            {
                trackSamples[index] = track.Sample(track.Length * index / trackSamples.Length);
            }

            var heights = new float[heightResolution, heightResolution];
            for (int z = 0; z < heightResolution; z += 1)
            {
                float worldZ = origin.z + z / (float)(heightResolution - 1) * size.z;
                for (int x = 0; x < heightResolution; x += 1)
                {
                    float worldX = origin.x + x / (float)(heightResolution - 1) * size.x;
                    float nearestDistance = float.MaxValue;
                    float nearestTrackHeight = -1f;
                    foreach (TrackSample sample in trackSamples)
                    {
                        float dx = worldX - sample.Position.x;
                        float dz = worldZ - sample.Position.z;
                        float distance = dx * dx + dz * dz;
                        if (distance >= nearestDistance) continue;
                        nearestDistance = distance;
                        nearestTrackHeight = sample.Position.y - 0.42f;
                    }

                    nearestDistance = Mathf.Sqrt(nearestDistance);
                    float broadNoise = Mathf.PerlinNoise((worldX + 760f) * 0.0046f, (worldZ + 680f) * 0.0046f);
                    float detailNoise = Mathf.PerlinNoise((worldX + 190f) * 0.014f, (worldZ + 270f) * 0.014f);
                    float surface = -4.2f + broadNoise * 15f + detailNoise * 2.2f;
                    float trackBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(17f, 60f, nearestDistance));
                    surface = Mathf.Lerp(nearestTrackHeight, surface, trackBlend);

                    float centerDistance = new Vector2(worldX, worldZ).magnitude;
                    float keepBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(58f, 105f, centerDistance));
                    surface = Mathf.Lerp(-1.5f, surface, keepBlend);
                    heights[z, x] = Mathf.Clamp01((surface - origin.y) / size.y);
                }
            }
            data.SetHeights(0, 0, heights);

            TerrainLayer grass = CreateTerrainLayer(
                TerrainRoot + "/RuneRush_Grass.terrainlayer",
                TerrainRoot + "/RuneRush_Grass.asset",
                new Color(0.075f, 0.24f, 0.11f),
                new Color(0.11f, 0.34f, 0.15f),
                new Vector2(28f, 28f));
            TerrainLayer rock = CreateTerrainLayer(
                TerrainRoot + "/RuneRush_Rock.terrainlayer",
                TerrainRoot + "/RuneRush_Rock.asset",
                new Color(0.24f, 0.27f, 0.26f),
                new Color(0.39f, 0.37f, 0.33f),
                new Vector2(18f, 18f));
            data.terrainLayers = new[] { grass, rock };

            var alphas = new float[alphaResolution, alphaResolution, 2];
            for (int z = 0; z < alphaResolution; z += 1)
            {
                for (int x = 0; x < alphaResolution; x += 1)
                {
                    float normalizedX = x / (float)(alphaResolution - 1);
                    float normalizedZ = z / (float)(alphaResolution - 1);
                    float slope = data.GetSteepness(normalizedX, normalizedZ);
                    float height = data.GetInterpolatedHeight(normalizedX, normalizedZ) + origin.y;
                    float rockWeight = Mathf.Clamp01(Mathf.InverseLerp(20f, 43f, slope) + Mathf.InverseLerp(6f, 17f, height) * 0.22f);
                    alphas[z, x, 0] = 1f - rockWeight;
                    alphas[z, x, 1] = rockWeight;
                }
            }
            data.SetAlphamaps(0, 0, alphas);
            EditorUtility.SetDirty(data);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Highcrag Editable Terrain";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = origin;
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 160f;
            terrain.drawInstanced = true;
            terrain.allowAutoConnect = false;
            GameObjectUtility.SetStaticEditorFlags(terrainObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return terrain;
        }

        private static TerrainLayer CreateTerrainLayer(string layerPath, string texturePath, Color dark, Color light, Vector2 tileSize)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(8, 8, TextureFormat.RGBA32, true) { name = Path.GetFileNameWithoutExtension(texturePath) };
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Bilinear;
                AssetDatabase.CreateAsset(texture, texturePath);
            }

            for (int y = 0; y < texture.height; y += 1)
            {
                for (int x = 0; x < texture.width; x += 1)
                {
                    float checker = ((x + y) & 1) == 0 ? 0.3f : 0.7f;
                    texture.SetPixel(x, y, Color.Lerp(dark, light, checker));
                }
            }
            texture.Apply(true, false);
            EditorUtility.SetDirty(texture);

            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer { name = Path.GetFileNameWithoutExtension(layerPath) };
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = texture;
            layer.tileSize = tileSize;
            layer.smoothness = 0.14f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void AssembleEnvironment(Transform generatedRoot, TrackPathData track, Terrain terrain, BuildAssets assets)
        {
            Transform gates = CreateGroup(generatedRoot, "01 Track Gates");
            string[] gateIds = { "c7e3b510-c143-4c89-ae1f-82bb48221e37", GateId, "9d56a215-6369-463e-8dc5-bf0ac85140bb", GateId };
            for (int index = 0; index < gateIds.Length; index += 1)
            {
                PlaceTrackModel(assets.SceneModels[gateIds[index]], gates, "Race Gate " + (index + 1), track, terrain,
                    index * 0.25f, 0f, 21f, false, 0f);
            }

            Transform towers = CreateGroup(generatedRoot, "02 Watchtowers and Ruins");
            for (int index = 0; index < 10; index += 1)
            {
                float lane = (index % 2 == 0 ? -1f : 1f) * (38f + index % 3 * 8f);
                PlaceTrackModel(assets.SceneModels[KeepTowerId], towers, "Watchtower " + (index + 1).ToString("00"), track, terrain,
                    Mathf.Repeat(0.035f + index / 10f, 1f), lane, 15f + index % 3 * 2.5f, true, index * 31f);
            }

            Transform bridges = CreateGroup(generatedRoot, "03 Bridge Landmarks");
            float[] bridgeRatios = { 0.12f, 0.43f, 0.72f };
            float[] bridgeLanes = { -31f, 34f, -36f };
            for (int index = 0; index < BridgeIds.Length; index += 1)
            {
                PlaceTrackModel(assets.SceneModels[BridgeIds[index]], bridges, "Bridge Landmark " + (index + 1), track, terrain,
                    bridgeRatios[index], bridgeLanes[index], 18f, true, index % 2 == 0 ? 18f : -22f);
            }

            Transform fences = CreateGroup(generatedRoot, "04 Trackside Fences");
            for (int index = 0; index < 24; index += 1)
            {
                string id = FenceIds[index % FenceIds.Length];
                float lane = (index % 2 == 0 ? -1f : 1f) * 14.2f;
                PlaceTrackModel(assets.SceneModels[id], fences, "Fence " + (index + 1).ToString("00"), track, terrain,
                    (index + 0.5f) / 24f, lane, 7.5f, true, 0f);
            }

            Transform forest = CreateGroup(generatedRoot, "05 Fantasy Forest");
            var random = new System.Random(0x594F5849);
            for (int index = 0; index < 34; index += 1)
            {
                string id = TreeIds[index % TreeIds.Length];
                float ratio = (float)random.NextDouble();
                float side = random.Next(0, 2) == 0 ? -1f : 1f;
                float lane = side * Mathf.Lerp(26f, 64f, (float)random.NextDouble());
                float size = Mathf.Lerp(8.5f, 14.5f, (float)random.NextDouble());
                PlaceTrackModel(assets.SceneModels[id], forest, "Fantasy Tree " + (index + 1).ToString("00"), track, terrain,
                    ratio, lane, size, true, (float)random.NextDouble() * 360f);
            }

            Transform props = CreateGroup(generatedRoot, "06 Trackside Props");
            string[] propIds =
            {
                "1916f80d-6c14-4ce4-b651-5ce4464cfa4c",
                "af710e56-960f-4661-a9cb-dd0f1bc14908",
                "96e654f9-13c5-4447-ad1f-6edb63ae0083",
                "dec765dd-174c-4012-ae0d-f8c70f71a61e",
                "22dbc1df-2952-4666-b147-6a78fb83de54",
                "29fb5809-578c-4540-a2d8-c2696a230913"
            };
            float[] propSizes = { 4.8f, 8f, 6.5f, 2.2f, 2.6f, 3.2f };
            for (int index = 0; index < 18; index += 1)
            {
                string id = propIds[index % propIds.Length];
                float lane = (index % 2 == 0 ? -1f : 1f) * (18f + index % 4 * 5f);
                PlaceTrackModel(assets.SceneModels[id], props, "Prop " + (index + 1).ToString("00"), track, terrain,
                    Mathf.Repeat(0.025f + index / 18f, 1f), lane, propSizes[index % propSizes.Length], true, index * 47f);
            }

            CreateCentralKeep(generatedRoot, terrain, assets);
            CreateVehiclePaddock(generatedRoot, track, terrain, assets);
            CreateTrackFeatureShowcase(generatedRoot, track, terrain, assets);
            CreateGateBraziers(generatedRoot, track, terrain, assets);
        }

        private static void CreateCentralKeep(Transform generatedRoot, Terrain terrain, BuildAssets assets)
        {
            Transform keep = CreateGroup(generatedRoot, "07 Crown Keep Model District");
            float baseHeight = TerrainHeight(terrain, Vector3.zero);
            InstantiateSceneModel(assets.SceneModels[RepairPadId], keep, "Keep Foundation", new Vector3(0f, baseHeight, 0f), Quaternion.identity, 34f, true);
            InstantiateSceneModel(assets.SceneModels["c7e3b510-c143-4c89-ae1f-82bb48221e37"], keep, "Crown Keep Gate",
                new Vector3(0f, baseHeight, -22f), Quaternion.identity, 23f, true);

            Vector3[] towerPositions =
            {
                new Vector3(-21f, 0f, -18f), new Vector3(21f, 0f, -18f),
                new Vector3(-21f, 0f, 18f), new Vector3(21f, 0f, 18f)
            };
            for (int index = 0; index < towerPositions.Length; index += 1)
            {
                Vector3 position = towerPositions[index];
                position.y = TerrainHeight(terrain, position);
                InstantiateSceneModel(assets.SceneModels[KeepTowerId], keep, "Keep Tower " + (index + 1),
                    position, Quaternion.Euler(0f, index * 90f, 0f), 19f, true);
            }

            InstantiateSceneModel(assets.SceneModels["6b3275a8-ce7c-4ccb-91eb-15e3f7e24084"], keep, "Keep Ruins",
                new Vector3(0f, baseHeight, 15f), Quaternion.Euler(0f, 180f, 0f), 20f, true);
            InstantiateSceneModel(assets.SceneModels["af710e56-960f-4661-a9cb-dd0f1bc14908"], keep, "Keep Banner Left",
                new Vector3(-10f, baseHeight, -25f), Quaternion.identity, 8f, true);
            InstantiateSceneModel(assets.SceneModels["af710e56-960f-4661-a9cb-dd0f1bc14908"], keep, "Keep Banner Right",
                new Vector3(10f, baseHeight, -25f), Quaternion.identity, 8f, true);
        }

        private static void CreateVehiclePaddock(Transform generatedRoot, TrackPathData track, Terrain terrain, BuildAssets assets)
        {
            Transform paddock = CreateGroup(generatedRoot, "08 Vehicle Paddock");
            for (int index = 0; index < VehicleIds.Length; index += 1)
            {
                TrackSample sample = track.Sample(12f + index * 5.8f, index % 2 == 0 ? -27f : -34f);
                Vector3 position = sample.Position;
                position.y = TerrainHeight(terrain, position);
                InstantiateSceneModel(assets.SceneModels[VehicleIds[index]], paddock, "Paddock Racer " + (index + 1).ToString("00"),
                    position, Quaternion.LookRotation(sample.Tangent, Vector3.up), 4.8f, true);
            }
        }

        private static void CreateTrackFeatureShowcase(Transform generatedRoot, TrackPathData track, Terrain terrain, BuildAssets assets)
        {
            Transform features = CreateGroup(generatedRoot, "09 Track Feature Models");
            string[] ids =
            {
                "5fe34376-698d-40e4-b190-57ae4b881ac1",
                "76026aaf-d753-4245-97b6-bf6d05ffc2d4",
                BoostPadId,
                BranchRoadId,
                "e8576261-f246-4d51-8ecb-37918aa81f86",
                "ef7c977c-7c4d-49bc-be06-b97777a40795"
            };
            for (int index = 0; index < ids.Length; index += 1)
            {
                TrackSample sample = track.Sample(track.Length * (0.06f + index * 0.145f), 28f);
                Vector3 position = sample.Position;
                position.y = TerrainHeight(terrain, position);
                InstantiateSceneModel(assets.SceneModels[ids[index]], features, "Track Feature " + (index + 1), position,
                    Quaternion.LookRotation(sample.Tangent, Vector3.up), index < 2 ? 11f : 7.5f, true);
            }
        }

        private static void CreateGateBraziers(Transform generatedRoot, TrackPathData track, Terrain terrain, BuildAssets assets)
        {
            Transform braziers = CreateGroup(generatedRoot, "10 Gate Braziers");
            for (int gateIndex = 0; gateIndex < 4; gateIndex += 1)
            {
                float ratio = gateIndex * 0.25f;
                for (int side = -1; side <= 1; side += 2)
                {
                    PlaceTrackModel(assets.SceneModels[BrazierId], braziers,
                        $"Gate Brazier {gateIndex + 1} {(side < 0 ? "L" : "R")}", track, terrain,
                        ratio, side * 14.5f, 2.6f, true, side < 0 ? 15f : -15f);
                }
            }
        }

        private static void ConfigureArtProfile(RuneRushSceneBindings bindings, BuildAssets assets)
        {
            RuneRushArtProfile profile = bindings.ArtProfile;
            if (profile == null)
            {
                profile = AssetDatabase.LoadAssetAtPath<RuneRushArtProfile>(RuneRushSceneBuilder.ArtProfilePath);
                bindings.ArtProfile = profile;
            }
            if (profile == null) return;

            profile.PlayerVehicle = assets.PlayerVehicle;
            profile.AiVehicle = assets.AiVehicle;
            profile.RacerVehicles = assets.RacerVehicles;
            profile.Pickup = assets.Pickup;
            profile.Mine = assets.Mine;
            profile.Crate = assets.Crate;
            profile.Barricade = assets.Barricade;
            profile.Oil = assets.Oil;
            profile.Spinner = assets.Spinner;
            profile.Gate = assets.Gate;
            profile.BoostPad = assets.BoostPad;
            profile.RepairPad = assets.RepairPad;
            profile.GenerateProceduralTrack = true;
            profile.GenerateProceduralEnvironment = false;
            profile.UseProceduralFallback = true;
            profile.OverrideSceneLighting = false;
            EditorUtility.SetDirty(profile);
        }

        private static void ReplaceLayoutMarkerVisuals(RuneRushSceneBindings bindings, BuildAssets assets)
        {
            bindings.EnsureRoots();
            RuneRushRaceLayoutMarker[] markers = bindings.LayoutMarkerRoot.GetComponentsInChildren<RuneRushRaceLayoutMarker>(true);
            foreach (RuneRushRaceLayoutMarker marker in markers)
            {
                for (int index = marker.transform.childCount - 1; index >= 0; index -= 1)
                {
                    Transform child = marker.transform.GetChild(index);
                    if (child.name == DefaultMarkerVisualName || child.name == GeneratedMarkerVisualName)
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }

                GameObject prefab = MarkerPrefab(marker, assets);
                if (prefab == null) continue;

                GameObject visual = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (visual == null) continue;
                visual.name = GeneratedMarkerVisualName;
                visual.transform.SetParent(marker.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                marker.EditorVisualPrefab = prefab;
                EditorUtility.SetDirty(marker);
            }
        }

        private static void ReplaceBakedGuardrailsAndTowers(
            RuneRushBakedSampleEnvironment sample,
            TrackPathData track,
            BuildAssets assets)
        {
            if (sample == null) return;

            Transform rails = FindDeepChild(sample.transform, "05 Guardrails - Editable")
                ?? FindDeepChild(sample.transform, "05 Fence Barriers - Editable");
            if (rails != null)
            {
                rails.name = "05 Fence Barriers - Editable";
                ClearChildren(rails);
                const int segmentCount = 48;
                for (int index = 0; index < segmentCount; index += 1)
                {
                    float beginDistance = track.Length * index / segmentCount;
                    float endDistance = track.Length * (index + 1) / segmentCount;
                    TrackSample first = track.Sample(beginDistance);
                    TrackSample second = track.Sample(endDistance);
                    Vector3 direction = second.Position - first.Position;
                    float length = Mathf.Max(1f, direction.magnitude + 0.8f);
                    Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    float centerDistance = (beginDistance + endDistance) * 0.5f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        TrackSample fenceSample = track.Sample(centerDistance, side * 10.35f, 0.04f);
                        GameObject fence = InstantiateSceneModel(assets.SceneModels[FenceBarrierId], rails,
                            $"Fence {(side < 0 ? "L" : "R")} {index + 1:000}", fenceSample.Position,
                            rotation, 1f, true);
                        FitSceneModel(fence, new Vector3(0.85f, 1.75f, length));
                    }
                }
            }

            Transform towers = FindDeepChild(sample.transform, "08 Kingdom Watchtowers - Editable");
            if (towers == null) return;
            ClearChildren(towers);
            for (int index = 0; index < 12; index += 1)
            {
                float distance = index / 12f * track.Length + 18f;
                float lane = (index % 2 == 0 ? -1f : 1f) * (39f + index % 3 * 8f);
                TrackSample towerSample = track.Sample(distance, lane, 0f);
                InstantiateSceneModel(assets.SceneModels[KeepTowerId], towers, $"Kingdom Watchtower {index + 1:00}",
                    towerSample.Position, Quaternion.Euler(0f, index * 37f, 0f), 15.5f + index % 3 * 1.5f, true);
            }
        }

        private static void ClearChildren(Transform root)
        {
            for (int index = root.childCount - 1; index >= 0; index -= 1)
            {
                UnityEngine.Object.DestroyImmediate(root.GetChild(index).gameObject);
            }
        }

        private static void FitSceneModel(GameObject instance, Vector3 targetSize)
        {
            if (instance == null) return;
            instance.transform.localScale = Vector3.one;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (!TryCalculateLocalBounds(instance.transform, renderers, out Bounds bounds)) return;

            instance.transform.localScale = new Vector3(
                bounds.size.x > 0.0001f ? targetSize.x / bounds.size.x : 1f,
                bounds.size.y > 0.0001f ? targetSize.y / bounds.size.y : 1f,
                bounds.size.z > 0.0001f ? targetSize.z / bounds.size.z : 1f);
        }

        private static bool TryCalculateLocalBounds(Transform root, Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                Bounds local = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(x, y, z));
                            Vector3 point = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                            if (!found)
                            {
                                bounds = new Bounds(point, Vector3.zero);
                                found = true;
                            }
                            else
                            {
                                bounds.Encapsulate(point);
                            }
                        }
                    }
                }
            }
            return found;
        }

        private static GameObject MarkerPrefab(RuneRushRaceLayoutMarker marker, BuildAssets assets)
        {
            if (marker.Kind == RuneRushLayoutMarkerKind.Pickup) return assets.Pickup;
            if (marker.Kind == RuneRushLayoutMarkerKind.Pad) return marker.PadKind == PadKind.Boost ? assets.BoostPad : assets.RepairPad;
            return marker.ObstacleKind switch
            {
                ObstacleKind.Crate => assets.Crate,
                ObstacleKind.Barricade => assets.Barricade,
                ObstacleKind.Oil => assets.Oil,
                ObstacleKind.Spinner => assets.Spinner,
                ObstacleKind.Gate => assets.Gate,
                _ => null
            };
        }

        private static Transform EnsureCustomContentRoot(RuneRushBakedSampleEnvironment sample)
        {
            if (sample.CustomContentRoot != null) return sample.CustomContentRoot;
            Transform existing = sample.transform.Find("CUSTOM CONTENT - Safe From Rebuild");
            if (existing == null)
            {
                existing = new GameObject("CUSTOM CONTENT - Safe From Rebuild").transform;
                existing.SetParent(sample.transform, false);
            }
            sample.CustomContentRoot = existing;
            EditorUtility.SetDirty(sample);
            return existing;
        }

        private static Transform RecreateGeneratedSceneRoot(Transform customContent)
        {
            Transform oldRoot = customContent.Find(GeneratedSceneRootName);
            if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);
            Transform root = new GameObject(GeneratedSceneRootName).transform;
            root.SetParent(customContent, false);
            return root;
        }

        private static void DisablePrimitiveGround(RuneRushBakedSampleEnvironment sample)
        {
            Transform ground = FindDeepChild(sample.transform, "Highcrag Ground");
            if (ground != null) ground.gameObject.SetActive(false);
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                Transform nested = FindDeepChild(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void PlaceTrackModel(GameObject prefab, Transform parent, string name, TrackPathData track, Terrain terrain,
            float ratio, float lane, float size, bool terrainGrounded, float yawOffset)
        {
            TrackSample sample = track.Sample(track.Length * ratio, lane);
            Vector3 position = sample.Position;
            if (terrainGrounded) position.y = TerrainHeight(terrain, position);
            Quaternion rotation = Quaternion.LookRotation(sample.Tangent, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
            InstantiateSceneModel(prefab, parent, name, position, rotation, size, true);
        }

        private static GameObject InstantiateSceneModel(GameObject prefab, Transform parent, string name, Vector3 position,
            Quaternion rotation, float size, bool makeStatic)
        {
            if (prefab == null) return null;
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) return null;
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * size;
            if (makeStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(instance,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
            return instance;
        }

        private static float TerrainHeight(Terrain terrain, Vector3 position)
        {
            return terrain != null ? terrain.SampleHeight(position) + terrain.transform.position.y : position.y;
        }

        private static bool TryCalculateBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static string ModelPath(string id)
        {
            string folder = "tripo_convert_" + id;
            return SourceRoot + "/" + folder + "/" + folder + ".fbx";
        }

        private static void EnsureFolders()
        {
            EnsureFolder(GeneratedAssetRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder(MaterialRoot);
            EnsureFolder(TerrainRoot);
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

        private enum PrefabShape
        {
            Standard,
            Vehicle,
            Spinner,
            Fence
        }

        private sealed class BuildAssets
        {
            public GameObject PlayerVehicle;
            public GameObject AiVehicle;
            public GameObject[] RacerVehicles;
            public GameObject Pickup;
            public GameObject Mine;
            public GameObject Crate;
            public GameObject Barricade;
            public GameObject Oil;
            public GameObject Spinner;
            public GameObject Gate;
            public GameObject BoostPad;
            public GameObject RepairPad;
            public readonly Dictionary<string, GameObject> SceneModels = new Dictionary<string, GameObject>();

            public bool IsValid => PlayerVehicle != null
                && AiVehicle != null
                && RacerVehicles != null
                && RacerVehicles.Length == VehicleIds.Length
                && Array.TrueForAll(RacerVehicles, vehicle => vehicle != null)
                && Pickup != null
                && Crate != null
                && Barricade != null
                && Gate != null
                && SceneModels.Count > 0;
        }
    }
}
