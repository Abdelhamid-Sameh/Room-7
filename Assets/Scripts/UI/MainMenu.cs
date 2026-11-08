using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The main menu: a dark hotel corridor with a sliver of warm light under door 7, a flickering title,
/// and four pages - Begin / Room select / Settings / Controls. Mouse, keyboard and gamepad all work.
///
/// Everything on screen is built from code (like the HUD and pause menu), so the scene only holds the
/// 3D backdrop and this component. The Room select page sets GameState to what the game would look like
/// when that room is reached normally, then loads the scene - for testing without playing through.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public const string SceneName = "MainMenu";

    [Header("Backdrop")]
    [SerializeField] private TMP_FontAsset font;          // body text (Constantia)
    [SerializeField] private TMP_FontAsset titleFont;     // ROOM 7 (Perpetua Titling Bold)
    [SerializeField] private TMP_FontAsset menuFont;      // buttons and headings (Perpetua Titling Light)
    [SerializeField] private TMP_FontAsset arabicFont;    // the Arabic title (Sakkal Majalla Bold)
    [SerializeField] private Camera cam;
    [SerializeField] private Light[] bulbs;
    [SerializeField] private Renderer[] bulbGlass;
    [SerializeField] private Light doorGlow;
    [SerializeField] private Renderer doorStrip;
    [SerializeField] private AudioClip ambience;
    [SerializeField] private AudioClip heartbeat;

    private static readonly Color Warm = new Color(0.95f, 0.91f, 0.82f);
    private static readonly Color Dim = new Color(0.55f, 0.53f, 0.47f);
    private static readonly Color Accent = new Color(0.93f, 0.70f, 0.30f);
    private static readonly Color Panel = new Color(0.05f, 0.05f, 0.06f, 0.9f);

    private struct Preset
    {
        public string Name, Hint, Scene, Spawn;
        public Action Setup;
    }

    private enum Page { Main, Rooms, Settings, Controls }

    private Transform canvasRoot;
    private readonly Dictionary<Page, CanvasGroup> pages = new Dictionary<Page, CanvasGroup>();
    private readonly Dictionary<Page, GameObject> firstSelected = new Dictionary<Page, GameObject>();
    private Page current = Page.Main;
    private Image fade;
    private TMP_Text titleRoom, titleSeven, titleAr;
    private AudioSource ambienceSrc, beatSrc;
    private GameObject lastSel;
    private float quietUntil, nextPreview;
    private Vector3 camBase;
    private Quaternion camRotBase;
    private bool loading, switching;
    private float nextDrop, dropUntil;

    // ------------------------------------------------------------------ setup --

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Start()
    {
        if (cam != null) { camBase = cam.transform.position; camRotBase = cam.transform.rotation; }
        EnsureEventSystem();
        BuildUi();
        BuildAudio();
        nextDrop = Time.time + 3.5f;
        StartCoroutine(Intro());
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
        InputSystemUIInputModule module = es.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    private void BuildAudio()
    {
        if (ambience != null) ambienceSrc = NewSource("Ambience", ambience);
        if (heartbeat != null) beatSrc = NewSource("Heartbeat", heartbeat);
    }

    private AudioSource NewSource(string n, AudioClip clip)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip; s.loop = true; s.spatialBlend = 0f; s.playOnAwake = false;
        s.volume = 0f;
        s.Play();
        return s;
    }

    // --------------------------------------------------------------------- UI --

