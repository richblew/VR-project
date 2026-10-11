using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRRocket
{
    public enum ReturnMode { Editable, InspectOnly }

    /// <summary>
    /// The one component other systems talk to (SPEC.md section 8.4): the state machine of 5.5 with gating and backward moves,
    /// the handling rules of 5.6, the build report of section 7, submission, prototype return and new builds.
    /// Lives on the RocketWorkstation prefab root. Every event is also exposed as a UnityEvent for Inspector wiring.
    /// The body tube is a loose part like any other: it starts flat on the bench, parts attach to it wherever it is (held or
    /// resting), and every attached part is a kinematic child of its attach point, so the rocket moves as one object.
    /// </summary>
    public sealed class RocketAssembly : MonoBehaviour
    {
        [SerializeField] AssemblyTuning m_Tuning;
        [SerializeField] RocketWorkstation m_Workstation;
        [SerializeField] PartRespawner m_Respawner;

        [Header("Inspector events")]
        public UnityEvent<AssemblyState> onStateChanged = new UnityEvent<AssemblyState>();
        public UnityEvent onPartAttached = new UnityEvent();
        public UnityEvent onPartRemoved = new UnityEvent();
        public UnityEvent<BuildReport> onSubmitted = new UnityEvent<BuildReport>();
        public UnityEvent onPrototypeComplete = new UnityEvent();

        public event Action<AssemblyState> StateChanged;
        public event Action PartAttached;
        public event Action PartRemoved;
        public event Action<BuildReport> Submitted;

        readonly AssemblyStateMachine m_Machine = new AssemblyStateMachine();
        readonly Dictionary<int, FlapPlacement> m_FlapPlacements = new Dictionary<int, FlapPlacement>();
        readonly List<RocketPart> m_Scratch = new List<RocketPart>(16);
        readonly List<Collider> m_ColliderScratch = new List<Collider>(64);
        readonly Dictionary<RocketPart, GameObject> m_Proxies = new Dictionary<RocketPart, GameObject>();

        RocketPart m_Tube;
        MotorCapTwist m_CapTwist;
        RocketPart m_InspectPrototype;
        Transform m_InspectAnchor;
        bool m_Frozen;
        BuildReport m_LastReport;
        float m_TubeOutsideTimer;
        float m_InspectOutsideTimer;
        AssemblyState m_LastState;

        public AssemblyState State => m_Machine.State;
        public bool isFrozen => m_Frozen;
        public RocketPart tube => m_Tube;
        public RocketPart inspectPrototype => m_InspectPrototype;
        public AssemblyStateMachine machine => m_Machine;
        public BuildReport lastReport => m_LastReport;

        public bool enforceOrder
        {
            get => m_Machine.enforceOrder;
            set { m_Machine.enforceOrder = value; RefreshGating(); }
        }

        bool m_InteractionEnabled = true;

        /// <summary>The flow switches this off outside the Assembly phase: nothing can be grabbed and no point accepts anything.</summary>
        public bool interactionEnabled
        {
            get => m_InteractionEnabled;
            set { m_InteractionEnabled = value; RefreshGating(); }
        }

        public void Configure(AssemblyTuning tuning, RocketWorkstation workstation, PartRespawner respawner)
        {
            m_Tuning = tuning; m_Workstation = workstation; m_Respawner = respawner;
        }

        void Awake()
        {
            if (m_Workstation == null) m_Workstation = GetComponent<RocketWorkstation>();
            if (m_Workstation != null)
            {
                if (m_Tuning == null) m_Tuning = m_Workstation.tuning;
                if (m_Respawner == null) m_Respawner = m_Workstation.respawner;
                m_Tube = m_Workstation.tube;
            }
            if (m_Tuning != null) m_Machine.enforceOrder = m_Tuning.enforceOrder;
            m_LastState = State;
        }

        void OnEnable()
        {
            AssemblyEvents.PartSeated += OnPartSeated;
            AssemblyEvents.PartRemoved += OnPartRemoved;
        }

        void OnDisable()
        {
            AssemblyEvents.PartSeated -= OnPartSeated;
            AssemblyEvents.PartRemoved -= OnPartRemoved;
        }

        void Start()
        {
            EnsureSync(m_Tube);
            RefreshGating();
            ApplyPartCollisionRules();
        }

        static void EnsureSync(RocketPart tube)
        {
            if (tube != null && tube.GetComponent<AttachedPartSync>() == null) tube.gameObject.AddComponent<AttachedPartSync>();
        }

        void Update()
        {
            var delay = m_Tuning != null ? m_Tuning.respawnDelay : 1.5f;
            // a dropped working rocket goes back to its spot on the bench
            if (m_Tube != null && !m_Frozen && m_Tube.state == PartState.Free && !m_Tube.isHeld && m_Respawner != null)
            {
                if (m_Respawner.IsOutOfBounds(m_Tube.transform.position, out var below))
                {
                    m_TubeOutsideTimer += Time.deltaTime;
                    if (below || m_TubeOutsideTimer >= delay)
                    {
                        m_TubeOutsideTimer = 0f;
                        ReturnTubeHome();
                        AssemblyEvents.RaisePartRespawned(m_Tube);
                        RefreshGating();
                    }
                }
                else m_TubeOutsideTimer = 0f;
            }
            // a dropped inspection prototype goes back to where it was returned
            if (m_InspectPrototype != null && m_InspectAnchor != null && !m_InspectPrototype.isHeld && m_Respawner != null)
            {
                if (m_Respawner.IsOutOfBounds(m_InspectPrototype.transform.position, out var below))
                {
                    m_InspectOutsideTimer += Time.deltaTime;
                    if (below || m_InspectOutsideTimer >= delay)
                    {
                        m_InspectOutsideTimer = 0f;
                        m_Respawner.PlaceAtRest(m_InspectPrototype, m_InspectAnchor.position, m_InspectAnchor.rotation);
                        AssemblyEvents.RaisePartRespawned(m_InspectPrototype);
                    }
                }
                else m_InspectOutsideTimer = 0f;
            }
        }

        /// <summary>Puts the working rocket back at rest on its bench spot (the tube's tray slot).</summary>
        void ReturnTubeHome()
        {
            if (m_Tube == null) return;
            var body = m_Tube.body;
            if (m_Workstation != null && m_Respawner != null && m_Workstation.TryGetTubeHome(out var pos, out var rot))
                m_Respawner.PlaceAtRest(m_Tube, pos, rot);
            else if (body != null) body.isKinematic = false;
        }

        // ---- state machine input ----

        bool IsWorkingRocketPoint(AttachPoint point) => point != null && m_Tube != null && point.tube == m_Tube;

        void OnPartSeated(RocketPart part, AttachPoint point)
        {
            if (part.partType == PartType.BodyTube) return;
            if (!IsWorkingRocketPoint(point)) return;
            if (part.partType == PartType.WingFlap) RecordFlap(part, point);
            if (part.partType == PartType.MotorCap) m_CapTwist = part.GetComponent<MotorCapTwist>();
            AddProxyColliders(part);
            m_Machine.Apply(part.partType, true);
            PartAttached?.Invoke();
            onPartAttached.Invoke();
            AfterChange();
        }

        void OnPartRemoved(RocketPart part, AttachPoint point)
        {
            if (part.partType == PartType.BodyTube) return;
            if (!IsWorkingRocketPoint(point)) return;
            if (part.partType == PartType.WingFlap) m_FlapPlacements.Remove(part.partId);
            if (part.partType == PartType.MotorCap) m_CapTwist = null;
            RemoveProxyColliders(part);
            m_Machine.Apply(part.partType, false);
            PartRemoved?.Invoke();
            onPartRemoved.Invoke();
            AfterChange();
        }

        void RecordFlap(RocketPart part, AttachPoint point)
        {
            m_FlapPlacements[part.partId] = new FlapPlacement
            {
                flapId = part.partId,
                slotName = point.pointName,
                position = point.isLowFlapSlot ? FlapPosition.Base : FlapPosition.Midpoint,
                orientation = GuideMath.FlapOrientationFor(point.transform.rotation, part.transform.rotation),
            };
        }

        void AfterChange()
        {
            RefreshGating();
            var s = State;
            if (s != m_LastState)
            {
                m_LastState = s;
                StateChanged?.Invoke(s);
                onStateChanged.Invoke(s);
                if (s == AssemblyState.PrototypeComplete)
                {
                    AssemblyEvents.RaisePrototypeComplete(m_Tube);
                    onPrototypeComplete.Invoke();
                }
            }
        }

        /// <summary>Pushes the gating of 5.5 into every attach point of the working rocket.</summary>
        public void RefreshGating()
        {
            var points = AttachPoint.active;
            for (var i = 0; i < points.Count; i++)
            {
                var ap = points[i];
                var owner = ap.tube;
                if (owner != null && owner == m_Tube) ap.gatingAllows = m_InteractionEnabled && !m_Frozen && m_Machine.Allows(ap.accepts);
                else ap.gatingAllows = false;   // points of an inspection prototype, or of nothing
            }
        }

        /// <summary>
        /// The rocket is one physical unit: an attached part's colliders are mirrored onto a child of the tube, so the tube's
        /// single rigidbody carries the whole assembled shape (it rests on its fins, it is pushed as one piece). The part keeps its
        /// own kinematic body and collider only so it can be hovered and grabbed for removal.
        /// </summary>
        void AddProxyColliders(RocketPart part)
        {
            if (m_Tube == null || part == null || m_Proxies.ContainsKey(part)) return;
            var go = new GameObject("Compound_" + part.name);
            go.layer = m_Tube.gameObject.layer;
            go.transform.SetParent(m_Tube.transform, false);
            go.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
            foreach (var src in part.GetComponents<Collider>())
            {
                if (src.isTrigger) continue;
                switch (src)
                {
                    case BoxCollider b: { var d = go.AddComponent<BoxCollider>(); d.center = b.center; d.size = b.size; d.sharedMaterial = b.sharedMaterial; break; }
                    case CapsuleCollider c: { var d = go.AddComponent<CapsuleCollider>(); d.center = c.center; d.radius = c.radius; d.height = c.height; d.direction = c.direction; d.sharedMaterial = c.sharedMaterial; break; }
                    case SphereCollider sp: { var d = go.AddComponent<SphereCollider>(); d.center = sp.center; d.radius = sp.radius; d.sharedMaterial = sp.sharedMaterial; break; }
                    case MeshCollider m: { var d = go.AddComponent<MeshCollider>(); d.sharedMesh = m.sharedMesh; d.convex = true; d.sharedMaterial = m.sharedMaterial; break; }
                }
            }
            m_Proxies[part] = go;
            ApplyPartCollisionRules();
        }

        void RemoveProxyColliders(RocketPart part)
        {
            if (part == null || !m_Proxies.TryGetValue(part, out var go)) return;
            m_Proxies.Remove(part);
            if (go != null) Destroy(go);
        }

        /// <summary>
        /// Rocket parts never collide with each other: the guided mechanic assembles them, and a held part must not shove the
        /// tube around the bench or knock attached parts. Parts still collide with the bench, the floor and the room.
        /// </summary>
        public void ApplyPartCollisionRules()
        {
            m_ColliderScratch.Clear();
            void Collect(RocketPart p)
            {
                if (p == null) return;
                foreach (var c in p.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) m_ColliderScratch.Add(c);
            }
            if (m_Respawner != null) foreach (var p in m_Respawner.parts) Collect(p);
            Collect(m_Tube);
            Collect(m_InspectPrototype);
            for (var i = 0; i < m_ColliderScratch.Count; i++)
                for (var j = i + 1; j < m_ColliderScratch.Count; j++)
                    Physics.IgnoreCollision(m_ColliderScratch[i], m_ColliderScratch[j], true);
        }

        // ---- grab permission (5.5 removal exceptions, 5.6 handling, inspect-only) ----

        /// <summary>May this part be grabbed right now? Asked by <see cref="AssemblyGrabGate"/> before every hover and select.</summary>
        public bool CanGrab(RocketPart part)
        {
            if (part == null) return false;
            if (!m_InteractionEnabled) return false;
            var isInspect = m_InspectPrototype != null && (part == m_InspectPrototype || (part.attachedTo != null && part.attachedTo.tube == m_InspectPrototype));
            if (isInspect) return part == m_InspectPrototype;   // examine the frozen rocket, never alter it
            if (m_Frozen) return false;
            if (part.partType == PartType.BodyTube) return true;   // the working rocket can always be picked up; grabbing anywhere grabs the whole rocket
            if (part.state != PartState.Attached) return true;
            if (part.attachedTo == null || part.attachedTo.tube != m_Tube) return true;
            // A resting rocket is grabbed as a whole: pointing at it never picks an attached part. Parts come off two-handed,
            // while the rocket is held (or otherwise fixed) in the other hand, by a deliberate pull along their guide, when
            // the state machine allows.
            var rocketHeld = m_Tube.isHeld || (m_Tube.body != null && m_Tube.body.isKinematic);
            if (!rocketHeld) return false;
            return m_Machine.CanRemove(part.partType);
        }

        // ---- interface of 8.4 ----

        /// <summary>Valid from PrototypeComplete onward. Flap placements and the cap lock are read now, not at seating.</summary>
        public BuildReport GetReport()
        {
            var flaps = new FlapPlacement[3];
            for (var i = 0; i < 3; i++)
            {
                if (!m_FlapPlacements.TryGetValue(i + 1, out flaps[i]))
                    flaps[i] = new FlapPlacement { flapId = i + 1, slotName = "", position = FlapPosition.Base, orientation = FlapOrientation.Down };
            }
            var capLocked = m_CapTwist != null && m_CapTwist.capLocked;
            return BuildReport.Build(flaps, capLocked);
        }

        /// <summary>Called by the bin: freezes the rocket and raises Submitted. False unless the prototype is complete and not held.</summary>
        public bool TrySubmit()
        {
            if (m_Tube == null || State != AssemblyState.PrototypeComplete) return false;
            if (m_Tube.isHeld) return false;
            m_LastReport = GetReport();
            m_Machine.SetSubmitted(true);
            m_Frozen = true;
            var body = m_Tube.body;
            if (body != null)
            {
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true;
            }
            AfterChange();
            Submitted?.Invoke(m_LastReport);
            onSubmitted.Invoke(m_LastReport);
            return true;
        }

        /// <summary>Stub helper for the bin animation: hides or shows the submitted rocket.</summary>
        public void SetRocketVisible(bool visible)
        {
            if (m_Tube != null) m_Tube.gameObject.SetActive(visible);
        }

        /// <summary>Brings the submitted rocket back: editable onto its bench spot, or frozen at <paramref name="at"/> for inspection.</summary>
        public void ReturnPrototype(ReturnMode mode, Transform at)
        {
            if (m_Tube == null) return;
            m_Tube.gameObject.SetActive(true);
            if (mode == ReturnMode.InspectOnly)
            {
                if (m_InspectPrototype != null && m_InspectPrototype != m_Tube) Destroy(m_InspectPrototype.gameObject);
                m_InspectPrototype = m_Tube;
                m_InspectAnchor = at;
                var guide = m_Tube.GetComponent<GuideGrabTransformer>();
                if (guide != null) guide.enabled = false;        // it must never attach to anything again
                if (m_Tube.attachedTo != null) { m_Tube.attachedTo.ClearAttached(); }
                m_Tube.SetFree();
                m_Tube.transform.SetParent(m_Workstation != null ? m_Workstation.scaledRoot : transform, true);
                var body = m_Tube.body;
                if (body != null) body.isKinematic = false;
                if (at != null && m_Respawner != null) m_Respawner.PlaceAtRest(m_Tube, at.position, at.rotation);
                m_Tube = null;
                m_Frozen = false;
                RefreshGating();
            }
            else
            {
                m_Frozen = false;
                m_Machine.SetSubmitted(false);
                if (m_Tube.attachedTo != null) m_Tube.attachedTo.ClearAttached();
                m_Tube.SetFree();
                if (m_Tube.homeParent != null && m_Tube.transform.parent != m_Tube.homeParent) m_Tube.transform.SetParent(m_Tube.homeParent, true);
                ReturnTubeHome();
                AfterChange();
            }
        }

        /// <summary>Fresh tube and fresh parts on the tray. The inspection prototype, if any, is left alone.</summary>
        public void BeginNewBuild()
        {
            if (m_Workstation == null || !m_Workstation.canSpawnKit)
            {
                Debug.LogError("RocketAssembly.BeginNewBuild: the workstation has no kit prefabs configured.", this);
                return;
            }
            // remove the current loose parts and the working rocket (unless it has become the inspection prototype)
            if (m_Respawner != null)
            {
                m_Scratch.Clear();
                m_Scratch.AddRange(m_Respawner.parts);
                foreach (var p in m_Scratch)
                {
                    if (p == null) continue;
                    var ownerTube = p.attachedTo != null ? p.attachedTo.tube : null;
                    if (ownerTube != null && ownerTube == m_InspectPrototype) continue;
                    if (p.state == PartState.Free || ownerTube == m_Tube) Destroy(p.gameObject);
                }
            }
            if (m_Tube != null && m_Tube != m_InspectPrototype)
            {
                if (m_Tube.attachedTo != null) m_Tube.attachedTo.ClearAttached();
                Destroy(m_Tube.gameObject);
            }
            var newParts = new List<RocketPart>();
            var tube = m_Workstation.SpawnKit(newParts);
            if (m_Respawner != null) m_Respawner.SetParts(newParts);
            m_Proxies.Clear();   // the old tube took its compound colliders with it
            m_Tube = tube;
            EnsureSync(m_Tube);
            m_Machine.Reset();
            m_FlapPlacements.Clear();
            m_CapTwist = null;
            m_Frozen = false;
            m_TubeOutsideTimer = 0f;
            ApplyPartCollisionRules();
            AfterChange();
            AssemblyEvents.RaiseKitSpawned();
        }
    }
}
