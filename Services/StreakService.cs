using Microsoft.EntityFrameworkCore;
using SoulScript.Data;
using SoulScript.Model;

namespace SoulScript.Services;

public class StreakService
{
    private readonly ApplicationDbContext _db;

    public StreakService(ApplicationDbContext db) => _db = db;

    public async Task<Streak> RecalculateAsync()
    {
        var dates = await _db.JournalEntries.AsNoTracking()
            .Where(e => !e.IsDeleted)
            .Select(e => e.EntryDate.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        var streak = await _db.Streaks.FirstOrDefaultAsync(s => s.StreakID == 1)
                     ?? new Streak { StreakID = 1 };

        if (dates.Count == 0)
        {
            streak.CurrentStreak = 0;
            streak.LongestStreak = 0;
            streak.LastEntryDate = null;
            streak.MissedDays = null;
            streak.UpdatedAt = DateTime.Now;

            if (_db.Entry(streak).State == EntityState.Detached)
                _db.Streaks.Add(streak);

            await _db.SaveChangesAsync();
            return streak;
        }

        // Longest streak across all history
        int longest = 1, run = 1;
        for (int i = 1; i < dates.Count; i++)
        {
            run = (dates[i] == dates[i - 1].AddDays(1)) ? run + 1 : 1;
            if (run > longest) longest = run;
        }

        // Current streak ending today (or yesterday if no entry today)
        var set = dates.ToHashSet();
        int current = 0;
        var today = DateTime.Today;

        if (set.Contains(today))
        {
            while (set.Contains(today.AddDays(-current))) current++;
        }
        else if (set.Contains(today.AddDays(-1)))
        {
            while (set.Contains(today.AddDays(-(current + 1)))) current++;
        }

        // Missed days between first entry and today
        var first = dates.First();
        var missed = new List<string>();
        for (var d = first; d <= today; d = d.AddDays(1))
        {
            if (!set.Contains(d))
                missed.Add(d.ToString("yyyy-MM-dd"));
        }

        streak.CurrentStreak = current;
        streak.LongestStreak = longest;
        streak.LastEntryDate = dates.Last();
        streak.MissedDays = missed.Count == 0 ? null : string.Join(",", missed);
        streak.UpdatedAt = DateTime.Now;

        if (_db.Entry(streak).State == EntityState.Detached)
            _db.Streaks.Add(streak);

        await _db.SaveChangesAsync();
        return streak;
    }

    public static List<string> ParseMissedDays(string? missedDaysCsv)
        => string.IsNullOrWhiteSpace(missedDaysCsv)
            ? new List<string>()
            : missedDaysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
}
