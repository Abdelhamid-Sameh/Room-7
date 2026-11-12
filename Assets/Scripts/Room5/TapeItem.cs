using UnityEngine;

/// <summary>The cassette in the teacher's drawer. Taking it unlocks "Harf Gar" on the radio.</summary>
public class TapeItem : Interactable
{
    [SerializeField] private TeacherDrawer drawer;

    public override string Prompt { get { return "Take the cassette"; } }
    public override bool CanInteract { get { return drawer != null && drawer.IsOpen && !StoryStateManager.HasFlag(Room5Data.Tape); } }

    private void Start()
    {
        if (StoryStateManager.HasFlag(Room5Data.Tape)) gameObject.SetActive(false);
    }

    public override void Interact(PlayerInteraction interactor)
    {
        StoryStateManager.SetFlag(Room5Data.Tape);
        GameState.UnlockedTracks.Add("Room05");
        GameState.RaiseChanged();
        Sfx.Play(SfxKind.Pickup);
        PhoneUI.Hint("New track for the radio", "حرف جر - المغنى خانة");
        gameObject.SetActive(false);
    }
}
