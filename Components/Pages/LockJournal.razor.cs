using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using SoulScript.Services;
using System.Web;


namespace SoulScript.Components.Pages;

public partial class LockJournal
{
    [Inject] public SecurityService Security { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;
    [Inject] public IJSRuntime JSRuntime { get; set; } = default!;

    private string _pinInput = "";
    private bool _hasPin;
    private string _error = "";
    private string _returnUrl = "/";


    protected override async Task OnInitializedAsync()
    {
        _hasPin = await Security.HasPinAsync();
        Security.Lock();

        var uri = Nav.ToAbsoluteUri(Nav.Uri);
        var query = HttpUtility.ParseQueryString(uri.Query);

        var ru = query["returnUrl"];
        if (!string.IsNullOrWhiteSpace(ru))
            _returnUrl = ru;
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
            Nav.NavigateTo(_returnUrl, replace: true);

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

    private DotNetObjectReference<LockJournal> _objRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _objRef = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("addKeyboardListener", _objRef);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JS Interop error: {ex.Message}");
                // Fail silently to prevent app crash, user can still use buttons
            }
        }
    }

    [JSInvokable]
    public async Task HandleKeyPress(string key)
    {
        if (int.TryParse(key, out int num))
        {
            await AppendPin(key);
            StateHasChanged();
        }
        else if (key == "Backspace")
        {
            Backspace();
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        _objRef?.Dispose();
        _ = JSRuntime.InvokeVoidAsync("removeKeyboardListener");
    }
}
