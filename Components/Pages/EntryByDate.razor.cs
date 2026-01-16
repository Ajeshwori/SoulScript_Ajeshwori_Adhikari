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
    [Inject] public IDialogService DialogService { get; set; } = default!;

    [Parameter] public string DateText { get; set; } = "";

    private bool _isLoading = true;
    private bool _invalidDate;
    private bool _isFuture;
    private bool _isCreating;
    private bool _isEditing; // New: separation of edit/view states

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

    private bool _shouldSetEditor;
    private string _editorTextToSet = "";

    // Helper to detect if content is likely Markdown (primitive check)
    // If it starts with <p> or <div or <h, it's likely HTML. otherwise treat as markdown.
   
    private bool IsHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var trimmed = content.Trim();
        return trimmed.StartsWith("<") && trimmed.EndsWith(">");
    }

    private readonly MarkdownPipeline _pipeline =
        new MarkdownPipelineBuilder().UseSoftlineBreakAsHardlineBreak().Build();

    protected override async Task OnParametersSetAsync()
    {
        await LoadPageAsync();
    }

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
        
        // Default to View Mode if entry exists
        _isEditing = false;
        
        _entry = null;

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

        if (Moods.Count == 0)
        {
            Moods = await Db.Moods.AsNoTracking()
                .OrderBy(m => m.Category)
                .ThenBy(m => m.MoodName)
                .ToListAsync();
        }

        // Tag loading logic ...
        if (PredefinedTags.Count == 5 && PredefinedTags.Contains("Work"))
        {
             // load top 5 using data
             PredefinedTags = await Db.EntryTags.AsNoTracking()
                .GroupBy(et => et.Tag.TagName)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(5)
                .ToListAsync();
        }

        _entry = await Db.JournalEntries
            .Include(e => e.EntryTags).ThenInclude(et => et.Tag)
            .Include(e => e.EntryMoods).ThenInclude(em => em.Mood)
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => !e.IsDeleted && e.EntryDate == _date);

        if (_entry is null)
        {
            // If entry doesn't exist, we just show "No entry" state initially.
            // User must click "New Entry" to start creating.
            _isLoading = false;
            return;
        }

        // Populate UI state
        EntryTitle = _entry.Title;
        EntryContent = _entry.Content;
        Category = _entry.Category?.CategoryName ?? "Personal";
        Tags = _entry.EntryTags.Select(t => t.Tag.TagName).ToList();
        PrimaryFeeling = _entry.EntryMoods.FirstOrDefault(m => m.IsPrimary)?.Mood.MoodName ?? "";
        SecondaryFeelings = _entry.EntryMoods.Where(m => !m.IsPrimary)
                .Select(m => m.Mood.MoodName).Take(2).ToList();

        // Default: View Mode.
        _isEditing = false;
        _isLoading = false;
    }

    private async Task CreateEntryForThisDay()
    {
        _isCreating = true;
        
        // Start in Edit Mode
        _isEditing = true;

        ResetEditorState();
        
        // Prepare editor
        _editorTextToSet = "";
        _shouldSetEditor = true;
    }

    private async Task EnableEditMode()
    {
        _isEditing = true;
        
        // Migration logic:
        // If stored content is Markdown, convert to HTML for the editor.
        // If it's already HTML, use as is.
        string contentForEditor = EntryContent;
        
        if (!IsHtml(contentForEditor) && !string.IsNullOrEmpty(contentForEditor))
        {
            contentForEditor = Markdown.ToHtml(contentForEditor, _pipeline);
        }

        _editorTextToSet = contentForEditor ?? "";
        _shouldSetEditor = true;
    }
    
    // Cancel editing implies going back to View Mode (re-read from DB or reset)
    private async Task CancelEdit()
    {
        if (_isCreating)
        {
            // If we were creating a new one, cancel means back to "No entry"
            _isCreating = false;
            _entry = null;
            _isEditing = false;
        }
        else
        {
            // Revert changes
           await LoadPageAsync();
        }
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

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        return await DialogService.ShowMessageBox(
            title,
            message,
            yesText: "Yes",
            cancelText: "Cancel"
        ) ?? false;
    }

    // Input/KeyDown no longer needed for textarea, handled by browser contenteditable + JS events if needed
    // But we might want to capture input to update word count if we had one.
    // For now we rely on Save to get content.

    private async Task SaveEntry()
    {
        var actionText = _entry is null ? "save" : "update";

        if (!await ConfirmAsync("Confirm", $"Do you want to {actionText} this entry?"))
            return;

        if (string.IsNullOrWhiteSpace(PrimaryFeeling))
        {
            Snackbar.Add("Select a primary mood.", Severity.Error);
            return;
        }

       

        // Get HTML from editor
        EntryContent = await JS.InvokeAsync<string>("getEditorContent");

        if (string.IsNullOrWhiteSpace(EntryContent) && _entry != null)
        {
            // Careful about wiping content
            // If creating new, empty might be okay-ish but usually not.
            Snackbar.Add("Content is empty. Not saving to avoid wiping your entry.", Severity.Warning);
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

        // After save, reload to switch to View Mode
        await LoadPageAsync();
    }

    private async Task DeleteEntry()
    {
        if (_entry is null) return;

        if (!await ConfirmAsync("Delete entry", "Do you want to delete this entry?"))
            return;

        _entry.IsDeleted = true;
        _entry.UpdatedAt = DateTime.Now;

        await Db.SaveChangesAsync();
        await StreakService.RecalculateAsync();

        Snackbar.Add("Entry deleted.", Severity.Success);
        Nav.NavigateTo("/calendar");
    }

}
