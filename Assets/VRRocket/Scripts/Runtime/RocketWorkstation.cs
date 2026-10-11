using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// Root of the RocketWorkstation prefab (SPEC.md section 8.1). Its origin sits on the desk surface. Everything rocket-related
    /// is a child of <see cref="scaledRoot"/>, which is scaled by the tuning asset's rocketScale. The body tube and the nine parts all
    /// start flat on the parts tray. Also knows how to spawn a fresh kit in the same places for <see cref="RocketAssembly.BeginNewBuild"/>.
    /// </summary>
    public sealed class RocketWorkstation : MonoBehaviour
    {
        [Serializable]
        public struct TraySlot
        {
            public PartType type;
            public int id;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        [SerializeField] AssemblyTuning m_Tuning;
        [SerializeField] Transform m_ScaledRoot;
        [SerializeField] PartRespawner m_Respawner;
        [SerializeField] Transform m_PartsTray;
        [SerializeField, Tooltip("The body tube lying on the tray at the start. Found among the tray's parts when empty.")] RocketPart m_Tube;
        [Header("Prefabs for a fresh kit")]
        [SerializeField] GameObject m_TubePrefab;
        [SerializeField] GameObject m_TailFinPrefab;
        [SerializeField] GameObject m_WingFlapPrefab;
        [SerializeField] GameObject m_NoseConePrefab;
        [SerializeField] GameObject m_MotorPrefab;
        [SerializeField] GameObject m_MotorCapPrefab;

        readonly List<TraySlot> m_TrayLayout = new List<TraySlot>();

        public AssemblyTuning tuning => m_Tuning;
        public Transform scaledRoot => m_ScaledRoot;
        public PartRespawner respawner => m_Respawner;
        public Transform partsTray => m_PartsTray;
        public IReadOnlyList<TraySlot> trayLayout => m_TrayLayout;

        /// <summary>The body tube the build starts with.</summary>
        public RocketPart tube
        {
            get
            {
                if (m_Tube == null)
                    foreach (var p in GetComponentsInChildren<RocketPart>(true))
                        if (p.partType == PartType.BodyTube) { m_Tube = p; break; }
                return m_Tube;
            }
        }

        public void Configure(AssemblyTuning tuning, Transform scaledRoot, PartRespawner respawner)
        {
            m_Tuning = tuning;
            m_ScaledRoot = scaledRoot;
            m_Respawner = respawner;
        }

        public void ConfigureKit(Transform partsTray, GameObject tube, GameObject fin, GameObject flap, GameObject nose, GameObject motor, GameObject cap)
        {
            m_PartsTray = partsTray;
            m_TubePrefab = tube; m_TailFinPrefab = fin; m_WingFlapPrefab = flap; m_NoseConePrefab = nose; m_MotorPrefab = motor; m_MotorCapPrefab = cap;
        }

        void Awake()
        {
            ApplyScale();
            CaptureTrayLayout();
        }

        /// <summary>Applies rocketScale from the tuning asset to the scaled root.</summary>
        public void ApplyScale()
        {
            if (m_ScaledRoot == null) return;
            var s = m_Tuning != null ? m_Tuning.rocketScale : 1f;
            m_ScaledRoot.localScale = Vector3.one * s;
        }

        /// <summary>Remembers where the tube and each loose part start on the tray, so a fresh kit lands in the same places.</summary>
        public void CaptureTrayLayout()
        {
            m_TrayLayout.Clear();
            if (m_PartsTray == null) return;
            foreach (var p in m_PartsTray.GetComponentsInChildren<RocketPart>(true))
            {
                if (p.transform.parent != m_PartsTray) continue;
                m_TrayLayout.Add(new TraySlot { type = p.partType, id = p.partId, localPosition = p.transform.localPosition, localRotation = p.transform.localRotation });
            }
        }

        /// <summary>The tube's rest pose on the bench: its tray slot, on the tray surface, in world space.</summary>
        public bool TryGetTubeHome(out Vector3 worldPosition, out Quaternion worldRotation)
        {
            foreach (var slot in m_TrayLayout)
            {
                if (slot.type != PartType.BodyTube) continue;
                worldPosition = m_PartsTray.TransformPoint(new Vector3(slot.localPosition.x, 0f, slot.localPosition.z));
                worldRotation = m_PartsTray.rotation * slot.localRotation;
                return true;
            }
            worldPosition = Vector3.zero; worldRotation = Quaternion.identity;
            return false;
        }

        GameObject PrefabFor(PartType type)
        {
            switch (type)
            {
                case PartType.BodyTube: return m_TubePrefab;
                case PartType.TailFin: return m_TailFinPrefab;
                case PartType.WingFlap: return m_WingFlapPrefab;
                case PartType.NoseCone: return m_NoseConePrefab;
                case PartType.Motor: return m_MotorPrefab;
                case PartType.MotorCap: return m_MotorCapPrefab;
                default: return null;
            }
        }

        bool HasTubeSlot()
        {
            foreach (var slot in m_TrayLayout) if (slot.type == PartType.BodyTube) return true;
            return false;
        }

        public bool canSpawnKit => m_TubePrefab != null && m_TailFinPrefab != null && m_WingFlapPrefab != null && m_NoseConePrefab != null && m_MotorPrefab != null && m_MotorCapPrefab != null && m_PartsTray != null && HasTubeSlot();

        /// <summary>Instantiates a new tube and nine new parts on the tray in their original places. The tube is returned, the parts are added to <paramref name="newParts"/>.</summary>
        public RocketPart SpawnKit(List<RocketPart> newParts)
        {
            if (!canSpawnKit) throw new InvalidOperationException("RocketWorkstation has no kit prefabs or tray layout to spawn from.");
            RocketPart tube = null;
            foreach (var slot in m_TrayLayout)
            {
                var go = Instantiate(PrefabFor(slot.type), m_PartsTray);
                go.name = slot.type == PartType.BodyTube ? "BodyTube" : slot.type + "_" + slot.id;
                go.transform.localPosition = slot.localPosition;
                go.transform.localRotation = slot.localRotation;
                var part = go.GetComponent<RocketPart>();
                part.Configure(slot.type, slot.id);
                // An interpolated rigidbody writes its own (stale) pose back over the transform next frame, so tell physics directly.
                var body = part.body;
                if (body != null) { body.position = go.transform.position; body.rotation = go.transform.rotation; }
                if (slot.type == PartType.BodyTube) tube = part;
                else newParts.Add(part);
            }
            Physics.SyncTransforms();
            m_Tube = tube;
            return tube;
        }
    }
}
