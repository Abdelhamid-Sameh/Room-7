using UnityEngine;
using UnityEngine.InputSystem;

public class GameSettings : MonoBehaviour
{
    void Start()
    {
        Screen.SetResolution(1920, 1080, FullScreenMode.ExclusiveFullScreen);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Application.Quit();
        }
    }
}