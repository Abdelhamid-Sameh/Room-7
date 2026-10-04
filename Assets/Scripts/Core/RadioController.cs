using UnityEngine;

/// <summary>
/// The portable radio Nabil picks up in the staff room.
///
/// It is a simple on/off cassette player with a next / previous track
/// skip - no stations. Playback state lives in GameState so it survives
/// scene changes; the AudioSource lives on the persistent GameSystems object.
///
/// >>> DROP YOUR AUDIO FILES IN HERE <<<
/// Assign clips to the "Tracks" list on the GameSystems object in the
/// Inspector (optionally name them in "Track Names", same length/order).
/// With an empty list everything still works - the HUD just shows
/// "No tape loaded" instead of a track name.
/// </summary>
public class RadioController : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("The playlist. Leave empty until the audio arrives.")]
    [SerializeField] private AudioClip[] tracks;

    [Tooltip("Optional display names, same order as Tracks. Leave empty to show 'Track N'.")]
    [SerializeField] private string[] trackNames;

    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;

    private AudioSource source;

    public int TrackCount => tracks != null ? tracks.Length : 0;
    public bool HasAudio => TrackCount > 0;

    public string TrackName
    {
        get
        {
            if (TrackCount == 0) return "No tape loaded";
            int index = Mathf.Clamp(GameState.RadioTrackIndex, 0, TrackCount - 1);
            if (trackNames != null && index < trackNames.Length && !string.IsNullOrWhiteSpace(trackNames[index]))
                return trackNames[index];
            return $"Track {index + 1}";
        }
    }

    public string TrackPosition => HasAudio ? $"{GameState.RadioTrackIndex + 1} / {TrackCount}" : "";

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;   // UI-style 2D sound, not positional
        source.volume = volume;

        // Keep whatever track we had before the last scene change.
        Apply();
    }

    private void Update()
    {
        if (!GameState.HasRadio) return;

        if (GameInput.RadioPlayPressed) TogglePower();
        else if (GameInput.RadioNextPressed) NextTrack();
        else if (GameInput.RadioPrevPressed) PreviousTrack();
    }

    /// <summary>R - on / off.</summary>
    public void TogglePower()
    {
        GameState.RadioPlaying = !GameState.RadioPlaying;
        Apply();
        GameState.RaiseChanged();
    }

    /// <summary>T - next song (wraps around). Does not change on/off state.</summary>
    public void NextTrack()
    {
        int count = Mathf.Max(1, TrackCount);
        GameState.RadioTrackIndex = (GameState.RadioTrackIndex + 1) % count;
        Apply();
        GameState.RaiseChanged();
    }

    /// <summary>Y - previous song (wraps around). Does not change on/off state.</summary>
    public void PreviousTrack()
    {
        int count = Mathf.Max(1, TrackCount);
        GameState.RadioTrackIndex = (GameState.RadioTrackIndex - 1 + count) % count;
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

        int index = Mathf.Clamp(GameState.RadioTrackIndex, 0, TrackCount - 1);
        if (source.clip != tracks[index] || !source.isPlaying)
        {
            source.clip = tracks[index];
            source.volume = volume;
            source.Play();
        }
    }
}
