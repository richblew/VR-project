using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// The menu, game flow and launch sequence wired into Env_ControlRoom: parts locked until START, build, carry to the bin,
    /// submit at the console, countdown, launch outside, result, and the returns of SPEC 8.4 (inspect-only after a failure,
    /// editable after an abort, new kit after a success). Timings are shortened; the flight itself runs at full length.
    /// </summary>
    public class GameFlowTests : DevSceneTestBase
    {
        const float k_Orbit = 0.06f;

        protected override string scenePath => "Assets/VRRocket/Scenes/Env_ControlRoom.unity";

        GameFlowController flow;
        SubmissionBin bin;
        LaunchSequence launch;
        MenuPanel menu;
        GameObject vehicle;

        IEnumerator FindFlow()
        {
            flow = Object.FindFirstObjectByType<GameFlowController>();
            bin = Object.FindFirstObjectByType<SubmissionBin>();
            launch = Object.FindFirstObjectByType<LaunchSequence>();
            menu = Object.FindFirstObjectByType<MenuPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(flow, "GameFlowController in Env_ControlRoom");
            Assert.IsNotNull(bin, "SubmissionBin in Env_ControlRoom");
            Assert.IsNotNull(launch, "LaunchSequence in Env_ControlRoom");
            Assert.IsNotNull(menu, "MenuPanel in Env_ControlRoom");
            vehicle = launch.transform.Find("LaunchVehicle").gameObject;
            flow.countdownSeconds = 1f;
            flow.resultSeconds = 1f;
            flow.submitToPadSeconds = 0.5f;
            yield return null;
        }

        IEnumerator WaitForPhase(GamePhase p, float timeout)
        {
            var t0 = Time.time;
            while (flow.phase != p && Time.time - t0 < timeout) yield return null;
            Assert.AreEqual(p, flow.phase, "phase " + p + " within " + timeout + " s");
        }

        IEnumerator BuildPrototype()
        {
            yield return BuildAirframe();
            yield return SeatPart(Part(PartType.Motor), Point("MotorSeat"), Quaternion.identity);
            yield return SeatPart(Part(PartType.MotorCap), Point("CapSeat"), Quaternion.identity);
            Assert.AreEqual(AssemblyState.PrototypeComplete, assembly.State);
        }

        Vector3 Orbit(AttachPoint seat, float unityYaw)
        {
            var t = seat.transform;
            return t.position + seat.GuideAxisWorld * 0.03f + Quaternion.AngleAxis(unityYaw, t.up) * t.right * k_Orbit;
        }

        /// <summary>Grabs the seated cap and twists it clockwise (viewed from below) past the lock angle, as CapTwistTests does.</summary>
        IEnumerator LockCap()
        {
            var cap = Part(PartType.MotorCap);
            var seat = Point("CapSeat");
            var twist = cap.GetComponent<MotorCapTwist>();
            yield return Grab(cap);
            Assert.AreEqual(PartState.Attached, cap.state, "gripping the seated cap does not unseat it");
            yield return MoveHand(Orbit(seat, 0f));
            Assert.IsTrue(twist.isTwisting);
            for (var a = 0f; a > -200f;)
            {
                a -= 5f;
                yield return MoveHand(Orbit(seat, a));
                if (twist.capLocked) break;
            }
            Assert.IsTrue(twist.capLocked, "cap locked");
            Release(cap);
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>Picks the finished rocket up from the bench, carries it over the bin opening and lets go.</summary>
        IEnumerator CarryToBin()
        {
            var tube = Tube;
            yield return Grab(tube);
            var binCol = bin.GetComponent<BoxCollider>();
            yield return PlaceHeld(binCol.bounds.center + Vector3.up * 0.15f, Quaternion.identity);
            Release(tube);
        }

        [UnityTest]
        public IEnumerator Menu_Start_Build_Submit_Launch_FailedCap_ReturnsForInspectionWithNewKit()
        {
            yield return FindFlow();
            Assert.AreEqual(GamePhase.Menu, flow.phase);
            Assert.IsTrue(menu.isShown, "the menu is up at start");
            Assert.IsFalse(vehicle.activeInHierarchy, "no vehicle on the pad yet");
            var fin = Part(PartType.TailFin, 1);
            var can = false;
            yield return CanGrabFromNear(fin, r => can = r);
            Assert.IsFalse(can, "parts are locked while the menu is up");

            flow.StartGame();
            yield return null;
            Assert.AreEqual(GamePhase.Assembly, flow.phase);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(menu.isShown, "menu hidden after START");
            yield return CanGrabFromNear(fin, r => can = r);
            Assert.IsTrue(can, "parts unlock after START");

            yield return BuildPrototype();            // cap seated but never locked: motor retention loss
            var tube = Tube;
            yield return CarryToBin();
            yield return new WaitForSeconds(tuning.respawnDelay + 1f);
            Assert.IsTrue(bin.rocketInside, "a loose complete rocket rests in the bin and is not pulled back to the bench");
            Assert.AreEqual(GamePhase.Assembly, flow.phase);

            BuildReport received = null;
            assembly.Submitted += r => received = r;
            bin.Submit();
            Assert.AreEqual(AssemblyState.Submitted, assembly.State);
            Assert.AreEqual(GamePhase.PreLaunch, flow.phase, "submission moves the flow to pre-launch");
            Assert.IsNotNull(received);
            Assert.AreEqual(LaunchOutcome.MotorRetentionLoss, received.outcome);
            flow.Abort(); flow.Abort();   // double press is harmless
            Assert.AreEqual(GamePhase.Assembly, flow.phase, "abort returns to assembly");
            Assert.AreEqual(AssemblyState.PrototypeComplete, assembly.State);
            Assert.IsFalse(assembly.isFrozen, "abort returns the same rocket to the bench, editable");
            Assert.IsTrue(tube.gameObject.activeInHierarchy);
            Assert.IsFalse(assembly.CanGrab(Part(PartType.MotorCap)), "the returned rocket rests on the bench and is grabbed as a whole");

            // submit again and launch this time
            yield return CarryToBin();
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(bin.rocketInside);
            bin.Submit();
            Assert.AreEqual(GamePhase.PreLaunch, flow.phase);
            flow.Launch();
            Assert.AreEqual(GamePhase.PreLaunch, flow.phase, "launch is refused until the vehicle is on the pad");
            yield return new WaitForSeconds(1.8f);   // sink animation and the pad transfer
            Assert.IsFalse(tube.gameObject.activeInHierarchy, "the bin took the prototype away");
            Assert.IsTrue(vehicle.activeInHierarchy, "the full-size vehicle stands on the pad");
            flow.Launch();
            Assert.AreEqual(GamePhase.Countdown, flow.phase, "launch starts the countdown from pre-launch");
            yield return WaitForPhase(GamePhase.Launch, 3f);
            Assert.IsTrue(launch.isRunning);
            yield return WaitForPhase(GamePhase.Result, 20f);
            Assert.IsTrue(launch.model.motorEjected, "retention loss: the motor left the rocket");
            Assert.Less(launch.altitude, 1f, "and it never lifted off");
            var title = GameObject.Find("ScreenTitle").GetComponent<TMPro.TextMeshPro>();
            Assert.AreEqual("LAUNCH FAILED", title.text);
            var diagram = title.transform.parent.Find("Diagram");
            Assert.IsTrue(diagram.gameObject.activeSelf, "failure diagram shown");

            yield return WaitForPhase(GamePhase.Assembly, 5f);
            Assert.AreEqual(1, flow.attempts);
            Assert.AreEqual(0, flow.successes);
            Assert.IsFalse(vehicle.activeInHierarchy, "pad cleared");
            Assert.IsTrue(tube.gameObject.activeInHierarchy, "the failed prototype came back");
            Assert.AreSame(tube, assembly.inspectPrototype);
            Assert.AreNotSame(tube, assembly.tube, "a fresh tube is on the bench");
            Assert.AreEqual(AssemblyState.BuildingAirframe, assembly.State);
            Assert.AreEqual(9, workstation.respawner.parts.Count, "nine fresh parts");
            Assert.IsTrue(assembly.interactionEnabled);
            Assert.Less(Vector3.Distance(tube.transform.position, bin.returnSpot.position), 0.35f, "returned at the console's return spot");
            yield return new WaitForSeconds(tuning.respawnDelay + 1f);
            Assert.Less(Vector3.Distance(tube.transform.position, bin.returnSpot.position), 0.35f, "and it stays there: the console counts as in bounds");
            Assert.IsFalse(diagram.gameObject.activeSelf, "diagram hidden again");
        }

        [UnityTest]
        public IEnumerator Menu_Start_Build_LockCap_Submit_Launch_Success_BackToMenuWithNewKit()
        {
            yield return FindFlow();
            flow.StartGame();
            yield return null;
            yield return BuildPrototype();
            yield return LockCap();
            Assert.AreEqual(LaunchOutcome.Success, assembly.GetReport().outcome, "a locked cap and correct flaps make a sound rocket");
            var tube = Tube;
            yield return CarryToBin();
            yield return new WaitForSeconds(0.6f);
            bin.Submit();
            Assert.AreEqual(GamePhase.PreLaunch, flow.phase);
            yield return new WaitForSeconds(1.8f);
            flow.Launch();
            yield return WaitForPhase(GamePhase.Launch, 3f);
            yield return WaitForPhase(GamePhase.Result, 40f);
            Assert.IsFalse(launch.model.hasExploded);
            Assert.GreaterOrEqual(launch.altitude, 1000f, "a clean flight climbs to the success altitude");
            Assert.AreEqual("MISSION SUCCESS", GameObject.Find("ScreenTitle").GetComponent<TMPro.TextMeshPro>().text);

            yield return WaitForPhase(GamePhase.Menu, 5f);
            Assert.AreEqual(1, flow.successes);
            Assert.IsTrue(menu.isShown, "the menu offers another build");
            Assert.IsFalse(vehicle.activeInHierarchy, "pad cleared");
            Assert.IsFalse(assembly.interactionEnabled, "parts locked again until START");
            Assert.AreEqual(AssemblyState.BuildingAirframe, assembly.State);
            Assert.AreEqual(9, workstation.respawner.parts.Count, "nine fresh parts for the next build");
            Assert.IsNull(assembly.inspectPrototype, "nothing to inspect after a success");
            Assert.AreNotSame(tube, assembly.tube);
        }

        [UnityTest]
        public IEnumerator ButtonsOutOfPhase_DoNothing()
        {
            yield return FindFlow();
            flow.Launch();
            flow.Abort();
            Assert.AreEqual(GamePhase.Menu, flow.phase, "launch and abort are ignored in the menu");
            bin.Submit();
            Assert.AreEqual(GamePhase.Menu, flow.phase, "submit with an empty bin is refused");
            flow.StartGame();
            yield return null;
            flow.StartGame();
            flow.Launch();
            bin.Submit();
            Assert.AreEqual(GamePhase.Assembly, flow.phase, "nothing launches or submits during assembly");
            Assert.AreEqual(AssemblyState.BuildingAirframe, assembly.State);
        }
    }
}
