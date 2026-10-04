using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen furniture: crosshair, contextual prompt, inventory read-out, clue
/// line and the carried-radio widget.
///
/// Everything here is built from code (see Build) so no scene wiring is
/// needed and the layout can be re-skinned in one file. Replace the generated
/// hierarchy with a designer-made prefab whenever the UI gets its final look.
/// </summary>
public class HUDController : MonoBehaviour
{
    private static Font cachedFont;

    private Image crosshair;
    private float crosshairTarget = 1f;

    private GameObject promptPanel;
    private Text promptText;

    private Text inventoryText;
    private GameObject cluePanel;
    private Text clueText;

    private GameObject radioPanel;
    private Image radioPowerDot;
    private Text radioTitleText;
    private Text radioPowerText;
    private Text radioTrackText;
    private Text radioPositionText;
    private Text radioHintText;

    private static readonly Color Warm = new Color(0.95f, 0.91f, 0.82f);
    private static readonly Color Dim = new Color(0.55f, 0.53f, 0.47f);
    private static readonly Color Scrim = new Color(0f, 0f, 0f, 0.38f);
    private static readonly Color Accent = new Color(0.93f, 0.70f, 0.30f);
    private static readonly Color PowerOn = new Color(0.47f, 0.86f, 0.49f);
    private static readonly Color PowerOff = new Color(0.40f, 0.38f, 0.34f);

    // -------------------------------------------------------------- build --

    public void Build()
    {
        BuildCrosshair();
        BuildPrompt();
        BuildInventory();
        BuildClue();
        BuildRadio();
    }

