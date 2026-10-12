using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

public class TransmissionController : MonoBehaviour
{
    public enum Mode
    {
        FullSequence,
        PreviewTransmissionScreen,
        PreviewStandbyScreen,
        PreviewLogoScreen,
        PreviewDimmedRoom
    }

    [Header("Mode (use a Preview mode to test one part)")]
    public Mode mode = Mode.FullSequence;

    [Header("Main Screen")]
    public Renderer mainScreen;
    public int screenMaterialIndex = 0;
    public Material nasaLogoMaterial;
    public Material standbyMaterial;
    public Material videoMaterial;

    [Header("Side Screens (blank during announcement + video)")]
    public Renderer[] sideScreens;
    public int sideScreenMaterialIndex = 0;
    public Material blankMaterial;
    public GameObject[] disableDuringBlank;   // e.g. the two "No singal" objects (optional)

    [Header("Videos")]
    public VideoPlayer standbyPlayer;
    public RenderTexture standbyTexture;
    public VideoPlayer videoPlayer;
    public RenderTexture transmissionTexture;

    [Header("Announcement")]
    public AudioSource announcementAudio;
    public AudioClip announcementClip;

    [Header("Timing (seconds)")]
    public float logoDelay = 10f;
    public float standbyAfterAudio = 4f;

    [Header("Red Alert Lights")]
    public GameObject lightsParent;
    public GameObject[] extraAlertLights;
    public Color alertColor = Color.red;
    public float flashInterval = 0.4f;
    public bool flashDuringLogo = false;

    [Header("Room Dimming")]
    [Range(0f, 1f)] public float dimmedLevel = 0.05f;   // 0 = pitch black, 1 = no dimming
    [Tooltip("0 = dim slowly over the whole standby period (audio + extra seconds)")]
    public float standbyDimTime = 0f;
    public float fadeUpTime = 2f;                       // brightening at the end

    [Header("Cube / Mesh Lights (white material)")]
    public GameObject glowParent;
    public Renderer[] glowRenderers;

    private class GlowMat
    {
        public Material mat;
        public List<string> props = new List<string>();
        public List<Color> cols = new List<Color>();
    }

    private Light[] alertLights = new Light[0];
    private Color[] alertOriginalColors = new Color[0];
    private Light[] roomLights = new Light[0];
    private float[] roomIntensities = new float[0];
    private float ambientOriginal;
    private float reflectionOriginal;
    private Coroutine flashRoutine;
    private Coroutine dimRoutine;
    private List<GlowMat> glowMats = new List<GlowMat>();

    private Material[][] sideOriginalMats;
    private bool sideBlanked = false;

    private void Start()
    {
        if (lightsParent != null)
        {
            alertLights = lightsParent.GetComponentsInChildren<Light>();
            alertOriginalColors = new Color[alertLights.Length];
            for (int i = 0; i < alertLights.Length; i++)
                alertOriginalColors[i] = alertLights[i].color;
        }

        Light[] all = FindObjectsByType<Light>(FindObjectsSortMode.None);
        var list = new List<Light>();
        foreach (Light l in all)
        {
            bool isExtra = false;
            foreach (GameObject g in extraAlertLights)
                if (g != null && l.gameObject == g) isExtra = true;
            if (!isExtra) list.Add(l);
        }
        roomLights = list.ToArray();
        roomIntensities = new float[roomLights.Length];
        for (int i = 0; i < roomLights.Length; i++)
            roomIntensities[i] = roomLights[i].intensity;

        ambientOriginal = RenderSettings.ambientIntensity;
        reflectionOriginal = RenderSettings.reflectionIntensity;

        // Remember the side screens' normal materials
        sideOriginalMats = new Material[sideScreens.Length][];
        for (int i = 0; i < sideScreens.Length; i++)
            if (sideScreens[i] != null)
                sideOriginalMats[i] = sideScreens[i].sharedMaterials;

        CollectGlowMaterials();
        SetAlert(false);

        switch (mode)
        {
            case Mode.FullSequence:
                StartCoroutine(PlaySequence());
                break;
            case Mode.PreviewTransmissionScreen:
                StartCoroutine(PreviewVideo(videoMaterial, videoPlayer, transmissionTexture));
                break;
            case Mode.PreviewStandbyScreen:
                StartCoroutine(PreviewVideo(standbyMaterial, standbyPlayer, standbyTexture));
                break;
            case Mode.PreviewLogoScreen:
                ShowMaterial(nasaLogoMaterial);
                break;
            case Mode.PreviewDimmedRoom:
                StartCoroutine(PreviewDim());
                break;
        }
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        SetAlert(false);
        SetDim(0f);
        if (sideBlanked) SetSideScreensBlank(false);
    }

    // ---------- finding the cube lights ----------
    private void CollectGlowMaterials()
    {
        var rends = new HashSet<Renderer>();
        foreach (Renderer r in glowRenderers)
            if (r != null) rends.Add(r);
        if (glowParent != null)
            foreach (Renderer r in glowParent.GetComponentsInChildren<Renderer>(true))
                rends.Add(r);
        if (mainScreen != null) rends.Remove(mainScreen); // never dim the TV
        foreach (Renderer s in sideScreens) rends.Remove(s);

        foreach (Renderer r in rends)
        {
            foreach (Material m in r.materials)
            {
                var g = new GlowMat { mat = m };
                Shader s = m.shader;
                for (int i = 0; i < s.GetPropertyCount(); i++)
                {
                    if (s.GetPropertyType(i) != ShaderPropertyType.Color) continue;
                    string n = s.GetPropertyName(i);
                    g.props.Add(n);
                    g.cols.Add(m.GetColor(n));
                }
                if (g.props.Count == 0)
                    Debug.LogWarning($"[Transmission] '{m.name}' has no colour properties, so it can't be dimmed. Shader: {s.name}");
                glowMats.Add(g);
            }
        }

        if (glowMats.Count == 0)
            Debug.LogWarning("[Transmission] No cube lights found. Fill in Glow Parent or Glow Renderers.");
    }

