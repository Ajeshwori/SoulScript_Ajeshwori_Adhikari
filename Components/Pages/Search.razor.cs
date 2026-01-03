using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using SoulScript.Data;
using SoulScript.Model;
using SoulScript.Services;
using MudBlazor;

namespace SoulScript.Components.Pages;

public partial class Search
{
    [Inject] public ApplicationDbContext Db { get; set; } = default!;
    [Inject] public EntryQueryService QueryService { get; set; } = default!;

    private string? _searchText;
    private DateRange _range = new(null, null);

    private List<string> _selectedMoods = new();
    private List<string> _selectedTags = new();

    private List<Mood> _allMoods = new();
    private List<string> _allTags = new();

    private List<JournalEntry>? _results;
    private int _totalEntries;
    private bool _isLoading;

    private CancellationTokenSource? _debounceCts;

    protected override async Task OnInitializedAsync()
    {
        _allMoods = await Db.Moods.AsNoTracking().OrderBy(m => m.MoodName).ToListAsync();

        _allTags = await Db.Tags.AsNoTracking()
            .Select(t => t.TagName.ToLower())
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(); // Removed double OrderBy from original

        _totalEntries = await Db.JournalEntries.CountAsync(e => !e.IsDeleted);

        await RunSearch();
    }

    private async Task RunSearch()
    {
        _isLoading = true;
        // Ensure UI updates to show loading spinner
        StateHasChanged(); 
        
        try
        {
            _results = await QueryService.SearchAsync(
                _searchText,
                _range.Start,
                _range.End,
                _selectedMoods.Count > 0 ? _selectedMoods : null,
                _selectedTags.Count > 0 ? _selectedTags : null);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task ClearFilters()
    {
        _searchText = null;
        _range = new DateRange(null, null);
        _selectedMoods.Clear();
        _selectedTags.Clear();
        StateHasChanged();
        await RunSearch();
    }

    private void OnSearchTextChanged(string? value)
    {
        _searchText = value;
        DebounceRunSearch();
    }

    private async Task OnRangeChanged(DateRange range)
    {
        _range = range;
        await RunSearch();
    }

    private async Task ToggleMood(string moodName)
    {
        if (_selectedMoods.Contains(moodName))
        {
            _selectedMoods.Remove(moodName);
        }
        else
        {
            _selectedMoods.Add(moodName);
        }
        StateHasChanged();
        await RunSearch();
    }

    private async Task ToggleTag(string tagName)
    {
        if (_selectedTags.Contains(tagName))
        {
            _selectedTags.Remove(tagName);
        }
        else
        {
            _selectedTags.Add(tagName);
        }
        StateHasChanged();
        await RunSearch();
    }

    private async Task OnMoodsChanged(IReadOnlyCollection<string> values)
    {
        _selectedMoods = values.ToList();
        StateHasChanged();
        await RunSearch();
    }

    private async Task OnTagsChanged(IReadOnlyCollection<string> values)
    {
        _selectedTags = values.ToList();
        StateHasChanged();
        await RunSearch();
    }

    private void DebounceRunSearch(int ms = 500)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ms, token);
                if (!token.IsCancellationRequested)
                    await InvokeAsync(RunSearch);
            }
            catch { }
        }, token);
    }

    private async Task HandleApplyFilters()
    {
        await RunSearch();
    }

    private async Task HandleClearFilters()
    {
        await ClearFilters();
    }

    private static string Preview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Trim();
        return t.Length > 180 ? t[..180] + "..." : t;
    }
}
