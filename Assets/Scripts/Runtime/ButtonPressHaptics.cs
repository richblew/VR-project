using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket
{
    /// <summary>
    /// Crisp press and release haptics for a push button, sent to the hand that is poking it.
    /// Wire the button's onPress and onRelease UnityEvents to <see cref="OnPress"/> and <see cref="OnRelease"/> in the Inspector,
    /// so no code references the example button class. Values come from the <see cref="FeedbackLibrary"/>.
    /// </summary>
    [RequireComponent(typeof(XRBaseInteractable))]
    public sealed class ButtonPressHaptics : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional. When empty the library of the scene's AssemblyFeedback is used.")] FeedbackLibrary m_Library;

        XRBaseInteractable m_Interactable;

        FeedbackLibrary library => m_Library != null ? m_Library : (AssemblyFeedback.instance != null ? AssemblyFeedback.instance.library : null);

        void Awake()
        {
            m_Interactable = GetComponent<XRBaseInteractable>();
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
            var lib = library; var fb = AssemblyFeedback.instance;
            if (lib == null || fb == null) return;
            fb.Play(PressingHand(), lib.hapticButtonPress);
        }

        public void OnRelease()
        {
            var lib = library; var fb = AssemblyFeedback.instance;
            if (lib == null || fb == null) return;
            fb.Play(PressingHand(), lib.hapticButtonRelease);
        }
    }
}
