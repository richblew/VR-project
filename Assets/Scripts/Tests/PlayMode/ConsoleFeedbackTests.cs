using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Content.Interaction;

namespace VRRocket.Tests
{
    /// <summary>
    /// Every console button and the bin answer a refused press: the reason on the big screen (and console monitor) and on the
    /// status monitor beside the bin, a buzz, and a double vibration on the pressing hand. An unfinished rocket put in the bin
    /// comes back to the workbench with the missing parts listed; the rocket that failed cannot be submitted again; the launch
    /// vehicle is built the way the player built the rocket.
    /// </summary>
    public class ConsoleFeedbackTests : DevSceneTestBase
    {
        protected override string scenePath => FinalSceneTests.Path;

        GameFlowController Flow => Object.FindAnyObjectByType<GameFlowController>();
        SubmissionBin Bin => Object.FindAnyObjectByType<SubmissionBin>();
        static string ScreenTitle => GameObject.Find("ScreenTitle").GetComponent<TMPro.TextMeshPro>().text;
        static string ScreenStatus => GameObject.Find("ScreenStatus").GetComponent<TMPro.TextMeshPro>().text;

        static ConsoleStatusScreens BinMonitor
        {
            get
            {
                // the right monitor, beside the bin, has the data panel
                foreach (var s in Object.FindObjectsByType<ConsoleStatusScreens>())
                {
                    s.Refresh();
                    if (!string.IsNullOrEmpty(s.dataTitle)) return s;
                }
                return null;
            }
        }

        int m_Refusals;

        IEnumerator StartBuilding(bool wired = true)
        {
            yield return null;
            Flow.StartWithoutIntro();
            if (wired) Flow.MiniGameSolved();
            Flow.Refused += () => m_Refusals++;
            yield return new WaitForSeconds(0.2f);
            m_Refusals = 0;
        }

        IEnumerator PutInBin(RocketPart tube, bool letGo = true)
        {
            yield return Grab(tube);
            var bin = Bin.GetComponent<BoxCollider>();
            yield return PlaceHeld(bin.bounds.center + Vector3.up * 0.15f, Quaternion.identity);
            if (!letGo) yield break;
            Release(tube);
            yield return new WaitForSeconds(0.8f);
        }

        [UnityTest]
        public IEnumerator Launch_And_Abort_With_The_Console_Offline_Say_Console_Fault()
        {
            yield return StartBuilding(wired: false);
            Flow.Launch();
            Assert.AreEqual("CONSOLE FAULT", ScreenTitle, "LAUNCH on a dead console says why, like SUBMIT does");
            Flow.Abort();
            Assert.AreEqual("CONSOLE FAULT", ScreenTitle);
            Assert.AreEqual(2, m_Refusals, "each refused press buzzes the hand");
            Assert.AreEqual(GamePhase.Assembly, Flow.phase);
            StringAssert.Contains("CONSOLE OFFLINE", BinMonitor.dataText, "and the monitor beside the bin says it too");
        }

        [UnityTest]
        public IEnumerator Launch_And_Abort_With_Nothing_On_The_Pad_Say_So_Then_The_Screen_Comes_Back()
        {
            yield return StartBuilding();
            Flow.Launch();
            Assert.AreEqual("NO ROCKET ON THE PAD", ScreenTitle);
            StringAssert.Contains("SUBMIT", ScreenStatus, "and what to do instead");
            StringAssert.Contains("NO ROCKET", BinMonitor.dataText, "the reason, wrapped onto the monitor beside the bin");
            Flow.Abort();
            Assert.AreEqual("NOTHING TO ABORT", ScreenTitle);
            Assert.AreEqual(2, m_Refusals);
            yield return new WaitForSeconds(4.5f);
            StringAssert.StartsWith("ASSEMBLY", ScreenTitle, "the build text comes back after a few seconds");
            Assert.AreEqual("SUBMISSION BIN", BinMonitor.dataTitle);
        }

