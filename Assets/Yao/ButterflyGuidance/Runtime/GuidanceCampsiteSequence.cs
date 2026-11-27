using System;
using System.Collections.Generic;
using ButterflyEffect.Prototype;
using UnityEngine;

namespace ButterflyEffect.Guidance
{
    public enum CampsiteGuidanceStage { Cigarette, Campfire, WaterBucket, Fireworks, ReturnPortal }
    public enum CampsiteSequencePhase { Intro, Travelling, AwaitingBegin, Narrating, Interacting, Complete }

    [DisallowMultipleComponent]
    public sealed class GuidanceCampsiteSequence : MonoBehaviour
    {
        [Min(.1f)] public float visitRadius = 1.7f;
        [Min(.1f)] public float leaveRadius = 2.5f;
        [Min(.1f)] public float movementRadius = 3f;
        [Min(0f)] public float setupGraceSeconds = .2f;
        public GuidanceNarrator narrator;
        public Transform tentExit;

        public bool IsInitialized { get; private set; }
        public bool HasExitedTent { get; private set; }
        public CampsiteGuidanceStage CurrentStage { get; private set; }
        public CampsiteSequencePhase Phase { get; private set; }
        public string CurrentContext => Phase == CampsiteSequencePhase.Interacting ? Context(CurrentStage) : string.Empty;
        public bool RoastingActive => CurrentStage == CampsiteGuidanceStage.Campfire && Phase == CampsiteSequencePhase.Interacting;
        public bool RoastingReady { get; private set; }
        public bool CanFinishRoasting => RoastingActive && RoastingReady && AtStation(1);
        public bool IsRoastingFinished => completed[1];
        public bool BucketPromptAvailable => CurrentStage == CampsiteGuidanceStage.WaterBucket && Phase == CampsiteSequencePhase.Interacting && AtStation(2);
        public bool CanBegin => CurrentStage != CampsiteGuidanceStage.WaterBucket
            && Phase == CampsiteSequencePhase.AwaitingBegin && introduced[(int)CurrentStage]
            && ReadyAtCurrentStation() && (CurrentStage != CampsiteGuidanceStage.ReturnPortal || !CigaretteHeld());
        public bool CanReturn => Phase == CampsiteSequencePhase.Complete && !CigaretteHeld();
        public int ExploredCount { get { int n = 0; for (int i = 0; i < 4; i++) if (completed[i]) n++; return n; } }
        public Transform User => guide != null ? guide.user : null;

        private GuidanceSceneBridge bridge;
        private GuidanceRouteCoordinator coordinator;
        private GuidanceView view;
        private ButterflyPointGuide guide;
        private readonly Transform[] destinations = new Transform[5];
        private readonly bool[] begun = new bool[5];
        private readonly bool[] introduced = new bool[5];
        private readonly string[] introductionText = new string[5];
        private readonly bool[] completed = new bool[5];
        private Transform[] activeRoute = Array.Empty<Transform>();
        private Vector3 routeOrigin;
        private readonly List<Vector3[]> openedRoutes = new List<Vector3[]>();
        private ChoiceState observedState;
        private Snapshot previous;
        private int revision;
        private float elapsed;
        private string panelKey;
        private bool returnWasHeld;
        private int fireworkBaseline;
        private int goldenEatenBaseline;
        private int burntEatenBaseline;
        private int keepRoastingServing = -1;
        private MarshmallowItem controlledMarshmallow;
        private bool previousRequireServingChoice;
        private bool ownsServingChoice;
        private CigaretteItem[] cigarettes = Array.Empty<CigaretteItem>();

        private enum RoastingPanelState { Raw, Ready, Keeping, Burnt, Consumed, PutDown }
        private enum RoastingAction { Keep, Bite, Fresh, Finish }

        private struct Snapshot
        {
            public bool cigaretteHeld, marshmallowHeld, launching;
            public CigaretteCondition cigarette;
            public FireCondition fire;
            public int launched;
        }

