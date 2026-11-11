using System;
using System.Collections.Generic;

/// <summary>
/// The story flags: every beat that has happened ("hangDone", "askedGift", ...). Anything that depends on the
/// story (objectives, doors, dialogue, the ending) asks HasFlag; anything that advances it calls SetFlag.
///
/// Static, so it lives for the whole play session and carries across scene loads (the same job a
/// DontDestroyOnLoad manager would do). GameState.ResetSession clears it. Setting a flag also raises
/// GameState.Changed so the HUD objectives repaint.
/// </summary>
public static class StoryStateManager
{
    /// <summary>Every flag name in one place, so a typo is a compile error instead of a stuck story.</summary>
    public static class Flags
    {
        public const string HangDone = "hangDone";       // the hanging room has played out
        public const string WokeUp = "wokeUp";           // Nabil is back in the hotel, the radio track is earned
        public const string AskedGift = "askedGift";     // CHOICE: he asked Malak whether she threw the gift away
        public const string GiftSkipped = "giftSkipped"; // CHOICE: he kept quiet
        public const string GiftDone = "giftDone";       // the gift exchange is over; Room 4 may open
        public const string Room4Done = "room4Done";
        public const string Room4Film = "room4Film";
    }

    private static readonly HashSet<string> flags = new HashSet<string>();

    /// <summary>Raised once each time a flag is newly set.</summary>
    public static event Action<string> OnFlagSet;

    public static bool HasFlag(string flag) { return !string.IsNullOrEmpty(flag) && flags.Contains(flag); }

    public static void SetFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return;   // each beat fires once
        OnFlagSet?.Invoke(flag);
        GameState.RaiseChanged();
    }

    public static IEnumerable<string> All { get { return flags; } }

    public static void Clear() { flags.Clear(); }
}
