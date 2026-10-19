using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket
{
    /// <summary>
    /// Crisp press and release haptics for a push button, sent to the hand that is poking it, and a double buzz on that hand when
    /// the press is refused (<see cref="GameFlowController.Refused"/> in the same frame: SUBMIT with an empty bin, LAUNCH with
    /// nothing on the pad...). Wire the button's onPress and onRelease UnityEvents to <see cref="OnPress"/> and <see cref="OnRelease"/>
    /// in the Inspector, so no code references the example button class. Values come from the <see cref="FeedbackLibrary"/>.
    /// </summary>
    [RequireComponent(typeof(XRBaseInteractable))]
    public sealed class ButtonPressHaptics : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional. When empty the library of the scene's AssemblyFeedback is used.")] FeedbackLibrary m_Library;

        XRBaseInteractable m_Interactable;
        GameFlowController m_Flow;
        IXRInteractor m_PressHand;
        int m_PressFrame = -10, m_RefusedFrame = -10, m_BuzzedFrame = -10;

        /// <summary>How many refused presses this button has answered with the double buzz.</summary>
        public int refusedBuzzes { get; private set; }

        FeedbackLibrary library => m_Library != null ? m_Library : (AssemblyFeedback.instance != null ? AssemblyFeedback.instance.library : null);

        void Awake()
        {
            m_Interactable = GetComponent<XRBaseInteractable>();
        }

        void OnEnable()
        {
            m_Flow = FindAnyObjectByType<GameFlowController>();
            if (m_Flow != null) m_Flow.Refused += OnRefused;
        }

        void OnDisable()
        {
            if (m_Flow != null) m_Flow.Refused -= OnRefused;
        }

        IXRInteractor PressingHand()
        {
            var hovering = m_Interactable.interactorsHovering;
            for (var i = 0; i < hovering.Count; i++) if (hovering[i] is XRBaseInputInteractor input) return input;
            var selecting = m_Interactable.interactorsSelecting;
            for (var i = 0; i < selecting.Count; i++) if (selecting[i] is XRBaseInputInteractor input) return input;
            return null;
        }

        public void OnPress()
        {
            m_PressHand = PressingHand();
            m_PressFrame = Time.frameCount;
            var lib = library; var fb = AssemblyFeedback.instance;
            if (lib != null && fb != null) fb.Play(m_PressHand, lib.hapticButtonPress);
            // the button's action may run before or after this listener: whichever comes second answers a refusal
            if (m_RefusedFrame == m_PressFrame) BuzzRefused();
        }

        public void OnRelease()
        {
            var lib = library; var fb = AssemblyFeedback.instance;
            if (lib == null || fb == null) return;
            fb.Play(PressingHand(), lib.hapticButtonRelease);
        }

        void OnRefused()
        {
            m_RefusedFrame = Time.frameCount;
            if (m_PressFrame == m_RefusedFrame) BuzzRefused();
        }

        void BuzzRefused()
        {
            if (m_BuzzedFrame == Time.frameCount) return;
            m_BuzzedFrame = Time.frameCount;
            refusedBuzzes++;
            var lib = library; var fb = AssemblyFeedback.instance;
            if (lib != null && fb != null) fb.Play(m_PressHand, lib.hapticRefused);
        }
    }
}
