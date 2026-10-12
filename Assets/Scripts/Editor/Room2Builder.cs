using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Builds the Room02 scene (the looping L-shaped corridor + electricity room) and the three prop prefabs.</summary>
public static class Room2Builder
{
    const float H = 2.9f, T = 0.2f, DoorW = 1.16f, DoorH = 2.2f;
    const string WallTex = "Assets/YughuesFreeFabricMaterials/Textures/T_YFFaM_Burlap01_d.tga";
    const string CeilTex = "Assets/YughuesFreeFabricMaterials/Textures/T_YFFaM_Burlap02_d.tga";
    const string CarpetTex = "Assets/YughuesFreeFabricMaterials/Textures/T_YFFaM_Carpet02_d.tga";
    const string DoorPrefab = "Assets/Free Wood Door Pack/Prefab/Wood/Door_4/Door_4_Brown.prefab";

    static Transform walls, lighting, props;
    static Material wallMat0, ceilMat0, floorMat0, concrete, fixture;

    static float R1(float v) { return Mathf.Round(v * 10f) / 10f; }

    static Material WallMat(float u, float v) { return RoomKit.Tiled("R2_Wall", WallTex, new Color(0.86f, 0.78f, 0.66f), R1(u), R1(v), 1.2f, 0.05f); }

    static void Slab(string name, Transform p, float x0, float z0, float x1, float z1, float y, string kind)
    {
        float sx = x1 - x0, sz = z1 - z0;
        Material m = kind == "floor"
            ? RoomKit.Tiled("R2_Carpet", CarpetTex, new Color(0.75f, 0.45f, 0.42f), R1(sx), R1(sz), 1.0f, 0.05f)
            : kind == "ceiling" ? RoomKit.Tiled("R2_Ceiling", CeilTex, new Color(0.92f, 0.86f, 0.76f), R1(sx), R1(sz), 1.2f, 0.05f)
            : RoomKit.Plain("R2_Concrete", new Color(0.42f, 0.43f, 0.42f), 0.1f);
        RoomKit.Box(name, p, new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(sx, 0.1f, sz), m);
    }

