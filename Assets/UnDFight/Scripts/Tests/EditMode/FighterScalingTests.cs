using NUnit.Framework;
using UnityEngine;

namespace UnDFight.Tests
{
    public class FighterScalingTests
    {
        [Test]
        public void ModelAndHurtboxScale_ReferenceHeight_ReturnsOne()
        {
            float modelScale = FighterScaling.ComputeModelScale(1.81f);
            float hurtboxScale = FighterScaling.ComputeHurtboxScale(1.81f);

            Assert.AreEqual(1.0f, modelScale, 1e-5f);
            Assert.AreEqual(1.0f, hurtboxScale, 1e-5f);
        }

        [Test]
        public void ModelAndHurtboxScale_TiagoHeight_IsProportionallySmaller()
        {
            float tiagoScale = FighterScaling.ComputeModelScale(1.79f);
            float expected = 1.79f / 1.81f;

            Assert.AreEqual(expected, tiagoScale, 1e-5f);
            Assert.Less(tiagoScale, 1.0f);
        }

        [Test]
        public void WeightScaling_HeavierFighter_HasHigherGravitySlowerWalkAndLessKnockback()
        {
            float lucasWeight = 92f;
            float tiagoWeight = 87f;
            float baseGravity = 34f;
            float baseWalk = 4.2f;

            float lucasGravity = FighterScaling.ComputeEffectiveGravity(baseGravity, lucasWeight);
            float tiagoGravity = FighterScaling.ComputeEffectiveGravity(baseGravity, tiagoWeight);
            Assert.Greater(lucasGravity, tiagoGravity);
            Assert.AreEqual(baseGravity, lucasGravity, 1e-4f);

            float lucasWalk = FighterScaling.ComputeEffectiveWalkSpeed(baseWalk, lucasWeight);
            float tiagoWalk = FighterScaling.ComputeEffectiveWalkSpeed(baseWalk, tiagoWeight);
            Assert.Less(lucasWalk, tiagoWalk);
            Assert.AreEqual(baseWalk, lucasWalk, 1e-4f);

            float lucasKb = FighterScaling.ComputeKnockbackMultiplier(lucasWeight);
            float tiagoKb = FighterScaling.ComputeKnockbackMultiplier(tiagoWeight);
            Assert.Less(lucasKb, tiagoKb);
            Assert.AreEqual(1.0f, lucasKb, 1e-4f);
        }
    }
}