        public void Initialize(GuidanceSceneBridge source)
        {
            if (source == null || IsInitialized && bridge == source) return;
            bridge = source;
            coordinator = source.coordinator;
            guide = coordinator != null ? coordinator.guide : null;
            view = source.view;
            if (narrator == null) narrator = GetComponent<GuidanceNarrator>();
            destinations[0] = Station("cigarette.");
            destinations[1] = Station("campfire.");
            destinations[2] = Station("bucket.");
            destinations[3] = Station("fireworks.");
            destinations[4] = Station("return.");
            coordinator?.SetIndependentExploration(true);
            view?.SetExplorationMode(true);
            view?.SetExplorationContext(false);
            cigarettes = Array.FindAll(FindObjectsByType<CigaretteItem>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                item => item.gameObject.scene == gameObject.scene);
            IsInitialized = true;
            BindPolicy();
            AcquireServingChoice();
            ResetProgress();
        }

        public bool IsExplored(CampsiteGuidanceStage stage) => completed[(int)stage];
        public bool IsStageStarted(CampsiteGuidanceStage stage) => begun[(int)stage];

        private void ResetProgress()
        {
            revision++;
            narrator?.Stop();
            Array.Clear(begun, 0, begun.Length);
            Array.Clear(introduced, 0, introduced.Length);
            Array.Clear(introductionText, 0, introductionText.Length);
            Array.Clear(completed, 0, completed.Length);
            ResetRoastingReadiness();
            observedState = Session != null ? Session.state : null;
            previous = Capture();
            elapsed = 0f;
            openedRoutes.Clear();
            CurrentStage = CampsiteGuidanceStage.Cigarette;
            HasExitedTent = tentExit == null;
            routeOrigin = User != null ? User.position : transform.position;
            activeRoute = EntranceRoute(destinations[0]);
            guide?.StopGuidance();
            Phase = CampsiteSequencePhase.Intro;
            panelKey = null;
            int version = revision;
            Play("camp.tent", () => { if (revision == version) StartRoute(null); });
        }

        private void Update() { Tick(Time.deltaTime); }

        public void Tick(float deltaTime)
        {
            if (!IsInitialized || !Finite(deltaTime) || deltaTime <= 0f || bridge == null) return;
            if (Session != null && Session.state != observedState) { ResetProgress(); return; }
            elapsed += deltaTime;
            if (!HasExitedTent && Phase == CampsiteSequencePhase.Travelling && tentExit != null && User != null)
            {
                Vector3 outward = Vector3.ProjectOnPlane(tentExit.forward, Vector3.up).normalized;
                HasExitedTent = Vector3.Dot(User.position - tentExit.position, outward) >= 0f;
            }
            Snapshot current = Capture();
            if (CurrentStage == CampsiteGuidanceStage.ReturnPortal && returnWasHeld && !current.cigaretteHeld
                && introduced[4] && Phase != CampsiteSequencePhase.Narrating)
            {
                introduced[4] = false;
                begun[4] = false;
                panelKey = null;
                if (ReadyAtCurrentStation()) IntroduceCurrentStage();
                else Phase = CampsiteSequencePhase.Travelling;
                previous = current;
                return;
            }
            if (Phase == CampsiteSequencePhase.Travelling || Phase == CampsiteSequencePhase.AwaitingBegin)
            {
                if (ReadyAtCurrentStation())
                {
                    if (CurrentStage == CampsiteGuidanceStage.WaterBucket) EnterWaterInteraction();
                    else if (!introduced[(int)CurrentStage]) IntroduceCurrentStage();
                    else ShowBegin();
                }
                else
                {
                    Phase = CampsiteSequencePhase.Travelling;
                    Panel("travel", string.Empty, Array.Empty<string>(), null);
                }
            }
            else if (Phase == CampsiteSequencePhase.Interacting)
            {
                ObserveRoastingReadiness();
                if (TryFinishFireworksVisit(current))
                {
                    previous = current;
                    return;
                }
                bool finish = CurrentStage == CampsiteGuidanceStage.Cigarette
                    && (current.cigarette == CigaretteCondition.Disposed || previous.cigaretteHeld && !current.cigaretteHeld);
                finish |= CurrentStage == CampsiteGuidanceStage.WaterBucket && current.fire == FireCondition.Cold;
                if (CurrentStage == CampsiteGuidanceStage.ReturnPortal)
                {
                    if (returnWasHeld && !current.cigaretteHeld) IntroduceCurrentStage();
                }
                else if (finish && AtStation((int)CurrentStage)) FinishCurrentStage();
                else RefreshInteractionPanel();
            }
            else if (Phase == CampsiteSequencePhase.Complete)
            {
                if (current.cigaretteHeld)
                {
                    introduced[4] = begun[4] = completed[4] = false;
                    IntroduceCurrentStage();
                }
            }
            previous = current;
        }

