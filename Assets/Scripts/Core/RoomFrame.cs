using UnityEngine;

/// <summary>
/// Sits on the holder a streamed room hangs under (RoomStreamer-style door code moves the whole room by moving this).
/// Room scripts that keep world-space numbers in the scene ask it to translate them; with no frame (the room
/// opened on its own) they come back unchanged.
/// </summary>
public class RoomFrame : MonoBehaviour
{
    public static Vector3 Dir(Component c, Vector3 v)
    {
        RoomFrame f = c.GetComponentInParent<RoomFrame>();
        return f != null ? f.transform.TransformDirection(v) : v;
    }

    /// <summary>A world position expressed in the room's own authored coordinates.</summary>
    public static Vector3 ToLocal(Component c, Vector3 p)
    {
        RoomFrame f = c.GetComponentInParent<RoomFrame>();
        return f != null ? f.transform.InverseTransformPoint(p) : p;
    }
}
