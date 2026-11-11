using System.Collections.Generic;
using RTLTMPro;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Room04 scene: a small cinema behind an ordinary hotel-room door (10 x 8 m). Cinema seats face the screen
/// on the east wall, the broken 35mm projector stands at the back, film posters hang on the side walls, rubbish lies
/// between the rows, and the projector's three missing parts (lens, bulb, film reel) are hidden around the room.
/// Models come from Assets/Models/Room4 (made in Blender). Menu: Room Seven / Build room 4. Re-running rebuilds
/// everything except Player / SpawnPoints / GameSystems, and patches the corridor (streamed doors, return spawn).
/// </summary>
public static class Room4Builder
{
    const float W = 10f, D = 8f, H = 3.2f, T = 0.1f, DoorW = 1.16f, DoorH = 2.2f, DoorZ = 1.3f, SeatTop = 0.5f;
    const string WallTex = "Assets/ThirdParty/YughuesFreeFabricMaterials/Textures/T_YFFaM_Burlap01_d.tga";
    const string CeilTex = "Assets/ThirdParty/YughuesFreeFabricMaterials/Textures/T_YFFaM_Burlap02_d.tga";
    const string CarpetTex = "Assets/ThirdParty/YughuesFreeFabricMaterials/Textures/T_YFFaM_Carpet02_d.tga";
    const string DoorPrefab = "Assets/ThirdParty/Free Wood Door Pack/Prefab/Wood/Door_4/Door_4_Brown.prefab";
    const string ScenePath = "Assets/Scenes/Room04.unity";
    const string ModelDir = "Assets/Models/Room4/";

    static Transform shell, lighting, props;
    static Material fixture;
    static readonly List<Light> lights = new List<Light>();
    static Dictionary<string, Material> map;

    static float R1(float v) { return Mathf.Round(v * 10f) / 10f; }
    static Material Mat(string n, Color c, float s = 0.35f, float m = 0f, Color? e = null) { return RoomKit.Plain("R4_" + n, c, s, m, e); }

