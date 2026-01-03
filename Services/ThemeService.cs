namespace SoulScript.Services;

public sealed class ThemeService
{
    public bool IsDarkMode { get; private set; }

    public event Action? OnChange;

    public void Set(bool value)
    {
        if (IsDarkMode == value) return;
        IsDarkMode = value;
        OnChange?.Invoke();
    }

    public void Toggle()
    {
        IsDarkMode = !IsDarkMode;
        OnChange?.Invoke();
    }
}
