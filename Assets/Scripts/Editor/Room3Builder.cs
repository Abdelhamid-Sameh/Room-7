using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Room03 scene: four 5 x 5 m rooms in a ring (A cleaning, B safe, C cleaning, D twisted) joined by
/// hinged doors, with the way back to the corridor in room A. Re-running it rebuilds everything except the
/// player / spawn / game-systems objects.
///
///      z=10 +--------+--------+
///           |   A    |   B    |      A -> B -> C -> D -> (back to A)
///      z=5  +--------+--------+
///           |   D    |   C    |
///      z=0  +--------+--------+
///          x=0      x=5     x=10
/// </summary>
public static partial class Room3Builder
{
    const float H = 2.9f, T = 0.1f, DoorW = 1.16f, DoorH = 2.2f;
    const string WallTex = "Assets/YughuesFreeFabricMaterials/Textures/T_YFFaM_Burlap01_d.tga";
    const string CeilTex = "Assets/YughuesFreeFabricMaterials/Textures/T_YFFaM_Burlap02_d.tga";
    const string CarpetTex = "Assets/YughuesFreeFabricMaterials/Textures/T_YFFaM_Carpet02_d.tga";
    const string DoorPrefab = "Assets/Free Wood Door Pack/Prefab/Wood/Door_4/Door_4_Brown.prefab";
    const string ScenePath = "Assets/Scenes/Room03.unity";

    static Transform root, shell, lighting, props;
    static Material fixture;

    static float R1(float v) { return Mathf.Round(v * 10f) / 10f; }

    // ------------------------------------------------------------------ shell --

    struct RoomSpec
    {
        public string id;
        public float x0, z0, x1, z1;
        public Color wall, floor, ceil;
        public float[] west, east, south, north;   // door centres along each wall (z for west/east, x for south/north)
    }

    static Material WallMat(string id, Color tint, float u, float v)
    {
        return RoomKit.Tiled("R3" + id + "_Wall", WallTex, tint, R1(u), R1(v), 1.2f, 0.05f);
    }

    static void BuildShell(RoomSpec r)
    {
        Transform g = RoomKit.Group("Room" + r.id, shell).transform;
        float sx = r.x1 - r.x0, sz = r.z1 - r.z0;

        Material floor = RoomKit.Tiled("R3" + r.id + "_Floor", CarpetTex, r.floor, R1(sx), R1(sz), 1.0f, 0.05f);
        Material ceil = RoomKit.Tiled("R3" + r.id + "_Ceiling", CeilTex, r.ceil, R1(sx), R1(sz), 1.2f, 0.05f);
        RoomKit.Box("Floor", g, new Vector3((r.x0 + r.x1) / 2f, -0.05f, (r.z0 + r.z1) / 2f), new Vector3(sx, 0.1f, sz), floor);
        RoomKit.Box("Ceiling", g, new Vector3((r.x0 + r.x1) / 2f, H + 0.05f, (r.z0 + r.z1) / 2f), new Vector3(sx, 0.1f, sz), ceil);

        // Walls: one slab per side; the south / north slabs run the full width, west / east fit between them.
        // Wall() builds each piece with a material sized to the piece, so the burlap has the same density everywhere.
        BuildSide(g, r, true, r.z0 + T / 2f, r.x0, r.x1, r.south, "South");
        BuildSide(g, r, true, r.z1 - T / 2f, r.x0, r.x1, r.north, "North");
        BuildSide(g, r, false, r.x0 + T / 2f, r.z0 + T, r.z1 - T, r.west, "West");
        BuildSide(g, r, false, r.x1 - T / 2f, r.z0 + T, r.z1 - T, r.east, "East");
    }

    static void BuildSide(Transform p, RoomSpec r, bool alongX, float fixedC, float from, float to, float[] doors, string name)
    {
        Transform g = RoomKit.Group("Wall_" + name, p).transform;
        List<float[]> parts = new List<float[]>();
        float cursor = from;
        if (doors != null)
        {
            System.Array.Sort(doors);
            foreach (float c in doors)
            {
                parts.Add(new[] { cursor, c - DoorW / 2f, 0f, H });
                parts.Add(new[] { c - DoorW / 2f, c + DoorW / 2f, DoorH, H });
                cursor = c + DoorW / 2f;
            }
        }
        parts.Add(new[] { cursor, to, 0f, H });

        int i = 0;
        foreach (float[] s in parts)
        {
            float len = s[1] - s[0], h = s[3] - s[2];
            if (len < 0.01f) continue;
            float mid = (s[0] + s[1]) / 2f, y = (s[2] + s[3]) / 2f;
            Vector3 c = alongX ? new Vector3(mid, y, fixedC) : new Vector3(fixedC, y, mid);
            Vector3 size = alongX ? new Vector3(len, h, T) : new Vector3(T, h, len);
            RoomKit.Box("Piece_" + (i++), g, c, size, WallMat(r.id, r.wall, len, h));
        }
    }

