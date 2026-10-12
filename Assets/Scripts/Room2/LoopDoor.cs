/// <summary>
/// A Room 2 door back to the corridor. The FRONT door ('Counts As Loop') is the one that makes the
/// corridor loop - leaving through it moves Room 2 on a stage. Leaving by the door Nabil came in
/// through changes nothing.
/// </summary>
public class LoopDoor : DoorTransition
{
    [UnityEngine.SerializeField] private bool countsAsLoop;

    public override void Interact(PlayerInteraction interactor)
    {
        if (IsLocked) return;
        if (countsAsLoop) GameState.CountLoop();
        base.Interact(interactor);
    }
}
