using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace VRRocket
{
    /// <summary>
    /// Connects the VR Rocket scene to the teammates' room at runtime, without ever saving a change into their scene:
    /// <list type="bullet">
    /// <item>swaps: a room object is switched off and our replacement switched on (their solid wall for our wall with a window),
    /// but only while the room object still has the bounds the replacement was built for;</item>
    /// <item>follows: one of our objects is placed relative to a room object, using an offset recorded in the Editor, so our screen
    /// overlay, console add-on or clock stay put if the teammates move their TV, console or desk;</item>
    /// <item>checks: room objects our layout depends on but cannot follow (the desk under the workstation) are compared with the
    /// bounds they had when the layout was made;</item>
    /// <item>teleport floors: room floors become teleport areas.</item>
    /// </list>
    /// Every lookup is by hierarchy path. Anything missing or moved logs a warning and counts in <see cref="warningCount"/>.
    /// </summary>
    public sealed class RoomBindings : MonoBehaviour
    {
        [Serializable]
        public class Swap
        {
            [Tooltip("Hierarchy path of the room object to switch off, e.g. Environment/Floorplan/Right wall")]
            public string roomPath;
            public GameObject replacement;
            [Tooltip("World bounds the room object had when the replacement was built. Zero size skips the check.")]
            public Bounds expectedBounds;
        }

        [Serializable]
        public class Follow
        {
            [Tooltip("Hierarchy path of the room object to follow")]
            public string roomPath;
            public Transform follower;
            [Tooltip("Follower pose in the room object's local space, recorded with the context menu 'Record follow offsets'")]
            public Vector3 localPosition;
            public Quaternion localRotation = Quaternion.identity;
            public bool recorded;
        }

        [Serializable]
        public class Check
        {
            public string roomPath;
            public Bounds expectedBounds;
            [Tooltip("What breaks if the room object moved, for the warning")]
            public string consequence;
        }

        [SerializeField] RoomSceneLoader m_Loader;
        [SerializeField] List<Swap> m_Swaps = new List<Swap>();
        [SerializeField] List<Follow> m_Follows = new List<Follow>();
        [SerializeField] List<Check> m_Checks = new List<Check>();
        [SerializeField, Tooltip("Hierarchy paths of room floors that become teleport areas")]
        List<string> m_TeleportFloors = new List<string>();
        [SerializeField] string m_TeleportInteractionLayer = "Teleport";
        [SerializeField, Tooltip("How far (metres, per bounds corner) a room object may drift before a swap is refused or a check warns")]
        float m_Tolerance = 0.02f;

        public bool isApplied { get; private set; }
        /// <summary>Warnings logged while applying: missing or moved room objects. Zero means the room matches our layout.</summary>
        public int warningCount { get; private set; }
        public event Action Applied;

        public RoomSceneLoader loader => m_Loader;
        public IReadOnlyList<Swap> swaps => m_Swaps;
        public IReadOnlyList<Follow> follows => m_Follows;

        public void Configure(RoomSceneLoader loader) => m_Loader = loader;
        public void AddTeleportFloor(string roomPath) { if (!m_TeleportFloors.Contains(roomPath)) m_TeleportFloors.Add(roomPath); }

        public void AddSwap(string roomPath, GameObject replacement, Bounds expectedBounds)
        {
            m_Swaps.RemoveAll(s => s.roomPath == roomPath);
            m_Swaps.Add(new Swap { roomPath = roomPath, replacement = replacement, expectedBounds = expectedBounds });
        }

        public void AddFollow(string roomPath, Transform follower, Vector3 localPosition, Quaternion localRotation)
        {
            m_Follows.RemoveAll(f => f.follower == follower);
            m_Follows.Add(new Follow { roomPath = roomPath, follower = follower, localPosition = localPosition, localRotation = localRotation, recorded = true });
        }

        public void AddCheck(string roomPath, Bounds expectedBounds, string consequence)
        {
            m_Checks.RemoveAll(c => c.roomPath == roomPath);
            m_Checks.Add(new Check { roomPath = roomPath, expectedBounds = expectedBounds, consequence = consequence });
        }

        void Start()
        {
            if (m_Loader == null) m_Loader = RoomSceneLoader.instance != null ? RoomSceneLoader.instance : FindAnyObjectByType<RoomSceneLoader>();
            if (m_Loader == null)
            {
                Warn("RoomBindings: no RoomSceneLoader; nothing to bind.");
                return;
            }
            m_Loader.WhenLoaded(Apply);
        }

        void Warn(string message)
        {
            warningCount++;
            Debug.LogWarning(message, this);
        }

        /// <summary>World bounds of a room object: its renderers, or its colliders if it has no renderer.</summary>
        public static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (found) return true;
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (!found) { bounds = c.bounds; found = true; }
                else bounds.Encapsulate(c.bounds);
            }
            return found;
        }

        bool BoundsMatch(GameObject go, Bounds expected, out Bounds actual)
        {
            actual = default;
            if (expected.size == Vector3.zero) return true;
            if (!TryGetWorldBounds(go, out actual)) return false;
            return Vector3.Distance(actual.min, expected.min) <= m_Tolerance && Vector3.Distance(actual.max, expected.max) <= m_Tolerance;
        }

        void Apply(Scene room)
        {
            foreach (var s in m_Swaps)
            {
                var target = RoomSceneLoader.FindByPath(room, s.roomPath);
                if (target == null)
                {
                    Warn("RoomBindings: the room has no '" + s.roomPath + "'; keeping our replacement only.");
                    if (s.replacement != null) s.replacement.SetActive(true);
                    continue;
                }
                if (!BoundsMatch(target, s.expectedBounds, out var actual))
                {
                    // Their object moved or changed size: our replacement would leave a gap or overlap, so leave theirs alone.
                    Warn("RoomBindings: '" + s.roomPath + "' is at " + actual.min.ToString("F2") + ".." + actual.max.ToString("F2") + ", not " + s.expectedBounds.min.ToString("F2") + ".." + s.expectedBounds.max.ToString("F2")
                        + "; keeping it and leaving " + (s.replacement != null ? s.replacement.name : "the replacement") + " off.");
                    if (s.replacement != null) s.replacement.SetActive(false);
                    continue;
                }
                target.SetActive(false);
                if (s.replacement != null) s.replacement.SetActive(true);
            }
            foreach (var f in m_Follows)
            {
                if (f.follower == null || !f.recorded) continue;
                var target = RoomSceneLoader.FindByPath(room, f.roomPath);
                if (target == null) { Warn("RoomBindings: the room has no '" + f.roomPath + "'; " + f.follower.name + " keeps its saved pose."); continue; }
                var t = target.transform;
                f.follower.SetPositionAndRotation(t.TransformPoint(f.localPosition), t.rotation * f.localRotation);
            }
            foreach (var c in m_Checks)
            {
                var target = RoomSceneLoader.FindByPath(room, c.roomPath);
                if (target == null) { Warn("RoomBindings: the room has no '" + c.roomPath + "'; " + c.consequence); continue; }
                if (!BoundsMatch(target, c.expectedBounds, out var actual))
                    Warn("RoomBindings: '" + c.roomPath + "' moved to " + actual.min.ToString("F2") + ".." + actual.max.ToString("F2") + "; " + c.consequence);
            }
            var teleportMask = InteractionLayerMask.GetMask(m_TeleportInteractionLayer);
            foreach (var path in m_TeleportFloors)
            {
                var floor = RoomSceneLoader.FindByPath(room, path);
                if (floor == null) { Warn("RoomBindings: the room has no floor '" + path + "' to teleport on."); continue; }
                if (floor.GetComponent<Collider>() == null) { Warn("RoomBindings: '" + path + "' has no collider; it cannot be a teleport area."); continue; }
                var area = floor.GetComponent<TeleportationArea>();
                if (area == null) area = floor.AddComponent<TeleportationArea>();
                area.interactionLayers = teleportMask;
                area.matchOrientation = MatchOrientation.WorldSpaceUp;
                area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            }
            isApplied = true;
            Applied?.Invoke();
        }

