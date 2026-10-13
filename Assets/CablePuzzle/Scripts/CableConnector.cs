using UnityEngine;

namespace CablePuzzle
{
    /// <summary>
    /// Marks a grabbable cable end. Each pair has one plug and one receptacle (the receptacle carries a PairSocket).
    /// </summary>
    public class CableConnector : MonoBehaviour
    {
        public int pairId;
        public bool isPlug;
        public CableConnector partner;

        void Start()
        {
            // The plug is snapped onto the receptacle, so stop the two solid bodies pushing each other apart.
            // Trigger colliders are skipped on purpose: ignoring them would also suppress the socket's trigger events.
            if (partner == null) return;
            var mine = GetComponentsInChildren<Collider>();
            var theirs = partner.GetComponentsInChildren<Collider>();
            foreach (var a in mine)
            {
                if (a == null || a.isTrigger) continue;
                foreach (var b in theirs)
                {
                    if (b == null || b.isTrigger) continue;
                    Physics.IgnoreCollision(a, b, true);
                }
            }
        }
    }
}
