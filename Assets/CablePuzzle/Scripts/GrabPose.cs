using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CablePuzzle
{
    /// <summary>
    /// Optional. Put on a grabbable object (plug, any later model) to say how it sits in the hand while held.
    /// Objects without this component make the hand close fully and keep whatever Attach settings they have.
    ///  - closure: how closed the fingers are while it is held (0 = open hand, 1 = full fist)
    ///  - snapToHoldPoint: the object's pivot goes to the hand's Hold Point (see VRHandRigDriver > Hold Point Offset)
    ///    instead of staying wherever it was when grabbed (turns off the grab interactable's Use Dynamic Attach)
    ///  - holdOffset / holdRotation: extra shift/turn for THIS object only, relative to the hold point
    ///    (e.g. move a long connector backwards so its tip sticks out of the fist). Only affects the hand,
    ///    so a plug seated in a socket is still placed by its own pivot.
    /// </summary>
    public class GrabPose : MonoBehaviour
    {
        [Range(0f, 1f)] public float closure = 0.8f;

        [Tooltip("Snap this object's pivot to the hand's Hold Point when grabbed. Turns Use Dynamic Attach off on the grab interactable.")]
        public bool snapToHoldPoint = true;
        [Tooltip("Extra shift while held, in hold-point space (metres). Move the object, not the hand: e.g. negative Z slides it back through the fist.")]
        public Vector3 holdOffset;
        [Tooltip("Extra rotation while held, in hold-point space (degrees).")]
        public Vector3 holdRotation;

        void Awake()
        {
            if (!snapToHoldPoint) return;
            var grab = GetComponentInParent<XRGrabInteractable>();
            if (grab != null) grab.useDynamicAttach = false;
        }

        // Scene view helper: wire sphere at the object's pivot.
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.015f);
        }
    }
}
