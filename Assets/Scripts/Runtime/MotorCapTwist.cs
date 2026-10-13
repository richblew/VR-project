using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket
{
    /// <summary>
    /// The motor cap twist (SPEC.md section 5.4). The cap is guided to CapSeat like any part and rests seated but unlocked,
    /// capUnlockedGap proud of the tube. Gripping the seated cap does not move it: the cap turns about the rocket axis following
    /// the twist of the hand (hand position orbiting the axis plus wrist roll, the XRKnob technique), clockwise only viewed from
    /// below, as a ratchet. Every capDetentAngle of new progress ticks; at capLockAngle it locks, the gap closes and the cap can
    /// no longer be turned or removed. An unlocked cap pulled more than capPullOff along the axis comes off and its progress resets.
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

        [SerializeField, Tooltip("Hands gripping further than this from the tube axis (metres, before scale) turn the cap by orbiting; closer hands turn it by wrist roll.")]
        float m_PositionModeRadius = 0.03f;

        enum TwistMode { None, Position, Wrist }

        readonly CapTwistLogic m_Logic = new CapTwistLogic();
        RocketPart m_Part;
        AttachPoint m_Point;
        IXRInteractor m_Interactor;
        Quaternion m_BaseRotation;      // world rotation of the cap at zero progress
        float m_GapShown;               // current rest offset from the seat, metres before scale
        float m_StartDepth;             // depth of the grabbed pose along the pull-off axis at grab begin
        float m_PrevPositionYaw;
        float m_PrevWristYaw;
        float m_TextureAccum;
        TwistMode m_Mode;
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
            // Zero progress is the rotation the cap seated with. Progress was reset when it last came off.
            m_BaseRotation = Quaternion.AngleAxis(-SignedProgress(), point.transform.up) * transform.rotation;
            var t = tuning;
            m_GapShown = m_Logic.locked ? 0f : (t != null ? t.capUnlockedGap : 0.002f);
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

        // ---- IAttachedGrabHandler ----

        public bool OnAttachedGrabBegin(RocketPart part, AttachPoint point, XRGrabInteractable grab)
        {
            if (grab.interactorsSelecting.Count == 0) return false;
            m_Point = point;
            m_Interactor = grab.interactorsSelecting[0];
            m_Mode = TwistMode.None;
            m_StartDepth = GuideMath.Depth(transform.position, point.transform.position, point.GuideAxisWorld);
            m_Active = true;
            return true;
        }

        float RadialDistance()
        {
            var local = m_Point.transform.InverseTransformPoint(m_Interactor.transform.position);
            return new Vector2(local.x, local.z).magnitude * WorldScale;
        }

        public AttachedGrabResult ProcessAttachedGrab(XRGrabInteractable grab, ref Pose targetPose)
        {
            var t = tuning;
            var point = m_Point;
            var axisUp = point.transform.up;
            var away = point.GuideAxisWorld;
            var seat = point.transform.position;
            var s = WorldScale;

            if (!m_Logic.locked)
            {
                // Pull-off: the hand has drawn the cap more than capPullOff away from the tube along the axis.
                var depth = GuideMath.Depth(targetPose.position, seat, away);
                var pullOff = (t != null ? t.capPullOff : 0.04f) * s;
                if (depth - m_StartDepth > pullOff)
                {
                    m_Logic.Reset();
                    return AttachedGrabResult.Detach;
                }

                // Twist, in Unity yaw terms. Like XRKnob, read the hand's position orbiting the axis when it grips off-axis,
                // and its wrist roll when it grips near the axis; never both, or a natural turn would count twice.
                var yawDelta = 0f;
                var usePosition = RadialDistance() >= m_PositionModeRadius * s;
                if (usePosition)
                {
                    var yawNow = PositionYaw();
                    if (m_Mode == TwistMode.Position) yawDelta = Mathf.DeltaAngle(m_PrevPositionYaw, yawNow);
                    m_PrevPositionYaw = yawNow;
                    m_Mode = TwistMode.Position;
                }
                else if (TryWristYaw(out var wristNow))
                {
                    if (m_Mode == TwistMode.Wrist) yawDelta = Mathf.DeltaAngle(m_PrevWristYaw, wristNow);
                    m_PrevWristYaw = wristNow;
                    m_Mode = TwistMode.Wrist;
                }
                else
                {
                    m_Mode = TwistMode.None;
                }
                var clockwiseDelta = m_ClockwiseFromBelowIsNegativeYaw ? -yawDelta : yawDelta;
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

            // Gap closes once locked.
            var targetGap = m_Logic.locked ? 0f : (t != null ? t.capUnlockedGap : 0.002f);
            var closeSpeed = m_GapCloseDuration > 0f ? (t != null ? t.capUnlockedGap : 0.002f) / m_GapCloseDuration : float.MaxValue;
            m_GapShown = Mathf.MoveTowards(m_GapShown, targetGap, closeSpeed * Time.deltaTime);

            targetPose.position = seat + away * (m_GapShown * s);
            targetPose.rotation = Quaternion.AngleAxis(SignedProgress(), axisUp) * m_BaseRotation;
            return AttachedGrabResult.Hold;
        }

        public void OnAttachedGrabEnd()
        {
            m_Active = false;
            m_Interactor = null;
        }

        /// <summary>Unity yaw (about the tube's +Y, in the attach point's frame) of the hand's position around the axis.</summary>
        float PositionYaw()
        {
            var local = m_Point.transform.InverseTransformPoint(m_Interactor.transform.position);
            // Atan2(z, x) grows from +X toward +Z, which is a negative Unity yaw, so negate.
            return -Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg;
        }

        /// <summary>Unity yaw of the hand's own roll about the tube axis, read from whichever of its forward or up vectors lies flattest.</summary>
        bool TryWristYaw(out float yaw)
        {
            var it = m_Interactor.transform;
            var f = m_Point.transform.InverseTransformDirection(it.forward);
            var u = m_Point.transform.InverseTransformDirection(it.up);
            var fFlat = new Vector2(f.x, f.z).magnitude;
            var uFlat = new Vector2(u.x, u.z).magnitude;
            var v = fFlat >= uFlat ? f : u;
            if (Mathf.Max(fFlat, uFlat) < 0.3f)
            {
                yaw = 0f;
                return false;
            }
            yaw = -Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg;
            return true;
        }
    }
}
