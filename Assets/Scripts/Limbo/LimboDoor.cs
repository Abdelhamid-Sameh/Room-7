using System.Collections;
using UnityEngine;

/// <summary>
/// A door in the strange corridor. Sits on the hinge pivot (the leaf, with the collider, is its child). E swings it
/// open away from the player. With a target scene that loads next; with none (the Game Boy room is in the same scene) it just stays open.
/// </summary>
public class LimboDoor : Interactable
{
    [SerializeField] private string targetScene = "";
    [SerializeField] private string spawnId = "Limbo_Start";
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float seconds = 0.9f;
    [SerializeField] private AudioClip openClip;

    private bool busy;

    public bool Opened { get; private set; }

    public override string Prompt { get { return "Open the door"; } }
    public override bool CanInteract { get { return !busy; } }

    public override void Interact(PlayerInteraction interactor)
    {
        if (!busy) StartCoroutine(Open());
    }

    private IEnumerator Open()
    {
        busy = true;
        if (openClip != null) AudioLevels.PlaySfx(openClip, transform.position);
        else Sfx.Play(SfxKind.Door);

        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            transform.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(0f, openAngle, t), 0f);
            yield return null;
        }
        transform.localRotation = Quaternion.Euler(0f, openAngle, 0f);
        Opened = true;
        if (string.IsNullOrEmpty(targetScene)) yield break;
        yield return new WaitForSeconds(0.3f);

        if (GameSystems.HasInstance) GameSystems.Instance.Fader.LoadScene(targetScene, spawnId);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
    }
}
