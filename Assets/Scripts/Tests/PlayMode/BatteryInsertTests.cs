using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// The battery (the kit's motor part) slots into the base of the tube with no real chance of failure: caught from a generous
    /// zone at an angle and off centre, kept on its track while any of it is in the tube whatever the hand does sideways, slid home
    /// when let go near the base, and, the wrong way up, kept out of the tube instead of passing through its wall.
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

        Quaternion Upright => m_Seat.transform.rotation;

        IEnumerator ReleaseAndExpectSeated(string why)
        {
            Release(m_Battery);
            yield return new WaitForSeconds(tuning.seatDurationMotor + 0.4f);
            Assert.AreEqual(PartState.Attached, m_Battery.state, why);
            Assert.AreSame(m_Seat, m_Battery.attachedTo);
            Assert.Less(Vector3.Distance(m_Battery.transform.position, m_Seat.transform.position), 1e-3f, "seated exactly");
            Assert.Less(Vector3.Angle(m_Battery.transform.up, m_Seat.transform.up), 1f, "+ end up, along the tube");
        }

        [UnityTest]
        public IEnumerator Caught_Tilted_And_Off_Centre_Then_Seats()
        {
            yield return Begin();
            var tilted = Quaternion.AngleAxis(50f, m_Seat.transform.right) * Upright;
            var pos = m_Seat.transform.position + m_Seat.GuideAxisWorld * 0.08f + m_Seat.transform.forward * 0.05f;
            yield return PlaceHeld(pos, tilted);
            Assert.IsTrue(m_Guide.isGuided, "caught 50 degrees off and 5 cm off centre");
            yield return ReleaseAndExpectSeated("and slides home when let go");
        }

        [UnityTest]
        public IEnumerator Stays_On_The_Track_When_The_Hand_Drifts_Mid_Insert()
        {
            yield return Begin();
            yield return PlaceHeld(m_Seat.transform.position + m_Seat.GuideAxisWorld * 0.04f, Upright);
            Assert.IsTrue(m_Guide.isGuided);
            // the hand wanders 15 cm sideways while the battery is part way into the tube
            var start = controller.position;
            for (var i = 1; i <= 15; i++)
            {
                controller.position = start + m_Seat.transform.right * (0.01f * i);
                yield return null;
                Assert.IsTrue(m_Guide.isGuided, "still on the track at " + i + " cm of drift");
                Assert.Less(GuideMath.Sideways(m_Battery.transform.position, m_Seat.transform.position, m_Seat.GuideAxisWorld), 1e-3f, "never off the tube's axis (never through the wall)");
            }
            yield return ReleaseAndExpectSeated("let go after drifting: still seats");
        }

        [UnityTest]
        public IEnumerator Let_Go_Near_The_Base_Slides_Home()
        {
            yield return Begin();
            yield return PlaceHeld(m_Seat.transform.position + m_Seat.GuideAxisWorld * 0.13f, Upright);
            Assert.IsFalse(m_Guide.isGuided, "13 cm below: not caught yet");
            yield return ReleaseAndExpectSeated("let go there: slides home rather than dropping");
        }

        [UnityTest]
        public IEnumerator Wrong_Way_Up_Is_Kept_Out_Of_The_Tube()
        {
            yield return Begin();
            var upsideDown = Quaternion.AngleAxis(180f, m_Seat.transform.right) * Upright;
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
    }
}
