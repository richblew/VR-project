using NUnit.Framework;
using UnityEngine;

namespace VRRocket.Tests
{
    /// <summary>The launch flight model behaves per SPEC 7.2 for each outcome.</summary>
    public class RocketFlightModelTests
    {
        static RocketFlightModel Run(LaunchOutcome outcome, float seconds, float dt = 1f / 72f, int wrongFlaps = 3)
        {
            var m = new RocketFlightModel { outcome = outcome, wrongFlaps = wrongFlaps };
            m.Reset();
            for (var t = 0f; t < seconds; t += dt) m.Step(dt);
            return m;
        }

        // the room's window, seen from the console: the pad 48 m out along +X, the window about 37 degrees either side and 35 m up at the pad
        static float BearingFromRoomDeg(RocketFlightModel m) => Mathf.Atan2(m.horizontalZ, 48f + m.horizontalX) * Mathf.Rad2Deg;

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
        public void UnstableFlight_TwoOrThreeWrongFlaps_BreaksUpLowInTheWindow()
        {
            foreach (var wrong in new[] { 2, 3 })
            {
                var early = Run(LaunchOutcome.UnstableFlight, 0.15f, wrongFlaps: wrong);
                Assert.IsTrue(early.hasLiftedOff, "it does leave the pad");
                Assert.AreEqual(0f, early.tiltDeg, 1e-3f, "steady before the instability onset");
                var m = new RocketFlightModel { outcome = LaunchOutcome.UnstableFlight, wrongFlaps = wrong };
                m.Reset();
                var maxTiltBy1s = 0f;
                for (var t = 0f; t < 10f && !m.hasExploded; t += 1f / 72f)
                {
                    m.Step(1f / 72f);
                    if (m.time <= 1.2f) maxTiltBy1s = Mathf.Max(maxTiltBy1s, m.tiltDeg);
                    if (Mathf.Abs(m.time - 1.2f) < 0.01f) Assert.AreNotEqual(0f, m.tiltAzimuthDeg, "corkscrewing");
                }
                Assert.Greater(maxTiltBy1s, 10f, "the wobble has grown within a second of liftoff");
                Assert.IsTrue(m.hasExploded, "destroyed in the end");
                Assert.IsFalse(m.hasCrashed, "in the air, not on the ground");
                Assert.IsTrue(m.hasTumbled, "tumbled first");
                Assert.AreEqual(m.tumbleTime + m.breakUpDelay, m.time, 0.05f, "breaks up soon after tumbling");
                Assert.Less(m.time, 3f, "within a few seconds of liftoff");
                Assert.Greater(m.altitude, 8f, "clear of the pad");
                Assert.Less(m.altitude, 22f, "low enough to be seen through the window, fireball and all");
                Assert.IsTrue(m.IsFinished(1e9f));
                var before = m.altitude;
                m.Step(1f);
                Assert.AreEqual(before, m.altitude, "nothing moves after the explosion");
            }
        }

        [Test]
        public void UnstableFlight_OneWrongFlap_VeersOffCourseAndCrashesInView()
        {
            var early = Run(LaunchOutcome.UnstableFlight, 0.8f, wrongFlaps: 1);
            Assert.IsTrue(early.hasLiftedOff);
            Assert.AreEqual(0f, early.tiltDeg, 1e-3f, "climbs straight at first");
            var m = new RocketFlightModel { outcome = LaunchOutcome.UnstableFlight, wrongFlaps = 1, groundBelowPad = 2.4f };
            m.Reset();
            var lastDistance = 0f;
            var turned = false;
            for (var t = 0f; t < 20f && !m.hasExploded; t += 1f / 72f)
            {
                m.Step(1f / 72f);
                var d = new Vector2(m.horizontalX, m.horizontalZ).magnitude;
                Assert.GreaterOrEqual(d, lastDistance - 1e-3f, "it keeps going away: a veer, not a corkscrew");
                lastDistance = d;
                if (m.tiltDeg > 0f) Assert.AreEqual(m.offCourseAzimuth, m.tiltAzimuthDeg, 1e-3f, "always the same way");
                if (m.tiltDeg > 90f) turned = true;
            }
            Assert.IsTrue(turned, "pitched over past the horizon");
            Assert.IsTrue(m.hasExploded && m.hasCrashed, "crashed into the ground");
            Assert.Less(m.time, 8f, "within a few seconds");
            Assert.AreEqual(-m.groundBelowPad, m.lowestPoint, 0.05f, "the explosion is on the ground, not above it");
            Assert.That(lastDistance, Is.InRange(45f, 120f), "downrange, but close enough to see");
            Assert.Less(m.maxAltitude, 65f, "an arc low enough to follow through the window");
            Assert.That(BearingFromRoomDeg(m), Is.InRange(5f, 30f), "lands inside the window's view, to one side of the pad");
        }

        [Test]
        public void Tilt_Pushes_The_Way_The_Body_Leans()
        {
            var m = Run(LaunchOutcome.UnstableFlight, 3f, wrongFlaps: 1);
            m.BodyAxis(out var bx, out _, out var bz);
            var lean = new Vector2(bx, bz).normalized;
            var drift = new Vector2(m.horizontalX, m.horizontalZ).normalized;
            Assert.Greater(Vector2.Dot(lean, drift), 0.99f, "it drifts the way its nose leans");
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
