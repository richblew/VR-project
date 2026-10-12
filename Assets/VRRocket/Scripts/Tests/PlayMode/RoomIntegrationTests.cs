using System.Collections;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
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