#if UNITY_EDITOR
        /// <summary>
        /// With the room scene open next to ours in the Editor, records each follower's pose relative to its room object and the
        /// current bounds of every swapped and checked room object.
        /// </summary>
        [ContextMenu("Record follow offsets and expected bounds")]
        public void RecordFromOpenRoom()
        {
            var room = SceneManager.GetSceneByPath(m_Loader != null ? m_Loader.roomScenePath : "Assets/Scenes/room.unity");
            if (!room.isLoaded) { Debug.LogWarning("RoomBindings: open the room scene additively before recording.", this); return; }
            foreach (var f in m_Follows)
            {
                var target = RoomSceneLoader.FindByPath(room, f.roomPath);
                if (target == null || f.follower == null) { Debug.LogWarning("RoomBindings: cannot record '" + f.roomPath + "'.", this); continue; }
                var t = target.transform;
                f.localPosition = t.InverseTransformPoint(f.follower.position);
                f.localRotation = Quaternion.Inverse(t.rotation) * f.follower.rotation;
                f.recorded = true;
            }
            foreach (var s in m_Swaps)
            {
                var target = RoomSceneLoader.FindByPath(room, s.roomPath);
                if (target != null && TryGetWorldBounds(target, out var b)) s.expectedBounds = b;
            }
            foreach (var c in m_Checks)
            {
                var target = RoomSceneLoader.FindByPath(room, c.roomPath);
                if (target != null && TryGetWorldBounds(target, out var b)) c.expectedBounds = b;
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
