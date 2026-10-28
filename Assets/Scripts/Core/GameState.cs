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
    public const string ClueMalakUniform = "Malak's uniform is folded in her locker.";

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

        // The safe code reads 21, 08, 02: keep those digit clues in that order however they were found.
        int rank = DigitClueRank(text);
        int at = rank < 0 ? -1 : Clues.FindIndex(c => DigitClueRank(c) > rank);
        if (at >= 0) Clues.Insert(at, text); else Clues.Add(text);
        ClueAdded?.Invoke(text);
        RaiseChanged();
        return true;
    }

    private static int DigitClueRank(string c) { return c.StartsWith("21 - ") ? 0 : c.StartsWith("08 - ") ? 1 : c.StartsWith("A cinema ticket") ? 2 : -1; }

    // --- phone messages ---------------------------------------------------
    public struct ChatMessage { public string Contact; public string Text; public bool FromPlayer; }

    /// <summary>Every text message, oldest first. The pause-menu Messages tab reads this.</summary>
    public static readonly List<ChatMessage> Messages = new List<ChatMessage>();

    /// <summary>The opening conversation with the Manager has played.</summary>
    public static bool IntroDone;

    /// <summary>Nabil has texted Malak - her contact only exists from then on.</summary>
    public static bool MalakContacted;

    public static void AddMessage(string contact, string text, bool fromPlayer)
    {
        Messages.Add(new ChatMessage { Contact = contact, Text = text, FromPlayer = fromPlayer });
        RaiseChanged();
    }

    // --- room 2: the looping corridor ---------------------------------------
    /// <summary>How many times Nabil has left Room 2 through the FRONT door (0-2). Leaving by the door he came in through does not count.</summary>
    public static int R2Loops;
    public static bool HasFlashlight, FlashlightOn, HasFuse;
    /// <summary>The fuse is in: the power is back and Room 2 is finished for good.</summary>
    public static bool R2Done;
    public static bool R2SideOpen;      // the door into the electricity room has been opened
    public static bool R2Replied;       // Malak has answered, after the last exit
    /// <summary>The whole floor is without power: dark corridor, no street noise, every room but Room 2 locked.</summary>
    public static bool PowerOut;

    // --- after room 3: the Unknown Number and the strange corridor ---
    /// <summary>Nabil has left the hotel for the strange corridor (no HUD cards, dead radio, no street noise).</summary>
    public static bool Limbo;
    /// <summary>The lights are back on in the strange corridor, so its hallway sound may play.</summary>
    public static bool LimboLit;
    /// <summary>The Unknown Number's first conversation has played.</summary>
    public static bool UnknownTexted;
    /// <summary>The Unknown Number's texts in the Game Boy room have played.</summary>
    public static bool UnknownGameBoyTexted;
    /// <summary>The Game Boy maze has been won.</summary>
    public static bool GameBoySolved;
    /// <summary>The radio is dead in a blackout and in the strange corridor.</summary>
    public static bool RadioDead => PowerOut || Limbo;

    /// <summary>Radio tracks earned so far (playlist 'unlockKey').</summary>
    public static readonly HashSet<string> UnlockedTracks = new HashSet<string>();
    public static bool IsTrackUnlocked(string key) { return string.IsNullOrEmpty(key) || UnlockedTracks.Contains(key); }

    public static void CountLoop()
    {
        if (R2Done || R2Loops >= 2) return;
        if (R2Loops == 1 && !PowerOut) return;   // the blackout has to have happened (it needs the flashlight) before the loop can go on
        R2Loops++;
        if (R2Loops >= 2) PowerOut = true;   // the blackout is on whether or not he waited for it
        RaiseChanged();
    }

    // --- room 3: the four connected rooms ---------------------------------------
    /// <summary>Open hinged doors (id -> signed angle), so they stay open across scene loads.</summary>
    public static readonly Dictionary<string, float> DoorAngles = new Dictionary<string, float>();
    public static bool R3SafeOpen;
    /// <summary>What is waiting inside the open safe ('GameBoy', 'Rock', or empty).</summary>
    public static string R3SafeItem = "";
    public static bool R3MainDone, R3EggDone;     // 210802 / 010503 have each been used

    // --- artefacts (pause-menu Artefacts tab) ------------------------------------
    public static readonly List<string> Artifacts = new List<string>();
    public static bool HasArtifact(string id) { return Artifacts.Contains(id); }

    public static void AddArtifact(string id)
    {
        if (string.IsNullOrEmpty(id) || Artifacts.Contains(id)) return;
        Artifacts.Add(id);
        RaiseChanged();
    }

    public static string ArtifactTitle(string id)
    {
        switch (id) { case "GameBoy": return "Red Game Boy"; case "Rock": return "Rock"; default: return id; }
    }

    public static string ArtifactNote(string id)
    {
        switch (id)
        {
            case "GameBoy": return "A red retro console. The screen is dark, but the buttons are worn smooth - someone played this a lot.";
            case "Rock": return "A rock or حجر in arabic";
            default: return "";
        }
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
        Messages.Clear();
        IntroDone = false;
        MalakContacted = false;

        NabilLockerOpen = false;
        MalakLockerOpen = false;

        R2Loops = 0;
        HasFlashlight = false; FlashlightOn = false; HasFuse = false;
        R2Done = false; R2SideOpen = false; R2Replied = false; PowerOut = false;
        Limbo = false; LimboLit = false; UnknownTexted = false; UnknownGameBoyTexted = false; GameBoySolved = false;
        UnlockedTracks.Clear();

        DoorAngles.Clear();
        R3SafeOpen = false; R3SafeItem = ""; R3MainDone = false; R3EggDone = false;
        Artifacts.Clear();

        RadioTrackIndex = 0;
        RadioPlaying = false;

        RaiseChanged();
    }
}
