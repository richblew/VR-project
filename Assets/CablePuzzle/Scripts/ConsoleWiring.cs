using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRRocket;

namespace CablePuzzle
{
    /// <summary>
    /// The cable puzzle as the console's wiring (VRRocket_Final), around Yi-Xuan's CablePairManager:
    /// <list type="bullet">
    /// <item>every plug going in or out clicks, pulses the hand that let go of it, and switches its pair's status light;</item>
    /// <item>when the last pair connects the console comes online (the manager's onAllConnected calls GameFlowController.MiniGameSolved),
    /// a firm lock click plays and the wiring locks: hands can no longer pull plugs out, the sockets keep them;</item>
    /// <item>when a new play needs the wiring done again (GameFlowController.ResetMiniGame), everything is unplugged, put back where
    /// it started, and the sockets' rows are shuffled so the cables cross differently each time.</item>
    /// </list>
    /// Put it on the PuzzleBox root.
    /// </summary>
    public class ConsoleWiring : MonoBehaviour, IXRSelectFilter
    {
        public CablePairManager manager;
        public GameFlowController flow;
        public AudioSource audioSource;
        [Tooltip("One status light per pair, in pair order (pair 1 first)")] public Renderer[] lights = new Renderer[0];
        public Material lightOff, lightOn;
        [Tooltip("Shuffle the sockets' rows at the start and on every reset")] public bool shuffle = true;

        class End
        {
            public XRGrabInteractable grab;
            public Rigidbody body;
            public Vector3 position;
            public Quaternion rotation;
            public IXRInteractor lastHand;
        }

        class Slot { public Vector3 anchor, receptacle; public Quaternion receptacleRotation; }

        readonly List<End> m_Ends = new List<End>();
        readonly List<PairSocket> m_Sockets = new List<PairSocket>();
        readonly List<VerletCable> m_Cables = new List<VerletCable>();
        readonly Dictionary<int, (Transform anchor, Transform receptacle)> m_ReceptacleSide = new Dictionary<int, (Transform, Transform)>();
        readonly List<Slot> m_Slots = new List<Slot>();
        readonly HashSet<int> m_Connected = new HashSet<int>();
        bool m_Locked, m_Resetting;

        public bool locked => m_Locked;
        public int connectedCount => m_Connected.Count;
        public int pairCount => m_Sockets.Count;
        public bool canProcess => isActiveAndEnabled;

        void Awake()
        {
            foreach (var c in GetComponentsInChildren<CableConnector>(true))
            {
                var e = new End { grab = c.GetComponent<XRGrabInteractable>(), body = c.GetComponent<Rigidbody>(), position = c.transform.localPosition, rotation = c.transform.localRotation };
                m_Ends.Add(e);
                if (!c.isPlug)
                {
                    var anchor = c.transform.parent.Find("Anchor_B");
                    if (anchor != null) m_ReceptacleSide[c.pairId] = (anchor, c.transform);
                }
            }
            m_Sockets.AddRange(GetComponentsInChildren<PairSocket>(true));
            m_Cables.AddRange(GetComponentsInChildren<VerletCable>(true));
            // the receptacle side's places (wall anchor + where the receptacle rests) are swapped between pairs to shuffle
            var ids = new List<int>(m_ReceptacleSide.Keys); ids.Sort();
            foreach (var id in ids)
            {
                var (anchor, receptacle) = m_ReceptacleSide[id];
                m_Slots.Add(new Slot { anchor = anchor.localPosition, receptacle = receptacle.localPosition, receptacleRotation = receptacle.localRotation });
            }
        }

        void OnEnable()
        {
            foreach (var e in m_Ends)
            {
                if (e.grab == null) continue;
                e.grab.selectFilters.Add(this);
                e.grab.selectExited.AddListener(OnEndReleased);
            }
            if (manager != null) manager.PairChanged += OnPairChanged;
            if (flow != null) flow.MiniGameChanged += OnMiniGameChanged;
        }

        void OnDisable()
        {
            foreach (var e in m_Ends)
            {
                if (e.grab == null) continue;
                e.grab.selectFilters.Remove(this);
                e.grab.selectExited.RemoveListener(OnEndReleased);
            }
            if (manager != null) manager.PairChanged -= OnPairChanged;
            if (flow != null) flow.MiniGameChanged -= OnMiniGameChanged;
        }

        void Start()
        {
            if (shuffle) Shuffle();
            ShowLights();
        }

        /// <summary>Once the wiring is done, only the sockets may hold the ends: hands cannot pull them out.</summary>
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => !m_Locked || interactor is XRSocketInteractor;

        void OnEndReleased(SelectExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            foreach (var e in m_Ends) if (ReferenceEquals(e.grab, args.interactableObject)) e.lastHand = args.interactorObject;
            var plug = args.interactableObject is Component c ? c.GetComponent<CableConnector>() : null;
            if (plug != null && plug.isPlug && isActiveAndEnabled && !m_Resetting) StartCoroutine(CheckWrongSocket(plug, args.interactorObject));
        }

