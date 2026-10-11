using System;
using System.Collections.Generic;
using UnityEngine;

public enum SfxKind { Click, Tidy, Paper, Pickup, Switch, Ping, Send, Tab }

/// <summary>
/// Interaction sound effects. The clips are synthesised in code the first time they are needed, so there
/// are no audio files to manage - swap any of them for a real recording by assigning it in Override.
/// Played on a 2D source that keeps working while the game is paused (menu clicks), scaled by AudioLevels.Sfx.
/// </summary>
public static class Sfx
{
    private const int Rate = 44100;
    private static readonly Dictionary<SfxKind, AudioClip> cache = new Dictionary<SfxKind, AudioClip>();

    /// <summary>Drop a real AudioClip in here to replace a synthesised sound.</summary>
    public static readonly Dictionary<SfxKind, AudioClip> Override = new Dictionary<SfxKind, AudioClip>();

    private static AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache.Clear(); source = null; }

    public static void Play(SfxKind kind, float mix = 1f)
    {
        if (source == null)
        {
            GameObject go = new GameObject("SfxSource");
            UnityEngine.Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }
        source.PlayOneShot(Get(kind), Mathf.Clamp01(mix * AudioLevels.SfxVolume));
    }

    /// <summary>The sound that goes with a successful interaction.</summary>
    public static void ForInteractable(Interactable i)
    {
        SfxKind k = SfxKind.Click;
        if (i is TidyItem) k = SfxKind.Tidy;
        else if (i is DocumentItem) k = SfxKind.Paper;
        else if (i is PickupItem) k = SfxKind.Pickup;
        else if (i is LightSwitch) k = SfxKind.Switch;
        Play(k);
    }

    private static AudioClip Get(SfxKind kind)
    {
        AudioClip clip;
        if (Override.TryGetValue(kind, out clip) && clip != null) return clip;
        if (cache.TryGetValue(kind, out clip) && clip != null) return clip;

        switch (kind)
        {
            case SfxKind.Click:  clip = Make("sfx_click", 0.06f, t => Sin(1400f, t) * Mathf.Exp(-t * 70f) * 0.5f); break;
            case SfxKind.Tab:    clip = Make("sfx_tab", 0.05f, t => Sin(1000f, t) * Mathf.Exp(-t * 90f) * 0.4f); break;
            case SfxKind.Switch: clip = Make("sfx_switch", 0.1f, t => (Sin(2200f, t) * Mathf.Exp(-t * 120f) + Sin(160f, t) * Mathf.Exp(-t * 50f)) * 0.5f); break;
            case SfxKind.Tidy:   clip = Make("sfx_tidy", 0.3f, MakeThump()); break;
            case SfxKind.Paper:  clip = Make("sfx_paper", 0.35f, MakeRustle()); break;
            case SfxKind.Pickup: clip = Make("sfx_pickup", 0.26f, t => Sin(t < 0.12f ? 660f : 990f, t) * Mathf.Exp(-(t % 0.12f) * 18f) * 0.45f); break;
            case SfxKind.Ping:   clip = Make("sfx_ping", 0.4f, t => (Sin(880f, t) * Mathf.Exp(-t * 9f) + (t > 0.12f ? Sin(1320f, t) * Mathf.Exp(-(t - 0.12f) * 9f) : 0f)) * 0.4f); break;
            default:             clip = Make("sfx_send", 0.2f, t => Mathf.Sin(2f * Mathf.PI * (500f * t + 1250f * t * t)) * Mathf.Sin(Mathf.PI * t / 0.2f) * 0.4f); break;
        }

        cache[kind] = clip;
        return clip;
    }

    private static float Sin(float hz, float t) { return Mathf.Sin(2f * Mathf.PI * hz * t); }

    private static Func<float, float> MakeThump()
    {
        System.Random rng = new System.Random(7);
        return t => Sin(140f, t) * Mathf.Exp(-t * 18f) * 0.8f + ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 40f) * 0.15f;
    }

    private static Func<float, float> MakeRustle()
    {
        System.Random rng = new System.Random(3);
        float prev = 0f;
        return t =>
        {
            float n = (float)rng.NextDouble() * 2f - 1f;
            float s = (n - prev) * 0.5f;   // crude high-pass: hiss, no rumble
            prev = n;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.35f)) * (0.5f + 0.5f * (float)rng.NextDouble());
            return s * env * 0.5f;
        };
    }

    private static AudioClip Make(string name, float seconds, Func<float, float> sample)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        float[] data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
        AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
