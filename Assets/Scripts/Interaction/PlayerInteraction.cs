using UnityEngine;

/// <summary>
/// First person raycast + input. Sits on the player, raycasts from the centre of the main camera,
/// and forwards E / Left Click to whatever Interactable is under the crosshair.
///
/// Taps call Interact straight away. Hold interactions (HoldDuration above 0) start on a fresh press,
/// report progress while the button stays down, and finish when the bar is full - letting go or looking
/// away cancels them.
///
/// The HUD prompt is re-checked every frame against what it should say right now, so it can never
/// be left over from a previous target, a changed prompt text, a dropped item, or the room you just
/// left. There is no prompt and no interaction while the screen is fading between rooms or paused.
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
    private string shownKey;
    private bool hudKnown;

    // hold interactions
    private Interactable holdTarget;
    private float holdTime;
    private bool holding;
    private float shownHold = -1f;

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
        ShowPrompt(null, null);
    }

    private void OnDisable()
    {
        // Leaving the scene: never leave our last prompt behind on the (persistent) HUD.
        if (GameSystems.HasInstance && GameSystems.Instance.HUD != null)
        {
            GameSystems.Instance.HUD.SetPrompt(null);
            GameSystems.Instance.HUD.SetHoldProgress(0f);
        }
    }

    private void Update()
    {
        if (PauseMenu.IsPaused || IsTransitioning)
        {
            CancelHold();
            current = null;
            ShowPrompt(null, null);
            return;
        }

        CarrySystem carry = CarrySystem.Instance;
        if (carry != null && carry.IsHolding)
        {
            CancelHold();
            current = null;
            ShowPrompt("Put it back", "E");
            if (GameInput.InteractPressed) carry.PutDownCurrent();
            return;
        }

        current = Scan();
        ShowPrompt(current != null ? current.Prompt : null, current != null ? current.KeyHint : null);

        if (current == null)
        {
            CancelHold();
            return;
        }

        if (current.HoldDuration > 0f)
        {
            UpdateHold();
            return;
        }

        CancelHold();
        if (GameInput.InteractPressed)
        {
            current.Interact(this);

            // The interaction may have changed what is there (item taken, door toggled, item picked up).
            current = Scan();
            ShowPrompt(current != null ? current.Prompt : null, current != null ? current.KeyHint : null);
        }
    }

    private void UpdateHold()
    {
        if (holdTarget != current) CancelHold();   // looked away to something else

        if (!holding)
        {
            if (!GameInput.InteractPressed) return;   // a hold only starts on a fresh press
            holding = true;
            holdTarget = current;
            holdTime = 0f;
        }

        if (!GameInput.InteractHeld)
        {
            CancelHold();
            return;
        }

        holdTime += Time.deltaTime;
        float progress = Mathf.Clamp01(holdTime / current.HoldDuration);
        current.OnHold(progress);
        SetHoldBar(progress);

        if (progress >= 1f)
        {
            Interactable finished = current;
            holding = false;
            holdTime = 0f;
            holdTarget = null;
            SetHoldBar(0f);

            finished.Interact(this);

            current = Scan();
            ShowPrompt(current != null ? current.Prompt : null, current != null ? current.KeyHint : null);
        }
    }

    private void CancelHold()
    {
        if (holding && holdTarget != null) holdTarget.OnHoldCancelled();
        holding = false;
        holdTime = 0f;
        holdTarget = null;
        SetHoldBar(0f);
    }

    private Interactable Scan()
    {
        if (viewCamera == null) return null;

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        // Layer 9 holds the plain 'solid' colliders of furniture (they stop the player walking through a bed). The
        // interaction ray ignores them, so the blanket on the bed or a drawer inside a nightstand can still be reached;
        // walls stay on the default layer and still block it.
        int mask = targetMask.value & ~((1 << 8) | (1 << 9));
        if (!Physics.Raycast(ray, out RaycastHit hit, reach, mask, QueryTriggerInteraction.Collide))
            return null;

        Interactable found = hit.collider.GetComponentInParent<Interactable>();
        if (found == null || !found.CanInteract) return null;
        return found;
    }

    /// <summary>Tells the HUD what to show, but only when that differs from what it is already showing.</summary>
    private void ShowPrompt(string prompt, string key)
    {
        if (!GameSystems.HasInstance || GameSystems.Instance.HUD == null) return;   // try again next frame
        if (hudKnown && prompt == shownPrompt && key == shownKey) return;

        GameSystems.Instance.HUD.SetPrompt(prompt, key ?? "E");
        shownPrompt = prompt;
        shownKey = key;
        hudKnown = true;
    }

    private void SetHoldBar(float progress)
    {
        if (Mathf.Approximately(progress, shownHold)) return;
        if (!GameSystems.HasInstance || GameSystems.Instance.HUD == null) return;

        GameSystems.Instance.HUD.SetHoldProgress(progress);
        shownHold = progress;
    }
}
