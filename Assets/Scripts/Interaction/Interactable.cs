using UnityEngine;

/// <summary>
/// Base class for anything the player can look at and trigger with
/// E / Left Mouse Button.
///
/// Put this on the GameObject that owns the collider (or on a parent of it -
/// PlayerInteraction uses GetComponentInParent).
///
/// Typical implementation:
/// <code>
/// public class MyThing : Interactable
/// {
///     public override string Prompt => "Do the thing";
///     public override void Interact(PlayerInteraction interactor) { ... }
/// }
/// </code>
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private string prompt = "Interact";

    /// <summary>Text shown under the crosshair while this is targeted.</summary>
    public virtual string Prompt => string.IsNullOrWhiteSpace(prompt) ? "Interact" : prompt;

    /// <summary>Return false to grey this out (e.g. a locker door hiding its contents).</summary>
    public virtual bool CanInteract => true;

    /// <summary>Called on the frame the player presses interact while looking at this.</summary>
    public abstract void Interact(PlayerInteraction interactor);
}
