using System;

namespace SoulScript.Model;

public class Streak
{
    public int StreakID { get; set; } = 1; // single row
    public int CurrentStreak { get; set; }
    public DateTime? LastEntryDate { get; set; } // store date only (use .Date)
    public int LongestStreak { get; set; }
    public string? MissedDays { get; set; } // CSV: "2025-12-01,2025-12-05"
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
