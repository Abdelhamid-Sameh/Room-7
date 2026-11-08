using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the mirror room inside Limbo_Corridor, past the Game Boy: 6 m wide, 12.6 m deep, 4 m high, a glass wall
/// across the middle. Nabil's half is bare and worn (concrete, peeling wallpaper, a cold flickering bulb); the copy's
/// half is the same room kept well (polished wood, clean wallpaper, a lamp, a clock). Called by LimboBuilder.
/// Room-local coordinates: x across, z from the Game Boy room's north wall (0) to the far wall (12.6); the glass is at z = 6.3.
/// </summary>
public static class MirrorRoomBuilder
{
    const float W = 3.0f, H = 4.0f, D = 12.6f, ZM = 6.3f;
    const float OriginZ = 22.8f;                  // world z of the room's local z = 0 (outer face of the Game Boy room's north wall)
    const string Tex = "Assets/Textures/Mirror/";
    const string Models = "Assets/Models/Mirror/";
    const string Aud = "Assets/Audio/Mirror/";

    static Material Pbr(string name, string folder, string stem, float uM, float vM, float tile, float smooth, float tint = 1f)
    {
        string path = RoomKit.MaterialDir + "Mirror_" + name + "_" + uM.ToString("0.0") + "x" + vM.ToString("0.0") + ".mat";
        System.IO.Directory.CreateDirectory(RoomKit.MaterialDir);
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        string baseDir = Tex + folder + "/" + stem;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseDir + "_Color.jpg"));
        Texture2D n = AssetDatabase.LoadAssetAtPath<Texture2D>(baseDir + "_NormalGL.jpg");
        if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
        Vector2 sc = new Vector2(uM / tile, vM / tile);
        m.SetTextureScale("_BaseMap", sc); m.SetTextureScale("_BumpMap", sc);
        m.SetColor("_BaseColor", new Color(tint, tint, tint, 1f));
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static Material Plain(string n, Color c, float s = 0.3f, float met = 0f, Color? em = null) { return RoomKit.Plain("Mirror_" + n, c, s, met, em); }

    static Material Glass()
    {
        string path = RoomKit.MaterialDir + "Mirror_Glass.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 3f);   // premultiply: keeps the glints
        m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        m.SetColor("_BaseColor", new Color(0.55f, 0.7f, 0.75f, 0.10f));
        m.SetFloat("_Smoothness", 0.97f); m.SetFloat("_Metallic", 0f);
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static Material ModelMat(string name, string color, string normal, string emissive, float smooth, float metal)
    {
        string path = RoomKit.MaterialDir + "Mirror_" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
        m.SetColor("_BaseColor", Color.white);
        if (normal != null) { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); m.EnableKeyword("_NORMALMAP"); }
        if (emissive != null) { m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(emissive)); m.SetColor("_EmissionColor", Color.white * 1.4f); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Box(string n, Transform p, Vector3 c, Vector3 s, Material m, bool col = true) { return RoomKit.Box(n, p, c, s, m, col); }

    static void Set(Object o, System.Action<SerializedObject> fill) { SerializedObject so = new SerializedObject(o); fill(so); so.ApplyModifiedPropertiesWithoutUndo(); }

    /// <summary>Puts a model on the floor at (x,z): scaled to 'height', turned to 'yaw', bounds-centred, with a box collider.</summary>
    static GameObject Place(string path, Transform parent, Vector3 floorPos, float yaw, float height, Material mat, string name, float pitch = 0f)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = name;
        go.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        go.transform.position = Vector3.zero; go.transform.localScale = Vector3.one;
        Bounds b = RoomKit.WorldBounds(go);
        go.transform.localScale = Vector3.one * (height / b.size.y);
        b = RoomKit.WorldBounds(go);
        go.transform.position += new Vector3(floorPos.x - b.center.x, floorPos.y - b.min.y, floorPos.z - b.center.z);
        if (mat != null) foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) { Material[] ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; }
        b = RoomKit.WorldBounds(go);
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.center = go.transform.InverseTransformPoint(b.center);
        Vector3 ls = go.transform.lossyScale; bc.size = new Vector3(b.size.x / ls.x, b.size.y / ls.y, b.size.z / ls.z);
        return go;
    }

    static AudioClip Clip(string f) { return AssetDatabase.LoadAssetAtPath<AudioClip>(Aud + f); }

    public static MirrorRoom Build(GameObject gameBoyLights)
    {
        Transform root = new GameObject("Limbo_Mirror_Root").transform;
        Transform shell = RoomKit.Group("Shell", root).transform;
        Transform props = RoomKit.Group("Props", root).transform;
        Transform lighting = RoomKit.Group("Lighting", root).transform;
        float half = ZM - 0.15f;                 // each half runs 6.15 m
        float w2 = W * 2f;

        // ---------------- materials ----------------
        Material hFloor = Pbr("Concrete048", "Concrete048_2K-JPG", "Concrete048_2K-JPG", w2, half, 2.0f, 0.2f, 0.55f);
        Material hCeil = Pbr("Concrete034", "Concrete034_2K-JPG", "Concrete034_2K-JPG", w2, half, 2.0f, 0.1f, 0.35f);
        Material cFloor = Pbr("Wood", "WoodFloor064_2K-JPG", "WoodFloor064_2K-JPG", w2, half, 2.0f, 0.6f, 0.9f);
        Material cCeil = Plain("CopyCeiling", new Color(0.55f, 0.50f, 0.42f), 0.1f);
        Material tileStrip = Pbr("Tiles", "Tiles074_2K-JPG", "Tiles074_2K-JPG", w2, 0.3f, 1.0f, 0.5f, 0.5f);
        Material darkWood = Plain("DarkWood", new Color(0.10f, 0.06f, 0.04f), 0.4f);
        Material black = Plain("Black", Color.black, 0f);
        Material trim = Plain("Trim", new Color(0.06f, 0.07f, 0.06f), 0.3f);

        // ---------------- floors, ceilings ----------------
        Box("Floor_You", shell, new Vector3(0f, -0.05f, half / 2f), new Vector3(w2 + 0.2f, 0.1f, half), hFloor);
        Box("Floor_Glass", shell, new Vector3(0f, -0.05f, ZM), new Vector3(w2 + 0.2f, 0.1f, 0.3f), tileStrip);
        Box("Floor_Copy", shell, new Vector3(0f, -0.05f, ZM + 0.15f + half / 2f), new Vector3(w2 + 0.2f, 0.1f, half), cFloor);
        Box("Ceiling_You", shell, new Vector3(0f, H + 0.05f, half / 2f), new Vector3(w2 + 0.2f, 0.1f, half), hCeil);
        Box("Ceiling_Glass", shell, new Vector3(0f, H + 0.05f, ZM), new Vector3(w2 + 0.2f, 0.1f, 0.3f), darkWood);
        Box("Ceiling_Copy", shell, new Vector3(0f, H + 0.05f, ZM + 0.15f + half / 2f), new Vector3(w2 + 0.2f, 0.1f, half), cCeil);

        // ---------------- walls ----------------
        // Nabil's half: wallpaper gone sour
        float yH = H / 2f;
        Material wYouSide = Pbr("WallpaperWorn", "Wallpaper001C_worn_2K-JPG", "Wallpaper001C_2K-JPG", half, H, 1.5f, 0.1f, 0.7f);
        Material wCopySide = Pbr("Wallpaper", "Wallpaper001A_2K-JPG", "Wallpaper001A_2K-JPG", half, H, 1.5f, 0.25f, 0.9f);
        Material wYouBack = Pbr("WallpaperWorn", "Wallpaper001C_worn_2K-JPG", "Wallpaper001C_2K-JPG", W * 2f, H, 1.5f, 0.1f, 0.7f);
        Material wCopyBack = Pbr("Wallpaper", "Wallpaper001A_2K-JPG", "Wallpaper001A_2K-JPG", W * 2f, H, 1.5f, 0.25f, 0.9f);

        Box("Wall_West_You", shell, new Vector3(-W - 0.05f, yH, half / 2f), new Vector3(0.1f, H, half), wYouSide);
        Box("Wall_West_Copy", shell, new Vector3(-W - 0.05f, yH, ZM + 0.15f + half / 2f), new Vector3(0.1f, H, half), wCopySide);
        Box("Wall_East_Copy", shell, new Vector3(W + 0.05f, yH, ZM + 0.15f + half / 2f), new Vector3(0.1f, H, half), wCopySide);
        Box("Wall_Glass_West", shell, new Vector3(-W - 0.05f, yH, ZM), new Vector3(0.1f, H, 0.3f), darkWood);
        Box("Wall_Glass_East", shell, new Vector3(W + 0.05f, yH, ZM), new Vector3(0.1f, H, 0.3f), darkWood);
        // east wall of Nabil's half has the exit doorway at z 1.1 - 2.1 (2.1 m high)
        float dz0 = 1.1f, dz1 = 2.1f;
        Material wEastA = Pbr("WallpaperWorn", "Wallpaper001C_worn_2K-JPG", "Wallpaper001C_2K-JPG", dz0, H, 1.5f, 0.1f, 0.7f);
        Material wEastB = Pbr("WallpaperWorn", "Wallpaper001C_worn_2K-JPG", "Wallpaper001C_2K-JPG", half - dz1, H, 1.5f, 0.1f, 0.7f);
        Material wEastC = Pbr("WallpaperWorn", "Wallpaper001C_worn_2K-JPG", "Wallpaper001C_2K-JPG", dz1 - dz0, H - 2.1f, 1.5f, 0.1f, 0.7f);
        Box("Wall_East_You_A", shell, new Vector3(W + 0.05f, yH, dz0 / 2f), new Vector3(0.1f, H, dz0), wEastA);
        Box("Wall_East_You_B", shell, new Vector3(W + 0.05f, yH, dz1 + (half - dz1) / 2f), new Vector3(0.1f, H, half - dz1), wEastB);
        Box("Wall_East_You_Top", shell, new Vector3(W + 0.05f, 2.1f + (H - 2.1f) / 2f, (dz0 + dz1) / 2f), new Vector3(0.1f, H - 2.1f, dz1 - dz0), wEastC);
        // far wall (copy's side)
        Box("Wall_North", shell, new Vector3(0f, yH, D + 0.05f), new Vector3(w2 + 0.2f, H, 0.1f), wCopyBack);
        // near wall: a veneer in front of the Game Boy room's north wall, with the doorway kept
        float sideW = W - 0.45f;
        Material wSouthSide = Pbr("WallpaperWorn", "Wallpaper001C_worn_2K-JPG", "Wallpaper001C_2K-JPG", sideW, H, 1.5f, 0.1f, 0.7f);
        Material wSouthTop = Pbr("WallpaperWorn", "Wallpaper001C_worn_2K-JPG", "Wallpaper001C_2K-JPG", 0.9f, H - 2.1f, 1.5f, 0.1f, 0.7f);
        Box("Wall_South_L", shell, new Vector3(-(0.45f + sideW / 2f), yH, 0.03f), new Vector3(sideW, H, 0.06f), wSouthSide);
        Box("Wall_South_R", shell, new Vector3(0.45f + sideW / 2f, yH, 0.03f), new Vector3(sideW, H, 0.06f), wSouthSide);
        Box("Wall_South_Top", shell, new Vector3(0f, 2.1f + (H - 2.1f) / 2f, 0.03f), new Vector3(0.9f, H - 2.1f, 0.06f), wSouthTop);

        // ---------------- the glass wall ----------------
        // frame: 1.9 x 1.2 m model scaled x3 -> 5.7 x 3.6 m, centred on the wall
        GameObject frame = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + "ornate-gold-vintage-frame/source/frame.glb"), props);
        frame.name = "Mirror_Frame";
        float cy = 1.4f;   // lowered so the frame's bottom border no longer hides the copy side's floor and plate
        frame.transform.position = new Vector3(0f, cy, ZM);
        frame.transform.localScale = Vector3.one * 3.0f;
        float fw = 1.9f * 3.0f, fh = 1.2f * 3.0f;
        float fy0 = Mathf.Max(0.05f, cy - fh / 2f), fy1 = cy + fh / 2f;
        Box("Glass_Under", shell, new Vector3(0f, fy0 / 2f, ZM), new Vector3(w2 + 0.2f, fy0, 0.3f), darkWood);
        Box("Glass_Over", shell, new Vector3(0f, fy1 + (H - fy1) / 2f, ZM), new Vector3(w2 + 0.2f, H - fy1, 0.3f), darkWood);
        float sideGap = W - fw / 2f;
        Box("Glass_Side_W", shell, new Vector3(-(W - sideGap / 2f), cy, ZM), new Vector3(sideGap, fh, 0.3f), darkWood);
        Box("Glass_Side_E", shell, new Vector3(W - sideGap / 2f, cy, ZM), new Vector3(sideGap, fh, 0.3f), darkWood);
        GameObject pane = Box("Glass", props, new Vector3(0f, (fy0 + fy1) / 2f, ZM), new Vector3(fw, fy1 - fy0, 0.04f), Glass());
        pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MirrorGlass mg = pane.AddComponent<MirrorGlass>();

        // ---------------- furniture ----------------
        List<Collider> blockers = new List<Collider>();
        Material concreteBlock = Pbr("Concrete034", "Concrete034_2K-JPG", "Concrete034_2K-JPG", 3.6f, 0.95f, 1.5f, 0.15f, 0.7f);
        // Nabil's side: a long concrete block wall across the left of the room (gap on the right)
        Material concreteBlock2 = Pbr("Concrete034", "Concrete034_2K-JPG", "Concrete034_2K-JPG", 3.9f, 0.8f, 1.5f, 0.15f, 0.7f);
        Box("Divider_You_A", props, new Vector3(-1.05f, 0.4f, 1.6f), new Vector3(3.9f, 0.8f, 0.5f), concreteBlock2);
        Material concreteBlock3 = Pbr("Concrete034", "Concrete034_2K-JPG", "Concrete034_2K-JPG", 3.6f, 0.8f, 1.5f, 0.15f, 0.7f);
        Box("Divider_You_B", props, new Vector3(1.2f, 0.4f, 3.05f), new Vector3(3.6f, 0.8f, 0.5f), concreteBlock3);
        // the copy's side: a polished sideboard, in the left of the far half (= the player's right, reflected)
        Material sideboard = Pbr("Wood", "WoodFloor064_2K-JPG", "WoodFloor064_2K-JPG", 3.0f, 0.9f, 1.0f, 0.6f, 0.6f);
        GameObject sb = Box("Sideboard_Copy", props, new Vector3(-2.25f, 0.45f, 8.6f), new Vector3(1.5f, 0.9f, 0.5f), sideboard);
        blockers.Add(sb.GetComponent<Collider>());

        // the life he drew for himself: the antique set (URP copies of the package's materials)
        string lp = "Assets/LouisPhilippeAntiqueFurniture/";
        Material chairM = ModelMat("LP_Chair", lp + "texmap/LouisPhilippe_chair_AlbedoTransparency.png", lp + "texmap/LouisPhilippe_chair_Normal.png", null, 0.5f, 0.1f);
        Material chairP = ModelMat("LP_ChairPillow", lp + "texmap/LouisPhilippe_pillow chair_AlbedoTransparency.png", lp + "texmap/LouisPhilippe_pillow chair_Normal.png", null, 0.4f, 0f);
        Material tableM = ModelMat("LP_Table", lp + "texmap/LouisPhilippe_table_AlbedoTransparency.png", lp + "texmap/LouisPhilippe_table_Normal.png", null, 0.55f, 0.1f);
        GameObject tbl = Place(lp + "models/LPTable.fbx", props, new Vector3(0.2f, 0f, 9.0f), 0f, 0.47f, tableM, "Table_Copy");
        blockers.Add(tbl.GetComponent<Collider>());
        GameObject chr = Place(lp + "models/LPChair.fbx", props, new Vector3(1.13f, 0f, 9.05f), 264.32f, 1.25f, chairM, "Chair_Copy");
        foreach (Renderer r in chr.GetComponentsInChildren<Renderer>()) if (r.name.StartsWith("a_padding")) r.sharedMaterial = chairP;
        blockers.Add(chr.GetComponent<Collider>());

        Material clockMat = ModelMat("Clock", Models + "antique-clock/textures/Clock_default_color.jpg", Models + "antique-clock/textures/Clock_default_nmap.jpg", null, 0.55f, 0.25f);
        GameObject clock = Place(Models + "antique-clock/model/G.obj", props, new Vector3(2.5f, 0f, 12.2f), 90f, 2.1f, clockMat, "Clock_Copy");
        blockers.Add(clock.GetComponent<Collider>());
        Material lampMat = ModelMat("Lamp", Models + "floor-lamp/model/model/textures/DefaultMaterial_albedo.jpg", Models + "floor-lamp/model/model/textures/DefaultMaterial_normal.jpg", Models + "floor-lamp/model/model/textures/DefaultMaterial_emissive.jpg", 0.5f, 0.3f);
        GameObject lamp = Place(Models + "floor-lamp/model/model/model.dae", props, new Vector3(-2.5f, 0f, 12.0f), 0f, 2.0f, lampMat, "Lamp_Copy");
        blockers.Add(lamp.GetComponent<Collider>());

        // ---------------- plates ----------------
        Material plateMat = Pbr("Tiles", "Tiles074_2K-JPG", "Tiles074_2K-JPG", 1.1f, 1.1f, 1.0f, 0.4f, 0.55f);
        Material glowMat = Plain("PlateGlow", new Color(0.45f, 0.30f, 0.10f), 0.3f, 0f, new Color(2.6f, 1.5f, 0.4f));
        Vector3 pYou = new Vector3(1.6f, 0f, 5.0f);
        Vector3 pCopy = new Vector3(-1.6f, 0f, D - 5.0f);
        Transform tYou, tCopy; Renderer gYou, gCopy;
        MakePlate("Plate_You", props, pYou, plateMat, glowMat, out tYou, out gYou);
        MakePlate("Plate_Copy", props, pCopy, plateMat, glowMat, out tCopy, out gCopy);

        // ---------------- Nabil's real life: the housekeeping side ----------------
        // Everything here sits where a reflected copy-side blocker stops Nabil, so no wall is ever invisible.
        string cartDir = "Assets/Models/Cleaning cart/";
        Material cartMat = ModelMat("Cart", cartDir + "textures/Cleaning_Cart_Unwrapped_Cleaning_Cart_Base.png", cartDir + "textures/Cleaning_Cart_Unwrapped_Cleaning_Cart_Norm.png", null, 0.45f, 0.5f);
        Place(cartDir + "source/Cleaning_Cart.fbx", props, new Vector3(2.25f, 0f, 4.0f), 90f, 1.25f, cartMat, "Cart_You", -90f);
        string lockerPrefab = "Assets/Low-Poly 3D Lockers/Low-Poly 3D Lockers/Prefabs/M.C.C.prefab";
        for (int li = 0; li < 2; li++) Place(lockerPrefab, props, new Vector3(-2.75f, 0f, 1.05f + li * 0.52f), 90f, 1.99f, null, "Locker_You_" + li);
        // coat rail on the west wall with the uniform hanging from it
        Material steel = Plain("RailSteel", new Color(0.45f, 0.46f, 0.48f), 0.5f, 0.7f);
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rail.name = "Rail_You"; rail.transform.SetParent(props, false);
        rail.transform.localPosition = new Vector3(-2.82f, 1.77f, 4.6f); rail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); rail.transform.localScale = new Vector3(0.04f, 0.8f, 0.04f);
        rail.GetComponent<Renderer>().sharedMaterial = steel; Object.DestroyImmediate(rail.GetComponent<Collider>());
        foreach (float rz in new[] { 3.8f, 5.4f })
            Box("Rail_Bracket", props, new Vector3(-2.9f, 1.77f, rz), new Vector3(0.2f, 0.04f, 0.04f), steel, false);
        // hanger: stem + hook over the rail + shoulder bar, so the uniform visibly hangs
        foreach (var hp in new[] { new object[] { "Hanger_Stem", PrimitiveType.Cylinder, new Vector3(-2.80f, 1.80f, 4.6f), new Vector3(0.012f, 0.05f, 0.012f), Vector3.zero }, new object[] { "Hanger_Hook", PrimitiveType.Cylinder, new Vector3(-2.80f, 1.83f, 4.6f), new Vector3(0.012f, 0.03f, 0.012f), new Vector3(90f, 0f, 0f) }, new object[] { "Hanger_Bar", PrimitiveType.Cube, new Vector3(-2.80f, 1.61f, 4.6f), new Vector3(0.02f, 0.012f, 0.6f), Vector3.zero } })
        {
            GameObject hg = GameObject.CreatePrimitive((PrimitiveType)hp[1]);
            hg.name = (string)hp[0]; hg.transform.SetParent(props, false);
            hg.transform.localPosition = (Vector3)hp[2]; hg.transform.localScale = (Vector3)hp[3]; hg.transform.localRotation = Quaternion.Euler((Vector3)hp[4]);
            hg.GetComponent<Renderer>().sharedMaterial = steel; Object.DestroyImmediate(hg.GetComponent<Collider>());
        }
        GameObject uni = Place("Assets/Models/HangingUniform/HangingUniform.fbx", props, new Vector3(-2.7f, 0.55f, 4.6f), 90f, 1.2f, null, "Uniform_You");
        Object.DestroyImmediate(uni.GetComponent<BoxCollider>());
        Material pail = Plain("Pail", new Color(0.35f, 0.38f, 0.40f), 0.5f, 0.4f);
        GameObject bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bucket.name = "Bucket_You"; bucket.transform.SetParent(props, false);
        bucket.transform.localPosition = new Vector3(2.75f, 0.25f, 4.0f); bucket.transform.localScale = new Vector3(0.45f, 0.25f, 0.45f);
        bucket.GetComponent<Renderer>().sharedMaterial = pail;
        Box("Crate_Cart", props, new Vector3(1.7f, 0.2f, 4.0f), new Vector3(0.4f, 0.4f, 0.4f), concreteBlock);
        Box("Boxes_You_A", props, new Vector3(-2.65f, 0.3f, 5.6f), new Vector3(0.7f, 0.6f, 0.6f), concreteBlock);
        Box("Boxes_You_B", props, new Vector3(-2.55f, 0.75f, 5.65f), new Vector3(0.45f, 0.3f, 0.45f), concreteBlock);
        GameObject shirt = Place("Assets/Models/Folded shirt/source/simple folded shirt.obj", props, new Vector3(-2.55f, 0.9f, 5.65f), 0f, 0.05f, null, "Shirt_You");
        Object.DestroyImmediate(shirt.GetComponent<BoxCollider>());

        // ---------------- exit door (Nabil's side, east wall) ----------------
        Material doorMat = Plain("ExitDoor", new Color(0.22f, 0.20f, 0.17f), 0.4f);
        GameObject hinge = new GameObject("Exit_Hinge");
        hinge.transform.SetParent(props, false);
        hinge.transform.position = new Vector3(W, 0f, dz0 + 0.02f);
        GameObject leaf = Box("Exit_Leaf", hinge.transform, Vector3.zero, new Vector3(0.06f, 2.08f, dz1 - dz0 - 0.04f), doorMat);
        leaf.transform.localPosition = new Vector3(0f, 1.04f, (dz1 - dz0 - 0.04f) / 2f);
        GameObject handle = Box("Exit_Handle", leaf.transform, Vector3.zero, Vector3.one, Plain("Metal", new Color(0.55f, 0.55f, 0.5f), 0.7f), false);
        handle.transform.localPosition = new Vector3(-0.9f, -0.04f, 0.36f);
        handle.transform.localScale = new Vector3(1.4f, 0.04f, 0.12f);
        LimboDoor door = hinge.AddComponent<LimboDoor>();
        Set(door, so =>
        {
            so.FindProperty("openAngle").floatValue = 95f;
            so.FindProperty("Locked").boolValue = true;
            so.FindProperty("openClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free Wood Door Pack/Audio/Door_Open.wav");
        });
        Box("Exit_Void", shell, new Vector3(W + 0.75f, 1.2f, (dz0 + dz1) / 2f), new Vector3(1.2f, 2.4f, 1.4f), black, true);
        GameObject lockBox = Box("Exit_LockLight", props, new Vector3(W - 0.04f, 2.35f, (dz0 + dz1) / 2f), new Vector3(0.05f, 0.12f, 0.12f), Plain("LockLight", new Color(0.6f, 0.05f, 0.05f), 0.2f, 0f, new Color(2.2f, 0.1f, 0.1f)), false);

        // ---------------- lights ----------------
        Material bulbMat = Plain("Bulb", new Color(0.8f, 0.9f, 1f), 0.1f, 0f, new Color(1.2f, 1.4f, 1.6f));
        GameObject fix = Box("Bulb_You_Fixture", lighting, new Vector3(0f, H - 0.06f, 2.9f), new Vector3(0.5f, 0.05f, 0.14f), bulbMat, false);
        Light cold = Point("Bulb_You", lighting, new Vector3(0f, H - 0.3f, 2.9f), new Color(0.72f, 0.88f, 1f), 3.0f, 8f);
        FlickerLight fl = cold.gameObject.AddComponent<FlickerLight>();
        Set(fl, so => { so.FindProperty("baseIntensity").floatValue = 3.0f; so.FindProperty("wobble").floatValue = 0.25f; so.FindProperty("dropEvery").floatValue = 3.2f; so.FindProperty("bulb").objectReferenceValue = fix.GetComponent<Renderer>(); });
        Light cold2 = Point("Glow_You", lighting, new Vector3(1.5f, 2.0f, 5.0f), new Color(0.6f, 0.75f, 0.9f), 1.2f, 3.5f);
        Light warm = Point("Chandelier_Copy", lighting, new Vector3(0f, H - 0.4f, 9.8f), new Color(1f, 0.78f, 0.5f), 6.0f, 10f);
        Point("Copy_Fill", lighting, new Vector3(0f, 2.2f, 10.6f), new Color(1f, 0.8f, 0.55f), 3.0f, 5.5f);
        Point("Glow_Copy", lighting, new Vector3(-1.6f, 1.6f, D - 5.0f), new Color(1f, 0.8f, 0.5f), 1.6f, 3.5f);
        Point("Fill_Copy_Floor", lighting, new Vector3(0f, 1.4f, 7.2f), new Color(1f, 0.85f, 0.6f), 2.0f, 6f);
        Point("Fill_You_Back", lighting, new Vector3(-0.8f, 2.2f, 1.2f), new Color(0.6f, 0.75f, 0.95f), 1.5f, 5f);
        Light lampLight = Point("Lamp_Copy_Light", lighting, lamp.transform.position + new Vector3(0f, 1.55f, 0f), new Color(1f, 0.72f, 0.4f), 2.8f, 5.5f);
        cold.shadows = LightShadows.Soft; warm.shadows = LightShadows.Soft;

        // ---------------- the copy ----------------
        GameObject copyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Mirror/Copy/MirrorNabil.prefab");
        GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(copyPrefab, root);
        copy.name = "Nabil_Copy";
        copy.transform.position = new Vector3(0f, 0f, D - 0.6f);
        copy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        Transform copyCentre = new GameObject("CopySide_Centre").transform;
        copyCentre.SetParent(root, false); copyCentre.position = new Vector3(0f, 1.5f, D - 2.5f);

        // ---------------- the brains ----------------
        GameObject brain = new GameObject("MirrorRoom");
        brain.transform.SetParent(root, false);
        MirrorRoom mr = brain.AddComponent<MirrorRoom>();
        Cubemap env = AssetDatabase.LoadAssetAtPath<Cubemap>(Tex + "IndoorEnvironmentHDRI014_2K/IndoorEnvironmentHDRI014_2K_HDR.exr");

        root.position = new Vector3(0f, 0f, OriginZ);   // against the Game Boy room's north wall
        Vector3 pivotWorld = root.TransformPoint(new Vector3(0f, 0f, ZM));
        Collider[] bl = blockers.ToArray();
        Set(mr, so =>
        {
            so.FindProperty("copy").objectReferenceValue = copy.transform;
            so.FindProperty("copyAnimator").objectReferenceValue = copy.GetComponent<Animator>();
            so.FindProperty("pivot").vector3Value = pivotWorld;
            SerializedProperty arr = so.FindProperty("copyBlockers");
            arr.arraySize = bl.Length;
            for (int i = 0; i < bl.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = bl[i];
            so.FindProperty("plateYou").objectReferenceValue = tYou;
            so.FindProperty("plateCopy").objectReferenceValue = tCopy;
            so.FindProperty("glowYou").objectReferenceValue = gYou;
            so.FindProperty("glowCopy").objectReferenceValue = gCopy;
            so.FindProperty("exitDoor").objectReferenceValue = door;
            so.FindProperty("lockLight").objectReferenceValue = lockBox.GetComponent<Renderer>();
            so.FindProperty("roomLights").objectReferenceValue = lighting.gameObject;
            so.FindProperty("gameBoyLights").objectReferenceValue = gameBoyLights;
            so.FindProperty("copySideCentre").objectReferenceValue = copyCentre;
            so.FindProperty("reflection").objectReferenceValue = env;
            so.FindProperty("musicBox").objectReferenceValue = Clip("335966__newagesoup__music-box-bridge-of-avignon.wav");
            so.FindProperty("drone").objectReferenceValue = Clip("freesound_community-low-pitched-drone-scary-34446.mp3");
            so.FindProperty("plateClick").objectReferenceValue = Clip("heavy_stone_button_p_#2-1794083174758.mp3");
            so.FindProperty("sting").objectReferenceValue = Clip("black_kumizhi-low-thumpy-kick-reverb-hit-494833.mp3");
            so.FindProperty("footsteps").objectReferenceValue = Clip("freesound_community-footsteps-on-wooden-floor-26822.mp3");
            so.FindProperty("knock").objectReferenceValue = Clip("floraphonic-glass-knock-1-189096.mp3");
        });
        Set(mg, so => so.FindProperty("room").objectReferenceValue = mr);
        lighting.gameObject.SetActive(false);   // MirrorRoom switches it on when Nabil walks in
        return mr;
    }

    static void MakePlate(string name, Transform parent, Vector3 pos, Material baseMat, Material glowMat, out Transform plate, out Renderer glow)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        Box("Slab", g.transform, pos + new Vector3(0f, 0.015f, 0f), new Vector3(1.1f, 0.03f, 1.1f), baseMat, false);
        GameObject inner = Box("Glow", g.transform, pos + new Vector3(0f, 0.032f, 0f), new Vector3(0.75f, 0.01f, 0.75f), glowMat, false);
        plate = g.transform; glow = inner.GetComponent<Renderer>();
    }

    static Light Point(string name, Transform parent, Vector3 pos, Color c, float intensity, float range)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }
}