    // A wall on a centre line, with door openings (centres along the wall) and a header over each.
    static void Wall(string name, Transform p, bool alongX, float fixedC, float from, float to, float[] doors, Material overrideMat = null)
    {
        Transform g = RoomKit.Group(name, p).transform;
        List<float[]> parts = new List<float[]>();   // {from,to,yBottom,yTop}
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
            RoomKit.Box(name + "_" + (i++), g, c, size, overrideMat != null ? overrideMat : WallMat(len, h));
        }
    }

    static void Lamp(string name, Vector3 pos, bool bulb = false)
    {
        GameObject g = RoomKit.Group(name, lighting);
        g.transform.position = pos;
        if (!bulb) RoomKit.Box("Fixture", g.transform, pos + new Vector3(0f, 0.28f, 0f), new Vector3(0.7f, 0.05f, 0.22f), fixture, false);
        else
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Bulb"; s.transform.SetParent(g.transform, false);
            s.transform.position = pos + new Vector3(0f, 0.15f, 0f); s.transform.localScale = Vector3.one * 0.12f;
            s.GetComponent<Renderer>().sharedMaterial = fixture;
            Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        GameObject lg = new GameObject("Light"); lg.transform.SetParent(g.transform, false);
        lg.transform.position = pos;
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = bulb ? 5.5f : 6f; l.intensity = bulb ? 2.5f : 2f;
        l.color = new Color(1f, 0.92f, 0.76f); l.shadows = LightShadows.None;
    }

    static GameObject Door(string name, Vector3 pos, float yaw, bool swing, string target, string label, bool loop)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab);
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(asset, props);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = name; root.transform.position = pos; root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        root.transform.localScale = Vector3.one * 1.01f;
        Transform leaf = root.transform.Find("Door");
        foreach (var c in leaf.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(c);   // the pack's own Door script
        if (swing)
        {
            leaf.gameObject.AddComponent<SwingDoor>();
            var so = new SerializedObject(leaf.GetComponent<SwingDoor>());
            so.FindProperty("leaf").objectReferenceValue = leaf;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            LoopDoor d = leaf.gameObject.AddComponent<LoopDoor>();
            d.SetDestination(target, "Room02_Door", label, false);
            var so = new SerializedObject(d);
            so.FindProperty("countsAsLoop").boolValue = loop;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return root;
    }

    static Dictionary<string, Material> PropMats()
    {
        var d = new Dictionary<string, Material>();
        d["FL_Body"] = RoomKit.Plain("R2_FlBody", new Color(0.08f, 0.09f, 0.10f), 0.5f, 0.6f);
        d["FL_Metal"] = RoomKit.Plain("R2_FlMetal", new Color(0.62f, 0.63f, 0.65f), 0.7f, 0.9f);
        d["FL_Lens"] = RoomKit.Plain("R2_FlLens", new Color(1f, 0.93f, 0.7f), 0.9f);
        d["FU_Glass"] = RoomKit.Plain("R2_FuGlass", new Color(0.85f, 0.82f, 0.7f), 0.8f);
        d["FU_Brass"] = RoomKit.Plain("R2_Brass", new Color(0.78f, 0.55f, 0.18f), 0.6f, 1f);
        d["FU_Wire"] = RoomKit.Plain("R2_Wire", new Color(0.3f, 0.3f, 0.3f), 0.5f, 1f);
        d["FB_Metal"] = RoomKit.Plain("R2_FbMetal", new Color(0.30f, 0.33f, 0.31f), 0.4f, 0.7f);
        d["FB_Dark"] = RoomKit.Plain("R2_FbDark", new Color(0.05f, 0.05f, 0.06f), 0.3f);
        d["FB_Panel"] = RoomKit.Plain("R2_FbPanel", new Color(0.72f, 0.70f, 0.62f), 0.2f);
        d["FB_Red"] = RoomKit.Plain("R2_FbRed", new Color(0.65f, 0.08f, 0.06f), 0.4f);
        d["FB_Yellow"] = RoomKit.Plain("R2_FbYellow", new Color(0.9f, 0.75f, 0.1f), 0.4f);
        d["FB_Brass"] = d["FU_Brass"];
        return d;
    }

    static GameObject MakePrefab(string model, bool collider)
    {
        var mats = PropMats();
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Room2/" + model + ".fbx");
        GameObject go = (GameObject)Object.Instantiate(asset);
        go.name = model;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] src = r.sharedMaterials; Material[] dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Material m; dst[i] = src[i] != null && mats.TryGetValue(src[i].name, out m) ? m : src[i];
            }
            r.sharedMaterials = dst;
        }
        if (collider) RoomKit.FitCollider(go);
        System.IO.Directory.CreateDirectory("Assets/Prefabs/Room2");
        string path = "Assets/Prefabs/Room2/" + model + ".prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return saved;
    }

    public static string Build()
    {
        var log = new System.Text.StringBuilder();
        Scene0(log);
        return log.ToString();
    }

    static void Scene0(System.Text.StringBuilder log)
    {
        MakePrefab("Flashlight", true); MakePrefab("Fuse", true); MakePrefab("FuseBox", false);
        var sc = EditorSceneManager.OpenScene("Assets/Scenes/Room02.unity", OpenSceneMode.Single);

        foreach (string n in new[] { "StaffRoom_Root", "StaffRoom_Exterior" })
        { GameObject g = GameObject.Find(n); if (g != null) Object.DestroyImmediate(g); }

        fixture = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RoomSeven/RS_LightFixture_Emissive.mat");

        GameObject root = new GameObject("Room02_Root");
        walls = RoomKit.Group("Shell", root.transform).transform;
        lighting = RoomKit.Group("Lighting", root.transform).transform;
        props = RoomKit.Group("Props", root.transform).transform;

        // ---- floors and ceilings (no overlaps) ----
        Slab("Floor_Leg1", walls, -1.3f, -0.1f, 1.3f, 7.9f, -0.05f, "floor");
        Slab("Floor_Leg2", walls, -1.3f, 7.9f, 12.1f, 10.5f, -0.05f, "floor");
        Slab("Floor_Elec", walls, 1.3f, 1.3f, 5.3f, 6.3f, -0.05f, "concrete");
        Slab("Ceil_Leg1", walls, -1.3f, -0.1f, 1.3f, 7.9f, H + 0.05f, "ceiling");
        Slab("Ceil_Leg2", walls, -1.3f, 7.9f, 12.1f, 10.5f, H + 0.05f, "ceiling");
        Slab("Ceil_Elec", walls, 1.3f, 1.3f, 5.3f, 6.3f, H + 0.05f, "concrete");

        // ---- walls: L-shaped corridor, door A (start wall), door C (to the electricity room), door B (far end) ----
        Wall("Wall_A_South", walls, true, 0f, -1.2f, 1.2f, new[] { 0f });
        Wall("Wall_Leg1_East", walls, false, 1.2f, 0f, 8f, new[] { 3.8f });
        Wall("Wall_Leg2_South", walls, true, 8f, 1.2f, 12f, null);
        Wall("Wall_B_End", walls, false, 12f, 8f, 10.4f, new[] { 9.2f });
        Wall("Wall_North", walls, true, 10.4f, -1.2f, 12f, null);
        Wall("Wall_West", walls, false, -1.2f, 0f, 10.4f, null);
        Material grey = RoomKit.Plain("R2_ConcreteWall", new Color(0.36f, 0.37f, 0.36f), 0.1f);
        Wall("Elec_South", walls, true, 1.3f, 1.2f, 5.2f, null, grey);
        Wall("Elec_North", walls, true, 6.3f, 1.2f, 5.2f, null, grey);
        Wall("Elec_East", walls, false, 5.2f, 1.3f, 6.3f, null, grey);

        // ---- doors ----
        Door("Door_A", new Vector3(0f, 0f, 0f), 0f, false, "HotelFloor_Corridor", "Go back", false);
        Door("Door_B", new Vector3(12f, 0f, 9.2f), -90f, false, "HotelFloor_Corridor", "Go through", true);
        Door("Door_C", new Vector3(1.2f, 0f, 3.8f), -90f, true, null, null, false);

        // ---- lights ----
        Lamp("Lamp_1", new Vector3(0f, 2.6f, 2.5f));
        Lamp("Lamp_2", new Vector3(0f, 2.6f, 6.0f));
        Lamp("Lamp_3", new Vector3(0.5f, 2.6f, 9.2f));
        Lamp("Lamp_4", new Vector3(4.5f, 2.6f, 9.2f));
        Lamp("Lamp_5", new Vector3(8.5f, 2.6f, 9.2f));
        Lamp("Lamp_Elec", new Vector3(3.2f, 2.5f, 3.8f), true);

        // ---- identical paintings: the same one everywhere, to make the loop feel wrong ----
        Material poster = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RoomSeven/Poster_PopularFigure_01.mat");
        Material frame = RoomKit.Plain("R2_Frame", new Color(0.18f, 0.12f, 0.07f), 0.3f);
        Vector3[] pp = { new Vector3(-1.09f, 1.6f, 4.8f), new Vector3(-1.09f, 1.6f, 7.2f), new Vector3(3f, 1.6f, 10.29f), new Vector3(7f, 1.6f, 10.29f) };
        Vector3[] pd = { Vector3.right, Vector3.right, Vector3.back, Vector3.back };
        for (int i = 0; i < pp.Length; i++)
        {
            RoomKit.Box("Frame_" + i, props, pp[i] - pd[i] * 0.01f, Mathf.Abs(pd[i].x) > 0.5f ? new Vector3(0.04f, 0.76f, 0.56f) : new Vector3(0.56f, 0.76f, 0.04f), frame, false);
            RoomKit.Quad("Picture_" + i, props, pp[i], Quaternion.LookRotation(-pd[i]), new Vector2(0.48f, 0.68f), poster);
        }

        // ---- electricity room dressing ----
        Material metal = RoomKit.Plain("R2_Pipe", new Color(0.30f, 0.30f, 0.28f), 0.4f, 0.7f);
        Material wood = RoomKit.Plain("R2_Crate", new Color(0.35f, 0.25f, 0.15f), 0.2f);
        RoomKit.Box("Crate_1", props, new Vector3(2.0f, 0.3f, 5.5f), new Vector3(0.7f, 0.6f, 0.7f), wood);
        RoomKit.Box("Crate_2", props, new Vector3(2.8f, 0.2f, 5.7f), new Vector3(0.5f, 0.4f, 0.5f), wood);
        GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipe.name = "Pipe"; pipe.transform.SetParent(props, false);
        pipe.transform.position = new Vector3(4.95f, 1.45f, 5.8f); pipe.transform.localScale = new Vector3(0.12f, 1.45f, 0.12f);
        pipe.GetComponent<Renderer>().sharedMaterial = metal;

        // ---- the fusebox + close-up camera ----
        GameObject boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Room2/FuseBox.prefab");
        GameObject fb = (GameObject)PrefabUtility.InstantiatePrefab(boxPrefab, props);
        PrefabUtility.UnpackPrefabInstance(fb, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        fb.name = "FuseBox"; fb.transform.position = new Vector3(5.1f, 1.0f, 3.8f); fb.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

        GameObject pivot = new GameObject("CoverPivot"); pivot.transform.SetParent(fb.transform, false);
        pivot.transform.localPosition = new Vector3(-0.23f, 0.31f, 0.12f);
        foreach (string n in new[] { "FB_Cover", "FB_Handle", "FB_Warning" })
            fb.transform.Find(n).SetParent(pivot.transform, true);

        GameObject fuseAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Room2/Fuse.prefab");
        GameObject slotFuse = (GameObject)PrefabUtility.InstantiatePrefab(fuseAsset, fb.transform);
        PrefabUtility.UnpackPrefabInstance(slotFuse, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        slotFuse.name = "InsertedFuse";
        foreach (Collider c in slotFuse.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        slotFuse.transform.localPosition = new Vector3(0f, 0.09f, 0.05f); slotFuse.transform.localRotation = Quaternion.identity;
        slotFuse.transform.localScale = Vector3.one * 1.6f;   // a chunky fuse, easy to read on screen

        BoxCollider col = fb.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.31f, 0.07f); col.size = new Vector3(0.46f, 0.62f, 0.16f);

        GameObject camGo = new GameObject("FuseCloseUp");
        camGo.transform.position = new Vector3(4.25f, 1.38f, 3.8f);
        camGo.transform.rotation = Quaternion.Euler(4f, 90f, 0f);
        CinemachineCamera vcam = camGo.AddComponent<CinemachineCamera>();
        vcam.Lens = new LensSettings { FieldOfView = 42f, NearClipPlane = 0.05f, FarClipPlane = 100f, ModeOverride = LensSettings.OverrideModes.None };
        vcam.Priority.Enabled = true; vcam.Priority.Value = 0;

        FuseBox fbs = fb.AddComponent<FuseBox>();
        var fso = new SerializedObject(fbs);
        fso.FindProperty("cover").objectReferenceValue = pivot.transform;
        fso.FindProperty("coverOpenAngle").floatValue = -110f;
        fso.FindProperty("fuse").objectReferenceValue = slotFuse.transform;
        fso.FindProperty("fuseOutOffset").vector3Value = new Vector3(0f, 0f, 0.3f);
        fso.FindProperty("closeUp").objectReferenceValue = vcam;
        fso.ApplyModifiedPropertiesWithoutUndo();

        // ---- spawn + logic ----
        SpawnPoint sp = Object.FindFirstObjectByType<SpawnPoint>();
        sp.SetSpawnId("FromCorridor");
        sp.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.1f), Quaternion.identity);

        GameObject logic = new GameObject("Room2Logic");
        logic.AddComponent<Room2Controller>();
        logic.AddComponent<PowerReactor>();

        RenderSettings.ambientLight = new Color(0.11f, 0.095f, 0.085f);
        RenderSettings.ambientIntensity = 1f;

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == "Assets/Scenes/Room02.unity"))
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Room02.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
        log.AppendLine("built Room02");
    }
}
