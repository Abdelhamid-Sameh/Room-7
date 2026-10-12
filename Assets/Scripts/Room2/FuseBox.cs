using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// The fusebox in the electricity room. First press opens the cover; with the fuse in hand, the next
/// press cuts to a close-up camera, slots the fuse in, and the power comes back. That finishes Room 2:
/// the task ticks and the Feyrouz track is added to the radio.
/// </summary>
public class FuseBox : Interactable
{
    [SerializeField] private Transform cover;               // pivot on the hinge
    [SerializeField] private float coverOpenAngle = 110f;
    [SerializeField] private Transform fuse;                // sits in the slot; hidden until inserted
    [SerializeField] private Vector3 fuseOutOffset = new Vector3(0f, 0f, 0.3f);   // local, towards the player
    [SerializeField] private CinemachineCamera closeUp;     // virtual camera framing the box

    private bool coverOpen;
    private bool busy;

    public override string Prompt
    {
        get
        {
            if (!coverOpen) return "Open the fusebox";
            return GameState.HasFuse ? "Insert the fuse" : "The fuse is gone";
        }
    }

    public override string KeyHint { get { return coverOpen && !GameState.HasFuse ? "" : "E"; } }
    public override bool CanInteract { get { return !GameState.R2Done && !busy; } }

    private void Start()
    {
        if (closeUp != null) closeUp.Priority.Value = 0;

        if (GameState.R2Done)
        {
            coverOpen = true;
            cover.localRotation = Quaternion.Euler(0f, coverOpenAngle, 0f);
            fuse.gameObject.SetActive(true);
        }
        else
        {
            fuse.gameObject.SetActive(false);
        }
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (busy || GameState.R2Done) return;

        if (!coverOpen) { StartCoroutine(OpenCover()); return; }
        if (GameState.HasFuse) StartCoroutine(Insert());
    }

    private IEnumerator OpenCover()
    {
        coverOpen = true;
        busy = true;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            cover.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(0f, coverOpenAngle, t), 0f);
            yield return null;
        }
        cover.localRotation = Quaternion.Euler(0f, coverOpenAngle, 0f);
        busy = false;
    }

    private IEnumerator Insert()
    {
        busy = true;
        MonoBehaviour ctrl = PauseMenu.FindPlayerController();
        if (ctrl != null) ctrl.enabled = false;

        // Camera behaviour: cut to the close-up virtual camera (Cinemachine blends it in).
        if (closeUp != null) closeUp.Priority.Value = 50;
        yield return new WaitForSeconds(1.2f);

        Vector3 slot = fuse.localPosition;
        fuse.gameObject.SetActive(true);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
        {
            fuse.localPosition = Vector3.Lerp(slot + fuseOutOffset, slot, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        fuse.localPosition = slot;
        Sfx.Play(SfxKind.Fuse);
        yield return new WaitForSeconds(0.9f);

        // The power comes back (PowerReactor flickers the lights on).
        Sfx.Play(SfxKind.PowerOn);
        GameState.HasFuse = false;
        GameState.PowerOut = false;
        GameState.R2Done = true;
        GameState.SetRoomCleaned("Room02");
        GameState.UnlockedTracks.Add("Room02");
        CameraEffects.Shake(0.3f);
        GameState.RaiseChanged();
        yield return new WaitForSeconds(1.6f);
        PhoneUI.Hint("New radio track", "شط اسكندرية - فيروز");

        if (closeUp != null) closeUp.Priority.Value = 0;
        yield return new WaitForSeconds(1.2f);

        PauseMenu.ClearStaleInput();
        if (ctrl != null) ctrl.enabled = true;
        busy = false;
    }
}
