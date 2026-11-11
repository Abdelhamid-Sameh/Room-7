using UnityEngine;

/// <summary>
/// Safety net for every room: remembers the last spot the player stood on solid ground and, if they ever drop
/// well below it (a gap in a floor, a room that unloaded under their feet), puts them back there.
/// Creates itself, so no scene needs it added.
/// </summary>
public class FallRescue : MonoBehaviour
{
    private const float DropLimit = 4f;      // metres below the last safe spot that counts as falling
    private CharacterController cc;
    private Vector3 safe;
    private bool haveSafe;
    private float nextSave;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject("FallRescue");
        DontDestroyOnLoad(go);
        go.AddComponent<FallRescue>();
    }

    private void LateUpdate()
    {
        if (cc == null)
        {
            cc = FindFirstObjectByType<CharacterController>();
            haveSafe = false;
            if (cc == null) return;
        }
        if (!cc.enabled) return;   // spawning / cutscene

        Vector3 p = cc.transform.position;
        if (cc.isGrounded && Time.time >= nextSave) { safe = p; haveSafe = true; nextSave = Time.time + 0.5f; }
        if (haveSafe && p.y < safe.y - DropLimit)
        {
            Debug.LogWarning("[FallRescue] player fell to " + p + ", back to " + safe);
            cc.enabled = false;
            cc.transform.position = safe + Vector3.up * 0.1f;
            cc.enabled = true;
        }
    }
}
