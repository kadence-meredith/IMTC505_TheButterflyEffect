using System;
using System.Linq;
using ButterflyEffect.Prototype;
using UnityEngine;

namespace ButterflyEffect.Guidance
{
    [DisallowMultipleComponent]
    public sealed class GuidanceStoryFlow : MonoBehaviour
    {
        public GuidanceSceneBridge bridge;
        public GuidanceNarrator narrator;
        public GuidanceCampusPortal storyPortal;
        public static bool SilentEndingVisit;
        public bool HasStarted { get; private set; }
        public bool StoryFinished { get; private set; }
        public bool CanEnterPortal => StoryFinished && storyPortal != null && (Guide == null
            || HorizontalDistance(Guide.transform.position, storyPortal.transform.position) <= .5f);

        private bool initialized;
        private bool introductionReady;
        private bool departing;
        private float departureTime;
        private Vector3 departureStart;
        private Vector3 departureEnd;
        private GameObject visual;
        private ButterflyPointGuide Guide => bridge != null && bridge.coordinator != null ? bridge.coordinator.guide : null;

        public void Initialize(GuidanceSceneBridge source)
        {
            if (initialized || source == null) return;
            initialized = true;
            bridge = source;
            if (narrator == null) narrator = GetComponent<GuidanceNarrator>();
            narrator?.Configure();
            DisableLegacyTransitions();
            if (bridge.role == GuidanceSceneRole.Campsite)
            {
                if (bridge.campsiteSequence == null) bridge.campsiteSequence = GetComponent<GuidanceCampsiteSequence>();
                if (bridge.campsiteSequence == null) bridge.campsiteSequence = gameObject.AddComponent<GuidanceCampsiteSequence>();
                bridge.campsiteSequence.narrator = narrator;
                var mode = GetComponent<GuidancePlayerMode>();
                if (mode != null && mode.xrOrigin != null)
                {
                    var constraint = GetComponent<GuidanceLocomotionConstraint>();
                    if (constraint == null) constraint = gameObject.AddComponent<GuidanceLocomotionConstraint>();
                    constraint.Configure(mode.xrOrigin);
                }
                return;
            }
            bridge.coordinator?.SetIndependentExploration(true);
            Guide?.StopGuidance();
            if (Guide != null)
            {
                var hover = Guide.GetComponentInChildren<GuidanceButterfly>(true);
                if (hover != null) { visual = hover.gameObject; visual.SetActive(true); }
            }
            PrototypeInteractionPolicy.ReturnHandler = ReturnAllowed;
            bridge.view?.SetExplorationMode(true);
            if (bridge.role == GuidanceSceneRole.CampusEnding && SilentEndingVisit)
            {
                SilentEndingVisit = false;
                StoryFinished = true;
                if (visual != null) visual.SetActive(false);
                bridge.view?.ShowSequencePanel(string.Empty, Array.Empty<string>(), null);
                return;
            }
            Introduce();
        }

        private void Start()
        {
            if (bridge == null) bridge = GetComponent<GuidanceSceneBridge>();
            if (bridge != null && bridge.scriptedNarration) Initialize(bridge);
        }

        private void Introduce()
        {
            string cue = bridge.role == GuidanceSceneRole.CampusStart ? "campus.opening"
                : bridge.ending == GuidanceEnding.Thriving ? "ending.good" : "ending.bad";
            if (narrator == null)
            {
                introductionReady = true;
                bridge.view?.ShowSequencePanel(string.Empty, new[] { "Begin" }, _ => Begin());
                return;
            }
            narrator.PlayIntroduction(cue, () => introductionReady = true, () => Begin(),
                () => introductionReady && !HasStarted);
        }

        public bool Begin()
        {
            if (!initialized || !introductionReady || HasStarted || bridge == null
                || bridge.role == GuidanceSceneRole.Campsite) return false;
            HasStarted = true;
            narrator?.CancelIntroduction();
            FinishStory();
            return true;
        }

        private void FinishStory()
        {
            StoryFinished = true;
            bridge.view?.ShowSequencePanel(string.Empty, Array.Empty<string>(), null);
            if (bridge.role == GuidanceSceneRole.CampusStart || bridge.ending == GuidanceEnding.Thriving)
            {
                if (storyPortal != null)
                {
                    storyPortal.enabled = true;
                    Guide?.GuideTo(storyPortal.transform);
                }
            }
            else if (Guide != null)
            {
                Guide.StopGuidance();
                departing = true;
                departureTime = 0f;
                departureStart = Guide.transform.position;
                Vector3 forward = Guide.user != null ? Guide.user.forward : transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
                departureEnd = departureStart + forward.normalized * 7f + Vector3.up * 3f;
            }
        }

        private void Update()
        {
            if (!initialized || Guide == null) return;
            if (departing)
            {
                departureTime += Time.deltaTime;
                Guide.transform.position = Vector3.Lerp(departureStart, departureEnd, Mathf.Clamp01(departureTime / 3.5f));
                if (departureTime >= 3.5f) { departing = false; if (visual != null) visual.SetActive(false); }
            }
            else if (StoryFinished && CanEnterPortal && visual != null) visual.SetActive(false);
        }

        private bool ReturnAllowed() => CanEnterPortal;

        private void DisableLegacyTransitions()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    string type = component.GetType().Name;
                    if (type == "PortalSceneLoader" || type == "SceneChanger"
                        || bridge.role != GuidanceSceneRole.Campsite && component is CampusReturnPortal)
                        component.enabled = false;
                }
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVisit() { SilentEndingVisit = false; }

        private void OnDisable()
        {
            narrator?.Stop();
            if (PrototypeInteractionPolicy.ReturnHandler == ReturnAllowed) PrototypeInteractionPolicy.Reset();
        }
    }
}
