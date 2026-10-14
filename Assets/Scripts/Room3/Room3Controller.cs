using Unity.Cinemachine;
using UnityEngine;

/// <summary>Room 3 logic that lives in the scene: snappier camera cuts for the safe close-up.</summary>
public class Room3Controller : MonoBehaviour
{
    private void Start()
    {
        Camera cam = Camera.main;
        CinemachineBrain brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.9f);
    }
}
