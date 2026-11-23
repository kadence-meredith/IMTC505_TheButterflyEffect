using System;
using UnityEngine;

namespace ButterflyEffect.Guidance
{
    [Serializable]
    public class GuidanceChoice
    {
        public string label;
        public string nextId;
        public string action;
    }

    [Serializable]
    public class GuidanceLine
    {
        public string id;
        public string context;
        public string speaker;
        [TextArea(2, 7)] public string text;
        public GuidanceChoice[] choices;
        public string nextId;
    }

    [CreateAssetMenu(fileName = "ButterflyGuidanceLibrary", menuName = "Butterfly Effect/Guidance Library")]
    public class GuidanceLibrary : ScriptableObject
    {
        public GuidanceLine[] lines;

        public GuidanceLine Find(string id)
        {
            if (lines == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != null && string.Equals(lines[i].id, id, StringComparison.Ordinal))
                    return lines[i];
            return null;
        }

        private static GuidanceChoice C(string label, string nextId = null, string action = null)
        {
            return new GuidanceChoice { label = label, nextId = nextId, action = action };
        }

        private static GuidanceLine L(string id, string context, string text,
            string nextId = null, params GuidanceChoice[] choices)
        {
            return new GuidanceLine
            {
                id = id, context = context, speaker = "Butterfly", text = text,
                nextId = nextId, choices = choices ?? new GuidanceChoice[0]
            };
        }

