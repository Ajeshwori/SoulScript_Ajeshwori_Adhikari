using System;
using System.Collections.Generic;

namespace SoulScript.Model;

public class JournalEntry
{
    public int JournalEntryId { get; set; }

    // One entry per day (always save as .Date)
    public DateTime EntryDate { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public int WordCount { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<EntryTag> EntryTags { get; set; } = new List<EntryTag>();
    public ICollection<EntryMood> EntryMoods { get; set; } = new List<EntryMood>();
}
