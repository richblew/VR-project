using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// A held fin or flap lights only the nearest slot that fits it, the one it would go into: never the others round the tube
    /// (which, with the near slot hidden behind the part's own edge, read as the wrong slot lighting). The light follows the part
    /// round the tube, does not flicker between two slots it is held between, and shows on the tube either side of the part's edge.
    /// </summary>
    public class SlotGlowTests : DevSceneTestBase
    {
        /// <summary>Holds the part in a slot's own orientation with its root edge <paramref name="out_"/> metres out from that slot.</summary>
        IEnumerator HoldOutFrom(AttachPoint slot, float out_)
        {
            yield return PlaceHeld(slot.transform.position + slot.GuideAxisWorld * out_, slot.transform.rotation);
            yield return null;
            yield return null;
        }

        static void AssertOnlyLit(AttachPoint lit, string why)
        {
            Assert.Greater(lit.glowLevel, 0.2f, lit.pointName + " lights: " + why);
            foreach (var p in AttachPoint.active)
                if (p != lit && (p.accepts == PartType.WingFlap || p.accepts == PartType.TailFin))
                    Assert.AreEqual(0f, p.glowLevel, p.pointName + " stays dark: " + why);
        }

        [UnityTest]
        public IEnumerator A_Flap_Lights_Only_The_Nearest_Flap_Slot()
        {
            assembly.enforceOrder = false;
            yield return Grab(Part(PartType.WingFlap));
            foreach (var name in new[] { "FlapSlot_Mid_1", "FlapSlot_Low_2", "FlapSlot_Mid_3", "FlapSlot_Low_1" })
            {
                yield return HoldOutFrom(Point(name), 0.07f);
                AssertOnlyLit(Point(name), "flap held 7 cm out from " + name);
            }
        }

        [UnityTest]
        public IEnumerator A_Fin_Lights_Only_The_Nearest_Fin_Slot()
        {
            assembly.enforceOrder = false;
            yield return Grab(Part(PartType.TailFin));
            foreach (var name in new[] { "FinSlot_2", "FinSlot_1" })
            {
                yield return HoldOutFrom(Point(name), 0.07f);
                AssertOnlyLit(Point(name), "fin held 7 cm out from " + name);
            }
        }

        [UnityTest]
        public IEnumerator The_Light_Gets_Brighter_Closer_And_Is_Full_On_The_Track()
        {
            assembly.enforceOrder = false;
            var flap = Part(PartType.WingFlap);
            yield return Grab(flap);
            var slot = Point("FlapSlot_Mid_2");
            yield return HoldOutFrom(slot, 0.1f);
            var far = slot.glowLevel;
            yield return HoldOutFrom(slot, 0.065f);
            Assert.Greater(slot.glowLevel, far, "brighter as it comes closer");
            yield return HoldOutFrom(slot, tuning.captureRadius * 0.6f);
            Assert.IsTrue(flap.GetComponent<GuideGrabTransformer>().isGuided, "caught on the slot that was lit");
            Assert.AreSame(slot, flap.GetComponent<GuideGrabTransformer>().guidePoint);
            Assert.AreEqual(1f, slot.glowLevel, 0.01f);
            AssertOnlyLit(slot, "on its track");
        }

        [UnityTest]
        public IEnumerator Held_Between_Two_Slots_The_Light_Does_Not_Flicker()
        {
            assembly.enforceOrder = false;
            yield return Grab(Part(PartType.WingFlap));
            var mid = Point("FlapSlot_Mid_1");
            var low = Point("FlapSlot_Low_1");
            yield return HoldOutFrom(mid, 0.07f);
            AssertOnlyLit(mid, "start at Mid 1");
            // halfway between the two slots' approach points, then a few millimetres either way: the light stays where it was
            var a = mid.transform.position + mid.GuideAxisWorld * 0.07f;
            var b = low.transform.position + low.GuideAxisWorld * 0.07f;
            var half = (a + b) * 0.5f;
            var toLow = (b - a).normalized;
            foreach (var nudge in new[] { 0f, 0.003f, -0.003f, 0.003f })
            {
                yield return PlaceHeld(half + toLow * nudge, mid.transform.rotation);
                yield return null; yield return null;
                AssertOnlyLit(mid, "held between Mid 1 and Low 1, nudged " + (nudge * 1000f).ToString("F0") + " mm");
            }
            // clearly nearer Low 1: the light moves there
            yield return PlaceHeld(half + toLow * 0.03f, mid.transform.rotation);
            yield return null; yield return null;
            AssertOnlyLit(low, "3 cm along towards Low 1");
        }

        [UnityTest]
        public IEnumerator The_Slot_Light_Shows_Either_Side_Of_A_Flaps_Edge()
        {
            yield return null;
            var slot = Point("FlapSlot_Mid_1");
            var strip = slot.glowRenderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            var width = strip.size.x * slot.glowRenderer.transform.lossyScale.x;
            Assert.Greater(width, 0.014f, "the strip is wider than a flap's edge by well over a centimetre");
        }
    }
}
