using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Esc (or Start on a gamepad) pauses the game: time and all audio freeze, the player stops moving
/// and looking, the cursor appears, and a menu with three tabs opens - Menu (Resume / Quit), Clues
/// (everything in GameState.Clues) and Messages (GameState.Messages grouped by contact; Malak only
/// shows up once Nabil has texted her). Esc again resumes.
///
/// Lives on the persistent GameSystems object and builds its own UI from code (same look as the
/// HUD cards). Other scripts check PauseMenu.IsPaused to ignore input while the game is paused.
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
    private static readonly Color PanelFill = new Color(0.10f, 0.10f, 0.11f, 1f);
    private static readonly string[] TabNames = { "Menu", "Clues", "Messages", "Artefacts" };

    private TMP_FontAsset font;
    private GameObject overlay;
    private MonoBehaviour playerController;

    private readonly GameObject[] pages = new GameObject[4];
    private readonly Button[] tabButtons = new Button[4];
    private readonly TMP_Text[] tabLabels = new TMP_Text[4];
    private RectTransform cluesContent;
    private RectTransform contactsContent;
    private ChatView chat;
    private string selectedContact;

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
        cardRt.sizeDelta = new Vector2(1100f, 720f);
        cardRt.anchoredPosition = Vector2.zero;

        Image edge = NewImage("TopEdge", card.transform, Accent);
        RectTransform er = edge.rectTransform;
        er.anchorMin = new Vector2(0f, 1f);
        er.anchorMax = new Vector2(1f, 1f);
        er.pivot = new Vector2(0.5f, 1f);
        er.anchoredPosition = Vector2.zero;
        er.sizeDelta = new Vector2(0f, 4f);

        TMP_Text title = NewText("Title", card.transform, 34, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(440f, 50f));
        title.text = "P A U S E D";

        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            tabButtons[i] = NewButton(card.transform, TabNames[i], -92f, () => { Sfx.Play(SfxKind.Tab); SetTab(index); },
                                      new Vector2((i - 1.5f) * 216f, 0f), new Vector2(200f, 48f), 24, out tabLabels[i]);
        }

        TMP_Text hint = NewText("Hint", card.transform, 18, TextAlignmentOptions.Center);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(440f, 28f));
        hint.color = Dim;
        hint.text = "Esc - resume";

        // Pages share one content area under the tabs.
        RectTransform area = new GameObject("Pages", typeof(RectTransform)).GetComponent<RectTransform>();
        area.SetParent(card.transform, false);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(40f, 56f);
        area.offsetMax = new Vector2(-40f, -160f);

        BuildMenuPage(area);
        BuildCluesPage(area);
        BuildMessagesPage(area);
        BuildArtifactsPage(area);

        overlay.SetActive(false);
    }

    private void BuildMenuPage(Transform parent)
    {
        pages[0] = NewPage("MenuPage", parent);
        TMP_Text l;
        NewButton(pages[0].transform, "Resume", -90f, Resume, Vector2.zero, new Vector2(360f, 56f), 26, out l);
        NewButton(pages[0].transform, "Quit", -170f, Quit, Vector2.zero, new Vector2(360f, 56f), 26, out l);
    }

    private void BuildCluesPage(Transform parent)
    {
        pages[1] = NewPage("CluesPage", parent);
        Image bg = NewImage("Bg", pages[1].transform, PanelFill);
        Fill(bg.rectTransform);
        PhoneUI.NewScroll(pages[1].transform, out cluesContent, 10f, 12);
    }

    private void BuildMessagesPage(Transform parent)
    {
        pages[2] = NewPage("MessagesPage", parent);

        Image left = NewImage("Contacts", pages[2].transform, PanelFill);
        RectTransform lr = left.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = new Vector2(0f, 1f);
        lr.pivot = new Vector2(0f, 0.5f);
        lr.offsetMin = Vector2.zero;
        lr.sizeDelta = new Vector2(300f, 0f);
        lr.anchoredPosition = Vector2.zero;
        PhoneUI.NewScroll(left.transform, out contactsContent, 8f, 10);

        Image right = NewImage("Conversation", pages[2].transform, PanelFill);
        RectTransform rr = right.rectTransform;
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = new Vector2(316f, 0f);
        rr.offsetMax = Vector2.zero;
        chat = ChatView.Create(right.transform, font, 520f);
    }

    private RectTransform artifactsContent;

    private void BuildArtifactsPage(Transform parent)
    {
        pages[3] = NewPage("ArtifactsPage", parent);
        Image bg = NewImage("Bg", pages[3].transform, PanelFill);
        Fill(bg.rectTransform);
        PhoneUI.NewScroll(pages[3].transform, out artifactsContent, 10f, 12);
    }

    private void RefreshArtifacts()
    {
        ClearChildren(artifactsContent);

        if (GameState.Artifacts.Count == 0)
        {
            AddArtifactRow(" ", "Nothing yet. Some things are worth keeping.", true);
            return;
        }

        foreach (string id in GameState.Artifacts)
        {
            string title = GameState.ArtifactTitle(id);
            AddArtifactRow(char.ToUpper(title[0]) + title.Substring(1), GameState.ArtifactNote(id), false, id);
        }
    }

    private void AddArtifactRow(string title, string note, bool dim, string id = null)
    {
        GameObject row = new GameObject("Artifact", typeof(RectTransform));
        row.transform.SetParent(artifactsContent, false);
        Image bg = row.AddComponent<Image>();
        bg.color = PhoneUI.BoxFill;
        bg.raycastTarget = false;

        Texture2D photo = string.IsNullOrEmpty(id) ? null : Resources.Load<Texture2D>("Room3/Artifact_" + id);
        float left = 24f, w = 860f;
        if (photo != null)
        {
            GameObject pg = new GameObject("Photo", typeof(RectTransform));
            pg.transform.SetParent(row.transform, false);
            RawImage ri = pg.AddComponent<RawImage>();
            ri.texture = photo; ri.raycastTarget = false;
            PhoneUI.Place(ri, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(190f, 190f));
            left = 230f; w = 660f;
        }
        TMP_Text head = PhoneUI.Txt(row.transform, font, "Title", 32, TextAlignmentOptions.TopLeft, Accent, title);
        PhoneUI.Place(head, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -14f), new Vector2(w, 42f));
        // Plain (left-to-right) text; Arabic words are shaped here so a mixed English / Arabic sentence keeps its order.
        TMP_Text body = PhoneUI.Txt(row.transform, font, "Note", 24, TextAlignmentOptions.TopLeft, dim ? Dim : Warm, "");
        body.text = ShapeArabicRuns(note);
        float h = body.GetPreferredValues(w, 0f).y;
        PhoneUI.Place(body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -60f), new Vector2(w, h));
        row.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(h + 84f, photo != null ? 218f : 0f);
    }

    private static string ShapeArabicRuns(string s)
    {
        return System.Text.RegularExpressions.Regex.Replace(s ?? "", @"[؀-ۿ]+(\s[؀-ۿ]+)*", m =>
        {
            RTLTMPro.FastStringBuilder fsb = new RTLTMPro.FastStringBuilder(64);
            RTLTMPro.RTLSupport.FixRTL(m.Value, fsb, false, true, false);
            return fsb.ToString();
        });
    }

    private static GameObject NewPage(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Fill((RectTransform)go.transform);
        return go;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
        InputSystemUIInputModule module = es.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        es.transform.SetParent(GameSystems.HasInstance ? GameSystems.Instance.transform : null, false);
    }

    // ---------------------------------------------------------------- tabs --

    private void SetTab(int index)
    {
        for (int i = 0; i < TabNames.Length; i++)
        {
            pages[i].SetActive(i == index);

            bool on = i == index;
            ColorBlock c = tabButtons[i].colors;
            c.normalColor = on ? Accent : ButtonNormal;
            c.selectedColor = c.normalColor;
            c.highlightedColor = on ? Accent : ButtonHover;
            tabButtons[i].colors = c;
            tabLabels[i].color = on ? new Color(0.1f, 0.08f, 0.05f) : Warm;
        }

        if (index == 1) RefreshClues();
        if (index == 2) RefreshContacts();
        if (index == 3) RefreshArtifacts();
    }

    private static void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject g = parent.GetChild(i).gameObject;
            g.SetActive(false);
            Destroy(g);
        }
    }

    private void RefreshClues()
    {
        ClearChildren(cluesContent);

        if (GameState.Clues.Count == 0)
        {
            AddClueRow("-", "No clues yet. Look around the rooms.", true);
            return;
        }

        for (int i = 0; i < GameState.Clues.Count; i++)
            AddClueRow((i + 1).ToString(), GameState.Clues[i], false);
    }

    private void AddClueRow(string number, string text, bool dim)
    {
        GameObject row = new GameObject("Clue", typeof(RectTransform));
        row.transform.SetParent(cluesContent, false);
        Image bg = row.AddComponent<Image>();
        bg.color = PhoneUI.BoxFill;
        bg.raycastTarget = false;

        bool ar = PhoneUI.HasArabic(text);
        TMP_Text body = PhoneUI.Txt(row.transform, font, "Text", 26, ar ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft, dim ? Dim : Warm, text);
        float h = Mathf.Max(60f, body.GetPreferredValues(860f, 0f).y + 28f);
        row.AddComponent<LayoutElement>().preferredHeight = h;

        RectTransform br = body.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(70f, 14f);
        br.offsetMax = new Vector2(-20f, -14f);

        TMP_Text n = PhoneUI.Txt(row.transform, font, "Number", 28, TextAlignmentOptions.TopLeft, Accent, number);
        PhoneUI.Place(n, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(44f, 36f));
    }

    private void RefreshContacts()
    {
        ClearChildren(contactsContent);
        chat.Clear();

        var contacts = new System.Collections.Generic.List<string>();
        foreach (GameState.ChatMessage m in GameState.Messages)
            if (!contacts.Contains(m.Contact)) contacts.Add(m.Contact);

        if (contacts.Count == 0)
        {
            selectedContact = null;
            AddContactRow(null, "No messages yet.", "");
            return;
        }

        if (selectedContact == null || !contacts.Contains(selectedContact)) selectedContact = contacts[0];

        foreach (string contact in contacts)
        {
            string last = "";
            foreach (GameState.ChatMessage m in GameState.Messages) if (m.Contact == contact) last = m.Text;
            AddContactRow(contact, contact, last);
        }

        foreach (GameState.ChatMessage m in GameState.Messages)
            if (m.Contact == selectedContact) chat.AddBubble(m.Text, m.FromPlayer);
    }

    private void AddContactRow(string contact, string name, string preview)
    {
        GameObject row = new GameObject("Contact", typeof(RectTransform));
        row.transform.SetParent(contactsContent, false);
        row.AddComponent<LayoutElement>().preferredHeight = 78f;

        Image bg = row.AddComponent<Image>();
        bg.color = Color.white;
        bool selected = contact != null && contact == selectedContact;

        TMP_Text title = PhoneUI.Txt(row.transform, font, "Name", 26, TextAlignmentOptions.Left, selected ? new Color(0.1f, 0.08f, 0.05f) : Warm, name);
        PhoneUI.Place(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(250f, 34f));

        if (!string.IsNullOrEmpty(preview))
        {
            if (preview.Length > 20) preview = preview.Substring(0, 20) + "...";
            TMP_Text p = PhoneUI.Txt(row.transform, font, "Preview", 19, PhoneUI.HasArabic(preview) ? TextAlignmentOptions.Right : TextAlignmentOptions.Left,
                                     selected ? new Color(0.25f, 0.19f, 0.1f) : Dim, preview);
            p.textWrappingMode = TextWrappingModes.NoWrap;
            PhoneUI.Place(p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -44f), new Vector2(250f, 26f));
        }

        if (contact == null) return;

        Button button = row.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = selected ? Accent : ButtonNormal;
        colors.selectedColor = colors.normalColor;
        colors.highlightedColor = selected ? Accent : ButtonHover;
        colors.pressedColor = Accent;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => { Sfx.Play(SfxKind.Tab); selectedContact = contact; RefreshContacts(); });
    }

    // ------------------------------------------------------------ runtime --

    private void Update()
    {
        if (GameInput.PausePressed && CanToggle())
        {
            if (IsPaused) Resume();
            else Pause();
        }

        UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.pKey.wasPressedThisFrame && CanToggle())
        {
            // P: quick look at the phone. Pressing it again on the Messages tab closes it.
            if (IsPaused && pages[2].activeSelf) Resume();
            else { if (!IsPaused) Pause(); SetTab(2); }
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
        // Not during the opening phone scene, while a paper is being read, or while the screen is fading.
        if (PhoneUI.Busy || DocumentViewer.IsOpen || SafeKeypad.IsOpen || GameBoyConsole.Playing || Time.frameCount == GameBoyConsole.ClosedFrame || Time.frameCount == SafeKeypad.ClosedFrame || Time.frameCount == DocumentViewer.ClosedFrame) return false;
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
        SetTab(0);
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
    internal static MonoBehaviour FindPlayerController()
    {
        CharacterController cc = FindFirstObjectByType<CharacterController>();
        if (cc == null) return null;

        foreach (MonoBehaviour mb in cc.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "FirstPersonController") return mb;

        return null;
    }

    internal static void ClearStaleInput()
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

    private Button NewButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction onClick,
                             Vector2 xOffset, Vector2 size, float fontSize, out TMP_Text text)
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

        Place((RectTransform)go.transform, new Vector2(0.5f, 1f), new Vector2(xOffset.x, y), size);

        text = NewText("Label", go.transform, fontSize, TextAlignmentOptions.Center);
        Fill(text.rectTransform);
        text.text = label;
        return button;
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
