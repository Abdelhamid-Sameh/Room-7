using UnityEngine;

/// <summary>
/// Wall switch that turns the room's lights on and off. Quick, visible proof
/// that player input changes world state (Assignment 2, Part 3.1 - 1.3).
/// </summary>
public class LightSwitch : Interactable
{
    [Tooltip("Object holding this room's lights. Falls back to the scene object named 'Lighting'.")]
    [SerializeField] private GameObject lightsRoot;

    [SerializeField] private bool startOn = true;

    [SerializeField] private AudioClip clickClip;

    private bool on;
    private bool resolved;

    private void Awake()
    {
        on = startOn;
    }

    private void Start()
    {
        if (!on) Apply();
    }

    public override string Prompt => on ? "Turn off the light" : "Turn on the light";

    public override void Interact(PlayerInteraction interactor)
    {
        on = !on;
        Apply();

        if (clickClip != null)
            AudioLevels.PlaySfx(clickClip, transform.position);
    }

    private void Apply()
    {
        if (!resolved)
        {
            resolved = true;
            if (lightsRoot == null) lightsRoot = GameObject.Find("Lighting");
        }

        if (lightsRoot != null) lightsRoot.SetActive(on);
    }
}
