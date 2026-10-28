using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes the scene follow GameState.PowerOut: every Light (except the player's flashlight) and every
/// glowing material goes dark with a stuttering flicker, and comes back the same way. Also dims the
/// ambient light so the dark really is dark. Drop one into any scene that has electric light.
/// </summary>
public class PowerReactor : MonoBehaviour
{
    private static readonly float[] FlickerOff = { 1f, 0.15f, 0.9f, 0.05f, 0.5f, 0f, 0.3f, 0f };
    private static readonly float[] FlickerOn = { 0f, 0.8f, 0.1f, 0.6f, 0f, 1f, 0.35f, 1f };

    private Light[] lights;
    private float[] baseIntensity;
    private readonly List<Material> glow = new List<Material>();
    private readonly List<Color> glowColor = new List<Color>();
    // Shared by every reactor (a streamed room brings its own): the lighting as authored, not as left dark by another reactor.
    private static float ambientIntensity, reflectionIntensity;
    private static Color ambientLight;
    private static int alive;
    private bool started;
    private bool on = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { alive = 0; }

    private void Start()
    {
        Light[] all = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Light> keep = new List<Light>();
        foreach (Light l in all) if (l.gameObject.name != "Flashlight" && l.gameObject.scene == gameObject.scene) keep.Add(l);   // only this scene's lights
        lights = keep.ToArray();
        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i].intensity;

        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer || r.gameObject.scene != gameObject.scene) continue;
            foreach (Material m in r.materials)   // instances, so nothing outside this scene changes
            {
                if (m == null || !m.HasProperty("_EmissionColor") || !m.IsKeywordEnabled("_EMISSION")) continue;
                if (m.GetColor("_EmissionColor").maxColorComponent < 0.05f) continue;
                glow.Add(m);
                glowColor.Add(m.GetColor("_EmissionColor"));
            }
        }

        if (alive++ == 0)
        {
            ambientIntensity = RenderSettings.ambientIntensity;
            ambientLight = RenderSettings.ambientLight;
            reflectionIntensity = RenderSettings.reflectionIntensity;
        }
        started = true;

        on = !GameState.PowerOut;
        Apply(on ? 1f : 0f);
    }

    // Hidden mid-flicker (the corridor is hidden when you leave Room 2 fast) the coroutine dies on a half-lit step: settle on the real state when shown again.
    private void OnEnable()
    {
        if (!started) return;
        StopAllCoroutines();
        on = !GameState.PowerOut;
        Apply(on ? 1f : 0f);
    }

    private void Update()
    {
        bool want = !GameState.PowerOut;
        if (want == on) return;
        on = want;
        StopAllCoroutines();
        StartCoroutine(Flicker(on ? FlickerOn : FlickerOff));
    }

    private void OnDestroy()
    {
        if (!started) return;
        // The last reactor out leaves the (shared) render settings as the scene authored them.
        if (--alive > 0) return;
        alive = 0;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.reflectionIntensity = reflectionIntensity;
    }

    private IEnumerator Flicker(float[] steps)
    {
        foreach (float s in steps)
        {
            Apply(s);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.14f));
        }
    }

    private void Apply(float f)
    {
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null) lights[i].intensity = baseIntensity[i] * f;

        for (int i = 0; i < glow.Count; i++)
            if (glow[i] != null) glow[i].SetColor("_EmissionColor", glowColor[i] * f);

        float dark = Mathf.Lerp(0.04f, 1f, f);
        RenderSettings.ambientIntensity = ambientIntensity * dark;
        RenderSettings.ambientLight = ambientLight * dark;
        RenderSettings.reflectionIntensity = reflectionIntensity * dark;
    }
}
