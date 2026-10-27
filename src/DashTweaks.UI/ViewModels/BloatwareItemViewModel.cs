using System.ComponentModel;
using System.Runtime.CompilerServices;
using DashTweaks.Core.Managers;

namespace DashTweaks.UI.ViewModels;

public class BloatwareItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isInstalled;
    private bool _isBusy;

    public BloatwareAppInfo AppInfo { get; }

    public string DisplayName => AppInfo.DisplayName;
    public string PackageName => AppInfo.PackageName;
    public string PackageFullName => AppInfo.PackageFullName;
    public string Description => AppInfo.Description;

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

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (_isInstalled != value)
            {
                _isInstalled = value;
                AppInfo.IsInstalled = value;
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
                OnPropertyChanged(nameof(CanSelect));
            }
        }
    }

    public bool CanSelect => !IsBusy;

    public string StatusText => IsInstalled ? "Instalado" : "Desinstalado";
    public string StatusColor => IsInstalled ? "#F59E0B" : "#6CCB76";
    public string StatusBackground => IsInstalled ? "#2B2211" : "#17291A";
    public string StatusBorder => IsInstalled ? "#594419" : "#244D29";

    public BloatwareItemViewModel(BloatwareAppInfo appInfo)
    {
        AppInfo = appInfo;
        _isInstalled = appInfo.IsInstalled;
        _isSelected = appInfo.IsInstalled; // Por defecto seleccionados los instalados para facilitar limpieza
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
