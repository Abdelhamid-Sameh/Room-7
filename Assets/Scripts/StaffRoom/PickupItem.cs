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

    /// <summary>
    /// Malak's uniform. A clue, not an item - interacting picks it up to the
    /// centre of the screen (see CarrySystem) and interacting again puts it
    /// back exactly where it was.
    /// </summary>
    InspectMalakUniform
}

/// <summary>
/// One-shot interaction that writes a flag into GameState and updates the HUD.
/// Add a new PickupKind, handle it in Take() and it appears in the inventory
/// read-out automatically.
///
/// PickupKind.InspectMalakUniform is the odd one out - instead of vanishing
/// into the inventory it implements ICarryable and toggles via CarrySystem.
/// </summary>
public class PickupItem : Interactable, ICarryable
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
            if (kind == PickupKind.InspectMalakUniform)
                return IsHeld ? "Put it back" : (string.IsNullOrWhiteSpace(promptOverride) ? "Look at Malak's uniform" : promptOverride);

            if (!string.IsNullOrWhiteSpace(promptOverride)) return promptOverride;

            switch (kind)
            {
                case PickupKind.CleaningKit: return "Take cleaning supplies";
                case PickupKind.Radio: return "Take radio";
                case PickupKind.WearUniform: return "Put on uniform";
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
                default: return false;   // clues stay interactable (toggle hold/put-down)
            }
        }
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (kind == PickupKind.InspectMalakUniform)
        {
            GameState.MalakUniformInspected = true;
            if (CarrySystem.Instance != null) CarrySystem.Instance.Toggle(this);
            GameState.RaiseChanged();
            return;
        }

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

    // ------------------------------------------------------------ ICarryable --

    public bool IsHeld { get; private set; }

    private Transform originalParent;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Collider[] carryColliders;

    public void PickUp(Transform holdPoint)
    {
        originalParent = transform.parent;
        originalLocalPos = transform.localPosition;
        originalLocalRot = transform.localRotation;

        carryColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in carryColliders) col.enabled = false;

        transform.SetParent(holdPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        IsHeld = true;
    }

    public void PutDown()
    {
        transform.SetParent(originalParent, false);
        transform.localPosition = originalLocalPos;
        transform.localRotation = originalLocalRot;

        if (carryColliders != null)
            foreach (Collider col in carryColliders)
                if (col != null) col.enabled = true;

        IsHeld = false;
    }
}
