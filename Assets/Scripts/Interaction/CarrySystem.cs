using System.Reflection;
using UnityEngine;

/// <summary>
/// Tracks the single object the player is currently holding up to the camera.
/// Lives on the player; looks for a child of the main camera named "HoldPoint"
/// if one isn't assigned in the Inspector.
///
/// While something is held the player cannot walk, sprint or jump (looking around
/// stays free so the item can be inspected). Turn that off with the checkbox below.
///
/// Only one thing can be held at a time.
/// </summary>
[DisallowMultipleComponent]
public class CarrySystem : MonoBehaviour
{
    [Tooltip("Where carried items sit, centred on screen. Defaults to a child of Camera.main named 'HoldPoint'.")]
    [SerializeField] private Transform holdPoint;

    [Tooltip("Freeze walking, sprinting and jumping while an item is held. Looking around stays free.")]
    [SerializeField] private bool lockMovementWhileHolding = true;

    // The Starter Assets controller exposes these as public floats. They are set by
    // name so this script has no compile-time dependency on the Starter Assets assembly.
    private static readonly string[] LockedFields = { "MoveSpeed", "SprintSpeed", "JumpHeight" };

    public static CarrySystem Instance { get; private set; }

    public ICarryable Current { get; private set; }
    public bool IsHolding => Current != null;

    private Component controller;
    private FieldInfo[] lockedFieldInfos;
    private float[] savedValues;
    private bool movementLocked;

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

    private void OnDisable()
    {
        UnlockMovement();
    }

    private void OnDestroy()
    {
        UnlockMovement();
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
        LockMovement();
        return true;
    }

    public void PutDownCurrent()
    {
        if (Current == null) return;
        Current.PutDown();
        Current = null;
        UnlockMovement();
    }

    // ------------------------------------------------------- movement lock --

    private void LockMovement()
    {
        if (!lockMovementWhileHolding || movementLocked) return;

        if (controller == null)
        {
            foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "FirstPersonController")
                {
                    controller = mb;
                    break;
                }
            }
        }

        if (controller == null)
        {
            Debug.LogWarning("[CarrySystem] No FirstPersonController on the player - movement was not locked.");
            return;
        }

        System.Type type = controller.GetType();
        lockedFieldInfos = new FieldInfo[LockedFields.Length];
        savedValues = new float[LockedFields.Length];

        for (int i = 0; i < LockedFields.Length; i++)
        {
            FieldInfo field = type.GetField(LockedFields[i], BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(float))
            {
                lockedFieldInfos[i] = field;
                savedValues[i] = (float)field.GetValue(controller);
                field.SetValue(controller, 0f);
            }
        }

        movementLocked = true;
    }

    private void UnlockMovement()
    {
        if (!movementLocked) return;

        for (int i = 0; i < lockedFieldInfos.Length; i++)
        {
            if (lockedFieldInfos[i] != null && controller != null)
                lockedFieldInfos[i].SetValue(controller, savedValues[i]);
        }

        movementLocked = false;
    }
}
