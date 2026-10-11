using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace VRRocket
{
    /// <summary>
    /// Adapts whatever XR rig the room provides (the teammates' Starter Assets "Player") to the VR Rocket systems at runtime,
    /// so the rig prefab and the room scene stay untouched:
    /// <list type="bullet">
    /// <item>silences the rig's generic hover and select buzz (SimpleHapticFeedback), because <see cref="AssemblyFeedback"/> plays
    /// designed haptics for every part event (SPEC 6);</item>
    /// <item>adds the RocketPart interaction layer to the near-far interactors so rocket parts can be grabbed;</item>
    /// <item>lowers the rig onto the floor under it, so the user does not drop at start;</item>
    /// <item>makes sure teleportation is enabled.</item>
    /// </list>
    /// </summary>
    public sealed class XRRigAdapter : MonoBehaviour
    {
        [SerializeField] RoomSceneLoader m_Loader;
        [SerializeField] bool m_SilenceGenericHaptics = true;
        [SerializeField] string[] m_RequiredInteractionLayers = { "Default", "RocketPart" };
        [SerializeField] bool m_SnapToFloor = true;
        [SerializeField, Tooltip("Layers the floor probe may land on")] LayerMask m_FloorMask = ~0;
        [SerializeField] bool m_EnableTeleport = true;
        [SerializeField, Tooltip("Jump is bound to the primary button, which Vive wands do not have and which is easy to hit by accident while building")]
        bool m_DisableJump = true;

        public XROrigin origin { get; private set; }
        public bool isApplied { get; private set; }

        public void Configure(RoomSceneLoader loader) => m_Loader = loader;

        void Start()
        {
            if (m_Loader == null) m_Loader = RoomSceneLoader.instance != null ? RoomSceneLoader.instance : FindAnyObjectByType<RoomSceneLoader>();
            if (m_Loader != null) m_Loader.WhenLoaded(_ => Apply());
            else Apply();
        }

        /// <summary>Applies the adaptations to the first XR Origin found. Safe to call more than once.</summary>
        public void Apply()
        {
            origin = FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogWarning("XRRigAdapter: no XR Origin in the loaded scenes.", this);
                return;
            }

            if (m_SilenceGenericHaptics)
            {
                foreach (var h in origin.GetComponentsInChildren<SimpleHapticFeedback>(true))
                {
                    h.playHoverEntered = false;
                    h.playSelectEntered = false;
                }
            }

            var required = InteractionLayerMask.GetMask(m_RequiredInteractionLayers);
            foreach (var nf in origin.GetComponentsInChildren<NearFarInteractor>(true))
                nf.interactionLayers = nf.interactionLayers | required;

            if (m_SnapToFloor) SnapToFloor();

            if (m_EnableTeleport)
            {
                foreach (var tp in origin.GetComponentsInChildren<TeleportationProvider>(true)) tp.enabled = true;
            }
            if (m_DisableJump)
            {
                foreach (var jump in origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump.JumpProvider>(true)) jump.enabled = false;
            }
            isApplied = true;
        }

        void SnapToFloor()
        {
            var t = origin.transform;
            var start = t.position + Vector3.up * 1.0f;
            // ignore the rig's own colliders (character controller, hands)
            var hits = Physics.RaycastAll(start, Vector3.down, 5f, m_FloorMask, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            var floorY = t.position.y;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(t)) continue;
                if (h.distance < best) { best = h.distance; floorY = h.point.y; }
            }
            if (best == float.MaxValue || Mathf.Abs(floorY - t.position.y) < 0.005f) return;
            var cc = origin.GetComponent<CharacterController>();
            var ccWasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            t.position = new Vector3(t.position.x, floorY, t.position.z);
            if (cc != null) cc.enabled = ccWasEnabled;
        }
    }
}
