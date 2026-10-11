using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket
{
    /// <summary>
    /// Identity and state of one rocket part (SPEC.md section 10.2), including the body tube.
    /// The guided attach mechanic lives in <see cref="GuideGrabTransformer"/>; this component records what the part is, where it stands,
    /// which hand held it last (for haptics), where it lives when loose, and drives the optional part-side glow (section 6, "should").
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RocketPart : MonoBehaviour
    {
        static readonly int k_BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] PartType m_PartType = PartType.TailFin;
        [SerializeField, Tooltip("1 to 3 for fins and flaps, 1 for the rest.")] int m_PartId = 1;
        [SerializeField, Tooltip("Optional emissive mesh on the part's attach feature (tab, shoulder, nozzle, lugs).")] Renderer m_GlowRenderer;
        [SerializeField, ColorUsage(false, true)] Color m_GlowColor = new Color(1f, 0.55f, 0.1f) * 2f;

        XRGrabInteractable m_Grab;
        Rigidbody m_Rigidbody;
        bool m_HookedGrab;
        MaterialPropertyBlock m_Block;
        float m_GlowRequest;
        float m_GlowShown = -1f;

        public PartType partType => m_PartType;
        public int partId => m_PartId;
        public PartState state { get; private set; } = PartState.Free;
        public AttachPoint attachedTo { get; private set; }

        /// <summary>The parent this part returns to when it is detached from the rocket (its tray group).</summary>
        public Transform homeParent { get; private set; }

        /// <summary>The interactor that is holding, or most recently held, this part. Used to send haptics after release.</summary>
        public IXRInteractor lastHoldingInteractor { get; private set; }

        public XRGrabInteractable grabInteractable => m_Grab != null ? m_Grab : (m_Grab = GetComponent<XRGrabInteractable>());
        public Rigidbody body => m_Rigidbody != null ? m_Rigidbody : (m_Rigidbody = GetComponent<Rigidbody>());
        public bool isHeld => grabInteractable != null && grabInteractable.isSelected;
        public Renderer glowRenderer => m_GlowRenderer;

        /// <summary>Id used in reports, for example "WingFlap_2".</summary>
        public string displayId => m_PartType + "_" + m_PartId;

        public void Configure(PartType type, int id)
        {
            m_PartType = type;
            m_PartId = id;
        }

        public void SetGlowRenderer(Renderer renderer)
        {
            m_GlowRenderer = renderer;
        }

        /// <summary>Overrides the tray group this part returns to when detached (used after a new build is spawned).</summary>
        public void SetHomeParent(Transform parent)
        {
            homeParent = parent;
        }

        void Awake()
        {
            homeParent = transform.parent;
            HookGrab();
        }

        float m_LastHoverTick = -10f;

        void HookGrab()
        {
            if (m_HookedGrab || grabInteractable == null) return;
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
            grabInteractable.hoverEntered.AddListener(OnHoverEntered);
            m_HookedGrab = true;
        }

        void OnEnable()
        {
            ApplyGlow(0f);
        }

        void OnDestroy()
        {
            if (!m_HookedGrab || m_Grab == null) return;
            m_Grab.selectEntered.RemoveListener(OnSelectEntered);
            m_Grab.selectExited.RemoveListener(OnSelectExited);
            m_Grab.hoverEntered.RemoveListener(OnHoverEntered);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            lastHoldingInteractor = args.interactorObject;
            AssemblyEvents.RaisePartGrabbed(this, args.interactorObject);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;   // disable-time release: no feedback
            AssemblyEvents.RaisePartReleased(this, args.interactorObject);
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (isHeld) return;
            var cooldown = AssemblyFeedback.instance != null && AssemblyFeedback.instance.library != null ? AssemblyFeedback.instance.library.hapticHoverCooldown : 0.25f;
            if (Time.time - m_LastHoverTick < cooldown) return;
            m_LastHoverTick = Time.time;
            AssemblyEvents.RaisePartHovered(this, args.interactorObject);
        }

        /// <summary>Ask for part-side glow this frame; the brightest request wins and it resets every frame.</summary>
        public void RequestGlow(float amount)
        {
            if (amount > m_GlowRequest) m_GlowRequest = amount;
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

        internal void SetGuided()
        {
            state = PartState.Guided;
            SetInterpolation(RigidbodyInterpolation.Interpolate);
        }

        /// <summary>The part's rest pose under its attach point, recorded when it seated. <see cref="AttachedPartSync"/> re-asserts it every frame.</summary>
        public Vector3 seatLocalPosition { get; private set; }
        public Quaternion seatLocalRotation { get; private set; } = Quaternion.identity;

        internal void SetAttached(AttachPoint point)
        {
            attachedTo = point;
            state = PartState.Attached;
            seatLocalPosition = transform.localPosition;
            seatLocalRotation = transform.localRotation;
            // An attached part is a kinematic child moved by its parent; interpolating it makes it lag and wobble behind a
            // carried rocket, so interpolation is only on while the part moves on its own.
            SetInterpolation(RigidbodyInterpolation.None);
        }

        internal void SetFree()
        {
            attachedTo = null;
            state = PartState.Free;
            SetInterpolation(RigidbodyInterpolation.Interpolate);
        }

        void SetInterpolation(RigidbodyInterpolation mode)
        {
            var b = body;
            if (b != null && b.interpolation != mode) b.interpolation = mode;
        }
    }
}
