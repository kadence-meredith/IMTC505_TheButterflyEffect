using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ButterflyEffect.Guidance
{
    public enum ButterflyGuideState { Idle, Flying, WaitingForUser, WaitingAtPoint, Finished }

    [Serializable]
    public class GuidePointEvent : UnityEvent<int> { }

    /// <summary>
    /// Leads a user through independent scene markers. This moves only the guide;
    /// it does not move the player, require a NavMesh, or depend on the dialogue.
    /// Place the animated/bobbing butterfly model on a child of this object.
    /// </summary>
    public sealed class ButterflyPointGuide : MonoBehaviour
    {
        [Header("Scene references")]
        [Tooltip("Assign the player, camera, or XR head transform to follow. The guide never moves it.")]
        public Transform user;
        [Tooltip("Ordered ground/object markers. Null markers are ignored when a route begins.")]
        public Transform[] points = Array.Empty<Transform>();

        [Header("Flight")]
        [Min(0)] public float flightHeight = 1.4f;
        [Min(0)] public float moveSpeed = 1.1f;
        [Min(0)] public float rotationSpeed = 150f;
        [Min(.001f)] public float pointArrivalDistance = .05f;

        [Header("Wait for the user")]
        [Min(.1f)] public float maxLeadDistance = 3f;
        [Min(0)] public float resumeLeadDistance = 2.2f;
        [Min(.01f)] public float userArrivalDistance = 1.3f;
        [Tooltip("Measure user proximity in XZ, so camera/head height does not affect arrival.")]
        public bool horizontalUserDistance = true;
        [Tooltip("Continue after the user catches up. Disable to wait for an interaction to call AdvanceToNextPoint.")]
        public bool autoAdvance = true;
        public bool playOnStart;

        [Header("Integration events (zero-based point index)")]
        public GuidePointEvent onPointReached = new GuidePointEvent();
        public GuidePointEvent onUserReachedPoint = new GuidePointEvent();
        public UnityEvent onRouteFinished = new UnityEvent();
        public UnityEvent onLeadPause = new UnityEvent();
        public UnityEvent onLeadResume = new UnityEvent();

        public ButterflyGuideState State { get; private set; } = ButterflyGuideState.Idle;
        public int CurrentPointIndex { get; private set; } = -1;
        public Transform TargetPoint
        {
            get
            {
                return CurrentPointIndex >= 0 && CurrentPointIndex < route.Count
                    ? route[CurrentPointIndex] : null;
            }
        }
        public bool IsAtPoint { get; private set; }
        public bool IsRunning { get; private set; }
        /// <summary>Number of valid markers copied into the current active route.</summary>
        public int PointCount { get { return route.Count; } }
        public Transform DestinationPoint { get { return route.Count > 0 ? route[route.Count - 1] : null; } }

        private readonly List<Transform> route = new List<Transform>();
        private bool userReached;
        private bool leadPaused;
        private bool finishRaised;
        private int revision;

        private void Start()
        {
            if (playOnStart) StartGuidance();
        }

        private void Update() { Tick(Time.deltaTime); }

        /// <summary>Start the configured route from the guide's current position, without teleporting.</summary>
        public void StartGuidance() { BeginRoute(points); }

        /// <summary>Lead to one external destination without changing the configured points array.</summary>
        public void GuideTo(Transform destination) { BeginRoute(new[] { destination }); }

        /// <summary>Follow a temporary route without replacing the authored route.</summary>
        public void GuideThrough(Transform[] destinations) { BeginRoute(destinations); }

        public void StopGuidance()
        {
            revision++;
            IsRunning = false;
            leadPaused = false;
            State = ButterflyGuideState.Idle;
        }

        /// <summary>Explicitly reset only the guide to the first configured marker, ready to start again.</summary>
        public void ResetToStart()
        {
            StopGuidance();
            SanitizeSettings();
            CopyRoute(points);
            CurrentPointIndex = route.Count == 0 ? -1 : 0;
            IsAtPoint = false;
            userReached = false;
            finishRaised = false;
            if (TargetPoint == null || (user != null && !ValidUserReference())) return;
            Vector3 destination = TargetPoint.position + Vector3.up * flightHeight;
            if (Finite(destination)) transform.position = destination;
        }

        /// <summary>Continue a manual route only after both guide and user have arrived.</summary>
        public void AdvanceToNextPoint()
        {
            if (!IsRunning || !IsAtPoint || !userReached || !ValidUserReference() || TargetPoint == null) return;
            SanitizeSettings();
            if (!Finite(user.position) || UserDistance(transform.position) > userArrivalDistance) return;
            if (CurrentPointIndex == route.Count - 1) { FinishRoute(); return; }
            revision++;
            CurrentPointIndex++;
            IsAtPoint = false;
            userReached = false;
            leadPaused = false;
            if (TargetPoint == null) { StopGuidance(); return; }
            State = ButterflyGuideState.Flying;
        }

        /// <summary>Advances the real runtime logic; exposed for deterministic editor validation.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsRunning || !Finite(deltaTime) || deltaTime <= 0) return;
            SanitizeSettings();
            if (!ValidUserReference() || TargetPoint == null || !Finite(user.position) || !Finite(transform.position))
            {
                StopGuidance();
                return;
            }
            Vector3 destination = TargetPoint.position + Vector3.up * flightHeight;
            if (!Finite(destination)) { StopGuidance(); return; }
            int token = revision;

            if (IsAtPoint)
            {
                WaitForUser(token);
                return;
            }

            // Recognise an existing arrival before applying the separation guard.
            if (Vector3.Distance(transform.position, destination) <= pointArrivalDistance)
            {
                ArriveAtPoint(token);
                return;
            }

            float separation = UserDistance(transform.position);
            if (leadPaused)
            {
                if (separation > resumeLeadDistance)
                {
                    State = ButterflyGuideState.WaitingForUser;
                    return;
                }
                leadPaused = false;
                State = ButterflyGuideState.Flying;
                onLeadResume?.Invoke();
                if (!StillRunning(token)) return;
            }
            else if (separation >= maxLeadDistance - .0001f)
            {
                PauseForUser(token);
                return;
            }

            State = ButterflyGuideState.Flying;
            Vector3 previous = transform.position;
            Vector3 candidate = Vector3.MoveTowards(previous, destination, moveSpeed * deltaTime);
            candidate = LimitStepToLeadRadius(previous, candidate);
            Vector3 direction = candidate - previous;
            transform.position = candidate;
            direction.y = 0;
            if (direction.sqrMagnitude > .000001f && rotationSpeed > 0)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), rotationSpeed * deltaTime);

            if (Vector3.Distance(candidate, destination) <= pointArrivalDistance)
                ArriveAtPoint(token);
            else if (UserDistance(candidate) >= maxLeadDistance - .0001f)
                PauseForUser(token);
        }

        private void BeginRoute(IEnumerable<Transform> markers)
        {
            revision++;
            SanitizeSettings();
            CopyRoute(markers);
            CurrentPointIndex = route.Count == 0 ? -1 : 0;
            IsAtPoint = false;
            userReached = false;
            leadPaused = false;
            finishRaised = false;
            IsRunning = ValidUserReference() && route.Count > 0;
            State = IsRunning ? ButterflyGuideState.Flying : ButterflyGuideState.Idle;
        }

        private void CopyRoute(IEnumerable<Transform> markers)
        {
            route.Clear();
            if (markers == null) return;
            foreach (var marker in markers)
                if (marker != null && marker != transform && !marker.IsChildOf(transform)) route.Add(marker);
        }

        private void ArriveAtPoint(int token)
        {
            IsAtPoint = true;
            leadPaused = false;
            State = ButterflyGuideState.WaitingForUser;
            onPointReached?.Invoke(CurrentPointIndex);
            if (StillRunning(token)) WaitForUser(token);
        }

        private void WaitForUser(int token)
        {
            if (!ValidUserReference()) { StopGuidance(); return; }
            if (!userReached)
            {
                State = ButterflyGuideState.WaitingForUser;
                if (UserDistance(transform.position) > userArrivalDistance) return;
                userReached = true;
                State = ButterflyGuideState.WaitingAtPoint;
                onUserReachedPoint?.Invoke(CurrentPointIndex);
                if (!StillRunning(token)) return;
            }
            if (CurrentPointIndex == route.Count - 1) FinishRoute();
            else if (autoAdvance) AdvanceToNextPoint();
            else State = ButterflyGuideState.WaitingAtPoint;
        }

        private void PauseForUser(int token)
        {
            State = ButterflyGuideState.WaitingForUser;
            if (leadPaused) return;
            leadPaused = true;
            onLeadPause?.Invoke();
            // Listener calls may stop, restart, or replace the route; Tick must then return.
            if (!StillRunning(token)) return;
        }

        private void FinishRoute()
        {
            if (finishRaised) return;
            revision++;
            IsRunning = false;
            leadPaused = false;
            State = ButterflyGuideState.Finished;
            finishRaised = true;
            onRouteFinished?.Invoke();
        }

        private bool StillRunning(int token) { return IsRunning && revision == token; }
        private bool ValidUserReference() { return user != null && user != transform && !user.IsChildOf(transform); }

        private Vector3 UserVector(Vector3 position)
        {
            Vector3 difference = position - user.position;
            if (horizontalUserDistance) difference.y = 0;
            return difference;
        }

        private float UserDistance(Vector3 position) { return UserVector(position).magnitude; }

        private Vector3 LimitStepToLeadRadius(Vector3 from, Vector3 candidate)
        {
            if (UserDistance(candidate) <= maxLeadDistance) return candidate;
            Vector3 offset = UserVector(from);
            Vector3 step = candidate - from;
            if (horizontalUserDistance) step.y = 0;
            float a = Vector3.Dot(step, step);
            if (a <= .0000001f) return from;
            float b = 2 * Vector3.Dot(offset, step);
            float c = Vector3.Dot(offset, offset) - maxLeadDistance * maxLeadDistance;
            float discriminant = Mathf.Max(0, b * b - 4 * a * c);
            float fraction = Mathf.Clamp01((-b + Mathf.Sqrt(discriminant)) / (2 * a));
            return Vector3.Lerp(from, candidate, fraction);
        }

        private void OnValidate() { SanitizeSettings(); }

        private void SanitizeSettings()
        {
            flightHeight = Mathf.Max(0, Safe(flightHeight, 1.4f));
            moveSpeed = Mathf.Max(0, Safe(moveSpeed, 1.1f));
            rotationSpeed = Mathf.Max(0, Safe(rotationSpeed, 150f));
            pointArrivalDistance = Mathf.Max(.001f, Safe(pointArrivalDistance, .05f));
            userArrivalDistance = Mathf.Max(.01f, Safe(userArrivalDistance, 1.3f));
            maxLeadDistance = Mathf.Max(.1f, Safe(maxLeadDistance, 3f));
            resumeLeadDistance = Mathf.Clamp(Safe(resumeLeadDistance, 2.2f), 0, maxLeadDistance - .001f);
        }

        private static float Safe(float value, float fallback) { return Finite(value) ? value : fallback; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }

        private void OnDrawGizmosSelected()
        {
            if (points == null) return;
            float height = Mathf.Max(0, Safe(flightHeight, 1.4f));
            Vector3 previous = transform.position;
            Gizmos.color = new Color(.28f, .85f, .9f, .9f);
            foreach (var point in points)
            {
                if (point == null || !Finite(point.position)) continue;
                Vector3 position = point.position + Vector3.up * height;
                Gizmos.DrawLine(previous, position);
                Gizmos.DrawWireSphere(position, .09f);
                previous = position;
            }
            if (user != null)
            {
                Gizmos.color = new Color(.28f, .85f, .9f, .3f);
                Gizmos.DrawWireSphere(user.position, Mathf.Max(.1f, Safe(maxLeadDistance, 3f)));
            }
        }
    }
}
