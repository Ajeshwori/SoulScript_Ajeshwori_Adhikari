using Microsoft.EntityFrameworkCore;
using SoulScript.Data;
using SoulScript.Model;

namespace SoulScript.Services;

public class EntryQueryService
{
    private readonly ApplicationDbContext _db;

    public EntryQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<JournalEntry>> SearchAsync(
        string? searchText,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<string>? moodNames,
        IReadOnlyCollection<string>? tagNames)
    {
        var q = _db.JournalEntries
            .AsNoTracking()
            .Include(e => e.EntryMoods).ThenInclude(em => em.Mood)
            .Include(e => e.EntryTags).ThenInclude(et => et.Tag)
            .Include(e => e.Category)
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var s = searchText.Trim().ToLower();

            q = q.Where(e =>
                (e.Title ?? "").ToLower().Contains(s) ||
                (e.Content ?? "").ToLower().Contains(s) ||
                e.EntryTags.Any(et => (et.Tag.TagName ?? "").ToLower().Contains(s)));
        }

        if (fromDate is not null)
            q = q.Where(e => e.EntryDate >= fromDate.Value.Date);

        if (toDate is not null)
            q = q.Where(e => e.EntryDate <= toDate.Value.Date);

        // Mood filter (OR)
        if (moodNames is not null && moodNames.Count > 0)
        {
            var moods = moodNames
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim().ToLower())
                .Distinct()
                .ToList();

            if (moods.Count > 0)
            {
                q = q.Where(e =>
                    e.EntryMoods.Any(em => moods.Contains(em.Mood.MoodName.ToLower())));
            }
        }

        // Tag filter (OR)
        if (tagNames is { Count: > 0 })
        {
            var tags = tagNames
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLower())
                .Distinct()
                .ToList();

            if (tags.Count > 0)
            {
                q = q.Where(e =>
                    e.EntryTags.Any(et =>
                        tags.Contains(((et.Tag.TagName ?? "").Trim().ToLower()))
                    ));
            }
        }


        return await q
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync();
    }
}
