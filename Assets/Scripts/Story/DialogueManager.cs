using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Plays ConversationData. Incoming and outgoing messages go through the phone (a toast per message, and the
/// pause-menu Messages tab keeps the history); a choice line pauses the story and shows the answers at the bottom
/// of the screen, picked with 1 / 2 / 3. Picking an answer sends it as Nabil's text, sets its flag and may start
/// another conversation.
///
/// Data (ConversationData) and display (PhoneUI) stay separate; this script is the logic between them.
/// Lives on the persistent GameSystems object.
/// </summary>
[DisallowMultipleComponent]
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    /// <summary>True while any conversation is running.</summary>
    public static bool IsPlaying { get; private set; }

    private TMP_FontAsset font;
    private Transform root;
    private int running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; IsPlaying = false; }

    public void Build(TMP_FontAsset fontAsset)
    {
        font = fontAsset;
        Instance = this;

        GameObject go = new GameObject("DialogueCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 846;   // above the phone toasts, below the pause menu and the fader
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = go.transform;
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; IsPlaying = false; }
    }

    /// <summary>Starts a conversation. Does nothing when the data or the manager is missing.</summary>
    public static void Play(ConversationData data)
    {
        if (Instance != null && data != null) Instance.StartCoroutine(Instance.RunTop(data));
    }

    private IEnumerator RunTop(ConversationData data)
    {
        running++;
        IsPlaying = true;
        yield return Run(data);
        running--;
        IsPlaying = running > 0;
    }

    private IEnumerator Run(ConversationData data)
    {
        foreach (DialogueLine line in data.lines)
        {
            if (line.choices != null && line.choices.Length > 0)
            {
                int picked = 0;
                yield return Choose(line, i => picked = i);
                ChoiceOption option = line.choices[picked];
                if (!string.IsNullOrEmpty(option.label)) yield return PhoneUI.Message(line.speaker, true, option.label, 0.3f);
                if (!string.IsNullOrEmpty(option.setFlag)) StoryStateManager.SetFlag(option.setFlag);
                if (option.next != null) yield return Run(option.next);
            }
            else
            {
                yield return PhoneUI.Message(line.speaker, line.fromPlayer, line.text, line.delay);
            }
            if (!string.IsNullOrEmpty(line.setFlag)) StoryStateManager.SetFlag(line.setFlag);
        }
        if (!string.IsNullOrEmpty(data.flagWhenFinished)) StoryStateManager.SetFlag(data.flagWhenFinished);
    }

    // ---------------------------------------------------------------- choice --

    private IEnumerator Choose(DialogueLine line, Action<int> pick)
    {
        MonoBehaviour ctrl = PauseMenu.FindPlayerController();
        if (ctrl != null) ctrl.enabled = false;   // Nabil stands still while he decides what to send

        int n = Mathf.Min(line.choices.Length, 3);
        const float RowH = 70f, W = 860f;

        Image card = PhoneUI.Img("Choices", root, PhoneUI.CardBg);
        PhoneUI.Place(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(W, 64f + n * RowH));
        Image edge = PhoneUI.Img("TopEdge", card.transform, PhoneUI.Accent);
        PhoneUI.Place(edge, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(W, 3f));
        PhoneUI.Place(PhoneUI.Txt(card.transform, font, "Title", 18, TextAlignmentOptions.Left, PhoneUI.Dim, "S E N D   T O   " + line.speaker.ToUpperInvariant()),
                      new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(600f, 26f));

        for (int i = 0; i < n; i++)
        {
            float y = -46f - i * RowH;
            Image chip = PhoneUI.Img("Key", card.transform, PhoneUI.Accent);
            PhoneUI.Place(chip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(46f, 46f));
            Image face = PhoneUI.Img("Fill", chip.transform, PhoneUI.BoxFill);
            PhoneUI.Fill(face, 2f);
            PhoneUI.Fill(PhoneUI.Txt(face.transform, font, "Num", 28, TextAlignmentOptions.Center, PhoneUI.Warm, (i + 1).ToString()));

            string label = string.IsNullOrEmpty(line.choices[i].label) ? "متبعتش حاجه" : line.choices[i].label;
            TMP_Text t = PhoneUI.Txt(card.transform, font, "Option", 28, TextAlignmentOptions.Right, PhoneUI.Warm, label);
            PhoneUI.Place(t, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(84f, y), new Vector2(W - 104f, 46f));
        }

        int result = -1;
        while (result < 0)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) result = 0;
                else if (n > 1 && (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)) result = 1;
                else if (n > 2 && (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)) result = 2;
            }
            yield return null;
        }

        Destroy(card.gameObject);
        Sfx.Play(SfxKind.Click);
        PauseMenu.ClearStaleInput();
        if (ctrl != null) ctrl.enabled = true;
        pick(result);
    }
}
