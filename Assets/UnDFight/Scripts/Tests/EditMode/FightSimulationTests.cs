using NUnit.Framework;

namespace UnDFight.Tests
{
    public class FightSimulationTests
    {
        [Test]
        public void FixedTickClock_AccumulatesOneSecondIntoExactly60Ticks()
        {
            var clock = new FixedTickClock(60);
            int totalTicks = 0;

            // Simulate 120 render frames at 120 FPS (1 second total)
            for (int i = 0; i < 120; i++)
            {
                totalTicks += clock.Advance(1f / 120f, null);
            }

            Assert.AreEqual(60, totalTicks);
            Assert.AreEqual(60, clock.CurrentTick);
        }

        [Test]
        public void FixedTickClock_At30Fps_ExecutesTwoTicksPerRenderFrame()
        {
            var clock = new FixedTickClock(60);
            int executed = clock.Advance(1f / 30f, null);

            Assert.AreEqual(2, executed);
            Assert.AreEqual(2, clock.CurrentTick);
        }

        [Test]
        public void FixedTickClock_SpiralOfDeathClamp_CapsMaxTicksPerFrame()
        {
            var clock = new FixedTickClock(60, maxAccumulatedTime: 0.25f);
            int executed = clock.Advance(5.0f, null);

            Assert.AreEqual(15, executed);
        }

        [Test]
        public void FixedTickClock_InterpolationAlpha_IsBoundedZeroToOne()
        {
            var clock = new FixedTickClock(60);
            clock.Advance(0.5f / 60f, null);

            Assert.AreEqual(0, clock.CurrentTick);
            Assert.AreEqual(0.5f, clock.InterpolationAlpha, 1e-3f);
        }
    }
}
