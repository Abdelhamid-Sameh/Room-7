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
    /// Malak's uniform. A clue, not an item - interacting picks it up to the centre of the screen
    /// (see CarrySystem) and interacting again puts it back exactly where it was.
    /// </summary>
    InspectMalakUniform
}

/// <summary>
/// One-shot interaction that writes a flag into GameState and updates the HUD. Anything already
/// taken stays gone when the scene is loaded again (GameState remembers it).
///
/// PickupKind.InspectMalakUniform is the odd one out - instead of vanishing into the inventory it
/// implements ICarryable and toggles via CarrySystem, and records a clue the first time.
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

    [Header("Carrying")]
    [Tooltip("Rotation relative to the camera while held. -90 on X turns a flat item to face the player.")]
    [SerializeField] private Vector3 heldEuler = new Vector3(-90f, 0f, 0f);

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

    private void Start()
    {
        // Back from another scene: anything already taken must stay taken.
        if (kind == PickupKind.InspectMalakUniform || !AlreadyHandled) return;

        if (kind == PickupKind.WearUniform && GameState.UniformWorn && activateOnTaken != null)
            activateOnTaken.SetActive(true);

        Disappear();
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (kind == PickupKind.InspectMalakUniform)
        {
            GameState.MalakUniformInspected = true;
            GameState.AddClue(GameState.ClueMalakUniform);   // flashes once, then lives in the clue log
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

        AudioLevels.PlaySfx(pickupClip, transform.position);

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
        transform.localRotation = Quaternion.Euler(heldEuler);

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
