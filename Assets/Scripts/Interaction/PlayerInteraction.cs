using UnityEngine;

/// <summary>
/// First person raycast + input. Sits on the player, raycasts from the centre of the main camera,
/// and forwards E / Left Click to whatever Interactable is under the crosshair.
///
/// The HUD prompt is re-checked every frame against what it should say right now, so it can never
/// be left over from a previous target, a changed prompt text (e.g. "Turn off" -> "Turn on"), a
/// dropped item, or the room you just left. There is no prompt and no interaction while the
/// screen is fading between rooms.
///
/// While something is being carried (see CarrySystem) it fills the centre of the screen and we stop
/// scanning - E always puts it back, no matter where the player is looking.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float reach = 3f;

    // Everything except layer 8 (the player's own capsule).
    [SerializeField] private LayerMask targetMask = ~(1 << 8);

    private Camera viewCamera;
    private Interactable current;

    // What the HUD is showing right now. hudKnown stays false until the HUD has really been told
    // something, so a prompt left over from another scene is always cleared on the first frame.
    private string shownPrompt;
    private bool hudKnown;

    /// <summary>The Interactable currently under the crosshair, or null.</summary>
    public Interactable Current => current;

    private static bool IsTransitioning
    {
        get
        {
            return GameSystems.HasInstance
                   && GameSystems.Instance.Fader != null
                   && GameSystems.Instance.Fader.IsBusy;
        }
    }

    private void Awake()
    {
        viewCamera = Camera.main;
        if (viewCamera == null) viewCamera = FindFirstObjectByType<Camera>();
    }

    private void OnEnable()
    {
        current = null;
        hudKnown = false;
        ShowPrompt(null);
    }

    private void OnDisable()
    {
        // Leaving the scene: never leave our last prompt behind on the (persistent) HUD.
        if (GameSystems.HasInstance && GameSystems.Instance.HUD != null)
            GameSystems.Instance.HUD.SetPrompt(null);
    }

    private void Update()
    {
        if (PauseMenu.IsPaused || IsTransitioning)
        {
            current = null;
            ShowPrompt(null);
            return;
        }

        CarrySystem carry = CarrySystem.Instance;
        if (carry != null && carry.IsHolding)
        {
            current = null;
            ShowPrompt("Put it back");
            if (GameInput.InteractPressed) carry.PutDownCurrent();
            return;
        }

        current = Scan();
        ShowPrompt(current != null ? current.Prompt : null);

        if (current != null && GameInput.InteractPressed)
        {
            current.Interact(this);

            // The interaction may have changed what is there (item taken, door toggled, item picked up).
            current = Scan();
            ShowPrompt(current != null ? current.Prompt : null);
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

    /// <summary>Tells the HUD what to show, but only when that differs from what it is already showing.</summary>
    private void ShowPrompt(string prompt)
    {
        if (!GameSystems.HasInstance || GameSystems.Instance.HUD == null) return;   // try again next frame
        if (hudKnown && prompt == shownPrompt) return;

        GameSystems.Instance.HUD.SetPrompt(prompt);
        shownPrompt = prompt;
        hudKnown = true;
    }
}
