using System;
using System.Collections.Generic;

namespace ButterflyEffect.Guidance
{
    public enum GuidanceEnding { Thriving, Threatened, Burning, Devastated }
    public enum CigaretteStatus { Holding, Ground, Disposed }
    public enum CampfireStatus { Burning, HotEmbers, Cold }

    /// <summary>
    /// Small, serializable state model shared by the standalone demo and future scenes.
    /// The 0–3 hazard-to-ending mapping is an authored prototype rule, not a fire prediction.
    /// A cigarette carried back in hand is not a cigarette left in the dry grass.
    /// </summary>
    [Serializable]
    public class GuidanceState
    {
        public CigaretteStatus Cigarette = CigaretteStatus.Holding;
        public CampfireStatus Campfire = CampfireStatus.Burning;
        public bool FireworksIgnited;
        public int MarshmallowsEaten;

        public int UnresolvedHazards
        {
            get
            {
                int count = Cigarette == CigaretteStatus.Ground ? 1 : 0;
                if (Campfire != CampfireStatus.Cold) count++;
                if (FireworksIgnited) count++;
                return count;
            }
        }

        public GuidanceEnding EvaluateEnding()
        {
            return (GuidanceEnding)UnresolvedHazards;
        }

        /// <summary>Exact supplied English cause lines, in cigarette / campfire / fireworks order.</summary>
        public string[] DebriefLines()
        {
            var result = new List<string>();
            if (Cigarette == CigaretteStatus.Ground)
                result.Add("The cigarette you left behind ignited the dry grass.");

            if (Campfire == CampfireStatus.Burning)
            {
                result.Add("Your marshmallow was finished. The fire wasn’t.");
                result.Add("After you left, sparks from the fire reached the dry grass.");
            }
            else if (Campfire == CampfireStatus.HotEmbers)
            {
                result.Add("The flames were gone. The embers were still hot.");
                result.Add("They reignited after you left.");
            }

            if (FireworksIgnited)
                result.Add("You watched the colours in the sky. A spark landed in the dry grass below.");
            if (result.Count == 0)
                result.Add("You left no unresolved fire hazards at the campsite.");
            return result.ToArray();
        }

        public void Reset()
        {
            Cigarette = CigaretteStatus.Holding;
            Campfire = CampfireStatus.Burning;
            FireworksIgnited = false;
            MarshmallowsEaten = 0;
        }
    }
}
