using UnityEngine;
using UnityEngine.Events;

namespace CablePuzzle
{
    /// <summary>
    /// Keeps the player inside the physically walkable area. The area is a polygon in the floor plane
    /// (a rectangle for this project). As the headset nears the edge a glowing outline fades in on the floor;
    /// outside the edge the view fades to black until the player steps back.
    /// The outline and fade are created at runtime, so nothing extra is stored in the scene.
    /// </summary>
    public class WalkableZone : MonoBehaviour
    {
        public Transform[] corners;                  // polygon corners, in order (floor plane)
        public Transform head;                       // the XR camera
        public float warnDistance = 0.5f;            // outline starts appearing this far inside the edge
        public float fadeDistance = 0.3f;            // view fully black this far outside the edge
        public Color outlineColor = new Color(0.2f, 0.9f, 1f, 1f);
        public float outlineWidth = 0.04f;
        public float outlineHeight = 0.02f;
        public UnityEvent onLeftArea;
        public UnityEvent onReturned;

        public bool IsInside { get; private set; } = true;
        public float SignedDistance { get; private set; }

        LineRenderer outline;
        Material outlineMat;
        Material fadeMat;
        GameObject fadeQuad;

        void Start()
        {
            if (corners == null || corners.Length < 3 || head == null)
            {
                Debug.LogError("[WalkableZone] Needs at least 3 corners and a head transform. Run Tools > Cable Puzzle > Set Up Room Link.");
                enabled = false;
                return;
            }
            CreateVisuals();
        }

        void CreateVisuals()
        {
            var shader = Shader.Find("Sprites/Default");     // simple unlit shader with alpha blending
            if (shader == null)
            {
                Debug.LogWarning("[WalkableZone] Sprites/Default shader not found; the boundary visuals are disabled (distance and events still work).");
                return;
            }

            // Floor outline
            outlineMat = new Material(shader);
            var lineGo = new GameObject("WalkableOutline");
            lineGo.transform.SetParent(transform, false);
            outline = lineGo.AddComponent<LineRenderer>();
            outline.loop = true;
            outline.useWorldSpace = true;
            outline.alignment = LineAlignment.TransformZ;
            outline.startWidth = outline.endWidth = outlineWidth;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.sharedMaterial = outlineMat;
            outline.positionCount = corners.Length;
            outline.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat so the ribbon faces up
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 p = corners[i].position;
                outline.SetPosition(i, new Vector3(p.x, p.y + outlineHeight, p.z));
            }

            // Full-view fade, a quad just in front of the camera
            fadeMat = new Material(shader);
            fadeMat.color = new Color(0f, 0f, 0f, 0f);
            fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fadeQuad.name = "WalkableFade";
            Destroy(fadeQuad.GetComponent<Collider>());
            fadeQuad.transform.SetParent(head, false);
            fadeQuad.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            fadeQuad.transform.localScale = new Vector3(3f, 3f, 1f);
            var r = fadeQuad.GetComponent<Renderer>();
            r.sharedMaterial = fadeMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            fadeQuad.SetActive(false);
        }

        void Update()
        {
            float d = SignedDistanceTo(head.position);
            SignedDistance = d;

            bool inside = d >= 0f;
            if (inside != IsInside)
            {
                IsInside = inside;
                (inside ? onReturned : onLeftArea)?.Invoke();
            }

            float outlineAlpha = Mathf.Clamp01(1f - d / Mathf.Max(0.01f, warnDistance));
            float fadeAlpha = inside ? 0f : Mathf.Clamp01(-d / Mathf.Max(0.01f, fadeDistance));

            if (outlineMat != null)
            {
                var c = outlineColor; c.a = outlineAlpha * outlineColor.a;
                outlineMat.color = c;
                outline.enabled = outlineAlpha > 0.01f;
            }
            if (fadeMat != null)
            {
                fadeMat.color = new Color(0f, 0f, 0f, fadeAlpha);
                fadeQuad.SetActive(fadeAlpha > 0.001f);
            }
        }

        void OnDestroy()
        {
            if (outlineMat != null) Destroy(outlineMat);
            if (fadeMat != null) Destroy(fadeMat);
            if (fadeQuad != null) Destroy(fadeQuad);
        }

        /// <summary>Distance to the polygon edge in metres: positive inside, negative outside.</summary>
        public float SignedDistanceTo(Vector3 world)
        {
            int n = corners.Length;
            var p = new Vector2(world.x, world.z);
            bool inside = false;
            float minD = float.MaxValue;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = XZ(corners[j]), b = XZ(corners[i]);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
                minD = Mathf.Min(minD, DistanceToSegment(p, a, b));
            }
            return inside ? minD : -minD;
        }

        static Vector2 XZ(Transform t) => new Vector2(t.position.x, t.position.z);

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        void OnDrawGizmos()
        {
            if (corners == null || corners.Length < 2) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < corners.Length; i++)
            {
                if (corners[i] == null) return;
                var next = corners[(i + 1) % corners.Length];
                if (next == null) return;
                Gizmos.DrawLine(corners[i].position, next.position);
                Gizmos.DrawWireSphere(corners[i].position, 0.06f);
            }
        }
    }
}
