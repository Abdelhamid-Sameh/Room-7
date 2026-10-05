using UnityEngine;

/// <summary>
/// A staff-room locker. The door swings open / closed and the state is kept in
/// GameState so it survives a trip to the corridor.
///
/// Contents (a PickupItem) are physically modelled inside the locker, so the
/// door's collider hides them while shut and reveals them when opened.
/// </summary>
public class Locker : Interactable
{
    public enum Owner { Nabil, Malak }

    [Header("Whose locker")]
    [SerializeField] private Owner owner = Owner.Nabil;
    [SerializeField] private string ownerName = "Nabil";

    [Header("Door")]
    [Tooltip("Child pivot the door mesh hangs off. Defaults to child 'DoorPivot'.")]
    [SerializeField] private Transform doorPivot;

    [Tooltip("Degrees the door swings outward, in the pivot's local space.")]
    [SerializeField] private float openAngle = 100f;

    [SerializeField] private float smooth = 12f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    private bool isOpen;
    private bool ready;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    public bool IsOpen => isOpen;
    public Owner OwnerId => owner;

    public override string Prompt =>
        isOpen ? $"Close {ownerName}'s locker" : $"Open {ownerName}'s locker";

    private void Start()
    {
        if (doorPivot == null)
        {
            Transform found = transform.Find("DoorPivot");
            if (found != null) doorPivot = found;
        }

        if (doorPivot == null)
        {
            Debug.LogError($"[Locker] '{name}' has no DoorPivot child - locker disabled.", this);
            enabled = false;
            return;
        }

        closedRotation = doorPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        isOpen = owner == Owner.Nabil ? GameState.NabilLockerOpen : GameState.MalakLockerOpen;
        doorPivot.localRotation = isOpen ? openRotation : closedRotation;

        ready = true;
    }

    private void Update()
    {
        if (!ready) return;

        Quaternion target = isOpen ? openRotation : closedRotation;
        doorPivot.localRotation = Quaternion.Slerp(
            doorPivot.localRotation, target, Time.deltaTime * smooth);
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (!ready) return;

        isOpen = !isOpen;

        if (owner == Owner.Nabil) GameState.NabilLockerOpen = isOpen;
        else GameState.MalakLockerOpen = isOpen;

        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null)
        {
            AudioClip clip = isOpen ? openClip : closeClip;
            if (clip != null) audio.PlayOneShot(clip, AudioLevels.SfxVolume);
        }

        GameState.RaiseChanged();
    }
}
