using System.Collections;
using UnityEngine;

/// <summary>
/// The side door of Room 2, into the electricity room. Locked until the corridor has looped twice,
/// then it swings open (no scene change - the electricity room is part of the Room 2 scene) and stays open.
/// </summary>
public class SwingDoor : Interactable
{
    [Tooltip("The door leaf. Its pivot must be on the hinge.")]
    [SerializeField] private Transform leaf;
    [SerializeField] private float openAngle = -105f;
    [SerializeField] private string lockedLabel = "Locked";

    public bool Opened { get; private set; }

    private static bool Unlocked { get { return GameState.R2SideOpen || GameState.R2Loops >= 2 || GameState.R2Done; } }

    public override string Prompt { get { return Opened ? "" : (Unlocked ? "Open the door" : lockedLabel); } }
    public override string KeyHint { get { return !Opened && Unlocked ? "E" : ""; } }
    public override bool CanInteract { get { return !Opened; } }

    private void Start()
    {
        if (!GameState.R2SideOpen) return;
        Opened = true;
        leaf.localRotation = Quaternion.Euler(0f, openAngle, 0f);
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (Opened || !Unlocked) return;
        Opened = true;
        GameState.R2SideOpen = true;
        StartCoroutine(Swing());
    }

    private IEnumerator Swing()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
        {
            leaf.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(0f, openAngle, t), 0f);
            yield return null;
        }
        leaf.localRotation = Quaternion.Euler(0f, openAngle, 0f);
    }
}
