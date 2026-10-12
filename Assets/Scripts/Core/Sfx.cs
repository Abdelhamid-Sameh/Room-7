using System;
using System.Collections.Generic;
using UnityEngine;

public enum SfxKind { Click, Tidy, Paper, Pickup, Switch, LockerOpen, LockerClose, SwitchOff, Wipe, Ping, Send, Tab }

/// <summary>
/// Interaction sound effects. A kind plays the recording at Assets/Resources/Sfx/&lt;Name&gt; when one exists
/// (Paper, Pickup, Switch, Ping, Locker_Open, Locker_Close ...); kinds without a file (Click, Tidy, Send, Tab)
/// use a simple synthesised sound. Played on a 2D source that keeps working while paused, scaled by AudioLevels.Sfx.
/// </summary>
public static class Sfx
{
    private const int Rate = 44100;
    private static readonly Dictionary<SfxKind, AudioClip> cache = new Dictionary<SfxKind, AudioClip>();
    private static AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache.Clear(); source = null; wipeSource = null; }

    public static void Play(SfxKind kind, float mix = 1f)
    {
        AudioClip clip = Get(kind);
        if (clip == null) return;

        if (source == null)
        {
            GameObject go = new GameObject("SfxSource");
            UnityEngine.Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }
        source.PlayOneShot(clip, Mathf.Clamp01(mix * AudioLevels.SfxVolume));
    }

    private static AudioSource wipeSource;

    /// <summary>Looping wipe sound while the player holds interact on something that needs cleaning.</summary>
    public static void SetWipe(bool on)
    {
        if (!on) { if (wipeSource != null && wipeSource.isPlaying) wipeSource.Stop(); return; }
        if (wipeSource != null && wipeSource.isPlaying) return;

        AudioClip clip = Get(SfxKind.Wipe);
        if (clip == null) return;
        if (wipeSource == null)
        {
            GameObject go = new GameObject("WipeSource");
            UnityEngine.Object.DontDestroyOnLoad(go);
            wipeSource = go.AddComponent<AudioSource>();
            wipeSource.spatialBlend = 0f;
            wipeSource.loop = true;
        }
        wipeSource.clip = clip;
        wipeSource.volume = Mathf.Clamp01(AudioLevels.SfxVolume);
        wipeSource.Play();
    }

    /// <summary>The sound that goes with a successful interaction (call after Interact).</summary>
    public static void ForInteractable(Interactable i)
    {
        SfxKind k = SfxKind.Click;
        if (i is TidyItem) k = SfxKind.Tidy;
        else if (i is DocumentItem) k = SfxKind.Paper;
        else if (i is PickupItem) k = SfxKind.Pickup;
        else if (i is LightSwitch) k = i.Prompt.Contains("off") ? SfxKind.Switch : SfxKind.SwitchOff;   // prompt says what the NEXT press does
        else if (i is Locker) k = ((Locker)i).IsOpen ? SfxKind.LockerOpen : SfxKind.LockerClose;
        Play(k);
    }

    private static AudioClip Get(SfxKind kind)
    {
        AudioClip clip;
        if (cache.TryGetValue(kind, out clip)) return clip;

        string file = kind == SfxKind.LockerOpen ? "Locker_Open" : kind == SfxKind.LockerClose ? "Locker_Close" : kind == SfxKind.SwitchOff ? "Switch" : kind.ToString();
        clip = Resources.Load<AudioClip>("Sfx/" + file);
        if (clip == null) clip = Synth(kind);
        else clip = Trim(kind, clip);
        cache[kind] = clip;
        return clip;
    }

    // ---- fallbacks for kinds that have no recording yet ----

    private static AudioClip Synth(SfxKind kind)
    {
        switch (kind)
        {
            case SfxKind.Click: return Make("sfx_click", 0.06f, t => Sin(1400f, t) * Mathf.Exp(-t * 70f) * 0.5f);
            case SfxKind.Tidy:  return Make("sfx_tidy", 0.35f, t => (Sin(1568f, t) * Mathf.Exp(-t * 14f) + 0.3f * Sin(3136f, t) * Mathf.Exp(-t * 22f)) * 0.22f);
            case SfxKind.Tab:   return Make("sfx_tab", 0.05f, t => Sin(1000f, t) * Mathf.Exp(-t * 90f) * 0.4f);
            case SfxKind.Send:  return Make("sfx_send", 0.2f, t => Mathf.Sin(2f * Mathf.PI * (500f * t + 1250f * t * t)) * Mathf.Sin(Mathf.PI * t / 0.2f) * 0.4f);
            default: return null;
        }
    }

    private static float Sin(float hz, float t) { return Mathf.Sin(2f * Mathf.PI * hz * t); }

    // Some recordings hold several takes (locker open has a latch click and the swing, the switch has on AND off).
    // kind -> start seconds, length seconds, loudness the slice is scaled to (0 = leave as recorded).
    private static AudioClip Trim(SfxKind kind, AudioClip src)
    {
        float start, len, peak = 0f;
        switch (kind)
        {
            case SfxKind.LockerOpen: start = 0.76f; len = 0.65f; break;
            case SfxKind.Switch:     start = 0.10f; len = 0.20f; break;
            case SfxKind.SwitchOff:  start = 1.40f; len = 0.20f; break;
            case SfxKind.Wipe:       start = 0.15f; len = 0.55f; peak = 0.4f; break;
            default: return src;
        }

        int ch = src.channels;
        int from = Mathf.Min(src.samples - 1, Mathf.RoundToInt(start * src.frequency));
        int n = Mathf.Min(src.samples - from, Mathf.RoundToInt(len * src.frequency));
        float[] all = new float[src.samples * ch];
        src.GetData(all, 0);
        float[] data = new float[n * ch];
        Array.Copy(all, from * ch, data, 0, n * ch);

        float max = 0f;
        foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
        float gain = (peak > 0f && max > 0f) ? peak / max : 1f;
        int fade = Mathf.Min(n, src.frequency / 30);   // 30 ms fade-out so the cut does not click
        for (int i = 0; i < n; i++)
        {
            float g = gain * (i > n - fade ? (n - i) / (float)fade : 1f);
            for (int c = 0; c < ch; c++) data[i * ch + c] *= g;
        }

        AudioClip clip = AudioClip.Create(src.name + "_" + kind, n, ch, src.frequency, false);
        clip.SetData(data, 0);
        return clip;
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
