using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorrowedHex.Data;
using BorrowedHex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Repeatable project/scene bootstrap. Safe to run any number of times: every object it
    /// owns is found by name and updated in place, and generated children are rebuilt, so a
    /// second run never duplicates the arena, camera or light. Objects it does not own are
    /// left alone.
    ///
    /// Invoke from the menu, or from the CLI:
    ///   unity command eval "BorrowedHex.EditorTools.ProjectBootstrap.Run(); return \"ok\";"
    /// </summary>
    public static class ProjectBootstrap
    {
        public const string Root = "Assets/Game";
        public const string ScenesDir = Root + "/Scenes";
        public const string DataDir = Root + "/Data";
        public const string MaterialsDir = DataDir + "/Materials";
        public const string ConfigPath = DataDir + "/GameConfig.asset";
        public const string BootstrapScene = ScenesDir + "/Bootstrap.unity";
        public const string ArenaScene = ScenesDir + "/Arena.unity";

        [MenuItem("Borrowed Hex/Bootstrap Project")]
        public static void RunFromMenu() => Run(false);

        [MenuItem("Borrowed Hex/Bootstrap Project (reset tuning to code defaults)")]
        public static void RunResetFromMenu() => Run(true);

        /// <param name="resetConfig">
        /// Recreate GameConfig.asset from code defaults. Serialized assets keep old values when
        /// a default changes in code; use this until hand-tuning starts, never after.
        /// </param>
        public static void Run(bool resetConfig = false)
        {
            if (resetConfig && AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null)
                AssetDatabase.DeleteAsset(ConfigPath);
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Data");
            EnsureFolder(DataDir, "Materials");

            var config = EnsureConfig();
            BuildBootstrapScene();
            BuildArenaScene(config);
            ApplyBuildSettings();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Bootstrap] Project bootstrap complete.");
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        static GameConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            config.unlitMaterial = EnsureMaterial("Unlit", "Universal Render Pipeline/Unlit", Color.white);
            config.litMaterial = EnsureMaterial("Lit", "Universal Render Pipeline/Lit", Color.white);
            EditorUtility.SetDirty(config);
            return config;
        }

        static Material EnsureMaterial(string name, string shaderName, Color color)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) throw new System.InvalidOperationException("Missing shader " + shaderName);
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_ReceiveShadows"))
            {
                // WebGL: URP Lit draws that receive shadows fail with "Mismatch between texture
                // format and sampler type" in Chrome/ANGLE and the whole arena renders blank.
                // Placeholders use blob shadows for grounding instead (see Docs/DECISIONS.md).
                mat.SetFloat("_ReceiveShadows", 0f);
                mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Scene OpenOrCreate(string path)
        {
            if (File.Exists(path)) return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }

        static GameObject FindOrCreateRoot(Scene scene, string name)
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go == null)
            {
                go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            return go;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void BuildBootstrapScene()
        {
            var scene = OpenOrCreate(BootstrapScene);
            var loader = FindOrCreateRoot(scene, "BootstrapLoader");
            Ensure<BootstrapLoader>(loader);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildArenaScene(GameConfig config)
        {
            var scene = OpenOrCreate(ArenaScene);

            // Camera: fixed and elevated; gameplay never moves it (no shake displacing aim).
            var camGo = FindOrCreateRoot(scene, "Main Camera");
            camGo.tag = "MainCamera";
            var cam = Ensure<Camera>(camGo);
            Ensure<AudioListener>(camGo);
            cam.fieldOfView = config.cameraRig.fieldOfView;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.09f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.transform.position = config.cameraRig.position;
            camGo.transform.rotation = Quaternion.LookRotation(config.cameraRig.lookAt - config.cameraRig.position, Vector3.up);

            var sunGo = FindOrCreateRoot(scene, "Sun");
            var sun = Ensure<Light>(sunGo);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.shadows = LightShadows.None; // see _ReceiveShadows note in EnsureMaterial
            sunGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.4f, 0.5f);

            BuildArenaGeometry(scene, config);

            var rootGo = FindOrCreateRoot(scene, "GameRoot");
            var root = Ensure<GameRoot>(rootGo);
            root.config = config;
            EditorUtility.SetDirty(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildArenaGeometry(Scene scene, GameConfig config)
        {
            var arenaRoot = FindOrCreateRoot(scene, "Arena");
            // Generated content is owned entirely by this method: wipe and rebuild so layout
            // edits in GameConfig are reflected and repeated runs cannot duplicate pieces.
            for (int i = arenaRoot.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(arenaRoot.transform.GetChild(i).gameObject);

            var layout = config.arena;
            var floorMat = EnsureMaterial("Floor", "Universal Render Pipeline/Lit", new Color(0.33f, 0.36f, 0.3f));
            var wallMat = EnsureMaterial("Wall", "Universal Render Pipeline/Lit", new Color(0.45f, 0.4f, 0.48f));
            var pillarMat = EnsureMaterial("Pillar", "Universal Render Pipeline/Lit", new Color(0.55f, 0.48f, 0.4f));

            var b = layout.bounds;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(arenaRoot.transform, false);
            // Top face sits at y = 0, the gameplay plane.
            floor.transform.position = new Vector3(b.center.x, -0.25f, b.center.y);
            floor.transform.localScale = new Vector3(b.width + 2 * layout.wallThickness, 0.5f, b.height + 2 * layout.wallThickness);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            var obstacles = layout.BuildObstacles();
            for (int i = 0; i < obstacles.Count; i++)
            {
                var r = obstacles[i];
                bool isPillar = i >= 4;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = isPillar ? $"Pillar_{i - 4}" : $"Wall_{i}";
                go.transform.SetParent(arenaRoot.transform, false);
                float h = isPillar ? layout.wallHeight * 1.6f : layout.wallHeight;
                go.transform.position = new Vector3(r.center.x, h * 0.5f, r.center.y);
                go.transform.localScale = new Vector3(r.width, h, r.height);
                go.GetComponent<Renderer>().sharedMaterial = isPillar ? pillarMat : wallMat;
                // Simulation collides analytically against GameConfig data; PhysX colliders on
                // the visuals would be a second, divergent collision model.
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
        }

        static void ApplyBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScene, true),
                new EditorBuildSettingsScene(ArenaScene, true),
            };
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Borrowed Hex";
            PlayerSettings.companyName = "BorrowedHex";
            PlayerSettings.runInBackground = false;
            // Uncompressed Web output until hosting headers are known (section 8).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
        }
    }
}
