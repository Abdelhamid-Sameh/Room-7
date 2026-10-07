using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helper: lays prefabs out in a labelled grid in the CURRENT scene so they can be looked at
/// and compared (use it in a scratch scene). Call AssetSheet.Build from an execute-code snippet or a menu.
/// </summary>
public static class AssetSheet
{
    public static void Build(string[] prefabPaths, float[] scales, string[] labels, int cols)
    {
        GameObject old = GameObject.Find("_SHEET");
        if (old != null) Object.DestroyImmediate(old);

        GameObject root = new GameObject("_SHEET");
        var objs = new GameObject[prefabPaths.Length];
        var sizes = new Vector3[prefabPaths.Length];
        float cell = 0f;
        float tallest = 0f;

        for (int i = 0; i < prefabPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            if (prefab == null) continue;

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            go.transform.localScale = Vector3.one * scales[i];
            objs[i] = go;

            Bounds b = Measure(go);
            sizes[i] = b.size;
            cell = Mathf.Max(cell, Mathf.Max(b.size.x, b.size.z));
            tallest = Mathf.Max(tallest, b.size.y);
        }

        cell += 0.6f;
        int rows = (prefabPaths.Length + cols - 1) / cols;

        for (int i = 0; i < prefabPaths.Length; i++)
        {
            if (objs[i] == null) continue;
            int col = i % cols, row = i / cols;

            objs[i].transform.position = Vector3.zero;
            Bounds b = Measure(objs[i]);
            objs[i].transform.position = new Vector3(col * cell - b.center.x, -b.min.y, row * cell - b.center.z);

            GameObject label = new GameObject("label_" + labels[i]);
            label.transform.SetParent(root.transform, false);
            label.transform.position = new Vector3(col * cell, sizes[i].y + 0.35f, row * cell);
            TMPro.TextMeshPro t = label.AddComponent<TMPro.TextMeshPro>();
            t.text = labels[i];
            t.fontSize = 3.2f;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.color = Color.yellow;
            t.rectTransform.sizeDelta = new Vector2(4f, 1f);
        }

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "floor";
        floor.transform.SetParent(root.transform, false);
        floor.transform.position = new Vector3((cols - 1) * cell * 0.5f, -0.01f, (rows - 1) * cell * 0.5f);
        floor.transform.localScale = new Vector3(cols * cell * 0.12f, 1f, rows * cell * 0.12f);
        Object.DestroyImmediate(floor.GetComponent<Collider>());

        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun == null)
        {
            GameObject lg = new GameObject("sun");
            sun = lg.AddComponent<Light>();
            sun.type = LightType.Directional;
            lg.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.intensity = 1.2f;
        }

        GameObject camGo = GameObject.Find("MainCamera");
        if (camGo == null) { camGo = new GameObject("MainCamera"); camGo.tag = "MainCamera"; camGo.AddComponent<Camera>(); }
        Camera cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.2f, 0.24f);

        float width = cols * cell;
        float depth = rows * cell;
        float dist = Mathf.Max(width * 0.62f, depth * 1.1f + tallest * 1.2f);
        Vector3 center = new Vector3((cols - 1) * cell * 0.5f, tallest * 0.4f, (rows - 1) * cell * 0.5f);
        Quaternion tilt = Quaternion.Euler(22f, 0f, 0f);
        camGo.transform.rotation = tilt;
        camGo.transform.position = center - tilt * Vector3.forward * dist;
    }

    private static Bounds Measure(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool has = false;
        foreach (Renderer r in rs)
        {
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
