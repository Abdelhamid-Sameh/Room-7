using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes the conversations as ScriptableObject assets under Assets/Resources/Story. Run it once (and again after
/// editing the text below); the assets themselves can also be edited by hand in the Inspector.
/// </summary>
public static class StoryDataBuilder
{
    const string Dir = "Assets/Resources/Story";

    static DialogueLine Say(string speaker, bool fromPlayer, string text, float delay = 1.4f)
    {
        return new DialogueLine { speaker = speaker, fromPlayer = fromPlayer, text = text, delay = delay };
    }

    static ConversationData Make(string name, string finishedFlag, params DialogueLine[] lines)
    {
        string path = Dir + "/" + name + ".asset";
        ConversationData c = AssetDatabase.LoadAssetAtPath<ConversationData>(path);
        if (c == null) { c = ScriptableObject.CreateInstance<ConversationData>(); AssetDatabase.CreateAsset(c, path); }
        c.lines = lines;
        c.flagWhenFinished = finishedFlag;
        EditorUtility.SetDirty(c);
        return c;
    }

    [MenuItem("Room Seven/Build story data")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        // CHOICE 1: after waking up, does Nabil ask whether she threw the gift away?
        ConversationData asked = Make("GiftAsked", null,
            Say("Malak", false, "لا طبعًا !!", 1.8f),
            Say("Malak", false, "ايه الى خلاك تسألنى السؤال ده ؟!", 1.5f),
            Say("Malak", true, "ولا حاجه متشغليش بالِك. آسف إنى سألت", 2.2f));

        Make("Gift", StoryStateManager.Flags.GiftDone,
            new DialogueLine
            {
                speaker = "Malak",
                choices = new[]
                {
                    new ChoiceOption { label = "انتي رميتى الهدية ؟", setFlag = StoryStateManager.Flags.AskedGift, next = asked },
                    new ChoiceOption { label = "", setFlag = StoryStateManager.Flags.GiftSkipped },
                }
            });

        AssetDatabase.SaveAssets();
        Debug.Log("built story data");
    }
}
