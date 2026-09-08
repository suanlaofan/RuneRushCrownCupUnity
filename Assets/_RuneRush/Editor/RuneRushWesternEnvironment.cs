using System;
using System.IO;
using System.Linq;
using RuneRush.UnityRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuneRush.Editor
{
    public static class RuneRushWesternEnvironment
    {
        private const string Root = "Assets/_RuneRush/Art/WesternKingdom";
        private const string GroupName = "WESTERN KINGDOM - Authored September 2026";
        private static readonly string[] Names = { "RoyalWall", "WallTower", "TwinGate", "GuildHouse", "StoneTower", "RuinedArch", "Tavern", "CrownGate", "TownHouse" };
        private static TrackPathData _track;
        private static Terrain _terrain;
        private static Material _stone, _gold, _teal;

        [MenuItem("Rune Rush/Revision/Prepare Western Assets")]
        public static void PrepareAssets()
        {
            foreach (string name in Names)
            {
                string folder = Root + "/" + name;
                string fbxPath = folder + "/" + name + ".fbx";
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing model: " + fbxPath);
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.isReadable = false;
                importer.SaveAndReimport();
                string imagePath = folder + "/" + name + "_Texture_0_0.png";
                var textureImporter = AssetImporter.GetAtPath(imagePath) as TextureImporter;
                textureImporter.maxTextureSize = 1024;
                textureImporter.mipmapEnabled = true;
                textureImporter.sRGBTexture = true;
                textureImporter.textureCompression = TextureImporterCompression.Compressed;
                textureImporter.SaveAndReimport();
                Material material = MaterialAsset(folder + "/" + name + ".mat", Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath));
                material.SetFloat("_Smoothness", 0.12f);
                material.enableInstancing = true;
                var root = new GameObject(name);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath));
                model.transform.SetParent(root.transform, false);
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                Bounds bounds = BoundsOf(root);
                float scale = 1f / Mathf.Max(0.001f, bounds.size.y);
                model.transform.localScale *= scale;
                bounds = BoundsOf(root);
                model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.012f, root.GetComponentsInChildren<Renderer>()) });
                lod.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("RUNE_RUSH_WESTERN_ASSETS_OK: 9 normalized textured prefabs");
        }

        [MenuItem("Rune Rush/Revision/Build Western Sample")]
        public static void BuildSample() => Build(false);

        [MenuItem("Rune Rush/Revision/Build Full Western Circuit")]
        public static void BuildFull() => Build(true);

        private static void Build(bool full)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
            var bindings = UnityEngine.Object.FindAnyObjectByType<RuneRushSceneBindings>();
            var authoring = UnityEngine.Object.FindAnyObjectByType<RuneRushTrackAuthoring>();
            if (bindings == null || authoring == null) throw new InvalidOperationException("Open RuneRushRace first.");
            _track = authoring.CreatePath();
            _terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (_terrain == null) throw new InvalidOperationException("Authored Terrain is missing.");
            // Preserve the original terrain asset; the revision works on a project-local copy.
            string terrainPath = Root + "/WesternTerrain.asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
            if (data == null)
            {
                data = UnityEngine.Object.Instantiate(_terrain.terrainData);
                data.name = "Western Kingdom Terrain";
                AssetDatabase.CreateAsset(data, terrainPath);
                SoftenRoadCorridor(data);
            }
            Undo.RecordObject(_terrain, "Western terrain reference");
            _terrain.terrainData = data;
            var terrainCollider = _terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null) { Undo.RecordObject(terrainCollider, "Western terrain collider"); terrainCollider.terrainData = data; }
            // Unity 6 terrain defaults to diffuse-alpha smoothness; opaque grass then looks like polished ice.
            // Keep original layers intact and explicitly use a rough constant on revision-only copies.
            TerrainLayer[] layers = data.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                string layerPath = Root + "/WesternLayer" + i + ".terrainlayer";
                TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (layer == null) { layer = UnityEngine.Object.Instantiate(layers[i]); AssetDatabase.CreateAsset(layer, layerPath); }
                layer.smoothness = 0.04f;
                layer.metallic = 0;
                var serializedLayer = new SerializedObject(layer);
                serializedLayer.FindProperty("m_SmoothnessSource").intValue = 2; // Constant, per installed URP TerrainLitPasses.
                serializedLayer.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(layer);
                layers[i] = layer;
            }
            data.terrainLayers = layers;
            EditorUtility.SetDirty(data);
            _terrain.heightmapPixelError = 8;
            _terrain.basemapDistance = 150;
            var oldRoot = bindings.AuthoredEnvironmentRoot.Find(GroupName);
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot.gameObject);
            Transform root = Group(bindings.AuthoredEnvironmentRoot, GroupName);
            _stone = MaterialAsset(Root + "/KingdomStone.mat", new Color(0.48f, 0.46f, 0.38f));
            _gold = MaterialAsset(Root + "/CrownGold.mat", new Color(0.78f, 0.52f, 0.16f));
            _teal = MaterialAsset(Root + "/CrownTeal.mat", new Color(0.07f, 0.3f, 0.3f));
            _gold.SetFloat("_Metallic", 0.45f);
            ConfigureLighting(root);
            // Disable only the named generated decorations identified in the baseline inspection.
            string[] replaced = { "02 Watchtowers and Ruins", "03 Bridge Landmarks", "05 Fantasy Forest", "06 Trackside Props", "07 Crown Keep Model District", "08 Vehicle Paddock", "09 Track Feature Models", "10 Gate Braziers", "08 Kingdom Watchtowers - Editable", "11 Sky Landmark - Editable", "12 Lighting - Edit or Replace" };
            foreach (Transform child in bindings.AuthoredEnvironmentRoot.GetComponentsInChildren<Transform>(true))
            {
                if (replaced.Contains(child.name)) { Undo.RecordObject(child.gameObject, "Replace generated scenery"); child.gameObject.SetActive(false); }
            }
            Transform city = Group(root, "01 Royal Starting District");
            Transform forest = Group(root, "02 Rune Woodland");
            Transform ruins = Group(root, "03 Valley Ruins");
            Transform finish = Group(root, "04 Crown Finale");
            int buildings = 0;
            for (int i = 0; i < (full ? 40 : 8); i++)
            {
                float ratio = i / 40f;
                int district = ratio < 0.24f ? 0 : ratio < 0.52f ? 1 : ratio < 0.76f ? 2 : 3;
                if (district == 1 && i % 3 != 0) continue;
                Transform parent = district == 0 ? city : district == 1 ? forest : district == 2 ? ruins : finish;
                string model = district == 2 ? (i % 2 == 0 ? "RuinedArch" : "StoneTower") : i % 4 == 0 ? "TwinGate" : i % 3 == 0 ? "Tavern" : "TownHouse";
                for (int side = -1; side <= 1; side += 2)
                {
                    float height = district == 2 ? 9f + i % 3 * 2 : 8f + i % 3 * 1.5f;
                    Place(parent, model, ratio + (side > 0 ? 0.006f : 0), side * (23 + i % 3 * 3), height, side < 0 ? 90 : -90);
                    buildings++;
                }
            }
            // Larger landmarks stay off the racing corridor so a closed model arch cannot block the route.
            Place(city, "CrownGate", 0.015f, -46, 22, 90);
            Place(city, "GuildHouse", 0.075f, 47, 17, -90);
            for (int i = 0; i < (full ? 16 : 5); i++)
            {
                float ratio = i < 8 ? i * 0.026f : 0.79f + (i-8)*0.026f;
                Place(i < 8 ? city : finish, i % 3 == 0 ? "WallTower" : "RoyalWall", ratio, -39, i % 3 == 0 ? 12 : 5.6f, 90);
            }
            if (full)
            {
                Place(ruins, "TwinGate", 0.62f, -48, 23, 70);
                Place(finish, "CrownGate", 0.94f, 46, 24, -90);
                Place(finish, "GuildHouse", 0.86f, -47, 17, 90);
            }
            AddForest(forest, full);
            AddRaceDressing(root, full);
            EditorSceneManager.MarkSceneDirty(bindings.gameObject.scene);
            EditorSceneManager.SaveScene(bindings.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("RUNE_RUSH_WESTERN_SCENE_OK: full=" + full + "; roadside_buildings=" + buildings + "; track geometry and gameplay layout retained");
        }

        private static void SoftenRoadCorridor(TerrainData data)
        {
            int resolution = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            Vector3[] points = Enumerable.Range(0, 385).Select(i => _track.Sample(_track.Length*i/384f).Position).ToArray();
            Vector3 terrainPosition = _terrain.transform.position;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float wx = terrainPosition.x + x/(float)(resolution-1)*data.size.x;
                float wz = terrainPosition.z + z/(float)(resolution-1)*data.size.z;
                float best = float.MaxValue, roadY = 0;
                for (int i = 0; i < points.Length-1; i++)
                {
                    Vector3 a=points[i], b=points[i+1];
                    float dx=b.x-a.x, dz=b.z-a.z;
                    float t=Mathf.Clamp01(((wx-a.x)*dx+(wz-a.z)*dz)/(dx*dx+dz*dz+0.0001f));
                    float ex=wx-a.x-dx*t, ez=wz-a.z-dz*t;
                    float d=ex*ex+ez*ez;
                    if (d<best) { best=d; roadY=Mathf.Lerp(a.y,b.y,t); }
                }
                float distance=Mathf.Sqrt(best);
                if (distance>65) continue;
                float original=heights[z,x]*data.size.y+terrainPosition.y;
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(27,65,distance));
                heights[z,x]=Mathf.Clamp01((Mathf.Lerp(roadY-0.55f,original,blend)-terrainPosition.y)/data.size.y);
            }
            data.SetHeights(0,0,heights);
            EditorUtility.SetDirty(data);
        }

        private static void AddForest(Transform root, bool full)
        {
            string[] trees = { "12ac5a87-fcd3-4eff-9768-cd3d885d4922", "1d10e098-4205-49b0-ae51-91fbd6879b02" };
            for (int i=0;i<(full?120:24);i++)
            {
                float ratio = i/120f;
                string path="Assets/_RuneRush/Settings/YouximoxingGenerated/Prefabs/Scene_"+trees[0]+".prefab";
                GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)continue;
                float side=i%2==0?-1:1;
                TrackSample sample=_track.Sample(_track.Length*ratio,side*(34+(i*13)%32));
                var tree=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                tree.name="Woodland Tree "+i.ToString("000"); tree.transform.SetParent(root,false);
                Bounds bounds=BoundsOf(tree);float height=7+(i*7)%10;
                tree.transform.localScale*=height/Mathf.Max(.01f,bounds.size.y);
                tree.transform.rotation=Quaternion.Euler(0,i*137.5f,0);
                tree.transform.position=sample.Position;
                Ground(tree);
            }
        }

        private static void AddRaceDressing(Transform root, bool full)
        {
            Transform dressing=Group(root,"05 Crown Race Markers");
            for(int i=0;i<(full?48:10);i++)
            {
                float ratio=i/48f;
                for(int side=-1;side<=1;side+=2)
                {
                    TrackSample sample=_track.Sample(_track.Length*ratio,side*12f);
                    var pole=Group(dressing,"Crown Banner "+i+" "+side);
                    pole.position=sample.Position;
                    pole.rotation=Quaternion.LookRotation(sample.Tangent,Vector3.up);
                    Cube(pole,"Stone foot",new Vector3(0,.1f,0),new Vector3(.8f,.6f,.8f),_stone);
                    Cube(pole,"Pole",new Vector3(0,2.6f,0),new Vector3(.12f,5,.12f),_gold);
                    Cube(pole,"Teal banner",new Vector3(side*.62f,3.6f,0),new Vector3(1.2f,1.7f,.06f),_teal);
                    Cube(pole,"Crown stripe",new Vector3(side*.62f,3.5f,-.045f),new Vector3(.85f,.18f,.025f),_gold);
                }
            }
            // A clear race arch, built to the actual road width, frames the start/finish line.
            Transform gate=Group(dressing,"Crown Cup Start Finish Arch");
            TrackSample start=_track.Sample(0);
            gate.position=start.Position;gate.rotation=Quaternion.LookRotation(start.Tangent,Vector3.up);
            foreach(int side in new[]{-1,1})
            {
                Cube(gate,"Tower footing",new Vector3(side*11.5f,3,0),new Vector3(2.2f,6,2.2f),_stone);
                Cube(gate,"Gold tower band",new Vector3(side*11.5f,5.5f,0),new Vector3(2.3f,.35f,2.3f),_gold);
            }
            Cube(gate,"Race arch beam",new Vector3(0,7,0),new Vector3(25,1.6f,1.4f),_teal);
            Cube(gate,"Gold arch cornice",new Vector3(0,7.85f,0),new Vector3(25.5f,.18f,1.6f),_gold);
            for(int i=-2;i<=2;i++)Cube(gate,"Crown crest",new Vector3(i*.65f,8.3f+(i%2==0?.25f:0),0),new Vector3(.5f,1,1),_gold);
        }

        private static void ConfigureLighting(Transform root)
        {
            var sunObject=new GameObject("Kingdom Late Afternoon Sun",typeof(Light));sunObject.transform.SetParent(root,false);
            sunObject.transform.rotation=Quaternion.Euler(38,-32,0);
            Light sun=sunObject.GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.35f;
            sun.color=new Color(1,.88f,.72f);sun.shadows=LightShadows.Soft;sun.shadowStrength=.7f;
            RenderSettings.sun=sun;
            Material sky=AssetDatabase.LoadAssetAtPath<Material>(Root+"/KingdomSky.mat");
            if(sky==null){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,Root+"/KingdomSky.mat");}
            sky.SetFloat("_Exposure",1.1f);sky.SetFloat("_AtmosphereThickness",.85f);sky.SetFloat("_SunSize",.03f);
            sky.SetColor("_SkyTint",new Color(.53f,.60f,.68f));sky.SetColor("_GroundColor",new Color(.36f,.40f,.35f));
            RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.48f,.58f,.72f);
            RenderSettings.ambientEquatorColor=new Color(.34f,.40f,.44f);
            RenderSettings.ambientGroundColor=new Color(.23f,.25f,.19f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogStartDistance=120;RenderSettings.fogEndDistance=490;RenderSettings.fogColor=new Color(.56f,.65f,.69f);
            EditorUtility.SetDirty(sky);
        }

        private static void Place(Transform parent,string name,float ratio,float lane,float height,float yaw)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+name+"/"+name+".prefab");
            if(prefab==null)throw new InvalidOperationException("Prepare assets first: "+name);
            TrackSample sample=_track.Sample(_track.Length*Mathf.Repeat(ratio,1),lane);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.name=name+" "+ratio.ToString("F3");
            instance.transform.SetParent(parent,false);instance.transform.localScale=Vector3.one*height;
            instance.transform.SetPositionAndRotation(sample.Position,Quaternion.LookRotation(sample.Tangent,Vector3.up)*Quaternion.Euler(0,yaw,0));
            Ground(instance);
        }

        private static void Ground(GameObject instance)
        {
            Bounds b=BoundsOf(instance);float y=_terrain.SampleHeight(instance.transform.position)+_terrain.transform.position.y;
            instance.transform.position+=Vector3.up*(y-b.min.y-.12f);
            foreach(Transform t in instance.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
        }

        private static Bounds BoundsOf(GameObject go)
        {
            Renderer[] r=go.GetComponentsInChildren<Renderer>();
            Bounds b=r.Length>0?r[0].bounds:new Bounds(go.transform.position,Vector3.one);
            for(int i=1;i<r.Length;i++)b.Encapsulate(r[i].bounds);return b;
        }

        private static Transform Group(Transform parent,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Western environment");return go.transform;
        }

        private static Material MaterialAsset(string path,Color color)
        {
            Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.16f);m.enableInstancing=true;return m;
        }

        private static void Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
        }
    }
}
