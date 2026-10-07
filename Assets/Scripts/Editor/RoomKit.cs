using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helpers for building guest rooms in code: tiled materials sized to each surface, boxes,
/// prefab placement with fitted colliders, and the small procedurally generated assets the rooms use
/// (tidy marker sprite, mirror smudge, bathroom tile, crumpled-paper meshes, handwriting paper).
/// </summary>
public static class RoomKit
{
    public const string MaterialDir = "Assets/Materials/RoomSeven/Rooms/";
    public const string GeneratedTexDir = "Assets/Textures/Generated/";
    public const string GeneratedMeshDir = "Assets/Models/Generated/";

    // ------------------------------------------------------------------ materials --

    /// <summary>
    /// A URP Lit material whose texture repeats once per 'tileMeters', for a surface that is uMeters x vMeters.
    /// One asset per distinct size, so the pattern is the same density on every wall.
    /// </summary>
    public static Material Tiled(string name, string texturePath, Color tint, float uMeters, float vMeters,
                                 float tileMeters, float smoothness = 0.1f)
    {
        System.IO.Directory.CreateDirectory(MaterialDir);
        string path = MaterialDir + name + "_" + uMeters.ToString("0.0") + "x" + vMeters.ToString("0.0") + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", new Vector2(uMeters / tileMeters, vMeters / tileMeters));
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);

        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>A plain colour material (created once, reused).</summary>
    public static Material Plain(string name, Color color, float smoothness = 0.3f, float metallic = 0f, Color? emission = null)
    {
        System.IO.Directory.CreateDirectory(MaterialDir);
        string path = MaterialDir + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Alpha-blended unlit material for decals such as smudges (fades by changing _BaseColor alpha).</summary>
    public static Material Decal(string name, Texture2D tex)
    {
        System.IO.Directory.CreateDirectory(MaterialDir);
        string path = MaterialDir + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);                 // transparent
        m.SetFloat("_Blend", 0f);                   // alpha
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;

        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------- objects --

    public static GameObject Group(string name, Transform parent)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g;
    }

