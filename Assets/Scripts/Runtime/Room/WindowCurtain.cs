using System.Collections.Generic;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// A blackout curtain over the alcove window (VRRocket_Final). It follows the room's intro: as the intro dims the room the two
    /// panels draw closed and black the window out, and they open again as the lights come back up. Skipping or stopping the intro
    /// opens them smoothly. Each panel is a pleated mesh whose outer edge is its pivot; it is gathered by scaling its width.
    /// </summary>
    public sealed class WindowCurtain : MonoBehaviour
    {
        [SerializeField] TransmissionBridge m_Intro;
        [SerializeField] Transform[] m_Panels = new Transform[0];
        [SerializeField, Tooltip("Width of a panel when gathered beside the window (metres)")] float m_OpenWidth = 0.3f;
        [SerializeField, Tooltip("Width of a panel when drawn (metres): half the window plus the overlap")] float m_ClosedWidth = 1.85f;
        [SerializeField, Tooltip("The curtain is fully drawn once the room is this dimmed (0 to 1), before the darkest point")] float m_ClosedAtDim = 0.6f;
        [SerializeField, Tooltip("Fastest change of the drawn fraction per second, so a skipped intro opens smoothly")] float m_MaxSpeed = 0.8f;

        /// <summary>0 open, 1 fully drawn.</summary>
        public float drawn { get; private set; }

        public void Configure(TransmissionBridge intro, Transform[] panels, float openWidth, float closedWidth)
        {
            m_Intro = intro; m_Panels = panels; m_OpenWidth = openWidth; m_ClosedWidth = closedWidth;
        }

        void OnEnable() => Apply(drawn);

        void Update()
        {
            var dim = m_Intro != null ? m_Intro.dimAmount : 0f;
            var target = Mathf.Clamp01(dim / Mathf.Max(0.01f, m_ClosedAtDim));
            var next = Mathf.MoveTowards(drawn, target, m_MaxSpeed * Time.deltaTime);
            if (!Mathf.Approximately(next, drawn)) Apply(next);
        }

        void Apply(float value)
        {
            drawn = value;
            var width = Mathf.Lerp(m_OpenWidth, m_ClosedWidth, Mathf.SmoothStep(0f, 1f, value));
            foreach (var p in m_Panels)
                if (p != null) { var s = p.localScale; s.x = width; p.localScale = s; }
        }

        /// <summary>
        /// A curtain panel one metre wide (local +X from the pivot) and <paramref name="height"/> tall (local +Y), hanging from
        /// its top edge, folded in <paramref name="folds"/> pleats of depth <paramref name="depth"/> along local Z.
        /// </summary>
        public static Mesh BuildPleatMesh(float height, int folds, float depth, int segmentsPerFold = 8)
        {
            var columns = Mathf.Max(2, folds * segmentsPerFold) + 1;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (var i = 0; i < columns; i++)
            {
                var u = i / (float)(columns - 1);
                var z = depth * 0.5f * Mathf.Sin(u * folds * Mathf.PI * 2f);
                verts.Add(new Vector3(u, -height, z)); uvs.Add(new Vector2(u * folds * 0.5f, 0f));
                verts.Add(new Vector3(u, 0f, z)); uvs.Add(new Vector2(u * folds * 0.5f, height * 0.6f));
                if (i == 0) continue;
                var a = (i - 1) * 2; var b = i * 2;
                tris.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }
            var mesh = new Mesh { name = "CurtainPanel" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
