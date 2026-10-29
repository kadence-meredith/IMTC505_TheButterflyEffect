using NUnit.Framework;

namespace ButterflyEffect.Prototype.Tests
{
    public sealed class ChoiceStateTests
    {
        [TestCase(false, false, false, CampusEnding.Thriving)]
        [TestCase(true, false, false, CampusEnding.Threatened)]
        [TestCase(false, true, false, CampusEnding.Threatened)]
        [TestCase(false, false, true, CampusEnding.Threatened)]
        [TestCase(true, true, false, CampusEnding.Burning)]
        [TestCase(true, false, true, CampusEnding.Burning)]
        [TestCase(false, true, true, CampusEnding.Burning)]
        [TestCase(true, true, true, CampusEnding.Devastated)]
        public void OriginalPitchCoversAllEightChoiceCombinations(bool cigarette, bool campfire, bool fireworks, CampusEnding expected)
        {
            var state = new ChoiceState();
            state.RecordCigarette(CigaretteCondition.Disposed, cigarette);
            if (campfire) state.RecordCampfire(FireCondition.Burning);
            if (fireworks) state.RecordFirework();
            Assert.That(state.PreviewEnding(EndingRule.OriginalPitch), Is.EqualTo(expected));
        }

        [Test]
        public void RepeatingFireworksDoesNotAddExtraUnsafeStations()
        {
            var state = new ChoiceState(); state.RecordCigarette(CigaretteCondition.Disposed);
            for (int i = 0; i < 12; i++) state.RecordFirework();
            Assert.That(state.UnsafeStations(EndingRule.OriginalPitch), Is.EqualTo(1));
            Assert.That(state.fireworksLaunched, Is.EqualTo(12));
        }

        [Test]
        public void CleanupChangesRemainingHazardsButPreservesPitchHistory()
        {
            var state = new ChoiceState();
            state.RecordCigarette(CigaretteCondition.Dropped, true); state.RecordCigarette(CigaretteCondition.Disposed);
            state.RecordCampfire(FireCondition.Burning); state.RecordCampfire(FireCondition.Cold);
            state.RecordFirework(); state.fireworksGrassBurning = false;
            Assert.That(state.UnsafeStations(EndingRule.OriginalPitch), Is.EqualTo(3));
            Assert.That(state.UnsafeStations(EndingRule.RemainingHazards), Is.Zero);
        }

        [Test]
        public void PickingUpCigaretteDoesNotClearAnExistingGrassFire()
        {
            var state = new ChoiceState(); state.cigaretteGrassBurning = true;
            state.RecordCigarette(CigaretteCondition.Disposed);
            Assert.That(state.UnsafeStations(EndingRule.RemainingHazards), Is.EqualTo(1));
        }

        [Test]
        public void FlamesOutDoesNotMeanEmbersAreCold()
        {
            var fire = new FireProgress(); fire.Ignite(); fire.Pour(FireProgress.FlameThreshold);
            Assert.That(fire.condition, Is.EqualTo(FireCondition.HotEmbers));
            fire.Pour(FireProgress.ColdThreshold);
            Assert.That(fire.condition, Is.EqualTo(FireCondition.Cold));
        }

        [Test]
        public void NegativeWaterCannotReheatOrUndoProgress()
        {
            var fire = new FireProgress(); fire.Ignite(); fire.Pour(1.5f); fire.Pour(-10f);
            Assert.That(fire.condition, Is.EqualTo(FireCondition.HotEmbers));
            Assert.That(fire.water, Is.EqualTo(1.5f));
        }

    }
}
