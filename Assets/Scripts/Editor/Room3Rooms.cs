using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Room 3, part 2: the dressing of the two cleaning rooms (A: the religious guest, C: the chaotic one).</summary>
public static partial class Room3Builder
{
    const string FM = "Assets/Furniture Mega Pack/Prefabs/";
    const string NP = "Assets/nappin/OfficeEssentialsPack/Prefabs/";
    const string GG = "Assets/Gogo Casual Pack/Gogo Casual Free Light Pack/Prefabs/";
    const string PhotoPath = "Assets/Resources/Room3/PeacePhoto.png";

    static Transform tidyGroup, binTarget, closetTarget;
    static Sprite markerSprite;

    // ------------------------------------------------------------ helpers --

    static GameObject Solid(string path, string name, Vector3 pos, float yaw, float scale)
    {
        GameObject g = RoomKit.Prefab(path, props, pos, yaw, scale, name);
        g.layer = 9;
        RoomKit.FitCollider(g);
        return g;
    }

    static GameObject Loose(string path, string name, Vector3 pos, float yaw, float scale)
    {
        return RoomKit.Prefab(path, tidyGroup, pos, yaw, scale, name);
    }

    static TidyItem Tidy(GameObject go, string id, string prompt, int mode, Vector3 tidyPos, Vector3 tidyEuler,
                         Transform dispose = null, Renderer fade = null, string clue = "")
    {
        RoomKit.FitCollider(go);
        TidyItem ti = go.AddComponent<TidyItem>();
        SerializedObject so = new SerializedObject(ti);
        so.FindProperty("itemId").stringValue = id;
        so.FindProperty("tidyPrompt").stringValue = prompt;
        so.FindProperty("mode").enumValueIndex = mode;
        so.FindProperty("tidyPosition").vector3Value = tidyPos;
        so.FindProperty("tidyEuler").vector3Value = tidyEuler;
        if (dispose != null) so.FindProperty("disposeTarget").objectReferenceValue = dispose;
        if (fade != null) { var p = so.FindProperty("fadeOnHold"); p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = fade; }
        so.FindProperty("clueOnTidy").stringValue = clue;
        so.FindProperty("markerSprite").objectReferenceValue = markerSprite;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ti;
    }

    static void Document(GameObject go, string title, string prompt, string resource, string clue)
    {
        DocumentItem d = go.AddComponent<DocumentItem>();
        SerializedObject so = new SerializedObject(d);
        so.FindProperty("title").stringValue = title;
        so.FindProperty("readPrompt").stringValue = prompt;
        var p = so.FindProperty("resourcePaths"); p.arraySize = 1; p.GetArrayElementAtIndex(0).stringValue = resource;
        so.FindProperty("clueId").stringValue = clue;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static float YawFacing(Vector3 viewerDir) { return Mathf.Atan2(-viewerDir.x, -viewerDir.z) * Mathf.Rad2Deg; }

    // ----------------------------------------------------------- textures --

    static string[] Glyph(char c)
    {
        switch (c)
        {
            case '0': return new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" };
            case '1': return new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" };
            case '2': return new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" };
            default:  return new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" };   // 8
        }
    }

    static void WritePng(string path, Color[] px, int w, int h, bool alpha)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.alphaIsTransparency = alpha; ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
    }

