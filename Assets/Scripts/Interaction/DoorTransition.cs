using UnityEngine;

/// <summary>
/// A door that hands the player to another scene instead of swinging open.
///
/// The screen fades to black, the target scene loads, the player is dropped on
/// the matching SpawnPoint, and the screen fades back in.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorTransition : Interactable
{
    [Header("Destination")]
    [SerializeField] private string targetScene = "";
    [SerializeField] private string spawnId = "";

    [Header("Label")]
    [TextArea] [SerializeField] private string displayName = "Enter";

    public string TargetScene => targetScene;
    public string SpawnId => spawnId;

    public override string Prompt =>
        string.IsNullOrWhiteSpace(displayName) ? "Enter" : displayName;

    public override void Interact(PlayerInteraction interactor)
    {
        if (!GameSystems.HasInstance)
        {
            Debug.LogError("[DoorTransition] No GameSystems in the scene - cannot fade.");
            return;
        }

        GameSystems.Instance.Fader.LoadScene(targetScene, spawnId);
    }

    public void SetDestination(string scene, string spawn, string label)
    {
        targetScene = scene;
        spawnId = spawn;
        if (!string.IsNullOrWhiteSpace(label)) displayName = label;
    }
}
