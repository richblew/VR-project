using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CablePuzzle.EditorTools
{
    /// <summary>
    /// Tools > Cable Puzzle > Build Puzzle Box In Open Scene
    /// Creates a puzzle box with 5 cable pairs (one cable anchored on each side per pair).
    /// Everything is created under one "PuzzleBox" root object and is undoable.
    /// </summary>
    public static class CablePuzzleBuilder
    {
        const int PairCount = 5;
        const string MaterialFolder = "Assets/CablePuzzle/Materials";

        static readonly Color[] PairColors =
        {
            new Color(0.90f, 0.20f, 0.20f), // red
            new Color(0.20f, 0.45f, 0.95f), // blue
            new Color(0.20f, 0.80f, 0.35f), // green
            new Color(0.97f, 0.80f, 0.15f), // yellow
            new Color(0.80f, 0.30f, 0.85f), // magenta
        };

        [MenuItem("Tools/Cable Puzzle/Build Puzzle Box In Open Scene")]
        public static void Build()
        {
            var size = new Vector3(1.2f, 0.8f, 0.5f);   // interior: width, height, depth
            const float wall = 0.02f;

            var root = new GameObject("PuzzleBox");
            Undo.RegisterCreatedObjectUndo(root, "Build Cable Puzzle");
            root.transform.position = new Vector3(0f, 1f, 1f);   // front of the box faces -Z (towards the player)

            // Interior volume (used to size and contain the cables)
            var volGo = Child(root.transform, "Volume");
            var volume = volGo.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = size;

            // Walls: open at the front (-Z). The front guard is an invisible collider that keeps connectors inside.
            Wall(root.transform, "Wall_Floor",   new Vector3(0, -size.y / 2 - wall / 2, 0), new Vector3(size.x + 2 * wall, wall, size.z));
            Wall(root.transform, "Wall_Ceiling", new Vector3(0,  size.y / 2 + wall / 2, 0), new Vector3(size.x + 2 * wall, wall, size.z));
            Wall(root.transform, "Wall_Left",    new Vector3(-size.x / 2 - wall / 2, 0, 0), new Vector3(wall, size.y, size.z));
            Wall(root.transform, "Wall_Right",   new Vector3( size.x / 2 + wall / 2, 0, 0), new Vector3(wall, size.y, size.z));
            Wall(root.transform, "Wall_Back",    new Vector3(0, 0, size.z / 2 + wall / 2), new Vector3(size.x + 2 * wall, size.y + 2 * wall, wall));
            var guard = Child(root.transform, "FrontGuard (invisible)");
            guard.transform.localPosition = new Vector3(0, 0, -size.z / 2 - wall / 2);
            guard.AddComponent<BoxCollider>().size = new Vector3(size.x, size.y, wall);

            var sockets = new XRSocketInteractor[PairCount];

            for (int i = 0; i < PairCount; i++)
            {
                int pairId = i + 1;
                var mat = GetMaterial(pairId, PairColors[i]);
                var pair = Child(root.transform, $"Pair_{pairId}");

                float y = -0.30f + 0.15f * i;
                var anchorA = Anchor(pair.transform, "Anchor_A", new Vector3(-size.x / 2, y, 0.08f), mat);
                var anchorB = Anchor(pair.transform, "Anchor_B", new Vector3( size.x / 2, y, 0.08f), mat);

                var plug = MakePlug(pair.transform, $"Plug_{pairId}", anchorA.transform.localPosition + new Vector3(0.18f, 0, 0), pairId, mat);
                var recep = MakeReceptacle(pair.transform, $"Receptacle_{pairId}", anchorB.transform.localPosition - new Vector3(0.18f, 0, 0), pairId, mat);
                plug.partner = recep;
                recep.partner = plug;

                sockets[i] = AddSocket(recep, pairId);

                MakeCable(pair.transform, $"Cable_{pairId}A", anchorA.transform, plug.transform, volume, mat);
                MakeCable(pair.transform, $"Cable_{pairId}B", anchorB.transform, recep.transform, volume, mat);
            }

            var mgr = Child(root.transform, "CablePairManager").AddComponent<CablePairManager>();
            mgr.sockets = sockets;

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[CablePuzzle] Built PuzzleBox with " + PairCount + " cable pairs. Move the PuzzleBox root to place it in your room.");
        }

        // ---------- helpers ----------

        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Wall(Transform parent, string name, Vector3 localPos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
        }

        static GameObject Anchor(Transform parent, string name, Vector3 localPos, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.04f;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static CableConnector MakePlug(Transform parent, string name, Vector3 localPos, int pairId, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);   // 0.1 m long along its Y axis
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(0.04f, 0.05f, 0.04f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            SetupGrab(go);
            var c = go.AddComponent<CableConnector>();
            c.pairId = pairId;
            c.isPlug = true;
            return c;
        }

        static CableConnector MakeReceptacle(Transform parent, string name, Vector3 localPos, int pairId, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.07f;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            SetupGrab(go);
            var c = go.AddComponent<CableConnector>();
            c.pairId = pairId;
            c.isPlug = false;
            return c;
        }

        static void SetupGrab(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;                       // connectors stay where you leave them
            rb.mass = 0.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            // Only the solid collider counts for grabbing (keeps the socket trigger out of the list).
            grab.colliders.Clear();
            grab.colliders.Add(col);
        }

        static PairSocket AddSocket(CableConnector receptacle, int pairId)
        {
            var sockGo = Child(receptacle.transform, "Socket");
            sockGo.transform.localPosition = Vector3.zero;

            var trigger = sockGo.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.06f / receptacle.transform.localScale.x;   // ~6 cm in world space

            // Plug sits on top of the receptacle's face (cube half-size 3.5 cm + capsule half-length 5 cm).
            var attach = Child(sockGo.transform, "Attach");
            attach.transform.localPosition = new Vector3(0f, 0.085f / receptacle.transform.localScale.x, 0f);

            var socket = sockGo.AddComponent<PairSocket>();
            socket.pairId = pairId;
            socket.attachTransform = attach.transform;
            socket.showInteractableHoverMeshes = false;
            return socket;
        }

        static void MakeCable(Transform parent, string name, Transform start, Transform end, BoxCollider volume, Material mat)
        {
            var go = Child(parent, name);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.startWidth = lr.endWidth = 0.015f;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 6;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.sharedMaterial = mat;
            lr.positionCount = 2;                         // editor preview; the rope resizes this at runtime
            lr.SetPosition(0, start.position);
            lr.SetPosition(1, end.position);

            var cable = go.AddComponent<VerletCable>();
            cable.startAnchor = start;
            cable.endAnchor = end;
            cable.volume = volume;
        }

        static Material GetMaterial(int pairId, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/CablePuzzle", "Materials");

            string path = $"{MaterialFolder}/Cable_{pairId}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
