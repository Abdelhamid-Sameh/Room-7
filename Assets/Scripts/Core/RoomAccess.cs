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
                return !GameState.PowerOut;        // the first stop of the shift (locked during the blackout)

            case "Room01":
                return GameState.HasAllThings && !GameState.PowerOut;   // 'Clean room 1' starts once the three things are collected

            case "Room02":
                return GameState.HasAllThings;     // the looping corridor - the only door that opens in the blackout

            default:
                return false;                      // every other room is locked for now
        }
    }
}
