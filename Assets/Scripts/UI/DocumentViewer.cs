using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Full-screen reader for papers and notes (images from Resources). Builds its own UI the first time it is used,
/// in the same look as the HUD cards.
///
/// The paper opens already large enough to read; the mouse wheel (or W / S, arrows) scrolls down it.
///   E / Left click - next page, and close on the last page      A / D or arrows - turn pages      Esc or Q - close
/// While open the player cannot walk or look.
/// </summary>
public class DocumentViewer : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    /// <summary>Frame the viewer last closed on - other scripts ignore input on that frame so the closing press does nothing else.</summary>
    public static int ClosedFrame { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOpen = false;
        ClosedFrame = -1;
        instance = null;
    }

    private static DocumentViewer instance;

    // Same palette as HUDController.
    private static readonly Color Warm = new Color(0.95f, 0.91f, 0.82f);
    private static readonly Color Dim = new Color(0.55f, 0.53f, 0.47f);
    private static readonly Color CardBg = new Color(0.07f, 0.07f, 0.08f, 0.92f);
    private static readonly Color BoxFill = new Color(0.07f, 0.07f, 0.08f, 1f);
    private static readonly Color Accent = new Color(0.93f, 0.70f, 0.30f);

    private const float ViewHeight = 980f;     // reference units (canvas is 1920x1080)
    private const float PageWidth = 1000f;     // the paper is shown this wide, so the handwriting is readable
    private const float WheelStep = 120f;
    private const float KeyScrollSpeed = 900f;
    private const float PageCentreX = 150f;    // paper sits right of centre, the card on the left

    private TMP_FontAsset font;
    private GameObject root;
    private RectTransform viewport;
    private RectTransform page;
    private RawImage pageImage;
    private RectTransform card;
    private RectTransform cardContent;
    private RectTransform thumb;
    private RectTransform track;

    private string title;
    private Texture2D[] pages;
    private int index;
    private float scroll;
    private float scrollTarget;
    private int openedFrame;
    private MonoBehaviour player;

    public static void Show(string title, Texture2D[] pages)
    {
        if (pages == null || pages.Length == 0) return;
        if (instance == null) instance = Create();
        instance.Open(title, pages);
    }

    private static DocumentViewer Create()
    {
        GameObject go = new GameObject("DocumentViewer");
        if (GameSystems.HasInstance) go.transform.SetParent(GameSystems.Instance.transform, false);
        DocumentViewer v = go.AddComponent<DocumentViewer>();
        v.Build();
        return v;
    }

    // -------------------------------------------------------------- build --

    private void Build()
    {
        font = GameSystems.HasInstance && GameSystems.Instance.HUD != null ? GameSystems.Instance.HUD.Font : TMP_Settings.defaultFontAsset;

        GameObject canvasGo = new GameObject("ViewerCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 840;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);
        Fill((RectTransform)root.transform);
        Image dim = NewImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.78f));
        Fill(dim.rectTransform);

        // the paper
        GameObject vp = new GameObject("Viewport", typeof(RectTransform));
        vp.transform.SetParent(root.transform, false);
        viewport = (RectTransform)vp.transform;
        viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = new Vector2(PageCentreX, 0f);
        viewport.sizeDelta = new Vector2(PageWidth, ViewHeight);
        vp.AddComponent<RectMask2D>();

        GameObject pg = new GameObject("Page", typeof(RectTransform));
        pg.transform.SetParent(viewport, false);
        page = (RectTransform)pg.transform;
        page.anchorMin = page.anchorMax = new Vector2(0.5f, 1f);
        page.pivot = new Vector2(0.5f, 1f);
        pageImage = pg.AddComponent<RawImage>();
        pageImage.raycastTarget = false;

        // thin scroll track on the paper's right side
        Image tr = NewImage("Track", root.transform, new Color(1f, 1f, 1f, 0.12f));
        track = tr.rectTransform;
        track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.5f);
        track.pivot = new Vector2(0f, 0.5f);
        track.sizeDelta = new Vector2(5f, ViewHeight);
        track.anchoredPosition = new Vector2(PageCentreX + PageWidth * 0.5f + 18f, 0f);
        Image th = NewImage("Thumb", track, Accent);
        thumb = th.rectTransform;
        thumb.anchorMin = new Vector2(0f, 1f);
        thumb.anchorMax = new Vector2(1f, 1f);
        thumb.pivot = new Vector2(0.5f, 1f);

        // the left card (same look as the HUD cards)
        Image bg = NewImage("Card", root.transform, CardBg);
        card = bg.rectTransform;
        card.anchorMin = card.anchorMax = new Vector2(0f, 0.5f);
        card.pivot = new Vector2(0f, 0.5f);
        card.anchoredPosition = new Vector2(90f, 0f);
        Image edge = NewImage("TopEdge", card, Accent);
        RectTransform er = edge.rectTransform;
        er.anchorMin = new Vector2(0f, 1f);
        er.anchorMax = new Vector2(1f, 1f);
        er.pivot = new Vector2(0.5f, 1f);
        er.anchoredPosition = Vector2.zero;
        er.sizeDelta = new Vector2(0f, 3f);

        GameObject cc = new GameObject("Content", typeof(RectTransform));
        cc.transform.SetParent(card, false);
        cardContent = (RectTransform)cc.transform;
        Fill(cardContent);

        root.SetActive(false);
    }

    private void RebuildCard()
    {
        for (int i = cardContent.childCount - 1; i >= 0; i--) Destroy(cardContent.GetChild(i).gameObject);

        float y = -18f;
        TMP_Text label = NewText("Heading", cardContent, 17, TextAlignmentOptions.TopLeft, Dim);
        Anchor(label.rectTransform, 18f, y, 300f, 22f);
        label.text = "READING";
        y -= 26f;

        TMP_Text name = NewText("Title", cardContent, 32, TextAlignmentOptions.TopLeft, Warm);
        Anchor(name.rectTransform, 18f, y, 300f, 44f);
        name.text = title;
        y -= 46f;

        if (pages.Length > 1)
        {
            TMP_Text count = NewText("Count", cardContent, 22, TextAlignmentOptions.TopLeft, Dim);
            Anchor(count.rectTransform, 18f, y, 300f, 28f);
            count.text = "Page " + (index + 1) + " of " + pages.Length;
            y -= 32f;
        }
        y -= 12f;

        AddRow(ref y, new[] { "Wheel" }, "Scroll down");
        if (pages.Length > 1) AddRow(ref y, new[] { "A", "D" }, "Turn page");
        AddRow(ref y, new[] { "E" }, index < pages.Length - 1 ? "Next page" : "Close");
        AddRow(ref y, new[] { "Esc" }, "Close");

        card.sizeDelta = new Vector2(340f, -y + 18f);
    }

    private void AddRow(ref float y, string[] keys, string text)
    {
        float x = 18f;
        foreach (string key in keys)
        {
            float w = key.Length > 1 ? 74f : 36f;
            Image outer = NewImage("Key", cardContent, Accent);
            Anchor(outer.rectTransform, x, y, w, 36f);
            Image inner = NewImage("Fill", outer.transform, BoxFill);
            Fill(inner.rectTransform);
            inner.rectTransform.offsetMin = new Vector2(2f, 2f);
            inner.rectTransform.offsetMax = new Vector2(-2f, -2f);
            TMP_Text t = NewText("Label", outer.transform, key.Length > 1 ? 19 : 22, TextAlignmentOptions.Center, Accent);
            Fill(t.rectTransform);
            t.text = key;
            x += w + 8f;
        }

        TMP_Text desc = NewText("Action", cardContent, 24, TextAlignmentOptions.MidlineLeft, Warm);
        Anchor(desc.rectTransform, x + 8f, y, 340f - x - 24f, 36f);
        desc.text = text;
        y -= 50f;
    }

    private static void Anchor(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private TMP_Text NewText(string name, Transform parent, float size, TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------ runtime --

    private void Open(string docTitle, Texture2D[] docPages)
    {
        title = docTitle;
        pages = docPages;
        index = 0;
        openedFrame = Time.frameCount;

        player = PauseMenu.FindPlayerController();
        if (player != null) player.enabled = false;

        IsOpen = true;
        root.SetActive(true);
        ShowPage();
    }

    private void Close()
    {
        IsOpen = false;
        ClosedFrame = Time.frameCount;
        root.SetActive(false);

        PauseMenu.ClearStaleInput();
        if (player != null) player.enabled = true;
        player = null;
    }

    private void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame || PauseMenu.IsPaused) return;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (GameInput.PausePressed || (kb != null && kb.qKey.wasPressedThisFrame)) { Close(); return; }

        if (GameInput.InteractPressed)
        {
            if (index < pages.Length - 1) { index++; ShowPage(); }
            else { Close(); return; }
        }

        if (kb != null)
        {
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Turn(1);
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Turn(-1);

            float dir = 0f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir -= 1f;
            scrollTarget += dir * KeyScrollSpeed * Time.unscaledDeltaTime;
        }

        if (mouse != null)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f) scrollTarget -= Mathf.Sign(wheel) * WheelStep;
        }

        float overflow = Mathf.Max(0f, page.sizeDelta.y - ViewHeight);
        scrollTarget = Mathf.Clamp(scrollTarget, 0f, overflow);
        scroll = Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
        page.anchoredPosition = new Vector2(0f, scroll);

        bool scrollable = overflow > 1f;
        track.gameObject.SetActive(scrollable);
        if (scrollable)
        {
            float thumbH = Mathf.Max(60f, ViewHeight * ViewHeight / page.sizeDelta.y);
            thumb.sizeDelta = new Vector2(0f, thumbH);
            thumb.anchoredPosition = new Vector2(0f, -(ViewHeight - thumbH) * (scroll / overflow));
        }
    }

    private void Turn(int d)
    {
        int n = Mathf.Clamp(index + d, 0, pages.Length - 1);
        if (n == index) return;
        index = n;
        ShowPage();
    }

    private void ShowPage()
    {
        Texture2D tex = pages[index];
        pageImage.texture = tex;

        float aspect = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 0.707f;
        page.sizeDelta = new Vector2(PageWidth, PageWidth / aspect);
        page.anchoredPosition = Vector2.zero;
        scroll = scrollTarget = 0f;
        RebuildCard();
    }

    private void OnDestroy()
    {
        if (IsOpen)
        {
            IsOpen = false;
            if (player != null) player.enabled = true;
        }
    }
}
