using System;
using System.Collections.Generic;

namespace SoulScript.Model;

public class Tag
{
    public int TagID { get; set; }
    public string TagName { get; set; } = string.Empty;

    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsPrebuilt { get; set; }

    public ICollection<EntryTag> EntryTags { get; set; } = new List<EntryTag>();
}