    public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    public static GameObject Quad(string name, Transform parent, Vector3 center, Quaternion rotation, Vector2 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(center, rotation);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>Instantiates a prefab/model, scales it, turns it to 'yaw' and puts its PIVOT at 'position' (these packs have bottom-centre pivots).</summary>
    public static GameObject Prefab(string path, Transform parent, Vector3 position, float yaw, float scale, string name = null)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogError("[RoomKit] missing " + path); return null; }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = name ?? asset.name;
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    public static Bounds WorldBounds(GameObject go)
    {
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool has = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is SpriteRenderer || r is ParticleSystemRenderer) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    /// <summary>Adds (or refits) one BoxCollider that covers every renderer, measured in the object's own space so rotated pieces get a tight box.</summary>
    public static BoxCollider FitCollider(GameObject go, bool trigger = false)
    {
        BoxCollider col = go.GetComponent<BoxCollider>();
        if (col == null) col = go.AddComponent<BoxCollider>();

        bool has = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is SpriteRenderer) continue;
            Bounds lb;
            if (r is MeshRenderer && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null)
                lb = r.GetComponent<MeshFilter>().sharedMesh.bounds;
            else if (r is SkinnedMeshRenderer) lb = ((SkinnedMeshRenderer)r).localBounds;
            else continue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = go.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!has) { min = max = p; has = true; }
                else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            }
        }

        if (has)
        {
            col.center = (min + max) * 0.5f;
            col.size = max - min;
        }
        col.isTrigger = trigger;
        return col;
    }

    public static void NoShadows(GameObject go)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // ------------------------------------------------------------ generated assets --

    public static Sprite MarkerSprite()
    {
        string path = GeneratedTexDir + "TidyMarker.png";
        if (!System.IO.File.Exists(path))
        {
            System.IO.Directory.CreateDirectory(GeneratedTexDir);
            int n = 64;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            Color amber = new Color(1f, 0.80f, 0.40f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - (n - 1) * 0.5f, dy = y - (n - 1) * 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (n * 0.5f);
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f) * 0.75f;
                    float diamond = Mathf.Clamp01(1f - (Mathf.Abs(dx) + Mathf.Abs(dy)) / 9f);         // small bright core
                    float star = Mathf.Clamp01(1f - Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)) / 1.6f) * Mathf.Clamp01(1f - r) * 0.7f;   // four faint rays
                    float a = Mathf.Clamp01(glow + diamond + star);
                    t.SetPixel(x, y, new Color(amber.r, amber.g, amber.b, a));
                }
            t.Apply();
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsPerUnit = 64f;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static Texture2D SmudgeTexture()
    {
        string path = GeneratedTexDir + "MirrorSmudge.png";
        if (!System.IO.File.Exists(path))
        {
            System.IO.Directory.CreateDirectory(GeneratedTexDir);
            int n = 256;
            var px = new float[n * n];
            var rng = new System.Random(77);
            for (int b = 0; b < 26; b++)
            {
                float cx = (float)rng.NextDouble() * n, cy = (float)rng.NextDouble() * n;
                float rx = 10f + (float)rng.NextDouble() * 38f, ry = 6f + (float)rng.NextDouble() * 20f;
                float rot = (float)rng.NextDouble() * Mathf.PI;               // smears at all angles, like a wiping hand
                float strength = 0.18f + (float)rng.NextDouble() * 0.30f;
                float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float u = (dx * cs + dy * sn) / rx, v = (-dx * sn + dy * cs) / ry;
                        float d = u * u + v * v;
                        if (d < 4f) px[y * n + x] += strength * Mathf.Exp(-d * 1.5f);
                    }
            }
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    t.SetPixel(x, y, new Color(0.88f, 0.90f, 0.92f, Mathf.Clamp01(px[y * n + x])));
            t.Apply();
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static Texture2D TileTexture()
    {
        string path = GeneratedTexDir + "BathroomTile.png";
        if (!System.IO.File.Exists(path))
        {
            System.IO.Directory.CreateDirectory(GeneratedTexDir);
            int n = 256, cell = 128, grout = 5;
            var rng = new System.Random(5);
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int lx = x % cell, ly = y % cell;
                    bool g = lx < grout || ly < grout;
                    float noise = 0.96f + 0.04f * (float)rng.NextDouble();
                    Color baseC = g ? new Color(0.50f, 0.55f, 0.53f) : new Color(0.80f, 0.87f, 0.84f) * noise;
                    // a soft highlight on every tile
                    if (!g) { float hx = (lx - cell * 0.5f) / cell, hy = (ly - cell * 0.5f) / cell; baseC *= 1f - 0.12f * (hx * hx + hy * hy) * 4f; }
                    baseC.a = 1f;
                    t.SetPixel(x, y, baseC);
                }
            t.Apply();
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Handwritten-looking paper. kind 0 = lines of writing, 1 = a column of sums with a total.</summary>
    public static Texture2D PaperTexture(string fileName, int kind, int seed)
    {
        string path = GeneratedTexDir + fileName;
        if (!System.IO.File.Exists(path))
        {
            System.IO.Directory.CreateDirectory(GeneratedTexDir);
            int w = 256, h = 362;
            var px = new Color[w * h];
            var rng = new System.Random(seed);
            for (int i = 0; i < px.Length; i++)
            {
                float n = 0.97f + 0.03f * (float)rng.NextDouble();
                px[i] = new Color(0.94f * n, 0.91f * n, 0.82f * n, 1f);
            }
            Color ink = new Color(0.10f, 0.13f, 0.32f, 1f);
            Color rule = new Color(0.72f, 0.78f, 0.86f, 1f);

            for (int y = 34; y < h - 20; y += 22)           // faint ruled lines
                for (int x = 14; x < w - 14; x++) px[y * w + x] = rule;

            int lines = (h - 60) / 22;
            for (int line = 0; line < lines; line++)
            {
                int y = h - 40 - line * 22;
                if (kind == 0)
                {
                    int x = 20 + rng.Next(0, 6);
                    int end = w - 20 - rng.Next(0, 70);
                    if (line == lines - 1) end = x + 80;                         // a short last line
                    while (x < end)
                    {
                        int len = 6 + rng.Next(0, 18);
                        Squiggle(px, w, h, x, y, Mathf.Min(len, end - x), rng, ink);
                        x += len + 5 + rng.Next(0, 5);
                    }
                }
                else
                {
                    Squiggle(px, w, h, 22, y, 44 + rng.Next(0, 40), rng, ink);                    // what was bought
                    Squiggle(px, w, h, w - 80, y, 30 + rng.Next(0, 14), rng, ink);                // what it cost
                    if (line == lines - 3) for (int x = w - 90; x < w - 18; x++) { px[(y - 8) * w + x] = ink; px[(y - 9) * w + x] = ink; }   // total line
                }
            }

            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void Squiggle(Color[] px, int w, int h, int x0, int y0, int length, System.Random rng, Color ink)
    {
        float phase = (float)rng.NextDouble() * 6.28f;
        float amp = 2f + (float)rng.NextDouble() * 3.5f;
        for (int i = 0; i < length; i++)
        {
            int x = x0 + i;
            int y = y0 + Mathf.RoundToInt(Mathf.Sin(i * 0.9f + phase) * amp);
            for (int t = -1; t <= 0; t++)
            {
                int yy = Mathf.Clamp(y + t, 0, h - 1), xx = Mathf.Clamp(x, 0, w - 1);
                px[yy * w + xx] = ink;
            }
        }
    }

    /// <summary>A lump of crumpled paper: a lightly subdivided sphere with random bumps and flat facets.</summary>
    public static Mesh CrumpledPaperMesh(int variant)
    {
        string path = GeneratedMeshDir + "CrumpledPaper0" + variant + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        System.IO.Directory.CreateDirectory(GeneratedMeshDir);
        var rng = new System.Random(900 + variant * 13);

        // icosahedron
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var verts = new List<Vector3>
        {
            new Vector3(-1,  t, 0), new Vector3( 1,  t, 0), new Vector3(-1, -t, 0), new Vector3( 1, -t, 0),
            new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
            new Vector3( t, 0, -1), new Vector3( t, 0,  1), new Vector3(-t, 0, -1), new Vector3(-t, 0,  1)
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
        var tris = new List<int[]>
        {
            new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
            new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
            new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1}
        };

        // one subdivision
        var mid = new Dictionary<long, int>();
        System.Func<int, int, int> midpoint = (a, b) =>
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            int idx;
            if (mid.TryGetValue(key, out idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            idx = verts.Count - 1;
            mid[key] = idx;
            return idx;
        };
        var subdivided = new List<int[]>();
        foreach (int[] f in tris)
        {
            int a = midpoint(f[0], f[1]), b = midpoint(f[1], f[2]), c = midpoint(f[2], f[0]);
            subdivided.Add(new[]{f[0], a, c}); subdivided.Add(new[]{f[1], b, a}); subdivided.Add(new[]{f[2], c, b}); subdivided.Add(new[]{a, b, c});
        }

        // random radius per vertex = the crumples
        var radius = new float[verts.Count];
        for (int i = 0; i < radius.Length; i++) radius[i] = 0.72f + (float)rng.NextDouble() * 0.38f;

        // unshared vertices per triangle = flat facets
        var outV = new List<Vector3>(); var outT = new List<int>(); var outUV = new List<Vector2>();
        foreach (int[] f in subdivided)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = verts[f[k]] * radius[f[k]];
                outV.Add(p);
                outUV.Add(new Vector2(p.x * 0.5f + 0.5f, p.y * 0.5f + 0.5f));
                outT.Add(outV.Count - 1);
            }
        }

        Mesh mesh = new Mesh { name = "CrumpledPaper0" + variant };
        mesh.SetVertices(outV);
        mesh.SetUVs(0, outUV);
        mesh.SetTriangles(outT, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
