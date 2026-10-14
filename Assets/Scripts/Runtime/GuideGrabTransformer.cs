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
        Quaternion m_SeatedLocal;   // the seated rotation relative to the point: follows the tube if it turns mid-insert
        bool m_Guided;
        bool m_FromAttached;
        bool m_HandlerActive;
        Coroutine m_SeatRoutine;

        bool m_MovementOverridden;
        XRBaseInteractable.MovementType m_SavedMovementType;
        bool m_WrongWayWarned;
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

        /// <summary>
        /// The rotation the part takes on its point, worked out from the point's rotation now: a rocket turned in the other hand
        /// while a part is on its track keeps the part straight in it, and it seats straight.
        /// </summary>
        Quaternion SeatedRotation => m_Point != null ? m_Point.transform.rotation * m_SeatedLocal : transform.rotation;

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
                m_SeatedLocal = Quaternion.Inverse(m_Point.transform.rotation) * transform.rotation;
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
            m_WrongWayWarned = false;
            if (m_MovementOverridden)
            {
                m_MovementOverridden = false;
                m_Grab.movementType = m_SavedMovementType;
            }
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
            if (!m_Guided && m_Part.partType == PartType.Motor && m_Part.state != PartState.Attached)
            {
                // Let go with its + end in the tube's base: the battery slides home (it is never left half inside the tube).
                // Let go outside the base, it simply drops.
                var t = tuning;
                var entry = t != null ? BatteryEntry(new Pose(transform.position, transform.rotation), t, WorldScale, out _, out _) : null;
                if (entry != null) Engage(entry, transform.rotation);
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
                    // The handler sets the pose every frame (the twisted cap): place it directly, not through physics a step
                    // behind the rocket in the other hand.
                    m_SavedMovementType = m_Grab.movementType;
                    m_MovementOverridden = true;
                    m_Grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
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

            if (!m_Guided && m_Part.partType == PartType.Motor)
            {
                // The battery is caught only once its + end is in the tube's base, never before (no snap zone outside it)
                KeepWrongWayBatteryOut(ref targetPose, t, s);
                var entry = BatteryEntry(targetPose, t, s, out var near, out var glow);
                if (near != null) { near.RequestGlow(glow); m_Part.RequestGlow(glow); }
                if (entry != null) Engage(entry, targetPose.rotation);
            }
            else if (!m_Guided)
            {
                var glowR = Mathf.Max(t.glowRadius, t.captureRadius) * s;
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

                if (m_Part.partType == PartType.Motor)
                {
                    // The battery runs straight on the tube's axis, its + end as far into the tube as the hand holds it; hand drift
                    // and tilt never take it off the track or through the wall. Only pulling its + end back out of the tube frees it.
                    var tipIn = Vector3.Dot(BatteryTip(targetPose, t, s) - TubeEnd(m_Point), -axis);
                    if (tipIn < -t.motorExitMargin * s)
                    {
                        if (m_FromAttached) Detach(); else ReleaseGuide();
                        ApplyBlend(ref targetPose, t);
                        return;
                    }
                    depth = Mathf.Max(0f, SeatTipIn(m_Point, t, s) - tipIn);
                }
                else if (m_FromAttached)
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
                targetPose.position = GuideMath.GuidedPosition(seat, axis, depth, Mathf.Max(guideLength, depth), fraction);
                targetPose.rotation = SeatedRotation;
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

        AttachPoint NearestAccepting(Vector3 position, float radius)
        {
            AttachPoint best = null;
            var bestDist = radius;
            var points = AttachPoint.active;
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (!p.Accepts(m_Part)) continue;
                var d = Vector3.Distance(position, p.transform.position);
                if (d <= bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        /// <summary>The battery's + end (its pivot is the - end; it goes into the tube + end first).</summary>
        static Vector3 BatteryTip(Pose pose, AssemblyTuning t, float s) => pose.position + pose.rotation * Vector3.up * (t.motorLength * s);

        /// <summary>How far into the tube the battery's + end is when seated.</summary>
        static float SeatTipIn(AttachPoint motorSeat, AssemblyTuning t, float s) =>
            t.motorLength * s + Vector3.Dot(motorSeat.transform.position - TubeEnd(motorSeat), -motorSeat.GuideAxisWorld);

        /// <summary>
        /// The battery seat whose tube's base the battery's + end is in: past the tube's end (a little or a long way), within the
        /// opening (Motor Entry Radius of the axis) and leaning no more than Motor Entry Tilt. Null when it is not in any.
        /// <paramref name="glowPoint"/> and <paramref name="glow"/> give the approach glow (the + end nearing a base, the right way up).
        /// </summary>
        AttachPoint BatteryEntry(Pose pose, AssemblyTuning t, float s, out AttachPoint glowPoint, out float glow)
        {
            glowPoint = null; glow = 0f;
            AttachPoint entry = null;
            var bestIn = float.MaxValue;
            var tip = BatteryTip(pose, t, s);
            var up = pose.rotation * Vector3.up;
            var glowR = t.glowRadius * s;
            var points = AttachPoint.active;
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (!p.Accepts(m_Part)) continue;
                var into = -p.GuideAxisWorld;
                if (Vector3.Angle(up, into) > t.motorEntryTilt) continue;   // too far over, or the wrong way up: no glow, no catch
                var end = TubeEnd(p);
                var d = Vector3.Distance(tip, end);
                if (d < glowR && 1f - d / glowR > glow) { glow = 1f - d / glowR; glowPoint = p; }
                var tipIn = Vector3.Dot(tip - end, into);
                var off = GuideMath.Sideways(tip, end, into);
                if (tipIn <= 0f || tipIn > SeatTipIn(p, t, s) + t.motorLength * s || off > t.motorEntryRadius * s) continue;
                if (tipIn < bestIn) { bestIn = tipIn; entry = p; }
            }
            return entry;
        }

        /// <summary>
        /// A battery the wrong way up (or leaning too far) is refused, and it must not pass into the tube either: parts do not
        /// collide with each other, so near the base it is held out below the tube's end along the axis, with one buzz.
        /// </summary>
        void KeepWrongWayBatteryOut(ref Pose targetPose, AssemblyTuning t, float s)
        {
            var seatPoint = NearestAccepting(targetPose.position, (2f * t.motorLength + t.motorEntryRadius) * s);
            if (seatPoint == null || OrientationOk(seatPoint, targetPose.rotation, t)) return;
            var away = seatPoint.GuideAxisWorld;
            var seat = seatPoint.transform.position;
            var top = targetPose.position + targetPose.rotation * Vector3.up * (t.motorLength * s);
            var tubeEnd = TubeEndDepth(seatPoint);
            var nearest = Mathf.Min(GuideMath.Depth(targetPose.position, seat, away), GuideMath.Depth(top, seat, away));
            var offAxis = Mathf.Min(GuideMath.Sideways(targetPose.position, seat, away), GuideMath.Sideways(top, seat, away));
            if (nearest >= tubeEnd || offAxis > t.motorBlockRadius * s) return;
            targetPose.position += away * (tubeEnd - nearest);
            if (m_WrongWayWarned) return;
            m_WrongWayWarned = true;
            var fb = AssemblyFeedback.instance;
            if (fb != null && fb.library != null && m_Grab.interactorsSelecting.Count > 0) fb.Play(m_Grab.interactorsSelecting[0], fb.library.hapticPartRemoved);
        }

        /// <summary>How far below the battery's seat the tube ends (its cap seat), along the guide axis; 0 when it has none.</summary>
        static float TubeEndDepth(AttachPoint motorSeat) =>
            Mathf.Max(0f, GuideMath.Depth(TubeEnd(motorSeat), motorSeat.transform.position, motorSeat.GuideAxisWorld));

        /// <summary>The tube's base end (its cap seat) for a battery seat; the seat itself when the tube has none.</summary>
        static Vector3 TubeEnd(AttachPoint motorSeat)
        {
            var points = AttachPoint.active;
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (p.pointName == "CapSeat" && p.tube == motorSeat.tube) return p.transform.position;
            }
            return motorSeat.transform.position;
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
            m_SeatedLocal = Quaternion.Inverse(point.transform.rotation) * GuideMath.SeatedRotation(m_Part.partType, point.transform.rotation, heldRotation);
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
                transform.SetPositionAndRotation(Vector3.Lerp(startPos, SeatPosition, k), Quaternion.Slerp(startRot, SeatedRotation, k));
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
            transform.SetPositionAndRotation(SeatPosition, m_HandlerActive || m_FromAttached ? transform.rotation : SeatedRotation);
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
