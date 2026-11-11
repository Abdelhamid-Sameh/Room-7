using UnityEngine;

/// <summary>Room 4 (the cinema) scene logic: once every bit of rubbish is gone the "Clean room 4" objective ticks.</summary>
public class Room4Controller : MonoBehaviour
{
    private void Update()
    {
        if (!StoryStateManager.HasFlag(StoryStateManager.Flags.Room4Done) && GameState.IsRoomCleaned("Room04"))
        {
            StoryStateManager.SetFlag(StoryStateManager.Flags.Room4Done);
            GameState.RaiseChanged();
        }
    }
}
