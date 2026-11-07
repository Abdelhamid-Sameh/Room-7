using UnityEngine;

/// <summary>The big mirror: E knocks on the glass - and something knocks back.</summary>
public class MirrorGlass : Interactable
{
    [SerializeField] private MirrorRoom room;
    public override string Prompt { get { return "Knock on the glass"; } }
    public override bool CanInteract { get { return room != null && room.CanKnock; } }
    public override void Interact(PlayerInteraction interactor) { if (room != null) room.Knock(); }
}
