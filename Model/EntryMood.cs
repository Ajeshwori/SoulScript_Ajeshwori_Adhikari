using System;

namespace SoulScript.Model;

public class EntryMood
{
    public int EntryMoodId { get; set; }

    public int JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;

    public int MoodId { get; set; }
    public Mood Mood { get; set; } = null!;

    // True = primary mood; False = secondary mood
    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }
}
