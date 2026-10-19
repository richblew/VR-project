using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket
{
    /// <summary>
    /// The battery cap twist (SPEC.md section 5.4; the kit's motor shows as a battery). The cap is guided to CapSeat like any part
    /// and rests seated but unlocked, capUnlockedGap proud of the tube. Gripping the seated cap does not move it: it turns about the
    /// rocket's axis with the twist of the wrist, clockwise only viewed from below, as a ratchet. Every capDetentAngle of new
    /// progress ticks; at capLockAngle it locks, the gap closes and it can no longer be turned or removed.
    /// <list type="bullet">
    /// <item>Everything is worked out in the seat's own frame, so the cap always turns about the tube's current axis, however the
    /// rocket has been turned since the cap was seated, and it is left square to the tube when let go.</item>
    /// <item>The turn is the twist of the hand's rotation about the axis (swing-twist decomposition), times capTwistSensitivity,
    /// so one comfortable wrist turn closes it. Tilting the hand does not count.</item>
    /// <item>Letting go after capAutoFinishFraction of the lock angle finishes the lock.</item>
    /// <item>An unlocked cap comes off only when pulled more than capPullOff, mostly along the axis, while the rocket is held or
    /// on the stand; its progress then resets.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(RocketPart))]
    public sealed class MotorCapTwist : MonoBehaviour, IAttachedGrabHandler
    {
        [SerializeField, Tooltip("Optional. When empty the tuning asset is taken from the RocketWorkstation above this part.")]
        AssemblyTuning m_Tuning;
        [SerializeField, Tooltip("Clockwise viewed from below the rocket is a negative Unity yaw about the tube's +Y. Flip if headset testing says the thread runs the wrong way.")]
        bool m_ClockwiseFromBelowIsNegativeYaw = true;
        [SerializeField, Tooltip("Seconds for the 2 mm gap to close when the cap locks.")]
        float m_GapCloseDuration = 0.1f;
        [Header("Twist hint")]
        [SerializeField, Tooltip("While the cap is seated but not locked, an arrow circles it in the direction it twists and its glow pulses, so the player knows it needs turning")]
        bool m_ShowTwistHint = true;
        [SerializeField, Tooltip("Degrees per second the arrow travels round the cap")] float m_HintSpeed = 120f;
        [SerializeField, Tooltip("Arrow radius round the tube's axis, metres before scale")] float m_HintRadius = 0.026f;
        [SerializeField, ColorUsage(false, true)] Color m_HintColor = new Color(1f, 0.55f, 0.1f) * 2f;

        readonly CapTwistLogic m_Logic = new CapTwistLogic();
        RocketPart m_Part;
        AttachPoint m_Point;
        IXRInteractor m_Interactor;
        Quaternion m_BaseLocalRotation = Quaternion.identity;   // the cap's rotation in the seat's frame at zero progress
        Quaternion m_PrevHandLocal;                               // the hand's rotation in the seat's frame, last step
        Vector3 m_StartLocal;                                     // where the grabbed pose started, in the seat's frame
        bool m_HasStart;
        float m_GapShown;               // current rest offset from the seat, metres before scale
        float m_TextureAccum;
        bool m_Active;

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

        public bool capLocked => m_Logic.locked;
        public float progressDegrees => m_Logic.progress;
        public bool isTwisting => m_Active;
        public float seatOffset => m_GapShown;
        /// <summary>The twist hint (arrow round the cap, pulsing glow) is showing: seated on the rocket being built, not locked.</summary>
        public bool twistHintShown => m_Hint != null && m_Hint.gameObject.activeSelf;
        /// <summary>The arrow's current angle round the tube's axis (it travels the way the cap twists).</summary>
        public float twistHintAngle => m_HintAngle;

        Transform m_Hint;
        float m_HintAngle;
        RocketAssembly m_Assembly;
        MaterialPropertyBlock m_HintBlock;

        void Awake()
        {
            m_Part = GetComponent<RocketPart>();
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
            var t = tuning;
            if (t != null)
            {
                m_Logic.lockAngle = t.capLockAngle;
                m_Logic.detentAngle = t.capDetentAngle;
                m_GapShown = t.capUnlockedGap;
            }
        }

        void OnPartSeated(RocketPart part, AttachPoint point)
        {
            if (part != m_Part) return;
            m_Point = point;
            // Zero progress is the rotation the cap seated with, kept in the seat's frame so it follows the rocket.
            var local = Quaternion.Inverse(point.transform.rotation) * transform.rotation;
            m_BaseLocalRotation = Quaternion.AngleAxis(-SignedProgress(), Vector3.up) * local;
            var t = tuning;
            m_GapShown = m_Logic.locked ? 0f : (t != null ? t.capUnlockedGap : 0.002f);
        }

        // ---- twist hint ----

        void Update()
        {
            var show = m_ShowTwistHint && m_Point != null && m_Part.state == PartState.Attached && m_Part.attachedTo == m_Point && !m_Logic.locked && OnWorkingRocket();
            if (!show)
            {
                if (m_Hint != null && m_Hint.gameObject.activeSelf) m_Hint.gameObject.SetActive(false);
                return;
            }
            if (m_Hint == null || m_Hint.parent != m_Point.transform) BuildHint();
            if (!m_Hint.gameObject.activeSelf) m_Hint.gameObject.SetActive(true);
            // travel the way the cap locks: clockwise viewed from below
            m_HintAngle = Mathf.Repeat(m_HintAngle + (m_ClockwiseFromBelowIsNegativeYaw ? -1f : 1f) * m_HintSpeed * Time.deltaTime, 360f);
            m_Hint.localRotation = Quaternion.AngleAxis(m_HintAngle, Vector3.up);
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            if (m_HintBlock == null) m_HintBlock = new MaterialPropertyBlock();
            m_HintBlock.SetColor("_BaseColor", m_HintColor * (0.55f + 0.45f * pulse));
            foreach (var line in m_Hint.GetComponentsInChildren<LineRenderer>()) line.SetPropertyBlock(m_HintBlock);
            if (!m_Active) m_Part.RequestGlow(0.35f + 0.5f * pulse);
        }

        bool OnWorkingRocket()
        {
            if (m_Assembly == null) m_Assembly = FindAnyObjectByType<RocketAssembly>();
            return m_Assembly != null && m_Assembly.interactionEnabled && !m_Assembly.isFrozen && m_Point.tube != null && m_Point.tube == m_Assembly.tube;
        }

        /// <summary>A curved arrow round the cap, in the seat's frame (so it stays with the rocket), drawn with the glow material.</summary>
        void BuildHint()
        {
            if (m_Hint != null) Destroy(m_Hint.gameObject);
            m_Hint = new GameObject("TwistHint").transform;
            m_Hint.SetParent(m_Point.transform, false);
            m_Hint.localPosition = Vector3.down * 0.005f;   // round the middle of the cap, below the tube's end
            var material = m_Part.glowRenderer != null ? m_Part.glowRenderer.sharedMaterial : null;
            var dir = m_ClockwiseFromBelowIsNegativeYaw ? -1f : 1f;
            const int n = 28; const float sweep = 250f;
            var arc = Line("Arc", material, n);
            Vector3 At(float deg) { var a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * m_HintRadius; }
            for (var i = 0; i < n; i++) arc.SetPosition(i, At(dir * sweep * i / (n - 1)));
            // the arrowhead at the leading end, pointing the way it travels
            var end = dir * sweep;
            var tip = At(end) + Quaternion.AngleAxis(dir * 90f, Vector3.up) * At(end).normalized * 0.008f;
            var head = Line("Head", material, 3);
            head.SetPosition(0, At(end) * 1.25f);
            head.SetPosition(1, tip);
            head.SetPosition(2, At(end) * 0.75f);
        }

        LineRenderer Line(string name, Material material, int points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(m_Hint, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points;
            line.widthMultiplier = 0.003f * WorldScale;
            line.numCapVertices = 2; line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (material != null) line.sharedMaterial = material;
            return line;
        }

        void OnPartRemoved(RocketPart part, AttachPoint point)
        {
            if (part != m_Part) return;
            m_Logic.Reset();
            var t = tuning;
            m_GapShown = t != null ? t.capUnlockedGap : 0.002f;
        }

        float SignedProgress() => m_ClockwiseFromBelowIsNegativeYaw ? -m_Logic.progress : m_Logic.progress;

        float WorldScale => transform.lossyScale.x;

        /// <summary>The cap's rotation in the seat's frame for the current progress: always square to the tube.</summary>
        Quaternion IdealLocalRotation => Quaternion.AngleAxis(SignedProgress(), Vector3.up) * m_BaseLocalRotation;

        Pose IdealPose()
        {
            var seat = m_Point.transform;
            return new Pose(seat.position + m_Point.GuideAxisWorld * (m_GapShown * WorldScale), seat.rotation * IdealLocalRotation);
        }

        Quaternion HandLocal() => Quaternion.Inverse(m_Point.transform.rotation) * m_Interactor.transform.rotation;

        /// <summary>The twist part of a rotation about the frame's +Y (swing-twist decomposition), as a signed Unity yaw in degrees.</summary>
        public static float TwistAboutUp(Quaternion q)
        {
            if (q.w < 0f) { q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w; }
            if (Mathf.Abs(q.y) < 1e-6f && Mathf.Abs(q.w) < 1e-6f) return 0f;   // a pure 180° swing: no twist to read
            return Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(q.y, q.w) * Mathf.Rad2Deg);
        }

        // ---- IAttachedGrabHandler ----

        public bool OnAttachedGrabBegin(RocketPart part, AttachPoint point, XRGrabInteractable grab)
        {
            if (grab.interactorsSelecting.Count == 0) return false;
            m_Point = point;
            m_Interactor = grab.interactorsSelecting[0];
            m_PrevHandLocal = HandLocal();
            m_HasStart = false;
            m_Active = true;
            return true;
        }

        /// <summary>The rocket is in a hand or fixed (on the stand): only then may the cap be pulled off.</summary>
        bool RocketSteady()
        {
            var tube = m_Point != null ? m_Point.tube : null;
            return tube != null && (tube.isHeld || (tube.body != null && tube.body.isKinematic));
        }

        public AttachedGrabResult ProcessAttachedGrab(XRGrabInteractable grab, ref Pose targetPose)
        {
            var t = tuning;
            var point = m_Point;
            var seatT = point.transform;
            var s = WorldScale;

            if (!m_Logic.locked)
            {
                // Pull-off: drawn clearly away along the axis (not drifting while twisting), with the rocket steady.
                var local = seatT.InverseTransformPoint(targetPose.position);
                if (!m_HasStart) { m_StartLocal = local; m_HasStart = true; }
                var moved = (local - m_StartLocal) * s;
                var axial = -moved.y;   // the guide axis points out of the tube, along the seat's -Y
                var lateral = new Vector2(moved.x, moved.z).magnitude;
                var pullOff = (t != null ? t.capPullOff : 0.06f) * s;
                if (axial > pullOff && axial > 2f * lateral && RocketSteady())
                {
                    m_Logic.Reset();
                    return AttachedGrabResult.Detach;
                }

                // Twist: the hand's rotation since the last step, in the seat's frame, reduced to its twist about the axis.
                var now = HandLocal();
                var yawDelta = TwistAboutUp(now * Quaternion.Inverse(m_PrevHandLocal)) * (t != null ? t.capTwistSensitivity : 1.5f);
                m_PrevHandLocal = now;
                var clockwiseDelta = m_ClockwiseFromBelowIsNegativeYaw ? -yawDelta : yawDelta;
                Advance(clockwiseDelta);
            }

            // Gap closes once locked.
            var targetGap = m_Logic.locked ? 0f : (t != null ? t.capUnlockedGap : 0.002f);
            var closeSpeed = m_GapCloseDuration > 0f ? (t != null ? t.capUnlockedGap : 0.002f) / m_GapCloseDuration : float.MaxValue;
            m_GapShown = Mathf.MoveTowards(m_GapShown, targetGap, closeSpeed * Time.deltaTime);

            var pose = IdealPose();
            targetPose.position = pose.position;
            targetPose.rotation = pose.rotation;
            return AttachedGrabResult.Hold;
        }

        void Advance(float clockwiseDelta)
        {
            var before = m_Logic.progress;
            var (detents, justLocked) = m_Logic.Advance(clockwiseDelta);
            for (var i = 0; i < detents; i++) AssemblyEvents.RaiseCapDetent(m_Part);
            if (justLocked) AssemblyEvents.RaiseCapLocked(m_Part);
            // Thread texture between detents: one light tick per hapticTwistTextureAngle of new progress; detent and lock frames skip it.
            var gained = m_Logic.progress - before;
            if (detents > 0 || justLocked) m_TextureAccum = 0f;
            else if (gained > 0f)
            {
                var lib = AssemblyFeedback.instance != null ? AssemblyFeedback.instance.library : null;
                var step = lib != null ? lib.hapticTwistTextureAngle : 6f;
                m_TextureAccum += gained;
                if (step > 0f && m_TextureAccum >= step)
                {
                    m_TextureAccum -= step;
                    AssemblyEvents.RaiseTwistTexture(m_Part);
                }
            }
        }

        public void OnAttachedGrabEnd()
        {
            var t = tuning;
            // Nearly there: letting go finishes the lock.
            if (!m_Logic.locked && t != null && m_Logic.progress >= t.capAutoFinishFraction * t.capLockAngle)
                Advance(t.capLockAngle - m_Logic.progress + 1f);
            if (m_Logic.locked) m_GapShown = 0f;
            // Leave it exactly square to the tube at its progress (the seat records this pose when it re-attaches).
            if (m_Point != null)
            {
                var pose = IdealPose();
                transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
            m_Active = false;
            m_Interactor = null;
        }
    }
}
