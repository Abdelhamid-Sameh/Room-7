using UnityEngine;

/// <summary>A tired ceiling bulb: a slow wobble plus the odd drop-out. The glow of its fixture follows the light.</summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [SerializeField] private float baseIntensity = 1.2f;
    [Range(0f, 1f)] [SerializeField] private float wobble = 0.15f;
    [Tooltip("Average seconds between drop-outs. 0 = never.")]
    [SerializeField] private float dropEvery = 5f;
    [SerializeField] private Renderer bulb;
    [SerializeField] private Color bulbGlow = new Color(0.9f, 1f, 0.75f);

    private Light lamp;
    private MaterialPropertyBlock block;
    private float seed, nextDrop, dropUntil;

    private void Awake()
    {
        lamp = GetComponent<Light>();
        block = new MaterialPropertyBlock();
        seed = Random.value * 100f;
        ScheduleDrop();
    }

    private void ScheduleDrop()
    {
        nextDrop = dropEvery > 0f ? Time.time + Random.Range(dropEvery * 0.4f, dropEvery * 1.6f) : float.MaxValue;
    }

    private void Update()
    {
        float level = 1f - wobble + wobble * Mathf.PerlinNoise(seed, Time.time * 7f);
        if (Time.time >= nextDrop) { dropUntil = Time.time + Random.Range(0.05f, 0.3f); ScheduleDrop(); }
        if (Time.time < dropUntil) level *= Random.Range(0f, 0.25f);

        lamp.intensity = baseIntensity * level;
        if (bulb != null)
        {
            block.SetColor("_EmissionColor", bulbGlow * (level * 2f));
            bulb.SetPropertyBlock(block);
        }
    }
}
