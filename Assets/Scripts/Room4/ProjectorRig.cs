using UnityEngine;

/// <summary>
/// The cinema's broken film projector. E opens its service panel (ProjectorPanel), where the three parts found
/// around the room are fitted and adjusted. The model shows what has been fitted: the lens barrel, the feed reel,
/// and the lamp-house window glowing once the lamp works. START on the panel runs the shadow film.
/// </summary>
public class ProjectorRig : Interactable
{
    [SerializeField] private ShadowFilm film;
    [SerializeField] private GameObject lensPart, reelPart;
    [SerializeField] private Renderer lampWindow;

    public override bool CanInteract => !ProjectorPanel.Open && !(film != null && (film.Playing || film.Finished));
    public override string Prompt => ProjectorPanel.AllDone ? "Start the projector" : "Inspect the projector";

    private void Start() { RefreshModel(); }

    public override void Interact(PlayerInteraction interactor) { ProjectorPanel.Show(this); }

    public void StartFilm() { if (film != null) film.Play(); }

    /// <summary>Show the parts that are fitted.</summary>
    public void RefreshModel()
    {
        if (lensPart != null) lensPart.SetActive(StoryStateManager.HasFlag(ProjectorPanel.LensDone));
        if (reelPart != null) reelPart.SetActive(StoryStateManager.HasFlag(ProjectorPanel.ReelDone));
        if (lampWindow != null)
        {
            Material m = lampWindow.material;
            bool on = StoryStateManager.HasFlag(ProjectorPanel.BulbDone);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", on ? new Color(1f, 0.75f, 0.35f) * 2.5f : Color.black);
        }
    }
}
