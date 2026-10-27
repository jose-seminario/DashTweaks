using System.ComponentModel;
using System.Runtime.CompilerServices;
using DashTweaks.Tweaks.Implementations;

namespace DashTweaks.UI.ViewModels;

public class ServiceItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private bool _isRunning;
    private int _startType = 3;

    public string ServiceName { get; }
    public string DisplayName { get; }
    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusBackground));
            }
        }
    }

    public int StartType
    {
        get => _startType;
        set
        {
            if (_startType != value)
            {
                _startType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartTypeText));
                OnPropertyChanged(nameof(StartTypeColor));
            }
        }
    }

    public string StatusText => IsRunning ? "● En ejecución" : "○ Detenido";
    public string StatusColor => IsRunning ? "#6CCB76" : "#9E9EA6";
    public string StatusBackground => IsRunning ? "#1A2E1C" : "#26262B";

    public string StartTypeText => StartType switch
    {
        2 => "Inicio: Automático",
        3 => "Inicio: Manual",
        4 => "Inicio: Deshabilitado",
        _ => $"Inicio: Tipo {StartType}"
    };

    public string StartTypeColor => StartType == 4 ? "#F59E0B" : "#A6A6B0";

    public ServiceItemViewModel(ServiceStateInfo info)
    {
        ServiceName = info.ServiceName;
        DisplayName = info.DisplayName;
        Description = info.Description;
        IsRunning = info.IsCurrentlyRunning;
        StartType = info.CurrentStartType;
        IsSelected = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
