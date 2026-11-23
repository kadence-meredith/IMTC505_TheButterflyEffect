using System;
using System.Collections.Generic;
using UnityEngine;

namespace ButterflyEffect.Guidance
{
    [Serializable]
    public class GuidanceStationBinding
    {
        public string name;
        public Transform target;
        public string[] dialoguePrefixes = Array.Empty<string>();
    }

    [Serializable]
    public class GuidanceRouteEdge
    {
        public Transform from;
        public Transform to;
        public Transform[] via = Array.Empty<Transform>();
        public bool bidirectional = true;
    }

    /// <summary>
    /// Connects dialogue IDs to independent scene anchors. Destination choices stay
    /// hidden until both butterfly and user arrive; this never moves the user.
    /// </summary>
    public sealed class GuidanceRouteCoordinator : MonoBehaviour
    {
        public GuidanceDirector director;
        public GuidanceView view;
        public ButterflyPointGuide guide;
        public GuidanceStationBinding[] stations = Array.Empty<GuidanceStationBinding>();
        public GuidanceRouteEdge[] routeEdges = Array.Empty<GuidanceRouteEdge>();

        public bool IsTravelling { get; private set; }
        public string PendingLineId { get; private set; } = string.Empty;
        public Transform CurrentStation { get; private set; }

        private GuidanceDirector subscribedDirector;
        private ButterflyPointGuide subscribedGuide;
        private ButterflyPointGuide activeGuide;
        private Transform pendingTarget;
        private string lastPrompt;

        private const string FollowPrompt = "Stay close. I have something to show you.";
        private const string ReturnPrompt = "Come with me. Let’s see what followed us home.";
        private const string WaitPrompt = "I'll wait here. Come a little closer.";

        private void OnEnable() { EnsureSubscriptions(); }
        private void Start() { RefreshRoute(); }

        private void OnDisable()
        {
            RemoveSubscriptions();
            CancelActiveTravel();
            ResumeView();
        }

        /// <summary>Resolve the current line after enabling or assigning this adapter manually.</summary>
        public void RefreshRoute()
        {
            EnsureSubscriptions();
            if (director == null || director.Current == null)
            {
                CancelActiveTravel();
                ResumeView();
                return;
            }
            OnLineChanged(director.Current);
        }

        private void Update()
        {
            if (!IsTravelling) return;
            EnsureSubscriptions();
            if (director == null || view == null || activeGuide == null
                || activeGuide != guide || activeGuide.user == null || pendingTarget == null)
            {
                CancelActiveTravel();
                ResumeView();
                return;
            }
            // A stopped route or another caller taking over the guide must not leave the UI locked.
            if (activeGuide.DestinationPoint != pendingTarget)
            {
                ClearTravelState();
                ResumeView();
                return;
            }
            if (!activeGuide.IsRunning && activeGuide.State != ButterflyGuideState.Finished)
            {
                CancelActiveTravel();
                ResumeView();
                return;
            }
            if (!activeGuide.IsRunning && activeGuide.State == ButterflyGuideState.Finished)
            {
                OnRouteFinished();
                return;
            }
            ShowTravelPrompt();
        }

        private void OnLineChanged(GuidanceLine line)
        {
            if (line == null) return;
            EnsureSubscriptions();
            var binding = FindBinding(line.id);
            if (binding == null || binding.target == null || director == null || view == null
                || guide == null || guide.user == null)
            {
                CancelActiveTravel();
                ResumeView();
                return;
            }

            if (CurrentStation == binding.target && ReadyAt(binding.target))
            {
                CancelActiveTravel();
                CurrentStation = binding.target;
                ResumeView();
                return;
            }

            // Repeated RefreshRoute calls and new lines at the same destination keep
            // the current flight, but only the latest line will be revealed on arrival.
            if (IsTravelling && activeGuide == guide && pendingTarget == binding.target && guide.IsRunning)
            {
                PendingLineId = line.id ?? string.Empty;
                ShowTravelPrompt();
                return;
            }

            CancelActiveTravel();
            PendingLineId = line.id ?? string.Empty;
            pendingTarget = binding.target;
            activeGuide = guide;
            IsTravelling = true;
            guide.GuideThrough(FindRoute(CurrentStation, pendingTarget));
            if (!guide.IsRunning)
            {
                CancelActiveTravel();
                ResumeView();
                return;
            }
            ShowTravelPrompt();
        }

        private GuidanceStationBinding FindBinding(string id)
        {
            if (string.IsNullOrEmpty(id) || stations == null) return null;
            GuidanceStationBinding best = null;
            int longest = -1;
            foreach (var binding in stations)
            {
                if (binding == null || binding.dialoguePrefixes == null) continue;
                foreach (var prefix in binding.dialoguePrefixes)
                {
                    if (string.IsNullOrEmpty(prefix) || prefix.Length <= longest) continue;
                    if (!id.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    best = binding;
                    longest = prefix.Length;
                }
            }
            return best;
        }

        private Transform[] FindRoute(Transform from, Transform destination)
        {
            if (from == null || from == destination || routeEdges == null || routeEdges.Length == 0)
                return new[] { destination };
            var paths = new Dictionary<Transform, List<Transform>>();
            var queue = new Queue<Transform>();
            paths[from] = new List<Transform>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var edge in routeEdges)
                {
                    if (edge == null || edge.from == null || edge.to == null) continue;
                    bool reverse = edge.bidirectional && edge.to == node;
                    Transform next = edge.from == node ? edge.to : reverse ? edge.from : null;
                    if (next == null || paths.ContainsKey(next)) continue;
                    var path = new List<Transform>(paths[node]);
                    if (edge.via != null)
                    {
                        if (reverse)
                        {
                            for (int i = edge.via.Length - 1; i >= 0; i--)
                                if (edge.via[i] != null) path.Add(edge.via[i]);
                        }
                        else foreach (var marker in edge.via) if (marker != null) path.Add(marker);
                    }
                    path.Add(next);
                    if (next == destination) return path.ToArray();
                    paths[next] = path;
                    queue.Enqueue(next);
                }
            }
            return new[] { destination };
        }

