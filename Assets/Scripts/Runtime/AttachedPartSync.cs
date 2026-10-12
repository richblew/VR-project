using System.Collections.Generic;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// Keeps attached parts on the rocket. Every part carries a Rigidbody (XRI's grab interactable requires one), and Unity only
    /// drags a child rigidbody along when its parent is moved through the transform: when the tube is moved by the physics engine
    /// (carried by XRI's kinematic movement, falling, settling on the bench) the physics write-back restores each child's stale
    /// pose and the parts stay behind. So, after physics and again right before rendering, every attached part is put back at the
    /// rest pose it had when it seated, in both its transform and its rigidbody. Held parts (being pulled off or twisted) are left
    /// to the hand. Lives on the body tube.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttachedPartSync : MonoBehaviour
    {
        readonly List<RocketPart> m_Parts = new List<RocketPart>(16);

        void OnEnable() => Application.onBeforeRender += SyncBeforeRender;
        void OnDisable() => Application.onBeforeRender -= SyncBeforeRender;
        void FixedUpdate() => Sync();
        void LateUpdate() => Sync();

        // After XRI's own before-render pass (XRInteractionUpdateOrder.k_BeforeRenderOrder = 100) and after tracked pose drivers
        // (order 0), so the parts are placed against the tube's final pose for this frame.
        [BeforeRenderOrder(200)]
        void SyncBeforeRender() => Sync();

        void Sync()
        {
            GetComponentsInChildren(true, m_Parts);
            for (var i = 0; i < m_Parts.Count; i++)
            {
                var p = m_Parts[i];
                if (p.transform == transform || p.state != PartState.Attached || p.attachedTo == null || p.isHeld) continue;
                var t = p.transform;
                t.localPosition = p.seatLocalPosition;
                t.localRotation = p.seatLocalRotation;
                var body = p.body;
                if (body != null)
                {
                    body.position = t.position;
                    body.rotation = t.rotation;
                }
            }
        }
    }
}
