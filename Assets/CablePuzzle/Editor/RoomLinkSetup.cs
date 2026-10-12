using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CablePuzzle.EditorTools
{
    /// <summary>
    /// Tools > Cable Puzzle > Set Up Room Link
    ///
    /// Links the virtual room to the physical room. It only touches the Player rig and adds new objects:
    ///   - Player: activated, tracking origin set to Floor, locomotion switched off, parented under a new RoomAnchor.
    ///   - New: RoomAnchor, RoomLink (RoomCalibrator + Virtual_O + Virtual_X), WalkableZone (4 corner markers).
    /// No other scene object is modified. Everything is one undo step.
    /// </summary>
    public static class RoomLinkSetup
    {
        struct Area
        {
            public float xmin, xmax, zmin, zmax;
            public bool fallback;
            public string note;
        }

        [MenuItem("Tools/Cable Puzzle/Set Up Room Link (Player, Calibration, Walkable Zone)")]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Room Link", "Exit Play mode first, then run this again.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            if (FindDeep(roots, "RoomLink") != null)
            {
                EditorUtility.DisplayDialog("Room Link", "A RoomLink object already exists in this scene, so nothing was changed.\n\nDelete RoomLink and RoomAnchor (and re-parent the Player) if you want to run the setup again.", "OK");
                return;
            }

            var origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (origin == null)
            {
                EditorUtility.DisplayDialog("Room Link", "Couldn't find an XR Origin (the Player) in the open scene.", "OK");
                return;
            }
            GameObject player = origin.gameObject;

            Transform asm = FindDeep(roots, "AssmblyArea");
            Transform mcd = FindDeep(roots, "Mission Control Desk");

            // ----- floor height and floor extents -----
            float floorY = 0.05f;
            var floorBounds = new Bounds(new Vector3(0f, 0.05f, -4f), new Vector3(8f, 0.1f, 16f));
            var floor = FindDeep(roots, "Floor");
            if (floor != null)
            {
                if (floor.TryGetComponent(out Collider fc)) floorBounds = fc.bounds;
                else if (floor.TryGetComponent(out Renderer fr)) floorBounds = fr.bounds;
                floorY = floorBounds.max.y;
            }

            Area area = ComputeArea(asm, mcd, floorBounds);

            Undo.SetCurrentGroupName("Set Up Room Link");
            int undoGroup = Undo.GetCurrentGroup();

            // ----- reference points -----
            Vector3 o = new Vector3(area.xmin, floorY, area.zmin);
            bool baselineAlongX = (area.xmax - area.xmin) >= (area.zmax - area.zmin);
            Vector3 x = baselineAlongX ? new Vector3(area.xmax, floorY, area.zmin) : new Vector3(area.xmin, floorY, area.zmax);

            var link = new GameObject("RoomLink");
            Undo.RegisterCreatedObjectUndo(link, "Set Up Room Link");
            var vo = NewChild(link.transform, "Virtual_O", o);
            var vx = NewChild(link.transform, "Virtual_X", x);

            // ----- walkable zone -----
            var zone = NewChild(link.transform, "WalkableZone", Vector3.zero);
            var corners = new[]
            {
                NewChild(zone.transform, "WalkableCorner_1", new Vector3(area.xmin, floorY, area.zmin)),
                NewChild(zone.transform, "WalkableCorner_2", new Vector3(area.xmax, floorY, area.zmin)),
                NewChild(zone.transform, "WalkableCorner_3", new Vector3(area.xmax, floorY, area.zmax)),
                NewChild(zone.transform, "WalkableCorner_4", new Vector3(area.xmin, floorY, area.zmax)),
            };

            // ----- room anchor + player -----
            var anchor = new GameObject("RoomAnchor");
            Undo.RegisterCreatedObjectUndo(anchor, "Set Up Room Link");
            Vector3 dir = x - o; dir.y = 0f;
            anchor.transform.SetPositionAndRotation(o, Quaternion.FromToRotation(Vector3.right, dir.normalized));

            Undo.SetTransformParent(player.transform, anchor.transform, "Set Up Room Link");
            Undo.RecordObject(player.transform, "Set Up Room Link");
            player.transform.localPosition = Vector3.zero;
            player.transform.localRotation = Quaternion.identity;

            Undo.RecordObject(player, "Set Up Room Link");
            player.SetActive(true);

            Undo.RecordObject(origin, "Set Up Room Link");
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            PrefabUtility.RecordPrefabInstancePropertyModifications(origin);

            string locomotionNote = "no Locomotion object found";
            var loco = FindIn(player.transform, "Locomotion");
            if (loco != null)
            {
                Undo.RecordObject(loco.gameObject, "Set Up Room Link");
                loco.gameObject.SetActive(false);
                locomotionNote = "Locomotion switched off";
            }

            // ----- components -----
            Transform head = origin.Camera != null ? origin.Camera.transform : FindIn(player.transform, "Main Camera");
            Transform rightHand = FindIn(player.transform, "Right Controller");

            var calibrator = Undo.AddComponent<RoomCalibrator>(link);
            calibrator.roomAnchor = anchor.transform;
            calibrator.xrOrigin = player.transform;
            calibrator.virtualPointA = vo;
            calibrator.virtualPointB = vx;
            calibrator.controllerTip = rightHand;
            calibrator.head = head;
            calibrator.confirm = FindActivateReference();
            calibrator.cables = Object.FindObjectsByType<VerletCable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EditorUtility.SetDirty(calibrator);

            var walkable = Undo.AddComponent<WalkableZone>(zone.gameObject);
            walkable.corners = corners;
            walkable.head = head;
            EditorUtility.SetDirty(walkable);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = link;

            // ----- report -----
            float w = area.xmax - area.xmin, d = area.zmax - area.zmin;
            string summary =
                $"Room link set up.\n\n" +
                $"Walkable rectangle: {w:F2} m (X) by {d:F2} m (Z)\n" +
                $"  X from {area.xmin:F2} to {area.xmax:F2},  Z from {area.zmin:F2} to {area.zmax:F2}\n" +
                (area.fallback ? $"\nNOTE: {area.note}\nMove the four WalkableCorner objects to match your real walkable area.\n" : "") +
                (Mathf.Max(w, d) > 6f ? "\nThis is larger than a typical room. Check the corner positions.\n" : "") +
                $"\nTape marks on the physical floor:\n" +
                $"  Mark O = corner at ({o.x:F2}, {o.z:F2})\n" +
                $"  Mark X = corner at ({x.x:F2}, {x.z:F2}), {(x - o).magnitude:F2} m from mark O\n\n" +
                $"Player: activated, tracking origin = Floor, {locomotionNote}, moved under RoomAnchor.\n" +
                (calibrator.confirm == null ? "\nNote: couldn't find the XRI trigger action; the right-hand trigger binding is used instead.\n" : "") +
                "\nSave the scene (Ctrl+S), then press Play. The first run asks you to calibrate: touch mark O, press the trigger, touch mark X, press the trigger.";
            Debug.Log("[RoomLink] " + summary);
            EditorUtility.DisplayDialog("Room Link", summary, "OK");
        }

        // ---------- area ----------

        static Area ComputeArea(Transform asm, Transform mcd, Bounds floor)
        {
            const float margin = 0.10f;   // keep the rectangle this far from both objects
            const float minGap = 0.8f;    // minimum space between the objects to count as "between"
            const float inset = 0.25f;    // keep away from the room walls

            var a = new Area();
            bool ok = false;

            if (TryGetBounds(asm, out Bounds A) && TryGetBounds(mcd, out Bounds B))
            {
                float gz1 = A.min.z - B.max.z;   // AssmblyArea further in +Z
                float gz2 = B.min.z - A.max.z;   // Mission Control Desk further in +Z
                float gx1 = A.min.x - B.max.x;
                float gx2 = B.min.x - A.max.x;
                float best = Mathf.Max(Mathf.Max(gz1, gz2), Mathf.Max(gx1, gx2));

                if (best >= minGap)
                {
                    if (best == gz1 || best == gz2)
                    {
                        float lo = best == gz1 ? B.max.z : A.max.z;
                        float hi = best == gz1 ? A.min.z : B.min.z;
                        a.zmin = lo + margin; a.zmax = hi - margin;
                        float x0 = Mathf.Max(A.min.x, B.min.x), x1 = Mathf.Min(A.max.x, B.max.x);
                        if (x1 - x0 < 1f) { x0 = A.min.x; x1 = A.max.x; }
                        a.xmin = x0; a.xmax = x1;
                    }
                    else
                    {
                        float lo = best == gx1 ? B.max.x : A.max.x;
                        float hi = best == gx1 ? A.min.x : B.min.x;
                        a.xmin = lo + margin; a.xmax = hi - margin;
                        float z0 = Mathf.Max(A.min.z, B.min.z), z1 = Mathf.Min(A.max.z, B.max.z);
                        if (z1 - z0 < 1f) { z0 = A.min.z; z1 = A.max.z; }
                        a.zmin = z0; a.zmax = z1;
                    }
                    ok = true;
                }
                else
                {
                    // The objects overlap or are too close to measure a gap between them: use a 2 m strip in front of AssmblyArea.
                    a.zmax = A.min.z - margin; a.zmin = a.zmax - 2f;
                    a.xmin = A.min.x; a.xmax = A.max.x;
                    a.note = "Couldn't find clear floor space between AssmblyArea and Mission Control Desk, so I used a 2 m strip in front of AssmblyArea.";
                }
            }
            else if (TryGetBounds(asm, out Bounds A2))
            {
                a.zmax = A2.min.z - margin; a.zmin = a.zmax - 2f;
                a.xmin = A2.min.x; a.xmax = A2.max.x;
                a.note = "Couldn't measure one of the two objects (no renderers found), so I used a 2 m strip in front of AssmblyArea.";
            }
            else
            {
                a.xmin = floor.center.x - 1.5f; a.xmax = floor.center.x + 1.5f;
                a.zmin = floor.center.z - 1.5f; a.zmax = floor.center.z + 1.5f;
                a.note = "Couldn't find AssmblyArea or Mission Control Desk, so I used a 3 m square in the middle of the floor.";
            }

            a.fallback = !ok;

            // keep inside the floor
            a.xmin = Mathf.Max(a.xmin, floor.min.x + inset); a.xmax = Mathf.Min(a.xmax, floor.max.x - inset);
            a.zmin = Mathf.Max(a.zmin, floor.min.z + inset); a.zmax = Mathf.Min(a.zmax, floor.max.z - inset);

            if (a.xmax - a.xmin < 0.5f || a.zmax - a.zmin < 0.5f)
            {
                a.fallback = true;
                a.note = (a.note ?? "") + " The rectangle came out very small.";
            }
            return a;
        }

        static bool TryGetBounds(Transform t, out Bounds bounds)
        {
            bounds = default;
            if (t == null) return false;
            bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        // ---------- helpers ----------

        static Transform NewChild(Transform parent, string name, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            return go.transform;
        }

        static Transform FindDeep(IEnumerable<GameObject> roots, string name)
        {
            foreach (var r in roots)
            {
                var t = FindIn(r.transform, name);
                if (t != null) return t;
            }
            return null;
        }

        static Transform FindIn(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindIn(c, name);
                if (f != null) return f;
            }
            return null;
        }

        static InputActionReference FindActivateReference()
        {
            foreach (var guid in AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is InputActionReference r && r.name == "XRI RightHand Interaction/Activate")
                        return r;
            }
            return null;
        }
    }
}
