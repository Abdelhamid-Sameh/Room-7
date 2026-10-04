using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central place for the small number of "meta" inputs (interact, radio).
///
/// Movement / look / jump / sprint keep going through Starter Assets'
/// PlayerInput + StarterAssetsInputs - only the new actions live here so they
/// never fight with the existing wiring.
///
/// TODO: if you later want rebinding, swap the members below for
/// InputActionReferences created from StarterAssets.inputactions.
/// </summary>
public static class GameInput
{
    /// <summary>E on keyboard, Left Mouse Button, or Pad North (Y / Triangle).</summary>
    public static bool InteractPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) return true;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            Gamepad pad = Gamepad.current;
            if (pad != null && pad.buttonNorth.wasPressedThisFrame) return true;

            return false;
        }
    }

    /// <summary>R - turn the carried radio on / off.</summary>
    public static bool RadioPlayPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            return kb != null && kb.rKey.wasPressedThisFrame;
        }
    }

    /// <summary>T - next song.</summary>
    public static bool RadioNextPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            return kb != null && kb.tKey.wasPressedThisFrame;
        }
    }

    /// <summary>Y - previous song.</summary>
    public static bool RadioPrevPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            return kb != null && kb.yKey.wasPressedThisFrame;
        }
    }
}
