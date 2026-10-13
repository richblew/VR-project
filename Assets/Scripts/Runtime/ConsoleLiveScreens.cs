using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// While the console's wiring is broken the room's side TVs show their NO SIGNAL test card. Once it is fixed, panels laid over
    /// three of them (the fourth carries the countdown clock) come on with live content: the NASA logo, SYSTEMS ONLINE, the rocket
    /// on its pad (VRRocket_Final). A new play that needs the wiring again switches them back off.
    /// </summary>
    public sealed class ConsoleLiveScreens : MonoBehaviour
    {
        [SerializeField] GameFlowController m_Flow;
        [SerializeField, Tooltip("Panels over the side TVs, shown while the console is online")] Renderer[] m_Panels = new Renderer[0];

        public Renderer[] panels => m_Panels;
        public bool isLive { get; private set; }

        public void Configure(GameFlowController flow, Renderer[] panels)
        {
            m_Flow = flow; m_Panels = panels;
        }

        void OnEnable()
        {
            if (m_Flow != null) m_Flow.MiniGameChanged += Apply;
            Apply();
        }

        void OnDisable()
        {
            if (m_Flow != null) m_Flow.MiniGameChanged -= Apply;
        }

        void Apply()
        {
            isLive = m_Flow == null || m_Flow.consoleOnline;
            foreach (var p in m_Panels) if (p != null) p.gameObject.SetActive(isLive);
        }
    }
}
