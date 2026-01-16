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

    [Inject] public NavigationManager Nav { get; set; } = default!; // Need this to redirect after save if desired, or to handle cancel

    private string EntryTitle { get; set; } = string.Empty;
    private string EntryContent { get; set; } = string.Empty;

    private bool _shouldLoadContent;
    private bool _isEditMode;
    private DateTime _currentDate = DateTime.Today;
    private DateTime CurrentDate => _currentDate;

    private string Category = "Personal";
    private List<string> Tags = new();

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
    
    // Helper checks if content is HTML
    private bool IsHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var trimmed = content.Trim();
        return trimmed.StartsWith("<") && trimmed.EndsWith(">");
    }

    protected override async Task OnInitializedAsync()
    {
        Moods = await Db.Moods.AsNoTracking()
            .OrderBy(m => m.Category)
            .ThenBy(m => m.MoodName)
            .ToListAsync();

        var existing = await EntryService.GetEntryByDateAsync(DateTime.Today);

        if (existing is not null)
        {
            _isEditMode = true;
            _currentDate = existing.EntryDate;

            EntryTitle = existing.Title ?? "";
            EntryContent = existing.Content ?? "";
            Category = existing.Category?.CategoryName ?? "Personal";
            Tags = existing.EntryTags.Select(et => et.Tag.TagName).ToList();
            PrimaryFeeling = existing.EntryMoods.FirstOrDefault(em => em.IsPrimary)?.Mood.MoodName ?? "";
            SecondaryFeelings = existing.EntryMoods.Where(em => !em.IsPrimary)
                    .Select(em => em.Mood.MoodName).Take(2).ToList();

            // Migration logic: convert MD to HTML if needed
            if (!IsHtml(EntryContent) && !string.IsNullOrEmpty(EntryContent))
            {
                EntryContent = Markdown.ToHtml(EntryContent, _pipeline);
            }

            _shouldLoadContent = true;
        }
        else
        {
            _isEditMode = false;
            _currentDate = DateTime.Today;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
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

    // Called on editor input event manually or we just pull on save
    private async Task OnEditorInput()
    {
        // Optional: could auto-update word count here if we bind an event
        EntryContent = await JS.InvokeAsync<string>("getEditorContent");
        StateHasChanged();
    }

    private int WordCount
    {
        get
        {
            if (string.IsNullOrWhiteSpace(EntryContent)) return 0;
            // Strip HTML tags for word count
            var text = System.Text.RegularExpressions.Regex.Replace(EntryContent, "<.*?>", " ");
            return text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
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
        
        // After saving on New Entry page, maybe redirect to the View page (EntryByDate)
        // so user sees the nice read-only view?
        // Or stay here. User didn't specify. Staying here but in "Edit Mode" is current behavior. 
        // But since we want "View Mode" behavior, redirecting to the specific entry page might be better.
        
         Nav.NavigateTo($"/entry/{DateTime.Today:yyyy-MM-dd}");
    }

    private async Task Cancel()
    {
        // Just clear or reload
        EntryTitle = string.Empty;
        EntryContent = string.Empty;
        
        await JS.InvokeVoidAsync("setEditorContent", "");
        _isEditMode = false; 
        
        // Or navigate away
        Nav.NavigateTo("/"); 
    }
}
