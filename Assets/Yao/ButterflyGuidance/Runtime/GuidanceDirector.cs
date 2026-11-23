using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ButterflyEffect.Guidance
{
    [Serializable]
    public class GuidanceStringEvent : UnityEvent<string> { }

    [Serializable]
    public class GuidanceEndingEvent : UnityEvent<GuidanceEnding> { }

    /// <summary>
    /// UI-independent dialogue flow. Scene interactions can call Explore, Show, or Choose;
    /// rendering and external effects subscribe to events. No editor APIs are required.
    /// </summary>
    public class GuidanceDirector : MonoBehaviour
    {
        public GuidanceLibrary library;

        public GuidanceState State { get; private set; } = new GuidanceState();
        public GuidanceLine Current { get; private set; }
        public string Location { get; private set; } = "campus";

        public event Action<GuidanceLine> LineChanged;
        public event Action<string> LocationChanged;
        public event Action<GuidanceEnding> EndingReached;
        public event Action<string> ActionInvoked;

        // Scene adapters can handle real interactions before the standalone demo flow.
        public Func<GuidanceChoice, bool> ChoiceHandler { get; set; }
        public Func<bool> AdvanceHandler { get; set; }

        [Header("Optional scene integration")]
        public GuidanceStringEvent onLineShown = new GuidanceStringEvent();
        public GuidanceStringEvent onLocationChanged = new GuidanceStringEvent();
        public GuidanceStringEvent onActionInvoked = new GuidanceStringEvent();
        public GuidanceEndingEvent onEndingReached = new GuidanceEndingEvent();
        public UnityEvent onFinished = new UnityEvent();

        private bool bucketHeld;
        private bool ownsLibrary;
        private readonly List<string> debriefIds = new List<string>();

        public void Begin()
        {
            EnsureLibrary();
            Show("campus.opening");
        }

        public void Show(string id)
        {
            EnsureLibrary();
            if (string.IsNullOrEmpty(id)) return;
            if (id == "debrief.next") { ShowNextDebrief(); return; }

            // Guard public entry points too, so scene triggers cannot bypass the hand rule.
            if (id == "campfire.approach" || id == "campfire.roasting")
            {
                if (State.Cigarette == CigaretteStatus.Holding) id = "cigarette.hand";
                else if (State.Campfire == CampfireStatus.Cold) id = "campfire.cold";
            }
            if (id == "cigarette.bin")
            {
                if (State.Cigarette == CigaretteStatus.Ground) id = "cigarette.bin-empty";
                else if (State.Cigarette == CigaretteStatus.Disposed) id = "cigarette.disposed";
            }

            GuidanceLine line = library.Find(id);
            if (line == null)
            {
                Debug.LogWarning("Butterfly Guidance: unknown dialogue ID '" + id + "'.", this);
                return;
            }

            if (id.StartsWith("forest.", StringComparison.Ordinal)
                || id.StartsWith("cigarette.", StringComparison.Ordinal)
                || id.StartsWith("campfire.", StringComparison.Ordinal)
                || id.StartsWith("bucket.", StringComparison.Ordinal)
                || id.StartsWith("fireworks.", StringComparison.Ordinal)
                || id.StartsWith("return.", StringComparison.Ordinal)) SetLocation("forest");
            else if (id.StartsWith("campus.", StringComparison.Ordinal)
                || id.StartsWith("ending.", StringComparison.Ordinal)
                || id.StartsWith("debrief.", StringComparison.Ordinal)) SetLocation("campus");

            Current = line;
            if (id == "debrief.question") BuildDebrief();
            LineChanged?.Invoke(Current);
            onLineShown?.Invoke(id);

            GuidanceEnding ending;
            if (TryGetEnding(id, out ending))
            {
                EndingReached?.Invoke(ending);
                onEndingReached?.Invoke(ending);
            }
        }

        public void Choose(int index)
        {
            if (Current == null || Current.choices == null || index < 0 || index >= Current.choices.Length)
                return;
            GuidanceChoice choice = Current.choices[index];
            if (choice == null) return;
            if (ChoiceHandler != null && ChoiceHandler(choice)) return;
            string next = choice.nextId;
            if (!string.IsNullOrEmpty(choice.action) && InvokeAction(choice.action)) return;
            if (!string.IsNullOrEmpty(next)) Show(next);
        }

        public void Advance()
        {
            if (AdvanceHandler != null && AdvanceHandler()) return;
            if (Current == null) { Begin(); return; }
            if (Current.choices != null && Current.choices.Length > 0)
            {
                // A sole continuation is safe; multiple-choice prompts require an explicit choice.
                if (Current.choices.Length == 1) Choose(0);
                return;
            }
            if (!string.IsNullOrEmpty(Current.nextId)) Show(Current.nextId);
            else Show(Location == "forest" ? "forest.hub" : "debrief.close");
        }

        public void Restart()
        {
            State.Reset();
            bucketHeld = false;
            debriefIds.Clear();
            SetLocation("campus");
            Show("campus.opening");
        }

        public void Explore(string station)
        {
            if (string.IsNullOrEmpty(station)) { Show("forest.hub"); return; }
            SetLocation("forest");
            switch (station.Trim().ToLowerInvariant())
            {
                case "cigarette":
                    Show(State.Cigarette == CigaretteStatus.Holding ? "cigarette.hand"
                        : State.Cigarette == CigaretteStatus.Ground ? "cigarette.ground" : "cigarette.disposed");
                    break;
                case "campfire": case "fire": case "marshmallow": case "marshmallows":
                    Show("campfire.approach"); break;
                case "bucket": case "water":
                    Show(State.Campfire == CampfireStatus.Cold ? "bucket.cold"
                        : State.Campfire == CampfireStatus.HotEmbers ? "bucket.embers"
                        : bucketHeld ? "bucket.held" : "bucket.ready");
                    break;
                case "fireworks": Show("fireworks.approach"); break;
                case "bin": case "disposal": Show("cigarette.bin"); break;
                case "return": case "portal": Show("return.prompt"); break;
                default: Show("forest.hub"); break;
            }
        }

        /// <summary>Authoring/demo shortcut: seeds a representative state for any ending.</summary>
        public void PreviewEnding(GuidanceEnding ending)
        {
            State.Reset();
            State.Cigarette = ending == GuidanceEnding.Burning || ending == GuidanceEnding.Devastated
                ? CigaretteStatus.Ground : CigaretteStatus.Disposed;
            State.Campfire = ending == GuidanceEnding.Thriving ? CampfireStatus.Cold
                : ending == GuidanceEnding.Threatened ? CampfireStatus.HotEmbers : CampfireStatus.Burning;
            State.FireworksIgnited = ending == GuidanceEnding.Devastated;
            Show(EndingId(ending));
        }

        private bool InvokeAction(string action)
        {
            bool navigated = false;
            switch (action)
            {
                case "portal.enter": SetLocation("forest"); break;
                case "cigarette.drop": State.Cigarette = CigaretteStatus.Ground; break;
                case "cigarette.pickup":
                    if (State.Cigarette == CigaretteStatus.Ground) State.Cigarette = CigaretteStatus.Holding;
                    break;
                case "cigarette.dispose":
                    if (State.Cigarette == CigaretteStatus.Holding) State.Cigarette = CigaretteStatus.Disposed;
                    else { Show("cigarette.bin"); navigated = true; }
                    break;
                case "marshmallow.pickup": case "marshmallow.roast":
                    if (State.Cigarette == CigaretteStatus.Holding) { Show("cigarette.hand"); navigated = true; }
                    else if (State.Campfire == CampfireStatus.Cold) { Show("campfire.cold"); navigated = true; }
                    break;
                case "marshmallow.eat": State.MarshmallowsEaten++; break;
                case "bucket.pickup": bucketHeld = true; break;
                case "bucket.pour": case "bucket.stir-pour":
                    bucketHeld = true;
                    if (State.Campfire == CampfireStatus.Burning) State.Campfire = CampfireStatus.HotEmbers;
                    else State.Campfire = CampfireStatus.Cold;
                    Show(State.Campfire == CampfireStatus.Cold ? "bucket.cold" : "bucket.embers");
                    navigated = true;
                    break;
                case "fireworks.light": State.FireworksIgnited = true; break;
                case "ending.evaluate": Show(EndingId(State.EvaluateEnding())); navigated = true; break;
                case "session.restart": Restart(); navigated = true; break;
                case "session.finish": onFinished?.Invoke(); break;
                default:
                    if (action.StartsWith("explore.", StringComparison.Ordinal))
                    {
                        Explore(action.Substring("explore.".Length));
                        navigated = true;
                    }
                    break;
            }
            ActionInvoked?.Invoke(action);
            onActionInvoked?.Invoke(action);
            return navigated;
        }

        private void BuildDebrief()
        {
            debriefIds.Clear();
            if (State.Cigarette == CigaretteStatus.Ground) debriefIds.Add("debrief.cigarette");
            if (State.Campfire == CampfireStatus.Burning) debriefIds.Add("debrief.campfire");
            else if (State.Campfire == CampfireStatus.HotEmbers) debriefIds.Add("debrief.embers");
            if (State.FireworksIgnited) debriefIds.Add("debrief.fireworks");
            if (debriefIds.Count == 0) debriefIds.Add("debrief.thriving");
            debriefIds.Add("debrief.close");
        }

        private void ShowNextDebrief()
        {
            if (debriefIds.Count == 0) BuildDebrief();
            int currentIndex = Current == null ? -1 : debriefIds.IndexOf(Current.id);
            int nextIndex = currentIndex + 1;
            Show(nextIndex < debriefIds.Count ? debriefIds[nextIndex] : "debrief.close");
        }

        private void SetLocation(string location)
        {
            if (Location == location) return;
            Location = location;
            LocationChanged?.Invoke(location);
            onLocationChanged?.Invoke(location);
        }

        private void EnsureLibrary()
        {
            if (library != null) return;
            library = GuidanceLibrary.CreateDefault();
            ownsLibrary = true;
        }

        private void OnDestroy()
        {
            if (!ownsLibrary || library == null) return;
            if (Application.isPlaying) Destroy(library);
            else DestroyImmediate(library);
        }

        private static string EndingId(GuidanceEnding ending)
        {
            return "ending." + ending.ToString().ToLowerInvariant();
        }

        private static bool TryGetEnding(string id, out GuidanceEnding ending)
        {
            switch (id)
            {
                case "ending.thriving": ending = GuidanceEnding.Thriving; return true;
                case "ending.threatened": ending = GuidanceEnding.Threatened; return true;
                case "ending.burning": ending = GuidanceEnding.Burning; return true;
                case "ending.devastated": ending = GuidanceEnding.Devastated; return true;
                default: ending = GuidanceEnding.Thriving; return false;
            }
        }
    }
}
