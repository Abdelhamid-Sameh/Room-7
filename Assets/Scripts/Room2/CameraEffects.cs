using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Cinemachine behaviours on the first-person camera:
///  1. sprint 'speed' - the field of view widens while running and settles when he stops;
///  2. shake - CameraEffects.Shake() fires a Cinemachine impulse (the blackout, the power coming back);
///  3. close-up - see FuseBox, which cuts to its own virtual camera and back with a blend.
/// Lives on GameSystems so every scene gets it.
/// </summary>
public class CameraEffects : MonoBehaviour
{
    private const float WalkFov = 70f, RunFov = 78f;

    private CinemachineCamera rig;
    private StarterAssetsInputs input;
    private CinemachineImpulseSource source;
    private static CameraEffects instance;

    private void Awake() { instance = this; }
    private void OnDestroy() { if (instance == this) instance = null; }

    private CinemachineCamera Rig()
    {
        if (rig != null) return rig;
        GameObject go = GameObject.Find("PlayerFollowCamera");
        if (go != null) rig = go.GetComponent<CinemachineCamera>();
        if (rig != null && rig.GetComponent<CinemachineImpulseListener>() == null) rig.gameObject.AddComponent<CinemachineImpulseListener>();
        return rig;
    }

    private void Update()
    {
        CinemachineCamera cam = Rig();
        if (cam == null) return;
        if (input == null) input = FindFirstObjectByType<StarterAssetsInputs>();

        bool running = input != null && input.sprint && input.move.sqrMagnitude > 0.01f;
        LensSettings lens = cam.Lens;
        lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, running ? RunFov : WalkFov, Time.deltaTime * 5f);
        cam.Lens = lens;
    }

    /// <summary>A short rumble through the player camera.</summary>
    public static void Shake(float force)
    {
        if (instance == null || instance.Rig() == null) return;

        if (instance.source == null)
        {
            instance.source = instance.gameObject.AddComponent<CinemachineImpulseSource>();
            instance.source.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Rumble;
            instance.source.ImpulseDefinition.ImpulseDuration = 0.7f;
        }

        instance.source.GenerateImpulse(Vector3.down * force);
    }
}
