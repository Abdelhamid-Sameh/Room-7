using UnityEngine;

/// <summary>
/// On scene load, drops the player onto the SpawnPoint requested by
/// GameState.LastSpawnId. If no matching SpawnPoint exists the player keeps
/// whatever position the scene author gave it, so every scene still works
/// without spawn points.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerSpawner : MonoBehaviour
{
    private void Awake()
    {
        ApplySpawn();
    }

    private void ApplySpawn()
    {
        string wanted = GameState.LastSpawnId;
        if (string.IsNullOrEmpty(wanted)) return;

        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null && points[i].SpawnId == wanted)
            {
                Teleport(points[i].transform);
                return;
            }
        }

        Debug.LogWarning($"[PlayerSpawner] No SpawnPoint with id '{wanted}' in this scene.");
    }

    private void Teleport(Transform target)
    {
        CharacterController controller = GetComponent<CharacterController>();

        // CharacterController ignores transform changes while enabled.
        if (controller != null) controller.enabled = false;
        transform.SetPositionAndRotation(target.position, target.rotation);
        if (controller != null) controller.enabled = true;
    }
}
