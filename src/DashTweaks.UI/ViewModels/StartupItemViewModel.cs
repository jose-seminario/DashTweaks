using System.ComponentModel;
using System.Runtime.CompilerServices;
using DashTweaks.Core.Managers;

namespace DashTweaks.UI.ViewModels;

public class StartupItemViewModel : INotifyPropertyChanged
{
    private bool _isEnabled;
    private bool _isBusy;

    public StartupItem Item { get; }

    public string Name => Item.Name;
    public string Command => Item.Command;
    public bool IsCritical => Item.IsCritical;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                Item.IsEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(StatusBorder));
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
                OnPropertyChanged(nameof(CanToggle));
            }
        }
    }

    public bool CanToggle => !IsBusy;

    public string LocationTypeText => Item.LocationType switch
    {
        StartupLocationType.RegistryCurrentUser => "Registro (HKCU)",
        StartupLocationType.RegistryLocalMachine => "Registro (HKLM)",
        StartupLocationType.StartupFolder => "Carpeta de Inicio",
        _ => "Desconocido"
    };

    public string LocationBackground => Item.LocationType switch
    {
        StartupLocationType.RegistryCurrentUser => "#1E2B3A",
        StartupLocationType.RegistryLocalMachine => "#2E241E",
        StartupLocationType.StartupFolder => "#1E2E24",
        _ => "#242428"
    };

    public string LocationColor => Item.LocationType switch
    {
        StartupLocationType.RegistryCurrentUser => "#60CDFF",
        StartupLocationType.RegistryLocalMachine => "#FFA060",
        StartupLocationType.StartupFolder => "#6CCB76",
        _ => "#CCCCCC"
    };

    public string StatusText => IsEnabled ? "Habilitado al Inicio" : "Deshabilitado";
    public string StatusColor => IsEnabled ? "#6CCB76" : "#A0A0AA";
    public string StatusBackground => IsEnabled ? "#17291A" : "#24242A";
    public string StatusBorder => IsEnabled ? "#244D29" : "#383842";

    // Salvaguarda 2: Advertencia de aplicaciones críticas / drivers
    public string CriticalWarningText => "⚠️ Controlador / Componente del Sistema";

    public StartupItemViewModel(StartupItem item)
    {
        Item = item;
        _isEnabled = item.IsEnabled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