private void BuildUi()
    {
        GameObject cgo = new GameObject("MenuCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3;
        CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        canvasRoot = cgo.transform;

        // a dark wash on the left so the title reads against the corridor
        Image wash = Img("Wash", canvasRoot, Color.white);
        wash.sprite = GradientSprite();
        Stretch(wash.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f));

        // title: the Arabic name is the main one (the 7 flickers like a dying sign), ROOM 7 sits under it, smaller
        titleSeven = Text(canvasRoot, "Seven", 270, TextAlignmentOptions.Left, Accent, "٧");
        titleAr = Text(canvasRoot, "Arabic", 270, TextAlignmentOptions.Left, Warm, "الغرفة");
        titleRoom = Text(canvasRoot, "Room", 60, TextAlignmentOptions.Left, Dim, "ROOM 7");
        if (arabicFont != null) { titleSeven.font = arabicFont; titleAr.font = arabicFont; }
        if (titleFont != null) titleRoom.font = titleFont;
        titleSeven.enableWordWrapping = false; titleAr.enableWordWrapping = false; titleRoom.enableWordWrapping = false;
        titleRoom.characterSpacing = 38f;
        titleSeven.ForceMeshUpdate(); titleAr.ForceMeshUpdate();
        float w7 = Mathf.Max(80f, titleSeven.GetPreferredValues().x), wAr = Mathf.Max(200f, titleAr.GetPreferredValues().x);
        float x0 = 150f, gap = 30f;
        Place(titleSeven, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0, -30f), new Vector2(w7 + 10f, 340f));
        Place(titleAr, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0 + w7 + gap, -30f), new Vector2(wAr + 10f, 340f));
        Place(titleRoom, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0 + 6f, -352f), new Vector2(700f, 80f));

        BuildMain();
        BuildRooms();
        BuildSettings();
        BuildControls();

        // the panels open on the right so the main list stays visible on the left
        foreach (KeyValuePair<Page, CanvasGroup> kv in pages)
        {
            Transform card = kv.Value.transform.Find("Card");
            if (card == null) continue;
            Place(card, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, -10f), new Vector2(990f, 740f));
        }

        // fonts: buttons and headings in the lighter display face
        if (menuFont != null)
            foreach (TMP_Text t in canvasRoot.GetComponentsInChildren<TMP_Text>(true))
                if (t.name == "Head" || t.name.EndsWith("Button")) t.font = menuFont;

        fade = Img("Fade", canvasRoot, Color.black);
        Stretch(fade.rectTransform, Vector2.zero, Vector2.one);
        Canvas fc = fade.gameObject.AddComponent<Canvas>();
        fc.overrideSorting = true; fc.sortingOrder = 950;
        fade.raycastTarget = false;

        ShowPage(Page.Main, true);
    }

    private CanvasGroup NewPage(Page p)
    {
        GameObject go = new GameObject(p + "Page", typeof(RectTransform));
        go.transform.SetParent(canvasRoot, false);
        Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one);
        CanvasGroup g = go.AddComponent<CanvasGroup>();
        pages[p] = g;
        return g;
    }

    private void BuildMain()
    {
        CanvasGroup pg = NewPage(Page.Main);
        Button first = null;
        string[] labels = { "Begin", "Room select", "Settings", "Controls", "Quit" };
        UnityAction[] acts = { Begin, () => ShowPage(Page.Rooms), () => ShowPage(Page.Settings), () => ShowPage(Page.Controls), Quit };
        for (int i = 0; i < labels.Length; i++)
        {
            Button b = MenuButton(pg.transform, labels[i], 44, new Vector2(150f, -520f - i * 78f), acts[i]);
            if (i == 0) first = b;
        }
        firstSelected[Page.Main] = first.gameObject;

        TMP_Text hint = Text(pg.transform, "Hint", 22, TextAlignmentOptions.Left, Dim, "Nabil has one room left to clean.");
        Place(hint, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(154f, 60f), new Vector2(900f, 40f));
    }

    private void BuildRooms()
    {
        CanvasGroup pg = NewPage(Page.Rooms);
        Image card = Img("Card", pg.transform, Panel);
        Place(card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, -30f), new Vector2(1020f, 740f));
        TMP_Text head = Text(card.transform, "Head", 40, TextAlignmentOptions.Left, Accent, "Room select");
        Place(head, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(700f, 56f));
        TMP_Text sub = Text(card.transform, "Sub", 20, TextAlignmentOptions.Left, Dim, "Jumps straight in with the story state that room normally has. Esc to go back.");
        Place(sub, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -76f), new Vector2(950f, 30f));

        Preset[] presets = Presets();
        Button first = null;
        for (int i = 0; i < presets.Length; i++)
        {
            Preset p = presets[i];
            Button b = MenuButton(card.transform, p.Name, 30, new Vector2(34f, -128f - i * 64f), () => StartPreset(p));
            ((RectTransform)b.transform).sizeDelta = new Vector2(430f, 56f);
            TMP_Text h = Text(b.transform, "Hint", 19, TextAlignmentOptions.Left, Dim, p.Hint);
            Place(h, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(450f, 0f), new Vector2(520f, 56f));
            h.raycastTarget = false;
            if (i == 0) first = b;
        }
        firstSelected[Page.Rooms] = first.gameObject;
    }

    private void BuildSettings()
    {
        CanvasGroup pg = NewPage(Page.Settings);
        Image card = Img("Card", pg.transform, Panel);
        Place(card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, -30f), new Vector2(1020f, 740f));
        TMP_Text head = Text(card.transform, "Head", 40, TextAlignmentOptions.Left, Accent, "Settings");
        Place(head, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(700f, 56f));

        float y = -120f;
        Slider bright = SliderRow(card.transform, "Brightness", ScreenBrightness.Value, y, v => ScreenBrightness.Value = v);
        y -= 74f;

        // test strip: the first swatches should be barely visible
        string[] names = { "", "", "", "", "", "" };
        float[] lum = { 0.0f, 0.015f, 0.03f, 0.05f, 0.08f, 0.13f };
        for (int i = 0; i < lum.Length; i++)
        {
            Image sw = Img("Swatch" + i, card.transform, new Color(lum[i], lum[i], lum[i] * 1.05f, 1f));
            Place(sw, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f + i * 86f, y), new Vector2(80f, 40f));
            names[i] = "";
        }
        TMP_Text strip = Text(card.transform, "StripHint", 19, TextAlignmentOptions.Left, Dim, "Raise brightness until the second box is barely visible.");
        Place(strip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, y - 46f), new Vector2(650f, 30f));
        y -= 112f;

        SliderRow(card.transform, "Master volume", AudioLevels.Master, y, v => AudioLevels.Master = v); y -= 66f;
        SliderRow(card.transform, "Music", AudioLevels.Music, y, v => AudioLevels.Music = v); y -= 66f;
        SliderRow(card.transform, "Ambience", AudioLevels.Ambient, y, v => AudioLevels.Ambient = v); y -= 66f;
        SliderRow(card.transform, "Sound effects", AudioLevels.Sfx, y, v => { AudioLevels.Sfx = v; if (Time.unscaledTime > nextPreview) { nextPreview = Time.unscaledTime + 0.15f; Sfx.Play(SfxKind.Click); } }); y -= 66f;

        Button back = MenuButton(card.transform, "Back", 30, new Vector2(34f, y - 12f), () => ShowPage(Page.Main));
        firstSelected[Page.Settings] = bright.gameObject;
        TMP_Text note = Text(card.transform, "Note", 18, TextAlignmentOptions.Left, Dim, "Saved automatically. Brightness also applies in the game.");
        Place(note, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 20f), new Vector2(800f, 28f));
    }

    private void BuildControls()
    {
        CanvasGroup pg = NewPage(Page.Controls);
        Image card = Img("Card", pg.transform, Panel);
        Place(card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, -30f), new Vector2(1020f, 740f));
        TMP_Text head = Text(card.transform, "Head", 40, TextAlignmentOptions.Left, Accent, "Controls");
        Place(head, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(700f, 56f));

        string[][] rows =
        {
            new[] { "W A S D", "Walk" },
            new[] { "Mouse", "Look around" },
            new[] { "Left Shift", "Run" },
            new[] { "E  /  Left click", "Interact, open, pick up, put back" },
            new[] { "F", "Flashlight (once you have it)" },
            new[] { "R   T   Y", "Radio: play / next / previous" },
            new[] { "Esc  /  P", "Pause menu: clues, messages, artefacts" },
            new[] { "Q  /  Esc", "Leave the Game Boy, keypad or document" },
        };
        for (int i = 0; i < rows.Length; i++)
        {
            float y = -118f - i * 68f;
            Image cap = Img("Cap", card.transform, Accent);
            Place(cap, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(290f, 54f));
            Image inner = Img("Fill", cap.transform, new Color(0.10f, 0.10f, 0.11f));
            Stretch(inner.rectTransform, Vector2.zero, Vector2.one, 2f);
            TMP_Text k = Text(inner.transform, "Key", 27, TextAlignmentOptions.Center, Warm, rows[i][0]);
            Stretch(k.rectTransform, Vector2.zero, Vector2.one);
            TMP_Text d = Text(card.transform, "Does", 27, TextAlignmentOptions.Left, Warm, rows[i][1]);
            Place(d, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(358f, y - 4f), new Vector2(640f, 48f));
        }
        TMP_Text pad = Text(card.transform, "Pad", 20, TextAlignmentOptions.Left, Dim, "Gamepad: left stick walk, right stick look, Y interact, Start pause.");
        Place(pad, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -678f), new Vector2(940f, 30f));

        Button back = MenuButton(card.transform, "Back", 30, new Vector2(760f, -672f), () => ShowPage(Page.Main));
        ((RectTransform)back.transform).sizeDelta = new Vector2(220f, 52f);
        firstSelected[Page.Controls] = back.gameObject;
    }

    // ------------------------------------------------------------ widgets --

    private Button MenuButton(Transform parent, string label, float size, Vector2 pos, UnityAction onClick)
    {
        TMP_Text t = Text(parent, label + "Button", size, TextAlignmentOptions.Left, Color.white, label);
        t.raycastTarget = true;
        Place(t, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(520f, size * 1.5f));
        Button b = t.gameObject.AddComponent<Button>();
        b.targetGraphic = t;
        ColorBlock c = b.colors;
        c.normalColor = Warm; c.highlightedColor = Accent; c.selectedColor = Accent;
        c.pressedColor = Color.white; c.disabledColor = Dim;
        c.colorMultiplier = 1f; c.fadeDuration = 0.1f;
        b.colors = c;
        b.onClick.AddListener(() => { Sfx.Play(SfxKind.Click); onClick(); });
        t.gameObject.AddComponent<HoverSelect>();
        return b;
    }

    private Slider SliderRow(Transform parent, string label, float value, float y, UnityAction<float> onChange)
    {
        TMP_Text l = Text(parent, label + "Label", 26, TextAlignmentOptions.Left, Warm, label);
        Place(l, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(250f, 40f));

        GameObject go = new GameObject(label + "Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, y - 4f), new Vector2(560f, 30f));

        Image track = Img("Track", go.transform, new Color(0.18f, 0.18f, 0.19f));
        Stretch(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
        track.rectTransform.sizeDelta = new Vector2(0f, 8f);

        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform far = fillArea.GetComponent<RectTransform>();
        far.anchorMin = new Vector2(0f, 0.5f); far.anchorMax = new Vector2(1f, 0.5f);
        far.sizeDelta = new Vector2(-18f, 8f); far.anchoredPosition = Vector2.zero;
        Image fill = Img("Fill", fillArea.transform, Accent);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        RectTransform har = handleArea.GetComponent<RectTransform>();
        har.anchorMin = Vector2.zero; har.anchorMax = Vector2.one;
        har.offsetMin = new Vector2(9f, 0f); har.offsetMax = new Vector2(-9f, 0f);
        Image handle = Img("Handle", handleArea.transform, Warm);
        handle.rectTransform.sizeDelta = new Vector2(18f, 30f);
        handle.raycastTarget = true;

        Slider s = go.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f;
        s.wholeNumbers = false;
        s.value = value;
        ColorBlock c = s.colors;
        c.normalColor = Warm; c.highlightedColor = Accent; c.selectedColor = Accent; c.pressedColor = Accent;
        s.colors = c;

        TMP_Text pct = Text(parent, label + "Pct", 24, TextAlignmentOptions.Right, Dim, Mathf.RoundToInt(value * 100f) + "%");
        Place(pct, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(880f, y), new Vector2(100f, 40f));
        s.onValueChanged.AddListener(v => { pct.text = Mathf.RoundToInt(v * 100f) + "%"; onChange(v); });
        go.AddComponent<HoverSelect>();
        return s;
    }

    /// <summary>Selecting on hover keeps the mouse and the keyboard / gamepad highlight in one place.</summary>
    private class HoverSelect : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData e)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    private TMP_Text Text(Transform parent, string name, float size, TextAlignmentOptions align, Color color, string text)
    {
        return PhoneUI.Txt(parent, font, name, size, align, color, text);
    }

    private static Image Img(string name, Transform parent, Color color)
    {
        return PhoneUI.Img(name, parent, color);
    }

    private static void Place(Component c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = (RectTransform)c.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, Vector2 min, Vector2 max, float inset = 0f)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static Sprite gradient;
    private static Sprite GradientSprite()
    {
        if (gradient != null) return gradient;
        Texture2D t = new Texture2D(256, 1, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < 256; x++)
        {
            float a = Mathf.Pow(1f - x / 255f, 1.6f) * 0.93f;
            t.SetPixel(x, 0, new Color(0.02f, 0.02f, 0.03f, a));
        }
        t.Apply();
        gradient = Sprite.Create(t, new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f), 100f);
        return gradient;
    }

    // ------------------------------------------------------------------ pages --

