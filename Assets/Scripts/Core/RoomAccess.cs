/// <summary>
/// Which rooms the player may walk into right now. Doors with 'Gated By Room Access' ask this.
///
/// The story unlocks rooms one at a time: add a case here when a new room opens. Anything not
/// listed stays locked.
/// </summary>
public static class RoomAccess
{
    public static bool IsOpen(string scene)
    {
        switch (scene)
        {
            case "StaffRoom":
                return true;                       // the first stop of the shift

            case "Room01":
                return GameState.HasAllThings;     // 'Clean room 1' starts once the three things are collected

            default:
                return false;                      // every other room is locked for now
        }
    }
}
