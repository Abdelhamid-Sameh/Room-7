using System.Collections;
using UnityEngine;

/// <summary>
/// The teacher's drawer, shut with a six-digit lock. The code is the desk numbers of the six children in the order the
/// rumour passed between them, from the one who started it to the last (see Room5Data.Code). Opening slides the drawer
/// out (its front faces -Z in model space) and switches its collider off so the cassette inside can be picked up.
/// </summary>
public class TeacherDrawer : Interactable
{
    [SerializeField] private float distance = 0.36f;

    private bool open;
    private int wrong;
    private Vector3 closedPos;

    public bool IsOpen { get { return open; } }
    public override string Prompt { get { return "Try the drawer lock"; } }
    public override bool CanInteract { get { return !open && !SafeKeypad.IsOpen; } }

    private void Awake() { closedPos = transform.localPosition; }

    private void Start() { if (StoryStateManager.HasFlag(Room5Data.Solved)) Open(true); }

    public override void Interact(PlayerInteraction interactor)
    {
        SafeKeypad.Show(Check, Unlocked, 6, "DRAWER");
    }

    private bool Check(string code)
    {
        if (code == Room5Data.Code) return true;
        wrong++;
        if (wrong % 2 == 0)
        {
            if (!Room5Data.AllNotes)
                PhoneUI.Hint("Not yet", Room5Data.NotesRead + " of 5 passed notes found. Look in bags, cases, the floor, the windows");
            else if (!Room5Data.Has(Room5Data.Chart))
                PhoneUI.Hint("Who sits where?", "There is a seating chart on the wall by the door");
            else
                PhoneUI.Hint("Who started it?", "Who saw it with her own eyes? Follow the notes from her");
        }
        return false;
    }

    private void Unlocked()
    {
        StoryStateManager.SetFlag(Room5Data.Solved);
        Open(false);
        PhoneUI.Hint("The drawer clicked open", "Rana started it, Sara heard the biggest version");
    }

    public void Open(bool instant)
    {
        if (open) return;
        open = true;
        Collider c = GetComponent<Collider>();
        if (c != null) c.enabled = false;
        Vector3 dir = transform.parent != null ? transform.parent.InverseTransformDirection(-transform.forward) : -transform.forward;
        Vector3 to = closedPos + dir.normalized * distance;
        if (instant) { transform.localPosition = to; return; }
        Sfx.Play(SfxKind.SafeOpen);
        StartCoroutine(Slide(to));
    }

    private IEnumerator Slide(Vector3 to)
    {
        Vector3 from = transform.localPosition;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
        {
            transform.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.localPosition = to;
    }
}
