using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRRocket
{
    public enum AttachedGrabResult
    {
        /// <summary>Keep the part attached; the handler has written the pose.</summary>
        Hold,
        /// <summary>The handler wants the part pulled off: the transformer detaches it and it follows the hand.</summary>
        Detach,
    }

    /// <summary>
    /// Lets a part replace the default "grab an attached part re-enters the guide" behaviour of <see cref="GuideGrabTransformer"/>
    /// with its own, such as the motor cap twist (SPEC.md section 5.4). Also lets it offset where the part rests when seated.
    /// </summary>
    public interface IAttachedGrabHandler
    {
        /// <summary>Metres along the guide axis (away from the tube), before rocketScale, that the seated part rests from the attach point.</summary>
        float seatOffset { get; }

        /// <summary>Called when an attached part is grabbed. Return true to take over the grab; false to use the normal guide.</summary>
        bool OnAttachedGrabBegin(RocketPart part, AttachPoint point, XRGrabInteractable grab);

        /// <summary>Called every grab transformer process step while the handler owns the grab. Write the pose the part must take.</summary>
        AttachedGrabResult ProcessAttachedGrab(XRGrabInteractable grab, ref Pose targetPose);

        /// <summary>Called when the handler stops owning the grab: the hand let go, or it asked for a detach.</summary>
        void OnAttachedGrabEnd();
    }
}
