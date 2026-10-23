using UnityEngine;

/// <summary>
/// The one switch for the hotel's power. Cutting it darkens every room (PowerReactor), silences the street,
/// plays the scary hallway loop (AmbientAudio) and kills the radio and its overlay. Restoring puts everything
/// back as it was, so it can be done again and again: call it from code, from a PowerTrigger, or from the
/// Room 7 menu while testing in play mode.
/// </summary>
public static class HotelPower
{
    public static bool IsOut { get { return GameState.PowerOut; } }

    /// <summary>
    /// Cut (dark = true) or restore (false) the power. A cut is refused while the player has no flashlight,
    /// so the hotel never goes dark on someone who cannot see; pass needFlashlight = false to force it.
    /// Returns true when the power is now in the requested state.
    /// </summary>
    public static bool Set(bool dark, bool needFlashlight = true)
    {
        if (GameState.PowerOut == dark) return true;
        if (dark && needFlashlight && !GameState.HasFlashlight) return false;
        GameState.PowerOut = dark;
        GameState.RaiseChanged();
        return true;
    }

    public static bool Cut(bool needFlashlight = true) { return Set(true, needFlashlight); }
    public static bool Restore() { return Set(false); }
    public static bool Toggle(bool needFlashlight = true) { return Set(!GameState.PowerOut, needFlashlight); }
}
