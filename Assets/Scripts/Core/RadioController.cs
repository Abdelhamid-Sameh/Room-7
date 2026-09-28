using UnityEngine;

/// <summary>
/// The portable radio Nabil picks up in the staff room.
///
/// Playback state lives in GameState so it survives scene changes; the
/// AudioSource lives on the persistent GameSystems object.
///
/// >>> DROP YOUR AUDIO FILES IN HERE <<<
/// Assign clips to the "Tracks" list on the GameSystems object in the
/// Inspector. With an empty list everything still works - the HUD just shows
/// "no signal" instead of a station name.
/// </summary>
public class RadioController : MonoBehaviour
{
    [Header("Stations (names shown in the HUD)")]
    [SerializeField] private string[] stationNames =
    {
        "Radio Masr 92.7",
        "Nogoum FM 100.6",
        "Alexandria FM 88.2"
    };

    [Header("Audio")]
    [Tooltip("One clip per station. Leave empty until the audio arrives.")]
    [SerializeField] private AudioClip[] tracks;

    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;

    private AudioSource source;

    public int StationCount => stationNames != null ? stationNames.Length : 0;
    public int TrackCount => tracks != null ? tracks.Length : 0;
    public bool HasAudio => TrackCount > 0;

    public string StationName
    {
        get
        {
            if (StationCount == 0) return "Static";
            int index = Mathf.Clamp(GameState.RadioStation, 0, StationCount - 1);
            return stationNames[index];
        }
    }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;   // UI-style 2D sound, not positional
        source.volume = volume;

        // Keep whatever station we had before the last scene change.
        Apply();
    }

    private void Update()
    {
        if (!GameState.HasRadio) return;

        if (GameInput.RadioPlayPressed) TogglePlay();
        else if (GameInput.RadioNextPressed) NextStation();
    }

    /// <summary>R - start / stop.</summary>
    public void TogglePlay()
    {
        GameState.RadioPlaying = !GameState.RadioPlaying;
        Apply();
        GameState.RaiseChanged();
    }

    /// <summary>T - next station (wraps around, and starts playback if stopped).</summary>
    public void NextStation()
    {
        int count = Mathf.Max(1, StationCount);
        GameState.RadioStation = (GameState.RadioStation + 1) % count;
        GameState.RadioPlaying = true;
        Apply();
        GameState.RaiseChanged();
    }

    /// <summary>Push GameState into the AudioSource.</summary>
    public void Apply()
    {
        if (source == null) return;

        if (!GameState.RadioPlaying || !HasAudio)
        {
            source.Stop();
            return;
        }

        int index = Mathf.Clamp(GameState.RadioStation, 0, TrackCount - 1);
        if (source.clip != tracks[index] || !source.isPlaying)
        {
            source.clip = tracks[index];
            source.volume = volume;
            source.Play();
        }
    }
}
