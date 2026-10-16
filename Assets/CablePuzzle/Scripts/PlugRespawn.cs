using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CablePuzzle
{
    /// <summary>
    /// Put on a plug. If it is dropped, or gets stuck, it goes back to where it started.
    /// The start pose is remembered relative to the plug's parent, so it still returns to the right spot if the box has been moved.
    /// A plug is sent back when:
    ///  - it falls more than Fall Distance below its start height, or
    ///  - it stays outside the puzzle volume for Outside Seconds, or
    ///  - it lies still, not held, right at the edge of the volume (or outside it) for Stuck Seconds, e.g. balanced on the box rim, or
    ///  - a hand is holding it but it can not follow the hand (wedged against the box) for Held Lag Seconds:
    ///    the plug is then let go of and sent back.
    /// A plug held by a socket is never touched. The cables attached to it are reset at the same moment,
    /// so the rope does not whip or stretch.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlugRespawn : MonoBehaviour
    {
        [Header("Volume")]
        [Tooltip("The puzzle box interior. Leave empty to use the volume of the plug's cable.")]
        public BoxCollider volume;
        [Tooltip("How far outside the volume (in the volume's local units) the plug may be before it counts as outside.")]
        public float volumeMargin = 0.05f;

        [Header("Dropped")]
        [Tooltip("Respawn after being outside the volume (and not held) for this long. 0 = never use this rule.")]
        public float outsideSeconds = 1f;
        [Tooltip("Respawn immediately when the plug drops this far below its start height (metres). 0 = never use this rule.")]
        public float fallDistance = 0.5f;

        [Header("Stuck on the edge (not held)")]
        [Tooltip("Respawn if the plug lies still within Edge Inset of the volume's edge (sides, front, top) - or outside it - for this long. 0 = never use this rule.")]
        public float stuckSeconds = 2f;
        [Tooltip("Distance from the volume's edge, in the volume's local units, that counts as 'on the edge'. The floor of the box is not an edge. 0 = only outside the volume.")]
        public float edgeInset = 0.06f;
        [Tooltip("Slower than this (metres per second) counts as lying still.")]
        public float stillSpeed = 0.03f;
        [Tooltip("A plug this close (metres) to its start pose is never treated as stuck.")]
        public float homeTolerance = 0.06f;

        [Header("Held but wedged")]
        [Tooltip("If a hand holds the plug but the plug is this far (metres) from where the hand wants it for Held Lag Seconds, let go of it and respawn it. 0 = never use this rule.")]
        public float heldLagDistance = 0.35f;
        public float heldLagSeconds = 1f;

        [Header("Cables")]
        [Tooltip("Cables to reset on respawn. Filled in automatically with every cable attached to this plug.")]
        public VerletCable[] cables;
        public UnityEvent onRespawned;

        Transform homeParent;
        Vector3 homePos;
        Quaternion homeRot;
        float outsideTimer, stillTimer, lagTimer;
        Vector3 lastPos;
        Rigidbody body;
        XRGrabInteractable grab;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();

            homeParent = transform.parent;
            if (homeParent != null) { homePos = transform.localPosition; homeRot = transform.localRotation; }
            else { homePos = transform.position; homeRot = transform.rotation; }
            lastPos = transform.position;

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
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 pos = transform.position;
            float speed = (pos - lastPos).magnitude / dt;
            lastPos = pos;

            if (grab != null && grab.isSelected)
            {
                // held by a hand or a socket: never respawn it normally...
                outsideTimer = 0f; stillTimer = 0f;
                CheckHeldButWedged(dt);
                return;
            }
            lagTimer = 0f;

            GetHome(out var homeWorldPos, out _);

            if (fallDistance > 0f && pos.y < homeWorldPos.y - fallDistance)
            {
                Respawn();
                return;
            }

            if (outsideSeconds > 0f && volume != null && IsOutside(pos))
            {
                outsideTimer += dt;
                if (outsideTimer >= outsideSeconds) { Respawn(); return; }
            }
            else outsideTimer = 0f;

            // lying still on the rim / against an edge, away from where it belongs
            bool atHome = Vector3.Distance(pos, homeWorldPos) < homeTolerance;
            if (stuckSeconds > 0f && volume != null && !atHome && speed < stillSpeed && IsNearEdgeOrOutside(pos))
            {
                stillTimer += dt;
                if (stillTimer >= stuckSeconds) Respawn();
            }
            else stillTimer = 0f;
        }

        // A hand is holding the plug, but the plug is jammed against the box and can not follow it.
        void CheckHeldButWedged(float dt)
        {
            if (heldLagDistance <= 0f) { lagTimer = 0f; return; }
            var interactor = grab.firstInteractorSelecting;
            if (interactor == null || interactor is XRSocketInteractor) { lagTimer = 0f; return; }   // sockets hold it on purpose

            var wanted = interactor.GetAttachTransform(grab);   // where the hand wants the grip to be
            var actual = grab.GetAttachTransform(interactor);   // where the plug's grip really is
            if (wanted == null || actual == null) { lagTimer = 0f; return; }

            if (Vector3.Distance(wanted.position, actual.position) > heldLagDistance) lagTimer += dt;
            else lagTimer = 0f;

            if (lagTimer >= heldLagSeconds) ForceRespawn();
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

        // Outside the volume, or within Edge Inset of its sides/front/back/top. The bottom is skipped: that is the box floor,
        // where a plug is allowed to lie.
        bool IsNearEdgeOrOutside(Vector3 world)
        {
            Vector3 l = volume.transform.InverseTransformPoint(world) - volume.center;
            Vector3 h = volume.size * 0.5f;
            float toEdge = Mathf.Min(h.x - Mathf.Abs(l.x), h.z - Mathf.Abs(l.z), h.y - l.y);   // no bottom: h.y + l.y left out
            if (l.y < -h.y) return true;                                                          // below the floor = outside
            return toEdge < edgeInset;
        }

        /// <summary>Send the plug back to its start pose and reset its cable(s). Does nothing while it is held.</summary>
        [ContextMenu("Respawn Now")]
        public void Respawn()
        {
            if (grab != null && grab.isSelected) return;   // never yank it out of a hand or socket
            DoRespawn();
        }

        /// <summary>Let go of the plug first (whoever holds it), then send it back.</summary>
        [ContextMenu("Force Respawn")]
        public void ForceRespawn()
        {
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection(grab);
            DoRespawn();
        }

        void DoRespawn()
        {
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

            // The rope's inner points are still where the plug was: restart the rope between its anchors.
            if (cables != null)
                foreach (var c in cables) if (c != null) c.ResetRope();

            outsideTimer = stillTimer = lagTimer = 0f;
            lastPos = p;
            onRespawned?.Invoke();
        }
    }
}
