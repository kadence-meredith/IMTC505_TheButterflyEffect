using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ButterflyEffect.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Editor-only visual smoke test. All simulated poses and prop changes are Play-mode changes.</summary>
    [InitializeOnLoad]
    public static class GuidanceMainScenePreview
    {
        private const string Prefix = "Butterfly.MainScenePreview.";
        private const string Campus = "0_CampusStart_Main";
        private const string Campsite = "1_Campsite_Main";
        private static double last;
        private static double next;
        private static ulong configuredSceneHandle = ulong.MaxValue;

        static GuidanceMainScenePreview()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(Prefix + "directory", "")))
            {
                last = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }
        }

        [MenuItem("Tools/Butterfly Effect/Preview Main Scene Integration")]
        public static void OpenAndCapture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start the main-scene preview outside Play mode.");
            string directory = Environment.GetEnvironmentVariable("BUTTERFLY_INTEGRATION_PREVIEWS");
            if (string.IsNullOrEmpty(directory)) directory = Path.GetFullPath("Temp/MainSceneGuidancePreviews");
            Directory.CreateDirectory(directory);
            SessionState.SetString(Prefix + "directory", directory);
            SessionState.SetString(Prefix + "report", "Actual main-scene Play-mode smoke test\n");
            SessionState.SetInt(Prefix + "step", 0);
            SessionState.SetFloat(Prefix + "began", (float)EditorApplication.timeSinceStartup);
            SessionState.EraseString(Prefix + "expectedEnding");
            configuredSceneHandle = ulong.MaxValue;
            EditorSceneManager.OpenScene(GuidanceMainSceneBuilder.SceneFolder + Campus + ".unity");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            last = EditorApplication.timeSinceStartup;
            next = last + .5;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            string directory = SessionState.GetString(Prefix + "directory", "");
            if (string.IsNullOrEmpty(directory)) { EditorApplication.update -= Tick; return; }
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - last), 0f, .05f);
            last = now;
            int step = SessionState.GetInt(Prefix + "step", 0);
            if (step == 99)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
                    RestoreEditScene();
                return;
            }
            try
            {
                if (now - SessionState.GetFloat(Prefix + "began", (float)now) > 120)
                    throw new TimeoutException("Main-scene smoke check timed out at step " + step + ".");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || now < next) return;
                GuidanceSceneBridge bridge = Object.FindFirstObjectByType<GuidanceSceneBridge>();
                if (bridge == null || bridge.director == null || bridge.coordinator == null || bridge.coordinator.guide == null) return;
                ConfigureTestPose(bridge);
                LookAtButterfly(bridge);
                switch (step)
                {
                    case 0:
                        if (SceneManager.GetActiveScene().name != Campus || !Ready(bridge, "campus.opening")) return;
                        MaximizeGameView();
                        SetStep(1, .7);
                        break;
                    case 1:
                        Capture(bridge, "01-Campus-opening.png");
                        Require(ActiveButtons(bridge).Length == 1, "Campus opening should have one original Begin choice.");
                        Note("PASS campus opening: one original dialogue card, active native butterfly and XR camera.");
                        SetStep(2, .5);
                        break;
                    case 2:
                        Press(bridge, "Begin");
                        SetStep(3);
                        break;
                    case 3:
                        if (!Drive(bridge, dt) || !Ready(bridge, "campus.portal")) return;
                        Press(bridge, "Continue");
                        SetStep(4, .2);
                        break;
                    case 4:
                        if (!Drive(bridge, dt) || !Ready(bridge, "portal.enter")) return;
                        Capture(bridge, "02-Campus-portal.png");
                        Press(bridge, "Touch the portal");
                        Require(SceneManager.GetActiveScene().name == Campus, "Portal button falsely teleported the player.");
                        Require(bridge.WaitingForPhysicalAction, "Portal choice did not wait for physical interaction.");
                        SetStep(5, .5);
                        break;
                    case 5:
                        Require(SceneManager.GetActiveScene().name == Campus, "Portal button loaded a different scene.");
                        Note("PASS campus portal: original choice waits for the physical portal without UI teleportation.");
                        bridge.CancelPhysicalWait();
                        SceneManager.LoadScene(Campsite);
                        configuredSceneHandle = ulong.MaxValue;
                        SetStep(6, .7);
                        break;
                    case 6:
                        if (SceneManager.GetActiveScene().name != Campsite || !Drive(bridge, dt) || !Ready(bridge, "forest.arrival")) return;
                        Press(bridge, "Continue");
                        SetStep(7, .2);
                        break;
                    case 7:
                        if (!Drive(bridge, dt) || !Ready(bridge, "forest.hub")) return;
                        Require(ActiveButtons(bridge).Length == 6, "Campsite hub lost one of the six original choices.");
                        Capture(bridge, "03-Campsite-options.png");
                        Note("PASS campsite arrival: six original exploration choices displayed in the actual group campsite.");
                        SetStep(8, .5);
                        break;
                    case 8:
                        Require(bridge.setup != null && bridge.setup.cigarette != null, "Actual cigarette reference is missing.");
                        CigaretteItem cigarette = bridge.setup.cigarette;
                        cigarette.Body.isKinematic = true;
                        cigarette.Body.useGravity = false;
                        cigarette.Grabbed(true);
                        bridge.SyncFromSession();
                        Press(bridge, "The cigarette");
                        SetStep(9);
                        break;
                    case 9:
                        if (!Drive(bridge, dt)) return;
                        // The real pickup event may correctly show cigarette.picked first.
                        // Reopening this station gives its current in-hand choices.
                        if (bridge.director.Current.id != "cigarette.hand")
                        { bridge.Explore("cigarette"); SetStep(9, .15); return; }
                        Press(bridge, "Drop it");
                        Require(bridge.WaitingForPhysicalAction, "XR Drop choice should await the real grip release.");
                        Require(bridge.setup.cigarette.IsHeld, "UI choice changed the real held item without releasing it.");
                        ReleaseActualCigarette(bridge);
                        SetStep(10, .25);
                        break;
                    case 10:
                        if (!Drive(bridge, dt) || !Ready(bridge, "cigarette.ground")) return;
                        Require(bridge.session.state.cigarette == CigaretteCondition.Dropped, "Actual cigarette release did not reach real session state.");
                        Transform fire = bridge.coordinator.stations.First(x => x.name == "Campfire").target;
                        Vector3 away = bridge.coordinator.guide.transform.position - fire.position;
                        away.y = 0;
                        if (away.sqrMagnitude < .01f) away = Vector3.back;
                        Vector3 heldUserPosition = bridge.coordinator.guide.transform.position + away.normalized * 1.1f;
                        PoseUserXZ(bridge, heldUserPosition);
                        Press(bridge, "Visit the campfire");
                        SetStep(11);
                        break;
                    case 11:
                        var guide = bridge.coordinator.guide;
                        if (guide.State != ButterflyGuideState.WaitingForUser && guide.State != ButterflyGuideState.WaitingAtPoint) return;
                        Require(bridge.coordinator.IsTravelling, "Travel finished before the stopped user caught up.");
                        Require(bridge.view.InteractionLocked && ActiveButtons(bridge).Length == 0,
                            "Destination choices unlocked before the actual XR user arrived.");
                        Capture(bridge, "04-Butterfly-waits.png");
                        Note("PASS real cigarette release and route gating: butterfly waits for the stopped XR user; destination options stay hidden.");
                        SetStep(12, .5);
                        break;
                    case 12:
                        if (!Drive(bridge, dt) || !Ready(bridge, "campfire.approach")) return;
                        Require(ActiveButtons(bridge).Any(x => ButtonLabel(x) == "Pick up a marshmallow"),
                            "Original marshmallow option did not return on arrival.");
                        Capture(bridge, "05-Campfire-options.png");
                        Note("PASS following to the actual campfire: original dialogue and marshmallow choices restored after both arrivals.");
                        SetStep(13, .7);
                        break;
                    case 13:
                        Press(bridge, "Leave");
                        SetStep(14);
                        break;
                    case 14:
                        if (!Drive(bridge, dt) || !Ready(bridge, "forest.hub")) return;
                        Press(bridge, "Return portal");
                        SetStep(15);
                        break;
                    case 15:
                        if (!Drive(bridge, dt) || !Ready(bridge, "return.prompt")) return;
                        Press(bridge, "Return to campus");
                        SetStep(16, .2);
                        break;
                    case 16:
                        if (!Drive(bridge, dt) || !Ready(bridge, "return.butterfly")) return;
                        Require(bridge.returnPortal != null, "Actual return portal adapter is missing.");
                        string expected = bridge.returnPortal.ResolveEndingScene();
                        Require(!string.IsNullOrEmpty(expected), "Actual session did not resolve an ending scene.");
                        SessionState.SetString(Prefix + "expectedEnding", expected);
                        bridge.returnPortal.Arm();
                        PoseUserXZ(bridge, bridge.returnPortal.transform.position);
                        configuredSceneHandle = ulong.MaxValue;
                        SetStep(17, .5);
                        break;
                    case 17:
                        string expectedScene = SessionState.GetString(Prefix + "expectedEnding", "");
                        if (SceneManager.GetActiveScene().name != expectedScene) return;
                        if (!Drive(bridge, dt) || !Ready(bridge, "campus.reveal")) return;
                        Press(bridge, "See what changed");
                        SetStep(18, .5);
                        break;
                    case 18:
                        if (!Drive(bridge, dt)) return;
                        Require(bridge.director.Current.id == "ending." + bridge.ending.ToString().ToLowerInvariant(),
                            "Ending dialogue disagrees with the actual loaded campus ending.");
                        Capture(bridge, "06-Actual-campus-ending.png");
                        Note("PASS physical return portal loaded " + SceneManager.GetActiveScene().name
                            + " from the real campsite hazards, with matching original ending dialogue.");
                        SetStep(19, .8);
                        break;
                    default:
                        Complete(true, null);
                        break;
                }
            }
            catch (Exception exception) { Complete(false, exception); }
        }

        private static void ConfigureTestPose(GuidanceSceneBridge bridge)
        {
            Scene scene = bridge.gameObject.scene;
            if (configuredSceneHandle == scene.handle.GetRawData()) return;
            var controlMode = bridge.GetComponent<GuidancePlayerMode>();
            if (controlMode != null)
            {
                controlMode.mode = GuidanceControlMode.XR;
                controlMode.ApplyMode();
            }
            Transform rig = Rig(bridge);
            Require(rig != null, "No actual XR rig owns the guidance user camera.");
            // Disable only runtime tracking/locomotion/starting-hold while the harness
            // moves the real rig without an HMD. Play-mode exit restores authored values.
            foreach (MonoBehaviour behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                string name = behaviour.GetType().Name;
                if (name == "TrackedPoseDriver" || name == "DynamicMoveProvider" || name == "ContinuousMoveProvider"
                    || name == "CampsiteVRPlayer" || name == "CharacterControllerDriver") behaviour.enabled = false;
            }
            foreach (Collider collider in rig.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            Camera camera = bridge.coordinator.guide.user.GetComponent<Camera>();
            Require(camera != null && camera.enabled, "Actual XR camera is disabled.");
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            // Simulate a standing tracked head; an untracked XR camera otherwise
            // stays at the floor in Editor and gives misleading visual captures.
            Vector3 head = camera.transform.position;
            head.y = rig.position.y + 1.6f;
            camera.transform.position = head;
            configuredSceneHandle = scene.handle.GetRawData();
        }

        private static Transform Rig(GuidanceSceneBridge bridge)
        {
            Transform camera = bridge.coordinator.guide.user;
            for (Transform parent = camera; parent != null; parent = parent.parent)
                if (parent.name.Contains("XR Origin")) return parent;
            return null;
        }

        private static bool Drive(GuidanceSceneBridge bridge, float dt)
        {
            if (!bridge.coordinator.IsTravelling) return true;
            Vector3 cameraPosition = bridge.coordinator.guide.user.position;
            Vector3 target = bridge.coordinator.guide.transform.position;
            target.y = cameraPosition.y;
            Vector3 delta = target - cameraPosition;
            if (delta.magnitude > .75f)
                PoseUserXZ(bridge, Vector3.MoveTowards(cameraPosition, target, Mathf.Min(2.8f * dt, delta.magnitude - .75f)));
            return false;
        }

        private static void PoseUserXZ(GuidanceSceneBridge bridge, Vector3 desiredCamera)
        {
            Transform rig = Rig(bridge);
            Vector3 delta = desiredCamera - bridge.coordinator.guide.user.position;
            delta.y = 0;
            rig.position += delta;
            Physics.SyncTransforms();
        }

        private static void LookAtButterfly(GuidanceSceneBridge bridge)
        {
            Transform camera = bridge.coordinator.guide.user;
            var rig = Rig(bridge);
            Vector3 standingHead = camera.position;
            standingHead.y = rig.position.y + 1.6f;
            camera.position = standingHead;
            Vector3 direction = bridge.coordinator.guide.transform.position - camera.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .05f) camera.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private static void ReleaseActualCigarette(GuidanceSceneBridge bridge)
        {
            CigaretteItem cigarette = bridge.setup.cigarette;
            Transform camera = bridge.coordinator.guide.user;
            Vector3 position = camera.position + camera.forward * .3f;
            position.y = bridge.cigaretteGroundAnchor != null ? bridge.cigaretteGroundAnchor.position.y + .12f : .2f;
            cigarette.transform.position = position;
            cigarette.Body.position = position;
            cigarette.Body.isKinematic = false;
            cigarette.Body.useGravity = true;
            cigarette.Body.linearVelocity = Vector3.zero;
            cigarette.Body.angularVelocity = Vector3.zero;
            cigarette.Released();
            Physics.SyncTransforms();
            bridge.SyncFromSession();
        }

        private static bool Ready(GuidanceSceneBridge bridge, string id)
        {
            return bridge.director.Current != null && bridge.director.Current.id == id
                && !bridge.coordinator.IsTravelling && !bridge.view.InteractionLocked;
        }

        private static Button[] ActiveButtons(GuidanceSceneBridge bridge)
        {
            return bridge.view.GetComponentsInChildren<Button>(false)
                .Where(x => x.isActiveAndEnabled && x.interactable).ToArray();
        }

        private static string ButtonLabel(Button button)
        {
            Text text = button.GetComponentInChildren<Text>();
            return text != null ? text.text : string.Empty;
        }

        private static void Press(GuidanceSceneBridge bridge, string label)
        {
            Require(!bridge.view.InteractionLocked && !bridge.coordinator.IsTravelling,
                "Attempted to press a locked original option: " + label);
            Canvas.ForceUpdateCanvases();
            Button button = ActiveButtons(bridge).FirstOrDefault(x => ButtonLabel(x) == label);
            Require(button != null, "Missing original button '" + label + "' at " + bridge.director.Current?.id);
            button.onClick.Invoke();
        }

        private static void Capture(GuidanceSceneBridge bridge, string file)
        {
            var panel = bridge.GetComponent<GuidanceVRPanel>();
            if (panel != null) panel.SnapToPlayer();
            Canvas.ForceUpdateCanvases();
            var camera = bridge.coordinator.guide.user.GetComponent<Camera>();
            var model = bridge.coordinator.guide.GetComponentInChildren<SkinnedMeshRenderer>();
            if (model != null)
                File.AppendAllText(Path.Combine(SessionState.GetString(Prefix + "directory", ""), "visual-positions.txt"),
                    file + " rig=" + Rig(bridge).position + " head=" + camera.transform.position
                    + " guide=" + bridge.coordinator.guide.transform.position + " model=" + model.bounds.center
                    + " size=" + model.bounds.size + " screen=" + camera.WorldToViewportPoint(model.bounds.center) + "\n");
            ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Prefix + "directory", ""), file));
        }

        private static void MaximizeGameView()
        {
            Type type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow window = EditorWindow.GetWindow(type);
            window.Show(); window.Focus(); window.maximized = true;
        }

        private static void SetStep(int step, double delay = 0)
        {
            SessionState.SetInt(Prefix + "step", step);
            next = EditorApplication.timeSinceStartup + delay;
            string directory = SessionState.GetString(Prefix + "directory", "");
            if (!string.IsNullOrEmpty(directory))
                File.WriteAllText(Path.Combine(directory, "smoke-progress.txt"),
                    "Step " + step + " | " + SceneManager.GetActiveScene().name
                    + " | editor time " + EditorApplication.timeSinceStartup.ToString("F2"));
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
            string report = SessionState.GetString(Prefix + "report", "")
                + (passed ? "PASS: all actual main-scene smoke checks completed.\n" : "FAIL: " + exception + "\n");
            if (!string.IsNullOrEmpty(directory)) File.WriteAllText(Path.Combine(directory, "smoke-result.txt"), report);
            SessionState.SetBool(Prefix + "passed", passed);
            SessionState.SetInt(Prefix + "step", 99);
            EditorApplication.isPlaying = false;
        }

        private static void RestoreEditScene()
        {
            try
            {
                EditorSceneManager.OpenScene(GuidanceMainSceneBuilder.SceneFolder + Campus + ".unity", OpenSceneMode.Single);
                Type type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                EditorWindow game = EditorWindow.GetWindow(type);
                game.maximized = false;
                SceneView.lastActiveSceneView?.Focus();
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
                SessionState.EraseString(Prefix + "expectedEnding");
                EditorApplication.update -= Tick;
            }
        }
    }
}
