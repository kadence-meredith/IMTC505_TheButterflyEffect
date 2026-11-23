using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Behavioral checks against the actual guide component and Unity transforms.</summary>
    public static class ButterflyPointGuideValidation
    {
        private const float Tolerance = .002f;

        [MenuItem("Butterfly Effect/Validate Point Guidance")]
        public static void Validate()
        {
            ValidateSpeedAndHeadHeight();
            ValidateLeadCapAndHysteresis();
            ValidateArrivalAndCompletion();
            ValidateManualProgression();
            ValidateDirectTargetStopResetAndReplay();
            ValidateInteractionCallbacks();
            ValidateMissingAndDestroyedReferences();
            ValidateInvalidInputs();
            Debug.Log("Butterfly point guidance validation passed: bounded movement; horizontal head tracking; "
                + "large-step lead cap and pause/resume hysteresis; waypoint/user arrival ordering; "
                + "manual progression; exact-once completion and replay; direct destinations; stop/reset; "
                + "route changes from interaction events; "
                + "missing/destroyed targets and user; invalid inputs; user transform preserved.");
        }

        private static void ValidateSpeedAndHeadHeight()
        {
            using (var fixture = new Fixture("Speed and head height"))
            {
                var guide = fixture.Guide;
                guide.points = new[] { fixture.Point("Destination", new Vector3(10, 0, 0)) };
                guide.moveSpeed = 1.25f;
                guide.maxLeadDistance = 3;
                fixture.User.position = new Vector3(0, 12, 0);
                guide.StartGuidance();
                var before = guide.transform.position;
                fixture.Tick(.2f);
                float traveled = Vector3.Distance(before, guide.transform.position);
                Require(traveled > .01f && traveled <= .25f + Tolerance, "Guide did not respect movement speed.");
                Require(guide.State == ButterflyGuideState.Flying, "Head height should not block horizontal guidance.");

                guide.StopGuidance();
                guide.transform.position = before;
                guide.horizontalUserDistance = false;
                guide.StartGuidance();
                fixture.Tick(.2f);
                Near(guide.transform.position, before, "Full 3D distance should pause for a vertically distant user.");
                Require(guide.State == ButterflyGuideState.WaitingForUser, "Full-distance lead pause was not reported.");
            }
        }

        private static void ValidateLeadCapAndHysteresis()
        {
            using (var fixture = new Fixture("Lead cap and hysteresis"))
            {
                var guide = fixture.Guide;
                guide.points = new[] { fixture.Point("Far destination", new Vector3(0, 0, 20)) };
                guide.moveSpeed = 10;
                guide.maxLeadDistance = 3;
                guide.resumeLeadDistance = 2.2f;
                fixture.User.position = new Vector3(0, 1.8f, 0);
                guide.StartGuidance();
                fixture.Tick(20);
                Require(HorizontalDistance(guide.transform.position, fixture.User.position) <= 3 + Tolerance,
                    "A large frame step allowed the butterfly to outrun the user.");
                fixture.Tick(.01f);
                Require(guide.State == ButterflyGuideState.WaitingForUser, "Guide did not pause at the lead limit.");
                Require(fixture.Pauses == 1, "Lead pause should fire once on entering a paused state.");
                var paused = guide.transform.position;

                fixture.User.position = paused + new Vector3(0, 1.6f, -2.6f);
                for (int i = 0; i < 5; i++) fixture.Tick(.02f);
                Near(guide.transform.position, paused, "Guide resumed inside the hysteresis band.");
                Require(guide.State == ButterflyGuideState.WaitingForUser && fixture.Resumes == 0,
                    "Guide should keep waiting until the user reaches the resume distance.");

                fixture.User.position = paused + new Vector3(0, 1.6f, -2);
                fixture.Tick(.05f);
                Require(Vector3.Distance(guide.transform.position, paused) > .01f, "Guide did not resume after catch-up.");
                Require(fixture.Resumes == 1, "Resume event did not fire exactly once.");
                Require(HorizontalDistance(guide.transform.position, fixture.User.position) <= 3 + Tolerance,
                    "Resume movement exceeded the lead limit.");
            }
        }

        private static void ValidateArrivalAndCompletion()
        {
            using (var fixture = new Fixture("Arrival and route completion"))
            {
                var guide = fixture.Guide;
                var first = fixture.Point("First", new Vector3(0, 0, 2));
                var last = fixture.Point("Last", new Vector3(0, 0, 4));
                guide.points = new[] { first, last };
                guide.moveSpeed = 2;
                guide.userArrivalDistance = .5f;
                fixture.User.position = new Vector3(0, 1.7f, 0);
                guide.StartGuidance();
                fixture.Until(() => guide.IsAtPoint, 50, .1f, "Guide did not reach the first target.");
                Require(guide.TargetPoint == first && guide.CurrentPointIndex == 0, "Guide skipped the first waypoint.");
                Require(fixture.PointReached(0) == 1 && fixture.UserReached(0) == 0,
                    "The butterfly and user arrival events must be separate.");
                for (int i = 0; i < 8; i++) fixture.Tick(.2f);
                Require(guide.TargetPoint == first && fixture.Finished == 0,
                    "Guide progressed before the user reached the target.");

                fixture.User.position = first.position + Vector3.up * 1.7f;
                fixture.Until(() => guide.TargetPoint == last, 5, .05f, "Guide did not advance after first-point catch-up.");
                Require(fixture.UserReached(0) == 1, "First-point catch-up event should fire once.");
                fixture.Until(() => guide.IsAtPoint && guide.TargetPoint == last, 50, .1f, "Guide did not reach the last target.");
                Require(fixture.PointReached(1) == 1 && fixture.UserReached(1) == 0 && fixture.Finished == 0,
                    "Route completed before the user caught up to its last point.");
                fixture.User.position = last.position + Vector3.up * 1.7f;
                fixture.Until(() => guide.State == ButterflyGuideState.Finished, 5, .05f, "Route did not finish after final catch-up.");
                Require(fixture.UserReached(1) == 1 && fixture.Finished == 1 && !guide.IsRunning,
                    "Final catch-up or route completion count is incorrect.");
                for (int i = 0; i < 10; i++) { guide.AdvanceToNextPoint(); fixture.Tick(.1f); }
                Require(fixture.PointReached(0) == 1 && fixture.PointReached(1) == 1
                    && fixture.UserReached(0) == 1 && fixture.UserReached(1) == 1 && fixture.Finished == 1,
                    "Finished-route events repeated on subsequent ticks.");
            }
        }

        private static void ValidateManualProgression()
        {
            using (var fixture = new Fixture("Manual progression"))
            {
                var guide = fixture.Guide;
                var first = fixture.Point("First interaction", new Vector3(0, 0, 2));
                var next = fixture.Point("Next interaction", new Vector3(0, 0, 4));
                guide.points = new[] { first, next };
                guide.autoAdvance = false;
                guide.moveSpeed = 2;
                guide.userArrivalDistance = .5f;
                fixture.User.position = new Vector3(0, 1.7f, 0);
                guide.StartGuidance();
                guide.AdvanceToNextPoint();
                Require(guide.TargetPoint == first, "Early advance bypassed travel.");
                fixture.Until(() => guide.IsAtPoint, 50, .1f, "Manual guide did not reach first point.");
                guide.AdvanceToNextPoint();
                Require(guide.TargetPoint == first && fixture.UserReached(0) == 0, "Advance bypassed user arrival.");
                fixture.User.position = first.position + Vector3.up * 1.7f;
                fixture.Until(() => guide.State == ButterflyGuideState.WaitingAtPoint, 5, .05f,
                    "Manual guide did not hold after the user arrived.");
                for (int i = 0; i < 10; i++) fixture.Tick(.2f);
                Require(guide.TargetPoint == first && fixture.UserReached(0) == 1,
                    "Manual guide auto-advanced or repeated its user-arrival event.");
                guide.AdvanceToNextPoint();
                fixture.Tick(.05f);
                Require(guide.TargetPoint == next && guide.CurrentPointIndex == 1,
                    "Manual advance did not begin the next leg.");
            }
        }

        private static void ValidateDirectTargetStopResetAndReplay()
        {
            using (var fixture = new Fixture("Direct target, stop, reset and replay"))
            {
                var guide = fixture.Guide;
                var configured = fixture.Point("Configured first", new Vector3(5, 0, 7));
                var destination = fixture.Point("External destination", new Vector3(0, 0, 3));
                var configuredPoints = new[] { configured };
                guide.points = configuredPoints;
                guide.maxLeadDistance = 100;
                guide.userArrivalDistance = .5f;
                var before = guide.transform.position;
                guide.GuideTo(destination);
                Near(guide.transform.position, before, "Direct guidance teleported the butterfly.");
                Require(guide.TargetPoint == destination && guide.points == configuredPoints && guide.points[0] == configured,
                    "A direct destination overwrote the configured route.");
                for (int i = 0; i < 150 && guide.State != ButterflyGuideState.Finished; i++)
                {
                    fixture.User.position = guide.transform.position + new Vector3(0, 1.6f, -.1f);
                    fixture.Tick(.1f);
                }
                Require(guide.State == ButterflyGuideState.Finished && fixture.Finished == 1, "Direct guidance did not complete.");

                guide.StartGuidance();
                Require(guide.TargetPoint == configured, "Starting again did not restore the configured route.");
                fixture.Tick(.1f);
                guide.StopGuidance();
                var stopped = guide.transform.position;
                fixture.Tick(10);
                Near(guide.transform.position, stopped, "A stopped guide kept moving.");
                Require(guide.State == ButterflyGuideState.Idle && !guide.IsRunning, "Stop did not return to Idle.");

                var userBeforeReset = fixture.User.position;
                guide.ResetToStart();
                Near(guide.transform.position, configured.position + Vector3.up * guide.flightHeight,
                    "Reset did not place the guide at the first configured point.");
                Near(fixture.User.position, userBeforeReset, "Reset moved the user.");
                Require(guide.State == ButterflyGuideState.Idle, "Reset should leave the guide Idle.");
            }

            using (var fixture = new Fixture("Replay events"))
            {
                var guide = fixture.Guide;
                guide.points = new[] { fixture.Point("Only point", Vector3.zero) };
                fixture.User.position = new Vector3(0, 1.7f, 0);
                for (int run = 1; run <= 2; run++)
                {
                    guide.StartGuidance();
                    fixture.Until(() => guide.State == ButterflyGuideState.Finished, 5, .05f, "Single-point replay did not finish.");
                    Require(fixture.PointReached(0) == run && fixture.UserReached(0) == run && fixture.Finished == run,
                        "Restart did not reset per-run event flags.");
                }
            }
        }

        private static void ValidateMissingAndDestroyedReferences()
        {
            using (var fixture = new Fixture("Missing references"))
            {
                var guide = fixture.Guide;
                guide.points = null;
                guide.StartGuidance();
                fixture.Tick(.1f);
                guide.points = new Transform[] { null };
                guide.StartGuidance();
                fixture.Tick(.1f);
                Require(fixture.Finished == 0 && !guide.IsRunning, "An empty route falsely completed.");
                guide.points = new[] { fixture.Point("Destination", new Vector3(0, 0, 5)) };
                guide.user = null;
                var before = guide.transform.position;
                guide.StartGuidance();
                fixture.Tick(.1f);
                Near(guide.transform.position, before, "Guide moved without a user reference.");
                Require(fixture.Finished == 0, "Missing user generated a completion event.");
            }

            using (var fixture = new Fixture("Destroyed references"))
            {
                var guide = fixture.Guide;
                var destination = fixture.Point("Destroyed destination", new Vector3(0, 0, 5));
                guide.points = new[] { destination };
                guide.StartGuidance();
                Object.DestroyImmediate(destination.gameObject);
                fixture.Tick(.1f);
                Require(fixture.Finished == 0 && guide.State != ButterflyGuideState.Finished,
                    "Destroying a destination falsely completed the route.");
                guide.points = new[] { fixture.Point("Destination", new Vector3(0, 0, 5)) };
                guide.StartGuidance();
                var before = guide.transform.position;
                Object.DestroyImmediate(fixture.User.gameObject);
                guide.Tick(.1f);
                Near(guide.transform.position, before, "Guide moved after its user was destroyed.");
                Require(fixture.Finished == 0, "Destroying the user generated completion.");
            }

            using (var fixture = new Fixture("Null point inside route"))
            {
                var guide = fixture.Guide;
                var valid = fixture.Point("Valid destination", Vector3.zero);
                guide.points = new Transform[] { null, valid, null };
                fixture.User.position = new Vector3(0, 1.7f, 0);
                guide.StartGuidance();
                fixture.Until(() => guide.State == ButterflyGuideState.Finished, 6, .05f, "Null route entries prevented valid-point completion.");
                Require(fixture.Finished == 1, "Null entries generated duplicate completion events.");
            }
        }

        private static void ValidateInteractionCallbacks()
        {
            using (var fixture = new Fixture("Route change from arrival callback"))
            {
                var guide = fixture.Guide;
                var first = fixture.Point("First", Vector3.zero);
                var redirected = fixture.Point("Interaction destination", new Vector3(0, 0, 3));
                guide.points = new[] { first };
                guide.onPointReached.AddListener(index => guide.GuideTo(redirected));
                guide.StartGuidance();
                fixture.Tick(.1f);
                Require(guide.TargetPoint == redirected && guide.State == ButterflyGuideState.Flying && !guide.IsAtPoint,
                    "An arrival listener's direct destination was overwritten by the previous route.");
                Require(fixture.UserReached(0) == 0 && fixture.Finished == 0,
                    "The old route fired catch-up/completion after an interaction replaced it.");
            }
            using (var fixture = new Fixture("Stop from user-arrival callback"))
            {
                var guide = fixture.Guide;
                guide.points = new[] { fixture.Point("Only point", Vector3.zero) };
                guide.onUserReachedPoint.AddListener(index => guide.StopGuidance());
                guide.StartGuidance();
                fixture.Tick(.1f);
                Require(guide.State == ButterflyGuideState.Idle && !guide.IsRunning && fixture.Finished == 0,
                    "Stopping from an interaction listener was ignored or spuriously completed the route.");
            }
        }

        private static void ValidateInvalidInputs()
        {
            using (var fixture = new Fixture("Invalid inputs"))
            {
                var guide = fixture.Guide;
                guide.points = new[] { fixture.Point("Destination", new Vector3(0, 0, 5)) };
                guide.StartGuidance();
                var before = guide.transform.position;
                foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) guide.Tick(invalid);
                Near(guide.transform.position, before, "An invalid delta time moved the guide.");
                Require(fixture.Finished == 0, "Invalid delta time produced a completion event.");
                guide.StopGuidance();
                guide.moveSpeed = -10;
                guide.maxLeadDistance = -3;
                guide.resumeLeadDistance = 1000;
                guide.userArrivalDistance = -1;
                guide.pointArrivalDistance = -1;
                guide.flightHeight = float.NaN;
                guide.StartGuidance();
                for (int i = 0; i < 5; i++) fixture.Tick(.1f);
                var position = guide.transform.position;
                Require(Finite(position.x) && Finite(position.y) && Finite(position.z), "Invalid parameters corrupted guide position.");
                Require(fixture.Finished == 0, "Invalid settings falsely completed a distant route.");
            }
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Require(Vector3.Distance(actual, expected) <= Tolerance, message + " Actual=" + actual + "; expected=" + expected);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Butterfly point guidance: " + message);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly Dictionary<int, int> pointEvents = new Dictionary<int, int>();
            private readonly Dictionary<int, int> userEvents = new Dictionary<int, int>();
            public readonly ButterflyPointGuide Guide;
            public readonly Transform User;
            public int Finished;
            public int Pauses;
            public int Resumes;

            public Fixture(string name)
            {
                root = new GameObject("Point guide validation — " + name) { hideFlags = HideFlags.HideAndDontSave };
                var guideObject = new GameObject("Guide");
                guideObject.transform.SetParent(root.transform, false);
                Guide = guideObject.AddComponent<ButterflyPointGuide>();
                Guide.playOnStart = false;
                Guide.transform.position = Vector3.up * Guide.flightHeight;
                User = Point("User", new Vector3(0, 1.7f, 0));
                Guide.user = User;
                Guide.onPointReached.AddListener(index => Increment(pointEvents, index));
                Guide.onUserReachedPoint.AddListener(index => Increment(userEvents, index));
                Guide.onRouteFinished.AddListener(() => Finished++);
                Guide.onLeadPause.AddListener(() => Pauses++);
                Guide.onLeadResume.AddListener(() => Resumes++);
            }

            public Transform Point(string name, Vector3 position)
            {
                var point = new GameObject(name).transform;
                point.SetParent(root.transform, false);
                point.position = position;
                return point;
            }
            public void Tick(float deltaTime)
            {
                if (User == null) { Guide.Tick(deltaTime); return; }
                var position = User.position;
                var rotation = User.rotation;
                var scale = User.localScale;
                Guide.Tick(deltaTime);
                Near(User.position, position, "A guidance tick moved the user.");
                Require(Quaternion.Angle(User.rotation, rotation) < .001f && User.localScale == scale,
                    "A guidance tick changed the user's rotation or scale.");
            }
            public void Until(Func<bool> condition, int limit, float deltaTime, string message)
            {
                for (int i = 0; i < limit && !condition(); i++) Tick(deltaTime);
                Require(condition(), message);
            }
            public int PointReached(int index) { return Count(pointEvents, index); }
            public int UserReached(int index) { return Count(userEvents, index); }
            public void Dispose() { if (root != null) Object.DestroyImmediate(root); }
            private static int Count(Dictionary<int, int> counts, int index) { int count; return counts.TryGetValue(index, out count) ? count : 0; }
            private static void Increment(Dictionary<int, int> counts, int index) { counts[index] = Count(counts, index) + 1; }
        }
    }
}
