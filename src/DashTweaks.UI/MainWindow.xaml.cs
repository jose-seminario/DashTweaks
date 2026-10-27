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
    private readonly ServiceManagerTweak _serviceManager = new();

    private bool _hasSessionCheckpoint = false;
    private bool _isBusy = false;

    public ObservableCollection<TweakItemViewModel> Tweaks { get; } = new();
    public ObservableCollection<ServiceItemViewModel> Services { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        InitializeTweaks();
        InitializeServices();

        TweaksItemsControl.ItemsSource = Tweaks;
        ServicesItemsControl.ItemsSource = Services;

        LogMessage("DashTweaks v1.2 (Definitive Gaming Edition) inicializado.");
        LogMessage("Estado: 9 Tweaks disponibles • Gestor de Servicios activo • Modo de Seguridad UAC.");
    }

    private void InitializeTweaks()
    {
        // 1. Esquema de Energía
        Tweaks.Add(new TweakItemViewModel(new PowerPlanTweak()));

        // 2. Servicios de Juegos (GameDVR)
        Tweaks.Add(new TweakItemViewModel(new GameDvrTweak()));

        // 3. Prioridades Multimedia (MMCSS)
        Tweaks.Add(new TweakItemViewModel(new MmcSsTweak()));

        // 4. Latencia de Red (Algoritmo de Nagle)
        Tweaks.Add(new TweakItemViewModel(new NagleTweak()));

        // 5. Suspensión de Procesos en Segundo Plano
        Tweaks.Add(new TweakItemViewModel(new ProcessSuspensionTweak()));

        // 6. Purga de Memoria Standby
        Tweaks.Add(new TweakItemViewModel(new MemoryPurgeTweak()));

        // 7. Optimización de Red Avanzada & LLMNR
        Tweaks.Add(new TweakItemViewModel(new NetworkAdvancedTweak()));

        // 8. Latencia de Entrada HID (Buffer Ratón y Teclado)
        Tweaks.Add(new TweakItemViewModel(new HidTweak()));

        // 9. Hardware-Accelerated GPU Scheduling (HAGS)
        Tweaks.Add(new TweakItemViewModel(new HagsTweak()));
    }

    private void InitializeServices()
    {
        RefreshServiceStates();
    }

    private void RefreshServiceStates()
    {
        try
        {
            var states = _serviceManager.GetServiceStates();
            Services.Clear();
            foreach (var st in states)
            {
                Services.Add(new ServiceItemViewModel(st));
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[AVISO] No se pudieron consultar todos los servicios: {ex.Message}");
        }
    }

    #region Manejo de Tweaks Individuales

    private async void TweakActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        if (sender is not Button btn || btn.DataContext is not TweakItemViewModel item) return;

        if (!item.IsApplied)
        {
            // DIÁLOGO PREVENTIVO: Suspensión de Procesos
            if (item.Tweak is ProcessSuspensionTweak)
            {
                var confirm = MessageBox.Show(
                    "La barra de tareas y el escritorio dejarán de responder temporalmente hasta que reviertas esta acción. Esto es normal y libera recursos para el juego.\n\n¿Deseas suspender los procesos en segundo plano ahora?",
                    "DashTweaks - Confirmación de Suspensión de Procesos",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (confirm != MessageBoxResult.Yes)
                {
                    LogMessage("[CANCELADO] El usuario canceló la suspensión de procesos.");
                    return;
                }
            }

            // REGLA CRÍTICA: Primer cambio de la sesión exige Checkpoint obligatorio
            if (!_hasSessionCheckpoint)
            {
                bool checkpointOk = await EnsureSessionCheckpointAsync(item.Name);
                if (!checkpointOk) return;
            }

            // Aplicación asíncrona del Tweak
            SetBusyState(true, $"Aplicando '{item.Name}'...");
            LogMessage($"Aplicando tweak: {item.Name}...");

            bool success = await Task.Run(() => item.Tweak.Apply());

            // Manejo según tipo de tweak
            if (item.Tweak is MemoryPurgeTweak memTweak)
            {
                if (success)
                {
                    item.IsApplied = true;
                    LogMessage($"[ÉXITO] {memTweak.StatusMessage}");
                    MessageBox.Show(memTweak.StatusMessage, "DashTweaks - Memoria", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogMessage($"[AVISO] {memTweak.StatusMessage}");
                    MessageBox.Show(memTweak.StatusMessage, "DashTweaks - Purga Omitida", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else if (item.Tweak is ProcessSuspensionTweak procTweak)
            {
                if (success)
                {
                    item.IsApplied = true;
                    LogMessage($"[ÉXITO] '{item.Name}' aplicado. ({procTweak.SuspendedCount} procesos suspendidos).");
                    MessageBox.Show(
                        "Procesos secundarios suspendidos exitosamente.\n\nPresiona 'REVERTIR' al finalizar la partida para restaurar el escritorio de Windows.",
                        "DashTweaks - Procesos Suspendidos",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    LogMessage($"[AVISO] No se encontraron procesos activos para suspender ({item.Name}).");
                }
            }
            else
            {
                if (success)
                {
                    item.IsApplied = true;
                    string restartNotice = item.RequiresRestart ? " [IMPORTANTE: Requiere reiniciar el PC]" : "";
                    LogMessage($"[ÉXITO] '{item.Name}' aplicado correctamente.{restartNotice}");
                }
                else
                {
                    LogMessage($"[ERROR] Falló la aplicación de '{item.Name}'. Verifica permisos del registro.");
                    MessageBox.Show($"No se pudo aplicar '{item.Name}'.", "DashTweaks - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            // REVERTIR TWEAK
            SetBusyState(true, $"Revirtiendo '{item.Name}'...");
            LogMessage($"Revirtiendo tweak: {item.Name}...");

            bool success = await Task.Run(() => item.Tweak.Revert());

            if (success)
            {
                item.IsApplied = false;
                string restartNotice = item.RequiresRestart ? " [Reinicia para restablecer completamente la configuración]" : "";
                LogMessage($"[REVERTIDO] '{item.Name}' restaurado a sus valores previos.{restartNotice}");

                if (item.Tweak is ProcessSuspensionTweak)
                {
                    MessageBox.Show(
                        "Todos los procesos han sido reanudados. El escritorio y la barra de tareas han vuelto a la normalidad.",
                        "DashTweaks - Procesos Reanudados",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            }
            else
            {
                LogMessage($"[ERROR] Falló la reversión de '{item.Name}'.");
                MessageBox.Show($"No se pudo revertir '{item.Name}'.", "DashTweaks - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        SetBusyState(false, "Listo");
    }

    #endregion

    #region Gestor de Servicios

    private async void BtnDisableServices_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selected = Services.Where(s => s.IsSelected).ToList();
        if (!selected.Any())
        {
            MessageBox.Show("Selecciona al menos un servicio con la casilla correspondiente.", "DashTweaks", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // REGLA CRÍTICA: Checkpoint obligatorio si es la primera modificación de la sesión
        if (!_hasSessionCheckpoint)
        {
            bool checkpointOk = await EnsureSessionCheckpointAsync("Gestor de Servicios");
            if (!checkpointOk) return;
        }

        SetBusyState(true, "Deshabilitando servicios seleccionados...");
        LogMessage("--- Deshabilitando Servicios Seleccionados ---");

        await Task.Run(() =>
        {
            foreach (var svc in selected)
            {
                bool ok = _serviceManager.DisableService(svc.ServiceName, out string msg);
                Dispatcher.Invoke(() =>
                {
                    if (ok) LogMessage($"[OK] {msg}");
                    else LogMessage($"[AVISO] {msg}");
                });
            }
        });

        RefreshServiceStates();
        SetBusyState(false, "Listo");
        MessageBox.Show("Los servicios seleccionados han sido detenidos y configurados como Deshabilitados.", "DashTweaks - Servicios", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnRestoreServices_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        SetBusyState(true, "Restaurando servicios a su estado original...");
        LogMessage("--- Restaurando Servicios Originales ---");

        await Task.Run(() =>
        {
            foreach (var svc in Services)
            {
                bool ok = _serviceManager.RestoreService(svc.ServiceName, out string msg);
                Dispatcher.Invoke(() =>
                {
                    if (ok) LogMessage($"[REVERTIDO] {msg}");
                    else LogMessage($"[AVISO] {msg}");
                });
            }
        });

        RefreshServiceStates();
        SetBusyState(false, "Listo");
        MessageBox.Show("Los servicios han sido restablecidos a sus tipos de inicio y estados originales.", "DashTweaks - Servicios", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnRefreshServices_Click(object sender, RoutedEventArgs e)
    {
        RefreshServiceStates();
        LogMessage("[ACTUALIZADO] Estados en vivo de servicios de Windows actualizados.");
    }

    #endregion

    #region Checkpoints Globales y Auxiliares

    private async Task<bool> EnsureSessionCheckpointAsync(string contextName)
    {
        SetBusyState(true, "Creando punto de restauración... por favor espera");
        LogMessage($"[SEGURIDAD] Primera optimización solicitada ('{contextName}'). Generando Checkpoint obligatorio...");

        BackupResult backupResult = await Task.Run(() => _backupManager.CreateCheckpoint());

        if (!backupResult.Success)
        {
            LogMessage($"[CANCELADO] Falló la creación del Checkpoint de seguridad: {backupResult.Message}");
            LogMessage("[SEGURIDAD] La operación ha sido abortada para proteger el sistema.");
            MessageBox.Show(
                $"No se pudo crear el backup de seguridad obligatorio:\n\n{backupResult.Message}\n\nLa operación ha sido cancelada.",
                "DashTweaks - Operación Abortada",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            SetBusyState(false, "Operación cancelada por fallo en backup");
            return false;
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

        return true;
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

                RefreshServiceStates();
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
        BtnDisableServices.IsEnabled = !isBusy;
        BtnRestoreServices.IsEnabled = !isBusy;
        BtnRefreshServices.IsEnabled = !isBusy;

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

    #endregion
}