        private void IntroduceCurrentStage()
        {
            Phase = CampsiteSequencePhase.Narrating;
            panelKey = null;
            int version = revision;
            CampsiteGuidanceStage stage = CurrentStage;
            returnWasHeld = stage == CampsiteGuidanceStage.ReturnPortal && CigaretteHeld();
            string cue = stage == CampsiteGuidanceStage.Cigarette ? "camp.cigarette"
                : stage == CampsiteGuidanceStage.Campfire ? "camp.campfire"
                : stage == CampsiteGuidanceStage.Fireworks ? "camp.fireworks"
                : stage == CampsiteGuidanceStage.ReturnPortal ? (returnWasHeld ? "camp.return-held" : "camp.return") : string.Empty;
            Action ready = () =>
            {
                if (version != revision || CurrentStage != stage) return;
                introduced[(int)stage] = true;
                introductionText[(int)stage] = narrator != null ? narrator.LastSubtitleText : string.Empty;
                Phase = CampsiteSequencePhase.AwaitingBegin;
                panelKey = null;
                ShowBegin();
            };
            if (string.IsNullOrEmpty(cue) || narrator == null || narrator.library == null)
            {
                introductionText[(int)stage] = string.Empty;
                introduced[(int)stage] = true;
                Phase = CampsiteSequencePhase.AwaitingBegin;
                ShowBegin();
            }
            else narrator.PlayIntroduction(cue, ready, () => BeginCurrentStage(), () => CanBegin);
        }

        private void ShowBegin()
        {
            Phase = CampsiteSequencePhase.AwaitingBegin;
            Panel("begin-" + CurrentStage + introductionText[(int)CurrentStage], introductionText[(int)CurrentStage],
                new[] { CurrentStage == CampsiteGuidanceStage.Cigarette ? "Continue" : "Begin" }, _ => BeginCurrentStage());
        }

        public bool BeginCurrentStage()
        {
            if (CurrentStage == CampsiteGuidanceStage.WaterBucket || !CanBegin || begun[(int)CurrentStage]) return false;
            narrator?.CancelIntroduction();
            begun[(int)CurrentStage] = true;
            panelKey = null;
            previous = Capture();
            fireworkBaseline = previous.launched;
            if (CurrentStage == CampsiteGuidanceStage.Campfire) ResetRoastingReadiness();
            if (CurrentStage == CampsiteGuidanceStage.ReturnPortal)
            {
                completed[4] = true;
                Phase = CampsiteSequencePhase.Complete;
                Panel("finished", string.Empty, Array.Empty<string>(), null);
                bridge.returnPortal?.Arm();
            }
            else
            {
                Phase = CampsiteSequencePhase.Interacting;
                RefreshInteractionPanel();
            }
            return true;
        }

        private void EnterWaterInteraction()
        {
            if (CurrentStage != CampsiteGuidanceStage.WaterBucket || !ReadyAtCurrentStation()) return;
            introduced[2] = begun[2] = true;
            introductionText[2] = string.Empty;
            Phase = CampsiteSequencePhase.Interacting;
            panelKey = null;
            previous = Capture();
            if (previous.fire == FireCondition.Cold) FinishCurrentStage();
            else RefreshInteractionPanel();
        }

        public bool FinishCurrentStage()
        {
            int index = (int)CurrentStage;
            if (Phase != CampsiteSequencePhase.Interacting || index >= 4 || !AtStation(index)) return false;
            if (CurrentStage == CampsiteGuidanceStage.Campfire && !CanFinishRoasting) return false;
            if (CurrentStage == CampsiteGuidanceStage.Campfire && bridge.setup != null
                && bridge.setup.player != null && bridge.setup.player.isActiveAndEnabled
                && bridge.setup.player.Held == bridge.setup.marshmallow) bridge.setup.player.DropHeld(false);
            completed[index] = true;
            CurrentStage++;
            panelKey = null;
            StartRoute(destinations[index]);
            return true;
        }