    // ------------------------------------------------------------------ pieces --

    static void Lamp(string name, Vector3 pos, Color color, float intensity = 3f, float range = 5.2f)
    {
        GameObject g = RoomKit.Group(name, lighting);
        g.transform.position = pos;
        RoomKit.Box("Fixture", g.transform, pos + new Vector3(0f, 0.28f, 0f), new Vector3(0.7f, 0.05f, 0.22f), fixture, false);
        GameObject lg = new GameObject("Light"); lg.transform.SetParent(g.transform, false);
        lg.transform.position = pos;
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = range; l.intensity = intensity;
        l.color = color; l.shadows = LightShadows.None;
    }

    static GameObject Door(string name, Vector3 pos, float yaw, string hingeId)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab);
        GameObject d = (GameObject)PrefabUtility.InstantiatePrefab(asset, props);
        PrefabUtility.UnpackPrefabInstance(d, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        d.name = name; d.transform.position = pos; d.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        d.transform.localScale = Vector3.one * 1.01f;
        Transform leaf = d.transform.Find("Door");
        foreach (var c in leaf.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(c);   // the pack's own Door script

        if (hingeId != null)
        {
            HingeDoor hd = leaf.gameObject.AddComponent<HingeDoor>();
            SerializedObject so = new SerializedObject(hd);
            so.FindProperty("leaf").objectReferenceValue = leaf;
            so.FindProperty("doorId").stringValue = hingeId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            DoorTransition dt = leaf.gameObject.AddComponent<DoorTransition>();
            dt.SetDestination("HotelFloor_Corridor", "Room03_Door", "Leave the room", false);
        }
        return d;
    }

    static Material Mat(string name, Color c, float smooth = 0.4f, float metal = 0f, Color? emission = null)
    {
        return RoomKit.Plain(name, c, smooth, metal, emission);
    }

    static void MapMaterials(GameObject go, Dictionary<string, Material> map)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] src = r.sharedMaterials; Material[] dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Material m; dst[i] = src[i] != null && map.TryGetValue(src[i].name, out m) ? m : src[i];
            }
            r.sharedMaterials = dst;
        }
    }

    static GameObject Model(string file, string name, Transform parent, Vector3 pos, float yaw, float scale, Dictionary<string, Material> map)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Room3/" + file + ".fbx");
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f); go.transform.localScale = Vector3.one * scale;
        if (map != null) MapMaterials(go, map);
        return go;
    }

    // ------------------------------------------------------------------- safe --

    static Dictionary<string, Material> SafeMats()
    {
        var d = new Dictionary<string, Material>();
        d["SF_Body"] = Mat("R3_SfBody", new Color(0.16f, 0.18f, 0.17f), 0.45f, 0.6f);
        d["SF_Door"] = Mat("R3_SfDoor", new Color(0.21f, 0.23f, 0.22f), 0.4f, 0.7f);
        d["SF_Chrome"] = Mat("R3_SfChrome", new Color(0.75f, 0.76f, 0.78f), 0.2f, 1f);
        d["SF_Keypad"] = Mat("R3_SfKeypad", new Color(0.04f, 0.04f, 0.05f), 0.3f, 0.2f);
        d["SF_Display"] = Mat("R3_SfDisplay", new Color(0.1f, 0.4f, 0.15f), 0.2f, 0f, new Color(0.15f, 0.9f, 0.3f) * 1.4f);
        d["SF_Button"] = Mat("R3_SfButton", new Color(0.78f, 0.78f, 0.76f), 0.4f, 0.3f);
        d["SF_Inner"] = Mat("R3_SfInner", new Color(0.08f, 0.08f, 0.09f), 0.9f);
        return d;
    }

    static Dictionary<string, Material> ItemMats()
    {
        var d = new Dictionary<string, Material>();
        d["GB_Red"] = Mat("R3_GbRed", new Color(0.78f, 0.05f, 0.07f), 0.4f);
        d["GB_Bezel"] = Mat("R3_GbBezel", new Color(0.35f, 0.35f, 0.38f), 0.4f);
        d["GB_Screen"] = Mat("R3_GbScreen", new Color(0.35f, 0.42f, 0.25f), 0.2f);
        d["GB_Magenta"] = Mat("R3_GbMagenta", new Color(0.55f, 0.06f, 0.25f), 0.35f);
        d["GB_Dark"] = Mat("R3_GbDark", new Color(0.10f, 0.10f, 0.11f), 0.5f);
        d["RK_Grey"] = Mat("R3_RkGrey", new Color(0.36f, 0.35f, 0.33f), 0.9f);
        return d;
    }

    static void BuildSafe(Vector3 pos, float yaw)
    {
        GameObject safe = Model("Safe", "Safe", props, pos, yaw, 1f, SafeMats());
        Transform door = safe.transform.Find("Safe_Door");

        // Colliders: the body is hollow (a mesh collider, so the ray can reach what is inside once the door is open),
        // the door plate is a box, and one invisible solid on layer 9 stops the player walking into it.
        Transform body = safe.transform.Find("Safe_Body");
        MeshCollider mc = body.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
        RoomKit.FitCollider(door.Find("Safe_DoorPlate").gameObject);

        GameObject solid = new GameObject("Solid");
        solid.transform.SetParent(safe.transform, false);
        solid.layer = 9;
        BoxCollider sc = solid.AddComponent<BoxCollider>();
        sc.center = new Vector3(0f, 0.5f, -0.325f); sc.size = new Vector3(0.8f, 1f, 0.65f);

        // what is inside
        var items = ItemMats();
        GameObject gb = Model("GameBoy", "Item_GameBoy", safe.transform, Vector3.zero, 0f, 1f, items);
        gb.transform.localPosition = new Vector3(0.02f, 0.085f, -0.26f);
        gb.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
        RoomKit.FitCollider(gb);
        gb.AddComponent<ArtifactPickup>();
        SetString(gb.GetComponent<ArtifactPickup>(), "artifactId", "GameBoy");

        GameObject rk = Model("Rock", "Item_Rock", safe.transform, Vector3.zero, 0f, 0.6f, items);
        rk.transform.localPosition = new Vector3(0f, 0.08f, -0.3f);
        rk.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
        RoomKit.FitCollider(rk);
        rk.AddComponent<ArtifactPickup>();
        SetString(rk.GetComponent<ArtifactPickup>(), "artifactId", "Rock");

        // close-up camera, in front of the door
        GameObject camGo = new GameObject("SafeCloseUp");
        camGo.transform.SetParent(props, false);
        camGo.transform.position = safe.transform.TransformPoint(new Vector3(-0.6f, 1.0f, 1.7f));
        camGo.transform.rotation = Quaternion.LookRotation(-safe.transform.forward) * Quaternion.Euler(14f, 0f, 0f);
        CinemachineCamera vcam = camGo.AddComponent<CinemachineCamera>();
        vcam.Lens = new LensSettings { FieldOfView = 45f, NearClipPlane = 0.05f, FarClipPlane = 100f, ModeOverride = LensSettings.OverrideModes.None };
        vcam.Priority.Enabled = true; vcam.Priority.Value = 0;

        SafeBox sb = safe.AddComponent<SafeBox>();
        SerializedObject so = new SerializedObject(sb);
        so.FindProperty("door").objectReferenceValue = door;
        so.FindProperty("doorOpenAngle").floatValue = 110f;
        so.FindProperty("closeUp").objectReferenceValue = vcam;
        so.FindProperty("gameBoy").objectReferenceValue = gb;
        so.FindProperty("rock").objectReferenceValue = rk;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetString(Object o, string field, string value)
    {
        SerializedObject so = new SerializedObject(o);
        so.FindProperty(field).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ build --

    public static string Build()
    {
        if (!System.IO.File.Exists(ScenePath)) AssetDatabase.CopyAsset("Assets/Scenes/StaffRoom.unity", ScenePath);
        var sc = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (GameObject g in sc.GetRootGameObjects())
            if (g.name != "GameSystems" && g.name != "SpawnPoints" && g.name != "Player") Object.DestroyImmediate(g);

        fixture = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RoomSeven/RS_LightFixture_Emissive.mat");

        GameObject rootGo = new GameObject("Room03_Root");
        root = rootGo.transform;
        shell = RoomKit.Group("Shell", root).transform;
        lighting = RoomKit.Group("Lighting", root).transform;
        props = RoomKit.Group("Props", root).transform;

        // ---- the four rooms ----
        BuildShell(new RoomSpec
        {
            id = "A", x0 = 0f, z0 = 5f, x1 = 5f, z1 = 10f,
            wall = new Color(0.88f, 0.82f, 0.70f), floor = new Color(0.55f, 0.62f, 0.50f), ceil = new Color(0.92f, 0.88f, 0.78f),
            west = new[] { 8.4f }, east = new[] { 6.2f }, south = new[] { 1.8f }, north = null
        });
        BuildShell(new RoomSpec
        {
            id = "B", x0 = 5f, z0 = 5f, x1 = 10f, z1 = 10f,
            wall = new Color(0.62f, 0.68f, 0.64f), floor = new Color(0.30f, 0.32f, 0.34f), ceil = new Color(0.55f, 0.58f, 0.58f),
            west = new[] { 6.2f }, east = null, south = new[] { 8.5f }, north = null
        });
        BuildShell(new RoomSpec
        {
            id = "C", x0 = 5f, z0 = 0f, x1 = 10f, z1 = 5f,
            wall = new Color(0.72f, 0.62f, 0.52f), floor = new Color(0.45f, 0.36f, 0.30f), ceil = new Color(0.70f, 0.64f, 0.56f),
            west = new[] { 3.0f }, east = null, south = null, north = new[] { 8.5f }
        });
        BuildShell(new RoomSpec
        {
            id = "D", x0 = 0f, z0 = 0f, x1 = 5f, z1 = 5f,
            wall = new Color(0.60f, 0.50f, 0.72f), floor = new Color(0.50f, 0.40f, 0.62f), ceil = new Color(0.62f, 0.54f, 0.72f),
            west = null, east = new[] { 3.0f }, south = null, north = new[] { 1.8f }
        });

        // ---- doors ----
        Door("Door_Entry", new Vector3(0.05f, 0f, 8.4f), 90f, null);        // back to the corridor
        Door("Door_AB", new Vector3(5f, 0f, 6.2f), 90f, "R3_AB");
        Door("Door_BC", new Vector3(8.5f, 0f, 5f), 0f, "R3_BC");
        Door("Door_CD", new Vector3(5f, 0f, 3.0f), 90f, "R3_CD");
        Door("Door_DA", new Vector3(1.8f, 0f, 5f), 0f, "R3_DA");

        // ---- lights ----
        Lamp("Lamp_A", new Vector3(2.5f, 2.6f, 7.5f), new Color(1f, 0.92f, 0.76f));
        Lamp("Lamp_B", new Vector3(7.5f, 2.6f, 7.5f), new Color(0.85f, 0.92f, 1f), 2.8f);
        Lamp("Lamp_C", new Vector3(7.5f, 2.6f, 2.5f), new Color(1f, 0.82f, 0.62f), 2.6f);
        Lamp("Lamp_D", new Vector3(2.5f, 2.6f, 2.5f), new Color(0.8f, 0.65f, 1f), 2.8f);

        // ---- the safe, against B's east wall, facing west ----
        BuildSafe(new Vector3(9.9f, 0f, 8.0f), -90f);

        GuestRooms();
        BuildTwisted();

        // ---- spawn + logic ----
        SpawnPoint sp = Object.FindFirstObjectByType<SpawnPoint>();
        sp.SetSpawnId("FromCorridor");
        sp.transform.SetPositionAndRotation(new Vector3(1.4f, 0f, 8.4f), Quaternion.Euler(0f, 90f, 0f));

        GameObject logic = new GameObject("Room3Logic");
        logic.AddComponent<Room3Controller>();
        TidyRoom tr = logic.AddComponent<TidyRoom>();
        SerializedObject tso = new SerializedObject(tr);
        tso.FindProperty("roomId").stringValue = "Room03";
        tso.FindProperty("displayName").stringValue = "Room 3";
        tso.ApplyModifiedPropertiesWithoutUndo();

        RenderSettings.ambientLight = new Color(0.11f, 0.095f, 0.085f);
        RenderSettings.ambientIntensity = 1f;

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return "built Room03";
    }

    /// <summary>Corridor side: a return spawn in front of the Room 3 door.</summary>
    public static string PatchCorridor()
    {
        var sc = EditorSceneManager.OpenScene("Assets/Scenes/HotelFloor_Corridor.unity", OpenSceneMode.Single);
        SpawnPoint template = null;
        foreach (SpawnPoint s in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
        {
            if (s.name == "Spawn_Room03_Door") { Object.DestroyImmediate(s.gameObject); continue; }
        }
        foreach (SpawnPoint s in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            if (s.name == "Spawn_Room02_Door") template = s;

        GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
        copy.name = "Spawn_Room03_Door";
        copy.GetComponent<SpawnPoint>().SetSpawnId("Room03_Door");
        copy.transform.SetPositionAndRotation(new Vector3(-1.0f, 0f, 6.31f), Quaternion.Euler(0f, 270f, 0f));

        foreach (DoorTransition d in Object.FindObjectsByType<DoorTransition>(FindObjectsSortMode.None))
            if (d.TargetScene == "Room03") d.SetDestination("Room03", "FromCorridor", null, true);

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        return "corridor patched";
    }
}
