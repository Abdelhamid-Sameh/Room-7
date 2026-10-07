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
///
/// Hold interactions (wiping, smoothing...): return a HoldDuration above 0. The player then has to
/// hold the button; OnHold reports 0..1 progress every frame and Interact is called once it reaches 1.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private string prompt = "Interact";

    /// <summary>Text shown under the crosshair while this is targeted.</summary>
    public virtual string Prompt => string.IsNullOrWhiteSpace(prompt) ? "Interact" : prompt;

    /// <summary>Return false to grey this out (e.g. a locker door hiding its contents).</summary>
    public virtual bool CanInteract => true;

    /// <summary>The key shown next to the prompt. Empty hides it (a locked door), 'hold E' for hold interactions.</summary>
    public virtual string KeyHint => "E";

    /// <summary>0 = a tap. Above 0 the player has to HOLD interact for this many seconds.</summary>
    public virtual float HoldDuration => 0f;

    /// <summary>Hold interactions: progress 0..1, every frame while the player holds.</summary>
    public virtual void OnHold(float progress) { }

    /// <summary>Hold interactions: the player let go or looked away before it finished.</summary>
    public virtual void OnHoldCancelled() { }

    /// <summary>Called on the frame the player presses interact while looking at this (or when a hold completes).</summary>
    public abstract void Interact(PlayerInteraction interactor);
}
