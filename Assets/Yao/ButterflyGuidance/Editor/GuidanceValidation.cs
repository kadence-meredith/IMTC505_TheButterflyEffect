using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Dependency-free editor checks against the actual runtime and dialogue graph.</summary>
    public static class GuidanceValidation
    {
        [MenuItem("Tools/Butterfly Effect/Validate Guidance Core")]
        public static void ValidateCore()
        {
            var host = new GameObject("Butterfly Guidance validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var director = host.AddComponent<GuidanceDirector>();
                director.Begin();
                ValidateLibrary(director.library);
                ValidateStateCombinations();
                ValidateStationLocations(director);
                ValidateSafePath(director);
                ValidateThreatenedPath(director);
                ValidateUnsafePath(director, false);
                ValidateUnsafePath(director, true);
                ValidatePreviews(director);
                Debug.Log("Butterfly Guidance validation passed: dialogue graph and original copy; "
                    + "18 hazard states; direct scene triggers; cigarette recovery; repeat interactions; "
                    + "two-stage water; four endings and causal debriefs; finish and replay reset.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void ValidateLibrary(GuidanceLibrary library)
        {
            Require(library != null && library.lines != null && library.lines.Length > 0, "Missing dialogue library.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var allCopy = new List<string>();
            var allLabels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in library.lines)
            {
                Require(line != null, "Null dialogue line.");
                Require(!string.IsNullOrWhiteSpace(line.id) && ids.Add(line.id), "Missing or duplicate line ID: " + line.id);
                Require(!string.IsNullOrWhiteSpace(line.text), "Missing copy: " + line.id);
                Require(!string.IsNullOrWhiteSpace(line.context), "Missing context: " + line.id);
                allCopy.Add(line.text);
                if (line.choices == null) continue;
                foreach (var choice in line.choices)
                {
                    Require(choice != null && !string.IsNullOrWhiteSpace(choice.label), "Missing choice label: " + line.id);
                    Require(!string.IsNullOrEmpty(choice.nextId) || !string.IsNullOrEmpty(choice.action), "Dead choice: " + line.id);
                    allLabels.Add(choice.label);
                }
            }
            foreach (var line in library.lines)
            {
                ValidateLink(ids, line.nextId, line.id);
                if (line.choices == null) continue;
                foreach (var choice in line.choices) ValidateLink(ids, choice.nextId, line.id);
                Require(line.choices.Length > 0 || !string.IsNullOrEmpty(line.nextId), "Dead-end line: " + line.id);
            }
            string copy = string.Join("\n", allCopy.ToArray());
            string[] requiredCopy =
            {
                "Small choices. Significant consequences.",
                "Stay close. I have something to show you.",
                "What happens beyond this courtyard can reach the places we call home.",
                "Touch the portal to enter the forest.",
                "Look around. There’s something waiting for you.", "Explore the campsite.",
                "Ready to head back?", "Come with me. Let’s see what followed us home.",
                "Back where we started. But something has changed.",
                "You’ll need a free hand for that marshmallow.", "Finally. Somewhere to put this.",
                "A warm fire. A bag of marshmallows. Stay a little longer.",
                "Almost golden. Just a little longer.", "That was good. One more?",
                "It’s Canada Day. Let’s light up the sky.", "Look up. The show’s starting.",
                "That was beautiful. Another one?", "A Place Protected", "The courtyard is full of life.",
                "You enjoyed the campsite and left no fire hazards behind.", "A Fragile Future",
                "The courtyard still stands. Smoke hangs in the air.", "Something you left behind is still burning.",
                "The Spark Came Home", "Fire has reached the courtyard.",
                "What you left in the forest has reached a place you call home.", "What We Leave Behind",
                "The courtyard you knew is almost unrecognizable.", "A moment of distraction. A place changed forever.",
                "What followed you home?", "The cigarette you left behind ignited the dry grass.",
                "Your marshmallow was finished. The fire wasn’t.",
                "After you left, sparks from the fire reached the dry grass.",
                "The flames were gone. The embers were still hot.", "They reignited after you left.",
                "You watched the colours in the sky. A spark landed in the dry grass below.",
                "You left no unresolved fire hazards at the campsite.",
                "The forest may feel far away. The places we care about are connected."
            };
            foreach (string expected in requiredCopy) Require(copy.Contains(expected), "Missing supplied copy: " + expected);
            string[] requiredLabels =
            {
                "Begin", "Return to campus", "Stay a little longer", "Keep holding it", "Drop it", "Pick it up",
                "Dispose of the cigarette", "Pick up a marshmallow", "Keep roasting", "Take a bite", "Roast another",
                "Leave", "Pick up", "Pour water", "Light the fireworks", "Leave them unlit", "Light another",
                "Try another path", "Finish"
            };
            foreach (string expected in requiredLabels) Require(allLabels.Contains(expected), "Missing supplied label: " + expected);
        }

        private static void ValidateLink(HashSet<string> ids, string target, string source)
        {
            Require(string.IsNullOrEmpty(target) || target == "debrief.next" || ids.Contains(target),
                "Broken link from " + source + " to " + target);
        }

        private static void ValidateStateCombinations()
        {
            var state = new GuidanceState();
            for (int cigarette = 0; cigarette < 3; cigarette++)
            for (int campfire = 0; campfire < 3; campfire++)
            for (int fireworks = 0; fireworks < 2; fireworks++)
            {
                state.Cigarette = (CigaretteStatus)cigarette;
                state.Campfire = (CampfireStatus)campfire;
                state.FireworksIgnited = fireworks == 1;
                int expected = (cigarette == (int)CigaretteStatus.Ground ? 1 : 0)
                    + (campfire == (int)CampfireStatus.Cold ? 0 : 1) + fireworks;
                Require(state.UnresolvedHazards == expected && (int)state.EvaluateEnding() == expected, "Hazard mapping mismatch.");
                string copy = string.Join("\n", state.DebriefLines());
                Require(copy.Contains("cigarette you left behind") == (state.Cigarette == CigaretteStatus.Ground), "Cigarette cause mismatch.");
                Require(copy.Contains("sparks from the fire") == (state.Campfire == CampfireStatus.Burning), "Campfire cause mismatch.");
                Require(copy.Contains("embers were still hot") == (state.Campfire == CampfireStatus.HotEmbers), "Ember cause mismatch.");
                Require(copy.Contains("spark landed") == state.FireworksIgnited, "Fireworks cause mismatch.");
                Require(copy.Contains("no unresolved fire hazards") == (expected == 0), "Safe debrief mismatch.");
            }
        }

        private static void ValidateStationLocations(GuidanceDirector director)
        {
            string[] stations = { "cigarette.hand", "campfire.approach", "bucket.ready", "fireworks.approach", "return.prompt" };
            foreach (string id in stations)
            {
                director.Show("campus.opening");
                director.Show(id);
                Require(director.Location == "forest", "Direct Show must enter forest for " + id);
            }
            director.Show("campus.reveal");
            Require(director.Location == "campus", "Campus reveal location mismatch.");
        }

        private static void EnterForest(GuidanceDirector director)
        {
            director.Restart();
            At(director, "campus.opening");
            Choose(director, "Begin");
            At(director, "campus.portal");
            director.Advance();
            Choose(director, "Touch the portal");
            At(director, "forest.arrival");
            director.Advance();
            At(director, "forest.hub");
            Require(director.Location == "forest", "Portal failed to enter forest.");
        }

        private static void DisposeCigarette(GuidanceDirector director)
        {
            director.Explore("bin");
            At(director, "cigarette.bin");
            Choose(director, "Dispose of the cigarette");
            Require(director.State.Cigarette == CigaretteStatus.Disposed, "Disposal failed.");
        }

        private static void FirstPour(GuidanceDirector director)
        {
            director.Explore("bucket");
            Choose(director, "Pick up");
            Choose(director, "Pour water");
            At(director, "bucket.embers");
            Require(director.State.Campfire == CampfireStatus.HotEmbers, "First pour must leave hot embers.");
        }

        private static void ValidateSafePath(GuidanceDirector director)
        {
            EnterForest(director);
            director.Explore("campfire");
            At(director, "cigarette.hand");
            Choose(director, "Keep holding it");
            At(director, "forest.hub");
            director.Explore("campfire");
            Choose(director, "Drop it");
            Require(director.State.Cigarette == CigaretteStatus.Ground, "Drop failed.");
            director.Explore("bin");
            At(director, "cigarette.bin-empty");
            Choose(director, "Go back to the cigarette");
            Choose(director, "Pick it up");
            Require(director.State.Cigarette == CigaretteStatus.Holding && director.State.UnresolvedHazards == 1,
                "Picking the cigarette back up must repair its hazard.");
            DisposeCigarette(director);
            director.Explore("campfire");
            Choose(director, "Pick up a marshmallow");
            Choose(director, "Keep roasting");
            Choose(director, "Take a bite");
            Choose(director, "Roast another");
            Choose(director, "Take a bite");
            Require(director.State.MarshmallowsEaten == 2, "Repeat marshmallow interaction failed.");
            FirstPour(director);
            Choose(director, "Stir and pour again");
            Require(director.State.Campfire == CampfireStatus.Cold, "Second pour must leave cold embers.");
            director.Explore("campfire");
            At(director, "campfire.cold");
            director.Explore("fireworks");
            Choose(director, "Leave them unlit");
            Require(!director.State.FireworksIgnited, "Unlit choice unexpectedly ignited fireworks.");
            director.Explore("return");
            Choose(director, "Stay a little longer");
            At(director, "forest.hub");
            ReturnAndDebrief(director, GuidanceEnding.Thriving, new[] { "debrief.thriving" });
            Choose(director, "Try another path");
            At(director, "campus.opening");
            Require(director.State.Cigarette == CigaretteStatus.Holding
                && director.State.Campfire == CampfireStatus.Burning
                && !director.State.FireworksIgnited && director.State.MarshmallowsEaten == 0
                && director.Location == "campus", "Replay did not reset state.");
        }

        private static void ValidateThreatenedPath(GuidanceDirector director)
        {
            EnterForest(director);
            DisposeCigarette(director);
            FirstPour(director);
            Choose(director, "Leave");
            ReturnAndDebrief(director, GuidanceEnding.Threatened, new[] { "debrief.embers" });
        }

        private static void ValidateUnsafePath(GuidanceDirector director, bool lightFireworks)
        {
            EnterForest(director);
            director.Explore("campfire");
            Choose(director, "Drop it");
            if (lightFireworks)
            {
                director.Explore("fireworks");
                Choose(director, "Light the fireworks");
                At(director, "fireworks.watch");
                director.Advance();
                Choose(director, "Light another");
                director.Advance();
                Choose(director, "Leave");
                Require(director.State.FireworksIgnited, "Repeat fireworks interaction failed.");
            }
            ReturnAndDebrief(director, lightFireworks ? GuidanceEnding.Devastated : GuidanceEnding.Burning,
                lightFireworks ? new[] { "debrief.cigarette", "debrief.campfire", "debrief.fireworks" }
                    : new[] { "debrief.cigarette", "debrief.campfire" });
            bool finishCalled = false;
            UnityEngine.Events.UnityAction finishListener = () => finishCalled = true;
            director.onFinished.AddListener(finishListener);
            try
            {
                Choose(director, "Finish");
                At(director, "session.finished");
                Require(finishCalled, "Finish integration event did not fire.");
            }
            finally { director.onFinished.RemoveListener(finishListener); }
        }

        private static void ReturnAndDebrief(GuidanceDirector director, GuidanceEnding ending, string[] causes)
        {
            int endingEvents = 0;
            Action<GuidanceEnding> endingListener = actual =>
            {
                Require(actual == ending, "Wrong ending event.");
                endingEvents++;
            };
            director.EndingReached += endingListener;
            try
            {
                director.Explore("return");
                Choose(director, "Return to campus");
                At(director, "return.butterfly");
                director.Advance();
                At(director, "campus.reveal");
                Require(director.Location == "campus", "Return did not reach campus.");
                Choose(director, "See what changed");
                At(director, "ending." + ending.ToString().ToLowerInvariant());
                Require(director.State.EvaluateEnding() == ending && endingEvents == 1, "Ending result/event mismatch.");
                director.Advance();
                At(director, "debrief.question");
                var actualCopy = new List<string>();
                foreach (string cause in causes)
                {
                    director.Advance();
                    At(director, cause);
                    actualCopy.Add(director.Current.text);
                }
                Require(string.Join("\n", actualCopy.ToArray()) == string.Join("\n", director.State.DebriefLines()),
                    "Dialogue debrief diverges from contributing state.");
                director.Advance();
                At(director, "debrief.close");
            }
            finally { director.EndingReached -= endingListener; }
        }

        private static void ValidatePreviews(GuidanceDirector director)
        {
            foreach (GuidanceEnding ending in Enum.GetValues(typeof(GuidanceEnding)))
            {
                director.PreviewEnding(ending);
                At(director, "ending." + ending.ToString().ToLowerInvariant());
                Require(director.State.EvaluateEnding() == ending, "Preview state does not match preview ending.");
            }
        }

        private static void Choose(GuidanceDirector director, string label)
        {
            Require(director.Current != null && director.Current.choices != null, "No choices for " + label);
            for (int i = 0; i < director.Current.choices.Length; i++)
            {
                if (director.Current.choices[i].label != label) continue;
                director.Choose(i);
                return;
            }
            throw new InvalidOperationException("Butterfly Guidance validation: missing choice '" + label + "' at " + director.Current.id);
        }

        private static void At(GuidanceDirector director, string id)
        {
            Require(director.Current != null && director.Current.id == id,
                "Expected " + id + ", found " + (director.Current == null ? "<none>" : director.Current.id));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Butterfly Guidance validation: " + message);
        }
    }
}
