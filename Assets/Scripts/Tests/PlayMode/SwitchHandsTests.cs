using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// Switching hands (spec 03, section 3): with the rocket in the left hand and the right gripping one of its parts, letting go
    /// with the left leaves the rocket in the right, the part back on its seat. Taking a part off with the rocket held is unchanged,
    /// and a part already pulled free never takes the rocket with it.
    /// </summary>
    public class SwitchHandsTests : DevSceneTestBase
    {
        /// <summary>Anything a scene needs before building can start.</summary>
        protected virtual IEnumerator Prepare() { yield break; }

        bool RocketInRightHand => Tube.grabInteractable.interactorsSelecting.Contains(hand);

        IEnumerator FlapSeatedAndRocketInLeftHand(RocketPart flap, AttachPoint slot)
        {
            yield return Prepare();
            yield return SeatPart(flap, slot);
            yield return TakeLeftHand();
            yield return HoldTubeInLeftHand();
        }

        [UnityTest]
        public IEnumerator Letting_Go_Of_The_Rocket_While_The_Other_Hand_Grips_A_Seated_Flap_Leaves_The_Rocket_In_That_Hand()
        {
            var flap = Part(PartType.WingFlap, 1);
            var slot = Point("FlapSlot_Mid_1");
            yield return FlapSeatedAndRocketInLeftHand(flap, slot);
            yield return Grab(flap);
            Assert.AreEqual(PartState.Guided, flap.state, "gripping a seated flap with the rocket held puts it on its track, ready to pull off");
            var before = Tube.transform.position;
            var beforeRot = Tube.transform.rotation;
            var rocketGrabs = Count("grabbed:BodyTube");
            LetGoWithLeft();
            yield return null;
            Assert.IsTrue(RocketInRightHand, "the rocket stays in the hand that gripped the flap");
            Assert.IsFalse(flap.isHeld, "that hand lets go of the flap");
            Assert.AreEqual(rocketGrabs + 1, Count("grabbed:BodyTube"), "a rocket grab, with its vibration, on the hand that now holds it");
            // a hand-model rig moves the hand's attach point a frame or two later (the flap's grip offset going): hold still a while
            yield return new WaitForSeconds(0.2f);
            Assert.Less(Vector3.Distance(before, Tube.transform.position), 0.01f, "the rocket neither jumps nor drops");
            Assert.Less(Quaternion.Angle(beforeRot, Tube.transform.rotation), 2f, "nor turns in the hand");
            yield return new WaitForSeconds(tuning.SeatDurationFor(PartType.WingFlap) + 0.1f);
            Assert.AreEqual(PartState.Attached, flap.state, "the flap is back on its seat");
            Assert.AreSame(slot, flap.attachedTo);
            var start = Tube.transform.position;
            yield return MoveHand(controller.position + Vector3.right * 0.1f);
            Assert.AreEqual(0.1f, Vector3.Distance(start, Tube.transform.position), 0.01f, "the rocket moves with the right hand now");
            Assert.AreSame(slot, flap.attachedTo, "carrying its flap");
        }

        [UnityTest]
        public IEnumerator A_Flap_Half_Pulled_Out_Goes_Back_On_Its_Seat_When_The_Rocket_Changes_Hands()
        {
            var flap = Part(PartType.WingFlap, 2);
            var slot = Point("FlapSlot_Mid_2");
            yield return FlapSeatedAndRocketInLeftHand(flap, slot);
            yield return Grab(flap);
            var len = tuning.GuideLengthFor(PartType.WingFlap);
            yield return PlaceHeld(slot.transform.position + slot.GuideAxisWorld * len * 0.5f, flap.transform.rotation);
            var depth = Vector3.Dot(flap.transform.position - slot.transform.position, slot.GuideAxisWorld);
            Assert.Greater(depth, len * 0.3f, "the flap is part way out along its track");
            Assert.AreEqual(PartState.Guided, flap.state, "and not off yet");
            LetGoWithLeft();
            yield return null;
            Assert.IsTrue(RocketInRightHand, "the rocket goes to the hand on the flap");
            yield return new WaitForSeconds(tuning.SeatDurationFor(PartType.WingFlap) + 0.3f);
            Assert.AreEqual(PartState.Attached, flap.state, "the flap slides back onto its seat");
            Assert.AreSame(slot, flap.attachedTo);
            Assert.Less(Vector3.Distance(flap.transform.position, slot.transform.position), 0.003f, "all the way home");
        }

        [UnityTest]
        public IEnumerator A_Cap_Mid_Twist_Keeps_Its_Twist_When_The_Rocket_Changes_Hands()
        {
            yield return Prepare();
            yield return SeatPart(Part(PartType.Motor), Point("MotorSeat"), Quaternion.identity);
            yield return SeatPart(Part(PartType.MotorCap), Point("CapSeat"), Quaternion.identity);
            var cap = Part(PartType.MotorCap);
            var seat = Point("CapSeat");
            var twist = cap.GetComponent<MotorCapTwist>();
            yield return TakeLeftHand();
            yield return HoldTubeInLeftHand();
            yield return Grab(cap);
            yield return null;
            Assert.IsTrue(twist.isTwisting, "the right hand grips the cap to twist it");
            var startPos = controller.position; var startRot = controller.rotation;
            for (var a = 0f; a < 90f && twist.progressDegrees < 60f;)
            {
                a += 5f;
                var q = Quaternion.AngleAxis(-a, seat.transform.up);
                controller.position = seat.transform.position + q * (startPos - seat.transform.position);
                controller.rotation = q * startRot;
                yield return null; yield return null;
            }
            var progress = twist.progressDegrees;
            Assert.GreaterOrEqual(progress, 60f, "part of the way round");
            Assert.IsFalse(twist.capLocked);
            LetGoWithLeft();
            yield return null;
            Assert.IsTrue(RocketInRightHand, "the rocket goes to the hand on the cap, though a hand there normally takes the cap");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(PartState.Attached, cap.state, "the cap stays seated");
            Assert.AreEqual(progress, twist.progressDegrees, 0.01f, "and keeps its twist");
            Assert.IsFalse(twist.capLocked);
        }

        [UnityTest]
        public IEnumerator A_Flap_Pulled_Off_With_The_Rocket_Held_Stays_In_The_Hand_And_The_Rocket_Drops_When_Let_Go()
        {
            var flap = Part(PartType.WingFlap, 3);
            var slot = Point("FlapSlot_Mid_3");
            yield return FlapSeatedAndRocketInLeftHand(flap, slot);
            yield return Grab(flap);
            var len = tuning.GuideLengthFor(PartType.WingFlap);
            for (var attempt = 0; attempt < 3 && flap.state != PartState.Free; attempt++)
                yield return PlaceHeld(slot.transform.position + slot.GuideAxisWorld * (len * (1.6f + 0.4f * attempt)), flap.transform.rotation);
            Assert.AreEqual(PartState.Free, flap.state, "pulled past the end of its track, the flap comes off as before");
            Assert.AreEqual(1, Count("removed:WingFlap_3@FlapSlot_Mid_3"));
            Assert.IsTrue(Tube.grabInteractable.interactorsSelecting.Contains(leftHand), "the rocket stays in the left hand");
            var height = Tube.transform.position.y;
            LetGoWithLeft();
            yield return null;
            Assert.IsFalse(Tube.isHeld, "a part already off is just a part in the hand: letting go of the rocket lets it go");
            Assert.IsTrue(flap.isHeld, "the flap stays in the right hand");
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Tube.transform.position.y, height - 0.05f, "and the rocket drops");
        }
    }

    /// <summary>The same in the scene we ship, with Yi-Xuan's hand-model rig.</summary>
    public class SwitchHandsFinalSceneTests : SwitchHandsTests
    {
        protected override string scenePath => "Assets/Scenes/VRRocket_Final.unity";

        protected override IEnumerator Prepare()
        {
            var flow = Object.FindAnyObjectByType<GameFlowController>();
            flow.StartWithoutIntro();
            yield return new WaitForSeconds(0.2f);
        }
    }
}