        [UnityTest]
        public IEnumerator A_Refused_Press_Buzzes_The_Hand_That_Pressed()
        {
            yield return StartBuilding();
            XRPushButton launch = null;
            foreach (var b in Object.FindObjectsByType<XRPushButton>()) if (b.name == "LaunchButton") launch = b;
            Assert.IsNotNull(launch);
            var haptics = launch.GetComponent<ButtonPressHaptics>();
            Assert.IsNotNull(haptics, "the launch button has press haptics");
            var up = launch.transform.up;
            var basePos = launch.button != null ? launch.button.position : launch.transform.position;
            controller.rotation = Quaternion.LookRotation(Vector3.forward, up);
            controller.position = basePos + up * 0.3f;
            yield return null; yield return null;
            for (var d = 0.3f; d > -0.03f; d -= 0.01f) { controller.position = basePos + up * d; yield return null; }
            yield return null;
            Assert.AreEqual(1, haptics.refusedBuzzes, "pressing LAUNCH with nothing on the pad gives the pressing hand the double buzz");
            Assert.AreEqual("NO ROCKET ON THE PAD", ScreenTitle);
        }

        [UnityTest]
        public IEnumerator Submit_With_An_Empty_Bin_Or_A_Rocket_In_The_Hand_Says_So()
        {
            yield return StartBuilding();
            Assert.AreEqual(BinState.Empty, Bin.state);
            StringAssert.Contains("EMPTY", BinMonitor.dataText);
            Bin.Submit();
            Assert.AreEqual("BIN EMPTY", ScreenTitle);
            yield return PutInBin(Tube, letGo: false);
            Assert.AreEqual(BinState.RocketHeld, Bin.state);
            yield return new WaitForSeconds(4.2f);
            StringAssert.Contains("LET GO", BinMonitor.dataText, "the monitor says to let go");
            Bin.Submit();
            Assert.AreEqual("LET GO OF THE ROCKET", ScreenTitle);
            Assert.AreEqual(2, m_Refusals);
            Assert.AreNotEqual(AssemblyState.Submitted, assembly.State);
        }

        [UnityTest]
        public IEnumerator An_Unfinished_Rocket_Comes_Back_To_The_Workbench_With_The_Missing_Parts_Listed()
        {
            yield return StartBuilding();
            yield return SeatPart(Part(PartType.TailFin, 1), Point("FinSlot_1"));
            var tube = Tube;
            yield return PutInBin(tube);
            Assert.AreEqual(BinState.PartsMissing, Bin.state);
            var monitor = BinMonitor.dataText;
            StringAssert.Contains("PARTS MISSING", monitor);
            StringAssert.Contains("2 fins", monitor);
            StringAssert.Contains("the nose cone", monitor);
            Bin.Submit();
            Assert.AreEqual("PARTS MISSING", ScreenTitle);
            StringAssert.Contains("2 fins, 3 flaps, the nose cone, the battery and the battery cap", ScreenStatus);
            StringAssert.Contains("workbench", BinMonitor.dataText, "the monitor beside the bin carries the whole message (the big screen shows its first sentence)");
            Assert.AreEqual(1, m_Refusals);
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(tube.gameObject.activeInHierarchy, "it comes back");
            Assert.AreSame(tube, assembly.tube, "the same rocket, still the one being built");
            Assert.IsFalse(assembly.isFrozen, "editable");
            Assert.AreEqual(AssemblyState.BuildingAirframe, assembly.State);
            Assert.AreEqual(PartState.Attached, Part(PartType.TailFin, 1).state, "with what was on it");
            workstation.TryGetTubeHome(out var home, out _);
            Assert.Less(Vector3.Distance(new Vector3(tube.transform.position.x, home.y, tube.transform.position.z), home), 0.15f, "on its spot on the workbench, next to the remaining parts");
            Assert.AreEqual(GamePhase.Assembly, Flow.phase, "no launch");
            Assert.AreEqual(0, Flow.attempts, "and it does not count as a flight");
            Assert.AreEqual(BinState.Empty, Bin.state);
        }

