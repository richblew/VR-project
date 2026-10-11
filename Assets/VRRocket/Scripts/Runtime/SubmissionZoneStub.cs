using System.Collections;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// Stub of the submission bin (SPEC.md 8.5): a trigger box on the second desk. Releasing the complete rocket inside it and
    /// pressing the stub button calls <see cref="RocketAssembly.TrySubmit"/>, then the rocket is hidden as a stand-in for the bin animation.
    /// Delete when the real bin arrives.
    /// </summary>
    public sealed class SubmissionZoneStub : MonoBehaviour
    {
        [SerializeField] RocketAssembly m_Assembly;
        [SerializeField, Tooltip("Seconds after submission before the stub hides the rocket, standing in for the bin animation.")] float m_TakeAwayDelay = 0.6f;

        RocketPart m_Inside;

        public RocketAssembly assembly
        {
            get
            {
                if (m_Assembly == null) m_Assembly = FindFirstObjectByType<RocketAssembly>();
                return m_Assembly;
            }
            set => m_Assembly = value;
        }

        /// <summary>A loose body tube is resting inside the zone.</summary>
        public bool rocketInside => m_Inside != null && m_Inside.gameObject.activeInHierarchy && !m_Inside.isHeld;

        void OnTriggerEnter(Collider other)
        {
            var part = other.GetComponentInParent<RocketPart>();
            if (part != null && part.partType == PartType.BodyTube) m_Inside = part;
        }

        void OnTriggerExit(Collider other)
        {
            var part = other.GetComponentInParent<RocketPart>();
            if (part != null && part == m_Inside) m_Inside = null;
        }

        /// <summary>Wired to the stub Submit button.</summary>
        public void OnSubmitPressed()
        {
            var a = assembly;
            if (a == null) return;
            if (!rocketInside)
            {
                Debug.Log("SubmissionZoneStub: no rocket in the zone.");
                return;
            }
            if (a.TrySubmit()) StartCoroutine(TakeAway(a));
            else Debug.Log("SubmissionZoneStub: TrySubmit refused (state " + a.State + ").");
        }

        IEnumerator TakeAway(RocketAssembly a)
        {
            yield return new WaitForSeconds(m_TakeAwayDelay);
            // An abort or failed-launch return may already have brought the rocket back; never hide it then.
            if (a.State == AssemblyState.Submitted) a.SetRocketVisible(false);
            m_Inside = null;
        }
    }
}