        public void MarkExplored(CampsiteGuidanceStage stage)
        {
            if (stage == CurrentStage) FinishCurrentStage();
        }

        public void FinishRoasting()
        {
            if (CurrentStage == CampsiteGuidanceStage.Campfire) FinishCurrentStage();
        }

        private bool TryFinishFireworksVisit(Snapshot current)
        {
            if (CurrentStage != CampsiteGuidanceStage.Fireworks || Phase != CampsiteSequencePhase.Interacting
                || !begun[3] || !introduced[3] || current.launching || User == null || destinations[3] == null) return false;
            bool displayFinished = Session != null && current.launched > fireworkBaseline;
            bool walkedAway = Distance(User.position, destinations[3].position) > Mathf.Max(.1f, leaveRadius);
            if (!displayFinished && !walkedAway) return false;
            completed[3] = true;
            CurrentStage = CampsiteGuidanceStage.ReturnPortal;
            panelKey = null;
            StartRoute(destinations[3]);
            return true;
        }

        private void StartRoute(Transform from)
        {
            Phase = CampsiteSequencePhase.Travelling;
            routeOrigin = User != null ? User.position : from != null ? from.position : transform.position;
            Transform target = destinations[(int)CurrentStage];
            activeRoute = target == null ? Array.Empty<Transform>()
                : coordinator != null ? coordinator.RouteBetween(from, target) : new[] { target };
            if (CurrentStage == CampsiteGuidanceStage.Cigarette && !HasExitedTent)
                activeRoute = EntranceRoute(target);
            var path = new List<Vector3> { routeOrigin };
            foreach (var marker in activeRoute) if (marker != null) path.Add(marker.position);
            openedRoutes.Add(path.ToArray());
            if (guide != null) guide.GuideThrough(activeRoute);
            Panel("travel", string.Empty, Array.Empty<string>(), null);
        }

        private Transform[] EntranceRoute(Transform destination)
        {
            if (destination == null) return Array.Empty<Transform>();
            return tentExit != null ? new[] { tentExit, destination } : new[] { destination };
        }

        private void Play(string cue, Action done)
        {
            if (string.IsNullOrEmpty(cue) || narrator == null || narrator.library == null)
            { done?.Invoke(); return; }
            narrator.Play(cue, done);
        }

        private void RefreshInteractionPanel()
        {
            if (Phase != CampsiteSequencePhase.Interacting) return;
            if (!AtStation((int)CurrentStage))
            { Panel("away", string.Empty, Array.Empty<string>(), null); return; }
            if (CurrentStage == CampsiteGuidanceStage.ReturnPortal)
            {
                Panel("held-return", string.Empty, Array.Empty<string>(), null);
                return;
            }
            if (CurrentStage == CampsiteGuidanceStage.Cigarette)
            {
                Panel("interact-cigarette", string.Empty, Array.Empty<string>(), null);
                return;
            }
            if (CurrentStage == CampsiteGuidanceStage.WaterBucket)
            {
                RefreshWaterPanel();
                return;
            }
            if (CurrentStage == CampsiteGuidanceStage.Campfire)
            {
                RefreshRoastingPanel();
                return;
            }
            if (CurrentStage == CampsiteGuidanceStage.Fireworks)
                Panel("interact-fireworks", string.Empty, Array.Empty<string>(), null);
        }

        public bool KeepRoasting()
        {
            var item = CurrentMarshmallow;
            if (!CanActOnMarshmallow(item) || RoastingState(item) != RoastingPanelState.Ready) return false;
            keepRoastingServing = item.ServingRevision;
            panelKey = null;
            RefreshInteractionPanel();
            return true;
        }

