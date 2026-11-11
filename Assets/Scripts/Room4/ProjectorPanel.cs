using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The projector's service panel: one screen, three modules, all mouse-driven.
///
///   LENS       drag the lens into its mount, then focus (drag the knurled ring / mouse wheel), frame (drag the
///              slider until no frame line shows) and level (drag the tilt dial) a test card, then LOCK.
///              Focus and framing are coupled, as on a real lens: reframing shifts the focus point.
///   LAMP       seat the bulb in the lamp house, then rotate wire tiles (left / right click) so power runs from the
///              supply, through the fixed ballast, to the lamp - with no live wire left open (open ends spark).
///   FILM PATH  put the reel on the feed spindle, thread the film through the right rollers in order (the worn
///              threading card on the left; a wrong roller snaps the film), set both loops, then turn the inching
///              knob two full turns. A bad loop makes the film chatter.
///
/// The left column shows each module's state (part missing / part in hand / done). When all three are done,
/// START runs the film. Progress is kept in story flags, a half-finished module starts over.
/// </summary>
public class ProjectorPanel : MonoBehaviour
{
    public const string LensPart = "p4Lens", BulbPart = "p4Bulb", ReelPart = "p4Reel";
    public const string LensDone = "p4LensFit", BulbDone = "p4BulbFit", ReelDone = "p4ReelFit";
    public const string Inspected = "p4Inspected";

    public static bool Open { get; private set; }
    public static int ClosedFrame { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Open = false; ClosedFrame = -1; }

    public static bool AllDone { get { return Has(LensDone) && Has(BulbDone) && Has(ReelDone); } }
    private static bool Has(string f) { return StoryStateManager.HasFlag(f); }

    // ------------------------------------------------------------------ colours --
    static readonly Color Metal = new Color(0.10f, 0.11f, 0.11f), Plate = new Color(0.16f, 0.17f, 0.17f);
    static readonly Color Amber = new Color(1f, 0.72f, 0.28f), Green = new Color(0.35f, 0.85f, 0.45f), Red = new Color(0.9f, 0.25f, 0.2f);
    static readonly Color Ink = new Color(0.85f, 0.82f, 0.74f), Faint = new Color(0.5f, 0.5f, 0.48f);

    private ProjectorRig rig;
    private TMP_FontAsset font;
    private MonoBehaviour ctrl;
    private RectTransform work;
    private readonly RectTransform[] cards = new RectTransform[3];
    private readonly TMP_Text[] cardState = new TMP_Text[3];
    private readonly TMP_Text[] cardSub = new TMP_Text[3];
    private RectTransform startBtn, closeBtn;
    private TMP_Text startLabel, hint;
    private Module current;
    private int currentIndex = -1, hover = -1;

    // ------------------------------------------------------------------ open / close --

    public static void Show(ProjectorRig rig)
    {
        if (Open) return;
        StoryStateManager.SetFlag(Inspected);
        GameState.RaiseChanged();
        GameObject go = new GameObject("ProjectorPanel", typeof(RectTransform));
        go.AddComponent<ProjectorPanel>().Build(rig);
    }

    private void Build(ProjectorRig r)
    {
        rig = r;
        Open = true;
        font = GameSystems.HasInstance && GameSystems.Instance.HUD != null ? GameSystems.Instance.HUD.Font : null;
        ctrl = PauseMenu.FindPlayerController();
        if (ctrl != null) ctrl.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 860;
        CanvasScaler s = gameObject.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f); s.matchWidthOrHeight = 0.5f;

        Image dim = PhoneUI.Img("Dim", transform, new Color(0f, 0f, 0f, 0.82f));
        PhoneUI.Fill(dim);
        RectTransform panel = Box("Panel", dim.transform, Metal, Vector2.zero, new Vector2(1840f, 1000f), true);
        Label(panel, "PROJECTOR  SERVICE", 40, Amber, new Vector2(-560f, 455f), new Vector2(700f, 60f), TextAlignmentOptions.Left);

        string[] names = { "LENS", "LAMP", "FILM PATH" };
        for (int i = 0; i < 3; i++)
        {
            RectTransform card = Box("Card" + i, panel, Plate, new Vector2(-735f, 300f - i * 190f), new Vector2(330f, 170f), true);
            Label(card, names[i], 34, Ink, new Vector2(0f, 45f), new Vector2(290f, 46f), TextAlignmentOptions.Left);
            cardState[i] = Label(card, "", 26, Amber, new Vector2(0f, -5f), new Vector2(290f, 36f), TextAlignmentOptions.Left);
            cardSub[i] = Label(card, "", 20, Faint, new Vector2(0f, -45f), new Vector2(290f, 30f), TextAlignmentOptions.Left);
            cards[i] = card;
        }
        startBtn = Box("Start", panel, Plate, new Vector2(-735f, -280f), new Vector2(330f, 110f), true);
        startLabel = Label(startBtn, "START PROJECTOR", 30, Faint, Vector2.zero, new Vector2(300f, 60f), TextAlignmentOptions.Center);
        closeBtn = Box("Close", panel, Plate, new Vector2(-735f, -420f), new Vector2(330f, 70f), true);
        Label(closeBtn, "CLOSE   (Esc)", 24, Ink, Vector2.zero, new Vector2(300f, 40f), TextAlignmentOptions.Center);

        work = Box("Work", panel, new Color(0.07f, 0.075f, 0.075f), new Vector2(170f, -20f), new Vector2(1440f, 900f), true);
        hint = Label(panel, "", 24, Faint, new Vector2(170f, -485f), new Vector2(1400f, 34f), TextAlignmentOptions.Center);

