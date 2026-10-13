using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket
{
    /// <summary>
    /// Decides whether an attached part, or the tube, may be grabbed at all (SPEC.md 5.5 removal exceptions and 5.6 handling).
    /// Asks the <see cref="RocketAssembly"/> above the part. A refused grab falls through to whatever else the hand touches,
    /// which is how "grabbing anywhere grabs the rocket" works once the prototype is out of the stand.
    /// </summary>
    [RequireComponent(typeof(RocketPart))]
    [RequireComponent(typeof(XRBaseInteractable))]
    public sealed class AssemblyGrabGate : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        RocketPart m_Part;
        XRBaseInteractable m_Interactable;
        RocketAssembly m_Assembly;

        public bool canProcess => isActiveAndEnabled;

        RocketAssembly assembly
        {
            get
            {
                if (m_Assembly == null) m_Assembly = GetComponentInParent<RocketAssembly>();
                return m_Assembly;
            }
        }

        void Awake()
        {
            m_Part = GetComponent<RocketPart>();
            m_Interactable = GetComponent<XRBaseInteractable>();
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
            if (interactable.interactorsSelecting.Contains(interactor)) return true;
            return Allowed();
        }

        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => Allowed();

        bool Allowed()
        {
            var a = assembly;
            if (a == null) return true;
            return a.CanGrab(m_Part);
        }
    }
}
