using System;
using System.Linq;
using ButterflyEffect.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButterflyEffect.Guidance.Editor
{
    public static class GuidanceStrictFlowBuilder
    {
        [MenuItem("Tools/Butterfly Effect/Apply Guided Narration Flow")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before saving the guidance flow.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var library = GuidanceNarrationBuilder.Build();
            foreach (string name in GuidanceMainSceneBuilder.SceneNames)
            {
                Scene scene = EditorSceneManager.OpenScene(GuidanceMainSceneBuilder.SceneFolder + name + ".unity", OpenSceneMode.Single);
                GameObject root = scene.GetRootGameObjects().Single(x => x.name == GuidanceMainSceneBuilder.OwnedRootName);
                var bridge = root.GetComponent<GuidanceSceneBridge>();
                var view = root.GetComponent<GuidanceView>();
                var mode = root.GetComponent<GuidancePlayerMode>();
                var narrator = root.GetComponent<GuidanceNarrator>();
                if (narrator == null) narrator = root.AddComponent<GuidanceNarrator>();
                narrator.view = view;
                narrator.library = library;
                narrator.Configure();
                var flow = root.GetComponent<GuidanceStoryFlow>();
                if (flow == null) flow = root.AddComponent<GuidanceStoryFlow>();
                bridge.scriptedNarration = true;
                bridge.storyFlow = flow;
                flow.bridge = bridge;
                flow.narrator = narrator;
                if (bridge.role == GuidanceSceneRole.Campsite)
                {
                    var sequence = root.GetComponent<GuidanceCampsiteSequence>();
                    if (sequence == null) sequence = root.AddComponent<GuidanceCampsiteSequence>();
                    bridge.campsiteSequence = sequence;
                    sequence.narrator = narrator;
                    sequence.visitRadius = 1.7f;
                    sequence.movementRadius = 3f;
                    ConfigureTentEntrance(bridge, sequence);
                    RefreshCampsiteApproaches(bridge);
                    var constraint = root.GetComponent<GuidanceLocomotionConstraint>();
                    if (constraint == null) constraint = root.AddComponent<GuidanceLocomotionConstraint>();
                    constraint.origin = mode != null ? mode.xrOrigin : null;
                    if (bridge.returnPortal != null)
                    {
                        bridge.returnPortal.gameObject.SetActive(true);
                        bridge.returnPortal.enabled = true;
                        bridge.returnPortal.user = bridge.coordinator.guide.user;
                        bridge.returnPortal.playerRoot = mode != null && mode.xrOrigin != null ? mode.xrOrigin.transform : null;
                    }
                    var physical = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CampusReturnPortal>(true))
                        .Where(x => x.enabled).OrderBy(x => bridge.returnPortal == null ? 0f
                            : Vector3.Distance(x.transform.position, bridge.returnPortal.transform.position)).FirstOrDefault();
                    if (physical != null && bridge.returnPortal != null)
                    {
                        bridge.returnPortal.transform.position = physical.transform.position;
                        Transform destination = FindStation(bridge, "return.");
                        if (destination != null)
                        {
                            Vector3 position = physical.transform.position;
                            position.y = destination.position.y;
                            destination.position = position;
                        }
                    }
                }
                else if (bridge.role == GuidanceSceneRole.CampusStart || bridge.ending == GuidanceEnding.Thriving)
                {
                    var portal = bridge.role == GuidanceSceneRole.CampusStart && mode != null ? mode.campusPortal : flow.storyPortal;
                    if (portal == null)
                        portal = root.GetComponentsInChildren<GuidanceCampusPortal>(true)
                            .FirstOrDefault(x => x.gameObject.name == "Narrative portal bridge");
                    if (portal == null)
                    {
                        var portalObject = new GameObject("Narrative portal bridge");
                        portalObject.transform.SetParent(root.transform, false);
                        Transform destination = FindStation(bridge, "campus.portal");
                        portalObject.transform.position = destination != null ? destination.position : new Vector3(-2.934f, 2.38f, 3.629f);
                        portal = portalObject.AddComponent<GuidanceCampusPortal>();
                    }
                    foreach (var duplicate in root.GetComponentsInChildren<GuidanceCampusPortal>(true))
                        if (duplicate != portal && duplicate.gameObject.name == "Narrative portal bridge")
                            UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
                    portal.gameObject.SetActive(true);
                    portal.user = bridge.coordinator.guide.user;
                    portal.playerRoot = mode != null && mode.xrOrigin != null ? mode.xrOrigin.transform : null;
                    portal.targetSceneName = bridge.role == GuidanceSceneRole.CampusStart ? "1_Campsite_Main" : "4_CampusFire_Main";
                    portal.silentComparison = bridge.role == GuidanceSceneRole.CampusEnding;
                    portal.enabled = true;
                    flow.storyPortal = portal;
                }
                view.Build();
                root.GetComponent<GuidanceVRPanel>()?.Configure(bridge.coordinator.guide.user.GetComponent<Camera>(), view);
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    EditorUtility.SetDirty(component);
                    if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + scene.path);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(GuidanceMainSceneBuilder.SceneFolder + "1_Campsite_Main.unity");
            Debug.Log("Strict per-station Begin flow and screenshot narration saved in six main scenes.");
        }

        private static void ConfigureTentEntrance(GuidanceSceneBridge bridge, GuidanceCampsiteSequence sequence)
        {
            if (bridge.setup == null || bridge.setup.playerSpawn == null) return;
            var facing = Quaternion.Euler(0f, 48.583f, 0f);
            bridge.setup.playerSpawn.SetPositionAndRotation(new Vector3(-.746f, .10384256f, .14f), facing);
            Transform parent = FindStation(bridge, "cigarette.")?.parent ?? bridge.transform;
            Transform exit = parent.Find("Tent exit");
            if (exit == null)
            {
                exit = new GameObject("Tent exit").transform;
                exit.SetParent(parent, false);
            }
            exit.SetPositionAndRotation(new Vector3(-.06704f, .1f, .65410f), facing);
            sequence.tentExit = exit;
            Transform cigarette = FindStation(bridge, "cigarette.");
            if (cigarette != null) cigarette.position = exit.position + exit.forward * 1.8f;
            if (bridge.setup.player != null)
            {
                var spawns = bridge.setup.player.stationSpawns;
                spawns = spawns != null && spawns.Length > 0 ? (Transform[])spawns.Clone() : new Transform[1];
                spawns[0] = bridge.setup.playerSpawn;
                bridge.setup.player.stationSpawns = spawns;
            }
        }

        [MenuItem("Tools/Butterfly Effect/Apply Tent Entrance")]
        public static void ApplyTentEntrance()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before saving the campsite entrance.");
            var scene = EditorSceneManager.OpenScene(GuidanceMainSceneBuilder.SceneFolder + "1_Campsite_Main.unity");
            var root = scene.GetRootGameObjects().Single(x => x.name == GuidanceMainSceneBuilder.OwnedRootName);
            ConfigureTentEntrance(root.GetComponent<GuidanceSceneBridge>(), root.GetComponent<GuidanceCampsiteSequence>());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the campsite entrance.");
            AssetDatabase.SaveAssets();
            Debug.Log("Campsite entrance saved: inside the tent, facing the doorway, with a physical exit boundary.");
        }

        private static void RefreshCampsiteApproaches(GuidanceSceneBridge bridge)
        {
            if (bridge.setup == null) return;
            Transform cigarette = FindStation(bridge, "cigarette.");
            if (cigarette != null && bridge.setup.disposalBin != null
                && Vector2.Distance(new Vector2(cigarette.position.x, cigarette.position.z),
                    new Vector2(bridge.setup.disposalBin.transform.position.x, bridge.setup.disposalBin.transform.position.z)) < .2f)
                cigarette.position = new Vector3(-.96f, cigarette.position.y, 2.65f);
            Transform campfire = FindStation(bridge, "campfire.");
            Transform bucket = FindStation(bridge, "bucket.");
            Transform fireworks = FindStation(bridge, "fireworks.");
            Approach(campfire, bridge.setup.marshmallowTable, cigarette, .7f);
            if (bucket != null && bridge.setup.bucket != null)
            {
                Vector3 position = bridge.setup.bucket.transform.position;
                position.y = bucket.position.y;
                bucket.position = position;
            }
            Approach(fireworks, bridge.setup.fireworksTable, bucket, .7f);
            var bin = FindStation(bridge, "cigarette.bin");
            if (bin != null && bridge.setup.disposalBin != null)
            {
                Vector3 position = bridge.setup.disposalBin.transform.position;
                position.y = bin.position.y;
                bin.position = position;
            }
            Debug.Log("Current campsite approaches: campfire=" + campfire?.position + " bucket=" + bucket?.position
                + " fireworks=" + fireworks?.position);
        }

        private static void Approach(Transform marker, Transform table, Transform from, float clearance)
        {
            if (marker == null || table == null) return;
            var renderers = table.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(table.position, Vector3.one);
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Vector3 direction = from != null ? from.position - bounds.center : Vector3.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
            direction.Normalize();
            Vector3 edge = bounds.ClosestPoint(bounds.center + direction * 100f);
            edge += direction * clearance;
            edge.y = marker.position.y;
            marker.position = edge;
            Debug.Log("Approach " + marker.name + ": table=" + table.name + " bounds=" + bounds + " marker=" + edge);
        }

        private static Transform FindStation(GuidanceSceneBridge bridge, string prefix)
        {
            return bridge.coordinator.stations.Where(x => x != null && x.dialoguePrefixes != null)
                .Select(x => new { station = x, length = x.dialoguePrefixes
                    .Where(p => !string.IsNullOrEmpty(p) && prefix.StartsWith(p, StringComparison.Ordinal))
                    .Select(p => p.Length).DefaultIfEmpty(-1).Max() })
                .Where(x => x.length >= 0).OrderByDescending(x => x.length).FirstOrDefault()?.station.target;
        }

        public static void BuildAndValidate()
        {
            Apply();
            GuidanceValidation.ValidateCore();
            ButterflyPointGuideValidation.Validate();
            GuidedDialogueValidation.Validate();
            GuidanceCampsiteSequenceValidation.RunAll();
            GuidanceStyleValidation.Validate();
        }
    }
}
