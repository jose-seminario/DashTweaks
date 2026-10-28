using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Excepción no controlada en la interfaz:\n\n{e.Exception}",
            "DashTweaks - Error No Controlado",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"Error crítico fatal en el dominio:\n\n{ex}",
                "DashTweaks - Error Fatal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 1. Iniciar la ventana principal
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            // 2. Configurar el icono en la bandeja del sistema (System Tray)
            InitializeTaskbarIcon();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error crítico al iniciar DashTweaks:\n\n{ex}",
                "DashTweaks - Error de Arranque",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

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
        try
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AVISO] No se pudo inicializar TaskbarIcon: {ex.Message}");
        }
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
