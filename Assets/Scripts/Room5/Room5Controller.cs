using System.Collections;
using UnityEngine;

/// <summary>Names shared by the Room 5 (classroom) scripts, the objective list and the main-menu presets.</summary>
public static class Room5Data
{
    public const string Entered = "p5Entered";
    public const string Solved = "p5Solved";   // the drawer lock is open
    public const string Tape = "p5Tape";       // the cassette is taken
    public const string Thought = "p5Thought"; // Nabil's thought once every passed note is read

    // The five passed notes, in the order the rumour travelled (who wrote -> who received).
    public static readonly string[] Notes =
    {
        "Passed note, Rana to Zaki: she saw it herself, ع gave هـ half his sandwich at break. Nobody else knows.",
        "Paper plane, Zaki to Eyad: Rana told him ع loves هـ, he gave her the whole sandwich.",
        "Note by a pencil case, Eyad to Haneen: Zaki says ع and هـ love each other and walk home together every day.",
        "Crumpled note, Haneen to Hana: Eyad told her ع wrote هـ a letter and will marry her when they grow up.",
        "Note on the chalk tray, Hana to Sara: Haneen says هـ told ع she loves him in front of the whole class."
    };
    public const string Chart = "The seating chart: who sits where in the classroom, facing the board.";

    /// <summary>Desk numbers of Rana, Zaki, Eyad, Haneen, Hana, Sara: the rumour from the one who started it to the last.</summary>
    public const string Code = "425136";

    public static bool Has(string clue) { return GameState.Clues.Contains(clue); }
    public static int NotesRead { get { int n = 0; foreach (string c in Notes) if (Has(c)) n++; return n; } }
    public static bool AllNotes { get { return NotesRead == Notes.Length; } }
}

/// <summary>
/// Room 5 (the classroom) scene logic: ticks "Clean room 5" when the rubbish is gone, and gives his one thought once every passed note has been read (no phone messages in this room).
/// </summary>
public class Room5Controller : MonoBehaviour
{
    private void Start()
    {
        StoryStateManager.SetFlag(Room5Data.Entered);   // unlocks the room's tasks; no arrival notification
    }

    private void Update()
    {
        if (!StoryStateManager.HasFlag(StoryStateManager.Flags.Room5Done) && GameState.IsRoomCleaned("Room05"))
        {
            StoryStateManager.SetFlag(StoryStateManager.Flags.Room5Done);
            GameState.RaiseChanged();
        }
        if (Room5Data.AllNotes && !StoryStateManager.HasFlag(Room5Data.Thought) && !ReadableItem.Open)
        {
            StoryStateManager.SetFlag(Room5Data.Thought);
            // "Every one of them added a bit... like what they say about Malak in the hotel."
            StartCoroutine(Say(1f, "نبيل", "كل واحد زوّد حتة ... زى الكلام اللى بيتقال على ملك فى الفندق"));
        }
    }

    private IEnumerator Say(float delay, string title, string line)
    {
        yield return new WaitForSecondsRealtime(delay);
        PhoneUI.Hint(title, line);
    }
}
