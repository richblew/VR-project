using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace VRRocket
{
    /// <summary>
    /// Loads the teammates' room scene additively next to the scene this component lives in, then makes this scene the active
    /// one so its lighting (skybox, ambient, fog) applies. The room scene is never edited or saved by the VR Rocket systems: they
    /// live in their own scene and bind to the room's objects at runtime (<see cref="RoomBindings"/>, <see cref="XRRigAdapter"/>).
    /// If the room is already loaded (both scenes open in the Editor, or loaded by a test), it is not loaded twice.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class RoomSceneLoader : MonoBehaviour
    {
        [SerializeField, Tooltip("Asset path of the room scene. In a player build it must be in the active build profile's scene list.")]
        string m_RoomScenePath = "Assets/Scenes/room.unity";
        [SerializeField, Tooltip("Make this scene the active scene once the room is loaded, so its lighting settings apply.")]
        bool m_MakeThisSceneActive = true;

        /// <summary>The loader of the running experience, if any.</summary>
        public static RoomSceneLoader instance { get; private set; }

        public string roomScenePath => m_RoomScenePath;
        public bool isRoomLoaded { get; private set; }
        public Scene roomScene { get; private set; }

        /// <summary>Raised once, when the room scene has finished loading.</summary>
        public event Action<Scene> RoomLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void Awake()
        {
            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        IEnumerator Start()
        {
            var scene = SceneManager.GetSceneByPath(m_RoomScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                AsyncOperation op;
#if UNITY_EDITOR
                // In the Editor the room does not have to be in a build profile.
                op = EditorSceneManager.LoadSceneAsyncInPlayMode(m_RoomScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
                op = SceneManager.LoadSceneAsync(m_RoomScenePath, LoadSceneMode.Additive);
#endif
                if (op == null)
                {
                    Debug.LogError("RoomSceneLoader: could not load '" + m_RoomScenePath + "'. Add it to the build profile's scene list.", this);
                    yield break;
                }
                while (!op.isDone) yield return null;
                scene = SceneManager.GetSceneByPath(m_RoomScenePath);
            }
            roomScene = scene;
            if (m_MakeThisSceneActive) SceneManager.SetActiveScene(gameObject.scene);
            isRoomLoaded = true;
            var handler = RoomLoaded;
            RoomLoaded = null;
            handler?.Invoke(scene);
        }

        /// <summary>Runs <paramref name="callback"/> now if the room is loaded, otherwise as soon as it is.</summary>
        public void WhenLoaded(Action<Scene> callback)
        {
            if (callback == null) return;
            if (isRoomLoaded) callback(roomScene);
            else RoomLoaded += callback;
        }

        /// <summary>
        /// Finds a GameObject in a scene by its hierarchy path ("Root/Child/Grandchild"), including inactive objects. A segment ending
        /// in '*' matches the first object whose name starts with the rest of it, for names with characters that do not survive
        /// copying around (their big TV is "TV LED 30" plus a diaeresis).
        /// </summary>
        public static GameObject FindByPath(Scene scene, string path)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(path)) return null;
            var segments = path.Split('/');
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!Matches(root.name, segments[0])) continue;
                var t = root.transform;
                for (var i = 1; i < segments.Length && t != null; i++)
                {
                    Transform next = null;
                    foreach (Transform c in t) if (Matches(c.name, segments[i])) { next = c; break; }
                    t = next;
                }
                if (t != null) return t.gameObject;
            }
            return null;
        }

        static bool Matches(string name, string segment) =>
            segment.EndsWith("*") ? name.StartsWith(segment.Substring(0, segment.Length - 1)) : name == segment;
    }
}
