using System.Collections;
using RTLTMPro;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Everything phone-shaped, drawn in UI: the opening text conversation (typing bubbles and all), the
/// WASD / Shift hint that follows, and the "message sent" toast when Nabil texts Malak on leaving the
/// staff room. ChatView below is the conversation list, shared with the pause-menu Messages tab.
/// Lives on the persistent GameSystems object.
/// </summary>
[DisallowMultipleComponent]
public class PhoneUI : MonoBehaviour
{
    public const string CorridorScene = "HotelFloor_Corridor";
    public const string StaffScene = "StaffRoom";

    /// <summary>True while the intro owns the player (no walking, no pausing, no interacting).</summary>
    public static bool Busy { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Busy = false; rounded = null; circle = null; }

    public static readonly Color Warm = new Color(0.95f, 0.91f, 0.82f);
    public static readonly Color Dim = new Color(0.55f, 0.53f, 0.47f);
    public static readonly Color CardBg = new Color(0.07f, 0.07f, 0.08f, 0.82f);
    public static readonly Color BoxFill = new Color(0.12f, 0.12f, 0.13f, 1f);
    public static readonly Color Accent = new Color(0.93f, 0.70f, 0.30f);

    private TMP_FontAsset font;
    private Transform root;

    // ------------------------------------------------------------- helpers --

    private static Sprite rounded, circle;

    /// <summary>Rounded rectangle, 9-sliced (use Image.Type.Sliced).</summary>
    public static Sprite Rounded { get { if (rounded == null) rounded = MakeSprite(24); return rounded; } }

    /// <summary>Filled circle.</summary>
    public static Sprite Circle { get { if (circle == null) circle = MakeSprite(32); return circle; } }

