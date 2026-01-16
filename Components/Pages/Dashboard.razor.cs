using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using SoulScript.Data;
using SoulScript.Model;

namespace SoulScript.Components.Pages;

public partial class Dashboard
{
    // --------------------------------------------------
    // Injected services
    // --------------------------------------------------
    [Inject] public ApplicationDbContext Db { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    // --------------------------------------------------
    // Header / user
    // --------------------------------------------------
    private string _username = "User";

    // --------------------------------------------------
    // Streaks
    // --------------------------------------------------
    private int _currentStreak;
    private int _longestStreak;

    // --------------------------------------------------
    // Totals
    // --------------------------------------------------
    private int _totalEntries;
    private int _totalWords;

    // --------------------------------------------------
    // Date range
    // --------------------------------------------------
    private DateRange _range = new(null, null);

    // --------------------------------------------------
    // Missed days
    // --------------------------------------------------
    private int _missedDaysCount;
    private List<DateTime> _missedDays = new();
    private string _missedDaysText = "—";

    // --------------------------------------------------
    // Mood analytics
    // --------------------------------------------------
    private string _mostFrequentMood = "—";
    private List<MoodStat> _moodStats = new();
    private List<MoodCategoryStat> _moodCategoryStats = new();

    // --------------------------------------------------
    // Tags
    // --------------------------------------------------
    private List<TagStat> _popularTags = new();
    private ChartSeries[] _tagComparisonSeries = [];
    private string[] _tagComparisonLabels = [];

    // Existing chart vars (kept)
    private List<ChartSeries> _mostUsedTagsSeries = new();
    private string[] _mostUsedTagsLabels = Array.Empty<string>();

    private List<ChartSeries> _tagSeries = new();
    private string[] _tagLabels = Array.Empty<string>();

    // --------------------------------------------------
    // Word trend
    // --------------------------------------------------
    private List<ChartSeries> _wordTrendSeries = new();
    private string[] _wordTrendLabels = Array.Empty<string>();

    // --------------------------------------------------
    // Recent entries
    // --------------------------------------------------
    private List<JournalEntry> _recentEntries = new();

    // --------------------------------------------------
    // Records
    // --------------------------------------------------
    private record MoodStat(string Name, MoodCategory Category, string Icon, double Percentage);
    private record MoodCategoryStat(MoodCategory Category, int Count, double Percentage);
    private record TagStat(string Name, int Count);

    // ==================================================
    // Lifecycle
    // ==================================================
    protected override async Task OnInitializedAsync()
    {
        var user = await Db.Users.AsNoTracking().FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(user?.Username))
            _username = user.Username;

        var streak = await Db.Streaks.AsNoTracking().FirstOrDefaultAsync();
        _currentStreak = streak?.CurrentStreak ?? 0;
        _longestStreak = streak?.LongestStreak ?? 0;

        await LoadAnalyticsAsync();
    }

    private async Task OnDateRangeChanged(DateRange range)
    {
        _range = range;
        await LoadAnalyticsAsync();
        await InvokeAsync(StateHasChanged);
    }

    // ==================================================
    // Analytics Orchestrator
    // ==================================================
    private async Task LoadAnalyticsAsync()
    {
        var (from, toExclusive) = GetEffectiveRange();

        var entriesQuery = Db.JournalEntries.AsNoTracking()
            .Where(e => !e.IsDeleted)
            .Where(e => e.EntryDate >= from && e.EntryDate < toExclusive);

        await CalculateMissedDays(entriesQuery, from, toExclusive);
        await LoadTotals(entriesQuery);
        await LoadMoodAnalytics(from, toExclusive);
        await LoadTagAnalytics(from, toExclusive);
        await LoadRecentEntries(entriesQuery);
        await LoadWordTrend(entriesQuery, from, toExclusive);
    }

    // ==================================================
    // Date helpers
    // ==================================================
    private (DateTime from, DateTime toExclusive) GetEffectiveRange()
    {
        var from = _range.Start?.Date ?? DateTime.Today.AddDays(-6);
        var toExclusive = (_range.End?.Date ?? DateTime.Today).AddDays(1);
        return (from, toExclusive);
    }

    private static int GetTotalDays(DateTime from, DateTime toExclusive)
        => Math.Max(1, (int)(toExclusive.Date - from.Date).TotalDays);

    // ==================================================
    // Missed days
    // ==================================================
    private async Task CalculateMissedDays(
        IQueryable<JournalEntry> entriesQuery,
        DateTime from,
        DateTime toExclusive)
    {
        var entryDays = await entriesQuery
            .Select(e => e.EntryDate.Date)
            .Distinct()
            .ToListAsync();

        var entryDaySet = entryDays.ToHashSet();
        _missedDays.Clear();

        int totalDays = GetTotalDays(from, toExclusive);

        for (int i = 0; i < totalDays; i++)
        {
            var day = from.Date.AddDays(i); // IMPORTANT: date-only
            if (!entryDaySet.Contains(day))
                _missedDays.Add(day);
        }

        _missedDaysCount = _missedDays.Count;

        _missedDaysText = _missedDaysCount == 0
            ? "0"
            : string.Join(", ", _missedDays.Take(8).Select(d => d.ToString("MMM dd"))) +
              (_missedDaysCount > 8 ? $" (+{_missedDaysCount - 8})" : "");
    }

