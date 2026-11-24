using System;
using System.Collections.Generic;
using ButterflyEffect.Prototype;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButterflyEffect.Guidance
{
    public enum GuidanceSceneRole { CampusStart, Campsite, CampusEnding }

    /// <summary>
    /// Scene adapter for the authored story. PrototypeSession and the physical props
    /// remain authoritative; the standalone director state is only a read-only projection.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GuidanceSceneBridge : MonoBehaviour
    {
        public GuidanceDirector director;
        public GuidanceView view;
        public GuidanceRouteCoordinator coordinator;
        public CampsiteSetup setup;
        public PrototypeSession session;
        public GuidanceSceneRole role;
        public GuidanceEnding ending;
        public string initialDialogueId;
        public string startingSceneName = "0_CampusStart_Main";
        public GuidanceReturnPortal returnPortal;
        public Transform cigaretteGroundAnchor;
        public bool sequentialExploration = true;
        public GuidanceCampsiteSequence campsiteSequence;

        public bool UsesSequentialExploration => role == GuidanceSceneRole.Campsite && sequentialExploration;

        public bool WaitingForPhysicalAction { get; private set; }
        public string PendingPhysicalLine => queuedPhysicalLine ?? string.Empty;

        private PrototypeSession subscribedSession;
        private GuidanceDirector hookedDirector;
        private Func<GuidanceChoice, bool> previousChoiceHandler;
        private Func<bool> previousAdvanceHandler;
        private readonly List<string> debrief = new List<string>();
        private Snapshot previous;
        private bool initialized;
        private int initializationFrame;
        private float physicalWaitUntil;
        private string queuedPhysicalLine;
        private Transform trackedCigaretteAnchor;
        private float cigaretteGroundHeight;

        private struct Snapshot
        {
            public CigaretteCondition cigarette;
            public FireCondition campfire;
            public bool cigaretteHeld, marshmallowHeld, bucketHeld;
            public int eaten, launched, disposals, pickups, releases, actions;
        }

        private void OnEnable()
        {
            ResolveReferences();
            InstallHandlers();
            SubscribeSession();
        }

        private void Start() { if (!initialized) Initialize(); }

        public void Initialize()
        {
            ResolveReferences();
            InstallHandlers();
            SubscribeSession();
            SyncFromSession();
            UpdateCigaretteAnchor();
            previous = Capture();
            initialized = true;
            initializationFrame = Time.frameCount;
            queuedPhysicalLine = null;
            WaitingForPhysicalAction = false;
            if (UsesSequentialExploration)
            {
                if (campsiteSequence == null) campsiteSequence = GetComponent<GuidanceCampsiteSequence>();
                if (campsiteSequence == null) campsiteSequence = gameObject.AddComponent<GuidanceCampsiteSequence>();
                campsiteSequence.Initialize(this);
                return;
            }
            ShowLine(!string.IsNullOrEmpty(initialDialogueId) ? initialDialogueId
                : role == GuidanceSceneRole.CampusStart ? "campus.opening"
                : role == GuidanceSceneRole.Campsite ? "forest.arrival" : "campus.reveal");
        }

        private void LateUpdate()
        {
            if (!initialized || director == null) return;
            SubscribeSession();
            SyncFromSession();
            UpdateCigaretteAnchor();
            if (UsesSequentialExploration && campsiteSequence != null && campsiteSequence.IsInitialized)
            {
                // Exploration handles nearby props immediately, independently of the suggested route.
                previous = Capture();
                queuedPhysicalLine = null;
                return;
            }
            Snapshot current = Capture();
            // Starting item selections and FireTarget.Start are setup, not player choices.
            if (Time.frameCount > initializationFrame + 1)
            {
                string changedLine = ChangedLine(previous, current);
                if (!string.IsNullOrEmpty(changedLine)) queuedPhysicalLine = changedLine;
            }
            previous = current;
            if (coordinator != null && coordinator.IsTravelling) return;
            if (!string.IsNullOrEmpty(queuedPhysicalLine))
            {
                string line = queuedPhysicalLine;
                queuedPhysicalLine = null;
                if (RelevantToCurrentLine(line)) ShowLine(line);
            }
            if (WaitingForPhysicalAction && Time.unscaledTime >= physicalWaitUntil)
                CancelPhysicalWait();
        }

        private void ResolveReferences()
        {
            if (director == null) director = GetComponent<GuidanceDirector>();
            if (view == null) view = GetComponent<GuidanceView>();
            if (coordinator == null) coordinator = GetComponent<GuidanceRouteCoordinator>();
            if (setup == null && role == GuidanceSceneRole.Campsite)
                setup = FindFirstObjectByType<CampsiteSetup>();
            if (session == null) session = PrototypeSession.Instance;
        }

        private void InstallHandlers()
        {
            if (hookedDirector == director) return;
            RestoreHandlers();
            hookedDirector = director;
            if (hookedDirector == null) return;
            previousChoiceHandler = hookedDirector.ChoiceHandler;
            previousAdvanceHandler = hookedDirector.AdvanceHandler;
            hookedDirector.ChoiceHandler = HandleChoice;
            hookedDirector.AdvanceHandler = HandleAdvance;
        }

        private void RestoreHandlers()
        {
            if (hookedDirector != null)
            {
                if (hookedDirector.ChoiceHandler == HandleChoice)
                    hookedDirector.ChoiceHandler = previousChoiceHandler;
                if (hookedDirector.AdvanceHandler == HandleAdvance)
                    hookedDirector.AdvanceHandler = previousAdvanceHandler;
            }
            hookedDirector = null;
        }

        private void SubscribeSession()
        {
            if (session == null) session = PrototypeSession.Instance;
            if (subscribedSession == session) return;
            if (subscribedSession != null) subscribedSession.onStateChanged.RemoveListener(SyncFromSession);
            subscribedSession = session;
            if (subscribedSession != null) subscribedSession.onStateChanged.AddListener(SyncFromSession);
        }

        public void SyncFromSession()
        {
            if (director == null || session == null || session.state == null) return;
            ChoiceState state = session.state;
            director.State.Cigarette = state.cigarette == CigaretteCondition.Disposed ? CigaretteStatus.Disposed
                : state.cigarette == CigaretteCondition.Held ? CigaretteStatus.Holding : CigaretteStatus.Ground;
            director.State.Campfire = state.campfire == FireCondition.Cold ? CampfireStatus.Cold
                : state.campfire == FireCondition.HotEmbers ? CampfireStatus.HotEmbers : CampfireStatus.Burning;
            director.State.FireworksIgnited = state.fireworksEverLaunched;
            director.State.MarshmallowsEaten = state.marshmallowsEaten;
        }

        /// <summary>Fully handles real-scene choices before any standalone demo action runs.</summary>
        public bool HandleChoice(GuidanceChoice choice)
        {
            if (choice == null || director == null) return true;
            if (!UsesSequentialExploration && (WaitingForPhysicalAction
                || coordinator != null && coordinator.IsTravelling)) return true;
            SyncFromSession();
            string action = choice.action ?? string.Empty;
            if (action.StartsWith("explore.", StringComparison.Ordinal))
            {
                Explore(action.Substring("explore.".Length));
                return true;
            }
            switch (action)
            {
                case "portal.enter":
                    WaitForPhysicalAction(); // Collision with the real portal loads the next scene.
                    return true;
                case "cigarette.drop":
                    if (setup != null && setup.cigarette != null && setup.player != null && setup.player.isActiveAndEnabled
                        && setup.player.Held == setup.cigarette)
                    {
                        setup.player.DropHeld(false);
                        ShowLine(setup.cigarette.IsDisposed ? "cigarette.disposed" : "cigarette.ground");
                    }
                    else if (setup != null && setup.cigarette != null && !setup.cigarette.IsHeld)
                        ShowLine(setup.cigarette.IsDisposed ? "cigarette.disposed" : "cigarette.ground");
                    else WaitForPhysicalAction();
                    return true;
                case "cigarette.pickup":
                    if (setup == null || setup.cigarette == null) { WaitForPhysicalAction(); return true; }
                    if (setup.cigarette.IsDisposed) { ShowLine("cigarette.disposed"); return true; }
                    TryDesktopGrab(setup.cigarette);
                    if (setup.cigarette.IsHeld) ShowLine("cigarette.picked");
                    else WaitForPhysicalAction();
                    return true;
                case "cigarette.dispose":
                    if (setup == null || setup.cigarette == null) { WaitForPhysicalAction(); return true; }
                    if (setup.cigarette.IsDisposed) { ShowLine("cigarette.disposed"); return true; }
                    if (!setup.cigarette.IsHeld) { ShowLine("cigarette.bin-empty"); return true; }
                    // The real bin must contain the cigarette before the desktop release.
                    if (setup.disposalBin != null && setup.disposalBin.Contains(setup.cigarette.transform.position)
                        && setup.player != null && setup.player.isActiveAndEnabled && setup.player.Held == setup.cigarette)
                        setup.player.DropHeld(false);
                    if (setup.cigarette.IsDisposed) ShowLine("cigarette.disposed");
                    else WaitForPhysicalAction();
                    return true;
                case "marshmallow.pickup":
                    if (CigaretteHeld()) { ShowLine("cigarette.hand"); return true; }
                    if (CampfireCold()) { ShowLine("campfire.cold"); return true; }
                    if (setup != null && setup.marshmallow != null)
                    {
                        TryDesktopGrab(setup.marshmallow);
                        if (setup.marshmallow.IsHeld) { ShowLine("campfire.roasting"); return true; }
                    }
                    WaitForPhysicalAction();
                    return true;
                case "marshmallow.roast":
                    if (CigaretteHeld()) ShowLine("cigarette.hand");
                    else if (CampfireCold()) ShowLine("campfire.cold");
                    else if (setup != null && setup.marshmallow != null && setup.marshmallow.IsHeld)
                        ShowLine("campfire.roasting");
                    else ShowLine("campfire.approach");
                    return true;
                case "marshmallow.eat":
                    if (setup != null && setup.marshmallow != null && setup.marshmallow.IsHeld)
                    {
                        int before = session != null ? session.state.marshmallowsEaten : 0;
                        setup.marshmallow.Use();
                        if (session != null && session.state.marshmallowsEaten > before)
                        { ShowLine("campfire.eaten"); return true; }
                    }
                    WaitForPhysicalAction();
                    return true;
                case "marshmallow.finish":
                    if (!UsesSequentialExploration || campsiteSequence == null) return true;
                    if (setup != null && setup.player != null && setup.player.isActiveAndEnabled
                        && setup.marshmallow != null && setup.player.Held == setup.marshmallow)
                        setup.player.DropHeld(false);
                    campsiteSequence.FinishRoasting();
                    if (view != null) view.SetExplorationContext(false);
                    return true;
                case "bucket.pickup":
                    if (setup != null && setup.bucket != null)
                    {
                        TryDesktopGrab(setup.bucket);
                        if (setup.bucket.IsHeld) { ShowLine("bucket.held"); return true; }
                    }
                    WaitForPhysicalAction();
                    return true;
                case "bucket.pour": case "bucket.stir-pour":
                    if (CampfireCold()) ShowLine("bucket.cold");
                    else if (setup == null || setup.bucket == null || !setup.bucket.IsHeld) ShowLine("bucket.ready");
                    else WaitForPhysicalAction(); // WaterBucketItem's real tilt/stream extinguishes fires.
                    return true;
                case "fireworks.light":
                    if (setup != null && setup.fireworks != null && At(setup.fireworks.transform, 3.5f))
                    {
                        int before = session != null ? session.state.fireworksLaunched : 0;
                        setup.fireworks.Launch();
                        if (session != null && session.state.fireworksLaunched > before)
                        { ShowLine("fireworks.watch"); return true; }
                    }
                    WaitForPhysicalAction();
                    return true;
                case "ending.evaluate":
                    ShowLine("ending." + CurrentEnding().ToString().ToLowerInvariant());
                    return true;
                case "session.restart":
                    RestartRun();
                    return true;
                case "session.finish":
                    director.onFinished?.Invoke();
                    ShowLine("session.finished");
                    return true;
                default:
                    if (choice.nextId == "return.butterfly" && returnPortal != null) returnPortal.Arm();
                    Navigate(choice.nextId);
                    return true;
            }
        }

        public bool HandleAdvance()
        {
            if (UsesSequentialExploration) return true;
            if (director == null || WaitingForPhysicalAction
                || coordinator != null && coordinator.IsTravelling) return true;
            if (director.Current == null) { Initialize(); return true; }
            GuidanceLine line = director.Current;
            if (line.choices != null && line.choices.Length > 0)
            {
                if (line.choices.Length == 1) HandleChoice(line.choices[0]);
                return true;
            }
            if (line.id == "return.butterfly" || line.id == "portal.enter")
            { WaitForPhysicalAction(); return true; }
            Navigate(!string.IsNullOrEmpty(line.nextId) ? line.nextId
                : role == GuidanceSceneRole.Campsite ? "forest.hub" : "debrief.close");
            return true;
        }

        public void Explore(string station)
        {
            SyncFromSession();
            switch ((station ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "cigarette":
                    ShowLine(setup != null && setup.cigarette != null && setup.cigarette.IsDisposed ? "cigarette.disposed"
                        : CigaretteHeld() ? "cigarette.hand" : "cigarette.ground"); break;
                case "campfire": case "fire": case "marshmallow": case "marshmallows":
                    if (UsesSequentialExploration && setup != null && setup.marshmallow != null
                        && setup.marshmallow.IsHeld) { ShowRoastingStatus(); break; }
                    ShowLine("campfire.approach"); break;
                case "bucket": case "water":
                    if (UsesSequentialExploration)
                    {
                        ShowLine(setup != null && setup.bucket != null && setup.bucket.IsHeld
                            ? "bucket.held" : "bucket.ready");
                        break;
                    }
                    ShowLine(CampfireCold() ? "bucket.cold"
                        : session != null && session.state.campfire == FireCondition.HotEmbers ? "bucket.embers"
                        : setup != null && setup.bucket != null && setup.bucket.IsHeld ? "bucket.held" : "bucket.ready"); break;
                case "fireworks": ShowLine("fireworks.approach"); break;
                case "bin": case "disposal": ShowLine("cigarette.bin"); break;
                case "return": case "portal": ShowLine("return.prompt"); break;
                default: ShowLine("forest.hub"); break;
            }
        }

        private void Navigate(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (id == "debrief.next") { NextDebrief(); return; }
            if (id == "debrief.question") BuildDebrief();
            ShowLine(id);
        }

        private void ShowLine(string id)
        {
            if (director == null || string.IsNullOrEmpty(id)) return;
            WaitingForPhysicalAction = false;
            if (view != null && (coordinator == null || !coordinator.IsTravelling)) view.ResumeDialogue();
            SyncFromSession();
            if (UsesSequentialExploration && director.library != null
                && (id == "campfire.roasting" || id == "campfire.eaten"
                    || id == "campfire.cold" && campsiteSequence != null && campsiteSequence.RoastingActive))
            {
                GuidanceLine authored = director.library.Find(id);
                if (authored != null)
                {
                    var choices = new List<GuidanceChoice>();
                    foreach (var choice in authored.choices ?? Array.Empty<GuidanceChoice>())
                        if (choice != null && !string.IsNullOrEmpty(choice.action)) choices.Add(choice);
                    choices.Add(new GuidanceChoice { label = "Finish roasting", action = "marshmallow.finish" });
                    director.ShowRuntimeLine(new GuidanceLine
                    {
                        id = authored.id, context = authored.context, speaker = authored.speaker,
                        text = authored.text, choices = choices.ToArray()
                    });
                    return;
                }
            }
            director.Show(id);
        }

        public void ShowRoastingStatus(bool afterEating = false)
        {
            ShowLine(afterEating ? "campfire.eaten" : CampfireCold() ? "campfire.cold" : "campfire.roasting");
        }

        private void WaitForPhysicalAction()
        {
            if (UsesSequentialExploration)
            {
                WaitingForPhysicalAction = false;
                return;
            }
            WaitingForPhysicalAction = true;
            physicalWaitUntil = Time.unscaledTime + 8f;
            // Reuse the authored line. No controls, status labels, or added instructions.
            if (view != null && director != null && director.Current != null)
                view.ShowTravelPrompt(director.Current.text);
        }

        public void CancelPhysicalWait()
        {
            WaitingForPhysicalAction = false;
            if (view != null && (coordinator == null || !coordinator.IsTravelling)) view.ResumeDialogue();
        }

        private void UpdateCigaretteAnchor()
        {
            if (cigaretteGroundAnchor == null) return;
            if (trackedCigaretteAnchor != cigaretteGroundAnchor)
            {
                trackedCigaretteAnchor = cigaretteGroundAnchor;
                cigaretteGroundHeight = cigaretteGroundAnchor.position.y;
            }
            CigaretteItem item = setup != null ? setup.cigarette : null;
            if (item == null || item.IsHeld || item.IsDisposed || session == null
                || session.state.cigarette != CigaretteCondition.Dropped) return;
            if (item.Body != null && item.Body.linearVelocity.sqrMagnitude >= .25f) return;
            // This is an independent ground marker, never parented to or chasing a held prop.
            Vector3 position = item.transform.position;
            position.y = cigaretteGroundHeight;
            cigaretteGroundAnchor.position = position;
        }

        private void TryDesktopGrab(PrototypeItem item)
        {
            if (setup == null || setup.player == null || !setup.player.isActiveAndEnabled || item == null) return;
            if (setup.player.Held == null && At(item.transform, setup.player.reach)) setup.player.Grab(item);
        }

        private bool At(Transform target, float distance)
        {
            Transform user = coordinator != null && coordinator.guide != null ? coordinator.guide.user : null;
            if (user == null && setup != null && setup.player != null && setup.player.isActiveAndEnabled)
                user = setup.player.transform;
            if (target == null || user == null) return false;
            Vector3 delta = target.position - user.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= distance * distance;
        }

        private bool CigaretteHeld()
        {
            return setup != null && setup.cigarette != null ? setup.cigarette.IsHeld
                : session != null && session.state.cigarette == CigaretteCondition.Held;
        }

        private bool CampfireCold()
        {
            return setup != null && setup.campfire != null ? setup.campfire.progress.condition == FireCondition.Cold
                : session != null && session.state.campfire == FireCondition.Cold;
        }

        private Snapshot Capture()
        {
            ChoiceState state = session != null ? session.state : null;
            return new Snapshot
            {
                cigarette = state != null ? state.cigarette : CigaretteCondition.Held,
                campfire = state != null ? state.campfire : FireCondition.Burning,
                cigaretteHeld = setup != null && setup.cigarette != null && setup.cigarette.IsHeld,
                marshmallowHeld = setup != null && setup.marshmallow != null && setup.marshmallow.IsHeld,
                bucketHeld = setup != null && setup.bucket != null && setup.bucket.IsHeld,
                eaten = state != null ? state.marshmallowsEaten : 0,
                launched = state != null ? state.fireworksLaunched : 0,
                disposals = state != null ? state.cigaretteDisposals : 0,
                pickups = state != null ? state.itemsPickedUp : 0,
                releases = state != null ? state.itemsReleased : 0,
                actions = state != null ? state.totalActions : 0
            };
        }

        private string ChangedLine(Snapshot before, Snapshot after)
        {
            if (role != GuidanceSceneRole.Campsite) return null;
            if (after.actions < before.actions) return "forest.arrival";
            if (after.disposals > before.disposals || after.cigarette != before.cigarette && after.cigarette == CigaretteCondition.Disposed)
                return "cigarette.disposed";
            if (after.eaten > before.eaten) return "campfire.eaten";
            if (after.launched > before.launched) return "fireworks.watch";
            if (after.campfire != before.campfire)
            {
                if (after.campfire == FireCondition.Cold) return "bucket.cold";
                if (after.campfire == FireCondition.HotEmbers) return "bucket.embers";
            }
            if (after.releases > before.releases && before.cigaretteHeld && !after.cigaretteHeld)
                return after.cigarette == CigaretteCondition.Disposed ? "cigarette.disposed" : "cigarette.ground";
            if (after.pickups > before.pickups)
            {
                if (!before.cigaretteHeld && after.cigaretteHeld) return "cigarette.picked";
                if (!before.marshmallowHeld && after.marshmallowHeld)
                    return after.campfire == FireCondition.Cold ? "campfire.cold" : "campfire.roasting";
                if (!before.bucketHeld && after.bucketHeld) return "bucket.held";
            }
            return null;
        }

        private bool RelevantToCurrentLine(string line)
        {
            string current = director.Current != null ? director.Current.id : string.Empty;
            if (current == "forest.hub" || line == "forest.arrival") return true;
            if (line.StartsWith("cigarette.", StringComparison.Ordinal)) return current.StartsWith("cigarette.", StringComparison.Ordinal);
            if (line.StartsWith("campfire.", StringComparison.Ordinal)) return current.StartsWith("campfire.", StringComparison.Ordinal);
            if (line.StartsWith("bucket.", StringComparison.Ordinal)) return current.StartsWith("bucket.", StringComparison.Ordinal)
                || current.StartsWith("campfire.", StringComparison.Ordinal);
            if (line.StartsWith("fireworks.", StringComparison.Ordinal)) return current.StartsWith("fireworks.", StringComparison.Ordinal);
            return false;
        }

        private GuidanceEnding CurrentEnding()
        {
            return role == GuidanceSceneRole.CampusEnding || session == null ? ending
                : (GuidanceEnding)session.state.PreviewEnding(session.endingRule);
        }

        private void BuildDebrief()
        {
            debrief.Clear();
            if (session != null && session.state != null)
            {
                ChoiceState state = session.state;
                if (state.cigaretteDroppedOutsideBin || state.cigarette != CigaretteCondition.Disposed || state.cigaretteGrassBurning)
                    debrief.Add("debrief.cigarette");
                if (state.campfire == FireCondition.Burning) debrief.Add("debrief.campfire");
                else if (state.campfire == FireCondition.HotEmbers) debrief.Add("debrief.embers");
                if (state.fireworksEverLaunched) debrief.Add("debrief.fireworks");
            }
            if (debrief.Count == 0 && CurrentEnding() == GuidanceEnding.Thriving) debrief.Add("debrief.thriving");
            debrief.Add("debrief.close");
        }

        private void NextDebrief()
        {
            if (debrief.Count == 0) BuildDebrief();
            string current = director.Current != null ? director.Current.id : string.Empty;
            int next = debrief.IndexOf(current) + 1;
            ShowLine(next < debrief.Count ? debrief[next] : "debrief.close");
        }

        private void RestartRun()
        {
            if (!string.IsNullOrEmpty(startingSceneName) && Application.CanStreamedLevelBeLoaded(startingSceneName))
            { SceneManager.LoadScene(startingSceneName); return; }
            if (role == GuidanceSceneRole.Campsite && session != null)
            {
                session.ResetRun();
                previous = Capture();
                ShowLine("forest.arrival");
            }
        }

        private void OnDisable()
        {
            RestoreHandlers();
            if (subscribedSession != null) subscribedSession.onStateChanged.RemoveListener(SyncFromSession);
            subscribedSession = null;
            CancelPhysicalWait();
        }
    }
}
