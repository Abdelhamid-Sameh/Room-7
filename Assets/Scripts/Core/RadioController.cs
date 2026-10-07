using UnityEngine;

/// <summary>
/// The portable radio Nabil picks up in the staff room.
///
/// R plays and pauses (a paused track carries on from the same second), T / Y go to the next /
/// previous track (they never change whether the radio is playing or paused). With more than one
/// track it moves on to the next one by itself when a track ends; with a single track it loops.
/// Playback state lives in GameState so it survives scene changes; the AudioSource lives on the
/// persistent GameSystems object.
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

    private bool isPaused;     // paused part-way through the current track
    private bool wasPlaying;   // used to tell a track that really ended from a stream that has not started yet
    private float lastTime;

    public int TrackCount => playlist != null ? playlist.Count : 0;
    public bool HasAudio => TrackCount > 0;

    private int CurrentIndex => Mathf.Clamp(GameState.RadioTrackIndex, 0, Mathf.Max(0, TrackCount - 1));

    public string TrackName => HasAudio ? playlist.NameAt(CurrentIndex) : "No tape loaded";

    public string TrackPosition => HasAudio ? $"{CurrentIndex + 1} / {TrackCount}" : "";

    /// <summary>True while the radio is paused part-way through a track (R carries on from there).</summary>
    public bool IsPaused => isPaused;

    /// <summary>Seconds played of the current track. Kept while paused, 0 for a track not started yet.</summary>
    public float Elapsed => source != null && source.clip != null ? source.time : 0f;

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

        if (GameInput.RadioPlayPressed) TogglePlayPause();
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

    /// <summary>R - play, or pause (the track carries on from the same second).</summary>
    public void TogglePlayPause()
    {
        GameState.RadioPlaying = !GameState.RadioPlaying;
        Apply();
        GameState.RaiseChanged();
    }

    /// <summary>Kept for older callers - same as TogglePlayPause.</summary>
    public void TogglePower()
    {
        TogglePlayPause();
    }

    /// <summary>T - next song (wraps around). Keeps playing or stays paused, as it was.</summary>
    public void NextTrack()
    {
        int count = Mathf.Max(1, TrackCount);
        GameState.RadioTrackIndex = (CurrentIndex + 1) % count;
        Apply();
        GameState.RaiseChanged();
    }

    /// <summary>Y - previous song (wraps around). Keeps playing or stays paused, as it was.</summary>
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

        AudioClip wanted = HasAudio ? playlist.ClipAt(CurrentIndex) : null;
        if (wanted == null)
        {
            source.Stop();
            source.clip = null;
            isPaused = false;
            wasPlaying = false;
            return;
        }

        // A different track than the one loaded: it starts again from the beginning.
        if (source.clip != wanted)
        {
            source.Stop();
            source.clip = wanted;
            isPaused = false;
            wasPlaying = false;
            lastTime = 0f;
            ApplyVolume();
        }

        if (GameState.RadioPlaying)
        {
            if (!source.isPlaying)
            {
                if (isPaused)
                {
                    source.UnPause();
                    if (!source.isPlaying) source.Play();
                }
                else
                {
                    source.Play();
                }

                isPaused = false;
                wasPlaying = false;
            }
        }
        else if (source.isPlaying)
        {
            source.Pause();
            isPaused = true;
            wasPlaying = false;
        }
    }

    private void ApplyVolume()
    {
        if (source != null) source.volume = volume * AudioLevels.MusicVolume;
    }
}
