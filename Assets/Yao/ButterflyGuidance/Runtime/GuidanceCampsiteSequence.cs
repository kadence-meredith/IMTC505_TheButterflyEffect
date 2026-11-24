using System;
using ButterflyEffect.Prototype;
using UnityEngine;

namespace ButterflyEffect.Guidance
{
    public enum CampsiteGuidanceStage { Cigarette, Campfire, WaterBucket, Fireworks, ReturnPortal }

    /// <summary>Suggested tour order, separate from the objects the player chooses to use.</summary>
    [DisallowMultipleComponent]
    public sealed class GuidanceCampsiteSequence : MonoBehaviour
    {
        [Min(.1f)] public float visitRadius = 1.7f;
        [Min(.1f)] public float leaveRadius = 2.5f;
        [Min(0f)] public float minimumVisitSeconds = 2f;
        [Min(0f)] public float setupGraceSeconds = .15f;

        public bool IsInitialized { get; private set; }
        public CampsiteGuidanceStage CurrentStage { get; private set; }
        public string CurrentContext => context;
        public bool RoastingActive { get; private set; }
        public bool IsRoastingFinished => IsExplored(CampsiteGuidanceStage.Campfire);
        public bool BucketPromptAvailable => IsRoastingFinished && User != null && destinations[2] != null
            && Distance(User.position, destinations[2].position) <= Mathf.Max(.1f, visitRadius);
        public int ExploredCount
        {
            get { int count = 0; foreach (bool value in explored) if (value) count++; return count; }
        }

        private GuidanceSceneBridge bridge;
        private GuidanceRouteCoordinator coordinator;
        private GuidanceView view;
        private ButterflyPointGuide guide;
        private readonly Transform[] destinations = new Transform[5];
        private readonly bool[] explored = new bool[4];
        private readonly bool[] entered = new bool[4];
        private readonly float[] visitTime = new float[4];
        private Transform previousGuideStation;
        private ChoiceState observedState;
        private Snapshot previous;
        private float elapsed;
        private string context = string.Empty;
        private bool dismissedCampfire;

        private struct Snapshot
        {
            public CigaretteCondition cigarette;
            public FireCondition fire;
            public bool cigaretteHeld, marshmallowHeld, bucketHeld, fireworksLaunching;
            public int eaten, launched, disposals, pours;
            public int roastBand;
        }

        public void Initialize(GuidanceSceneBridge source)
        {
            if (source == null) return;
            if (IsInitialized && bridge == source) return;
            bridge = source;
            coordinator = source.coordinator;
            view = source.view;
            guide = coordinator != null ? coordinator.guide : null;
            destinations[0] = Station("cigarette.");
            destinations[1] = Station("campfire.");
            destinations[2] = Station("bucket.");
            destinations[3] = Station("fireworks.");
            destinations[4] = Station("return.");
            if (coordinator != null) coordinator.SetIndependentExploration(true);
            if (view != null)
            {
                view.SetExplorationMode(true);
                view.SetExplorationContext(false);
                view.ShowTransientPrompt("Follow the butterfly.", 3f);
            }
            IsInitialized = true;
            ResetProgress();
        }

        private void Update() { Tick(Time.deltaTime); }

        public bool IsExplored(CampsiteGuidanceStage stage)
        {
            int index = (int)stage;
            return index >= 0 && index < explored.Length && explored[index];
        }

        public void MarkExplored(CampsiteGuidanceStage stage)
        {
            int index = (int)stage;
            if (!IsInitialized || index < 0 || index >= explored.Length || explored[index]) return;
            if (stage == CampsiteGuidanceStage.Campfire) { FinishRoasting(); return; }
            explored[index] = true;
            AdvanceSuggestedRoute();
        }

        public void FinishRoasting()
        {
            if (!IsInitialized) return;
            RoastingActive = false;
            explored[(int)CampsiteGuidanceStage.Campfire] = true;
            dismissedCampfire = true;
            context = string.Empty;
            if (view != null) view.SetExplorationContext(false);
            AdvanceSuggestedRoute();
        }

        /// <summary>Runs the same progression and proximity checks used during play.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsInitialized || bridge == null || !Finite(deltaTime) || deltaTime <= 0f) return;
            PrototypeSession session = Session;
            if (session != null && session.state != observedState)
            {
                ResetProgress();
                session = Session;
            }
            Snapshot current = Capture();
            if (current.marshmallowHeld && !IsRoastingFinished) RoastingActive = true;
            bool samplingSetup = elapsed < Mathf.Max(0f, setupGraceSeconds);
            elapsed += deltaTime;
            if (!samplingSetup) RecordActions(previous, current);
            Transform user = User;
            if (user != null)
            {
                float exitDistance = Mathf.Max(visitRadius + .1f, leaveRadius);
                if (destinations[1] != null && Distance(user.position, destinations[1].position) > exitDistance)
                {
                    dismissedCampfire = false;
                    if (RoastingActive && !current.marshmallowHeld) FinishRoasting();
                }
                if (BucketPromptAvailable) dismissedCampfire = false;
                RecordVisits(user.position, deltaTime);
                UpdateContext(user.position, previous, current);
            }
            else if (view != null) view.SetExplorationContext(false);
            previous = current;
        }

