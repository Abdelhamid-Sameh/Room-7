using System;
using UnityEngine;

/// <summary>One answer the player can pick. The label is sent as Nabil's text; an empty label means "say nothing".</summary>
[Serializable]
public class ChoiceOption
{
    public string label;
    [Tooltip("Story flag set when this answer is picked.")] public string setFlag;
    [Tooltip("Conversation that continues from this answer (optional).")] public ConversationData next;
}

/// <summary>One message. A line with choices is a question put to the player instead of a message.</summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("Contact name on the phone.")] public string speaker = "Malak";
    [Tooltip("True when Nabil sends it.")] public bool fromPlayer;
    [TextArea(1, 3)] public string text;
    [Tooltip("Seconds to wait before it arrives.")] public float delay = 1.3f;
    [Tooltip("Flag set after this line.")] public string setFlag;
    [Tooltip("Fill this to make the line a choice.")] public ChoiceOption[] choices;
}

/// <summary>
/// One conversation as data: the story lives in these assets (Assets/Resources/Story), not in scripts, so it can be
/// rewritten without touching code. DialogueManager plays it; StoryStateManager records its flags.
/// </summary>
[CreateAssetMenu(fileName = "NewConversation", menuName = "Room 7/Conversation")]
public class ConversationData : ScriptableObject
{
    public DialogueLine[] lines;
    [Tooltip("Flag set when the whole conversation has played.")] public string flagWhenFinished;
}
