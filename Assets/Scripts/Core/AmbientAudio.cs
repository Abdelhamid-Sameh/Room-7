using UnityEngine;

/// <summary>
/// Looping background noise that keeps playing across scene changes.
///
/// Sits on the "GameSystems" object (which already survives scene loads) and plays through its
/// own child AudioSource, so it never interferes with the radio's AudioSource. Every scene has a
/// GameSystems copy; only the first one to wake up does anything, later copies switch themselves
/// off - so the noise never restarts or doubles up when you walk between rooms.
///
/// Mix: this sound's own level (Volume) x the player's Master and Ambient levels (AudioLevels).
/// To change the sound, swap the Clip in the Inspector (on GameSystems, in each scene).
/// </summary>
[DisallowMultipleComponent]
public class AmbientAudio : MonoBehaviour
{
    [Tooltip("The looping background noise.")]
    [SerializeField] private AudioClip clip;

    [Tooltip("This sound's own level. The player's Master and Ambient sliders multiply it.")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.12f;

    [Header("Distance feel")]
    [Tooltip("Cuts the highs so the sound seems far away, heard from the 5th floor through a window.")]
    [SerializeField] private bool muffle = true;

    [Tooltip("Lower = more muffled / further away. 22000 = no effect.")]
    [SerializeField, Range(300f, 22000f)] private float muffleCutoffHz = 1600f;

    private static AmbientAudio instance;
    private AudioSource source;
    private AudioLowPassFilter lowPass;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // A copy from another scene - the original is already playing.
            enabled = false;
            return;
        }

        instance = this;

        // Own child object so the radio's GetComponent<AudioSource>() is untouched.
        GameObject child = new GameObject("AmbientSource");
        child.transform.SetParent(transform, false);

        source = child.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // plain 2D sound

        lowPass = child.AddComponent<AudioLowPassFilter>();
        ApplyLevels();
    }

    // Playback starts in Start, not Awake: GameSystems moves itself to the persistent scene
    // during Awake, and a source started before that move can end up silent.
    private void Start()
    {
        if (source == null) return;

        if (clip == null)
        {
            Debug.LogWarning("[AmbientAudio] No clip assigned - nothing to play.");
            return;
        }

        source.Stop();
        source.Play();
    }

    private void OnEnable()
    {
        AudioLevels.Changed += ApplyLevels;
        ApplyLevels();
    }

    private void OnDisable()
    {
        AudioLevels.Changed -= ApplyLevels;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnValidate()
    {
        ApplyLevels();
    }

    private void ApplyLevels()
    {
        if (source == null) return;

        source.volume = volume * AudioLevels.AmbientVolume;

        if (lowPass != null)
        {
            lowPass.enabled = muffle;
            lowPass.cutoffFrequency = muffleCutoffHz;
        }
    }
}
