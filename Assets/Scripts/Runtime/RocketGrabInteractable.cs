using System;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRRocket
{
    /// <summary>
    /// XRGrabInteractable with one addition: an event raised before the grab records the rigidbody's state.
    /// The guide mechanic uses it to make an attached (kinematic) part dynamic again as it is grabbed, so that
    /// XRGrabInteractable neither restores it to kinematic on release nor refuses to throw it.
    /// </summary>
    public class RocketGrabInteractable : XRGrabInteractable
    {
        /// <summary>Raised at the start of a selection, before the grab reads the rigidbody.</summary>
        public event Action<SelectEnterEventArgs> selectEntering;

        protected override void OnSelectEntering(SelectEnterEventArgs args)
        {
            selectEntering?.Invoke(args);
            base.OnSelectEntering(args);
        }
    }
}
