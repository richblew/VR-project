using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace VRRocket
{
    /// <summary>
    /// The guided attach mechanic of SPEC.md section 5.2, shared by every part.
    /// Free: the part follows the hand. Approach: accepting points within glowRadius glow. Guided: within captureRadius and
    /// orientation tolerance the part locks to the point's axis and slides between the seat and guideLength. Break away: sideways
    /// drift or pulling past the end releases the guide. Seat: releasing while guided animates to the seat and attaches.
    /// Remove: grabbing an attached part re-enters the guide at depth 0; pulling past guideLength detaches it.
    /// A part may replace the attached-grab behaviour through <see cref="IAttachedGrabHandler"/> (the motor cap twist).
    /// Transitions between free and guided poses are eased over guideBlendDuration so nothing jumps (SPEC.md 5.3).
    /// Registered in Start, after XRGrabInteractable adds its default transformer in Awake, so this one runs last and wins.
    /// </summary>
    [RequireComponent(typeof(RocketPart))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class GuideGrabTransformer : XRBaseGrabTransformer
    {
        [SerializeField, Tooltip("Optional. When empty the tuning asset is taken from the RocketWorkstation above this part.")]
        AssemblyTuning m_Tuning;

        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

        RocketPart m_Part;
        XRGrabInteractable m_Grab;
        RocketGrabInteractable m_RocketGrab;
        Rigidbody m_Body;
        Collider[] m_OwnColliders;
        IAttachedGrabHandler m_Handler;
        readonly List<Collider> m_IgnoredColliders = new List<Collider>(32);

        AttachPoint m_Point;
        Quaternion m_SeatedRotation;
        bool m_Guided;
        bool m_FromAttached;
        bool m_HandlerActive;
        Coroutine m_SeatRoutine;

        bool m_Blending;
        float m_BlendStart;
        Pose m_BlendFrom;
        float m_LastTickDepth;

        public AssemblyTuning tuning
        {
            get
            {
                if (m_Tuning == null)
                {
                    var ws = GetComponentInParent<RocketWorkstation>();
                    if (ws != null) m_Tuning = ws.tuning;
                }
                return m_Tuning;
            }
            set => m_Tuning = value;
        }

        public bool isGuided => m_Guided;
        public bool isHandlerActive => m_HandlerActive;
        public AttachPoint guidePoint => m_Point;

        void Awake()
        {
            m_Part = GetComponent<RocketPart>();
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Body = GetComponent<Rigidbody>();
            m_OwnColliders = GetComponentsInChildren<Collider>();
            m_Handler = GetComponent<IAttachedGrabHandler>();
            m_RocketGrab = m_Grab as RocketGrabInteractable;
            if (m_RocketGrab != null) m_RocketGrab.selectEntering += OnSelectEntering;
            else Debug.LogError("GuideGrabTransformer needs a RocketGrabInteractable so it can hook the grab before the rigidbody state is recorded.", this);
            m_Grab.selectExited.AddListener(OnSelectExited);
        }

        protected override void Start()
        {
            // XRGrabInteractable only adds its default XRGeneralGrabTransformer when no single transformer is registered by the
            // first update. This one registers in Start, which is earlier, so make sure the general transformer (the one that
            // makes the part follow the hand) is registered first. Ours then runs after it and overrides the pose while guided.
            var general = GetComponent<XRGeneralGrabTransformer>();
            if (general == null) general = gameObject.AddComponent<XRGeneralGrabTransformer>();
            var registered = false;
            for (var i = 0; i < m_Grab.singleGrabTransformersCount; i++)
                if (ReferenceEquals(m_Grab.GetSingleGrabTransformerAt(i), general)) registered = true;
            if (!registered) m_Grab.AddSingleGrabTransformer(general);
            base.Start();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_Grab == null) return;
            if (m_RocketGrab != null) m_RocketGrab.selectEntering -= OnSelectEntering;
            m_Grab.selectExited.RemoveListener(OnSelectExited);
        }

        float WorldScale => m_Part.transform.lossyScale.x;

        /// <summary>Where the part rests when seated on the current point (the attach point, plus any handler offset along the axis).</summary>
        Vector3 SeatPosition => m_Point.transform.position + m_Point.GuideAxisWorld * ((m_Handler != null ? m_Handler.seatOffset : 0f) * WorldScale);

        // ---- grab lifecycle ----

        void OnSelectEntering(SelectEnterEventArgs args)
        {
            // Runs before XRGrabInteractable.Grab() records the body's kinematic state, so an attached part must be dynamic here
            // or the grab interactable restores it to kinematic on release and refuses to throw it.
            if (m_Grab.interactorsSelecting.Count > 0) return; // second hand joining: keep state
            m_Grab.throwOnDetach = true;
            if (m_SeatRoutine != null)
            {
                StopCoroutine(m_SeatRoutine);
                m_SeatRoutine = null;
                FinishSeat();
            }
            m_Blending = false;
            if (m_Part.state == PartState.Attached && m_Part.attachedTo != null)
            {
                m_Point = m_Part.attachedTo;
                m_SeatedRotation = transform.rotation;
                m_Body.isKinematic = false;
                m_FromAttached = true;
                m_HandlerActive = false;
                m_Guided = false;
                // The handler has to be told after XRI has registered the interactor, which happens after this callback,
                // so defer the decision to the first Process step.
                m_PendingAttachedGrab = true;
            }
            else
            {
                m_Guided = false;
                m_FromAttached = false;
                m_HandlerActive = false;
                m_PendingAttachedGrab = false;
                m_Point = null;
            }
        }

        bool m_PendingAttachedGrab;

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (m_Grab.interactorsSelecting.Count > 0) return; // still held by the other hand
            m_PendingAttachedGrab = false;
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                // XRBaseInteractable.OnDisable releases a held object while it is being deactivated (scene unload, part
                // disabled). Re-parenting or starting coroutines there throws, so only drop the transient state.
                m_HandlerActive = false;
                m_Guided = false;
                m_Blending = false;
                return;
            }
            if (m_HandlerActive)
            {
                // Still attached (the cap stays seated). XRGrabInteractable throws in the manager's LateUpdate, after this
                // call, so suppress the throw, and put the part back to kinematic at its rest pose.
                m_HandlerActive = false;
                m_Handler.OnAttachedGrabEnd();
                m_Grab.throwOnDetach = false;
                FinishSeat();
                return;
            }
            if (m_Guided)
            {
                m_Grab.throwOnDetach = false;
                m_Guided = false;
                m_SeatRoutine = StartCoroutine(SeatRoutine());
            }
            else if (m_Part.state != PartState.Attached)
            {
                m_Part.SetFree();
                // XRGrabInteractable restores the parent it saw at grab time, which for a part pulled off the rocket is the
                // attach point. A loose part belongs to its tray group.
                if (transform.parent != m_Part.homeParent) transform.SetParent(m_Part.homeParent, true);
            }
        }

        // ---- per-frame ----

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            var t = tuning;
            if (t == null) return;
            var s = WorldScale;

            if (m_PendingAttachedGrab)
            {
                m_PendingAttachedGrab = false;
                if (m_Handler != null && m_Handler.OnAttachedGrabBegin(m_Part, m_Point, m_Grab))
                {
                    m_HandlerActive = true;
                }
                else
                {
                    m_Guided = true;
                    m_Part.SetGuided();
                    m_LastTickDepth = 0f;
                }
            }

            if (m_HandlerActive)
            {
                var result = m_Handler.ProcessAttachedGrab(m_Grab, ref targetPose);
                if (result == AttachedGrabResult.Detach)
                {
                    m_HandlerActive = false;
                    m_Handler.OnAttachedGrabEnd();
                    Detach();
                    // from here the part follows the hand; targetPose is already the free pose from the general transformer
                }
                ApplyBlend(ref targetPose, t);
                return;
            }

            if (!m_Guided)
            {
                var glowR = t.glowRadius * s;
                var captureR = t.captureRadius * s;
                AttachPoint best = null;
                var bestDist = float.MaxValue;
                var points = AttachPoint.active;
                for (var i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    if (!p.Accepts(m_Part)) continue;
                    var d = Vector3.Distance(targetPose.position, p.transform.position);
                    if (d >= glowR) continue;
                    // The motor presented nozzle-up is rejected completely: no glow, no snap (SPEC 5.3).
                    if (m_Part.partType == PartType.Motor && !OrientationOk(p, targetPose.rotation, t)) continue;
                    p.RequestGlow(1f - d / glowR);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = p;
                    }
                }
                if (best != null) m_Part.RequestGlow(1f - bestDist / glowR);
                if (best != null && bestDist <= captureR && OrientationOk(best, targetPose.rotation, t))
                    Engage(best, targetPose.rotation);
            }

            if (m_Guided)
            {
                var seat = SeatPosition;
                var axis = m_Point.GuideAxisWorld;
                var depth = GuideMath.Depth(targetPose.position, seat, axis);
                var sideways = GuideMath.Sideways(targetPose.position, seat, axis);
                var guideLength = t.GuideLengthFor(m_Part.partType) * s;
                var breakRadius = t.breakRadius * s;

                if (m_FromAttached)
                {
                    // An attached part only comes off by being pulled out past the end of the guide.
                    if (depth > guideLength)
                    {
                        Detach();
                        ApplyBlend(ref targetPose, t);
                        return;
                    }
                }
                else if (GuideMath.ShouldBreak(depth, sideways, guideLength, breakRadius))
                {
                    ReleaseGuide();
                    ApplyBlend(ref targetPose, t);
                    return;
                }

                // Nose magnetism fades in along the guide: exact follow at the outer end, full pull at the seat (SPEC 5.3).
                var fraction = 1f;
                if (m_Part.partType == PartType.NoseCone && guideLength > 0f)
                    fraction = Mathf.Lerp(t.noseMagnetism, 1f, Mathf.Clamp01(depth / guideLength));
                targetPose.position = GuideMath.GuidedPosition(seat, axis, depth, guideLength, fraction);
                targetPose.rotation = m_SeatedRotation;
                m_Point.RequestGlow(1f);
                m_Part.RequestGlow(1f);

                // Slot texture: one haptic tick per hapticSlideTickDistance of displayed travel. XRGrabInteractable runs the
                // transformers in the Dynamic and OnBeforeRender phases, so count in Dynamic only: once per frame at most.
                if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
                {
                    var shown = GuideMath.Depth(targetPose.position, seat, axis);
                    var lib = AssemblyFeedback.instance != null ? AssemblyFeedback.instance.library : null;
                    var tick = (lib != null ? lib.hapticSlideTickDistance : 0.004f) * s;
                    if (tick > 0f && Mathf.Abs(shown - m_LastTickDepth) >= tick)
                    {
                        m_LastTickDepth = shown;
                        AssemblyEvents.RaiseGuideSlideTick(m_Part);
                    }
                }
            }

            ApplyBlend(ref targetPose, t);
        }

        bool OrientationOk(AttachPoint point, Quaternion heldRotation, AssemblyTuning t)
        {
            var tolerance = t.OrientationToleranceFor(m_Part.partType);
            var partAxis = point.isSlot ? heldRotation * Vector3.forward : heldRotation * Vector3.up;
            return GuideMath.WithinOrientation(partAxis, point.ReferenceAxisWorld, tolerance);
        }

        // ---- transitions ----

        void StartBlend()
        {
            m_BlendFrom = new Pose(transform.position, transform.rotation);
            m_BlendStart = Time.time;
            m_Blending = true;
        }

        void ApplyBlend(ref Pose targetPose, AssemblyTuning t)
        {
            if (!m_Blending) return;
            var duration = t.guideBlendDuration;
            var k = duration > 0f ? (Time.time - m_BlendStart) / duration : 1f;
            if (k >= 1f)
            {
                m_Blending = false;
                return;
            }
            k = Mathf.SmoothStep(0f, 1f, k);
            targetPose.position = Vector3.Lerp(m_BlendFrom.position, targetPose.position, k);
            targetPose.rotation = Quaternion.Slerp(m_BlendFrom.rotation, targetPose.rotation, k);
        }

        void Engage(AttachPoint point, Quaternion heldRotation)
        {
            m_Point = point;
            m_SeatedRotation = GuideMath.SeatedRotation(m_Part.partType, point.transform.rotation, heldRotation);
            m_Guided = true;
            m_FromAttached = false;
            m_Part.SetGuided();
            m_LastTickDepth = GuideMath.Depth(transform.position, SeatPosition, point.GuideAxisWorld);
            SetRocketCollisionsIgnored(true);
            StartBlend();
            AssemblyEvents.RaiseGuideEngaged(m_Part, point);
        }

        void ReleaseGuide()
        {
            var point = m_Point;
            m_Guided = false;
            m_Point = null;
            m_Part.SetFree();
            SetRocketCollisionsIgnored(false);
            StartBlend();
            AssemblyEvents.RaiseGuideReleased(m_Part, point);
        }

        void Detach()
        {
            var point = m_Point;
            m_Guided = false;
            m_FromAttached = false;
            m_Point = null;
            point.ClearAttached();
            m_Part.SetFree();
            transform.SetParent(m_Part.homeParent, true);
            SetRocketCollisionsIgnored(false);
            StartBlend();
            AssemblyEvents.RaisePartRemoved(m_Part, point);
        }

        IEnumerator SeatRoutine()
        {
            var t = tuning;
            var duration = t != null ? t.SeatDurationFor(m_Part.partType) : 0.12f;
            if (!m_Body.isKinematic)
            {
                m_Body.linearVelocity = Vector3.zero;
                m_Body.angularVelocity = Vector3.zero;
            }
            m_Body.isKinematic = true;
            m_Body.interpolation = RigidbodyInterpolation.None;   // the seat animation drives the transform; interpolation would overwrite it
            var startPos = transform.position;
            var startRot = transform.rotation;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.SetPositionAndRotation(Vector3.Lerp(startPos, SeatPosition, k), Quaternion.Slerp(startRot, m_SeatedRotation, k));
                yield return null;
            }
            m_SeatRoutine = null;
            FinishSeat();
            AssemblyEvents.RaisePartSeated(m_Part, m_Point);
        }

        void FinishSeat()
        {
            if (m_Point == null) return;
            if (!m_Body.isKinematic)
            {
                m_Body.linearVelocity = Vector3.zero;
                m_Body.angularVelocity = Vector3.zero;
            }
            m_Body.isKinematic = true;
            m_Body.interpolation = RigidbodyInterpolation.None;
            transform.SetPositionAndRotation(SeatPosition, m_HandlerActive || m_FromAttached ? transform.rotation : m_SeatedRotation);
            transform.SetParent(m_Point.transform, true);
            m_Body.position = transform.position;
            m_Body.rotation = transform.rotation;
            m_Point.SetAttached(m_Part);
            m_Part.SetAttached(m_Point);
            m_FromAttached = false;
        }

        void SetRocketCollisionsIgnored(bool ignore)
        {
            if (ignore)
            {
                if (m_Point == null || m_Point.tube == null) return;
                m_IgnoredColliders.Clear();
                var rocketColliders = m_Point.tube.GetComponentsInChildren<Collider>();
                foreach (var c in rocketColliders)
                {
                    if (c.isTrigger || System.Array.IndexOf(m_OwnColliders, c) >= 0) continue;
                    foreach (var own in m_OwnColliders) Physics.IgnoreCollision(own, c, true);
                    m_IgnoredColliders.Add(c);
                }
            }
            else
            {
                // Parts never collide with each other (RocketAssembly.ApplyPartCollisionRules), so nothing is restored.
                m_IgnoredColliders.Clear();
            }
        }
    }
}
