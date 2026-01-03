using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoulScript.Services;

namespace SoulScript.Components.Pages;

public partial class LockJournal
{
    [Inject] public SecurityService Security { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    private string _pinInput = "";
    private bool _hasPin;
    private string _error = "";

    protected override async Task OnInitializedAsync()
    {
        _hasPin = await Security.HasPinAsync();
        Security.Lock();
    }

    private async Task AppendPin(string digit)
    {
        if (_pinInput.Length >= 4) return;

        _error = "";
        _pinInput += digit;

        if (_pinInput.Length == 4)
            await HandlePinSubmit();
    }

    private void Backspace()
    {
        _error = "";
        if (_pinInput.Length > 0)
            _pinInput = _pinInput[..^1];
    }

    private async Task HandlePinSubmit()
    {
        await Task.Delay(100);

        if (_hasPin)
        {
            var valid = await Security.VerifyPinAsync(_pinInput);
            if (!valid)
            {
                _error = "Incorrect PIN";
                Snackbar.Add(_error, Severity.Error);
                _pinInput = "";
                return;
            }

            Security.Unlock();
            Snackbar.Add("Unlocked!", Severity.Success);
            _pinInput = "";
            Nav.NavigateTo("/", replace: true);
            return;
        }

        // First time: set PIN once
        await Security.SetPinAsync(_pinInput, enableLock: true);
        _hasPin = true;

        Security.Unlock();
        Snackbar.Add("PIN set successfully!", Severity.Success);
        _pinInput = "";
        Nav.NavigateTo("/", replace: true);
    }
}
