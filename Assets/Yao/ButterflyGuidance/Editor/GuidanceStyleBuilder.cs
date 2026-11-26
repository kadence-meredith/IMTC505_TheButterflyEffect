using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButterflyEffect.Guidance.Editor
{
    public static class GuidanceStyleBuilder
    {
        private const string Root = "Assets/Yao/ButterflyGuidance";

        [MenuItem("Tools/Butterfly Effect/Apply Low-Poly Style")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before changing the saved butterfly style.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            LowPolyButterflyBuilder.Build();
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefabs" })
                         .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x))
            {
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    StyleRoot(prefab);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }

            string[] scenes = GuidanceMainSceneBuilder.SceneNames
                .Select(x => GuidanceMainSceneBuilder.SceneFolder + x + ".unity")
                .Concat(AssetDatabase.FindAssets("t:Scene", new[] { Root + "/Scenes" })
                    .Select(AssetDatabase.GUIDToAssetPath)).Distinct().ToArray();
            foreach (string path in scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<GuidanceButterfly>(true) == null
                        && root.GetComponentInChildren<GuidanceView>(true) == null) continue;
                    StyleRoot(root);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Could not save butterfly styling in " + path);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(GuidanceMainSceneBuilder.SceneFolder + "1_Campsite_Main.unity");
            Debug.Log("Low-poly butterfly and natural dialogue styling saved in all main scenes and guidance prefabs.");
        }

        private static void StyleRoot(GameObject root)
        {
            LowPolyButterflyBuilder.NormalizeInstances(root);
            foreach (var wings in root.GetComponentsInChildren<LowPolyButterflyWings>(true))
            {
                if (wings.GetComponentInParent<ButterflyPointGuide>() != null) continue;
                LowPolyButterflyBuilder.NormalizeModel(wings, 3.6f);
                wings.transform.localRotation = Quaternion.Euler(35f, -20f, 0f);
            }
            foreach (var view in root.GetComponentsInChildren<GuidanceView>(true))
            {
                view.Build();
                view.ApplyNaturalStyle();
            }
            foreach (var panel in root.GetComponentsInChildren<GuidanceVRPanel>(true))
                panel.Configure(panel.playerCamera, panel.view);
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        public static void BuildAndValidate()
        {
            Apply();
            GuidanceValidation.ValidateCore();
            ButterflyPointGuideValidation.Validate();
            GuidedDialogueValidation.Validate();
            GuidanceCampsiteSequenceValidation.RunAll();
            GuidanceStyleValidation.Validate();
        }
    }
}
