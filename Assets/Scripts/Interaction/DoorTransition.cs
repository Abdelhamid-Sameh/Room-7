using UnityEngine;

/// <summary>
/// A door that hands the player to another scene instead of swinging open.
///
/// The screen fades to black, the target scene loads, the player is dropped on
/// the matching SpawnPoint, and the screen fades back in.
///
/// With 'Gated By Room Access' on, the door is locked (and says so) until RoomAccess opens the
/// target room.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorTransition : Interactable
{
    [Header("Destination")]
    [SerializeField] private string targetScene = "";
    [SerializeField] private string spawnId = "";

    [Header("Label")]
    [TextArea] [SerializeField] private string displayName = "Enter";

    [Header("Access")]
    [Tooltip("Locked until RoomAccess.IsOpen(target scene) says otherwise.")]
    [SerializeField] private bool gatedByRoomAccess = false;

    [Tooltip("Shown instead of the label while the door is locked.")]
    [SerializeField] private string lockedLabel = "Locked";

    public string TargetScene => targetScene;
    public string SpawnId => spawnId;

    public bool IsLocked => gatedByRoomAccess && !RoomAccess.IsOpen(targetScene);

    public override string Prompt =>
        IsLocked ? lockedLabel : (string.IsNullOrWhiteSpace(displayName) ? "Enter" : displayName);

    public override string KeyHint => IsLocked ? "" : "E";

    public override void Interact(PlayerInteraction interactor)
    {
        if (IsLocked) return;

        if (!GameSystems.HasInstance)
        {
            Debug.LogError("[DoorTransition] No GameSystems in the scene - cannot fade.");
            return;
        }

        GameSystems.Instance.Fader.LoadScene(targetScene, spawnId);
    }

    public void SetDestination(string scene, string spawn, string label, bool gated = false)
    {
        targetScene = scene;
        spawnId = spawn;
        gatedByRoomAccess = gated;
        if (!string.IsNullOrWhiteSpace(label)) displayName = label;
    }
}
