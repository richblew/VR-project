using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRRocket.Editor
{
    /// <summary>
    /// The red ABORT screen that <see cref="AbortAlarm"/> flashes on the room's side screens. VR Rocket > Make Abort Screen renders
    /// the picture (white text on red, hazard stripes top and bottom) and makes the bright and dim materials it flashes between.
    /// </summary>
    public static class AbortScreenTools
    {
        public const string ImagePath = "Assets/Texture/Screens/Abort_Screen.png";
        public const string OnMaterialPath = "Assets/Materials/Screens/Mat_AbortScreen.mat";
        public const string OffMaterialPath = "Assets/Materials/Screens/Mat_AbortScreenDim.mat";
        const int k_Width = 1024, k_Height = 828;   // the shape of the side TVs' faces (1.82 x 1.47 m)
        const int k_Stripe = 40;
        static readonly Color k_Red = new Color(0.78f, 0.03f, 0.02f);
        static readonly Color k_Yellow = new Color(1f, 0.8f, 0.05f);
        static readonly Color k_Black = new Color(0.04f, 0.04f, 0.04f);

        [MenuItem("VR Rocket/Make Abort Screen")]
        public static void MakeMenu()
        {
            Make();
            Debug.Log("Make Abort Screen: wrote " + ImagePath + ", " + OnMaterialPath + " and " + OffMaterialPath);
        }

        /// <summary>Renders the picture if it is missing (or when <paramref name="force"/>), then returns the bright and dim materials.</summary>
        public static (Material on, Material off) Make(bool force = true)
        {
            if (force || !File.Exists(ImagePath)) Render();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
            return (MakeMaterial(OnMaterialPath, tex, Color.white), MakeMaterial(OffMaterialPath, tex, new Color(0.22f, 0.2f, 0.2f)));
        }

        static Material MakeMaterial(string path, Texture2D tex, Color tint)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void Render()
        {
            // built far below the room and never saved with the scene
            var origin = new Vector3(0f, -600f, 0f);
            var camGo = new GameObject("AbortScreenCamera");
            var titleGo = new GameObject("AbortTitle");
            var subGo = new GameObject("AbortSubtitle");
            foreach (var go in new[] { camGo, titleGo, subGo }) go.hideFlags = HideFlags.HideAndDontSave;
            var rt = new RenderTexture(k_Width, k_Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var tex = new Texture2D(k_Width, k_Height, TextureFormat.RGB24, false);
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = k_Red;
                cam.orthographic = true;
                cam.orthographicSize = 0.5f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 2f;
                cam.targetTexture = rt;
                camGo.transform.SetPositionAndRotation(origin + Vector3.back, Quaternion.identity);
                Text(titleGo, origin + new Vector3(0f, 0.08f, 0f), "ABORT", 2.5f, 0.4f);
                Text(subGo, origin + new Vector3(0f, -0.17f, 0f), "LAUNCH ABORTED", 0.85f, 0.1f);
                cam.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, k_Width, k_Height), 0, 0);
                RenderTexture.active = previous;
                // hazard stripes along the top and bottom edges
                var band = k_Height / 9;
                for (var y = 0; y < k_Height; y++)
                {
                    if (y >= band && y < k_Height - band) continue;
                    for (var x = 0; x < k_Width; x++)
                        tex.SetPixel(x, y, (x + y) / k_Stripe % 2 == 0 ? k_Yellow : k_Black);
                }
                tex.Apply();
                File.WriteAllBytes(Path.GetFullPath(ImagePath), tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(titleGo);
                Object.DestroyImmediate(subGo);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(ImagePath, ImportAssetOptions.ForceUpdate);
        }

        static void Text(GameObject go, Vector3 position, string text, float fontSize, float height)
        {
            go.transform.position = position;
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(1.15f, height);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.characterSpacing = 8f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.color = Color.white;
            tmp.ForceMeshUpdate();
        }
    }
}
