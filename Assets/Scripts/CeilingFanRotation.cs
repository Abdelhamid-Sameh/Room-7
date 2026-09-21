using UnityEngine;

public class CeilingFanRotation : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 180f;

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
