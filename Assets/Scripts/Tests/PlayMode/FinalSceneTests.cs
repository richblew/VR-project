using System.Collections;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace VRRocket.Tests
{
    /// <summary>VRRocket_Final: our game in Richard's room, one scene, intro on demand, curtain, step browsing.</summary>
    public class FinalSceneTests : DevSceneTestBase
    {
        public const string Path = "Assets/Scenes/VRRocket_Final.unity";
        protected override string scenePath => Path;

        GameFlowController Flow => Object.FindAnyObjectByType<GameFlowController>();
        TransmissionBridge Bridge => Object.FindAnyObjectByType<TransmissionBridge>();
        WindowCurtain Curtain => Object.FindAnyObjectByType<WindowCurtain>();

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator One_Scene_With_One_Rig_EventSystem_And_Manager()
        {
            yield return null;
            Assert.AreEqual(1, SceneManager.sceneCount, "the room is part of the scene: nothing is loaded next to it");
            Assert.IsNull(Object.FindAnyObjectByType<RoomSceneLoader>());
            Assert.IsNull(Object.FindAnyObjectByType<RoomBindings>());
            Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<XRInteractionManager>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Length);
            Assert.IsNull(GameObject.Find("HelpWall"), "the help board is removed");
            Assert.IsNull(GameObject.Find("WindowWall"), "the old front-room window is removed");
            Assert.IsNotNull(Curtain, "the alcove window has its curtain");
        }

        [UnityTest]
        public IEnumerator Rig_Is_Adapted()
        {
            var adapter = Object.FindAnyObjectByType<XRRigAdapter>();
            Assert.IsNotNull(adapter);
            for (var i = 0; i < 30 && !adapter.isApplied; i++) yield return null;
            Assert.IsTrue(adapter.isApplied);
            var origin = adapter.origin;
            var haptics = origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Feedback.SimpleHapticFeedback>(true);
            Assert.Greater(haptics.Length, 0, "the rig has generic haptics to silence");
            foreach (var h in haptics)
                Assert.IsFalse(h.playHoverEntered || h.playSelectEntered, h.name + ": generic buzz off");
            foreach (var jump in origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump.JumpProvider>(true))
                Assert.IsFalse(jump.enabled, "Jump off");
            Assert.AreEqual(XROrigin.TrackingOriginMode.Floor, origin.RequestedTrackingOriginMode, "floor tracking: the player is their real height");
            Assert.AreEqual(0f, origin.CameraYOffset, "no camera offset to add height in any mode");
            var mediator = origin.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionMediator>(true);
            Assert.IsTrue(mediator != null && mediator.gameObject.activeInHierarchy, "locomotion (move, turn, teleport, gravity) is on");
            var cc = origin.GetComponent<CharacterController>();
            if (cc != null) Assert.GreaterOrEqual(cc.center.y - cc.height * 0.5f, -0.001f, "the body capsule does not reach below the feet");
            for (var i = 0; i < 90; i++) yield return null;
            Assert.Less(origin.transform.position.y, 0.1f, "on the floor once locomotion and gravity run");
            Assert.Greater(origin.transform.position.y, 0.0f);
            var rocketLayer = InteractionLayerMask.GetMask("RocketPart");
            var hands = origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
            Assert.AreEqual(2, hands.Length, "two near-far hands");
            foreach (var nf in hands)
                Assert.AreNotEqual(0, nf.interactionLayers.value & rocketLayer, nf.name + " can grab rocket parts");
        }

        [UnityTest]
        public IEnumerator Menu_Offers_Start_With_Intro_And_Skip_Intro()
        {
            yield return null;
            var flow = Flow;
            Assert.IsTrue(flow.introOnDemand, "the intro waits for START WITH INTRO");
            Assert.IsNotNull(Bridge.transmission, "the intro controller is found in the same scene");
            Assert.IsFalse(Bridge.isPlaying, "nothing plays on the menu");
            var menu = Object.FindAnyObjectByType<MenuPanel>(FindObjectsInactive.Include);
            AssertCalls(menu.transform.Find("StartButton").GetComponent<UnityEngine.UI.Button>().onClick, flow, "StartGame");
            AssertCalls(menu.transform.Find("SkipIntroButton").GetComponent<UnityEngine.UI.Button>().onClick, flow, "StartWithoutIntro");
            StringAssert.Contains("INTRO", menu.transform.Find("StartButton/Label").GetComponent<TMPro.TextMeshProUGUI>().text);
        }

        [UnityTest]
        public IEnumerator Skip_Intro_Goes_Straight_To_Building()
        {
            yield return null;
            var displays = Object.FindAnyObjectByType<PhaseDisplays>();
            Assert.AreEqual(3, displays.gameDisplays.Count, "screen overlay, corner clock and console mirror");
            foreach (var d in displays.gameDisplays) Assert.IsFalse(d.activeSelf, d.name + " off during the menu");
            Flow.StartWithoutIntro();
            yield return null;
            Assert.AreEqual(GamePhase.Assembly, Flow.phase);
            Assert.IsTrue(assembly.interactionEnabled);
            Assert.IsFalse(Bridge.isPlaying);
            Assert.AreEqual(0f, Curtain.drawn, "the curtain stays open");
            foreach (var d in displays.gameDisplays) Assert.IsTrue(d.activeSelf, d.name + " on for the game");
            StringAssert.StartsWith("ASSEMBLY", GameObject.Find("ScreenTitle").GetComponent<TMPro.TextMeshPro>().text);
        }

        [UnityTest]
        public IEnumerator Start_With_Intro_Plays_It_Curtain_Closes_Then_Opens_And_Building_Starts()
        {
            yield return null;
            var flow = Flow; var bridge = Bridge; var curtain = Curtain;
            flow.StartGame();
            yield return null;
            Assert.AreEqual(GamePhase.Intro, flow.phase);
            Assert.IsTrue(bridge.isPlaying, "the intro plays after START WITH INTRO");
            Assert.IsFalse(assembly.interactionEnabled, "parts stay locked during the intro");
            Assert.IsFalse(GameObject.Find("ScreenTitle") != null, "our big-screen overlay is off so the intro shows");

            Time.timeScale = 8f;   // the logo and standby waits run on game time
            var t0 = Time.realtimeSinceStartup;
            while (curtain.drawn < 0.99f && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            Assert.GreaterOrEqual(curtain.drawn, 0.99f, "the curtain is drawn as the room dims");
            Assert.Greater(bridge.dimAmount, 0.5f);

            // jump the transmission video to its end, then the lights fade back up
            var video = (VideoPlayer)bridge.transmission.GetType().GetField("videoPlayer").GetValue(bridge.transmission);
            t0 = Time.realtimeSinceStartup;
            while (!video.isPlaying && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            Assert.IsTrue(video.isPlaying, "the transmission video plays in the dark");
            video.time = Mathf.Max(0f, (float)video.length - 0.3f);
            t0 = Time.realtimeSinceStartup;
            while (flow.phase == GamePhase.Intro && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            Assert.AreEqual(GamePhase.Assembly, flow.phase, "building starts when the intro has finished");
            Assert.IsTrue(assembly.interactionEnabled);
            t0 = Time.realtimeSinceStartup;
            while (curtain.drawn > 0.01f && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            Assert.LessOrEqual(curtain.drawn, 0.01f, "the curtain opens again with the lights");
        }

        [UnityTest]
        public IEnumerator SkipIntro_During_The_Intro_Ends_It_And_Opens_The_Curtain()
        {
            yield return null;
            Flow.StartGame();
            yield return null;
            Flow.SkipIntro();
            yield return null;
            Assert.AreEqual(GamePhase.Assembly, Flow.phase);
            Assert.IsTrue(Bridge.isStopped);
            Assert.AreEqual(0f, Bridge.dimAmount);
        }

        [UnityTest]
        public IEnumerator Abort_Flashes_Panels_Over_The_Side_Screens_With_A_Siren_Then_Hides_Them()
        {
            yield return null;
            var alarm = Object.FindAnyObjectByType<AbortAlarm>();
            Assert.IsNotNull(alarm, "the abort alarm is in the scene");
            var screens = Bridge.sideScreens;
            var panels = alarm.panels;
            Assert.AreEqual(screens.Length, panels.Length, "one ABORT panel per side TV");
            Assert.Greater(panels.Length, 0);
            var before = System.Array.ConvertAll(screens, r => r.sharedMaterial);
            foreach (var p in panels) Assert.IsFalse(p.gameObject.activeInHierarchy, p.name + " hidden until an abort");
            for (var i = 0; i < panels.Length; i++)
            {
                // laid on the TV's face: centred on it, facing the same way, as wide as the TV
                var tv = screens[i].transform;
                var local = tv.InverseTransformPoint(panels[i].transform.position);
                Assert.Less(Mathf.Abs(local.x), 0.01f, panels[i].name + " centred on " + tv.name);
                Assert.Less(Quaternion.Angle(tv.rotation, panels[i].transform.rotation), 0.5f, panels[i].name + " faces the room like the TV");
                Assert.AreEqual(screens[i].bounds.size.y, panels[i].transform.lossyScale.y, 0.03f, panels[i].name + " covers the TV's height");
            }
            alarm.Play();
            yield return null;
            Assert.IsTrue(alarm.sirenPlaying, "the siren sounds");
            foreach (var p in panels) Assert.IsTrue(p.gameObject.activeInHierarchy && p.sharedMaterial == alarm.alarmOn, p.name + " shows ABORT");
            var sawOff = false;
            var t0 = Time.time;
            while (alarm.isPlaying && Time.time - t0 < alarm.seconds + 1f)
            {
                if (panels[0].sharedMaterial != alarm.alarmOn) sawOff = true;
                yield return null;
            }
            Assert.IsTrue(sawOff, "the panels flash");
            Assert.IsFalse(alarm.isPlaying);
            Assert.IsFalse(alarm.sirenPlaying, "the siren stops");
            foreach (var p in panels) Assert.IsFalse(p.gameObject.activeInHierarchy, p.name + " hidden again");
            for (var i = 0; i < screens.Length; i++) Assert.AreSame(before[i], screens[i].sharedMaterial, screens[i].name + " itself never changed");
        }

        [UnityTest]
        public IEnumerator Side_Monitors_Poll_The_Build_And_Show_Range_Status()
        {
            yield return null;
            var screens = Object.FindAnyObjectByType<ConsoleStatusScreens>();
            Assert.IsNotNull(screens, "the console's side monitors");
            Assert.IsFalse(GameObject.Find("Environment/Mission Control Desk/hq/jscmiscon2/Monitors").GetComponent<Renderer>().enabled, "the console's own screens are hidden under our monitors");
            var left = GameObject.Find("ConsoleAddOn/ConsoleMonitorLeft").transform;
            var right = GameObject.Find("ConsoleAddOn/ConsoleMonitorRight").transform;
            Assert.Greater(left.position.x, right.position.x, "facing the console (-Z), GO / NO-GO is on the player's left");
            screens.Refresh();
            StringAssert.Contains("STBY", screens.pollText, "standby on the menu");
            Assert.AreEqual("RANGE STATUS", screens.dataTitle);
            Flow.StartWithoutIntro();
            yield return null;
            screens.Refresh();
            StringAssert.Contains("NO-GO", screens.pollText, "an empty bench is not ready");
            StringAssert.Contains("HOLD", screens.pollVerdict);
            StringAssert.Contains("FLIGHTS    0", screens.dataText);
            StringAssert.DoesNotContain("LOCK", screens.pollText, "the poll never gives away whether the cap is locked");
        }

        [UnityTest]
        public IEnumerator Countdown_Clock_Covers_Its_Whole_TV()
        {
            yield return null;
            Transform clock = null;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == "CountdownClock" && t.gameObject.scene.IsValid()) clock = t;
            Assert.IsNotNull(clock);
            var screen = clock.Find("ClockScreen");
            Renderer tv = null;
            foreach (var r in Bridge.sideScreens)
                if (tv == null || Vector3.Distance(r.bounds.center, clock.position) < Vector3.Distance(tv.bounds.center, clock.position)) tv = r;
            var face = tv.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            Assert.GreaterOrEqual(screen.lossyScale.x, face.x * tv.transform.lossyScale.x, "as wide as the TV");
            Assert.GreaterOrEqual(screen.lossyScale.y, face.y * tv.transform.lossyScale.y, "as tall as the TV");
            var local = tv.transform.InverseTransformPoint(screen.position);
            Assert.Less(Mathf.Abs(local.x), 0.01f, "centred on it");
            Assert.Less(local.z, -0.009f, "in front of its face");
        }

        [UnityTest]
        public IEnumerator Console_And_Step_Buttons_Are_Wired()
        {
            yield return null;
            var flow = Flow;
            AssertCalls(Button("SubmitButton").onPress, Object.FindAnyObjectByType<SubmissionBin>(), "Submit");
            AssertCalls(Button("LaunchButton").onPress, flow, "Launch");
            AssertCalls(Button("AbortButton").onPress, flow, "Abort");
            var display = Object.FindAnyObjectByType<BlueprintDisplay>();
            AssertCalls(Button("NextButton").onPress, display, "ShowNextStep");
            AssertCalls(Button("BackButton").onPress, display, "ShowPreviousStep");
            var deskFront = GameObject.Find("Environment/AssmblyArea/DeskTop").GetComponent<Renderer>().bounds.min.z;
            foreach (var n in new[] { "BackButton", "NextButton" })
                Assert.Less(Button(n).transform.position.z - deskFront, 0.3f, n + " within easy reach at the player's edge of the desk");
            var nameplate = GameObject.Find("ConsoleNameplateLabel/LabelText");
            Assert.IsNotNull(nameplate, "the console's nameplate is a label screen");
            Assert.AreEqual("LAUNCH STATION", nameplate.GetComponent<TMPro.TextMeshPro>().text);
            var addOn = GameObject.Find("ConsoleAddOn");
            Assert.IsNotNull(addOn.transform.Find("ConsoleMirror"), "the second screen on the console (hidden while the menu is up)");
        }

        [UnityTest]
        public IEnumerator Console_And_Step_Buttons_Press_With_The_Hand()
        {
            foreach (var name in new[] { "SubmitButton", "LaunchButton", "AbortButton", "NextButton", "BackButton" })
            {
                var button = Button(name);
                var presses = 0;
                UnityAction count = () => presses++;
                button.onPress.AddListener(count);
                var up = button.transform.up;
                var basePos = button.button != null ? button.button.position : button.transform.position;
                controller.rotation = Quaternion.LookRotation(Vector3.forward, up);
                controller.position = basePos + up * 0.3f;
                yield return null; yield return null;
                for (var d = 0.3f; d > -0.03f; d -= 0.01f) { controller.position = basePos + up * d; yield return null; }
                yield return null;
                button.onPress.RemoveListener(count);
                Assert.Greater(presses, 0, name + " is pressed by pushing the hand down onto it");
                controller.position = basePos + up * 0.5f;
                yield return null; yield return null;
            }
        }

        [UnityTest]
        public IEnumerator The_Pad_Is_In_View_Through_The_Window()
        {
            yield return null;
            var pad = GameObject.Find("PadOrigin").transform.position + Vector3.up * 6f;
            foreach (var eye in new[] { new Vector3(0.02f, 1.65f, 1.8f), new Vector3(0.6f, 1.6f, 2.0f) })
            {
                var hits = Physics.RaycastAll(eye, pad - eye, Vector3.Distance(eye, pad), ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                    Assert.AreEqual("Glass", h.collider.name, "only the window's glass between " + eye + " and the rocket, not " + h.collider.name);
            }
        }

        [UnityTest]
        public IEnumerator Teleport_Areas_Fit_The_Room()
        {
            yield return null;
            Assert.IsTrue(TeleportFloorAt(new Vector3(0.02f, 0f, 1.8f)), "the spawn");
            Assert.IsTrue(TeleportFloorAt(new Vector3(0f, 0f, -5f)), "the front area");
            Assert.IsTrue(TeleportFloorAt(new Vector3(-1.5f, 0f, 0f)), "beside the console");
            Assert.IsFalse(TeleportFloorAt(new Vector3(0.2f, 0f, 0.5f)), "not under the console");
            Assert.IsFalse(TeleportFloorAt(new Vector3(0f, 0f, 3.2f)), "not under the desk");
            Assert.IsFalse(TeleportFloorAt(new Vector3(3.0f, 0f, 2.0f)), "not outside the window");
        }

        static bool TeleportFloorAt(Vector3 xz)
        {
            var hits = Physics.RaycastAll(new Vector3(xz.x, 3.5f, xz.z), Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<XROrigin>() != null) continue;
                return h.collider.GetComponentInParent<BaseTeleportationInteractable>() != null;
            }
            return false;
        }

        static XRPushButton Button(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            return go.GetComponent<XRPushButton>();
        }

        static void AssertCalls(UnityEventBase e, Object target, string method)
        {
            for (var i = 0; i < e.GetPersistentEventCount(); i++)
                if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method) return;
            Assert.Fail("not wired to " + target + "." + method);
        }
    }
}
