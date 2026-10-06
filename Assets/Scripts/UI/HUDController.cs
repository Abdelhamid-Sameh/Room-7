using RTLTMPro;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen furniture: crosshair, contextual prompt, task list, clue toast and the carried-radio widget.
///
/// The tasks, clue and radio cards are all made by NewCard, so they share one look: dark translucent
/// plate, amber top edge, spaced-out grey title. Change CardScale to resize all three at once.
///
/// All text is TextMesh Pro with the font passed to Build, so Arabic works (RTL Text Mesh Pro is used
/// for anything whose text can come from the story or the music files).
///
/// Tasks: a completed task stays on the list, ticked; the next task appears after one is completed.
/// Add or reorder tasks in BuildTasks. Clues: each new clue (GameState.AddClue) is shown once, then
/// fades away; the full list lives in GameState.Clues for the pause-menu journal later.
///
/// Everything is built from code (see Build) so no scene wiring is needed.
/// </summary>
public class HUDController : MonoBehaviour
{
    /// <summary>1 = the sizes written below, 1.2 = 20% bigger. Applies to tasks, clue and radio.</summary>
    private const float CardScale = 1.2f;

    private const float CardWidth = 360f;
    private const float TaskFirstY = -52f;
    private const float TaskStep = 30f;

    private const float ClueFadeIn = 0.35f;
    private const float ClueHold = 6f;
    private const float ClueFadeOut = 1.2f;

    private TMP_FontAsset font;

    private Image crosshair;
    private float crosshairTarget = 1f;

    private GameObject promptPanel;
    private TMP_Text promptText;

    private class TaskDef
    {
        public string Label;
        public System.Func<bool> IsDone;
    }

    private class TaskRow
    {
        public TaskDef Def;
        public RectTransform Root;
        public CanvasGroup Group;
        public Image Outer;
        public Image Inner;
        public GameObject Tick;
        public TMP_Text Label;
        public bool Done;
        public bool Visible;
        public bool Placed;
        public float RevealAt;
        public float Alpha;
        public float Y;
        public float Pop;
    }

    private RectTransform tasksCard;
    private TaskRow[] taskRows;
    private bool tasksInitialized;
    private float tasksCardHeight;

    private GameObject cluePanel;
    private CanvasGroup clueGroup;
    private TMP_Text clueText;
    private float clueTime = -1f;

    private GameObject radioPanel;
    private Image radioPowerDot;
    private TMP_Text radioPowerText;
    private TMP_Text radioTrackText;
    private TMP_Text radioPositionText;
    private TMP_Text radioElapsedText;
    private TMP_Text radioTotalText;
    private TMP_Text radioPlayLabel;
    private RectTransform radioProgress;
    private Image[] radioBars;
    private int radioLastSecond = -1;

    private static readonly Color Warm = new Color(0.95f, 0.91f, 0.82f);
    private static readonly Color Dim = new Color(0.55f, 0.53f, 0.47f);
    private static readonly Color CardBg = new Color(0.07f, 0.07f, 0.08f, 0.74f);
    private static readonly Color BoxFill = new Color(0.07f, 0.07f, 0.08f, 1f);
    private static readonly Color Accent = new Color(0.93f, 0.70f, 0.30f);
    private static readonly Color ClueTint = new Color(0.93f, 0.78f, 0.48f);
    private static readonly Color PowerOn = new Color(0.47f, 0.86f, 0.49f);
    private static readonly Color PowerOff = new Color(0.40f, 0.38f, 0.34f);

    // -------------------------------------------------------------- build --

    /// <summary>Builds the whole HUD. 'fontAsset' is the TMP font used for every label (Cairo SDF).</summary>
    public void Build(TMP_FontAsset fontAsset)
    {
        font = fontAsset;

        BuildCrosshair();
        BuildPrompt();
        BuildTasks();
        BuildClue();
        BuildRadio();
    }

    private void OnEnable()
    {
        GameState.ClueAdded += ShowClue;
    }

    private void OnDisable()
    {
        GameState.ClueAdded -= ShowClue;
    }

