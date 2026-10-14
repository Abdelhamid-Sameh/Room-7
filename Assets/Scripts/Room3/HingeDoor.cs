using System.Collections;
using UnityEngine;

/// <summary>
/// An ordinary door inside a scene: E opens it (it swings away from whoever is pushing it) and E closes it
/// again. No transition. Every door remembers its angle in GameState, so it is still open after a trip
/// to the corridor and back. A door will not close while the player is standing in the doorway.
/// The leaf's pivot must be on the hinge; its parent is the door frame (its forward points through the opening).
/// </summary>
public class HingeDoor : Interactable
{
    [SerializeField] private Transform leaf;
    [SerializeField] private string doorId = "";
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float seconds = 0.8f;
    [Tooltip("0 = swing away from whoever pushes it. +1 / -1 = always swing the same way (use when furniture is in the way on one side).")]
    [SerializeField] private int fixedSide = 0;

    public bool Opened { get; private set; }

    private bool busy;
    private float angle;   // signed angle the leaf is open by (0 = shut)

    public override string Prompt { get { return Opened ? "Close the door" : "Open the door"; } }
    public override bool CanInteract { get { return !busy; } }

    private void Start()
    {
        float saved;
        if (GameState.DoorAngles.TryGetValue(doorId, out saved) && Mathf.Abs(saved) > 1f)
        {
            Opened = true;
            angle = saved;
            leaf.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (busy) return;
        Vector3 toPlayer = interactor.transform.position - leaf.parent.position;
        toPlayer.y = 0f;

        if (Opened)
        {
            if (toPlayer.magnitude < 0.8f) return;   // standing in the doorway
            StartCoroutine(Swing(angle, 0f));
            Opened = false;
            GameState.DoorAngles.Remove(doorId);
        }
        else
        {
            // positive angles swing the leaf towards the frame's forward side, so push it to the far side
            if (fixedSide != 0) angle = Mathf.Sign(fixedSide) * openAngle;
            else angle = Vector3.Dot(toPlayer, leaf.parent.forward) > 0f ? -openAngle : openAngle;
            StartCoroutine(Swing(0f, angle));
            Opened = true;
            GameState.DoorAngles[doorId] = angle;
        }
    }

    private IEnumerator Swing(float from, float to)
    {
        busy = true;
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            leaf.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(from, to, t), 0f);
            yield return null;
        }
        leaf.localRotation = Quaternion.Euler(0f, to, 0f);
        busy = false;
    }
}
