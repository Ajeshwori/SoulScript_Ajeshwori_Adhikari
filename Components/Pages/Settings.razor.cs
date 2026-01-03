using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using SoulScript.Data;
using SoulScript.Services;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using PdfColors = QuestPDF.Helpers.Colors;

namespace SoulScript.Components.Pages;

public partial class Settings
{
    [Inject] public ThemeService Theme { get; set; } = default!;
    [Inject] public ApplicationDbContext Db { get; set; } = default!;
    [Inject] public SecurityService Security { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;
    [Inject] public IDialogService DialogService { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    private bool _autoLockEnabled;

    private int _totalEntries;
    private int _totalWords;

    private bool _isExporting;

    private DateRange _exportDateRange = new(null, null);

    protected override async Task OnInitializedAsync()
    {
        _autoLockEnabled = await Security.IsLockEnabledAsync();
        await LoadStatistics();
    }

    private async Task LoadStatistics()
    {
        var entries = await Db.JournalEntries
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .ToListAsync();

        _totalEntries = entries.Count;
        _totalWords = entries.Sum(e => e.WordCount);
    }

    private void ToggleTheme()
    {
        Theme.Toggle();
    }

    private async Task OnAutoLockAfter()
    {
        await Security.SetLockStateAsync(_autoLockEnabled);
        Snackbar.Add(_autoLockEnabled ? "Auto-lock enabled" : "Auto-lock disabled", Severity.Success);
    }

    private async Task OpenChangePinDialog()
    {
        var parameters = new DialogParameters
        {
            { nameof(SoulScript.Components.ChangePinDialog.SecurityService), Security }
        };

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<SoulScript.Components.ChangePinDialog>("Change PIN", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled)
            Snackbar.Add("PIN changed successfully", Severity.Success);
    }

    private async Task ExportAsPdf()
    {
        _isExporting = true;
        StateHasChanged();

        try
        {
            var query = Db.JournalEntries
                .AsNoTracking()
                .Where(e => !e.IsDeleted);

            if (_exportDateRange.Start is DateTime start)
                query = query.Where(e => e.EntryDate >= start);

            if (_exportDateRange.End is DateTime end)
                query = query.Where(e => e.EntryDate <= end);

            var entries = await query
                .OrderBy(e => e.EntryDate)
                .ToListAsync();

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    page.Header()
                        .Text("SoulScript - Journal Export")
                        .FontSize(18)
                        .SemiBold()
                        .FontColor(PdfColors.Green.Darken2);

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        foreach (var e in entries)
                        {
                            col.Item().Border(1).BorderColor(PdfColors.Grey.Lighten2).Padding(10).Column(entryCol =>
                            {
                                entryCol.Item().Text($"{e.EntryDate:yyyy-MM-dd}  {e.Title}")
                                               .SemiBold()
                                               .FontColor(PdfColors.BlueGrey.Darken3);

                                entryCol.Item().Text(e.Content ?? "")
                                               .FontSize(11)
                                               .FontColor(PdfColors.Grey.Darken3);
                            });
                        }
                    });
                });
            }).GeneratePdf();

            var base64 = Convert.ToBase64String(pdfBytes);
            var fileName = $"soulscript-export-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";

            await JS.InvokeVoidAsync("downloadFile", fileName, "application/pdf", base64, true);
            Snackbar.Add("PDF exported successfully", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"PDF export failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isExporting = false;
            StateHasChanged();
        }
    }
}