        private bool ReadyAt(Transform target)
        {
            if (guide == null || guide.user == null || target == null) return false;
            Vector3 destination = target.position + Vector3.up * Mathf.Max(0, Safe(guide.flightHeight, 1.4f));
            float butterflyTolerance = Mathf.Max(.001f, Safe(guide.pointArrivalDistance, .05f)) + .1f;
            if (Vector3.Distance(guide.transform.position, destination) > butterflyTolerance) return false;
            Vector3 difference = guide.user.position - guide.transform.position;
            if (guide.horizontalUserDistance) difference.y = 0;
            return difference.magnitude <= Mathf.Max(.01f, Safe(guide.userArrivalDistance, 1.3f));
        }

        private void OnRouteFinished()
        {
            // Other finish listeners may already have retargeted the guide. Ignore
            // that old callback rather than revealing the new destination early.
            if (!IsTravelling || activeGuide == null || activeGuide.IsRunning
                || activeGuide.State != ButterflyGuideState.Finished) return;
            if (activeGuide.DestinationPoint != pendingTarget || pendingTarget == null)
            {
                CancelActiveTravel();
                ResumeView();
                return;
            }
            if (director != null && director.Current != null
                && !string.Equals(director.Current.id, PendingLineId, StringComparison.Ordinal))
            {
                RefreshRoute();
                return;
            }

            var destination = pendingTarget;
            // Clear our old trip before rendering: a view callback may show a new line.
            ClearTravelState();
            CurrentStation = destination;
            ResumeView();
        }

        private void ShowTravelPrompt()
        {
            if (view == null || !IsTravelling || activeGuide == null) return;
            string prompt = activeGuide.State == ButterflyGuideState.WaitingForUser ? WaitPrompt
                : PendingLineId.StartsWith("return.", StringComparison.Ordinal) ? ReturnPrompt : FollowPrompt;
            if (string.Equals(lastPrompt, prompt, StringComparison.Ordinal) && view.InteractionLocked) return;
            lastPrompt = prompt;
            view.ShowTravelPrompt(prompt);
        }

        private void OnGuidePointReached(int index) { ShowTravelPrompt(); }
        private void OnGuideLeadChanged() { ShowTravelPrompt(); }

        private void CancelActiveTravel()
        {
            var previous = activeGuide;
            bool stop = IsTravelling && previous != null && previous.IsRunning;
            ClearTravelState();
            if (stop) previous.StopGuidance();
        }

        private void ClearTravelState()
        {
            IsTravelling = false;
            PendingLineId = string.Empty;
            pendingTarget = null;
            activeGuide = null;
            lastPrompt = null;
        }

        private void ResumeView()
        {
            if (view != null) view.ResumeDialogue();
        }

        private void EnsureSubscriptions()
        {
            if (subscribedDirector != director)
            {
                if (subscribedDirector != null) subscribedDirector.LineChanged -= OnLineChanged;
                subscribedDirector = director;
                if (subscribedDirector != null) subscribedDirector.LineChanged += OnLineChanged;
            }
            if (subscribedGuide != guide)
            {
                RemoveGuideListeners(subscribedGuide);
                subscribedGuide = guide;
                if (subscribedGuide != null)
                {
                    if (subscribedGuide.onRouteFinished == null)
                        subscribedGuide.onRouteFinished = new UnityEngine.Events.UnityEvent();
                    subscribedGuide.onRouteFinished.AddListener(OnRouteFinished);
                    subscribedGuide.onPointReached?.AddListener(OnGuidePointReached);
                    subscribedGuide.onLeadPause?.AddListener(OnGuideLeadChanged);
                    subscribedGuide.onLeadResume?.AddListener(OnGuideLeadChanged);
                }
            }
        }

        private void RemoveSubscriptions()
        {
            if (subscribedDirector != null) subscribedDirector.LineChanged -= OnLineChanged;
            RemoveGuideListeners(subscribedGuide);
            subscribedDirector = null;
            subscribedGuide = null;
        }

        private void RemoveGuideListeners(ButterflyPointGuide previous)
        {
            if (previous == null) return;
            previous.onRouteFinished?.RemoveListener(OnRouteFinished);
            previous.onPointReached?.RemoveListener(OnGuidePointReached);
            previous.onLeadPause?.RemoveListener(OnGuideLeadChanged);
            previous.onLeadResume?.RemoveListener(OnGuideLeadChanged);
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
