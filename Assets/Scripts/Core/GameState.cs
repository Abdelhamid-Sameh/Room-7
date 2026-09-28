using System;

/// <summary>
/// Everything the player has picked up or changed, held in memory for a single
/// play session. Nothing is written to disk, so quitting the game resets it.
///
/// When the real save / load system arrives (Assignment 3+), serialise the
/// fields of this class instead of moving them somewhere else - every other
/// script only talks to GameState, never to the save system directly.
///
/// Always call <see cref="RaiseChanged"/> after mutating a value so the HUD
/// repaints.
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

    // --- narrative flags -------------------------------------------------
    /// <summary>Nabil looked inside Malak's locker and saw her uniform.</summary>
    public static bool MalakUniformInspected;

    // --- locker doors (so they stay open / closed across scene loads) -----
    public static bool NabilLockerOpen;
    public static bool MalakLockerOpen;

    // --- radio -----------------------------------------------------------
    public static int RadioStation;
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

        MalakUniformInspected = false;

        NabilLockerOpen = false;
        MalakLockerOpen = false;

        RadioStation = 0;
        RadioPlaying = false;

        RaiseChanged();
    }
}
