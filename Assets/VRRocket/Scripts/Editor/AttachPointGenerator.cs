using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRRocket.Editor
{
    /// <summary>
    /// Generates the attach transforms under a BodyTube from SourceArt/VR_Rocket_Kit_Parts_v2/attach_points.json
    /// (SPEC.md section 4.4) so nothing is typed by hand. Idempotent: re-running updates the existing children.
    /// </summary>
    public static class AttachPointGenerator
    {
        public const string JsonRelativePath = "SourceArt/VR_Rocket_Kit_Parts_v2/attach_points.json";
        public const string ContainerName = "AttachPoints";

        [Serializable]
        public class PointData
        {
            public string name;
            public float yaw_deg;
            public float[] pos_m;
        }

        [Serializable]
        public class FileData
        {
            public string space;
            public PointData[] points;
        }

        public static string JsonAbsolutePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", JsonRelativePath));

        public static FileData Load()
        {
            var path = JsonAbsolutePath;
            if (!File.Exists(path)) throw new FileNotFoundException("attach_points.json not found", path);
            var data = JsonUtility.FromJson<FileData>(File.ReadAllText(path));
            if (data == null || data.points == null || data.points.Length == 0) throw new InvalidDataException("attach_points.json has no points");
            return data;
        }

        [MenuItem("VR Rocket/Generate Attach Points On Selected BodyTube")]
        static void GenerateOnSelection()
        {
            var root = Selection.activeTransform;
            if (root == null)
            {
                Debug.LogError("Select the BodyTube root first.");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Generate attach points");
            var points = Generate(root);
            Debug.Log("Generated " + points.Count + " attach points under " + root.name + ".");
        }

        /// <summary>Creates or updates the attach point children under the body tube root. Positions are in the tube's local space.</summary>
        public static List<AttachPoint> Generate(Transform bodyTubeRoot)
        {
            var data = Load();
            var container = bodyTubeRoot.Find(ContainerName);
            if (container == null)
            {
                container = new GameObject(ContainerName).transform;
                container.SetParent(bodyTubeRoot, false);
            }
            container.localPosition = Vector3.zero;
            container.localRotation = Quaternion.identity;
            container.localScale = Vector3.one;

            var result = new List<AttachPoint>(data.points.Length);
            foreach (var p in data.points)
            {
                var child = container.Find(p.name);
                if (child == null)
                {
                    child = new GameObject(p.name).transform;
                    child.SetParent(container, false);
                }
                child.localPosition = new Vector3(p.pos_m[0], p.pos_m[1], p.pos_m[2]);
                child.localRotation = Quaternion.Euler(0f, p.yaw_deg, 0f);
                child.localScale = Vector3.one;
                var ap = child.GetComponent<AttachPoint>();
                if (ap == null) ap = child.gameObject.AddComponent<AttachPoint>();
                ap.Configure(p.name, AttachPoint.AcceptsFromName(p.name), p.yaw_deg);
                EditorUtility.SetDirty(ap);
                result.Add(ap);
            }
            return result;
        }
    }
}
