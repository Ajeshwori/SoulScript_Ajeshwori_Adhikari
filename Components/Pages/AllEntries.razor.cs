using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using SoulScript.Data;
using SoulScript.Model;
using System;

namespace SoulScript.Components.Pages;

public partial class AllEntries
{
    [Inject] public ApplicationDbContext Db { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    private bool _isLoading = true;
    private bool _showFilters = false;
    
    // Data
    private List<JournalEntry> _entries = new();
    private List<Mood> _allMoods = new();
    private int _totalEntries = 0;

    // Filter State
    private string _sortBy = "Newest";
    private string _filterMood = "All";
    
    // Pagination
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _totalPages = 1;

    protected override async Task OnInitializedAsync()
    {
        // Load moods for filter
        _allMoods = await Db.Moods.AsNoTracking().OrderBy(m => m.Category).ThenBy(m => m.MoodName).ToListAsync();
        
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        StateHasChanged();

        var query = Db.JournalEntries.AsNoTracking().Where(e => !e.IsDeleted);

        // Apply Mood Filter
        if (!string.IsNullOrEmpty(_filterMood) && _filterMood != "All")
        {
            query = query.Where(e => e.EntryMoods.Any(em => em.Mood.MoodName == _filterMood));
        }

        // Apply Sorting
        if (_sortBy == "Oldest")
        {
            query = query.OrderBy(e => e.EntryDate);
        }
        else // Newest default
        {
            query = query.OrderByDescending(e => e.EntryDate);
        }

        // Count for pagination
        _totalEntries = await query.CountAsync();
        _totalPages = (int)Math.Ceiling(_totalEntries / (double)_pageSize);
        if (_totalPages < 1) _totalPages = 1;
        
        // Clamp page
        if (_currentPage > _totalPages) _currentPage = _totalPages;
        if (_currentPage < 1) _currentPage = 1;

        // Fetch Page
        _entries = await query
            .Include(e => e.EntryMoods).ThenInclude(em => em.Mood)
            .Include(e => e.EntryTags).ThenInclude(et => et.Tag)
            .Include(e => e.Category)
            .Skip((_currentPage - 1) * _pageSize)
            .Take(_pageSize)
            .ToListAsync();

        _isLoading = false;
        StateHasChanged();
    }

    private async Task PageChanged(int page)
    {
        _currentPage = page;
        await LoadData();
    }

    private async Task OnFilterMoodChanged(string mood)
    {
        _filterMood = mood;
        _currentPage = 1; // Reset to first page on filter change
        await LoadData();
    }

    private async Task ResetFilters()
    {
        _sortBy = "Newest";
        _filterMood = "All";
        _currentPage = 1;
        await LoadData();
    }

    private string Preview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "No content preview available.";
        return text.Length > 150 ? text[..150] + "..." : text;
    }
}
