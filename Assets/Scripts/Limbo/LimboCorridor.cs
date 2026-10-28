using UnityEngine;
using System.Collections;

/// <summary>
/// The narrow corridor after the hotel, and the Game Boy room at its end (same scene, no loading). Nobody moves
/// while the dark lasts (the riser plays); when the lights come on the whispering starts. Once the door is open the
/// room's lights come on, and as Nabil walks in the whispers fade, the fog and ambient change to the room's, and the
/// Unknown Number starts texting.
/// Pressed Play straight in this scene? It sets itself up as if Nabil had arrived.
/// </summary>
public class LimboCorridor : MonoBehaviour
{
    [SerializeField] private AudioSource whisper;
    [Range(0f, 1f)] [SerializeField] private float whisperVolume = 0.6f;
    [SerializeField] private LimboDoor door;
    [Tooltip("The room's lights: off until the door opens, so they do not shine through the wall.")]
    [SerializeField] private GameObject roomLights;
    [Tooltip("Walking from z=From to z=To fades corridor into room.")]
    [SerializeField] private float blendFromZ = 12.6f, blendToZ = 14.6f;
    [SerializeField] private float enteredZ = 13.3f;

    private static readonly Color CorridorFog = new Color(0.02f, 0.025f, 0.02f), RoomFog = new Color(0.02f, 0.03f, 0.07f);
    private static readonly Color CorridorAmbient = new Color(0.03f, 0.035f, 0.03f), RoomAmbient = new Color(0.035f, 0.04f, 0.07f);
    private const float CorridorDensity = 0.09f, RoomDensity = 0.025f;

    private Transform player;
    private bool inRoom;

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
        if (roomLights != null && door != null && door.Opened && !roomLights.activeSelf) roomLights.SetActive(true);

        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
        float k = 0f;
        if (player != null)
        {
            k = Mathf.InverseLerp(blendFromZ, blendToZ, player.position.z);
            if (!inRoom && player.position.z > enteredZ) { inRoom = true; PhoneUI.GameBoyRoomEntered(); }
        }

        RenderSettings.fogColor = Color.Lerp(CorridorFog, RoomFog, k);
        RenderSettings.fogDensity = Mathf.Lerp(CorridorDensity, RoomDensity, k);
        RenderSettings.ambientLight = Color.Lerp(CorridorAmbient, RoomAmbient, k);

        if (whisper != null)
        {
            whisper.volume = whisperVolume * (1f - k) * AudioLevels.AmbientVolume;
            if (k >= 1f && whisper.isPlaying) whisper.Stop();
        }
    }
}
