using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds Assets/Scenes/MainMenu.unity: a dark hotel corridor seen from one end, a sliver of warm light under
/// door 7, flickering bulbs. The menu itself is built at runtime by MainMenu. Menu: Room Seven / Build main menu.
/// Safe to run again - it recreates the scene and keeps it first in the build settings.
/// </summary>
public static class MainMenuBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const float W = 2.6f, H = 3.0f, L = 16f;

    [MenuItem("Room Seven/Build main menu")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene_Setup();
        Debug.Log("[MainMenuBuilder] Built " + ScenePath);
    }

    private static void Scene_Setup()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.025f, 0.026f, 0.03f);
        RenderSettings.fogDensity = 0.055f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.06f, 0.075f);

        GameObject root = new GameObject("Menu_Backdrop");
        Transform r = root.transform;

        Material carpet = RoomKit.Plain("Menu_Carpet", new Color(0.13f, 0.04f, 0.05f), 0.1f);
        Material ceiling = RoomKit.Plain("Menu_Ceiling", new Color(0.13f, 0.12f, 0.11f), 0.1f);
        Material wall = AssetDatabase.LoadAssetAtPath<Material>(RoomKit.MaterialDir + "RS_Wall_Burlap_4x3.mat");
        if (wall == null) wall = RoomKit.Plain("Menu_Wall", new Color(0.35f, 0.3f, 0.22f), 0.1f);
        Material doorWood = RoomKit.Plain("Menu_Door", new Color(0.16f, 0.09f, 0.05f), 0.35f);
        Material frameWood = RoomKit.Plain("Menu_DoorFrame", new Color(0.09f, 0.06f, 0.04f), 0.3f);
        Material brass = RoomKit.Plain("Menu_Brass", new Color(0.55f, 0.42f, 0.2f), 0.6f, 0.8f);
        Material trim = RoomKit.Plain("Menu_Skirting", new Color(0.07f, 0.05f, 0.04f), 0.3f);

        RoomKit.Box("Floor", r, new Vector3(0f, -0.05f, L / 2f), new Vector3(W, 0.1f, L), carpet, false);
        RoomKit.Box("Ceiling", r, new Vector3(0f, H + 0.05f, L / 2f), new Vector3(W, 0.1f, L), ceiling, false);
        for (int i = 0; i < 4; i++)
        {
            float z = 2f + i * 4f;
            RoomKit.Box("Wall_L" + i, r, new Vector3(-W / 2f - 0.05f, H / 2f, z), new Vector3(0.1f, H, 4f), wall, false);
            RoomKit.Box("Wall_R" + i, r, new Vector3(W / 2f + 0.05f, H / 2f, z), new Vector3(0.1f, H, 4f), wall, false);
        }
        RoomKit.Box("Wall_End", r, new Vector3(0f, H / 2f, L + 0.05f), new Vector3(W, H, 0.1f), wall, false);
        RoomKit.Box("Skirting_L", r, new Vector3(-W / 2f + 0.015f, 0.08f, L / 2f), new Vector3(0.03f, 0.16f, L), trim, false);
        RoomKit.Box("Skirting_R", r, new Vector3(W / 2f - 0.015f, 0.08f, L / 2f), new Vector3(0.03f, 0.16f, L), trim, false);

        // doors down both sides, numbered 1..6
        float[] zs = { 3.5f, 5.5f, 7.5f, 9.5f, 11.5f, 13.5f };
        for (int i = 0; i < zs.Length; i++)
        {
            bool left = i % 2 == 0;
            float x = left ? -W / 2f : W / 2f;
            float s = left ? 1f : -1f;
            RoomKit.Box("DoorFrame" + (i + 1), r, new Vector3(x + s * 0.02f, 1.1f, zs[i]), new Vector3(0.05f, 2.2f, 1.15f), frameWood, false);
            RoomKit.Box("Door" + (i + 1), r, new Vector3(x + s * 0.05f, 1.05f, zs[i]), new Vector3(0.05f, 2.1f, 1.0f), doorWood, false);
            RoomKit.Box("Handle" + (i + 1), r, new Vector3(x + s * 0.09f, 1.0f, zs[i] + 0.38f), new Vector3(0.06f, 0.04f, 0.14f), brass, false);
            Plate(r, (i + 1).ToString(), new Vector3(x + s * 0.085f, 1.72f, zs[i]), left ? -90f : 90f, new Color(0.7f, 0.55f, 0.3f), false);
        }

        // door 7 at the end, with the light under it
        RoomKit.Box("DoorFrame7", r, new Vector3(0f, 1.1f, L - 0.02f), new Vector3(1.15f, 2.2f, 0.05f), frameWood, false);
        RoomKit.Box("Door7", r, new Vector3(0f, 1.05f, L - 0.05f), new Vector3(1.0f, 2.1f, 0.05f), doorWood, false);
        RoomKit.Box("Handle7", r, new Vector3(0.38f, 1.0f, L - 0.09f), new Vector3(0.14f, 0.04f, 0.06f), brass, false);
        Plate(r, "7", new Vector3(0f, 1.78f, L - 0.085f), 0f, new Color(1f, 0.72f, 0.3f), true);

        Material strip = RoomKit.Plain("Menu_DoorLight", new Color(1f, 0.75f, 0.35f), 0.2f, 0f, new Color(1f, 0.7f, 0.3f) * 3.2f);
        GameObject stripGo = RoomKit.Box("DoorLight", r, new Vector3(0f, 0.012f, L - 0.3f), new Vector3(0.95f, 0.012f, 0.5f), strip, false);
        Light glow = NewLight("DoorGlow", r, new Vector3(0f, 0.2f, L - 0.9f), new Color(1f, 0.72f, 0.35f), 2.6f, 9f);

        // ceiling bulbs
        Material bulbMat = RoomKit.Plain("Menu_Bulb", new Color(0.9f, 0.8f, 0.6f), 0.1f, 0f, new Color(1f, 0.85f, 0.6f) * 2.4f);
        List<Light> bulbs = new List<Light>();
        List<Renderer> glass = new List<Renderer>();
        float[] bz = { 2.2f, 7.2f, 12.2f };
        for (int i = 0; i < bz.Length; i++)
        {
            GameObject fix = RoomKit.Box("Fixture" + i, r, new Vector3(0f, H - 0.03f, bz[i]), new Vector3(0.5f, 0.05f, 0.14f), bulbMat, false);
            glass.Add(fix.GetComponent<Renderer>());
            bulbs.Add(NewLight("Bulb" + i, r, new Vector3(0f, H - 0.35f, bz[i]), new Color(1f, 0.82f, 0.55f), 2.2f, 6.5f));
        }

        // a cleaning cart parked by the wall: Nabil's trade
        System.Reflection.MethodInfo place = typeof(MirrorRoomBuilder).GetMethod("Place", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Material cartMat = AssetDatabase.LoadAssetAtPath<Material>(RoomKit.MaterialDir + "Mirror_Cart.mat");
        if (place != null)
        {
            GameObject cart = (GameObject)place.Invoke(null, new object[] { "Assets/Models/Cleaning cart/source/Cleaning_Cart.fbx", r, new Vector3(0.78f, 0f, 5.0f), 8f, 1.25f, cartMat, "Cleaning_Cart", -90f });
            Collider cc = cart.GetComponent<Collider>(); if (cc != null) Object.DestroyImmediate(cc);
        }

        // camera at the near end
        GameObject camGo = new GameObject("MenuCamera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 48f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0.4f, 1.55f, 0.4f);
        camGo.transform.rotation = Quaternion.Euler(1.5f, 4f, 0f);

        // the menu itself
        GameObject menu = new GameObject("MainMenu");
        MainMenu mm = menu.AddComponent<MainMenu>();
        SerializedObject so = new SerializedObject(mm);
        so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Menu/Constantia SDF.asset");
        so.FindProperty("titleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Menu/PerpetuaTitling-Bold SDF.asset");
        so.FindProperty("menuFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Menu/PerpetuaTitling-Light SDF.asset");
        so.FindProperty("arabicFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Menu/SakkalMajalla-Bold SDF.asset");
        so.FindProperty("cam").objectReferenceValue = cam;
        SetArray(so.FindProperty("bulbs"), bulbs.ToArray());
        SetArray(so.FindProperty("bulbGlass"), glass.ToArray());
        so.FindProperty("doorGlow").objectReferenceValue = glow;
        so.FindProperty("doorStrip").objectReferenceValue = stripGo.GetComponent<Renderer>();
        so.FindProperty("ambience").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/scary hallway.mp3");
        so.FindProperty("heartbeat").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/heartbeat.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutFirstInBuild();
    }

    private static void SetArray<T>(SerializedProperty p, T[] items) where T : Object
    {
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static Light NewLight(string name, Transform parent, Vector3 pos, Color c, float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    private static void Plate(Transform parent, string number, Vector3 pos, float yaw, Color color, bool glow)
    {
        GameObject go = new GameObject("Plate" + number);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = number;
        t.fontSize = glow ? 2.4f : 1.6f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.rectTransform.sizeDelta = new Vector2(0.4f, 0.3f);
    }

    private static void PutFirstInBuild()
    {
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