    private static Sprite MakeSprite(int r)
    {
        const int S = 64;
        Texture2D t = new Texture2D(S, S, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, S - r), cy = Mathf.Clamp(y + 0.5f, r, S - r);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f));
            }
        t.SetPixels32(px);
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    public static bool HasArabic(string s)
    {
        if (s == null) return false;
        foreach (char c in s) if (c >= 0x0600 && c <= 0x06FF) return true;
        return false;
    }

    public static Image Img(string name, Transform parent, Color color, bool roundedShape = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (roundedShape) { image.sprite = Rounded; image.type = Image.Type.Sliced; }
        return image;
    }

    /// <summary>Text label; switches to RTL Text Mesh Pro when the text contains Arabic.</summary>
    public static TMP_Text Txt(Transform parent, TMP_FontAsset font, string name, float size,
                               TextAlignmentOptions align, Color color, string text)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TMP_Text t;
        if (HasArabic(text))
        {
            RTLTextMeshPro r = go.AddComponent<RTLTextMeshPro>();
            r.Farsi = false;
            t = r;
        }
        else t = go.AddComponent<TextMeshProUGUI>();

        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (text != null) t.text = text;
        return t;
    }

    public static RectTransform Place(Component c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = (RectTransform)c.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static void Fill(Component c, float inset = 0f)
    {
        RectTransform rt = (RectTransform)c.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>Vertical scroll list (mouse wheel) whose rows are stacked by a layout group.</summary>
    public static ScrollRect NewScroll(Transform parent, out RectTransform content, float spacing, int pad)
    {
        GameObject go = new GameObject("Scroll", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Fill(go.transform);
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        ScrollRect sr = go.AddComponent<ScrollRect>();

        GameObject vp = new GameObject("Viewport", typeof(RectTransform));
        vp.transform.SetParent(go.transform, false);
        Fill(vp.transform);
        vp.AddComponent<RectMask2D>();

        GameObject c = new GameObject("Content", typeof(RectTransform));
        c.transform.SetParent(vp.transform, false);
        content = (RectTransform)c.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = c.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.padding = new RectOffset(pad, pad, pad, pad);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = (RectTransform)vp.transform;
        sr.content = content;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;
        return sr;
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    // --------------------------------------------------------------- setup --

    public void Build(TMP_FontAsset fontAsset)
    {
        font = fontAsset;

        GameObject go = new GameObject("PhoneCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 845;   // above HUD, below pause menu and fader
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = go.transform;

        SceneManager.sceneLoaded += OnSceneLoaded;

        // The game starts in the corridor; later sessions skip the intro.
        if (!GameState.IntroDone && SceneManager.GetActiveScene().name == CorridorScene)
        {
            Busy = true;
            StartCoroutine(Intro());
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Busy = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Back in the corridor after the staff room: Nabil texts Malak.
        if (scene.name == CorridorScene && GameState.HasVisited(StaffScene) && !GameState.MalakContacted)
            StartCoroutine(TextMalak());
    }

    // --------------------------------------------------------------- intro --

    private IEnumerator Intro()
    {
        yield return Wait(1.0f);   // let the fade-in finish

        MonoBehaviour ctrl = PauseMenu.FindPlayerController();
        if (ctrl != null) ctrl.enabled = false;

        Image dim = Img("Dim", root, new Color(0f, 0f, 0f, 0f));
        Fill(dim);
        RectTransform phone;
        ChatView chat;
        BuildPhone(dim.transform, out phone, out chat);

        yield return Slide(phone, dim, true);
        yield return Wait(0.7f);

        yield return Receive(chat, "Manager", "نبيل", 1.1f);
        yield return Receive(chat, "Manager", "نضف كل الغرف ما عدا 7 علشان ملك نضفتها خلاص", 2.0f);
        yield return Wait(1.1f);
        yield return Send(chat, "Manager", "تمام يا ريس", 1.2f);
        yield return Wait(1.4f);

        yield return Slide(phone, dim, false);
        Destroy(dim.gameObject);

        PauseMenu.ClearStaleInput();
        if (ctrl != null) ctrl.enabled = true;
        GameState.IntroDone = true;
        Busy = false;

        yield return ControlsHint();
    }

    private IEnumerator Receive(ChatView chat, string contact, string text, float typingSeconds)
    {
        GameObject typing = chat.AddTyping(false);
        yield return Wait(typingSeconds);
        typing.SetActive(false);
        Destroy(typing);
        chat.AddBubble(text, false);
        GameState.AddMessage(contact, text, false);
        Sfx.Play(SfxKind.Ping);
    }

    private IEnumerator Send(ChatView chat, string contact, string text, float typingSeconds)
    {
        GameObject typing = chat.AddTyping(true);
        yield return Wait(typingSeconds);
        typing.SetActive(false);
        Destroy(typing);
        chat.AddBubble(text, true);
        GameState.AddMessage(contact, text, true);
        Sfx.Play(SfxKind.Send);
    }

    private static IEnumerator Slide(RectTransform phone, Image dim, bool show)
    {
        const float Dur = 0.7f;
        for (float t = 0f; t < Dur; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / Dur);
            if (!show) k = 1f - k;
            phone.anchoredPosition = new Vector2(0f, Mathf.Lerp(-1200f, 0f, k));
            dim.color = new Color(0f, 0f, 0f, 0.6f * k);
            yield return null;
        }
        phone.anchoredPosition = new Vector2(0f, show ? 0f : -1200f);
        dim.color = new Color(0f, 0f, 0f, show ? 0.6f : 0f);
    }

    private void BuildPhone(Transform parent, out RectTransform phone, out ChatView chat)
    {
        Image rim = Img("Phone", parent, new Color(0.22f, 0.22f, 0.25f), true);
        phone = Place(rim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1200f), new Vector2(540f, 960f));

        Fill(Img("Body", phone, new Color(0.04f, 0.04f, 0.05f), true), 6f);
        Image screen = Img("Screen", phone, new Color(0.085f, 0.085f, 0.095f), true);
        Fill(screen, 18f);
        Place(Img("Notch", screen.transform, Color.black, true), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(130f, 28f));

        Place(Txt(screen.transform, font, "Time", 20, TextAlignmentOptions.Left, Warm, "02:47"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -12f), new Vector2(120f, 30f));
        Place(Img("Battery", screen.transform, Warm, true), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -17f), new Vector2(34f, 16f));

        Image header = Img("Header", screen.transform, new Color(0.13f, 0.13f, 0.15f));
        RectTransform hr = Place(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -56f), new Vector2(0f, 96f));
        hr.anchorMax = new Vector2(1f, 1f);
        hr.sizeDelta = new Vector2(0f, 96f);

        Image avatar = Img("Avatar", header.transform, Accent);
        avatar.sprite = Circle;
        Place(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(60f, 60f));
        Fill(Txt(avatar.transform, font, "Initial", 30, TextAlignmentOptions.Center, new Color(0.1f, 0.08f, 0.05f), "M"));
        Place(Txt(header.transform, font, "Name", 30, TextAlignmentOptions.Left, Warm, "Manager"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, 12f), new Vector2(300f, 38f));
        Place(Txt(header.transform, font, "Status", 19, TextAlignmentOptions.Left, new Color(0.45f, 0.8f, 0.5f), "online"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, -18f), new Vector2(300f, 26f));

        Image bar = Img("InputBar", screen.transform, new Color(0.11f, 0.11f, 0.13f));
        RectTransform br = Place(bar, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 90f));
        br.anchorMax = new Vector2(1f, 0f);
        br.sizeDelta = new Vector2(0f, 90f);
        Image field = Img("Field", bar.transform, new Color(0.17f, 0.17f, 0.19f), true);
        Place(field, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(370f, 54f));
        Place(Txt(field.transform, font, "Hint", 22, TextAlignmentOptions.Left, Dim, "Message"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(300f, 30f));
        Image send = Img("Send", bar.transform, Accent);
        send.sprite = Circle;
        Place(send, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(54f, 54f));

        chat = ChatView.Create(screen.transform, font, 330f);
        RectTransform cr = chat.Root;
        cr.anchorMin = Vector2.zero;
        cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(0f, 90f);
        cr.offsetMax = new Vector2(0f, -152f);
        // Header and input bar must draw over the scrolling list.
        header.transform.SetAsLastSibling();
        bar.transform.SetAsLastSibling();
    }

    // ------------------------------------------------------ controls hint --

    private IEnumerator ControlsHint()
    {
        Image card = Img("ControlsHint", root, CardBg);
        Place(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(900f, 112f));
        Image edge = Img("TopEdge", card.transform, Accent);
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Place(edge, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 3f));
        CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        HorizontalLayoutGroup row = card.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 12f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;

        foreach (string k in new[] { "W", "A", "S", "D" }) Chip(card.transform, k, 64f);
        Label(card.transform, "Walk", 120f);
        Label(card.transform, " ", 40f);
        Chip(card.transform, "Left Shift", 190f);
        Label(card.transform, "Run", 100f);

        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.5f; yield return null; }
        group.alpha = 1f;
        yield return Wait(8f);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t; yield return null; }
        Destroy(card.gameObject);
    }

    private void Chip(Transform parent, string key, float width)
    {
        Image outer = Img("Key", parent, Accent);
        LayoutElement le = outer.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 64f;
        Image inner = Img("Fill", outer.transform, BoxFill);
        Fill(inner, 2f);
        Fill(Txt(inner.transform, font, "Label", key.Length > 1 ? 26 : 32, TextAlignmentOptions.Center, Warm, key));
    }

    private void Label(Transform parent, string text, float width)
    {
        TMP_Text t = Txt(parent, font, "Label", 30, TextAlignmentOptions.Left, Warm, text);
        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 64f;
    }

    // --------------------------------------------------------- Malak / toast --

    private IEnumerator TextMalak()
    {
        yield return Wait(2.5f);
        if (GameState.MalakContacted) yield break;

        GameState.MalakContacted = true;
        GameState.AddMessage("Malak", "ملك، إنتي فين؟", true);   // placeholder wording
        Sfx.Play(SfxKind.Send);
        yield return Toast("Message sent to Malak", "ملك، إنتي فين؟");
    }

    private IEnumerator Toast(string title, string line)
    {
        Image card = Img("Toast", root, CardBg);
        Place(card, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(420f, 96f));
        Place(Img("LeftEdge", card.transform, Accent), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 96f));
        CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
        Place(Txt(card.transform, font, "Title", 22, TextAlignmentOptions.Left, Accent, title), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(380f, 30f));
        Place(Txt(card.transform, font, "Line", 26, HasArabic(line) ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, Warm, line), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -46f), new Vector2(380f, 36f));

        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.4f; yield return null; }
        group.alpha = 1f;
        yield return Wait(3.5f);
        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.8f; yield return null; }
        Destroy(card.gameObject);
    }
}