        private void ResetProgress()
        {
            Array.Clear(explored, 0, explored.Length);
            Array.Clear(entered, 0, entered.Length);
            Array.Clear(visitTime, 0, visitTime.Length);
            observedState = Session != null ? Session.state : null;
            previous = Capture();
            RoastingActive = previous.marshmallowHeld;
            dismissedCampfire = false;
            elapsed = 0f;
            context = string.Empty;
            previousGuideStation = null;
            CurrentStage = CampsiteGuidanceStage.Cigarette;
            if (view != null) view.SetExplorationContext(false);
            StartSuggestedRoute();
        }

        private void RecordActions(Snapshot before, Snapshot after)
        {
            if (before.cigaretteHeld != after.cigaretteHeld || before.cigarette != after.cigarette
                || after.disposals > before.disposals)
                explored[(int)CampsiteGuidanceStage.Cigarette] = true;
            if (!before.marshmallowHeld && after.marshmallowHeld)
                RoastingActive = true;
            // Water is a separate stop after roasting. Earlier physical use stays in the
            // real session state, but cannot skip visiting the bucket on this tour.
            if (BucketPromptAvailable && (before.bucketHeld != after.bucketHeld
                || after.pours > before.pours || before.fire != after.fire))
                explored[(int)CampsiteGuidanceStage.WaterBucket] = true;
            if (after.launched > before.launched)
                explored[(int)CampsiteGuidanceStage.Fireworks] = true;
            AdvanceSuggestedRoute();
        }

        private void RecordVisits(Vector3 position, float deltaTime)
        {
            float enterDistance = Mathf.Max(.1f, visitRadius);
            float exitDistance = Mathf.Max(enterDistance + .1f, leaveRadius);
            for (int i = 0; i < explored.Length; i++)
            {
                if (explored[i] || destinations[i] == null) continue;
                if (i == (int)CampsiteGuidanceStage.WaterBucket && !IsRoastingFinished) continue;
                float distance = Distance(position, destinations[i].position);
                if (distance <= enterDistance)
                {
                    entered[i] = true;
                    visitTime[i] += deltaTime;
                }
                else if (entered[i] && distance > exitDistance)
                {
                    if (i == (int)CampsiteGuidanceStage.Campfire && RoastingActive) continue;
                    if (visitTime[i] >= Mathf.Max(0f, minimumVisitSeconds))
                    {
                        if (i == (int)CampsiteGuidanceStage.Campfire) FinishRoasting();
                        else explored[i] = true;
                    }
                    entered[i] = false;
                    visitTime[i] = 0f;
                }
            }
            AdvanceSuggestedRoute();
        }

        private void AdvanceSuggestedRoute()
        {
            int index = (int)CurrentStage;
            int next = index;
            while (next < explored.Length && explored[next]) next++;
            if (next == index) return;
            previousGuideStation = destinations[index];
            CurrentStage = (CampsiteGuidanceStage)next;
            StartSuggestedRoute();
        }

        private void StartSuggestedRoute()
        {
            if (guide == null) return;
            Transform target = destinations[(int)CurrentStage];
            if (target == null) { guide.StopGuidance(); return; }
            Transform[] route = coordinator != null
                ? coordinator.RouteBetween(previousGuideStation, target) : new[] { target };
            guide.GuideThrough(route);
        }

        private void UpdateContext(Vector3 position, Snapshot before, Snapshot current)
        {
            string nearest = NearestContext(position);
            bool changedContext = !string.Equals(context, nearest, StringComparison.Ordinal);
            bool changedState = RelevantChange(nearest, before, current)
                || before.bucketHeld != current.bucketHeld;
            context = nearest;
            if (view != null) view.SetExplorationContext(!string.IsNullOrEmpty(context));
            if (string.IsNullOrEmpty(context) || !changedContext && !changedState) return;
            bridge.CancelPhysicalWait();
            if (context == "campfire" && current.eaten > before.eaten)
                bridge.ShowRoastingStatus(true);
            else bridge.Explore(context);
            if (bridge.director == null) return;
            if (context == "fireworks" && current.fireworksLaunching)
                bridge.director.Show("fireworks.watch");
        }

