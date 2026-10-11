using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRRocket
{
    /// <summary>
    /// The submission bin in the console (SPEC 9, built here as a working version). A trigger over the opening detects the loose
    /// complete rocket; the console's Submit button calls <see cref="Submit"/>, which asks the assembly to submit and then sinks the
    /// rocket into the bin. A failed prototype comes back up at <see cref="returnSpot"/>.
    /// </summary>
    public sealed class SubmissionBin : MonoBehaviour
    {
        [SerializeField] RocketAssembly m_Assembly;
        [SerializeField, Tooltip("Where a failed prototype comes back for inspection.")] Transform m_ReturnSpot;
        [SerializeField] float m_SinkDepth = 0.45f;
        [SerializeField] float m_SinkSeconds = 1.2f;
        [SerializeField] AudioSource m_Audio;
        [SerializeField] AudioClip m_CloseClip;
        [SerializeField] AudioClip m_OpenClip;

        public UnityEvent onAccepted = new UnityEvent();
        public UnityEvent onRefused = new UnityEvent();

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

        public Transform returnSpot => m_ReturnSpot;
        public bool rocketInside => m_Inside != null && m_Inside.gameObject.activeInHierarchy && !m_Inside.isHeld;

        public void Configure(RocketAssembly assembly, Transform returnSpot, AudioSource audio, AudioClip close, AudioClip open)
        {
            m_Assembly = assembly; m_ReturnSpot = returnSpot; m_Audio = audio; m_CloseClip = close; m_OpenClip = open;
        }

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

        /// <summary>Wired to the console's Submit button.</summary>
        public void Submit()
        {
            var a = assembly;
            if (a == null) return;
            if (!rocketInside || a.State != AssemblyState.PrototypeComplete)
            {
                onRefused.Invoke();
                return;
            }
            var tube = m_Inside;
            if (!a.TrySubmit())
            {
                onRefused.Invoke();
                return;
            }
            onAccepted.Invoke();
            StartCoroutine(Sink(a, tube));
        }

        IEnumerator Sink(RocketAssembly a, RocketPart tube)
        {
            if (m_Audio != null && m_CloseClip != null) m_Audio.PlayOneShot(m_CloseClip);
            var t = tube.transform;
            var start = t.position;
            var end = start + Vector3.down * m_SinkDepth;
            var elapsed = 0f;
            while (elapsed < m_SinkSeconds)
            {
                elapsed += Time.deltaTime;
                if (tube == null || a.State != AssemblyState.Submitted) yield break;   // aborted meanwhile
                t.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / m_SinkSeconds));
                yield return null;
            }
            if (a.State == AssemblyState.Submitted) a.SetRocketVisible(false);
            m_Inside = null;
        }

        /// <summary>Plays the "bin opens" sound when something is returned through it.</summary>
        public void PlayOpen()
        {
            if (m_Audio != null && m_OpenClip != null) m_Audio.PlayOneShot(m_OpenClip);
        }
    }
}
