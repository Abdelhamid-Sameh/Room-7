using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A corridor door that streams its room in, so walking into a room never shows a loading screen.
///
///  - Come within Load Radius and the room scene loads in the background (additively, hidden).
///  - E swings the door open and the room is shown behind it; the swing covers any last bit of work.
///  - Walk back out and get further than Unload Radius and the door swings shut, the room is unloaded.
///  - Only one room is open at a time (opening a door shuts the others), so rooms never overlap.
///
/// The room scene still contains its own Player / GameSystems / entrance door: on load the extra Player is
/// removed, the room's own entrance door is hidden (this door replaces it), and the whole room is turned and
/// moved so its entrance sits exactly on this doorway.
///
/// A room with a second door back to the corridor (Room 2, the looping corridor) is handled like this: once the
/// player is round the corner and can no longer see the way they came in, this door shuts and the WHOLE CORRIDOR
/// is moved so that it sits outside the room's far door. Walking out of that door is the loop.
///
/// The leaf's pivot is on the hinge and its parent is the door frame, whose forward points into the room.
/// </summary>
[RequireComponent(typeof(Collider))]
public class StreamDoor : Interactable
{
    [SerializeField] private string sceneName = "";
    [SerializeField] private bool gatedByRoomAccess = true;
    [SerializeField] private string lockedLabel = "Locked";
    [SerializeField] private float loadRadius = 9f;
    [SerializeField] private float unloadRadius = 13f;
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float seconds = 0.8f;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    private const string CorridorScene = "HotelFloor_Corridor";
    // shortcut: the corner of Room 2's L is hard-coded (room-local metres), move it into a field if another looping room appears.
    private const float CornerX = 2f, CornerZ = 8f;

    private enum Stage { Idle, Loading, Ready }

    private static readonly List<StreamDoor> all = new List<StreamDoor>();
    private static Transform corridorHolder;   // everything in the corridor scene except the player, once it has been moved
    private static bool shifting;
    private static readonly List<GameObject> hiddenParts = new List<GameObject>();   // the corridor, switched off while the player is inside Room 2
    private static float floorY = float.NaN;   // the corridor floor height, remembered from the start so the small offsets below never add up

    private Stage stage;
    private Scene room;
    private Transform holder, frame, player, entrance, exitDoor;
    private bool opened, busy, pendingOpen, wasInside, shifted, unloadOnLoad;
    private float angle;

    public bool IsLocked { get { return gatedByRoomAccess && !RoomAccess.IsOpen(sceneName); } }

    public override string Prompt { get { return IsLocked ? lockedLabel : (opened ? "Close the door" : "Open the door"); } }
    public override string KeyHint { get { return IsLocked ? "" : "E"; } }
    public override bool CanInteract { get { return !busy && !pendingOpen; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); corridorHolder = null; shifting = false; floorY = float.NaN; hiddenParts.Clear(); }

    private void Awake()
    {
        frame = transform.parent;
        if (float.IsNaN(floorY)) floorY = frame.position.y;
        all.Add(this);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        all.Remove(this);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (stage == Stage.Ready && room.IsValid() && room.isLoaded) SceneManager.UnloadSceneAsync(room);
    }

    private void Update()
    {
        if (player == null)
        {
            CharacterController cc = FindFirstObjectByType<CharacterController>();
            if (cc == null) return;
            player = cc.transform;
        }

        Vector3 d = player.position - frame.position;
        d.y = 0f;
        float dist = d.magnitude;
        bool inRoom = Vector3.Dot(d, frame.forward) > 0f && stage == Stage.Ready;

        if (inRoom && !wasInside) GameState.Visit(sceneName);               // 'enter the staff room' counts when you step in
        if (!inRoom && wasInside && dist > 1.5f) { PhoneUI.BackInCorridor(); if (sceneName == "Room03") PhoneUI.LeftRoom3(); }   // ...and stepping back out is the old 'back in the corridor'
        if (inRoom != wasInside && (inRoom || dist > 1.5f)) wasInside = inRoom;

        if (stage == Stage.Idle)
        {
            if (!shifting && !IsLocked && dist < loadRadius) Begin();
            return;
        }
        if (stage != Stage.Ready || busy) return;

        if (shifted)
        {
            // Out of the far door: that was the loop. The room is no use any more (the next visit starts at its front door again).
            if (!inRoom && dist > 1.2f)
            {
                GameState.CountLoop();
                StartCoroutine(ExitAndUnload());
            }
            return;
        }

        if (exitDoor != null && inRoom)
        {
            Vector3 lp = holder.InverseTransformPoint(player.position);
            if (lp.x > CornerX && lp.z > CornerZ) { StartCoroutine(ShiftCorridor()); return; }
        }

        if (!inRoom && dist > unloadRadius)
        {
            if (opened) StartCoroutine(CloseAndUnload());
            else Unload();
        }
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (IsLocked || busy) return;

        if (opened)
        {
            Vector3 d = player.position - frame.position;
            d.y = 0f;
            if (d.magnitude < 0.8f) return;   // standing in the doorway
            StartCoroutine(Swing(false));
            return;
        }

        if (stage == Stage.Ready) { StartCoroutine(Swing(true)); return; }

        pendingOpen = true;   // pressed before the room finished loading: it opens the moment it is ready
        Begin();
    }

