using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CablePuzzle
{
    /// <summary>
    /// Put on a plug. If it is dropped and ends up away from the puzzle, it goes back to where it started.
    /// The start pose is remembered relative to the plug's parent, so it still returns to the right spot if the box has been moved.
    /// A plug is never respawned while it is being held (by a hand or by a socket).
    /// A plug is sent back when either:
    ///  - it falls more than Fall Distance below its start height, or
    ///  - it stays outside the puzzle volume for Outside Seconds.
    /// The cables attached to it are reset at the same moment, so the rope does not whip or stretch.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlugRespawn : MonoBehaviour
    {
        [Tooltip("The puzzle box interior. Leave empty to use the volume of the plug's cable.")]
        public BoxCollider volume;
        [Tooltip("How far outside the volume (in the volume's local units) the plug may be before it counts as outside.")]
        public float volumeMargin = 0.05f;
        [Tooltip("Respawn after being outside the volume (and not held) for this long. 0 = never use this rule.")]
        public float outsideSeconds = 1f;
        [Tooltip("Respawn immediately when the plug drops this far below its start height (metres). 0 = never use this rule.")]
        public float fallDistance = 0.5f;
        [Tooltip("Cables to reset on respawn. Filled in automatically with every cable attached to this plug.")]
        public VerletCable[] cables;
        public UnityEvent onRespawned;

        Transform homeParent;
        Vector3 homePos;
        Quaternion homeRot;
        float outsideTimer;
        Rigidbody body;
        XRGrabInteractable grab;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();

            homeParent = transform.parent;
            if (homeParent != null) { homePos = transform.localPosition; homeRot = transform.localRotation; }
            else { homePos = transform.position; homeRot = transform.rotation; }

            if (cables == null || cables.Length == 0)
            {
                var found = new System.Collections.Generic.List<VerletCable>();
                foreach (var c in FindObjectsByType<VerletCable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (IsMine(c.endAnchor) || IsMine(c.startAnchor)) found.Add(c);
                }
                cables = found.ToArray();
            }
            if (volume == null)
            {
                foreach (var c in cables)
                    if (c != null && c.volume != null) { volume = c.volume; break; }
            }
        }

        bool IsMine(Transform t) => t != null && (t == transform || t.IsChildOf(transform));

        void Update()
        {
            // being held (hand or socket): never respawn
            if (grab != null && grab.isSelected) { outsideTimer = 0f; return; }

            GetHome(out var homeWorldPos, out _);

            if (fallDistance > 0f && transform.position.y < homeWorldPos.y - fallDistance)
            {
                Respawn();
                return;
            }

            if (outsideSeconds > 0f && volume != null && IsOutside(transform.position))
            {
                outsideTimer += Time.deltaTime;
                if (outsideTimer >= outsideSeconds) Respawn();
            }
            else outsideTimer = 0f;
        }

        void GetHome(out Vector3 p, out Quaternion r)
        {
            if (homeParent != null) { p = homeParent.TransformPoint(homePos); r = homeParent.rotation * homeRot; }
            else { p = homePos; r = homeRot; }
        }

        bool IsOutside(Vector3 world)
        {
            Vector3 l = volume.transform.InverseTransformPoint(world) - volume.center;
            Vector3 h = volume.size * 0.5f + Vector3.one * volumeMargin;
            return Mathf.Abs(l.x) > h.x || Mathf.Abs(l.y) > h.y || Mathf.Abs(l.z) > h.z;
        }

        /// <summary>Send the plug back to its start pose and reset its cable(s).</summary>
        [ContextMenu("Respawn Now")]
        public void Respawn()
        {
            if (grab != null && grab.isSelected) return;   // never yank it out of a hand or socket

            GetHome(out var p, out var r);
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = p;
                body.rotation = r;
            }
            transform.SetPositionAndRotation(p, r);
            Physics.SyncTransforms();                       // make the physics engine see the new pose now

            // The rope's inner points are still where the plug fell: restart the rope between its anchors.
            if (cables != null)
                foreach (var c in cables) if (c != null) c.ResetRope();

            outsideTimer = 0f;
            onRespawned?.Invoke();
        }
    }
}