        public bool TakeBite()
        {
            var item = CurrentMarshmallow;
            if (!CanActOnMarshmallow(item) || RoastingState(item) != RoastingPanelState.Ready) return false;
            int serving = item.ServingRevision;
            item.Use();
            if (!item.IsServingConsumed || item.ServingRevision == serving) return false;
            keepRoastingServing = -1;
            panelKey = null;
            ObserveRoastingReadiness();
            RefreshInteractionPanel();
            return true;
        }

        public bool RoastAnother()
        {
            var item = CurrentMarshmallow;
            if (!CanActOnMarshmallow(item)) return false;
            var state = RoastingState(item);
            if (state != RoastingPanelState.Ready && state != RoastingPanelState.Burnt
                && state != RoastingPanelState.Consumed) return false;
            ObserveRoastingReadiness();
            if (!item.TryStartNewServing()) return false;
            keepRoastingServing = -1;
            var player = bridge.setup.player;
            if (!item.IsHeld && player != null && player.isActiveAndEnabled && player.Held == null)
                player.Grab(item);
            panelKey = null;
            RefreshInteractionPanel();
            return true;
        }

        private void RefreshRoastingPanel()
        {
            var item = CurrentMarshmallow;
            if (item == null)
            {
                Panel("roasting-missing", string.Empty, Array.Empty<string>(), null);
                return;
            }
            var state = RoastingState(item);
            string text = string.Empty;
            RoastingAction[] actions;
            if (state == RoastingPanelState.Consumed)
            {
                text = ConsumedMarshmallowText(item);
                actions = RoastingReady ? new[] { RoastingAction.Fresh, RoastingAction.Finish } : new[] { RoastingAction.Fresh };
            }
            else if (!RoastingReady) actions = Array.Empty<RoastingAction>();
            else if (state == RoastingPanelState.Ready)
            {
                text = "It's ready. Would you like a bite?";
                actions = new[] { RoastingAction.Keep, RoastingAction.Bite, RoastingAction.Fresh, RoastingAction.Finish };
            }
            else if (state == RoastingPanelState.Burnt)
            {
                text = "It's burnt.";
                actions = new[] { RoastingAction.Fresh, RoastingAction.Finish };
            }
            else actions = new[] { RoastingAction.Finish };
            var labels = new string[actions.Length];
            for (int i = 0; i < actions.Length; i++) labels[i] = RoastingLabel(actions[i]);
            int version = revision;
            int serving = item.ServingRevision;
            bool ready = RoastingReady;
            Panel("roasting-" + serving + "-" + state + "-" + ready + "-" + text, text, labels, index =>
            {
                if (version != revision || !RoastingActive || !AtStation(1) || item != CurrentMarshmallow
                    || item == null || item.ServingRevision != serving || RoastingState(item) != state
                    || RoastingReady != ready || index < 0 || index >= actions.Length) return;
                switch (actions[index])
                {
                    case RoastingAction.Keep: KeepRoasting(); break;
                    case RoastingAction.Bite: TakeBite(); break;
                    case RoastingAction.Fresh: RoastAnother(); break;
                    case RoastingAction.Finish: FinishCurrentStage(); break;
                }
            });
        }

        private RoastingPanelState RoastingState(MarshmallowItem item)
        {
            if (item.IsServingConsumed) return RoastingPanelState.Consumed;
            if (!item.IsHeld) return RoastingPanelState.PutDown;
            if (Finite(item.cookedSeconds) && item.cookedSeconds >= item.burntSeconds) return RoastingPanelState.Burnt;
            if (Finite(item.cookedSeconds) && item.cookedSeconds >= item.goldenSeconds)
                return keepRoastingServing == item.ServingRevision ? RoastingPanelState.Keeping : RoastingPanelState.Ready;
            return RoastingPanelState.Raw;
        }

        private bool CanActOnMarshmallow(MarshmallowItem item) => RoastingActive && item != null && AtStation(1) && CanUseItem(item);

        private MarshmallowItem CurrentMarshmallow => bridge != null && bridge.setup != null ? bridge.setup.marshmallow : null;

