using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRRocket.Tests
{
    /// <summary>
    /// The room's rig has NearFarInteractors, which can also reach far. Rocket parts are near-only: a hand half a metre away
    /// neither hovers nor selects them, a hand at the part does.
    /// </summary>
    public class NearOnlyGrabTests : DevSceneTestBase
    {
        [UnityTest]
        public IEnumerator Parts_Refuse_The_Far_Hand_And_Accept_The_Near_One()
        {
            var fin = Part(PartType.TailFin, 1);
            var grab = fin.GetComponent<XRGrabInteractable>();
            var filter = fin.GetComponent<NearOnlyGrabFilter>();
            Assert.IsNotNull(grab);
            Assert.IsNotNull(filter, "every part carries the near-only filter");
            var hoverFilters = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Filtering.IXRHoverFilter>();
            grab.hoverFilters.GetAll(hoverFilters);
            var selectFilters = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Filtering.IXRSelectFilter>();
            grab.selectFilters.GetAll(selectFilters);
            Assert.Contains(filter, hoverFilters, "registered as a hover filter");
            Assert.Contains(filter, selectFilters, "registered as a select filter");

            var toHand = (Vector3.up + Vector3.back).normalized;
            controller.position = fin.transform.position + toHand * 0.5f;
            yield return null;
            yield return null;
            Assert.IsFalse(filter.Process((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRHoverInteractor)hand, grab), "no hover from 0.5 m");
            Assert.IsFalse(filter.Process((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand, grab), "no grab from 0.5 m");
            Assert.IsFalse(grab.isHovered && grab.interactorsHovering.Contains(hand), "the far hand does not hover it");

            controller.position = fin.transform.position + toHand * 0.02f;
            yield return null;
            yield return null;
            Assert.IsTrue(filter.Process((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand, grab), "a hand at the part can grab it");
        }
    }
}
