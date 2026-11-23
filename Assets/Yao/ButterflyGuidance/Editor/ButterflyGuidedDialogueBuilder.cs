using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Combines the existing dialogue library with scene-anchor travel, using one clean Canvas.</summary>
    public static class ButterflyGuidedDialogueBuilder
    {
        public const string Root = "Assets/Yao/ButterflyGuidance";
        public const string ScenePath = ButterflyPointGuidanceBuilder.ScenePath;
        public const string PrefabPath = Root + "/Prefabs/ButterflyGuidedDialogue.prefab";

        [MenuItem("Butterfly Effect/Open Guided Dialogue Demo")]
        public static void OpenDemo()
        {
            if (!File.Exists(ScenePath)) BuildDemo();
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Butterfly Effect/Rebuild Guided Dialogue Demo")]
        public static void BuildDemo()
        {
            ButterflyPointGuidanceBuilder.BuildBaseScene();
            var oldUI = UnityEngine.Object.FindFirstObjectByType<ButterflyGuideDemoUI>();
            if (oldUI != null) UnityEngine.Object.DestroyImmediate(oldUI.gameObject);
            var guide = UnityEngine.Object.FindFirstObjectByType<ButterflyPointGuide>();
            var player = UnityEngine.Object.FindFirstObjectByType<ButterflyGuideDemoPlayer>();
            var oldPoints = GameObject.Find("Guide points - replace with your own scene anchors");
            var oldStage = GameObject.Find("Demo reference grid - not campsite scenery");
            guide.user = null;
            guide.points = Array.Empty<Transform>();
            if (oldPoints != null) UnityEngine.Object.DestroyImmediate(oldPoints);
            if (oldStage != null) UnityEngine.Object.DestroyImmediate(oldStage);

            var root = new GameObject("Butterfly Guided Dialogue");
            var director = root.AddComponent<GuidanceDirector>();
            director.library = AssetDatabase.LoadAssetAtPath<GuidanceLibrary>(GuidanceDemoBuilder.LibraryPath);
            if (director.library == null) throw new InvalidOperationException("The original dialogue library is missing.");
            var view = root.AddComponent<GuidanceView>();
            view.director = director;
            view.showDemoHeadings = false;
            view.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/GuidanceSans.ttf");
            view.headingFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/GuidanceSerif.ttf");
            view.Build();
            guide.transform.SetParent(root.transform, false);
            guide.transform.localPosition = Vector3.up * 1.8f;
            guide.flightHeight = 1.8f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(guide);
            PrefabUtility.RecordPrefabInstancePropertyModifications(guide.transform);
            var coordinator = root.AddComponent<GuidanceRouteCoordinator>();
            coordinator.director = director;
            coordinator.view = view;
            coordinator.guide = guide;
            coordinator.stations = Bindings();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            coordinator = instance.GetComponent<GuidanceRouteCoordinator>();
            guide = coordinator.guide;

            var anchors = new GameObject("Dialogue anchors - empty placeholders only").transform;
            string[] names = { "Campus", "Portal", "Campsite", "Cigarette", "Campfire", "Bucket", "Fireworks", "Disposal bin", "Return portal" };
            float[] z = { 0, 3, 6, 8, 11, 13, 15, 12, 4 };
            var targets = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                targets[i] = new GameObject(names[i] + " anchor").transform;
                targets[i].SetParent(anchors, false);
                targets[i].localPosition = new Vector3(0, 0, z[i]);
                coordinator.stations[i].target = targets[i];
            }
            guide.user = player.transform;
            guide.points = targets;
            guide.ResetToStart();
            player.guide = guide;
            player.dialogueDirector = coordinator.director;
            PrefabUtility.RecordPrefabInstancePropertyModifications(coordinator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(guide);
            PrefabUtility.RecordPrefabInstancePropertyModifications(guide.transform);
            ButterflyPointGuidanceBuilder.MakeNeutralStage(targets[3], targets[4]);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static GuidanceStationBinding[] Bindings()
        {
            return new[]
            {
                Binding("Campus", "campus.opening", "campus.butterfly", "campus.reveal", "ending.", "debrief.", "session."),
                Binding("Portal", "campus.portal", "portal.enter"),
                Binding("Campsite", "forest."),
                Binding("Cigarette", "cigarette."),
                Binding("Campfire", "campfire."),
                Binding("Bucket", "bucket."),
                Binding("Fireworks", "fireworks."),
                Binding("Disposal bin", "cigarette.bin", "cigarette.disposed"),
                Binding("Return portal", "return.")
            };
        }

        private static GuidanceStationBinding Binding(string name, params string[] prefixes)
        {
            return new GuidanceStationBinding { name = name, dialoguePrefixes = prefixes };
        }

        public static void BuildBatch()
        {
            GuidanceDemoBuilder.BuildDemo();
            BuildDemo();
            ButterflyPointGuideValidation.Validate();
            GuidanceValidation.ValidateCore();
            GuidedDialogueValidation.Validate();
            var package = Environment.GetEnvironmentVariable("BUTTERFLY_COMBINED_PACKAGE")
                ?? Environment.GetEnvironmentVariable("BUTTERFLY_POINT_PACKAGE");
            if (!string.IsNullOrEmpty(package))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(package));
                AssetDatabase.ExportPackage(Root, package, ExportPackageOptions.Recurse);
                Debug.Log("Guided dialogue package exported: " + package);
            }
        }
    }
}
