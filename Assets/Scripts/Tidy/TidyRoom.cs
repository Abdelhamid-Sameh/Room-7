using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One per cleanable room (put it on an empty object in the room's scene).
///
/// Counts the room's TidyItems, publishes 'done / total' for the HUD progress bar through GameState,
/// and marks the room as cleaned once everything is tidy - which is what finishes the
/// 'Clean room N' task. The bar disappears when the player leaves the room.
///
/// Room Id is the scene's key everywhere else (the task list, RoomAccess, remembered tidy items), so it
/// should match the scene name: Room01, Room02...
/// </summary>
[DisallowMultipleComponent]
public class TidyRoom : MonoBehaviour
{
    public static TidyRoom Instance { get; private set; }

    [SerializeField] private string roomId = "Room01";
    [Tooltip("Shown on the progress bar.")]
    [SerializeField] private string displayName = "Room 1";

    public string RoomId => roomId;

    private readonly List<TidyItem> items = new List<TidyItem>();
    private bool dirty;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;

        // Leaving the room: the progress bar goes away.
        GameState.TidyRoomLabel = "";
        GameState.TidyDone = 0;
        GameState.TidyTotal = 0;
        GameState.RaiseChanged();
    }

    public void Register(TidyItem item)
    {
        if (!items.Contains(item)) items.Add(item);
        dirty = true;
    }

    public void NotifyChanged()
    {
        dirty = true;
    }

    private void LateUpdate()
    {
        if (dirty) Publish();
    }

    private void Publish()
    {
        dirty = false;

        int done = 0;
        foreach (TidyItem item in items)
            if (item != null && item.IsTidy) done++;

        GameState.TidyRoomLabel = displayName;
        GameState.TidyDone = done;
        GameState.TidyTotal = items.Count;

        if (items.Count > 0 && done == items.Count) GameState.SetRoomCleaned(roomId);

        GameState.RaiseChanged();
    }
}
