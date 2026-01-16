using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using SoulScript.Data;
using SoulScript.Model;
using SoulScript.Services;
using System;
using System.Net;
using System.Text.RegularExpressions;

namespace SoulScript.Components.Pages;

public partial class Search
{
    [Inject] public ApplicationDbContext Db { get; set; } = default!;
    [Inject] public EntryQueryService QueryService { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;
    [Inject] public IDialogService DialogService { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;

    private string? _searchText;
    private DateRange _range = new(null, null);

    private List<string> _selectedMoods = new();
    private List<string> _selectedTags = new();

    private List<Mood> _allMoods = new();
    private List<string> _allTags = new();

    private List<JournalEntry> _results = new();
    private int _totalEntries;
    private bool _isLoading;

    // Pagination (like AllEntries)
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _totalPages = 1;

    private CancellationTokenSource? _debounceCts;

    private IEnumerable<JournalEntry> PagedResults =>
        _results.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);

    protected override async Task OnInitializedAsync()
    {
        _allMoods = await Db.Moods.AsNoTracking()
            .OrderBy(m => m.Category)
            .ThenBy(m => m.MoodName)
            .ToListAsync();

        _allTags = await Db.Tags.AsNoTracking()
            .Select(t => t.TagName.ToLower())
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        _totalEntries = await Db.JournalEntries.CountAsync(e => !e.IsDeleted);

        await RunSearch();
    }

    private async Task RunSearch()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var list = await QueryService.SearchAsync(
                _searchText,
                _range.Start,
                _range.End,
                _selectedMoods.Count > 0 ? _selectedMoods : null,
                _selectedTags.Count > 0 ? _selectedTags : null);

            _results = list ?? new List<JournalEntry>();

            // update pagination
            _totalPages = (int)Math.Ceiling(_results.Count / (double)_pageSize);
            if (_totalPages < 1) _totalPages = 1;

            // clamp current page
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;
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

        _selectedMoods = new List<string>();
        _selectedTags = new List<string>();

        _currentPage = 1;

        StateHasChanged();
        await RunSearch();
    }

    private void OnSearchTextChanged(string? value)
    {
        _searchText = value;
        _currentPage = 1;
        DebounceRunSearch();
    }

    private async Task OnRangeChanged(DateRange range)
    {
        _range = range;
        _currentPage = 1;
        await RunSearch();
    }

    private async Task ToggleMood(string moodName)
    {
        if (_selectedMoods.Contains(moodName))
            _selectedMoods.Remove(moodName);
        else
            _selectedMoods.Add(moodName);

        _currentPage = 1;
        StateHasChanged();
        await RunSearch();
    }

    private async Task ToggleTag(string tagName)
    {
        if (_selectedTags.Contains(tagName))
            _selectedTags.Remove(tagName);
        else
            _selectedTags.Add(tagName);

        _currentPage = 1;
        StateHasChanged();
        await RunSearch();
    }

    private async Task OnMoodsChanged(IReadOnlyCollection<string> values)
    {
        _selectedMoods = values.ToList();
        _currentPage = 1;
        StateHasChanged();
        await RunSearch();
    }

    private async Task OnTagsChanged(IReadOnlyCollection<string> values)
    {
        _selectedTags = values.ToList();
        _currentPage = 1;
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
        _currentPage = 1;
        await RunSearch();
    }

    private async Task HandleClearFilters()
    {
        await ClearFilters();
    }

    private async Task PageChanged(int page)
    {
        _currentPage = page;
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task DeleteEntryFromList(JournalEntry e)
    {
        bool? ok = await DialogService.ShowMessageBox(
            "Delete entry",
            $"Delete entry for {e.EntryDate:MMM dd, yyyy}? This can’t be undone.",
            yesText: "Delete",
            cancelText: "Cancel"
        );

        if (ok != true) return;

        var entry = await Db.JournalEntries.FirstOrDefaultAsync(x => x.JournalEntryId == e.JournalEntryId);
        if (entry is null) return;

        entry.IsDeleted = true;
        entry.UpdatedAt = DateTime.Now;

        await Db.SaveChangesAsync();
        Snackbar.Add("Entry deleted.", Severity.Success);

        await RunSearch();
    }

    private static string Preview(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = Regex.Replace(html, "<.*?>", string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }
}
