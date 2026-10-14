using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// SPEC.md section 5.4 on a bare tube with gating off: seat unlocked with the gap, ratchet by turning the wrist clockwise
    /// (viewed from below), detent ticks, lock at capLockAngle with the gap closing, nothing after lock, early let-go keeps the
    /// seat, progress kept between grabs, pull-off resets.
    /// </summary>
    public class CapTwistTests : DevSceneTestBase
    {
        RocketPart m_Cap;
        MotorCapTwist m_Twist;
        AttachPoint m_Seat;

        /// <summary>Events that carry feedback. Raw grab, release and hover notifications are filtered by the feedback layer (a seated cap's let-go plays nothing).</summary>
        int FeedbackEventCount()
        {
            var n = 0;
            foreach (var e in events) if (!e.StartsWith("grabbed:") && !e.StartsWith("releasedPart:") && !e.StartsWith("hovered:")) n++;
            return n;
        }

        IEnumerator SeatCap()
        {
            // SPEC 5.5: the cap always needs the motor, even with the order gating off, so seat the motor on the bare tube first.
            assembly.enforceOrder = false;
            yield return SeatPart(Part(PartType.Motor), Point("MotorSeat"), Quaternion.identity);
            m_Cap = Part(PartType.MotorCap);
            m_Twist = m_Cap.GetComponent<MotorCapTwist>();
            m_Seat = Point("CapSeat");
            yield return SeatPart(m_Cap, m_Seat, Quaternion.identity);
        }

        float Gap() => Vector3.Dot(m_Cap.transform.position - m_Seat.transform.position, m_Seat.GuideAxisWorld);

        Vector3 m_HandStartPos;
        Quaternion m_HandStartRot;

        /// <summary>Grabs the seated cap; the hand then turns rigidly about the tube's axis, as a wrist does.</summary>
        IEnumerator GrabSeatedCapOnOrbit()
        {
            yield return Grab(m_Cap);
            Assert.AreEqual(PartState.Attached, m_Cap.state, "gripping the seated cap does not unseat it");
            m_HandStartPos = controller.position;
            m_HandStartRot = controller.rotation;
            yield return null;
            Assert.IsTrue(m_Twist.isTwisting);
        }

        /// <summary>
        /// Turns the hand from one cap angle to another (clockwise viewed from below, in degrees of cap progress): the wrist turns
        /// by that angle divided by the twist sensitivity, rigidly about the tube's axis through the seat.
        /// </summary>
        IEnumerator Turn(float fromCap, float toCap, float step = 20f)
        {
            var dir = Mathf.Sign(toCap - fromCap);
            var seat = m_Seat.transform;
            for (var a = fromCap; Mathf.Abs(toCap - a) > 1e-3f;)
            {
                a = dir > 0 ? Mathf.Min(a + step, toCap) : Mathf.Max(a - step, toCap);
                var q = Quaternion.AngleAxis(-a / tuning.capTwistSensitivity, seat.up);   // clockwise from below: negative Unity yaw
                controller.position = seat.position + q * (m_HandStartPos - seat.position);
                controller.rotation = q * m_HandStartRot;
                yield return null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Cap_SeatsUnlockedWithGap_RatchetsClockwise_LocksAtLockAngle()
        {
            yield return SeatCap();
            var gap = tuning.capUnlockedGap;
            Assert.IsFalse(m_Twist.capLocked);
            Assert.AreEqual(gap, Gap(), 5e-4f, "unlocked cap sits capUnlockedGap proud of the tube");
            Assert.AreSame(m_Seat.transform, m_Cap.transform.parent);
            var seatedRotation = m_Cap.transform.rotation;
            var up = m_Seat.transform.up;

            yield return GrabSeatedCapOnOrbit();
            Assert.AreEqual(gap, Gap(), 5e-4f, "gripping does not move it");

            // Clockwise viewed from below is a negative Unity yaw about the tube's +Y. Fine steps, as a real wrist moves.
            yield return Turn(0f, 100f, 5f);
            Assert.AreEqual(100f, m_Twist.progressDegrees, 1.5f);
            Assert.AreEqual(3, Count("detent"), "ticks at 30, 60, 90");
            Assert.GreaterOrEqual(Count("twistTexture"), 6, "thread texture ticks between the detents");
            Assert.Less(Quaternion.Angle(m_Cap.transform.rotation, Quaternion.AngleAxis(-100f, up) * seatedRotation), 1.5f, "cap turned with the hand");
            Assert.AreEqual(gap, Gap(), 5e-4f, "still unlocked, gap stays");

            // Turning back does not unwind and makes no clicks
            yield return Turn(100f, 50f);
            Assert.AreEqual(100f, m_Twist.progressDegrees, 1.5f);
            Assert.AreEqual(3, Count("detent"));
            Assert.IsFalse(m_Twist.capLocked);

            // Forward again accumulates like a ratchet, then locks exactly at capLockAngle
            yield return Turn(50f, 140f);
            Assert.IsTrue(m_Twist.capLocked, "locked");
            Assert.AreEqual(tuning.capLockAngle, m_Twist.progressDegrees, 1e-3f, "locks exactly at capLockAngle, no overshoot");
            Assert.AreEqual(1, Count("locked"));
            Assert.AreEqual(5, Count("detent"), "120 and 150 ticked; the 180 detent is the lock click");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, Gap(), 5e-4f, "gap closes on lock");
            Assert.Less(Quaternion.Angle(m_Cap.transform.rotation, Quaternion.AngleAxis(-tuning.capLockAngle, up) * seatedRotation), 1.5f);

            // A locked cap can no longer be turned or removed
            yield return Turn(140f, 200f);
            Assert.AreEqual(tuning.capLockAngle, m_Twist.progressDegrees, 1e-3f);
            Assert.AreEqual(5, Count("detent"));
            Assert.AreEqual(1, Count("locked"));
            yield return MoveHand(controller.position + m_Seat.GuideAxisWorld * (tuning.capPullOff * 2f));
            Assert.AreEqual(PartState.Attached, m_Cap.state, "a locked cap cannot be pulled off");
            Assert.AreEqual(0, Count("removed:MotorCap_1@CapSeat"));

            Release(m_Cap);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(PartState.Attached, m_Cap.state);
            Assert.IsTrue(m_Cap.body.isKinematic);
            Assert.AreSame(m_Seat.transform, m_Cap.transform.parent);
            Assert.AreEqual(0f, Gap(), 5e-4f);
        }

        [UnityTest]
        public IEnumerator Cap_EarlyLetGoKeepsSeat_ProgressKeptBetweenGrabs_PullOffResets()
        {
            yield return SeatCap();
            var gap = tuning.capUnlockedGap;

            yield return GrabSeatedCapOnOrbit();
            yield return Turn(0f, 60f);
            Assert.AreEqual(60f, m_Twist.progressDegrees, 1.5f);
            Assert.AreEqual(2, Count("detent"));

            // Let go early: stays seated, unlocked, no warning of any kind
            var eventsBefore = FeedbackEventCount();
            Release(m_Cap);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(PartState.Attached, m_Cap.state, "cap stays seated");
            Assert.IsFalse(m_Twist.capLocked);
            Assert.AreEqual(gap, Gap(), 5e-4f, "gap remains");
            Assert.AreEqual(eventsBefore, FeedbackEventCount(), "letting go early is silent");

            // Progress is kept between grabs
            yield return GrabSeatedCapOnOrbit();
            yield return Turn(0f, 35f);
            Assert.AreEqual(95f, m_Twist.progressDegrees, 1.5f, "accumulates across grabs");
            Assert.AreEqual(3, Count("detent"), "90 ticked on the second grab");

            // Pull straight off: detaches, progress resets
            yield return MoveHand(controller.position + m_Seat.GuideAxisWorld * (tuning.capPullOff * 1.5f));
            Assert.AreEqual(PartState.Free, m_Cap.state, "unlocked cap pulls off");
            Assert.IsFalse(m_Seat.isOccupied);
            Assert.AreEqual(1, Count("removed:MotorCap_1@CapSeat"));
            Assert.AreEqual(0f, m_Twist.progressDegrees, 1e-3f, "progress resets to zero");
            // The dynamic attach snapped to the cap's collider surface (lugs top), so the hand-to-origin offset is not exactly grabOffset.
            Assert.Less(Vector3.Distance(m_Cap.transform.position, controller.position), 0.05f, "follows the hand");
            Release(m_Cap);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(m_Cap.body.isKinematic);
            Assert.AreSame(m_Cap.homeParent, m_Cap.transform.parent);

            // Re-seat: starts again from zero with the gap
            yield return SeatPart(m_Cap, m_Seat, Quaternion.identity);
            Assert.AreEqual(gap, Gap(), 5e-4f);
            Assert.AreEqual(0f, m_Twist.progressDegrees, 1e-3f);
            Assert.IsFalse(m_Twist.capLocked);
        }
            [UnityTest]
        public IEnumerator Cap_Stays_Square_To_The_Tube_When_The_Rocket_Was_Turned_After_Seating()
        {
            yield return SeatCap();
            // the rocket is held at an angle now, not as it was when the cap went on
            var tube = Tube.transform;
            tube.rotation = Quaternion.Euler(40f, 25f, 0f) * tube.rotation;
            if (Tube.body != null) Tube.body.rotation = tube.rotation;
            yield return null; yield return null;
            Assert.Less(Vector3.Angle(m_Cap.transform.up, m_Seat.transform.up), 1f, "seated cap follows the rocket");

            yield return GrabSeatedCapOnOrbit();
            for (var a = 0f; a < 120f; a += 20f)
            {
                yield return Turn(a, a + 20f, 10f);
                Assert.Less(Vector3.Angle(m_Cap.transform.up, m_Seat.transform.up), 1f, "the cap turns about the tube's axis at " + (a + 20f) + " deg");
                Assert.AreEqual(0f, Vector3.ProjectOnPlane(m_Cap.transform.position - m_Seat.transform.position, m_Seat.transform.up).magnitude, 1e-3f, "and stays centred on it");
            }
            Assert.AreEqual(120f, m_Twist.progressDegrees, 2f);
            Release(m_Cap);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(PartState.Attached, m_Cap.state);
            Assert.Less(Vector3.Angle(m_Cap.transform.up, m_Seat.transform.up), 1f, "left square to the tube when let go");
            Assert.AreEqual(120f, m_Twist.progressDegrees, 2f, "progress kept");
        }

        [UnityTest]
        public IEnumerator Cap_Let_Go_Nearly_Closed_Finishes_The_Lock()
        {
            yield return SeatCap();
            yield return GrabSeatedCapOnOrbit();
            yield return Turn(0f, tuning.capLockAngle * tuning.capAutoFinishFraction + 5f);
            Assert.IsFalse(m_Twist.capLocked, "not locked yet while held");
            Release(m_Cap);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(m_Twist.capLocked, "letting go nearly closed finishes the lock");
            Assert.AreEqual(1, Count("locked"), "with the lock click");
            Assert.AreEqual(0f, Gap(), 5e-4f, "gap closed");
        }

        [UnityTest]
        public IEnumerator A_Hand_At_The_Cap_Takes_The_Cap_Not_The_Rocket()
        {
            yield return SeatCap();
            controller.position = m_Cap.transform.position + m_Seat.GuideAxisWorld * 0.02f;
            yield return null;
            Assert.IsTrue(assembly.CanGrab(m_Cap, hand), "the unlocked cap can be gripped");
            Assert.IsFalse(assembly.CanGrab(Tube, hand), "a hand at the cap does not take the whole rocket");
            controller.position = Tube.transform.position + Tube.transform.up * 0.15f;
            yield return null;
            Assert.IsTrue(assembly.CanGrab(Tube, hand), "away from the cap the rocket is grabbed as before");
        }
    }
}