    // ---------- previews ----------
    private IEnumerator PreviewVideo(Material mat, VideoPlayer vp, RenderTexture rt)
    {
        SetupPlayer(vp, rt, true);
        ShowMaterial(mat);
        vp.Prepare();
        while (!vp.isPrepared)
            yield return null;
        vp.Play();
    }

    private IEnumerator PreviewDim()
    {
        yield return new WaitForSeconds(1f);
        SetSideScreensBlank(true);
        yield return FadeRoom(0f, 1f, 6f);
    }

    // ---------- full sequence ----------
    private IEnumerator PlaySequence()
    {
        float startTime = Time.time;

        // 1. NASA logo (side screens show their normal picture)
        ShowMaterial(nasaLogoMaterial);
        if (flashDuringLogo) StartFlashing();

        SetupPlayer(standbyPlayer, standbyTexture, true);
        SetupPlayer(videoPlayer, transmissionTexture, false);
        standbyPlayer.Prepare();
        videoPlayer.Prepare();
        while (!standbyPlayer.isPrepared || !videoPlayer.isPrepared)
            yield return null;

        float remaining = logoDelay - (Time.time - startTime);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 2. Standby + announcement + red flashing + side screens blank + slow dim
        float standbyTotal = announcementClip.length + standbyAfterAudio;
        float dimDuration = standbyDimTime > 0f ? standbyDimTime : standbyTotal;

        ShowMaterial(standbyMaterial);
        SetSideScreensBlank(true);
        standbyPlayer.Play();
        announcementAudio.clip = announcementClip;
        announcementAudio.Play();
        StartFlashing();
        dimRoutine = StartCoroutine(FadeRoom(0f, 1f, dimDuration));

        // 3. Audio length + extra standby time
        yield return new WaitForSeconds(standbyTotal);
        StopFlashing();
        standbyPlayer.Stop();

        // 4. Transmission video in the dark
        bool finished = false;
        videoPlayer.loopPointReached += _ => finished = true;
        ShowMaterial(videoMaterial);
        videoPlayer.Play();
        while (!finished)
            yield return null;

        // 5. Back to the NASA logo, side screens and lights return
        ShowMaterial(nasaLogoMaterial);
        SetSideScreensBlank(false);
        if (dimRoutine != null) StopCoroutine(dimRoutine);
        yield return FadeRoom(1f, 0f, fadeUpTime);
    }

    // ---------- side screens ----------
    private void SetSideScreensBlank(bool blank)
    {
        sideBlanked = blank;

        foreach (GameObject g in disableDuringBlank)
            if (g != null) g.SetActive(!blank);

        for (int i = 0; i < sideScreens.Length; i++)
        {
            Renderer r = sideScreens[i];
            if (r == null || sideOriginalMats[i] == null) continue;

            Material[] mats = (Material[])sideOriginalMats[i].Clone();
            if (blank && sideScreenMaterialIndex < mats.Length)
                mats[sideScreenMaterialIndex] = blankMaterial;
            r.sharedMaterials = mats;
        }
    }

    // ---------- flashing ----------
    private void StartFlashing()
    {
        if (flashRoutine == null)
            flashRoutine = StartCoroutine(FlashLoop());
    }

    private void StopFlashing()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        SetAlert(false);
    }

    private IEnumerator FlashLoop()
    {
        bool on = false;
        while (true)
        {
            on = !on;
            SetAlert(on);
            yield return new WaitForSeconds(flashInterval);
        }
    }

    private void SetAlert(bool on)
    {
        for (int i = 0; i < alertLights.Length; i++)
        {
            if (alertLights[i] == null) continue;
            alertLights[i].color = on ? alertColor : alertOriginalColors[i];
        }

        foreach (GameObject g in extraAlertLights)
            if (g != null) g.SetActive(on);
    }

    // ---------- dimming ----------
    private IEnumerator FadeRoom(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetDim(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetDim(to);
    }

    // amount: 0 = normal, 1 = fully dimmed
    private void SetDim(float amount)
    {
        float factor = Mathf.Lerp(1f, dimmedLevel, amount);

        for (int i = 0; i < roomLights.Length; i++)
            if (roomLights[i] != null)
                roomLights[i].intensity = roomIntensities[i] * factor;

        RenderSettings.ambientIntensity = ambientOriginal * factor;
        RenderSettings.reflectionIntensity = reflectionOriginal * factor;

        foreach (GlowMat g in glowMats)
        {
            if (g.mat == null) continue;
            for (int i = 0; i < g.props.Count; i++)
            {
                Color c = g.cols[i];
                g.mat.SetColor(g.props[i], new Color(c.r * factor, c.g * factor, c.b * factor, c.a));
            }
        }
    }

    // ---------- helpers ----------
    private void ShowMaterial(Material m)
    {
        Material[] mats = mainScreen.sharedMaterials;
        mats[screenMaterialIndex] = m;
        mainScreen.sharedMaterials = mats;
    }

    private void SetupPlayer(VideoPlayer vp, RenderTexture rt, bool loop)
    {
        vp.playOnAwake = false;
        vp.isLooping = loop;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
    }
}