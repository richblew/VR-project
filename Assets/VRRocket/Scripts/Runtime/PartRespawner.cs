using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRRocket
{
    /// <summary>
    /// Parts tray and respawn (SPEC.md section 8.3). A free part that falls below the floor level, or stays outside the
    /// workstation bounds for respawnDelay, reappears at rest on the respawn pad. Also remembers every part's tray pose for a new build.
    /// </summary>
    public sealed class PartRespawner : MonoBehaviour
    {
        [SerializeField] AssemblyTuning m_Tuning;
        [SerializeField, Tooltip("Trigger box marking the workstation bounds. Parts outside it for respawnDelay respawn.")] BoxCollider m_Bounds;
        [SerializeField, Tooltip("Extra zones that count as in bounds (the submission bin, the console top). Parts inside any of them never respawn.")] List<BoxCollider> m_SafeZones = new List<BoxCollider>();
        [SerializeField, Tooltip("Parts below this world-space offset from the pad (metres, negative) have touched the floor and respawn at once.")] float m_FloorOffset = -0.5f;
        [SerializeField, Tooltip("Centre of the respawn pad. Parts are laid out in a small grid around it.")] Transform m_Pad;
        [SerializeField] Vector2 m_PadSlotSpacing = new Vector2(0.08f, 0.08f);
        [SerializeField] int m_PadColumns = 3;
        [SerializeField, Tooltip("Parts managed here. Filled automatically from children when empty.")] List<RocketPart> m_Parts = new List<RocketPart>();

        public UnityEvent<RocketPart> onRespawned = new UnityEvent<RocketPart>();

        readonly Dictionary<RocketPart, float> m_OutsideTimers = new Dictionary<RocketPart, float>();
        readonly Dictionary<RocketPart, Pose> m_TrayPoses = new Dictionary<RocketPart, Pose>();
        readonly List<Collider> m_ColliderBuffer = new List<Collider>(8);
        int m_NextSlot;

        public IReadOnlyList<RocketPart> parts => m_Parts;

        public void Configure(AssemblyTuning tuning, BoxCollider bounds, Transform pad)
        {
            m_Tuning = tuning;
            m_Bounds = bounds;
            m_Pad = pad;
        }

        /// <summary>Registers another box inside which parts count as in bounds (the bin, the console top).</summary>
        public void AddSafeZone(BoxCollider zone)
        {
            if (zone != null && !m_SafeZones.Contains(zone)) m_SafeZones.Add(zone);
        }

        /// <summary>Is the position inside the workstation bounds or any safe zone?</summary>
        public bool Contains(Vector3 position)
        {
            if (m_Bounds != null && m_Bounds.bounds.Contains(position)) return true;
            for (var i = 0; i < m_SafeZones.Count; i++)
                if (m_SafeZones[i] != null && m_SafeZones[i].bounds.Contains(position)) return true;
            return false;
        }

        void Awake()
        {
            if (m_Parts.Count == 0)
            {
                foreach (var p in GetComponentsInChildren<RocketPart>(true))
                    if (p.partType != PartType.BodyTube) m_Parts.Add(p);
            }
            RegisterParts();
        }

        void RegisterParts()
        {
            m_TrayPoses.Clear();
            m_OutsideTimers.Clear();
            foreach (var p in m_Parts)
            {
                if (p == null) continue;
                m_TrayPoses[p] = new Pose(p.transform.localPosition, p.transform.localRotation);
                m_OutsideTimers[p] = 0f;
            }
        }

        /// <summary>Replaces the managed parts (a fresh kit after a new build).</summary>
        public void SetParts(IEnumerable<RocketPart> parts)
        {
            m_Parts.Clear();
            m_Parts.AddRange(parts);
            RegisterParts();
        }

        /// <summary>Is this world position below the floor level or outside the workstation bounds?</summary>
        public bool IsOutOfBounds(Vector3 position, out bool belowFloor)
        {
            belowFloor = m_Pad != null && position.y < m_Pad.position.y + m_FloorOffset;
            if (belowFloor) return true;
            return m_Bounds != null && !Contains(position);
        }

        void Update()
        {
            if (m_Bounds == null || m_Pad == null) return;
            var delay = m_Tuning != null ? m_Tuning.respawnDelay : 1.5f;
            var floorY = m_Pad.position.y + m_FloorOffset;
            for (var i = 0; i < m_Parts.Count; i++)
            {
                var part = m_Parts[i];
                if (part == null || part.state != PartState.Free) continue;
                if (!m_OutsideTimers.ContainsKey(part)) m_OutsideTimers[part] = 0f;
                var grab = part.grabInteractable;
                if (grab != null && grab.isSelected) { m_OutsideTimers[part] = 0f; continue; }
                var pos = part.transform.position;
                if (pos.y < floorY)
                {
                    Respawn(part);
                    continue;
                }
                if (Contains(pos))
                {
                    m_OutsideTimers[part] = 0f;
                    continue;
                }
                var t = m_OutsideTimers[part] + Time.deltaTime;
                if (t >= delay) Respawn(part);
                else m_OutsideTimers[part] = t;
            }
        }

        /// <summary>Puts the part at rest on the respawn pad.</summary>
        public void Respawn(RocketPart part)
        {
            m_OutsideTimers[part] = 0f;
            var slot = m_NextSlot++;
            var col = slot % m_PadColumns;
            var row = (slot / m_PadColumns) % 2;
            var offset = new Vector3((col - (m_PadColumns - 1) * 0.5f) * m_PadSlotSpacing.x, 0f, (row - 0.5f) * m_PadSlotSpacing.y);
            var rotation = m_Pad.rotation * (m_TrayPoses.TryGetValue(part, out var tray) ? tray.rotation : Quaternion.identity);
            PlaceAtRest(part, m_Pad.position + m_Pad.rotation * offset, rotation);
            onRespawned.Invoke(part);
            AssemblyEvents.RaisePartRespawned(part);
        }

        /// <summary>Returns every part to its original tray pose, at rest.</summary>
        public void ResetAllToTray()
        {
            foreach (var part in m_Parts)
            {
                if (part == null || !m_TrayPoses.TryGetValue(part, out var tray)) continue;
                var parent = part.transform.parent;
                var worldPos = parent != null ? parent.TransformPoint(tray.position) : tray.position;
                var worldRot = parent != null ? parent.rotation * tray.rotation : tray.rotation;
                PlaceAtRest(part, worldPos, worldRot, keepHeight: true);
            }
        }

        /// <summary>Moves a part so that its colliders rest on the surface at <paramref name="surfacePoint"/> with no velocity.</summary>
        public void PlaceAtRest(RocketPart part, Vector3 surfacePoint, Quaternion rotation, bool keepHeight = false)
        {
            var body = part.body;
            var t = part.transform;
            if (body != null)
            {
                // A free part is always a dynamic body; a part parked out of bounds while kinematic must not stay kinematic.
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            t.SetPositionAndRotation(surfacePoint, rotation);
            if (!keepHeight)
            {
                // Collider bounds only update after a physics sync, so force one before measuring.
                Physics.SyncTransforms();
                // Lift so the lowest collider point sits on the surface.
                m_ColliderBuffer.Clear();
                part.GetComponentsInChildren(false, m_ColliderBuffer);
                var minY = float.MaxValue;
                foreach (var c in m_ColliderBuffer)
                {
                    if (c.isTrigger) continue;
                    minY = Mathf.Min(minY, c.bounds.min.y);
                }
                if (minY < float.MaxValue)
                    t.position += Vector3.up * (surfacePoint.y - minY + 0.001f);
            }
            if (body != null)
            {
                body.position = t.position;
                body.rotation = t.rotation;
                Physics.SyncTransforms();
            }
        }
    }
}
