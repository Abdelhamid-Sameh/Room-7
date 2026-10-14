using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Room D. While the player is inside, the Cinemachine camera rolls (dutch tilt) and breathes (field of view);
/// the room's lamp slowly cycles colour and flickers whether or not anyone is in there.
/// </summary>
public class TwistedRoom : MonoBehaviour
{
    [SerializeField] private Vector3 center;
    [SerializeField] private Vector3 size = new Vector3(4.8f, 3f, 4.8f);
    [SerializeField] private Light lamp;
    [SerializeField] private float maxDutch = 9f;
    [SerializeField] private float fovBreath = 3f;

    private CinemachineCamera cam;
    private CharacterController player;
    private float amount, baseFov, baseIntensity;

    private void Start()
    {
        if (lamp != null) baseIntensity = lamp.intensity;
    }

    private void Update()
    {
        if (player == null) player = FindFirstObjectByType<CharacterController>();
        if (cam == null)
            foreach (CinemachineCamera c in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                if (c.Follow != null) { cam = c; baseFov = c.Lens.FieldOfView; break; }

        bool inside = player != null && new Bounds(center, size).Contains(player.transform.position);
        amount = Mathf.MoveTowards(amount, inside ? 1f : 0f, Time.deltaTime * 0.5f);

        if (cam != null)
        {
            LensSettings l = cam.Lens;
            l.Dutch = Mathf.Sin(Time.time * 0.5f) * maxDutch * amount;
            l.FieldOfView = baseFov + Mathf.Sin(Time.time * 1.1f) * fovBreath * amount;
            cam.Lens = l;
        }

        if (lamp != null)
        {
            lamp.color = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.07f, 1f), 0.55f, 1f);
            lamp.intensity = baseIntensity * (0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 3f, 0.5f));
        }
    }
}
