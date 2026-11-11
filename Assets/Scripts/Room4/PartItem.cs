using UnityEngine;

/// <summary>
/// A loose projector part (lens, bulb, film reel) lying somewhere in the cinema. E picks it up: it vanishes from
/// the room and its story flag is set, which is what the projector checks before it lets the matching puzzle start.
/// </summary>
public class PartItem : Interactable
{
    [SerializeField] private string partName = "Lens";
    [SerializeField] private string flag = "p4Lens";

    public override string Prompt => "Pick up the " + partName.ToLowerInvariant();
    public override bool CanInteract => !StoryStateManager.HasFlag(flag);

    private void Start()
    {
        if (StoryStateManager.HasFlag(flag)) gameObject.SetActive(false);   // already taken on an earlier visit
    }

    public override void Interact(PlayerInteraction interactor)
    {
        StoryStateManager.SetFlag(flag);
        Sfx.Play(SfxKind.Pickup);
        StoryStateManager.SetFlag(ProjectorPanel.Inspected);   // the projector's sub-tasks appear in the task list
        GameState.RaiseChanged();
        PhoneUI.Hint("Projector " + partName.ToLowerInvariant() + " found", "Fit it at the projector at the back of the cinema");
        gameObject.SetActive(false);
    }
}
