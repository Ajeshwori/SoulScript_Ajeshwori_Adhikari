using System.Collections.Generic;

namespace SoulScript.Model;

public enum MoodCategory
{
    Positive,
    Neutral,
    Negative
}

public class Mood
{
    public int MoodId { get; set; }
    public string MoodName { get; set; } = string.Empty;
    public string? EmojiIcon { get; set; }
    public MoodCategory Category { get; set; }

    // Navigation
    public ICollection<EntryMood> EntryMoods { get; set; } = new List<EntryMood>();
}
