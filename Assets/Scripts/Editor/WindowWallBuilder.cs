using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRRocket.Editor
{
    /// <summary>
    /// Builds the window wall that replaces the room's right wall: a mesh of their wall with an opening, in their own material, whose
    /// UVs continue the original cube's mapping face by face (so the texture lines up as if the opening were cut into their wall),
    /// four box colliders for the solid parts, glass, frame, mullions and an inner sill. Driven by <see cref="WindowOpening"/>.
    /// </summary>
    public static class WindowWallBuilder
    {
        const string GlassPath = "Assets/Materials/Environment/Mat_Glass.mat";
        const string FramePath = "Assets/Materials/Environment/Mat_DarkMetal.mat";

        [MenuItem("VR Rocket/Rebuild Window Wall")]
        static void RebuildFromOpenScenes()
        {
            var opening = Object.FindAnyObjectByType<WindowOpening>(FindObjectsInactive.Include);
            if (opening == null) { EditorUtility.DisplayDialog("Rebuild Window Wall", "Open VRRocket_Main (it has the WindowWall with a WindowOpening).", "OK"); return; }
            GameObject wall = null;
            for (var i = 0; i < SceneManager.sceneCount && wall == null; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != opening.gameObject.scene) wall = RoomSceneLoader.FindByPath(s, opening.roomWallPath);
            }
            // VRRocket_Final holds the room itself: the wall is in the same scene, and is switched off once replaced
            var sameScene = false;
            if (wall == null) { wall = RoomSceneLoader.FindByPath(opening.gameObject.scene, opening.roomWallPath); sameScene = wall != null; }
            if (wall == null) { EditorUtility.DisplayDialog("Rebuild Window Wall", "Open Assets/Scenes/room.unity additively next to VRRocket_Main, so the wall '" + opening.roomWallPath + "' can be read.", "OK"); return; }
            Rebuild(opening, wall);
            if (sameScene) { Undo.RecordObject(wall, "Rebuild window wall"); wall.SetActive(false); }
            var bindings = Object.FindAnyObjectByType<RoomBindings>(FindObjectsInactive.Include);
            if (bindings != null) bindings.RecordSwapBounds();
            EditorSceneManager.MarkSceneDirty(opening.gameObject.scene);
            Debug.Log("Window wall rebuilt from " + opening.roomWallPath + ". Save VRRocket_Main (not room.unity).", opening);
        }

        /// <summary>Replaces every child of the opening's object with a freshly built window wall.</summary>
        public static void Rebuild(WindowOpening opening, GameObject theirWall)
        {
            var root = opening.transform;
            for (var i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            var frame = AssetDatabase.LoadAssetAtPath<Material>(FramePath);

            var src = theirWall.GetComponent<MeshFilter>().sharedMesh;
            var l2w = theirWall.transform.localToWorldMatrix;
            var b = theirWall.GetComponent<Renderer>().bounds;
            float x0 = b.min.x, x1 = b.max.x, y0 = b.min.y, y1 = b.max.y, z0 = b.min.z, z1 = b.max.z;
            float wz0 = opening.zRange.x, wz1 = opening.zRange.y, wy0 = opening.yRange.x, wy1 = opening.yRange.y;

            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>(); var tris = new List<int>();
            void Quad4(Vector3 n, Vector3 a, Vector3 bb, Vector3 c, Vector3 d)
            {
                var i = verts.Count;
                foreach (var p in new[] { a, bb, c, d }) { verts.Add(p); normals.Add(n); uvs.Add(FaceUV(src, l2w, n, p)); }
                if (Vector3.Dot(Vector3.Cross(bb - a, c - a), n) >= 0f) tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            void XFace(float x, Vector3 n, float za, float zb, float ya, float yb) =>
                Quad4(n, new Vector3(x, ya, za), new Vector3(x, yb, za), new Vector3(x, yb, zb), new Vector3(x, ya, zb));
            void YFace(float y, Vector3 n, float za, float zb) =>
                Quad4(n, new Vector3(x0, y, za), new Vector3(x1, y, za), new Vector3(x1, y, zb), new Vector3(x0, y, zb));
            void ZFace(float z, Vector3 n, float ya, float yb) =>
                Quad4(n, new Vector3(x0, ya, z), new Vector3(x1, ya, z), new Vector3(x1, yb, z), new Vector3(x0, yb, z));
            foreach (var side in new[] { (x0, Vector3.left), (x1, Vector3.right) })
            {
                XFace(side.Item1, side.Item2, z0, wz0, y0, y1);
                XFace(side.Item1, side.Item2, wz1, z1, y0, y1);
                XFace(side.Item1, side.Item2, wz0, wz1, y0, wy0);
                XFace(side.Item1, side.Item2, wz0, wz1, wy1, y1);
            }
            YFace(y1, Vector3.up, z0, z1);
            YFace(y0, Vector3.down, z0, z1);
            ZFace(z0, Vector3.back, y0, y1);
            ZFace(z1, Vector3.forward, y0, y1);
            YFace(wy0, Vector3.up, wz0, wz1);       // sill reveal
            YFace(wy1, Vector3.down, wz0, wz1);     // head reveal
            ZFace(wz0, Vector3.forward, wy0, wy1);  // jamb reveals
            ZFace(wz1, Vector3.back, wy0, wy1);

            var mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(opening.meshAssetPath) };
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var MeshPath = opening.meshAssetPath;
            var folder = System.IO.Path.GetDirectoryName(MeshPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); mesh = existing; EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(mesh, MeshPath);
            AssetDatabase.SaveAssets();

            var wallGo = new GameObject("Wall");
            SceneManager.MoveGameObjectToScene(wallGo, root.gameObject.scene);
            wallGo.transform.SetParent(root, false);
            wallGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            wallGo.AddComponent<MeshRenderer>().sharedMaterials = theirWall.GetComponent<MeshRenderer>().sharedMaterials;
            void Col(Vector3 min, Vector3 max) { var c = wallGo.AddComponent<BoxCollider>(); c.center = (min + max) * 0.5f; c.size = max - min; }
            Col(new Vector3(x0, y0, z0), new Vector3(x1, y1, wz0));
            Col(new Vector3(x0, y0, wz1), new Vector3(x1, y1, z1));
            Col(new Vector3(x0, y0, wz0), new Vector3(x1, wy0, wz1));
            Col(new Vector3(x0, wy1, wz0), new Vector3(x1, y1, wz1));

            var midZ = (wz0 + wz1) * 0.5f; var w = wz1 - wz0; var h = wy1 - wy0; var midY = (wy0 + wy1) * 0.5f;
            var glassGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glassGo.name = "Glass";
            Object.DestroyImmediate(glassGo.GetComponent<Collider>());
            SceneManager.MoveGameObjectToScene(glassGo, root.gameObject.scene);
            glassGo.transform.SetParent(root, false);
            glassGo.transform.SetPositionAndRotation(new Vector3(x0 + 0.05f, midY, midZ), Quaternion.Euler(0f, 90f, 0f));
            glassGo.transform.localScale = new Vector3(w, h, 1f);
            var gr = glassGo.GetComponent<MeshRenderer>();
            gr.sharedMaterial = glass;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glassGo.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 0.02f);
            var fx = x0 + 0.02f;
            Box("Frame_Head", root, new Vector3(fx, wy1, midZ), new Vector3(0.12f, 0.06f, w + 0.1f), frame, false);
            Box("Frame_Sill", root, new Vector3(fx, wy0, midZ), new Vector3(0.12f, 0.06f, w + 0.1f), frame, false);
            Box("Frame_JambFront", root, new Vector3(fx, midY, wz0), new Vector3(0.12f, h, 0.06f), frame, false);
            Box("Frame_JambBack", root, new Vector3(fx, midY, wz1), new Vector3(0.12f, h, 0.06f), frame, false);
            if (opening.mullionZ != null) foreach (var z in opening.mullionZ) Box("Mullion", root, new Vector3(fx, midY, z), new Vector3(0.12f, h, 0.06f), frame, false);
            Box("InnerSill", root, new Vector3(x0 - 0.07f, wy0 - 0.02f, midZ), new Vector3(0.16f, 0.04f, w + 0.2f), frame, true);
        }

        static void Box(string name, Transform parent, Vector3 centre, Vector3 size, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetPositionAndRotation(centre, Quaternion.identity);
            go.transform.localScale = size;
            go.transform.SetParent(parent, true);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>The UV the original mesh gives a point on its face with normal <paramref name="n"/>, by an affine fit on that face.</summary>
        static Vector2 FaceUV(Mesh src, Matrix4x4 l2w, Vector3 n, Vector3 p)
        {
            var v = src.vertices; var nn = src.normals; var uv = src.uv;
            var pts = new List<int>();
            for (var i = 0; i < v.Length && pts.Count < 4; i++)
                if (Vector3.Dot(l2w.MultiplyVector(nn[i]).normalized, n) > 0.99f) pts.Add(i);
            if (pts.Count < 3) return Vector2.zero;
            int ax, ay;   // the two world axes spanning the face
            if (Mathf.Abs(n.x) > 0.5f) { ax = 2; ay = 1; } else if (Mathf.Abs(n.y) > 0.5f) { ax = 0; ay = 2; } else { ax = 0; ay = 1; }
            var w = new Vector3[pts.Count];
            for (var i = 0; i < pts.Count; i++) w[i] = l2w.MultiplyPoint3x4(v[pts[i]]);
            int a = 0, b = 1, c = 2;
            for (var k = 2; k < pts.Count; k++)
            {
                var d = (w[1][ax] - w[0][ax]) * (w[k][ay] - w[0][ay]) - (w[1][ay] - w[0][ay]) * (w[k][ax] - w[0][ax]);
                if (Mathf.Abs(d) > 1e-6f) { c = k; break; }
            }
            float s0 = w[a][ax], t0 = w[a][ay], s1 = w[b][ax], t1 = w[b][ay], s2 = w[c][ax], t2 = w[c][ay];
            var det = (s1 - s0) * (t2 - t0) - (s2 - s0) * (t1 - t0);
            if (Mathf.Abs(det) < 1e-8f) return Vector2.zero;
            float ps = p[ax], pt = p[ay];
            var l1 = ((ps - s0) * (t2 - t0) - (s2 - s0) * (pt - t0)) / det;
            var l2 = ((s1 - s0) * (pt - t0) - (ps - s0) * (t1 - t0)) / det;
            return uv[pts[a]] + l1 * (uv[pts[b]] - uv[pts[a]]) + l2 * (uv[pts[c]] - uv[pts[a]]);
        }
    }
}
