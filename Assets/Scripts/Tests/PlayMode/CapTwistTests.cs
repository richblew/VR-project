using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// SPEC.md section 5.4 on a bare tube with gating off: seat unlocked with the gap, ratchet by orbiting the hand clockwise
    /// (viewed from below), detent ticks, lock at capLockAngle with the gap closing, nothing after lock, early let-go keeps the
    /// seat, progress kept between grabs, pull-off resets.
    /// </summary>
    public class CapTwistTests : DevSceneTestBase
    {
        const float k_Orbit = 0.06f;   // hand radius from the tube axis while twisting (position mode)

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

        /// <summary>Hand position at a Unity yaw around the tube axis, at cap height, on the orbit radius.</summary>
        Vector3 Orbit(float unityYaw)
        {
            var seat = m_Seat.transform;
            return seat.position + m_Seat.GuideAxisWorld * 0.03f + Quaternion.AngleAxis(unityYaw, seat.up) * seat.right * k_Orbit;
        }

        /// <summary>Grabs the seated cap and moves the hand to the orbit start without turning.</summary>
        IEnumerator GrabSeatedCapOnOrbit()
        {
            yield return Grab(m_Cap);
            Assert.AreEqual(PartState.Attached, m_Cap.state, "gripping the seated cap does not unseat it");
            yield return MoveHand(Orbit(0f));
            Assert.IsTrue(m_Twist.isTwisting);
        }

        IEnumerator Turn(float fromYaw, float toYaw, float step = 20f)
        {
            var dir = Mathf.Sign(toYaw - fromYaw);
            for (var a = fromYaw; Mathf.Abs(toYaw - a) > 1e-3f;)
            {
                a = dir > 0 ? Mathf.Min(a + step, toYaw) : Mathf.Max(a - step, toYaw);
                yield return MoveHand(Orbit(a));
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
            yield return Turn(0f, -100f, 5f);
            Assert.AreEqual(100f, m_Twist.progressDegrees, 1.5f);
            Assert.AreEqual(3, Count("detent"), "ticks at 30, 60, 90");
            Assert.GreaterOrEqual(Count("twistTexture"), 6, "thread texture ticks between the detents");
            Assert.Less(Quaternion.Angle(m_Cap.transform.rotation, Quaternion.AngleAxis(-100f, up) * seatedRotation), 1.5f, "cap turned with the hand");
            Assert.AreEqual(gap, Gap(), 5e-4f, "still unlocked, gap stays");

            // Turning back does not unwind and makes no clicks
            yield return Turn(-100f, -50f);
            Assert.AreEqual(100f, m_Twist.progressDegrees, 1.5f);
            Assert.AreEqual(3, Count("detent"));
            Assert.IsFalse(m_Twist.capLocked);

            // Forward again accumulates like a ratchet, then locks exactly at capLockAngle
            yield return Turn(-50f, -140f);
            Assert.IsTrue(m_Twist.capLocked, "locked");
            Assert.AreEqual(tuning.capLockAngle, m_Twist.progressDegrees, 1e-3f, "locks exactly at capLockAngle, no overshoot");
            Assert.AreEqual(1, Count("locked"));
            Assert.AreEqual(5, Count("detent"), "120 and 150 ticked; the 180 detent is the lock click");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, Gap(), 5e-4f, "gap closes on lock");
            Assert.Less(Quaternion.Angle(m_Cap.transform.rotation, Quaternion.AngleAxis(-tuning.capLockAngle, up) * seatedRotation), 1.5f);

            // A locked cap can no longer be turned or removed
            yield return Turn(-140f, -200f);
            Assert.AreEqual(tuning.capLockAngle, m_Twist.progressDegrees, 1e-3f);
            Assert.AreEqual(5, Count("detent"));
            Assert.AreEqual(1, Count("locked"));
            yield return MoveHand(Orbit(-200f) + m_Seat.GuideAxisWorld * (tuning.capPullOff * 2f));
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
            yield return Turn(0f, -60f);
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
            yield return Turn(0f, -35f);
            Assert.AreEqual(95f, m_Twist.progressDegrees, 1.5f, "accumulates across grabs");
            Assert.AreEqual(3, Count("detent"), "90 ticked on the second grab");

            // Pull straight off: detaches, progress resets
            yield return MoveHand(Orbit(-35f) + m_Seat.GuideAxisWorld * (tuning.capPullOff * 1.5f));
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
    }
}