        [UnityTest]
        public IEnumerator The_Failed_Rocket_In_The_Bin_Is_Not_Submitted()
        {
            yield return StartBuilding();
            // a stand-in for the rocket that came back from a failed launch: a body tube that is not the one being built
            var old = Object.Instantiate(Tube.gameObject, Bin.GetComponent<BoxCollider>().bounds.center + Vector3.up * 0.05f, Quaternion.identity);
            old.name = "OldRocket";
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(BinState.OldRocket, Bin.state);
            StringAssert.Contains("FAILED ROCKET", BinMonitor.dataText);
            Bin.Submit();
            Assert.AreEqual("WRONG ROCKET", ScreenTitle);
            Assert.AreNotEqual(AssemblyState.Submitted, assembly.State, "the rocket on the bench is not launched in its place");
            Assert.AreEqual(GamePhase.Assembly, Flow.phase);
            Object.Destroy(old);
        }

        [UnityTest]
        public IEnumerator The_Cap_Before_The_Battery_Says_Battery_First()
        {
            yield return StartBuilding();
            var cap = Part(PartType.MotorCap);
            var seat = Point("CapSeat");
            yield return Grab(cap);
            yield return PlaceHeld(seat.transform.position + seat.GuideAxisWorld * (tuning.captureRadius * 0.5f), Quaternion.identity);
            Assert.IsFalse(cap.GetComponent<GuideGrabTransformer>().isGuided);
            Assert.AreEqual("BATTERY FIRST", ScreenTitle, "the cap near the base before the battery says why it will not go on");
            Assert.AreEqual(1, m_Refusals, "once, not every frame");
        }