    private void BuildCrosshair()
    {
        Image dot = NewImage("Crosshair", transform, Warm);
        Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
              Vector2.zero, new Vector2(9f, 9f));
        crosshair = dot;
    }

    private void BuildPrompt()
    {
        RectTransform card = NewCard("PromptCard", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                     new Vector2(0f, 175f), new Vector2(640f, 62f), null, 1f);
        promptPanel = card.gameObject;

        promptText = NewText("Prompt", card, 30, TextAlignmentOptions.Center, true);
        Fill(promptText.rectTransform, new Vector2(20f, 4f), new Vector2(-20f, -6f));
        promptText.text = "";

        // Static control reminder, always visible.
        TMP_Text hint = NewText("Controls", transform, 20, TextAlignmentOptions.Center, false);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
              new Vector2(0f, 122f), new Vector2(900f, 40f));
        hint.text = "E  /  Left Click - interact";
        SetAlpha(hint, 0.55f);

        promptPanel.SetActive(false);
    }

    /// <summary>
    /// Top-left task list. A task is shown ticked once done, and the next one appears when the
    /// current one is completed. Tasks can be done in any order; the first unfinished one is the
    /// current task.
    /// </summary>
    private void BuildTasks()
    {
        tasksCard = NewCard("TasksCard", new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(28f, -28f), new Vector2(CardWidth, 78f), "T A S K S", CardScale);
        tasksCardHeight = 78f;

        // Add or reorder tasks here. Order = the order they are revealed in.
        TaskDef[] defs =
        {
            new TaskDef { Label = "Put on your uniform",         IsDone = () => GameState.UniformWorn },
            new TaskDef { Label = "Take the cleaning supplies",  IsDone = () => GameState.HasCleaningKit },
            new TaskDef { Label = "Take the radio",              IsDone = () => GameState.HasRadio },
        };

        taskRows = new TaskRow[defs.Length];
        for (int i = 0; i < defs.Length; i++)
            taskRows[i] = BuildTaskRow(tasksCard, defs[i]);
    }

    private TaskRow BuildTaskRow(RectTransform parent, TaskDef def)
    {
        TaskRow row = new TaskRow { Def = def };

        GameObject rootGo = new GameObject("Task", typeof(RectTransform), typeof(CanvasGroup));
        rootGo.transform.SetParent(parent, false);
        row.Root = (RectTransform)rootGo.transform;
        Place(row.Root, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, TaskFirstY), new Vector2(CardWidth, 26f));
        row.Group = rootGo.GetComponent<CanvasGroup>();
        row.Group.alpha = 0f;

        row.Outer = NewImage("Box", row.Root, Dim);
        Place(row.Outer.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
              new Vector2(16f, 0f), new Vector2(18f, 18f));

        row.Inner = NewImage("Fill", row.Outer.transform, BoxFill);
        Fill(row.Inner.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));

        // Tick mark drawn from two thin bars so no special font glyph is needed.
        row.Tick = new GameObject("Tick", typeof(RectTransform));
        row.Tick.transform.SetParent(row.Outer.transform, false);
        Fill((RectTransform)row.Tick.transform, Vector2.zero, Vector2.zero);
        Stroke(row.Tick.transform, new Vector2(-3.5f, -1.5f), new Vector2(6f, 2.4f), -45f);
        Stroke(row.Tick.transform, new Vector2(1.5f, 0.5f), new Vector2(10.5f, 2.4f), 45f);
        row.Tick.SetActive(false);

        row.Label = NewText("Label", row.Root, 22, TextAlignmentOptions.Left, true);
        Place(row.Label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
              new Vector2(46f, 0f), new Vector2(300f, 26f));
        row.Label.text = def.Label;

        rootGo.SetActive(false);
        return row;
    }

    /// <summary>A short note that fades in, stays a few seconds and fades out. See ShowClue.</summary>
    private void BuildClue()
    {
        RectTransform card = NewCard("ClueCard", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                     new Vector2(0f, -36f), new Vector2(560f, 100f), "C L U E", CardScale);
        cluePanel = card.gameObject;
        clueGroup = cluePanel.AddComponent<CanvasGroup>();
        clueGroup.alpha = 0f;

        clueText = NewText("Clue", card, 22, TextAlignmentOptions.TopLeft, true);
        Place(clueText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
              new Vector2(16f, -38f), new Vector2(528f, 56f));
        clueText.color = ClueTint;
        clueText.text = "";

        cluePanel.SetActive(false);
    }

    /// <summary>
    /// The radio card: a little equalizer that dances while it plays, ON/OFF pill, the track name
    /// (shrinks to fit long Arabic names), a progress bar with elapsed / total time, and key caps
    /// showing the controls.
    /// </summary>
    private void BuildRadio()
    {
        RectTransform card = NewCard("RadioCard", new Vector2(1f, 0f), new Vector2(1f, 0f),
                                     new Vector2(-28f, 28f), new Vector2(CardWidth, 158f), "R A D I O", CardScale);
        radioPanel = card.gameObject;

        // Equalizer next to the title.
        radioBars = new Image[4];
        for (int i = 0; i < radioBars.Length; i++)
        {
            Image bar = NewImage("Bar", card, Dim);
            Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f),
                  new Vector2(112f + i * 7f, -30f), new Vector2(4f, 3f));
            radioBars[i] = bar;
        }

        radioPowerDot = NewImage("PowerDot", card, PowerOff);
        Place(radioPowerDot.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
              new Vector2(-84f, -22f), new Vector2(10f, 10f));

        radioPowerText = NewText("PowerLabel", card, 17, TextAlignmentOptions.TopRight, false);
        Place(radioPowerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
              new Vector2(-16f, -12f), new Vector2(60f, 22f));
        radioPowerText.text = "OFF";

        // Track name - can be Arabic and long, so it shrinks to fit.
        radioTrackText = NewText("Track", card, 25, TextAlignmentOptions.Left, true);
        Place(radioTrackText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
              new Vector2(16f, -42f), new Vector2(262f, 32f));
        radioTrackText.enableAutoSizing = true;
        radioTrackText.fontSizeMin = 14f;
        radioTrackText.fontSizeMax = 25f;
        radioTrackText.text = "";

        radioPositionText = NewText("TrackPosition", card, 15, TextAlignmentOptions.Right, false);
        Place(radioPositionText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
              new Vector2(-16f, -46f), new Vector2(60f, 24f));
        radioPositionText.color = Dim;
        radioPositionText.text = "";

        // Progress: elapsed - bar - total
        radioElapsedText = NewText("Elapsed", card, 14, TextAlignmentOptions.Left, false);
        Place(radioElapsedText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
              new Vector2(16f, -90f), new Vector2(46f, 18f));
        radioElapsedText.color = Dim;
        radioElapsedText.text = "0:00";

        Image barBg = NewImage("ProgressBg", card, new Color(1f, 1f, 1f, 0.12f));
        Place(barBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
              new Vector2(66f, -90f), new Vector2(228f, 4f));

        Image fill = NewImage("ProgressFill", barBg.transform, Accent);
        radioProgress = fill.rectTransform;
        radioProgress.anchorMin = new Vector2(0f, 0f);
        radioProgress.anchorMax = new Vector2(0f, 1f);
        radioProgress.offsetMin = Vector2.zero;
        radioProgress.offsetMax = Vector2.zero;

        radioTotalText = NewText("Total", card, 14, TextAlignmentOptions.Right, false);
        Place(radioTotalText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
              new Vector2(-16f, -90f), new Vector2(46f, 18f));
        radioTotalText.color = Dim;
        radioTotalText.text = "0:00";

        Image divider = NewImage("Divider", card, new Color(1f, 1f, 1f, 0.08f));
        Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
              new Vector2(0f, -112f), new Vector2(328f, 1f));

        // Controls as little key caps.
        BuildKey(card, "Y", "Prev", 16f);
        radioPlayLabel = BuildKey(card, "R", "Turn on", 128f);
        BuildKey(card, "T", "Next", 240f);

        radioPanel.SetActive(false);
    }

    private TMP_Text BuildKey(RectTransform parent, string key, string label, float x)
    {
        Image cap = NewImage("KeyCap_" + key, parent, Dim);
        Place(cap.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f),
              new Vector2(x, 24f), new Vector2(22f, 22f));

        Image face = NewImage("Face", cap.transform, BoxFill);
        Fill(face.rectTransform, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f));

        TMP_Text k = NewText("Key", cap.transform, 14, TextAlignmentOptions.Center, false);
        Fill(k.rectTransform, Vector2.zero, Vector2.zero);
        k.text = key;

        TMP_Text l = NewText("Label", parent, 15, TextAlignmentOptions.Left, false);
        Place(l.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f),
              new Vector2(x + 30f, 24f), new Vector2(78f, 22f));
        l.color = Dim;
        l.text = label;
        return l;
    }

    // ------------------------------------------------------------- public --

    /// <summary>Called by PlayerInteraction. Pass null to hide the prompt.</summary>
    public void SetPrompt(string prompt)
    {
        bool has = !string.IsNullOrEmpty(prompt);

        if (promptPanel != null && promptPanel.activeSelf != has)
            promptPanel.SetActive(has);

        if (has && promptText != null)
            promptText.text = $"{prompt}   <color=#EDB34D>[E]</color>";

        crosshairTarget = has ? 1.7f : 1f;
    }

    /// <summary>Redraws every state-driven element. Safe to call any time.</summary>
    public void Refresh()
    {
        RefreshTasks();
        RefreshRadio();
    }

    // ------------------------------------------------------------ refresh --

    private void RefreshTasks()
    {
        if (taskRows == null) return;

        bool animate = tasksInitialized;
        bool anyCompleted = false;

        for (int i = 0; i < taskRows.Length; i++)
        {
            TaskRow row = taskRows[i];
            bool done = row.Def.IsDone();

            if (done && !row.Done)
            {
                anyCompleted = true;
                if (animate) row.Pop = 1f;
            }

            row.Done = done;
            row.Outer.color = done ? Accent : Dim;
            row.Inner.color = done ? Accent : BoxFill;
            row.Tick.SetActive(done);
            row.Label.color = done ? Dim : Warm;
        }

        // Every finished task stays; only the first unfinished one is shown.
        bool pendingSeen = false;
        for (int i = 0; i < taskRows.Length; i++)
        {
            TaskRow row = taskRows[i];
            bool visible = row.Done || !pendingSeen;
            if (!row.Done) pendingSeen = true;

            if (visible && !row.Visible)
            {
                // A new task shows up a moment after the previous one is ticked off.
                bool delayed = animate && !row.Done && anyCompleted;
                row.RevealAt = delayed ? Time.unscaledTime + 0.9f : Time.unscaledTime - 1f;
            }

            row.Visible = visible;
        }

        UpdateTasks(0f, !tasksInitialized);
        tasksInitialized = true;
    }

    private void UpdateTasks(float dt, bool snap)
    {
        if (taskRows == null || tasksCard == null) return;

        int slot = 0;
        for (int i = 0; i < taskRows.Length; i++)
        {
            TaskRow row = taskRows[i];
            bool revealed = row.Visible && Time.unscaledTime >= row.RevealAt;
            float targetY = TaskFirstY - slot * TaskStep;

            if (!row.Placed || snap) { row.Y = targetY; row.Placed = true; }
            else row.Y = Mathf.Lerp(row.Y, targetY, dt * 10f);

            float targetAlpha = revealed ? 1f : 0f;
            row.Alpha = snap ? targetAlpha : Mathf.MoveTowards(row.Alpha, targetAlpha, dt * 3f);

            row.Pop = Mathf.MoveTowards(row.Pop, 0f, dt * 3.5f);
            row.Outer.rectTransform.localScale = Vector3.one * (1f + 0.35f * row.Pop);

            row.Root.anchoredPosition = new Vector2(0f, row.Y);
            row.Group.alpha = row.Alpha;

            bool show = revealed || row.Alpha > 0.001f;
            if (row.Root.gameObject.activeSelf != show) row.Root.gameObject.SetActive(show);

            if (revealed) slot++;
        }

        float targetHeight = 34f + Mathf.Max(slot, 1) * TaskStep + 14f;
        tasksCardHeight = snap ? targetHeight : Mathf.Lerp(tasksCardHeight, targetHeight, dt * 10f);
        tasksCard.sizeDelta = new Vector2(CardWidth, tasksCardHeight);
    }

    private void RefreshRadio()
    {
        if (radioPanel == null) return;

        bool show = GameState.HasRadio;
        if (radioPanel.activeSelf != show) radioPanel.SetActive(show);
        if (!show) return;

        RadioController radio = GameSystems.HasInstance ? GameSystems.Instance.Radio : null;
        bool playing = GameState.RadioPlaying;

        radioPowerDot.color = playing ? PowerOn : PowerOff;
        radioPowerText.text = playing ? "ON" : "OFF";
        radioPowerText.color = playing ? PowerOn : Dim;
        radioPlayLabel.text = playing ? "Turn off" : "Turn on";

        if (radio != null && radio.HasAudio)
        {
            radioTrackText.text = radio.TrackName;
            radioPositionText.text = radio.TrackPosition;
        }
        else
        {
            radioTrackText.text = "No tape loaded";
            radioPositionText.text = "";
        }
        radioTrackText.color = playing ? Warm : Dim;

        radioLastSecond = -1;   // make the time labels redraw
        UpdateRadio(0f);
    }

    /// <summary>Every frame: equalizer bars, progress bar and the time labels.</summary>
    private void UpdateRadio(float dt)
    {
        if (radioPanel == null || !radioPanel.activeSelf) return;

        RadioController radio = GameSystems.HasInstance ? GameSystems.Instance.Radio : null;
        bool playing = GameState.RadioPlaying && radio != null && radio.HasAudio;

        for (int i = 0; i < radioBars.Length; i++)
        {
            float target = playing
                ? 4f + 12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (3.1f + i * 1.3f) + i * 1.7f))
                : 3f;

            Vector2 size = radioBars[i].rectTransform.sizeDelta;
            size.y = dt <= 0f ? target : Mathf.Lerp(size.y, target, dt * 14f);
            radioBars[i].rectTransform.sizeDelta = size;
            radioBars[i].color = playing ? Accent : Dim;
        }

        float elapsed = radio != null ? radio.Elapsed : 0f;
        float duration = radio != null ? radio.Duration : 0f;
        float fraction = duration > 0.01f ? Mathf.Clamp01(elapsed / duration) : 0f;
        radioProgress.anchorMax = new Vector2(fraction, 1f);

        int second = Mathf.FloorToInt(elapsed);
        if (second != radioLastSecond)
        {
            radioLastSecond = second;
            radioElapsedText.text = FormatTime(elapsed);
            radioTotalText.text = FormatTime(duration);
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60) + ":" + (total % 60).ToString("00");
    }

    // --------------------------------------------------------------- clue --

    /// <summary>Shows a clue: fade in, stay a few seconds, fade out. A newer clue replaces an older one.</summary>
    private void ShowClue(string text)
    {
        if (cluePanel == null) return;

        clueText.text = text;
        clueTime = 0f;
        clueGroup.alpha = 0f;
        cluePanel.SetActive(true);
    }

    private void UpdateClue(float dt)
    {
        if (clueTime < 0f || cluePanel == null) return;

        clueTime += dt;

        float alpha;
        if (clueTime < ClueFadeIn) alpha = clueTime / ClueFadeIn;
        else if (clueTime < ClueFadeIn + ClueHold) alpha = 1f;
        else alpha = 1f - (clueTime - ClueFadeIn - ClueHold) / ClueFadeOut;

        if (alpha <= 0f)
        {
            clueTime = -1f;
            clueGroup.alpha = 0f;
            cluePanel.SetActive(false);
            return;
        }

        clueGroup.alpha = alpha;
    }

    // -------------------------------------------------------------- update --

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (crosshair != null)
        {
            Vector3 target = Vector3.one * crosshairTarget;
            crosshair.rectTransform.localScale = Vector3.Lerp(crosshair.rectTransform.localScale, target, dt * 14f);

            Color c = crosshair.color;
            c.a = Mathf.Lerp(c.a, crosshairTarget > 1f ? 0.95f : 0.45f, dt * 14f);
            crosshair.color = c;
        }

        UpdateTasks(dt, false);
        UpdateClue(dt);
        UpdateRadio(dt);
    }

    // ------------------------------------------------------------- utils ---

    /// <summary>
    /// The shared card: translucent dark plate, amber top edge and (optionally) a small spaced-out
    /// title. Scaling happens around the pivot, so a card pinned to a screen edge grows inward.
    /// </summary>
    private RectTransform NewCard(string name, Vector2 anchor, Vector2 pivot,
                                  Vector2 position, Vector2 size, string title, float scale)
    {
        Image bg = NewImage(name, transform, CardBg);
        Place(bg.rectTransform, anchor, pivot, position, size);
        bg.rectTransform.localScale = Vector3.one * scale;

        Image edge = NewImage("TopEdge", bg.transform, Accent);
        RectTransform er = edge.rectTransform;
        er.anchorMin = new Vector2(0f, 1f);
        er.anchorMax = new Vector2(1f, 1f);
        er.pivot = new Vector2(0.5f, 1f);
        er.anchoredPosition = Vector2.zero;
        er.sizeDelta = new Vector2(0f, 3f);

        if (!string.IsNullOrEmpty(title))
        {
            TMP_Text t = NewText("Title", bg.transform, 17, TextAlignmentOptions.TopLeft, false);
            Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(16f, -12f), new Vector2(180f, 22f));
            t.text = title;
            t.color = Dim;
        }

        return bg.rectTransform;
    }

    private static void Stroke(Transform parent, Vector2 position, Vector2 size, float degrees)
    {
        Image bar = NewImage("Stroke", parent, BoxFill);
        Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
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

    /// <summary>
    /// 'rtl' = use RTL Text Mesh Pro, which joins and reverses Arabic correctly. English text that
    /// starts with a letter is left alone by it, so it is safe for any label that starts with a letter.
    /// Labels that start with a symbol (like "[Y] prev") use plain TMP.
    /// </summary>
    private TMP_Text NewText(string name, Transform parent, float size, TextAlignmentOptions align, bool rtl)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TMP_Text t;
        if (rtl)
        {
            RTLTextMeshPro r = go.AddComponent<RTLTextMeshPro>();
            r.Farsi = false;
            t = r;
        }
        else
        {
            t = go.AddComponent<TextMeshProUGUI>();
        }

        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Warm;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot,
                              Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Fill(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }
}