        private string ConsumedMarshmallowText(MarshmallowItem item)
        {
            var line = bridge.director != null && bridge.director.library != null ? bridge.director.library.Find("campfire.eaten") : null;
            string text = line != null && !string.IsNullOrEmpty(line.text) ? line.text : "That was good. One more?";
            if (Finite(item.cookedSeconds) && item.cookedSeconds >= item.goldenSeconds && item.cookedSeconds < item.burntSeconds) return text;
            int suffix = text.IndexOf("One more?", StringComparison.Ordinal);
            return suffix >= 0 ? text.Substring(suffix) : "One more?";
        }

        private string RoastingLabel(RoastingAction action)
        {
            if (action == RoastingAction.Finish) return "Finish roasting";
            string id = action == RoastingAction.Fresh ? "campfire.eaten" : "campfire.roasting";
            string value = action == RoastingAction.Keep ? "marshmallow.roast" : action == RoastingAction.Bite ? "marshmallow.eat" : "marshmallow.pickup";
            var line = bridge.director != null && bridge.director.library != null ? bridge.director.library.Find(id) : null;
            if (line != null && line.choices != null)
                foreach (var choice in line.choices)
                    if (choice != null && string.Equals(choice.action, value, StringComparison.Ordinal)) return choice.label;
            return action == RoastingAction.Keep ? "Keep roasting" : action == RoastingAction.Bite ? "Take a bite" : "Roast another";
        }

        private void AcquireServingChoice()
        {
            var item = CurrentMarshmallow;
            if (ownsServingChoice && controlledMarshmallow == item) return;
            ReleaseServingChoice();
            if (item == null) return;
            controlledMarshmallow = item;
            previousRequireServingChoice = item.RequireServingChoice;
            ownsServingChoice = true;
            item.RequireServingChoice = true;
        }

        private void ReleaseServingChoice()
        {
            if (ownsServingChoice && controlledMarshmallow != null)
                controlledMarshmallow.RequireServingChoice = previousRequireServingChoice;
            ownsServingChoice = false;
            controlledMarshmallow = null;
        }

        private void RefreshWaterPanel()
        {
            var state = Session != null ? Session.state : null;
            var bucket = bridge != null && bridge.setup != null ? bridge.setup.bucket : null;
            string id = state != null && state.campfire == FireCondition.HotEmbers ? "bucket.embers"
                : bucket != null && bucket.IsHeld ? "bucket.held" : "bucket.ready";
            var library = bridge != null && bridge.director != null ? bridge.director.library : null;
            var line = library != null ? library.Find(id) : null;
            GuidanceChoice leave = null;
            if (line != null && line.choices != null)
                foreach (var choice in line.choices)
                    if (choice != null && string.Equals(choice.label, "Leave", StringComparison.Ordinal))
                    { leave = choice; break; }
            Panel("interact-water-" + id, line != null ? line.text : string.Empty,
                leave != null ? new[] { leave.label } : Array.Empty<string>(), FinishStageSelection());
        }

        private Action<int> FinishStageSelection()
        {
            CampsiteGuidanceStage stage = CurrentStage;
            int version = revision;
            return _ =>
            {
                if (version == revision && CurrentStage == stage) FinishCurrentStage();
            };
        }

        private void ResetRoastingReadiness()
        {
            RoastingReady = false;
            keepRoastingServing = -1;
            var state = Session != null ? Session.state : null;
            goldenEatenBaseline = state != null ? state.goldenMarshmallowsEaten : 0;
            burntEatenBaseline = state != null ? state.burntMarshmallowsEaten : 0;
        }

        private void ObserveRoastingReadiness()
        {
            if (!RoastingActive || RoastingReady || bridge == null || bridge.setup == null) return;
            var item = bridge.setup.marshmallow;
            if (item == null) return;
            bool goldenThreshold = Finite(item.goldenSeconds) && item.goldenSeconds > 0f;
            bool burntThreshold = Finite(item.burntSeconds) && item.burntSeconds > 0f;
            bool cooked = Finite(item.cookedSeconds) &&
                (goldenThreshold && item.cookedSeconds >= item.goldenSeconds
                    || burntThreshold && item.cookedSeconds >= item.burntSeconds);
            var state = Session != null ? Session.state : null;
            bool ateCooked = state != null &&
                (goldenThreshold && state.goldenMarshmallowsEaten > goldenEatenBaseline
                    || burntThreshold && state.burntMarshmallowsEaten > burntEatenBaseline);
            // Eating can replace the serving before this component's next update.
            RoastingReady = cooked || ateCooked;
        }

