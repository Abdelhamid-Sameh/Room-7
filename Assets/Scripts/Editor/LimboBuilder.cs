using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the two scenes of the strange place Nabil is dragged to after Room 3:
///   Limbo_Corridor - a narrow, dim tiled corridor with one door at the end (whispers, flickering bulbs);
///   the Game Boy room  - (built into the same scene, past the door) a child's bedroom at night, a huge red Game Boy playing a snake maze, a door in its side.
/// Re-running rebuilds everything except the player / spawn / game-systems objects.
/// </summary>
public static class LimboBuilder
{
    const string CorridorPath = "Assets/Scenes/Limbo_Corridor.unity";
    const string RoomPath = "Assets/Scenes/Limbo_GameBoy.unity";
    const string DoorOpenClip = "Assets/Free Wood Door Pack/Audio/Door_Open.wav";
    const string WhisperClip = "Assets/Audio/Sounds/whispering.mp3";
    const string TilePath = "Assets/Textures/Generated/BathroomTile.png";

    [MenuItem("Room Seven/Build Limbo scenes")]
    public static void BuildAll()
    {
        Debug.Log(BuildCorridor());
    }

    // ------------------------------------------------------------------ shared --

    static Material M(string n, Color c, float smooth = 0.3f, Color? emission = null)
    {
        return RoomKit.Plain("Limbo_" + n, c, smooth, 0f, emission);
    }

    static Scene Prepare(string path)
    {
        if (!System.IO.File.Exists(path)) AssetDatabase.CopyAsset("Assets/Scenes/StaffRoom.unity", path);
        Scene sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        foreach (GameObject g in sc.GetRootGameObjects())
            if (g.name != "GameSystems" && g.name != "SpawnPoints" && g.name != "Player") Object.DestroyImmediate(g);
        SpawnPoint[] sps = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        for (int i = 1; i < sps.Length; i++) Object.DestroyImmediate(sps[i].gameObject);
        return sc;
    }

    static GameObject Box(string name, Transform parent, Vector3 c, Vector3 size, Material mat, bool collider = true)
    {
        return RoomKit.Box(name, parent, c, size, mat, collider);
    }

