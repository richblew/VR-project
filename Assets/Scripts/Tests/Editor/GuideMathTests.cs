using NUnit.Framework;
using UnityEngine;

namespace VRRocket.Tests
{
    public class GuideMathTests
    {
        static readonly Vector3 Seat = new Vector3(1f, 2f, 3f);
        static readonly Vector3 Up = Vector3.up;

        [Test]
        public void DepthAndSideways_DecomposeCorrectly()
        {
            var p = Seat + Up * 0.03f + Vector3.right * 0.04f;
            Assert.AreEqual(0.03f, GuideMath.Depth(p, Seat, Up), 1e-6f);
            Assert.AreEqual(0.04f, GuideMath.Sideways(p, Seat, Up), 1e-6f);
            Assert.AreEqual(-0.01f, GuideMath.Depth(Seat - Up * 0.01f, Seat, Up), 1e-6f);
        }

        [Test]
        public void GuidedPosition_ClampsAndAppliesMagnetism()
        {
            Assert.AreEqual(Seat + Up * 0.03f, GuideMath.GuidedPosition(Seat, Up, 0.03f, 0.06f, 1f));
            Assert.AreEqual(Seat + Up * 0.06f, GuideMath.GuidedPosition(Seat, Up, 0.09f, 0.06f, 1f));
            Assert.AreEqual(Seat, GuideMath.GuidedPosition(Seat, Up, -0.02f, 0.06f, 1f));
            // nose magnetism 0.5 shows half the hand's distance
            Assert.AreEqual(Seat + Up * 0.02f, GuideMath.GuidedPosition(Seat, Up, 0.04f, 0.06f, 0.5f));
        }

        [Test]
        public void ShouldBreak_OnSidewaysDriftOrBeyondGuideEnd()
        {
            Assert.IsFalse(GuideMath.ShouldBreak(0.03f, 0.02f, 0.06f, 0.08f));
            Assert.IsTrue(GuideMath.ShouldBreak(0.03f, 0.09f, 0.06f, 0.08f));
            Assert.IsTrue(GuideMath.ShouldBreak(0.07f, 0.0f, 0.06f, 0.08f));
            Assert.IsFalse(GuideMath.ShouldBreak(-0.01f, 0.0f, 0.06f, 0.08f), "pushing past the seat clamps, it does not break");
        }

        [Test]
        public void WithinOrientation_UsesTolerance()
        {
            var tilted = Quaternion.Euler(30f, 0f, 0f) * Vector3.up;
            Assert.IsTrue(GuideMath.WithinOrientation(tilted, Vector3.up, 45f));
            Assert.IsFalse(GuideMath.WithinOrientation(tilted, Vector3.up, 20f));
            Assert.IsFalse(GuideMath.WithinOrientation(Vector3.down, Vector3.up, 45f), "a reversed motor is outside tolerance");
        }

        [Test]
        public void SeatedRotation_NoseKeepsRollAndAlignsAxis()
        {
            var point = Quaternion.identity;
            var held = Quaternion.Euler(20f, 70f, 10f);
            var seated = GuideMath.SeatedRotation(PartType.NoseCone, point, held);
            Assert.Less(Vector3.Angle(seated * Vector3.up, Vector3.up), 1e-3f, "axis aligned to the tube");
            // roll kept: the seated yaw should be close to the held yaw, not forced to the point's
            var heldForwardFlat = Vector3.ProjectOnPlane(held * Vector3.forward, Vector3.up);
            var seatedForwardFlat = Vector3.ProjectOnPlane(seated * Vector3.forward, Vector3.up);
            Assert.Less(Vector3.Angle(heldForwardFlat, seatedForwardFlat), 25f);
        }

        [Test]
        public void SeatedRotation_FinIsRollCorrected()
        {
            var point = Quaternion.Euler(0f, 120f, 0f);
            var held = point * Quaternion.Euler(0f, 0f, 160f);
            Assert.AreEqual(point, GuideMath.SeatedRotation(PartType.TailFin, point, held));
        }

        [Test]
        public void SeatedRotation_FlapKeepsWhicheverWayUp()
        {
            var point = Quaternion.Euler(0f, 240f, 0f);
            var heldUp = point * Quaternion.Euler(0f, 0f, 20f);
            var heldDown = point * Quaternion.Euler(0f, 0f, 200f);
            var seatedUp = GuideMath.SeatedRotation(PartType.WingFlap, point, heldUp);
            var seatedDown = GuideMath.SeatedRotation(PartType.WingFlap, point, heldDown);
            Assert.AreEqual(FlapOrientation.Up, GuideMath.FlapOrientationFor(point, seatedUp));
            Assert.AreEqual(FlapOrientation.Down, GuideMath.FlapOrientationFor(point, seatedDown));
            // both share the slot's outward axis
            Assert.Less(Vector3.Angle(seatedDown * Vector3.forward, point * Vector3.forward), 1e-3f);
        }
    }
}
