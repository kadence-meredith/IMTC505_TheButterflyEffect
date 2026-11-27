using System;
using System.Collections.Generic;
using System.Linq;
using ButterflyEffect.Prototype;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace ButterflyEffect.Guidance
{
    public enum GuidanceControlMode { Automatic, Desktop, XR }

    /// <summary>Uses keyboard/mouse when no XR runtime is available, while retaining the authored XR rig.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-400)]
    public sealed class GuidancePlayerMode : MonoBehaviour
    {
        public GuidanceControlMode mode = GuidanceControlMode.Automatic;
        public XROrigin xrOrigin;
        public DesktopPlayer desktopPlayer;
        public GuidanceDirector director;
        public GuidanceView view;
        public GuidanceVRPanel panel;
        public GuidanceRouteCoordinator coordinator;
        public GuidanceSceneBridge bridge;
        public GuidanceCampusPortal campusPortal;

        public Camera PlayerCamera { get; private set; }
        public bool IsDesktop { get; private set; }

        private bool initialized;
        private Vector3 authoredSpawn;
        private Quaternion authoredRotation;
        private bool capturedSpawn;
        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        private void Awake() { ApplyMode(); }

        /// <summary>Safe to call again after assigning references; an already active player keeps its pose and held item.</summary>
        public void ApplyMode()
        {
            ResolveReferences();
            CaptureSpawn();
            bool desktop = WantsDesktop();
            if (initialized && IsDesktop == desktop && PlayerCamera != null && PlayerCamera.isActiveAndEnabled)
            {
                BindCamera(PlayerCamera, desktop ? desktopPlayer.transform : xrOrigin.transform, desktop);
                return;
            }

            if (desktop) EnableDesktop();
            else EnableXR();
            initialized = PlayerCamera != null;
        }

        private void ResolveReferences()
        {
            if (director == null) director = GetComponent<GuidanceDirector>();
            if (view == null) view = GetComponent<GuidanceView>();
            if (panel == null) panel = GetComponent<GuidanceVRPanel>();
            if (coordinator == null) coordinator = GetComponent<GuidanceRouteCoordinator>();
            if (bridge == null) bridge = GetComponent<GuidanceSceneBridge>();
            if (xrOrigin == null) xrOrigin = SceneComponents<XROrigin>().FirstOrDefault();
            if (desktopPlayer == null)
                desktopPlayer = bridge != null && bridge.setup != null && bridge.setup.player != null
                    ? bridge.setup.player : SceneComponents<DesktopPlayer>().FirstOrDefault();
            if (campusPortal == null) campusPortal = GetComponentInChildren<GuidanceCampusPortal>(true);
        }

        private void CaptureSpawn()
        {
            if (capturedSpawn) return;
            Transform source = xrOrigin != null ? xrOrigin.transform
                : desktopPlayer != null ? desktopPlayer.transform : transform;
            authoredSpawn = source.position;
            authoredRotation = Quaternion.Euler(0f, source.eulerAngles.y, 0f);
            capturedSpawn = true;
        }

        private bool WantsDesktop()
        {
            if (mode == GuidanceControlMode.Desktop) return true;
            if (mode == GuidanceControlMode.XR) return false;
            if (Application.platform == RuntimePlatform.Android) return false;
            if (XRSettings.isDeviceActive) return false;
            var settings = XRGeneralSettings.Instance;
            if (settings != null && settings.Manager != null && settings.Manager.activeLoader != null) return false;
            displays.Clear();
            SubsystemManager.GetSubsystems(displays);
            return !displays.Any(display => display.running);
        }

        private void EnableDesktop()
        {
            Camera authoredCamera = xrOrigin != null ? xrOrigin.Camera : null;
            if (xrOrigin != null) xrOrigin.gameObject.SetActive(false);

            bool created = desktopPlayer == null;
            if (created) desktopPlayer = CreateDesktopPlayer(authoredCamera);
            bool wasActive = desktopPlayer.gameObject.activeInHierarchy;
            Camera camera = desktopPlayer.view != null ? desktopPlayer.view
                : desktopPlayer.GetComponentInChildren<Camera>(true);
            if (camera == null) camera = CreateCamera(desktopPlayer.transform, authoredCamera);

            CampsiteSetup setup = bridge != null && bridge.setup != null ? bridge.setup
                : SceneComponents<CampsiteSetup>().FirstOrDefault();
            if (!wasActive)
            {
                Vector3 position = authoredSpawn;
                Quaternion rotation = authoredRotation;
                if (setup != null)
                {
                    if (bridge != null && bridge.scriptedNarration && setup.playerSpawn != null)
                    {
                        position = setup.playerSpawn.position;
                        rotation = setup.playerSpawn.rotation;
                        var spawns = desktopPlayer.stationSpawns;
                        spawns = spawns != null && spawns.Length > 0 ? (Transform[])spawns.Clone() : new Transform[1];
                        spawns[0] = setup.playerSpawn;
                        desktopPlayer.stationSpawns = spawns;
                    }
                    else
                    {
                        position = new Vector3(-.436f, setup.playerSpawn != null ? setup.playerSpawn.position.y : .1038f, .65f);
                        Transform arrival = FindStation(bridge != null && bridge.UsesSequentialExploration
                            ? "cigarette." : "forest.arrival");
                        if (arrival == null) arrival = FindStation("forest.arrival");
                        Vector3 heading = arrival != null ? arrival.position - position : Vector3.forward;
                        heading.y = 0f;
                        if (heading.sqrMagnitude > .001f) rotation = Quaternion.LookRotation(heading);
                    }
                }
                PosePlayer(position, rotation);
            }

            desktopPlayer.view = camera;
            desktopPlayer.enabled = true;
            desktopPlayer.inputEnabled = true;
            if (setup != null)
            {
                setup.player = desktopPlayer;
                desktopPlayer.startingCigarette = setup.cigarette;
                if (bridge != null) bridge.setup = setup;
            }

            desktopPlayer.gameObject.SetActive(true);
            for (Transform node = camera.transform;
                node != null && node.IsChildOf(desktopPlayer.transform); node = node.parent)
                node.gameObject.SetActive(true);
            desktopPlayer.gameObject.tag = "Player";
            if (created && (setup == null || bridge == null || !bridge.scriptedNarration || setup.playerSpawn == null))
            {
                var spawn = new GameObject("Desktop spawn").transform;
                spawn.SetParent(transform, false);
                spawn.SetPositionAndRotation(authoredSpawn, authoredRotation);
                desktopPlayer.stationSpawns = new[] { spawn };
            }

            if (!initialized && setup != null && !wasActive && coordinator != null && coordinator.guide != null)
            {
                Transform arrival = FindStation("forest.arrival");
                coordinator.guide.flightHeight = 1.6f;
                Transform first = bridge != null && bridge.UsesSequentialExploration ? FindStation("cigarette.") : null;
                if (bridge != null && bridge.scriptedNarration && bridge.campsiteSequence != null
                    && bridge.campsiteSequence.tentExit != null) first = bridge.campsiteSequence.tentExit;
                if (first != null)
                {
                    Vector3 direction = first.position - desktopPlayer.transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude < .001f) direction = desktopPlayer.transform.forward;
                    Vector3 position = desktopPlayer.transform.position + direction.normalized * 1.3f;
                    position.y = first.position.y + coordinator.guide.flightHeight;
                    coordinator.guide.transform.position = position;
                }
                else if (arrival != null)
                    coordinator.guide.transform.position = arrival.position + Vector3.up * coordinator.guide.flightHeight;
            }

            IsDesktop = true;
            PlayerCamera = camera;
            BindCamera(camera, desktopPlayer.transform, true);
        }

        private DesktopPlayer CreateDesktopPlayer(Camera source)
        {
            var playerObject = new GameObject("Computer player");
            playerObject.SetActive(false);
            playerObject.transform.SetParent(transform, false);
            var motor = playerObject.AddComponent<CharacterController>();
            motor.height = 1.75f;
            motor.radius = .22f;
            motor.center = new Vector3(0f, .875f, 0f);
            motor.stepOffset = .3f;
            motor.slopeLimit = 45f;
            var player = playerObject.AddComponent<DesktopPlayer>();
            player.view = CreateCamera(playerObject.transform, source);
            return player;
        }

        private static Camera CreateCamera(Transform parent, Camera source)
        {
            var cameraObject = new GameObject("Computer camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = Vector3.up * 1.6f;
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .03f;
            camera.farClipPlane = source != null ? source.farClipPlane : 1000f;
            camera.fieldOfView = source != null ? source.fieldOfView : 60f;
            if (source != null)
            {
                camera.clearFlags = source.clearFlags;
                camera.backgroundColor = source.backgroundColor;
                camera.cullingMask = source.cullingMask;
            }
            return camera;
        }

        private void PosePlayer(Vector3 position, Quaternion rotation)
        {
            var motor = desktopPlayer.GetComponent<CharacterController>();
            bool enabled = motor != null && motor.enabled;
            if (motor != null) motor.enabled = false;
            desktopPlayer.transform.SetPositionAndRotation(position, rotation);
            if (motor != null) motor.enabled = enabled;
        }

        private void EnableXR()
        {
            if (desktopPlayer != null) desktopPlayer.gameObject.SetActive(false);
            if (xrOrigin == null) return;
            xrOrigin.gameObject.SetActive(true);
            Camera camera = xrOrigin.Camera != null ? xrOrigin.Camera : xrOrigin.GetComponentInChildren<Camera>(true);
            if (camera == null) return;
            IsDesktop = false;
            PlayerCamera = camera;
            BindCamera(camera, xrOrigin.transform, false);
        }

        private void BindCamera(Camera camera, Transform playerRoot, bool desktop)
        {
            foreach (var other in SceneComponents<Camera>()) other.enabled = other == camera;
            camera.enabled = true;
            camera.gameObject.tag = "MainCamera";
            var listener = camera.GetComponent<AudioListener>();
            if (listener == null) listener = camera.gameObject.AddComponent<AudioListener>();
            foreach (var other in SceneComponents<AudioListener>()) other.enabled = other == listener;
            if (coordinator != null && coordinator.guide != null) coordinator.guide.user = camera.transform;
            if (view != null) view.useNumberKeys = !desktop;
            if (panel != null) panel.Configure(camera, view);
            foreach (var portal in SceneComponents<GuidanceReturnPortal>())
            {
                portal.user = camera.transform;
                portal.playerRoot = playerRoot;
            }
            if (campusPortal != null)
            {
                campusPortal.user = camera.transform;
                campusPortal.playerRoot = playerRoot;
                campusPortal.enabled = desktop || bridge != null && bridge.scriptedNarration;
            }
            foreach (var portal in SceneComponents<GuidanceCampusPortal>())
            {
                portal.user = camera.transform;
                portal.playerRoot = playerRoot;
            }
        }

        private Transform FindStation(string id)
        {
            if (coordinator == null || coordinator.stations == null) return null;
            return coordinator.stations.FirstOrDefault(station => station != null && station.dialoguePrefixes != null
                && station.dialoguePrefixes.Any(prefix => !string.IsNullOrEmpty(prefix)
                    && id.StartsWith(prefix, StringComparison.Ordinal)))?.target;
        }

        private IEnumerable<T> SceneComponents<T>() where T : Component
        {
            return gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        }
    }
}
