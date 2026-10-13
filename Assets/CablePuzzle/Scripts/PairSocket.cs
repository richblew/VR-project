using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CablePuzzle
{
    /// <summary>
    /// Socket that only accepts the plug with the same pairId. No interaction layers needed.
    /// </summary>
    public class PairSocket : XRSocketInteractor
    {
        public int pairId;

        bool Matches(IXRInteractable interactable)
        {
            var connector = interactable.transform.GetComponent<CableConnector>();
            return connector != null && connector.isPlug && connector.pairId == pairId;
        }

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            return base.CanHover(interactable) && Matches(interactable);
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            return base.CanSelect(interactable) && Matches(interactable);
        }
    }
}
