using UnityEngine;

/// <summary>
/// Tracks the single object the player is currently holding up to the
/// camera. Lives on the player; looks for a child of the main camera named
/// "HoldPoint" if one isn't assigned in the Inspector.
///
/// Only one thing can be held at a time - trying to pick up a second item
/// while already holding one is simply ignored (CanInteract on the held
/// item's Prompt should already steer the player to put the first one down).
/// </summary>
[DisallowMultipleComponent]
public class CarrySystem : MonoBehaviour
{
    [Tooltip("Where carried items sit, centred on screen. Defaults to a child of Camera.main named 'HoldPoint'.")]
    [SerializeField] private Transform holdPoint;

    public static CarrySystem Instance { get; private set; }

    public ICarryable Current { get; private set; }
    public bool IsHolding => Current != null;

    private void Awake()
    {
        Instance = this;

        if (holdPoint == null)
        {
            Camera cam = Camera.main;
            Transform found = cam != null ? cam.transform.Find("HoldPoint") : null;
            if (found == null)
            {
                GameObject hp = GameObject.Find("HoldPoint");
                if (hp != null) found = hp.transform;
            }
            holdPoint = found;
        }

        if (holdPoint == null)
            Debug.LogWarning("[CarrySystem] No HoldPoint found - carried items will have nowhere to go.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Pick up 'item', or put it back down if it is already the held one.</summary>
    public bool Toggle(ICarryable item)
    {
        if (item == null) return false;

        if (ReferenceEquals(Current, item))
        {
            PutDownCurrent();
            return true;
        }

        if (Current != null || holdPoint == null) return false;

        item.PickUp(holdPoint);
        Current = item;
        return true;
    }

    public void PutDownCurrent()
    {
        if (Current == null) return;
        Current.PutDown();
        Current = null;
    }
}
