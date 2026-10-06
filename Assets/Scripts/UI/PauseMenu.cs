using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Esc (or Start on a gamepad) pauses the game: time and all audio freeze, the player stops moving
/// and looking, the cursor appears, and a small menu offers Resume and Quit. Esc again resumes.
///
/// Lives on the persistent GameSystems object and builds its own UI from code (same look as the
/// HUD cards). Other scripts check PauseMenu.IsPaused to ignore input while the game is paused.
///
/// Later additions (audio sliders bound to AudioLevels, the clue journal from GameState.Clues) can
/// simply be added as more rows in Build.
/// </summary>
[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPaused = false;
    }

    private static readonly Color Warm = new Color(0.95f, 0.91f, 0.82f);
    private static readonly Color Dim = new Color(0.55f, 0.53f, 0.47f);
    private static readonly Color CardBg = new Color(0.07f, 0.07f, 0.08f, 0.94f);
    private static readonly Color Accent = new Color(0.93f, 0.70f, 0.30f);
    private static readonly Color ButtonNormal = new Color(0.16f, 0.16f, 0.17f, 1f);
    private static readonly Color ButtonHover = new Color(0.30f, 0.25f, 0.15f, 1f);

    private TMP_FontAsset font;
    private GameObject overlay;
    private MonoBehaviour playerController;

    // -------------------------------------------------------------- build --

    public void Build(TMP_FontAsset fontAsset)
    {
        font = fontAsset;

        EnsureEventSystem();

        GameObject canvasGo = new GameObject("PauseCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;   // above the HUD (800), below the scene fade (900)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Full-screen dimmer; also swallows clicks so nothing behind the menu can be hit.
        overlay = new GameObject("Overlay", typeof(RectTransform));
        overlay.transform.SetParent(canvasGo.transform, false);
        Fill((RectTransform)overlay.transform);
        Image dim = overlay.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.62f);

        // Card
        Image card = NewImage("Card", overlay.transform, CardBg);
        RectTransform cardRt = card.rectTransform;
        cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(480f, 330f);
        cardRt.anchoredPosition = Vector2.zero;

        Image edge = NewImage("TopEdge", card.transform, Accent);
        RectTransform er = edge.rectTransform;
        er.anchorMin = new Vector2(0f, 1f);
        er.anchorMax = new Vector2(1f, 1f);
        er.pivot = new Vector2(0.5f, 1f);
        er.anchoredPosition = Vector2.zero;
        er.sizeDelta = new Vector2(0f, 4f);

        TMP_Text title = NewText("Title", card.transform, 36, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(440f, 56f));
        title.text = "P A U S E D";

        NewButton(card.transform, "Resume", -128f, Resume);
        NewButton(card.transform, "Quit", -204f, Quit);

        TMP_Text hint = NewText("Hint", card.transform, 18, TextAlignmentOptions.Center);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(440f, 28f));
        hint.color = Dim;
        hint.text = "Esc - resume";

        overlay.SetActive(false);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
        InputSystemUIInputModule module = es.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        es.transform.SetParent(GameSystems.HasInstance ? GameSystems.Instance.transform : null, false);
    }

    // ------------------------------------------------------------ runtime --

    private void Update()
    {
        if (GameInput.PausePressed && CanToggle())
        {
            if (IsPaused) Resume();
            else Pause();
        }

        if (IsPaused)
        {
            // The game keeps re-locking the cursor when the window regains focus - not while paused.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private static bool CanToggle()
    {
        // Not while the screen is fading between rooms.
        return !(GameSystems.HasInstance && GameSystems.Instance.Fader != null && GameSystems.Instance.Fader.IsBusy);
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        playerController = FindPlayerController();
        if (playerController != null) playerController.enabled = false;   // no walking, no looking

        overlay.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;

        overlay.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        ClearStaleInput();
        if (playerController != null) playerController.enabled = true;
        playerController = null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        // Never leave the game frozen if this object disappears while paused.
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    // The Starter Assets first person controller is switched off while paused (it owns movement,
    // gravity and camera look), and its remembered look / move values are cleared on resume so the
    // camera does not keep turning from a stale mouse value.
    private static MonoBehaviour FindPlayerController()
    {
        CharacterController cc = FindFirstObjectByType<CharacterController>();
        if (cc == null) return null;

        foreach (MonoBehaviour mb in cc.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "FirstPersonController") return mb;

        return null;
    }

    private static void ClearStaleInput()
    {
        CharacterController cc = FindFirstObjectByType<CharacterController>();
        if (cc == null) return;

        foreach (MonoBehaviour mb in cc.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb.GetType().Name != "StarterAssetsInputs") continue;

            System.Type type = mb.GetType();
            System.Reflection.FieldInfo look = type.GetField("look");
            System.Reflection.FieldInfo move = type.GetField("move");
            System.Reflection.FieldInfo sprint = type.GetField("sprint");
            if (look != null) look.SetValue(mb, Vector2.zero);
            if (move != null) move.SetValue(mb, Vector2.zero);
            if (sprint != null) sprint.SetValue(mb, false);
        }
    }

    // -------------------------------------------------------------- utils --

    private void NewButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image bg = go.AddComponent<Image>();
        bg.color = Color.white;   // the Button's colour tint does the actual colouring

        Button button = go.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonNormal;
        colors.highlightedColor = ButtonHover;
        colors.selectedColor = ButtonNormal;
        colors.pressedColor = Accent;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        Place((RectTransform)go.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(360f, 56f));

        TMP_Text text = NewText("Label", go.transform, 26, TextAlignmentOptions.Center);
        Fill(text.rectTransform);
        text.text = label;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private TMP_Text NewText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Warm;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