    static void Set(Object o, string field, object v)
    {
        SerializedObject so = new SerializedObject(o);
        SerializedProperty p = so.FindProperty(field);
        if (v is string) p.stringValue = (string)v;
        else if (v is int) p.enumValueIndex = (int)v;
        else if (v is Object || v == null) p.objectReferenceValue = (Object)v;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------- materials --

    static Dictionary<string, Material> ModelMats()
    {
        var d = new Dictionary<string, Material>();
        d["R4_SeatFabric"] = RoomKit.Plain("R4M_SeatFabric", new Color(0.42f, 0.05f, 0.07f), 0.12f);
        d["R4_SeatFrame"] = RoomKit.Plain("R4M_SeatFrame", new Color(0.06f, 0.06f, 0.065f), 0.5f, 0.6f);
        d["R4_ArmWood"] = RoomKit.Plain("R4M_ArmWood", new Color(0.22f, 0.12f, 0.06f), 0.45f);
        d["R4_ReelMetal"] = RoomKit.Plain("R4M_ReelMetal", new Color(0.55f, 0.56f, 0.58f), 0.65f, 1f);
        d["R4_Film"] = RoomKit.Plain("R4M_Film", new Color(0.05f, 0.04f, 0.035f), 0.6f);
        d["R4_Glass"] = RoomKit.Plain("R4M_Glass", new Color(0.85f, 0.88f, 0.9f), 0.95f);
        d["R4_Brass"] = RoomKit.Plain("R4M_Brass", new Color(0.75f, 0.58f, 0.25f), 0.7f, 1f);
        d["R4_Filament"] = RoomKit.Plain("R4M_Filament", new Color(0.2f, 0.18f, 0.15f), 0.4f, 1f);
        d["R4_ProjBody"] = RoomKit.Plain("R4M_ProjBody", new Color(0.16f, 0.22f, 0.2f), 0.55f, 0.5f);
        d["R4_ProjSteel"] = RoomKit.Plain("R4M_ProjSteel", new Color(0.5f, 0.5f, 0.52f), 0.7f, 1f);
        d["R4_ProjBlack"] = RoomKit.Plain("R4M_ProjBlack", new Color(0.04f, 0.04f, 0.045f), 0.5f, 0.3f);
        d["R4_ProjWindow"] = RoomKit.Plain("R4M_ProjWindow", new Color(0.25f, 0.2f, 0.1f), 0.9f);
        d["R4_LensGlass"] = RoomKit.Plain("R4M_LensGlass", new Color(0.3f, 0.45f, 0.55f), 0.95f, 0.2f);
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

    // ---------------------------------------------------------------- shell --

    static Material WallMat(float u, float v) { return RoomKit.Tiled("R4_Wall", WallTex, new Color(0.42f, 0.16f, 0.17f), R1(u), R1(v), 1.2f, 0.05f); }

    static void Wall(string name, bool alongX, float fixedC, float from, float to, float doorCentre)
    {
        Transform g = RoomKit.Group(name, shell).transform;
        List<float[]> parts = new List<float[]>();
        if (doorCentre > 0f)
        {
            parts.Add(new[] { from, doorCentre - DoorW / 2f, 0f, H });
            parts.Add(new[] { doorCentre - DoorW / 2f, doorCentre + DoorW / 2f, DoorH, H });
            parts.Add(new[] { doorCentre + DoorW / 2f, to, 0f, H });
        }
        else parts.Add(new[] { from, to, 0f, H });
        int i = 0;
        foreach (float[] s in parts)
        {
            float len = s[1] - s[0], h = s[3] - s[2];
            if (len < 0.01f) continue;
            float mid = (s[0] + s[1]) / 2f, y = (s[2] + s[3]) / 2f;
            Vector3 c = alongX ? new Vector3(mid, y, fixedC) : new Vector3(fixedC, y, mid);
            Vector3 size = alongX ? new Vector3(len, h, T) : new Vector3(T, h, len);
            RoomKit.Box("Piece_" + (i++), g, c, size, WallMat(len, h));
        }
    }

    static void Lamp(string name, Vector3 pos, Color color, float intensity)
    {
        GameObject g = RoomKit.Group(name, lighting);
        RoomKit.Box("Fixture", g.transform, pos + new Vector3(0f, 0.28f, 0f), new Vector3(0.6f, 0.05f, 0.2f), fixture, false);
        GameObject lg = new GameObject("Light"); lg.transform.SetParent(g.transform, false);
        lg.transform.position = pos;
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = 6f; l.intensity = intensity; l.color = color; l.shadows = LightShadows.None;
        lights.Add(l);
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
        dt.SetDestination("HotelFloor_Corridor", "Room04_Door", "Leave the room", false);
    }

    // ---------------------------------------------------------------- props --

    /// <summary>Seat base up to the cushion and the backrest only, so things lying on the cushion can be looked at and picked up.</summary>
    static void SeatColliders(GameObject seat)
    {
        BoxCollider b = seat.AddComponent<BoxCollider>();
        b.center = new Vector3(0f, 0.24f, -0.03f); b.size = new Vector3(0.6f, 0.48f, 0.56f);   // local +Z is the front
        BoxCollider back = seat.AddComponent<BoxCollider>();
        back.center = new Vector3(0f, 0.78f, -0.24f); back.size = new Vector3(0.56f, 0.62f, 0.14f);
    }

    static GameObject Part(GameObject go, string partName, string flag)
    {
        RoomKit.FitCollider(go);
        PartItem pi = go.AddComponent<PartItem>();
        Set(pi, "partName", partName); Set(pi, "flag", flag);
        return go;
    }

    /// <summary>World-space Arabic text lying flat on an object's top face, its top towards +X (the screen).</summary>
    static RTLTextMeshPro3D FlatText(Transform parent, Vector3 pos, Vector2 size, string text, Color col, float maxSize, TextAlignmentOptions align)
    {
        GameObject g = new GameObject("Text", typeof(RectTransform));
        g.transform.SetParent(parent, true);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(90f, 90f, 0f);
        RTLTextMeshPro3D t = g.AddComponent<RTLTextMeshPro3D>();
        t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cairo/Cairo-Regular SDF.asset");
        t.rectTransform.sizeDelta = size;
        t.enableAutoSizing = true; t.fontSizeMin = maxSize * 0.25f; t.fontSizeMax = maxSize;   // TMP 3D: font size 1 is about 0.1 m
        t.alignment = align; t.color = col;
        t.enableWordWrapping = true;
        t.text = text;
        return t;
    }

    static GameObject Readable(string name, Vector3 pos, Vector3 size, Material mat, int kind, string title, string subtitle, string prompt, string body, string clue)
    {
        GameObject g = RoomKit.Box(name, props, pos, size, mat);
        ReadableItem r = g.AddComponent<ReadableItem>();
        Set(r, "kind", kind); Set(r, "title", title); Set(r, "subtitle", subtitle);
        Set(r, "readPrompt", prompt); Set(r, "body", body); Set(r, "clueId", clue);
        return g;
    }

    static void Trash(string id, PrimitiveType type, Vector3 pos, Vector3 scale, Color col, Transform bin)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = "Trash_" + id; g.transform.SetParent(props, false);
        g.transform.position = pos; g.transform.localScale = scale;
        g.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
        g.GetComponent<Renderer>().sharedMaterial = Mat("Trash_" + id, col, 0.3f);
        TidyItem t = g.AddComponent<TidyItem>();
        Set(t, "itemId", id); Set(t, "tidyPrompt", "Throw away"); Set(t, "mode", 1);
        Set(t, "disposeTarget", bin); Set(t, "markerSprite", RoomKit.MarkerSprite());
    }

    static void Posters()
    {
        List<Texture2D> tex = new List<Texture2D>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Room4" }))
            tex.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)));
        float[] xs = { 2.6f, 5.0f, 7.4f };
        for (int i = 0; i < tex.Count && i < 6; i++)
        {
            bool north = i < 3;
            float h = 1.5f, w = h * tex[i].width / tex[i].height;
            string path = RoomKit.MaterialDir + "R4_Poster" + i + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex[i]); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(m);
            Vector3 p = new Vector3(xs[i % 3], 1.65f, north ? D - T - 0.01f : T + 0.01f);
            RoomKit.Quad("Poster_" + i, props, p, Quaternion.Euler(0f, north ? 0f : 180f, 0f), new Vector2(w, h), m);
        }
    }

    // ---------------------------------------------------------------- build --

    public static string Build()
    {
        if (!System.IO.File.Exists(ScenePath)) AssetDatabase.CopyAsset("Assets/Scenes/StaffRoom.unity", ScenePath);
        var sc = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject g in sc.GetRootGameObjects())
            if (g.name != "GameSystems" && g.name != "SpawnPoints" && g.name != "Player") Object.DestroyImmediate(g);

        lights.Clear();
        map = ModelMats();
        fixture = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RoomSeven/RS_LightFixture_Emissive.mat");
        Transform root = new GameObject("Room04_Root").transform;
        shell = RoomKit.Group("Shell", root).transform;
        lighting = RoomKit.Group("Lighting", root).transform;
        props = RoomKit.Group("Props", root).transform;

        RoomKit.Box("Floor", shell, new Vector3(W / 2f, -0.05f, D / 2f), new Vector3(W, 0.1f, D), RoomKit.Tiled("R4_Floor", CarpetTex, new Color(0.30f, 0.12f, 0.13f), W, D, 1f, 0.05f));
        RoomKit.Box("Ceiling", shell, new Vector3(W / 2f, H + 0.05f, D / 2f), new Vector3(W, 0.1f, D), RoomKit.Tiled("R4_Ceiling", CeilTex, new Color(0.20f, 0.12f, 0.12f), W, D, 1.2f, 0.05f));
        Wall("Wall_South", true, T / 2f, 0f, W, -1f);
        Wall("Wall_North", true, D - T / 2f, 0f, W, -1f);
        Wall("Wall_East", false, W - T / 2f, T, D - T, -1f);
        Wall("Wall_West", false, T / 2f, T, D - T, DoorZ);
        Door();

        // screen + curtains (east wall, facing west)
        Material off = Mat("ScreenOff", new Color(0.08f, 0.08f, 0.09f), 0.1f);
        GameObject screen = RoomKit.Quad("Screen", props, new Vector3(W - T - 0.02f, 1.75f, D / 2f), Quaternion.Euler(0f, 90f, 0f), new Vector2(6f, 2.6f), off);
        Material curtain = Mat("Curtain", new Color(0.28f, 0.04f, 0.06f), 0.15f);
        RoomKit.Box("CurtainN", props, new Vector3(W - 0.25f, H / 2f, D / 2f + 3.6f), new Vector3(0.3f, H, 1.4f), curtain, false);
        RoomKit.Box("CurtainS", props, new Vector3(W - 0.25f, H / 2f, D / 2f - 3.6f), new Vector3(0.3f, H, 1.4f), curtain, false);
        RoomKit.Box("Stage", props, new Vector3(W - 0.6f, 0.1f, D / 2f), new Vector3(1.0f, 0.2f, 6.4f), Mat("Stage", new Color(0.12f, 0.06f, 0.06f)));

        // seats: 4 rows, two blocks of four around a central aisle, facing the screen (+X)
        float[] rows = { 3.0f, 4.2f, 5.4f, 6.6f };
        float[] zs = { 1.3f, 1.92f, 2.54f, 3.16f, 4.84f, 5.46f, 6.08f, 6.7f };
        Transform seatGroup = RoomKit.Group("Seats", props).transform;
        foreach (float x in rows) foreach (float z in zs) SeatColliders(Model("CinemaSeat", "Seat", seatGroup, new Vector3(x, 0f, z), 90f));

        // the projector at the back, aimed at the screen; parts it still needs are hidden until fitted
        GameObject proj = Model("FilmProjector", "Projector", props, new Vector3(1.5f, 0f, D / 2f), 90f);
        RoomKit.FitCollider(proj);
        GameObject filmGo = new GameObject("ShadowFilm");
        filmGo.transform.SetParent(props, false);
        ShadowFilm film = filmGo.AddComponent<ShadowFilm>();
        ProjectorRig rig = proj.AddComponent<ProjectorRig>();
        Set(rig, "film", film);
        Set(rig, "lensPart", proj.transform.Find("Proj_Lens").gameObject);
        Set(rig, "reelPart", proj.transform.Find("Proj_FeedReel").gameObject);
        Set(rig, "lampWindow", proj.transform.Find("Proj_Window").GetComponent<Renderer>());
        proj.transform.Find("Proj_Lens").gameObject.SetActive(false);
        proj.transform.Find("Proj_FeedReel").gameObject.SetActive(false);

        // the three loose parts
        GameObject lensSrc = Model("FilmProjector", "tmp", props, Vector3.zero, 90f);
        GameObject lens = lensSrc.transform.Find("Proj_Lens").gameObject;
        lens.transform.SetParent(props, true);
        Object.DestroyImmediate(lensSrc);
        lens.name = "Part_Lens";
        lens.transform.rotation = Quaternion.Euler(0f, 30f, 0f) * lens.transform.rotation;
        lens.transform.position += new Vector3(4.8f, 0.067f, 3.7f) - RoomKit.WorldBounds(lens).center;   // rolled into the aisle between the 2nd and 3rd rows
        Part(lens, "Lens", ProjectorPanel.LensPart);

        RoomKit.Box("Crate", props, new Vector3(8.7f, 0.15f, 1.0f), new Vector3(0.5f, 0.3f, 0.5f), Mat("Crate", new Color(0.2f, 0.14f, 0.09f)));
        Part(Model("ProjectorBulb", "Part_Bulb", props, new Vector3(8.75f, 0.3f, 1.05f), 0f), "Bulb", ProjectorPanel.BulbPart);   // by the stage, in the corner

        RoomKit.Box("Shelf", props, new Vector3(0.45f, 0.45f, 6.9f), new Vector3(0.6f, 0.9f, 0.9f), Mat("Shelf", new Color(0.18f, 0.12f, 0.08f)));
        Part(Model("FilmReel", "Part_Reel", props, new Vector3(0.45f, 0.9f, 6.9f), 0f), "Film reel", ProjectorPanel.ReelPart);

        // readables: the Narcissus paper in the second row, the residents' phone in the back row; both readable at a glance
        Material paperMat = Mat("Paper", new Color(0.9f, 0.86f, 0.74f), 0.2f);
        GameObject paper = Readable("NarcissusPaper", new Vector3(4.2f, SeatTop + 0.003f, 2.54f), new Vector3(0.297f, 0.004f, 0.21f), paperMat, 0,
            "نرسيس", "", "Read the paper",
            "ناركيسوس هو شاب وسيم من الأساطير الإغريقية، ابن إله النهر كيفيسوس والحورية ليريوبي، اشتهر بجماله الاستثنائي وغروره الذي دفعه لرفض مشاعر جميع من أحبوه، بما في ذلك الحورية إيكو التي كانت تحبه بجنون.\nعندما لاحظت الآلهة غطرسته، قررت عقابه عبر إيقاعه في حب انعكاس صورته في مياه بركة صافية. لم يستطع نرسيس الابتعاد عن ذلك الوهم الجميل، مما أدى إلى ذبول جسده وموته من شدة الحزن أو الجوع، لتتحول الآلهة جسده بعد وفاته إلى زهرة النرجس، التي أصبحت رمزاً للأدب والنفسية لـ حب الذات المفرط.",
            "A paper about Narcissus, left on a seat");
        Color ink = new Color(0.15f, 0.12f, 0.1f);
        FlatText(props, paper.transform.position + new Vector3(0.115f, 0.004f, 0f), new Vector2(0.19f, 0.04f), "نرسيس", ink, 0.3f, TextAlignmentOptions.Center);
        FlatText(props, paper.transform.position + new Vector3(-0.025f, 0.004f, 0f), new Vector2(0.19f, 0.22f),
            "ناركيسوس هو شاب وسيم من الأساطير الإغريقية، ابن إله النهر كيفيسوس والحورية ليريوبي، اشتهر بجماله الاستثنائي وغروره الذي دفعه لرفض مشاعر جميع من أحبوه، بما في ذلك الحورية إيكو التي كانت تحبه بجنون...", ink, 0.12f, TextAlignmentOptions.TopRight);

        GameObject phone = Readable("ResidentPhone", new Vector3(6.6f, SeatTop + 0.005f, 5.46f), new Vector3(0.155f, 0.009f, 0.075f), Mat("Phone", new Color(0.03f, 0.03f, 0.035f), 0.8f), 1,
            "Clara", "online", "Pick up the phone",
            "مينا انت صاحى ؟\n>آه ليه ؟\nسمعت صوت صويت فى الطرقة\n>تانى ؟!\nآه و شكله صوت البنت الى بتنضف. لما بصيت من العين شوفت المدير بتاعها\n>تفتكرى بيضايقها ؟\nانا شوفتها لما رجعنا من برا، وشها كان أبيض وماسكة نفسها بالعافية\n>ربنا يستر عليها، احنا لازم نبلغ الموضوع ده.",
            "Mina and Clara heard screaming in the corridor and saw Malak's manager there");
        GameObject glass = RoomKit.Box("PhoneScreen", props, phone.transform.position + new Vector3(0f, 0.0047f, 0f), new Vector3(0.143f, 0.0005f, 0.067f),
            Mat("PhoneScreen", new Color(0.05f, 0.07f, 0.12f), 0.9f, 0f, new Color(0.08f, 0.12f, 0.25f)), false);
        Color lit = new Color(0.9f, 0.92f, 1f);
        FlatText(props, glass.transform.position + new Vector3(0.045f, 0.001f, 0f), new Vector2(0.06f, 0.02f), "02:14", lit, 0.16f, TextAlignmentOptions.Center);
        FlatText(props, glass.transform.position + new Vector3(-0.015f, 0.001f, 0f), new Vector2(0.06f, 0.05f), "Clara\nانا شوفتها لما رجعنا من برا...", lit, 0.07f, TextAlignmentOptions.Center);

        // rubbish + the bin by the door
        GameObject bin = RoomKit.Box("Bin", props, new Vector3(0.45f, 0.35f, 2.6f), new Vector3(0.45f, 0.7f, 0.45f), Mat("Bin", new Color(0.1f, 0.1f, 0.11f)));
        Color cup = new Color(0.85f, 0.85f, 0.8f), pop = new Color(0.9f, 0.8f, 0.5f);
        Trash("cup1", PrimitiveType.Cylinder, new Vector3(3.6f, 0.1f, 2.2f), new Vector3(0.1f, 0.1f, 0.1f), cup, bin.transform);
        Trash("pop1", PrimitiveType.Cube, new Vector3(4.8f, 0.07f, 4.0f), new Vector3(0.2f, 0.15f, 0.2f), pop, bin.transform);
        Trash("cup2", PrimitiveType.Cylinder, new Vector3(6.0f, 0.1f, 1.6f), new Vector3(0.1f, 0.1f, 0.1f), cup, bin.transform);
        Trash("pop2", PrimitiveType.Cube, new Vector3(7.2f, 0.07f, 5.8f), new Vector3(0.2f, 0.15f, 0.2f), pop, bin.transform);
        Trash("ticket1", PrimitiveType.Cube, new Vector3(2.4f, 0.01f, 5.2f), new Vector3(0.12f, 0.01f, 0.07f), new Color(0.8f, 0.2f, 0.2f), bin.transform);
        Trash("cup3", PrimitiveType.Cylinder, new Vector3(7.8f, 0.1f, 6.9f), new Vector3(0.1f, 0.1f, 0.1f), cup, bin.transform);

        Posters();

        Lamp("Lamp_A", new Vector3(2.5f, 3.0f, 4.0f), new Color(1f, 0.78f, 0.55f), 2.2f);
        Lamp("Lamp_B", new Vector3(6.0f, 3.0f, 2.0f), new Color(1f, 0.7f, 0.45f), 1.8f);
        Lamp("Lamp_C", new Vector3(6.0f, 3.0f, 6.0f), new Color(1f, 0.7f, 0.45f), 1.8f);

        AudioClip music = null;
        foreach (string guid in AssetDatabase.FindAssets("lyricless t:AudioClip")) music = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
        SerializedObject fso = new SerializedObject(film);
        fso.FindProperty("screen").objectReferenceValue = screen.GetComponent<Renderer>();
        fso.FindProperty("music").objectReferenceValue = music;
        SerializedProperty lp = fso.FindProperty("roomLights");
        lp.arraySize = lights.Count;
        for (int i = 0; i < lights.Count; i++) lp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
        fso.ApplyModifiedPropertiesWithoutUndo();

        SpawnPoint sp = Object.FindFirstObjectByType<SpawnPoint>();
        sp.SetSpawnId("FromCorridor");
        sp.transform.SetPositionAndRotation(new Vector3(1.4f, 0f, DoorZ), Quaternion.Euler(0f, 90f, 0f));

        // where the camera sits during the film: middle of the room, eyes level with the screen
        GameObject fv = new GameObject("FilmView");
        fv.transform.SetParent(props, false);
        fv.transform.position = new Vector3(4.8f, 1.45f, D / 2f);   // high enough to see over the seat backs
        fv.transform.rotation = Quaternion.LookRotation(new Vector3(W - T, 1.75f, D / 2f) - fv.transform.position);
        Unity.Cinemachine.CinemachineCamera vcam = fv.AddComponent<Unity.Cinemachine.CinemachineCamera>();
        vcam.Lens = new Unity.Cinemachine.LensSettings { FieldOfView = 55f, NearClipPlane = 0.05f, FarClipPlane = 100f };
        vcam.Priority.Enabled = true; vcam.Priority.Value = 0;
        Set(film, "filmView", vcam);

        GameObject logic = new GameObject("Room4Logic");
        logic.AddComponent<Room4Controller>();
        TidyRoom tr = logic.AddComponent<TidyRoom>();
        Set(tr, "roomId", "Room04"); Set(tr, "displayName", "Room 4");

        RenderSettings.ambientLight = new Color(0.08f, 0.06f, 0.06f);
        RenderSettings.ambientIntensity = 1f;

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return "built Room04 (music " + (music != null) + ")";
    }

    /// <summary>Corridor side: a return spawn in front of the Room 4 door, and streamed doors for rooms 4-7.</summary>
    public static string PatchCorridor()
    {
        var sc = EditorSceneManager.OpenScene("Assets/Scenes/HotelFloor_Corridor.unity", OpenSceneMode.Single);
        SpawnPoint template = null;
        foreach (SpawnPoint s in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            if (s.name == "Spawn_Room04_Door") Object.DestroyImmediate(s.gameObject);
        foreach (SpawnPoint s in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            if (s.name == "Spawn_Room02_Door") template = s;
        GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
        copy.name = "Spawn_Room04_Door";
        copy.GetComponent<SpawnPoint>().SetSpawnId("Room04_Door");
        copy.transform.SetPositionAndRotation(new Vector3(1.0f, 0f, 7.7f), Quaternion.Euler(0f, 270f, 0f));
        StreamRoomDoors();
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        return "corridor patched";
    }

    /// <summary>Rooms 4-7: swap the corridor's load-screen doors for streamed doors, copying Room 3's door settings.</summary>
    static void StreamRoomDoors()
    {
        StreamDoor template = null;
        foreach (StreamDoor s in Object.FindObjectsByType<StreamDoor>(FindObjectsSortMode.None))
            if (new SerializedObject(s).FindProperty("sceneName").stringValue == "Room03") template = s;
        foreach (DoorTransition d in Object.FindObjectsByType<DoorTransition>(FindObjectsSortMode.None))
        {
            string scene = d.TargetScene;
            if (scene != "Room04" && scene != "Room05" && scene != "Room06" && scene != "Room07") continue;
            GameObject leaf = d.gameObject;
            foreach (MonoBehaviour mb in leaf.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(mb);   // Door + DoorTransition
            StreamDoor sd = leaf.AddComponent<StreamDoor>();
            EditorUtility.CopySerialized(template, sd);
            SerializedObject so = new SerializedObject(sd);
            so.FindProperty("sceneName").stringValue = scene;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    [MenuItem("Room Seven/Build room 4")]
    public static void BuildMenu()
    {
        Debug.Log("[Room4Builder] " + Build());
        Debug.Log("[Room4Builder] " + PatchCorridor());
    }
}
