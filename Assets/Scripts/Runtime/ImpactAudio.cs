using UnityEngine;

namespace VRRocket
{
    /// <summary>Collision sounds (SPEC.md section 6): dull on the desk, sharp on another part, volume by speed. Raises an event; the feedback player plays it.</summary>
    [RequireComponent(typeof(RocketPart))]
    public sealed class ImpactAudio : MonoBehaviour
    {
        [SerializeField, Tooltip("Impacts slower than this are silent.")] float m_MinSpeed = 0.25f;
        [SerializeField, Tooltip("Minimum seconds between two impact sounds from this part.")] float m_Cooldown = 0.12f;

        RocketPart m_Part;
        float m_LastTime = -10f;

        void Awake()
        {
            m_Part = GetComponent<RocketPart>();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (Time.time - m_LastTime < m_Cooldown) return;
            var speed = collision.relativeVelocity.magnitude;
            if (speed < m_MinSpeed) return;
            m_LastTime = Time.time;
            var hitPart = collision.collider != null && collision.collider.GetComponentInParent<RocketPart>() != null;
            AssemblyEvents.RaisePartImpact(m_Part, speed, hitPart);
        }
    }
}
