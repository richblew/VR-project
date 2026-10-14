using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// The battery (the kit's motor part) is caught only once its + end is in the tube's base (a little or a long way, straight
    /// or leaning), never before. Caught, it straightens on the tube's axis without moving in or out, stays on that axis whatever
    /// the hand or the tube does, and let go it always slides home straight. Let go outside the base it drops. The wrong way up
    /// it is kept out of the tube. Its glow ring is on the + end, the end that goes in.
    /// </summary>
    public class BatteryInsertTests : DevSceneTestBase
    {
        RocketPart m_Battery;
        AttachPoint m_Seat;
        GuideGrabTransformer m_Guide;

        IEnumerator Begin()
        {
            assembly.enforceOrder = false;   // the shipped free order: the battery may go in on a bare tube
            m_Battery = Part(PartType.Motor);
            m_Seat = Point("MotorSeat");
            m_Guide = m_Battery.GetComponent<GuideGrabTransformer>();
            yield return Grab(m_Battery);
        }

        Vector3 Into => -m_Seat.GuideAxisWorld;
        Vector3 TubeEnd => Point("CapSeat").transform.position;
        Vector3 Tip => m_Battery.transform.position + m_Battery.transform.up * tuning.motorLength;
        /// <summary>How far the battery's + end is inside the tube (negative: still outside).</summary>
        float TipIn => Vector3.Dot(Tip - TubeEnd, Into);
        float TipOff => GuideMath.Sideways(Tip, TubeEnd, Into);

        /// <summary>Holds the battery with its + end <paramref name="tipIn"/> into the tube (on its axis plus <paramref name="aside"/>), leaning <paramref name="lean"/> degrees.</summary>
        IEnumerator HoldTip(float tipIn, float lean, Vector3 aside = default)
        {
            var rot = Quaternion.AngleAxis(lean, m_Seat.transform.right) * m_Seat.transform.rotation;
            var tip = TubeEnd + Into * tipIn + aside;
            yield return PlaceHeld(tip - rot * Vector3.up * tuning.motorLength, rot);
        }

        IEnumerator ReleaseAndExpectSeated(string why)
        {
            Release(m_Battery);
            yield return new WaitForSeconds(tuning.seatDurationMotor + 0.4f);
            Assert.AreEqual(PartState.Attached, m_Battery.state, why);
            Assert.AreSame(m_Seat, m_Battery.attachedTo);
            Assert.Less(Vector3.Distance(m_Battery.transform.position, m_Seat.transform.position), 1e-3f, "seated exactly");
            Assert.Less(Vector3.Angle(m_Battery.transform.up, m_Seat.transform.up), 1f, "straight in the tube, + end up");
        }

        void AssertOnTheAxis(string why)
        {
            Assert.Less(Vector3.Angle(m_Battery.transform.up, Into), 1f, why + ": straight along the tube");
            Assert.Less(TipOff, 1e-3f, why + ": on the tube's axis, never through its wall");
        }

        [UnityTest]
        public IEnumerator Not_Caught_Before_It_Is_In_The_Base()
        {
            yield return Begin();
            // close below the base and off to the side: the old snap zone caught this and pulled it over
            yield return PlaceHeld(m_Seat.transform.position + m_Seat.GuideAxisWorld * 0.08f + m_Seat.transform.right * 0.06f, m_Seat.transform.rotation);
            Assert.IsFalse(m_Guide.isGuided, "8 cm below and 6 cm aside: not caught");
            // right under the opening, straight, its + end 1 cm short of the tube
            yield return HoldTip(-0.01f, 0f);
            Assert.IsFalse(m_Guide.isGuided, "1 cm short of the base: not caught");
            Assert.Less(Vector3.Angle(m_Battery.transform.up, Into), 1f);
            // leaning 50 degrees just under the base: it stays leaning in the hand (no straightening in mid-air)
            yield return HoldTip(-0.01f, 50f);
            Assert.IsFalse(m_Guide.isGuided);
            Assert.AreEqual(50f, Vector3.Angle(m_Battery.transform.up, Into), 3f, "nothing turns it before it is in");
        }

        [UnityTest]
        public IEnumerator Caught_Barely_In_And_Leaning_Then_Seats_Straight()
        {
            yield return Begin();
            yield return HoldTip(0.008f, 40f);
            Assert.IsTrue(m_Guide.isGuided, "8 mm in, leaning 40 degrees: caught");
            yield return new WaitForSeconds(0.2f);
            AssertOnTheAxis("caught");
            Assert.AreEqual(0.008f, TipIn, 0.006f, "straightened without being pushed in or pulled out");
            yield return ReleaseAndExpectSeated("let go barely in: slides home");
        }

        [UnityTest]
        public IEnumerator Caught_Deep_In_Then_Seats_Straight()
        {
            yield return Begin();
            yield return HoldTip(0.05f, 20f);
            Assert.IsTrue(m_Guide.isGuided, "5 cm in, leaning 20 degrees: caught");
            yield return new WaitForSeconds(0.2f);
            AssertOnTheAxis("caught deep");
            Assert.AreEqual(0.05f, TipIn, 0.006f);
            yield return ReleaseAndExpectSeated("let go deep in: slides home");
        }

        [UnityTest]
        public IEnumerator Stays_On_The_Axis_When_The_Hand_Drifts_And_Leans()
        {
            yield return Begin();
            yield return HoldTip(0.03f, 0f);
            Assert.IsTrue(m_Guide.isGuided);
            var start = controller.position;
            var startRot = controller.rotation;
            for (var i = 1; i <= 15; i++)
            {
                // the hand wanders 15 cm sideways and leans 45 degrees while the battery is in the tube
                controller.SetPositionAndRotation(start + m_Seat.transform.right * (0.01f * i), Quaternion.AngleAxis(3f * i, m_Seat.transform.forward) * startRot);
                yield return null;
                Assert.IsTrue(m_Guide.isGuided, "still on the track at " + i + " cm of drift");
                AssertOnTheAxis(i + " cm of drift");
            }
            yield return ReleaseAndExpectSeated("let go after drifting: still seats");
        }

        [UnityTest]
        public IEnumerator Tube_Turned_Mid_Insert_Still_Seats_Straight()
        {
            yield return Begin();
            yield return HoldTip(0.03f, 0f);
            Assert.IsTrue(m_Guide.isGuided);
            // the player turns the rocket in the other hand while the battery is in it
            var tube = Tube.transform;
            var pivot = TubeEnd;
            for (var i = 0; i < 20; i++)
            {
                tube.RotateAround(pivot, Vector3.right, 1.5f);
                if (Tube.body != null) { Tube.body.position = tube.position; Tube.body.rotation = tube.rotation; }
                Physics.SyncTransforms();
                yield return null;
            }
            yield return new WaitForSeconds(0.1f);
            AssertOnTheAxis("the tube turned 30 degrees");
            yield return ReleaseAndExpectSeated("and it seats straight in the turned tube");
        }

        [UnityTest]
        public IEnumerator Let_Go_Outside_The_Base_It_Drops()
        {
            yield return Begin();
            yield return HoldTip(-0.03f, 0f);
            Assert.IsFalse(m_Guide.isGuided);
            Release(m_Battery);
            yield return new WaitForSeconds(tuning.seatDurationMotor + 0.4f);
            Assert.AreNotEqual(PartState.Attached, m_Battery.state, "no sliding in from outside the base");
        }

        [UnityTest]
        public IEnumerator Pulled_Back_Out_It_Comes_Off_The_Track()
        {
            yield return Begin();
            yield return HoldTip(0.03f, 0f);
            Assert.IsTrue(m_Guide.isGuided);
            yield return HoldTip(-0.03f, 0f);
            Assert.IsFalse(m_Guide.isGuided, "its + end out of the tube: free again");
            Assert.Less(TipIn, 0f);
        }

        [UnityTest]
        public IEnumerator Wrong_Way_Up_Is_Kept_Out_Of_The_Tube()
        {
            yield return Begin();
            var upsideDown = Quaternion.AngleAxis(180f, m_Seat.transform.right) * m_Seat.transform.rotation;
            // pushed up into the tube's base the wrong way round
            yield return PlaceHeld(m_Seat.transform.position + m_Seat.transform.up * 0.02f, upsideDown);
            Assert.IsFalse(m_Guide.isGuided, "refused: it goes in + end up");
            var away = m_Seat.GuideAxisWorld;
            var seat = m_Seat.transform.position;
            var capSeat = Point("CapSeat").transform.position;
            var tubeEnd = Mathf.Max(0f, GuideMath.Depth(capSeat, seat, away));
            var baseEnd = m_Battery.transform.position;
            var topEnd = baseEnd + m_Battery.transform.up * (tuning.motorLength * m_Battery.transform.lossyScale.x);
            Assert.GreaterOrEqual(GuideMath.Depth(baseEnd, seat, away), tubeEnd - 2e-3f, "no part of it inside the tube");
            Assert.GreaterOrEqual(GuideMath.Depth(topEnd, seat, away), tubeEnd - 2e-3f, "no part of it inside the tube");
            Release(m_Battery);
            yield return new WaitForSeconds(0.3f);
            Assert.AreNotEqual(PartState.Attached, m_Battery.state);
        }

        [UnityTest]
        public IEnumerator Its_Glow_Ring_Is_On_The_Plus_End()
        {
            yield return Begin();
            var ring = m_Battery.glowRenderer;
            Assert.IsNotNull(ring);
            var along = Vector3.Dot(ring.bounds.center - m_Battery.transform.position, m_Battery.transform.up);
            Assert.Greater(along, tuning.motorLength * 0.9f, "the ring lights the + end, the end that goes into the tube");
        }
    }
}
