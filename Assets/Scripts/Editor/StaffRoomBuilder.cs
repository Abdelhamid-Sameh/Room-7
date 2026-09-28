using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click builder for the staff room scene.
///
///   Room 7 > Build Staff Room
///
/// Everything under "StaffRoom_Root" is wiped and rebuilt, so the layout can
/// be changed here and regenerated at any time without leaving stray objects.
/// Run it while Assets/Scenes/StaffRoom.unity is the open scene.
/// </summary>
public static class StaffRoomBuilder
{
    private const string RootName = "StaffRoom_Root";
    private const string StaffRoomScene = "Assets/Scenes/StaffRoom.unity";

    // ---------------------------------------------------------------- palette
    private const string WallMat = "Assets/Materials/RoomSeven/RS_Wall_Burlap_4x3.mat";
    private const string CeilingMat = "Assets/Materials/RoomSeven/RS_Ceiling_Burlap_12x4p4.mat";
    private const string FloorMat = "Assets/Materials/RoomSeven/RS_Floor_Carpet.mat";
    private const string LampMat = "Assets/Materials/RoomSeven/RS_LightFixture_Emissive.mat";

    private const string GreyMat = "Assets/nappin/OfficeEssentialsPack/Materials/(Mat)GradientGrey.mat";
    private const string LockerBodyMat = "Assets/nappin/OfficeEssentialsPack/Materials/(Mat)GradientGrey.mat";
    private const string LockerDoorMat = "Assets/nappin/OfficeEssentialsPack/Materials/(Mat)GradientDarkGreen.mat";
    private const string MetalMat = "Assets/Free Wood Door Pack/Materials/Door_Knob_Material/Silver.mat";
    private const string BlackMat = "Assets/nappin/OfficeEssentialsPack/Materials/(Mat)MetallicBlack.mat";
    private const string BrownMat = "Assets/nappin/OfficeEssentialsPack/Materials/(Mat)GradientBrown.mat";
    private const string BeigeMat = "Assets/nappin/OfficeEssentialsPack/Materials/(Mat)GradientBeige.mat";
    private const string RedMat = "Assets/nappin/OfficeEssentialsPack/Materials/(Mat)GradientRed.mat";
    private const string GreenMat = "Assets/nappin/OfficeEssentialsPack/Materials/(Mat)GradientDarkGreen.mat";

    private const string OfficePack = "Assets/nappin/OfficeEssentialsPack/Prefabs/";
    private const string ExitDoorPrefab = "Assets/Free Wood Door Pack/Prefab/Wood/Door_4/Door_4_Brown.prefab";

    /// <summary>
    /// The Office Essentials pack still ships Built-in "Standard" shader
    /// materials, which render magenta under URP. Any prefab material caught
    /// using the Standard (or error) shader is swapped for a URP-safe one.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> MaterialFixes =
        new System.Collections.Generic.Dictionary<string, string>
    {
        { "(Mat)GradientBlue", BeigeMat },
        { "(Mat)GradientBlack", BlackMat },
        { "(Mat)GradientDarkRed", RedMat },
        { "(Mat)GradientDarkOrange", GreyMat },
        { "(Mat)GradientOrange", GreyMat },
        { "(Mat)GradientDarkBrown", BrownMat },
        { "(Mat)GradientDarkGrey", GreyMat },
        { "(Mat)GradientDarkGreen", GreenMat },
    };

    // ------------------------------------------------------------------ entry
    [MenuItem("Room 7/Build Staff Room")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != StaffRoomScene)
        {
            Debug.LogError($"[StaffRoomBuilder] Open {StaffRoomScene} first (active: '{scene.path}').");
            return;
        }

        Transform root = Ensure(RootName, null);
        ClearChildren(root);

        BuildShell(root);
        BuildLighting(root);
        BuildExitDoor(root);
        BuildLockers(root);
        BuildCleaningKit(root);
        BuildFurniture(root);
        BuildWallDetails(root);
        ConfigurePlayerAndSpawns();
        FixUnsafeMaterials(root.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[StaffRoomBuilder] Staff room rebuilt. Save the scene when happy.");
    }

