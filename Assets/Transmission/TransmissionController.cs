using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class TransmissionController : MonoBehaviour
{
    public enum Mode
    {
        FullSequence,
        PreviewTransmissionScreen,
        PreviewStandbyScreen,
        PreviewLogoScreen
    }

    [Header("Mode (use a Preview mode to line up the screen)")]
    public Mode mode = Mode.FullSequence;

    [Header("Main Screen")]
    public Renderer mainScreen;
    public int screenMaterialIndex = 0;
    public Material nasaLogoMaterial;
    public Material standbyMaterial;
    public Material videoMaterial;

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
    public GameObject lightsParent;          // e.g. Environment/Lights (these turn red)
    public GameObject[] extraAlertLights;    // optional red lights switched on/off
    public Color alertColor = Color.red;
    public float flashInterval = 0.4f;
    public bool flashDuringLogo = false;     // tick to flash from the very start

    [Header("Room Dimming")]
    [Range(0f, 1f)] public float dimmedLevel = 0.05f;  // 0 = pitch black, 1 = no dimming
    public float fadeTime = 2f;

    [Header("Glowing Meshes (ceiling panels, lamps). NOT the TV screen")]
    public Renderer[] glowRenderers;

    private Light[] alertLights = new Light[0];
    private Color[] alertOriginalColors = new Color[0];
    private Light[] roomLights = new Light[0];
    private float[] roomIntensities = new float[0];
    private float ambientOriginal;
    private float reflectionOriginal;
    private Coroutine flashRoutine;

    private List<Material> glowMaterials = new List<Material>();
    private List<Color> glowEmission = new List<Color>();
    private List<Color> glowBase = new List<Color>();

    private void Start()
    {
        if (lightsParent != null)
        {
            alertLights = lightsParent.GetComponentsInChildren<Light>();
            alertOriginalColors = new Color[alertLights.Length];
            for (int i = 0; i < alertLights.Length; i++)
                alertOriginalColors[i] = alertLights[i].color;
        }

        // Every light in the scene (except the extra red ones) gets dimmed for the video
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

        foreach (Renderer r in glowRenderers)
        {
            if (r == null) continue;
            foreach (Material m in r.materials) // instances, so your original assets aren't changed
            {
                glowMaterials.Add(m);
                glowEmission.Add(m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black);
                glowBase.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
            }
        }

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
        }
    }

    private void OnDisable()
    {
        SetAlert(false);
        SetDim(0f);
    }

    // ---------- preview (no lights, no timing, video loops) ----------
    private IEnumerator PreviewVideo(Material mat, VideoPlayer vp, RenderTexture rt)
    {
        SetupPlayer(vp, rt, true);
        ShowMaterial(mat);
        vp.Prepare();
        while (!vp.isPrepared)
            yield return null;
        vp.Play();
    }

    // ---------- full sequence ----------
    private IEnumerator PlaySequence()
    {
        float startTime = Time.time;

        // 1. NASA logo
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

        // 2. Standby video + announcement + red flashing, all together
        ShowMaterial(standbyMaterial);
        standbyPlayer.Play();
        announcementAudio.clip = announcementClip;
        announcementAudio.Play();
        StartFlashing();

        // 3. Keep going for the audio length + extra standby time
        yield return new WaitForSeconds(announcementClip.length + standbyAfterAudio);
        StopFlashing();
        standbyPlayer.Stop();

        // 4. Dim the room and play the transmission
        StartCoroutine(FadeRoom(0f, 1f));
        bool finished = false;
        videoPlayer.loopPointReached += _ => finished = true;
        ShowMaterial(videoMaterial);
        videoPlayer.Play();
        while (!finished)
            yield return null;

        // 5. Back to the NASA logo, lights back up
        ShowMaterial(nasaLogoMaterial);
        yield return FadeRoom(1f, 0f);
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
            alertLights[i].color = on ? alertColor : alertOriginalColors[i];

        foreach (GameObject g in extraAlertLights)
            if (g != null) g.SetActive(on);
    }

    // ---------- dimming ----------
    private IEnumerator FadeRoom(float from, float to)
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            SetDim(Mathf.Lerp(from, to, t / fadeTime));
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

        for (int i = 0; i < glowMaterials.Count; i++)
        {
            Material m = glowMaterials[i];
            if (m == null) continue;
            if (m.HasProperty("_EmissionColor"))
            {
                Color e = glowEmission[i];
                m.SetColor("_EmissionColor", new Color(e.r * factor, e.g * factor, e.b * factor, e.a));
            }
            if (m.HasProperty("_BaseColor"))
            {
                Color b = glowBase[i];
                m.SetColor("_BaseColor", new Color(b.r * factor, b.g * factor, b.b * factor, b.a));
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