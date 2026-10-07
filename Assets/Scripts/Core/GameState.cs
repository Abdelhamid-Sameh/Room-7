using System;
using System.Collections.Generic;

/// <summary>
/// Everything the player has picked up or changed, held in memory for a single
/// play session. Nothing is written to disk, so quitting the game resets it.
///
/// When the real save / load system arrives, serialise the fields of this class
/// instead of moving them somewhere else - every other script only talks to
/// GameState, never to the save system directly.
///
/// Always call <see cref="RaiseChanged"/> after mutating a value so the HUD repaints.
/// </summary>
public static class GameState
{
    /// <summary>Known SpawnPoint ids. Must match the ids used in the scenes.</summary>
    public const string SpawnStart = "Start";
    public const string SpawnFromCorridor = "FromCorridor";
    public const string SpawnStaffRoomDoor = "StaffRoom_Door";

    /// <summary>The SpawnPoint the player should be moved to on the next scene load.</summary>
    public static string LastSpawnId = SpawnStart;

    // --- inventory -------------------------------------------------------
    /// <summary>Without the cleaning kit Nabil cannot clean anything.</summary>
    public static bool HasCleaningKit;
    public static bool HasRadio;

    // --- the uniform -----------------------------------------------------
    public static bool UniformTaken;
    public static bool UniformWorn;

    /// <summary>The three things Nabil collects in the staff room before his first room.</summary>
    public static bool HasAllThings => HasCleaningKit && HasRadio && UniformWorn;

    // --- rooms -------------------------------------------------------------
    /// <summary>Names of every scene the player has been in. 'Enter the staff room' is done by this.</summary>
    public static readonly HashSet<string> VisitedScenes = new HashSet<string>();

    public static bool HasVisited(string scene) { return VisitedScenes.Contains(scene); }

    public static void Visit(string scene)
    {
        if (!string.IsNullOrEmpty(scene) && VisitedScenes.Add(scene)) RaiseChanged();
    }

    /// <summary>Rooms whose every untidy thing has been put right.</summary>
    public static readonly HashSet<string> CleanedRooms = new HashSet<string>();

    public static bool IsRoomCleaned(string roomId) { return CleanedRooms.Contains(roomId); }

    public static void SetRoomCleaned(string roomId)
    {
        if (!string.IsNullOrEmpty(roomId) && CleanedRooms.Add(roomId)) RaiseChanged();
    }

    /// <summary>'Room01/Chair' style keys of everything already tidied, so a room stays tidy when you come back.</summary>
    public static readonly HashSet<string> TidiedItems = new HashSet<string>();

    // --- live tidy read-out for the room the player is in (set by TidyRoom, drawn by the HUD) ---
    public static string TidyRoomLabel = "";
    public static int TidyDone;
    public static int TidyTotal;

    // --- narrative flags -------------------------------------------------
    /// <summary>Nabil looked at Malak's uniform in her locker.</summary>
    public static bool MalakUniformInspected;

    // --- clues -------------------------------------------------------------
    public const string ClueMalakUniform = "Malak's uniform is still folded in her locker.";

    /// <summary>
    /// Every clue the player has found, in the order found. The HUD flashes each one once when it
    /// is added; the pause-menu clue journal (later) should read this list.
    /// </summary>
    public static readonly List<string> Clues = new List<string>();

    /// <summary>Raised once per new clue, with its text.</summary>
    public static event Action<string> ClueAdded;

    /// <summary>Records a clue. Returns false (and does nothing) if it was already found.</summary>
    public static bool AddClue(string text)
    {
        if (string.IsNullOrEmpty(text) || Clues.Contains(text)) return false;

        Clues.Add(text);
        ClueAdded?.Invoke(text);
        RaiseChanged();
        return true;
    }

    // --- locker doors (so they stay open / closed across scene loads) -----
    public static bool NabilLockerOpen;
    public static bool MalakLockerOpen;

    // --- radio -------------------------------------------------------------
    /// <summary>Index into RadioController's track list.</summary>
    public static int RadioTrackIndex;
    public static bool RadioPlaying;

    /// <summary>Fired whenever any value above changes. HUD + widgets subscribe.</summary>
    public static event Action Changed;

    public static void RaiseChanged()
    {
        Changed?.Invoke();
    }

    /// <summary>Debug helper - clears everything back to a fresh play session.</summary>
    public static void ResetSession()
    {
        LastSpawnId = SpawnStart;

        HasCleaningKit = false;
        HasRadio = false;

        UniformTaken = false;
        UniformWorn = false;

        VisitedScenes.Clear();
        CleanedRooms.Clear();
        TidiedItems.Clear();
        TidyRoomLabel = "";
        TidyDone = 0;
        TidyTotal = 0;

        MalakUniformInspected = false;
        Clues.Clear();

        NabilLockerOpen = false;
        MalakLockerOpen = false;

        RadioTrackIndex = 0;
        RadioPlaying = false;

        RaiseChanged();
    }
}
