using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// The safe in room B. Two codes open it: 210802 (the Game Boy and the Jadal track) and the easter egg 010503
/// (the rock and the Masar Egbari track). Each code works once; after taking what is inside the player can shut
/// the safe and try the other. The keypad cuts to a close-up camera (Cinemachine), the door swings open.
/// </summary>
public class SafeBox : Interactable
{
    public const string MainCode = "210802";
    public const string EggCode = "010503";

    [SerializeField] private Transform door;                // pivot on the hinge
    [SerializeField] private float doorOpenAngle = 110f;
    [SerializeField] private CinemachineCamera closeUp;
    [SerializeField] private GameObject gameBoy;
    [SerializeField] private GameObject rock;

    private bool busy;
    private string pendingCode;

    private bool BothDone { get { return GameState.R3MainDone && GameState.R3EggDone; } }
    private bool ItemInside { get { return !string.IsNullOrEmpty(GameState.R3SafeItem); } }

    public override string Prompt
    {
        get
        {
            if (GameState.R3SafeOpen) return "Close the safe";
            return BothDone ? "The safe is empty" : "Enter the code";
        }
    }

    public override string KeyHint { get { return !GameState.R3SafeOpen && BothDone ? "" : "E"; } }
    public override bool CanInteract { get { return !busy && !SafeKeypad.IsOpen && !(GameState.R3SafeOpen && ItemInside); } }

    private void Start()
    {
        if (closeUp != null) closeUp.Priority.Value = 0;
        door.localRotation = Quaternion.Euler(0f, GameState.R3SafeOpen ? doorOpenAngle : 0f, 0f);
        ShowItems();
    }

private void ShowItems()
    {
        // Closed and unopened: both items already sit inside, so nothing pops in when the door swings.
        bool fresh = !GameState.R3SafeOpen && !ItemInside;
        gameBoy.SetActive(GameState.R3SafeItem == "GameBoy" || (fresh && !GameState.R3MainDone));
        rock.SetActive(GameState.R3SafeItem == "Rock" || (fresh && !GameState.R3EggDone));
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (busy) return;
        if (GameState.R3SafeOpen) { StartCoroutine(Shut()); return; }
        if (BothDone) return;

        if (closeUp != null) closeUp.Priority.Value = 50;
        SafeKeypad.Show(Check, () => StartCoroutine(OpenSequence()));
    }

    private void Update()
    {
        // Esc on the keypad: step back from the close-up.
        if (closeUp != null && !busy && !SafeKeypad.IsOpen && closeUp.Priority.Value != 0) closeUp.Priority.Value = 0;
    }

    private bool Check(string code)
    {
        bool ok = (code == MainCode && !GameState.R3MainDone) || (code == EggCode && !GameState.R3EggDone);
        if (ok) pendingCode = code;
        return ok;
    }

private IEnumerator OpenSequence()
    {
        busy = true;
        MonoBehaviour ctrl = PauseMenu.FindPlayerController();
        if (ctrl != null) ctrl.enabled = false;
        yield return new WaitForSeconds(0.5f);

        bool main = pendingCode == MainCode;
        GameState.R3SafeItem = main ? "GameBoy" : "Rock";
        ShowItems();   // door is still shut: the other item is hidden before anyone can see it
        Sfx.Play(SfxKind.SafeOpen);
        yield return Swing(0f, doorOpenAngle, 1.1f);

        GameState.R3SafeOpen = true;
        if (main) GameState.R3MainDone = true; else GameState.R3EggDone = true;
        GameState.UnlockedTracks.Add(main ? "Room03_Main" : "Room03_Egg");
        GameState.RaiseChanged();

        yield return new WaitForSeconds(1.0f);
        PhoneUI.Hint("New radio track", main ? "يا أحلى عيون - جدل" : "شيروفوبيا - مسار إجبارى");
        yield return new WaitForSeconds(1.2f);

        if (closeUp != null) closeUp.Priority.Value = 0;
        yield return new WaitForSeconds(0.6f);
        PauseMenu.ClearStaleInput();
        if (ctrl != null) ctrl.enabled = true;
        busy = false;
    }

    private IEnumerator Shut()
    {
        busy = true;
        GameState.R3SafeOpen = false;
        Sfx.Play(SfxKind.SafeClose);
        yield return Swing(doorOpenAngle, 0f, 0.9f);
        busy = false;
    }

    private IEnumerator Swing(float from, float to, float seconds)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            door.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(from, to, t), 0f);
            yield return null;
        }
        door.localRotation = Quaternion.Euler(0f, to, 0f);
    }
}