    /// <summary>Chunky pixel digits on a plain or transparent background.</summary>
    static string DigitTex(string path, string text, Color ink, Color bg, int cell)
    {
        int n = text.Length, w = (n * 6 + 1) * cell, h = 9 * cell;
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        for (int k = 0; k < n; k++)
        {
            string[] g = Glyph(text[k]);
            for (int r = 0; r < 7; r++)
                for (int c = 0; c < 5; c++)
                {
                    if (g[r][c] != '1') continue;
                    int x0 = (1 + k * 6 + c) * cell, y0 = (1 + 6 - r) * cell;
                    for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++) px[(y0 + y) * w + x0 + x] = ink;
                }
        }
        WritePng(path, px, w, h, bg.a < 1f);
        return path;
    }

    static string TicketTex(string path)
    {
        int w = 320, h = 160;
        Color[] px = new Color[w * h];
        Color paper = new Color(0.93f, 0.88f, 0.74f), red = new Color(0.65f, 0.12f, 0.1f), ink = new Color(0.12f, 0.1f, 0.1f);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = (x < 24 || y < 10 || y > h - 11) ? red : paper;
        for (int y = 0; y < h; y += 10) px[y * w + 40] = red;
        // "08" in big pixels, centred
        int cell = 11, ox = 70, oy = 40;
        for (int k = 0; k < 2; k++)
        {
            string[] g = Glyph(k == 0 ? '0' : '8');
            for (int r = 0; r < 7; r++) for (int c = 0; c < 5; c++)
            {
                if (g[r][c] != '1') continue;
                int x0 = ox + (k * 6 + c) * cell, y0 = oy + (6 - r) * cell - 20;
                for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++) { int yy = y0 + y; if (yy >= 0 && yy < h) px[yy * w + x0 + x] = ink; }
            }
        }
        for (int x = 70; x < 250; x++) { px[18 * w + x] = ink; px[17 * w + x] = ink; }
        WritePng(path, px, w, h, false);
        return path;
    }

    static string RugTex(string path)
    {
        int w = 128, h = 208;
        Color[] px = new Color[w * h];
        Color border = new Color(0.78f, 0.62f, 0.25f), field = new Color(0.10f, 0.32f, 0.22f), arch = new Color(0.16f, 0.45f, 0.32f), dark = new Color(0.05f, 0.18f, 0.12f);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            Color c = field;
            int d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
            if (d < 6) c = border; else if (d < 14) c = dark; else if (d < 17) c = border;
            else
            {   // a mihrab arch near the top end: where the head goes
                float cx = w * 0.5f, cy = h * 0.72f, rx = 34f;
                bool inArch = Mathf.Abs(x - cx) < rx && (y < cy ? y > h * 0.28f : (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (46f * 46f) < 1f);
                if (inArch) c = arch;
            }
            px[y * w + x] = c;
        }
        WritePng(path, px, w, h, false);
        return path;
    }

    static string DustTex(string path)
    {
        int n = 64;
        Color[] px = new Color[n * n];
        var rng = new System.Random(3);
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0.42f, 0.40f, 0.36f, 0.90f + 0.06f * (float)rng.NextDouble());
        WritePng(path, px, n, n, true);
        return path;
    }

    static void PeaceTexIfMissing()
    {
        if (System.IO.File.Exists(PhotoPath)) return;
        int n = 256;
        Color[] px = new Color[n * n];
        System.Func<float, float, float, float, float, float, float> seg = (x, y, ax, ay, bx, by) =>
        {
            float dx = bx - ax, dy = by - ay, t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            float px2 = ax + t * dx - x, py2 = ay + t * dy - y;
            return Mathf.Sqrt(px2 * px2 + py2 * py2);
        };
        Color skin = new Color(0.86f, 0.66f, 0.52f);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = x / (float)n, v = y / (float)n;
            Color c = Color.Lerp(new Color(0.80f, 0.70f, 0.55f), new Color(0.55f, 0.50f, 0.45f), v);
            bool hand = (u - 0.5f) * (u - 0.5f) + (v - 0.32f) * (v - 0.32f) < 0.045f
                || seg(u, v, 0.43f, 0.45f, 0.34f, 0.82f) < 0.045f || seg(u, v, 0.57f, 0.45f, 0.66f, 0.82f) < 0.045f
                || seg(u, v, 0.36f, 0.40f, 0.40f, 0.55f) < 0.04f;
            px[y * n + x] = hand ? skin : c;
        }
        WritePng(PhotoPath, px, n, n, false);
    }

    // ---------------------------------------------------------------- pieces --

    static GameObject PhotoFrame(string name, Vector3 basePos, Vector3 viewerDir)
    {
        GameObject g = RoomKit.Group(name, props);
        g.transform.SetPositionAndRotation(basePos + Vector3.up * 0.13f, Quaternion.Euler(0f, YawFacing(viewerDir), 0f));
        Material wood = Mat("R3_FrameWood", new Color(0.22f, 0.13f, 0.07f), 0.3f);
        Material pic = RoomKit.Tiled("R3_PeacePhoto", PhotoPath, Color.white, 1f, 1f, 1f, 0.3f);
        RoomKit.Box("Frame", g.transform, g.transform.position, new Vector3(0.2f, 0.26f, 0.02f), wood, false);
        RoomKit.Quad("Picture", g.transform, g.transform.TransformPoint(0f, 0f, -0.0105f), g.transform.rotation, new Vector2(0.17f, 0.22f), pic);
        RoomKit.FitCollider(g);
        Document(g, "Photo", "Look at the photo", "Room3/PeacePhoto", "");
        return g;
    }

    static Dictionary<string, Material> SebhaMats()
    {
        var d = new Dictionary<string, Material>();
        d["SB_Bead"] = Mat("R3_SbBead", new Color(0.14f, 0.08f, 0.05f), 0.55f);
        d["SB_Red"] = Mat("R3_SbRed", new Color(0.7f, 0.05f, 0.05f), 0.6f);
        d["SB_Imam"] = Mat("R3_SbImam", new Color(0.85f, 0.7f, 0.25f), 0.7f, 0.8f);
        d["SB_String"] = Mat("R3_SbString", new Color(0.8f, 0.75f, 0.6f), 0.2f);
        d["SB_Tassel"] = Mat("R3_SbTassel", new Color(0.1f, 0.35f, 0.2f), 0.3f);
        return d;
    }

    static GameObject Cylinder(string name, Vector3 pos, Vector3 scale, Material m)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = name; c.transform.SetParent(tidyGroup, false);
        c.transform.position = pos; c.transform.localScale = scale;
        c.GetComponent<Renderer>().sharedMaterial = m;
        return c;
    }

    // ----------------------------------------------------------------- rooms --

    static void GuestRooms()
    {
        markerSprite = RoomKit.MarkerSprite();
        tidyGroup = RoomKit.Group("Tidy", root).transform;
        PeaceTexIfMissing();
        string tex = RoomKit.GeneratedTexDir;
        Material paper = Mat("R3_Paper", new Color(0.9f, 0.88f, 0.8f), 0.1f);

        binTarget = RoomKit.Group("DisposeTarget_Bin", tidyGroup).transform; binTarget.position = new Vector3(9.55f, 0.75f, 4.45f);
        closetTarget = RoomKit.Group("DisposeTarget_Closet", tidyGroup).transform; closetTarget.position = new Vector3(8.8f, 1.1f, 0.95f);

        // ============================== ROOM A: the religious guest, mostly clean ==============================
        Solid(FM + "Beds/Bed03.prefab", "A_Bed", new Vector3(3.2f, 0f, 8.63f), 0f, 0.41f);
        Solid(FM + "Drawers/Drawer02.prefab", "A_Nightstand_L", new Vector3(1.85f, 0f, 9.62f), 180f, 0.40f);
        Solid(FM + "Drawers/Drawer02.prefab", "A_Nightstand_R", new Vector3(4.55f, 0f, 9.62f), 180f, 0.40f);
        RoomKit.Prefab(GG + "Decoration_Light_TableLamp_03_02.prefab", props, new Vector3(1.85f, 0.58f, 9.62f), 0f, 1f, "A_Lamp");
        Solid(FM + "Closets/Closet03.prefab", "A_Closet", new Vector3(3.6f, 0f, 5.49f), 0f, 0.40f);
        Solid(FM + "Tables/Table04.prefab", "A_Desk", new Vector3(0.48f, 0f, 6.6f), 90f, 0.47f);
        Solid(FM + "Chairs/Chair01.prefab", "A_Chair", new Vector3(1.15f, 0f, 6.6f), 270f, 0.47f);
        PhotoFrame("A_PhotoFrame", new Vector3(0.45f, 0.75f, 6.95f), Vector3.right);

        // prayer rug, crooked -> lined up towards the qibla (south-east)
        Material rugMat = RoomKit.Tiled("R3_PrayerRug", RugTex(tex + "PrayerRug.png"), Color.white, 1f, 1f, 1f, 0.1f);
        GameObject rug = RoomKit.Box("A_PrayerRug", tidyGroup, new Vector3(2.1f, 0.012f, 7.0f), new Vector3(0.8f, 0.024f, 1.3f), rugMat);
        rug.transform.rotation = Quaternion.Euler(0f, 175f, 0f);
        Tidy(rug, "A_Rug", "Straighten the prayer rug", 0, new Vector3(2.6f, 0.012f, 6.6f), new Vector3(0f, 135f, 0f));

        // sebha on the floor -> its hook on the wall
        GameObject sebha = Model("Sebha", "A_Sebha", tidyGroup, new Vector3(3.7f, 0.035f, 6.5f), 0f, 1f, SebhaMats());
        sebha.transform.rotation = Quaternion.Euler(-90f, 30f, 0f);
        RoomKit.Box("A_SebhaHook", props, new Vector3(0.12f, 1.52f, 7.5f), new Vector3(0.04f, 0.03f, 0.03f), Mat("R3_FrameWood", Color.black), false);
        Tidy(sebha, "A_Sebha", "Hang the sebha on its hook", 0, new Vector3(0.14f, 1.5f, 7.5f), new Vector3(0f, 90f, 0f));

        // books off the desk
        GameObject books = Loose(NP + "(Prb)BookPile1.prefab", "A_Books", new Vector3(1.3f, 0f, 5.8f), 30f, 1f);
        Tidy(books, "A_Books", "Put the books back on the desk", 0, new Vector3(0.45f, 0.75f, 6.15f), new Vector3(0f, 90f, 0f));

        // dusty mirror with a number written under the dust
        GameObject mir = RoomKit.Group("A_Mirror", tidyGroup);
        mir.transform.SetPositionAndRotation(new Vector3(4.88f, 1.5f, 8.2f), Quaternion.Euler(0f, 90f, 0f));
        Transform mt = mir.transform;
        RoomKit.Box("Frame", mt, mt.position, new Vector3(0.72f, 0.95f, 0.04f), Mat("R3_MirrorFrame", new Color(0.25f, 0.17f, 0.1f), 0.3f), false);
        RoomKit.Quad("Glass", mt, mt.TransformPoint(0f, 0f, -0.021f), mt.rotation, new Vector2(0.62f, 0.85f), Mat("R3_MirrorGlass", new Color(0.18f, 0.22f, 0.26f), 0.95f));
        Material numMat = RoomKit.Decal("R3_Mirror21", AssetDatabase.LoadAssetAtPath<Texture2D>(DigitTex(tex + "Mirror21.png", "21", new Color(0.75f, 0.08f, 0.14f), new Color(0, 0, 0, 0), 8)));
        RoomKit.Quad("Number", mt, mt.TransformPoint(0f, 0.02f, -0.026f), mt.rotation, new Vector2(0.4f, 0.2f), numMat);
        Material dustMat = RoomKit.Decal("R3_Dust", AssetDatabase.LoadAssetAtPath<Texture2D>(DustTex(tex + "Dust.png")));
        GameObject dust = RoomKit.Quad("Dust", mt, mt.TransformPoint(0f, 0f, -0.031f), mt.rotation, new Vector2(0.62f, 0.85f), dustMat);
        BoxCollider mc = mir.AddComponent<BoxCollider>(); mc.size = new Vector3(0.72f, 0.95f, 0.04f);
        Tidy(mir, "A_Mirror", "Wipe the mirror", 2, Vector3.zero, Vector3.zero, null, dust.GetComponent<Renderer>(),
             "Room 3: 21, under the mirror's dust");

        // ============================== ROOM C: the chaotic guest, nothing clean ==============================
        Solid(FM + "Beds/Bed03.prefab", "C_Bed", new Vector3(8.65f, 0f, 2.4f), 90f, 0.41f);
        Solid(FM + "Closets/Closet03.prefab", "C_Closet", new Vector3(8.8f, 0f, 0.49f), 0f, 0.40f);
        Solid(FM + "Tables/Table04.prefab", "C_Desk", new Vector3(6.6f, 0f, 0.48f), 0f, 0.47f);
        Solid(NP + "(Prb)TrashCan.prefab", "C_Bin", new Vector3(9.55f, 0f, 4.45f), 0f, 1f);
        PhotoFrame("C_PhotoFrame", new Vector3(6.2f, 0.75f, 0.35f), Vector3.forward);

        // knocked-over chair
        GameObject chair = Loose(FM + "Chairs/Chair01.prefab", "C_Chair", new Vector3(6.4f, 0.3f, 1.7f), 200f, 0.47f);
        chair.transform.rotation = Quaternion.Euler(82f, 200f, 0f);
        Tidy(chair, "C_Chair", "Pick up the chair", 0, new Vector3(6.6f, 0f, 1.1f), new Vector3(0f, 180f, 0f));

        // pillow on the floor
        GameObject pillow = Loose(FM + "Cushioins/Cushion01.prefab", "C_Pillow", new Vector3(7.0f, 0.1f, 3.7f), 20f, 0.75f);
        Tidy(pillow, "C_Pillow", "Put the pillow back", 0, new Vector3(9.5f, 0.53f, 2.4f), new Vector3(0f, 90f, 0f));

        // ticket stub under a pizza box on the desk: throw the box away to read it
        GameObject ticket = RoomKit.Group("C_Ticket", props);
        ticket.transform.position = new Vector3(6.85f, 0.752f, 0.45f);
        Material tkMat = RoomKit.Tiled("R3_Ticket", TicketTex(tex + "TicketStub.png"), Color.white, 1f, 1f, 1f, 0.3f);
        RoomKit.Quad("Stub", ticket.transform, ticket.transform.position, Quaternion.Euler(90f, 0f, 0f), new Vector2(0.15f, 0.075f), tkMat);
        BoxCollider tc = ticket.AddComponent<BoxCollider>(); tc.size = new Vector3(0.16f, 0.03f, 0.09f);
        // the readable copy the viewer shows
        System.IO.File.Copy(tex + "TicketStub.png", "Assets/Resources/Room3/TicketStub.png", true);
        AssetDatabase.ImportAsset("Assets/Resources/Room3/TicketStub.png");
        Document(ticket, "Ticket stub", "Read the ticket", "Room3/TicketStub", "Room 3: 08, on the ticket stub");

        GameObject pizza = RoomKit.Box("C_PizzaBox", tidyGroup, new Vector3(6.85f, 0.78f, 0.45f), new Vector3(0.42f, 0.05f, 0.42f), Mat("R3_Pizza", new Color(0.78f, 0.62f, 0.4f), 0.2f));
        RoomKit.Box("Logo", pizza.transform, pizza.transform.position + Vector3.up * 0.026f, new Vector3(0.3f, 0.005f, 0.3f), Mat("R3_PizzaRed", new Color(0.7f, 0.12f, 0.08f), 0.2f), false).transform.SetParent(pizza.transform, true);
        Tidy(pizza, "C_Pizza", "Throw away the pizza box", 1, Vector3.zero, Vector3.zero, binTarget);

        // cans, shirts, crumpled paper
        Material can = Mat("R3_Can", new Color(0.7f, 0.1f, 0.1f), 0.8f, 0.8f);
        Vector3[] cans = { new Vector3(6.9f, 0.06f, 2.4f), new Vector3(7.6f, 0.06f, 4.2f), new Vector3(5.6f, 0.06f, 3.6f) };
        for (int i = 0; i < 3; i++)
            Tidy(Cylinder("C_Can_" + i, cans[i], new Vector3(0.065f, 0.06f, 0.065f), can), "C_Can" + i, "Throw away the can", 1, Vector3.zero, Vector3.zero, binTarget);

        string shirtObj = "Assets/Models/Folded shirt/source/simple folded shirt.obj";
        Vector3[] shirts = { new Vector3(6.2f, 0.03f, 2.9f), new Vector3(8.0f, 0.03f, 3.9f) };
        for (int i = 0; i < 2; i++)
            Tidy(Loose(shirtObj, "C_Shirt_" + i, shirts[i], 20f + i * 80f, 0.15f), "C_Shirt" + i, "Put the shirt in the closet", 1, Vector3.zero, Vector3.zero, closetTarget);

        Vector3[] papers = { new Vector3(5.8f, 0.07f, 1.6f), new Vector3(7.0f, 0.07f, 3.3f), new Vector3(6.4f, 0.07f, 4.3f) };
        for (int i = 0; i < 3; i++)
        {
            GameObject p = new GameObject("C_Paper_" + i);
            p.transform.SetParent(tidyGroup, false);
            p.transform.position = papers[i]; p.transform.localScale = Vector3.one * (0.08f + 0.01f * i);
            p.AddComponent<MeshFilter>().sharedMesh = RoomKit.CrumpledPaperMesh(1 + i % 3);
            p.AddComponent<MeshRenderer>().sharedMaterial = paper;
            Tidy(p, "C_Paper" + i, "Throw away the crumpled paper", 1, Vector3.zero, Vector3.zero, binTarget);
        }

        // electric guitar PLACEHOLDER (swap for the real model), leaning on the west wall
        GameObject guitar = RoomKit.Group("C_Guitar_PLACEHOLDER", props);
        guitar.transform.position = new Vector3(5.28f, 0f, 4.3f);
        guitar.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
        Material gRed = Mat("R3_GuitarBody", new Color(0.55f, 0.06f, 0.08f), 0.8f), gDark = Mat("R3_GuitarNeck", new Color(0.12f, 0.08f, 0.05f), 0.4f);
        Vector3 gp = guitar.transform.position;
        RoomKit.Box("Body", guitar.transform, gp + guitar.transform.TransformVector(new Vector3(0f, 0.27f, 0f)), new Vector3(0.05f, 0.42f, 0.34f), gRed, false);
        RoomKit.Box("Neck", guitar.transform, gp + guitar.transform.TransformVector(new Vector3(0f, 0.78f, 0f)), new Vector3(0.03f, 0.62f, 0.06f), gDark, false);
        RoomKit.Box("Head", guitar.transform, gp + guitar.transform.TransformVector(new Vector3(0f, 1.15f, 0f)), new Vector3(0.03f, 0.14f, 0.09f), gDark, false);
        guitar.layer = 9; RoomKit.FitCollider(guitar);
    }

    // ============================== ROOM D: the twisted room ==============================

    static void Stick(GameObject g, Vector3 euler) { g.transform.rotation = Quaternion.Euler(euler); g.layer = 9; RoomKit.FitCollider(g); }

    static void Float(GameObject g, float spin = 20f)
    {
        Floater f = g.AddComponent<Floater>();
        SerializedObject so = new SerializedObject(f);
        so.FindProperty("spin").floatValue = spin;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildTwisted()
    {
        string tex = RoomKit.GeneratedTexDir;

        // furniture that ignores gravity: a bed and a chair on the ceiling, a desk (with its lamp) on the west wall
        Stick(RoomKit.Prefab(FM + "Beds/Bed03.prefab", props, new Vector3(2.7f, 2.9f, 2.3f), 0f, 0.41f, "D_Bed_Ceiling"), new Vector3(180f, 20f, 0f));
        Stick(RoomKit.Prefab(FM + "Chairs/Chair01.prefab", props, new Vector3(1.3f, 2.9f, 3.8f), 0f, 0.47f, "D_Chair_Ceiling"), new Vector3(180f, 60f, 0f));
        Stick(RoomKit.Prefab(FM + "Tables/Table04.prefab", props, new Vector3(0.1f, 1.4f, 1.6f), 0f, 0.47f, "D_Desk_Wall"), new Vector3(0f, 0f, -90f));
        GameObject wl = RoomKit.Prefab(GG + "Decoration_Light_TableLamp_01_02.prefab", props, new Vector3(0.85f, 1.4f, 1.6f), 0f, 1f, "D_Lamp_Wall");
        wl.transform.rotation = Quaternion.Euler(0f, 0f, -90f);

        // things hanging in the air
        Float(RoomKit.Prefab(FM + "Cushioins/Cushion01.prefab", props, new Vector3(3.5f, 1.6f, 1.4f), 0f, 0.75f, "D_Pillow_Float"), 15f);
        Float(RoomKit.Prefab(NP + "(Prb)Mug.prefab", props, new Vector3(1.9f, 1.9f, 3.2f), 0f, 1f, "D_Mug_Float"), 40f);
        Float(RoomKit.Prefab(NP + "(Prb)BookPile1.prefab", props, new Vector3(3.8f, 1.2f, 3.9f), 0f, 1f, "D_Books_Float"), 10f);
        Float(RoomKit.Prefab(GG + "Decoration_Light_TableLamp_01_02.prefab", props, new Vector3(3.9f, 1.0f, 2.2f), 0f, 1f, "D_Lamp_Float"), 25f);

        // the key tag with the last two digits
        string tagPath = DigitTex(tex + "KeyTag.png", "02", new Color(0.1f, 0.08f, 0.05f), new Color(0.82f, 0.66f, 0.22f), 10);
        System.IO.File.Copy(tagPath, "Assets/Resources/Room3/KeyTag.png", true);
        AssetDatabase.ImportAsset("Assets/Resources/Room3/KeyTag.png");
        Material tagMat = RoomKit.Tiled("R3_KeyTag", tagPath, Color.white, 1f, 1f, 1f, 0.5f);
        GameObject tag = RoomKit.Group("D_KeyTag", props);
        tag.transform.position = new Vector3(2.6f, 1.5f, 2.6f);
        Transform tt = tag.transform;
        RoomKit.Box("Body", tt, tt.position, new Vector3(0.2f, 0.12f, 0.012f), Mat("R3_Gold", new Color(0.82f, 0.66f, 0.22f), 0.7f, 0.8f), false);
        RoomKit.Box("Ring", tt, tt.position + new Vector3(-0.12f, 0f, 0f), new Vector3(0.05f, 0.05f, 0.012f), Mat("R3_Gold", Color.black), false);
        RoomKit.Quad("Front", tt, tt.position + new Vector3(0f, 0f, -0.007f), Quaternion.identity, new Vector2(0.19f, 0.11f), tagMat);
        RoomKit.Quad("Back", tt, tt.position + new Vector3(0f, 0f, 0.007f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.19f, 0.11f), tagMat);
        RoomKit.FitCollider(tag);
        Document(tag, "Key tag", "Look at the key tag", "Room3/KeyTag", "Room 3: 02, on the floating key tag");
        Float(tag, 30f);

        // camera roll + lamp colour while inside
        GameObject tw = new GameObject("Room3_Twisted");
        TwistedRoom tr = tw.AddComponent<TwistedRoom>();
        SerializedObject so = new SerializedObject(tr);
        so.FindProperty("center").vector3Value = new Vector3(2.5f, 1.5f, 2.5f);
        so.FindProperty("size").vector3Value = new Vector3(4.8f, 3f, 4.8f);
        so.FindProperty("lamp").objectReferenceValue = lighting.Find("Lamp_D/Light").GetComponent<Light>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
