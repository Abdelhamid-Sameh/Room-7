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
    private const string FoldedShirtPrefab = "Assets/Models/Folded shirt/source/simple folded shirt.obj";
    private const string RadioPrefab = "Assets/AK STUDIO ART/Radio/Prefabs/Radio.prefab";
    private const string CleaningCartSource = "Assets/Models/Cleaning cart/source/Cleaning_Cart.fbx";

    /// <summary>Folded shirt is authored 2 m across - 0.15 makes it 0.30 m, sized to fit a real locker.</summary>
    private const float ShirtScale = 0.15f;

    // ------------------------------------------------ real locker (Low-Poly 3D Lockers)
    private const string RealLockerSource = "Assets/Low-Poly 3D Lockers/Low-Poly 3D Lockers/Mesh/M.C.C.fbx";
    private const string SplitDir = "Assets/Models/Lockers";

    /// <summary>
    /// The shipped mesh is one merged lump: a plain shell behind z = 0, and the
    /// entire front slab (door face + vents + handle) ahead of it. Everything
    /// past this plane is peeled off into the hinged door.
    /// </summary>
    private const float DoorSeamZ = 0f;

    /// <summary>Vertical hinge axis on the door's right edge, in source-mesh space.</summary>
    private static readonly Vector3 DoorHinge = new Vector3(0.24f, 0f, 0.2f);

    /// <summary>The model faces +Z but the room sits on -Z, so body and door are both flipped.</summary>
    private static readonly Quaternion ModelFlip = Quaternion.Euler(0f, 180f, 0f);

    /// <summary>Top of the shelf baked into the model, in source-mesh space.</summary>
    private const float RealShelfLocalY = 0.66f;

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

        if (!EditorUtility.DisplayDialog("Rebuild the staff room?",
                "This wipes everything under StaffRoom_Root, including any props you moved or replaced by hand, and rebuilds it from code. Continue only if you want that.",
                "Rebuild", "Cancel"))
            return;

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

        // Nabil's is the real Low-Poly locker: origin at the model's centre (half its
        // 1.985 m height above the floor) and its back resting on the north wall at
        // z 2.5.
        BuildLocker(lockers, "Nabil", 0, new Vector3(-1.4f, 0.993f, 2.281f), 2, true);    // WearUniform
        BuildLocker(lockers, "Malak", 1, new Vector3(-0.80f, 0.993f, 2.281f), 3, true);   // InspectMalakUniform - same real locker as Nabil
    }

    private static void BuildLocker(Transform parent, string ownerName, int ownerIndex,
                                    Vector3 position, int pickupKind, bool useRealModel)
    {
        Transform locker = Ensure("Locker_" + ownerName, parent);
        locker.localPosition = position;
        locker.localRotation = Quaternion.identity;
        locker.localScale = Vector3.one;

        float shelfTop = useRealModel
            ? BuildRealLocker(locker, locker.name)
            : BuildGreyboxLocker(locker);

        // Contents - physically inside the locker, hidden by the door collider.
        GameObject uniform = BuildUniform(locker, useRealModel, shelfTop);

        PickupItem pickup = uniform.AddComponent<PickupItem>();
        SetEnum(pickup, "kind", pickupKind);

        Locker component = locker.gameObject.AddComponent<Locker>();
        SetEnum(component, "owner", ownerIndex);
        SetString(component, "ownerName", ownerName);
        locker.gameObject.AddComponent<AudioSource>();
    }

    /// <summary>The original primitive locker. Returns the world-space shelf top.</summary>
    private static float BuildGreyboxLocker(Transform locker)
    {
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

        Transform door = Ensure("LockerDoor", pivot);
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

        return locker.position.y + 1.165f;
    }

    /// <summary>
    /// Swaps in the real Low-Poly locker. The shipped mesh has no separate door
    /// object - shell and front slab are a single lump - so this peels the slab
    /// off at <see cref="DoorSeamZ"/> into a static <c>Body</c> plus a hinged
    /// <c>Door</c> hanging off the <c>DoorPivot</c> that Locker.cs animates.
    /// Returns the world-space top of the shelf baked into the model.
    /// </summary>
    private static float BuildRealLocker(Transform locker, string assetTag)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(RealLockerSource);
        Mesh sourceMesh = null;
        Material sourceMat = null;
        if (source != null)
        {
            MeshFilter filter = source.GetComponentInChildren<MeshFilter>();
            Renderer renderer = source.GetComponentInChildren<Renderer>();
            if (filter != null) sourceMesh = filter.sharedMesh;
            if (renderer != null) sourceMat = renderer.sharedMaterial;
        }

        if (sourceMesh == null)
        {
            Debug.LogWarning("[StaffRoomBuilder] " + RealLockerSource
                             + " missing or meshless - falling back to the greybox locker.", locker);
            return BuildGreyboxLocker(locker);
        }

        Mesh bodyMesh, doorMesh;
        SplitDoorMesh(sourceMesh, assetTag, out bodyMesh, out doorMesh);
        Material mat = EnsureDoubleSidedMaterial(sourceMat);

        // Shell - every triangle behind the seam, so its front stays open.
        Transform body = Ensure("Body", locker);
        body.localPosition = Vector3.zero;
        body.localRotation = ModelFlip;
        body.localScale = Vector3.one;
        MeshFilter bodyFilter = GetOrAdd<MeshFilter>(body.gameObject);
        MeshRenderer bodyRend = GetOrAdd<MeshRenderer>(body.gameObject);
        bodyFilter.sharedMesh = bodyMesh;
        bodyRend.sharedMaterial = mat;

        // Non-convex MeshCollider: the walls and back stop the player, but the
        // missing front face lets the interaction ray through to the shelf.
        if (body.gameObject.GetComponent<Collider>() == null)
            body.gameObject.AddComponent<MeshCollider>().sharedMesh = bodyMesh;

        // Door - DoorPivot sits on the hinge axis, Door cancels that transform so
        // the slab starts exactly where it did in the source mesh.
        Vector3 hinge = ModelFlip * DoorHinge;
        Transform pivot = Ensure("DoorPivot", locker);
        pivot.localPosition = hinge;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;

        Transform door = Ensure("Door", pivot);
        door.localPosition = -hinge;
        door.localRotation = ModelFlip;
        door.localScale = Vector3.one;
        MeshFilter doorFilter = GetOrAdd<MeshFilter>(door.gameObject);
        MeshRenderer doorRend = GetOrAdd<MeshRenderer>(door.gameObject);
        doorFilter.sharedMesh = doorMesh;
        doorRend.sharedMaterial = mat;

        // A BoxCollider, not a MeshCollider: this one has to block the ray while shut.
        if (door.gameObject.GetComponent<Collider>() == null)
        {
            BoxCollider box = door.gameObject.AddComponent<BoxCollider>();
            box.center = doorMesh.bounds.center;
            box.size = doorMesh.bounds.size;
        }

        return locker.position.y + RealShelfLocalY;
    }

    /// <summary>
    /// Splits the merged locker mesh in two along <c>seamZ</c>. A triangle belongs
    /// to the door only when its lowest vertex clears the seam, which keeps the
    /// side/top/bottom walls (they span the full depth) in the shell while the
    /// front slab - face, vents and handle together - leaves as one piece.
    /// The halves are written to <c>Assets/Models/Lockers</c> as mesh assets.
    /// </summary>
    private static void SplitDoorMesh(Mesh source, string assetTag, out Mesh body, out Mesh door)
    {
        Vector3[] verts = source.vertices;
        Vector3[] norms = source.normals;
        Vector2[] uvs = source.uv;
        Vector4[] tangents = source.tangents;
        int[] tris = source.triangles;
        bool hasNorms = norms != null && norms.Length == verts.Length;
        bool hasUvs = uvs != null && uvs.Length == verts.Length;
        bool hasTangents = tangents != null && tangents.Length == verts.Length;

        var bPos = new System.Collections.Generic.List<Vector3>();
        var bNrm = new System.Collections.Generic.List<Vector3>();
        var bUv = new System.Collections.Generic.List<Vector2>();
        var bTan = new System.Collections.Generic.List<Vector4>();
        var bIdx = new System.Collections.Generic.List<int>();
        var bMap = new System.Collections.Generic.Dictionary<int, int>();

        var dPos = new System.Collections.Generic.List<Vector3>();
        var dNrm = new System.Collections.Generic.List<Vector3>();
        var dUv = new System.Collections.Generic.List<Vector2>();
        var dTan = new System.Collections.Generic.List<Vector4>();
        var dIdx = new System.Collections.Generic.List<int>();
        var dMap = new System.Collections.Generic.Dictionary<int, int>();

        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
            float lowest = Mathf.Min(verts[i0].z, Mathf.Min(verts[i1].z, verts[i2].z));

            if (lowest > DoorSeamZ)
                AddTriangle(i0, i1, i2, verts, norms, uvs, tangents, hasNorms, hasUvs, hasTangents,
                            dPos, dNrm, dUv, dTan, dIdx, dMap);
            else
                AddTriangle(i0, i1, i2, verts, norms, uvs, tangents, hasNorms, hasUvs, hasTangents,
                            bPos, bNrm, bUv, bTan, bIdx, bMap);
        }

        body = BuildMesh(assetTag + "_Shell", bPos, bNrm, bUv, bTan, bIdx, hasNorms);
        door = BuildMesh(assetTag + "_Door", dPos, dNrm, dUv, dTan, dIdx, hasNorms);

        EnsureFolder(SplitDir);
        WriteMeshAsset(body, SplitDir + "/" + assetTag + "_Shell.asset");
        WriteMeshAsset(door, SplitDir + "/" + assetTag + "_Door.asset");
    }

    private static void AddTriangle(int i0, int i1, int i2,
                                    Vector3[] verts, Vector3[] norms, Vector2[] uvs, Vector4[] tangents,
                                    bool hasNorms, bool hasUvs, bool hasTangents,
                                    System.Collections.Generic.List<Vector3> pos,
                                    System.Collections.Generic.List<Vector3> nrm,
                                    System.Collections.Generic.List<Vector2> uv,
                                    System.Collections.Generic.List<Vector4> tan,
                                    System.Collections.Generic.List<int> idx,
                                    System.Collections.Generic.Dictionary<int, int> map)
    {
        int[] src = { i0, i1, i2 };
        for (int k = 0; k < 3; k++)
        {
            int oldIndex = src[k];
            int newIndex;
            if (map.TryGetValue(oldIndex, out newIndex)) { idx.Add(newIndex); continue; }

            newIndex = pos.Count;
            pos.Add(verts[oldIndex]);
            if (hasNorms) nrm.Add(norms[oldIndex]);
            if (hasUvs) uv.Add(uvs[oldIndex]);
            if (hasTangents) tan.Add(tangents[oldIndex]);
            map.Add(oldIndex, newIndex);
            idx.Add(newIndex);
        }
    }

    private static Mesh BuildMesh(string name,
                                  System.Collections.Generic.List<Vector3> pos,
                                  System.Collections.Generic.List<Vector3> nrm,
                                  System.Collections.Generic.List<Vector2> uv,
                                  System.Collections.Generic.List<Vector4> tan,
                                  System.Collections.Generic.List<int> idx,
                                  bool hasNorms)
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.indexFormat = pos.Count > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(pos);
        if (nrm.Count == pos.Count) mesh.SetNormals(nrm);
        if (uv.Count == pos.Count) mesh.SetUVs(0, uv);
        if (tan.Count == pos.Count) mesh.SetTangents(tan);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateBounds();
        if (!hasNorms || nrm.Count != pos.Count) mesh.RecalculateNormals();
        return mesh;
    }

    private static void WriteMeshAsset(Mesh mesh, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }

    /// <summary>
    /// Once the door swings open you are looking at the inside of the shell, and
    /// every wall there is back-facing - culling would make the locker see-through.
    /// Copies the package material with culling off rather than editing the original.
    /// </summary>
    private static Material EnsureDoubleSidedMaterial(Material source)
    {
        string path = SplitDir + "/Locker_DoubleSided.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        EnsureFolder(SplitDir);
        Material material = source != null
            ? new Material(source)
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = "Locker_DoubleSided";
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", 0);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        string leaf = path.Substring(path.LastIndexOf('/') + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>
    /// The folded uniform resting on the locker shelf. Nabil's is the real
    /// folded-shirt model; Malak's stays a simple folded slab until hers is
    /// modelled separately. Either way the object is named "Uniform", sits
    /// inside the locker so the closed door hides it, and returns the
    /// GameObject the PickupItem belongs on.
    /// </summary>
    private static GameObject BuildUniform(Transform locker, bool useModel, float shelfTop)
    {
        if (useModel)
        {
            // The real locker's interior is 0.45 m wide and 0.37 m deep, so the shirt
            // sits a little back from centre to clear the door on its inner face.
            GameObject shirt = Prefab(FoldedShirtPrefab, locker,
                                      new Vector3(0f, shelfTop - locker.position.y, 0.011f),
                                      Vector3.zero, new Vector3(ShirtScale, ShirtScale, ShirtScale));
            if (shirt != null)
            {
                shirt.name = "Uniform";
                RestOnShelf(shirt, shelfTop);
                EnsureBoxCollider(shirt);
                return shirt;
            }

            Debug.LogWarning("[StaffRoomBuilder] Folded shirt missing - falling back to a grey slab.", locker);
        }

        SetMaterial(Box("Uniform", locker, new Vector3(0f, 1.22f, 0.05f),
                        new Vector3(0.36f, 0.1f, 0.3f)), BeigeMat);
        RestOnShelf(locker.Find("Uniform").gameObject, shelfTop);
        return locker.Find("Uniform").gameObject;
    }

    /// <summary>
    /// Drops the object so its lowest point sits exactly on the shelf top.
    /// The shirt is authored around y 0.71..1.00, so its pivot floats below it
    /// - reading the bounds beats hard-coding an offset that breaks on reimport.
    /// </summary>
    private static void RestOnShelf(GameObject item, float shelfTop)
    {
        Renderer itemRenderer = item.GetComponentInChildren<Renderer>();
        if (itemRenderer == null) return;

        item.transform.position += new Vector3(0f, shelfTop - itemRenderer.bounds.min.y, 0f);
    }

    /// <summary>The imported OBJ has no collider, and the interaction raycast needs one.</summary>
    private static void EnsureBoxCollider(GameObject host)
    {
        if (host.GetComponentInChildren<Collider>(true) != null) return;

        MeshFilter filter = host.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        BoxCollider box = filter.gameObject.AddComponent<BoxCollider>();
        box.center = filter.sharedMesh.bounds.center;
        box.size = filter.sharedMesh.bounds.size;
    }

    /// <summary>
    /// Adds one BoxCollider on 'host' covering every Renderer underneath it.
    /// Assumes host has no rotation and uniform scale 1 (true for everything
    /// this builder places), so world bounds can be used directly.
    /// </summary>
    private static void AddCombinedBoxCollider(GameObject host)
    {
        if (host.GetComponentInChildren<Collider>(true) != null) return;

        Renderer[] renderers = host.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        BoxCollider box = host.AddComponent<BoxCollider>();
        box.center = bounds.center - host.transform.position;
        box.size = bounds.size;
    }

    // ---------------------------------------------------------- cleaning kit
    private static void BuildCleaningKit(Transform root)
    {
        Transform items = Ensure("Items", root);

        // Real cleaning cart model - replaces the old box + mop + bucket primitives
        // and already reads as a housekeeper's cart on its own.
        GameObject cart = Prefab(CleaningCartSource, items,
                                 new Vector3(-2.2f, 0f, -1.85f), Vector3.zero, Vector3.one);
        if (cart != null)
        {
            cart.name = "CleaningCart";
            AddCombinedBoxCollider(cart);
            cart.AddComponent<PickupItem>();   // kind defaults to CleaningKit
        }
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

        // Radio - the real AK Studio Art prop, front of the desk, easy to spot from the door.
        GameObject radio = Prefab(RadioPrefab, props,
                                  new Vector3(cx, top, cz - halfDepth * 0.25f), Vector3.zero, Vector3.one);
        if (radio != null)
        {
            radio.name = "Radio";
            PickupItem radioPickup = radio.AddComponent<PickupItem>();
            SetEnum(radioPickup, "kind", 1);   // Radio
        }

        // Desk clutter - nappin Office Essentials props, pivots sit at their own base.
        Prefab(OfficePack + "(Prb)Mug.prefab", props,
               new Vector3(cx - 0.4f, top, cz - halfDepth * 0.35f), Vector3.zero, Vector3.one);
        Prefab(OfficePack + "(Prb)DocumentHolder.prefab", props,
               new Vector3(cx + 0.4f, top, cz - halfDepth * 0.2f), Vector3.zero, Vector3.one);
        Prefab(OfficePack + "(Prb)PenHolder.prefab", props,
               new Vector3(cx - 0.16f, top, cz + halfDepth * 0.3f), Vector3.zero, Vector3.one);
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
