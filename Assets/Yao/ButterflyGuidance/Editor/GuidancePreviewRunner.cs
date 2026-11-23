using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Explicit visual QA entry point. Captures actual Game View frames and leaves the demo open.</summary>
    [InitializeOnLoad]
    public static class GuidancePreviewRunner
    {
        private const string Key = "ButterflyGuidance.CaptureDirectory";
        private static int step;
        private static double next;
        static GuidancePreviewRunner()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(Key, "")))
                EditorApplication.update += Tick;
        }

        public static void OpenPreviewAndCapture()
        {
            var directory = Environment.GetEnvironmentVariable("BUTTERFLY_PREVIEW_DIR");
            if (string.IsNullOrEmpty(directory)) directory = Path.GetFullPath("Temp/ButterflyGuidancePreviews");
            Directory.CreateDirectory(directory);
            SessionState.SetString(Key, directory);
            EditorSceneManager.OpenScene(GuidanceDemoBuilder.ScenePath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            step = 0;
            next = EditorApplication.timeSinceStartup + 2;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
            var director = UnityEngine.Object.FindFirstObjectByType<GuidanceDirector>();
            if (director == null) return;
            string directory = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(directory)) { EditorApplication.update -= Tick; return; }
            switch (step++)
            {
                case 0:
                    var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                    var window = EditorWindow.GetWindow(type);
                    window.Show();
                    window.Focus();
                    window.maximized = true;
                    director.Restart();
                    break;
                case 1: Capture(directory, "01-Campus.png"); break;
                case 2: director.Show("forest.hub"); break;
                case 3: Capture(directory, "02-Campsite.png"); break;
                case 4: director.PreviewEnding(GuidanceEnding.Devastated); break;
                case 5: Capture(directory, "03-Ending.png"); break;
                default:
                    director.Restart();
                    SessionState.EraseString(Key);
                    EditorApplication.update -= Tick;
                    Debug.Log("Butterfly Guidance visual previews captured to " + directory + ". Demo is ready in Play Mode.");
                    break;
            }
            next = EditorApplication.timeSinceStartup + 1.5;
        }

        private static void Capture(string directory, string filename)
        {
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, filename));
        }
    }
}
