using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButterflyEffect.Guidance.Editor
{
    public static class GuidanceDemoBuilder
    {
        public const string Root = "Assets/Yao/ButterflyGuidance";
        public const string ScenePath = Root + "/Scenes/Yao_ButterflyGuidance_Demo.unity";
        public const string LibraryPath = Root + "/Data/ButterflyGuidance_English.asset";
        public const string PrefabPath = Root + "/Prefabs/ButterflyGuidance.prefab";

        [MenuItem("Butterfly Effect/Open Guidance Demo")]
        public static void OpenDemo()
        {
            if (!File.Exists(ScenePath)) BuildDemo();
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        [MenuItem("Butterfly Effect/Rebuild Guidance Demo")]
        public static void BuildDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Data");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            var library = AssetDatabase.LoadAssetAtPath<GuidanceLibrary>(LibraryPath);
            if (library == null)
            {
                library = GuidanceLibrary.CreateDefault();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            var opening = library.Find("campus.opening");
            if (opening != null && opening.text == "The Butterfly Effect\nSmall choices. Significant consequences.")
            {
                opening.text = "Stay close. I have something to show you.";
                opening.choices[0].nextId = "campus.portal";
                EditorUtility.SetDirty(library);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Yao — Butterfly Guidance Demo";
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 1.8f, -6);
            cameraObject.transform.LookAt(new Vector3(0, 1.8f, 0));
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 44;
            camera.orthographic = true;
            camera.orthographicSize = 3;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .055f, .07f);
            camera.allowHDR = true;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.3f, .32f, .35f);
            var light = new GameObject("Butterfly light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = Color.white;
            light.transform.rotation = Quaternion.Euler(25, -25, 0);

            var system = new GameObject("Butterfly Guidance");
            var director = system.AddComponent<GuidanceDirector>();
            director.library = library;
            var view = system.AddComponent<GuidanceView>();
            view.director = director;
            view.showDemoHeadings = false;
            view.headingFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/GuidanceSerif.ttf");
            view.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/GuidanceSans.ttf");
            view.Build();
            AddUlysses(system.transform);
            // The reusable prefab contains only dialogue, options and the actual butterfly.
            PrefabUtility.SaveAsPrefabAsset(system, PrefabPath);
            UnityEngine.Object.DestroyImmediate(system);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            director = instance.GetComponent<GuidanceDirector>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var existing = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            existing.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = existing.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Butterfly Guidance demo ready: " + ScenePath + " (" + library.lines.Length + " dialogue entries)");
        }

        // Command-line entry point. Fails the process if compilation, graph, or behavioral validation fails.
        public static void BuildBatch()
        {
            BuildDemo();
            GuidanceValidation.ValidateCore();
            string path = Environment.GetEnvironmentVariable("BUTTERFLY_PACKAGE_PATH");
            if (!string.IsNullOrEmpty(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.ExportPackage(Root, path, ExportPackageOptions.Recurse);
                Debug.Log("Exported reusable package: " + path);
            }
        }

        public static void BuildUlyssesBatch()
        {
            UlyssesModelBake.Bake();
            BuildBatch();
        }

        private static void AddUlysses(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UlyssesModelBake.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("The native Ulysses Butterfly prefab is missing.");
            var guide = new GameObject("Ulysses Butterfly").AddComponent<GuidanceButterfly>();
            guide.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.transform.SetParent(guide.transform, false);
            model.transform.localRotation = Quaternion.AngleAxis(165, Vector3.forward) * Quaternion.Euler(-65, -20, 0);
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float scale = 3.6f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            model.transform.localScale *= scale;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= bounds.center;
            guide.transform.localPosition = new Vector3(0, 2.7f, 0);
            Debug.Log("Ulysses presentation: source bounds " + bounds.size + ", display scale " + scale);
        }

        public static void OpenAndPlay()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.delayCall += () => { EditorApplication.isPlaying = true; };
        }

        [MenuItem("Butterfly Effect/Validate Dialogue Flow")]
        public static void Validate() { GuidanceValidation.ValidateCore(); }

        [MenuItem("Butterfly Effect/Capture Game Preview")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) { Debug.Log("Enter Play Mode before capturing a preview."); return; }
            string path = EditorUtility.SaveFilePanel("Save Game Preview", "", "ButterflyGuidance.png", "png");
            if (!string.IsNullOrEmpty(path)) ScreenCapture.CaptureScreenshot(path);
        }
    }
}
