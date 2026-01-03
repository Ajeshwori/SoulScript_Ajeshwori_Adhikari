using System;
using System.Collections.Generic;

namespace SoulScript.Model;

public class Category
{
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ColorCode { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<JournalEntry> Entries { get; set; } = new List<JournalEntry>();
}
