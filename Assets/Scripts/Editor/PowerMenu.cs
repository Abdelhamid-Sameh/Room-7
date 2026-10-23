using UnityEditor;
using UnityEngine;

/// <summary>Test the blackout without playing through Room 2: Room 7 > Toggle Power (in play mode).</summary>
public static class PowerMenu
{
    [MenuItem("Room 7/Toggle Power (play mode)")]
    private static void Toggle()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Power] Enter play mode first."); return; }
        HotelPower.Toggle(false);
    }
}
