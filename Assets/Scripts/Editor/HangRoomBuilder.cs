using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds Hang_Room: a long depressing corridor (the door behind Nabil slams shut), a door at the end, and past it a
/// bare room with one tired lamp, a rope from the ceiling and a chair on its side. See HangRoom for the sequence.
/// Re-running rebuilds everything except the player / spawn / game-systems objects.
/// </summary>
public static class HangRoomBuilder
{
    public const string ScenePath = "Assets/Scenes/Limbo_Corridor.unity";   // the corridor + room live in the Limbo scene, behind the mirror room's east door
    const string DoorOpenClip = "Assets/ThirdParty/Free Wood Door Pack/Audio/Door_Open.wav";
    const string Hang = "Assets/Audio/Hang/ElevenLabs_ghozlan-";

    static Material M(string n, Color c, float smooth = 0.3f, Color? emission = null)
    {
        return RoomKit.Plain("Hang_" + n, c, smooth, 0f, emission);
    }

    static GameObject Box(string name, Transform parent, Vector3 c, Vector3 size, Material mat, bool collider = true)
    {
        return RoomKit.Box(name, parent, c, size, mat, collider);
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range, LightShadows shadows = LightShadows.None)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = shadows;
        return l;
    }

    static void Set(Object o, System.Action<SerializedObject> fill)
    {
        SerializedObject so = new SerializedObject(o);
        fill(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static AudioClip Clip(string path) { return AssetDatabase.LoadAssetAtPath<AudioClip>(path); }

    static Transform Deep(Transform t, string n)
    {
        if (t.name == n) return t;
        for (int i = 0; i < t.childCount; i++) { Transform r = Deep(t.GetChild(i), n); if (r != null) return r; }
        return null;
    }

    [MenuItem("Room Seven/Build hanging room")]
    public static void Build()
    {
        Scene sc = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject old = GameObject.Find("Hang_Root"); if (old != null) Object.DestroyImmediate(old);
        GameObject oldSp = GameObject.Find("Hang_Start"); if (oldSp != null) Object.DestroyImmediate(oldSp);
        GameObject exitVoid = GameObject.Find("Exit_Void"); if (exitVoid != null) Object.DestroyImmediate(exitVoid);   // the black box that hid the void behind the door
        GameObject exitHinge = GameObject.Find("Exit_Hinge");
        Set(exitHinge.GetComponent<LimboDoor>(), so => so.FindProperty("targetScene").stringValue = "");   // no load: it just opens onto the corridor

        Transform root = new GameObject("Hang_Root").transform;
        Transform shell = RoomKit.Group("Shell", root).transform;
        Transform lighting = RoomKit.Group("Lighting", root).transform;
        Transform props = RoomKit.Group("Props", root).transform;

        Material floor = M("Floor", new Color(0.12f, 0.12f, 0.10f), 0.55f);
        Material wall = M("Wall", new Color(0.22f, 0.24f, 0.21f), 0.08f);
        Material roomWall = M("RoomWall", new Color(0.27f, 0.25f, 0.21f), 0.08f);
        Material ceil = M("Ceiling", new Color(0.07f, 0.07f, 0.07f), 0.05f);
        Material trim = M("Trim", new Color(0.05f, 0.05f, 0.05f), 0.3f);
        Material doorMat = M("Door", new Color(0.16f, 0.15f, 0.13f), 0.35f);
        Material stain = M("Stain", new Color(0.08f, 0.09f, 0.07f), 0.2f);
        Material wet = M("Puddle", new Color(0.03f, 0.04f, 0.045f), 0.95f);
        Material metal = M("Metal", new Color(0.4f, 0.4f, 0.38f), 0.6f);
        Material black = M("Black", Color.black, 0f);
        Material bulbMat = M("Bulb", new Color(0.9f, 0.8f, 0.55f), 0.1f, new Color(2.2f, 1.8f, 1.1f));
        Material ropeMat = M("Rope", new Color(0.42f, 0.34f, 0.22f), 0.1f);
        Material wood = M("ChairWood", new Color(0.22f, 0.14f, 0.08f), 0.3f);

        // ---------------------------------------------------------- corridor (z 0..12) --
        // (starts at z=0, behind the mirror room's wall: an earlier start at -0.1 was coplanar with that wall and z-fought, flashing a frame around the door)
        Box("C_Floor", shell, new Vector3(0f, -0.05f, 6.05f), new Vector3(2.2f, 0.1f, 12.1f), floor);
        Box("C_Ceiling", shell, new Vector3(0f, 2.75f, 6.05f), new Vector3(2.2f, 0.1f, 12.1f), ceil);
        Box("C_Wall_W", shell, new Vector3(-1.05f, 1.35f, 6.05f), new Vector3(0.1f, 2.7f, 12.1f), wall);
        Box("C_Wall_E", shell, new Vector3(1.05f, 1.35f, 6.05f), new Vector3(0.1f, 2.7f, 12.1f), wall);
        Box("Skirt_W", shell, new Vector3(-0.99f, 0.07f, 6f), new Vector3(0.04f, 0.14f, 12f), trim, false);
        Box("Skirt_E", shell, new Vector3(0.99f, 0.07f, 6f), new Vector3(0.04f, 0.14f, 12f), trim, false);

        

        // the end wall: doorway 1 m wide, 2.1 m high; the room's south wall is 3.2 m so it reaches higher
        Box("End_L", shell, new Vector3(-1.575f, 1.6f, 12f), new Vector3(2.15f, 3.2f, 0.1f), wall);
        Box("End_R", shell, new Vector3(1.575f, 1.6f, 12f), new Vector3(2.15f, 3.2f, 0.1f), wall);
        Box("End_Lintel", shell, new Vector3(0f, 2.65f, 12f), new Vector3(1f, 1.1f, 0.1f), wall);
        Box("Frame_L", shell, new Vector3(-0.53f, 1.05f, 11.95f), new Vector3(0.06f, 2.1f, 0.14f), trim, false);
        Box("Frame_R", shell, new Vector3(0.53f, 1.05f, 11.95f), new Vector3(0.06f, 2.1f, 0.14f), trim, false);
        Box("Frame_Top", shell, new Vector3(0f, 2.13f, 11.95f), new Vector3(1.12f, 0.06f, 0.14f), trim, false);
        GameObject hinge = new GameObject("Door_Hinge");
        hinge.transform.SetParent(props, false);
        hinge.transform.position = new Vector3(0.5f, 0f, 12f);
        GameObject leaf = Box("Door_Leaf", hinge.transform, Vector3.zero, new Vector3(0.98f, 2.08f, 0.06f), doorMat);
        leaf.transform.localPosition = new Vector3(-0.49f, 1.04f, 0f);
        GameObject handle = Box("Door_Handle", leaf.transform, Vector3.zero, Vector3.one, metal, false);
        handle.transform.localPosition = new Vector3(-0.41f, -0.04f, -0.9f);
        handle.transform.localScale = new Vector3(0.12f, 0.04f, 1.3f);
        LimboDoor door = hinge.AddComponent<LimboDoor>();
        Set(door, so => so.FindProperty("openClip").objectReferenceValue = Clip(DoorOpenClip));

        // shut doors along the corridor, water stains, a puddle, a ceiling pipe
        float[] dz = { 3.4f, 6.2f, 9.0f };
        for (int i = 0; i < dz.Length; i++)
            foreach (float s in new[] { -1f, 1f })
            {
                float z = dz[i] + (s < 0 ? 0.7f : -0.5f);
                Box("SideDoor", props, new Vector3(0.97f * s, 1.03f, z), new Vector3(0.06f, 2.06f, 0.92f), doorMat);
                Box("SideDoorFrame", props, new Vector3(0.965f * s, 1.05f, z), new Vector3(0.04f, 2.12f, 1.02f), trim, false);
                Box("SideDoorKnob", props, new Vector3(0.93f * s, 1.0f, z + 0.36f), new Vector3(0.06f, 0.06f, 0.06f), metal, false);
            }
        
        Prim(PrimitiveType.Cylinder, "Pipe", props, new Vector3(-0.75f, 2.55f, 6f), new Vector3(0.08f, 6.1f, 0.08f), new Vector3(90f, 0f, 0f), metal);

        // three dim bulbs, tired and greenish
        float[] zs = { 2.4f, 6.2f, 9.8f };
        float[] inten = { 1.4f, 1.1f, 0.9f };
        for (int i = 0; i < zs.Length; i++)
        {
            Box("Fixture_" + i, lighting, new Vector3(0f, 2.67f, zs[i]), new Vector3(0.5f, 0.05f, 0.14f), bulbMat, false);
            Light l = PointLight("Bulb_" + i, lighting, new Vector3(0f, 2.5f, zs[i]), new Color(0.75f, 0.85f, 0.65f), inten[i], 4.2f);
            FlickerLight fl = l.gameObject.AddComponent<FlickerLight>();
            int k = i;
            Set(fl, so =>
            {
                so.FindProperty("baseIntensity").floatValue = inten[k];
                so.FindProperty("wobble").floatValue = 0.25f;
                so.FindProperty("dropEvery").floatValue = k == 1 ? 3f : 7f;
                so.FindProperty("bulb").objectReferenceValue = lighting.Find("Fixture_" + k).GetComponent<Renderer>();
            });
        }

        // -------------------------------------------------------------- room (z 12..17.6) --
        Box("R_Floor", shell, new Vector3(0f, -0.05f, 14.85f), new Vector3(5.4f, 0.1f, 5.7f), floor);
        Box("R_Ceiling", shell, new Vector3(0f, 3.25f, 14.85f), new Vector3(5.4f, 0.1f, 5.7f), ceil);
        Box("R_Wall_W", shell, new Vector3(-2.65f, 1.6f, 14.85f), new Vector3(0.1f, 3.2f, 5.7f), roomWall);
        Box("R_Wall_E", shell, new Vector3(2.65f, 1.6f, 14.85f), new Vector3(0.1f, 3.2f, 5.7f), roomWall);
        Box("R_Wall_N", shell, new Vector3(0f, 1.6f, 17.65f), new Vector3(5.4f, 3.2f, 0.1f), roomWall);
        Box("R_Skirt_N", shell, new Vector3(0f, 0.07f, 17.58f), new Vector3(5.2f, 0.14f, 0.04f), trim, false);
        

        // the lamp: a cord from the ceiling and a bare bulb, off to one side
        Prim(PrimitiveType.Cylinder, "Lamp_Cord", props, new Vector3(-1.25f, 3.0f, 14.0f), new Vector3(0.012f, 0.2f, 0.012f), Vector3.zero, black);
        GameObject bulbGo = Prim(PrimitiveType.Sphere, "Lamp_Bulb", props, new Vector3(-1.25f, 2.76f, 14.0f), new Vector3(0.16f, 0.2f, 0.16f), Vector3.zero, bulbMat);
        Light lamp = PointLight("Lamp_Light", lighting, new Vector3(-1.25f, 2.7f, 14.0f), new Color(1f, 0.78f, 0.45f), 2.6f, 9f, LightShadows.Soft);
        FlickerLight idle = lamp.gameObject.AddComponent<FlickerLight>();
        Set(idle, so =>
        {
            so.FindProperty("baseIntensity").floatValue = 2.6f;
            so.FindProperty("wobble").floatValue = 0.3f;
            so.FindProperty("dropEvery").floatValue = 2.2f;
            so.FindProperty("bulb").objectReferenceValue = bulbGo.GetComponent<Renderer>();
            so.FindProperty("bulbGlow").colorValue = new Color(1f, 0.82f, 0.5f);
        });
        Light cold = PointLight("ColdFill", lighting, new Vector3(0f, 2.3f, 16.9f), new Color(0.5f, 0.62f, 1f), 1.1f, 6f);

        // the rope: hangs from the ceiling centre; the body (when it is there) swings from the same pivot
        const float RopeX = 0f, RopeZ = 14.85f;
        GameObject pivot = new GameObject("Hang_Pivot");
        pivot.transform.SetParent(props, false);
        pivot.transform.position = new Vector3(RopeX, 3.2f, RopeZ);
        const float RingR = 0.085f, RingY = -1.23f;   // must match the loop in Assets/Models/Hang/Noose.fbx (Blender)
        Vector3 ringC = new Vector3(RingR, RingY, 0f);   // the noose is mirrored (scale x -1) so the loop leans to the viewer's left
        GameObject nooseFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Hang/Noose.fbx");
        GameObject noose = (GameObject)PrefabUtility.InstantiatePrefab(nooseFbx, pivot.transform);
        noose.name = "Noose";
        noose.transform.localPosition = Vector3.zero;
        noose.transform.localScale = new Vector3(-1f, 1f, 1f);
        foreach (Renderer nr in noose.GetComponentsInChildren<Renderer>()) nr.sharedMaterial = ropeMat;

        // the body: Nabil's rig, posed hanging. Hidden until the sequence shows it in the dark moments.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Mirror/Copy/MirrorNabil.prefab");
        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot.transform);
        body.name = "Nabil_Hanged";
        body.transform.localPosition = new Vector3(0f, -2.65f, 0f);
        body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Animator anim = body.GetComponent<Animator>();
        if (anim != null) anim.enabled = false;
        PoseHanging(body.transform);
        Transform nk = Deep(body.transform, "neck1");
        if (nk != null) body.transform.position += pivot.transform.TransformPoint(ringC) - nk.position + pivot.transform.TransformVector(new Vector3(-0.045f, 0.03f, 0f));   // nudge: the neck sits in the middle of the loop, not at its edge
        RoomKit.NoShadows(body);

        // the chair, knocked over under the rope
        Transform chair = RoomKit.Group("Chair", props).transform;
        Box("Seat", chair, Vector3.zero, new Vector3(0.44f, 0.05f, 0.44f), wood, false);
        foreach (float sx in new[] { -0.19f, 0.19f })
            foreach (float sz in new[] { -0.19f, 0.19f })
                Box("Leg", chair, new Vector3(sx, -0.25f, sz), new Vector3(0.045f, 0.46f, 0.045f), wood, false);
        foreach (float sx in new[] { -0.19f, 0.19f })
            Box("BackPost", chair, new Vector3(sx, 0.4f, -0.19f), new Vector3(0.045f, 0.78f, 0.045f), wood, false);
        Box("BackRail", chair, new Vector3(0f, 0.62f, -0.19f), new Vector3(0.4f, 0.1f, 0.04f), wood, false);
        Box("BackRail2", chair, new Vector3(0f, 0.38f, -0.19f), new Vector3(0.4f, 0.06f, 0.04f), wood, false);
        chair.position = new Vector3(0.55f, 0.24f, 15.2f);
        chair.rotation = Quaternion.Euler(0f, 28f, 90f);
        BoxCollider cc = chair.gameObject.AddComponent<BoxCollider>();
        cc.center = new Vector3(0.1f, 0f, 0f); cc.size = new Vector3(1.1f, 0.5f, 0.5f);

        // ------------------------------------------------------------------ the brains --
        GameObject brain = new GameObject("HangRoom");
        brain.transform.SetParent(root, false);
        BoxCollider trig = brain.AddComponent<BoxCollider>();
        trig.isTrigger = true; trig.center = new Vector3(0f, 1.2f, 12.9f); trig.size = new Vector3(1.8f, 2.4f, 0.8f);
        HangRoom hr = brain.AddComponent<HangRoom>();
        Set(hr, so =>
        {
            so.FindProperty("lamp").objectReferenceValue = lamp;
            so.FindProperty("bulb").objectReferenceValue = bulbGo.GetComponent<Renderer>();
            so.FindProperty("coldFill").objectReferenceValue = cold;
            so.FindProperty("idleFlicker").objectReferenceValue = idle;
            so.FindProperty("hangPivot").objectReferenceValue = pivot.transform;
            so.FindProperty("hanged").objectReferenceValue = body;
            so.FindProperty("slamHinge").objectReferenceValue = exitHinge.transform;   // the mirror room's door slams shut behind him
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/Cairo/Cairo-Regular SDF.asset");
            so.FindProperty("ambience").objectReferenceValue = Clip("Assets/Audio/Sounds/scary hallway.mp3");
            so.FindProperty("heartbeat").objectReferenceValue = Clip("Assets/Audio/Sounds/heartbeat.mp3");
            so.FindProperty("voiceCalm").objectReferenceValue = Clip(Hang + "lesa-m4-faker-calm.mp3");
            so.FindProperty("voiceSarcastic").objectReferenceValue = Clip(Hang + "lesa-m4-faker-sarcastic.mp3");
            so.FindProperty("voiceAngry").objectReferenceValue = Clip(Hang + "lesa-m4-faker-angry.mp3");
            so.FindProperty("voiceDie").objectReferenceValue = Clip(Hang + "you'll-die here.mp3");
            so.FindProperty("laugh1").objectReferenceValue = Clip(Hang + "laugh-1.mp3");
            so.FindProperty("laugh2").objectReferenceValue = Clip(Hang + "laugh-2.mp3");
            so.FindProperty("laughLoud").objectReferenceValue = Clip(Hang + "loud-laugh.mp3");
        });

        // the corridor runs straight out of the mirror room's east door (x 3.05, z 24.4), heading +x
        root.SetPositionAndRotation(new Vector3(3.1f, 0f, 24.4f), Quaternion.Euler(0f, 90f, 0f));
        GameObject sg = new GameObject("Hang_Start");
        sg.transform.SetParent(GameObject.Find("SpawnPoints").transform, false);
        sg.transform.SetPositionAndRotation(new Vector3(1.4f, 0f, 24.4f), Quaternion.Euler(0f, 90f, 0f));
        sg.AddComponent<SpawnPoint>().SetSpawnId("Hang_Start");

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        
        Debug.Log("built Hang_Room");
    }

    /// <summary>Limp: head dropped, arms straight down, legs together. Angles are tuned by eye (the rig's axes).</summary>
    static void PoseHanging(Transform rig)
    {
        Rot(rig, "neck", new Vector3(0f, 0f, -35f));
        Rot(rig, "head", new Vector3(0f, 0f, -20f));
        Rot(rig, "l upperarm", new Vector3(0f, 0f, -55f));
        Rot(rig, "r upperarm", new Vector3(0f, 0f, 55f));
        Rot(rig, "spine1", new Vector3(0f, 0f, -8f));
    }

    static void Rot(Transform rig, string bone, Vector3 euler)
    {
        Transform t = Deep(rig, bone);
        if (t != null) t.localRotation = t.localRotation * Quaternion.Euler(euler);
    }



}
