using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Draws the three safe-clue textures (mirror number, cinema ticket, brass key tag) with real fonts by
/// rendering TextMeshPro text through a throw-away orthographic camera. Run Room3ClueArt.Build() from
/// the editor (menu: Room 7 > Room 3 > Rebuild clue art).
/// </summary>
public static class Room3ClueArt
{
    const string Reenie = "Assets/Fonts/Clues/ReenieBeanie.ttf";
    const string Courier = "Assets/Fonts/Clues/CourierPrime-Regular.ttf";
    const string Elite = "Assets/Fonts/Clues/SpecialElite-Regular.ttf";
    const string Oswald = "Assets/TextMesh Pro/Examples & Extras/Fonts/Oswald-Bold.ttf";

    struct Line { public string text, font; public float px; public Color color; public Vector2 at; public float rot; }
    struct Box { public Vector2 at, size; public Color color; }

    static Line L(string text, string font, float px, Color color, float x, float y, float rot)
    {
        Line l = new Line(); l.text = text; l.font = font; l.px = px; l.color = color; l.at = new Vector2(x, y); l.rot = rot; return l;
    }

    static Box B(float x, float y, float w, float h, Color c)
    {
        Box b = new Box(); b.at = new Vector2(x, y); b.size = new Vector2(w, h); b.color = c; return b;
    }

    [MenuItem("Room 7/Room 3/Rebuild clue art")]
    public static string Build()
    {
        // 1. numbers written in the mirror: soft, pale, hand-drawn. Transparent PNG.
        Color[] mirror = Render(512, 256, Color.black, true, new Line[] {
            L("21", Reenie, 230f, new Color(0.93f, 0.95f, 0.93f, 0.92f), 256f, 120f, 5f) }, new Box[0]);
        Save("Assets/Textures/Generated/MirrorNumber.png", mirror, 512, 256, true);

        // 2. cinema ticket stub (the date stamp reads 01/05/02)
        Color paper = new Color(0.90f, 0.85f, 0.70f);
        Color ink = new Color(0.16f, 0.13f, 0.12f);
        List<Box> tb = new List<Box>();
        tb.Add(B(34f, 200f, 68f, 400f, new Color(0.62f, 0.13f, 0.11f)));
        for (int i = 0; i < 20; i++) tb.Add(B(104f, 14f + i * 20f, 4f, 10f, new Color(0.55f, 0.50f, 0.40f)));
        // the date reads 01/05/02: the last two digits (blue) are the clue, the red ones are decoys
        Color red = new Color(0.72f, 0.10f, 0.09f, 1f), blue = new Color(0.10f, 0.26f, 0.82f, 1f);
        float dw = 45f;   // width of one typewriter character
        Color[] ticket = Render(800, 400, paper, false, new Line[] {
            L("ALHAMBRA CINEMA", Courier, 44f, ink, 470f, 338f, 0f),
            L("ADMIT ONE", Courier, 70f, ink, 470f, 268f, 0f),
            L("SCREEN 3    ROW F    SEAT 12", Courier, 30f, ink, 470f, 205f, 0f),
            L("01", Elite, 76f, red, 450f - 3f * dw, 125f, 0f),
            L("/", Elite, 76f, ink, 450f - 1.5f * dw, 125f, 0f),
            L("05", Elite, 76f, red, 450f, 125f, 0f),
            L("/", Elite, 76f, ink, 450f + 1.5f * dw, 125f, 0f),
            L("02", Elite, 76f, blue, 450f + 3f * dw, 125f, 0f),
            L("No. 004718", Courier, 26f, new Color(0.45f, 0.4f, 0.35f), 470f, 48f, 0f) }, tb.ToArray());
        Grain(ticket, 800, 400, 0.05f, 0.012f);
        Save("Assets/Resources/Room3/TicketStub.png", ticket, 800, 400, false);

        // 3. brass hotel key tag
        Color dark = new Color(0.18f, 0.11f, 0.03f);
        Color[] tag = Render(640, 380, new Color(0.74f, 0.58f, 0.22f), false, new Line[] {
            L("GRAND ALEXANDRIA", Oswald, 38f, dark, 320f, 322f, 0f),
            L("08", Oswald, 200f, dark, 320f, 185f, 0f),
            L("RETURN TO RECEPTION", Oswald, 26f, dark, 320f, 58f, 0f) }, new Box[0]);
        Grain(tag, 640, 380, 0.07f, 0.06f);
        Save("Assets/Resources/Room3/KeyTag.png", tag, 640, 380, false);

        AssetDatabase.SaveAssets();
        return "clue art rebuilt";
    }

