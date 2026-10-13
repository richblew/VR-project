using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// Draws a cable as a round tube along a LineRenderer's points (Yi-Xuan's VerletCable keeps moving the points; this follows
    /// them after it, every frame). The flat ribbon of the LineRenderer is switched off. The tube's texture runs along the cable,
    /// so a rubber material reads as a real wire.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CableTube : MonoBehaviour
    {
        [SerializeField, Tooltip("Tube radius in metres; 0 takes half the line's width")] float m_Radius;
        [SerializeField, Range(3, 16)] int m_Sides = 8;
        [SerializeField] Material m_Material;

        LineRenderer m_Line;
        Transform m_Tube;
        Mesh m_Mesh;
        Vector3[] m_Points = new Vector3[0];
        Vector3[] m_Vertices, m_Normals;
        Vector2[] m_Uvs;
        int m_Built = -1;

        public Mesh mesh => m_Mesh;
        public float radius => m_Radius > 0f ? m_Radius : (m_Line != null ? m_Line.startWidth * m_Line.widthMultiplier * 0.5f : 0.004f);

        public void Configure(Material material, float radius)
        {
            m_Material = material; m_Radius = radius;
        }

        void Awake()
        {
            m_Line = GetComponent<LineRenderer>();
            m_Tube = new GameObject("Tube").transform;
            m_Tube.SetParent(transform, false);
            m_Mesh = new Mesh { name = name + "_Tube" };
            m_Mesh.MarkDynamic();
            m_Tube.gameObject.AddComponent<MeshFilter>().sharedMesh = m_Mesh;
            var r = m_Tube.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = m_Material != null ? m_Material : m_Line.sharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnEnable() { if (m_Line != null) m_Line.enabled = false; }

        void OnDisable() { if (m_Line != null) m_Line.enabled = true; }

        void LateUpdate()
        {
            var n = m_Line.positionCount;
            if (n < 2) return;
            if (m_Points.Length != n) m_Points = new Vector3[n];
            m_Line.GetPositions(m_Points);
            if (!m_Line.useWorldSpace) for (var i = 0; i < n; i++) m_Points[i] = transform.TransformPoint(m_Points[i]);
            if (m_Built != n) Build(n);
            Fill(n);
        }

        void Build(int n)
        {
            var ring = m_Sides + 1;   // a seam vertex so the texture wraps cleanly
            m_Vertices = new Vector3[n * ring]; m_Normals = new Vector3[n * ring]; m_Uvs = new Vector2[n * ring];
            var tris = new int[(n - 1) * m_Sides * 6];
            var k = 0;
            for (var i = 0; i < n - 1; i++)
                for (var s = 0; s < m_Sides; s++)
                {
                    int a = i * ring + s, b = a + 1, c = a + ring, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            m_Mesh.Clear();
            m_Mesh.vertices = m_Vertices; m_Mesh.normals = m_Normals; m_Mesh.uv = m_Uvs;
            m_Mesh.triangles = tris;
            m_Built = n;
        }

        void Fill(int n)
        {
            var ring = m_Sides + 1;
            var r = radius;
            var tangent = (m_Points[1] - m_Points[0]).normalized;
            var normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var along = 0f;
            var toLocal = m_Tube.worldToLocalMatrix;
            for (var i = 0; i < n; i++)
            {
                if (i > 0) along += Vector3.Distance(m_Points[i], m_Points[i - 1]);
                var next = m_Points[Mathf.Min(i + 1, n - 1)]; var prev = m_Points[Mathf.Max(i - 1, 0)];
                var t = (next - prev).sqrMagnitude > 1e-10f ? (next - prev).normalized : tangent;
                // carry the ring's frame along the cable (parallel transport) so it never twists
                normal = Vector3.ProjectOnPlane(normal, t);
                if (normal.sqrMagnitude < 1e-8f) normal = Vector3.Cross(t, Vector3.up);
                normal.Normalize();
                var binormal = Vector3.Cross(t, normal);
                tangent = t;
                for (var s = 0; s <= m_Sides; s++)
                {
                    var angle = s * Mathf.PI * 2f / m_Sides;
                    var dir = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                    var v = i * ring + s;
                    m_Vertices[v] = toLocal.MultiplyPoint3x4(m_Points[i] + dir * r);
                    m_Normals[v] = toLocal.MultiplyVector(dir).normalized;
                    m_Uvs[v] = new Vector2((float)s / m_Sides, along / (Mathf.PI * 2f * r));
                }
            }
            m_Mesh.vertices = m_Vertices; m_Mesh.normals = m_Normals; m_Mesh.uv = m_Uvs;
            m_Mesh.RecalculateBounds();
        }
    }
}
