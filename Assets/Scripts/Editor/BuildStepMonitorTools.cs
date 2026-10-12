using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRRocket.Editor
{
    /// <summary>
    /// The build-step monitor beside the blueprint panel (planned with Richard, 12 October 2026): one screen gives the step in
    /// text, the monitor next to it shows the rocket as it should look after that step.
    /// <list type="bullet">
    /// <item>VR Rocket > Capture Build Step Images renders the five pictures from AssembledRocket_Static (rerun it if the rocket's
    /// look changes);</item>
    /// <item>VR Rocket > Set Up Build Step Monitor adds the monitor to the open VRRocket_Main and wires it to BlueprintDisplay.</item>
    /// </list>
    /// </summary>
    public static class BuildStepMonitorTools
    {
        public const string RocketPrefab = "Assets/Prefabs/Rocket/AssembledRocket_Static.prefab";
        public const string ImageFolder = "Assets/Texture/BuildSteps";
        public const string MaterialPath = "Assets/Materials/Screens/Mat_BuildStep.mat";
        public static readonly string[] StepNames = { "1_Fins", "2_Flaps", "3_Nose", "4_Motor", "5_Cap" };
        // part name prefixes shown from each step on (the tube is always shown)
        static readonly string[] k_StepParts = { "TailFin_", "WingFlap_", "NoseCone_", "Motor_", "MotorCap_" };
        static readonly Color k_Background = new Color(0.02f, 0.045f, 0.065f, 1f);   // the menu panel's dark glass
        const int k_Width = 768, k_Height = 512;   // 3:2, the shape of the blueprint screen (0.9 x 0.6 m)

        public static string ImagePath(int step) => ImageFolder + "/BuildStep_" + StepNames[step] + ".png";

        [MenuItem("VR Rocket/Capture Build Step Images")]
        public static void CaptureImages()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RocketPrefab);
            if (prefab == null) { Debug.LogError("Capture Build Step Images: no " + RocketPrefab); return; }
            if (!AssetDatabase.IsValidFolder(ImageFolder)) AssetDatabase.CreateFolder("Assets/Texture", "BuildSteps");

            // everything is built far below the room and never saved with the scene
            var origin = new Vector3(0f, -500f, 0f);
            // lying on its side, nose to the right, so it fills the wide screen
            var rocket = (GameObject)Object.Instantiate(prefab, origin, Quaternion.Euler(0f, 0f, -90f));
            var lightGo = new GameObject("BuildStepKeyLight");
            var camGo = new GameObject("BuildStepCamera");
            foreach (var go in new[] { rocket, lightGo, camGo }) go.hideFlags = HideFlags.HideAndDontSave;
            var rt = new RenderTexture(k_Width, k_Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var tex = new Texture2D(k_Width, k_Height, TextureFormat.RGB24, false);
            try
            {
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(30f, 150f, 0f);
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = k_Background;
                cam.fieldOfView = 20f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 20f;
                cam.targetTexture = rt;

                for (var step = 0; step < StepNames.Length; step++)
                {
                    foreach (var t in rocket.GetComponentsInChildren<Transform>(true))
                    {
                        for (var k = 0; k < k_StepParts.Length; k++)
                            if (t.name.StartsWith(k_StepParts[k])) t.gameObject.SetActive(k <= step);
                    }
                    // frame the finished rocket the same way in every picture, so the steps line up
                    var b = Bounds(rocket, true);
                    var tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    var distance = 1.15f * Mathf.Max(b.extents.y / tanHalf, b.extents.x / (tanHalf * k_Width / k_Height)) + b.extents.z;
                    var dir = Quaternion.Euler(18f, 15f, 0f) * Vector3.back;
                    camGo.transform.position = b.center + dir * distance;
                    camGo.transform.LookAt(b.center);
                    cam.Render();
                    var previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, k_Width, k_Height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.GetFullPath(ImagePath(step)), tex.EncodeToPNG());
                }
            }
            finally
            {
                Object.DestroyImmediate(rocket);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(camGo);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.Refresh();
            Debug.Log("Capture Build Step Images: wrote " + StepNames.Length + " images to " + ImageFolder);
        }

        /// <summary>Bounds of every renderer under the root; with <paramref name="includeInactive"/> the hidden parts count too.</summary>
        static Bounds Bounds(GameObject root, bool includeInactive)
        {
            var found = false;
            var b = new Bounds(root.transform.position, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>(includeInactive))
            {
                var rb = r.bounds;
                if (!r.gameObject.activeInHierarchy)
                {
                    // inactive renderers report empty bounds: use the mesh bounds instead
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var m = mf.sharedMesh.bounds;
                    rb = new Bounds(r.transform.TransformPoint(m.center), Vector3.Scale(m.size, r.transform.lossyScale));
                }
                if (!found) { b = rb; found = true; }
                else b.Encapsulate(rb);
            }
            return b;
        }

        [MenuItem("VR Rocket/Set Up Build Step Monitor")]
        public static void SetUpMonitor()
        {
            var display = Object.FindAnyObjectByType<BlueprintDisplay>(FindObjectsInactive.Include);
            var panel = GameObject.Find("BlueprintPanel");
            if (display == null || panel == null) { Debug.LogError("Set Up Build Step Monitor: open VRRocket_Main (it has GameFlow and BlueprintPanel)."); return; }

            var images = new Texture[StepNames.Length];
            for (var i = 0; i < images.Length; i++)
            {
                images[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath(i));
                if (images[i] == null) { Debug.LogError("Set Up Build Step Monitor: run VR Rocket > Capture Build Step Images first."); return; }
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.SetTexture("_BaseMap", images[images.Length - 1]);
            EditorUtility.SetDirty(mat);

            // the monitor is a copy of the blueprint panel (same frame and screen), beside it on the back wall, without the text
            var old = GameObject.Find("BuildStepMonitor");
            if (old != null) Object.DestroyImmediate(old);
            var monitor = Object.Instantiate(panel, panel.transform.parent);
            monitor.name = "BuildStepMonitor";
            monitor.transform.position = panel.transform.position + Vector3.left * 1.1f;
            var text = monitor.transform.Find("BlueprintText");
            if (text != null) Object.DestroyImmediate(text.gameObject);
            var frame = monitor.transform.Find("BlueprintFrame");
            if (frame != null) frame.name = "MonitorFrame";
            var screen = monitor.transform.Find("BlueprintScreen");
            screen.name = "MonitorScreen";
            var renderer = screen.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;

            var so = new SerializedObject(display);
            so.FindProperty("m_ImageScreen").objectReferenceValue = renderer;
            var list = so.FindProperty("m_StepImages");
            list.arraySize = images.Length;
            for (var i = 0; i < images.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            PlaceOnDesk();
            Debug.Log("Set Up Build Step Monitor: BuildStepMonitor added and wired to BlueprintDisplay. Save the scene.");
        }

        // Desk monitors (12 October 2026): the back wall above the desk holds Richard's three portraits (x -1.62 to 1.75,
        // y 1.50 to 2.80) with name plaques below them (y 1.37 to 1.45), so the two screens stand on the back of the desk
        // instead, in the gaps between the plaques, low enough to stay under the portraits.
        const float k_DeskTopY = 1.125f;    // Environment/AssmblyArea/DeskTop
        const float k_MonitorZ = 3.70f;     // near the desk's back edge (z 3.91)
        const float k_MonitorScale = 0.6f;  // of the 0.98 x 0.68 m panel: a 0.54 x 0.36 m screen
        const float k_Tilt = 12f;           // leaned back, facing up toward the eyes
        const float k_PictureX = -0.525f;   // right behind the kit, between the BA and NA plaques
        const float k_TextX = 0.66f;        // between the NA and MC plaques

        [MenuItem("VR Rocket/Place Build Screens On Desk")]
        public static void PlaceOnDesk()
        {
            var panel = GameObject.Find("BlueprintPanel");
            var monitor = GameObject.Find("BuildStepMonitor");
            if (panel == null || monitor == null) { Debug.LogError("Place Build Screens On Desk: open VRRocket_Main with BlueprintPanel and BuildStepMonitor."); return; }
            var stand = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Mat_DarkMetal.mat");
            foreach (var (go, x) in new[] { (monitor, k_PictureX), (panel, k_TextX) })
            {
                Undo.RecordObject(go.transform, "Place build screens");
                var halfHeight = 0.68f * k_MonitorScale * 0.5f;
                var centreY = k_DeskTopY + 0.03f + halfHeight * Mathf.Cos(k_Tilt * Mathf.Deg2Rad);
                go.transform.SetPositionAndRotation(new Vector3(x, centreY, k_MonitorZ), Quaternion.Euler(k_Tilt, 0f, 0f));
                go.transform.localScale = Vector3.one * k_MonitorScale;
                var old = go.transform.parent != null ? go.transform.parent.Find(go.name + "Stand") : GameObject.Find(go.name + "Stand")?.transform;
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                foot.name = go.name + "Stand";
                Object.DestroyImmediate(foot.GetComponent<Collider>());
                foot.transform.SetParent(go.transform.parent, false);
                foot.transform.position = new Vector3(x, k_DeskTopY + 0.015f, k_MonitorZ + 0.02f);
                foot.transform.localScale = new Vector3(0.24f, 0.03f, 0.14f);
                if (stand != null) foot.GetComponent<Renderer>().sharedMaterial = stand;
            }
            EditorSceneManager.MarkSceneDirty(panel.scene);
        }
    }
}
