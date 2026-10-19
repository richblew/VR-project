using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace VRRocket
{
    /// <summary>
    /// Holds the rocket after it changed hands (<see cref="RocketAssembly"/>, switching hands): it keeps the place it had in the
    /// hand when the other hand let go, relative to the hand itself, until this hand lets go too. The hand's attach point is not
    /// used, because a hand-model rig may move it: Yi-Xuan's hold point carries the gripped part's GrabPose offset (a flap's is
    /// 2 cm and a quarter turn), which goes once the hand holds the rocket instead, and the rocket would jump and turn in the hand.
    /// Not registered on its own: <see cref="RocketAssembly"/> adds it after the tube's other grab transformers, so it runs last.
    /// </summary>
    public sealed class HandOverHold : XRBaseGrabTransformer
    {
        protected override RegistrationMode registrationMode => RegistrationMode.None;

        IXRSelectInteractor m_Hand;
        Vector3 m_LocalPosition;     // the rocket in the hand's own space
        Quaternion m_LocalRotation;

        public bool isHolding => m_Hand != null;

        /// <summary>From now until this hand lets go, the rocket keeps its current place relative to the hand.</summary>
        public void Begin(IXRSelectInteractor hand)
        {
            m_Hand = hand;
            var h = hand.transform;
            var inv = Quaternion.Inverse(h.rotation);
            m_LocalPosition = inv * (transform.position - h.position);
            m_LocalRotation = inv * transform.rotation;
        }

        public void End() => m_Hand = null;

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            if (m_Hand == null) return;
            if (grabInteractable.interactorsSelecting.Count == 0 || grabInteractable.interactorsSelecting[0] != m_Hand)
            {
                m_Hand = null;
                return;
            }
            var h = m_Hand.transform;
            targetPose = new Pose(h.position + h.rotation * m_LocalPosition, h.rotation * m_LocalRotation);
        }
    }
}
