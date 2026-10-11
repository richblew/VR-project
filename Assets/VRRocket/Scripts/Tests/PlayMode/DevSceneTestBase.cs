using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket.Tests
{
    /// <summary>
    /// Loads Dev_Interactions, takes over the right controller's transform, and offers grab/move helpers plus an event log.
    /// Feedback is checked as events and audio; haptics can only be felt in the headset (SPEC.md 12.2).
    /// </summary>
    public abstract class DevSceneTestBase
    {
        /// <summary>Scene loaded before each test. Override to run the same helpers in another scene.</summary>
        protected virtual string scenePath => "Assets/VRRocket/Scenes/Dev_Interactions.unity";

        protected RocketWorkstation workstation;
        protected RocketAssembly assembly;
        protected XRInteractionManager manager;
        protected NearFarInteractor hand;
        protected Transform controller;
        protected AssemblyTuning tuning;
        protected readonly List<string> events = new List<string>();

        /// <summary>The hand holds a part this far above its origin, so hand = target pose + grabOffset.</summary>
        protected static readonly Vector3 grabOffset = Vector3.up * 0.03f;

        Vector3 m_GrabLocalPos;
        Quaternion m_GrabLocalRot = Quaternion.identity;

        [UnitySetUp]
        public IEnumerator SetUpScene()
        {
            // Scene start-up with the simulator can log an XRI error ("GameObject is already being activated or deactivated")
            // from the rig's modality manager toggling controller objects. That is not what these tests check.
            LogAssert.ignoreFailingMessages = true;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            workstation = Object.FindFirstObjectByType<RocketWorkstation>();
            assembly = Object.FindFirstObjectByType<RocketAssembly>();
            manager = Object.FindFirstObjectByType<XRInteractionManager>();
            for (var frame = 0; frame < 120 && hand == null; frame++)
            {
                foreach (var nf in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (nf.transform.parent != null && nf.transform.parent.name.StartsWith("Right") && nf.gameObject.activeInHierarchy) hand = nf;
                if (hand == null) yield return null;
            }
            if (hand == null)
            {
                foreach (var nf in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (nf.transform.parent != null && nf.transform.parent.name.StartsWith("Right")) hand = nf;
            }
            Assert.IsNotNull(workstation, "RocketWorkstation in the dev scene");
            Assert.IsNotNull(hand, "right NearFarInteractor");
            yield return null;
            foreach (var modality in Object.FindObjectsByType<XRInputModalityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                modality.enabled = false;
            yield return null;
            controller = hand.transform.parent;
            if (!controller.gameObject.activeSelf) controller.gameObject.SetActive(true);
            foreach (var tpd in controller.GetComponents<TrackedPoseDriver>()) tpd.enabled = false;
            // The attach controller smooths and velocity-scales the anchor behind a real hand; tests teleport the hand,
            // so make the anchor follow it rigidly.
            foreach (var iac in hand.GetComponents<UnityEngine.XR.Interaction.Toolkit.Attachment.InteractionAttachController>())
            {
                iac.smoothOffset = false;
                iac.useDistanceBasedVelocityScaling = false;
            }
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            // park the hand away from the tray so its pusher body cannot sweep parts off the desk
            controller.position = new Vector3(0.6f, 1.4f, 0.3f);
            controller.rotation = Quaternion.identity;
            tuning = workstation.tuning;
            events.Clear();
            AssemblyEvents.GuideEngaged += OnEngaged;
            AssemblyEvents.GuideReleased += OnReleased;
            AssemblyEvents.PartSeated += OnSeated;
            AssemblyEvents.PartRemoved += OnRemoved;
            AssemblyEvents.CapDetent += OnCapDetent;
            AssemblyEvents.CapLocked += OnCapLocked;
            AssemblyEvents.PrototypeComplete += OnPrototypeComplete;
            AssemblyEvents.GuideSlideTick += OnSlideTick;
            AssemblyEvents.TwistTexture += OnTwistTexture;
            AssemblyEvents.PartGrabbed += OnGrabbed;
            AssemblyEvents.PartReleased += OnReleasedPart;
            AssemblyEvents.PartHovered += OnHovered;
            AssemblyEvents.KitSpawned += OnKitSpawned;
            yield return new WaitForSeconds(0.5f);
            StandTubeUpright();
            // let physics take the new pose before any test reads attach point positions
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>
        /// The tube starts flat on the bench; a player holds it upright in the other hand while fitting parts. Tests stand in for that
        /// hand by parking the tube upright above its tray slot and freezing it there, so the upright seat poses of the part helpers hold.
        /// </summary>
        protected void StandTubeUpright()
        {
            var tube = Tube;
            if (tube == null || tube.state != PartState.Free) return;
            workstation.TryGetTubeHome(out var home, out _);
            var body = tube.body;
            if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.isKinematic = true; body.interpolation = RigidbodyInterpolation.None; }
            tube.transform.SetPositionAndRotation(home + Vector3.up * 0.2f, workstation.partsTray.rotation);
            if (body != null) { body.position = tube.transform.position; body.rotation = tube.transform.rotation; }
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            AssemblyEvents.GuideEngaged -= OnEngaged;
            AssemblyEvents.GuideReleased -= OnReleased;
            AssemblyEvents.PartSeated -= OnSeated;
            AssemblyEvents.PartRemoved -= OnRemoved;
            AssemblyEvents.CapDetent -= OnCapDetent;
            AssemblyEvents.CapLocked -= OnCapLocked;
            AssemblyEvents.PrototypeComplete -= OnPrototypeComplete;
            AssemblyEvents.GuideSlideTick -= OnSlideTick;
            AssemblyEvents.TwistTexture -= OnTwistTexture;
            AssemblyEvents.PartGrabbed -= OnGrabbed;
            AssemblyEvents.PartReleased -= OnReleasedPart;
            AssemblyEvents.PartHovered -= OnHovered;
            AssemblyEvents.KitSpawned -= OnKitSpawned;
            yield return null;
        }

        void OnSlideTick(RocketPart p) => events.Add("slideTick:" + p.name);
        void OnTwistTexture(RocketPart p) => events.Add("twistTexture");
        void OnGrabbed(RocketPart p, UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor i) => events.Add("grabbed:" + p.name);
        void OnReleasedPart(RocketPart p, UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor i) => events.Add("releasedPart:" + p.name);
        void OnHovered(RocketPart p, UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor i) => events.Add("hovered:" + p.name);
        void OnKitSpawned() => events.Add("kitSpawned");

        void OnEngaged(RocketPart p, AttachPoint a) => events.Add("engaged:" + p.name + "@" + a.pointName);
        void OnReleased(RocketPart p, AttachPoint a) => events.Add("released:" + p.name + "@" + a.pointName);
        void OnSeated(RocketPart p, AttachPoint a) => events.Add("seated:" + p.name + "@" + a.pointName);
        void OnRemoved(RocketPart p, AttachPoint a) => events.Add("removed:" + p.name + "@" + a.pointName);
        void OnCapDetent(RocketPart p) => events.Add("detent");
        void OnCapLocked(RocketPart p) => events.Add("locked");
        void OnPrototypeComplete(RocketPart p) => events.Add("prototypeComplete");

        protected int Count(string e)
        {
            var n = 0;
            foreach (var x in events) if (x == e) n++;
            return n;
        }

        protected int CountPrefix(string prefix)
        {
            var n = 0;
            foreach (var x in events) if (x.StartsWith(prefix)) n++;
            return n;
        }

        protected RocketPart Part(PartType type, int id = 1)
        {
            foreach (var p in workstation.respawner.parts) if (p != null && p.partType == type && p.partId == id) return p;
            throw new AssertionException("no part " + type + "_" + id);
        }

        protected RocketPart Tube => assembly.tube;

        protected AttachPoint Point(string name)
        {
            return Tube.transform.Find("AttachPoints/" + name).GetComponent<AttachPoint>();
        }

        protected IEnumerator MoveHand(Vector3 worldPos)
        {
            controller.position = worldPos;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>Grabs the part from grabOffset above its origin and remembers the hand-to-part relation.</summary>
        protected IEnumerator Grab(RocketPart part)
        {
            controller.rotation = Quaternion.identity;
            yield return MoveHand(part.transform.position + grabOffset);
            if (!manager.IsSelectPossible(hand, part.grabInteractable) && part.state == PartState.Free)
            {
                // a loose part knocked somewhere awkward by physics: put it back on the pad at rest and try again
                workstation.respawner.Respawn(part);
                yield return new WaitForSeconds(0.3f);
                yield return MoveHand(part.transform.position + grabOffset);
            }
            Assert.IsTrue(manager.IsSelectPossible(hand, part.grabInteractable), "near grab of " + part.name + " must be possible (state " + part.state + ", pos " + part.transform.position.ToString("F2") + ")");
            manager.SelectEnter((IXRSelectInteractor)hand, part.grabInteractable);
            Assert.IsTrue(part.grabInteractable.isSelected, part.name + " selected");
            yield return null;
            m_Held = part;
            var inv = Quaternion.Inverse(part.transform.rotation);
            m_GrabLocalPos = inv * (controller.position - part.transform.position);
            m_GrabLocalRot = inv * controller.rotation;
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>Can the right hand grab this part from right next to it?</summary>
        protected IEnumerator CanGrabFromNear(RocketPart part, System.Action<bool> result)
        {
            controller.rotation = Quaternion.identity;
            yield return MoveHand(part.transform.position + grabOffset);
            result(manager.IsSelectPossible(hand, part.grabInteractable));
        }

        /// <summary>Moves the hand so that the held part would take exactly this pose (rigid dynamic attach).</summary>
        RocketPart m_Held;

        /// <summary>Moves the hand so that the held part takes this pose. Verifies the result and re-derives the hand relation if XRI's attach anchor drifted.</summary>
        protected IEnumerator PlaceHeld(Vector3 partPos, Quaternion partRot)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                controller.rotation = partRot * m_GrabLocalRot;
                controller.position = partPos + partRot * m_GrabLocalPos;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(0.4f);
                if (m_Held == null || m_Held.state != PartState.Free) yield break;   // guided or attached: the mechanic owns the pose now
                var posErr = Vector3.Distance(m_Held.transform.position, partPos);
                var rotErr = Quaternion.Angle(m_Held.transform.rotation, partRot);
                if (posErr < 0.01f && rotErr < 3f) yield break;
                // re-derive the relation from where the part really is
                var inv = Quaternion.Inverse(m_Held.transform.rotation);
                m_GrabLocalPos = inv * (controller.position - m_Held.transform.position);
                m_GrabLocalRot = inv * controller.rotation;
                lastPlaceError = "pos " + posErr.ToString("F3") + " rot " + rotErr.ToString("F1");
            }
        }

        protected string lastPlaceError = "";

        protected void Release(RocketPart part)
        {
            manager.SelectExit((IXRSelectInteractor)hand, part.grabInteractable);
        }

        /// <summary>The upright pose a part takes when guided into a point: slots use the point's rotation, seats keep the part upright.</summary>
        protected static Quaternion SeatPoseRotation(AttachPoint point) => point.isSlot ? point.transform.rotation : point.transform.rotation;

        /// <summary>Guides a part onto a point along its axis, holding it with the given rotation, and releases it. Expects it to seat.</summary>
        protected IEnumerator SeatPart(RocketPart part, AttachPoint point, Quaternion? heldRotation = null)
        {
            yield return Grab(part);
            var rot = heldRotation ?? SeatPoseRotation(point);
            var away = point.GuideAxisWorld;
            var guide = part.GetComponent<GuideGrabTransformer>();
            for (var attempt = 0; attempt < 3 && !guide.isGuided; attempt++)
                yield return PlaceHeld(point.transform.position + away * (tuning.captureRadius * 0.6f), rot);
            var dist = Vector3.Distance(part.transform.position, point.transform.position);
            var ang = Vector3.Angle(point.isSlot ? part.transform.forward : part.transform.up, point.ReferenceAxisWorld);
            Assert.IsTrue(guide.isGuided, part.name + " guided at " + point.pointName + " (gating " + point.gatingAllows + ", occupied " + point.isOccupied + ", dist " + dist.ToString("F3") + ", angle " + ang.ToString("F1") + ", held " + part.isHeld + ", state " + part.state + ", last place error " + lastPlaceError + ")");
            Release(part);
            yield return new WaitForSeconds(tuning.SeatDurationFor(part.partType) + 0.3f);
            Assert.AreEqual(PartState.Attached, part.state, part.name + " attached");
            Assert.AreSame(point, part.attachedTo);
        }

        /// <summary>Grabs an attached part and pulls it out along the point's axis past the guide length, then drops it.</summary>
        protected IEnumerator RemovePart(RocketPart part)
        {
            var point = part.attachedTo;
            Assert.IsNotNull(point, part.name + " is attached");
            yield return Grab(part);
            var away = point.GuideAxisWorld;
            var len = tuning.GuideLengthFor(part.partType);
            for (var attempt = 0; attempt < 3 && part.state != PartState.Free; attempt++)
                yield return PlaceHeld(point.transform.position + away * (len * (1.6f + 0.4f * attempt)), part.transform.rotation);
            Assert.AreEqual(PartState.Free, part.state, part.name + " detached");
            Release(part);
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>Builds the complete airframe: three fins, three flaps (Mid, Up), nose cone.</summary>
        protected IEnumerator BuildAirframe()
        {
            yield return SeatPart(Part(PartType.TailFin, 1), Point("FinSlot_1"));
            yield return SeatPart(Part(PartType.TailFin, 2), Point("FinSlot_2"));
            yield return SeatPart(Part(PartType.TailFin, 3), Point("FinSlot_3"));
            yield return SeatPart(Part(PartType.WingFlap, 1), Point("FlapSlot_Mid_1"));
            yield return SeatPart(Part(PartType.WingFlap, 2), Point("FlapSlot_Mid_2"));
            yield return SeatPart(Part(PartType.WingFlap, 3), Point("FlapSlot_Mid_3"));
            yield return SeatPart(Part(PartType.NoseCone), Point("NoseSeat"), Quaternion.identity);
        }
    }
}