        int first = !Has(LensDone) ? 0 : !Has(BulbDone) ? 1 : !Has(ReelDone) ? 2 : 0;
        Select(first);
        RefreshCards();
    }

    private void Close()
    {
        if (current != null) current.Dispose();
        Destroy(gameObject);
        Open = false;
        ClosedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        PauseMenu.ClearStaleInput();
        if (ctrl != null) ctrl.enabled = true;
    }

    private void Select(int i)
    {
        if (current != null) current.Dispose();
        for (int k = work.childCount - 1; k >= 0; k--) Destroy(work.GetChild(k).gameObject);
        currentIndex = i;
        current = i == 0 ? (Module)new LensModule() : i == 1 ? new LampModule() : new FilmModule();
        current.Init(this, work);
        RefreshCards();
    }

    private void RefreshCards()
    {
        string[] part = { LensPart, BulbPart, ReelPart }, done = { LensDone, BulbDone, ReelDone };
        string[] where = { "the lens", "a bulb", "a film reel" };
        for (int i = 0; i < 3; i++)
        {
            bool d = Has(done[i]), p = Has(part[i]);
            cardState[i].text = d ? "DONE" : p ? "PART IN HAND" : "PART MISSING";
            cardState[i].color = d ? Green : p ? Amber : Red;
            cardSub[i].text = d ? "fitted and working" : p ? "fit and adjust it" : "find " + where[i] + " in the cinema";
            cards[i].GetComponent<Image>().color = i == currentIndex ? new Color(0.24f, 0.22f, 0.18f) : i == hover ? new Color(0.2f, 0.2f, 0.2f) : Plate;
        }
        startLabel.color = AllDone ? Amber : Faint;
        startBtn.GetComponent<Image>().color = AllDone ? new Color(0.28f, 0.2f, 0.08f) : Plate;
    }

    public void ModuleFinished()
    {
        RefreshCards();
        if (AllDone) { SetHint("Everything is fitted. Press START."); ProjectorSounds.Play(ProjectorSounds.K.Hum, 0.5f); }
        rig.RefreshModel();
    }

    public void SetHint(string s) { hint.text = s; }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse m = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (m == null) return;

        int h = -1;
        for (int i = 0; i < 3; i++) if (Over(cards[i])) h = i;
        if (h != hover) { hover = h; if (h >= 0) Sfx.Play(SfxKind.Hover, 0.6f); RefreshCards(); }

        if (m.leftButton.wasPressedThisFrame)
        {
            if (hover >= 0 && hover != currentIndex) { Sfx.Play(SfxKind.Click); Select(hover); return; }
            if (Over(closeBtn)) { Sfx.Play(SfxKind.Click); Close(); return; }
            if (Over(startBtn))
            {
                if (AllDone) { Sfx.Play(SfxKind.PowerOn); Close(); rig.StartFilm(); return; }
                ProjectorSounds.Play(ProjectorSounds.K.Buzz, 0.6f);
                SetHint("The projector is not ready - every module must say DONE.");
            }
        }
        if (current != null) current.Tick();
    }

    // ------------------------------------------------------------------ UI helpers --

    public static Vector2 MousePos { get { return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero; } }
    public static bool Over(RectTransform rt) { return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, MousePos, null); }
    public static Vector2 Local(RectTransform space)
    {
        Vector2 p; RectTransformUtility.ScreenPointToLocalPointInRectangle(space, MousePos, null, out p); return p;
    }

    public static RectTransform Box(string name, Transform parent, Color col, Vector2 pos, Vector2 size, bool rounded = false)
    {
        Image i = PhoneUI.Img(name, parent, col, rounded);
        return PhoneUI.Place(i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
    }

    public static RectTransform Dot(string name, Transform parent, Color col, Vector2 pos, float d)
    {
        Image i = PhoneUI.Img(name, parent, col);
        i.sprite = PhoneUI.Circle;
        return PhoneUI.Place(i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(d, d));
    }

    public TMP_Text Label(Transform parent, string text, float size, Color col, Vector2 pos, Vector2 box, TextAlignmentOptions align)
    {
        TMP_Text t = PhoneUI.Txt(parent, font, "Label", size, align, col, text);
        t.raycastTarget = false;
        PhoneUI.Place(t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, box);
        return t;
    }

    public static Image Line(Transform parent, Vector2 a, Vector2 b, float w, Color col)
    {
        Image i = PhoneUI.Img("Line", parent, col);
        SetLine(i, a, b, w);
        return i;
    }

    public static void SetLine(Image i, Vector2 a, Vector2 b, float w)
    {
        RectTransform rt = i.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.sizeDelta = new Vector2((b - a).magnitude, w);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
    }

    // ================================================================== modules ==

    private abstract class Module
    {
        protected ProjectorPanel P;
        protected RectTransform A;
        protected string part, done, partName;
        protected bool seated, finished;
        private RectTransform icon, socket;
        private bool dragging;
        private Vector2 home;
        protected float finishAt = -1f;

        public void Init(ProjectorPanel p, RectTransform area)
        {
            P = p; A = area;
            Setup();
            if (StoryStateManager.HasFlag(done)) { finished = true; BuildFinished(); P.SetHint(""); return; }
            if (!StoryStateManager.HasFlag(part))
            {
                P.Label(A, "The " + partName + " is missing.", 40, Red, new Vector2(0f, 40f), new Vector2(1200f, 60f), TextAlignmentOptions.Center);
                P.Label(A, "Search the cinema for it, then come back.", 28, Faint, new Vector2(0f, -20f), new Vector2(1200f, 40f), TextAlignmentOptions.Center);
                P.SetHint("");
                return;
            }
            socket = BuildSocket();
            home = new Vector2(-560f, -330f);
            P.Label(A, "PARTS TRAY", 20, Faint, home + new Vector2(0f, 90f), new Vector2(200f, 30f), TextAlignmentOptions.Center);
            Box("Tray", A, new Color(0.12f, 0.12f, 0.12f), home, new Vector2(170f, 150f), true);
            icon = BuildIcon(A, home);
            P.SetHint("Drag the " + partName + " from the tray into place.");
        }

        public void Tick()
        {
            if (finished) { if (finishAt > 0f && Time.unscaledTime > finishAt) { finishAt = -1f; P.ModuleFinished(); } return; }
            if (icon == null) return;
            Mouse m = Mouse.current;
            if (!seated)
            {
                if (m.leftButton.wasPressedThisFrame && Over(icon)) { dragging = true; ProjectorSounds.Play(ProjectorSounds.K.Tick); }
                if (dragging) icon.anchoredPosition = Local(A);
                if (dragging && m.leftButton.wasReleasedThisFrame)
                {
                    dragging = false;
                    if ((icon.anchoredPosition - socket.anchoredPosition).magnitude < 90f)
                    {
                        seated = true;
                        icon.anchoredPosition = socket.anchoredPosition;
                        ProjectorSounds.Play(ProjectorSounds.K.Seat);
                        BuildPuzzle();
                    }
                    else { icon.anchoredPosition = home; ProjectorSounds.Play(ProjectorSounds.K.Clack, 0.5f); }
                }
                return;
            }
            Puzzle();
        }

        protected void Win(string msg)
        {
            finished = true;
            StoryStateManager.SetFlag(done);
            ProjectorSounds.Play(ProjectorSounds.K.Chime);
            P.SetHint(msg);
            finishAt = Time.unscaledTime + 0.6f;
        }

        protected abstract void Setup();
        protected abstract RectTransform BuildSocket();
        protected abstract RectTransform BuildIcon(Transform parent, Vector2 pos);
        protected abstract void BuildPuzzle();
        protected abstract void Puzzle();
        protected virtual void BuildFinished()
        {
            P.Label(A, partName.ToUpperInvariant() + "  -  DONE", 44, Green, Vector2.zero, new Vector2(1200f, 60f), TextAlignmentOptions.Center);
        }
        public virtual void Dispose() { }
    }

    // ------------------------------------------------------------------ LENS --

    private class LensModule : Module
    {
        private RectTransform card, ring, tiltDial, slider, handle, lockBtn;
        private RawImage[] ghosts;
        private Image[] knurl;
        private Image pointer;
        private float f, ft, v, r, lastF, lastV, lastR;
        private int drag;   // 0 none, 1 focus, 2 frame, 3 tilt

        static readonly Vector2 Aperture = new Vector2(0f, 150f);

        protected override void Setup() { part = LensPart; done = LensDone; partName = "lens"; }

        protected override RectTransform BuildSocket()
        {
            P.Label(A, "LENS MOUNT", 20, Faint, new Vector2(-520f, 280f), new Vector2(200f, 30f), TextAlignmentOptions.Center);
            RectTransform s = Dot("Mount", A, new Color(0.03f, 0.03f, 0.03f), new Vector2(-520f, 180f), 150f);
            Dot("MountIn", s, new Color(0.12f, 0.12f, 0.13f), Vector2.zero, 120f);
            return s;
        }

        protected override RectTransform BuildIcon(Transform parent, Vector2 pos)
        {
            RectTransform i = Dot("LensIcon", parent, new Color(0.08f, 0.08f, 0.09f), pos, 116f);
            Dot("Glass", i, new Color(0.35f, 0.5f, 0.62f), Vector2.zero, 82f);
            Dot("Glint", i, new Color(0.8f, 0.9f, 1f, 0.6f), new Vector2(-16f, 16f), 18f);
            return i;
        }

        protected override void BuildPuzzle()
        {
            ft = Random.Range(0.3f, 0.7f);
            f = Mathf.Clamp01(ft + (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.18f, 0.28f));
            v = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.35f, 0.75f);
            r = (Random.value < 0.5f ? -1f : 1f) * Random.Range(8f, 18f);
            lastF = f; lastV = v; lastR = r;

            // the aperture: what the lens throws onto the screen
            RectTransform ap = Box("Aperture", A, Color.black, Aperture, new Vector2(660f, 420f));
            ap.gameObject.AddComponent<RectMask2D>();
            card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(ap, false); card.sizeDelta = new Vector2(640f, 400f);
            Texture2D tex = TestCard();
            ghosts = new RawImage[7];
            for (int i = 0; i < ghosts.Length; i++)
            {
                RawImage g = new GameObject("Ghost" + i, typeof(RectTransform)).AddComponent<RawImage>();
                g.transform.SetParent(card, false);
                g.texture = tex; g.raycastTarget = false;
                g.rectTransform.sizeDelta = new Vector2(640f, 400f);
                g.color = new Color(1f, 1f, 1f, i == 0 ? 1f : 0.32f);
                ghosts[i] = g;
            }
            // frame lines above and below the picture: they show when the framing is off
            Box("FrameLineTop", card, new Color(0.02f, 0.02f, 0.02f), new Vector2(0f, 240f), new Vector2(700f, 80f));
            Box("FrameLineBottom", card, new Color(0.02f, 0.02f, 0.02f), new Vector2(0f, -240f), new Vector2(700f, 80f));
            Box("Next", card, new Color(0.55f, 0.53f, 0.5f), new Vector2(0f, 480f), new Vector2(640f, 400f));
            Box("Prev", card, new Color(0.55f, 0.53f, 0.5f), new Vector2(0f, -480f), new Vector2(640f, 400f));
            // aperture marks
            Box("MarkL", A, Amber, Aperture + new Vector2(-345f, 0f), new Vector2(22f, 4f));
            Box("MarkR", A, Amber, Aperture + new Vector2(345f, 0f), new Vector2(22f, 4f));

            // focus ring
            P.Label(A, "FOCUS  -  drag the ring or use the mouse wheel", 20, Faint, new Vector2(0f, -105f), new Vector2(700f, 30f), TextAlignmentOptions.Center);
            ring = Box("Ring", A, new Color(0.13f, 0.13f, 0.14f), new Vector2(0f, -160f), new Vector2(660f, 70f), true);
            ring.gameObject.AddComponent<RectMask2D>();
            knurl = new Image[26];
            for (int i = 0; i < knurl.Length; i++) knurl[i] = Box("K" + i, ring, new Color(0.28f, 0.28f, 0.3f), Vector2.zero, new Vector2(8f, 60f)).GetComponent<Image>();

            // framing slider
            P.Label(A, "FRAME", 20, Faint, new Vector2(400f, 380f), new Vector2(120f, 30f), TextAlignmentOptions.Center);
            slider = Box("Track", A, new Color(0.13f, 0.13f, 0.14f), new Vector2(400f, 150f), new Vector2(26f, 400f), true);
            Box("Centre", slider, new Color(0.3f, 0.3f, 0.3f), Vector2.zero, new Vector2(40f, 2f));
            handle = Box("Handle", slider, new Color(0.55f, 0.55f, 0.57f), Vector2.zero, new Vector2(64f, 26f), true);

            // tilt dial (where the lens sits)
            P.Label(A, "TILT  -  drag sideways", 20, Faint, new Vector2(-520f, 60f), new Vector2(300f, 30f), TextAlignmentOptions.Center);
            tiltDial = Dot("Tilt", A, new Color(0.13f, 0.13f, 0.14f), new Vector2(-520f, -60f), 150f);
            pointer = Line(tiltDial, Vector2.zero, new Vector2(0f, 62f), 6f, Amber);
            pointer.rectTransform.pivot = new Vector2(0.5f, 0f);
            pointer.rectTransform.anchoredPosition = Vector2.zero;
            pointer.rectTransform.sizeDelta = new Vector2(6f, 62f);
            pointer.rectTransform.localRotation = Quaternion.identity;

            lockBtn = Box("Lock", A, new Color(0.25f, 0.19f, 0.08f), new Vector2(0f, -300f), new Vector2(320f, 76f), true);
            P.Label(lockBtn, "LOCK THE LENS", 28, Amber, Vector2.zero, new Vector2(300f, 40f), TextAlignmentOptions.Center);
            P.SetHint("Make the test card sharp, centred between the marks and level, then LOCK the lens.");
        }

        protected override void Puzzle()
        {
            Mouse m = Mouse.current;
            Vector2 delta = m.delta.ReadValue();
            if (m.leftButton.wasPressedThisFrame)
            {
                drag = Over(ring) ? 1 : (Over(slider) || Over(handle)) ? 2 : Over(tiltDial) ? 3 : 0;
                if (Over(lockBtn)) Check();
            }
            if (!m.leftButton.isPressed) drag = 0;

            float scroll = m.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) f += Mathf.Sign(scroll) * 0.006f;
            if (drag == 1) f += delta.x / 2600f;
            if (drag == 2) v = Mathf.Clamp(Local(slider).y / 180f, -1f, 1f);
            if (drag == 3) r -= delta.x * 0.06f;
            f = Mathf.Clamp01(f); r = Mathf.Clamp(r, -30f, 30f);

            if (Mathf.Abs(f - lastF) > 0.008f) { lastF = f; ProjectorSounds.Play(ProjectorSounds.K.Tick, 0.7f); }
            if (Mathf.Abs(v - lastV) > 0.06f) { lastV = v; ProjectorSounds.Play(ProjectorSounds.K.Tick, 0.5f); }
            if (Mathf.Abs(r - lastR) > 1.5f) { lastR = r; ProjectorSounds.Play(ProjectorSounds.K.Tick, 0.5f); }

            // focus point moves with framing, as it does on a real lens
            float err = f - (ft + 0.12f * v);
            float blur = Mathf.Clamp01(Mathf.Abs(err) * 7f);
            for (int i = 1; i < ghosts.Length; i++)
            {
                float a = i * Mathf.PI * 2f / (ghosts.Length - 1);
                ghosts[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * blur * 34f;
            }
            card.anchoredPosition = new Vector2(0f, v * 160f);
            card.localRotation = Quaternion.Euler(0f, 0f, r);
            handle.anchoredPosition = new Vector2(0f, v * 180f);
            pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, r * 3f);
            for (int i = 0; i < knurl.Length; i++)
                knurl[i].rectTransform.anchoredPosition = new Vector2(Mathf.Repeat(i * 26f + f * 2400f, knurl.Length * 26f) - knurl.Length * 13f, 0f);
        }

        private void Check()
        {
            bool sharp = Mathf.Abs(f - (ft + 0.12f * v)) < 0.012f, framed = Mathf.Abs(v) < 0.05f, level = Mathf.Abs(r) < 1.0f;
            if (sharp && framed && level) { ProjectorSounds.Play(ProjectorSounds.K.Seat); Win("The lens is locked. The picture is sharp."); return; }
            ProjectorSounds.Play(ProjectorSounds.K.Buzz, 0.7f);
            P.SetHint(!framed ? "A frame line is showing - centre the picture between the marks first." : !level ? "The picture is tilted." : "It's still soft. Reframing moved the focus - refocus.");
        }

        private static Texture2D tc;
        private static Texture2D TestCard()
        {
            if (tc != null) return tc;
            int w = 320, h = 200;
            tc = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color bg = new Color(0.55f, 0.53f, 0.5f), dk = new Color(0.08f, 0.08f, 0.08f), lt = new Color(0.92f, 0.9f, 0.86f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x - w / 2f, dy = y - h / 2f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = bg;
                    if (Mathf.Abs(d - 80f) < 2f || Mathf.Abs(d - 50f) < 1.5f || Mathf.Abs(d - 20f) < 1.2f) c = dk;
                    if ((Mathf.Abs(dx) < 1f || Mathf.Abs(dy) < 1f) && d < 92f) c = dk;
                    if (d < 18f) { float a = Mathf.Atan2(dy, dx); c = ((int)((a + Mathf.PI) / (Mathf.PI / 8f)) % 2 == 0) ? dk : lt; }   // star for focus
                    if ((x < 40 || x > w - 41) && ((x / 8 + y / 8) % 2 == 0)) c = dk;                                          // edge checkers
                    if (y < 6 || y > h - 7 || x < 3 || x > w - 4) c = lt;
                    tc.SetPixel(x, y, c);
                }
            tc.Apply();
            return tc;
        }
    }

    // ------------------------------------------------------------------ LAMP --

    private class LampModule : Module
    {
        const int Cols = 7, Rows = 5, LampRow = 2;
        const float Cell = 92f;
        static readonly Vector2 Grid = new Vector2(-60f, 30f);
        private int[,] mask = new int[Cols, Rows];
        private bool[,] fixedCell = new bool[Cols, Rows], dead = new bool[Cols, Rows];
        private RectTransform[,] tile = new RectTransform[Cols, Rows];
        private Image[,,] arm = new Image[Cols, Rows, 4];
        private Image[,] nub = new Image[Cols, Rows];
        private int sourceRow, pathLen, lastLeaks;
        private Image lampGlow;
        private AudioSource hum;
        private float sparkCool;

        protected override void Setup() { part = BulbPart; done = BulbDone; partName = "bulb"; }

        static Vector2 Pos(int c, int r) { return Grid + new Vector2((c - (Cols - 1) / 2f) * Cell, ((Rows - 1) / 2f - r) * Cell); }

        protected override RectTransform BuildSocket()
        {
            Vector2 lampAt = Pos(Cols - 1, LampRow) + new Vector2(170f, 0f);
            Box("LampHouse", A, new Color(0.16f, 0.2f, 0.19f), lampAt, new Vector2(170f, 240f), true);
            P.Label(A, "LAMP", 20, Faint, lampAt + new Vector2(0f, 145f), new Vector2(160f, 30f), TextAlignmentOptions.Center);
            lampGlow = Dot("Glow", A, new Color(1f, 0.85f, 0.5f, 0f), lampAt, 190f).GetComponent<Image>();
            return Dot("Socket", A, new Color(0.03f, 0.03f, 0.03f), lampAt, 90f);
        }

        protected override RectTransform BuildIcon(Transform parent, Vector2 pos)
        {
            RectTransform b = Dot("BulbIcon", parent, new Color(0.85f, 0.88f, 0.9f, 0.9f), pos, 80f);
            Box("Base", b, new Color(0.75f, 0.58f, 0.25f), new Vector2(0f, -52f), new Vector2(40f, 34f), true);
            Box("Filament", b, new Color(0.3f, 0.25f, 0.2f), Vector2.zero, new Vector2(24f, 5f));
            return b;
        }

        protected override void BuildPuzzle()
        {
            Generate();
            Vector2 src = Pos(0, sourceRow) - new Vector2(130f, 0f);
            Box("Supply", A, new Color(0.2f, 0.16f, 0.1f), src, new Vector2(110f, 110f), true);
            P.Label(A, "220 V", 24, Amber, src, new Vector2(110f, 40f), TextAlignmentOptions.Center);
            Box("SupplyLead", A, Amber, src + new Vector2(70f, 0f), new Vector2(40f, 14f));
            Box("LampLead", A, Faint, Pos(Cols - 1, LampRow) + new Vector2(70f, 0f), new Vector2(60f, 14f));

            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < Rows; r++)
                {
                    RectTransform t = Box("T" + c + r, A, dead[c, r] ? new Color(0.2f, 0.07f, 0.06f) : fixedCell[c, r] ? new Color(0.17f, 0.15f, 0.1f) : new Color(0.12f, 0.12f, 0.13f), Pos(c, r), new Vector2(Cell - 6f, Cell - 6f), true);
                    tile[c, r] = t;
                    if (dead[c, r]) { P.Label(t, "X", 40, Red, Vector2.zero, new Vector2(60f, 60f), TextAlignmentOptions.Center); continue; }
                    for (int d = 0; d < 4; d++)
                    {
                        Vector2 dir = Dir(d);
                        Image a = Box("Arm" + d, t, Faint, dir * 23f, d % 2 == 0 ? new Vector2(16f, 46f) : new Vector2(46f, 16f)).GetComponent<Image>();
                        arm[c, r, d] = a;
                    }
                    nub[c, r] = Box("Nub", t, Faint, Vector2.zero, new Vector2(24f, 24f)).GetComponent<Image>();
                    if (fixedCell[c, r]) P.Label(t, "BALLAST", 13, Amber, new Vector2(0f, -36f), new Vector2(86f, 18f), TextAlignmentOptions.Center);
                }
            hum = ProjectorSounds.Loop(A.gameObject, ProjectorSounds.K.Hum, 0f);
            Redraw();
            P.SetHint("Left / right click a tile to turn it. Power must reach the lamp through the ballast - no live wire may be left open.");
        }

        static Vector2 Dir(int d) { return d == 0 ? Vector2.up : d == 1 ? Vector2.right : d == 2 ? Vector2.down : Vector2.left; }
        static int Bit(int d) { return 1 << d; }   // N=1 E=2 S=4 W=8
        static int Rot(int m, int times) { for (int i = 0; i < times; i++) m = ((m << 1) | (m >> 3)) & 15; return m; }
        static int Opp(int d) { return (d + 2) % 4; }

        private void Generate()
        {
            List<Vector2Int> path = null;
            for (int attempt = 0; attempt < 400 && path == null; attempt++)
            {
                sourceRow = Random.Range(0, Rows);
                var p = new List<Vector2Int> { new Vector2Int(0, sourceRow) };
                bool[,] seen = new bool[Cols, Rows];
                seen[0, sourceRow] = true;
                int budget = 6000;
                if (Walk(p, seen, ref budget, 14)) path = p;
            }
            if (path == null) { path = new List<Vector2Int>(); sourceRow = LampRow; for (int c = 0; c < Cols; c++) path.Add(new Vector2Int(c, LampRow)); }   // shortcut: straight line fallback, never seen in practice
            pathLen = path.Count;

            bool[,] onPath = new bool[Cols, Rows];
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int c = path[i];
                onPath[c.x, c.y] = true;
                int inDir = i == 0 ? 3 : DirTo(c, path[i - 1]);
                int outDir = i == path.Count - 1 ? 1 : DirTo(c, path[i + 1]);
                mask[c.x, c.y] = Bit(inDir) | Bit(outDir);
            }
            Vector2Int ballast = path[path.Count / 2];
            fixedCell[ballast.x, ballast.y] = true;

            int deadLeft = 2;
            int[] shapes = { 5, 3, 7, 3, 5, 7, 3 };
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < Rows; r++)
                {
                    if (!onPath[c, r])
                    {
                        if (deadLeft > 0 && c > 0 && c < Cols - 1 && Random.value < 0.12f) { dead[c, r] = true; deadLeft--; continue; }
                        mask[c, r] = Rot(shapes[Random.Range(0, shapes.Length)], Random.Range(0, 4));
                    }
                    else if (!fixedCell[c, r]) mask[c, r] = Rot(mask[c, r], Random.Range(1, 4));
                }
        }

        private static int DirTo(Vector2Int from, Vector2Int to)
        {
            Vector2Int d = to - from;
            return d.y < 0 ? 0 : d.x > 0 ? 1 : d.y > 0 ? 2 : 3;   // rows grow downwards
        }

        private bool Walk(List<Vector2Int> p, bool[,] seen, ref int budget, int minLen)
        {
            if (--budget < 0) return false;
            Vector2Int at = p[p.Count - 1];
            if (at.x == Cols - 1 && at.y == LampRow) return p.Count >= minLen;
            int[] order = { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = Random.Range(0, i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
            foreach (int d in order)
            {
                Vector2Int n = at + (d == 0 ? new Vector2Int(0, -1) : d == 1 ? new Vector2Int(1, 0) : d == 2 ? new Vector2Int(0, 1) : new Vector2Int(-1, 0));
                if (n.x < 0 || n.y < 0 || n.x >= Cols || n.y >= Rows || seen[n.x, n.y]) continue;
                seen[n.x, n.y] = true; p.Add(n);
                if (Walk(p, seen, ref budget, minLen)) return true;
                seen[n.x, n.y] = false; p.RemoveAt(p.Count - 1);
            }
            return false;
        }

        protected override void Puzzle()
        {
            Mouse m = Mouse.current;
            bool l = m.leftButton.wasPressedThisFrame, rr = m.rightButton.wasPressedThisFrame;
            sparkCool -= Time.unscaledDeltaTime;
            if (!l && !rr) { if (lastLeaks > 0) FlickerLeaks(); return; }
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < Rows; r++)
                {
                    if (!Over(tile[c, r])) continue;
                    if (dead[c, r]) return;
                    if (fixedCell[c, r]) { ProjectorSounds.Play(ProjectorSounds.K.Buzz, 0.4f); P.SetHint("The ballast is bolted down - route the wires through it."); return; }
                    mask[c, r] = Rot(mask[c, r], l ? 1 : 3);
                    ProjectorSounds.Play(ProjectorSounds.K.Clack);
                    Redraw();
                    return;
                }
        }

        private bool[,] live = new bool[Cols, Rows];
        private bool[,,] leak = new bool[Cols, Rows, 4];

        private void Redraw()
        {
            live = new bool[Cols, Rows]; leak = new bool[Cols, Rows, 4];
            bool lit = false; int leaks = 0, count = 0; bool ballastLive = false;
            var q = new Queue<Vector2Int>();
            if ((mask[0, sourceRow] & Bit(3)) != 0 && !dead[0, sourceRow]) { live[0, sourceRow] = true; q.Enqueue(new Vector2Int(0, sourceRow)); }
            while (q.Count > 0)
            {
                Vector2Int c = q.Dequeue(); count++;
                if (fixedCell[c.x, c.y]) ballastLive = true;
                for (int d = 0; d < 4; d++)
                {
                    if ((mask[c.x, c.y] & Bit(d)) == 0) continue;
                    if (c.x == 0 && c.y == sourceRow && d == 3) continue;   // the supply lead
                    Vector2Int n = c + (d == 0 ? new Vector2Int(0, -1) : d == 1 ? new Vector2Int(1, 0) : d == 2 ? new Vector2Int(0, 1) : new Vector2Int(-1, 0));
                    if (n.x < 0 || n.y < 0 || n.x >= Cols || n.y >= Rows)
                    {
                        if (c.x == Cols - 1 && c.y == LampRow && d == 1) lit = true; else { leak[c.x, c.y, d] = true; leaks++; }
                        continue;
                    }
                    if (dead[n.x, n.y] || (mask[n.x, n.y] & Bit(Opp(d))) == 0) { leak[c.x, c.y, d] = true; leaks++; continue; }
                    if (!live[n.x, n.y]) { live[n.x, n.y] = true; q.Enqueue(n); }
                }
            }

            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < Rows; r++)
                {
                    if (dead[c, r]) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        arm[c, r, d].enabled = (mask[c, r] & Bit(d)) != 0;
                        arm[c, r, d].color = leak[c, r, d] ? Red : live[c, r] ? Amber : Faint;
                    }
                    nub[c, r].color = live[c, r] ? Amber : Faint;
                }

            if (leaks > lastLeaks && sparkCool <= 0f) { ProjectorSounds.Play(ProjectorSounds.K.Spark, 0.8f); sparkCool = 0.4f; }
            lastLeaks = leaks;
            if (hum != null) hum.volume = Mathf.Clamp01(count / (float)pathLen) * 0.25f * AudioLevels.SfxVolume;

            bool on = lit && leaks == 0 && ballastLive;
            lampGlow.color = new Color(1f, 0.85f, 0.5f, on ? 0.9f : lit ? 0.25f : 0f);
            if (lit && !on) P.SetHint(leaks > 0 ? "Power reaches the lamp, but live wires are open and sparking." : "The lamp flickers - the current must pass through the ballast.");
            if (on) Win("The lamp burns steady.");
        }

        private void FlickerLeaks()
        {
            float a = Random.value > 0.5f ? 1f : 0.3f;
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < Rows; r++)
                    for (int d = 0; d < 4; d++)
                        if (leak[c, r, d]) arm[c, r, d].color = new Color(Red.r, Red.g, Red.b, a);
            if (sparkCool <= 0f && Random.value < 0.02f) { ProjectorSounds.Play(ProjectorSounds.K.Spark, 0.35f); sparkCool = 0.6f; }
        }

        public override void Dispose() { if (hum != null) Destroy(hum); }
    }

    // ------------------------------------------------------------------ FILM --

    private class FilmModule : Module
    {
        enum Icon { Reel, Sprocket, Gate, Roller, Drum }
        struct Node { public Vector2 pos; public Icon icon; public int order; }   // order -1 = decoy

        static readonly Node[] nodes =
        {
            new Node { pos = new Vector2(170f, 330f), icon = Icon.Reel, order = 0 },
            new Node { pos = new Vector2(60f, 200f), icon = Icon.Sprocket, order = 1 },
            new Node { pos = new Vector2(250f, 210f), icon = Icon.Roller, order = -1 },
            new Node { pos = new Vector2(60f, 50f), icon = Icon.Gate, order = 2 },
            new Node { pos = new Vector2(60f, -80f), icon = Icon.Sprocket, order = 3 },
            new Node { pos = new Vector2(260f, -40f), icon = Icon.Sprocket, order = -1 },
            new Node { pos = new Vector2(180f, -150f), icon = Icon.Roller, order = 4 },
            new Node { pos = new Vector2(-50f, -170f), icon = Icon.Roller, order = -1 },
            new Node { pos = new Vector2(90f, -230f), icon = Icon.Drum, order = 5 },
            new Node { pos = new Vector2(60f, -330f), icon = Icon.Sprocket, order = 6 },
            new Node { pos = new Vector2(290f, -330f), icon = Icon.Reel, order = 7 },
        };
        static readonly Vector2 Off = new Vector2(80f, 0f);   // the diagram sits right of centre

        private RectTransform diagram, knob;
        private readonly List<int> chain = new List<int>();
        private readonly List<GameObject> segs = new List<GameObject>();
        private RectTransform[] nodeRt;
        private Image band;
        private float[] loop = new float[2], loopTarget = new float[2];
        private RectTransform[] loopHandle = new RectTransform[2];
        private Image[][] loopSegs = new Image[2][];
        private int dragLoop = -1;
        private bool threaded, turning;
        private float inched, lastAngle, tickAcc;
        private Image knobMark;

        protected override void Setup() { part = ReelPart; done = ReelDone; partName = "film reel"; }

        static Vector2 P0(int i) { return nodes[i].pos + Off; }

        protected override RectTransform BuildSocket()
        {
            Box("Body", A, new Color(0.13f, 0.17f, 0.16f), new Vector2(230f, -10f), new Vector2(560f, 840f), true);
            P.Label(A, "FEED SPINDLE", 18, Faint, P0(0) + new Vector2(0f, 95f), new Vector2(200f, 26f), TextAlignmentOptions.Center);
            return Dot("Spindle", A, new Color(0.03f, 0.03f, 0.03f), P0(0), 120f);
        }

        protected override RectTransform BuildIcon(Transform parent, Vector2 pos) { return ReelIcon(parent, pos, 120f); }

        private RectTransform ReelIcon(Transform parent, Vector2 pos, float d)
        {
            RectTransform r = Dot("Reel", parent, new Color(0.55f, 0.56f, 0.58f), pos, d);
            Dot("Film", r, new Color(0.08f, 0.07f, 0.06f), Vector2.zero, d * 0.7f);
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f;
                Dot("Hole", r, new Color(0.3f, 0.3f, 0.32f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d * 0.22f, d * 0.14f);
            }
            Dot("Hub", r, new Color(0.7f, 0.7f, 0.72f), Vector2.zero, d * 0.12f);
            return r;
        }

        private RectTransform DrawIcon(Icon ic, Vector2 pos, Transform parent)
        {
            switch (ic)
            {
                case Icon.Reel: return ReelIcon(parent, pos, 90f);
                case Icon.Sprocket:
                    {
                        RectTransform s = Dot("Sprocket", parent, new Color(0.45f, 0.45f, 0.47f), pos, 58f);
                        for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; Box("Tooth", s, new Color(0.45f, 0.45f, 0.47f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 31f, new Vector2(10f, 10f)); }
                        Dot("Hub", s, new Color(0.2f, 0.2f, 0.2f), Vector2.zero, 16f);
                        return s;
                    }
                case Icon.Gate:
                    {
                        RectTransform g = Box("Gate", parent, new Color(0.3f, 0.3f, 0.32f), pos, new Vector2(46f, 90f), true);
                        Box("Aperture", g, new Color(1f, 0.9f, 0.6f, 0.8f), Vector2.zero, new Vector2(20f, 26f));
                        return g;
                    }
                case Icon.Drum:
                    {
                        RectTransform d = Dot("Drum", parent, new Color(0.38f, 0.38f, 0.4f), pos, 74f);
                        Dot("In", d, new Color(0.22f, 0.22f, 0.24f), Vector2.zero, 46f);
                        return d;
                    }
                default:
                    {
                        RectTransform r = Dot("Roller", parent, new Color(0.5f, 0.5f, 0.52f), pos, 34f);
                        Dot("Hub", r, new Color(0.2f, 0.2f, 0.2f), Vector2.zero, 10f);
                        return r;
                    }
            }
        }

        protected override void BuildPuzzle()
        {
            // the worn threading card
            RectTransform card = Box("Guide", A, new Color(0.78f, 0.74f, 0.62f), new Vector2(-300f, 40f), new Vector2(300f, 760f), true);
            P.Label(card, "THREADING", 26, new Color(0.2f, 0.15f, 0.1f), new Vector2(0f, 345f), new Vector2(260f, 36f), TextAlignmentOptions.Center);
            string[] steps = { "Feed reel", "Upper sprocket", "leave a loop", "Gate", "leave a loop", "Intermittent", "Guide roller", "Sound drum", "Lower sprocket", "Take-up reel" };
            Icon?[] icons = { Icon.Reel, Icon.Sprocket, null, Icon.Gate, null, Icon.Sprocket, Icon.Roller, Icon.Drum, Icon.Sprocket, Icon.Reel };
            int num = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                float y = 290f - i * 62f;
                if (icons[i].HasValue) { RectTransform ic = DrawIcon(icons[i].Value, new Vector2(-95f, y), card); ic.localScale = Vector3.one * 0.5f; }
                if (!icons[i].HasValue)
                {
                    // not a roller: a reminder that the film needs slack here (set after threading)
                    P.Label(card, "(" + steps[i] + ")", 18, new Color(0.4f, 0.32f, 0.22f), new Vector2(30f, y), new Vector2(210f, 30f), TextAlignmentOptions.Left);
                    continue;
                }
                num++;
                P.Label(card, num + ".  " + steps[i], 22, new Color(0.2f, 0.15f, 0.1f), new Vector2(30f, y), new Vector2(210f, 30f), TextAlignmentOptions.Left);
            }
            P.Label(card, "film runs DOWN through the head", 16, new Color(0.35f, 0.28f, 0.2f), new Vector2(0f, -340f), new Vector2(270f, 24f), TextAlignmentOptions.Center);

            nodeRt = new RectTransform[nodes.Length];
            for (int i = 1; i < nodes.Length; i++) nodeRt[i] = DrawIcon(nodes[i].icon, P0(i), A);
            nodeRt[0] = Dot("FeedHit", A, new Color(0f, 0f, 0f, 0f), P0(0), 120f);
            band = Line(A, P0(0), P0(0), 6f, new Color(0.15f, 0.12f, 0.08f));
            band.raycastTarget = false;
            chain.Clear(); chain.Add(0);

            knob = Dot("Knob", A, new Color(0.2f, 0.2f, 0.21f), new Vector2(560f, -300f), 130f);
            Dot("KnobIn", knob, new Color(0.3f, 0.3f, 0.32f), Vector2.zero, 100f);
            knobMark = Line(knob, Vector2.zero, new Vector2(0f, 46f), 8f, Amber);
            knobMark.rectTransform.pivot = new Vector2(0.5f, 0f); knobMark.rectTransform.anchoredPosition = Vector2.zero; knobMark.rectTransform.sizeDelta = new Vector2(8f, 46f); knobMark.rectTransform.localRotation = Quaternion.identity;
            P.Label(knob, "INCHING KNOB", 18, Faint, new Vector2(0f, 95f), new Vector2(200f, 26f), TextAlignmentOptions.Center);
            knob.gameObject.SetActive(false);

            P.SetHint("Click the rollers in order to thread the film from the feed reel down to the take-up reel.");
        }

        protected override void Puzzle()
        {
            Mouse m = Mouse.current;
            Vector2 mp = Local(A);

            if (!threaded)
            {
                SetLine(band, P0(chain[chain.Count - 1]), mp, 5f);
                if (!m.leftButton.wasPressedThisFrame) return;
                for (int i = 1; i < nodes.Length; i++)
                {
                    if (!Over(nodeRt[i]) || chain.Contains(i)) continue;
                    int want = chain.Count;   // order of the next correct node
                    if (nodes[i].order == want)
                    {
                        chain.Add(i);
                        ProjectorSounds.Play(ProjectorSounds.K.Clack);
                        segs.Add(Line(A, P0(chain[chain.Count - 2]), P0(i), 5f, new Color(0.15f, 0.12f, 0.08f)).gameObject);
                        if (want == 7) Threaded();
                    }
                    else Snap();
                    return;
                }
                return;
            }

            // loops + inching
            if (m.leftButton.wasPressedThisFrame)
            {
                for (int k = 0; k < 2; k++) if (Over(loopHandle[k])) dragLoop = k;
                if (Over(knob)) { turning = true; lastAngle = Angle(mp); }
            }
            if (!m.leftButton.isPressed) { dragLoop = -1; turning = false; }
            if (dragLoop >= 0)
            {
                Vector2 a = P0(chain[1 + dragLoop]), b = P0(chain[2 + dragLoop]);
                Vector2 n = Normal(a, b);
                float s = Mathf.Clamp(Vector2.Dot(mp - (a + b) * 0.5f, n), 0f, 150f);
                if (Mathf.Abs(s - loop[dragLoop]) > 8f) ProjectorSounds.Play(ProjectorSounds.K.Tick, 0.4f);
                loop[dragLoop] = s;
                DrawLoop(dragLoop);
            }
            if (turning)
            {
                float ang = Angle(mp), d = Mathf.DeltaAngle(lastAngle, ang);
                lastAngle = ang;
                if (d < 0f) { inched -= d; tickAcc -= d; }   // clockwise only
                knobMark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -inched);
                if (tickAcc > 45f) { tickAcc = 0f; ProjectorSounds.Play(ProjectorSounds.K.Tick, 0.9f); }
                if (inched > 720f) { turning = false; Test(); }
            }
        }

        private float Angle(Vector2 mp) { Vector2 d = mp - knob.anchoredPosition; return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; }
        static Vector2 Normal(Vector2 a, Vector2 b) { Vector2 d = (b - a).normalized; Vector2 n = new Vector2(-d.y, d.x); return n.x > 0f ? -n : n; }   // loops always bulge out to the left of the head

        private void Snap()
        {
            ProjectorSounds.Play(ProjectorSounds.K.Snap);
            foreach (GameObject g in segs) if (g != null) Destroy(g);
            segs.Clear(); chain.Clear(); chain.Add(0);
            P.SetHint("The film jumped off that roller. Start again from the feed reel.");
        }

        private void Threaded()
        {
            threaded = true;
            band.enabled = false;
            for (int s = 1; s <= 2; s++) if (segs[s] != null) Destroy(segs[s]);   // the taut runs either side of the gate become loops
            for (int k = 0; k < 2; k++)
            {
                loop[k] = 0f;
                loopTarget[k] = Random.Range(55f, 105f);
                loopSegs[k] = new Image[10];
                for (int s = 0; s < 10; s++) { loopSegs[k][s] = Line(A, Vector2.zero, Vector2.zero, 5f, new Color(0.15f, 0.12f, 0.08f)); loopSegs[k][s].raycastTarget = false; }
                Vector2 a = P0(chain[1 + k]), b = P0(chain[2 + k]), mid = (a + b) * 0.5f, n = Normal(a, b);
                Dot("GaugeLo", A, new Color(0.4f, 0.8f, 0.5f, 0.6f), mid + n * (loopTarget[k] - 14f) * 0.5f, 8f);
                Dot("GaugeHi", A, new Color(0.4f, 0.8f, 0.5f, 0.6f), mid + n * (loopTarget[k] + 14f) * 0.5f, 8f);
                loopHandle[k] = Dot("LoopHandle" + k, A, Amber, mid, 30f);
                DrawLoop(k);
            }
            knob.gameObject.SetActive(true);
            P.SetHint("Pull both loops out to the green marks, then turn the inching knob clockwise two full turns.");
        }

        private void DrawLoop(int k)
        {
            Vector2 a = P0(chain[1 + k]), b = P0(chain[2 + k]), n = Normal(a, b);
            Vector2 c = (a + b) * 0.5f + n * loop[k];
            Vector2 prev = a;
            for (int s = 1; s <= 10; s++)
            {
                float t = s / 10f;
                Vector2 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
                SetLine(loopSegs[k][s - 1], prev, p, 5f);
                prev = p;
            }
            loopHandle[k].anchoredPosition = 0.25f * a + 0.5f * c + 0.25f * b;   // the curve's midpoint
        }

        private void Test()
        {
            // the drawn bulge is half the control offset, so compare in drawn units
            bool upper = Mathf.Abs(loop[0] * 0.5f - loopTarget[0] * 0.5f) < 7f, lower = Mathf.Abs(loop[1] * 0.5f - loopTarget[1] * 0.5f) < 7f;
            if (upper && lower)
            {
                ProjectorSounds.Play(ProjectorSounds.K.Motor, 0.8f);
                Win("The film runs smooth through the gate.");
                return;
            }
            inched = 0f;
            knobMark.rectTransform.localRotation = Quaternion.identity;
            ProjectorSounds.Play(ProjectorSounds.K.Chatter);
            string which = !upper ? "upper" : "lower", how = (!upper ? loop[0] < loopTarget[0] : loop[1] < loopTarget[1]) ? "too tight" : "too big";
            P.SetHint("The film chatters - the " + which + " loop is " + how + ".");
        }

        public override void Dispose() { }
    }
}
