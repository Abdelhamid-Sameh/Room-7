using UnityEngine;

public class GameSettings : MonoBehaviour
{
    // Every room scene has its own player, so Start runs on each load. The display mode is set once per run;
    // setting it again on every scene load made the window flash when a door was opened.
    private static bool displaySet;

    void Start()
    {
        if (!displaySet)
        {
            displaySet = true;
            // Borderless fullscreen at the desktop resolution: no display-mode switch, so no flicker.
            Resolution r = Screen.currentResolution;
            Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
        }
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { displaySet = false; }

    // Esc used to quit the game here. It now opens the pause menu instead (see PauseMenu),
    // which has the Quit button.
}
