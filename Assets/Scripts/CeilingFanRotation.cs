using UnityEngine;

/// <summary>Spins a ceiling fan. The fan is electric: it winds down when the power goes out and speeds up when it returns.</summary>
public class CeilingFanRotation : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 180f;

    private float current = -1f;

    private void Update()
    {
        if (current < 0f) current = GameState.PowerOut ? 0f : rotationSpeed;
        current = Mathf.MoveTowards(current, GameState.PowerOut ? 0f : rotationSpeed, rotationSpeed * 0.6f * Time.deltaTime);
        transform.Rotate(Vector3.up, current * Time.deltaTime, Space.Self);
    }
}