    // ------------------------------------------------------------------ shell
    private static void BuildShell(Transform root)
    {
        Transform shell = Ensure("Shell", root);

        SetMaterial(Box("Floor", shell, new Vector3(0f, -0.05f, 0f), new Vector3(6f, 0.1f, 5f)), FloorMat);
        SetMaterial(Box("Ceiling", shell, new Vector3(0f, 3.05f, 0f), new Vector3(6f, 0.1f, 5f)), CeilingMat);
        SetMaterial(Box("Wall_North", shell, new Vector3(0f, 1.5f, 2.55f), new Vector3(6.2f, 3f, 0.1f)), WallMat);
        SetMaterial(Box("Wall_South", shell, new Vector3(0f, 1.5f, -2.55f), new Vector3(6.2f, 3f, 0.1f)), WallMat);
        SetMaterial(Box("Wall_West", shell, new Vector3(-3.05f, 1.5f, 0f), new Vector3(0.1f, 3f, 5f)), WallMat);
        SetMaterial(Box("Wall_East", shell, new Vector3(3.05f, 1.5f, 0f), new Vector3(0.1f, 3f, 5f)), WallMat);
    }

    // --------------------------------------------------------------- lighting
    private static void BuildLighting(Transform root)
    {
        Transform lighting = Ensure("Lighting", root);

        BuildLamp(lighting, "A", new Vector3(0f, 0f, 1.1f));
        BuildLamp(lighting, "B", new Vector3(0f, 0f, -1.2f));
    }

    private static void BuildLamp(Transform parent, string suffix, Vector3 position)
    {
        SetMaterial(Box("Fixture_" + suffix, parent, position + new Vector3(0f, 2.97f, 0f),
                        new Vector3(0.8f, 0.08f, 0.35f)), LampMat);

        GameObject spot = Empty("Spot_" + suffix, parent, position + new Vector3(0f, 2.9f, 0f), new Vector3(90f, 0f, 0f));
        Light spotLight = spot.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.color = new Color(1f, 0.83f, 0.6f);
        spotLight.intensity = 10f;
        spotLight.range = 12f;
        spotLight.spotAngle = 130f;
        spotLight.shadows = LightShadows.Soft;

        GameObject point = Empty("Point_" + suffix, parent, position + new Vector3(0f, 2.5f, 0f), Vector3.zero);
        Light pointLight = point.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = new Color(1f, 0.86f, 0.66f);
        pointLight.intensity = 2.2f;
        pointLight.range = 7f;
        pointLight.shadows = LightShadows.None;
    }

    // ------------------------------------------------------------- exit door
    private static void BuildExitDoor(Transform root)
    {
        GameObject door = Prefab(ExitDoorPrefab, root,
                                 new Vector3(0f, 0f, -2.46f), new Vector3(0f, 180f, 0f), Vector3.one);
        if (door == null) return;

        door.name = "ExitDoor";

        Transform leaf = door.transform.Find("Door");
        if (leaf == null)
        {
            Debug.LogWarning("[StaffRoomBuilder] Exit door prefab has no 'Door' child.", door);
            return;
        }

        DoorTransition transition = leaf.gameObject.AddComponent<DoorTransition>();
        SetString(transition, "targetScene", "HotelFloor_Corridor");
        SetString(transition, "spawnId", GameState.SpawnStaffRoomDoor);
        SetString(transition, "displayName", "Step back into the corridor");
    }

    // --------------------------------------------------------------- lockers
    private static void BuildLockers(Transform root)
    {
        Transform lockers = Ensure("Lockers", root);

        BuildLocker(lockers, "Nabil", 0, new Vector3(-1.4f, 0f, 2.25f), 2);  // WearUniform
        BuildLocker(lockers, "Malak", 1, new Vector3(-0.5f, 0f, 2.25f), 3);  // InspectMalakUniform
    }

