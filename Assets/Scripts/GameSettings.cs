using UnityEngine;

public class GameSettings : MonoBehaviour
{
    void Start()
    {
        Screen.SetResolution(1920, 1080, FullScreenMode.ExclusiveFullScreen);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }

    // Esc used to quit the game here. It now opens the pause menu instead (see PauseMenu),
    // which has the Quit button.
}
