using System.Collections.Generic;
using RTLTMPro;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Room05 scene: an old classroom (4.9 x 8 m, narrow so it does not run into Room 1 next door) behind an
/// ordinary hotel-room door. The door is at the back of the class; two columns of numbered desks leave a wide aisle down
/// to the blackboard on the south wall, where somebody chalked a heart around "ع + هـ".
/// The puzzle ("who started the rumour?"): five notes the children passed are hidden around the room (a school bag, a
/// pencil case, a paper plane on the windowsill, a crumpled ball on the floor, the chalk tray). Each says who wrote it
/// and to whom, and the story grows each time. The seating chart by the door says who sits where; the desks carry
/// painted numbers. The teacher's drawer opens with the desk numbers in the order the rumour travelled (Room5Data.Code).
/// Models come from Assets/Models/Room5 (made in Blender). Menu: Room Seven / Build room 5. Re-running rebuilds
/// everything except Player / SpawnPoints / GameSystems, and adds the return spawn to the corridor.
/// </summary>
public static class Room5Builder
{
    const float W = 4.9f, D = 8f, H = 3.2f, T = 0.1f, DoorW = 1.16f, DoorH = 2.2f, DoorZ = 6.7f;
    const float SillY = 0.96f, HeadY = 2.54f, WinW = 1.2f;   // window openings, as in Room 1
    const string WallTex = "Assets/ThirdParty/YughuesFreeFabricMaterials/Textures/T_YFFaM_Burlap01_d.tga";
    const string CeilTex = "Assets/ThirdParty/YughuesFreeFabricMaterials/Textures/T_YFFaM_Burlap02_d.tga";
    const string DoorPrefab = "Assets/ThirdParty/Free Wood Door Pack/Prefab/Wood/Door_4/Door_4_Brown.prefab";
    const string WindowPrefab = "Assets/ThirdParty/nappin/OfficeEssentialsPack/Prefabs/(Prb)Window.prefab";
    const string ScenePath = "Assets/Scenes/Room05.unity";
    const string ModelDir = "Assets/Models/Room5/";
    const string BagDir = "Assets/Bags - Props for Interior/Bags - Props for Interior/models/prefab/";
    const string PropDir = "Assets/JustPlay/Household props/Prefabs/";
    const float DeskTop = 0.7575f;   // height of the school desk's top surface

    static Transform shell, lighting, props;
    static Material fixture;
    static Dictionary<string, Material> map;

