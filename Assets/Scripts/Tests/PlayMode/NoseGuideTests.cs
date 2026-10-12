using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// Drives the guided attach mechanic of SPEC.md section 5.2 on the nose cone: free, approach glow, capture, axis lock,
    /// break away, seat, remove, drop.
    /// </summary>
    public class NoseGuideTests : DevSceneTestBase
    {
        [UnityTest]
        public IEnumerator NoseCone_ApproachCaptureSlideBreakSeatAndRemove()
        {
            var nose = Part(PartType.NoseCone);
            var guide = nose.GetComponent<GuideGrabTransformer>();
            var seat = Point("NoseSeat");
            var seatPos = seat.transform.position;
            Vector3 Rel() => nose.transform.position - seatPos;

            var glowR = tuning.glowRadius;
            var captureR = tuning.captureRadius;
            var guideLen = tuning.guideLengthNose;
            var breakR = tuning.breakRadius;
            var magnet = tuning.noseMagnetism;
            // displayed fraction at a given depth: fades from noseMagnetism at the seat to 1 at the guide's outer end
            float Fraction(float depth) => Mathf.Lerp(magnet, 1f, Mathf.Clamp01(depth / guideLen));

            yield return Grab(nose);
            Assert.AreEqual(PartState.Free, nose.state);
            Assert.IsFalse(guide.isGuided);

            // Free, far: follows the hand, no glow
            yield return MoveHand(seatPos + new Vector3(0.4f, 0.1f, 0f) + grabOffset);
            Assert.Less(Vector3.Distance(nose.transform.position, controller.position - grabOffset), 0.01f, "free part follows the hand");
            Assert.IsFalse(seat.glowRenderer.enabled, "no glow outside glowRadius");

            // Approach: inside glowRadius, outside captureRadius
            yield return MoveHand(seatPos + Vector3.up * (glowR * 0.5f) + grabOffset);
            Assert.IsTrue(seat.glowRenderer.enabled, "glow inside glowRadius");
            Assert.IsFalse(guide.isGuided, "not yet captured");

            // Capture: inside captureRadius, upright
            var d1 = captureR * 0.6f;
            yield return MoveHand(seatPos + new Vector3(0.01f, d1, 0f) + grabOffset);
            Assert.IsTrue(guide.isGuided, "guide engages inside captureRadius");
            Assert.AreEqual(PartState.Guided, nose.state);
            CollectionAssert.Contains(events, "engaged:NoseCone_1@NoseSeat");
            Assert.Less(Mathf.Abs(Rel().x) + Mathf.Abs(Rel().z), 1e-3f, "on the axis (rel " + Rel().ToString("F4") + ", seat now " + seat.transform.position.ToString("F4") + ", captured seatPos " + seatPos.ToString("F4") + ", tube " + Tube.transform.position.ToString("F4") + " rot " + Tube.transform.eulerAngles.ToString("F1") + ", hand " + controller.position.ToString("F4") + ", point " + (nose.attachedTo != null ? nose.attachedTo.pointName : "guided:" + (guide.isGuided ? "yes" : "no")) + ")");
            Assert.AreEqual(d1 * Fraction(d1), Rel().y, 2e-3f, "displayed depth follows the faded magnetism");
            Assert.Less(Vector3.Angle(nose.transform.up, seat.transform.up), 0.5f, "rotation locked upright");

            // Slide with a sideways hand offset inside breakRadius: stays on the axis
            yield return MoveHand(seatPos + new Vector3(breakR * 0.4f, 0.02f, 0f) + grabOffset);
            Assert.IsTrue(guide.isGuided);
            Assert.Less(Mathf.Abs(Rel().x) + Mathf.Abs(Rel().z), 1e-3f, "sideways hand motion does not move the part off the axis");
            Assert.AreEqual(0.02f * Fraction(0.02f), Rel().y, 2e-3f);

            // Near the outer end the nose follows the hand almost exactly
            var d2 = guideLen * 0.95f;
            yield return MoveHand(seatPos + Vector3.up * d2 + grabOffset);
            Assert.IsTrue(guide.isGuided);
            Assert.AreEqual(d2 * Fraction(d2), Rel().y, 2e-3f);
            Assert.Greater(Rel().y, d2 * 0.9f, "pull has faded out at the outer end");
            Assert.GreaterOrEqual(CountPrefix("slideTick:NoseCone_1"), 3, "sliding along the guide ticks the slot texture haptic");
            Assert.AreEqual(1, CountPrefix("grabbed:NoseCone_1"), "pick-up haptic once");

            // Break away: sideways beyond breakRadius
            yield return MoveHand(seatPos + new Vector3(breakR * 1.5f, 0.02f, 0f) + grabOffset);
            Assert.IsFalse(guide.isGuided, "guide lets go beyond breakRadius");
            Assert.AreEqual(PartState.Free, nose.state);
            CollectionAssert.Contains(events, "released:NoseCone_1@NoseSeat");
            Assert.Less(Vector3.Distance(nose.transform.position, controller.position - grabOffset), 0.01f, "follows the hand again");

            // Re-capture and release: seats
            yield return MoveHand(seatPos + Vector3.up * (captureR * 0.6f) + grabOffset);
            Assert.IsTrue(guide.isGuided);
            Release(nose);
            yield return new WaitForSeconds(tuning.seatDuration + 0.3f);
            Assert.AreEqual(PartState.Attached, nose.state);
            Assert.AreSame(seat, nose.attachedTo);
            Assert.IsTrue(seat.isOccupied);
            Assert.AreSame(seat.transform, nose.transform.parent, "attached parts are parented under the tube");
            Assert.Less(nose.transform.localPosition.magnitude, 1e-4f, "flush on the seat");
            Assert.IsTrue(nose.body.isKinematic);
            Assert.IsFalse(seat.glowRenderer.enabled, "glow off once seated");
            CollectionAssert.Contains(events, "seated:NoseCone_1@NoseSeat");
            var tubeCollider = Tube.GetComponent<Collider>();
            Assert.IsTrue(Physics.GetIgnoreCollision(nose.GetComponent<Collider>(), tubeCollider), "attached part ignores the rocket");
            var feedback = workstation.GetComponent<AssemblyFeedback>();
            var playedSeatClip = false;
            foreach (var src in feedback.GetComponentsInChildren<AudioSource>())
                if (src.clip == feedback.library.noseSeat) playedSeatClip = true;
            Assert.IsTrue(playedSeatClip, "nose seat clip was queued on a pooled 3D source");

            // Remove: grab the seated nose, it re-enters the guide at depth 0, pulling past guideLength detaches
            yield return Grab(nose);
            Assert.IsTrue(guide.isGuided, "grabbing an attached part re-enters the guide");
            Assert.AreEqual(PartState.Guided, nose.state);
            var d3 = guideLen * 0.5f;
            yield return MoveHand(seatPos + Vector3.up * d3 + grabOffset);
            Assert.IsTrue(guide.isGuided, "inside guideLength it slides");
            Assert.AreEqual(d3 * Fraction(d3), Rel().y, 2e-3f);
            yield return MoveHand(seatPos + Vector3.up * (guideLen * 1.6f) + grabOffset);
            Assert.IsFalse(guide.isGuided, "pulled past guideLength detaches");
            Assert.AreEqual(PartState.Free, nose.state);
            Assert.IsFalse(seat.isOccupied);
            CollectionAssert.Contains(events, "removed:NoseCone_1@NoseSeat");
            Assert.IsTrue(Physics.GetIgnoreCollision(nose.GetComponent<Collider>(), tubeCollider), "parts never collide with the rocket, so a loose part cannot shove it (RocketAssembly.ApplyPartCollisionRules)");

            // Drop it: dynamic again, back in its tray group, and it falls
            Release(nose);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(nose.body.isKinematic, "dropped part is dynamic");
            Assert.AreSame(nose.homeParent, nose.transform.parent, "dropped part returns to its tray group, not the attach point");
            Assert.Less(nose.transform.position.y, seatPos.y + guideLen * 1.6f - 0.03f, "it fell");
        }

        [UnityTest]
        public IEnumerator NoseCone_EngageAndBreakAwayDoNotJump()
        {
            var nose = Part(PartType.NoseCone);
            var guide = nose.GetComponent<GuideGrabTransformer>();
            var seat = Point("NoseSeat");
            var seatPos = seat.transform.position;
            var captureR = tuning.captureRadius;

            yield return Grab(nose);
            // Approach along the axis to just outside capture, then step just inside with a sideways offset:
            // the part must ease onto the axis over guideBlendDuration rather than snap.
            yield return MoveHand(seatPos + Vector3.up * (captureR * 1.3f) + grabOffset);
            var before = nose.transform.position;
            controller.position = seatPos + new Vector3(captureR * 0.5f, captureR * 0.5f, 0f) + grabOffset;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.IsTrue(guide.isGuided, "captured");
            var twoFramesLater = nose.transform.position;
            var perFrameStep = Vector3.Distance(before, twoFramesLater) / 2f;
            Assert.Less(perFrameStep, 0.02f, "no visible jump on engage: the part eases toward the axis");
            yield return new WaitForSeconds(tuning.guideBlendDuration + 0.2f);
            Assert.Less(Mathf.Abs(nose.transform.position.x - seatPos.x), 1e-3f, "settled on the axis after the blend");

            // Break away sideways: must also ease back to the hand
            var guidedPos = nose.transform.position;
            controller.position = seatPos + new Vector3(tuning.breakRadius * 1.5f, captureR * 0.5f, 0f) + grabOffset;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.IsFalse(guide.isGuided, "broke away");
            var afterBreak = nose.transform.position;
            Assert.Less(Vector3.Distance(guidedPos, afterBreak), tuning.breakRadius * 1.5f * 0.6f, "no visible jump on break away");
            yield return new WaitForSeconds(tuning.guideBlendDuration + 0.3f);
            Assert.Less(Vector3.Distance(nose.transform.position, controller.position - grabOffset), 0.01f, "caught up with the hand");
            Release(nose);
            yield return null;
        }
    }
}