        /// <summary>
        /// A plug let go right next to a socket of another colour: the socket will not take it, so the hand gets a buzz and the
        /// screens say why, instead of nothing happening.
        /// </summary>
        IEnumerator CheckWrongSocket(CableConnector plug, IXRInteractor hand)
        {
            yield return null;
            yield return null;
            var grab = plug.GetComponent<XRGrabInteractable>();
            if (grab == null || grab.isSelected) yield break;   // in its socket, or picked up again
            PairSocket nearest = null;
            var best = 0.045f * transform.lossyScale.x / 0.3f;
            foreach (var socket in m_Sockets)
            {
                if (socket == null || socket.hasSelection) continue;
                var d = Vector3.Distance(socket.transform.position, plug.transform.position);
                if (d < best) { best = d; nearest = socket; }
            }
            if (nearest == null || nearest.pairId == plug.pairId) yield break;
            var fb = AssemblyFeedback.instance;
            if (fb != null && fb.library != null && hand != null) fb.Play(hand, fb.library.hapticPartRemoved);
            if (flow != null) flow.Refuse("WRONG SOCKET", "NO CONNECTION", "Each plug only fits the socket of its own colour.");
        }

        IXRInteractor HandFor(int pairId)
        {
            foreach (var e in m_Ends)
                if (e.lastHand != null && e.grab != null && e.grab.TryGetComponent<CableConnector>(out var c) && c.pairId == pairId) return e.lastHand;
            return null;
        }

        void OnPairChanged(int pairId, bool connected)
        {
            if (connected) m_Connected.Add(pairId); else m_Connected.Remove(pairId);
            ShowLights();
            if (m_Resetting) return;
            var fb = AssemblyFeedback.instance;
            var lib = fb != null ? fb.library : null;
            var hand = HandFor(pairId);
            if (lib != null)
            {
                Play(connected ? lib.seat : lib.unseat);
                if (hand != null) fb.Play(hand, connected ? lib.hapticSeat : lib.hapticPartRemoved);
            }
            if (connected && m_Connected.Count == m_Sockets.Count && !m_Locked)
            {
                m_Locked = true;
                if (lib != null)
                {
                    Play(lib.capLock);
                    if (hand != null) fb.Play(hand, lib.hapticCapLock);
                }
            }
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        void ShowLights()
        {
            var ids = new List<int>(m_ReceptacleSide.Keys); ids.Sort();
            for (var i = 0; i < lights.Length && i < ids.Count; i++)
                if (lights[i] != null) lights[i].sharedMaterial = m_Connected.Contains(ids[i]) ? lightOn : lightOff;
        }

        void OnMiniGameChanged()
        {
            if (flow != null && !flow.miniGameSolved && (m_Locked || m_Connected.Count > 0) && isActiveAndEnabled) StartCoroutine(ResetWiring());
        }

        /// <summary>Unplugs everything, puts every end back where it started and shuffles the sockets' rows.</summary>
        public IEnumerator ResetWiring()
        {
            m_Resetting = true;
            m_Locked = false;
            foreach (var s in m_Sockets) s.socketActive = false;   // so they do not grab the plugs straight back
            foreach (var s in m_Sockets)
                if (s.hasSelection) s.interactionManager.SelectExit(s, s.interactablesSelected[0]);
            foreach (var e in m_Ends)
                if (e.grab != null && e.grab.isSelected) e.grab.interactionManager.SelectExit(e.grab.interactorsSelecting[0], e.grab);
            yield return null;
            foreach (var e in m_Ends)
            {
                if (e.grab == null) continue;
                var t = e.grab.transform;
                t.SetLocalPositionAndRotation(e.position, e.rotation);
                if (e.body != null) { e.body.linearVelocity = Vector3.zero; e.body.angularVelocity = Vector3.zero; e.body.position = t.position; e.body.rotation = t.rotation; }
                e.lastHand = null;
            }
            if (shuffle) Shuffle();
            yield return new WaitForFixedUpdate();
            foreach (var s in m_Sockets) s.socketActive = true;
            m_Connected.Clear();
            ShowLights();
            m_Resetting = false;
        }

        void Shuffle()
        {
            var ids = new List<int>(m_ReceptacleSide.Keys); ids.Sort();
            if (ids.Count < 2) return;
            var order = new List<int>();
            for (var i = 0; i < ids.Count; i++) order.Add(i);
            for (var i = order.Count - 1; i > 0; i--) { var j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
            for (var i = 0; i < ids.Count; i++)
            {
                var (anchor, receptacle) = m_ReceptacleSide[ids[i]];
                var slot = m_Slots[order[i]];
                anchor.localPosition = slot.anchor;
                receptacle.SetLocalPositionAndRotation(slot.receptacle, slot.receptacleRotation);
                var body = receptacle.GetComponent<Rigidbody>();
                if (body != null) { body.position = receptacle.position; body.rotation = receptacle.rotation; }
            }
            foreach (var c in m_Cables) c.ResetRope();
        }
    }
}
