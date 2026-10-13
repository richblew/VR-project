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
    /// <item>makes sure teleportation is enabled;</item>
    /// <item>tracks from the real floor (Floor tracking origin), so the player is their own height;</item>
    /// <item>switches locomotion back on and keeps the character controller's capsule at the feet.</item>
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
        [SerializeField, Tooltip("Track the head from the real floor, so the player is their own height. The room's rig asks for 'Not Specified', which on Quest can become Device mode, where the rig's 1.36 m camera offset is added on top of the real head height and the player stands far too tall.")]
        bool m_ForceFloorTracking = true;
        [SerializeField, Tooltip("Switch the rig's Locomotion object (move, turn, teleport, gravity) on if the room saved it off.")]
        bool m_EnableLocomotion = true;
        [SerializeField, Tooltip("Give the rig's CharacterController a standing-height capsule whose bottom is at the feet. A capsule reaching below the rig's origin is pushed up out of the floor, lifting the player.")]
        bool m_FixCharacterController = true;
        [SerializeField, Tooltip("Log the rig's height and tracking mode a few seconds after start (visible in adb logcat on Quest).")]
        bool m_LogRigState = true;

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

            if (m_ForceFloorTracking)
            {
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                origin.CameraYOffset = 0f;   // only used in Device mode; zero so a fallback never adds height
            }

            if (m_FixCharacterController) FixCharacterController();

            if (m_EnableLocomotion)
            {
                var mediator = origin.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionMediator>(true);
                if (mediator != null && !mediator.gameObject.activeSelf) mediator.gameObject.SetActive(true);
            }

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
            if (isActiveAndEnabled) StartCoroutine(SettleAndReport());
        }

        /// <summary>Snaps again once XR tracking has started, then logs the rig's state for headset debugging.</summary>
        System.Collections.IEnumerator SettleAndReport()
        {
            yield return new WaitForSeconds(1f);
            if (origin == null) yield break;
            if (m_SnapToFloor) SnapToFloor();
            yield return new WaitForSeconds(2f);
            if (origin == null || !m_LogRigState) yield break;
            var cam = origin.Camera != null ? origin.Camera.transform : null;
            var cc = origin.GetComponent<CharacterController>();
            Debug.Log("XRRigAdapter: rig y=" + origin.transform.position.y.ToString("F3")
                + " tracking=" + origin.CurrentTrackingOriginMode + " (requested " + origin.RequestedTrackingOriginMode + ")"
                + " eye y=" + (cam != null ? cam.position.y.ToString("F3") : "?")
                + " cc height=" + (cc != null ? cc.height.ToString("F2") + " centre y=" + cc.center.y.ToString("F2") : "none"), this);
        }

        void FixCharacterController()
        {
            var cc = origin.GetComponent<CharacterController>();
            if (cc == null) return;
            // XRI's body transformer resizes it to the head each frame while locomotion runs; this is the safe starting shape
            if (cc.height > 2.2f || cc.center.y - cc.height * 0.5f < 0f)
            {
                cc.height = 1.36f;
                cc.center = new Vector3(0f, cc.height * 0.5f + cc.skinWidth, 0f);
            }
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
