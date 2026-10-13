using System.Collections;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// What an abort looks and sounds like (VRRocket_Final): red ABORT panels laid over the room's side TVs around the big TV flash
    /// on and off while a two-tone siren sounds, then they hide again. Started by <see cref="GameFlowController.Abort"/>.
    /// The panels are separate quads cut to each TV's front face (the TVs are one mesh each, so their own material cannot carry it).
    /// </summary>
    public sealed class AbortAlarm : MonoBehaviour
    {
        [SerializeField, Tooltip("ABORT panels over the side TVs, hidden until the alarm")] Renderer[] m_Panels = new Renderer[0];
        [SerializeField] Material m_AlarmOn;
        [SerializeField] Material m_AlarmOff;
        [SerializeField] AudioSource m_Audio;
        [SerializeField] float m_Seconds = 4f;
        [SerializeField] float m_FlashesPerSecond = 2.5f;
        [SerializeField, Range(0f, 1f)] float m_Volume = 0.9f;

        AudioClip m_Siren;
        Coroutine m_Run;

        public bool isPlaying => m_Run != null;
        public bool sirenPlaying => m_Audio != null && m_Audio.isPlaying;
        public float seconds => m_Seconds;
        public Material alarmOn => m_AlarmOn;
        public Renderer[] panels => m_Panels;

        public void Configure(Renderer[] panels, Material on, Material off, AudioSource audio)
        {
            m_Panels = panels; m_AlarmOn = on; m_AlarmOff = off; m_Audio = audio;
        }

        void Awake()
        {
            m_Siren = ProceduralAudio.Siren(620f, 880f, 0.45f, m_Seconds);
            Show(false);
        }

        void OnDisable() => Stop();

        public void Play()
        {
            if (m_Run != null) StopCoroutine(m_Run);
            m_Run = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Show(true);
            if (m_Audio != null && m_Siren != null) { m_Audio.clip = m_Siren; m_Audio.volume = m_Volume; m_Audio.Play(); }
            for (var t = 0f; t < m_Seconds; t += Time.deltaTime)
            {
                var on = Mathf.FloorToInt(t * m_FlashesPerSecond * 2f) % 2 == 0;
                foreach (var p in m_Panels) if (p != null) p.sharedMaterial = on ? m_AlarmOn : m_AlarmOff;
                yield return null;
            }
            m_Run = null;
            Stop();
        }

        void Stop()
        {
            if (m_Run != null) { StopCoroutine(m_Run); m_Run = null; }
            if (m_Audio != null && m_Audio.clip == m_Siren) m_Audio.Stop();
            Show(false);
        }

        void Show(bool show)
        {
            foreach (var p in m_Panels) if (p != null) p.gameObject.SetActive(show);
        }
    }
}