private void ShowPage(Page p, bool instant = false)
    {
        if (switching && !instant) return;
        current = p;
        foreach (KeyValuePair<Page, CanvasGroup> kv in pages)
        {
            bool on = kv.Key == p || kv.Key == Page.Main;   // the main list stays on screen next to a panel
            kv.Value.gameObject.SetActive(on);
            kv.Value.alpha = on ? 1f : 0f;
            kv.Value.interactable = on;
            kv.Value.blocksRaycasts = on;
        }
        if (!instant) Sfx.Play(SfxKind.Click);
        quietUntil = Time.unscaledTime + 0.25f;
        StartCoroutine(SelectNextFrame(firstSelected[p]));
    }

    private IEnumerator SelectNextFrame(GameObject go)
    {
        yield return null;
        if (EventSystem.current != null && go != null) EventSystem.current.SetSelectedGameObject(go);
    }

    private void Back()
    {
        if (current != Page.Main && !loading) ShowPage(Page.Main);
    }

    // ---------------------------------------------------------------- actions --

    private void Begin()
    {
        StartRoute(() => { GameState.ResetSession(); GameState.LastSpawnId = GameState.SpawnStart; }, "HotelFloor_Corridor");
    }

    private void StartPreset(Preset p)
    {
        StartRoute(() => { p.Setup(); GameState.LastSpawnId = p.Spawn; }, p.Scene);
    }

    private void StartRoute(Action setup, string scene)
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("[MainMenu] Scene '" + scene + "' is not in the build settings.");
            return;
        }
        loading = true;
        StartCoroutine(LoadRoutine(setup, scene));
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadRoutine(Action setup, string scene)
    {
        float v0a = ambienceSrc != null ? ambienceSrc.volume : 0f, v0b = beatSrc != null ? beatSrc.volume : 0f;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.9f)
        {
            fade.color = new Color(0f, 0f, 0f, t);
            if (ambienceSrc != null) ambienceSrc.volume = v0a * (1f - t);
            if (beatSrc != null) beatSrc.volume = v0b * (1f - t);
            yield return null;
        }
        fade.color = Color.black;
        GameState.ResetSession();
        setup();
        GameState.RaiseChanged();
        SceneManager.LoadScene(scene);
    }

    /// <summary>Back to the menu from inside the game (the pause menu calls this).</summary>
    public static void GoToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (GameSystems.HasInstance) Destroy(GameSystems.Instance.gameObject);
        GameState.ResetSession();
        SceneManager.LoadScene(SceneName);
    }

    // ------------------------------------------------------------ room presets --

    private static Preset[] Presets()
    {
        return new[]
        {
            new Preset { Name = "Start of the night", Hint = "Corridor, with the manager's texts", Scene = "HotelFloor_Corridor", Spawn = GameState.SpawnStart, Setup = () => { } },
            new Preset { Name = "Hotel floor", Hint = "Corridor, nothing done yet", Scene = "HotelFloor_Corridor", Spawn = GameState.SpawnStart, Setup = () => { Base(); } },
            new Preset { Name = "Staff room", Hint = "Kit, radio and uniform to collect", Scene = "StaffRoom", Spawn = GameState.SpawnFromCorridor, Setup = () => { Base(); } },
            new Preset { Name = "Room 1", Hint = "The one normal room", Scene = "Room01", Spawn = GameState.SpawnFromCorridor, Setup = () => { Base(); Kit(); } },
            new Preset { Name = "Room 2", Hint = "The looping corridor", Scene = "Room02", Spawn = GameState.SpawnFromCorridor, Setup = () => { Base(); Kit(); GameState.SetRoomCleaned("Room01"); } },
            new Preset { Name = "Room 3", Hint = "Four rooms, the safe", Scene = "Room03", Spawn = GameState.SpawnFromCorridor, Setup = () => { Base(); Kit(); AfterRoom2(); } },
            new Preset { Name = "Strange corridor", Hint = "After the safe: Limbo", Scene = "Limbo_Corridor", Spawn = "Limbo_Start", Setup = () => { Base(); Kit(); AfterRoom2(); Limbo(); } },
            new Preset { Name = "Game Boy room", Hint = "The red console and its maze", Scene = "Limbo_Corridor", Spawn = "Limbo_GameBoy", Setup = () => { Base(); Kit(); AfterRoom2(); Limbo(); } },
            new Preset { Name = "Mirror room", Hint = "The copy behind the glass", Scene = "Limbo_Corridor", Spawn = "Limbo_Mirror", Setup = () => { Base(); Kit(); AfterRoom2(); Limbo(); GameState.GameBoySolved = true; GameState.UnknownGameBoyTexted = true; } },
        };
    }

    private static void Base() { GameState.IntroDone = true; }

    private static void Kit()
    {
        GameState.HasCleaningKit = true; GameState.HasRadio = true;
        GameState.UniformTaken = true; GameState.UniformWorn = true;
    }

    private static void AfterRoom2()
    {
        GameState.SetRoomCleaned("Room01");
        GameState.HasFlashlight = true; GameState.HasFuse = true;
        GameState.R2Loops = 2; GameState.R2Done = true; GameState.R2SideOpen = true; GameState.R2Replied = true;
        GameState.PowerOut = false;
    }

    private static void Limbo()
    {
        GameState.R3SafeOpen = true; GameState.R3SafeItem = ""; GameState.R3MainDone = true;
        GameState.AddArtifact("GameBoy");
        GameState.Limbo = true; GameState.LimboLit = true; GameState.UnknownTexted = true;
    }

    // ----------------------------------------------------------------- frame --

    private IEnumerator Intro()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.6f)
        {
            fade.color = new Color(0f, 0f, 0f, 1f - t);
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, 0f);
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        // camera: a slow drift down the corridor plus a little mouse parallax
        if (cam != null)
        {
            Vector2 m = Vector2.zero;
            if (Mouse.current != null) { Vector2 p = Mouse.current.position.ReadValue(); m = new Vector2(p.x / Screen.width - 0.5f, p.y / Screen.height - 0.5f); }
            Vector3 drift = new Vector3(Mathf.Sin(t * 0.21f) * 0.12f + m.x * 0.35f, Mathf.Sin(t * 0.17f) * 0.04f + m.y * 0.12f, Mathf.Sin(t * 0.05f) * 0.5f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, camBase + drift, Time.unscaledDeltaTime * 2f);
            Quaternion look = camRotBase * Quaternion.Euler(-m.y * 2.5f, m.x * 3f, Mathf.Sin(t * 0.13f) * 0.4f);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, look, Time.unscaledDeltaTime * 2f);
        }

        // the corridor bulbs and the title flicker
        float dropping = 1f;
        if (t > nextDrop) { dropUntil = t + UnityEngine.Random.Range(0.12f, 0.5f); nextDrop = t + UnityEngine.Random.Range(4f, 9f); }
        if (t < dropUntil) dropping = (int)(t * 24f) % 2 == 0 ? 0.15f : 0.7f;

        for (int i = 0; bulbs != null && i < bulbs.Length; i++)
        {
            float n = Mathf.PerlinNoise(t * (3f + i), i * 7.3f);
            float f = i == 0 ? dropping * (0.75f + 0.25f * n) : 0.85f + 0.15f * n;
            if (bulbs[i] != null) bulbs[i].intensity = 2.2f * f;
            if (bulbGlass != null && i < bulbGlass.Length && bulbGlass[i] != null)
                bulbGlass[i].material.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * (2.4f * f));
        }
        if (doorGlow != null) doorGlow.intensity = 2.6f + Mathf.Sin(t * 0.9f) * 0.6f + (dropping < 1f ? 0.8f : 0f);
        if (doorStrip != null) doorStrip.material.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.3f) * (3.2f + Mathf.Sin(t * 0.9f)));

        float sign = dropping < 1f ? dropping : 1f - (Mathf.PerlinNoise(t * 7f, 1.3f) < 0.12f ? 0.55f : 0f);
        if (titleSeven != null) { Color c = Accent; c.a = Mathf.Clamp01(sign); titleSeven.color = c; }
        if (titleRoom != null) { Color c = Warm; c.a = 0.94f; titleRoom.color = c; }

        // audio fades in and follows the saved volumes
        if (!loading)
        {
            if (ambienceSrc != null) ambienceSrc.volume = Mathf.MoveTowards(ambienceSrc.volume, 0.55f * AudioLevels.AmbientVolume, Time.unscaledDeltaTime * 0.3f);
            if (beatSrc != null) beatSrc.volume = Mathf.MoveTowards(beatSrc.volume, 0.12f * AudioLevels.AmbientVolume, Time.unscaledDeltaTime * 0.2f);
        }

        // a soft tick whenever the highlighted entry changes (mouse hover, keyboard or pad)
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (sel != lastSel)
        {
            if (sel != null && lastSel != null && !loading && t > quietUntil) Sfx.Play(SfxKind.Hover);
            lastSel = sel;
        }

        // back: Esc or gamepad B
        bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (back) Back();

        // keep something selected so the keyboard / pad always has a target
        if (!loading && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && firstSelected.ContainsKey(current))
        {
            bool navKey = (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame))
                          || (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.2f);
            if (navKey) EventSystem.current.SetSelectedGameObject(firstSelected[current]);
        }
    }
}

