using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket
{
    /// <summary>
    /// A cover that slides along one axis, moved by grabbing its grip (this interactable) and pulling. Used for the console's wiring
    /// hatch in VRRocket_Final: the cover slides into a slot under the panel surface and uncovers Yi-Xuan's cable puzzle.
    /// While the cover is closed the puzzle's grabbables are switched off, except any that are held or plugged in, so nothing
    /// is grabbed through the lid. Click and a controller pulse at each end of the travel.
    /// </summary>
    public sealed class SlidingPanel : XRBaseInteractable
    {
        [SerializeField, Tooltip("The part that slides (the cover with this grip on it)")] Transform m_Cover;
        [SerializeField, Tooltip("Slide direction in the cover's parent space, closed to open")] Vector3 m_Axis = Vector3.right;
        [SerializeField, Tooltip("Metres from closed to fully open")] float m_Travel = 0.33f;
        [SerializeField, Tooltip("Grabbables behind the cover: switched off while it is closed")] Transform m_Contents;
        [SerializeField, Range(0f, 1f), Tooltip("Open fraction from which the contents can be grabbed")] float m_UnlockAt = 0.5f;
        [SerializeField] AudioSource m_Audio;
        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();

        Vector3 m_ClosedPosition;
        float m_Offset, m_GrabOffset, m_GrabAlong;
        bool m_AtOpen, m_AtClosed = true, m_Unlocked = true;
        readonly List<XRBaseInteractable> m_Locked = new List<XRBaseInteractable>();
        AudioClip m_EndClick;

        /// <summary>0 closed, 1 fully open.</summary>
        public float openAmount => m_Travel > 0f ? m_Offset / m_Travel : 0f;
        public bool contentsUnlocked => m_Unlocked;
        public Transform cover => m_Cover;
        public Vector3 axisWorld => m_Cover != null && m_Cover.parent != null ? m_Cover.parent.TransformDirection(m_Axis.normalized) : m_Axis.normalized;
        public float travel => m_Travel;

        public void Configure(Transform cover, Vector3 axis, float travel, Transform contents, AudioSource audio)
        {
            m_Cover = cover; m_Axis = axis; m_Travel = travel; m_Contents = contents; m_Audio = audio;
        }

        protected override void Awake()
        {
            base.Awake();
            if (m_Cover == null) m_Cover = transform.parent;
            m_ClosedPosition = m_Cover.localPosition;
            m_EndClick = ProceduralAudio.Beep(1900f, 0.035f, 0.4f);
        }

        void Start() => ApplyLock();

        /// <summary>Sets the cover's opening directly (0 closed, 1 open), as a hand would.</summary>
        public void SetOpenAmount(float amount)
        {
            m_Offset = Mathf.Clamp01(amount) * m_Travel;
            Place();
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            m_GrabAlong = Along(args.interactorObject);
            m_GrabOffset = m_Offset;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || !isSelected) return;
            var hand = interactorsSelecting[0];
            m_Offset = Mathf.Clamp(m_GrabOffset + Along(hand) - m_GrabAlong, 0f, m_Travel);
            Place(hand);
        }

        float Along(IXRSelectInteractor interactor)
        {
            var p = interactor.GetAttachTransform(this).position;
            var parent = m_Cover.parent;
            var local = parent != null ? parent.InverseTransformPoint(p) : p;
            return Vector3.Dot(local, m_Axis.normalized);
        }

        void Place(IXRInteractor hand = null)
        {
            m_Cover.localPosition = m_ClosedPosition + m_Axis.normalized * m_Offset;
            var atOpen = m_Offset >= m_Travel - 0.002f;
            var atClosed = m_Offset <= 0.002f;
            if (atOpen && !m_AtOpen) { EndStop(hand); onOpened.Invoke(); }
            if (atClosed && !m_AtClosed) { EndStop(hand); onClosed.Invoke(); }
            m_AtOpen = atOpen; m_AtClosed = atClosed;
            ApplyLock();
        }

        void EndStop(IXRInteractor hand)
        {
            if (m_Audio != null && m_EndClick != null) m_Audio.PlayOneShot(m_EndClick, 0.8f);
            var fb = AssemblyFeedback.instance;
            if (fb != null && fb.library != null && hand != null) fb.Play(hand, fb.library.hapticButtonPress);
        }

        void ApplyLock()
        {
            var unlock = openAmount >= m_UnlockAt;
            if (unlock == m_Unlocked || m_Contents == null) { m_Unlocked = unlock; return; }
            m_Unlocked = unlock;
            if (!unlock)
            {
                m_Locked.Clear();
                foreach (var g in m_Contents.GetComponentsInChildren<XRBaseInteractable>())
                {
                    if (g == this || g.isSelected) continue;   // held or plugged in: leave it be
                    g.enabled = false;
                    m_Locked.Add(g);
                }
            }
            else
            {
                foreach (var g in m_Locked) if (g != null) g.enabled = true;
                m_Locked.Clear();
            }
        }
    }
}
