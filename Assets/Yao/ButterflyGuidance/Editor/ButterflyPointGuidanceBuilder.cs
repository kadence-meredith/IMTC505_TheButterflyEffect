using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ButterflyEffect.Guidance.Editor
{
    public static class ButterflyPointGuidanceBuilder
    {
        public const string Root = "Assets/Yao/ButterflyGuidance";
        public const string ScenePath = Root + "/Scenes/Yao_ButterflyPointGuidance_Demo.unity";
        public const string GuidePrefabPath = Root + "/Prefabs/ButterflyPointGuide.prefab";
        private const string Art = Root + "/Art/PointGuideDemo";

        [MenuItem("Butterfly Effect/Open Point-to-Point Demo")]
        public static void OpenDemo()
        {
            if (!File.Exists(ScenePath)) BuildDemo();
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Butterfly Effect/Rebuild Point-to-Point Demo")]
        public static void BuildDemo()
        {
            ButterflyGuidedDialogueBuilder.BuildDemo();
        }

        public static void BuildBaseScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Yao — Point-to-Point Butterfly Guidance";
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.35f, .4f, .45f);
            var sun = new GameObject("Neutral preview light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(35, -25, 0);

            var guide = MakeGuidePrefab();
            var points = new GameObject("Guide points - replace with your own scene anchors");
            var pointA = new GameObject("Point A").transform;
            pointA.SetParent(points.transform);
            pointA.position = new Vector3(0, 0, 1);
            var pointB = new GameObject("Point B").transform;
            pointB.SetParent(points.transform);
            pointB.position = new Vector3(0, 0, 8);

            var player = new GameObject("Demo user - WASD controls").AddComponent<ButterflyGuideDemoPlayer>();
            player.transform.position = new Vector3(0, 0, -1.2f);
            player.guide = guide;
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.6f, 0);
            camera.transform.localRotation = Quaternion.Euler(4, 0, 0);
            camera.fieldOfView = 65;
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .055f, .07f);
            player.view = camera.transform;

            guide.user = player.transform;
            guide.points = new[] { pointA, pointB };
            guide.ResetToStart();
            EditorUtility.SetDirty(guide);
            PrefabUtility.RecordPrefabInstancePropertyModifications(guide);
            PrefabUtility.RecordPrefabInstancePropertyModifications(guide.transform);

            MakeNeutralStage(pointA, pointB);
            MakeUI(guide, player);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Butterfly point guidance demo ready: " + ScenePath);
        }

        public static ButterflyPointGuide MakeGuidePrefab()
        {
            const string modelPath = Root + "/Art/Ulysses/UlyssesButterfly.prefab";
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab == null || modelPrefab.GetComponent<LowPolyButterflyWings>() == null)
            {
                LowPolyButterflyBuilder.Build();
                modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            }
            var root = new GameObject("Butterfly Point Guide");
            var guide = root.AddComponent<ButterflyPointGuide>();
            guide.autoAdvance = true;
            guide.playOnStart = false;
            var hover = new GameObject("Butterfly visual - bobbing only").AddComponent<GuidanceButterfly>();
            hover.transform.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.transform.SetParent(hover.transform, false);
            LowPolyButterflyBuilder.NormalizeInstances(root);
            PrefabUtility.SaveAsPrefabAsset(root, GuidePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GuidePrefabPath))).GetComponent<ButterflyPointGuide>();
        }

        public static void MakeNeutralStage(Transform a, Transform b)
        {
            var stage = new GameObject("Demo reference grid - not campsite scenery").transform;
            var floorMaterial = MakeMaterial("Neutral floor", new Color(.035f, .075f, .09f), false);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Neutral walking surface";
            floor.transform.SetParent(stage, false);
            floor.transform.position = new Vector3(0, -.02f, 4);
            floor.transform.localScale = new Vector3(2.4f, 1, 2.4f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            var grid = MakeMaterial("Faint grid", new Color(.065f, .14f, .17f), true);
            for (int i = -10; i <= 12; i++)
            {
                Line(stage, "Grid", new[] { new Vector3(i, 0, -8), new Vector3(i, 0, 16) }, grid, .012f);
                Line(stage, "Grid", new[] { new Vector3(-12, 0, i), new Vector3(12, 0, i) }, grid, .012f);
            }
            var marker = MakeMaterial("Point marker", new Color(.38f, .88f, .86f), true);
            foreach (var point in new[] { a, b })
            {
                var circle = Enumerable.Range(0, 65).Select(i => point.position + new Vector3(Mathf.Cos(i * Mathf.PI / 32) * .42f, .015f, Mathf.Sin(i * Mathf.PI / 32) * .42f)).ToArray();
                Line(stage, point.name + " marker", circle, marker, .025f);
            }
        }

        private static Material MakeMaterial(string name, Color color, bool unlit)
        {
            string path = Art + "/" + name.Replace(" ", "_") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Line(Transform parent, string name, Vector3[] positions, Material material, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            line.positionCount = positions.Length;
            line.SetPositions(positions);
            line.sharedMaterial = material;
            line.startWidth = line.endWidth = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private static void MakeUI(ButterflyPointGuide guide, ButterflyGuideDemoPlayer player)
        {
            var go = new GameObject("Demo controls - not part of guide prefab", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = go.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1600, 900);
            scale.matchWidthOrHeight = .5f;
            var ui = go.AddComponent<ButterflyGuideDemoUI>();
            ui.guide = guide;
            ui.player = player;
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/GuidanceSans.ttf");
            var heading = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/GuidanceSerif.ttf");
            var cream = new Color(.94f, .95f, .88f);
            var cyan = new Color(.38f, .88f, .86f);
            var root = (RectTransform)go.transform;
            TextAt(root, "Camp", "Camp", 40, cream, new Vector2(.05f, .87f), new Vector2(.4f, .95f), heading);
            ui.route = TextAt(root, "Route", "A  >  B", 15, cyan, new Vector2(.65f, .88f), new Vector2(.95f, .95f), font);
            ui.route.alignment = TextAnchor.MiddleRight;
            var card = RectAt(root, "Guide dialogue", new Vector2(.045f, .07f), new Vector2(.955f, .29f));
            card.gameObject.AddComponent<Image>().color = new Color(.027f, .073f, .092f, .97f);
            var accent = RectAt(card, "Accent", new Vector2(0, .99f), Vector2.one).gameObject.AddComponent<Image>();
            accent.color = cyan;
            TextAt(card, "Speaker", "Butterfly", 14, cyan, new Vector2(.035f, .74f), new Vector2(.6f, .94f), font);
            ui.dialogue = TextAt(card, "Dialogue", "Stay close. I have something to show you.", 27, cream, new Vector2(.035f, .27f), new Vector2(.68f, .74f), font);
            var buttonRect = RectAt(card, "Action", new Vector2(.75f, .18f), new Vector2(.965f, .65f));
            buttonRect.gameObject.AddComponent<Image>().color = cyan;
            ui.action = buttonRect.gameObject.AddComponent<Button>();
            ui.actionLabel = TextAt(buttonRect, "Label", "Begin", 17, new Color(.032f, .082f, .105f), Vector2.zero, Vector2.one, font);
            ui.actionLabel.alignment = TextAnchor.MiddleCenter;
            TextAt(root, "Controls", "WASD / arrows: walk     Hold right mouse: look     Space: continue     R: restart", 13, new Color(.6f, .72f, .7f), new Vector2(.05f, .015f), new Vector2(.95f, .06f), font);
        }

        private static RectTransform RectAt(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text TextAt(Transform parent, string name, string value, int size, Color color, Vector2 min, Vector2 max, Font font)
        {
            var text = RectAt(parent, name, min, max).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        public static void BuildBatch()
        {
            ButterflyGuidedDialogueBuilder.BuildBatch();
            return;
        }

        private static void ExportMovementOnlyPackage()
        {
            var package = Environment.GetEnvironmentVariable("BUTTERFLY_POINT_PACKAGE");
            if (!string.IsNullOrEmpty(package))
            {
                string[] paths = {
                    ScenePath, GuidePrefabPath, Root + "/Runtime/ButterflyPointGuide.cs",
                    Root + "/Runtime/ButterflyGuideDemoPlayer.cs", Root + "/Runtime/ButterflyGuideDemoUI.cs",
                    Root + "/Runtime/GuidanceButterfly.cs", Root + "/Editor/ButterflyPointGuidanceBuilder.cs",
                    Root + "/Editor/ButterflyPointGuideValidation.cs", Root + "/Editor/ButterflyPointGuidancePreview.cs",
                    Root + "/Art/Ulysses", Root + "/Art/PointGuideDemo", Root + "/Art/GuidanceSans.ttf",
                    Root + "/Art/GuidanceSerif.ttf", Root + "/Art/manrope-OFL.txt", Root + "/Art/cormorantgaramond-OFL.txt",
                    Root + "/POINT_GUIDANCE_README.md"
                };
                Directory.CreateDirectory(Path.GetDirectoryName(package));
                AssetDatabase.ExportPackage(paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToArray(), package, ExportPackageOptions.Recurse);
                Debug.Log("Point guidance package exported: " + package);
            }
        }
    }
}
