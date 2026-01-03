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

public partial class NewEntry
{
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;
    [Inject] public ApplicationDbContext Db { get; set; } = default!;
    [Inject] public JournalEntryService EntryService { get; set; } = default!;


    private string EntryTitle { get; set; } = string.Empty;
    private string EntryContent { get; set; } = string.Empty;


    private bool _shouldLoadContent;
    private bool _isEditMode;
    private DateTime _currentDate = DateTime.Today;
    private DateTime CurrentDate => _currentDate;

    private string Category = "Personal";
    private List<string> Tags = new();

    // Full pre-built tags list
    private List<string> PredefinedTags = new()
    {
        "Work", "Career", "Studies", "Family", "Friends", "Relationships",
        "Health", "Fitness", "Personal Growth", "Self-care", "Hobbies", "Travel",
        "Nature", "Finance", "Spirituality", "Birthday", "Holiday", "Vacation",
        "Celebration", "Exercise", "Reading", "Writing", "Cooking", "Meditation",
        "Yoga", "Music", "Shopping", "Parenting", "Projects", "Planning", "Reflection"
    };

    private List<Mood> Moods = new();
    private string PrimaryFeeling = string.Empty;
    private List<string> SecondaryFeelings = new();

    private readonly MarkdownPipeline _pipeline =
        new MarkdownPipelineBuilder()
            .UseSoftlineBreakAsHardlineBreak()
            .Build();

    protected override async Task OnInitializedAsync()
    {
        // Load moods/options
        Moods = await Db.Moods.AsNoTracking()
            .OrderBy(m => m.Category)
            .ThenBy(m => m.MoodName)
            .ToListAsync();

        // Load existing entry for today -> switch to edit mode
        var existing = await EntryService.GetEntryByDateAsync(DateTime.Today);

        if (existing is not null)
        {
            _isEditMode = true;
            _currentDate = existing.EntryDate;

            EntryTitle = existing.Title ?? "";
            EntryContent = existing.Content ?? "";
            Category = existing.Category?.CategoryName ?? "Personal";

            Tags = existing.EntryTags.Select(et => et.Tag.TagName).ToList();

            PrimaryFeeling =
                existing.EntryMoods.FirstOrDefault(em => em.IsPrimary)?.Mood.MoodName ?? "";

            SecondaryFeelings =
                existing.EntryMoods.Where(em => !em.IsPrimary)
                    .Select(em => em.Mood.MoodName)
                    .Take(2)
                    .ToList();

            _shouldLoadContent = true;
        }
        else
        {
            _isEditMode = false;
            _currentDate = DateTime.Today;
        }
    }

private async Task OnEditorKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            // Let the browser insert the newline first, then continue list numbering/bullets.
            await Task.Yield();
            await JS.InvokeVoidAsync("continueListIfNeeded");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // JS editor can only be set after first render (DOM exists)
        if (firstRender && _shouldLoadContent)
        {
            _shouldLoadContent = false;
            await JS.InvokeVoidAsync("setEditorContent", EntryContent);
        }
    }

    private async Task FormatText(string command)
    {
        await JS.InvokeVoidAsync("editorFormat", command);
        EntryContent = await JS.InvokeAsync<string>("getEditorContent");
    }

    private async Task OnEditorInput()
    {
        EntryContent = await JS.InvokeAsync<string>("getEditorContent");
        StateHasChanged(); // Update word count
    }

    private int WordCount
    {
        get
        {
            if (string.IsNullOrWhiteSpace(EntryContent))
                return 0;
            
            return EntryContent
                .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }
    }

  

    private async Task SaveEntry()
    {
        if (string.IsNullOrWhiteSpace(PrimaryFeeling))
        {
            Snackbar.Add("Please select a primary mood.", Severity.Error);
            return;
        }

        EntryContent = await JS.InvokeAsync<string>("getEditorContent");

        await EntryService.SaveTodayAsync(
            EntryTitle,
            EntryContent,
            Category,
            Tags,
            PrimaryFeeling,
            SecondaryFeelings);

        Snackbar.Add(_isEditMode ? "Changes saved." : "Entry created.", Severity.Success);
        _isEditMode = true;
    }

    private async Task Cancel()
    {
        // Clears editor (if you prefer "Back" behavior, navigate instead)
        EntryTitle = string.Empty;
        EntryContent = string.Empty;
        Category = "Personal";
        Tags.Clear();
        PrimaryFeeling = string.Empty;
        SecondaryFeelings.Clear();

        await JS.InvokeVoidAsync("setEditorContent", "");
        _isEditMode = false;
    }
}
