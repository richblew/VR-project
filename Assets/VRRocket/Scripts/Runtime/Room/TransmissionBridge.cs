using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace VRRocket
{
    /// <summary>
    /// Lets the room's intro (the teammates' TransmissionController: NASA logo, standby video, announcement, red alert lights,
    /// dimmed room, transmission video) play while the start menu is up, and ends it cleanly when the player presses START, so the
    /// video, its audio and the dimmed lights do not run on under the assembly. The intro lives in the default assembly, which an
    /// asmdef cannot reference, so it is found by type name and driven through its public fields.
    /// </summary>
    public sealed class TransmissionBridge : MonoBehaviour
    {
        [SerializeField] GameFlowController m_Flow;
        [SerializeField] RoomSceneLoader m_Loader;
        [SerializeField] string m_ControllerTypeName = "TransmissionController";
        [SerializeField, Tooltip("End the intro when the game leaves the menu (START pressed)")] bool m_StopWhenGameStarts = true;

        MonoBehaviour m_Transmission;

        /// <summary>The room's intro controller, or null if the room has none.</summary>
        public MonoBehaviour transmission => m_Transmission;
        public bool isStopped { get; private set; }

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

        void Start()
        {
            if (m_Loader == null) m_Loader = RoomSceneLoader.instance != null ? RoomSceneLoader.instance : FindAnyObjectByType<RoomSceneLoader>();
            if (m_Loader != null) m_Loader.WhenLoaded(FindIn);
        }

        void FindIn(Scene room)
        {
            foreach (var root in room.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.GetType().Name == m_ControllerTypeName) { m_Transmission = mb; return; }
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (m_StopWhenGameStarts && phase != GamePhase.Menu) Stop();
        }

        /// <summary>Stops the intro wherever it is and puts the room back: lights up, alert off, the logo on the big TV.</summary>
        public void Stop()
        {
            if (m_Transmission == null || isStopped) return;
            isStopped = true;
            m_Transmission.StopAllCoroutines();
            Get<VideoPlayer>("standbyPlayer")?.Stop();
            Get<VideoPlayer>("videoPlayer")?.Stop();
            Get<AudioSource>("announcementAudio")?.Stop();
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
            var f = m_Transmission.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            var v = f != null ? f.GetValue(m_Transmission) : null;
            if (v is T t && (!(v is Object o) || o != null)) return t;
            return default;
        }
    }
}