        /// <summary>
        /// Ready-to-run copy library. Supplied narrative is preserved verbatim;
        /// added connective lines and labels make the standalone prototype navigable.
        /// </summary>
        public static GuidanceLibrary CreateDefault()
        {
            var result = CreateInstance<GuidanceLibrary>();
            result.name = "Butterfly Guidance — Default English";
            result.lines = new[]
            {
                L("campus.opening", "Campus — butterfly guidance",
                    "Stay close. I have something to show you.", null,
                    C("Begin", "campus.portal")),
                L("campus.butterfly", "Campus — butterfly appears",
                    "Stay close. I have something to show you.", "campus.portal"),
                L("campus.portal", "Campus — approaching the portal",
                    "What happens beyond this courtyard can reach the places we call home.", "portal.enter"),
                L("portal.enter", "Portal — interaction prompt",
                    "Touch the portal to enter the forest.", null,
                    C("Touch the portal", "forest.arrival", "portal.enter")),
                L("forest.arrival", "Forest — arrival",
                    "Look around. There’s something waiting for you.", "forest.hub"),
                L("forest.hub", "Forest — exploration objective", "Explore the campsite.", null,
                    C("The cigarette", null, "explore.cigarette"),
                    C("Campfire & marshmallows", null, "explore.campfire"),
                    C("Water bucket", null, "explore.bucket"),
                    C("Canada Day fireworks", null, "explore.fireworks"),
                    C("Look for the disposal bin", null, "explore.bin"),
                    C("Return portal", "return.prompt")),

                L("cigarette.hand", "Cigarette — in hand",
                    "You’ll need a free hand for that marshmallow.", null,
                    C("Keep holding it", "forest.hub"),
                    C("Drop it", "cigarette.ground", "cigarette.drop")),
                L("cigarette.ground", "Cigarette — on the ground",
                    "The cigarette is on the ground. You can still pick it up.", null,
                    C("Pick it up", "cigarette.picked", "cigarette.pickup"),
                    C("Visit the campfire", null, "explore.campfire"),
                    C("Explore the campsite", "forest.hub")),
                L("cigarette.picked", "Cigarette — picked up",
                    "The cigarette is back in your hand.", null,
                    C("Look for the disposal bin", null, "explore.bin"),
                    C("Explore the campsite", "forest.hub")),
                L("cigarette.bin", "Cigarette disposal bin — found",
                    "Finally. Somewhere to put this.", null,
                    C("Dispose of the cigarette", "cigarette.disposed", "cigarette.dispose"),
                    C("Leave", "forest.hub")),
                L("cigarette.bin-empty", "Cigarette disposal bin",
                    "The cigarette is still on the ground. Pick it up before using the bin.", null,
                    C("Go back to the cigarette", "cigarette.ground"),
                    C("Leave", "forest.hub")),
                L("cigarette.disposed", "Cigarette — disposed",
                    "The cigarette is safely in the disposal bin. Your hand is free.", null,
                    C("Visit the campfire", null, "explore.campfire"),
                    C("Explore the campsite", "forest.hub")),

                L("campfire.approach", "Campfire — approaching",
                    "A warm fire. A bag of marshmallows. Stay a little longer.", null,
                    C("Pick up a marshmallow", "campfire.roasting", "marshmallow.pickup"),
                    C("Check the water bucket", null, "explore.bucket"),
                    C("Leave", "forest.hub")),
                L("campfire.roasting", "Marshmallow — roasting",
                    "Almost golden. Just a little longer.", null,
                    C("Keep roasting", "campfire.roasting", "marshmallow.roast"),
                    C("Take a bite", "campfire.eaten", "marshmallow.eat"),
                    C("Leave", "forest.hub")),
                L("campfire.eaten", "Marshmallow — after eating",
                    "That was good. One more?", null,
                    C("Roast another", "campfire.roasting", "marshmallow.pickup"),
                    C("Leave", "forest.hub")),
                L("campfire.cold", "Campfire — cold",
                    "The campfire is cold. No fire hazard remains here.", null,
                    C("Explore the campsite", "forest.hub")),

                L("bucket.ready", "Water bucket", "A water bucket sits beside the campfire.", null,
                    C("Pick up", "bucket.held", "bucket.pickup"),
                    C("Leave", "forest.hub")),
                L("bucket.held", "Water bucket — held", "The bucket is ready.", null,
                    C("Pour water", null, "bucket.pour"),
                    C("Leave", "forest.hub")),
                L("bucket.embers", "Campfire — hot embers",
                    "The flames were gone. The embers were still hot.", null,
                    C("Stir and pour again", null, "bucket.stir-pour"),
                    C("Leave", "forest.hub")),
                L("bucket.cold", "Campfire — cold", "The embers are cold. This fire is out.", null,
                    C("Explore the campsite", "forest.hub")),

                L("fireworks.approach", "Canada Day — approaching the fireworks",
                    "It’s Canada Day. Let’s light up the sky.", null,
                    C("Light the fireworks", "fireworks.watch", "fireworks.light"),
                    C("Leave them unlit", "forest.hub")),
                L("fireworks.watch", "Canada Day — watching the fireworks",
                    "Look up. The show’s starting.", "fireworks.again"),
                L("fireworks.again", "Canada Day — after the display",
                    "That was beautiful. Another one?", null,
                    C("Light another", "fireworks.watch", "fireworks.light"),
                    C("Leave", "forest.hub")),

                L("return.prompt", "Return portal", "Ready to head back?", null,
                    C("Return to campus", "return.butterfly"),
                    C("Stay a little longer", "forest.hub")),
                L("return.butterfly", "Return portal — butterfly",
                    "Come with me. Let’s see what followed us home.", "campus.reveal"),
                L("campus.reveal", "Campus — reveal",
                    "Back where we started. But something has changed.", null,
                    C("See what changed", null, "ending.evaluate")),

                L("ending.thriving", "Thriving",
                    "A Place Protected\nThe courtyard is full of life.\nYou enjoyed the campsite and left no fire hazards behind.", "debrief.question"),
                L("ending.threatened", "Threatened",
                    "A Fragile Future\nThe courtyard still stands. Smoke hangs in the air.\nSomething you left behind is still burning.", "debrief.question"),
                L("ending.burning", "Burning",
                    "The Spark Came Home\nFire has reached the courtyard.\nWhat you left in the forest has reached a place you call home.", "debrief.question"),
                L("ending.devastated", "Devastated",
                    "What We Leave Behind\nThe courtyard you knew is almost unrecognizable.\nA moment of distraction. A place changed forever.", "debrief.question"),

                L("debrief.question", "Ending debrief", "What followed you home?", "debrief.next"),
                L("debrief.cigarette", "Cigarette left behind",
                    "The cigarette you left behind ignited the dry grass.", "debrief.next"),
                L("debrief.campfire", "Campfire left burning",
                    "Your marshmallow was finished. The fire wasn’t.\nAfter you left, sparks from the fire reached the dry grass.", "debrief.next"),
                L("debrief.embers", "Hot embers left behind",
                    "The flames were gone. The embers were still hot.\nThey reignited after you left.", "debrief.next"),
                L("debrief.fireworks", "Fireworks ignited the grass",
                    "You watched the colours in the sky. A spark landed in the dry grass below.", "debrief.next"),
                L("debrief.thriving", "A place protected",
                    "You left no unresolved fire hazards at the campsite.", "debrief.next"),
                L("debrief.close", "Shared closing line",
                    "The forest may feel far away. The places we care about are connected.", null,
                    C("Try another path", null, "session.restart"),
                    C("Finish", "session.finished", "session.finish")),
                L("session.finished", "The Butterfly Effect",
                    "Small choices. Significant consequences.", null,
                    C("Try another path", null, "session.restart"))
            };
            return result;
        }
    }
}
