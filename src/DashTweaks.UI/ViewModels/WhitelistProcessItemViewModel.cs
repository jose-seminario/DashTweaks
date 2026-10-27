using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DashTweaks.UI.ViewModels;

public class WhitelistProcessItemViewModel : INotifyPropertyChanged
{
    private bool _isWhitelisted;

    public string ProcessName { get; }
    public string ExeName => ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? ProcessName : $"{ProcessName}.exe";
    public string MainWindowTitle { get; }
    public int ProcessId { get; }

    public bool IsWhitelisted
    {
        get => _isWhitelisted;
        set
        {
            if (_isWhitelisted != value)
            {
                _isWhitelisted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(StatusBadgeBackground));
                OnPropertyChanged(nameof(StatusBadgeBorder));
            }
        }
    }

    public string StatusBadgeText => IsWhitelisted ? "🛡️ En Lista Blanca (No tocar)" : "Suspender si aplica";
    public string StatusBadgeBackground => IsWhitelisted ? "#19381E" : "#222227";
    public string StatusBadgeBorder => IsWhitelisted ? "#2E6936" : "#323238";

    public WhitelistProcessItemViewModel(string processName, string windowTitle, int pid, bool isWhitelisted)
    {
        ProcessName = processName;
        MainWindowTitle = string.IsNullOrWhiteSpace(windowTitle) ? "(Aplicación sin título principal)" : windowTitle;
        ProcessId = pid;
        _isWhitelisted = isWhitelisted;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