        private void Panel(string key, string text, string[] labels, Action<int> select)
        {
            if (view == null || panelKey == key && view.SequencePanelActive) return;
            panelKey = key;
            view.ShowSequencePanel(text, labels, select);
        }

        private bool ReadyAtCurrentStation()
        {
            if (CurrentStage == CampsiteGuidanceStage.Cigarette && !HasExitedTent) return false;
            Transform target = destinations[(int)CurrentStage];
            if (!AtStation((int)CurrentStage) || target == null || guide == null) return false;
            return Distance(guide.transform.position, target.position) <= Mathf.Max(.2f, guide.pointArrivalDistance * 2f);
        }

        private bool AtStation(int index)
        {
            if (User == null || destinations[index] == null) return false;
            if (Distance(User.position, destinations[index].position) <= Mathf.Max(.1f, visitRadius)) return true;
            if (index == 1 && begun[1] && bridge != null && bridge.setup != null && bridge.setup.marshmallowTable != null
                && Distance(User.position, bridge.setup.marshmallowTable.position) <= Mathf.Max(.1f, visitRadius)) return true;
            Transform bin = index == 0 && begun[0] ? Station("cigarette.bin") : null;
            return bin != null && Distance(User.position, bin.position) <= Mathf.Max(.1f, visitRadius);
        }

        public bool CanGrabItem(PrototypeItem item)
        {
            if (item == null) return false;
            if (elapsed < setupGraceSeconds && item is CigaretteItem) return true;
            var stage = ItemStage(item);
            return stage.HasValue && CanInteractWith(stage.Value)
                && User != null && Distance(User.position, item.transform.position) <= Mathf.Max(leaveRadius, 3f);
        }

        public bool CanUseItem(PrototypeItem item)
        {
            if (item == null) return false;
            var stage = ItemStage(item);
            if (!stage.HasValue || !CanInteractWith(stage.Value)) return false;
            if (item is WaterBucketItem && item.IsHeld) return true;
            return User != null && Distance(User.position, item.transform.position) <= Mathf.Max(leaveRadius, 3f);
        }

        public bool CanPressButton(PrototypeButton button)
        {
            if (button == null || bridge.setup == null) return false;
            return CanInteractWith(CampsiteGuidanceStage.Fireworks) && AtStation(3)
                && bridge.setup.fireworks != null && Distance(button.transform.position, bridge.setup.fireworks.transform.position) <= 4f;
        }

        public bool CanLaunch(FireworkStation station) => bridge.setup != null && station == bridge.setup.fireworks
            && CanInteractWith(CampsiteGuidanceStage.Fireworks) && AtStation(3);

        public bool CanInteractWith(CampsiteGuidanceStage stage)
        {
            int index = (int)stage;
            if (!IsInitialized || !begun[index] || Phase == CampsiteSequencePhase.Intro
                || Phase == CampsiteSequencePhase.Narrating) return false;
            if (index < (int)CurrentStage) return completed[index];
            return stage == CurrentStage && Phase == CampsiteSequencePhase.Interacting && AtStation(index);
        }

        private static CampsiteGuidanceStage? ItemStage(PrototypeItem item)
        {
            if (item is CigaretteItem) return CampsiteGuidanceStage.Cigarette;
            if (item is MarshmallowItem) return CampsiteGuidanceStage.Campfire;
            if (item is WaterBucketItem) return CampsiteGuidanceStage.WaterBucket;
            if (item is LighterItem) return CampsiteGuidanceStage.Fireworks;
            return null;
        }

        public bool CanMove(Vector3 from, Vector3 to)
        {
            if (!IsInitialized || User == null) return true;
            float distance = DistanceToOpenArea(to);
            if (distance <= movementRadius) return true;
            // Real room-scale motion can start outside a virtual route; allow a return toward it.
            return distance < DistanceToOpenArea(from) - .001f;
        }

