using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ButterflyEffect.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Transient Play-mode checks of the real desktop camera, physics motor, and scene transitions.</summary>
    [InitializeOnLoad]
    public static class GuidanceKeyboardPlayPreview
    {
        private const string Prefix = "Butterfly.KeyboardPlayPreview.";
        private const string Campus = "0_CampusStart_Main";
        private const string Campsite = "1_Campsite_Main";
        private const string CampusPath = "Assets/Main Scenes/0_CampusStart_Main.unity";

        static GuidanceKeyboardPlayPreview()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(Prefix + "directory", "")))
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Butterfly Effect/Check Campus Keyboard Route")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before checking the campus keyboard route.");
            string directory = Environment.GetEnvironmentVariable("BUTTERFLY_KEYBOARD_PREVIEWS");
            if (string.IsNullOrEmpty(directory)) directory = Path.GetFullPath("Temp/KeyboardGuidancePreviews");
            Directory.CreateDirectory(directory);
            SessionState.SetString(Prefix + "directory", directory);
            SessionState.SetString(Prefix + "report",
                "Actual main-scene desktop bindings, CharacterController physics, and portal-transition smoke check.\n"
                + "Raw W keyboard input is verified separately; this helper calls the real physics motor directly.\n");
            SessionState.SetInt(Prefix + "step", 0);
            SessionState.SetFloat(Prefix + "began", (float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(Prefix + "next", (float)EditorApplication.timeSinceStartup + .7f);
            SessionState.EraseString(Prefix + "ending");
            EditorSceneManager.OpenScene(CampusPath, OpenSceneMode.Single);
            ShowGameView();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            string directory = SessionState.GetString(Prefix + "directory", "");
            if (string.IsNullOrEmpty(directory)) { EditorApplication.update -= Tick; return; }
            int step = SessionState.GetInt(Prefix + "step", 0);
            if (step == 99)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
                    RestoreEditScene();
                return;
            }
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - SessionState.GetFloat(Prefix + "began", (float)now) > 120)
                    throw new TimeoutException("Desktop route smoke check timed out at step " + step + ".");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling
                    || now < SessionState.GetFloat(Prefix + "next", 0f)) return;
                Scene scene = SceneManager.GetActiveScene();
                GuidancePlayerMode mode = Components<GuidancePlayerMode>(scene).FirstOrDefault();
                if (mode == null || mode.PlayerCamera == null) return;

                switch (step)
                {
                    case 0:
                        if (scene.name != Campus) return;
                        CheckDesktopBindings(mode);
                        CheckPhysicsMovement(mode);
                        Capture(mode, "01-Campus-keyboard-player.png");
                        Note("PASS opening campus: Automatic selected the desktop player; one camera, enabled keyboard input, "
                            + "active CharacterController, inactive XR rig, and matching butterfly/dialogue camera.");
                        SetStep(1, .6f);
                        break;
                    case 1:
                        CheckDesktopBindings(mode);
                        var entry = mode.campusPortal != null ? mode.campusPortal
                            : Components<GuidanceCampusPortal>(scene).FirstOrDefault();
                        Require(entry != null && entry.isActiveAndEnabled
                            && entry.user == mode.PlayerCamera.transform
                            && entry.playerRoot == mode.desktopPlayer.transform,
                            "The physical campus portal is not bound to the desktop player.");
                        PoseCameraXZ(mode, entry.transform.position);
                        Require(entry.TryEnter(), "Standing inside the campus portal did not start the actual scene transition.");
                        Note("PASS physical campus portal: the actual desktop player entered the authored campsite portal.");
                        SetStep(2, .8f);
                        break;
                    case 2:
                        if (scene.name != Campsite) return;
                        CheckDesktopBindings(mode);
                        var setup = Components<CampsiteSetup>(scene).SingleOrDefault();
                        Require(setup != null && setup.player == mode.desktopPlayer,
                            "The campsite did not reuse its real desktop player.");
                        Require(setup.cigarette != null && mode.desktopPlayer.Held == setup.cigarette
                            && setup.cigarette.IsHeld,
                            "The real starting cigarette was not held by the desktop player.");
                        CheckPhysicsMovement(mode);
                        Capture(mode, "02-Campsite-keyboard-player.png");
                        Note("PASS campsite: the existing physical desktop player, starting cigarette, input, motor, "
                            + "and butterfly/dialogue bindings survived the campus transition.");
                        SetStep(3, .6f);
                        break;
                    case 3:
                        var exit = Components<GuidanceReturnPortal>(scene).SingleOrDefault();
                        Require(exit != null && exit.user == mode.PlayerCamera.transform
                            && exit.playerRoot == mode.desktopPlayer.transform,
                            "The return portal is not bound to the actual desktop player.");
                        string ending = exit.ResolveEndingScene();
                        Require(!string.IsNullOrEmpty(ending) && Application.CanStreamedLevelBeLoaded(ending),
                            "The real campsite state did not resolve a loadable ending.");
                        SessionState.SetString(Prefix + "ending", ending);
                        PoseCameraXZ(mode, exit.transform.position + Vector3.right * 2f);
                        exit.Arm();
                        Require(exit.IsArmed, "The return portal did not arm outside its entry radius.");
                        PoseCameraXZ(mode, exit.transform.position);
                        Require(exit.TryEnter(), "Standing inside the armed return portal did not load the real ending.");
                        Note("PASS physical return: the real campsite state selected " + ending + ".");
                        SetStep(4, .8f);
                        break;
                    case 4:
                        string expected = SessionState.GetString(Prefix + "ending", "");
                        if (scene.name != expected) return;
                        CheckDesktopBindings(mode);
                        CheckPhysicsMovement(mode);
                        Capture(mode, "03-Ending-keyboard-player.png");
                        Note("PASS ending: desktop camera, input and physics motor remained usable after the real portal transition.");
                        SetStep(5, .8f);
                        break;
                    default:
                        Complete(true, null);
                        break;
                }
            }
            catch (Exception exception) { Complete(false, exception); }
        }

        private static void CheckDesktopBindings(GuidancePlayerMode mode)
        {
            Require(mode.mode == GuidanceControlMode.Automatic && mode.IsDesktop,
                "Automatic did not select desktop control without an XR runtime.");
            Require(mode.desktopPlayer != null && mode.desktopPlayer.isActiveAndEnabled
                && mode.desktopPlayer.inputEnabled, "The actual desktop keyboard input is disabled.");
            var motor = mode.desktopPlayer.GetComponent<CharacterController>();
            Require(motor != null && motor.enabled, "The actual desktop physics motor is disabled.");
            Camera camera = mode.PlayerCamera;
            Require(camera != null && camera.isActiveAndEnabled && mode.desktopPlayer.view == camera,
                "The desktop player does not own the active camera.");
            Require(mode.xrOrigin != null && !mode.xrOrigin.gameObject.activeInHierarchy,
                "The XR rig is still active alongside the computer player.");
            Require(Components<Camera>(mode.gameObject.scene).Count(c => c.isActiveAndEnabled) == 1,
                "The scene has multiple active cameras.");
            Require(mode.coordinator != null && mode.coordinator.guide != null
                && mode.coordinator.guide.user == camera.transform,
                "The butterfly follows a different player camera.");
            Require(mode.view != null && mode.panel != null && mode.panel.playerCamera == camera
                && mode.panel.view == mode.view && mode.view.director == mode.director,
                "The dialogue view and player mode disagree about the active camera.");
            Require(mode.panel.DialogueCanvas != null && mode.panel.DialogueCanvas.worldCamera == camera,
                "The dialogue Canvas still points at the disabled XR camera.");
        }

        private static void CheckPhysicsMovement(GuidancePlayerMode mode)
        {
            DesktopPlayer player = mode.desktopPlayer;
            var motor = player.GetComponent<CharacterController>();
            Vector3 start = player.transform.position;
            Quaternion rotation = player.transform.rotation;
            bool input = player.inputEnabled;
            player.inputEnabled = false;
            float moved = 0f;
            try
            {
                foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    RestorePose(player, start, rotation);
                    motor.Move(direction * .2f);
                    Physics.SyncTransforms();
                    Vector3 difference = player.transform.position - start;
                    difference.y = 0f;
                    moved = difference.magnitude;
                    if (moved > .025f) break;
                }
                Require(moved > .025f, "The real CharacterController could not move in any cardinal direction.");
                Note("PASS " + player.gameObject.scene.name + " physics motor moved " + moved.ToString("F3")
                    + "m through CharacterController.Move; transient test pose restored.");
            }
            finally
            {
                RestorePose(player, start, rotation);
                player.inputEnabled = input;
            }
        }

        private static void RestorePose(DesktopPlayer player, Vector3 position, Quaternion rotation)
        {
            var motor = player.GetComponent<CharacterController>();
            bool enabled = motor != null && motor.enabled;
            if (motor != null) motor.enabled = false;
            player.transform.SetPositionAndRotation(position, rotation);
            if (motor != null) motor.enabled = enabled;
            Physics.SyncTransforms();
        }

        private static void PoseCameraXZ(GuidancePlayerMode mode, Vector3 cameraPosition)
        {
            Vector3 delta = cameraPosition - mode.PlayerCamera.transform.position;
            delta.y = 0f;
            RestorePose(mode.desktopPlayer, mode.desktopPlayer.transform.position + delta,
                mode.desktopPlayer.transform.rotation);
        }

        private static void Capture(GuidancePlayerMode mode, string name)
        {
            mode.panel.SnapToPlayer();
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Prefix + "directory", ""), name));
        }

        private static IEnumerable<T> Components<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        }

        private static void SetStep(int step, float delay)
        {
            SessionState.SetInt(Prefix + "step", step);
            SessionState.SetFloat(Prefix + "next", (float)EditorApplication.timeSinceStartup + delay);
            File.WriteAllText(Path.Combine(SessionState.GetString(Prefix + "directory", ""), "smoke-progress.txt"),
                "Step " + step + " | " + SceneManager.GetActiveScene().name);
        }

        private static void Note(string text)
        {
            SessionState.SetString(Prefix + "report", SessionState.GetString(Prefix + "report", "") + text + "\n");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Complete(bool passed, Exception exception)
        {
            string directory = SessionState.GetString(Prefix + "directory", "");
            File.WriteAllText(Path.Combine(directory, "smoke-result.txt"),
                SessionState.GetString(Prefix + "report", "")
                + (passed ? "PASS: actual desktop physics and portal continuity checks completed.\n"
                    : "FAIL: " + exception + "\n"));
            SessionState.SetBool(Prefix + "passed", passed);
            SessionState.SetInt(Prefix + "step", 99);
            EditorApplication.isPlaying = false;
        }

        private static void RestoreEditScene()
        {
            try
            {
                EditorSceneManager.OpenScene(CampusPath, OpenSceneMode.Single);
                ShowGameView();
                if (SessionState.GetBool(Prefix + "passed", false))
                {
                    Type entries = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
                    entries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(null, null);
                }
            }
            finally
            {
                SessionState.EraseString(Prefix + "directory");
                SessionState.EraseString(Prefix + "report");
                SessionState.EraseString(Prefix + "ending");
                EditorApplication.update -= Tick;
            }
        }

        private static void ShowGameView()
        {
            Type type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (type == null) return;
            EditorWindow window = EditorWindow.GetWindow(type);
            window.Show();
            window.Focus();
        }
    }
}
