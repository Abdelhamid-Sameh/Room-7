using UnityEngine;

/// <summary>
/// A paper or note the player can pick up and read. 'Resource Paths' are images under a Resources folder
/// (for example "Room1/poem1"); more than one makes a multi-page document. Reading it records a clue.
/// </summary>
public class DocumentItem : Interactable
{
    [SerializeField] private string title = "Note";
    [SerializeField] private string readPrompt = "Read";
    [SerializeField] private string[] resourcePaths = new string[0];
    [Tooltip("Optional clue id added to GameState.Clues the first time this is read.")]
    [SerializeField] private string clueId = "";

    public override string Prompt => readPrompt;

    public override void Interact(PlayerInteraction interactor)
    {
        var loaded = new System.Collections.Generic.List<Texture2D>();
        foreach (string path in resourcePaths)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null) loaded.Add(tex);
            else Debug.LogWarning("DocumentItem: no image at Resources/" + path, this);
        }
        if (loaded.Count == 0) return;

        if (!string.IsNullOrEmpty(clueId)) GameState.AddClue(clueId);
        DocumentViewer.Show(title, loaded.ToArray());
    }
}