        private float DistanceToOpenArea(Vector3 point)
        {
            float best = Distance(point, routeOrigin);
            Vector3 previousPoint = routeOrigin;
            foreach (Transform marker in activeRoute)
            {
                if (marker == null) continue;
                best = Mathf.Min(best, SegmentDistance(point, previousPoint, marker.position));
                previousPoint = marker.position;
            }
            foreach (var path in openedRoutes)
                for (int p = 1; p < path.Length; p++)
                    best = Mathf.Min(best, SegmentDistance(point, path[p - 1], path[p]));
            for (int i = 0; i < completed.Length; i++)
                if (completed[i] && destinations[i] != null) best = Mathf.Min(best, Distance(point, destinations[i].position));
            Transform bin = Station("cigarette.bin");
            if (begun[0] && bin != null && destinations[0] != null)
                best = Mathf.Min(best, SegmentDistance(point, destinations[0].position, bin.position));
            return best;
        }

        private static float SegmentDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = a.y = b.y = 0f;
            Vector3 edge = b - a;
            float t = edge.sqrMagnitude > .001f ? Mathf.Clamp01(Vector3.Dot(point - a, edge) / edge.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + edge * t);
        }

        private void BindPolicy()
        {
            PrototypeInteractionPolicy.GrabHandler = CanGrabItem;
            PrototypeInteractionPolicy.UseHandler = CanUseItem;
            PrototypeInteractionPolicy.PressHandler = CanPressButton;
            PrototypeInteractionPolicy.LaunchHandler = CanLaunch;
            PrototypeInteractionPolicy.MoveHandler = CanMove;
            PrototypeInteractionPolicy.ReturnHandler = ReturnAllowed;
        }

        private bool ReturnAllowed() => CanReturn;
        private void ClearPolicy()
        {
            if (PrototypeInteractionPolicy.ReturnHandler == ReturnAllowed) PrototypeInteractionPolicy.Reset();
        }

        private Transform Station(string prefix)
        {
            if (coordinator == null || coordinator.stations == null) return null;
            foreach (var station in coordinator.stations)
                if (station != null && station.target != null && station.dialoguePrefixes != null)
                    foreach (string value in station.dialoguePrefixes)
                        if (string.Equals(value, prefix, StringComparison.Ordinal)) return station.target;
            return null;
        }

        private Snapshot Capture()
        {
            var setup = bridge != null ? bridge.setup : null;
            var state = Session != null ? Session.state : null;
            return new Snapshot
            {
                cigaretteHeld = CigaretteHeld(),
                marshmallowHeld = setup != null && setup.marshmallow != null && setup.marshmallow.IsHeld,
                launching = setup != null && setup.fireworks != null && setup.fireworks.IsLaunching,
                cigarette = state != null ? state.cigarette : CigaretteCondition.Held,
                fire = state != null ? state.campfire : FireCondition.Burning,
                launched = state != null ? state.fireworksLaunched : 0
            };
        }

        private bool CigaretteHeld()
        {
            bool physical = false;
            foreach (var item in cigarettes)
            {
                if (item == null) continue;
                physical = true;
                if (item.IsHeld) return true;
            }
            if (bridge != null && bridge.setup != null && bridge.setup.cigarette != null)
                return bridge.setup.cigarette.IsHeld;
            return !physical && Session != null && Session.state.cigarette == CigaretteCondition.Held;
        }
        private PrototypeSession Session => bridge != null && bridge.session != null ? bridge.session : PrototypeSession.Instance;
        private static string Context(CampsiteGuidanceStage stage) => stage == CampsiteGuidanceStage.Campfire ? "campfire"
            : stage == CampsiteGuidanceStage.WaterBucket ? "bucket" : stage == CampsiteGuidanceStage.Fireworks ? "fireworks"
            : stage == CampsiteGuidanceStage.ReturnPortal ? "return" : "cigarette";
        private static float Distance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnEnable() { if (IsInitialized) { BindPolicy(); AcquireServingChoice(); } }
        private void OnDisable()
        {
            revision++;
            ClearPolicy();
            ReleaseServingChoice();
            narrator?.Stop();
            guide?.StopGuidance();
            if (view != null) view.HideSequencePanel();
        }
    }
}