    private void BuildCrosshair()
    {
        Image dot = NewImage("Crosshair", transform, Warm);
        Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
              Vector2.zero, new Vector2(9f, 9f));
        dot.raycastTarget = false;
        crosshair = dot;
    }

    private void BuildPrompt()
    {
        Image panel = NewImage("PromptPanel", transform, Scrim);
        Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
              new Vector2(0f, 175f), new Vector2(1100f, 66f));
        panel.raycastTarget = false;
        promptPanel = panel.gameObject;

        promptText = NewText("Prompt", panel.transform, 30, TextAnchor.MiddleCenter);
        Fill(promptText.rectTransform, new Vector2(20f, 4f), new Vector2(-20f, -4f));
        promptText.text = "";

        // Static control reminder, always visible.
        Text hint = NewText("Controls", transform, 20, TextAnchor.MiddleCenter);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
              new Vector2(0f, 122f), new Vector2(900f, 40f));
        hint.text = "E  /  Left Click - interact";
        SetAlpha(hint, 0.55f);

        promptPanel.SetActive(false);
    }

    private void BuildInventory()
    {
        Image panel = NewImage("InventoryPanel", transform, Scrim);
        Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
              new Vector2(28f, -28f), new Vector2(430f, 142f));
        panel.raycastTarget = false;

        inventoryText = NewText("Inventory", panel.transform, 24, TextAnchor.UpperLeft);
        Fill(inventoryText.rectTransform, new Vector2(18f, 14f), new Vector2(-14f, -12f));
        inventoryText.text = "";
    }

    private void BuildClue()
    {
        Image panel = NewImage("CluePanel", transform, Scrim);
        Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
              new Vector2(28f, -182f), new Vector2(720f, 76f));
        panel.raycastTarget = false;
        cluePanel = panel.gameObject;

        clueText = NewText("Clue", panel.transform, 21, TextAnchor.MiddleLeft);
        Fill(clueText.rectTransform, new Vector2(18f, 6f), new Vector2(-18f, -6f));
        clueText.text = "";
        clueText.color = new Color(0.93f, 0.78f, 0.48f);

        cluePanel.SetActive(false);
    }

    /// <summary>
    /// A small "cassette player" card: amber top edge, a power dot + ON/OFF,
    /// the current track name and position, and a key-hint row at the bottom.
    /// </summary>
    private void BuildRadio()
    {
        Image panel = NewImage("RadioPanel", transform, new Color(0.07f, 0.07f, 0.08f, 0.74f));
        Place(panel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
              new Vector2(-28f, 28f), new Vector2(360f, 132f));
        panel.raycastTarget = false;
        radioPanel = panel.gameObject;

        // Amber top edge - gives the card a bit of "hardware" identity.
        Image edge = NewImage("TopEdge", panel.transform, Accent);
        Place(edge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
              Vector2.zero, new Vector2(360f, 3f));

        // Header row: RADIO  ................  [dot] ON/OFF
        radioTitleText = NewText("Title", panel.transform, 17, TextAnchor.UpperLeft);
        Place(radioTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
              new Vector2(16f, -12f), new Vector2(140f, 22f));
        radioTitleText.text = "R A D I O";
        radioTitleText.color = Dim;

        radioPowerDot = NewImage("PowerDot", panel.transform, PowerOff);
        Place(radioPowerDot.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
              new Vector2(-84f, -22f), new Vector2(10f, 10f));

        radioPowerText = NewText("PowerLabel", panel.transform, 17, TextAnchor.UpperRight);
        Place(radioPowerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
              new Vector2(-16f, -12f), new Vector2(60f, 22f));
        radioPowerText.text = "OFF";

        // Track name, big and warm.
        radioTrackText = NewText("Track", panel.transform, 25, TextAnchor.MiddleLeft);
        Place(radioTrackText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
              new Vector2(16f, -42f), new Vector2(260f, 32f));
        radioTrackText.text = "";

        radioPositionText = NewText("TrackPosition", panel.transform, 15, TextAnchor.MiddleRight);
        Place(radioPositionText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
              new Vector2(-16f, -46f), new Vector2(60f, 24f));
        radioPositionText.color = Dim;
        radioPositionText.text = "";

        // Thin divider.
        Image divider = NewImage("Divider", panel.transform, new Color(1f, 1f, 1f, 0.08f));
        Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
              new Vector2(0f, -80f), new Vector2(328f, 1f));

        // Key hints.
        radioHintText = NewText("Hints", panel.transform, 16, TextAnchor.LowerLeft);
        Place(radioHintText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
              new Vector2(16f, 10f), new Vector2(328f, 26f));
        radioHintText.color = Dim;
        radioHintText.text = "[Y] prev      [R] on/off      [T] next";

        radioPanel.SetActive(false);
    }

    // ------------------------------------------------------------- public --

    /// <summary>Called by PlayerInteraction when the crosshair target changes.</summary>
    public void SetPrompt(string prompt)
    {
        bool has = !string.IsNullOrEmpty(prompt);

        if (promptPanel != null && promptPanel.activeSelf != has)
            promptPanel.SetActive(has);

        if (has && promptText != null)
            promptText.text = $"{prompt}   <color=#B9AE95>[E]</color>";

        crosshairTarget = has ? 1.7f : 1f;
    }

    /// <summary>Redraws every state-driven element. Safe to call any time.</summary>
    public void Refresh()
    {
        RefreshInventory();
        RefreshClue();
        RefreshRadio();
    }

    // ------------------------------------------------------------ refresh --

    private void RefreshInventory()
    {
        if (inventoryText == null) return;

        inventoryText.text =
            Line(GameState.HasCleaningKit, "Cleaning supplies") + "\n" +
            Line(GameState.HasRadio, "Radio") + "\n" +
            Line(GameState.UniformWorn, "Uniform (worn)");
    }

    private static string Line(bool owned, string label)
    {
        return owned
            ? $"<color=#F2E9D8>[x] {label}</color>"
            : $"<color=#857F70>[ ] {label}</color>";
    }

    private void RefreshClue()
    {
        if (cluePanel == null) return;

        bool show = GameState.MalakUniformInspected;
        if (cluePanel.activeSelf != show) cluePanel.SetActive(show);

        if (show && clueText != null)
            clueText.text = "Clue: Malak's uniform is still folded in her locker.";
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
    }

    private void Update()
    {
        if (crosshair == null) return;

        Vector3 target = Vector3.one * crosshairTarget;
        crosshair.rectTransform.localScale =
            Vector3.Lerp(crosshair.rectTransform.localScale, target, Time.deltaTime * 14f);

        Color c = crosshair.color;
        c.a = Mathf.Lerp(c.a, crosshairTarget > 1f ? 0.95f : 0.45f, Time.deltaTime * 14f);
        crosshair.color = c;
    }

    // ------------------------------------------------------------- utils ---

    private static Font DefaultFont
    {
        get
        {
            if (cachedFont != null) return cachedFont;

            try
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                cachedFont = null;
            }

            if (cachedFont == null)
                cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 24);

            return cachedFont;
        }
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

    private static Text NewText(string name, Transform parent, int size, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = DefaultFont;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Warm;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
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