        [UnityTest]
        public IEnumerator A_Plug_Let_Go_At_The_Wrong_Socket_Says_So()
        {
            yield return StartBuilding(wired: false);
            Object.FindAnyObjectByType<SlidingPanel>().SetOpenAmount(1f);
            yield return null;
            var box = GameObject.Find("PuzzleBox");
            int Id(Component c) => (int)c.GetType().GetField("pairId").GetValue(c);
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable plug = null;
            foreach (var c in box.GetComponentsInChildren<MonoBehaviour>())
                if (c.GetType().Name == "CableConnector" && (bool)c.GetType().GetField("isPlug").GetValue(c) && Id(c) == 1)
                    plug = c.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor other = null;
            foreach (var s in box.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>())
                if (Id(s) == 2) other = s;
            Assert.IsNotNull(plug); Assert.IsNotNull(other);
            // pick up the red plug, let go of it just above the blue socket
            manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)plug);
            yield return null;
            manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)plug);
            var body = plug.GetComponent<Rigidbody>();
            if (body != null) { body.linearVelocity = Vector3.zero; body.isKinematic = true; }
            plug.transform.position = other.attachTransform.position + other.transform.up * 0.01f;
            Physics.SyncTransforms();
            yield return null; yield return null; yield return null;
            Assert.IsFalse(other.hasSelection, "the blue socket does not take the red plug");
            Assert.AreEqual("WRONG SOCKET", ScreenTitle, "and the screens say why");
            Assert.AreEqual(1, m_Refusals, "with the buzz");
        }

        [UnityTest]
        public IEnumerator A_Plug_Dropped_Out_Of_The_Box_Comes_Back()
        {
            yield return StartBuilding(wired: false);
            Object.FindAnyObjectByType<SlidingPanel>().SetOpenAmount(1f);
            yield return null;
            var box = GameObject.Find("PuzzleBox");
            foreach (var t in box.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("Guard (invisible)")) Assert.IsFalse(t.gameObject.activeInHierarchy, t.name + ": no wall keeps a plug out of the box");
            Transform plug = null;
            foreach (var c in box.GetComponentsInChildren<MonoBehaviour>())
                if (c.GetType().Name == "PlugRespawn") { plug = c.transform; break; }
            Assert.IsNotNull(plug, "the plugs respawn (Yi-Xuan's PlugRespawn)");
            var home = plug.position;
            // dropped over the console, outside the box
            var body = plug.GetComponent<Rigidbody>();
            plug.position = home + Vector3.up * 0.3f + Vector3.forward * 0.3f;
            if (body != null) { body.position = plug.position; body.linearVelocity = Vector3.zero; }
            Physics.SyncTransforms();
            yield return new WaitForSeconds(2f);
            Assert.Less(Vector3.Distance(plug.position, home), 0.03f, "it comes back to where it started in the box");
        }

        /// <summary>Builds the rocket from the current kit, fin i in finSlots[i] and flap i in flapSlots[i], the cap seated but never twisted, and submits it.</summary>
        IEnumerator BuildAndSubmit(string[] finSlots, string[] flapSlots, bool flap2UpsideDown)
        {
            for (var i = 0; i < 3; i++) yield return SeatPart(Part(PartType.TailFin, i + 1), Point(finSlots[i]));
            for (var i = 0; i < 3; i++)
            {
                var slot = Point(flapSlots[i]);
                if (i == 1 && flap2UpsideDown) yield return SeatPart(Part(PartType.WingFlap, 2), slot, slot.transform.rotation * Quaternion.Euler(0f, 0f, 180f));
                else yield return SeatPart(Part(PartType.WingFlap, i + 1), slot);
            }
            yield return SeatPart(Part(PartType.NoseCone), Point("NoseSeat"), Quaternion.identity);
            yield return SeatPart(Part(PartType.Motor), Point("MotorSeat"), Quaternion.identity);
            yield return SeatPart(Part(PartType.MotorCap), Point("CapSeat"), Quaternion.identity);
            Assert.AreEqual(AssemblyState.PrototypeComplete, assembly.State);
            yield return PutInBin(Tube);
            Assert.AreEqual(BinState.Ready, Bin.state);
            Bin.Submit();
            Assert.AreEqual(GamePhase.PreLaunch, Flow.phase);
            yield return new WaitForSeconds(Flow.submitToPadSeconds + 0.3f);   // the vehicle has grown to full size on the pad
        }

        IEnumerator WaitForPhase(GamePhase phase, float seconds)
        {
            for (var t = 0f; t < seconds && Flow.phase != phase; t += Time.deltaTime) yield return null;
            Assert.AreEqual(phase, Flow.phase);
        }

        [UnityTest]
        public IEnumerator Two_Flights_The_Vehicle_Matches_Each_Build_And_The_Battery_Keeps_Its_Size()
        {
            yield return StartBuilding();
            Flow.countdownSeconds = 1f;
            Flow.resultSeconds = 1f;
            var launch = Object.FindAnyObjectByType<LaunchSequence>();

            // flight 1: every part where it belongs, the cap left unscrewed: the battery blows out of the base
            yield return BuildAndSubmit(new[] { "FinSlot_1", "FinSlot_2", "FinSlot_3" }, new[] { "FlapSlot_Mid_1", "FlapSlot_Mid_2", "FlapSlot_Mid_3" }, false);
            Assert.AreEqual(36f, VehicleRatio(launch, "TailFin_1", Part(PartType.TailFin, 1)), 0.5f, "flight 1: the fins go up at full size");
            Assert.AreEqual(36f, VehicleRatio(launch, "Motor_1", Part(PartType.Motor)), 0.5f, "flight 1: the battery too");
            Flow.Launch();
            yield return WaitForPhase(GamePhase.Launch, 4f);
            var ejected = false;
            for (var t = 0f; t < 15f && Flow.phase == GamePhase.Launch; t += Time.deltaTime) { ejected |= launch.model.motorEjected; yield return null; }
            Assert.IsTrue(ejected, "flight 1: the battery blew out");
            yield return WaitForPhase(GamePhase.Assembly, 6f);
            yield return new WaitForSeconds(0.5f);
            StandTubeUpright();   // the new kit's tube lies flat on the bench, like the first one did before the set-up stood it up
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // flight 2, from the new kit: the fins each one slot round, flap 2 in a low slot and upside down
            yield return BuildAndSubmit(new[] { "FinSlot_2", "FinSlot_3", "FinSlot_1" }, new[] { "FlapSlot_Mid_1", "FlapSlot_Low_1", "FlapSlot_Mid_3" }, true);
            Assert.IsTrue(launch.TryGetVehiclePart("TailFin_1", out var point, out var shown, out _));
            Assert.AreEqual("FinSlot_2", point, "flight 2: fin 1 goes up in the slot it went on");
            Assert.IsTrue(shown);
            Assert.IsTrue(launch.TryGetVehiclePart("TailFin_3", out point, out _, out _));
            Assert.AreEqual("FinSlot_1", point);
            foreach (var id in new[] { 1, 2, 3 })
                Assert.AreEqual(36f, VehicleRatio(launch, "TailFin_" + id, Part(PartType.TailFin, id)), 0.5f, "fin " + id + " at full size");
            Assert.IsTrue(launch.TryGetVehiclePart("WingFlap_2", out point, out shown, out var flipped));
            Assert.AreEqual("FlapSlot_Low_1", point, "the flap in the wrong slot goes up in the wrong slot");
            Assert.IsTrue(shown);
            Assert.IsTrue(flipped, "upside down, as it went on");
            Assert.AreEqual("MotorSeat", VehiclePart(launch, "Motor_1").parent.name, "the battery is back in the base");
            Assert.AreEqual(36f, VehicleRatio(launch, "Motor_1", Part(PartType.Motor)), 0.5f, "at its own size, not 36 times it as after the blow-out");
            Assert.AreEqual(36f, VehicleRatio(launch, "MotorCap_1", Part(PartType.MotorCap)), 0.5f, "the cap too");
        }

        [UnityTest]
        public IEnumerator The_Launch_Vehicle_Is_Built_Like_The_Rocket()
        {
            yield return StartBuilding();
            yield return SeatPart(Part(PartType.TailFin, 1), Point("FinSlot_1"));
            yield return SeatPart(Part(PartType.TailFin, 2), Point("FinSlot_2"));
            yield return SeatPart(Part(PartType.TailFin, 3), Point("FinSlot_3"));
            yield return SeatPart(Part(PartType.WingFlap, 1), Point("FlapSlot_Mid_1"));
            var low = Point("FlapSlot_Low_1");
            yield return SeatPart(Part(PartType.WingFlap, 2), low, low.transform.rotation * Quaternion.Euler(0f, 0f, 180f));   // wrong slot, upside down
            yield return SeatPart(Part(PartType.WingFlap, 3), Point("FlapSlot_Mid_3"));
            yield return SeatPart(Part(PartType.NoseCone), Point("NoseSeat"), Quaternion.identity);
            yield return SeatPart(Part(PartType.Motor), Point("MotorSeat"), Quaternion.identity);
            yield return SeatPart(Part(PartType.MotorCap), Point("CapSeat"), Quaternion.identity);
            Assert.AreEqual(AssemblyState.PrototypeComplete, assembly.State);
            yield return PutInBin(Tube);
            Assert.AreEqual(BinState.Ready, Bin.state);
            StringAssert.Contains("READY", BinMonitor.dataText);
            Bin.Submit();
            Assert.AreEqual(GamePhase.PreLaunch, Flow.phase);
            Assert.AreEqual(1, Flow.lastReport.wrongFlapCount);
            yield return null;
            var launch = Object.FindAnyObjectByType<LaunchSequence>();
            Assert.IsTrue(launch.TryGetVehiclePart("WingFlap_2", out var point, out var shown, out var flipped));
            Assert.AreEqual("FlapSlot_Low_1", point, "the vehicle's flap 2 is where the player put it");
            Assert.IsTrue(shown);
            Assert.IsTrue(flipped, "and upside down, as it went on");
            Assert.IsTrue(launch.TryGetVehiclePart("WingFlap_1", out point, out shown, out flipped));
            Assert.AreEqual("FlapSlot_Mid_1", point);
            Assert.IsFalse(flipped);
            Assert.IsTrue(launch.TryGetVehiclePart("WingFlap_3", out point, out _, out _));
            Assert.AreEqual("FlapSlot_Mid_3", point);
            Assert.IsTrue(launch.TryGetVehiclePart("MotorCap_1", out _, out shown, out _));
            Assert.IsTrue(shown);
            StringAssert.Contains("SUBMITTED", BinMonitor.dataText);
        }
    }
}
