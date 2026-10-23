using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Runs the Room 2 story beat that happens INSIDE the room: on the second entry the lights go out,
/// the street noise stops and Nabil texts Malak. (The corridor, the doors and the fusebox do the rest.)
/// </summary>
public class Room2Controller : MonoBehaviour
{
    private void Start()
    {
        // Snappier cuts between the player camera and the fusebox close-up than the 2 s default.
        Camera cam = Camera.main;
        CinemachineBrain brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.9f);

        if (!GameState.R2Done && GameState.R2Loops == 1 && !GameState.PowerOut && GameState.HasFlashlight) StartCoroutine(Blackout());
    }

    private IEnumerator Blackout()
    {
        yield return new WaitForSeconds(3.5f);
        Sfx.Play(SfxKind.PowerDown);
        HotelPower.Cut();
        CameraEffects.Shake(0.5f);
        PhoneUI.TextMalakLightsOut();
    }
}
