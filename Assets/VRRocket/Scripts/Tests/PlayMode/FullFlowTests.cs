using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// M7 on the bench: the tube starts flat on the tray and is always grabbable, parts stay attached while the whole rocket is
    /// carried, submission stub, report, inspect-only return, new build, abort return to the bench, dropped rocket (SPEC.md 5.6, 8.3, 8.4, 8.5).
    /// </summary>
    public class FullFlowTests : DevSceneTestBase
    {
        IEnumerator BuildPrototype()
        {
            yield return BuildAirframe();
            yield return SeatPart(Part(PartType.Motor), Point("MotorSeat"), Quaternion.identity);
            yield return SeatPart(Part(PartType.MotorCap), Point("CapSeat"), Quaternion.identity);
            Assert.AreEqual(AssemblyState.PrototypeComplete, assembly.State);
        }

        Vector3 Home()
        {
            Assert.IsTrue(workstation.TryGetTubeHome(out var p, out _), "the tray layout has a slot for the tube");
            return p;
        }

        void AssertAtHome(RocketPart tube, string why)
        {
            var home = Home();
            var d = tube.transform.position - home;
            Assert.Less(new Vector2(d.x, d.z).magnitude, 0.03f, why + " (horizontal offset " + new Vector2(d.x, d.z).magnitude.ToString("F3") + ", tube at " + tube.transform.position.ToString("F3") + " local " + tube.transform.localPosition.ToString("F3") + " parent " + (tube.transform.parent != null ? tube.transform.parent.name : "none") + ", home " + home.ToString("F3") + ", kinematic " + tube.body.isKinematic + ")");
            Assert.Less(Mathf.Abs(d.y), 0.1f, why + " (height offset " + d.y.ToString("F3") + ")");
        }

        IEnumerator CarryToZone(RocketPart tube, SubmissionZoneStub zone)
        {
            yield return Grab(tube);
            yield return PlaceHeld(zone.GetComponent<BoxCollider>().bounds.center + Vector3.down * 0.1f, Quaternion.identity);
            Release(tube);
            yield return new WaitForSeconds(0.6f);
        }

        [UnityTest]
        public IEnumerator Build_Carry_Submit_FailedLaunch_NewBuild()
        {
            var tube = Tube;
            Assert.AreEqual(PartState.Free, tube.state, "the tube is a loose part, not clamped anywhere");
            var can = false;
            yield return CanGrabFromNear(tube, r => can = r);
            Assert.IsTrue(can, "the tube can be picked up at any time");

            yield return BuildPrototype();
            Assert.AreEqual(1, Count("prototypeComplete"), "prototype complete fired once");
            var fin = Part(PartType.TailFin, 1);
            Assert.AreSame(tube, fin.attachedTo.tube, "parts are children of the rocket");
            Assert.IsTrue(assembly.CanGrab(fin), "in the free build order any part comes off a held rocket");
            Assert.IsTrue(assembly.CanGrab(Part(PartType.MotorCap)), "the cap can still be twisted");
            foreach (var p in tube.GetComponentsInChildren<RocketPart>())
                if (p != tube) Assert.AreEqual(RigidbodyInterpolation.None, p.body.interpolation, p.name + " is not interpolated while attached");

            // carry it to the submission zone and let go there
            var zone = Object.FindFirstObjectByType<SubmissionZoneStub>();
            Assert.IsNotNull(zone, "submission zone stub in the dev scene");
            yield return CarryToZone(tube, zone);
            Assert.IsTrue(zone.rocketInside, "a loose complete rocket is inside the zone");
            foreach (var p in tube.GetComponentsInChildren<RocketPart>())
                if (p != tube) Assert.AreEqual(PartState.Attached, p.state, p.name + " stays attached after the carry");

            // submit
            BuildReport received = null;
            assembly.Submitted += r => received = r;
            zone.OnSubmitPressed();
            Assert.AreEqual(AssemblyState.Submitted, assembly.State);
            Assert.IsNotNull(received, "Submitted raised with a report");
            Assert.AreEqual(LaunchOutcome.MotorRetentionLoss, received.outcome, "cap was seated but never locked");
            Assert.IsFalse(received.FlapError);
            CollectionAssert.AreEqual(new[] { "MotorCap" }, received.failedComponents);
            Assert.IsTrue(assembly.isFrozen);
            Assert.IsFalse(assembly.CanGrab(tube), "frozen rocket");
            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(tube.gameObject.activeInHierarchy, "the stub bin took it away");

            // failed launch: inspect-only return plus a new build
            var readout = Object.FindFirstObjectByType<OutcomeReadoutStub>();
            Assert.IsNotNull(readout, "outcome readout stub in the dev scene");
            readout.OnFailedLaunchPressed();
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(tube.gameObject.activeInHierarchy, "the prototype came back");
            Assert.AreSame(tube, assembly.inspectPrototype);
            Assert.IsTrue(assembly.CanGrab(tube), "it can be picked up and examined");
            Assert.IsFalse(assembly.CanGrab(fin), "but not altered");
            Assert.IsFalse(tube.body.isKinematic, "rests as a loose object");
            Assert.AreNotSame(tube, assembly.tube, "a fresh tube is on the bench");
            AssertAtHome(assembly.tube, "the fresh tube lies at the tube's tray slot");
            Assert.IsFalse(assembly.tube.body.isKinematic, "and is a loose part");
            Assert.AreEqual(AssemblyState.BuildingAirframe, assembly.State);
            Assert.AreEqual(9, workstation.respawner.parts.Count, "nine fresh parts on the tray");
            Assert.AreEqual(1, Count("kitSpawned"), "fresh-kit haptic once");
            Assert.AreEqual(1, CountPrefix("grabbed:BodyTube"), "rocket heft haptic once");
            Assert.GreaterOrEqual(CountPrefix("releasedPart:BodyTube"), 1, "rocket let-go haptic");
            foreach (var p in workstation.respawner.parts) Assert.AreEqual(PartState.Free, p.state, p.name + " is loose");
            Assert.IsTrue(Point("FinSlot_1").Accepts(Part(PartType.TailFin, 1)), "the new tube accepts parts");
            // only one inspection prototype at a time: the old inspect rocket's points are gated off
            foreach (var ap in tube.GetComponentsInChildren<AttachPoint>()) Assert.IsFalse(ap.gatingAllows, ap.pointName + " of the inspection prototype is dead");
        }

        [UnityTest]
        public IEnumerator Submit_Abort_ReturnsEditableToBench()
        {
            var tube = Tube;
            yield return BuildPrototype();
            var zone = Object.FindFirstObjectByType<SubmissionZoneStub>();
            yield return CarryToZone(tube, zone);
            zone.OnSubmitPressed();
            Assert.AreEqual(AssemblyState.Submitted, assembly.State);

            var readout = Object.FindFirstObjectByType<OutcomeReadoutStub>();
            readout.OnAbortPressed();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(AssemblyState.PrototypeComplete, assembly.State, "the same rocket is editable again");
            Assert.IsTrue(tube.gameObject.activeInHierarchy);
            Assert.IsFalse(assembly.isFrozen);
            Assert.IsFalse(tube.body.isKinematic, "back as a loose rocket");
            AssertAtHome(tube, "returned to its bench spot");
            Assert.AreSame(tube, Part(PartType.NoseCone).attachedTo.tube, "parts came back with it");
            Assert.IsFalse(assembly.CanGrab(Part(PartType.MotorCap)), "a resting rocket is grabbed as a whole: no part can be picked off it");
            StandTubeUpright();   // the player picks the rocket up with the other hand
            Assert.IsTrue(assembly.CanGrab(Part(PartType.MotorCap)), "held: the cap is editable");
            Assert.IsTrue(assembly.CanGrab(Part(PartType.TailFin, 1)), "held: fins come off too in the free build order");
            // and it can be corrected: pull the cap, which is a backward move
            yield return RemovePart(Part(PartType.MotorCap));
            Assert.AreEqual(AssemblyState.MotorFitted, assembly.State);
        }

        [UnityTest]
        public IEnumerator AttachedParts_FollowTheCarriedTube_AndDroppedPartsRespawn()
        {
            var tube = Tube;
            yield return SeatPart(Part(PartType.NoseCone), Point("NoseSeat"), Quaternion.identity);
            yield return SeatPart(Part(PartType.TailFin, 1), Point("FinSlot_1"));
            var nose = Part(PartType.NoseCone);
            var fin = Part(PartType.TailFin, 1);
            Assert.AreSame(Point("NoseSeat").transform, nose.transform.parent, "the seated nose is a child of its seat");
            var noseRel = tube.transform.InverseTransformPoint(nose.transform.position);
            var finRel = tube.transform.InverseTransformPoint(fin.transform.position);

            // carry the tube somewhere else, with the hand, and turn it
            yield return Grab(tube);
            var target = tube.transform.position + new Vector3(0.25f, 0.15f, -0.2f);
            var rot = Quaternion.Euler(0f, 60f, 30f);
            yield return PlaceHeld(target, rot);
            yield return PlaceHeld(target + new Vector3(0f, 0.05f, 0.1f), rot);
            Assert.Less(Vector3.Distance(tube.transform.InverseTransformPoint(nose.transform.position), noseRel), 0.003f, "the nose moved with the tube (tube " + tube.transform.position.ToString("F3") + ", nose " + nose.transform.position.ToString("F3") + ", nose parent " + (nose.transform.parent != null ? nose.transform.parent.name : "none") + ", kinematic " + nose.body.isKinematic + ")");
            Assert.Less(Vector3.Distance(tube.transform.InverseTransformPoint(fin.transform.position), finRel), 0.003f, "the fin moved with the tube (parent " + (fin.transform.parent != null ? fin.transform.parent.name : "none") + ")");
            Assert.AreEqual(PartState.Attached, nose.state);
            Release(tube);
            yield return new WaitForSeconds(0.5f);
            Assert.Less(Vector3.Distance(tube.transform.InverseTransformPoint(nose.transform.position), noseRel), 0.003f, "the nose stayed on the tube after the drop (parent " + (nose.transform.parent != null ? nose.transform.parent.name : "none") + ")");

            // a loose part knocked off the bench comes back on the respawn pad by itself
            var flap = Part(PartType.WingFlap, 1);
            yield return Grab(flap);
            yield return PlaceHeld(workstation.transform.position + new Vector3(0.9f, -0.6f, 0f), Quaternion.identity);
            Release(flap);
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(flap.transform.position.y, workstation.transform.position.y - 0.05f, "a part dropped below the bench is back on the bench (y " + flap.transform.position.y.ToString("F3") + ")");
            Assert.IsTrue(workstation.respawner.Contains(flap.transform.position), "and inside the workstation bounds");
        }

        /// <summary>Moves the hand a little every frame and measures, after each frame has rendered, how far the nose is from where the tube says it should be.</summary>
        [UnityTest]
        public IEnumerator AttachedParts_DoNotLagBehindTheRocket_FrameByFrame()
        {
            var tube = Tube;
            yield return SeatPart(Part(PartType.NoseCone), Point("NoseSeat"), Quaternion.identity);
            var nose = Part(PartType.NoseCone);
            var seat = Point("NoseSeat").transform;
            var expectedLocal = seat.InverseTransformPoint(nose.transform.position);
            yield return Grab(tube);
            var start = controller.position;
            var worst = 0f; var worstFrame = -1; var samples = 0;
            for (var frame = 0; frame < 90; frame++)
            {
                // a steady hand sweep of about 0.6 m/s with a slow turn, like carrying the rocket to the bin
                var k = frame / 90f;
                controller.position = start + new Vector3(0.5f * k, 0.15f * Mathf.Sin(k * 6f), -0.3f * k);
                controller.rotation = Quaternion.Euler(0f, 90f * k, 20f * Mathf.Sin(k * 4f));
                yield return new WaitForEndOfFrame();
                if (frame < 10) continue;   // let the grab settle
                var err = Vector3.Distance(nose.transform.position, seat.TransformPoint(expectedLocal));
                samples++;
                if (err > worst) { worst = err; worstFrame = frame; }
            }
            Assert.Greater(samples, 50, "sampled frames");
            Assert.Less(worst, 0.002f, "the nose is rendered where the tube says it is, every frame (worst " + (worst * 1000f).ToString("F1") + " mm at frame " + worstFrame + ")");
            Release(tube);
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTest]
        public IEnumerator DroppedRocket_ReturnsToBench()
        {
            var tube = Tube;
            yield return BuildPrototype();
            yield return Grab(tube);
            // drop it below the floor level
            yield return PlaceHeld(new Vector3(0f, 0.1f, 0.85f), Quaternion.identity);
            Release(tube);
            yield return new WaitForSeconds(0.6f);
            AssertAtHome(tube, "a dropped rocket comes back to its bench spot");
            Assert.AreEqual(AssemblyState.PrototypeComplete, assembly.State, "still complete");
            Assert.AreSame(tube, Part(PartType.NoseCone).attachedTo.tube, "parts came back with it");
        }
    }
}
