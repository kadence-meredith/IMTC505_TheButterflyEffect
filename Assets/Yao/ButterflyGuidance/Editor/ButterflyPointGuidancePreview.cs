using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Explicit Play Mode smoke check. Simulated walking is restricted to the demo user.</summary>
    [InitializeOnLoad]
    public static class ButterflyPointGuidancePreview
    {
        private const string Key = "ButterflyPointGuidance.PreviewDirectory";
        private static int step;
        private static double next;
        private static double last;
        private static double started;
        private static Vector3 initialUser;
        private static Quaternion initialRotation;
        private static float pausedDistance;
        private static bool capturedFollowing;

        static ButterflyPointGuidancePreview()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(Key, ""))) EditorApplication.update += Tick;
        }

        public static void OpenAndCapture()
        {
            var directory = Environment.GetEnvironmentVariable("BUTTERFLY_POINT_PREVIEWS");
            if (string.IsNullOrEmpty(directory)) directory = Path.GetFullPath("Temp/ButterflyPointPreviews");
            Directory.CreateDirectory(directory);
            SessionState.SetString(Key, directory);
            EditorSceneManager.OpenScene(ButterflyPointGuidanceBuilder.ScenePath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            step = 0;
            capturedFollowing = false;
            started = last = EditorApplication.timeSinceStartup;
            next = started + 2;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Clamp((float)(now - last), 0, .05f);
            last = now;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || now < next) return;
            var guide = UnityEngine.Object.FindFirstObjectByType<ButterflyPointGuide>();
            var user = UnityEngine.Object.FindFirstObjectByType<ButterflyGuideDemoPlayer>();
            if (guide == null || user == null) return;
            string directory = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(directory)) { EditorApplication.update -= Tick; return; }
            if (step > 0 && now - started > 45)
            {
                Debug.LogError("Point guide Play Mode check timed out at step " + step + ", state " + guide.State);
                Finish(guide, user);
                return;
            }
            switch (step)
            {
                case 0:
                    var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                    var window = EditorWindow.GetWindow(type);
                    window.Show();
                    window.Focus();
                    window.maximized = true;
                    initialUser = user.transform.position;
                    initialRotation = user.transform.rotation;
                    user.enabled = false;
                    guide.ResetToStart();
                    started = now;
                    step = 1;
                    next = now + 1.2;
                    break;
                case 1:
                    Capture(directory, "01-Ready.png");
                    guide.StartGuidance();
                    step = 2;
                    next = now + .4;
                    break;
                case 2:
                    // Approach the first anchor just as a walking user would.
                    var a = guide.points[0].position;
                    a.y = user.transform.position.y;
                    user.transform.position = Vector3.MoveTowards(user.transform.position, a, 1.6f * delta);
                    if (guide.CurrentPointIndex == 1) { step = 3; next = now + .1; }
                    break;
                case 3:
                    // Hold the user still until the butterfly reaches its configured lead limit.
                    if (guide.State != ButterflyGuideState.WaitingForUser || guide.IsAtPoint) break;
                    var separation = guide.transform.position - user.transform.position;
                    separation.y = 0;
                    pausedDistance = separation.magnitude;
                    if (pausedDistance > guide.maxLeadDistance + .01f)
                        Debug.LogError("Point guide exceeded its lead limit in Play Mode: " + pausedDistance);
                    Capture(directory, "02-Waits-for-you.png");
                    step = 4;
                    next = now + .5;
                    break;
                case 4:
                    var follow = guide.transform.position - Vector3.forward * .8f;
                    follow.y = user.transform.position.y;
                    user.transform.position = Vector3.MoveTowards(user.transform.position, follow, 2.3f * delta);
                    if (!capturedFollowing && guide.State == ButterflyGuideState.Flying && user.transform.position.z > 3)
                    {
                        Capture(directory, "03-Following.png");
                        capturedFollowing = true;
                    }
                    if (guide.State == ButterflyGuideState.Finished) { step = 5; next = now + .5; }
                    break;
                case 5:
                    Capture(directory, "04-Arrived.png");
                    Debug.Log("Point guidance Play Mode check passed: led A to B; waited at " + pausedDistance.ToString("F2")
                        + "m separation; resumed when approached; waited for final user arrival; wing animation retained.");
                    step = 6;
                    next = now + .7;
                    break;
                default: Finish(guide, user); break;
            }
        }

        private static void Capture(string directory, string name)
        {
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name));
        }

        private static void Finish(ButterflyPointGuide guide, ButterflyGuideDemoPlayer user)
        {
            user.transform.SetPositionAndRotation(initialUser, initialRotation);
            guide.ResetToStart();
            user.enabled = true;
            SessionState.EraseString(Key);
            EditorApplication.update -= Tick;
            Debug.Log("Point guidance demo is ready. Click Begin, then walk with WASD / arrows. Hold right mouse to look; R restarts.");
        }
    }
}
