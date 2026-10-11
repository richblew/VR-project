using NUnit.Framework;

namespace VRRocket.Tests
{
    /// <summary>The launch flight model behaves per SPEC 7.2 for each outcome.</summary>
    public class RocketFlightModelTests
    {
        static RocketFlightModel Run(LaunchOutcome outcome, float seconds, float dt = 1f / 72f)
        {
            var m = new RocketFlightModel { outcome = outcome };
            m.Reset();
            for (var t = 0f; t < seconds; t += dt) m.Step(dt);
            return m;
        }

        [Test]
        public void Success_LiftsOffPromptly_ClimbsAndBurnsOut()
        {
            var m = Run(LaunchOutcome.Success, 1f);
            Assert.IsTrue(m.hasLiftedOff, "off the pad within a second at T/W above 2");
            Assert.Greater(m.verticalSpeed, 5f);
            var at7 = Run(LaunchOutcome.Success, 7f);
            Assert.Greater(at7.altitude, 150f, "well up by burnout");
            Assert.AreEqual(at7.dryMass, at7.mass, 1f, "propellant gone at burnout");
            Assert.IsFalse(at7.hasTumbled);
            Assert.IsFalse(at7.hasExploded);
            Assert.AreEqual(0f, at7.tiltDeg, 1e-3f, "flies straight");
            var later = Run(LaunchOutcome.Success, 12f);
            Assert.Greater(later.altitude, at7.altitude, "keeps coasting upward after burnout");
            Assert.IsTrue(later.IsFinished(later.altitude - 1f), "finished once past the success altitude");
        }

        [Test]
        public void MassFallsLinearlyDuringBurn_NeverBelowDry()
        {
            var m = new RocketFlightModel();
            m.Reset();
            var start = m.mass;
            m.Step(m.burnTime * 0.5f);
            Assert.AreEqual(m.dryMass + m.propellantMass * 0.5f, m.mass, 1f);
            m.Step(m.burnTime);
            Assert.AreEqual(m.dryMass, m.mass, 1e-3f);
            Assert.Less(m.mass, start);
        }

        [Test]
        public void UnstableFlight_LiftsOff_TiltGrows_TumblesThenExplodes()
        {
            var early = Run(LaunchOutcome.UnstableFlight, 1.2f);
            Assert.IsTrue(early.hasLiftedOff, "it does leave the pad");
            Assert.AreEqual(0f, early.tiltDeg, 1e-3f, "steady before the instability onset");
            var mid = Run(LaunchOutcome.UnstableFlight, 5f);
            Assert.Greater(mid.tiltDeg, 5f, "tilt has grown");
            Assert.AreNotEqual(0f, mid.tiltAzimuthDeg, "corkscrewing");
            var m = new RocketFlightModel { outcome = LaunchOutcome.UnstableFlight };
            m.Reset();
            var tumbledAt = -1f;
            for (var t = 0f; t < 10f && !m.hasExploded; t += 1f / 72f) { m.Step(1f / 72f); if (m.hasTumbled && tumbledAt < 0f) tumbledAt = m.time; }
            Assert.IsTrue(m.hasExploded, "destroyed in the end");
            Assert.AreEqual(m.explodeTime, m.time, 0.05f, "at the explode time");
            Assert.Greater(tumbledAt, 0f, "tumbled before that");
            Assert.Less(tumbledAt, m.explodeTime);
            Assert.IsTrue(m.IsFinished(1e9f));
            var before = m.altitude;
            m.Step(1f);
            Assert.AreEqual(before, m.altitude, "nothing moves after the explosion");
        }

        [Test]
        public void MotorRetentionLoss_NeverLeavesThePad_MotorEjects()
        {
            var m = Run(LaunchOutcome.MotorRetentionLoss, 10f);
            Assert.IsFalse(m.hasLiftedOff);
            Assert.AreEqual(0f, m.altitude);
            Assert.IsTrue(m.motorEjected, "the motor is driven out of the base");
            Assert.Greater(m.motorEjectDistance, 5f, "and travels away from the pad");
            Assert.IsFalse(m.burning, "the airframe has no thrust once the motor is gone");
            Assert.IsFalse(m.hasExploded);
            Assert.IsTrue(m.IsFinished(1e9f), "the sequence ends on its own");
            var early = Run(LaunchOutcome.MotorRetentionLoss, 0.3f);
            Assert.IsFalse(early.motorEjected, "ejection comes after ignition, not at once");
        }
    }
}
