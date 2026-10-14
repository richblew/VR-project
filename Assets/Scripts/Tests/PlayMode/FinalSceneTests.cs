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
            Assert.AreEqual(MenuState.FirstPlay, menu.shownState);
            AssertMenuShows(menu, menu.firstPlay);
            Assert.IsTrue(menu.transform.Find("SkipIntroButton").gameObject.activeSelf, "both buttons on offer");
        }

        static string Text(MenuPanel menu, string path) => menu.transform.Find(path).GetComponent<TMPro.TextMeshProUGUI>().text;

        static void AssertMenuShows(MenuPanel menu, MenuWords words)
        {
            Assert.AreEqual(words.title, Text(menu, "Title"));
            Assert.AreEqual(words.kicker, Text(menu, "Kicker"));
            Assert.AreEqual(words.body, Text(menu, "Body"));
            Assert.AreEqual(words.briefButton, Text(menu, "StartButton/Label"));
            Assert.AreEqual(words.buildButton, Text(menu, "SkipIntroButton/Label"));
            Assert.AreEqual(words.footer, Text(menu, "Footer"));
        }

        [UnityTest]
        public IEnumerator Menu_Words_Are_Set_Per_State_On_The_Menu_Panel()
        {
            yield return null;
            var menu = Object.FindAnyObjectByType<MenuPanel>(FindObjectsInactive.Include);
            Assert.AreNotEqual(menu.firstPlay.title, menu.afterSuccess.title, "each state has its own words");
            menu.Show(MenuState.AfterSuccess, true);
            AssertMenuShows(menu, menu.afterSuccess);
            // whatever is typed in the Inspector is what shows: nothing in code overrides it
            menu.afterSuccess.body = "Edited in the Inspector";
            menu.afterSuccess.buildButton = "AGAIN!";
            menu.Show(MenuState.AfterSuccess, true);
            Assert.AreEqual("Edited in the Inspector", Text(menu, "Body"));
            Assert.AreEqual("AGAIN!", Text(menu, "SkipIntroButton/Label"));
            menu.Show(MenuState.FirstPlay, false);
            Assert.AreEqual(menu.noBriefingButton, Text(menu, "StartButton/Label"), "a room without a briefing: the left button starts building");
            Assert.IsFalse(menu.transform.Find("SkipIntroButton").gameObject.activeSelf, "and the right one is hidden");
            Assert.AreEqual("VR ROCKET", new MenuWords().title);
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
                // a picture on the TV's screen, inside a bezel, not a sheet over the whole TV
                var faceHeight = screens[i].bounds.size.y;
                Assert.Less(panels[i].transform.lossyScale.y, faceHeight - 0.1f, panels[i].name + " is inset inside the TV's frame");
                Assert.Greater(panels[i].transform.lossyScale.y, faceHeight * 0.75f, panels[i].name + " still fills most of the screen");
                var bezel = panels[i].transform.Find("Bezel");
                Assert.IsNotNull(bezel, panels[i].name + " has a bezel");
                Assert.AreEqual(faceHeight, bezel.lossyScale.y, 0.03f, "the bezel covers the TV's face");
                Assert.Greater(tv.InverseTransformPoint(bezel.position).z, tv.InverseTransformPoint(panels[i].transform.position).z, "the bezel sits behind the picture");
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
            Flow.MiniGameSolved();   // the wiring done: the poll is about the build (Console_Is_Offline_... covers the wiring)
            yield return null;
            screens.Refresh();
            StringAssert.Contains("NO-GO", screens.pollText, "an empty bench is not ready");
            StringAssert.Contains("HOLD", screens.pollVerdict);
            StringAssert.Contains("FLIGHTS    0", screens.dataText);
            StringAssert.DoesNotContain("LOCK", screens.pollText, "the poll never gives away whether the cap is locked");
        }

        [UnityTest]
        public IEnumerator Hatch_Slides_Open_By_Its_Grip_And_Unlocks_The_Wiring()
        {
            yield return null;
            var hatch = Object.FindAnyObjectByType<SlidingPanel>();
            Assert.IsNotNull(hatch, "the wiring hatch on the console");
            var wiring = GameObject.Find("PuzzleBox").GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);
            Assert.AreEqual(5, wiring.Length, "five plugs; the five sockets are fixed in the well (Yi-Xuan's 5dcb547)");
            var sockets = 0;
            foreach (var t in GameObject.Find("PuzzleBox").GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Receptacle_")) { sockets++; Assert.IsNull(t.GetComponent<Rigidbody>(), t.name + " is fixed: no rigidbody, it cannot be pulled out"); }
            Assert.AreEqual(5, sockets);
            foreach (var g in wiring) Assert.IsFalse(g.enabled, g.name + " cannot be grabbed through the closed hatch");
            // take the grip and pull it along the slot
            controller.rotation = Quaternion.identity;
            yield return MoveHand(hatch.transform.position + Vector3.up * 0.015f);
            Assert.IsTrue(manager.IsSelectPossible(hand, hatch), "the hand can take the grip");
            manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand, hatch);
            yield return null;
            var start = controller.position;
            for (var i = 1; i <= 30; i++) { controller.position = start + hatch.axisWorld * (hatch.travel * 1.2f * i / 30f); yield return null; }
            Assert.Greater(hatch.openAmount, 0.98f, "pulled fully open");
            Assert.LessOrEqual(hatch.openAmount, 1f, "and no further");
            foreach (var g in wiring) Assert.IsTrue(g.enabled, g.name + " can be grabbed with the hatch open");
            for (var i = 29; i >= 0; i--) { controller.position = start + hatch.axisWorld * (hatch.travel * 1.2f * i / 30f); yield return null; }
            Assert.Less(hatch.openAmount, 0.02f, "pushed shut again");
            foreach (var g in wiring) Assert.IsFalse(g.enabled, g.name + " locked again");
            manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand, hatch);
        }

        [UnityTest]
        public IEnumerator Console_Is_Offline_Until_The_Wiring_Is_Connected()
        {
            yield return null;
            var flow = Flow;
            Assert.IsTrue(flow.requireMiniGame, "the final scene needs the wiring puzzle");
            flow.StartWithoutIntro();
            yield return null;
            Assert.IsFalse(flow.consoleOnline, "offline at the start of a play");
            var screens = Object.FindAnyObjectByType<ConsoleStatusScreens>();
            screens.Refresh();
            StringAssert.Contains("WIRING", screens.pollText);
            StringAssert.Contains("CONSOLE OFFLINE", screens.pollVerdict);
            // SUBMIT refuses while offline, even before anything else is checked
            var bin = Object.FindAnyObjectByType<SubmissionBin>();
            var refused = 0;
            UnityAction onRefused = () => refused++;
            bin.onRefused.AddListener(onRefused);
            var live = Object.FindAnyObjectByType<ConsoleLiveScreens>();
            Assert.IsNotNull(live, "the side TVs' live panels");
            Assert.IsFalse(live.isLive, "the side TVs show NO SIGNAL while the console is broken");
            foreach (var p in live.panels) Assert.IsFalse(p.gameObject.activeInHierarchy, p.name + " off");
            bin.Submit();
            bin.onRefused.RemoveListener(onRefused);
            Assert.AreEqual(1, refused);
            Assert.AreEqual(GamePhase.Assembly, flow.phase);
            var title = GameObject.Find("ScreenTitle").GetComponent<TMPro.TextMeshPro>();
            Assert.AreEqual("CONSOLE FAULT", title.text, "pressing SUBMIT on a broken console says why on the screens");
            // the puzzle's "all connected" event is wired to the flow: raise it as the puzzle would
            MonoBehaviour pairManager = null;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>()) if (mb.GetType().Name == "CablePairManager") pairManager = mb;
            Assert.IsNotNull(pairManager, "Yi-Xuan's CablePairManager");
            var allConnected = (UnityEvent)pairManager.GetType().GetField("onAllConnected").GetValue(pairManager);
            AssertCalls(allConnected, flow, "MiniGameSolved");
            allConnected.Invoke();
            Assert.IsTrue(flow.miniGameSolved && flow.consoleOnline, "connecting every pair brings the console online");
            Assert.AreEqual("CONSOLE ONLINE", title.text, "the screens say it is fixed");
            Assert.IsTrue(live.isLive, "the side TVs come alive");
            Assert.AreEqual(3, live.panels.Length, "three side TVs (the fourth carries the countdown clock)");
            foreach (var p in live.panels) Assert.IsTrue(p.gameObject.activeInHierarchy, p.name + " on");
            screens.Refresh();
            StringAssert.DoesNotContain("CONSOLE OFFLINE", screens.pollVerdict);
        }

        [UnityTest]
        public IEnumerator Wiring_Lights_Each_Pair_Locks_When_Done_And_Resets_For_A_New_Play()
        {
            yield return null;
            var flow = Flow;
            flow.StartWithoutIntro();
            Object.FindAnyObjectByType<SlidingPanel>().SetOpenAmount(1f);
            yield return null;
            var box = GameObject.Find("PuzzleBox");
            MonoBehaviour wiring = null;
            foreach (var mb in box.GetComponents<MonoBehaviour>()) if (mb.GetType().Name == "ConsoleWiring") wiring = mb;
            Assert.IsNotNull(wiring, "ConsoleWiring on the PuzzleBox");
            var lights = (Renderer[])wiring.GetType().GetField("lights").GetValue(wiring);
            var lightOn = (Material)wiring.GetType().GetField("lightOn").GetValue(wiring);
            bool Locked() => (bool)wiring.GetType().GetProperty("locked").GetValue(wiring);
            int Id(Component c) => (int)c.GetType().GetField("pairId").GetValue(c);
            var sockets = box.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            Assert.AreEqual(5, sockets.Length);
            Assert.AreEqual(5, lights.Length, "a status light per pair");
            foreach (var l in lights) Assert.AreNotSame(lightOn, l.sharedMaterial, "all lights off before any plug is in");

            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable PlugFor(int id)
            {
                foreach (var c in box.GetComponentsInChildren<MonoBehaviour>())
                    if (c.GetType().Name == "CableConnector" && (bool)c.GetType().GetField("isPlug").GetValue(c) && Id(c) == id)
                        return c.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                return null;
            }
            var lit = 0; var done = 0;
            foreach (var socket in sockets)
            {
                var id = Id(socket);
                var plug = PlugFor(id);
                plug.transform.position = socket.attachTransform.position;
                manager.SelectEnter(socket, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)plug);
                yield return null;
                Assert.IsTrue(socket.hasSelection, "pair " + id + " plugged in");
                lit = 0;
                foreach (var l in lights) if (l.sharedMaterial == lightOn) lit++;
                done++;
                Assert.AreEqual(done, lit, "one more light on with pair " + id);
            }
            Assert.AreEqual(5, lit, "every pair's light is on");
            Assert.IsTrue(flow.miniGameSolved && flow.consoleOnline, "all pairs connected: the console comes online");
            Assert.IsTrue(Locked(), "the wiring locks");
            var filter = (UnityEngine.XR.Interaction.Toolkit.Filtering.IXRSelectFilter)wiring;
            Assert.IsFalse(filter.Process(hand, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)PlugFor(1)), "a hand cannot pull a plug out once the wiring is done");
            Assert.IsTrue(filter.Process(sockets[0], (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)PlugFor(1)), "the sockets keep their plugs");

            flow.ResetMiniGame();   // what a new play does after a successful flight
            for (var i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            foreach (var socket in sockets) Assert.IsFalse(socket.hasSelection, "pair " + Id(socket) + " unplugged for the new play");
            foreach (var l in lights) Assert.AreNotSame(lightOn, l.sharedMaterial, "lights off again");
            Assert.IsFalse(Locked());
            Assert.IsFalse(flow.consoleOnline, "the console is offline until the wiring is done again");
            foreach (var p in Object.FindAnyObjectByType<ConsoleLiveScreens>().panels) Assert.IsFalse(p.gameObject.activeInHierarchy, p.name + " back to NO SIGNAL");
        }

        [UnityTest]
        public IEnumerator Side_TVs_Are_Stacked_Square_And_The_Same_Size()
        {
            yield return null;
            Transform Tv(string n) { foreach (var r in Bridge.sideScreens) if (r.name == n) return r.transform; return null; }
            foreach (var side in new[] { "left", "right" })
            {
                var bottom = Tv("TV bottom " + side); var top = Tv("TV top " + side);
                Assert.IsNotNull(bottom); Assert.IsNotNull(top);
                Assert.Less(Vector2.Distance(new Vector2(top.position.x, top.position.z), new Vector2(bottom.position.x, bottom.position.z)), 0.002f, side + ": the top TV sits straight over the bottom one");
                Assert.Less(Quaternion.Angle(top.rotation, bottom.rotation), 0.1f, side + ": both face the same way");
                Assert.AreEqual(bottom.lossyScale.x, top.lossyScale.x, 0.001f, side + ": the same width");
                var gap = top.position.y - (bottom.position.y + bottom.GetComponent<MeshFilter>().sharedMesh.bounds.size.y * bottom.lossyScale.y);
                Assert.That(gap, Is.InRange(0f, 0.02f), side + ": stacked with a small gap, not overlapping");
            }
            Assert.AreEqual(Tv("TV bottom left").position.y, Tv("TV bottom right").position.y, 0.001f, "both pairs at the same height");
            Assert.AreEqual(Tv("TV bottom left").lossyScale.x, Tv("TV bottom right").lossyScale.x, 0.001f, "all four the same size");
        }

        [UnityTest]
        public IEnumerator The_Kit_Motor_Is_A_Battery()
        {
            yield return null;
            var part = Part(PartType.Motor);
            var mesh = part.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.AreEqual("Battery", mesh.name, "the kit's motor part shows the battery model");
            var screens = Object.FindAnyObjectByType<ConsoleStatusScreens>();
            Flow.StartWithoutIntro();
            yield return null;
            screens.Refresh();
            StringAssert.Contains("BATTERY", screens.pollText);
            StringAssert.DoesNotContain("MOTOR", screens.pollText);
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
            // the buttons are used while building, with the menu (which hangs over the console) gone
            Flow.StartWithoutIntro();
            yield return new WaitForSeconds(0.5f);
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
