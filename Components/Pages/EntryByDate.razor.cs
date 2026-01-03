using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using Markdig;
using SoulScript.Data;
using SoulScript.Model;
using SoulScript.Services;

namespace SoulScript.Components.Pages;

public partial class EntryByDate
{
    [Inject] public ApplicationDbContext Db { get; set; } = default!;
    [Inject] public JournalEntryService EntryService { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public StreakService StreakService { get; set; } = default!;

    [Parameter] public string DateText { get; set; } = "";

    private bool _isLoading = true;
    private bool _invalidDate;
    private bool _isFuture;
    private bool _isCreating;

    private DateTime _date;
    private JournalEntry? _entry;

    private string EntryTitle { get; set; } = "";
    private string EntryContent { get; set; } = "";

    private string Category = "Personal";
    private List<string> Tags = new();
    private List<string> PredefinedTags = new() { "Work", "Health", "Travel", "Family", "Friends" };

    private string PrimaryFeeling = "";
    private List<string> SecondaryFeelings = new();

    private List<Mood> Moods = new();

    // NEW: editor-set handshake (prevents timing issues)
    private bool _shouldSetEditor;
    private string _editorTextToSet = "";

    private readonly MarkdownPipeline _pipeline =
        new MarkdownPipelineBuilder().UseSoftlineBreakAsHardlineBreak().Build();

    protected override async Task OnParametersSetAsync()
    {
        await LoadPageAsync();
    }

    // NEW: run JS only after render (DOM exists)
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldSetEditor)
        {
            _shouldSetEditor = false;
            await JS.InvokeVoidAsync("setEditorContent", _editorTextToSet);
        }
    }

    private async Task LoadPageAsync()
    {
        _isLoading = true;

        _invalidDate = false;
        _isFuture = false;
        _isCreating = false;
        _entry = null;

        // Parse route param
        if (!DateTime.TryParse(DateText, out _date))
        {
            _invalidDate = true;
            _isLoading = false;
            return;
        }

        _date = _date.Date;

        if (_date > DateTime.Today)
        {
            _isFuture = true;
            _isLoading = false;
            return;
        }

        // Load moods once
        if (Moods.Count == 0)
        {
            Moods = await Db.Moods.AsNoTracking()
                .OrderBy(m => m.Category)
                .ThenBy(m => m.MoodName)
                .ToListAsync();
        }

        // Replace default tags with top 5 from DB (optional)
        if (PredefinedTags.Count == 5 && PredefinedTags.Contains("Work"))
        {
            PredefinedTags = await Db.EntryTags.AsNoTracking()
                .GroupBy(et => et.Tag.TagName)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(5)
                .ToListAsync();
        }
        else if (PredefinedTags.Count == 0)
        {
            PredefinedTags = await Db.EntryTags.AsNoTracking()
                .GroupBy(et => et.Tag.TagName)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(5)
                .ToListAsync();
        }

        // Load entry
        _entry = await Db.JournalEntries
            .Include(e => e.EntryTags).ThenInclude(et => et.Tag)
            .Include(e => e.EntryMoods).ThenInclude(em => em.Mood)
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => !e.IsDeleted && e.EntryDate == _date);

        if (_entry is null)
        {
            ResetEditorState();

            // schedule editor clear AFTER render
            _editorTextToSet = "";
            _shouldSetEditor = true;

            _isLoading = false;
            return;
        }

        // Fill UI state from entry
        EntryTitle = _entry.Title;
        EntryContent = _entry.Content;
        Category = _entry.Category?.CategoryName ?? "Personal";

        Tags = _entry.EntryTags.Select(t => t.Tag.TagName).ToList();

        PrimaryFeeling =
            _entry.EntryMoods.FirstOrDefault(m => m.IsPrimary)?.Mood.MoodName ?? "";

        SecondaryFeelings =
            _entry.EntryMoods.Where(m => !m.IsPrimary)
                .Select(m => m.Mood.MoodName)
                .Take(2)
                .ToList();

        // schedule editor set AFTER render
        _editorTextToSet = EntryContent ?? "";
        _shouldSetEditor = true;

        _isLoading = false;
    }

    private async Task CreateEntryForThisDay()
    {
        _isCreating = true;

        ResetEditorState();

        // schedule editor clear AFTER render
        _editorTextToSet = "";
        _shouldSetEditor = true;
    }

    private void ResetEditorState()
    {
        EntryTitle = "";
        EntryContent = "";
        Category = "Personal";
        Tags = new();
        PrimaryFeeling = "";
        SecondaryFeelings = new();
    }

    private async Task FormatText(string command)
    {
        await JS.InvokeVoidAsync("editorFormat", command);
        EntryContent = await JS.InvokeAsync<string>("getEditorContent");
    }

    private async Task OnEditorInput()
        => EntryContent = await JS.InvokeAsync<string>("getEditorContent");

    private async Task OnEditorKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await JS.InvokeVoidAsync("continueListIfNeeded");
    }

    private async Task SaveEntry()
    {
        if (string.IsNullOrWhiteSpace(PrimaryFeeling))
        {
            Snackbar.Add("Select a primary mood.", Severity.Error);
            return;
        }

        EntryContent = await JS.InvokeAsync<string>("getEditorContent");

        // NEW: guard to prevent accidental blank overwrite
        if (_entry is not null && string.IsNullOrWhiteSpace(EntryContent))
        {
            Snackbar.Add("Editor is empty (previous text may not have loaded). Reload and try again.", Severity.Error);
            return;
        }

        await EntryService.SaveForDateAsync(
            _date,
            EntryTitle,
            EntryContent,
            Category,
            Tags,
            PrimaryFeeling,
            SecondaryFeelings);

        await StreakService.RecalculateAsync();
        Snackbar.Add("Saved successfully!", Severity.Success);

        // Reload so state reflects DB
        await LoadPageAsync();
    }

    private async Task DeleteEntry()
    {
        if (_entry is null) return;

        _entry.IsDeleted = true;
        _entry.UpdatedAt = DateTime.Now;

        await Db.SaveChangesAsync();
        await StreakService.RecalculateAsync();

        Snackbar.Add("Entry deleted.", Severity.Success);
        Nav.NavigateTo("/calendar");
    }
}
