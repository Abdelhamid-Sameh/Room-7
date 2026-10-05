using System;
using UnityEngine;

/// <summary>
/// The one place that holds every volume the player will eventually control from the audio menu.
/// Values are 0..1 and are saved in PlayerPrefs. Anything that makes sound multiplies its own
/// mix level by the matching property here:
///
///     final volume = (the source's own mix level) x Master x (Ambient | Music | Sfx)
///
/// To build the settings menu later, bind each slider to one of the four properties below
/// (for example AudioLevels.Music = slider.value). Everything listening to Changed updates live.
/// </summary>
public static class AudioLevels
{
    private const string MasterKey = "audio.master";
    private const string AmbientKey = "audio.ambient";
    private const string MusicKey = "audio.music";
    private const string SfxKey = "audio.sfx";

    private static float master = 1f;
    private static float ambient = 1f;
    private static float music = 1f;
    private static float sfx = 1f;
    private static bool loaded;

    /// <summary>Raised whenever any level changes.</summary>
    public static event Action Changed;

    // The four sliders (0..1).
    public static float Master  { get { Load(); return master;  } set { Set(ref master,  value, MasterKey);  } }
    public static float Ambient { get { Load(); return ambient; } set { Set(ref ambient, value, AmbientKey); } }
    public static float Music   { get { Load(); return music;   } set { Set(ref music,   value, MusicKey);   } }
    public static float Sfx     { get { Load(); return sfx;     } set { Set(ref sfx,     value, SfxKey);     } }

    // What the sound sources actually multiply by (master already included).
    public static float AmbientVolume { get { Load(); return master * ambient; } }
    public static float MusicVolume   { get { Load(); return master * music;   } }
    public static float SfxVolume     { get { Load(); return master * sfx;     } }

    /// <summary>One-shot sound effect at a point in the world, scaled by the Master and Sfx levels.</summary>
    public static void PlaySfx(AudioClip clip, Vector3 position, float mixLevel = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(mixLevel * SfxVolume));
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        master  = PlayerPrefs.GetFloat(MasterKey, 1f);
        ambient = PlayerPrefs.GetFloat(AmbientKey, 1f);
        music   = PlayerPrefs.GetFloat(MusicKey, 1f);
        sfx     = PlayerPrefs.GetFloat(SfxKey, 1f);
    }

    private static void Set(ref float field, float value, string key)
    {
        Load();
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(field, value)) return;

        field = value;
        PlayerPrefs.SetFloat(key, value);
        Changed?.Invoke();
    }
}
