using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ButterflyEffect.Prototype;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Checks the actual player/camera boundary used by the main scenes.</summary>
    public static class GuidancePlayerModeValidation
    {
        private static readonly string[] MainScenes =
        {
            "0_CampusStart_Main", "1_Campsite_Main", "2_CampusFire_Main",
            "3_CampusFire_Main", "4_CampusFire_Main", "5_CampusEnd_Main"
        };

        [MenuItem("Tools/Butterfly Effect/Validate Keyboard Mode")]
        public static void Validate()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before validating player mode.");
            ValidateRuntimeLogic();
            ValidateSavedMainScenes();
            Debug.Log("Keyboard mode validation passed: all six main scenes; real desktop player/camera bindings; "
                + "existing campsite player reuse; repeated initialization; authored preview pose; XR preservation; "
                + "campus and return portal player bindings.");
        }

        public static void ValidateRuntimeLogic()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run player-mode validation outside Play mode.");
            Scene previousScene = SceneManager.GetActiveScene();
            Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            try
            {
                var events = new GameObject("Keyboard validation EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                ValidateNewCampusPlayer(temporary);
                ValidateExistingCampsitePlayer(temporary);
                ValidateScriptedCampsiteSpawn(temporary);
                ValidateActivePreviewPlayer(temporary);
                ValidateXRPlayer(temporary);
            }
            finally
            {
                EditorSceneManager.CloseScene(temporary, true);
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            }
        }

        private static void ValidateNewCampusPlayer(Scene scene)
        {
            using (var fixture = new Fixture(scene, false, false))
            {
                fixture.Mode.mode = GuidanceControlMode.Desktop;
                fixture.Mode.ApplyMode();
                var player = fixture.Mode.desktopPlayer;
                Require(player != null && player.isActiveAndEnabled && player.inputEnabled,
                    "A campus without a desktop player did not gain active keyboard input.");
                var motor = player.GetComponent<CharacterController>();
                Require(motor != null && motor.enabled && motor.height > 1f && motor.radius > 0f,
                    "The campus computer player has no usable character controller.");
                Require(Vector3.Distance(player.transform.position, fixture.XRSpawn) < .001f,
                    "The computer campus player ignored the authored starting pose.");
                CheckDesktopBindings(fixture);
                Vector3 moved = player.transform.position + new Vector3(2f, 0f, -1f);
                player.transform.position = moved;
                fixture.Mode.ApplyMode();
                Require(fixture.Root.GetComponentsInChildren<DesktopPlayer>(true).Length == 1,
                    "Repeated initialization created a second keyboard player.");
                Require(fixture.Mode.desktopPlayer == player && player.transform.position == moved,
                    "Repeated initialization reset the user's current position.");
                CheckDesktopBindings(fixture);
            }
        }

        private static void ValidateExistingCampsitePlayer(Scene scene)
        {
            using (var fixture = new Fixture(scene, true, false))
            {
                DesktopPlayer authoredPlayer = fixture.ExistingPlayer;
                Camera authoredCamera = authoredPlayer.view;
                var first = new GameObject("First cigarette stop").transform;
                first.SetParent(fixture.Root.transform, false);
                first.position = new Vector3(-.96f, .1f, 2.65f);
                fixture.Coordinator.stations = fixture.Coordinator.stations.Concat(new[]
                {
                    new GuidanceStationBinding { name = "Cigarette", target = first,
                        dialoguePrefixes = new[] { "cigarette." } }
                }).ToArray();
                fixture.Mode.mode = GuidanceControlMode.Desktop;
                fixture.Mode.ApplyMode();
                Require(fixture.Mode.desktopPlayer == authoredPlayer && fixture.Mode.PlayerCamera == authoredCamera,
                    "The campsite's authored desktop player/camera was replaced.");
                Require(fixture.Setup.player == authoredPlayer && fixture.Bridge.setup == fixture.Setup
                    && authoredPlayer.startingCigarette == fixture.Setup.cigarette,
                    "Physical campsite item actions are bound to a different player.");
                Vector3 towardGuide = fixture.Coordinator.guide.transform.position - authoredCamera.transform.position;
                Require(Vector3.Dot(authoredCamera.transform.forward, towardGuide.normalized) > .98f,
                    "The first butterfly is outside the computer player's opening view.");
                Require(fixture.Root.GetComponentsInChildren<DesktopPlayer>(true).Length == 1,
                    "Activating the campsite created a duplicate player.");
                CheckDesktopBindings(fixture);
                fixture.Mode.ApplyMode();
                CheckDesktopBindings(fixture);
            }
        }

        private static void ValidateScriptedCampsiteSpawn(Scene scene)
        {
            using (var fixture = new Fixture(scene, true, false))
            {
                fixture.Bridge.scriptedNarration = true;
                Transform spawn = fixture.Setup.playerSpawn;
                spawn.SetPositionAndRotation(new Vector3(-.746f, .1038f, .14f), Quaternion.Euler(0f, 48.58f, 0f));
                var later = new GameObject("Later station spawn").transform;
                later.SetParent(fixture.Root.transform, false);
                var oldSpawns = new[] { later, later };
                fixture.ExistingPlayer.stationSpawns = oldSpawns;
                fixture.Mode.mode = GuidanceControlMode.Desktop;
                fixture.Mode.ApplyMode();
                var player = fixture.ExistingPlayer;
                Require(Vector3.Distance(player.transform.position, spawn.position) < .001f
                    && Quaternion.Angle(player.transform.rotation, spawn.rotation) < .001f,
                    "The narrated campsite ignored its inside-tent starting pose.");
                Require(player.stationSpawns != oldSpawns && oldSpawns[0] == later
                    && player.stationSpawns.Length == 2 && player.stationSpawns[0] == spawn
                    && player.stationSpawns[1] == later,
                    "The campsite reset spawn was not aligned or changed later authored station spawns.");
                Vector3 moved = player.transform.position + new Vector3(.3f, 0f, .2f);
                player.transform.position = moved;
                fixture.Mode.ApplyMode();
                Require(player.transform.position == moved,
                    "Reapplying narrated player mode moved an already active player back inside the tent.");
                typeof(DesktopPlayer).GetMethod("Awake", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)?.Invoke(player, null);
                player.ResetToStation(0);
                Require(Vector3.Distance(player.transform.position, spawn.position) < .001f
                    && Quaternion.Angle(player.transform.rotation, spawn.rotation) < .001f,
                    "Restarting the camp returned the player outside instead of to its inside-tent spawn.");
                CheckDesktopBindings(fixture);
            }
            using (var fixture = new Fixture(scene, true, false))
            {
                fixture.Bridge.scriptedNarration = true;
                Vector3 position = fixture.Origin.transform.position;
                Quaternion rotation = fixture.Origin.transform.rotation;
                Vector3 headOffset = fixture.XRCamera.transform.localPosition;
                fixture.Mode.mode = GuidanceControlMode.XR;
                fixture.Mode.ApplyMode();
                Require(fixture.Mode.PlayerCamera == fixture.XRCamera && !fixture.Mode.IsDesktop
                    && fixture.Origin.transform.position == position
                    && Quaternion.Angle(fixture.Origin.transform.rotation, rotation) < .001f
                    && fixture.XRCamera.transform.localPosition == headOffset,
                    "The narrated desktop spawn fix moved the authored VR rig or headset offset.");
            }
        }

        private static void ValidateActivePreviewPlayer(Scene scene)
        {
            using (var fixture = new Fixture(scene, true, true))
            {
                Vector3 authoredPose = fixture.ExistingPlayer.transform.position;
                Quaternion authoredRotation = fixture.ExistingPlayer.transform.rotation;
                fixture.Mode.mode = GuidanceControlMode.Desktop;
                fixture.Mode.ApplyMode();
                Require(fixture.ExistingPlayer.transform.position == authoredPose
                    && Quaternion.Angle(fixture.ExistingPlayer.transform.rotation, authoredRotation) < .001f,
                    "An already active desktop preview lost its authored starting pose.");
                CheckDesktopBindings(fixture);
            }
        }

        private static void ValidateXRPlayer(Scene scene)
        {
            using (var fixture = new Fixture(scene, false, false))
            {
                fixture.Mode.mode = GuidanceControlMode.XR;
                fixture.Mode.ApplyMode();
                Require(!fixture.Mode.IsDesktop && fixture.Mode.PlayerCamera == fixture.XRCamera
                    && fixture.Origin.gameObject.activeInHierarchy,
                    "Explicit XR mode did not retain the original rig and camera.");
                Require(fixture.Root.GetComponentsInChildren<DesktopPlayer>(true).Length == 0,
                    "XR mode created an unnecessary desktop player.");
                Require(fixture.Coordinator.guide.user == fixture.XRCamera.transform
                    && fixture.Panel.playerCamera == fixture.XRCamera,
                    "XR guidance and dialogue stopped following the actual head.");
                Require(!fixture.CampusPortal.enabled && fixture.CampusPortal.user == fixture.XRCamera.transform,
                    "The additional desktop campus portal could compete with the authored XR portal.");
                Require(EnabledCameras(scene) == 1, "XR mode left a second camera enabled.");
                fixture.Mode.ApplyMode();
                Require(fixture.Mode.PlayerCamera == fixture.XRCamera
                    && fixture.Root.GetComponentsInChildren<DesktopPlayer>(true).Length == 0,
                    "Repeated XR initialization changed the player.");
            }
        }

        private static void CheckDesktopBindings(Fixture fixture)
        {
            var mode = fixture.Mode;
            var camera = mode.PlayerCamera;
            Require(mode.IsDesktop && camera != null && camera.isActiveAndEnabled && camera == mode.desktopPlayer.view,
                "Desktop mode has no active player-owned camera.");
            Require(!fixture.Origin.gameObject.activeInHierarchy && EnabledCameras(fixture.Scene) == 1,
                "The desktop and XR cameras are active at the same time.");
            Require(fixture.Coordinator.guide.user == camera.transform && fixture.Panel.playerCamera == camera
                && fixture.Panel.DialogueCanvas != null && fixture.Panel.DialogueCanvas.worldCamera == camera,
                "Butterfly, world-space dialogue, and keyboard player use different cameras.");
            Require(!fixture.View.useNumberKeys && !fixture.View.showDemoHeadings,
                "Desktop choices conflict with the player's station keys or show an unwanted heading.");
            Require(fixture.CampusPortal.enabled && fixture.CampusPortal.user == camera.transform
                && fixture.CampusPortal.playerRoot == mode.desktopPlayer.transform,
                "The computer campus portal is not following the keyboard player.");
            Require(fixture.ReturnPortal.user == camera.transform
                && fixture.ReturnPortal.playerRoot == mode.desktopPlayer.transform,
                "The campsite return portal still follows the inactive XR head.");
        }

        private static void ValidateSavedMainScenes()
        {
            Scene previous = SceneManager.GetActiveScene();
            var opened = new List<Scene>();
            try
            {
                foreach (string name in MainScenes)
                {
                    string path = "Assets/Main Scenes/" + name + ".unity";
                    Scene scene = SceneManager.GetSceneByPath(path);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        opened.Add(scene);
                    }
                    var modes = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<GuidancePlayerMode>(true)).ToArray();
                    Require(modes.Length == 1, name + ": expected one automatic player-mode component.");
                    var mode = modes[0];
                    Require(mode.mode == GuidanceControlMode.Automatic && mode.xrOrigin != null
                        && mode.director != null && mode.view != null && mode.panel != null
                        && mode.coordinator != null && mode.bridge != null,
                        name + ": player-mode scene bindings are incomplete.");
                    Require(mode.coordinator.guide != null && mode.xrOrigin.Camera != null
                        && mode.coordinator.guide.user == mode.xrOrigin.Camera.transform,
                        name + ": the saved VR binding was replaced instead of switching at runtime.");
                    if (name == MainScenes[0])
                    {
                        Require(mode.campusPortal != null && mode.campusPortal.targetSceneName == MainScenes[1],
                            "The campus keyboard portal does not lead to the real campsite.");
                        Require(EditorBuildSettings.scenes.Any(s => s.enabled
                            && s.path == "Assets/Main Scenes/" + mode.campusPortal.targetSceneName + ".unity"),
                            "The keyboard campus portal's destination is missing from build scenes.");
                    }
                    if (name == MainScenes[1])
                        Require(mode.desktopPlayer != null && mode.bridge.setup != null
                            && mode.bridge.setup.player == mode.desktopPlayer,
                            "The saved campsite mode does not reuse the actual desktop interaction player.");
                }
            }
            finally
            {
                foreach (Scene scene in opened)
                    if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static int EnabledCameras(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Count(camera => camera.isActiveAndEnabled);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Keyboard mode validation: " + message);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly XROrigin Origin;
            public readonly Camera XRCamera;
            public readonly GuidancePlayerMode Mode;
            public readonly GuidanceView View;
            public readonly GuidanceVRPanel Panel;
            public readonly GuidanceRouteCoordinator Coordinator;
            public readonly GuidanceSceneBridge Bridge;
            public readonly GuidanceCampusPortal CampusPortal;
            public readonly GuidanceReturnPortal ReturnPortal;
            public readonly CampsiteSetup Setup;
            public readonly DesktopPlayer ExistingPlayer;
            public readonly Vector3 XRSpawn = new Vector3(3f, 2f, 5f);

            public Fixture(Scene scene, bool campsite, bool activeDesktop)
            {
                Scene = scene;
                Root = new GameObject("Keyboard mode fixture");
                SceneManager.MoveGameObjectToScene(Root, scene);
                var xrObject = Child("Authored XR Origin");
                xrObject.transform.SetPositionAndRotation(XRSpawn, Quaternion.Euler(0f, 125f, 0f));
                Origin = xrObject.AddComponent<XROrigin>();
                var offset = new GameObject("Camera Offset");
                offset.transform.SetParent(xrObject.transform, false);
                var cameraObject = new GameObject("Authored XR camera", typeof(Camera));
                cameraObject.transform.SetParent(offset.transform, false);
                cameraObject.transform.localPosition = Vector3.up * 1.6f;
                XRCamera = cameraObject.GetComponent<Camera>();
                Origin.Camera = XRCamera;
                Origin.CameraFloorOffsetObject = offset;

                var director = Root.AddComponent<GuidanceDirector>();
                View = Root.AddComponent<GuidanceView>();
                View.director = director;
                Panel = Root.AddComponent<GuidanceVRPanel>();
                Panel.view = View;
                Panel.playerCamera = XRCamera;
                var guide = Child("Animated guide controller").AddComponent<ButterflyPointGuide>();
                guide.user = XRCamera.transform;
                Coordinator = Root.AddComponent<GuidanceRouteCoordinator>();
                Coordinator.guide = guide;
                Coordinator.director = director;
                Coordinator.view = View;
                var arrival = Child("Campsite arrival").transform;
                arrival.position = new Vector3(.1f, .1f, 1.2f);
                Coordinator.stations = new[]
                {
                    new GuidanceStationBinding { name = "Arrival", target = arrival, dialoguePrefixes = new[] { "forest." } }
                };
                Bridge = Root.AddComponent<GuidanceSceneBridge>();
                Bridge.director = director;
                Bridge.view = View;
                Bridge.coordinator = Coordinator;
                Bridge.role = campsite ? GuidanceSceneRole.Campsite : GuidanceSceneRole.CampusStart;
                CampusPortal = Child("Campus portal").AddComponent<GuidanceCampusPortal>();
                CampusPortal.enabled = true;
                ReturnPortal = Child("Return portal").AddComponent<GuidanceReturnPortal>();
                Bridge.returnPortal = ReturnPortal;

                if (campsite)
                {
                    Setup = Child("Physical campsite").AddComponent<CampsiteSetup>();
                    var cigarette = Child("Actual cigarette", Setup.transform);
                    cigarette.AddComponent<Rigidbody>();
                    Setup.cigarette = cigarette.AddComponent<CigaretteItem>();
                    Setup.playerSpawn = Child("Authored desktop spawn", Setup.transform).transform;
                    Setup.playerSpawn.position = new Vector3(-1f, .1038f, 3f);
                    var desktop = Child("Authored desktop player");
                    desktop.SetActive(false);
                    ExistingPlayer = desktop.AddComponent<DesktopPlayer>();
                    var desktopCamera = new GameObject("Authored desktop camera", typeof(Camera));
                    desktopCamera.transform.SetParent(desktop.transform, false);
                    desktopCamera.transform.localPosition = Vector3.up * 1.6f;
                    ExistingPlayer.view = desktopCamera.GetComponent<Camera>();
                    desktop.transform.SetPositionAndRotation(new Vector3(8f, .2f, 3f), Quaternion.Euler(0f, 47f, 0f));
                    Setup.player = ExistingPlayer;
                    Bridge.setup = Setup;
                    desktop.SetActive(activeDesktop);
                }

                Mode = Root.AddComponent<GuidancePlayerMode>();
                Mode.xrOrigin = Origin;
                Mode.desktopPlayer = ExistingPlayer;
                Mode.director = director;
                Mode.view = View;
                Mode.panel = Panel;
                Mode.coordinator = Coordinator;
                Mode.bridge = Bridge;
                Mode.campusPortal = CampusPortal;
            }

            private GameObject Child(string name, Transform parent = null)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent != null ? parent : Root.transform, false);
                return child;
            }

            public void Dispose() { Object.DestroyImmediate(Root); }
        }
    }
}
