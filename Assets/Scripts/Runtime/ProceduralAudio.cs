using UnityEngine;

namespace VRRocket
{
    /// <summary>Small synthesized clips so the flow has beeps and tones without any external asset.</summary>
    public static class ProceduralAudio
    {
        const int k_SampleRate = 44100;

        /// <summary>A sine beep with a short fade in and out.</summary>
        public static AudioClip Beep(float frequency, float seconds, float gain = 0.6f)
        {
            var n = Mathf.Max(1, (int)(k_SampleRate * seconds));
            var data = new float[n];
            var fade = Mathf.Max(1, (int)(k_SampleRate * 0.008f));
            for (var i = 0; i < n; i++)
            {
                var env = Mathf.Min(1f, Mathf.Min(i, n - 1 - i) / (float)fade);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / k_SampleRate) * gain * env;
            }
            var clip = AudioClip.Create("Beep" + (int)frequency, n, 1, k_SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Two-tone chime (rising) for confirmations.</summary>
        public static AudioClip Chime(float f1, float f2, float seconds, float gain = 0.5f)
        {
            var n = Mathf.Max(1, (int)(k_SampleRate * seconds));
            var data = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)k_SampleRate;
                var env = Mathf.Exp(-3f * t / seconds);
                var f = t < seconds * 0.5f ? f1 : f2;
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * gain * env;
            }
            var clip = AudioClip.Create("Chime", n, 1, k_SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