    // ==================================================
    // Totals
    // ==================================================
    private async Task LoadTotals(IQueryable<JournalEntry> entriesQuery)
    {
        _totalEntries = await entriesQuery.CountAsync();

        _totalWords = await entriesQuery
            .Select(e => (int?)e.WordCount)
            .SumAsync() ?? 0;
    }

    // ==================================================
    // Mood analytics
    // ==================================================
    private async Task LoadMoodAnalytics(DateTime from, DateTime toExclusive)
    {
        var moodData = await Db.EntryMoods.AsNoTracking()
            .Where(em => !em.JournalEntry.IsDeleted)
            .Where(em => em.JournalEntry.EntryDate >= from && em.JournalEntry.EntryDate < toExclusive)
            .GroupBy(em => new { em.Mood.MoodName, em.Mood.Category, em.Mood.EmojiIcon })
            .Select(g => new
            {
                g.Key.MoodName,
                g.Key.Category,
                g.Key.EmojiIcon,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        int total = moodData.Sum(x => x.Count);

        if (total == 0)
        {
            _moodStats.Clear();
            _moodCategoryStats.Clear();
            _mostFrequentMood = "—";
            return;
        }

        _moodStats = moodData.Select(x => new MoodStat(
            x.MoodName,
            x.Category,
            x.EmojiIcon ?? "😐",
            (double)x.Count / total * 100
        )).ToList();

        _moodCategoryStats = moodData
            .GroupBy(x => x.Category)
            .Select(g => new MoodCategoryStat(
                g.Key,
                g.Sum(x => x.Count),
                (double)g.Sum(x => x.Count) / total * 100
            ))
            .OrderByDescending(x => x.Percentage)
            .ToList();

        _mostFrequentMood = _moodStats.First().Name;
    }

    // ==================================================
    // Tag analytics (chips + charts)
    // ==================================================
    private async Task LoadTagAnalytics(DateTime from, DateTime toExclusive)
    {
        var entryTagsQuery = Db.EntryTags.AsNoTracking()
            .Where(et => et.JournalEntryId != null && !et.JournalEntry!.IsDeleted)
            .Where(et => et.JournalEntry!.EntryDate >= from && et.JournalEntry!.EntryDate < toExclusive);

        // Popular tags (chips)
        _popularTags = await entryTagsQuery
            .GroupBy(et => et.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Join(Db.Tags.AsNoTracking(),
                x => x.TagId,
                t => t.TagID,
                (x, t) => new TagStat(t.TagName, x.Count))
            .ToListAsync();

        // Base top tags (shared labels for charts)
        var breakdown = await entryTagsQuery
            .GroupBy(et => et.Tag!.TagName)
            .Select(g => new { Tag = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();

        var labels = breakdown.Select(x => x.Tag).ToArray();
        var counts = breakdown.Select(x => (double)x.Count).ToArray();

        // Keep your existing two chart vars (if Razor still uses them)
        _mostUsedTagsLabels = labels;
        _mostUsedTagsSeries = new()
        {
            new ChartSeries { Name = "Most Used Tags", Data = counts }
        };

        _tagLabels = labels;
        _tagSeries = new()
        {
            new ChartSeries { Name = "Tag Breakdown", Data = counts }
        };

        // Comparison chart (single bar chart with 2 series)
        _tagComparisonLabels = labels;
        _tagComparisonSeries = new ChartSeries[]
        {
            new ChartSeries { Name = "Most Used Tags", Data = counts },
            new ChartSeries { Name = "Tag Breakdown", Data = counts }
        };
    }

    // ==================================================
    // Recent entries
    // ==================================================
    private async Task LoadRecentEntries(IQueryable<JournalEntry> entriesQuery)
    {
        _recentEntries = await entriesQuery
            .Include(e => e.EntryMoods).ThenInclude(em => em.Mood)
            .Include(e => e.EntryTags).ThenInclude(et => et.Tag)
            .Include(e => e.Category)
            .OrderByDescending(e => e.EntryDate)
            .Take(3)
            .ToListAsync();
    }

    // ==================================================
    // Word trend
    // ==================================================
    private async Task LoadWordTrend(
        IQueryable<JournalEntry> entriesQuery,
        DateTime from,
        DateTime toExclusive)
    {
        var wordsByDay = await entriesQuery
            .GroupBy(e => e.EntryDate.Date)
            .Select(g => new { Day = g.Key, Words = g.Sum(x => x.WordCount) })
            .ToListAsync();

        var map = wordsByDay.ToDictionary(x => x.Day, x => x.Words);
        int days = GetTotalDays(from, toExclusive);

        _wordTrendLabels = Enumerable.Range(0, days)
            .Select(i => from.Date.AddDays(i).ToString("MMM dd"))
            .ToArray();

        _wordTrendSeries = new()
        {
            new ChartSeries
            {
                Name = "Words/day",
                Data = Enumerable.Range(0, days)
                    .Select(i =>
                    {
                        var day = from.Date.AddDays(i);
                        return (double)(map.TryGetValue(day, out var w) ? w : 0);
                    })
                    .ToArray()
            }
        };
    }

    // ==================================================
    // UI helpers
    // ==================================================
    private string GetMoodColor(MoodCategory cat) => cat switch
    {
        MoodCategory.Positive => "#FFC107",
        MoodCategory.Neutral => "#9E9E9E",
        MoodCategory.Negative => "#FF7043",
        _ => "#E0E0E0"
    };
}
