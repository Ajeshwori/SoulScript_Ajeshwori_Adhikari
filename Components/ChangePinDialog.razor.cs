using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoulScript.Services;

namespace SoulScript.Components;

public partial class ChangePinDialog : ComponentBase
{
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public SecurityService SecurityService { get; set; } = default!;

    private string _pinInput = "";
    private string _error = "";

    private async Task AppendPin(string digit)
    {
        if (_pinInput.Length >= 4) return;

        _error = "";
        _pinInput += digit;

        if (_pinInput.Length == 4)
            await SavePin();
    }

    private void Backspace()
    {
        _error = "";
        if (_pinInput.Length > 0)
            _pinInput = _pinInput[..^1];
    }

    private async Task SavePin()
    {
        if (_pinInput.Length != 4)
        {
            _error = "PIN must be 4 digits";
            return;
        }

        await SecurityService.SetPinAsync(_pinInput, await SecurityService.IsLockEnabledAsync());
        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void Cancel() => MudDialog?.Cancel();
}