    private static void BuildLocker(Transform parent, string ownerName, int ownerIndex,
                                    Vector3 position, int pickupKind)
    {
        Transform locker = Ensure("Locker_" + ownerName, parent);
        locker.localPosition = position;
        locker.localRotation = Quaternion.identity;
        locker.localScale = Vector3.one;

        SetMaterial(Box("Back", locker, new Vector3(0f, 0.9f, 0.225f), new Vector3(0.9f, 1.8f, 0.05f)), LockerBodyMat);
        SetMaterial(Box("Side_L", locker, new Vector3(-0.425f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.5f)), LockerBodyMat);
        SetMaterial(Box("Side_R", locker, new Vector3(0.425f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.5f)), LockerBodyMat);
        SetMaterial(Box("Top", locker, new Vector3(0f, 1.775f, 0f), new Vector3(0.9f, 0.05f, 0.5f)), LockerBodyMat);
        SetMaterial(Box("Bottom", locker, new Vector3(0f, 0.025f, 0f), new Vector3(0.9f, 0.05f, 0.5f)), LockerBodyMat);
        SetMaterial(Box("Shelf", locker, new Vector3(0f, 1.15f, 0f), new Vector3(0.85f, 0.03f, 0.45f)), LockerBodyMat);

        // Door swings outward from the left edge. Locker.Start() finds this by name.
        Transform pivot = Ensure("DoorPivot", locker);
        pivot.localPosition = new Vector3(-0.43f, 0.9f, -0.245f);
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;

        Transform door = Ensure("LockerDoor", pivot).transform;
        door.localPosition = new Vector3(0.43f, 0f, 0f);
        door.localRotation = Quaternion.identity;
        door.localScale = new Vector3(0.86f, 1.7f, 0.04f);
        SetMaterial(Box("Panel", door, Vector3.zero, Vector3.one), LockerDoorMat);
        SetMaterial(Box("Handle", door, new Vector3(0.36f, 0f, -1.4f), new Vector3(0.07f, 0.12f, 0.6f)), MetalMat);

        for (int i = 0; i < 4; i++)
        {
            SetMaterial(Box("Vent_" + i, door, new Vector3(0f, 0.32f - i * 0.07f, -1.4f),
                          new Vector3(0.55f, 0.025f, 0.35f)), LockerBodyMat);
        }

        // Contents - physically inside the locker, hidden by the door collider.
        SetMaterial(Box("Uniform", locker, new Vector3(0f, 1.22f, 0.05f),
                        new Vector3(0.36f, 0.1f, 0.3f)), BeigeMat);

        PickupItem pickup = locker.Find("Uniform").gameObject.AddComponent<PickupItem>();
        SetEnum(pickup, "kind", pickupKind);

        Locker component = locker.gameObject.AddComponent<Locker>();
        SetEnum(component, "owner", ownerIndex);
        SetString(component, "ownerName", ownerName);
        locker.gameObject.AddComponent<AudioSource>();
    }

    // ---------------------------------------------------------- cleaning kit
    private static void BuildCleaningKit(Transform root)
    {
        Transform items = Ensure("Items", root);

        Transform box = Ensure("CleaningBox", items);
        box.localPosition = new Vector3(0.45f, 0f, 1.7f);
        box.localRotation = Quaternion.identity;
        box.localScale = Vector3.one;

        SetMaterial(Box("Body", box, new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.5f, 0.45f)), BrownMat);
        SetMaterial(Box("Flap", box, new Vector3(0f, 0.47f, -0.26f), new Vector3(0.55f, 0.14f, 0.03f)), BrownMat);
        SetMaterial(Cyl("Bottle_A", box, new Vector3(0.13f, 0.6f, 0.08f), new Vector3(0.09f, 0.11f, 0.09f)), GreenMat);
        SetMaterial(Cyl("Bottle_B", box, new Vector3(-0.14f, 0.57f, -0.02f), new Vector3(0.08f, 0.09f, 0.08f)), RedMat);
        SetMaterial(Box("Rags", box, new Vector3(0f, 0.52f, 0.15f), new Vector3(0.24f, 0.05f, 0.16f)), BeigeMat);

        box.gameObject.AddComponent<PickupItem>();   // kind defaults to CleaningKit

        // Mop + bucket, pure set dressing.
        Transform mop = Ensure("Mop", items);
        mop.localPosition = new Vector3(-2.78f, 0f, -2.0f);
        SetMaterial(Cyl("Handle", mop, new Vector3(0f, 0.85f, 0f), new Vector3(0.035f, 0.85f, 0.035f)), BrownMat);
        SetMaterial(Cyl("Head", mop, new Vector3(0f, 0.07f, 0f), new Vector3(0.16f, 0.07f, 0.16f)), BeigeMat);

