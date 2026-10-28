using UnityEngine;
using System.Collections;

/// <summary>
/// The narrow corridor after the hotel. Nobody moves while the dark lasts (the riser plays); when the lights come
/// on the whispering starts, and it carries on until Nabil goes through the door (the scene unloads with it).
/// Pressed Play straight in this scene? It sets itself up as if Nabil had arrived.
/// </summary>
public class LimboCorridor : MonoBehaviour
{
    [SerializeField] private AudioSource whisper;
    [Range(0f, 1f)] [SerializeField] private float whisperVolume = 0.6f;

    private IEnumerator Start()
    {
        bool arrivedInDark = GameState.Limbo && !GameState.LimboLit;
        if (!GameState.Limbo) { GameState.Limbo = true; GameState.LimboLit = true; GameState.RaiseChanged(); }   // straight into this scene from the editor

        MonoBehaviour ctrl = PauseMenu.FindPlayerController();
        if (arrivedInDark && ctrl != null) ctrl.enabled = false;
        while (!GameState.LimboLit) yield return null;
        if (arrivedInDark && ctrl != null)
        {
            PauseMenu.ClearStaleInput();
            ctrl.enabled = true;
        }

        if (whisper != null) whisper.Play();
    }

    private void Update()
    {
        if (whisper != null) whisper.volume = whisperVolume * AudioLevels.AmbientVolume;
    }
}
