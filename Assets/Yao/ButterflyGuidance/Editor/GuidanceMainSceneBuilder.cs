using System;
using System.Collections.Generic;
using System.Linq;
using ButterflyEffect.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Adds the butterfly layer to the group's authored scenes without rebuilding their scenery.</summary>
    public static class GuidanceMainSceneBuilder
    {
        public const string OwnedRootName = "Yao Butterfly Guidance";
        public const string SceneFolder = "Assets/Main Scenes/";
        public static readonly string[] SceneNames =
        {
            "0_CampusStart_Main", "1_Campsite_Main", "2_CampusFire_Main",
            "3_CampusFire_Main", "4_CampusFire_Main", "5_CampusEnd_Main"
        };

        private static readonly Vector3 CampusSpawn = new Vector3(-5.303f, 2.08f, 4.583f);
        private static readonly Quaternion CampusRotation = new Quaternion(0f, .89550483f, 0f, .44505188f);

        [MenuItem("Butterfly Effect/Open Integrated Campus")]
        public static void OpenCampus()
        {
            EditorSceneManager.OpenScene(SceneFolder + SceneNames[0] + ".unity", OpenSceneMode.Single);
            Selection.activeGameObject = GameObject.Find(OwnedRootName);
            Type gameType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameType != null)
            {
                var game = EditorWindow.GetWindow(gameType);
                game.Show();
                game.Focus();
            }
        }

        [MenuItem("Tools/Butterfly Effect/Integrate Main Scenes")]
        public static void BuildBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before integrating the main scenes.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButterflyGuidedDialogueBuilder.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("The guided butterfly prefab is missing.");
            foreach (string name in SceneNames)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneFolder + name + ".unity") == null)
                    throw new InvalidOperationException("Main scene is missing: " + name);

            // Keep an in-memory clone of the actual campus rig while opening the other scenes.
            // This preserves its authored controller/input configuration without creating another asset.
            Scene campus = EditorSceneManager.OpenScene(SceneFolder + SceneNames[0] + ".unity", OpenSceneMode.Single);
            Transform sourceRig = FindRig(campus);
            if (sourceRig == null) throw new InvalidOperationException("The opening campus XR rig is missing.");
            Scene templateScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject rigTemplate = Object.Instantiate(sourceRig.gameObject);
            rigTemplate.name = "Butterfly integration XR rig template";
            SceneManager.MoveGameObjectToScene(rigTemplate, templateScene);
            rigTemplate.SetActive(false);
            foreach (var component in rigTemplate.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Name == "PortalSceneLoader") Object.DestroyImmediate(component);

            Scene previous = campus;
            try
            {
                for (int i = 0; i < SceneNames.Length; i++)
                {
                    Scene scene = i == 0 ? campus : EditorSceneManager.OpenScene(SceneFolder + SceneNames[i] + ".unity", OpenSceneMode.Additive);
                    SceneManager.SetActiveScene(scene);
                    if (i > 0 && previous.IsValid() && previous.isLoaded) EditorSceneManager.CloseScene(previous, true);
                    Integrate(scene, prefab, rigTemplate, i);
                    ValidateScene(scene, i);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + scene.path);
                    previous = scene;
                }
                EnableMainScenes();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (templateScene.IsValid() && templateScene.isLoaded) EditorSceneManager.CloseScene(templateScene, true);
            }
            EditorSceneManager.OpenScene(SceneFolder + SceneNames[0] + ".unity", OpenSceneMode.Single);
            Debug.Log("Butterfly guidance integrated and validated in all six main scenes.");
        }

        public static void ValidateBatch()
        {
            GuidanceValidation.ValidateCore();
            ButterflyPointGuideValidation.Validate();
            GuidedDialogueValidation.Validate();
            EditorSceneManager.OpenScene(SceneFolder + SceneNames[0] + ".unity", OpenSceneMode.Single);
            GuidanceMainSceneValidation.Validate();
        }

        private static void Integrate(Scene scene, GameObject prefab, GameObject rigTemplate, int index)
        {
            foreach (var old in scene.GetRootGameObjects().Where(x => x.name == OwnedRootName)) Object.DestroyImmediate(old);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            root.name = OwnedRootName;
            var director = root.GetComponent<GuidanceDirector>();
            var view = root.GetComponent<GuidanceView>();
            var coordinator = root.GetComponent<GuidanceRouteCoordinator>();
            if (director == null || view == null || coordinator == null || coordinator.guide == null)
                throw new InvalidOperationException("The guidance prefab is incomplete.");
            director.library = AssetDatabase.LoadAssetAtPath<GuidanceLibrary>(GuidanceDemoBuilder.LibraryPath);
            if (director.library == null) throw new InvalidOperationException("The original English dialogue library is missing.");
            view.showDemoHeadings = false;
            coordinator.director = director;
            coordinator.view = view;

            Transform rig;
            if (index >= 2)
            {
                // The group's ending scenes have scenery and a preview camera, but no XR player yet.
                foreach (var camera in ComponentsInScene<Camera>(scene))
                {
                    camera.gameObject.SetActive(false);
                    Record(camera.gameObject);
                }
                var copy = Object.Instantiate(rigTemplate, root.transform);
                copy.name = "Ending XR Origin (from campus rig)";
                Vector3 endingSpawn = CampusSpawn + (index < 5
                    ? new Vector3(-6.320186f, -.56455245f, -6.7162513f) : Vector3.zero);
                copy.transform.SetPositionAndRotation(endingSpawn, CampusRotation);
                copy.SetActive(true);
                rig = copy.transform;
                foreach (var player in copy.GetComponentsInChildren<CampsiteVRPlayer>(true))
                {
                    player.startWithCigarette = false;
                    player.startingCigarette = null;
                }
                var manager = root.AddComponent<XRInteractionManager>();
                foreach (var interactor in copy.GetComponentsInChildren<XRBaseInteractor>(true))
                    interactor.interactionManager = manager;
            }
            else rig = FindRig(scene);
            if (rig == null) throw new InvalidOperationException("No XR player in " + scene.name);
            Camera playerCamera = rig.GetComponentsInChildren<Camera>(true).FirstOrDefault(x => x.enabled && x.gameObject.activeInHierarchy);
            if (playerCamera == null) throw new InvalidOperationException("No active XR camera in " + scene.name);

            EnsureEventSystem(scene, root.transform);
            var guide = coordinator.guide;
            guide.user = playerCamera.transform;
            guide.flightHeight = index == 1 ? 1.4f : 1.6f;
            guide.autoAdvance = true;
            guide.playOnStart = false;
            guide.horizontalUserDistance = true;
            var anchors = new GameObject("Guidance route anchors").transform;
            anchors.SetParent(root.transform, false);
            Vector3 forward = Vector3.ProjectOnPlane(rig.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            Vector3 startPosition = rig.position + forward * 1.1f;
            // The authored campus spawn falls onto the courtyard during startup.
            // Route markers belong on that walking surface, below the initial rig pose.
            startPosition.y = index == 1 ? .1f : rig.position.y - .68f;
            Transform start = Anchor(anchors, "Start", startPosition);
            if (index == 1) ConfigureCampsite(coordinator, anchors, start);
            else ConfigureCampus(coordinator, anchors, start);
            guide.points = coordinator.stations.Select(x => x.target).Distinct().ToArray();
            guide.transform.SetPositionAndRotation(start.position + Vector3.up * guide.flightHeight, rig.rotation);

            var bridge = root.AddComponent<GuidanceSceneBridge>();
            bridge.role = index == 0 ? GuidanceSceneRole.CampusStart : index == 1 ? GuidanceSceneRole.Campsite : GuidanceSceneRole.CampusEnding;
            bridge.director = director;
            bridge.view = view;
            bridge.coordinator = coordinator;
            bridge.setup = ComponentsInScene<CampsiteSetup>(scene).FirstOrDefault();
            bridge.session = ComponentsInScene<PrototypeSession>(scene).FirstOrDefault();
            bridge.ending = EndingForIndex(index);
            bridge.startingSceneName = SceneNames[0];
            if (index == 1)
                bridge.cigaretteGroundAnchor = coordinator.stations.First(x => x.name == "Cigarette").target;

            if (index == 1)
            {
                var portalObject = new GameObject("Campsite return portal bridge");
                portalObject.transform.SetParent(root.transform, false);
                portalObject.transform.position = new Vector3(-.436f, .58f, -.801f);
                var portal = portalObject.AddComponent<GuidanceReturnPortal>();
                portal.user = playerCamera.transform;
                portal.playerRoot = rig;
                portal.endingScenes = new[] { SceneNames[5], SceneNames[2], SceneNames[3], SceneNames[4] };
                bridge.returnPortal = portal;
            }

            const string materialPath = ButterflyGuidedDialogueBuilder.Root + "/Art/GuidanceUIOverlay.mat";
            var overlay = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (overlay == null)
            {
                var shader = Shader.Find("Butterfly Effect/Guidance UI Overlay");
                if (shader == null) throw new InvalidOperationException("The guidance UI shader is missing.");
                overlay = new Material(shader);
                AssetDatabase.CreateAsset(overlay, materialPath);
            }
            view.uiMaterial = overlay;
            var panel = root.AddComponent<GuidanceVRPanel>();
            panel.Configure(playerCamera, view);
            var mode = root.AddComponent<GuidancePlayerMode>();
            mode.mode = GuidanceControlMode.Automatic;
            mode.xrOrigin = rig.GetComponent<XROrigin>();
            mode.desktopPlayer = bridge.setup != null ? bridge.setup.player : null;
            mode.director = director;
            mode.view = view;
            mode.panel = panel;
            mode.coordinator = coordinator;
            mode.bridge = bridge;
            if (index == 0)
            {
                var campusPortalObject = new GameObject("Campus desktop portal bridge");
                campusPortalObject.transform.SetParent(root.transform, false);
                campusPortalObject.transform.position = new Vector3(-2.934f, 2.38f, 3.629f);
                var campusPortal = campusPortalObject.AddComponent<GuidanceCampusPortal>();
                campusPortal.enabled = false;
                campusPortal.targetSceneName = SceneNames[1];
                mode.campusPortal = campusPortal;
            }
            DisablePrototypeOverlays(scene);
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null) Record(component);
            Record(root);
        }

        private static void ConfigureCampus(GuidanceRouteCoordinator coordinator, Transform anchors, Transform start)
        {
            Vector3 sceneOffset = start.position - CampusSpawn;
            sceneOffset -= Vector3.ProjectOnPlane(CampusRotation * Vector3.forward, Vector3.up).normalized * 1.1f;
            Transform portal = Anchor(anchors, "Campus portal approach", new Vector3(-3.65f, 2.08f, 3.95f) + sceneOffset);
            coordinator.stations = new[]
            {
                Binding("Campus", start, "campus.opening", "campus.butterfly", "campus.reveal", "ending.", "debrief.", "session.", "return."),
                Binding("Campus portal", portal, "campus.portal", "portal.enter")
            };
            coordinator.routeEdges = new[] { Edge(start, portal) };
        }

        private static void ConfigureCampsite(GuidanceRouteCoordinator coordinator, Transform anchors, Transform start)
        {
            Transform hub = Anchor(anchors, "Campsite arrival", new Vector3(.1f, .1f, 1.2f));
            Transform cigarette = Anchor(anchors, "Cigarette approach", new Vector3(-.96f, .1f, 2.65f));
            Transform fire = Anchor(anchors, "Campfire and marshmallow approach", new Vector3(1.3f, .1f, 1.27f));
            Transform bucket = Anchor(anchors, "Water bucket approach", new Vector3(3.2f, .1f, -.03f));
            Transform fireworks = Anchor(anchors, "Fireworks approach", new Vector3(3.99f, .1f, 2.3f));
            Transform bin = Anchor(anchors, "Disposal bin approach", new Vector3(-.96f, .1f, -3.25f));
            Transform portal = Anchor(anchors, "Return portal approach", new Vector3(-.436f, .1f, .65f));
            Transform eastOfTent = Anchor(anchors, "Waypoint east of tent", new Vector3(1.3f, .1f, -2.7f));
            coordinator.stations = new[]
            {
                Binding("Arrival", hub, "forest.", "campus.opening", "campus.butterfly"),
                Binding("Cigarette", cigarette, "cigarette."),
                Binding("Campfire", fire, "campfire.", "bucket.held", "bucket.embers", "bucket.cold"),
                Binding("Water bucket", bucket, "bucket."),
                Binding("Fireworks", fireworks, "fireworks."),
                Binding("Disposal bin", bin, "cigarette.bin", "cigarette.disposed"),
                Binding("Return portal", portal, "return.", "portal.")
            };
            coordinator.routeEdges = new[]
            {
                Edge(start, fire), Edge(hub, fire), Edge(cigarette, fire), Edge(bucket, fire),
                Edge(fireworks, fire), Edge(fire, bin, eastOfTent), Edge(portal, fire)
            };
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.position = position;
            return anchor;
        }

        private static GuidanceStationBinding Binding(string name, Transform target, params string[] prefixes)
        { return new GuidanceStationBinding { name = name, target = target, dialoguePrefixes = prefixes }; }

        private static GuidanceRouteEdge Edge(Transform from, Transform to, params Transform[] via)
        { return new GuidanceRouteEdge { from = from, to = to, via = via, bidirectional = true }; }

        private static GuidanceEnding EndingForIndex(int index)
        {
            if (index == 2) return GuidanceEnding.Threatened;
            if (index == 3) return GuidanceEnding.Burning;
            if (index == 4) return GuidanceEnding.Devastated;
            return GuidanceEnding.Thriving;
        }

        private static Transform FindRig(Scene scene)
        {
            return ComponentsInScene<Transform>(scene).FirstOrDefault(x => x.gameObject.activeInHierarchy &&
                x.name.Contains("XR Origin") && x.GetComponentsInChildren<Camera>(true).Any(c => c.enabled && c.gameObject.activeInHierarchy));
        }

        private static IEnumerable<T> ComponentsInScene<T>(Scene scene) where T : Component
        { return scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)); }

        private static void EnsureEventSystem(Scene scene, Transform owner)
        {
            var events = ComponentsInScene<EventSystem>(scene).FirstOrDefault(x => x.gameObject.activeInHierarchy);
            if (events == null)
            {
                var eventsObject = new GameObject("Butterfly UI EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                eventsObject.transform.SetParent(owner, false);
                events = eventsObject.GetComponent<EventSystem>();
            }
            var xr = events.GetComponent<XRUIInputModule>();
            if (xr == null) xr = events.gameObject.AddComponent<XRUIInputModule>();
            xr.enabled = true;
            foreach (var module in events.GetComponents<BaseInputModule>())
            {
                if (module != xr) module.enabled = false;
                Record(module);
            }
        }

        private static void DisablePrototypeOverlays(Scene scene)
        {
            foreach (var hud in ComponentsInScene<PrototypeHud>(scene)) { hud.enabled = false; Record(hud); }
            foreach (var label in ComponentsInScene<WorldLabel>(scene))
            {
                label.enabled = false;
                Record(label);
                var renderer = label.GetComponent<Renderer>();
                if (renderer != null) { renderer.enabled = false; Record(renderer); }
            }
        }

        private static void Record(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static void EnableMainScenes()
        {
            var settings = new List<EditorBuildSettingsScene>();
            foreach (string name in SceneNames)
            {
                string path = SceneFolder + name + ".unity";
                settings.Add(new EditorBuildSettingsScene(path, true));
            }
            settings.AddRange(EditorBuildSettings.scenes.Where(x => !settings.Any(main => main.path == x.path)));
            EditorBuildSettings.scenes = settings.ToArray();
        }

        private static void ValidateScene(Scene scene, int index)
        {
            var roots = scene.GetRootGameObjects().Where(x => x.name == OwnedRootName).ToArray();
            if (roots.Length != 1) throw new InvalidOperationException("Expected one owned guidance root in " + scene.name);
            if (ComponentsInScene<GuidanceDirector>(scene).Count() != 1 ||
                ComponentsInScene<GuidanceRouteCoordinator>(scene).Count() != 1 ||
                ComponentsInScene<ButterflyPointGuide>(scene).Count() != 1)
                throw new InvalidOperationException("Main scenes must contain one butterfly guidance system: " + scene.name);
            GameObject root = roots[0];
            if (root.GetComponentsInChildren<Component>(true).Any(x => x == null))
                throw new InvalidOperationException("Missing script on the guidance layer in " + scene.name);
            var coordinator = root.GetComponent<GuidanceRouteCoordinator>();
            var guide = coordinator.guide;
            var panel = root.GetComponent<GuidanceVRPanel>();
            var bridge = root.GetComponent<GuidanceSceneBridge>();
            if (root.GetComponent<GuidanceDirector>()?.library == null || bridge == null || guide == null || guide.user == null ||
                guide.user.GetComponent<Camera>() == null || panel == null || panel.playerCamera == null)
                throw new InvalidOperationException("Incomplete guidance/camera binding in " + scene.name);
            if (!guide.user.gameObject.activeInHierarchy || !guide.user.GetComponent<Camera>().enabled)
                throw new InvalidOperationException("Guidance is bound to an inactive camera in " + scene.name);
            if (coordinator.stations.Length == 0 || coordinator.stations.Any(x => x == null || x.target == null || x.dialoguePrefixes.Length == 0))
                throw new InvalidOperationException("Unbound guidance station in " + scene.name);
            if (coordinator.routeEdges.Any(x => x == null || x.from == null || x.to == null || x.via.Any(v => v == null)))
                throw new InvalidOperationException("Unbound route edge in " + scene.name);
            var view = root.GetComponent<GuidanceView>();
            if (view.showDemoHeadings || panel.DialogueCanvas == null || panel.DialogueCanvas.renderMode != RenderMode.WorldSpace)
                throw new InvalidOperationException("The clean XR dialogue card is not configured in " + scene.name);
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                if (text.gameObject.activeSelf && !IsUnderDialogueCard(text.transform))
                    throw new InvalidOperationException("Unexpected text outside the dialogue card in " + scene.name + ": " + text.name);
            if (ComponentsInScene<ButterflyGuideDemoPlayer>(scene).Any() || ComponentsInScene<ButterflyGuideDemoUI>(scene).Any())
                throw new InvalidOperationException("Standalone demo controls must not be added to main scenes.");
            if (index == 1 && (bridge.setup == null || root.GetComponentInChildren<GuidanceReturnPortal>(true) == null))
                throw new InvalidOperationException("Campsite gameplay/return portal binding is missing.");
        }

        private static bool IsUnderDialogueCard(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
                if (current.name == "Butterfly dialogue card") return true;
            return false;
        }
    }
}
