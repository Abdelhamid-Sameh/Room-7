using UnityEngine;

/// <summary>
/// Something that hides an item until Nabil opens it (a school bag, a pencil case). Opening swings or slides the lid
/// and shows the hidden object. Remembered with a story flag so a re-streamed room stays the way he left it.
/// </summary>
public class Stash : Interactable
{
    [SerializeField] private GameObject hidden;     // shown when opened
    [SerializeField] private Transform lid;         // optional: rotated by lidAngle around its local X
    [SerializeField] private float lidAngle = -110f;

    private bool opened;

    private string Flag { get { return "p5stash_" + name; } }
    public override bool CanInteract { get { return !opened; } }

    private void Start()
    {
        if (StoryStateManager.HasFlag(Flag)) Show();
        else if (hidden != null) hidden.SetActive(false);
    }

    public override void Interact(PlayerInteraction interactor)
    {
        StoryStateManager.SetFlag(Flag);
        Sfx.Play(SfxKind.Paper);
        Show();
    }

    private void Show()
    {
        opened = true;
        if (hidden != null) hidden.SetActive(true);
        if (lid != null) lid.localRotation *= Quaternion.Euler(lidAngle, 0f, 0f);
    }
}
