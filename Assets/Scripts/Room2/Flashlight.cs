using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The flashlight Nabil finds in the corridor. F switches it on and off. Lives on GameSystems (it
/// survives scene changes) and hangs a spot light on whichever camera is current, so it works in
/// every scene. The light is called 'Flashlight' so PowerReactor leaves it alone in a blackout.
/// </summary>
public class Flashlight : MonoBehaviour
{
    private Light beam;

    private void Update()
    {
        if (!GameState.HasFlashlight) return;

        Camera cam = Camera.main;
        if (cam == null) return;
        if (beam == null || beam.transform.parent != cam.transform) Make(cam);

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame && !PauseMenu.IsPaused && !PhoneUI.Busy)
        {
            GameState.FlashlightOn = !GameState.FlashlightOn;
            Sfx.Play(GameState.FlashlightOn ? SfxKind.Switch : SfxKind.SwitchOff);   // same sounds as the wall light switch
        }

        beam.enabled = GameState.FlashlightOn;
    }

    private void Make(Camera cam)
    {
        if (beam != null) Destroy(beam.gameObject);

        GameObject go = new GameObject("Flashlight");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0.18f, -0.14f, 0.25f);

        beam = go.AddComponent<Light>();
        beam.type = LightType.Spot;
        beam.range = 20f;
        beam.spotAngle = 62f;
        beam.innerSpotAngle = 44f;
        beam.intensity = 18f;
        beam.color = new Color(1f, 0.93f, 0.78f);
        beam.shadows = LightShadows.Soft;
        beam.enabled = GameState.FlashlightOn;
    }
}
