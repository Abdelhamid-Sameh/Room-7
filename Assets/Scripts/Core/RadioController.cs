using UnityEngine;

/// <summary>
/// The portable radio Nabil picks up in the staff room.
///
/// A simple on/off player with next / previous track and a playlist - no stations. Playback state
/// lives in GameState so it survives scene changes; the AudioSource lives on the persistent
/// GameSystems object. With more than one track it moves on to the next one by itself when a
/// track ends; with a single track it loops.
///
/// Mix: the radio's own level (Volume) x the player's Master and Music levels (AudioLevels).
/// Playlist: assign clips to "Tracks" on the GameSystems object (and optional display names to
/// "Track Names", same order). With an empty list the HUD just shows "No tape loaded".
/// </summary>
public class RadioController : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("The playlist.")]
    [SerializeField] private AudioClip[] tracks;

    [Tooltip("Optional display names, same order as Tracks. Leave empty to show 'Track N'.")]
    [SerializeField] private string[] trackNames;

    [Tooltip("The radio's own level. The player's Master and Music sliders multiply it.")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.2f;

    private static RadioController instance;
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
        if (instance != null && instance != this)
        {
            // A copy from another scene - the original keeps playing across scene changes.
            enabled = false;
            return;
        }

        instance = this;

        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.spatialBlend = 0f;   // UI-style 2D sound, not positional
        source.loop = TrackCount <= 1;
        ApplyVolume();
    }

    private void Start()
    {
        // Keep whatever state we had before the last scene change.
        Apply();
    }

    private void OnEnable()
    {
        AudioLevels.Changed += ApplyVolume;
    }

    private void OnDisable()
    {
        AudioLevels.Changed -= ApplyVolume;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!GameState.HasRadio) return;

        if (GameInput.RadioPlayPressed) TogglePower();
        else if (GameInput.RadioNextPressed) NextTrack();
        else if (GameInput.RadioPrevPressed) PreviousTrack();

        // A track just ran out: move on to the next one.
        if (GameState.RadioPlaying && TrackCount > 1 && source != null && source.clip != null && !source.isPlaying)
            NextTrack();
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

        source.loop = TrackCount <= 1;

        if (!GameState.RadioPlaying || !HasAudio)
        {
            source.Stop();
            return;
        }

        int index = Mathf.Clamp(GameState.RadioTrackIndex, 0, TrackCount - 1);
        if (source.clip != tracks[index] || !source.isPlaying)
        {
            source.clip = tracks[index];
            ApplyVolume();
            source.Play();
        }
    }

    private void ApplyVolume()
    {
        if (source != null) source.volume = volume * AudioLevels.MusicVolume;
    }
}
