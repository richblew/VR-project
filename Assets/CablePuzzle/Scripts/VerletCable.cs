using UnityEngine;

namespace CablePuzzle
{
    /// <summary>
    /// Cheap Verlet rope drawn with a LineRenderer. One end is pinned to a fixed anchor,
    /// the other follows a connector. Rope points are kept inside the puzzle volume.
    /// In edit mode it draws a simple sagging preview so the cable follows the box when you move it.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public class VerletCable : MonoBehaviour
    {
        public Transform startAnchor;                 // fixed end (wall)
        public Transform endAnchor;                   // connector end
        public BoxCollider volume;                    // puzzle box interior (optional)
        public bool autoMaxLength = true;             // reach = farthest volume corner * 1.1
        [Min(3)] public int points = 24;
        public float minLength = 0.15f;
        public float maxLength = 2f;
        public float slack = 1.12f;                   // >1 = sags when not taut
        public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
        [Range(0.9f, 1f)] public float damping = 0.985f;
        [Range(2, 20)] public int iterations = 8;

        public float MaxLength => maxLength;

        LineRenderer lr;
        [System.NonSerialized] Vector3[] pos, prev;   // runtime-only; never saved or carried over a reload
        float accumulator;
        const float Step = 1f / 60f;
        const float TeleportDistance = 0.5f;          // anchor jump that triggers a rope reset

        void Awake()
        {
            if (Application.isPlaying) Init();        // edit mode only draws the preview
        }

        // Also called lazily from LateUpdate: after a script recompile during Play mode the
        // non-serialized rope arrays are lost and Awake is not called again.
        void Init()
        {
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = points;
            pos = new Vector3[points];
            prev = new Vector3[points];
            if (volume != null && autoMaxLength)
                maxLength = FarthestCornerDistance() * 1.1f;
            ResetRope();
        }

        /// <summary>Snap the rope back to a straight line between its ends (call after moving the rig or the box).</summary>
        public void ResetRope()
        {
            if (pos == null || startAnchor == null || endAnchor == null) return;
            accumulator = 0f;
            for (int i = 0; i < points; i++)
                pos[i] = prev[i] = Vector3.Lerp(startAnchor.position, endAnchor.position, i / (points - 1f));
            lr.SetPositions(pos);
        }

        void LateUpdate()
        {
            if (startAnchor == null || endAnchor == null) return;

            if (!Application.isPlaying)
            {
                DrawEditorPreview();
                return;
            }

            if (lr == null || pos == null || prev == null || pos.Length != points || prev.Length != points)
                Init();
            if (pos.Length < 2) return;

            // The whole box was moved/teleported, or the connector was (e.g. a plug respawning):
            // restart the rope instead of letting it whip.
            if (Vector3.Distance(pos[0], startAnchor.position) > TeleportDistance ||
                Vector3.Distance(pos[pos.Length - 1], endAnchor.position) > TeleportDistance)
                ResetRope();

            accumulator += Mathf.Min(Time.deltaTime, 0.05f);
            while (accumulator >= Step) { Simulate(Step); accumulator -= Step; }
            lr.SetPositions(pos);
        }

        void DrawEditorPreview()
        {
            if (lr == null) lr = GetComponent<LineRenderer>();
            if (lr == null) return;

            const int n = 16;
            lr.useWorldSpace = true;
            if (lr.positionCount != n) lr.positionCount = n;

            Vector3 a = startAnchor.position, b = endAnchor.position;
            float d = Vector3.Distance(a, b);
            float length = d * slack;
            float h = Mathf.Sqrt(Mathf.Max(0f, 3f * d * (length - d) / 8f));   // parabola sag
            Vector3 c = (a + b) * 0.5f + Vector3.down * 2f * h;                // Bezier control point
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f), u = 1f - t;
                lr.SetPosition(i, u * u * a + 2f * u * t * c + t * t * b);
            }
        }

        void Simulate(float dt)
        {
            int last = points - 1;
            float dist = Vector3.Distance(startAnchor.position, endAnchor.position);
            float segLen = Mathf.Clamp(dist * slack, minLength, maxLength) / last;

            for (int i = 1; i < last; i++)
            {
                Vector3 vel = (pos[i] - prev[i]) * damping;
                prev[i] = pos[i];
                pos[i] += vel + gravity * (dt * dt);
            }

            for (int it = 0; it < iterations; it++)
            {
                pos[0] = startAnchor.position;
                pos[last] = endAnchor.position;
                for (int i = 0; i < last; i++)
                {
                    Vector3 delta = pos[i + 1] - pos[i];
                    float d = delta.magnitude;
                    if (d < 1e-5f) continue;
                    Vector3 corr = delta * ((d - segLen) / d);
                    if (i == 0) pos[i + 1] -= corr;
                    else if (i + 1 == last) pos[i] += corr;
                    else { pos[i] += corr * 0.5f; pos[i + 1] -= corr * 0.5f; }
                }
            }

            if (volume != null)
            {
                for (int i = 1; i < last; i++)
                {
                    Vector3 c = ClampToVolume(pos[i]);
                    if (c != pos[i]) { pos[i] = c; prev[i] = c; }   // stop at contact, no bounce
                }
            }

            pos[0] = startAnchor.position;
            pos[last] = endAnchor.position;
            prev[0] = pos[0];
            prev[last] = pos[last];
        }

        float FarthestCornerDistance()
        {
            float best = 0f;
            Vector3 h = volume.size * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = volume.transform.TransformPoint(
                    volume.center + new Vector3(h.x * sx, h.y * sy, h.z * sz));
                best = Mathf.Max(best, Vector3.Distance(startAnchor.position, corner));
            }
            return best;
        }

        Vector3 ClampToVolume(Vector3 world)
        {
            Vector3 l = volume.transform.InverseTransformPoint(world) - volume.center;
            Vector3 h = volume.size * 0.5f;
            l = new Vector3(Mathf.Clamp(l.x, -h.x, h.x), Mathf.Clamp(l.y, -h.y, h.y), Mathf.Clamp(l.z, -h.z, h.z));
            return volume.transform.TransformPoint(l + volume.center);
        }
    }
}
