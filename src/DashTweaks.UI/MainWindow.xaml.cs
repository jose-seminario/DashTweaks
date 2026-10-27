using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DashTweaks.Core.Managers;
using DashTweaks.Core.Models;
using DashTweaks.Tweaks.Implementations;
using DashTweaks.UI.ViewModels;

namespace DashTweaks.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly BackupManager _backupManager = new();
    private readonly RollbackManager _rollbackManager = new();

    private bool _hasSessionCheckpoint = false;
    private bool _isBusy = false;

    public ObservableCollection<TweakItemViewModel> Tweaks { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        InitializeTweaks();
        TweaksItemsControl.ItemsSource = Tweaks;

        LogMessage("DashTweaks inicializado en modo de alta seguridad.");
        LogMessage("Políticas: Backup First • Mitigaciones intactas • Cero cajas negras.");
    }

    private void InitializeTweaks()
    {
        Tweaks.Add(new TweakItemViewModel(new PowerPlanTweak()));
        Tweaks.Add(new TweakItemViewModel(new GameDvrTweak()));
        Tweaks.Add(new TweakItemViewModel(new MmcSsTweak()));
        Tweaks.Add(new TweakItemViewModel(new NagleTweak()));
    }

    private async void TweakActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        if (sender is not Button btn || btn.DataContext is not TweakItemViewModel item) return;

        if (!item.IsApplied)
        {
            // FLUJO: APLICAR TWEAK
            // Regla crítica: Si es la primera optimización de la sesión, exigir Checkpoint
            if (!_hasSessionCheckpoint)
            {
                SetBusyState(true, "Creando punto de restauración... por favor espera");
                LogMessage($"[SEGURIDAD] Primera optimización solicitada ('{item.Name}'). Generando Checkpoint obligatorio...");

                BackupResult backupResult = await Task.Run(() => _backupManager.CreateCheckpoint());

                if (!backupResult.Success)
                {
                    LogMessage($"[CANCELADO] Falló la creación del Checkpoint de seguridad: {backupResult.Message}");
                    LogMessage("[SEGURIDAD] La optimización ha sido abortada para proteger la integridad del sistema.");
                    MessageBox.Show(
                        $"No se pudo crear el backup de seguridad obligatorio:\n\n{backupResult.Message}\n\nLa optimización ha sido cancelada.",
                        "DashTweaks - Optimización Abortada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    SetBusyState(false, "Operación cancelada por fallo en backup");
                    return;
                }

                _hasSessionCheckpoint = true;
                LogMessage($"[CHECKPOINT OK] Respaldo guardado en: {backupResult.BackupPath}");
                if (backupResult.RestorePointCreated)
                {
                    LogMessage("[CHECKPOINT OK] Punto de restauración del sistema Windows creado exitosamente.");
                }
                else
                {
                    LogMessage($"[AVISO] {backupResult.RestorePointMessage}");
                }
            }

            // Aplicar el Tweak asíncronamente
            SetBusyState(true, $"Aplicando tweak '{item.Name}'...");
            LogMessage($"Aplicando tweak: {item.Name}...");

            bool success = await Task.Run(() => item.Tweak.Apply());

            if (success)
            {
                item.IsApplied = true;
                string restartNotice = item.RequiresRestart ? " [IMPORTANTE: Reinicia para aplicar]" : "";
                LogMessage($"[ÉXITO] '{item.Name}' aplicado correctamente.{restartNotice}");
            }
            else
            {
                LogMessage($"[ERROR] Falló la aplicación de '{item.Name}'. Verifica permisos del registro.");
                MessageBox.Show(
                    $"No se pudo aplicar '{item.Name}'.",
                    "DashTweaks - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
        else
        {
            // FLUJO: REVERTIR TWEAK
            SetBusyState(true, $"Revirtiendo tweak '{item.Name}'...");
            LogMessage($"Revirtiendo tweak: {item.Name}...");

            bool success = await Task.Run(() => item.Tweak.Revert());

            if (success)
            {
                item.IsApplied = false;
                string restartNotice = item.RequiresRestart ? " [Reinicia para restablecer la configuración de red]" : "";
                LogMessage($"[REVERTIDO] '{item.Name}' restaurado a sus valores previos.{restartNotice}");
            }
            else
            {
                LogMessage($"[ERROR] Falló la reversión de '{item.Name}'.");
                MessageBox.Show(
                    $"No se pudo revertir '{item.Name}'.",
                    "DashTweaks - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        SetBusyState(false, "Listo");
    }

    private async void BtnCreateBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        SetBusyState(true, "Creando punto de restauración... por favor espera");
        LogMessage("--- Creando Checkpoint Manual ---");

        try
        {
            BackupResult result = await Task.Run(() => _backupManager.CreateCheckpoint());

            if (result.Success)
            {
                _hasSessionCheckpoint = true;
                LogMessage($"[ÉXITO] Backup creado en: {result.BackupPath}");
                if (result.RestorePointCreated)
                {
                    LogMessage("[OK] Punto de restauración del sistema creado exitosamente.");
                }
                else
                {
                    LogMessage($"[AVISO] {result.RestorePointMessage}");
                }

                MessageBox.Show(
                    result.Message,
                    "DashTweaks - Backup Manual",
                    MessageBoxButton.OK,
                    result.RestorePointCreated ? MessageBoxImage.Information : MessageBoxImage.Warning
                );
            }
            else
            {
                LogMessage($"[ERROR] {result.Message}");
                MessageBox.Show(result.Message, "DashTweaks - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[EXCEPCIÓN] {ex.Message}");
            MessageBox.Show($"Error inesperado: {ex.Message}", "DashTweaks", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Listo");
        }
    }

    private async void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        SetBusyState(true, "Consultando puntos de restauración disponibles...");
        LogMessage("--- Solicitud de Rollback Total ---");

        try
        {
            var backups = await Task.Run(() => _backupManager.GetAvailableBackups());

            if (!backups.Any())
            {
                LogMessage("[AVISO] No se encontraron checkpoints previos en disco.");
                MessageBox.Show(
                    "No se encontró ningún backup previo guardado en este equipo.",
                    "DashTweaks - Rollback",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            var latest = backups.First();
            var confirm = MessageBox.Show(
                $"¿Deseas restaurar la configuración completa desde el backup del {latest.CreatedAt:yyyy-MM-dd HH:mm:ss}?\n\nCarpeta: {latest.FolderPath}",
                "Confirmación de Rollback",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (confirm != MessageBoxResult.Yes)
            {
                LogMessage("[CANCELADO] El usuario canceló la restauración.");
                return;
            }

            SetBusyState(true, "Restaurando registro y esquemas de energía...");
            RollbackResult result = await Task.Run(() => _rollbackManager.RestoreFromBackup(latest.FolderPath));

            if (result.Success)
            {
                LogMessage($"[ÉXITO] {result.Message}");

                // Marcar tweaks visualmente como no aplicados si se restauró todo
                foreach (var tweakItem in Tweaks)
                {
                    tweakItem.IsApplied = false;
                }

                MessageBox.Show(result.Message, "DashTweaks - Rollback Exitoso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                LogMessage($"[ERROR] Falló la restauración: {result.Message}");
                foreach (var err in result.Errors)
                {
                    LogMessage($"       * {err}");
                }
                MessageBox.Show(result.Message, "DashTweaks - Error en Restauración", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[EXCEPCIÓN] {ex.Message}");
            MessageBox.Show($"Error durante la restauración: {ex.Message}", "DashTweaks", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, "Listo");
        }
    }

    private void SetBusyState(bool isBusy, string statusText)
    {
        _isBusy = isBusy;

        BtnCreateBackup.IsEnabled = !isBusy;
        BtnRestoreBackup.IsEnabled = !isBusy;

        foreach (var tweak in Tweaks)
        {
            tweak.IsBusy = isBusy;
        }

        TxtStatus.Text = isBusy ? "Operando..." : statusText;
        TxtBusyStatus.Text = statusText;
        BusyBanner.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LogMessage(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        TxtLog.AppendText($"[{timestamp}] {message}\n");
        LogScrollViewer.ScrollToEnd();
    }
}
