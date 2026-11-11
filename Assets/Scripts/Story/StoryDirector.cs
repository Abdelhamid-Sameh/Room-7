using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fires the story beats that are not tied to one room: right now, waking up in the hotel after the hanging room.
/// Each beat checks the story flags, so it fires once and only in order. Lives on the persistent GameSystems object.
/// </summary>
[DisallowMultipleComponent]
public class StoryDirector : MonoBehaviour
{
    /// <summary>Radio track earned when Nabil wakes up (RadioPlaylist unlockKey).</summary>
    public const string WakeTrackKey = "Hang";

    private void OnEnable() { SceneManager.sceneLoaded += OnLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }
    private void Start() { Check(SceneManager.GetActiveScene()); }

    private void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Check(scene);
    }

    private void Check(Scene scene)
    {
        if (scene.name == PhoneUI.CorridorScene && GameState.HangDone && !StoryStateManager.HasFlag(StoryStateManager.Flags.WokeUp))
            StartCoroutine(WakeUpBeat());
        else if (scene.name == PhoneUI.CorridorScene && StoryStateManager.HasFlag(StoryStateManager.Flags.Room4Film) && !StoryStateManager.HasFlag("salmaDone"))
            StartCoroutine(SalmaBeat());
    }

    /// <summary>Back in the hotel: everything is normal. A new track, the next job, and the gift question.</summary>
    private IEnumerator WakeUpBeat()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        while (GameObject.Find("WakeUp") != null) yield return null;   // let the eyes finish opening
        yield return new WaitForSecondsRealtime(1.5f);

        StoryStateManager.SetFlag(StoryStateManager.Flags.WokeUp);   // objective "Clean room 4" appears
        GameState.UnlockedTracks.Add(WakeTrackKey);
        GameState.RaiseChanged();
        Sfx.Play(SfxKind.Ping);
        PhoneUI.Hint("New track for the radio", "نقطة بيضه - كايروكى");

        yield return new WaitForSecondsRealtime(4f);
        DialogueManager.Play(Resources.Load<ConversationData>("Story/Gift"));
    }

    /// <summary>Stepped out of the streamed cinema into the corridor.</summary>
    public static void LeftRoom4()
    {
        StoryDirector d = GameSystems.HasInstance ? GameSystems.Instance.GetComponent<StoryDirector>() : null;
        if (d != null && StoryStateManager.HasFlag(StoryStateManager.Flags.Room4Film) && !StoryStateManager.HasFlag("salmaDone"))
            d.StartCoroutine(d.SalmaBeat());
    }

    /// <summary>Out of the cinema: the Unknown Number asks if he remembers; he knows who it is, and cuts her off.</summary>
    private IEnumerator SalmaBeat()
    {
        StoryStateManager.SetFlag("salmaDone");
        yield return new WaitForSecondsRealtime(2.5f);
        const string Unk = "Unknown Number";
        yield return PhoneUI.Message(Unk, false, "افتكرت ؟", 1.5f);
        yield return PhoneUI.Message(Unk, true, "سلمى", 1.6f);
        GameState.RenameContact(Unk, "Salma");
        yield return PhoneUI.Message("Salma", false, "ايه الى غيرك يا نبيل ؟ لقيت ايه عندها ملقتوش عندى ؟", 2.2f);
        yield return PhoneUI.Message("Salma", true, "سلام يا سلمى", 1.8f);
        StoryStateManager.SetFlag("salmaBlocked");
        PhoneUI.Hint("Salma blocked", "You will not hear from her again");
    }
}
