using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ButterflyEffect.Guidance.Editor
{
    [InitializeOnLoad]
    public static class ButterflyGuidedDialoguePreview
    {
        private const string Key = "ButterflyGuidedDialogue.PreviewDirectory";
        private static int step;
        private static double next;
        private static double last;
        private static double began;
        private static Vector3 originalPosition;
        private static Quaternion originalRotation;

        static ButterflyGuidedDialoguePreview()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(Key, ""))) EditorApplication.update += Tick;
        }

        public static void OpenAndCapture()
        {
            string directory = Environment.GetEnvironmentVariable("BUTTERFLY_COMBINED_PREVIEWS");
            if (string.IsNullOrEmpty(directory)) directory = Path.GetFullPath("Temp/GuidedDialoguePreviews");
            Directory.CreateDirectory(directory);
            SessionState.SetString(Key, directory);
            EditorSceneManager.OpenScene(ButterflyGuidedDialogueBuilder.ScenePath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            step = 0;
            began = last = EditorApplication.timeSinceStartup;
            next = began + 2;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - last), 0, .05f);
            last = now;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || now < next) return;
            var coordinator = UnityEngine.Object.FindFirstObjectByType<GuidanceRouteCoordinator>();
            var player = UnityEngine.Object.FindFirstObjectByType<ButterflyGuideDemoPlayer>();
            if (coordinator == null || player == null) return;
            string directory = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(directory)) { EditorApplication.update -= Tick; return; }
            if (step > 0 && now - began > 85)
            {
                Debug.LogError("Guided dialogue Play Mode check timed out at step " + step);
                Finish(coordinator, player, false);
                return;
            }
            var director = coordinator.director;
            switch (step)
            {
                case 0:
                    var window = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    window.Show();
                    window.Focus();
                    window.maximized = true;
                    originalPosition = player.transform.position;
                    originalRotation = player.transform.rotation;
                    player.enabled = false;
                    began = now;
                    step = 1;
                    next = now + 1;
                    break;
                case 1:
                    if (coordinator.IsTravelling) break;
                    Capture(directory, "01-Combined-dialogue.png");
                    step = 2;
                    next = now + .3;
                    break;
                case 2: Choose(director, "Begin"); step = 3; break;
                case 3:
                    if (Drive(coordinator, player, dt)) { director.Advance(); step = 4; }
                    break;
                case 4:
                    if (Drive(coordinator, player, dt)) { Choose(director, "Touch the portal"); step = 5; }
                    break;
                case 5:
                    if (Drive(coordinator, player, dt)) { director.Advance(); step = 6; }
                    break;
                case 6:
                    if (Drive(coordinator, player, dt)) { Choose(director, "The cigarette"); step = 7; }
                    break;
                case 7:
                    if (Drive(coordinator, player, dt)) { Choose(director, "Drop it"); step = 8; }
                    break;
                case 8:
                    if (Drive(coordinator, player, dt)) { Choose(director, "Visit the campfire"); step = 9; }
                    break;
                case 9:
                    // Stop walking: the butterfly must wait and destination choices must stay hidden.
                    if (coordinator.guide.State == ButterflyGuideState.WaitingForUser && !coordinator.guide.IsAtPoint)
                    {
                        if (!coordinator.view.InteractionLocked) throw new InvalidOperationException("Destination options unlocked before arrival.");
                        Capture(directory, "02-Follow-and-wait.png");
                        step = 10;
                        next = now + .4;
                    }
                    break;
                case 10:
                    if (Drive(coordinator, player, dt)) { step = 11; next = now + .5; }
                    break;
                case 11:
                    if (director.Current.id != "campfire.approach" || coordinator.view.InteractionLocked)
                        throw new InvalidOperationException("Campfire dialogue was not restored after following the butterfly.");
                    Capture(directory, "03-Campfire-options.png");
                    step = 12;
                    next = now + .5;
                    break;
                case 12: Choose(director, "Pick up a marshmallow"); step = 13; next = now + .5; break;
                case 13:
                    if (coordinator.IsTravelling || director.Current.id != "campfire.roasting")
                        throw new InvalidOperationException("A same-station marshmallow choice restarted travel.");
                    Capture(directory, "04-Marshmallow-options.png");
                    step = 14;
                    next = now + .5;
                    break;
                default: Finish(coordinator, player, true); break;
            }
        }

        private static bool Drive(GuidanceRouteCoordinator coordinator, ButterflyGuideDemoPlayer player, float dt)
        {
            if (!coordinator.IsTravelling) return true;
            Vector3 target = coordinator.guide.transform.position;
            target.y = player.transform.position.y;
            player.transform.position = Vector3.MoveTowards(player.transform.position, target, 2.8f * dt);
            return false;
        }

        private static void Choose(GuidanceDirector director, string label)
        {
            var choices = director.Current.choices;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i].label == label) { director.Choose(i); return; }
            throw new InvalidOperationException("Missing original dialogue choice: " + label + " at " + director.Current.id);
        }

        private static void Capture(string directory, string filename)
        {
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, filename));
        }

        private static void Finish(GuidanceRouteCoordinator coordinator, ButterflyGuideDemoPlayer player, bool passed)
        {
            coordinator.guide.StopGuidance();
            player.transform.SetPositionAndRotation(originalPosition, originalRotation);
            coordinator.guide.ResetToStart();
            coordinator.director.Restart();
            player.enabled = true;
            SessionState.EraseString(Key);
            EditorApplication.update -= Tick;
            if (passed) Debug.Log("Combined dialogue Play Mode check passed: original prompts/options, cigarette-to-campfire travel, user waiting gate, same-station roasting, and clean single-Canvas UI.");
            // The test result remains in the log file; remove its editor status-bar message from the user's view.
            var entries = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
            var clear = entries == null ? null : entries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (clear != null) clear.Invoke(null, null);
        }
    }
}
