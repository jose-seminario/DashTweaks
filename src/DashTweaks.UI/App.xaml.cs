using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using DashTweaks.Core.Managers;
using Hardcodet.Wpf.TaskbarNotification;

namespace DashTweaks.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetTimerResolution(uint desiredResolution, [MarshalAs(UnmanagedType.U1)] bool setResolution, out uint currentResolution);

    private TaskbarIcon? _taskbarIcon;
    private readonly SettingsManager _settingsManager = new();

    public static App Instance => (App)Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Iniciar la ventana principal
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        // 2. Configurar el icono en la bandeja del sistema (System Tray)
        InitializeTaskbarIcon();

        // 3. Salvaguarda: Liberar Timer Resolution al salir
        AppDomain.CurrentDomain.ProcessExit += (s, ev) =>
        {
            try
            {
                NtSetTimerResolution(5000, false, out uint _);
            }
            catch { }
        };
    }

    private void InitializeTaskbarIcon()
    {
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "DashTweaks - Optimizador Gaming",
            Icon = SystemIcons.Shield // Icono seguro por defecto de Windows
        };

        // Doble clic: Abrir ventana principal
        _taskbarIcon.TrayMouseDoubleClick += (s, e) => ShowMainWindow();

        // Menú contextual en el icono de la bandeja
        var contextMenu = new ContextMenu();

        var itemOpen = new MenuItem { Header = "🖥️ Abrir DashTweaks" };
        itemOpen.Click += (s, e) => ShowMainWindow();
        contextMenu.Items.Add(itemOpen);

        var itemToggleMode = new MenuItem { Header = "🟢 Activar/Desactivar Auto-Game Mode" };
        itemToggleMode.Click += (s, e) => ToggleAutoGameMode();
        contextMenu.Items.Add(itemToggleMode);

        contextMenu.Items.Add(new Separator());

        var itemExit = new MenuItem { Header = "❌ Salir" };
        itemExit.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(itemExit);

        _taskbarIcon.ContextMenu = contextMenu;
    }

    public void ShowMainWindow()
    {
        if (MainWindow != null)
        {
            if (MainWindow.WindowState == WindowState.Minimized)
            {
                MainWindow.WindowState = WindowState.Normal;
            }
            MainWindow.Show();
            MainWindow.Activate();
        }
    }

    public void ToggleAutoGameMode()
    {
        var settings = _settingsManager.LoadSettings();
        settings.AutoGameModeEnabled = !settings.AutoGameModeEnabled;
        _settingsManager.SaveSettings(settings);

        if (MainWindow is MainWindow main)
        {
            main.SyncAutoGameModeState(settings.AutoGameModeEnabled);
        }

        string statusText = settings.AutoGameModeEnabled ? "ACTIVADO" : "DESACTIVADO";
        ShowNotification("DashTweaks", $"Auto-Game Mode {statusText}", BalloonIcon.Info);
    }

    public void ShowNotification(string title, string message, BalloonIcon icon = BalloonIcon.Info)
    {
        _taskbarIcon?.ShowBalloonTip(title, message, icon);
    }

    public void ExitApplication()
    {
        // Salvaguarda 2: Dispose del TaskbarIcon para evitar iconos fantasma y Shutdown total
        try
        {
            if (MainWindow is MainWindow main)
            {
                main.PrepareForFinalShutdown();
            }

            _taskbarIcon?.Dispose();
            _taskbarIcon = null;
        }
        catch { }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            NtSetTimerResolution(5000, false, out uint _);
            _taskbarIcon?.Dispose();
        }
        catch { }

        base.OnExit(e);
    }
}
