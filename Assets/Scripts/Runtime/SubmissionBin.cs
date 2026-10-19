using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRRocket
{
    /// <summary>What is in the submission bin, as the console's status monitor shows it.</summary>
    public enum BinState { Empty, RocketHeld, Ready, PartsMissing, OldRocket, Busy }

    /// <summary>
    /// The submission bin in the console (SPEC 9, built here as a working version). A trigger over the opening detects a rocket
    /// (a body tube); the console's Submit button calls <see cref="Submit"/>:
    /// <list type="bullet">
    /// <item>a complete rocket (all nine parts) is submitted and sinks into the bin;</item>
    /// <item>an unfinished one sinks for a moment, the screens say what is missing, and it comes back up on the workbench, editable;</item>
    /// <item>anything else (an empty bin, a rocket still in the hand, the rocket that failed, a rocket already submitted, the
    /// console offline) is refused with the reason on the screens, a buzz and a vibration.</item>
    /// </list>
    /// A rocket going in or coming out clicks.
    /// </summary>
    public sealed class SubmissionBin : MonoBehaviour
    {
        [SerializeField] RocketAssembly m_Assembly;
        [SerializeField] GameFlowController m_Flow;
        [SerializeField, Tooltip("Where a failed prototype comes back when the game flow has no failed-rocket spot of its own.")] Transform m_ReturnSpot;
        [SerializeField] float m_SinkDepth = 0.45f;
        [SerializeField] float m_SinkSeconds = 1.2f;
        [SerializeField, Tooltip("An unfinished rocket sinks this far, waits, then comes back up on the workbench.")] float m_CheckDepth = 0.3f;
        [SerializeField] float m_CheckSeconds = 0.8f;
        [SerializeField] AudioSource m_Audio;
        [SerializeField] AudioClip m_CloseClip;
        [SerializeField] AudioClip m_OpenClip;

        public UnityEvent onAccepted = new UnityEvent();
        public UnityEvent onRefused = new UnityEvent();

        RocketPart m_Inside;
        bool m_Busy;

        public RocketAssembly assembly
        {
            get
            {
                if (m_Assembly == null) m_Assembly = FindAnyObjectByType<RocketAssembly>();
                return m_Assembly;
            }
            set => m_Assembly = value;
        }

        GameFlowController flow
        {
            get
            {
                if (m_Flow == null) m_Flow = FindAnyObjectByType<GameFlowController>();
                return m_Flow;
            }
        }

        public Transform returnSpot => m_ReturnSpot;
        public bool rocketInside => m_Inside != null && m_Inside.gameObject.activeInHierarchy && !m_Inside.isHeld;
        /// <summary>The rocket (body tube) over or in the bin, if any.</summary>
        public RocketPart rocket => m_Inside != null && m_Inside.gameObject.activeInHierarchy ? m_Inside : null;

        /// <summary>What is in the bin now.</summary>
        public BinState state
        {
            get
            {
                if (m_Busy) return BinState.Busy;
                var r = rocket;
                if (r == null) return BinState.Empty;
                if (r.isHeld) return BinState.RocketHeld;
                var a = assembly;
                if (a == null || r != a.tube) return BinState.OldRocket;
                return a.isComplete ? BinState.Ready : BinState.PartsMissing;
            }
        }

        public void Configure(RocketAssembly assembly, Transform returnSpot, AudioSource audio, AudioClip close, AudioClip open)
        {
            m_Assembly = assembly; m_ReturnSpot = returnSpot; m_Audio = audio; m_CloseClip = close; m_OpenClip = open;
        }

        void OnTriggerEnter(Collider other)
        {
            var part = other.GetComponentInParent<RocketPart>();
            if (part == null || part.partType != PartType.BodyTube || part == m_Inside) return;
            m_Inside = part;
            if (!m_Busy) Click(true);
        }

        void OnTriggerExit(Collider other)
        {
            var part = other.GetComponentInParent<RocketPart>();
            if (part == null || part != m_Inside || m_Busy) return;
            // a rocket with several colliders leaves only when none of them is still inside
            foreach (var c in part.GetComponentsInChildren<Collider>())
                if (c != other && c.enabled && !c.isTrigger && GetComponent<Collider>().bounds.Intersects(c.bounds)) return;
            m_Inside = null;
            Click(false);
        }

        void Click(bool goingIn)
        {
            var lib = AssemblyFeedback.instance != null ? AssemblyFeedback.instance.library : null;
            var clip = lib == null ? null : goingIn ? lib.seat : lib.unseat;
            if (m_Audio != null && clip != null) m_Audio.PlayOneShot(clip, 0.6f);
        }

        void Refuse(string title, string big, string detail)
        {
            onRefused.Invoke();
            if (flow != null) flow.Refuse(title, big, detail);
        }

        /// <summary>Wired to the console's Submit button.</summary>
        public void Submit()
        {
            var a = assembly;
            if (a == null) return;
            var f = flow;
            if (f != null && (f.phase == GamePhase.Menu || f.phase == GamePhase.Intro)) { Refuse(null, null, null); return; }
            // the console is offline until the wiring puzzle is solved (VRRocket_Final; see the mini game block in GameFlowController.cs)
            if (f != null && !f.consoleOnline) { onRefused.Invoke(); f.ReportConsoleOffline(); return; }
            if (m_Busy || a.State == AssemblyState.Submitted || (f != null && f.phase != GamePhase.Assembly))
            {
                Refuse("ALREADY SUBMITTED", "ON THE PAD", "Your rocket is on the pad. Press LAUNCH, or ABORT to bring it back.");
                return;
            }
            switch (state)
            {
                case BinState.Empty:
                    Refuse("BIN EMPTY", "NOTHING TO SUBMIT", "Put your rocket in the bin, let go of it, then press SUBMIT.");
                    return;
                case BinState.RocketHeld:
                    Refuse("LET GO OF THE ROCKET", "STILL IN YOUR HAND", "Rest the rocket in the bin and let go of it, then press SUBMIT.");
                    return;
                case BinState.OldRocket:
                    Refuse("WRONG ROCKET", "THE FAILED ONE", "That is the rocket that failed. Put your new build in the bin.");
                    return;
                case BinState.PartsMissing:
                    StartCoroutine(BounceBack(a, m_Inside, a.machine.DescribeMissing()));
                    return;
            }
            var tube = m_Inside;
            if (!a.TrySubmit())
            {
                Refuse("LET GO OF THE ROCKET", "STILL IN YOUR HAND", "Rest the rocket in the bin and let go of it, then press SUBMIT.");
                return;
            }
            onAccepted.Invoke();
            StartCoroutine(Sink(a, tube));
        }

        IEnumerator Sink(RocketAssembly a, RocketPart tube)
        {
            m_Busy = true;
            if (m_Audio != null && m_CloseClip != null) m_Audio.PlayOneShot(m_CloseClip);
            var t = tube.transform;
            var start = t.position;
            var end = start + Vector3.down * m_SinkDepth;
            var elapsed = 0f;
            while (elapsed < m_SinkSeconds)
            {
                elapsed += Time.deltaTime;
                if (tube == null || a.State != AssemblyState.Submitted) { m_Busy = false; yield break; }   // aborted meanwhile
                t.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / m_SinkSeconds));
                yield return null;
            }
            if (a.State == AssemblyState.Submitted) a.SetRocketVisible(false);
            m_Inside = null;
            m_Busy = false;
        }

        /// <summary>
        /// An unfinished rocket: the bin takes it, the screens say what is missing, and it comes back up on the workbench next to
        /// the parts still on the tray, editable. Not a flight: nothing is launched or counted.
        /// </summary>
        IEnumerator BounceBack(RocketAssembly a, RocketPart tube, string missing)
        {
            if (!a.TryTakeIncomplete())
            {
                Refuse("LET GO OF THE ROCKET", "STILL IN YOUR HAND", "Rest the rocket in the bin and let go of it, then press SUBMIT.");
                yield break;
            }
            m_Busy = true;
            onRefused.Invoke();
            if (flow != null) flow.Refuse("PARTS MISSING", "INCOMPLETE", "Missing: " + missing + ". It is back on your workbench.");
            if (m_Audio != null && m_CloseClip != null) m_Audio.PlayOneShot(m_CloseClip);
            var t = tube.transform;
            var start = t.position;
            var end = start + Vector3.down * m_CheckDepth;
            for (var elapsed = 0f; elapsed < m_CheckSeconds; elapsed += Time.deltaTime)
            {
                if (tube == null) { m_Busy = false; yield break; }
                t.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / m_CheckSeconds));
                yield return null;
            }
            tube.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.4f);
            m_Inside = null;
            PlayOpen();
            a.ReturnIncomplete();
            m_Busy = false;
        }

        /// <summary>Plays the "bin opens" sound when something is returned through it.</summary>
        public void PlayOpen()
        {
            if (m_Audio != null && m_OpenClip != null) m_Audio.PlayOneShot(m_OpenClip);
        }
    }
}
