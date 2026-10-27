using System.Windows;
using DashTweaks.Core.Managers;
using DashTweaks.Core.Models;

namespace DashTweaks.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly BackupManager _backupManager = new();
    private readonly RollbackManager _rollbackManager = new();

    public MainWindow()
    {
        InitializeComponent();
        LogMessage("DashTweaks inicializado.");
        LogMessage("Listo para operaciones de seguridad.");
    }

    private async void BtnCreateBackup_Click(object sender, RoutedEventArgs e)
    {
        SetBusyState(true, "Creando punto de control...");
        LogMessage("--- Iniciando proceso de Backup ---");

        try
        {
            BackupResult result = await Task.Run(() => _backupManager.CreateCheckpoint());

            if (result.Success)
            {
                LogMessage($"[ÉXITO] Carpeta de backup: {result.BackupPath}");
                if (result.RestorePointCreated)
                {
                    LogMessage("[OK] Punto de restauración del sistema creado con éxito.");
                }
                else
                {
                    LogMessage($"[AVISO] No se pudo crear el punto de restauración: {result.RestorePointMessage}");
                    LogMessage("       (Nota: Si la Protección del Sistema está desactivada en Windows, los backups de Registro y Energía siguen protegidos).");
                }

                MessageBox.Show(
                    result.Message,
                    "DashTweaks - Backup",
                    MessageBoxButton.OK,
                    result.RestorePointCreated ? MessageBoxImage.Information : MessageBoxImage.Warning
                );
            }
            else
            {
                LogMessage($"[ERROR] Falló la creación del backup: {result.Message}");
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
        SetBusyState(true, "Consultando backups disponibles...");
        LogMessage("--- Iniciando proceso de Restauración ---");

        try
        {
            var backups = await Task.Run(() => _backupManager.GetAvailableBackups());

            if (!backups.Any())
            {
                LogMessage("[AVISO] No se encontraron backups locales previos para restaurar.");
                MessageBox.Show(
                    "No se encontró ningún backup previo en el sistema. Primero crea un checkpoint.",
                    "DashTweaks - Restauración",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            var latestBackup = backups.First();
            LogMessage($"Restaurando el backup más reciente: {latestBackup.Id} ({latestBackup.CreatedAt:yyyy-MM-dd HH:mm:ss})");

            var confirm = MessageBox.Show(
                $"¿Deseas restaurar la configuración desde el backup del {latestBackup.CreatedAt:yyyy-MM-dd HH:mm:ss}?\n\nUbicación: {latestBackup.FolderPath}",
                "Confirmación de Rollback",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (confirm != MessageBoxResult.Yes)
            {
                LogMessage("[CANCELADO] El usuario canceló la restauración.");
                return;
            }

            RollbackResult rollbackResult = await Task.Run(() => _rollbackManager.RestoreFromBackup(latestBackup.FolderPath));

            if (rollbackResult.Success)
            {
                LogMessage($"[ÉXITO] {rollbackResult.Message}");
                MessageBox.Show(rollbackResult.Message, "DashTweaks - Rollback Exitoso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                LogMessage($"[ERROR] Falló la restauración: {rollbackResult.Message}");
                foreach (var err in rollbackResult.Errors)
                {
                    LogMessage($"       * {err}");
                }
                MessageBox.Show(rollbackResult.Message, "DashTweaks - Error en Restauración", MessageBoxButton.OK, MessageBoxImage.Error);
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
        BtnCreateBackup.IsEnabled = !isBusy;
        BtnRestoreBackup.IsEnabled = !isBusy;
        TxtStatus.Text = statusText;
    }

    private void LogMessage(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        TxtLog.AppendText($"[{timestamp}] {message}\n");
        LogScrollViewer.ScrollToEnd();
    }
}
