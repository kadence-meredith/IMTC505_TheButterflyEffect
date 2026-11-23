using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using ButterflyEffect.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Checks the saved team scenes and the integration's actual movement/ending boundary.</summary>
    public static class GuidanceMainSceneValidation
    {
        private static readonly string[] SceneNames =
        {
            "0_CampusStart_Main", "1_Campsite_Main", "2_CampusFire_Main",
            "3_CampusFire_Main", "4_CampusFire_Main", "5_CampusEnd_Main"
        };

        [MenuItem("Tools/Butterfly Effect/Validate Main Scene Integration")]
        public static void Validate()
        {
            Require(!EditorApplication.isPlaying, "Run saved-scene validation outside Play Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Save scene changes before validating integration.");
            ValidateOriginalCopy();
            ValidateBuildScenes();
            ValidateRuntimeLogic();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in SceneNames)
                {
                    var scene = EditorSceneManager.OpenScene("Assets/Main Scenes/" + name + ".unity", OpenSceneMode.Single);
                    ValidateScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }

            string summary = "Main scene integration validation passed: six team scenes, actual XR head bindings, "
                + "world-space tracked UI, original 38-line library, native animated model, fixed route markers, "
                + "minimal UI, physical ending state, and intermediate/final user-arrival locks.";
            Debug.Log(summary);
            string report = Environment.GetEnvironmentVariable("BUTTERFLY_INTEGRATION_REPORT");
            if (!string.IsNullOrEmpty(report))
            {
                string directory = Path.GetDirectoryName(report);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(report, JsonUtility.ToJson(new ValidationReport
                { passed = true, scenes = SceneNames, summary = summary, utc = DateTime.UtcNow.ToString("O") }, true));
            }
        }

        private static void ValidateOriginalCopy()
        {
            const string originalAsset = "Assets/Yao/ButterflyGuidance/Data/ButterflyGuidance_English.asset";
            // Captured from the actual saved asset before main-scene integration. The
            // original authoring contexts differ from the runtime fallback's metadata.
            const string originalHash = "ef6152d53d452334e199807ef0792552cf5c43473949bf9cf7184827570db1db";
            using (var sha = SHA256.Create())
            {
                string actualHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(originalAsset)))
                    .Replace("-", string.Empty).ToLowerInvariant();
                Require(actualHash == originalHash, "The original English asset changed from the pre-integration baseline.");
            }
            var saved = AssetDatabase.LoadAssetAtPath<GuidanceLibrary>(originalAsset);
            Require(saved != null && saved.lines != null && saved.lines.Length == 38,
                "The original 38-line English asset is missing or its line count changed.");
            var original = GuidanceLibrary.CreateDefault();
            try
            {
                Require(original.lines.Length == 38, "The default original library changed.");
                foreach (var expected in original.lines)
                {
                    var actual = saved.Find(expected.id);
                    Require(actual != null, "Original line missing: " + expected.id);
                    Require(Same(actual.speaker, expected.speaker)
                        && Same(actual.text, expected.text) && Same(actual.nextId, expected.nextId),
                        "Original copy or flow changed: " + expected.id);
                    var expectedChoices = expected.choices ?? Array.Empty<GuidanceChoice>();
                    var actualChoices = actual.choices ?? Array.Empty<GuidanceChoice>();
                    Require(expectedChoices.Length == actualChoices.Length, "Original choices changed: " + expected.id);
                    for (int i = 0; i < expectedChoices.Length; i++)
                        Require(actualChoices[i] != null && Same(actualChoices[i].label, expectedChoices[i].label)
                            && Same(actualChoices[i].nextId, expectedChoices[i].nextId)
                            && Same(actualChoices[i].action, expectedChoices[i].action),
                            "Original choice changed: " + expected.id + " / " + i);
                }
            }
            finally { Object.DestroyImmediate(original); }
        }

        private static bool Same(string a, string b) { return (a ?? string.Empty) == (b ?? string.Empty); }

        private static void ValidateBuildScenes()
        {
            var enabled = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
            foreach (string name in SceneNames)
                Require(enabled.Contains("Assets/Main Scenes/" + name + ".unity"), "Scene absent from enabled build scenes: " + name);
        }

        private static void ValidateScene(Scene scene)
        {
            string prefix = scene.name + ": ";
            Require(scene.GetRootGameObjects().Count(root => root.name == "Yao Butterfly Guidance") == 1,
                prefix + "Expected exactly one owned butterfly guidance root.");
            var directors = Active<GuidanceDirector>(scene);
            var views = Active<GuidanceView>(scene);
            var coordinators = Active<GuidanceRouteCoordinator>(scene);
            var guides = Active<ButterflyPointGuide>(scene);
            var panels = Active<GuidanceVRPanel>(scene);
            var bridges = Active<GuidanceSceneBridge>(scene);
            Require(directors.Length == 1 && views.Length == 1 && coordinators.Length == 1
                && guides.Length == 1 && panels.Length == 1 && bridges.Length == 1,
                prefix + "Expected exactly one complete active guidance rig.");
            var director = directors[0];
            var view = views[0];
            var coordinator = coordinators[0];
            var guide = guides[0];
            var panel = panels[0];
            var bridge = bridges[0];
            Require(director.library != null, prefix + "Scene dialogue library missing.");
            Require(view.director == director && coordinator.director == director && coordinator.view == view
                && coordinator.guide == guide && panel.view == view && bridge.director == director
                && bridge.view == view && bridge.coordinator == coordinator, prefix + "Guidance rig references disagree.");
            var expectedRole = scene.name == SceneNames[0] ? GuidanceSceneRole.CampusStart
                : scene.name == SceneNames[1] ? GuidanceSceneRole.Campsite : GuidanceSceneRole.CampusEnding;
            Require(bridge.role == expectedRole, prefix + "Narrative adapter is configured for the wrong scene role.");
            if (expectedRole == GuidanceSceneRole.CampusEnding)
            {
                var expectedEnding = scene.name == SceneNames[2] ? GuidanceEnding.Threatened
                    : scene.name == SceneNames[3] ? GuidanceEnding.Burning
                    : scene.name == SceneNames[4] ? GuidanceEnding.Devastated : GuidanceEnding.Thriving;
                Require(bridge.ending == expectedEnding, prefix + "Narration does not match the authored ending scene.");
            }

            var cameras = Active<Camera>(scene);
            Require(cameras.Length == 1, prefix + "Expected one enabled scene camera; a legacy camera is still active.");
            var camera = cameras[0];
            Require(camera.GetComponentInParent<XROrigin>() != null, prefix + "The guidance camera is not on the actual XR Origin.");
            Require(panel.playerCamera == camera && guide.user == camera.transform,
                prefix + "Panel and butterfly must follow the actual XR head.");
            Require(!guide.user.IsChildOf(guide.transform), prefix + "The butterfly must not own or move the player.");

            var canvases = view.GetComponentsInChildren<Canvas>(true).Where(c => c.gameObject.activeInHierarchy).ToArray();
            Require(canvases.Length == 1, prefix + "Expected a single dialogue canvas.");
            var canvas = canvases[0];
            Require(canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == camera,
                prefix + "Dialogue canvas is not configured for the XR camera.");
            Require(canvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null,
                prefix + "The dialogue canvas cannot receive XR UI rays.");
            var events = Active<EventSystem>(scene);
            Require(events.Length == 1, prefix + "Expected exactly one active EventSystem.");
            var modules = events[0].GetComponents<BaseInputModule>().Where(m => m.enabled).ToArray();
            Require(modules.Length == 1 && modules[0] is XRUIInputModule,
                prefix + "Expected one enabled XR UI input module.");

            Require(!view.showDemoHeadings, prefix + "Demo heading is still enabled.");
            Transform card = canvas.transform.Find("Butterfly dialogue card");
            Require(card != null, prefix + "Dialogue card missing.");
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
                Require(!text.gameObject.activeInHierarchy || text.transform.IsChildOf(card),
                    prefix + "Extra guidance text is active outside the dialogue card: " + text.name);
            Require(Active<ButterflyGuideDemoUI>(scene).Length == 0,
                prefix + "Old route title, progress, or control-tip UI is enabled.");
            Require(Active<PrototypeHud>(scene).Length == 0, prefix + "Prototype HUD is still enabled.");

            var visuals = guide.GetComponentsInChildren<GuidanceButterfly>(true);
            Require(visuals.Length == 1 && visuals[0].transform != guide.transform,
                prefix + "The floating model must be a separate child of the moving guide.");
            var animations = visuals[0].GetComponentsInChildren<Animation>(true);
            Require(animations.Any(a => a.clip != null && a.clip.length > 0), prefix + "Native butterfly wing animation missing.");
            Require(visuals[0].GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh != null),
                prefix + "Native Ulysses skinned mesh missing.");

            Require(coordinator.stations != null && coordinator.stations.Length > 0, prefix + "No station bindings.");
            foreach (var station in coordinator.stations)
            {
                Require(station != null && station.dialoguePrefixes != null && station.dialoguePrefixes.Length > 0,
                    prefix + "Empty station binding.");
                ValidateAnchor(station.target, guide, prefix);
            }
            foreach (var edge in coordinator.routeEdges ?? Array.Empty<GuidanceRouteEdge>())
            {
                Require(edge != null, prefix + "Null route edge.");
                ValidateAnchor(edge.from, guide, prefix);
                ValidateAnchor(edge.to, guide, prefix);
                foreach (var marker in edge.via ?? Array.Empty<Transform>()) ValidateAnchor(marker, guide, prefix);
            }

            if (scene.name == "1_Campsite_Main")
            {
                Require(Active<CampsiteSetup>(scene).Length == 1, prefix + "Actual campsite setup missing.");
                Require(bridge.setup == Active<CampsiteSetup>(scene)[0] && bridge.session != null,
                    prefix + "Narrative adapter is not bound to the physical campsite setup/session.");
                var portals = Active<GuidanceReturnPortal>(scene);
                Require(portals.Length == 1 && portals[0].user == camera.transform && portals[0].playerRoot != null,
                    prefix + "Return portal is not bound to the actual player.");
                Require(bridge.returnPortal == portals[0], prefix + "Narrative and physical return portals disagree.");
                Require(Active<SceneChanger>(scene).Length == 0,
                    prefix + "A legacy fixed-ending scene changer can race the physical-result return portal.");
                Require(coordinator.routeEdges.Length > 0, prefix + "Campsite routes have no authored waypoint graph.");
            }
        }

        private static void ValidateAnchor(Transform anchor, ButterflyPointGuide guide, string prefix)
        {
            Require(anchor != null, prefix + "A station or route marker is missing.");
            Require(anchor != guide.transform && !anchor.IsChildOf(guide.transform)
                && anchor != guide.user && !anchor.IsChildOf(guide.user), prefix + "Route marker moves with the butterfly/player.");
            Require(anchor.GetComponentInParent<PrototypeItem>() == null && anchor.GetComponentInParent<Rigidbody>() == null,
                prefix + "Route marker is attached to a portable physics object: " + anchor.name);
        }

        private static T[] Active<T>(Scene scene) where T : Behaviour
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .Where(component => component.enabled && component.gameObject.activeInHierarchy).ToArray();
        }

        [MenuItem("Tools/Butterfly Effect/Validate Main Integration Logic")]
        public static void ValidateRuntimeLogic()
        {
            Require(!EditorApplication.isPlaying, "Run deterministic integration validation outside Play Mode.");
            var oldActive = SceneManager.GetActiveScene();
            var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                ValidateGraphArrivalLocks(temporary);
                ValidatePhysicalEndings(temporary);
            }
            finally
            {
                EditorSceneManager.CloseScene(temporary, true);
                if (oldActive.IsValid() && oldActive.isLoaded) SceneManager.SetActiveScene(oldActive);
            }
            Debug.Log("Main integration logic passed: intermediate waypoints and final user catch-up retain dialogue locks; "
                + "all eight physical hazard combinations choose the real ending scene, independent of demo state.");
        }

        private static void ValidateGraphArrivalLocks(Scene scene)
        {
            var root = new GameObject("Integration route validation");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false);
            var library = GuidanceLibrary.CreateDefault();
            try
            {
                var director = root.AddComponent<GuidanceDirector>();
                director.library = library;
                var view = root.AddComponent<GuidanceView>();
                view.director = director;
                view.Build();
                var guideObject = new GameObject("Guide");
                guideObject.transform.SetParent(root.transform, false);
                var guide = guideObject.AddComponent<ButterflyPointGuide>();
                var user = Marker(root, "XR head", new Vector3(0, 1.7f, 0));
                var start = Marker(root, "Station A", Vector3.zero);
                var corner = Marker(root, "Safe path corner", new Vector3(2, 0, 0));
                var destination = Marker(root, "Station B", new Vector3(2, 0, 3));
                guide.user = user;
                guide.flightHeight = 1.4f;
                guide.maxLeadDistance = 10f;
                guide.resumeLeadDistance = 9f;
                guide.userArrivalDistance = .3f;
                guide.moveSpeed = 2f;
                guide.transform.position = start.position + Vector3.up * guide.flightHeight;
                var coordinator = root.AddComponent<GuidanceRouteCoordinator>();
                coordinator.director = director;
                coordinator.view = view;
                coordinator.guide = guide;
                coordinator.stations = new[]
                {
                    new GuidanceStationBinding { name = "A", target = start, dialoguePrefixes = new[] { "cigarette." } },
                    new GuidanceStationBinding { name = "B", target = destination, dialoguePrefixes = new[] { "fireworks." } }
                };
                coordinator.routeEdges = new[]
                {
                    new GuidanceRouteEdge { from = start, to = destination, via = new[] { corner }, bidirectional = true }
                };
                root.SetActive(true);
                Invoke(view, "OnDisable");
                Invoke(view, "OnEnable");
                coordinator.RefreshRoute();
                director.State.Cigarette = CigaretteStatus.Ground;
                director.Show("cigarette.ground");
                guide.Tick(.1f);
                Require(coordinator.CurrentStation == start && !view.InteractionLocked, "Initial station did not settle.");
                var originalUserPosition = user.position;
                director.Show("fireworks.approach");
                Require(guide.PointCount == 2 && guide.TargetPoint == corner && guide.DestinationPoint == destination,
                    "The authored safe corner was skipped by the graph route.");
                TickUntil(guide, () => guide.IsAtPoint, "Guide never reached the intermediate corner.");
                Require(user.position == originalUserPosition, "Route logic moved the actual user transform.");
                Require(view.InteractionLocked && coordinator.IsTravelling && ActiveButtons(view) == 0,
                    "Destination options unlocked at an intermediate waypoint.");
                user.position = corner.position + Vector3.up * 1.7f;
                guide.Tick(.1f);
                Require(guide.TargetPoint == destination && coordinator.CurrentStation == start
                    && view.InteractionLocked && coordinator.IsTravelling, "Intermediate user arrival unlocked destination dialogue.");
                TickUntil(guide, () => guide.IsAtPoint, "Guide never reached the final station.");
                Require(view.InteractionLocked && coordinator.IsTravelling && ActiveButtons(view) == 0,
                    "Final options unlocked before the user caught up.");
                user.position = destination.position + Vector3.up * 1.7f;
                guide.Tick(.1f);
                Require(!view.InteractionLocked && !coordinator.IsTravelling && coordinator.CurrentStation == destination
                    && director.Current.id == "fireworks.approach" && ActiveButtons(view) == 2,
                    "Final user arrival did not restore the destination's original two choices.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(library); }
        }

        private static void ValidatePhysicalEndings(Scene scene)
        {
            var root = new GameObject("Physical ending validation");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false);
            var previous = PrototypeSession.Instance;
            var instance = typeof(PrototypeSession).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            try
            {
                var session = root.AddComponent<PrototypeSession>();
                instance.SetValue(null, session);
                session.endingRule = EndingRule.RemainingHazards;
                var portal = root.AddComponent<GuidanceReturnPortal>();
                var demo = root.AddComponent<GuidanceDirector>();
                demo.State.Cigarette = CigaretteStatus.Ground;
                demo.State.Campfire = CampfireStatus.Burning;
                demo.State.FireworksIgnited = true;
                // Explicit expected outcomes: bit 0=cigarette, bit 1=campfire, bit 2=fireworks.
                string[] expected = { "5_CampusEnd_Main", "2_CampusFire_Main", "2_CampusFire_Main", "3_CampusFire_Main",
                    "2_CampusFire_Main", "3_CampusFire_Main", "3_CampusFire_Main", "4_CampusFire_Main" };
                for (int combination = 0; combination < 8; combination++)
                {
                    session.state = new ChoiceState
                    {
                        cigarette = (combination & 1) != 0 ? CigaretteCondition.Held : CigaretteCondition.Disposed,
                        campfire = (combination & 2) != 0 ? FireCondition.Burning : FireCondition.Cold,
                        fireworksEverLaunched = (combination & 4) != 0,
                        fireworksGrassBurning = false
                    };
                    Require(portal.ResolveEndingScene() == expected[combination],
                        "Physical hazard combination " + combination + " chose the wrong campus scene.");
                }
                demo.State.Cigarette = CigaretteStatus.Disposed;
                demo.State.Campfire = CampfireStatus.Cold;
                demo.State.FireworksIgnited = false;
                Require(portal.ResolveEndingScene() == "4_CampusFire_Main", "Demo state overrode the real unsafe campsite.");
                session.state = new ChoiceState { cigarette = CigaretteCondition.Disposed, campfire = FireCondition.Cold,
                    cigaretteDroppedOutsideBin = true };
                Require(portal.ResolveEndingScene() == "2_CampusFire_Main", "Past unsafe cigarette action was erased during return.");
                session.state = new ChoiceState { cigarette = CigaretteCondition.Disposed, campfire = FireCondition.HotEmbers };
                Require(portal.ResolveEndingScene() == "2_CampusFire_Main", "Hot embers incorrectly returned a protected campus.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                instance.SetValue(null, previous);
            }
        }

        private static Transform Marker(GameObject parent, string name, Vector3 position)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent.transform, false);
            marker.position = position;
            return marker;
        }

        private static int ActiveButtons(GuidanceView view)
        {
            return view.GetComponentsInChildren<Button>(true).Count(b => b.interactable && b.gameObject.activeInHierarchy);
        }

        private static void TickUntil(ButterflyPointGuide guide, Func<bool> ready, string failure)
        {
            for (int i = 0; i < 100 && !ready(); i++) guide.Tick(.1f);
            Require(ready(), failure);
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Main scene integration: " + message);
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public bool passed;
            public string[] scenes;
            public string summary;
            public string utc;
        }
    }
}
