using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wall switch that turns the room's lights on and off.
///
/// It switches every Light under the 'Lighting' object and darkens the glow of any emissive fixture there,
/// but leaves the fixtures themselves visible - a switched-off ceiling lamp is still a lamp.
/// </summary>
public class LightSwitch : Interactable
{
    [Tooltip("Object holding this room's lights and fixtures. Falls back to the scene object named 'Lighting'.")]
    [SerializeField] private GameObject lightsRoot;

    [SerializeField] private bool startOn = true;

    [SerializeField] private AudioClip clickClip;

    private bool on;
    private bool resolved;

    private Light[] lights = new Light[0];
    private readonly List<Material> glowMaterials = new List<Material>();
    private readonly List<Color> glowColors = new List<Color>();

    private void Awake()
    {
        on = startOn;
    }

    private void Start()
    {
        Resolve();
        Apply();
    }

    public override string Prompt => on ? "Turn off the light" : "Turn on the light";

    public override void Interact(PlayerInteraction interactor)
    {
        on = !on;
        Apply();

        if (clickClip != null)
            AudioLevels.PlaySfx(clickClip, transform.position);
    }

    private void Resolve()
    {
        if (resolved) return;
        resolved = true;

        if (lightsRoot == null) lightsRoot = GameObject.Find("Lighting");
        if (lightsRoot == null) return;

        lights = lightsRoot.GetComponentsInChildren<Light>(true);

        foreach (Renderer r in lightsRoot.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material m in r.materials)   // instances, so other objects sharing the material are unaffected
            {
                if (m == null || !m.HasProperty("_EmissionColor") || !m.IsKeywordEnabled("_EMISSION")) continue;
                glowMaterials.Add(m);
                glowColors.Add(m.GetColor("_EmissionColor"));
            }
        }
    }

    private void Apply()
    {
        Resolve();

        foreach (Light l in lights)
            if (l != null) l.enabled = on;

        for (int i = 0; i < glowMaterials.Count; i++)
            glowMaterials[i].SetColor("_EmissionColor", on ? glowColors[i] : Color.black);
    }
}