    static float R1(float v) { return Mathf.Round(v * 10f) / 10f; }
    static Material Mat(string n, Color c, float s = 0.35f, float m = 0f, Color? e = null) { return RoomKit.Plain("R5_" + n, c, s, m, e); }
    static TMP_FontAsset Cairo { get { return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cairo/Cairo-Regular SDF.asset"); } }

    static void Set(Object o, string field, object v)
    {
        SerializedObject so = new SerializedObject(o);
        SerializedProperty p = so.FindProperty(field);
        if (v is string) p.stringValue = (string)v;
        else if (v is int) p.enumValueIndex = (int)v;
        else if (v is Object || v == null) p.objectReferenceValue = (Object)v;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------- models --

    static Dictionary<string, Material> ModelMats()
    {
        var d = new Dictionary<string, Material>();
        d["R5_DeskWood"] = RoomKit.Plain("R5M_DeskWood", new Color(0.36f, 0.22f, 0.11f), 0.45f);
        d["R5_DeskEdge"] = RoomKit.Plain("R5M_DeskEdge", new Color(0.16f, 0.10f, 0.06f), 0.5f);
        d["R5_Steel"] = RoomKit.Plain("R5M_Steel", new Color(0.13f, 0.13f, 0.14f), 0.5f, 0.8f);
        d["R5_TeacherWood"] = RoomKit.Plain("R5M_TeacherWood", new Color(0.30f, 0.17f, 0.08f), 0.4f);
        d["R5_TeacherDark"] = RoomKit.Plain("R5M_TeacherDark", new Color(0.14f, 0.08f, 0.05f), 0.5f);
        d["R5_Brass"] = RoomKit.Plain("R5M_Brass", new Color(0.75f, 0.58f, 0.25f), 0.7f, 1f);
        d["R5_DrawerInner"] = RoomKit.Plain("R5M_DrawerInner", new Color(0.22f, 0.13f, 0.07f), 0.5f);
        d["R5_TapeBody"] = RoomKit.Plain("R5M_TapeBody", new Color(0.10f, 0.10f, 0.11f), 0.4f);
        d["R5_TapeLabel"] = RoomKit.Plain("R5M_TapeLabel", new Color(0.88f, 0.82f, 0.62f), 0.5f);
        d["R5_TapeReel"] = RoomKit.Plain("R5M_TapeReel", new Color(0.85f, 0.85f, 0.82f), 0.5f);
        return d;
    }

    static GameObject Model(string file, string name, Transform parent, Vector3 pos, float yaw)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + file + ".fbx");
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] src = r.sharedMaterials, dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++) { Material m; dst[i] = src[i] != null && map.TryGetValue(src[i].name, out m) ? m : src[i]; }
            r.sharedMaterials = dst;
        }
        return go;
    }

    /// <summary>Top, seat and backrest only, so things lying on the desk can be looked at and picked up.</summary>
    static void DeskColliders(GameObject desk)
    {
        // model space: the desk top is towards -Z, the bench behind it towards +Z
        BoxCollider top = desk.AddComponent<BoxCollider>();
        top.center = new Vector3(0f, DeskTop - 0.02f, -0.42f); top.size = new Vector3(0.64f, 0.04f, 0.48f);
        BoxCollider seat = desk.AddComponent<BoxCollider>();
        seat.center = new Vector3(0f, 0.43f, 0.14f); seat.size = new Vector3(0.44f, 0.08f, 0.40f);
        BoxCollider back = desk.AddComponent<BoxCollider>();
        back.center = new Vector3(0f, 0.70f, 0.33f); back.size = new Vector3(0.42f, 0.22f, 0.04f);
    }

    // ---------------------------------------------------------------- shell --

    static Material WallMat(float u, float v) { return RoomKit.Tiled("R5_Wall", WallTex, new Color(0.50f, 0.58f, 0.52f), R1(u), R1(v), 1.2f, 0.05f); }

    /// <summary>A wall from 'from' to 'to' with openings {centre, width, bottom, top}, sorted by centre.</summary>
    static void Wall(string name, bool alongX, float fixedC, float from, float to, params float[][] holes)
    {
        Transform g = RoomKit.Group(name, shell).transform;
        List<float[]> parts = new List<float[]>();
        float at = from;
        foreach (float[] o in holes)
        {
            float a = o[0] - o[1] / 2f, b = o[0] + o[1] / 2f;
            parts.Add(new[] { at, a, 0f, H });
            if (o[2] > 0f) parts.Add(new[] { a, b, 0f, o[2] });
            parts.Add(new[] { a, b, o[3], H });
            at = b;
        }
        parts.Add(new[] { at, to, 0f, H });
        int i = 0;
        foreach (float[] s in parts)
        {
            float len = s[1] - s[0], h = s[3] - s[2];
            if (len < 0.01f || h < 0.01f) continue;
            float mid = (s[0] + s[1]) / 2f, y = (s[2] + s[3]) / 2f;
            Vector3 c = alongX ? new Vector3(mid, y, fixedC) : new Vector3(fixedC, y, mid);
            Vector3 size = alongX ? new Vector3(len, h, T) : new Vector3(T, h, len);
            RoomKit.Box("Piece_" + (i++), g, c, size, WallMat(len, h));
        }
    }

    static void Lamp(string name, Vector3 pos, Color color, float intensity)
    {
        GameObject g = RoomKit.Group(name, lighting);
        RoomKit.Box("Fixture", g.transform, pos + new Vector3(0f, 0.28f, 0f), new Vector3(0.9f, 0.05f, 0.18f), fixture, false);
        GameObject lg = new GameObject("Light"); lg.transform.SetParent(g.transform, false);
        lg.transform.position = pos;
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = 6.5f; l.intensity = intensity; l.color = color; l.shadows = LightShadows.None;
    }

    static void Door()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab);
        GameObject d = (GameObject)PrefabUtility.InstantiatePrefab(asset, props);
        PrefabUtility.UnpackPrefabInstance(d, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        d.name = "Door_Entry"; d.transform.position = new Vector3(0.05f, 0f, DoorZ); d.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        d.transform.localScale = Vector3.one * 1.01f;
        Transform leaf = d.transform.Find("Door");
        foreach (var c in leaf.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(c);
        DoorTransition dt = leaf.gameObject.AddComponent<DoorTransition>();
        dt.SetDestination("HotelFloor_Corridor", "Room05_Door", "Leave the room", false);
    }

    /// <summary>The same window as Room 1, in an opening of the east wall (interior towards -X), with a sill inside.</summary>
    static void Window(float zCentre, Material sill)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefab);
        GameObject w = (GameObject)PrefabUtility.InstantiatePrefab(asset, props);
        PrefabUtility.UnpackPrefabInstance(w, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        w.name = "Window";
        // the prefab is marked static; static batching would leave it behind when the room is streamed in at the door
        foreach (Transform t in w.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        // the prefab's pivot is the bottom corner of the opening and it extends along its local +Z; yaw 180 turns that to -Z
        w.transform.position = new Vector3(W - T / 2f, SillY - 0.01f, zCentre + WinW / 2f);
        w.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        w.transform.localScale = Vector3.one * 0.55f;
        // the prefab's children are offset inside it, so line the frame itself up with the opening
        Bounds body = w.transform.Find("window/window_body") != null ? w.transform.Find("window/window_body").GetComponent<Renderer>().bounds : w.GetComponentInChildren<Renderer>().bounds;
        w.transform.position += new Vector3(W - T / 2f, (SillY + HeadY) / 2f + 0.02f, zCentre) - body.center;
        RoomKit.Box("WindowSill", props, new Vector3(W - T - 0.08f, SillY - 0.02f, zCentre), new Vector3(0.16f, 0.04f, WinW + 0.2f), sill);
    }

    /// <summary>
    /// What Nabil sees through the windows: the staff room's city at night (real-size buildings tens of metres away),
    /// copied from StaffRoom.unity and turned so it lies beyond this room's east wall, lined up on its windows.
    /// </summary>
    static void CopyExterior()
    {
        var src = EditorSceneManager.OpenScene("Assets/Scenes/StaffRoom.unity", OpenSceneMode.Additive);
        GameObject ext = null;
        foreach (GameObject g in src.GetRootGameObjects()) if (g.name == "StaffRoom_Exterior") ext = Object.Instantiate(g);
        EditorSceneManager.CloseScene(src, true);
        ext.name = "Room05_Exterior";
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ext, EditorSceneManager.GetActiveScene());
        foreach (Transform t in ext.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        // staff-room window (0.3, 2.6), looking +Z  ->  this room's windows (W, ~2.45), looking +X
        Quaternion turn = Quaternion.Euler(0f, 90f, 0f);
        ext.transform.SetPositionAndRotation(new Vector3(W, 0f, 2.45f) + turn * (ext.transform.position - new Vector3(0.3f, 0f, 2.6f)), turn * ext.transform.rotation);
    }

    // ----------------------------------------------------------------- text --

    static RTLTextMeshPro3D Arabic(Transform parent, Vector3 pos, Quaternion rot, Vector2 size, string text, Color col, float maxSize)
    {
        GameObject g = new GameObject("Text", typeof(RectTransform));
        g.transform.SetParent(parent, true);
        g.transform.SetPositionAndRotation(pos, rot);
        RTLTextMeshPro3D t = g.AddComponent<RTLTextMeshPro3D>();
        t.font = Cairo;
        t.rectTransform.sizeDelta = size;
        t.enableAutoSizing = true; t.fontSizeMin = maxSize * 0.3f; t.fontSizeMax = maxSize;
        t.alignment = TextAlignmentOptions.Center; t.color = col; t.enableWordWrapping = true;
        t.text = text;
        return t;
    }

    /// <summary>A desk number painted on the desk top, readable by someone standing behind the desk.</summary>
    static void Plate(Vector3 pos, string digit)
    {
        GameObject g = new GameObject("DeskNumber_" + digit, typeof(RectTransform));
        g.transform.SetParent(props, true);
        g.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));
        TextMeshPro t = g.AddComponent<TextMeshPro>();
        t.font = Cairo; t.rectTransform.sizeDelta = new Vector2(0.15f, 0.15f);
        t.fontSize = 1.3f; t.alignment = TextAlignmentOptions.Center; t.color = new Color(0.92f, 0.88f, 0.78f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.text = digit;
    }

    /// <summary>A chalk heart drawn with short strokes (the classic heart curve).</summary>
    static void ChalkHeart(Vector3 centre, float scale, Material chalk)
    {
        Transform g = RoomKit.Group("ChalkHeart", props).transform;
        const int N = 36;
        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= N; i++)
        {
            float t = i * Mathf.PI * 2f / N;
            float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
            float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
            Vector3 p = centre + new Vector3(x * scale, y * scale, 0f);
            if (i > 0)
            {
                Vector3 d = p - prev;
                GameObject s = RoomKit.Box("Stroke", g, (p + prev) / 2f, new Vector3(d.magnitude + 0.012f, 0.016f, 0.003f), chalk, false);
                s.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            prev = p;
        }
    }

    static ReadableItem Readable(GameObject g, string title, string body, string clue, string prompt)
    {
        ReadableItem r = g.AddComponent<ReadableItem>();
        Set(r, "kind", 0); Set(r, "title", title); Set(r, "subtitle", "");
        Set(r, "readPrompt", prompt); Set(r, "body", body); Set(r, "clueId", clue);
        return r;
    }

    /// <summary>A piece of rubbish from the JustPlay household props, thrown into the bin when tidied.</summary>
    static void Trash(string id, string prefab, Vector3 pos, float scale, Transform bin)
    {
        GameObject g = RoomKit.Prefab(prefab, props, pos, Random.Range(0f, 360f), scale, "Trash_" + id);
        TidyItem t = g.AddComponent<TidyItem>();
        Set(t, "itemId", id); Set(t, "tidyPrompt", "Throw away"); Set(t, "mode", 1);
        Set(t, "disposeTarget", bin); Set(t, "markerSprite", RoomKit.MarkerSprite());
    }

    static Stash StashOn(GameObject body, string prompt, GameObject hidden, Transform lid)
    {
        Stash s = body.AddComponent<Stash>();
        Set(s, "prompt", prompt); Set(s, "hidden", hidden); Set(s, "lid", lid);
        return s;
    }

    // ---------------------------------------------------------------- build --

    public static string Build()
    {
        if (!System.IO.File.Exists(ScenePath)) AssetDatabase.CopyAsset("Assets/Scenes/StaffRoom.unity", ScenePath);
        var sc = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject g in sc.GetRootGameObjects())
            if (g.name != "GameSystems" && g.name != "SpawnPoints" && g.name != "Player") Object.DestroyImmediate(g);   // the exterior is re-copied too
        Random.InitState(5);

        map = ModelMats();
        fixture = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RoomSeven/RS_LightFixture_Emissive.mat");
        Transform root = new GameObject("Room05_Root").transform;
        shell = RoomKit.Group("Shell", root).transform;
        lighting = RoomKit.Group("Lighting", root).transform;
        props = RoomKit.Group("Props", root).transform;

        float[] win1 = { 1.6f, WinW, SillY, HeadY }, win2 = { 3.3f, WinW, SillY, HeadY };   // east of z = 4.2 is Room 1
        RoomKit.Box("Floor", shell, new Vector3(W / 2f, -0.05f, D / 2f), new Vector3(W, 0.1f, D), Mat("Floor", new Color(0.36f, 0.33f, 0.28f), 0.3f));
        RoomKit.Box("Ceiling", shell, new Vector3(W / 2f, H + 0.05f, D / 2f), new Vector3(W, 0.1f, D), RoomKit.Tiled("R5_Ceiling", CeilTex, new Color(0.45f, 0.45f, 0.42f), W, D, 1.2f, 0.05f));
        Wall("Wall_South", true, T / 2f, 0f, W);
        Wall("Wall_North", true, D - T / 2f, 0f, W);
        Wall("Wall_East", false, W - T / 2f, T, D - T, win1, win2);
        Wall("Wall_West", false, T / 2f, T, D - T, new[] { DoorZ, DoorW, 0f, DoorH });
        Door();
        Material sillMat = Mat("Sill", new Color(0.75f, 0.73f, 0.68f), 0.4f);
        Window(win1[0], sillMat); Window(win2[0], sillMat);
        CopyExterior();

        // ---- the blackboard (south wall, facing the class): a chalk heart around "ع + هـ"
        float cx = W / 2f, bz = T + 0.02f, tz = T + 0.045f;
        Quaternion facing = Quaternion.Euler(0f, 180f, 0f);
        RoomKit.Box("Chalkboard", props, new Vector3(cx, 1.75f, bz), new Vector3(4.2f, 1.6f, 0.04f), Mat("Board", new Color(0.05f, 0.17f, 0.13f), 0.1f));
        Material frame = Mat("BoardFrame", new Color(0.22f, 0.14f, 0.08f), 0.4f);
        RoomKit.Box("FrameTop", props, new Vector3(cx, 2.58f, bz + 0.01f), new Vector3(4.4f, 0.06f, 0.07f), frame, false);
        RoomKit.Box("FrameBottom", props, new Vector3(cx, 0.92f, bz + 0.01f), new Vector3(4.4f, 0.06f, 0.07f), frame, false);
        RoomKit.Box("FrameE", props, new Vector3(cx + 2.13f, 1.75f, bz + 0.01f), new Vector3(0.06f, 1.7f, 0.07f), frame, false);
        RoomKit.Box("FrameW", props, new Vector3(cx - 2.13f, 1.75f, bz + 0.01f), new Vector3(0.06f, 1.7f, 0.07f), frame, false);
        RoomKit.Box("ChalkTray", props, new Vector3(cx, 0.88f, bz + 0.06f), new Vector3(4.2f, 0.04f, 0.12f), frame);
        Color chalk = new Color(0.92f, 0.93f, 0.9f), faint = new Color(0.92f, 0.93f, 0.9f, 0.55f);
        Material chalkMat = Mat("Chalk", chalk, 0.05f, 0f, chalk * 0.25f);
        ChalkHeart(new Vector3(cx + 0.2f, 1.74f, tz), 0.034f, chalkMat);
        Arabic(props, new Vector3(cx + 0.2f, 1.82f, tz), facing, new Vector2(0.75f, 0.36f), "ع  +  هـ", chalk, 3.4f);
        // the teacher's lines, half rubbed out
        Arabic(props, new Vector3(cx - 1.3f, 2.3f, tz), facing, new Vector2(1.3f, 0.22f), "الخميس", faint, 1.4f);
        Arabic(props, new Vector3(cx - 1.3f, 1.25f, tz), facing, new Vector2(1.3f, 0.3f), "ممنوع الكلام فى الفصل !", faint, 1.4f);
        Arabic(props, new Vector3(cx + 1.4f, 2.3f, tz), facing, new Vector2(1.2f, 0.22f), "الفصل : تالتة ب", faint, 1.3f);

        // ---- the teacher's desk in front of the board, drawers towards the class
        GameObject teacher = Model("TeacherDesk", "TeacherDesk", props, new Vector3(cx, 0f, 0.95f), 180f);
        BoxCollider tc;
        tc = teacher.AddComponent<BoxCollider>(); tc.center = new Vector3(0f, 0.78f, 0f); tc.size = new Vector3(1.7f, 0.04f, 0.78f);
        tc = teacher.AddComponent<BoxCollider>(); tc.center = new Vector3(-0.6f, 0.37f, 0f); tc.size = new Vector3(0.5f, 0.74f, 0.66f);
        tc = teacher.AddComponent<BoxCollider>(); tc.center = new Vector3(0.6f, 0.37f, 0f); tc.size = new Vector3(0.5f, 0.74f, 0.66f);

        Vector3 knobL = teacher.transform.Find("KnobL").GetComponent<Renderer>().bounds.center;
        Vector3 c = teacher.transform.position;
        Vector3 mirrored = new Vector3(2f * c.x - knobL.x, knobL.y, knobL.z);   // the same height and face, on the other pedestal
        Vector3 frontDir = teacher.transform.TransformDirection(Vector3.back);
        GameObject drawer = Model("Drawer", "Drawer", props, mirrored - frontDir * 0.008f, 180f);
        RoomKit.FitCollider(drawer);
        TeacherDrawer td = drawer.AddComponent<TeacherDrawer>();
        // a small brass padlock dial on the drawer front, so it reads as locked
        RoomKit.Box("Lock", drawer.transform, drawer.transform.position + new Vector3(0f, 0.04f, 0.035f), new Vector3(0.09f, 0.05f, 0.02f), map["R5_Brass"], false);

        GameObject tape = Model("Cassette", "Cassette", drawer.transform, Vector3.zero, 0f);
        tape.transform.localPosition = new Vector3(0f, -0.062f, 0.2f);
        tape.transform.localRotation = Quaternion.Euler(90f, 20f, 0f);
        RoomKit.FitCollider(tape);
        Set(tape.AddComponent<TapeItem>(), "drawer", td);

        Material paper = Mat("Paper", new Color(0.92f, 0.88f, 0.76f), 0.2f);
        Material lined = Mat("LinedPaper", new Color(0.86f, 0.9f, 0.95f), 0.2f);
        GameObject teacherNote = RoomKit.Box("TeacherNote", props, new Vector3(cx - 0.45f, 0.805f, 0.95f), new Vector3(0.21f, 0.004f, 0.28f), paper);
        teacherNote.transform.rotation = Quaternion.Euler(0f, 8f, 0f);
        Readable(teacherNote, "ورقة الأبلة",
            "لقيت الورق ده بيتنقل فى الفصل النهارده واتبعثر فى كل حتة.\n" +
            "الإشاعة دايمًا بتبدأ من حد واحد.\n\n" +
            "اللى عايز الشريط اللى فى درجى، يكتب أرقام الدكك بالترتيب :\n" +
            "من اللى بدأ الكلام ... لحد آخر واحد وصله.",
            "", "Read the teacher's note");

        // ---- school desks facing the board: two columns, a wide aisle between them; numbers painted out of order
        Transform desks = RoomKit.Group("Desks", props).transform;
        float right = 1.2f, left = 3.7f;           // seen from a seat facing the board, right is -X
        float front = 3.3f, mid = 4.5f, back = 5.7f;
        float[,] seats = { { right, front, 4 }, { left, front, 1 }, { right, mid, 6 }, { left, mid, 2 }, { right, back, 5 }, { left, back, 3 } };
        for (int i = 0; i < 6; i++)
        {
            float x = seats[i, 0], z = seats[i, 1];
            DeskColliders(Model("SchoolDesk", "Desk_" + (int)seats[i, 2], desks, new Vector3(x, 0f, z), 0f));
            Plate(new Vector3(x - 0.22f, DeskTop + 0.002f, z - 0.27f), ((int)seats[i, 2]).ToString());
        }

        // ---- the seating chart, pinned by the door
        RoomKit.Box("Corkboard", props, new Vector3(T + 0.015f, 1.55f, 4.4f), new Vector3(0.03f, 0.75f, 0.95f), Mat("Cork", new Color(0.55f, 0.38f, 0.22f), 0.6f), false);
        GameObject chart = RoomKit.Box("SeatingChart", props, new Vector3(T + 0.035f, 1.55f, 4.4f), new Vector3(0.006f, 0.42f, 0.56f), paper);
        Material ink = Mat("Ink", new Color(0.15f, 0.2f, 0.45f), 0.3f);
        for (int r = 0; r < 3; r++) for (int col = 0; col < 2; col++)
            RoomKit.Box("Seat", props, new Vector3(T + 0.04f, 1.45f + r * 0.1f, 4.4f + (col == 0 ? 0.1f : -0.1f)), new Vector3(0.003f, 0.06f, 0.12f), ink, false);
        Readable(chart, "خريطة القعدة - تالتة ب",
            "(وإنت قاعد وباصص للسبورة)\n\n" +
            "الصف الأول :   رنا على اليمين   -   حنين على الشمال\n" +
            "الصف التانى :   سارة على اليمين   -   زكى على الشمال\n" +
            "الصف الأخير :   إياد على اليمين   -   هنا على الشمال",
            Room5Data.Chart, "Read the seating chart");

        // ---- the five passed notes, each hidden somewhere different
        string[] titles =
        {
            "ورقة مطوية فى الشنطة",
            "طيارة ورق",
            "ورقة فى المقلمة",
            "ورقة مكرمشة",
            "ورقة على درج الطباشير"
        };
        // each child says who told them, so the chain leads back to Rana, who saw it herself
        string[] bodies =
        {
            "زكى\nماتقولش لحد خالص ...\nأنا لسه شايفاهم بعينى : ع ادّى هـ نص السندوتش بتاعه فى الفسحة.\nماحدش يعرف غيرى أنا وإنت.\n\n- رنا",
            "إياد\nرنا قالتلى إن ع بيحب هـ !!\nوادّاها السندوتش كله كمان.\n\n- زكى",
            "حنين\nزكى بيقول إن ع و هـ بيحبوا بعض\nوبيروّحوا مع بعض كل يوم.\n\n- إياد",
            "هنا\nإياد قاللى إن ع كتب جواب لـ هـ\nوقال هيتجوزها لما يكبروا !!!\n\n- حنين",
            "سارة\nحنين بتقول إن هـ قالت لـ ع بحبك قدام الفصل كله\nوالمدرسة كلها عارفة.\n\n- هنا"
        };

        // 1. Rana -> Zaki: in the backpack leaning on Zaki's chair (middle row, left)
        GameObject bag = RoomKit.Prefab(BagDir + "backpack_1.prefab", props, new Vector3(left + 0.32f, 0f, mid + 0.25f), -75f, 1f, "SchoolBag");
        bag.transform.rotation = Quaternion.Euler(-12f, -75f, 0f);   // leaning against the desk leg
        // tucked into the bag's top opening, in line with the bag, only its top edge showing
        GameObject n1 = RoomKit.Box("Note_1", props, bag.transform.position + bag.transform.up * 0.46f, new Vector3(0.14f, 0.12f, 0.004f), lined);
        n1.transform.rotation = bag.transform.rotation;
        Readable(n1, titles[0], bodies[0], Room5Data.Notes[0], "Read the note");
        StashOn(bag, "Open the backpack", n1, null);
        // a satchel on another chair, just for the room (no note in it)
        GameObject satchel = RoomKit.Prefab(BagDir + "school_bag_1.prefab", props, new Vector3(right, 0.47f, back + 0.12f), 90f, 1f, "Satchel");

        // 2. Zaki -> Eyad: a paper plane that landed on the first windowsill
        GameObject plane = new GameObject("Note_2");
        plane.transform.SetParent(props, false);
        plane.transform.SetPositionAndRotation(new Vector3(W - T - 0.1f, SillY + 0.03f, 1.5f), Quaternion.Euler(0f, 160f, 0f));
        GameObject wingL = RoomKit.Box("WingL", plane.transform, plane.transform.position + plane.transform.right * 0.03f, new Vector3(0.06f, 0.003f, 0.2f), paper, false);
        wingL.transform.rotation = plane.transform.rotation * Quaternion.Euler(0f, 0f, 18f);
        GameObject wingR = RoomKit.Box("WingR", plane.transform, plane.transform.position - plane.transform.right * 0.03f, new Vector3(0.06f, 0.003f, 0.2f), paper, false);
        wingR.transform.rotation = plane.transform.rotation * Quaternion.Euler(0f, 0f, -18f);
        BoxCollider pc = plane.AddComponent<BoxCollider>(); pc.size = new Vector3(0.16f, 0.06f, 0.24f);
        Readable(plane, titles[1], bodies[1], Room5Data.Notes[1], "Unfold the paper plane");

        // 3. Eyad -> Haneen: rolled up beside the pencil case on Haneen's desk (front row, left)
        Vector3 casePos = new Vector3(left + 0.12f, DeskTop, front - 0.42f);
        GameObject pcase = RoomKit.Prefab(PropDir + "Purse.prefab", props, casePos + new Vector3(0f, 0.026f, 0f), 12f, 1.6f, "PencilCase");
        pcase.transform.rotation = Quaternion.Euler(90f, 12f, 0f);   // the pouch is modelled standing up; lay it flat on the desk
        GameObject n3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        n3.name = "Note_3"; n3.transform.SetParent(props, false);
        n3.transform.SetPositionAndRotation(casePos + new Vector3(-0.2f, 0.015f, 0.02f), Quaternion.Euler(0f, 12f, 90f));
        n3.transform.localScale = new Vector3(0.03f, 0.08f, 0.03f);
        n3.GetComponent<Renderer>().sharedMaterial = lined;
        Readable(n3, titles[2], bodies[2], Room5Data.Notes[2], "Unroll the note");
        StashOn(pcase, "Open the pencil case", n3, null);

        // 4. Haneen -> Hana: crumpled up and thrown on the floor behind Hana's desk (back row, left)
        GameObject n4 = RoomKit.Prefab(PropDir + "Paper.prefab", props, new Vector3(4.35f, 0f, 6.6f), 30f, 2.6f, "Note_4");
        Readable(n4, titles[3], bodies[3], Room5Data.Notes[3], "Unfold the crumpled paper");

        // 5. Hana -> Sara: left on the chalk tray (Sara drew the heart)
        GameObject n5 = RoomKit.Box("Note_5", props, new Vector3(cx + 1.5f, 0.905f, bz + 0.07f), new Vector3(0.12f, 0.006f, 0.08f), lined);
        n5.transform.rotation = Quaternion.Euler(0f, -14f, 0f);
        Readable(n5, titles[4], bodies[4], Room5Data.Notes[4], "Read the note");
        // a stick of chalk and a duster on the tray
        RoomKit.Box("ChalkStick", props, new Vector3(cx - 0.6f, 0.91f, bz + 0.06f), new Vector3(0.08f, 0.012f, 0.012f), chalkMat, false);
        RoomKit.Box("Duster", props, new Vector3(cx - 0.9f, 0.92f, bz + 0.06f), new Vector3(0.13f, 0.04f, 0.06f), Mat("Duster", new Color(0.3f, 0.22f, 0.15f), 0.6f), false);

        // ---- rubbish in the aisles + the bin in the corner by the door
        Transform bin = RoomKit.TrashCan(props, new Vector3(0.4f, 0f, 7.55f), 0f);   // the same pedal bin as Rooms 1-3
        Trash("paper1", PropDir + "Paper.prefab", new Vector3(2.2f, 0f, 4.1f), 2.2f, bin);
        Trash("paper2", PropDir + "Paper.prefab", new Vector3(2.8f, 0f, 5.4f), 2.0f, bin);
        Trash("gum", PropDir + "ChewingGum.prefab", new Vector3(0.6f, 0f, 3.0f), 1.5f, bin);
        Trash("eraser", PropDir + "Eraser.prefab", new Vector3(3.9f, 0f, 1.9f), 1.3f, bin);
        Trash("pencil", PropDir + "Pencil.prefab", new Vector3(2.5f, 0.005f, 2.4f), 1f, bin);
        GameObject chalkBit = RoomKit.Box("Trash_chalk", props, new Vector3(0.9f, 0.01f, 1.6f), new Vector3(0.05f, 0.012f, 0.012f), chalkMat);
        TidyItem ct = chalkBit.AddComponent<TidyItem>();
        Set(ct, "itemId", "chalk"); Set(ct, "tidyPrompt", "Throw away"); Set(ct, "mode", 1);
        Set(ct, "disposeTarget", bin); Set(ct, "markerSprite", RoomKit.MarkerSprite());

        Lamp("Lamp_Board", new Vector3(cx, 3.0f, 1.0f), new Color(1f, 0.95f, 0.85f), 2.0f);
        Lamp("Lamp_A", new Vector3(cx, 3.0f, 3.4f), new Color(0.9f, 0.96f, 1f), 2.2f);
        Lamp("Lamp_B", new Vector3(cx, 3.0f, 5.6f), new Color(0.9f, 0.96f, 1f), 2.2f);
        Lamp("Lamp_C", new Vector3(cx, 3.0f, 7.3f), new Color(0.9f, 0.96f, 1f), 1.6f);

        SpawnPoint sp = Object.FindFirstObjectByType<SpawnPoint>();
        sp.SetSpawnId("FromCorridor");
        sp.transform.SetPositionAndRotation(new Vector3(1.3f, 0f, DoorZ), Quaternion.Euler(0f, 90f, 0f));

        GameObject logic = new GameObject("Room5Logic");
        logic.AddComponent<Room5Controller>();
        TidyRoom tr = logic.AddComponent<TidyRoom>();
        Set(tr, "roomId", "Room05"); Set(tr, "displayName", "Room 5");

        RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.09f);
        RenderSettings.ambientIntensity = 1f;

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return "built Room05";
    }

    /// <summary>Corridor side: a return spawn in front of the Room 5 door (its streamed door was already set up by Room4Builder).</summary>
    public static string PatchCorridor()
    {
        var sc = EditorSceneManager.OpenScene("Assets/Scenes/HotelFloor_Corridor.unity", OpenSceneMode.Single);
        SpawnPoint template = null;
        foreach (SpawnPoint s in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            if (s.name == "Spawn_Room05_Door") Object.DestroyImmediate(s.gameObject);
        foreach (SpawnPoint s in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            if (s.name == "Spawn_Room04_Door") template = s;
        GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
        copy.name = "Spawn_Room05_Door";
        copy.GetComponent<SpawnPoint>().SetSpawnId("Room05_Door");
        copy.transform.SetPositionAndRotation(new Vector3(1.0f, 0f, -6.3f), Quaternion.Euler(0f, 270f, 0f));
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        return "corridor patched";
    }

    [MenuItem("Room Seven/Build room 5")]
    public static void BuildMenu()
    {
        Debug.Log("[Room5Builder] " + Build());
        Debug.Log("[Room5Builder] " + PatchCorridor());
    }
}
