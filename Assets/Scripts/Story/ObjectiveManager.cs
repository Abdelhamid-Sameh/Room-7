using System;

/// <summary>
/// The player's objectives, in story order. The HUD task list is built from this table: a group is on screen
/// together, a task ticks itself when its IsDone turns true, and a group whose Unlocked is false stays hidden
/// until the story gets there. To add an objective, add a row here - nothing else.
/// </summary>
public static class ObjectiveManager
{
    public struct Objective
    {
        public int Group;
        public string Label;
        public Func<bool> IsDone;
        public Func<bool> Unlocked;   // null = always available
    }

    public static readonly Objective[] All =
    {
        new Objective { Group = 0, Label = "Enter the staff room",       IsDone = () => GameState.HasVisited("StaffRoom") },
        new Objective { Group = 1, Label = "Put on your uniform",        IsDone = () => GameState.UniformWorn },
        new Objective { Group = 1, Label = "Take the cleaning supplies", IsDone = () => GameState.HasCleaningKit },
        new Objective { Group = 1, Label = "Take the radio",             IsDone = () => GameState.HasRadio },
        new Objective { Group = 2, Label = "Clean room 1",               IsDone = () => GameState.IsRoomCleaned("Room01") },
        new Objective { Group = 2, Label = "Clean room 2",               IsDone = () => GameState.R2Done },
        new Objective { Group = 2, Label = "Clean room 3",               IsDone = () => GameState.IsRoomCleaned("Room03") },
        // after the hanging room: back in the hotel, everything is normal again
        new Objective { Group = 3, Label = "Clean room 4",
                        IsDone = () => StoryStateManager.HasFlag(StoryStateManager.Flags.Room4Done),
                        Unlocked = () => StoryStateManager.HasFlag(StoryStateManager.Flags.WokeUp) },
        // the cinema's projector: these appear once Nabil has looked at it (or picked up one of its parts)
        new Objective { Group = 3, Label = "Find the projector lens", IsDone = () => StoryStateManager.HasFlag(ProjectorPanel.LensPart), Unlocked = () => StoryStateManager.HasFlag(ProjectorPanel.Inspected) },
        new Objective { Group = 3, Label = "Find a projector bulb",   IsDone = () => StoryStateManager.HasFlag(ProjectorPanel.BulbPart), Unlocked = () => StoryStateManager.HasFlag(ProjectorPanel.Inspected) },
        new Objective { Group = 3, Label = "Find a film reel",        IsDone = () => StoryStateManager.HasFlag(ProjectorPanel.ReelPart), Unlocked = () => StoryStateManager.HasFlag(ProjectorPanel.Inspected) },
        new Objective { Group = 3, Label = "Repair the projector",    IsDone = () => ProjectorPanel.AllDone, Unlocked = () => StoryStateManager.HasFlag(ProjectorPanel.Inspected) },
        new Objective { Group = 3, Label = "Watch the film",          IsDone = () => StoryStateManager.HasFlag(StoryStateManager.Flags.Room4Film), Unlocked = () => StoryStateManager.HasFlag(ProjectorPanel.Inspected) },
    };

    /// <summary>The first objective that is unlocked and not done yet ("" when there is none).</summary>
    public static string Current
    {
        get
        {
            foreach (Objective o in All)
                if ((o.Unlocked == null || o.Unlocked()) && !o.IsDone()) return o.Label;
            return "";
        }
    }
}
