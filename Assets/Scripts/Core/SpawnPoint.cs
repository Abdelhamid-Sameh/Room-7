using UnityEngine;

/// <summary>
/// Marks a spot the player can be dropped onto after a scene change.
/// The id must match the spawnId passed to SceneFader.LoadScene.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId = "";

    public string SpawnId => spawnId;

    public void SetSpawnId(string id)
    {
        spawnId = id;
    }
}