    private void Begin()
    {
        if (stage != Stage.Idle) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName)) return;
        stage = Stage.Loading;
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    }

    // Runs after the room's Awake/OnEnable and before any Start: the one moment the room can be moved around
    // without its scripts having noticed.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Additive || stage != Stage.Loading || scene.name != sceneName) return;

        room = scene;
        var doors = new List<Transform>();
        SpawnPoint inside = null;
        var roots = new List<GameObject>(scene.GetRootGameObjects());

        foreach (GameObject r in roots)
        {
            foreach (DoorTransition dt in r.GetComponentsInChildren<DoorTransition>(true))
            {
                Transform f = dt.transform.parent != null ? dt.transform.parent : dt.transform;
                if (dt.TargetScene == CorridorScene && !doors.Contains(f)) doors.Add(f);
            }
            if (inside == null) inside = r.GetComponentInChildren<SpawnPoint>(true);
        }

        if (doors.Count == 0) { Debug.LogError("[StreamDoor] '" + sceneName + "' has no door back to the corridor."); stage = Stage.Idle; SceneManager.UnloadSceneAsync(scene); return; }

        // The entrance is the door nearest where the player starts inside; any other door back to the corridor is the exit.
        entrance = doors[0];
        if (inside != null)
            foreach (Transform f in doors)
                if ((f.position - inside.transform.position).sqrMagnitude < (entrance.position - inside.transform.position).sqrMagnitude) entrance = f;
        exitDoor = null;
        foreach (Transform f in doors) if (f != entrance) exitDoor = f;

        Vector3 intoRoom = inside != null ? inside.transform.position - entrance.position : entrance.forward;
        intoRoom.y = 0f;
        Vector3 wanted = frame.forward;
        wanted.y = 0f;
        Quaternion turn = Quaternion.Euler(0f, Vector3.SignedAngle(intoRoom, wanted, Vector3.up), 0f);
        Vector3 anchor = entrance.position;

        // Where the room's own entrance wall sits relative to its door frame, so it can be tucked inside the corridor wall.
        Vector3 axis = intoRoom.normalized, side = new Vector3(-axis.z, 0f, axis.x);
        float wallS = 0f, wallT = 0.1f, bestLat = 99f;
        foreach (GameObject r in roots)
            foreach (Renderer rd in r.GetComponentsInChildren<Renderer>(true))
            {
                if (rd.transform.IsChildOf(entrance)) continue;
                Bounds b = rd.bounds;
                if (b.size.x > 50f) continue;
                Vector3 rel = b.center - entrance.position;
                float sAlong = Vector3.Dot(rel, axis), lat = Mathf.Abs(Vector3.Dot(rel, side)), thick = Mathf.Abs(Vector3.Dot(b.size, axis));
                if (thick >= 0.05f && thick < 0.35f && Mathf.Abs(sAlong) < 0.5f && lat < 3.2f && b.max.y > 2.85f && lat < bestLat) { bestLat = lat; wallS = sAlong; wallT = thick; }
            }
        // the corridor wall is 0.2 thick centred on its frame: put the room wall's inner face 2 mm proud of the corridor wall's
        float push = 0.102f - wallT * 0.5f - wallS;

        // The room's own copies of the things the corridor already has.
        for (int i = roots.Count - 1; i >= 0; i--)
        {
            GameObject r = roots[i];
            if (r.GetComponentInChildren<CharacterController>(true) != null || r.GetComponent<GameSystems>() != null)
            {
                DestroyImmediate(r);
                roots.RemoveAt(i);
            }
        }
        entrance.gameObject.SetActive(false);   // this door takes its place

        GameObject h = new GameObject("Streamed_" + sceneName);
        SceneManager.MoveGameObjectToScene(h, scene);
        h.AddComponent<RoomFrame>();
        foreach (GameObject r in roots) r.transform.SetParent(h.transform, true);
        // a hair lower than the corridor, so the two floors never fight where they overlap under the wall
        Vector3 at = frame.position - turn * anchor + wanted.normalized * push;
        at.y = floorY - 0.003f - anchor.y;
        h.transform.SetPositionAndRotation(at, turn);
        h.SetActive(false);   // hidden and idle until the door opens

        holder = h.transform;
        stage = Stage.Ready;

        if (unloadOnLoad) { unloadOnLoad = false; Unload(); return; }
        if (pendingOpen) { pendingOpen = false; StartCoroutine(Swing(true)); }
    }

    private IEnumerator Swing(bool open, float duration = -1f, bool hideRoomHalfway = false)
    {
        busy = true;
        float from, to;
        if (duration <= 0f) duration = seconds;
        bool hidden = false;

        if (open)
        {
            if (shifted) SetCorridorVisible(true);   // the corridor is about to be seen through this door
            CloseOthers();
            holder.gameObject.SetActive(true);
            Vector3 toPlayer = player.position - frame.position;
            toPlayer.y = 0f;
            // positive angles swing the leaf towards the frame's forward (the room), so push it to the far side
            angle = Vector3.Dot(toPlayer, frame.forward) > 0f ? -openAngle : openAngle;
            from = 0f; to = angle;
        }
        else { from = angle; to = 0f; }

        AudioLevels.PlaySfx(open ? openClip : closeClip, transform.position);

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            transform.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(from, to, t), 0f);
            if (hideRoomHalfway && !hidden && t > 0.5f && holder != null) { holder.gameObject.SetActive(false); hidden = true; }
            yield return null;
        }
        transform.localRotation = Quaternion.Euler(0f, to, 0f);

        opened = open;
        busy = false;
    }

    // Opening a door shuts every other open room first, so two rooms are never on show in the same place.
    private void CloseOthers()
    {
        foreach (StreamDoor o in all)
            if (o != this && o.stage == Stage.Ready && o.opened && !o.busy) o.StartCoroutine(o.CloseAndUnload());
    }

    private IEnumerator CloseAndUnload()
    {
        if (opened) yield return Swing(false);
        Unload();
    }

    // Out of Room 2's far door: shut it quickly and hide the room before it can poke through the corridor it overlaps.
    private IEnumerator ExitAndUnload()
    {
        if (opened) yield return Swing(false, 0.35f, true);
        Unload();
    }

    private void Unload()
    {
        if (stage == Stage.Loading) { unloadOnLoad = true; return; }
        if (room.IsValid() && room.isLoaded) SceneManager.UnloadSceneAsync(room);
        stage = Stage.Idle;
        holder = null; entrance = null; exitDoor = null;
        opened = false; pendingOpen = false; shifted = false; busy = false;
        transform.localRotation = Quaternion.identity;
    }

    // Room 2: the player is round the corner. Shut the way in, then move the whole corridor to the far door.
    private IEnumerator ShiftCorridor()
    {
        shifting = true;
        busy = true;   // nothing else on this door while it happens
        if (opened) { busy = false; yield return Swing(false); busy = true; }

        foreach (StreamDoor o in all) if (o != this) { StopAllCoroutinesOf(o); o.Unload(); }

        // Behind the player the way in is now just a shut door in a wall.
        entrance.gameObject.SetActive(true);
        foreach (DoorTransition dt in entrance.GetComponentsInChildren<DoorTransition>(true)) Destroy(dt);
        exitDoor.gameObject.SetActive(false);   // the corridor's own door is the one in the doorway now

        Vector3 into = exitDoor.forward;
        into.y = 0f;
        if (Vector3.Dot(into, player.position - exitDoor.position) < 0f) into = -into;
        Vector3 want = frame.forward;
        want.y = 0f;
        Quaternion q = Quaternion.Euler(0f, Vector3.SignedAngle(want, into, Vector3.up), 0f);
        Vector3 pivot = frame.position;
        Vector3 target = exitDoor.position - into.normalized * 0.002f;   // 2 mm towards the corridor: its wall and the room's far wall never share a plane
        target.y = floorY - 0.006f;

        if (corridorHolder == null)
        {
            GameObject go = new GameObject("CorridorRoot");
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            corridorHolder = go.transform;
            foreach (GameObject r in gameObject.scene.GetRootGameObjects())
            {
                if (r == go || r.GetComponentInChildren<CharacterController>(true) != null || r.GetComponent<GameSystems>() != null) continue;
                r.transform.SetParent(corridorHolder, true);
            }
        }
        corridorHolder.rotation = q * corridorHolder.rotation;
        corridorHolder.position = target + q * (corridorHolder.position - pivot);
        Physics.SyncTransforms();
        SetCorridorVisible(false);   // the corridor now overlaps Room 2's own walls: keep it off until the far door opens

        shifted = true;
        busy = false;
        shifting = false;
    }

    // Everything in the corridor except this door (leaf, frame, knob) is switched off or back on.
    private void SetCorridorVisible(bool visible)
    {
        if (visible)
        {
            foreach (GameObject g in hiddenParts) if (g != null) g.SetActive(true);
            hiddenParts.Clear();
            return;
        }
        if (corridorHolder == null || hiddenParts.Count > 0) return;
        foreach (Transform root in corridorHolder) HideExceptDoor(root);
    }

    private void HideExceptDoor(Transform t)
    {
        if (t == frame || t.IsChildOf(frame)) return;                 // the door itself
        if (frame.IsChildOf(t)) { foreach (Transform c in t) HideExceptDoor(c); return; }   // a parent of the door: stays on, its other children go
        if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); hiddenParts.Add(t.gameObject); }
    }

    private static void StopAllCoroutinesOf(StreamDoor d) { d.StopAllCoroutines(); d.busy = false; }
}
