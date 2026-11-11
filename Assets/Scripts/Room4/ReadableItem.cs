using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Something Nabil can read that is not an image: an Arabic note (the Narcissus paper) or a phone left on a seat
/// with a text thread. In thread mode each line of 'Body' is one message; a line starting with "&gt;" is Nabil's side
/// (right, amber), anything else is the other person's. Reading it records a clue. Close with E / Q / Space.
/// </summary>
public class ReadableItem : Interactable
{
    public enum Kind { Note, Thread }

    [SerializeField] private Kind kind = Kind.Note;
    [SerializeField] private string title = "Note";
    [SerializeField] private string subtitle = "";
    [SerializeField] private string readPrompt = "Read";
    [TextArea(3, 30)] [SerializeField] private string body = "";
    [SerializeField] private string clueId = "";

    /// <summary>True while a reader is on screen (the pause menu stays shut).</summary>
    public static bool Open { get; private set; }
    public static int ClosedFrame { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Open = false; ClosedFrame = -1; }

    public override string Prompt => readPrompt;

    public override void Interact(PlayerInteraction interactor)
    {
        if (Open || !GameSystems.HasInstance) return;
        if (!string.IsNullOrEmpty(clueId)) GameState.AddClue(clueId);
        GameSystems.Instance.StartCoroutine(Run());
    }

    private System.Collections.IEnumerator Run()
    {
        Open = true;
        TMP_FontAsset font = GameSystems.Instance.HUD != null ? GameSystems.Instance.HUD.Font : null;
        MonoBehaviour ctrl = PauseMenu.FindPlayerController();
        if (ctrl != null) ctrl.enabled = false;

        GameObject go = new GameObject("Reader", typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image dim = PhoneUI.Img("Dim", go.transform, new Color(0f, 0f, 0f, 0.7f));
        PhoneUI.Fill(dim);

        ScrollRect scroll = null;
        if (kind == Kind.Note) BuildNote(dim.transform, font); else scroll = BuildThread(dim.transform, font);

        TMP_Text hint = PhoneUI.Txt(dim.transform, font, "Hint", 22, TextAlignmentOptions.Center, PhoneUI.Dim, kind == Kind.Thread ? "mouse wheel / W S  scroll        E  /  Q   close" : "E  /  Q   close");
        PhoneUI.Place(hint, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(600f, 32f));

        yield return null;
        while (true)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) break;
            if (scroll != null)
            {
                // the reader has no raycaster (the cursor stays locked), so scroll by hand: wheel, W/S or the arrows
                float view = scroll.viewport != null ? scroll.viewport.rect.height : ((RectTransform)scroll.transform).rect.height;
                float extra = Mathf.Max(1f, scroll.content.rect.height - view);
                float step = 0f;
                Mouse ms = Mouse.current;
                if (ms != null) { float wheel = ms.scroll.ReadValue().y; if (Mathf.Abs(wheel) > 0.01f) step += Mathf.Sign(wheel) * 110f; }   // one notch = one bubble, whatever the platform's scroll units
                if (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed)) step += 700f * Time.unscaledDeltaTime;
                if (kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed)) step -= 700f * Time.unscaledDeltaTime;
                if (step != 0f) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + step / extra);
            }
            yield return null;
        }

        Destroy(go);
        ClosedFrame = Time.frameCount;
        PauseMenu.ClearStaleInput();
        if (ctrl != null) ctrl.enabled = true;
        Open = false;
    }

    private void BuildNote(Transform parent, TMP_FontAsset font)
    {
        Image paper = PhoneUI.Img("Paper", parent, new Color(0.89f, 0.83f, 0.68f), true);
        PhoneUI.Place(paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 880f));
        Color ink = new Color(0.16f, 0.12f, 0.09f);
        TMP_Text t = PhoneUI.Txt(paper.transform, font, "Title", 46, TextAlignmentOptions.Center, ink, title);
        PhoneUI.Place(t, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(900f, 70f));
        TMP_Text b = PhoneUI.Txt(paper.transform, font, "Body", 34, TextAlignmentOptions.TopRight, ink, body);
        b.enableAutoSizing = true; b.fontSizeMin = 22f; b.fontSizeMax = 34f;
        b.lineSpacing = 18f;
        PhoneUI.Place(b, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(900f, 720f));
    }

    private ScrollRect BuildThread(Transform parent, TMP_FontAsset font)
    {
        Image rim = PhoneUI.Img("Phone", parent, new Color(0.22f, 0.22f, 0.25f), true);
        PhoneUI.Place(rim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 960f));
        PhoneUI.Fill(PhoneUI.Img("Body", rim.transform, new Color(0.04f, 0.04f, 0.05f), true), 6f);
        Image screen = PhoneUI.Img("Screen", rim.transform, new Color(0.085f, 0.085f, 0.095f), true);
        PhoneUI.Fill(screen, 18f);

        Image header = PhoneUI.Img("Header", screen.transform, new Color(0.13f, 0.13f, 0.15f));
        RectTransform hr = PhoneUI.Place(header, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 96f));
        hr.anchorMax = new Vector2(1f, 1f); hr.sizeDelta = new Vector2(0f, 96f);
        Image avatar = PhoneUI.Img("Avatar", header.transform, PhoneUI.Accent);
        avatar.sprite = PhoneUI.Circle;
        PhoneUI.Place(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(60f, 60f));
        PhoneUI.Fill(PhoneUI.Txt(avatar.transform, font, "Initial", 30, TextAlignmentOptions.Center, new Color(0.1f, 0.08f, 0.05f), title.Substring(0, 1)));
        PhoneUI.Place(PhoneUI.Txt(header.transform, font, "Name", 30, TextAlignmentOptions.Left, PhoneUI.Warm, title), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, 10f), new Vector2(340f, 38f));
        PhoneUI.Place(PhoneUI.Txt(header.transform, font, "Status", 19, TextAlignmentOptions.Left, PhoneUI.Dim, subtitle), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, -18f), new Vector2(340f, 26f));

        ChatView chat = ChatView.Create(screen.transform, font, 330f);
        RectTransform cr = chat.Root;
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(0f, 10f); cr.offsetMax = new Vector2(0f, -100f);
        header.transform.SetAsLastSibling();

        foreach (string raw in body.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            bool mine = line.StartsWith(">");
            chat.AddBubble(mine ? line.Substring(1).Trim() : line, mine);
        }
        chat.ScrollToEnd();
        ScrollRect sr = cr.GetComponent<ScrollRect>();
        sr.verticalNormalizedPosition = 1f;   // start at the first message
        return sr;
    }
}
