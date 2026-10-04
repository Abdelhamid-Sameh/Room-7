using UnityEngine;

/// <summary>
/// Anything that can be picked up and held centred on screen until the
/// player presses interact again to put it back exactly where it came from.
/// </summary>
public interface ICarryable
{
    bool IsHeld { get; }

    /// <summary>Move to the hold point, remember where "back" is, disable colliders.</summary>
    void PickUp(Transform holdPoint);

    /// <summary>Return to the original parent / local position, re-enable colliders.</summary>
    void PutDown();
}
