using System.Collections;
using UnityEngine;

/// <summary>The door in the side of the big Game Boy. Put on the hinge pivot; the console opens it when the maze is won.</summary>
public class GameBoyDoor : MonoBehaviour
{
    [SerializeField] private float openAngle = 105f;
    [SerializeField] private float seconds = 1.6f;
    [Tooltip("Sound slot: the door swinging open (falls back to the ordinary door sound).")]
    [SerializeField] private AudioClip openSound;

    public bool IsOpen { get; private set; }

    private void Start()
    {
        if (GameState.GameBoySolved) { IsOpen = true; transform.localRotation = Quaternion.Euler(0f, openAngle, 0f); }
    }

    public void Open()
    {
        if (!IsOpen) StartCoroutine(Swing());
    }

    private IEnumerator Swing()
    {
        IsOpen = true;
        if (openSound != null) AudioLevels.PlaySfx(openSound, transform.position);
        else Sfx.Play(SfxKind.Door);

        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            transform.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(0f, openAngle, t), 0f);
            yield return null;
        }
        transform.localRotation = Quaternion.Euler(0f, openAngle, 0f);
    }
}