        private string NearestContext(Vector3 position)
        {
            if (CurrentStage == CampsiteGuidanceStage.WaterBucket && BucketPromptAvailable) return "bucket";
            if (dismissedCampfire && CurrentStage == CampsiteGuidanceStage.WaterBucket
                && destinations[1] != null
                && Distance(position, destinations[1].position) <= Mathf.Max(visitRadius, leaveRadius))
                return string.Empty;
            if (!dismissedCampfire && RoastingActive && bridge.setup != null && bridge.setup.marshmallow != null
                && bridge.setup.marshmallow.IsHeld && destinations[1] != null
                && Distance(position, destinations[1].position) <= Mathf.Max(visitRadius, leaveRadius)) return "campfire";
            string nearest = string.Empty;
            float best = float.PositiveInfinity;
            TestContext("cigarette", destinations[0], position, ref nearest, ref best);
            if (!dismissedCampfire) TestContext("campfire", destinations[1], position, ref nearest, ref best);
            if (BucketPromptAvailable) TestContext("bucket", destinations[2], position, ref nearest, ref best);
            TestContext("fireworks", destinations[3], position, ref nearest, ref best);
            TestContext("bin", Station("cigarette.bin"), position, ref nearest, ref best);
            return nearest;
        }

        private void TestContext(string name, Transform target, Vector3 position,
            ref string nearest, ref float best)
        {
            if (target == null) return;
            float distance = Distance(position, target.position);
            if (distance > Mathf.Max(.1f, visitRadius)) return;
            Vector3 toward = target.position - position;
            Vector3 forward = User != null ? User.forward : Vector3.zero;
            toward.y = 0f;
            forward.y = 0f;
            float facing = toward.sqrMagnitude > .01f && forward.sqrMagnitude > .01f
                ? Vector3.Dot(forward.normalized, toward.normalized) : 0f;
            // Adjacent stations overlap. Prefer the one the user is approaching or looking toward.
            float score = distance + (1f - facing) * .6f;
            if (name == context) score -= .1f;
            if (score >= best) return;
            best = score;
            nearest = name;
        }

        private static bool RelevantChange(string name, Snapshot before, Snapshot after)
        {
            switch (name)
            {
                case "cigarette": case "bin":
                    return before.cigarette != after.cigarette || before.cigaretteHeld != after.cigaretteHeld
                        || before.disposals != after.disposals;
                case "campfire":
                    return before.marshmallowHeld != after.marshmallowHeld || before.eaten != after.eaten
                        || before.fire != after.fire || before.cigaretteHeld != after.cigaretteHeld
                        || before.roastBand != after.roastBand;
                case "bucket": return before.bucketHeld != after.bucketHeld || before.fire != after.fire;
                case "fireworks": return before.launched != after.launched
                        || before.fireworksLaunching != after.fireworksLaunching;
                default: return false;
            }
        }

        private Transform Station(string prefix)
        {
            if (coordinator == null || coordinator.stations == null) return null;
            foreach (var station in coordinator.stations)
            {
                if (station == null || station.target == null || station.dialoguePrefixes == null) continue;
                foreach (string value in station.dialoguePrefixes)
                    if (string.Equals(value, prefix, StringComparison.Ordinal)) return station.target;
            }
            return null;
        }

        private Snapshot Capture()
        {
            ChoiceState state = Session != null ? Session.state : null;
            CampsiteSetup setup = bridge != null ? bridge.setup : null;
            return new Snapshot
            {
                cigarette = state != null ? state.cigarette : CigaretteCondition.Held,
                fire = state != null ? state.campfire : FireCondition.Cold,
                cigaretteHeld = setup != null && setup.cigarette != null && setup.cigarette.IsHeld,
                marshmallowHeld = setup != null && setup.marshmallow != null && setup.marshmallow.IsHeld,
                bucketHeld = setup != null && setup.bucket != null && setup.bucket.IsHeld,
                fireworksLaunching = setup != null && setup.fireworks != null && setup.fireworks.IsLaunching,
                eaten = state != null ? state.marshmallowsEaten : 0,
                launched = state != null ? state.fireworksLaunched : 0,
                disposals = state != null ? state.cigaretteDisposals : 0,
                pours = state != null ? state.poursStarted : 0,
                roastBand = RoastBand(setup != null ? setup.marshmallow : null)
            };
        }

        private static int RoastBand(MarshmallowItem item)
        {
            if (item == null) return 0;
            if (item.cookedSeconds >= item.burntSeconds) return 2;
            return item.cookedSeconds >= item.goldenSeconds ? 1 : 0;
        }

        private PrototypeSession Session => bridge != null && bridge.session != null
            ? bridge.session : PrototypeSession.Instance;
        private Transform User => guide != null && guide.user != null ? guide.user
            : bridge != null && bridge.setup != null && bridge.setup.player != null
                ? bridge.setup.player.transform : null;
        private static float Distance(Vector3 a, Vector3 b)
        { Vector3 delta = a - b; delta.y = 0f; return delta.magnitude; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        private void OnEnable()
        {
            if (IsInitialized) StartSuggestedRoute();
        }

        private void OnDisable()
        {
            if (!IsInitialized) return;
            if (guide != null) guide.StopGuidance();
            if (view != null) view.SetExplorationContext(false);
        }
    }
}
