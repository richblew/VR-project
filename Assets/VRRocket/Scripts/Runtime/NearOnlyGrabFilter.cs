using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket
{
    /// <summary>
    /// Makes a part grabbable by direct (near) interaction only (SPEC.md section 5.1).
    /// The rig's hands are NearFarInteractors, one interactor for both near and far casting, so an interaction layer cannot
    /// separate the two. This filter rejects hover and select when the hand is further than <see cref="maxDistance"/>
    /// from the part's colliders, and always rejects ray and socket interactors. It lives on the part, so it works in any scene.
    /// </summary>
    [RequireComponent(typeof(XRBaseInteractable))]
    public sealed class NearOnlyGrabFilter : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        [SerializeField, Tooltip("Hands further than this from the part's colliders cannot hover or grab it.")]
        float m_MaxDistance = 0.12f;

        XRBaseInteractable m_Interactable;

        public float maxDistance
        {
            get => m_MaxDistance;
            set => m_MaxDistance = value;
        }

        public bool canProcess => isActiveAndEnabled;

        void Awake()
        {
            m_Interactable = GetComponent<XRBaseInteractable>();
            m_Interactable.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderVolume;
            m_Interactable.selectFilters.Add(this);
            m_Interactable.hoverFilters.Add(this);
        }

        void OnDestroy()
        {
            if (m_Interactable == null) return;
            m_Interactable.selectFilters.Remove(this);
            m_Interactable.hoverFilters.Remove(this);
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            // Never drop a selection that is already in progress because the hand moved away: the guide mechanic owns that.
            if (interactable.interactorsSelecting.Contains(interactor)) return true;
            return IsNear(interactor);
        }

        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
        {
            return IsNear(interactor);
        }

        bool IsNear(IXRInteractor interactor)
        {
            if (interactor is XRRayInteractor || interactor is XRSocketInteractor) return false;
            if (m_Interactable == null) return true;
            // Measure from the interactor itself, not its attach transform: a NearFarInteractor parks its attach
            // transform at the far-cast hit point, which would make every far target look near.
            var d2 = m_Interactable.GetDistance(interactor.transform.position).distanceSqr;
            return d2 <= m_MaxDistance * m_MaxDistance;
        }
    }
}
