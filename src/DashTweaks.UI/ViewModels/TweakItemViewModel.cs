using System.ComponentModel;
using System.Runtime.CompilerServices;
using DashTweaks.Tweaks;

namespace DashTweaks.UI.ViewModels;

public class TweakItemViewModel : INotifyPropertyChanged
{
    private bool _isApplied;
    private bool _isBusy;

    public ITweak Tweak { get; }

    public string Id => Tweak.Id;
    public string Name => Tweak.Name;
    public string Description => Tweak.Description;
    public string Category => Tweak.Category;
    public RiskLevel Risk => Tweak.Risk;
    public bool RequiresRestart => Tweak.RequiresRestart;

    public bool IsApplied
    {
        get => _isApplied;
        set
        {
            if (_isApplied != value)
            {
                _isApplied = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(ActionButtonBackground));
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(StatusBadgeBackground));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanExecute));
            }
        }
    }

    public bool CanExecute => !IsBusy;

    public string ActionButtonText => IsApplied ? "REVERTIR" : "APLICAR";
    public string ActionButtonBackground => IsApplied ? "#93261E" : (Risk == RiskLevel.Aggressive ? "#B0232A" : "#0066B8");
    public string StatusBadgeText => IsApplied ? "APLICADO" : "PENDIENTE";
    public string StatusBadgeBackground => IsApplied ? "#107C10" : "#2E2E33";

    public string RiskBadgeBackground => Risk switch
    {
        RiskLevel.Safe => "#107C10",
        RiskLevel.Moderate => "#D83B01",
        RiskLevel.Aggressive => "#D13438",
        _ => "#555555"
    };

    public TweakItemViewModel(ITweak tweak)
    {
        Tweak = tweak;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
