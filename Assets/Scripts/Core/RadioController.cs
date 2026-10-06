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
/// Playlist: the "Playlist" asset (tracks + the names shown) - edit it in one place for both scenes.
/// </summary>
public class RadioController : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("The tracks and the names shown for them (Assets/Audio/Music/RadioPlaylist.asset).")]
    [SerializeField] private RadioPlaylist playlist;

    [Tooltip("The radio's own level. The player's Master and Music sliders multiply it.")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.2f;

    private static RadioController instance;
    private AudioSource source;

    // Used to tell a track that really ended from a stream that has not started yet (big streamed
    // files take a moment) or a one-off audio hiccup.
    private bool wasPlaying;
    private float lastTime;

    public int TrackCount => playlist != null ? playlist.Count : 0;
    public bool HasAudio => TrackCount > 0;

    private int CurrentIndex => Mathf.Clamp(GameState.RadioTrackIndex, 0, Mathf.Max(0, TrackCount - 1));

    public string TrackName => HasAudio ? playlist.NameAt(CurrentIndex) : "No tape loaded";

    public string TrackPosition => HasAudio ? $"{CurrentIndex + 1} / {TrackCount}" : "";

    /// <summary>Seconds played of the current track (0 while the radio is off).</summary>
    public float Elapsed => source != null && source.clip != null && source.isPlaying ? source.time : 0f;

    /// <summary>Length in seconds of the current track.</summary>
    public float Duration
    {
        get
        {
            if (!HasAudio) return 0f;
            AudioClip clip = playlist.ClipAt(CurrentIndex);
            return clip != null ? clip.length : 0f;
        }
    }

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
        if (!GameState.HasRadio || PauseMenu.IsPaused) return;

        if (GameInput.RadioPlayPressed) TogglePower();
        else if (GameInput.RadioNextPressed) NextTrack();
        else if (GameInput.RadioPrevPressed) PreviousTrack();

        WatchPlayback();
    }

    /// <summary>
    /// Moves on to the next track when one really finishes. A track only counts as finished if it was
    /// seen playing and stopped near its end; if it stopped any other way (an audio hiccup) it is simply
    /// started again, and a stream that has not begun playing yet is left alone.
    /// </summary>
    private void WatchPlayback()
    {
        if (!GameState.RadioPlaying || source == null || source.clip == null || AudioListener.pause) return;

        if (source.isPlaying)
        {
            wasPlaying = true;
            lastTime = source.time;
            return;
        }

        if (!wasPlaying) return;   // not started yet
        wasPlaying = false;

        bool endedNaturally = lastTime >= source.clip.length - 1.5f;
        if (endedNaturally && TrackCount > 1) NextTrack();
        else Apply();
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
        GameState.RadioTrackIndex = (CurrentIndex + 1) % count;
        Apply();
        GameState.RaiseChanged();
    }

    /// <summary>Y - previous song (wraps around). Does not change on/off state.</summary>
    public void PreviousTrack()
    {
        int count = Mathf.Max(1, TrackCount);
        GameState.RadioTrackIndex = (CurrentIndex - 1 + count) % count;
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

        AudioClip wanted = playlist.ClipAt(CurrentIndex);
        if (wanted == null)
        {
            source.Stop();
            return;
        }

        if (source.clip != wanted || !source.isPlaying)
        {
            source.clip = wanted;
            ApplyVolume();
            wasPlaying = false;
            lastTime = 0f;
            source.Play();
        }
    }

    private void ApplyVolume()
    {
        if (source != null) source.volume = volume * AudioLevels.MusicVolume;
    }
}
