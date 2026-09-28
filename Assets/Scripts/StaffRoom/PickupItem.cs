using UnityEngine;

/// <summary>Every pick-up / look-at object in the staff room.</summary>
public enum PickupKind
{
    /// <summary>The box of cleaning supplies - Nabil cannot clean without it.</summary>
    CleaningKit,

    /// <summary>The radio. Taking it reveals the radio widget in the HUD.</summary>
    Radio,

    /// <summary>Nabil's own uniform - taking it means he puts it on.</summary>
    WearUniform,

    /// <summary>Malak's uniform. Looked at, never taken - it is a clue.</summary>
    InspectMalakUniform
}

/// <summary>
/// One-shot interaction that writes a flag into GameState and updates the HUD.
/// Add a new PickupKind, handle it in Take() and it appears in the inventory
/// read-out automatically.
/// </summary>
public class PickupItem : Interactable
{
    [SerializeField] private PickupKind kind = PickupKind.CleaningKit;

    [Tooltip("Optional label. Leave empty to use the default for this kind.")]
    [SerializeField] private string promptOverride = "";

    [Tooltip("Object switched off once taken. Defaults to this GameObject.")]
    [SerializeField] private GameObject hideOnTaken;

    [Tooltip("Optional object switched on once taken (e.g. the worn uniform).")]
    [SerializeField] private GameObject activateOnTaken;

    [Header("Audio (optional)")]
    [SerializeField] private AudioClip pickupClip;

    public PickupKind Kind => kind;

    public override string Prompt
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(promptOverride)) return promptOverride;

            switch (kind)
            {
                case PickupKind.CleaningKit: return "Take cleaning supplies";
                case PickupKind.Radio: return "Take radio";
                case PickupKind.WearUniform: return "Put on uniform";
                case PickupKind.InspectMalakUniform: return "Look at Malak's uniform";
                default: return "Take";
            }
        }
    }

    public override bool CanInteract => !AlreadyHandled;

    private bool AlreadyHandled
    {
        get
        {
            switch (kind)
            {
                case PickupKind.CleaningKit: return GameState.HasCleaningKit;
                case PickupKind.Radio: return GameState.HasRadio;
                case PickupKind.WearUniform: return GameState.UniformTaken;
                default: return false;   // clues stay readable
            }
        }
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (AlreadyHandled) return;

        switch (kind)
        {
            case PickupKind.CleaningKit:
                GameState.HasCleaningKit = true;
                Disappear();
                break;

            case PickupKind.Radio:
                GameState.HasRadio = true;
                Disappear();
                break;

            case PickupKind.WearUniform:
                GameState.UniformTaken = true;
                GameState.UniformWorn = true;
                if (activateOnTaken != null) activateOnTaken.SetActive(true);
                Disappear();
                break;

            case PickupKind.InspectMalakUniform:
                // The uniform stays where it is - noticing it is the point.
                GameState.MalakUniformInspected = true;
                break;
        }

        if (pickupClip != null) AudioSource.PlayClipAtPoint(pickupClip, transform.position);

        GameState.RaiseChanged();
    }

    private void Disappear()
    {
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        GameObject target = hideOnTaken != null ? hideOnTaken : gameObject;
        target.SetActive(false);
    }
}
