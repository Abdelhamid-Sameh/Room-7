using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A drawer you can slide open and push shut with E. Sits on a child "Front" object that owns the trigger pad
/// the player aims at (the furniture's own collider would otherwise swallow the ray). The pad and the drawer
/// move together. A drawer that starts out of place also carries a TidyItem: it can only be opened once the
/// tidying has pushed it shut.
/// </summary>
public class Drawer : Interactable
{
    [SerializeField] private Transform drawer;                       // the moving drawer mesh
    [SerializeField] private Vector3 closedLocal;                    // its closed position, local to its parent
    [SerializeField] private Vector3 openWorldOffset = new Vector3(-0.22f, 0f, 0f);   // which way and how far it slides
    [SerializeField] private Collider pad;                           // the trigger pad on the front
    [SerializeField] private TidyItem tidy;                          // optional: this drawer starts untidy
    [SerializeField] private Collider tidyCollider;                  // that TidyItem's own collider, off once tidy
    [SerializeField] private string drawerId;
    [Tooltip("Colliders of things inside the drawer (a pickup): only live while the drawer is open.")]
    [SerializeField] private Collider[] contents;
    [SerializeField] private float seconds = 0.35f;

    // Kept for the whole run so a drawer stays as you left it when you come back to the room.
    private static readonly HashSet<string> openIds = new HashSet<string>();

    private Vector3 drawerClosed, padClosed, slide;
    private bool ready, busy;

    public override string Prompt { get { return openIds.Contains(drawerId) ? "Close the drawer" : "Open the drawer"; } }
    public override bool CanInteract { get { return ready && !busy; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { openIds.Clear(); }

    private void Update()
    {
        bool armed = tidy == null || tidy.IsTidy;
        if (pad != null) pad.enabled = armed;
        if (tidyCollider != null) tidyCollider.enabled = !armed;
        if (!armed || ready) return;

        // First frame the drawer is in its closed place: remember it and show the state we left it in.
        drawerClosed = drawer.parent != null ? drawer.parent.TransformPoint(closedLocal) : closedLocal;
        padClosed = transform.position;
        slide = RoomFrame.Dir(this, openWorldOffset);
        float t = openIds.Contains(drawerId) ? 1f : 0f;
        Place(t);
        ready = true;
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (!ready || busy) return;
        StartCoroutine(Slide(!openIds.Contains(drawerId)));
    }

    private IEnumerator Slide(bool open)
    {
        busy = true;
        if (open) openIds.Add(drawerId); else openIds.Remove(drawerId);
        float from = open ? 0f : 1f, to = open ? 1f : 0f;
        for (float k = 0f; k < 1f; k += Time.deltaTime / seconds)
        {
            Place(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k)));
            yield return null;
        }
        Place(to);
        busy = false;
    }

    private void Place(float t)
    {
        foreach (Collider c in contents) if (c != null) c.enabled = t > 0.6f;
        drawer.position = drawerClosed + slide * t;
        transform.position = padClosed + slide * t;
    }
}