/// <summary>
/// The brightness setting. A soft light veil over the whole game (under the HUD), saved in PlayerPrefs.
/// 0 = the game exactly as built; 1 = the blacks are lifted a lot. Works in every scene, menu included.
/// </summary>
public static class ScreenBrightness
{
    private const string Key = "r7_brightness";
    private const float MaxAlpha = 0.26f;
    private static Image veil;
    private static bool additive;

    public static float Value
    {
        get { return PlayerPrefs.GetFloat(Key, 0f); }
        set { PlayerPrefs.SetFloat(Key, Mathf.Clamp01(value)); Apply(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { veil = null; additive = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init() { Apply(); }

private static void Apply()
    {
        float v = Value;
        if (veil == null)
        {
            if (v <= 0.001f) return;
            GameObject go = new GameObject("BrightnessVeil", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(go);
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 5;
            veil = PhoneUI.Img("Veil", go.transform, Color.white);
            veil.raycastTarget = false;
            RectTransform rt = veil.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            // additive, so it lifts the shadows and leaves the highlights alone (a plain tint just greys the picture)
            Material add = Resources.Load<Material>("BrightnessAdd");
            if (add != null) { veil.material = new Material(add); additive = true; }
        }
        veil.color = additive ? new Color(0.9f, 0.93f, 1f, v * 0.45f) : new Color(0.78f, 0.82f, 0.95f, v * MaxAlpha);
        veil.enabled = v > 0.001f;
    }
}
