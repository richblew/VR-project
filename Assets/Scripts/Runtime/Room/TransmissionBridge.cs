using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace VRRocket
{
    /// <summary>
    /// Connects the room's intro (the teammates' TransmissionController: NASA logo, standby video, announcement, red alert lights,
    /// dimmed room, transmission video) to the game flow. The intro lives in the default assembly, which an asmdef cannot
    /// reference, so it is found by type name and driven through its public members.
    /// <list type="bullet">
    /// <item>VRRocket_Main: the intro plays by itself under the start menu; START ends it cleanly.</item>
    /// <item>VRRocket_Final: the intro is set not to play by itself (its playOnStart is off). START WITH INTRO plays it on demand
    /// (<see cref="Play"/>) and building starts when it ends; SKIP INTRO never starts it.</item>
    /// </list>
    /// </summary>
    public sealed class TransmissionBridge : MonoBehaviour
    {
        [SerializeField] GameFlowController m_Flow;
        [SerializeField] RoomSceneLoader m_Loader;
        [SerializeField] string m_ControllerTypeName = "TransmissionController";
        [SerializeField, Tooltip("End the intro when the game moves on to building")] bool m_StopWhenGameStarts = true;
        [SerializeField, Tooltip("NASA logo time before the standby video when the intro is played on demand (the menu already showed the logo)")]
        float m_LogoSecondsOnDemand = 2f;

        MonoBehaviour m_Transmission;
        Coroutine m_Watch;

        /// <summary>The room's intro controller, or null if the room has none.</summary>
        public MonoBehaviour transmission => m_Transmission;
        public bool isStopped { get; private set; }
        public bool isPlaying { get; private set; }

        /// <summary>The room's smaller screens around the big TV (the intro's side screens), and their material slot.</summary>
        public Renderer[] sideScreens => Get<Renderer[]>("sideScreens");
        public int sideScreenMaterialIndex => Get<int>("sideScreenMaterialIndex");

        /// <summary>How dimmed the intro has made the room, 0 to 1 (0 when it is stopped or has none).</summary>
        public float dimAmount => m_Transmission != null && !isStopped && m_Transmission.enabled ? Get<float>("currentDim") : 0f;

        /// <summary>The intro waits to be started (playOnStart off) and can be played with <see cref="Play"/>.</summary>
        public bool canPlayOnDemand =>
            m_Transmission != null && m_Transmission.GetType().GetMethod("PlaySequenceNow") != null && !Get<bool>("playOnStart");

        public void Configure(GameFlowController flow, RoomSceneLoader loader)
        {
            m_Flow = flow; m_Loader = loader;
        }

        void OnEnable()
        {
            if (m_Flow == null) m_Flow = FindAnyObjectByType<GameFlowController>();
            if (m_Flow != null) m_Flow.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            if (m_Flow != null) m_Flow.PhaseChanged -= OnPhaseChanged;
        }

        void Awake()
        {
            // the final scene holds the room itself: find the intro straight away. The scene is still loading here (its root
            // objects cannot be listed yet), so look through the loaded objects instead.
            if (m_Loader != null) return;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (mb != null && mb.gameObject.scene == gameObject.scene && mb.GetType().Name == m_ControllerTypeName) { m_Transmission = mb; return; }
        }

        void Start()
        {
            if (m_Transmission != null) return;
            if (m_Loader == null) m_Loader = RoomSceneLoader.instance != null ? RoomSceneLoader.instance : FindAnyObjectByType<RoomSceneLoader>();
            if (m_Loader != null) m_Loader.WhenLoaded(FindIn);
        }

        void FindIn(Scene room)
        {
            if (!room.IsValid() || !room.isLoaded) return;
            foreach (var root in room.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.GetType().Name == m_ControllerTypeName) { m_Transmission = mb; return; }
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (m_StopWhenGameStarts && phase != GamePhase.Menu && phase != GamePhase.Intro) Stop();
        }

        /// <summary>Plays the whole intro from the start; <paramref name="onFinished"/> runs when it has ended by itself.</summary>
        public void Play(Action onFinished)
        {
            if (m_Transmission == null) { onFinished?.Invoke(); return; }
            isStopped = false;
            isPlaying = true;
            var announcement = Get<AudioSource>("announcementAudio");
            if (announcement != null) announcement.enabled = true;
            m_Transmission.enabled = true;
            Set("logoDelay", m_LogoSecondsOnDemand);
            m_Transmission.GetType().GetMethod("PlaySequenceNow")?.Invoke(m_Transmission, null);
            if (m_Watch != null) StopCoroutine(m_Watch);
            m_Watch = StartCoroutine(WatchForEnd(onFinished));
        }

        IEnumerator WatchForEnd(Action onFinished)
        {
            yield return null;
            while (!isStopped && !Get<bool>("sequenceFinished")) yield return null;
            m_Watch = null;
            if (isStopped) yield break;
            isPlaying = false;
            onFinished?.Invoke();
        }

        /// <summary>Stops the intro wherever it is and puts the room back: lights up, alert off, the logo on the big TV.</summary>
        public void Stop()
        {
            if (m_Transmission == null || isStopped) return;
            isStopped = true;
            isPlaying = false;
            if (m_Watch != null) { StopCoroutine(m_Watch); m_Watch = null; }
            m_Transmission.StopAllCoroutines();
            Get<VideoPlayer>("standbyPlayer")?.Stop();
            Get<VideoPlayer>("videoPlayer")?.Stop();
            var announcement = Get<AudioSource>("announcementAudio");
            if (announcement != null)
            {
                announcement.Stop();
                // switched off as well: a disabled source is silent whatever state the audio system reports
                announcement.enabled = false;
            }
            // its OnDisable restores the alert light colours and the room's light, ambient and reflection levels
            m_Transmission.enabled = false;
            var screen = Get<Renderer>("mainScreen");
            var logo = Get<Material>("nasaLogoMaterial");
            var index = Get<int>("screenMaterialIndex");
            if (screen != null && logo != null)
            {
                var mats = screen.sharedMaterials;
                if (index >= 0 && index < mats.Length) { mats[index] = logo; screen.sharedMaterials = mats; }
            }
        }

        T Get<T>(string field)
        {
            if (m_Transmission == null) return default;
            var f = m_Transmission.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            var v = f != null ? f.GetValue(m_Transmission) : null;
            if (v is T t && (!(v is UnityEngine.Object o) || o != null)) return t;
            return default;
        }

        void Set(string field, object value)
        {
            var f = m_Transmission.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType.IsInstanceOfType(value)) f.SetValue(m_Transmission, value);
        }
    }
}
