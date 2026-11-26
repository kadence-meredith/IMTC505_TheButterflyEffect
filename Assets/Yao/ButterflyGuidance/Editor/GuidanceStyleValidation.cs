using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    public static class GuidanceStyleValidation
    {
        [MenuItem("Tools/Butterfly Effect/Validate Guidance Readability")]
        public static void Validate()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run readability checks outside Play mode.");
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Main Scenes/0_CampusStart_Main.unity", OpenSceneMode.Single);
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = new GameObject("Guidance readability fixture");
                root.SetActive(false);
                var camera = new GameObject("XR head", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                var events = new GameObject("Readability EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                events.transform.SetParent(root.transform, false);
                var view = root.AddComponent<GuidanceView>();
                view.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Yao/ButterflyGuidance/Art/GuidanceSans.ttf");
                view.uiMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Yao/ButterflyGuidance/Art/GuidanceUIOverlay.mat");
                var panel = root.AddComponent<GuidanceVRPanel>();
                panel.Configure(camera, view);
                var library = AssetDatabase.LoadAssetAtPath<GuidanceLibrary>("Assets/Yao/ButterflyGuidance/Data/ButterflyGuidance_English.asset");
                Require(library != null, "The actual dialogue library is missing.");
                var failures = new List<string>();
                foreach (var line in library.lines)
                {
                    view.Present(line);
                    CheckText(view, line.id, failures);
                }
                view.SetExplorationMode(true);
                view.SetExplorationContext(true);
                foreach (var line in library.lines)
                {
                    view.Present(line);
                    CheckText(view, "exploration/" + line.id, failures);
                }
                var roasting = library.Find("campfire.roasting");
                var finish = new GuidanceLine
                {
                    id = roasting.id, text = roasting.text, speaker = roasting.speaker,
                    choices = (roasting.choices ?? Array.Empty<GuidanceChoice>()).Concat(new[]
                    {
                        new GuidanceChoice { label = "Finish roasting", action = "marshmallow.finish" }
                    }).ToArray()
                };
                view.Present(finish);
                CheckText(view, "exploration/roasting-finish", failures);
                view.ShowTransientPrompt("Follow the butterfly.", 3f);
                CheckText(view, "exploration/entry", failures);
                Require(failures.Count == 0, "Text does not fit its VR panel:\n" + string.Join("\n", failures));
                Debug.Log("Guidance readability passed: all actual dialogue lines and choice labels, compact exploration actions, roasting completion and entry prompt.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void CheckText(GuidanceView view, string context, List<string> failures)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in view.GetComponentsInChildren<Text>(true))
            {
                if (string.IsNullOrEmpty(text.text) || !LocallyVisible(text.transform, view.transform)) continue;
                if (text.font == null) { failures.Add(context + ": missing font on " + text.name); continue; }
                var rect = text.rectTransform.rect;
                int minSize = text.resizeTextForBestFit ? text.resizeTextMinSize : text.fontSize;
                var settings = text.GetGenerationSettings(new Vector2(rect.width, rect.height));
                settings.resizeTextForBestFit = false;
                settings.fontSize = minSize;
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                using (var generator = new TextGenerator())
                {
                    float requiredHeight = generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
                    if (rect.width <= 0f || requiredHeight > rect.height + 2f)
                        failures.Add(context + "/" + text.name + ": \"" + text.text.Replace("\n", " / ")
                            + "\" needs " + requiredHeight.ToString("F1") + "px, has " + rect.height.ToString("F1") + "px at " + minSize + "pt.");
                }
                var button = text.GetComponentInParent<Button>();
                if (button == null) continue;
                Require(button.targetGraphic != null && button.targetGraphic.raycastTarget,
                    context + ": action does not accept a controller ray.");
                float physicalHeight = button.GetComponent<RectTransform>().rect.height * Mathf.Abs(button.transform.lossyScale.y);
                Require(physicalHeight >= .06f, context + ": controller target is too small.");
            }
        }

        private static bool LocallyVisible(Transform value, Transform root)
        {
            for (var current = value; current != null && current != root; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Guidance readability: " + message);
        }
    }
}
