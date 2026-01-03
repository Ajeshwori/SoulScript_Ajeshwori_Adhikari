using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using SoulScript.Data;
using SoulScript.Model;

namespace SoulScript.Components.Pages;

public partial class Home
{
    [Inject] public ApplicationDbContext Db { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    private int _currentStreak;
    private int _longestStreak;

    private int _totalEntries;
    private int _totalWords;

    // Charts
    private List<ChartSeries> _tagSeries = new();
    private string[] _tagLabels = Array.Empty<string>();

    private List<ChartSeries> _wordTrendSeries = new();
    private string[] _wordTrendLabels = Array.Empty<string>();

    private List<MoodStat> _moodStats = new();
    private List<TagStat> _popularTags = new();
    private List<JournalEntry> _recentEntries = new();

    protected override async Task OnInitializedAsync()
    {
        // 1) Streaks
        var streak = await Db.Streaks.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StreakID == 1);

        _currentStreak = streak?.CurrentStreak ?? 0;
        _longestStreak = streak?.LongestStreak ?? 0;

        // 2) Totals
        _totalEntries = await Db.JournalEntries.AsNoTracking()
            .CountAsync(e => !e.IsDeleted);

        // 3) Total words (simple estimate)
        var contents = await Db.JournalEntries.AsNoTracking()
            .Where(e => !e.IsDeleted)
            .Select(e => e.Content ?? "")
            .ToListAsync();

        _totalWords = contents.Sum(c =>
            c.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length);

        // 4) Mood distribution / frequent moods
        var moodData = await Db.EntryMoods.AsNoTracking()
            .Where(em => !em.JournalEntry.IsDeleted)
            .GroupBy(em => new { em.Mood.MoodName, em.Mood.Category, em.Mood.EmojiIcon })
            .Select(g => new { g.Key.MoodName, g.Key.Category, g.Key.EmojiIcon, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        int totalMoods = moodData.Sum(x => x.Count);
        if (totalMoods > 0)
        {
            _moodStats = moodData.Select(x => new MoodStat(
                x.MoodName,
                x.Category,
                x.EmojiIcon ?? "😐",
                (double)x.Count / totalMoods * 100
            )).ToList();
        }

        // 5) Most used tags (top 5)
        _popularTags = await Db.EntryTags.AsNoTracking()
            .Where(et => et.JournalEntryId != null && !et.JournalEntry!.IsDeleted)
            .GroupBy(et => et.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Join(Db.Tags.AsNoTracking(),
                x => x.TagId,
                t => t.TagID,
                (x, t) => new TagStat(t.TagName, x.Count))
            .ToListAsync();

        // 6) Recent entries
        _recentEntries = await Db.JournalEntries.AsNoTracking()
            .Where(e => !e.IsDeleted)
            .Include(e => e.EntryMoods).ThenInclude(em => em.Mood)
            .Include(e => e.EntryTags).ThenInclude(et => et.Tag)
            .Include(e => e.Category)
            .OrderByDescending(e => e.EntryDate)
            .Take(3)
            .ToListAsync();

        // 7) Tag breakdown chart (top 8)
        var tagBreakdown = await Db.EntryTags.AsNoTracking()
            .Where(et => et.JournalEntryId != null && !et.JournalEntry!.IsDeleted)
            .GroupBy(et => et.Tag!.TagName)
            .Select(g => new { Tag = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();

        _tagLabels = tagBreakdown.Select(x => x.Tag).ToArray();
        _tagSeries = new List<ChartSeries>
        {
            new ChartSeries
            {
                Name = "Tag usage",
                Data = tagBreakdown.Select(x => (double)x.Count).ToArray()
            }
        };

        // 8) Word count trend (last 7 days)
        var start = DateTime.Today.AddDays(-6);

        var wordsByDay = await Db.JournalEntries.AsNoTracking()
            .Where(e => !e.IsDeleted && e.EntryDate.Date >= start && e.EntryDate.Date <= DateTime.Today)
            .GroupBy(e => e.EntryDate.Date)
            .Select(g => new { Day = g.Key, Words = g.Sum(x => x.WordCount) })
            .ToListAsync();

        var map = wordsByDay.ToDictionary(x => x.Day, x => x.Words);

        _wordTrendLabels = Enumerable.Range(0, 7)
            .Select(i => start.AddDays(i).ToString("MMM dd"))
            .ToArray();

        _wordTrendSeries = new List<ChartSeries>
        {
            new ChartSeries
            {
                Name = "Words/day",
                Data = Enumerable.Range(0, 7)
                    .Select(i => (double)(map.TryGetValue(start.AddDays(i), out var w) ? w : 0))
                    .ToArray()
            }
        };
    }

    private string GetMoodColor(MoodCategory cat) => cat switch
    {
        MoodCategory.Positive => "#FFC107",
        MoodCategory.Neutral => "#9E9E9E",
        MoodCategory.Negative => "#FF7043",
        _ => "#E0E0E0"
    };

    private string Preview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "No content...";
        return text.Length > 100 ? text[..100] + "..." : text;
    }

    private record MoodStat(string Name, MoodCategory Category, string Icon, double Percentage);
    private record TagStat(string Name, int Count);
}