/// <summary>A scrolling list of chat bubbles (mine on the right, theirs on the left) plus typing bubbles.</summary>
public class ChatView
{
    private static readonly Color Mine = new Color(0.93f, 0.70f, 0.30f);
    private static readonly Color MineText = new Color(0.10f, 0.08f, 0.05f);
    private static readonly Color Theirs = new Color(0.20f, 0.20f, 0.22f);

    private TMP_FontAsset font;
    private ScrollRect scroll;
    private RectTransform content;
    private float maxText;

    public RectTransform Root { get; private set; }

    public static ChatView Create(Transform parent, TMP_FontAsset font, float maxTextWidth)
    {
        ChatView v = new ChatView();
        v.font = font;
        v.maxText = maxTextWidth;
        v.scroll = PhoneUI.NewScroll(parent, out v.content, 10f, 16);
        v.Root = (RectTransform)v.scroll.transform;
        return v;
    }

    public void Clear()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject g = content.GetChild(i).gameObject;
            g.SetActive(false);
            Object.Destroy(g);
        }
    }

    public GameObject AddBubble(string text, bool mine)
    {
        GameObject row = NewRow();
        Image bubble = PhoneUI.Img("Bubble", row.transform, mine ? Mine : Theirs, true);

        TextAlignmentOptions align = PhoneUI.HasArabic(text) ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        TMP_Text label = PhoneUI.Txt(bubble.transform, font, "Text", 26, align, mine ? MineText : PhoneUI.Warm, text);
        Vector2 pref = label.GetPreferredValues(maxText, 0f);
        float w = Mathf.Min(Mathf.Max(pref.x, 24f) + 2f, maxText);
        float h = pref.y;

        Vector2 anchor = new Vector2(mine ? 1f : 0f, 1f);
        PhoneUI.Place(bubble, anchor, anchor, Vector2.zero, new Vector2(w + 36f, h + 24f));
        RectTransform trt = label.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(18f, 12f);
        trt.offsetMax = new Vector2(-18f, -12f);

        row.GetComponent<LayoutElement>().preferredHeight = h + 24f;
        ScrollToEnd();
        return row;
    }

    public GameObject AddTyping(bool mine)
    {
        GameObject row = NewRow();
        Image bubble = PhoneUI.Img("Typing", row.transform, mine ? Mine : Theirs, true);
        Vector2 anchor = new Vector2(mine ? 1f : 0f, 1f);
        PhoneUI.Place(bubble, anchor, anchor, Vector2.zero, new Vector2(88f, 48f));
        row.GetComponent<LayoutElement>().preferredHeight = 48f;

        TypingDots dots = bubble.gameObject.AddComponent<TypingDots>();
        dots.dots = new Image[3];
        Color c = mine ? MineText : PhoneUI.Warm;
        for (int i = 0; i < 3; i++)
        {
            Image d = PhoneUI.Img("Dot" + i, bubble.transform, c);
            d.sprite = PhoneUI.Circle;
            PhoneUI.Place(d, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 22f, 0f), new Vector2(12f, 12f));
            dots.dots[i] = d;
        }
        ScrollToEnd();
        return row;
    }

    private GameObject NewRow()
    {
        GameObject row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(content, false);
        row.AddComponent<LayoutElement>();
        return row;
    }

    public void ScrollToEnd()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition = 0f;
    }
}

/// <summary>The pulsing three dots of a "typing..." bubble.</summary>
public class TypingDots : MonoBehaviour
{
    public Image[] dots;

    private void Update()
    {
        for (int i = 0; i < dots.Length; i++)
        {
            Color c = dots[i].color;
            c.a = 0.3f + 0.7f * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 6f - i * 0.9f));
            dots[i].color = c;
        }
    }
}
