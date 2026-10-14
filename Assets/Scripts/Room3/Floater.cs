using UnityEngine;

/// <summary>Bobs and slowly turns, so an object seems to hang in the air (the twisted room).</summary>
public class Floater : MonoBehaviour
{
    [SerializeField] private float bob = 0.08f;
    [SerializeField] private float speed = 0.8f;
    [SerializeField] private float spin = 20f;

    private Vector3 start;
    private float phase;

    private void Start()
    {
        start = transform.position;
        phase = Random.value * 6.2831f;
    }

    private void Update()
    {
        transform.position = start + Vector3.up * (Mathf.Sin(Time.time * speed + phase) * bob);
        transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
    }
}
