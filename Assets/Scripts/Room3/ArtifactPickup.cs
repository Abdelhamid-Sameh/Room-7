using UnityEngine;

/// <summary>An artefact lying in the world (the Game Boy or the rock inside the safe). Taking it files it in the pause menu's Artefacts tab.</summary>
public class ArtifactPickup : Interactable
{
    [SerializeField] private string artifactId = "";

    public override string Prompt { get { return "Take the " + GameState.ArtifactTitle(artifactId); } }

    public override void Interact(PlayerInteraction interactor)
    {
        GameState.AddArtifact(artifactId);
        if (GameState.R3SafeItem == artifactId) GameState.R3SafeItem = "";
        PhoneUI.Hint("Artefact found", GameState.ArtifactTitle(artifactId));
        gameObject.SetActive(false);
    }
}
