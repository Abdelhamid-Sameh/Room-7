using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The six-digit keypad of the safe. Click the keys or type the digits (numpad works too); Backspace deletes,
/// Esc or Q walks away. Builds its own UI on first use, in the same look as the other cards.
/// The caller decides what a code means: validate returns true for a right code (the keypad closes and
/// onSuccess runs), false for a wrong one (the display shakes and clears).
/// </summary>
public class SafeKeypad : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static int ClosedFrame { get; private set; } = -1;
    private static SafeKeypad instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { IsOpen = false; ClosedFrame = -1; instance = null; }

    private const int Digits = 6;
    private static readonly Color Warm = new Color(0.95f, 0.91f, 0.82f);
    private static readonly Color Dim = new Color(0.55f, 0.53f, 0.47f);
    private static readonly Color CardBg = new Color(0.07f, 0.07f, 0.08f, 0.94f);
    private static readonly Color Accent = new Color(0.93f, 0.70f, 0.30f);
    private static readonly Color Bad = new Color(0.85f, 0.25f, 0.2f);

    private TMP_FontAsset font;
    private GameObject root;
    private RectTransform card;
    private TMP_Text display;
    private string entered = "";
    private Func<string, bool> validate;
    private Action onSuccess;
    private MonoBehaviour player;
    private int openedFrame;
    private bool locked;

    public static void Show(Func<string, bool> validate, Action onSuccess)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("SafeKeypad");
            if (GameSystems.HasInstance) go.transform.SetParent(GameSystems.Instance.transform, false);
            instance = go.AddComponent<SafeKeypad>();
            instance.Build();
        }
        instance.Open(validate, onSuccess);
    }

    private void Build()
    {
        font = GameSystems.HasInstance && GameSystems.Instance.HUD != null ? GameSystems.Instance.HUD.Font : TMP_Settings.defaultFontAsset;

        GameObject canvasGo = new GameObject("KeypadCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 842;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);
        Fill((RectTransform)root.transform);

        card = Img("Card", root.transform, CardBg).rectTransform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(420f, 640f);
        card.anchoredPosition = new Vector2(480f, 0f);   // to the right, so the safe stays visible

        RectTransform edge = Img("Edge", card, Accent).rectTransform;
        edge.anchorMin = new Vector2(0f, 1f); edge.anchorMax = Vector2.one; edge.pivot = new Vector2(0.5f, 1f);
        edge.sizeDelta = new Vector2(0f, 4f);

        TMP_Text title = Txt("Title", card, 26, Dim);
        Place(title.rectTransform, new Vector2(0f, -22f), new Vector2(380f, 34f));
        title.text = "SAFE";

        RectTransform well = Img("Display", card, new Color(0.02f, 0.03f, 0.02f, 1f)).rectTransform;
        Place(well, new Vector2(0f, -66f), new Vector2(360f, 76f));
        display = Txt("Digits", well, 46, new Color(0.55f, 0.95f, 0.55f));
        Fill(display.rectTransform);

        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "<" };
        for (int i = 0; i < keys.Length; i++)
        {
            string k = keys[i];
            int col = i % 3, row = i / 3;
            Button b = MakeButton(card, k, new Vector2((col - 1) * 112f, -168f - row * 100f), new Vector2(96f, 84f));
            b.onClick.AddListener(() => Press(k));
        }

        TMP_Text hint = Txt("Hint", card, 20, Dim);
        Place(hint.rectTransform, new Vector2(0f, -590f), new Vector2(380f, 28f));
        hint.text = "Type or click   |   Esc - step back";

        root.SetActive(false);
    }

    private void Open(Func<string, bool> check, Action success)
    {
        validate = check; onSuccess = success;
        entered = ""; locked = false;
        Refresh(false);

        player = PauseMenu.FindPlayerController();
        if (player != null) player.enabled = false;
        IsOpen = true;
        openedFrame = Time.frameCount;
        root.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Close()
    {
        IsOpen = false;
        ClosedFrame = Time.frameCount;
        root.SetActive(false);
        PauseMenu.ClearStaleInput();
        if (player != null) player.enabled = true;
        player = null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!IsOpen || PauseMenu.IsPaused) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Time.frameCount == openedFrame || locked) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) { Close(); return; }
        if (kb.backspaceKey.wasPressedThisFrame) Press("<");

        Key[] top = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        Key[] pad = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };
        for (int d = 0; d < 10; d++)
            if (kb[top[d]].wasPressedThisFrame || kb[pad[d]].wasPressedThisFrame) Press(d.ToString());
    }

    private void Press(string key)
    {
        if (locked || !IsOpen) return;
        Sfx.Play(SfxKind.Keypad);

        if (key == "C") entered = "";
        else if (key == "<") { if (entered.Length > 0) entered = entered.Substring(0, entered.Length - 1); }
        else if (entered.Length < Digits) entered += key;
        Refresh(false);

        if (entered.Length == Digits) StartCoroutine(Submit());
    }

    private IEnumerator Submit()
    {
        locked = true;
        yield return new WaitForSecondsRealtime(0.35f);
        if (validate(entered))
        {
            Close();
            onSuccess();
            yield break;
        }

        Refresh(true);
        Vector2 home = card.anchoredPosition;
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            card.anchoredPosition = home + new Vector2(Mathf.Sin(t * 60f) * 12f * (1f - t / 0.4f), 0f);
            yield return null;
        }
        card.anchoredPosition = home;
        entered = "";
        Refresh(false);
        locked = false;
    }

    private void Refresh(bool bad)
    {
        string s = "";
        for (int i = 0; i < Digits; i++) s += (i < entered.Length ? entered[i].ToString() : "-") + (i < Digits - 1 ? " " : "");
        display.text = s;
        display.color = bad ? Bad : new Color(0.55f, 0.95f, 0.55f);
    }

    private void OnDestroy()
    {
        if (IsOpen) { IsOpen = false; if (player != null) player.enabled = true; }
    }

    // ---- small UI helpers ----
    private Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size)
    {
        Image bg = Img(label + "Key", parent, Color.white);
        bg.raycastTarget = true;
        Place(bg.rectTransform, pos + new Vector2(0f, 0f), size, true);
        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        ColorBlock c = b.colors;
        c.normalColor = new Color(0.16f, 0.16f, 0.17f, 1f);
        c.highlightedColor = new Color(0.30f, 0.25f, 0.15f, 1f);
        c.selectedColor = c.normalColor;
        c.pressedColor = Accent;
        c.fadeDuration = 0.06f;
        b.colors = c;
        TMP_Text t = Txt("Label", bg.transform, 36, Warm);
        Fill(t.rectTransform);
        t.text = label;
        return b;
    }

    private static Image Img(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image im = go.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        return im;
    }

    private TMP_Text Txt(string name, Transform parent, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size, bool centreAnchored = false)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
