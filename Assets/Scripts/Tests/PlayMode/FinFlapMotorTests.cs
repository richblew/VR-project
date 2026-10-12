using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>M4 fins, M5 flaps, M6 motor rejection and gating (SPEC.md 5.3, 5.5, 12.1).</summary>
    public class FinFlapMotorTests : DevSceneTestBase
    {
        [UnityTest]
        public IEnumerator Fins_AnyOrder_RollCorrected_Removable()
        {
            var slot3 = Point("FinSlot_3");
            var fin1 = Part(PartType.TailFin, 1);
            // held rolled 150 degrees about its outward axis: the slot corrects it
            yield return SeatPart(fin1, slot3, slot3.transform.rotation * Quaternion.Euler(0f, 0f, 150f));
            Assert.Less(Quaternion.Angle(fin1.transform.rotation, slot3.transform.rotation), 1f, "roll corrected to the slot");
            Assert.Less(fin1.transform.localPosition.magnitude, 1e-4f, "flush in the slot");
            yield return SeatPart(Part(PartType.TailFin, 2), Point("FinSlot_1"));
            yield return SeatPart(Part(PartType.TailFin, 3), Point("FinSlot_2"));
            Assert.AreEqual(3, assembly.machine.finsAttached);
            Assert.AreEqual(3, CountPrefix("engaged:TailFin"));
            Assert.AreEqual(3, CountPrefix("seated:TailFin"));
            Assert.AreEqual(AssemblyState.BuildingAirframe, assembly.State);

            // an occupied slot does not glow for another fin: there are only three, so check the point itself refuses
            Assert.IsFalse(slot3.Accepts(fin1), "occupied slot accepts nothing");

            // pull one back out along the slot axis
            var fin2 = Part(PartType.TailFin, 2);
            yield return RemovePart(fin2);
            Assert.AreEqual(2, assembly.machine.finsAttached);
            Assert.AreEqual(1, CountPrefix("removed:TailFin"));
            Assert.IsFalse(Point("FinSlot_1").isOccupied);
            Assert.AreSame(fin2.homeParent, fin2.transform.parent);
        }

        [UnityTest]
        public IEnumerator Flaps_SixSlots_EitherWayUp_PlacementsRecorded_FeedbackIdentical()
        {
            var low2 = Point("FlapSlot_Low_2");
            var mid1 = Point("FlapSlot_Mid_1");
            var mid3 = Point("FlapSlot_Mid_3");
            yield return SeatPart(Part(PartType.WingFlap, 1), low2, low2.transform.rotation);                                  // low, up
            yield return SeatPart(Part(PartType.WingFlap, 2), mid1, mid1.transform.rotation * Quaternion.Euler(0f, 0f, 180f)); // mid, upside down
            yield return SeatPart(Part(PartType.WingFlap, 3), mid3, mid3.transform.rotation);                                  // mid, up
            var report = assembly.GetReport();
            Assert.AreEqual(FlapPosition.Base, report.flapPlacement[0].position);
            Assert.AreEqual(FlapOrientation.Up, report.flapPlacement[0].orientation);
            Assert.AreEqual("FlapSlot_Low_2", report.flapPlacement[0].slotName);
            Assert.AreEqual(FlapPosition.Midpoint, report.flapPlacement[1].position);
            Assert.AreEqual(FlapOrientation.Down, report.flapPlacement[1].orientation);
            Assert.AreEqual(FlapPosition.Midpoint, report.flapPlacement[2].position);
            Assert.AreEqual(FlapOrientation.Up, report.flapPlacement[2].orientation);
            Assert.IsTrue(report.FlapError);
            CollectionAssert.AreEqual(new[] { "MotorCap", "WingFlap_1", "WingFlap_2" }, report.failedComponents);
            // whichever way up it was held is kept, and the upside-down flap still shares the slot's outward axis
            var flap2 = Part(PartType.WingFlap, 2);
            Assert.Less(Vector3.Angle(flap2.transform.forward, mid1.transform.forward), 1f);
            Assert.Less(Vector3.Angle(flap2.transform.up, -mid1.transform.up), 1f, "kept upside down");
            // identical feedback: every placement produced exactly one engage and one seat, wrong or right
            for (var i = 1; i <= 3; i++)
            {
                Assert.AreEqual(1, CountPrefix("engaged:WingFlap_" + i), "flap " + i + " engaged once");
                Assert.AreEqual(1, CountPrefix("seated:WingFlap_" + i), "flap " + i + " seated once");
            }
            // removing a flap clears its record
            yield return RemovePart(Part(PartType.WingFlap, 1));
            Assert.AreEqual("", assembly.GetReport().flapPlacement[0].slotName);
        }

        [UnityTest]
        public IEnumerator Motor_RefusedBeforeAirframe_RefusedNozzleUp_SeatsNozzleDown_ThenCapGating()
        {
            assembly.enforceOrder = true;   // this test covers the ordered build; the shipped default is a free order
            var motor = Part(PartType.Motor);
            var seat = Point("MotorSeat");
            var guide = motor.GetComponent<GuideGrabTransformer>();
            var ring = seat.glowRenderer;

            // 1. order enforced: a bare tube refuses the motor, with no glow and no engage
            yield return Grab(motor);
            yield return PlaceHeld(seat.transform.position + seat.GuideAxisWorld * (tuning.captureRadius * 0.5f), Quaternion.identity);
            Assert.IsFalse(guide.isGuided, "motor refused before the airframe is complete");
            Assert.IsFalse(ring.enabled, "no glow when gated off");
            Assert.AreEqual(0, CountPrefix("engaged:Motor"));
            Release(motor);
            yield return new WaitForSeconds(0.3f);

            // 2. complete the airframe
            yield return BuildAirframe();
            Assert.AreEqual(AssemblyState.AirframeComplete, assembly.State);

            // 3. nozzle-up is rejected completely
            yield return Grab(motor);
            yield return PlaceHeld(seat.transform.position + seat.GuideAxisWorld * (tuning.captureRadius * 0.5f), Quaternion.Euler(180f, 0f, 0f));
            Assert.IsFalse(guide.isGuided, "reversed motor never engages");
            Assert.IsFalse(ring.enabled, "reversed motor gets no glow");
            Assert.AreEqual(0, CountPrefix("engaged:Motor"));

            // 4. nozzle-down engages, and on release travels up by itself over about 0.3 s
            yield return PlaceHeld(seat.transform.position + seat.GuideAxisWorld * (tuning.captureRadius * 0.5f), Quaternion.identity);
            Assert.IsTrue(guide.isGuided, "upright motor engages");
            Assert.IsTrue(ring.enabled);
            Release(motor);
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(PartState.Guided, motor.state, "still travelling at 0.15 s of a 0.30 s seat");
            yield return new WaitForSeconds(tuning.seatDurationMotor + 0.2f);
            Assert.AreEqual(PartState.Attached, motor.state);
            Assert.AreEqual(AssemblyState.MotorFitted, assembly.State);
            Assert.Less(motor.transform.localPosition.magnitude, 1e-4f, "seated 11 mm below the rim as the attach point says");

            // 5. airframe parts are now locked in, the motor is not
            bool can = true;
            yield return CanGrabFromNear(Part(PartType.TailFin, 1), r => can = r);
            Assert.IsFalse(can, "fins cannot be removed once the motor is seated");
            yield return CanGrabFromNear(motor, r => can = r);
            Assert.IsTrue(can, "the motor can still be removed before the cap");

            // 6. cap seats, then the motor is locked in and the prototype is complete
            yield return SeatPart(Part(PartType.MotorCap), Point("CapSeat"), Quaternion.identity);
            Assert.AreEqual(AssemblyState.PrototypeComplete, assembly.State);
            Assert.AreEqual(1, Count("prototypeComplete"));
            yield return CanGrabFromNear(motor, r => can = r);
            Assert.IsFalse(can, "the motor cannot be removed while the cap is seated");
            yield return CanGrabFromNear(Part(PartType.MotorCap), r => can = r);
            Assert.IsTrue(can, "the cap can still be gripped (twist)");

            // 7. backward move: pulling the cap off returns to MotorFitted
            yield return RemovePart(Part(PartType.MotorCap));
            Assert.AreEqual(AssemblyState.MotorFitted, assembly.State);
        }

        [UnityTest]
        public IEnumerator Cap_RefusedBeforeMotor_AndEnforceOrderOff_MotorAcceptedEarly()
        {
            var cap = Part(PartType.MotorCap);
            var capSeat = Point("CapSeat");
            yield return Grab(cap);
            yield return PlaceHeld(capSeat.transform.position + capSeat.GuideAxisWorld * (tuning.captureRadius * 0.5f), Quaternion.identity);
            Assert.IsFalse(cap.GetComponent<GuideGrabTransformer>().isGuided, "cap refused before the motor");
            Assert.IsFalse(capSeat.glowRenderer.enabled);
            Release(cap);
            yield return new WaitForSeconds(0.3f);

            assembly.enforceOrder = false;
            yield return SeatPart(Part(PartType.Motor), Point("MotorSeat"), Quaternion.identity);
            Assert.AreEqual(AssemblyState.MotorFitted, assembly.State, "with order off the motor seats on a bare tube");
            Assert.IsTrue(capSeat.Accepts(cap), "the cap needs the motor, which is now there");
            Assert.IsTrue(Point("FinSlot_1").Accepts(Part(PartType.TailFin, 1)), "airframe points stay open with order off");
        }
    }
}
