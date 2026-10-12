using System.Collections;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.TestTools;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace VRRocket.Tests
{
    /// <summary>
    /// VRRocket_Main on the teammates' room: one rig, one EventSystem, one interaction manager; the rig adapted (generic haptics
    /// off, Jump off, standing on the floor); the room bindings matched with no warnings; the window wall swapped in; the overlay
    /// on their big TV; the mission displays off during their intro and on after START, which also ends the intro.
    /// </summary>
    public class RoomIntegrationTests : DevSceneTestBase
    {
        protected override string scenePath => "Assets/VRRocket/Scenes/VRRocket_Main.unity";

        [UnityTest]
        public IEnumerator Room_Loads_Once_With_One_Rig_EventSystem_And_Manager()
        {
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>().Length, "one XR Origin (the room's Player)");
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length, "one EventSystem");
            var managers = Object.FindObjectsByType<XRInteractionManager>();
            var where = string.Join(", ", System.Array.ConvertAll(managers, m => m.name + " in " + m.gameObject.scene.name));
            Assert.AreEqual(1, managers.Length, "one XR Interaction Manager (" + where + ")");
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Length, "one AudioListener");
            var loader = RoomSceneLoader.instance;
            Assert.IsNotNull(loader);
            Assert.IsTrue(loader.roomScene.isLoaded);
            Assert.AreEqual(loader.gameObject.scene, UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "our scene stays active, so its sky and sun apply");
        }

        [UnityTest]
        public IEnumerator Rig_Is_Adapted()
        {
            var adapter = Object.FindAnyObjectByType<XRRigAdapter>();
            Assert.IsNotNull(adapter);
            for (var i = 0; i < 30 && !adapter.isApplied; i++) yield return null;
            Assert.IsTrue(adapter.isApplied);
            var origin = adapter.origin;
            foreach (var h in origin.GetComponentsInChildren<SimpleHapticFeedback>(true))
                Assert.IsFalse(h.playHoverEntered || h.playSelectEntered, h.name + ": generic buzz off");
            foreach (var jump in origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump.JumpProvider>(true))
                Assert.IsFalse(jump.enabled, "Jump off");
            Assert.Less(origin.transform.position.y, 0.1f, "the rig stands on the floor (their spawn is in the air)");
            Assert.Greater(origin.transform.position.y, 0.0f);
            var rocketLayer = InteractionLayerMask.GetMask("RocketPart");
            foreach (var nf in origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true))
                Assert.AreNotEqual(0, nf.interactionLayers.value & rocketLayer, nf.name + " can grab rocket parts");
        }

        [UnityTest]
        public IEnumerator Bindings_Match_The_Room()
        {
            var bindings = Object.FindAnyObjectByType<RoomBindings>();
            Assert.IsNotNull(bindings);
            for (var i = 0; i < 30 && !bindings.isApplied; i++) yield return null;
            Assert.IsTrue(bindings.isApplied);
            Assert.AreEqual(0, bindings.warningCount, "the room matches the layout (see the RoomBindings warnings in the Console)");
            var room = RoomSceneLoader.instance.roomScene;
            var wall = RoomSceneLoader.FindByPath(room, "Environment/Floorplan/Right wall");
            Assert.IsNotNull(wall);
            Assert.IsFalse(wall.activeSelf, "their solid right wall is swapped out");
            foreach (var s in bindings.swaps) Assert.IsTrue(s.replacement.activeInHierarchy, s.replacement.name + " is in");
            var tv = RoomSceneLoader.FindByPath(room, "Environment/TV/TV LED 30*");
            Assert.IsNotNull(tv, "their big TV, found by prefix");
            var title = GameObject.Find("ScreenTitle");
            Assert.IsNull(title, "the overlay is off while the menu is up");
        }

        [UnityTest]
        public IEnumerator Teleport_Areas_Cover_The_Floor_But_Not_The_Furniture()
        {
            yield return null;
            var areas = Object.FindObjectsByType<TeleportationArea>();
            Assert.Greater(areas.Length, 0);
            var teleport = InteractionLayerMask.GetMask("Teleport");
            foreach (var a in areas) Assert.AreNotEqual(0, a.interactionLayers.value & teleport, a.name + " is on the Teleport layer");
            Assert.IsTrue(TeleportFloorAt(new Vector3(-0.4f, 0f, 0.78f)), "the spawn");
            Assert.IsTrue(TeleportFloorAt(new Vector3(-0.3f, 0f, 2.0f)), "in front of the desk");
            Assert.IsTrue(TeleportFloorAt(new Vector3(3.0f, 0f, -3.0f)), "at the window");
            Assert.IsFalse(TeleportFloorAt(new Vector3(0f, 0f, -1.2f)), "not under the console");
            Assert.IsFalse(TeleportFloorAt(new Vector3(0f, 0f, 3.2f)), "not under the desk");
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

        [UnityTest]
        public IEnumerator Console_Buttons_And_Start_Are_Wired()
        {
            yield return null;
            var flow = Object.FindAnyObjectByType<GameFlowController>();
            var bin = Object.FindAnyObjectByType<SubmissionBin>();
            Assert.IsNotNull(bin, "the console add-on is switched on once placed on their console");
            AssertCalls(Button("SubmitButton").onPress, bin, "Submit");
            AssertCalls(Button("LaunchButton").onPress, flow, "Launch");
            AssertCalls(Button("AbortButton").onPress, flow, "Abort");
            var menu = Object.FindAnyObjectByType<MenuPanel>(FindObjectsInactive.Include);
            var start = menu.GetComponentInChildren<UnityEngine.UI.Button>(true);
            Assert.IsNotNull(start, "START button");
            AssertCalls(start.onClick, flow, "StartGame");
            Assert.IsNotNull(menu.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(), "the menu takes XR rays");
        }

        [UnityTest]
        public IEnumerator Console_Buttons_Press_With_The_Hand()
        {
            foreach (var name in new[] { "SubmitButton", "LaunchButton", "AbortButton" })
            {
                var button = Button(name);
                var presses = 0;
                UnityAction count = () => presses++;
                button.onPress.AddListener(count);
                var up = button.transform.up;
                var basePos = button.button != null ? button.button.position : button.transform.position;
                controller.rotation = Quaternion.LookRotation(Vector3.forward, up);
                controller.position = basePos + up * 0.3f;
                yield return null;
                yield return null;
                for (var d = 0.3f; d > -0.03f; d -= 0.01f)
                {
                    controller.position = basePos + up * d;
                    yield return null;
                }
                yield return null;
                button.onPress.RemoveListener(count);
                Assert.Greater(presses, 0, name + " is pressed by pushing the hand down onto it");
                controller.position = basePos + up * 0.5f;
                yield return null;
                yield return null;
            }
            Assert.AreEqual(GamePhase.Menu, Object.FindAnyObjectByType<GameFlowController>().phase, "the console buttons do nothing in the menu");
        }

        static XRPushButton Button(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            var b = go.GetComponent<XRPushButton>();
            Assert.IsNotNull(b, name + " is a push button");
            return b;
        }

        static void AssertCalls(UnityEventBase e, Object target, string method)
        {
            for (var i = 0; i < e.GetPersistentEventCount(); i++)
                if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method) return;
            Assert.Fail("not wired to " + target + "." + method);
        }

        [UnityTest]
        public IEnumerator Start_Ends_The_Intro_And_Switches_The_Mission_Displays_On()
        {
            var flow = Object.FindAnyObjectByType<GameFlowController>();
            var bridge = Object.FindAnyObjectByType<TransmissionBridge>();
            var displays = Object.FindAnyObjectByType<PhaseDisplays>();
            Assert.IsNotNull(flow); Assert.IsNotNull(bridge); Assert.IsNotNull(displays);
            yield return null;
            Assert.IsNotNull(bridge.transmission, "the room's intro controller was found");
            Assert.IsTrue(bridge.transmission.enabled, "the intro runs while the menu is up");
            foreach (var d in displays.gameDisplays) Assert.IsFalse(d.activeSelf, d.name + " off during the menu");

            flow.StartGame();
            yield return null;
            Assert.IsTrue(bridge.isStopped);
            Assert.IsFalse(bridge.transmission.enabled, "the intro ended");
            var room = RoomSceneLoader.instance.roomScene;
            foreach (var name in new[] { "Transmission_Video", "stand_by" })
            {
                var player = RoomSceneLoader.FindByPath(room, name);
                if (player != null) Assert.IsFalse(player.GetComponent<VideoPlayer>().isPlaying, name + " stopped");
            }
            var announcement = RoomSceneLoader.FindByPath(room, "Incoming_Transmission");
            if (announcement != null) Assert.IsFalse(announcement.GetComponent<AudioSource>().isPlaying, "announcement stopped");
            foreach (var d in displays.gameDisplays) Assert.IsTrue(d.activeSelf, d.name + " on for the game");
            var title = GameObject.Find("ScreenTitle");
            Assert.IsNotNull(title);
            StringAssert.StartsWith("ASSEMBLY", title.GetComponent<TMPro.TextMeshPro>().text);
        }
    }
}
