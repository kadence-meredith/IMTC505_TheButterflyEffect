using System;

namespace ButterflyEffect.Prototype
{
    public enum CigaretteCondition { Held, Dropped, Disposed }
    public enum FireCondition { Cold, Burning, HotEmbers }
    public enum EndingRule { OriginalPitch, RemainingHazards }
    public enum CampusEnding { Thriving, Threatened, Burning, Devastated }
    public enum FireSource { Campfire, CigaretteGrass, FireworksGrass }

    // No scene references: this data can later be retained across portal transitions.
    [Serializable]
    public sealed class ChoiceState
    {
        public CigaretteCondition cigarette = CigaretteCondition.Held;
        public bool cigaretteDroppedOutsideBin;
        public bool campfireEverLit;
        public bool fireworksEverLaunched;
        public FireCondition campfire = FireCondition.Cold;
        public bool cigaretteGrassBurning;
        public bool fireworksGrassBurning;
        public int marshmallowsEaten;
        public int fireworksLaunched;

        public void RecordCigarette(CigaretteCondition condition, bool outsideBin = false)
        {
            cigarette = condition;
            cigaretteDroppedOutsideBin |= outsideBin;
        }

        public void RecordCampfire(FireCondition condition)
        {
            campfire = condition;
            if (condition == FireCondition.Burning) campfireEverLit = true;
        }

        public void RecordFirework()
        {
            fireworksEverLaunched = true;
            fireworksLaunched++;
        }

        public int UnsafeStations(EndingRule rule)
        {
            if (rule == EndingRule.OriginalPitch)
                return (cigaretteDroppedOutsideBin || cigarette != CigaretteCondition.Disposed ? 1 : 0)
                    + (campfireEverLit ? 1 : 0) + (fireworksEverLaunched ? 1 : 0);
            return (cigarette != CigaretteCondition.Disposed || cigaretteGrassBurning ? 1 : 0)
                + (campfire != FireCondition.Cold ? 1 : 0) + (fireworksGrassBurning ? 1 : 0);
        }

        public CampusEnding PreviewEnding(EndingRule rule)
        {
            return (CampusEnding)UnsafeStations(rule);
        }

        public static string EndingTitle(CampusEnding ending)
        {
            switch (ending)
            {
                case CampusEnding.Thriving: return "A Place Protected";
                case CampusEnding.Threatened: return "A Fragile Future";
                case CampusEnding.Burning: return "The Spark Came Home";
                default: return "What We Leave Behind";
            }
        }
    }

    public interface IPrototypeResettable { void ResetPrototype(); }

    // Shared by campfire and grass fires; water amounts are game units, not safety advice.
    [Serializable]
    public sealed class FireProgress
    {
        public FireCondition condition = FireCondition.Cold;
        public float water;
        public const float FlameThreshold = 1.2f;
        public const float ColdThreshold = 3.2f;

        public void Ignite() { water = 0f; condition = FireCondition.Burning; }
        public void Reset() { water = 0f; condition = FireCondition.Cold; }
        public void Pour(float amount)
        {
            if (condition == FireCondition.Cold || amount <= 0f) return;
            water = Math.Min(ColdThreshold, water + amount);
            condition = water >= ColdThreshold ? FireCondition.Cold
                : water >= FlameThreshold ? FireCondition.HotEmbers : FireCondition.Burning;
        }
    }
}