        SetMaterial(Cyl("Bucket", items, new Vector3(-2.45f, 0.16f, -1.75f),
                        new Vector3(0.32f, 0.16f, 0.32f)), RedMat);
    }

    // -------------------------------------------------------------- furniture
    private static void BuildFurniture(Transform root)
    {
        Transform props = Ensure("Props", root);

        GameObject desk = Prefab(OfficePack + "(Prb)Desk1.prefab", props,
                                 new Vector3(1.7f, 0f, 2.15f), new Vector3(0f, 180f, 0f), Vector3.one);
        Prefab(OfficePack + "(Prb)OfficeChair.prefab", props,
               new Vector3(1.7f, 0f, 1.35f), Vector3.zero, Vector3.one);
        Prefab(OfficePack + "(Prb)Shelves2.prefab", props,
               new Vector3(-2.72f, 0f, 0.7f), new Vector3(0f, 90f, 0f), Vector3.one);
        Prefab(OfficePack + "(Prb)TrashCan.prefab", props,
               new Vector3(2.6f, 0f, -1.95f), Vector3.zero, Vector3.one);
        Prefab(OfficePack + "(Prb)Clock.prefab", props,
               new Vector3(2.94f, 2.15f, -0.5f), new Vector3(0f, -90f, 0f), Vector3.one);
        Prefab(OfficePack + "(Prb)Plant2.prefab", props,
               new Vector3(2.6f, 0f, 2.1f), Vector3.zero, Vector3.one);
        Prefab(OfficePack + "(Prb)WaterDispenser.prefab", props,
               new Vector3(2.72f, 0f, 0.7f), new Vector3(0f, -90f, 0f), Vector3.one);

        PlaceDeskClutter(desk, props);
    }

    /// <summary>Sits the radio, mug, papers etc. on whatever surface the desk turned out to have.</summary>
    private static void PlaceDeskClutter(GameObject desk, Transform props)
    {
        if (desk == null) return;

        Renderer[] renderers = desk.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float top = bounds.max.y;
        float cx = bounds.center.x;
        float cz = bounds.center.z;
        float halfDepth = bounds.size.z * 0.5f;

        // Radio - front of the desk, easy to spot from the door.
        Transform radio = Ensure("Radio", props);
        radio.localPosition = new Vector3(cx, top, cz - halfDepth * 0.25f);
        radio.localRotation = Quaternion.identity;
        radio.localScale = Vector3.one;

        SetMaterial(Box("Body", radio, new Vector3(0f, 0.11f, 0f), new Vector3(0.34f, 0.22f, 0.13f)), BlackMat);
        SetMaterial(Box("Face", radio, new Vector3(0f, 0.11f, -0.07f), new Vector3(0.3f, 0.18f, 0.02f)), BeigeMat);
        SetMaterial(Cyl("Dial", radio, new Vector3(0.11f, 0.11f, -0.085f), new Vector3(0.045f, 0.015f, 0.045f)), MetalMat);
        SetMaterial(Cyl("Antenna", radio, new Vector3(-0.14f, 0.28f, 0.02f), new Vector3(0.012f, 0.1f, 0.012f)), MetalMat);
        radio.gameObject.AddComponent<PickupItem>();
        SetEnum(radio.GetComponent<PickupItem>(), "kind", 1);   // Radio

        SetMaterial(Cyl("Mug", props, new Vector3(cx - 0.4f, top + 0.05f, cz - halfDepth * 0.35f),
                        new Vector3(0.16f, 0.05f, 0.16f)), RedMat);
        SetMaterial(Box("Paperwork", props, new Vector3(cx + 0.4f, top + 0.01f, cz - halfDepth * 0.2f),
                        new Vector3(0.3f, 0.02f, 0.22f)), BeigeMat);
        SetMaterial(Cyl("PenHolder", props, new Vector3(cx - 0.16f, top + 0.07f, cz + halfDepth * 0.3f),
                        new Vector3(0.1f, 0.07f, 0.1f)), GreenMat);
    }

    // ---------------------------------------------------------- wall details
    private static void BuildWallDetails(Transform root)
    {
        Transform details = Ensure("Details", root);

        // Notice board on the south wall (room side is +Z).
        Transform board = Ensure("NoticeBoard", details);
        board.localPosition = new Vector3(-1.9f, 1.65f, -2.475f);
        SetMaterial(Box("Board", board, Vector3.zero, new Vector3(1.2f, 0.8f, 0.05f)), BrownMat);
        SetMaterial(Box("Note_1", board, new Vector3(-0.3f, 0.15f, 0.03f), new Vector3(0.28f, 0.34f, 0.01f)), BeigeMat);
        SetMaterial(Box("Note_2", board, new Vector3(0.08f, -0.1f, 0.03f), new Vector3(0.24f, 0.28f, 0.01f)), BeigeMat);
        SetMaterial(Box("Note_3", board, new Vector3(0.38f, 0.18f, 0.03f), new Vector3(0.2f, 0.22f, 0.01f)), RedMat);

        // Light switch beside the door.
        SetMaterial(Box("LightSwitch", details, new Vector3(0.8f, 1.2f, -2.475f),
                        new Vector3(0.13f, 0.19f, 0.05f)), BeigeMat);
        details.Find("LightSwitch").gameObject.AddComponent<LightSwitch>();

        // Coat hooks on the east wall.
        for (int i = 0; i < 3; i++)
        {
            SetMaterial(Box("CoatHook_" + i, details,
                            new Vector3(2.98f, 1.62f, -1.35f + i * 0.32f),
                            new Vector3(0.06f, 0.14f, 0.07f)), MetalMat);
        }
    }

    // -------------------------------------------------------- player + spawns
    private static void ConfigurePlayerAndSpawns()
    {
        Transform spawnRoot = Ensure("SpawnPoints", null);

        Transform inside = Ensure("Spawn_Inside", spawnRoot);
        inside.localPosition = new Vector3(0f, 0f, -1.7f);
        inside.localRotation = Quaternion.identity;

        SpawnPoint spawn = inside.GetComponent<SpawnPoint>();
        if (spawn == null) spawn = inside.gameObject.AddComponent<SpawnPoint>();
        SetString(spawn, "spawnId", GameState.SpawnFromCorridor);

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            player.transform.SetPositionAndRotation(inside.position, inside.rotation);
        }
    }

    // ----------------------------------------------------------------- utils
    private static Transform Ensure(string name, Transform parent)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        if (existing == null && parent == null)
        {
            GameObject found = GameObject.Find(name);
            if (found != null) existing = found.transform;
        }
        if (existing != null) return existing;

        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.GetChild(i).gameObject);
    }

    private static GameObject Empty(string name, Transform parent, Vector3 localPosition, Vector3 localEuler)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localEulerAngles = localEuler;
        return go;
    }

    private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        return Shape(PrimitiveType.Cube, name, parent, localPosition, localScale);
    }

    private static GameObject Cyl(string name, Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        return Shape(PrimitiveType.Cylinder, name, parent, localPosition, localScale);
    }

    private static GameObject Shape(PrimitiveType type, string name, Transform parent,
                                    Vector3 localPosition, Vector3 localScale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        return go;
    }

    private static GameObject Prefab(string path, Transform parent,
                                     Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null)
        {
            Debug.LogWarning($"[StaffRoomBuilder] Missing prefab: {path}");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localEulerAngles = localEuler;
        instance.transform.localScale = localScale;
        return instance;
    }

    private static void SetMaterial(GameObject go, string path)
    {
        if (go == null) return;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Debug.LogWarning($"[StaffRoomBuilder] Missing material: {path}");
            return;
        }

        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>Swaps any Built-in Standard / error-shader material for a URP-safe one.</summary>
    private static void FixUnsafeMaterials(GameObject root)
    {
        int fixedCount = 0;

        foreach (Renderer rend in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = rend.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;

                string shaderName = material.shader != null ? material.shader.name : "";
                if (shaderName != "Standard" && !shaderName.Contains("Error")) continue;

                string replacement;
                if (!MaterialFixes.TryGetValue(material.name, out replacement)) replacement = BeigeMat;

                Material urp = AssetDatabase.LoadAssetAtPath<Material>(replacement);
                if (urp == null) continue;

                materials[i] = urp;
                changed = true;
                fixedCount++;
            }

            if (changed) rend.sharedMaterials = materials;
        }

        if (fixedCount > 0)
            Debug.Log($"[StaffRoomBuilder] Remapped {fixedCount} Built-in material slot(s) to URP.");
    }

    private static void SetString(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) { Warn(target, field); return; }
        property.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum(Object target, string field, int index)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) { Warn(target, field); return; }
        property.enumValueIndex = index;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Warn(Object target, string field)
    {
        Debug.LogWarning($"[StaffRoomBuilder] Field '{field}' not found on {target.GetType().Name}.");
    }
}
