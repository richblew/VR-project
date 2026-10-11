using System.Collections.Generic;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// One attach point on the body tube (SPEC.md section 4.4). Generated from attach_points.json by the editor script.
    /// Knows what it accepts, which axis the guide runs along, whether it is occupied, whether gating currently allows it,
    /// and drives its own glow mesh (section 6: brightness rising with closeness, several points can glow at once).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttachPoint : MonoBehaviour
    {
        static readonly List<AttachPoint> s_Active = new List<AttachPoint>();
        static readonly int k_BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Every enabled attach point in the scene. Read-only; iterate without allocating.</summary>
        public static IReadOnlyList<AttachPoint> active => s_Active;

        [SerializeField] string m_PointName = "";
        [SerializeField] PartType m_Accepts = PartType.TailFin;
        [SerializeField, Tooltip("Yaw of the point around the tube axis, from attach_points.json.")] float m_YawDegrees;
        [SerializeField, Tooltip("Emissive glow mesh for the approach feedback. Unlit, no bloom needed.")] Renderer m_GlowRenderer;
        [SerializeField, ColorUsage(false, true)] Color m_GlowColor = new Color(1f, 0.55f, 0.1f) * 2f;

        RocketPart m_Tube;
        MaterialPropertyBlock m_Block;
        float m_GlowRequest;
        float m_GlowShown = -1f;

        /// <summary>Set by the assembly state machine. When false the point behaves as a rejection: no glow, no snap.</summary>
        public bool gatingAllows { get; set; } = true;

        public string pointName => string.IsNullOrEmpty(m_PointName) ? name : m_PointName;
        public PartType accepts => m_Accepts;
        public float yawDegrees => m_YawDegrees;
        public Renderer glowRenderer => m_GlowRenderer;
        public RocketPart attachedPart { get; private set; }
        public bool isOccupied => attachedPart != null;

        /// <summary>The body tube this point belongs to.</summary>
        public RocketPart tube
        {
            get
            {
                if (m_Tube == null) m_Tube = GetComponentInParent<RocketPart>();
                return m_Tube;
            }
        }

        /// <summary>True for fins and flaps: the guide runs along local +Z (outward). False for the seats, where it runs along the tube axis.</summary>
        public bool isSlot => m_Accepts == PartType.TailFin || m_Accepts == PartType.WingFlap;

        /// <summary>Is this a Low (wrong) flap slot? Only meaningful when accepts == WingFlap.</summary>
        public bool isLowFlapSlot => pointName.StartsWith("FlapSlot_Low");

        /// <summary>World-space unit direction the guide runs along, pointing away from the seated position.</summary>
        public Vector3 GuideAxisWorld
        {
            get
            {
                if (isSlot) return transform.forward;
                // Seats: the guide extends away from the tube. Nose above (+Y), motor and cap below (-Y).
                // The stand's point lifts the tube out upward.
                return m_Accepts == PartType.NoseCone || m_Accepts == PartType.BodyTube ? transform.up : -transform.up;
            }
        }

        /// <summary>The axis of the held part that must roughly match <see cref="ReferenceAxisWorld"/> before the guide engages.</summary>
        public Vector3 ReferenceAxisWorld => isSlot ? transform.forward : transform.up;

        /// <summary>Does this point currently accept the given part?</summary>
        public bool Accepts(RocketPart part)
        {
            return part != null && part.partType == m_Accepts && !isOccupied && gatingAllows;
        }

        public void Configure(string pointName, PartType accepts, float yawDegrees)
        {
            m_PointName = pointName;
            m_Accepts = accepts;
            m_YawDegrees = yawDegrees;
        }

        public void SetGlowRenderer(Renderer renderer)
        {
            m_GlowRenderer = renderer;
        }

        /// <summary>Ask for glow this frame. The brightest request wins; it resets every frame so leaving a point turns it off.</summary>
        public void RequestGlow(float amount)
        {
            if (amount > m_GlowRequest) m_GlowRequest = amount;
        }

        internal void SetAttached(RocketPart part)
        {
            attachedPart = part;
        }

        internal void ClearAttached()
        {
            attachedPart = null;
        }

        void OnEnable()
        {
            s_Active.Add(this);
            ApplyGlow(0f);
        }

        void OnDisable()
        {
            s_Active.Remove(this);
        }

        void LateUpdate()
        {
            ApplyGlow(m_GlowRequest);
            m_GlowRequest = 0f;
        }

        void ApplyGlow(float amount)
        {
            if (m_GlowRenderer == null || Mathf.Approximately(amount, m_GlowShown)) return;
            m_GlowShown = amount;
            var on = amount > 0.001f;
            m_GlowRenderer.enabled = on;
            if (!on) return;
            if (m_Block == null) m_Block = new MaterialPropertyBlock();
            m_GlowRenderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColorId, m_GlowColor * (0.25f + 0.75f * amount));
            m_GlowRenderer.SetPropertyBlock(m_Block);
        }

        /// <summary>Maps an attach point name from attach_points.json to the part type it accepts.</summary>
        public static PartType AcceptsFromName(string pointName)
        {
            if (pointName == "StandClamp") return PartType.BodyTube;
            if (pointName.StartsWith("FinSlot")) return PartType.TailFin;
            if (pointName.StartsWith("FlapSlot")) return PartType.WingFlap;
            if (pointName == "NoseSeat") return PartType.NoseCone;
            if (pointName == "MotorSeat") return PartType.Motor;
            if (pointName == "CapSeat") return PartType.MotorCap;
            throw new System.ArgumentException("Unknown attach point name: " + pointName);
        }
    }
}
