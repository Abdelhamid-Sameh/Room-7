using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Synthesised mechanical sounds for the projector repair (no recordings needed): ratchet ticks, tile clacks, a
/// part seating, sparks, a film snap, film chatter, a success chime, a fail buzz, and two loops (lamp hum, projector
/// clatter). Clips are generated once and cached. One-shots go through a 2D source scaled by AudioLevels.SfxVolume.
/// </summary>
public static class ProjectorSounds
{
    public enum K { Tick, Clack, Seat, Spark, Snap, Chatter, Chime, Buzz, Hum, Motor }

    private const int Rate = 44100;
    private static readonly Dictionary<K, AudioClip> cache = new Dictionary<K, AudioClip>();
    private static AudioSource src;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache.Clear(); src = null; }

    public static void Play(K k, float mix = 1f)
    {
        if (src == null)
        {
            GameObject go = new GameObject("ProjectorSfx");
            Object.DontDestroyOnLoad(go);
            src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
        }
        src.pitch = 1f;
        src.PlayOneShot(Get(k), Mathf.Clamp01(mix * AudioLevels.SfxVolume));
    }

    /// <summary>A looping source for the hum / clatter; the caller owns and destroys it.</summary>
    public static AudioSource Loop(GameObject host, K k, float volume)
    {
        AudioSource s = host.AddComponent<AudioSource>();
        s.clip = Get(k); s.loop = true; s.spatialBlend = 0f;
        s.volume = volume * AudioLevels.SfxVolume;
        s.Play();
        return s;
    }

    public static AudioClip Get(K k)
    {
        AudioClip c;
        if (cache.TryGetValue(k, out c) && c != null) return c;
        c = Make(k);
        cache[k] = c;
        return c;
    }

    private static AudioClip Make(K k)
    {
        System.Random rng = new System.Random((int)k * 7919 + 3);
        float seconds;
        switch (k)
        {
            case K.Tick: seconds = 0.03f; break;
            case K.Clack: seconds = 0.08f; break;
            case K.Seat: seconds = 0.25f; break;
            case K.Spark: seconds = 0.35f; break;
            case K.Snap: seconds = 0.3f; break;
            case K.Chatter: seconds = 0.7f; break;
            case K.Chime: seconds = 0.9f; break;
            case K.Buzz: seconds = 0.35f; break;
            case K.Hum: seconds = 1f; break;
            default: seconds = 1f; break;   // Motor: exactly 24 claps per second so it loops cleanly
        }
        int n = (int)(Rate * seconds);
        float[] d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float v = 0f;
            switch (k)
            {
                case K.Tick:
                    v = noise * Mathf.Exp(-t * 260f) * 0.7f + Mathf.Sin(t * 2f * Mathf.PI * 3200f) * Mathf.Exp(-t * 400f) * 0.4f;
                    break;
                case K.Clack:
                    lp += (noise - lp) * 0.25f;
                    v = lp * Mathf.Exp(-t * 90f) * 1.4f + Mathf.Sin(t * 2f * Mathf.PI * 900f) * Mathf.Exp(-t * 120f) * 0.35f;
                    break;
                case K.Seat:
                    v = Mathf.Sin(t * 2f * Mathf.PI * (140f - t * 120f)) * Mathf.Exp(-t * 18f) * 0.8f + noise * Mathf.Exp(-t * 200f) * 0.5f;
                    break;
                case K.Spark:
                    {
                        float burst = Mathf.PerlinNoise(t * 90f, 0.3f) > 0.55f ? 1f : 0.15f;
                        v = noise * burst * Mathf.Exp(-t * 6f) * 0.8f;
                        break;
                    }
                case K.Snap:
                    v = noise * Mathf.Exp(-t * 60f) + Mathf.Sin(t * 2f * Mathf.PI * 60f) * noise * Mathf.Exp(-t * 9f) * 0.5f;
                    break;
                case K.Chatter:
                    {
                        float ph = (t * 31f) % 1f;
                        v = noise * Mathf.Exp(-ph * 40f) * 0.8f * (1f - t / seconds);
                        break;
                    }
                case K.Chime:
                    v = (Mathf.Sin(t * 2f * Mathf.PI * 660f) + 0.6f * Mathf.Sin(t * 2f * Mathf.PI * 990f) * (t > 0.12f ? 1f : 0f)) * Mathf.Exp(-t * 4f) * 0.45f;
                    break;
                case K.Buzz:
                    v = Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * 110f)) * 0.25f * (1f - t / seconds);
                    break;
                case K.Hum:
                    v = (Mathf.Sin(t * 2f * Mathf.PI * 100f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 200f) * 0.25f + Mathf.Sin(t * 2f * Mathf.PI * 300f) * 0.1f) * 0.6f;
                    break;
                case K.Motor:
                    {
                        float ph = (t * 24f) % 1f;
                        lp += (noise - lp) * 0.2f;
                        v = lp * Mathf.Exp(-ph * 30f) * 1.2f + Mathf.Sin(t * 2f * Mathf.PI * 48f) * 0.15f + noise * 0.03f;
                        break;
                    }
            }
            d[i] = Mathf.Clamp(v, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("Proj_" + k, n, 1, Rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
