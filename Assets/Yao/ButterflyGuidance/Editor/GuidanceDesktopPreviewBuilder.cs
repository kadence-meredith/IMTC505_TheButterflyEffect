using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ButterflyEffect.Prototype;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Creates a computer-controlled copy of the authored campsite.</summary>
    public static class GuidanceDesktopPreviewBuilder
    {
        public const string SourcePath = "Assets/Main Scenes/1_Campsite_Main.unity";
        public const string ScenePath = "Assets/Yao/ButterflyGuidance/Scenes/Yao_Campsite_DesktopPreview.unity";

        [MenuItem("Butterfly Effect/Open Campsite on Computer")]
        public static void BuildAndOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before opening the computer campsite.");
            if (!File.Exists(SourcePath))
                throw new FileNotFoundException("The integrated campsite scene is missing.", SourcePath);

            Scene scene = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            // Saving under a different path first keeps every edit below in this copy.
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not create the computer campsite scene.");

            var setup = Components<CampsiteSetup>(scene).SingleOrDefault();
            var bridge = Components<GuidanceSceneBridge>(scene).SingleOrDefault();
            var coordinator = Components<GuidanceRouteCoordinator>(scene).SingleOrDefault();
            var panel = Components<GuidanceVRPanel>(scene).SingleOrDefault();
            var view = Components<GuidanceView>(scene).SingleOrDefault();
            if (setup == null || bridge == null || coordinator == null || coordinator.guide == null
                || panel == null || view == null)
                throw new InvalidOperationException("The integrated campsite guidance references are incomplete.");

            DesktopPlayer player = setup.player != null ? setup.player : Components<DesktopPlayer>(scene).SingleOrDefault();
            if (player == null)
                throw new InvalidOperationException("The group's desktop player is missing.");
            Camera camera = player.view != null ? player.view : player.GetComponentInChildren<Camera>(true);
            if (camera == null)
                throw new InvalidOperationException("The group's desktop camera is missing.");

            foreach (var origin in Components<XROrigin>(scene))
            {
                origin.gameObject.SetActive(false);
                Record(origin.gameObject);
            }
            player.gameObject.SetActive(true);
            for (Transform node = camera.transform; node != null && node.IsChildOf(player.transform); node = node.parent)
            {
                node.gameObject.SetActive(true);
                Record(node.gameObject);
            }
            foreach (var other in Components<Camera>(scene))
            {
                other.enabled = other == camera;
                Record(other);
            }
            camera.gameObject.SetActive(true);
            camera.tag = "MainCamera";
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            player.enabled = true;
            player.inputEnabled = true;
            player.view = camera;
            player.startingCigarette = setup.cigarette;
            setup.player = player;

            coordinator.guide.user = camera.transform;
            coordinator.guide.flightHeight = 1.6f;
            coordinator.guide.playOnStart = false;
            var cigaretteStation = coordinator.stations.Single(x => x.name == "Cigarette");
            var campfireStation = coordinator.stations.Single(x => x.name == "Campfire");
            Vector3 direction = Vector3.ProjectOnPlane(campfireStation.target.position - cigaretteStation.target.position,
                Vector3.up).normalized;
            if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
            Vector3 playerStart = cigaretteStation.target.position - direction * .95f;
            playerStart.y = setup.playerSpawn != null ? setup.playerSpawn.position.y : .1038f;
            var motor = player.GetComponent<CharacterController>();
            bool motorEnabled = motor != null && motor.enabled;
            if (motor != null) motor.enabled = false;
            player.transform.SetPositionAndRotation(playerStart, Quaternion.LookRotation(direction, Vector3.up));
            if (motor != null) motor.enabled = motorEnabled;
            camera.transform.localRotation = Quaternion.identity;
            coordinator.guide.transform.SetPositionAndRotation(
                cigaretteStation.target.position + Vector3.up * coordinator.guide.flightHeight, player.transform.rotation);

            bridge.director = view.director;
            bridge.view = view;
            bridge.coordinator = coordinator;
            bridge.setup = setup;
            bridge.session = Components<PrototypeSession>(scene).SingleOrDefault();
            bridge.role = GuidanceSceneRole.Campsite;
            bridge.initialDialogueId = "cigarette.hand";
            view.showDemoHeadings = false;
            view.useNumberKeys = false;
            panel.Configure(camera, view);
            foreach (var portal in Components<GuidanceReturnPortal>(scene))
            {
                portal.user = camera.transform;
                portal.playerRoot = player.transform;
                bridge.returnPortal = portal;
                Record(portal);
            }

            foreach (var hud in Components<PrototypeHud>(scene)) { hud.enabled = false; Record(hud); }
            foreach (var label in Components<WorldLabel>(scene))
            {
                label.enabled = false;
                Record(label);
                var renderer = label.GetComponent<Renderer>();
                if (renderer != null) { renderer.enabled = false; Record(renderer); }
            }
            foreach (var session in Components<PrototypeSession>(scene))
            {
                session.endingRule = EndingRule.CampsiteFinal;
                Record(session);
            }

            foreach (var component in new Component[]
            {
                player, player.transform, motor, camera, camera.gameObject.transform, setup, bridge, view,
                coordinator, coordinator.guide, coordinator.guide.transform, panel
            }) Record(component);
            Record(player.gameObject);
            Record(camera.gameObject);
            foreach (var component in panel.GetComponentsInChildren<Component>(true))
                if (component != null) Record(component);

            Require(camera.isActiveAndEnabled && player.isActiveAndEnabled, "The computer player is inactive.");
            Require(Components<Camera>(scene).Count(c => c.isActiveAndEnabled) == 1,
                "The computer scene must have one active camera.");
            Require(!Components<XROrigin>(scene).Any(x => x.gameObject.activeInHierarchy),
                "An XR rig is still active in the computer copy.");
            Require(coordinator.guide.user == camera.transform && panel.playerCamera == camera,
                "The butterfly or dialogue panel is bound to the wrong camera.");
            Require(bridge.setup.player == player && !view.useNumberKeys && !view.showDemoHeadings,
                "Computer input or clean dialogue configuration is incomplete.");
            Require(!Components<PrototypeHud>(scene).Any(h => h.isActiveAndEnabled),
                "The prototype HUD must stay disabled.");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the computer campsite scene.");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = bridge.gameObject;
            ShowGameView();
        }

        public static void Open()
        {
            if (!File.Exists(ScenePath)) { BuildAndOpen(); return; }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ShowGameView();
        }

        private static void ShowGameView()
        {
            Type gameType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameType == null) return;
            EditorWindow window = EditorWindow.GetWindow(gameType);
            window.Show();
            window.Focus();
        }

        private static IEnumerable<T> Components<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        }

        private static void Record(Object target)
        {
            if (target == null) return;
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
