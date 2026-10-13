using System.Collections.Generic;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// Switches the mission displays (the overlay on the big screen, the countdown clock, the console mirror) off while the start
    /// menu is up (and during the intro) and on for the game, so the room's own screens and its intro show until building starts.
    /// </summary>
    public sealed class PhaseDisplays : MonoBehaviour
    {
        [SerializeField] GameFlowController m_Flow;
        [SerializeField, Tooltip("Shown in every phase except the menu")] List<GameObject> m_GameDisplays = new List<GameObject>();

        public IReadOnlyList<GameObject> gameDisplays => m_GameDisplays;

        public void Configure(GameFlowController flow, IEnumerable<GameObject> gameDisplays)
        {
            m_Flow = flow;
            m_GameDisplays = new List<GameObject>(gameDisplays);
        }

        void OnEnable()
        {
            if (m_Flow == null) m_Flow = FindAnyObjectByType<GameFlowController>();
            if (m_Flow == null) return;
            m_Flow.PhaseChanged += Apply;
            Apply(m_Flow.phase);
        }

        void OnDisable()
        {
            if (m_Flow != null) m_Flow.PhaseChanged -= Apply;
        }

        void Apply(GamePhase phase)
        {
            var show = phase != GamePhase.Menu && phase != GamePhase.Intro;   // the room's own screens show the intro
            foreach (var go in m_GameDisplays)
                if (go != null && go.activeSelf != show) go.SetActive(show);
        }
    }
}
