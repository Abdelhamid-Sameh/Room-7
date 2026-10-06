using UnityEngine;

/// <summary>
/// Footstep sounds for the player, driven by how far the player actually walks: one step per
/// 'Stride Length' metres, so running steps faster automatically, standing still is silent, and a
/// teleport (spawning in a room) makes no sound.
///
/// Drop short step clips (about 0.2-0.4 s each) into 'Clips'; one is picked at random each step,
/// never the same twice in a row. Volume follows the player's Master and Sfx levels (AudioLevels).
/// </summary>
[DisallowMultipleComponent]
public class PlayerFootsteps : MonoBehaviour
{
    [Tooltip("Short footstep clips. One is picked at random for every step.")]
    [SerializeField] private AudioClip[] clips;

    [Tooltip("The footsteps' own level. The player's Master and Sfx sliders multiply it.")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;

    [Tooltip("Metres walked between two steps.")]
    [SerializeField] private float strideLength = 0.85f;

    [Tooltip("Slower than this (metres per second) counts as standing still.")]
    [SerializeField] private float minSpeed = 0.4f;

    [Tooltip("Random pitch change per step, so the steps do not sound identical.")]
    [SerializeField, Range(0f, 0.2f)] private float pitchVariation = 0.06f;

    private AudioSource source;
    private Vector3 lastPosition;
    private bool hasLast;
    private float distance;
    private int lastIndex = -1;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;   // plain 2D - the player's own steps
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // paused

        Vector3 position = transform.position;
        if (!hasLast)
        {
            lastPosition = position;
            hasLast = true;
            return;
        }

        Vector3 delta = position - lastPosition;
        delta.y = 0f;
        lastPosition = position;

        float moved = delta.magnitude;
        if (moved > 1.5f)
        {
            // Teleported (spawned into a room) - not a step.
            distance = 0f;
            return;
        }

        if (moved / dt < minSpeed)
        {
            // Standing still: the first step after starting to walk comes after half a stride.
            distance = strideLength * 0.5f;
            return;
        }

        distance += moved;
        if (distance >= strideLength)
        {
            distance -= strideLength;
            PlayStep();
        }
    }

    private void PlayStep()
    {
        if (clips == null || clips.Length == 0) return;

        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == lastIndex) index = (index + 1) % clips.Length;
        lastIndex = index;

        AudioClip clip = clips[index];
        if (clip == null) return;

        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, volume * AudioLevels.SfxVolume);
    }
}