    static GameObject Rotated(GameObject g, Vector3 euler)
    {
        g.transform.rotation = Quaternion.Euler(euler);
        return g;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat, bool collider = false)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    static void Set(Object o, System.Action<SerializedObject> fill)
    {
        SerializedObject so = new SerializedObject(o);
        fill(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Finish(Scene sc, string path, Vector3 spawnPos, Quaternion spawnRot)
    {
        SpawnPoint sp = Object.FindFirstObjectByType<SpawnPoint>();
        sp.SetSpawnId("Limbo_Start");
        sp.transform.SetPositionAndRotation(spawnPos, spawnRot);
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) player.transform.SetPositionAndRotation(spawnPos, spawnRot);

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------------------------------------------------------- corridor --

    public static string BuildCorridor()
    {
        Scene sc = Prepare(CorridorPath);
        RoomKit.TileTexture();
        AssetDatabase.ImportAsset(TilePath);

        Transform root = new GameObject("Limbo_Corridor_Root").transform;
        Transform shell = RoomKit.Group("Shell", root).transform;
        Transform lighting = RoomKit.Group("Lighting", root).transform;
        Transform props = RoomKit.Group("Props", root).transform;

        Material floor = M("CorridorFloor", new Color(0.13f, 0.14f, 0.13f), 0.65f);
        Material ceil = M("CorridorCeiling", new Color(0.09f, 0.10f, 0.09f), 0.1f);
        Material plaster = M("CorridorPlaster", new Color(0.34f, 0.36f, 0.31f), 0.1f);
        Material trim = M("CorridorTrim", new Color(0.06f, 0.07f, 0.06f), 0.3f);
        Material tile = RoomKit.Tiled("Limbo_Tile", TilePath, new Color(0.42f, 0.55f, 0.47f), 13.2f, 1.4f, 0.6f, 0.55f);
        Material doorMat = M("CorridorDoor", new Color(0.22f, 0.27f, 0.22f), 0.45f);
        Material metal = M("Metal", new Color(0.55f, 0.55f, 0.5f), 0.7f);
        Material black = M("Black", Color.black, 0f);
        Material bulbMat = M("Bulb", new Color(0.8f, 0.9f, 0.7f), 0.1f, new Color(1.2f, 1.4f, 1.0f));

        // a long thin box: 1.6 m wide inside, 13 m long, 2.5 m high
        Box("Floor", shell, new Vector3(0f, -0.05f, 6.5f), new Vector3(1.8f, 0.1f, 13.2f), floor);
        Box("Ceiling", shell, new Vector3(0f, 2.55f, 6.5f), new Vector3(1.8f, 0.1f, 13.2f), ceil);
        foreach (float s in new[] { -1f, 1f })
        {
            string side = s < 0 ? "West" : "East";
            Box("Tile_" + side, shell, new Vector3(0.85f * s, 0.7f, 6.5f), new Vector3(0.1f, 1.4f, 13.2f), tile);
            Box("Plaster_" + side, shell, new Vector3(0.85f * s, 1.95f, 6.5f), new Vector3(0.1f, 1.1f, 13.2f), plaster);
            Box("Trim_" + side, shell, new Vector3(0.795f * s, 1.4f, 6.5f), new Vector3(0.02f, 0.08f, 13.1f), trim, false);
        }
        Box("Wall_Behind", shell, new Vector3(0f, 1.25f, -0.05f), new Vector3(1.8f, 2.5f, 0.1f), plaster);

        // the end wall with its doorway (1 m wide, 2.1 m high)
        Box("End_Left", shell, new Vector3(-0.7f, 1.25f, 13.05f), new Vector3(0.4f, 2.5f, 0.1f), plaster);
        Box("End_Right", shell, new Vector3(0.7f, 1.25f, 13.05f), new Vector3(0.4f, 2.5f, 0.1f), plaster);
        Box("End_Lintel", shell, new Vector3(0f, 2.3f, 13.05f), new Vector3(1f, 0.4f, 0.1f), plaster);
        Box("Frame_L", shell, new Vector3(-0.53f, 1.05f, 13.0f), new Vector3(0.06f, 2.1f, 0.14f), trim, false);
        Box("Frame_R", shell, new Vector3(0.53f, 1.05f, 13.0f), new Vector3(0.06f, 2.1f, 0.14f), trim, false);
        Box("Frame_Top", shell, new Vector3(0f, 2.13f, 13.0f), new Vector3(1.12f, 0.06f, 0.14f), trim, false);
        
        // the door: hinge on the right-hand edge, leaf swings away from the player
        GameObject hinge = new GameObject("Door_Hinge");
        hinge.transform.SetParent(props, false);
        hinge.transform.position = new Vector3(0.5f, 0f, 13.05f);
        GameObject leaf = Box("Door_Leaf", hinge.transform, new Vector3(0f, 0f, 0f), new Vector3(0.98f, 2.08f, 0.06f), doorMat);
        leaf.transform.localPosition = new Vector3(-0.49f, 1.04f, 0f);
        GameObject handle = Box("Handle", leaf.transform, Vector3.zero, new Vector3(0.12f, 0.04f, 0.08f), metal, false);
        handle.transform.localPosition = new Vector3(-0.41f, -0.04f, -0.9f);
        handle.transform.localScale = new Vector3(0.12f, 0.04f, 1.3f);
        LimboDoor door = hinge.AddComponent<LimboDoor>();
        // no target scene: the door opens onto the Game Boy room, which is built into this same scene
        Set(door, so => so.FindProperty("openClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorOpenClip));

        // two pipes along the ceiling
        Prim(PrimitiveType.Cylinder, "Pipe_A", props, new Vector3(-0.6f, 2.38f, 6.5f), new Vector3(0.07f, 6.55f, 0.07f), new Vector3(90f, 0f, 0f), trim);
        Prim(PrimitiveType.Cylinder, "Pipe_B", props, new Vector3(0.62f, 2.44f, 6.5f), new Vector3(0.05f, 6.55f, 0.05f), new Vector3(90f, 0f, 0f), metal);

        // three tired bulbs, the last one nearly dead; a faint cold glow marks the door
        float[] zs = { 2.4f, 6.8f, 10.8f };
        float[] intensity = { 1.4f, 1.0f, 1.2f };
        float[] drops = { 4f, 0f, 1.6f };
        for (int i = 0; i < zs.Length; i++)
        {
            Box("Fixture_" + i, lighting, new Vector3(0f, 2.47f, zs[i]), new Vector3(0.5f, 0.05f, 0.14f), bulbMat, false);
            Light l = PointLight("Bulb_" + i, lighting, new Vector3(0f, 2.3f, zs[i]), new Color(0.85f, 0.95f, 0.7f), intensity[i], 4.5f);
            FlickerLight fl = l.gameObject.AddComponent<FlickerLight>();
            int k = i;
            Set(fl, so =>
            {
                so.FindProperty("baseIntensity").floatValue = intensity[k];
                so.FindProperty("wobble").floatValue = k == 2 ? 0.45f : 0.15f;
                so.FindProperty("dropEvery").floatValue = drops[k];
                so.FindProperty("bulb").objectReferenceValue = lighting.Find("Fixture_" + k).GetComponent<Renderer>();
            });
        }
        PointLight("DoorGlow", lighting, new Vector3(0f, 1.4f, 12.4f), new Color(0.4f, 0.5f, 0.9f), 0.5f, 3f);
        Transform roomLighting = BuildGameBoyContent();

        // the whispers: start when the lights do, stop when this scene goes
        GameObject wh = new GameObject("Whispers");
        wh.transform.SetParent(root, false);
        AudioSource src = wh.AddComponent<AudioSource>();
        src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WhisperClip);
        src.loop = true; src.playOnAwake = false; src.spatialBlend = 0f;
        LimboCorridor lc = wh.AddComponent<LimboCorridor>();
        Set(lc, so =>
        {
            so.FindProperty("whisper").objectReferenceValue = src;
            so.FindProperty("door").objectReferenceValue = door;
            so.FindProperty("roomLights").objectReferenceValue = roomLighting.gameObject;
        });

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.03f, 0.035f, 0.03f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 0.05f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.02f);
        RenderSettings.fogDensity = 0.09f;

        Finish(sc, CorridorPath, new Vector3(0f, 0f, 0.9f), Quaternion.identity);
        return "built Limbo_Corridor";
    }

    // ----------------------------------------------------------- game boy room --

    static Transform BuildGameBoyContent()
    {
        Transform root = new GameObject("Limbo_GameBoy_Root").transform;
        Transform shell = RoomKit.Group("Shell", root).transform;
        Transform lighting = RoomKit.Group("Lighting", root).transform;
        Transform props = RoomKit.Group("Props", root).transform;

        Material carpet = M("RoomCarpet", new Color(0.17f, 0.21f, 0.34f), 0.1f);
        Material wallMat = M("RoomWall", new Color(0.09f, 0.13f, 0.27f), 0.1f);
        Material wains = M("RoomWainscot", new Color(0.20f, 0.26f, 0.42f), 0.15f);
        Material stripe = M("RoomStripe", new Color(0.85f, 0.78f, 0.55f), 0.2f);
        Material ceilMat = M("RoomCeiling", new Color(0.05f, 0.06f, 0.12f), 0.05f);
        Material star = M("Star", new Color(1f, 0.95f, 0.6f), 0f, new Color(2f, 1.9f, 1.2f));
        Material doorMat = M("CorridorDoor", new Color(0.22f, 0.27f, 0.22f), 0.45f);
        Material trim = M("CorridorTrim", new Color(0.06f, 0.07f, 0.06f), 0.3f);

        // ---- the room: 9.6 wide, 9.6 deep, 4.2 high ----
        Box("Floor", shell, new Vector3(0f, -0.05f, 4.8f), new Vector3(9.8f, 0.1f, 9.8f), carpet);
        Box("Ceiling", shell, new Vector3(0f, 4.25f, 4.8f), new Vector3(9.8f, 0.1f, 9.8f), ceilMat);
        // the south wall is the corridor's end wall: a doorway 1.8 wide (the corridor's own door sits in it), open above 2.5 m
        Box("Wall_South_L", shell, new Vector3(-2.9f, 2.1f, -0.05f), new Vector3(4f, 4.2f, 0.1f), wallMat);
        Box("Wall_South_R", shell, new Vector3(2.9f, 2.1f, -0.05f), new Vector3(4f, 4.2f, 0.1f), wallMat);
        Box("Wall_South_Top", shell, new Vector3(0f, 3.4f, -0.05f), new Vector3(1.8f, 1.6f, 0.1f), wallMat);
        Box("Wall_North", shell, new Vector3(0f, 2.1f, 9.65f), new Vector3(9.8f, 4.2f, 0.1f), wallMat);
        Box("Wall_West", shell, new Vector3(-4.85f, 2.1f, 4.8f), new Vector3(0.1f, 4.2f, 9.8f), wallMat);
        Box("Wall_East", shell, new Vector3(4.85f, 2.1f, 4.8f), new Vector3(0.1f, 4.2f, 9.8f), wallMat);
        // wainscot and a pale stripe above it, all the way round
        Box("Wains_S_L", shell, new Vector3(-2.65f, 0.55f, 0.025f), new Vector3(4.3f, 1.1f, 0.05f), wains, false);
        Box("Wains_S_R", shell, new Vector3(2.65f, 0.55f, 0.025f), new Vector3(4.3f, 1.1f, 0.05f), wains, false);
        Box("Wains_N", shell, new Vector3(0f, 0.55f, 9.575f), new Vector3(9.6f, 1.1f, 0.05f), wains, false);
        Box("Wains_W", shell, new Vector3(-4.775f, 0.55f, 4.8f), new Vector3(0.05f, 1.1f, 9.6f), wains, false);
        Box("Wains_E", shell, new Vector3(4.775f, 0.55f, 4.8f), new Vector3(0.05f, 1.1f, 9.6f), wains, false);
        Box("Stripe_S_L", shell, new Vector3(-2.65f, 1.14f, 0.03f), new Vector3(4.3f, 0.05f, 0.06f), stripe, false);
        Box("Stripe_S_R", shell, new Vector3(2.65f, 1.14f, 0.03f), new Vector3(4.3f, 0.05f, 0.06f), stripe, false);
        Box("Stripe_N", shell, new Vector3(0f, 1.14f, 9.57f), new Vector3(9.6f, 0.05f, 0.06f), stripe, false);
        Box("Stripe_W", shell, new Vector3(-4.77f, 1.14f, 4.8f), new Vector3(0.06f, 0.05f, 9.6f), stripe, false);
        Box("Stripe_E", shell, new Vector3(4.77f, 1.14f, 4.8f), new Vector3(0.06f, 0.05f, 9.6f), stripe, false);

        // glow-in-the-dark stars on the ceiling
        Transform stars = RoomKit.Group("Stars", root).transform;
        System.Random rng = new System.Random(3);
        for (int i = 0; i < 48; i++)
        {
            float sz = 0.05f + (float)rng.NextDouble() * 0.08f;
            Vector3 p = new Vector3(-4.5f + (float)rng.NextDouble() * 9f, 4.195f, 0.3f + (float)rng.NextDouble() * 9f);
            GameObject q = RoomKit.Quad("Star_" + i, stars, p, Quaternion.Euler(-90f, (float)rng.NextDouble() * 360f, 0f), new Vector2(sz, sz), star);
            Object.DestroyImmediate(q.GetComponent<Collider>());
        }


        // rug, toy blocks, a ball, a lamp in the corner
        Material rugA = M("RugRed", new Color(0.45f, 0.08f, 0.14f), 0.05f);
        Material rugB = M("RugGold", new Color(0.70f, 0.60f, 0.28f), 0.05f);
        Prim(PrimitiveType.Cylinder, "Rug_A", props, new Vector3(0f, 0.012f, 3.6f), new Vector3(4.6f, 0.012f, 4.6f), Vector3.zero, rugA);
        Prim(PrimitiveType.Cylinder, "Rug_B", props, new Vector3(0f, 0.016f, 3.6f), new Vector3(3.6f, 0.012f, 3.6f), Vector3.zero, rugB);
        Prim(PrimitiveType.Cylinder, "Rug_C", props, new Vector3(0f, 0.02f, 3.6f), new Vector3(2.7f, 0.012f, 2.7f), Vector3.zero, rugA);
        Rotated(Box("Block_Red", props, new Vector3(-2.8f, 0.2f, 6.2f), new Vector3(0.4f, 0.4f, 0.4f), M("BlockRed", new Color(0.7f, 0.1f, 0.1f), 0.3f)), new Vector3(0f, 12f, 0f));
        Rotated(Box("Block_Blue", props, new Vector3(-2.35f, 0.2f, 6.55f), new Vector3(0.4f, 0.4f, 0.4f), M("BlockBlue", new Color(0.1f, 0.25f, 0.7f), 0.3f)), new Vector3(0f, 28f, 0f));
        Rotated(Box("Block_Yellow", props, new Vector3(-2.75f, 0.6f, 6.25f), new Vector3(0.4f, 0.4f, 0.4f), M("BlockYellow", new Color(0.8f, 0.65f, 0.1f), 0.3f)), new Vector3(0f, -16f, 0f));
        Prim(PrimitiveType.Sphere, "Ball", props, new Vector3(3.0f, 0.3f, 5.6f), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero, M("Ball", new Color(0.2f, 0.6f, 0.35f), 0.5f), true);
        Prim(PrimitiveType.Cylinder, "Lamp_Stand", props, new Vector3(-4.3f, 0.6f, 1.0f), new Vector3(0.06f, 0.6f, 0.06f), Vector3.zero, trim);
        Prim(PrimitiveType.Sphere, "Lamp_Shade", props, new Vector3(-4.3f, 1.3f, 1.0f), new Vector3(0.35f, 0.3f, 0.35f), Vector3.zero, M("LampShade", new Color(0.9f, 0.7f, 0.4f), 0.1f, new Color(1.4f, 0.9f, 0.4f)));
        PointLight("Lamp_Light", lighting, new Vector3(-4.3f, 1.3f, 1.0f), new Color(1f, 0.78f, 0.45f), 1.4f, 6.5f);
        PointLight("Moon", lighting, new Vector3(0f, 3.9f, 3.0f), new Color(0.5f, 0.6f, 1f), 0.8f, 8f);

        // ---- the Game Boy: 2.4 wide, 3.4 high, 1.6 deep, hollow, against the north wall ----
        Transform gb = RoomKit.Group("GameBoy", root).transform;
        Material red = M("GBRed", new Color(0.74f, 0.05f, 0.07f), 0.6f);
        Material darkRed = M("GBDarkRed", new Color(0.45f, 0.03f, 0.05f), 0.5f);
        Material bezel = M("GBBezel", new Color(0.30f, 0.31f, 0.36f), 0.3f);
        Material dark = M("GBDark", new Color(0.09f, 0.09f, 0.11f), 0.3f);
        Material button = M("GBButton", new Color(0.50f, 0.05f, 0.22f), 0.5f);
        Material led = M("GBLed", new Color(0.9f, 0.1f, 0.1f), 0.1f, new Color(3f, 0.2f, 0.2f));
        Material pcb = M("GBPcb", new Color(0.05f, 0.25f, 0.12f), 0.5f);
        Material chip = M("GBChip", new Color(0.04f, 0.04f, 0.05f), 0.4f);

        Box("Body_Back", gb, new Vector3(0f, 1.7f, 9.25f), new Vector3(2.4f, 3.4f, 0.1f), red);
        Box("Body_Left", gb, new Vector3(-1.15f, 1.7f, 8.5f), new Vector3(0.1f, 3.4f, 1.6f), red);
        Box("Body_Top", gb, new Vector3(0f, 3.35f, 8.5f), new Vector3(2.4f, 0.1f, 1.6f), red);
        Box("Body_Front", gb, new Vector3(0f, 1.7f, 7.75f), new Vector3(2.4f, 3.4f, 0.1f), red);
        // right side, with a doorway 0.9 wide and 2.05 high
        Box("Body_Right_Front", gb, new Vector3(1.15f, 1.7f, 7.875f), new Vector3(0.1f, 3.4f, 0.35f), red);
        Box("Body_Right_Back", gb, new Vector3(1.15f, 1.7f, 9.125f), new Vector3(0.1f, 3.4f, 0.35f), red);
        Box("Body_Right_Lintel", gb, new Vector3(1.15f, 2.725f, 8.5f), new Vector3(0.1f, 1.35f, 0.9f), red);

        // the door: hinge on the front edge, swings outwards when the maze is won
        GameObject gbHinge = new GameObject("GB_Door_Hinge");
        gbHinge.transform.SetParent(gb, false);
        gbHinge.transform.position = new Vector3(1.17f, 0f, 8.05f);
        GameObject gbLeaf = Box("GB_Door_Leaf", gbHinge.transform, Vector3.zero, new Vector3(0.04f, 2.0f, 0.88f), red);
        gbLeaf.transform.localPosition = new Vector3(0f, 1.0f, 0.44f);
        GameObject inset = Box("GB_Door_Inset", gbLeaf.transform, Vector3.zero, new Vector3(1f, 1f, 1f), darkRed, false);
        inset.transform.localPosition = new Vector3(0.7f, 0f, 0f);
        inset.transform.localScale = new Vector3(0.3f, 0.8f, 0.72f);
        GameObject gbHandle = Box("GB_Door_Handle", gbLeaf.transform, Vector3.zero, Vector3.one, dark, false);
        gbHandle.transform.localPosition = new Vector3(1.1f, 0f, 0.36f);
        gbHandle.transform.localScale = new Vector3(1.6f, 0.15f, 0.06f);
        GameBoyDoor gbDoor = gbHinge.AddComponent<GameBoyDoor>();
        Set(gbDoor, so => so.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorOpenClip));   // same door sound as the hotel's

        // inside: a circuit board floor, a few chips, a green glow
        GameObject board = RoomKit.Quad("PCB", gb, new Vector3(0f, 0.012f, 8.5f), Quaternion.Euler(90f, 0f, 0f), new Vector2(2.2f, 1.4f), pcb);
        Object.DestroyImmediate(board.GetComponent<Collider>());
        Box("Chip_A", gb, new Vector3(-0.5f, 0.03f, 8.7f), new Vector3(0.4f, 0.05f, 0.4f), chip, false);
        Box("Chip_B", gb, new Vector3(0.3f, 0.03f, 8.3f), new Vector3(0.5f, 0.05f, 0.25f), chip, false);
        Box("Chip_C", gb, new Vector3(-0.2f, 0.03f, 8.1f), new Vector3(0.2f, 0.05f, 0.2f), chip, false);
        PointLight("Inside_Glow", lighting, new Vector3(0f, 2.6f, 8.5f), new Color(0.3f, 1f, 0.5f), 1.2f, 3f);

        // ---- the face ----
        Box("Bezel", gb, new Vector3(0f, 2.5f, 7.68f), new Vector3(1.95f, 1.6f, 0.04f), bezel, false);
        Box("Stripe_Blue", gb, new Vector3(0f, 1.83f, 7.665f), new Vector3(1.5f, 0.025f, 0.01f), M("GBBlue", new Color(0.2f, 0.2f, 0.6f), 0.3f), false);
        Box("Stripe_Magenta", gb, new Vector3(0f, 1.78f, 7.665f), new Vector3(1.5f, 0.025f, 0.01f), M("GBMagenta", new Color(0.6f, 0.1f, 0.35f), 0.3f), false);
        Box("Led", gb, new Vector3(-0.84f, 2.75f, 7.655f), new Vector3(0.06f, 0.06f, 0.02f), led, false);

        Material screenMat = AssetDatabase.LoadAssetAtPath<Material>(RoomKit.MaterialDir + "Limbo_GBScreen.mat");
        if (screenMat == null)
        {
            screenMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(screenMat, RoomKit.MaterialDir + "Limbo_GBScreen.mat");
        }
        screenMat.SetColor("_BaseColor", new Color(0.9f, 0.95f, 0.85f));
        EditorUtility.SetDirty(screenMat);
        GameObject screen = RoomKit.Quad("Screen", gb, new Vector3(0f, 2.56f, 7.655f), Quaternion.identity, new Vector2(1.4f, 1.26f), screenMat);
        Object.DestroyImmediate(screen.GetComponent<Collider>());
        NoShadow(screen);

        Box("Dpad_H", gb, new Vector3(-0.62f, 1.0f, 7.675f), new Vector3(0.56f, 0.18f, 0.05f), dark, false);
        Box("Dpad_V", gb, new Vector3(-0.62f, 1.0f, 7.675f), new Vector3(0.18f, 0.56f, 0.05f), dark, false);
        Prim(PrimitiveType.Cylinder, "Button_A", gb, new Vector3(0.78f, 1.15f, 7.675f), new Vector3(0.34f, 0.025f, 0.34f), new Vector3(90f, 0f, 0f), button);
        Prim(PrimitiveType.Cylinder, "Button_B", gb, new Vector3(0.40f, 0.93f, 7.675f), new Vector3(0.34f, 0.025f, 0.34f), new Vector3(90f, 0f, 0f), button);
        Rotated(Box("Select", gb, new Vector3(-0.12f, 0.42f, 7.68f), new Vector3(0.3f, 0.08f, 0.04f), bezel, false), new Vector3(0f, 0f, 25f));
        Rotated(Box("Start", gb, new Vector3(0.28f, 0.42f, 7.68f), new Vector3(0.3f, 0.08f, 0.04f), bezel, false), new Vector3(0f, 0f, 25f));
        for (int i = 0; i < 5; i++)
            Rotated(Box("Speaker_" + i, gb, new Vector3(0.55f + i * 0.13f, 0.28f, 7.69f), new Vector3(0.06f, 0.45f, 0.02f), darkRed, false), new Vector3(0f, 0f, 30f));

        // the part you talk to: a trigger in front of the screen
        GameObject console = new GameObject("GameBoy_Console");
        console.transform.SetParent(gb, false);
        console.transform.position = new Vector3(0f, 2.5f, 7.4f);
        BoxCollider trig = console.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.size = new Vector3(1.95f, 1.6f, 0.6f);
        GameBoyConsole gbc = console.AddComponent<GameBoyConsole>();

        GameObject camGo = new GameObject("GameBoyCloseUp");
        camGo.transform.SetParent(gb, false);
        camGo.transform.position = new Vector3(0f, 2.3f, 5.0f);
        camGo.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 2.56f, 7.655f) - camGo.transform.position);
        CinemachineCamera vcam = camGo.AddComponent<CinemachineCamera>();
        vcam.Lens = new LensSettings { FieldOfView = 38f, NearClipPlane = 0.05f, FarClipPlane = 100f, ModeOverride = LensSettings.OverrideModes.None };
        vcam.Priority.Enabled = true; vcam.Priority.Value = 0;

        Set(gbc, so =>
        {
            so.FindProperty("screen").objectReferenceValue = screen.GetComponent<Renderer>();
            so.FindProperty("closeUp").objectReferenceValue = vcam;
            so.FindProperty("door").objectReferenceValue = gbDoor;
            so.FindProperty("eatSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/gameboy/pluck.mp3");
            so.FindProperty("crashSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/gameboy/dead.mp3");
            so.FindProperty("winSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/gameboy/win.mp3");
            so.FindProperty("musicClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/gameboy/music.mp3");
        });
        PointLight("Screen_Glow", lighting, new Vector3(0f, 2.5f, 7.0f), new Color(0.6f, 1f, 0.5f), 3f, 8f);

        root.position = new Vector3(0f, 0f, 13.1f);   // against the corridor's end wall
        lighting.gameObject.SetActive(false);          // LimboCorridor switches it on when the door opens
        return lighting;
    }

    static void NoShadow(GameObject g)
    {
        Renderer r = g.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }
}