    static TMP_FontAsset MakeFont(string path)
    {
        Font f = AssetDatabase.LoadAssetAtPath<Font>(path);
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(f, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
        fa.hideFlags = HideFlags.HideAndDontSave;
        return fa;
    }

    static Color[] Render(int w, int h, Color bg, bool luma, Line[] lines, Box[] boxes)
    {
        GameObject root = new GameObject("__ClueArt");
        root.transform.position = new Vector3(0f, 6000f, 0f);

        GameObject camGo = new GameObject("Cam");
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = h / 200f; cam.aspect = w / (float)h;
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = luma ? Color.black : bg;
        cam.enabled = false;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        List<Material> temp = new List<Material>();
        foreach (Box b in boxes)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(root.transform, false);
            q.transform.localPosition = new Vector3((b.at.x - w / 2f) / 100f, (b.at.y - h / 2f) / 100f, 0.5f);
            q.transform.localScale = new Vector3(b.size.x / 100f, b.size.y / 100f, 1f);
            Material m = new Material(unlit); m.SetColor("_BaseColor", b.color); temp.Add(m);
            q.GetComponent<Renderer>().sharedMaterial = m;
        }

        Dictionary<string, TMP_FontAsset> fonts = new Dictionary<string, TMP_FontAsset>();
        foreach (Line l in lines)
        {
            if (!fonts.ContainsKey(l.font)) fonts[l.font] = MakeFont(l.font);
            fonts[l.font].TryAddCharacters(l.text);

            GameObject go = new GameObject("T");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3((l.at.x - w / 2f) / 100f, (l.at.y - h / 2f) / 100f, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, l.rot);
            TextMeshPro t = go.AddComponent<TextMeshPro>();
            t.font = fonts[l.font];
            t.text = l.text;
            t.fontSize = l.px / 10f;
            t.color = luma ? new Color(1f, 1f, 1f, 1f) : l.color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(w / 100f, h / 100f);
            t.ForceMeshUpdate();
        }

        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Color[] px = tex.GetPixels();
        Object.DestroyImmediate(tex);

        Object.DestroyImmediate(root);
        foreach (Material m in temp) Object.DestroyImmediate(m);
        foreach (TMP_FontAsset f in fonts.Values) Object.DestroyImmediate(f, true);

        if (luma)
        {
            Color c = lines.Length > 0 ? lines[0].color : Color.white;
            for (int i = 0; i < px.Length; i++) px[i] = new Color(c.r, c.g, c.b, px[i].r * c.a);
        }
        return px;
    }

    // paper / metal wear: slow blotches plus fine grain, applied to opaque textures
    static void Grain(Color[] px, int w, int h, float blotch, float fine)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = (Mathf.PerlinNoise(x * 0.02f + 3f, y * 0.02f + 9f) - 0.5f) * 2f * blotch
                        + (Mathf.PerlinNoise(x * 0.9f, y * 0.9f) - 0.5f) * 2f * fine;
                Color c = px[y * w + x];
                px[y * w + x] = new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n), 1f);
            }
    }

    static void Save(string path, Color[] px, int w, int h, bool alpha)
    {
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.alphaIsTransparency = alpha; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
    }
}
