using UnityEngine;

/// <summary>
/// First person raycast + input. Sits on the player, raycasts from the centre
/// of the main camera, and forwards E / Left Click to whatever Interactable is
/// under the crosshair.
///
/// While something is being carried (see CarrySystem), it fills the centre of
/// the screen and we stop scanning - E always puts it back, no matter where
/// the player is looking.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float reach = 3f;

    // Everything except layer 8 (the player's own capsule).
    [SerializeField] private LayerMask targetMask = ~(1 << 8);

    private Camera viewCamera;
    private Interactable current;
    private bool wasHolding;

    /// <summary>The Interactable currently under the crosshair, or null.</summary>
    public Interactable Current => current;

    private void Awake()
    {
        viewCamera = Camera.main;
        if (viewCamera == null) viewCamera = FindFirstObjectByType<Camera>();
    }

    private void Update()
    {
        bool holding = CarrySystem.Instance != null && CarrySystem.Instance.IsHolding;

        if (holding)
        {
            if (!wasHolding)
            {
                current = null;
                PushHeldPrompt();
            }
            wasHolding = true;

            if (GameInput.InteractPressed) CarrySystem.Instance.PutDownCurrent();
            return;
        }

        wasHolding = false;

        Interactable found = Scan();

        if (!ReferenceEquals(found, current))
        {
            current = found;
            PushPrompt();
        }

        if (current != null && GameInput.InteractPressed)
        {
            current.Interact(this);
        }
    }

    private Interactable Scan()
    {
        if (viewCamera == null) return null;

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, reach, targetMask, QueryTriggerInteraction.Collide))
            return null;

        Interactable found = hit.collider.GetComponentInParent<Interactable>();
        if (found == null || !found.CanInteract) return null;
        return found;
    }

    private void PushPrompt()
    {
        if (GameSystems.HasInstance && GameSystems.Instance.HUD != null)
        {
            GameSystems.Instance.HUD.SetPrompt(current != null ? current.Prompt : null);
        }
    }

    private void PushHeldPrompt()
    {
        if (GameSystems.HasInstance && GameSystems.Instance.HUD != null)
        {
            GameSystems.Instance.HUD.SetPrompt("Put it back");
        }
    }
}
